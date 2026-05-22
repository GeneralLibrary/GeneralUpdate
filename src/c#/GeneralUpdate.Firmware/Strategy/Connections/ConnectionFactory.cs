using System;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections
{
    /// <summary>
    /// Factory that creates the appropriate <see cref="IConnection"/> instance
    /// based on the connection type specified in <see cref="DeviceConnection"/>.
    /// </summary>
    internal static class ConnectionFactory
    {
        /// <summary>
        /// Creates a connection instance for the given device connection configuration.
        /// </summary>
        /// <param name="config">The device connection configuration.</param>
        /// <returns>An <see cref="IConnection"/> suitable for the specified connection type.</returns>
        /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the connection type is not supported.</exception>
        public static IConnection Create(DeviceConnection config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            FirmwareTrace.Debug("Creating connection for type: {0}", config.Type);

            switch (config.Type)
            {
                case ConnectionType.BlockDevice:
                    if (string.IsNullOrWhiteSpace(config.DevicePath))
                        throw new InvalidOperationException(
                            "DevicePath is required for BlockDevice connection.");
                    return new BlockDeviceConnection(config.DevicePath);

                case ConnectionType.Serial:
                    if (string.IsNullOrWhiteSpace(config.SerialPort))
                        throw new InvalidOperationException(
                            "SerialPort is required for Serial connection.");
                    return new SerialConnection(config);

                case ConnectionType.UsbDfu:
                    if (config.VendorId == 0 && config.ProductId == 0)
                        throw new InvalidOperationException(
                            "VendorId and/or ProductId is required for UsbDfu connection.");
                    return new UsbDfuConnection(config);

                case ConnectionType.Uefi:
                    // UEFI is handled inline by the Windows strategy
                    // Return a block device connection as fallback
                    return new BlockDeviceConnection(config.DevicePath ?? @"\\.\PhysicalDrive0");

                default:
                    throw new NotSupportedException(string.Format(
                        "Connection type '{0}' is not supported.", config.Type));
            }
        }
    }
}
