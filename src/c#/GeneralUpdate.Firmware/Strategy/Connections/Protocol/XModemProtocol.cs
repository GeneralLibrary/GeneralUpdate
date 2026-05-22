using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections.Protocol
{
    /// <summary>
    /// XMODEM protocol implementation.
    /// Supports three variants:
    /// <list type="bullet">
    ///   <item><description>XMODEM — 128-byte packets, 8-bit checksum</description></item>
    ///   <item><description>XMODEM-CRC — 128-byte packets, CRC-8</description></item>
    ///   <item><description>XMODEM-1K — 1024-byte packets, CRC-8</description></item>
    /// </list>
    /// 
    /// <para>Pure Stream-based I/O. Works with SerialPort.BaseStream, FileStream, or NetworkStream.</para>
    /// </summary>
    internal static class XModemProtocol
    {
        /// <summary>
        /// Sends firmware data using XMODEM protocol.
        /// </summary>
        /// <param name="stream">Bidirectional stream.</param>
        /// <param name="data">Firmware data to send.</param>
        /// <param name="packetSize">128 or 1024 bytes.</param>
        /// <param name="useCrc">True for CRC-8, false for 8-bit checksum.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task SendAsync(
            Stream stream,
            byte[] data,
            int packetSize,
            bool useCrc,
            CancellationToken cancellationToken)
        {
            int totalPackets = (data.Length + packetSize - 1) / packetSize;
            byte packetMarker = packetSize == 1024 ? StreamHelpers.STX : StreamHelpers.SOH;
            byte expectedRequest = useCrc ? StreamHelpers.C : StreamHelpers.NAK;

            FirmwareTrace.Info(
                "XMODEM: {0} bytes, {1} packets of {2}B, CRC={3}",
                data.Length, totalPackets, packetSize, useCrc);

            // Wait for receiver readiness signal
            await StreamHelpers.DrainAsync(stream, cancellationToken).ConfigureAwait(false);

            for (int attempt = 0; attempt < StreamHelpers.MaxRetries; attempt++)
            {
                int rc = await StreamHelpers.ReadByteWithTimeoutAsync(
                    stream, StreamHelpers.DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);
                if (rc == expectedRequest || rc == StreamHelpers.C || rc == StreamHelpers.NAK) break;
            }

            byte sequenceNumber = 1;
            int dataOffset = 0;

            while (dataOffset < data.Length)
            {
                int remaining = data.Length - dataOffset;
                int currentSize = Math.Min(packetSize, remaining);

                byte[] packet = BuildPacket(data, dataOffset, currentSize, packetSize, sequenceNumber, useCrc);

                bool acked = false;
                for (int retry = 0; retry < StreamHelpers.MaxRetries; retry++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await stream.WriteAsync(packet, 0, packet.Length, cancellationToken)
                        .ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                    int response = await StreamHelpers.ReadByteWithTimeoutAsync(
                        stream, StreamHelpers.DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

                    if (response == StreamHelpers.ACK) { acked = true; break; }
                    if (response == StreamHelpers.CAN)
                        throw new IOException("XMODEM cancelled by receiver (CAN).");
                    if (response == StreamHelpers.NAK)
                        FirmwareTrace.Warn("NAK for packet {0} (retry {1})", sequenceNumber, retry + 1);
                }

                if (!acked)
                    throw new IOException(string.Format(
                        "XMODEM packet {0} failed after {1} retries.", sequenceNumber, StreamHelpers.MaxRetries));

                dataOffset += currentSize;
                sequenceNumber = (byte)((sequenceNumber + 1) % 256);
                if (sequenceNumber == 0) sequenceNumber = 1;

                FirmwareTrace.Progress("XMODEM", dataOffset, data.Length);
            }

            // End of transfer
            await StreamHelpers.SendEotAsync(stream, cancellationToken).ConfigureAwait(false);
            FirmwareTrace.Info("XMODEM transfer complete.");
        }

        /// <summary>
        /// Builds an XMODEM packet: marker + seq + ~seq + data(padded) + checksum/crc.
        /// </summary>
        private static byte[] BuildPacket(
            byte[] data, int offset, int size, int packetSize,
            byte sequenceNumber, bool useCrc)
        {
            int totalSize = 3 + packetSize + (useCrc ? 2 : 1);
            byte[] packet = new byte[totalSize];

            // Header
            packet[0] = (byte)(packetSize == 1024 ? StreamHelpers.STX : StreamHelpers.SOH);
            packet[1] = sequenceNumber;
            packet[2] = (byte)(255 - sequenceNumber);

            // Data with 0x1A padding
            for (int i = 0; i < packetSize; i++)
                packet[3 + i] = i < size ? data[offset + i] : (byte)0x1A;

            // Checksum or CRC
            if (useCrc)
            {
                ushort crc = CrcUtils.ComputeCrc16(packet, 3, packetSize);
                packet[3 + packetSize] = (byte)(crc >> 8);
                packet[3 + packetSize + 1] = (byte)(crc & 0xFF);
            }
            else
            {
                byte sum = 0;
                for (int i = 0; i < packetSize; i++) sum += packet[3 + i];
                packet[3 + packetSize] = sum;
            }

            return packet;
        }
    }
}
