using System;

namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// Represents the result of a firmware update operation.
    /// Contains status, message, and diagnostic information for the caller.
    /// </summary>
    public class FirmwareUpdateResult
    {
        /// <summary>
        /// Gets or sets whether the firmware update completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a human-readable message describing the result
        /// (success confirmation or failure reason).
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the firmware version that was applied, or null if the update failed.
        /// </summary>
        public string AppliedVersion { get; set; }

        /// <summary>
        /// Gets or sets the total duration of the update operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the error code, if any (e.g., "FW_DOWNLOAD_FAILED").
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets an exception object if the update failed due to an error.
        /// </summary>
        public Exception Error { get; set; }

        /// <summary>
        /// Creates a success result with the applied firmware version.
        /// </summary>
        /// <param name="version">The firmware version applied.</param>
        /// <param name="duration">The duration of the operation.</param>
        /// <returns>A successful FirmwareUpdateResult.</returns>
        public static FirmwareUpdateResult Succeed(string version, TimeSpan duration)
        {
            return new FirmwareUpdateResult
            {
                Success = true,
                AppliedVersion = version,
                Duration = duration,
                Message = string.Format("Firmware updated successfully to version {0}.", version)
            };
        }

        /// <summary>
        /// Creates a failure result with a message and optional error details.
        /// </summary>
        /// <param name="message">Human-readable failure message.</param>
        /// <param name="errorCode">Error code for diagnostics.</param>
        /// <param name="error">The underlying exception, if any.</param>
        /// <param name="duration">The duration of the attempt.</param>
        /// <returns>A failed FirmwareUpdateResult.</returns>
        public static FirmwareUpdateResult Fail(string message, string errorCode = null, Exception error = null, TimeSpan duration = default)
        {
            return new FirmwareUpdateResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Error = error,
                Duration = duration
            };
        }

        /// <summary>
        /// Returns a string representation of the update result.
        /// </summary>
        public override string ToString()
        {
            string status = Success ? "SUCCESS" : "FAILED";
            return string.Format(
                "FirmwareUpdateResult[{0}] Version={1}, Message={2}, Duration={3}",
                status,
                AppliedVersion ?? "(none)",
                Message ?? "(none)",
                Duration);
        }
    }
}
