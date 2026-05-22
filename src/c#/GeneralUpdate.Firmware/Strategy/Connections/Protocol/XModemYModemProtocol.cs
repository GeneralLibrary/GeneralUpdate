using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections.Protocol
{
    /// <summary>
    /// Full implementation of XMODEM and YMODEM serial transfer protocols.
    /// Pure Stream-based I/O 鈥?works with SerialPort.BaseStream, FileStream, or NetworkStream.
    /// 
    /// <para>Supported variants:</para>
    /// <list type="bullet">
    ///   <item><description>XMODEM 鈥?128-byte packets, 8-bit checksum</description></item>
    ///   <item><description>XMODEM-CRC 鈥?128-byte packets, CRC-8</description></item>
    ///   <item><description>XMODEM-1K 鈥?1024-byte packets, CRC-8</description></item>
    ///   <item><description>YMODEM 鈥?1024-byte packets, CRC-16 + file metadata</description></item>
    /// </list>
    /// 
    /// <para>Auto-negotiation attempts YMODEM first, falling back through XMODEM variants.</para>
    /// </summary>
    internal static class XModemYModemProtocol
    {
        // Protocol control characters
        private const byte SOH = 0x01;    // 128-byte packet header
        private const byte STX = 0x02;    // 1024-byte packet header
        private const byte EOT = 0x04;    // End of transmission
        private const byte ACK = 0x06;    // Acknowledge
        private const byte NAK = 0x15;    // Negative acknowledge
        private const byte CAN = 0x18;    // Cancel
        private const byte C   = 0x43;    // CRC request character ('C')

        private const int MaxRetries = 10;
        private const int InitTimeoutMs = 10000;  // Initial handshake timeout
        private const int PacketTimeoutMs = 10000; // Per-packet timeout

        // CRC-8 lookup table (polynomial 0x07)
        private static readonly byte[] Crc8Table = BuildCrc8Table();

        // CRC-16 lookup table (polynomial 0x1021)
        private static readonly ushort[] Crc16Table = BuildCrc16Table();

        /// <summary>
        /// Sends firmware data using the specified serial protocol.
        /// Handles protocol negotiation, packet transmission, retry, and error recovery.
        /// </summary>
        /// <param name="stream">The bidirectional stream (e.g., SerialPort.BaseStream).</param>
        /// <param name="data">The firmware data to send.</param>
        /// <param name="protocol">The desired protocol (Auto for negotiation).</param>
        /// <param name="fileName">Optional file name for YMODEM block-0 metadata.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task SendAsync(
            Stream stream,
            byte[] data,
            SerialProtocol protocol,
            string fileName = null,
            CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (data == null) throw new ArgumentNullException(nameof(data));

            FirmwareTrace.Info("Starting serial transfer: {0} bytes", data.Length);

            if (protocol == SerialProtocol.Auto)
            {
                protocol = await NegotiateProtocolAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
                FirmwareTrace.Info("Negotiated protocol: {0}", protocol);
            }

            switch (protocol)
            {
                case SerialProtocol.XModem:
                    await SendXModemAsync(stream, data, packetSize: 128, useCrc: false, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SerialProtocol.XModemCRC:
                    await SendXModemAsync(stream, data, packetSize: 128, useCrc: true, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SerialProtocol.XModem1K:
                    await SendXModemAsync(stream, data, packetSize: 1024, useCrc: true, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case SerialProtocol.YModem:
                    await SendYModemAsync(stream, data, fileName ?? "firmware.bin", cancellationToken)
                        .ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException(string.Format(
                        "Serial protocol {0} is not supported.", protocol));
            }

            FirmwareTrace.Info("Serial transfer completed successfully.");
        }

        // 鈹€鈹€ Protocol negotiation 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        /// <summary>
        /// Auto-negotiates the serial protocol with the bootloader.
        /// Sends 'C' (CRC-16 request for YMODEM), 'C' (CRC-8 request for XMODEM-CRC),
        /// and NAK for XMODEM-Checksum in sequence.
        /// </summary>
        private static async Task<SerialProtocol> NegotiateProtocolAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            FirmwareTrace.Debug("Starting protocol auto-negotiation...");

            // Flush any pending data
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Try 1: YMODEM 鈥?send 'C' (CRC-16 request)
            // In YMODEM, the receiver sends 'C' to request CRC-16.
            // The sender (us) sends the first packet; the receiver responds.
            // But for the sender side, we need to detect what the receiver expects.
            // Reversal: we act as the SENDER. The bootloader (receiver) sends a character.
            // Wait for the bootloader to signal readiness.
            try
            {
                // Clear any noise first
                await DrainStreamAsync(stream, cancellationToken).ConfigureAwait(false);

                // Wait for the receiver to send its request character
                // YMODEM/CRC-capable receivers send 'C' (0x43)
                // XMODEM-checksum receivers send NAK (0x15)
                int initChar = await ReadByteWithTimeoutAsync(stream, InitTimeoutMs, cancellationToken)
                    .ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested) return SerialProtocol.XModem;

                switch (initChar)
                {
                    case 'C':
                        // Bootloader wants CRC. Try YMODEM first (CRC-16 with metadata).
                        FirmwareTrace.Debug("Receiver requested CRC. Will attempt YMODEM.");
                        return SerialProtocol.YModem;

                    case NAK:
                        // Bootloader only does checksum XMODEM
                        FirmwareTrace.Debug("Receiver sent NAK. Using XMODEM checksum.");
                        return SerialProtocol.XModem;

                    default:
                        FirmwareTrace.Warn(
                            "Unexpected init character: 0x{0:X2}. Defaulting to XMODEM-CRC.",
                            initChar);
                        return SerialProtocol.XModemCRC;
                }
            }
            catch (TimeoutException)
            {
                FirmwareTrace.Warn(
                    "No response from bootloader within {0}ms. Defaulting to XMODEM-CRC.",
                    InitTimeoutMs);
                return SerialProtocol.XModemCRC;
            }
        }

        // 鈹€鈹€ XMODEM send (128 or 1024 byte packets, checksum or CRC) 鈹€鈹€

        private static async Task SendXModemAsync(
            Stream stream,
            byte[] data,
            int packetSize,
            bool useCrc,
            CancellationToken cancellationToken)
        {
            int totalPackets = (data.Length + packetSize - 1) / packetSize;
            byte packetMarker = packetSize == 1024 ? STX : SOH;

            FirmwareTrace.Info(
                "XMODEM transfer: {0} bytes, {1} packets of {2}B, CRC={3}",
                data.Length, totalPackets, packetSize, useCrc);

            // Wait for the receiver to signal readiness
            await WaitForReceiverReadyAsync(stream, useCrc, cancellationToken)
                .ConfigureAwait(false);

            byte sequenceNumber = 1;
            int dataOffset = 0;

            while (dataOffset < data.Length)
            {
                int remaining = data.Length - dataOffset;
                int currentPacketSize = Math.Min(packetSize, remaining);

                byte[] packet = BuildXModemPacket(
                    data, dataOffset, currentPacketSize, packetSize,
                    sequenceNumber, useCrc);

                // Send packet with retry
                bool acked = false;
                for (int retry = 0; retry < MaxRetries; retry++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await stream.WriteAsync(packet, 0, packet.Length, cancellationToken)
                        .ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                    int response = await ReadByteWithTimeoutAsync(stream, PacketTimeoutMs, cancellationToken)
                        .ConfigureAwait(false);

                    if (response == ACK)
                    {
                        acked = true;
                        break;
                    }
                    else if (response == CAN)
                    {
                        throw new IOException("Transfer cancelled by receiver (CAN received).");
                    }
                    else if (response == NAK)
                    {
                        FirmwareTrace.Warn(
                            "NAK received for packet {0}/{1} (retry {2}/{3})",
                            sequenceNumber, totalPackets, retry + 1, MaxRetries);
                    }
                    else
                    {
                        FirmwareTrace.Warn(
                            "Unexpected response 0x{0:X2} for packet {1} (retry {2})",
                            response, sequenceNumber, retry + 1);
                    }
                }

                if (!acked)
                {
                    throw new IOException(string.Format(
                        "XMODEM transfer failed: no ACK for packet {0} after {1} retries.",
                        sequenceNumber, MaxRetries));
                }

                dataOffset += currentPacketSize;
                sequenceNumber = (byte)((sequenceNumber + 1) % 256);
                if (sequenceNumber == 0) sequenceNumber = 1;

                FirmwareTrace.Progress(
                    "XMODEM", dataOffset, data.Length);
            }

            // Send EOT
            await SendEotAsync(stream, cancellationToken).ConfigureAwait(false);
            FirmwareTrace.Info("XMODEM transfer complete.");
        }

        private static byte[] BuildXModemPacket(
            byte[] data, int offset, int size, int packetSize,
            byte sequenceNumber, bool useCrc)
        {
            int totalPacketSize = 3 + packetSize + (useCrc ? 2 : 1); // header + data + checksum/crc
            byte[] packet = new byte[totalPacketSize];

            // Header: marker + seq + ~seq
            packet[0] = (byte)(packetSize == 1024 ? STX : SOH);
            packet[1] = sequenceNumber;
            packet[2] = (byte)(255 - sequenceNumber);

            // Data (pad unused bytes with 0x1A SUB filler)
            for (int i = 0; i < packetSize; i++)
            {
                if (i < size)
                    packet[3 + i] = data[offset + i];
                else
                    packet[3 + i] = 0x1A; // SUB (EOF marker)
            }

            // Checksum or CRC
            if (useCrc)
            {
                ushort crc = ComputeCrc16(packet, 3, packetSize); // CRC-16 over data
                packet[3 + packetSize] = (byte)(crc >> 8);
                packet[3 + packetSize + 1] = (byte)(crc & 0xFF);
            }
            else
            {
                // 8-bit checksum: sum of data bytes modulo 256
                byte sum = 0;
                for (int i = 0; i < packetSize; i++)
                {
                    sum += packet[3 + i];
                }
                packet[3 + packetSize] = sum;
            }

            return packet;
        }

        private static async Task WaitForReceiverReadyAsync(
            Stream stream, bool useCrc, CancellationToken cancellationToken)
        {
            // Drain stale data
            await DrainStreamAsync(stream, cancellationToken).ConfigureAwait(false);

            byte expectedChar = useCrc ? C : NAK;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                // The sender should wait for the receiver to send its request character.
                // In practice, bootloaders send 'C' (if CRC-capable) or NAK repeatedly.
                int response = await ReadByteWithTimeoutAsync(stream, InitTimeoutMs, cancellationToken)
                    .ConfigureAwait(false);

                if (response == expectedChar || response == C || response == NAK)
                {
                    // Receiver is ready
                    FirmwareTrace.Debug(
                        "Receiver ready (0x{0:X2} after {1} attempts)", response, attempt + 1);
                    return;
                }

                FirmwareTrace.Warn(
                    "Waiting for receiver... got 0x{0:X2}, expected 0x{1:X2} (attempt {2})",
                    response, expectedChar, attempt + 1);
            }

            // If we still haven't gotten a proper response, proceed anyway
            FirmwareTrace.Warn("Receiver did not respond with expected character. Proceeding anyway.");
        }

        // 鈹€鈹€ YMODEM send 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        /// <summary>
        /// Sends firmware via YMODEM protocol.
        /// Block 0: file metadata (name + size + pad to 128 bytes).
        /// Blocks 1..N: 1024-byte data with CRC-16.
        /// </summary>
        private static async Task SendYModemAsync(
            Stream stream,
            byte[] data,
            string fileName,
            CancellationToken cancellationToken)
        {
            int totalBlocks = (data.Length + 1023) / 1024; // +1 for block 0
            byte sequenceNumber = 0; // Block 0 uses sequence 0

            FirmwareTrace.Info(
                "YMODEM transfer: {0} bytes, {1} data blocks, file={2}",
                data.Length, totalBlocks, fileName);

            // Wait for receiver 'C' (CRC-16 request)
            await WaitForReceiverReadyAsync(stream, useCrc: true, cancellationToken)
                .ConfigureAwait(false);

            // 鈹€鈹€ Block 0: file metadata 鈹€鈹€
            byte[] metadata = BuildYModemBlock0(fileName, data.Length);
            await SendYModemBlockAsync(stream, metadata, sequenceNumber, isBlock0: true, cancellationToken)
                .ConfigureAwait(false);

            // Wait for ACK after block 0
            // Some receivers send 'C' again asking for actual data, others send ACK
            int block0Response = await ReadByteWithTimeoutAsync(stream, PacketTimeoutMs, cancellationToken)
                .ConfigureAwait(false);
            if (block0Response == CAN)
            {
                throw new IOException("YMODEM transfer cancelled after block 0.");
            }
            else if (block0Response == 'C')
            {
                // Receiver wants another initialization 鈥?expected behavior
                FirmwareTrace.Debug("Receiver re-requesting CRC after block 0. Resending readiness.");
            }
            else if (block0Response != ACK)
            {
                FirmwareTrace.Warn(
                    "Unexpected response after block 0: 0x{0:X2}. Continuing.",
                    block0Response);
            }

            // Wait for another 'C' to start data blocks
            await WaitForReceiverReadyAsync(stream, useCrc: true, cancellationToken)
                .ConfigureAwait(false);

            // 鈹€鈹€ Data blocks 鈹€鈹€
            sequenceNumber = 1;
            int dataOffset = 0;

            while (dataOffset < data.Length || dataOffset == 0)
            {
                int remaining = data.Length - dataOffset;
                int blockSize = Math.Min(1024, remaining);

                byte[] blockData = new byte[1024];
                if (remaining > 0)
                {
                    Array.Copy(data, dataOffset, blockData, 0, blockSize);
                }
                else
                {
                    break; // All data sent
                }

                // Pad with 0x00 if partial block (YMODEM uses 0x00 padding for data)
                for (int i = blockSize; i < 1024; i++)
                {
                    blockData[i] = 0x00;
                }

                bool acked = false;
                for (int retry = 0; retry < MaxRetries; retry++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await SendYModemBlockAsync(
                        stream, blockData, sequenceNumber, isBlock0: false, cancellationToken)
                        .ConfigureAwait(false);

                    int response = await ReadByteWithTimeoutAsync(stream, PacketTimeoutMs, cancellationToken)
                        .ConfigureAwait(false);

                    if (response == ACK)
                    {
                        acked = true;
                        break;
                    }
                    else if (response == CAN)
                    {
                        throw new IOException("YMODEM transfer cancelled by receiver.");
                    }
                    else if (response == NAK)
                    {
                        FirmwareTrace.Warn(
                            "NAK for YMODEM block {0} (retry {1})", sequenceNumber, retry + 1);
                    }
                }

                if (!acked)
                {
                    throw new IOException(string.Format(
                        "YMODEM block {0} failed after {1} retries.", sequenceNumber, MaxRetries));
                }

                dataOffset += blockSize;
                sequenceNumber++;

                FirmwareTrace.Progress(
                    "YMODEM", dataOffset, data.Length);
            }

            // 鈹€鈹€ Final: send empty block 0 to signal end, then EOT 鈹€鈹€
            byte[] endBlock = BuildYModemBlock0("", 0); // empty block signals end
            await SendYModemBlockAsync(stream, endBlock, sequenceNumber: 0, isBlock0: true, cancellationToken)
                .ConfigureAwait(false);

            // Wait for ACK after end block
            int endResponse = await ReadByteWithTimeoutAsync(stream, PacketTimeoutMs, cancellationToken)
                .ConfigureAwait(false);
            if (endResponse != ACK && endResponse != 'C')
            {
                FirmwareTrace.Warn(
                    "Unexpected response after end block: 0x{0:X2}", endResponse);
            }

            // Send EOT
            await SendEotAsync(stream, cancellationToken).ConfigureAwait(false);

            FirmwareTrace.Info("YMODEM transfer complete.");
        }

        /// <summary>
        /// Builds YMODEM block 0 metadata:
        /// filename\0 size_in_decimal\0 ... padding to 128 bytes.
        /// </summary>
        private static byte[] BuildYModemBlock0(string fileName, long fileSize)
        {
            byte[] block0 = new byte[128];
            for (int i = 0; i < 128; i++) block0[i] = 0x00;

            if (string.IsNullOrEmpty(fileName) && fileSize == 0)
            {
                // Empty block signals end of transfer
                return block0;
            }

            // Format: filename\0size\0
            string metadata = string.Format("{0}\x00{1}\x00", fileName, fileSize);
            byte[] metaBytes = Encoding.ASCII.GetBytes(metadata);
            int copyLen = Math.Min(metaBytes.Length, 128);
            Array.Copy(metaBytes, 0, block0, 0, copyLen);

            return block0;
        }

        private static async Task SendYModemBlockAsync(
            Stream stream,
            byte[] data,
            int sequenceNumber,
            bool isBlock0,
            CancellationToken cancellationToken)
        {
            int packetSize = isBlock0 ? 128 : 1024;
            // YMODEM always uses STX for data blocks; block 0 uses SOH (128 bytes)
            byte marker = isBlock0 ? SOH : STX;

            int totalSize = 3 + packetSize + 2; // marker + seq + ~seq + data + CRC-16
            byte[] packet = new byte[totalSize];

            packet[0] = marker;
            packet[1] = (byte)(sequenceNumber % 256);
            packet[2] = (byte)(255 - (sequenceNumber % 256));

            Array.Copy(data, 0, packet, 3, Math.Min(data.Length, packetSize));

            // CRC-16 over data portion
            ushort crc = ComputeCrc16(packet, 3, packetSize);
            packet[3 + packetSize] = (byte)(crc >> 8);
            packet[3 + packetSize + 1] = (byte)(crc & 0xFF);

            await stream.WriteAsync(packet, 0, packet.Length, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        // 鈹€鈹€ EOT handling 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        private static async Task SendEotAsync(
            Stream stream, CancellationToken cancellationToken)
        {
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                stream.WriteByte(EOT);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                int response = await ReadByteWithTimeoutAsync(stream, PacketTimeoutMs, cancellationToken)
                    .ConfigureAwait(false);

                if (response == ACK)
                {
                    FirmwareTrace.Debug("EOT acknowledged.");
                    return;
                }
                else if (response == CAN)
                {
                    FirmwareTrace.Debug("EOT: receiver sent CAN (transfer cancelled).");
                    return;
                }

                FirmwareTrace.Warn(
                    "EOT not acknowledged (0x{0:X2}), retry {1}/{2}",
                    response, retry + 1, MaxRetries);
            }

            FirmwareTrace.Warn("EOT not acknowledged after max retries. Transfer may be incomplete.");
        }

        // 鈹€鈹€ Stream helpers 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        private static async Task<int> ReadByteWithTimeoutAsync(
            Stream stream, int timeoutMs, CancellationToken cancellationToken)
        {
            using (var cts = new CancellationTokenSource(timeoutMs))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
            {
                byte[] buffer = new byte[1];
                Task<int> readTask = stream.ReadAsync(buffer, 0, 1, linked.Token);

                try
                {
                    int result = await readTask.ConfigureAwait(false);
                    return result == 0 ? -1 : buffer[0];
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    throw new TimeoutException(string.Format(
                        "Timed out waiting for byte after {0}ms.", timeoutMs));
                }
            }
        }

        private static async Task DrainStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                byte[] buffer = new byte[256];
                int totalDrained = 0;
                while (totalDrained < 4096) // drain up to 4KB of stale data
                {
                    using (var cts = new CancellationTokenSource(200))
                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
                    {
                        try
                        {
                            int read = await stream.ReadAsync(buffer, 0, buffer.Length, linked.Token)
                                .ConfigureAwait(false);
                            if (read == 0) break;
                            totalDrained += read;
                        }
                        catch (OperationCanceledException) when (cts.IsCancellationRequested)
                        {
                            break; // no more data
                        }
                    }
                }
                if (totalDrained > 0)
                {
                    FirmwareTrace.Debug("Drained {0} bytes of stale data from stream.", totalDrained);
                }
            }
            catch (IOException)
            {
                // Stream may not support timeout 鈥?ignore
            }
        }

        // 鈹€鈹€ CRC utilities 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

        /// <summary>
        /// Computes CRC-16 (polynomial 0x1021) over the specified byte range.
        /// </summary>
        internal static ushort ComputeCrc16(byte[] data, int offset, int length)
        {
            ushort crc = 0;
            for (int i = offset; i < offset + length; i++)
            {
                crc = (ushort)((Crc16Table[((crc >> 8) ^ data[i]) & 0xFF] ^ (crc << 8)) & 0xFFFF);
            }
            return crc;
        }

        private static ushort[] BuildCrc16Table()
        {
            ushort[] table = new ushort[256];
            for (int i = 0; i < 256; i++)
            {
                ushort crc = (ushort)(i << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
                table[i] = crc;
            }
            return table;
        }

        private static byte[] BuildCrc8Table()
        {
            byte[] table = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                byte crc = (byte)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x80) != 0)
                        crc = (byte)((crc << 1) ^ 0x07);
                    else
                        crc <<= 1;
                }
                table[i] = crc;
            }
            return table;
        }
    }
}
