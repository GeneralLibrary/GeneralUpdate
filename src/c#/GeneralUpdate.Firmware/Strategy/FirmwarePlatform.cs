namespace GeneralUpdate.Firmware.Strategy
{
    /// <summary>
    /// Enumerates the supported platforms for firmware update strategies.
    /// Used by <see cref="GeneralFirmwareBootstrap"/> to resolve the appropriate strategy at runtime.
    /// </summary>
    public enum FirmwarePlatform
    {
        /// <summary>
        /// Linux-based platforms (embedded Linux, Yocto, Buildroot, etc.).
        /// Strategy uses vela FlashPack for dual A/B slot firmware updates.
        /// </summary>
        Linux,

        /// <summary>
        /// Windows-based platforms (Windows 10/11, Windows IoT Enterprise).
        /// Strategy calls Windows OS firmware commands (WMI, DeviceIoControl).
        /// </summary>
        Windows
    }
}
