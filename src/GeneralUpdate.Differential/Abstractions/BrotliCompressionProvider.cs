#if NET6_0_OR_GREATER
using System.IO;
using System.Threading;
using GeneralUpdate.Differential.Abstractions;

namespace GeneralUpdate.Differential.Abstractions
{
    /// <summary>
    /// Brotli compression provider using <see cref="System.IO.Compression.BrotliStream"/> (BCL, .NET 6+).
    /// Format version byte: 0x02.
    /// </summary>
    /// <remarks>
    /// Compared to BZip2:
    /// - Decompression is 3-5x faster (critical for client-side patch application).
    /// - Compression ratio is comparable or slightly better for binary data.
    /// - Only available on .NET 6+ targets.
    ///
    /// For netstandard2.0 targets, use <see cref="DeflateCompressionProvider"/> instead (format 0x01).
    /// </remarks>
    public sealed class BrotliCompressionProvider : ICompressionProvider
    {
        private readonly System.IO.Compression.CompressionLevel _compressionLevel;

        /// <summary>
        /// Initialises a new Brotli compression provider with the specified quality.
        /// </summary>
        /// <param name="optimalLevel">
        /// If true, uses CompressionLevel.Optimal (best compression, slower).
        /// If false, uses CompressionLevel.Fastest (faster compression, slightly larger output).
        /// Default: true (optimal).
        /// </param>
        public BrotliCompressionProvider(bool optimalLevel = true)
        {
            _compressionLevel = optimalLevel
                ? System.IO.Compression.CompressionLevel.Optimal
                : System.IO.Compression.CompressionLevel.Fastest;
        }

        /// <inheritdoc/>
        public byte FormatVersion => 0x02;

        /// <inheritdoc/>
        public Stream CreateCompressStream(Stream output, CancellationToken cancellationToken = default)
        {
            return new System.IO.Compression.BrotliStream(output, _compressionLevel, leaveOpen: true);
        }

        /// <inheritdoc/>
        public Stream CreateDecompressStream(Stream input, CancellationToken cancellationToken = default)
        {
            return new System.IO.Compression.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
        }
    }
}
#endif
