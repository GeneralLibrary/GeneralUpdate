namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// Enumerates the hardware connection types for firmware update.
    /// Determines which physical transport channel is used to communicate
    /// with the target device.
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>
        /// Local block device (eMMC, SD card, SATA, NVMe).
        /// Uses FileStream for raw sector read/write.
        /// </summary>
        BlockDevice,

        /// <summary>
        /// Serial port (UART/RS232) with XMODEM/YMODEM protocol.
        /// Common in MCU bootloaders (STM32, ESP32, AVR).
        /// </summary>
        Serial,

        /// <summary>
        /// USB Device Firmware Upgrade (DFU).
        /// Uses libusb on Linux, WinUSB on Windows.
        /// Common in STM32 DFU, RP2040, ATmega16U2.
        /// </summary>
        UsbDfu,

        /// <summary>
        /// UEFI firmware capsule update.
        /// Windows-only: uses DeviceIoControl with FSCTL_SET_FIRMWARE_RESOURCE.
        /// </summary>
        Uefi
    }
}
