using System;
using GeneralUpdate.Firmware.Strategy;

namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// Aggregated configuration object for firmware update operations.
    /// All strategies, OTA parameters, and behavioral options are centralized here.
    /// Pass an instance of this class to the <see cref="GeneralFirmwareBootstrap"/> builder.
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
        /// Gets or sets the expected firmware file format.
        /// Set to <see cref="FirmwareFormat.Auto"/> (default) for automatic detection
        /// based on file extension and magic bytes.
        /// </summary>
        public FirmwareFormat Format { get; set; } = FirmwareFormat.Auto;

        /// <summary>
        /// Gets or sets a custom firmware format decoder instance.
        /// When set, this overrides both auto-detection and the <see cref="Format"/> property.
        /// Use this for proprietary or non-standard firmware formats.
        /// </summary>
        public OTA.Decoders.IFirmwareDecoder CustomDecoder { get; set; }

        /// <summary>
        /// Gets or sets pre-decoded raw firmware data.
        /// When set, the entire format decoding step (Step 3) is skipped
        /// and this byte array is written directly to the device.
        /// </summary>
        public byte[] PreDecodedData { get; set; }

        /// <summary>
        /// Gets or sets the expected SHA256 hash of the firmware for integrity validation.
        /// Leave null or empty to skip validation.
        /// </summary>
        public string ExpectedSha256 { get; set; }

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
        /// Gets or sets whether to verify the written firmware by reading it back
        /// from the device and comparing with the source file.
        /// Default value is true (safe-by-default).
        /// Verification uses chunked comparison to keep memory usage low.
        /// </summary>
        public bool EnableWriteVerify { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to automatically restore the backup if flashing fails.
        /// When enabled and a backup exists, the original firmware is written back
        /// to the device automatically on flash failure.
        /// Default value is true (safe-by-default).
        /// Requires <see cref="BackupEnabled"/> to be true for the backup to exist.
        /// </summary>
        public bool EnableAutoRollback { get; set; } = true;

        /// <summary>
        /// Set internally by <see cref="Strategy.IFirmwareStrategy.BackupCurrentFirmwareAsync"/>
        /// after a successful backup. Used for automatic rollback if flashing fails.
        /// </summary>
        internal string LastBackupPath { get; set; }

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
        [Obsolete("Use OnDownloadProgress to also get download speed and estimated time remaining. Both callbacks fire together.")]
        public Action<long, long> ProgressCallback { get; set; }

        // ===== New notification callbacks =====

        /// <summary>
        /// Gets or sets a callback invoked when the firmware update enters a new stage.
        /// Parameters: (<see cref="FirmwareUpdateStage"/> stage, string description).
        /// Example stages: ValidatingDevice, BackingUp, Downloading, Flashing, Completed, Failed.
        /// </summary>
        public Action<FirmwareUpdateStage, string> OnStageChanged { get; set; }

        /// <summary>
        /// Gets or sets a callback invoked periodically (approx. every 500 ms) with rich progress information.
        /// Parameter: <see cref="FirmwareProgressInfo"/> containing stage, percentages, speed, ETA, etc.
        /// Suitable for driving a progress bar or status display.
        /// </summary>
        public Action<FirmwareProgressInfo> OnProgress { get; set; }

        /// <summary>
        /// Gets or sets a callback invoked during the download stage with speed and ETA.
        /// Parameters: (long bytesReceived, long totalBytes, double speedBytesPerSecond, TimeSpan estimatedRemaining).
        /// Fires roughly every 500 ms during the download.
        /// </summary>
        public Action<long, long, double, TimeSpan> OnDownloadProgress { get; set; }

        /// <summary>
        /// Gets or sets a callback invoked when the firmware update completes successfully.
        /// Parameter: <see cref="FirmwareUpdateResult"/> with the applied version, duration, etc.
        /// </summary>
        public Action<FirmwareUpdateResult> OnCompleted { get; set; }

        /// <summary>
        /// Gets or sets a callback invoked when the firmware update fails.
        /// Parameter: <see cref="FirmwareUpdateResult"/> with the error code, message, and exception details.
        /// </summary>
        public Action<FirmwareUpdateResult> OnFailed { get; set; }

        /// <summary>
        /// Gets or sets a callback invoked for non-fatal warnings during the update.
        /// Parameters: (string message, string warningCode).
        /// Examples: backup skipped by configuration, retry attempts, etc.
        /// </summary>
        public Action<string, string> OnWarning { get; set; }

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

            if (TimeoutSeconds <= 0)
            {
                return false;
            }

            return true;
        }

    }
}
