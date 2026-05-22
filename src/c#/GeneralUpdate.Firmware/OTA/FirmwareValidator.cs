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
    /// is null or empty. Progress is reported via <see cref="FirmwareConfig.OnProgress"/>
    /// if configured.
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
        /// Fires <see cref="FirmwareConfig.OnProgress"/> during hash computation.
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
        public async Task<bool> ValidateAsync(string firmwareFilePath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Validate(firmwareFilePath), cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronously computes the SHA256 hash of the firmware file and compares
        /// it against the expected value. Reports progress via
        /// <see cref="FirmwareConfig.OnProgress"/>.
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
                // --- Fire progress: start ---
                FireValidationProgress(0, firmwareFilePath);

                string computedHash;
                long fileSize = new FileInfo(firmwareFilePath).Length;

                using (var stream = new FileStream(firmwareFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024))
                using (var sha256 = SHA256.Create())
                {
                    // Compute hash with progress reporting for large files
                    byte[] buffer = new byte[1024 * 1024]; // 1 MB chunks
                    long totalRead = 0;
                    int bytesRead;
                    var chunkSw = System.Diagnostics.Stopwatch.StartNew();
                    long lastReportBytes = 0;
                    long lastReportMs = 0;

                    // Use TransformBlock to incrementally compute hash with progress
                    sha256.Initialize();
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                        totalRead += bytesRead;

                        long elapsedMs = chunkSw.ElapsedMilliseconds;
                        if (elapsedMs - lastReportMs >= 500 || totalRead == fileSize)
                        {
                            float stagePct = fileSize > 0 ? (float)totalRead / fileSize * 100f : 0f;
                            FireValidationProgress(stagePct, firmwareFilePath);
                            lastReportBytes = totalRead;
                            lastReportMs = elapsedMs;
                        }
                    }
                    sha256.TransformFinalBlock(buffer, 0, 0);
                    computedHash = BitConverter.ToString(sha256.Hash).Replace("-", string.Empty);
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

                // --- Fire progress: complete ---
                FireValidationProgress(isValid ? 100 : 0, firmwareFilePath);

                return isValid;
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Firmware validation encountered an error", ex);
                FirmwareTrace.EndOperation("FirmwareValidation", TimeSpan.Zero, false);
                return false;
            }
        }

        /// <summary>
        /// Fires <see cref="FirmwareConfig.OnProgress"/> with the current validation progress.
        /// </summary>
        private void FireValidationProgress(float stagePct, string firmwareFilePath)
        {
            var callback = _config.OnProgress;
            if (callback == null) return;

            try
            {
                callback(new FirmwareProgressInfo
                {
                    Stage = FirmwareUpdateStage.ValidatingFirmware,
                    StageProgressPercent = stagePct,
                    OverallProgressPercent = 70f + (stagePct * 0.10f),
                    StatusText = string.Format(
                        CultureInfo.InvariantCulture,
                        "Validating firmware integrity... {0:F0}%",
                        stagePct)
                });
            }
            catch (Exception ex)
            {
                FirmwareTrace.Warn("Validation progress callback threw an exception (ignored): {0}", ex.Message);
            }
        }
    }
}
