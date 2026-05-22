using System;

namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// Represents metadata about a firmware package.
    /// This model carries all identifying information needed to download and validate firmware.
    /// </summary>
    public class FirmwareInfo
    {
        /// <summary>
        /// Gets or sets the firmware name (e.g., "STM32-Bootloader").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the firmware version string (e.g., "2.1.0").
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the target hardware model or device identifier.
        /// </summary>
        public string HardwareModel { get; set; }

        /// <summary>
        /// Gets or sets the remote URL from which the firmware binary can be downloaded.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets the expected SHA256 hash of the firmware file for integrity validation.
        /// If null or empty, hash validation is skipped.
        /// </summary>
        public string Sha256Hash { get; set; }

        /// <summary>
        /// Gets or sets the size of the firmware file in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Gets or sets the firmware file format (e.g., "bin", "hex", "dfu", "fpk").
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Gets or sets the release date of this firmware version.
        /// </summary>
        public DateTimeOffset ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets an optional changelog or release notes for this firmware version.
        /// </summary>
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// Returns a string representation of the firmware info.
        /// </summary>
        public override string ToString()
        {
            return string.Format(
                "FirmwareInfo[Name={0}, Version={1}, Hardware={2}, Format={3}, Size={4}]",
                Name ?? "(null)",
                Version ?? "(null)",
                HardwareModel ?? "(null)",
                Format ?? "(null)",
                FileSize);
        }
    }
}
