using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections.Protocol
{
    /// <summary>
    /// YMODEM protocol implementation.
    /// Uses 1024-byte data packets with CRC-16 and Block-0 file metadata.
    /// 
    /// <para>Transfer sequence:</para>
    /// <list type="number">
    ///   <item><description>Wait for receiver 'C' (CRC-16 request)</description></item>
    ///   <item><description>Send Block 0: SOH, seq=0, 128 bytes of file metadata + CRC-16</description></item>
    ///   <item><description>Wait for ACK then another 'C'</description></item>
    ///   <item><description>Send data blocks: STX, seq=1..N, 1024 bytes + CRC-16</description></item>
    ///   <item><description>Send empty Block 0 to signal end</description></item>
    ///   <item><description>Send EOT, wait for ACK</description></item>
    /// </list>
    /// 
    /// <para>Pure Stream-based I/O. Works with SerialPort.BaseStream.</para>
    /// </summary>
    internal static class YModemProtocol
    {
        /// <summary>
        /// Sends firmware data using YMODEM protocol with CRC-16.
        /// </summary>
        /// <param name="stream">Bidirectional stream.</param>
        /// <param name="data">Firmware data to send.</param>
        /// <param name="fileName">File name for Block-0 metadata (max 127 chars).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task SendAsync(
            Stream stream,
            byte[] data,
            string fileName,
            CancellationToken cancellationToken)
        {
            int dataBlocks = (data.Length + 1023) / 1024;

            FirmwareTrace.Info(
                "YMODEM: {0} bytes, {1} data blocks, file={2}",
                data.Length, dataBlocks, fileName);

            // Step 1: Wait for receiver CRC request
            await StreamHelpers.DrainAsync(stream, cancellationToken).ConfigureAwait(false);

            for (int attempt = 0; attempt < StreamHelpers.MaxRetries; attempt++)
            {
                int rc = await StreamHelpers.ReadByteWithTimeoutAsync(
                    stream, StreamHelpers.DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);
                if (rc == StreamHelpers.C) break;
            }

            // Step 2: Send Block 0 (file metadata, 128 bytes)
            byte[] block0 = BuildBlock0(fileName, data.Length);
            await SendBlockAsync(stream, block0, sequenceNumber: 0, isBlock0: true, cancellationToken)
                .ConfigureAwait(false);

            // Wait for ACK
            int ack0 = await StreamHelpers.ReadByteWithTimeoutAsync(
                stream, StreamHelpers.DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);
            if (ack0 == StreamHelpers.CAN)
                throw new IOException("YMODEM cancelled after Block 0.");

            // Step 3: Wait for another 'C' before data blocks
            for (int attempt = 0; attempt < StreamHelpers.MaxRetries; attempt++)
            {
                int rc = await StreamHelpers.ReadByteWithTimeoutAsync(
                    stream, StreamHelpers.DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);
                if (rc == StreamHelpers.C) break;
            }

            // Step 4: Send data blocks
            int sequenceNumber = 1;
            int dataOffset = 0;

            while (dataOffset < data.Length)
            {
                int remaining = data.Length - dataOffset;
                int blockSize = Math.Min(1024, remaining);

                byte[] block = new byte[1024];
                Array.Copy(data, dataOffset, block, 0, blockSize);

                bool acked = false;
                for (int retry = 0; retry < StreamHelpers.MaxRetries; retry++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await SendBlockAsync(stream, block, sequenceNumber, isBlock0: false, cancellationToken)
                        .ConfigureAwait(false);

                    int response = await StreamHelpers.ReadByteWithTimeoutAsync(
                        stream, StreamHelpers.DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

                    if (response == StreamHelpers.ACK) { acked = true; break; }
                    if (response == StreamHelpers.CAN)
                        throw new IOException("YMODEM cancelled by receiver.");
                    if (response == StreamHelpers.NAK)
                        FirmwareTrace.Warn("NAK for block {0} (retry {1})", sequenceNumber, retry + 1);
                }

                if (!acked)
                    throw new IOException(string.Format(
                        "YMODEM block {0} failed after {1} retries.", sequenceNumber, StreamHelpers.MaxRetries));

                dataOffset += blockSize;
                sequenceNumber++;

                FirmwareTrace.Progress("YMODEM", dataOffset, data.Length);
            }

            // Step 5: Send empty Block 0 to signal end
            byte[] endBlock = BuildBlock0(string.Empty, 0);
            await SendBlockAsync(stream, endBlock, sequenceNumber: 0, isBlock0: true, cancellationToken)
                .ConfigureAwait(false);

            int endAck = await StreamHelpers.ReadByteWithTimeoutAsync(
                stream, StreamHelpers.DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

            // Step 6: Send EOT
            await StreamHelpers.SendEotAsync(stream, cancellationToken).ConfigureAwait(false);

            FirmwareTrace.Info("YMODEM transfer complete.");
        }

        /// <summary>
        /// Builds YMODEM Block 0: filename\0 size\0 ... padded to 128 bytes with 0x00.
        /// Empty file name + size=0 signals end of transfer.
        /// </summary>
        private static byte[] BuildBlock0(string fileName, long fileSize)
        {
            byte[] block = new byte[128];
            for (int i = 0; i < 128; i++) block[i] = 0x00;

            if (!string.IsNullOrEmpty(fileName) || fileSize != 0)
            {
                string meta = string.Format("{0}\x00{1}\x00", fileName, fileSize);
                byte[] metaBytes = Encoding.ASCII.GetBytes(meta);
                Array.Copy(metaBytes, 0, block, 0, Math.Min(metaBytes.Length, 128));
            }

            return block;
        }

        /// <summary>
        /// Sends a YMODEM block: marker + seq + ~seq + data + CRC-16.
        /// Block 0 uses SOH (128B), data blocks use STX (1024B).
        /// </summary>
        private static async Task SendBlockAsync(
            Stream stream,
            byte[] data,
            int sequenceNumber,
            bool isBlock0,
            CancellationToken cancellationToken)
        {
            int dataSize = isBlock0 ? 128 : 1024;
            byte marker = isBlock0 ? StreamHelpers.SOH : StreamHelpers.STX;

            byte[] packet = new byte[3 + dataSize + 2];
            packet[0] = marker;
            packet[1] = (byte)(sequenceNumber % 256);
            packet[2] = (byte)(255 - (sequenceNumber % 256));

            Array.Copy(data, 0, packet, 3, Math.Min(data.Length, dataSize));

            ushort crc = CrcUtils.ComputeCrc16(packet, 3, dataSize);
            packet[3 + dataSize] = (byte)(crc >> 8);
            packet[3 + dataSize + 1] = (byte)(crc & 0xFF);

            await stream.WriteAsync(packet, 0, packet.Length, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
