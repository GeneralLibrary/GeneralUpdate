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
        /// Gets or sets the hardware connection configuration.
        /// Determines how the firmware is physically transferred to the target device.
        /// Default is <see cref="ConnectionType.BlockDevice"/>.
        /// </summary>
        public DeviceConnection Connection { get; set; } = new DeviceConnection();

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
        /// Gets or sets the device path or identifier to which the firmware should be written
        /// (e.g., "/dev/mmcblk0" on Linux, "\\.\PhysicalDrive0" on Windows).
        /// This is a convenience property that delegates to <see cref="DeviceConnection.DevicePath"/>.
        /// </summary>
        public string DevicePath
        {
            get => Connection?.DevicePath;
            set
            {
                if (Connection == null) Connection = new DeviceConnection();
                Connection.DevicePath = value;
            }
        }

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

            if (Connection == null || !Connection.Validate())
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
                "FirmwareConfig[Url={0}, Connection={1}, Format={2}, Platform={3}, Timeout={4}s]",
                FirmwareUrl ?? "(local file)",
                Connection?.ToString() ?? "(not set)",
                Format,
                Platform?.ToString() ?? "auto-detect",
                TimeoutSeconds);
        }
    }
}
