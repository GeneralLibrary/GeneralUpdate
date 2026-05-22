using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Strategy.Connections.Protocol;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections
{
    /// <summary>
    /// Connection implementation for serial firmware transfer (UART/RS232).
    /// Uses the full XMODEM/YMODEM protocol implementations with automatic negotiation.
    /// 
    /// <para>
    /// Requires the System.IO.Ports NuGet package (v4.5+) for SerialPort support.
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

            _serialPort = new SerialPort(
                _config.SerialPort,
                _config.BaudRate,
                Parity.None, 8, StopBits.One)
            {
                ReadTimeout = StreamHelpers.DefaultTimeoutMs,
                WriteTimeout = StreamHelpers.DefaultTimeoutMs,
                DtrEnable = true,
                RtsEnable = true
            };

            _serialPort.Open();
            FirmwareTrace.Info("Serial port opened: {0}", _config.SerialPort);
            return Task.CompletedTask;
        }

        public async Task WriteAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("Serial port is not open. Call OpenAsync first.");

            FirmwareTrace.Info(
                "Serial transfer: {0} bytes via {1}", data.Length, _config.SerialProtocol);

            Stream stream = _serialPort.BaseStream;
            SerialProtocol protocol = _config.SerialProtocol;

            // Auto-negotiate if needed
            if (protocol == SerialProtocol.Auto)
            {
                protocol = await NegotiateAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
                FirmwareTrace.Info("Negotiated protocol: {0}", protocol);
            }

            switch (protocol)
            {
                case SerialProtocol.XModem:
                    await XModemProtocol.SendAsync(stream, data, 128, useCrc: false, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SerialProtocol.XModemCRC:
                    await XModemProtocol.SendAsync(stream, data, 128, useCrc: true, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SerialProtocol.XModem1K:
                    await XModemProtocol.SendAsync(stream, data, 1024, useCrc: true, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SerialProtocol.YModem:
                    await YModemProtocol.SendAsync(stream, data, "firmware.bin", cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException(string.Format(
                        "Protocol {0} not supported.", protocol));
            }
        }

        public Task CloseAsync()
        {
            if (_serialPort != null)
            {
                try { if (_serialPort.IsOpen) _serialPort.Close(); }
                catch { /* ignore close errors */ }
                _serialPort.Dispose();
                _serialPort = null;
                FirmwareTrace.Debug("Serial port closed.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Auto-negotiates protocol by reading the receiver's init character.
        /// 'C' (0x43) = CRC-capable → YMODEM.
        /// NAK (0x15) = checksum-only → XMODEM.
        /// Default = XMODEM-CRC.
        /// </summary>
        private static async Task<SerialProtocol> NegotiateAsync(
            Stream stream, CancellationToken cancellationToken)
        {
            await StreamHelpers.DrainAsync(stream, cancellationToken).ConfigureAwait(false);

            try
            {
                int initChar = await StreamHelpers.ReadByteWithTimeoutAsync(
                    stream, StreamHelpers.DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

                if (initChar == StreamHelpers.C)
                {
                    FirmwareTrace.Debug("Receiver requested CRC. Using YMODEM.");
                    return SerialProtocol.YModem;
                }
                if (initChar == StreamHelpers.NAK)
                {
                    FirmwareTrace.Debug("Receiver sent NAK. Using XMODEM checksum.");
                    return SerialProtocol.XModem;
                }
            }
            catch (TimeoutException) { }

            FirmwareTrace.Debug("Defaulting to XMODEM-CRC.");
            return SerialProtocol.XModemCRC;
        }
    }
}
