using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections
{
    /// <summary>
    /// Connection implementation for serial firmware transfer (UART/RS232).
    /// Uses the full XMODEM/YMODEM protocol implementation with automatic negotiation.
    /// 
    /// <para>
    /// Requires the System.IO.Ports NuGet package (v4.5+) for SerialPort support.
    /// Add to your host application:
    /// <code>&lt;PackageReference Include="System.IO.Ports" Version="4.5.0" /&gt;</code>
    /// </para>
    /// </summary>
    internal class SerialConnection : IConnection
    {
        private readonly DeviceConnection _config;
        private SerialPort _serialPort;

        public SerialConnection(DeviceConnection config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            FirmwareTrace.Info(
                "Opening serial port: {0} @ {1} baud (protocol: {2})",
                _config.SerialPort,
                _config.BaudRate,
                _config.SerialProtocol);

            try
            {
                _serialPort = new SerialPort(
                    _config.SerialPort,
                    _config.BaudRate,
                    Parity.None,
                    8,
                    StopBits.One)
                {
                    ReadTimeout = 10000,
                    WriteTimeout = 10000,
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serialPort.Open();

                FirmwareTrace.Info(
                    "Serial port opened: {0} (IsOpen={1})",
                    _config.SerialPort,
                    _serialPort.IsOpen);
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Failed to open serial port {0}: {1}", _config.SerialPort, ex.Message);
                throw new InvalidOperationException(
                    string.Format(
                        "Failed to open serial port '{0}'. " +
                        "Ensure the port exists, you have access permissions, " +
                        "and the System.IO.Ports NuGet package is referenced. " +
                        "Error: {1}",
                        _config.SerialPort,
                        ex.Message),
                    ex);
            }

            return Task.CompletedTask;
        }

        public async Task WriteAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("Serial port is not open. Call OpenAsync first.");

            FirmwareTrace.Info(
                "Starting serial firmware transfer: {0} bytes via {1}",
                data.Length,
                _config.SerialProtocol);

            try
            {
                await Protocol.XModemYModemProtocol.SendAsync(
                    _serialPort.BaseStream,
                    data,
                    _config.SerialProtocol,
                    "firmware.bin",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                FirmwareTrace.Error("Serial transfer failed", ex);
                throw;
            }
        }

        public Task CloseAsync()
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                    _serialPort.Dispose();
                }
                catch (Exception ex)
                {
                    FirmwareTrace.Warn("Error closing serial port: {0}", ex.Message);
                }
                finally
                {
                    _serialPort = null;
                }

                FirmwareTrace.Debug("Serial port closed.");
            }
            return Task.CompletedTask;
        }
    }
}
