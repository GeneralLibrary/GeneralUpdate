using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;

namespace GeneralUpdate.Firmware.Strategy
{
    /// <summary>
    /// Defines the contract for platform-specific firmware update strategies.
    /// Each platform (Linux, Windows) provides its own implementation.
    /// The strategy is only responsible for applying the firmware file to the target device;
    /// downloading and validation are handled separately by the OTA layer.
    /// </summary>
    public interface IFirmwareStrategy
    {
        /// <summary>
        /// Applies the firmware file at the given local path to the target device.
        /// This is the core operation: write the firmware binary to the device,
        /// handle pre/post flashing steps, and verify the write.
        /// </summary>
        /// <param name="firmwareFilePath">Absolute path to the firmware file on disk.</param>
        /// <param name="config">The firmware update configuration.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A result indicating success or failure of the flashing operation.</returns>
        Task<FirmwareUpdateResult> ApplyFirmwareAsync(
            string firmwareFilePath,
            FirmwareConfig config,
            CancellationToken cancellationToken);

        /// <summary>
        /// Validates whether the target device is ready and compatible for a firmware update.
        /// Implementations should check device presence, permissions, and compatibility.
        /// </summary>
        /// <param name="config">The firmware update configuration.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the device is ready for firmware update; false otherwise.</returns>
        Task<bool> ValidateDeviceAsync(FirmwareConfig config, CancellationToken cancellationToken);

        /// <summary>
        /// Backs up the current firmware from the device to the specified backup directory.
        /// This enables rollback in case the update fails or produces unexpected results.
        /// </summary>
        /// <param name="config">The firmware update configuration containing backup settings.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if the backup was created successfully; false otherwise.</returns>
        Task<bool> BackupCurrentFirmwareAsync(FirmwareConfig config, CancellationToken cancellationToken);
    }
}
