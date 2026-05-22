using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections
{
    /// <summary>
    /// Stub connection for network-based firmware transfer (TFTP/TCP).
    /// Will use UdpClient for TFTP or TcpClient for direct TCP.
    /// </summary>
    internal class NetworkConnection : IConnection
    {
        private readonly DeviceConnection _config;

        public NetworkConnection(DeviceConnection config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            FirmwareTrace.Info(
                "Network connection: {0}:{1}",
                _config.Host,
                _config.Port);

            FirmwareTrace.Warn(
                "Network firmware transfer is a framework stub. " +
                "Full TFTP implementation requires System.Net.Sockets and " +
                "will be provided in a follow-up PR.");

            // In the full implementation:
            //   TFTP: UdpClient → RRQ → DATA packets (512B) → ACK
            //   TCP:  TcpClient.Connect → direct stream write

            return Task.CompletedTask;
        }

        public Task WriteAsync(byte[] data, CancellationToken cancellationToken)
        {
            throw new NotImplementedException(
                "Network firmware transfer is a framework stub. " +
                "Full implementation will support TFTP (UDP port 69) and direct TCP.");
        }

        public Task CloseAsync()
        {
            FirmwareTrace.Debug("Network connection closed.");
            return Task.CompletedTask;
        }
    }
}
