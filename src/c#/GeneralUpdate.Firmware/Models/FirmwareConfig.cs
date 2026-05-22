using System;
using GeneralUpdate.Firmware.Strategy;

namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// Aggregated configuration object for firmware update operations.
    /// All strategies, OTA parameters, and behavioral options are centralized here.
    /// Pass an instance of this class to the <see cref="FirmwareBootstrap"/> builder.
    /// </summary>
    public class FirmwareConfig
    {
        /// <summary>
        /// Gets or sets the remote URL from which to download the firmware binary.
        /// Required for OTA updates; optional if the firmware file is already local.
        /// </summary>
        public string FirmwareUrl { get; set; }

        /// <summary>
        /// Gets or sets the local file path where the downloaded firmware will be saved.
        /// Defaults to a temporary path if not specified.
        /// </summary>
        public string LocalFilePath { get; set; }

        /// <summary>
        /// Gets or sets the device path or identifier to which the firmware should be written
        /// (e.g., "/dev/mmcblk0" on Linux, "\\.\PhysicalDrive0" on Windows).
        /// </summary>
        public string DevicePath { get; set; }

        /// <summary>
        /// Gets or sets the expected SHA256 hash of the firmware for integrity validation.
        /// Leave null or empty to skip validation.
        /// </summary>
        public string ExpectedSha256 { get; set; }

        /// <summary>
        /// Gets or sets the platform override. When null, the platform is auto-detected.
        /// Set explicitly for testing or cross-compilation scenarios.
        /// </summary>
        public FirmwarePlatform? Platform { get; set; }

        /// <summary>
        /// Gets or sets the timeout in seconds for the entire update operation.
        /// Default value is 600 seconds (10 minutes).
        /// </summary>
        public int TimeoutSeconds { get; set; } = 600;

        /// <summary>
        /// Gets or sets the number of retry attempts for transient failures.
        /// Default value is 3.
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay in seconds between retry attempts.
        /// Default value is 5 seconds.
        /// </summary>
        public int RetryDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets whether to create a backup of the current firmware before updating.
        /// Default value is true (safe-by-default).
        /// </summary>
        public bool BackupEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the directory path where backups are stored.
        /// Defaults to the current directory if not specified.
        /// </summary>
        public string BackupDirectory { get; set; }

        /// <summary>
        /// Gets or sets whether to require user confirmation before proceeding with the update.
        /// Default value is false (headless/automated by default).
        /// </summary>
        public bool RequireConfirmation { get; set; } = false;

        /// <summary>
        /// Gets or sets a user-provided callback for progress reporting during download.
        /// The first argument is bytes received, second is total bytes.
        /// Set to null to disable progress callbacks.
        /// </summary>
        public Action<long, long> ProgressCallback { get; set; }

        /// <summary>
        /// Validates that the configuration contains all required fields for an update operation.
        /// </summary>
        /// <returns>True if the configuration is valid; false otherwise.</returns>
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(FirmwareUrl) && string.IsNullOrWhiteSpace(LocalFilePath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(DevicePath))
            {
                return false;
            }

            if (TimeoutSeconds <= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a string representation of the configuration (excluding sensitive fields).
        /// </summary>
        public override string ToString()
        {
            return string.Format(
                "FirmwareConfig[Url={0}, Device={1}, Platform={2}, Timeout={3}s, Retry={4}, Backup={5}]",
                FirmwareUrl ?? "(local file)",
                DevicePath ?? "(not set)",
                Platform?.ToString() ?? "auto-detect",
                TimeoutSeconds,
                RetryCount,
                BackupEnabled);
        }
    }
}
