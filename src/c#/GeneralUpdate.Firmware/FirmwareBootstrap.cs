using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Strategy;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware
{
    /// <summary>
    /// The primary entry point for firmware update operations.
    /// Developers use this class to configure and execute firmware updates across platforms.
    /// 
    /// <para>Usage example:</para>
    /// <code>
    /// // Initialize trace logging (call once at application startup)
    /// FirmwareTrace.Initialize();
    /// 
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
            FirmwareTrace.Info("FirmwareBootstrap instance created with config: {0}", config);
        }

        /// <summary>
        /// Creates a new <see cref="FirmwareBootstrap"/> instance with the provided configuration.
        /// This is the recommended way to start a firmware update flow.
        /// </summary>
        /// <param name="configure">A delegate to populate the <see cref="FirmwareConfig"/>.</param>
        /// <returns>A configured <see cref="FirmwareBootstrap"/> instance.</returns>
        public static FirmwareBootstrap Create(Action<FirmwareConfig> configure)
        {
            FirmwareTrace.Info("FirmwareBootstrap.Create called");

            if (configure == null)
            {
                FirmwareTrace.Error("FirmwareBootstrap.Create failed: configure delegate is null");
                throw new ArgumentNullException(nameof(configure));
            }

            var config = new FirmwareConfig();
            configure(config);

            if (!config.Validate())
            {
                var error = "FirmwareConfig validation failed. Ensure FirmwareUrl (or LocalFilePath) and DevicePath are set, and TimeoutSeconds > 0.";
                FirmwareTrace.Error(error);
                throw new InvalidOperationException(error);
            }

            FirmwareTrace.Info("FirmwareConfig validated successfully");
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
            FirmwareTrace.Info("Selecting default strategy (auto-detect platform)");

            // Use explicit platform override if set
            if (_config.Platform.HasValue)
            {
                FirmwareTrace.Info("Using explicit platform override: {0}", _config.Platform.Value);
                _strategy = ResolveStrategy(_config.Platform.Value);
                return this;
            }

            // Auto-detect platform
            var platform = DetectPlatform();
            FirmwareTrace.Info("Auto-detected platform: {0} (OS: {1})", platform, RuntimeInformation.OSDescription);
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
            FirmwareTrace.Info("Using specified platform strategy: {0}", platform);
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
            if (strategy == null)
            {
                FirmwareTrace.Error("UseStrategy called with null strategy");
                throw new ArgumentNullException(nameof(strategy));
            }

            FirmwareTrace.Info("Using custom strategy: {0} (Platform: {1})",
                strategy.GetType().Name,
                strategy.TargetPlatform);
            _strategy = strategy;
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
                var error = "No strategy has been configured. Call UseDefaultStrategy(), UsePlatform(), or UseStrategy() before ExecuteAsync().";
                FirmwareTrace.Error(error);
                throw new InvalidOperationException(error);
            }

            FirmwareTrace.BeginOperation("FirmwareUpdate");

            var overallSw = Stopwatch.StartNew();
            var totalStopwatch = Stopwatch.StartNew();

            try
            {
                // ========================================================
                // Step 1: Validate device readiness
                // ========================================================
                FirmwareTrace.BeginOperation("DeviceValidation");
                var validationSw = Stopwatch.StartNew();

                bool isReady = await _strategy.ValidateDeviceAsync(_config, cancellationToken)
                    .ConfigureAwait(false);

                validationSw.Stop();
                FirmwareTrace.EndOperation("DeviceValidation", validationSw.Elapsed, isReady);

                if (!isReady)
                {
                    return FirmwareUpdateResult.Fail(
                        "Device validation failed. The target device is not ready for firmware update.",
                        "FW_DEVICE_NOT_READY");
                }

                // ========================================================
                // Step 2: Create backup (if enabled)
                // ========================================================
                if (_config.BackupEnabled)
                {
                    FirmwareTrace.BeginOperation("FirmwareBackup");
                    var backupSw = Stopwatch.StartNew();

                    bool backupOk = await _strategy.BackupCurrentFirmwareAsync(_config, cancellationToken)
                        .ConfigureAwait(false);

                    backupSw.Stop();
                    FirmwareTrace.EndOperation("FirmwareBackup", backupSw.Elapsed, backupOk);

                    if (!backupOk)
                    {
                        return FirmwareUpdateResult.Fail(
                            "Firmware backup failed. Update aborted for safety.",
                            "FW_BACKUP_FAILED");
                    }
                }
                else
                {
                    FirmwareTrace.Warn("Firmware backup is disabled. Proceeding without safety net.");
                }

                // ========================================================
                // Step 3: Download firmware (if URL is provided)
                // ========================================================
                string localPath = _config.LocalFilePath;
                if (!string.IsNullOrWhiteSpace(_config.FirmwareUrl))
                {
                    FirmwareTrace.Info("Firmware URL provided: {0}", _config.FirmwareUrl);

                    if (string.IsNullOrWhiteSpace(localPath))
                    {
                        var downloadError = "Firmware download is not yet implemented. Provide a LocalFilePath for now.";
                        FirmwareTrace.Error(downloadError);
                        return FirmwareUpdateResult.Fail(
                            downloadError,
                            "FW_DOWNLOAD_NOT_IMPLEMENTED");
                    }

                    // Placeholder: download will be implemented in the OTA layer (Issue 3)
                    FirmwareTrace.Info("Local firmware file path: {0}", localPath);
                }
                else
                {
                    FirmwareTrace.Info("Using local firmware file: {0}", localPath ?? "(not set)");
                }

                // ========================================================
                // Step 4: Apply firmware to device
                // ========================================================
                FirmwareTrace.BeginOperation("ApplyFirmware");
                var applySw = Stopwatch.StartNew();

                FirmwareTrace.Info("Applying firmware to device: {0} | File: {1}",
                    _config.DevicePath,
                    localPath);
                FirmwareTrace.Info("Strategy: {0} (Platform: {1})",
                    _strategy.GetType().Name,
                    _strategy.TargetPlatform);

                FirmwareUpdateResult result = await _strategy.ApplyFirmwareAsync(localPath, _config, cancellationToken)
                    .ConfigureAwait(false);

                applySw.Stop();
                FirmwareTrace.EndOperation("ApplyFirmware", applySw.Elapsed, result.Success);

                overallSw.Stop();
                result.Duration = overallSw.Elapsed;

                FirmwareTrace.EndOperation("FirmwareUpdate", overallSw.Elapsed, result.Success);
                FirmwareTrace.Info("Total firmware update time: {0:F3}s", overallSw.Elapsed.TotalSeconds);

                return result;
            }
            catch (OperationCanceledException)
            {
                overallSw.Stop();
                FirmwareTrace.Warn("Firmware update was cancelled after {0:F3}s", overallSw.Elapsed.TotalSeconds);
                FirmwareTrace.EndOperation("FirmwareUpdate", overallSw.Elapsed, false);

                return FirmwareUpdateResult.Fail(
                    "Firmware update was cancelled.",
                    "FW_CANCELLED",
                    duration: overallSw.Elapsed);
            }
            catch (Exception ex)
            {
                overallSw.Stop();
                FirmwareTrace.Error("Firmware update failed with unexpected error", ex);
                FirmwareTrace.EndOperation("FirmwareUpdate", overallSw.Elapsed, false);

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
            FirmwareTrace.Debug("Detecting current runtime platform...");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                FirmwareTrace.Debug("Platform detected: Linux");
                return FirmwarePlatform.Linux;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FirmwareTrace.Debug("Platform detected: Windows");
                return FirmwarePlatform.Windows;
            }

            var error = string.Format(
                "The current platform '{0}' is not supported by GeneralUpdate.Firmware. Supported platforms: Linux, Windows.",
                RuntimeInformation.OSDescription);
            FirmwareTrace.Error(error);
            throw new PlatformNotSupportedException(error);
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
            FirmwareTrace.Debug("Resolving strategy for platform: {0}", platform);

            switch (platform)
            {
                case FirmwarePlatform.Linux:
                    // Placeholder: will be replaced with LinuxFirmwareStrategy in Issue 4
                    FirmwareTrace.Warn("Linux firmware strategy is not yet implemented (planned for Issue 4)");
                    throw new PlatformNotSupportedException(
                        "Linux firmware strategy is not yet implemented. It will use vela FlashPack for dual A/B slot updates.");

                case FirmwarePlatform.Windows:
                    // Placeholder: will be replaced with WindowsFirmwareStrategy in Issue 5
                    FirmwareTrace.Warn("Windows firmware strategy is not yet implemented (planned for Issue 5)");
                    throw new PlatformNotSupportedException(
                        "Windows firmware strategy is not yet implemented. It will use OS firmware commands (WMI/DeviceIoControl).");

                default:
                    FirmwareTrace.Error("Unknown firmware platform: {0}", platform);
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown firmware platform.");
            }
        }
    }
}
