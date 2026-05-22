using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Firmware.Strategy.Connections
{
    /// <summary>
    /// Internal interface for hardware connection channels.
    /// Each implementation encapsulates the transport-specific I/O
    /// for a given <see cref="Models.ConnectionType"/>.
    /// 
    /// <para>
    /// Implementations:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>BlockDeviceConnection</c> — FileStream to local block device</description></item>
    ///   <item><description><c>SerialConnection</c> — SerialPort with XMODEM/YMODEM</description></item>
    ///   <item><description><c>UsbDfuConnection</c> — libusb (Linux) / WinUSB (Windows), full USB DFU 1.1 protocol</description></item>
    /// </list>
    /// </summary>
    internal interface IConnection
    {
        /// <summary>
        /// Opens the connection to the target device and prepares it for data transfer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task OpenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Writes the firmware data to the target device over this connection.
        /// The connection must be opened via <see cref="OpenAsync"/> first.
        /// </summary>
        /// <param name="data">The decoded firmware data to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task WriteAsync(byte[] data, CancellationToken cancellationToken);

        /// <summary>
        /// Closes the connection and releases all resources.
        /// Safe to call even if the connection was never opened.
        /// </summary>
        Task CloseAsync();
    }
}
