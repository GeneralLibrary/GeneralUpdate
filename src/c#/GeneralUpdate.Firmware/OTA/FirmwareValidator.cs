using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.OTA
{
    /// <summary>
    /// Validates firmware file integrity using SHA256 hash comparison.
    /// Ensures the downloaded firmware binary matches the expected hash
    /// before it is applied to the target device.
    /// 
    /// <para>
    /// Validation is skipped when <see cref="FirmwareConfig.ExpectedSha256"/>
    /// is null or empty.
    /// </para>
    /// </summary>
    public class FirmwareValidator
    {
        private readonly FirmwareConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="FirmwareValidator"/> class.
        /// </summary>
        /// <param name="config">The firmware update configuration.</param>
        public FirmwareValidator(FirmwareConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Validates the firmware file at the given path against the expected hash.
        /// If no expected hash is configured, validation is skipped and returns true.
        /// </summary>
        /// <param name="firmwareFilePath">The full path to the firmware file to validate.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// True if the firmware is valid (hash matches or validation is skipped);
        /// false if the hash does not match.
        /// </returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the firmware file does not exist.
        /// </exception>
        public Task<bool> ValidateAsync(string firmwareFilePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Validate(firmwareFilePath));
        }

        /// <summary>
        /// Synchronously computes the SHA256 hash of the firmware file and compares
        /// it against the expected value.
        /// </summary>
        /// <param name="firmwareFilePath">The full path to the firmware file.</param>
        /// <returns>True if the hash matches or validation is skipped; false otherwise.</returns>
        private bool Validate(string firmwareFilePath)
        {
            if (string.IsNullOrWhiteSpace(_config.ExpectedSha256))
            {
                FirmwareTrace.Warn("No expected SHA256 hash configured — skipping firmware validation.");
                return true;
            }

            FirmwareTrace.BeginOperation("FirmwareValidation");
            FirmwareTrace.Info("Validating firmware file: {0}", firmwareFilePath);

            if (!File.Exists(firmwareFilePath))
            {
                FirmwareTrace.Error("Firmware file not found at path: {0}", firmwareFilePath);
                throw new FileNotFoundException(
                    string.Format(CultureInfo.InvariantCulture, "Firmware file not found: {0}", firmwareFilePath),
                    firmwareFilePath);
            }

            try
            {
                string computedHash;

                using (var stream = new FileStream(firmwareFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    computedHash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
                }

                bool isValid = string.Equals(
                    computedHash,
                    _config.ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase);

                if (isValid)
                {
                    FirmwareTrace.Info("Firmware validation PASSED. Hash: {0}", computedHash);
                    FirmwareTrace.EndOperation("FirmwareValidation", TimeSpan.Zero, true);
                }
                else
                {
                    FirmwareTrace.Error(
                        "Firmware validation FAILED. Expected: {0}, Actual: {1}",
                        _config.ExpectedSha256,
                        computedHash);
                    FirmwareTrace.EndOperation("FirmwareValidation", TimeSpan.Zero, false);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Firmware validation encountered an error", ex);
                FirmwareTrace.EndOperation("FirmwareValidation", TimeSpan.Zero, false);
                return false;
            }
        }
    }
}
