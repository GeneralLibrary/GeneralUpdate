using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Strategy;

namespace GeneralUpdate.Firmware
{
    /// <summary>
    /// The primary entry point for firmware update operations.
    /// Developers use this class to configure and execute firmware updates across platforms.
    /// 
    /// <para>Usage example:</para>
    /// <code>
    /// var result = await FirmwareBootstrap.Create(config =>
    /// {
    ///     config.FirmwareUrl = "https://example.com/firmware.bin";
    ///     config.DevicePath = "/dev/mmcblk0";
    /// })
    /// .UseDefaultStrategy()
    /// .ExecuteAsync();
    /// </code>
    /// </summary>
    public class FirmwareBootstrap
    {
        private FirmwareConfig _config;
        private IFirmwareStrategy _strategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="FirmwareBootstrap"/> class.
        /// Use the static <see cref="Create"/> method instead of calling the constructor directly.
        /// </summary>
        /// <param name="config">The firmware update configuration.</param>
        private FirmwareBootstrap(FirmwareConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Creates a new <see cref="FirmwareBootstrap"/> instance with the provided configuration.
        /// This is the recommended way to start a firmware update flow.
        /// </summary>
        /// <param name="configure">A delegate to populate the <see cref="FirmwareConfig"/>.</param>
        /// <returns>A configured <see cref="FirmwareBootstrap"/> instance.</returns>
        public static FirmwareBootstrap Create(Action<FirmwareConfig> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var config = new FirmwareConfig();
            configure(config);

            if (!config.Validate())
            {
                throw new InvalidOperationException(
                    "FirmwareConfig validation failed. Ensure FirmwareUrl (or LocalFilePath) and DevicePath are set, and TimeoutSeconds > 0.");
            }

            return new FirmwareBootstrap(config);
        }

        /// <summary>
        /// Uses the default platform strategy based on auto-detection of the current OS.
        /// On Linux, this selects the vela-based strategy.
        /// On Windows, this selects the OS firmware command strategy.
        /// </summary>
        /// <returns>This instance for fluent chaining.</returns>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown when the current platform is not supported.
        /// </exception>
        public FirmwareBootstrap UseDefaultStrategy()
        {
            // Use explicit platform override if set
            if (_config.Platform.HasValue)
            {
                _strategy = ResolveStrategy(_config.Platform.Value);
                return this;
            }

            // Auto-detect platform
            var platform = DetectPlatform();
            _strategy = ResolveStrategy(platform);
            return this;
        }

        /// <summary>
        /// Uses an explicitly specified platform strategy.
        /// </summary>
        /// <param name="platform">The target platform.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public FirmwareBootstrap UsePlatform(FirmwarePlatform platform)
        {
            _strategy = ResolveStrategy(platform);
            return this;
        }

        /// <summary>
        /// Uses a custom <see cref="IFirmwareStrategy"/> implementation.
        /// Useful for testing or when extending with custom platform support.
        /// </summary>
        /// <param name="strategy">A custom strategy implementation.</param>
        /// <returns>This instance for fluent chaining.</returns>
        public FirmwareBootstrap UseStrategy(IFirmwareStrategy strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            return this;
        }

        /// <summary>
        /// Executes the firmware update operation asynchronously.
        /// This performs validation, backup (if enabled), download, and flashing in sequence.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A result indicating success or failure of the entire operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no strategy has been configured.
        /// </exception>
        public async Task<FirmwareUpdateResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (_strategy == null)
            {
                throw new InvalidOperationException(
                    "No strategy has been configured. Call UseDefaultStrategy(), UsePlatform(), or UseStrategy() before ExecuteAsync().");
            }

            var overallSw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Step 1: Validate device readiness
                bool isReady = await _strategy.ValidateDeviceAsync(_config, cancellationToken)
                    .ConfigureAwait(false);

                if (!isReady)
                {
                    return FirmwareUpdateResult.Fail(
                        "Device validation failed. The target device is not ready for firmware update.",
                        "FW_DEVICE_NOT_READY");
                }

                // Step 2: Create backup (if enabled)
                if (_config.BackupEnabled)
                {
                    bool backupOk = await _strategy.BackupCurrentFirmwareAsync(_config, cancellationToken)
                        .ConfigureAwait(false);

                    if (!backupOk)
                    {
                        return FirmwareUpdateResult.Fail(
                            "Firmware backup failed. Update aborted for safety.",
                            "FW_BACKUP_FAILED");
                    }
                }

                // Step 3: Download firmware (if URL is provided — downloader will be implemented in Issue 3)
                string localPath = _config.LocalFilePath;
                if (!string.IsNullOrWhiteSpace(_config.FirmwareUrl))
                {
                    // Placeholder: download will be implemented in the OTA layer
                    // For now, if LocalFilePath is not set, this will fail
                    if (string.IsNullOrWhiteSpace(localPath))
                    {
                        return FirmwareUpdateResult.Fail(
                            "Firmware download is not yet implemented. Provide a LocalFilePath for now.",
                            "FW_DOWNLOAD_NOT_IMPLEMENTED");
                    }
                }

                // Step 4: Apply firmware to device
                FirmwareUpdateResult result = await _strategy.ApplyFirmwareAsync(localPath, _config, cancellationToken)
                    .ConfigureAwait(false);

                overallSw.Stop();
                result.Duration = overallSw.Elapsed;

                return result;
            }
            catch (OperationCanceledException)
            {
                overallSw.Stop();
                return FirmwareUpdateResult.Fail(
                    "Firmware update was cancelled.",
                    "FW_CANCELLED",
                    duration: overallSw.Elapsed);
            }
            catch (Exception ex)
            {
                overallSw.Stop();
                return FirmwareUpdateResult.Fail(
                    string.Format("An unexpected error occurred: {0}", ex.Message),
                    "FW_UNEXPECTED_ERROR",
                    ex,
                    overallSw.Elapsed);
            }
        }

        /// <summary>
        /// Auto-detects the current runtime platform.
        /// </summary>
        /// <returns>The detected firmware platform.</returns>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown when the current OS is not supported.
        /// </exception>
        internal static FirmwarePlatform DetectPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return FirmwarePlatform.Linux;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return FirmwarePlatform.Windows;
            }

            throw new PlatformNotSupportedException(
                string.Format("The current platform '{0}' is not supported by GeneralUpdate.Firmware. Supported platforms: Linux, Windows.",
                    RuntimeInformation.OSDescription));
        }

        /// <summary>
        /// Resolves a platform enum to a concrete strategy instance.
        /// </summary>
        /// <param name="platform">The target firmware platform.</param>
        /// <returns>An instance of <see cref="IFirmwareStrategy"/> for the platform.</returns>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown when the platform strategy is not yet implemented.
        /// </exception>
        internal static IFirmwareStrategy ResolveStrategy(FirmwarePlatform platform)
        {
            switch (platform)
            {
                case FirmwarePlatform.Linux:
                    // Placeholder: will be replaced with LinuxFirmwareStrategy in Issue 4
                    throw new PlatformNotSupportedException(
                        "Linux firmware strategy is not yet implemented. It will use vela FlashPack for dual A/B slot updates.");

                case FirmwarePlatform.Windows:
                    // Placeholder: will be replaced with WindowsFirmwareStrategy in Issue 5
                    throw new PlatformNotSupportedException(
                        "Windows firmware strategy is not yet implemented. It will use OS firmware commands (WMI/DeviceIoControl).");

                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown firmware platform.");
            }
        }
    }
}
