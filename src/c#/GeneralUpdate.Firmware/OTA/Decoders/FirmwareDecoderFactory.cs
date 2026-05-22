using System;
using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Firmware.Models;

namespace GeneralUpdate.Firmware.OTA.Decoders
{
    /// <summary>
    /// Global registry for firmware format decoders.
    /// Developers can register custom decoders for proprietary firmware formats
    /// without modifying the framework source code.
    /// 
    /// <para>Usage:</para>
    /// <code>
    /// FirmwareDecoderFactory.Register(".myfmt", () => new MyCustomDecoder());
    /// FirmwareDecoderFactory.Register(FirmwareFormat.IntelHex, () => new MyIntelHexDecoder());
    /// </code>
    /// </summary>
    public static class FirmwareDecoderFactory
    {
        private static readonly object SyncLock = new object();
        private static readonly Dictionary<string, Func<IFirmwareDecoder>> ExtensionRegistry
            = new Dictionary<string, Func<IFirmwareDecoder>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<FirmwareFormat, Func<IFirmwareDecoder>> FormatRegistry
            = new Dictionary<FirmwareFormat, Func<IFirmwareDecoder>>();

        /// <summary>
        /// Registers a decoder factory for a custom file extension.
        /// Overwrites any existing registration for the same extension.
        /// </summary>
        /// <param name="extension">
        /// The file extension including the leading dot (e.g., ".myfmt").
        /// </param>
        /// <param name="factory">A factory function that creates a new decoder instance.</param>
        public static void Register(string extension, Func<IFirmwareDecoder> factory)
        {
            if (string.IsNullOrWhiteSpace(extension))
                throw new ArgumentException("Extension cannot be null or empty.", nameof(extension));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            lock (SyncLock)
            {
                ExtensionRegistry[extension] = factory;
            }
        }

        /// <summary>
        /// Registers a decoder factory for a specific <see cref="FirmwareFormat"/>.
        /// This overrides the built-in decoder for the given format.
        /// </summary>
        /// <param name="format">The firmware format to handle.</param>
        /// <param name="factory">A factory function that creates a new decoder instance.</param>
        public static void Register(FirmwareFormat format, Func<IFirmwareDecoder> factory)
        {
            if (format == FirmwareFormat.Auto)
                throw new ArgumentException("Cannot register a decoder for the Auto format.", nameof(format));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            lock (SyncLock)
            {
                FormatRegistry[format] = factory;
            }
        }

        /// <summary>
        /// Removes a previously registered custom decoder for the given extension.
        /// </summary>
        /// <param name="extension">The file extension to unregister.</param>
        /// <returns>True if a registration was removed; false otherwise.</returns>
        public static bool Unregister(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;

            lock (SyncLock)
            {
                return ExtensionRegistry.Remove(extension);
            }
        }

        /// <summary>
        /// Removes a previously registered custom decoder for the given format.
        /// Restores the built-in decoder for that format.
        /// </summary>
        /// <param name="format">The format to unregister.</param>
        /// <returns>True if a registration was removed; false otherwise.</returns>
        public static bool Unregister(FirmwareFormat format)
        {
            lock (SyncLock)
            {
                return FormatRegistry.Remove(format);
            }
        }

        /// <summary>
        /// Clears all custom decoder registrations.
        /// </summary>
        public static void Clear()
        {
            lock (SyncLock)
            {
                ExtensionRegistry.Clear();
                FormatRegistry.Clear();
            }
        }

        /// <summary>
        /// Resolves the appropriate <see cref="IFirmwareDecoder"/> for the given file.
        /// Resolution order:
        /// <list type="number">
        ///   <item><description>If <paramref name="customDecoder"/> is not null, use it.</description></item>
        ///   <item><description>If <paramref name="format"/> is not Auto, look up in registry or use built-in.</description></item>
        ///   <item><description>Detect format by file extension via registry or built-in rules.</description></item>
        /// </list>
        /// </summary>
        /// <param name="filePath">The firmware file path.</param>
        /// <param name="format">The explicit format (may be Auto for detection).</param>
        /// <param name="customDecoder">An optional custom decoder instance (highest priority).</param>
        /// <returns>A firmware decoder instance ready for decoding.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no decoder could be resolved for the given file format.
        /// </exception>
        public static IFirmwareDecoder Resolve(
            string filePath,
            FirmwareFormat format,
            IFirmwareDecoder customDecoder = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            // Priority 1: Explicit custom decoder instance
            if (customDecoder != null)
            {
                return customDecoder;
            }

            string extension = Path.GetExtension(filePath);

            // Priority 2: Explicit format (non-Auto)
            if (format != FirmwareFormat.Auto)
            {
                lock (SyncLock)
                {
                    if (FormatRegistry.TryGetValue(format, out var factory))
                    {
                        return factory();
                    }
                }
                return CreateBuiltIn(format);
            }

            // Priority 3: Auto-detect by file extension
            // Check custom extension registry first
            if (!string.IsNullOrEmpty(extension))
            {
                lock (SyncLock)
                {
                    if (ExtensionRegistry.TryGetValue(extension, out var factory))
                    {
                        return factory();
                    }
                }
            }

            // Priority 4: Built-in auto-detection by extension
            return AutoDetect(filePath, extension);
        }

        /// <summary>
        /// Creates a built-in decoder for the specified format.
        /// </summary>
        internal static IFirmwareDecoder CreateBuiltIn(FirmwareFormat format)
        {
            switch (format)
            {
                case FirmwareFormat.Raw:
                    return new RawDecoder();

                case FirmwareFormat.IntelHex:
                    return new IntelHexDecoder();

                case FirmwareFormat.SRecord:
                    return new SRecordDecoder();

                case FirmwareFormat.AndroidSparse:
                    return new AndroidSparseDecoder();

                case FirmwareFormat.FlashPack:
                    // FlashPack is handled by vela-ffi (P/Invoke), not a decoder
                    throw new InvalidOperationException(
                        "FlashPack format is handled directly by vela-ffi. Use Raw decoder to pass the .fpk path.");

                case FirmwareFormat.UefiCapsule:
                    // UEFI capsule is handled by the Windows strategy
                    throw new InvalidOperationException(
                        "UEFI capsule format is handled by the platform strategy. Use Raw decoder if needed.");

                default:
                    throw new InvalidOperationException(
                        string.Format("No built-in decoder for format: {0}", format));
            }
        }

        /// <summary>
        /// Auto-detects the firmware format by file extension and returns the appropriate decoder.
        /// </summary>
        private static IFirmwareDecoder AutoDetect(string filePath, string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                // No extension — assume raw binary
                return new RawDecoder();
            }

            switch (extension.ToLowerInvariant())
            {
                case ".hex":
                    return new IntelHexDecoder();

                case ".srec":
                case ".s19":
                case ".s28":
                case ".s37":
                    return new SRecordDecoder();

                case ".sparse":
                case ".sparseimg":
                    return new AndroidSparseDecoder();

                case ".bin":
                case ".img":
                case ".rom":
                case ".fw":
                default:
                    // Binary files — raw decoder
                    return new RawDecoder();
            }
        }
    }
}
