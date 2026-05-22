namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// Represents the current stage of a firmware update operation.
    /// Used by <see cref="FirmwareProgressInfo"/> and stage-change callbacks
    /// to inform the caller which phase is in progress.
    /// </summary>
    public enum FirmwareUpdateStage
    {
        /// <summary>The update has not started yet.</summary>
        Idle,

        /// <summary>Validating whether the target device is ready and compatible.</summary>
        ValidatingDevice,

        /// <summary>Backing up the current firmware before applying the update.</summary>
        BackingUp,

        /// <summary>Downloading the firmware binary from the remote URL.</summary>
        Downloading,

        /// <summary>Validating firmware integrity (e.g., SHA256 hash check).</summary>
        ValidatingFirmware,

        /// <summary>Writing (flashing) the firmware to the target device.</summary>
        Flashing,

        /// <summary>Verifying that the firmware was written correctly by comparing with source.</summary>
        VerifyingWrite,

        /// <summary>Restoring the original firmware from a backup after a failed update.</summary>
        RollingBack,

        /// <summary>The firmware update completed successfully.</summary>
        Completed,

        /// <summary>The firmware update failed.</summary>
        Failed
    }
}
