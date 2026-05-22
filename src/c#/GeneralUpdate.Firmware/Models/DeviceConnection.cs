using System;

namespace GeneralUpdate.Firmware.Models
{
    /// <summary>
    /// Describes how to connect to the target hardware device for firmware update.
    /// Aggregates all connection parameters in one place.
    /// 
    /// <para>
    /// Different connection types require different subsets of parameters.
    /// Only the parameters relevant to the selected <see cref="ConnectionType"/> are used.
    /// </para>
    /// </summary>
    public class DeviceConnection
    {
        /// <summary>
        /// Gets or sets the hardware connection type.
        /// Default is <see cref="ConnectionType.BlockDevice"/> for direct block device I/O.
        /// </summary>
        public ConnectionType Type { get; set; } = ConnectionType.BlockDevice;

        // ── BlockDevice ──────────────────────────────────────────

        /// <summary>
        /// Gets or sets the block device path.
        /// Linux: "/dev/mmcblk0", Windows: "\\.\PhysicalDrive0".
        /// </summary>
        public string DevicePath { get; set; }

        // ── Serial ───────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the serial port name.
        /// Linux: "/dev/ttyUSB0", Windows: "COM3".
        /// </summary>
        public string SerialPort { get; set; }

        /// <summary>
        /// Gets or sets the baud rate for serial communication.
        /// Default value is 115200 bps.
        /// </summary>
        public int BaudRate { get; set; } = 115200;

        /// <summary>
        /// Gets or sets the serial transfer protocol.
        /// Default is <see cref="SerialProtocol.Auto"/> for automatic protocol negotiation
        /// (tries YMODEM with CRC-16 first, falls back to XMODEM-CRC, then XMODEM-Checksum).
        /// </summary>
        public SerialProtocol SerialProtocol { get; set; } = SerialProtocol.Auto;

        // ── USB ──────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the USB vendor ID (VID) for device identification.
        /// </summary>
        public ushort VendorId { get; set; }

        /// <summary>
        /// Gets or sets the USB product ID (PID) for device identification.
        /// </summary>
        public ushort ProductId { get; set; }

        // ── Network (deprecated — not implemented) ────────────

        /// <summary>
        /// Gets or sets the remote host IP address or hostname
        /// for network-based firmware transfer (TFTP/TCP).
        /// </summary>
        [Obsolete("Network connection is not implemented. This property is reserved for future use.")]
        public string Host { get; set; }

        /// <summary>
        /// Gets or sets the remote port for network communication.
        /// Default value is 69 (TFTP).
        /// </summary>
        [Obsolete("Network connection is not implemented. This property is reserved for future use.")]
        public int Port { get; set; } = 69;

        // ── Jtag (deprecated — not implemented) ────────────────

        /// <summary>
        /// Gets or sets the OpenOCD configuration file or script path.
        /// Example: "interface/stlink.cfg", "board/raspberrypi.cfg".
        /// </summary>
        [Obsolete("Jtag connection is not implemented. This property is reserved for future use.")]
        public string JtagConfig { get; set; }

        /// <summary>
        /// Validates that all required parameters for the selected connection type are present.
        /// </summary>
        /// <returns>True if the connection is properly configured; false otherwise.</returns>
        public bool Validate()
        {
            switch (Type)
            {
                case ConnectionType.BlockDevice:
                    return !string.IsNullOrWhiteSpace(DevicePath);

                case ConnectionType.Serial:
                    return !string.IsNullOrWhiteSpace(SerialPort) && BaudRate > 0;

                case ConnectionType.UsbDfu:
                    return VendorId != 0 || ProductId != 0;

                case ConnectionType.Network:
                    return !string.IsNullOrWhiteSpace(Host) && Port > 0;

                case ConnectionType.Uefi:
                    return true; // Uses system firmware interface, no extra params needed

                case ConnectionType.Jtag:
                    return !string.IsNullOrWhiteSpace(JtagConfig);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns a string representation of the connection (excluding sensitive fields).
        /// </summary>
        public override string ToString()
        {
            switch (Type)
            {
                case ConnectionType.BlockDevice:
                    return string.Format("BlockDevice[{0}]", DevicePath ?? "(not set)");

                case ConnectionType.Serial:
                    return string.Format("Serial[{0}, {1}bps, {2}]",
                        SerialPort ?? "(not set)", BaudRate, SerialProtocol);

                case ConnectionType.UsbDfu:
                    return string.Format("UsbDfu[VID:0x{0:X4}, PID:0x{1:X4}]", VendorId, ProductId);

                case ConnectionType.Network:
                    return string.Format("Network[{0}:{1}]", Host ?? "(not set)", Port);

                case ConnectionType.Uefi:
                    return "Uefi[SystemFirmware]";

                case ConnectionType.Jtag:
                    return string.Format("Jtag[{0}]", JtagConfig ?? "(not set)");

                default:
                    return string.Format("Unknown[{0}]", Type);
            }
        }
    }

    /// <summary>
    /// Enumerates the serial transfer protocols available for firmware updates.
    /// </summary>
    /// <remarks>
    /// <para><b>Auto</b> — attempts YMODEM with CRC-16 first, then XMODEM-CRC,
    /// then XMODEM-Checksum. This is the recommended default.</para>
    /// <para><b>XModem</b> — 128-byte packets with 8-bit checksum.
    /// Most compatible but slowest.</para>
    /// <para><b>XModemCRC</b> — 128-byte packets with CRC-8.
    /// Better error detection than plain XMODEM.</para>
    /// <para><b>XModem1K</b> — 1024-byte packets with CRC-8.
    /// Higher throughput than 128-byte modes.</para>
    /// <para><b>YModem</b> — 1024-byte packets with CRC-16 and file metadata.
    /// Modern protocol used by ESP32, U-Boot. Supports batch transfer.</para>
    /// </remarks>
    public enum SerialProtocol
    {
        /// <summary>Auto-negotiate protocol with the bootloader.</summary>
        Auto,

        /// <summary>XMODEM-128 with 8-bit checksum.</summary>
        XModem,

        /// <summary>XMODEM-128 with CRC-8.</summary>
        XModemCRC,

        /// <summary>XMODEM-1K with CRC-8 (1024-byte packets).</summary>
        XModem1K,

        /// <summary>YMODEM with CRC-16 and file metadata.</summary>
        YModem
    }
}
