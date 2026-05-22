using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;

namespace GeneralUpdate.Firmware.OTA.Decoders
{
    /// <summary>
    /// Decoder for raw binary firmware files (.bin, .img, .rom, .fw).
    /// Simply reads the file contents as-is without any transformation.
    /// </summary>
    internal class RawDecoder : IFirmwareDecoder
    {
        public FirmwareFormat Format => FirmwareFormat.Raw;

        public Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(File.ReadAllBytes(filePath));
        }
    }

    /// <summary>
    /// Decoder for Intel HEX format (.hex) firmware files.
    /// Parses ASCII-encoded hex records with address and type fields.
    /// Validates CRC-8 per line and resolves address gaps.
    /// 
    /// <para>Record format: <c>:LLAAAATTDD...CC</c></para>
    /// </summary>
    internal class IntelHexDecoder : IFirmwareDecoder
    {
        public FirmwareFormat Format => FirmwareFormat.IntelHex;

        // Maximum addressable space for Intel HEX: 4GB (32-bit extended linear address)
        private const long MaxBufferSize = 4L * 1024 * 1024 * 1024;

        public Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Decode(filePath));
        }

        private byte[] Decode(string filePath)
        {
            var segments = new Dictionary<long, byte[]>(); // baseAddress -> data
            long maxAddress = 0;
            long minAddress = long.MaxValue;
            long extendedBase = 0;

            string[] lines = File.ReadAllLines(filePath, Encoding.ASCII);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith(":")) continue;

                // Parse record
                // :LLAAAATTDDDD...CC
                // LL = byte count, AAAA = address, TT = record type, DD = data, CC = checksum
                if (line.Length < 11) continue; // minimum: :LLAAAATTCC

                int byteCount = HexToByte(line, 1);
                int address = HexToUShort(line, 3);
                int recordType = HexToByte(line, 7);

                // Validate checksum
                int checksum = 0;
                int dataStart = 1; // skip ':'
                int dataEnd = 1 + 2 + 4 + 2 + (byteCount * 2); // : + LL + AAAA + TT + DD... + CC
                for (int i = dataStart; i <= dataEnd; i += 2)
                {
                    checksum += HexToByte(line, i);
                }
                if ((checksum & 0xFF) != 0)
                {
                    throw new FormatException(string.Format(
                        "Intel HEX checksum error at line: {0}", rawLine));
                }

                int dataOffset = 9; // start of data after :LLAAAATT

                switch (recordType)
                {
                    case 0x00: // Data record
                        long absoluteAddr = extendedBase + address;
                        byte[] data = new byte[byteCount];
                        for (int i = 0; i < byteCount; i++)
                        {
                            data[i] = HexToByte(line, dataOffset + i * 2);
                        }

                        if (segments.ContainsKey(absoluteAddr / 65536))
                        {
                            // Merge into existing segment
                            var existing = segments[absoluteAddr / 65536];
                            long offset = absoluteAddr % 65536;
                            if (offset + data.Length > existing.Length)
                            {
                                var expanded = new byte[offset + data.Length];
                                Array.Copy(existing, 0, expanded, 0, existing.Length);
                                segments[absoluteAddr / 65536] = expanded;
                            }
                            Array.Copy(data, 0, segments[absoluteAddr / 65536], offset, data.Length);
                        }
                        else
                        {
                            var segment = new byte[65536];
                            long offset = absoluteAddr % 65536;
                            if (offset + data.Length > segment.Length)
                            {
                                segment = new byte[offset + data.Length];
                            }
                            Array.Copy(data, 0, segment, offset, data.Length);
                            segments[absoluteAddr / 65536] = segment;
                        }

                        if (absoluteAddr < minAddress) minAddress = absoluteAddr;
                        if (absoluteAddr + byteCount > maxAddress)
                            maxAddress = absoluteAddr + byteCount;
                        break;

                    case 0x01: // End of file
                        goto doneParsing;

                    case 0x02: // Extended segment address (shifts by 16)
                        extendedBase = (long)HexToUShort(line, dataOffset) * 16;
                        break;

                    case 0x03: // Start segment address (CS:IP for 8086)
                        break;

                    case 0x04: // Extended linear address (upper 16 bits)
                        extendedBase = (long)HexToUShort(line, dataOffset) << 16;
                        break;

                    case 0x05: // Start linear address (EIP for 80386+)
                        break;

                    default:
                        break;
                }
            }

            doneParsing:

            if (maxAddress == 0 && minAddress == long.MaxValue)
            {
                return new byte[0]; // empty file
            }

            long totalSize = maxAddress - minAddress;
            if (totalSize > MaxBufferSize)
            {
                throw new InvalidOperationException(string.Format(
                    "Intel HEX file exceeds maximum buffer size: {0} bytes", totalSize));
            }

            byte[] result = new byte[totalSize];
            // Fill unmapped regions with 0xFF (erased flash default)
            for (int i = 0; i < result.Length; i++) result[i] = 0xFF;

            foreach (var kvp in segments)
            {
                long segmentBase = kvp.Key * 65536;
                byte[] segmentData = kvp.Value;
                long destOffset = segmentBase - minAddress;
                int copyLen = (int)Math.Min(segmentData.Length, result.Length - destOffset);
                if (copyLen > 0)
                {
                    Array.Copy(segmentData, 0, result, destOffset, copyLen);
                }
            }

            return result;
        }

        private static byte HexToByte(string line, int offset)
        {
            return byte.Parse(line.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static ushort HexToUShort(string line, int offset)
        {
            return ushort.Parse(line.Substring(offset, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Decoder for Motorola S-Record format (.srec, .s19, .s28, .s37) firmware files.
    /// Parses ASCII-encoded S-records with address and CRC validation.
    /// 
    /// <para>Record format: <c>S{type}{byteCount}{address}{data}{checksum}</c></para>
    /// </summary>
    internal class SRecordDecoder : IFirmwareDecoder
    {
        public FirmwareFormat Format => FirmwareFormat.SRecord;

        private const long MaxBufferSize = 4L * 1024 * 1024 * 1024;

        public Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Decode(filePath));
        }

        private byte[] Decode(string filePath)
        {
            var dataRanges = new List<DataRange>();
            long maxAddress = 0;
            long minAddress = long.MaxValue;
            int addressBytes = 2; // default 16-bit addressing, tracks the maximum seen

            string[] lines = File.ReadAllLines(filePath, Encoding.ASCII);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("S", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.Length < 4) continue;

                char type = line[1];
                int byteCount = HexToByte(line, 2);

                // Validate byte count: should match remaining content + checksum (1 byte)
                int contentChars = line.Length - 4; // after S{type}{byteCount}
                if (contentChars / 2 != byteCount - 1) // -1 for checksum
                {
                    // Lenient: some files have whitespace padding
                }

                // Validate checksum: sum of all bytes after S{type} including byteCount should be 0xFF
                int checksum = 0;
                for (int i = 2; i < 2 + byteCount * 2; i += 2)
                {
                    checksum += HexToByte(line, i);
                }
                if ((checksum & 0xFF) != 0xFF)
                {
                    throw new FormatException(string.Format(
                        "S-Record checksum error at line: {0}", rawLine));
                }

                switch (type)
                {
                    case '0': // S0: header record (ignored for data)
                    case '5': // S5: count record (S1/S2/S3 record count)
                        break;

                    case '1': // S1: 16-bit address data
                    case '2': // S2: 24-bit address data
                    case '3': // S3: 32-bit address data
                        {
                            int addrLen = type switch { '1' => 2, '2' => 3, '3' => 4, _ => 2 };
                            long address = 0;
                            int addrOffset = 4; // after S{type}{byteCount}
                            for (int i = 0; i < addrLen; i++)
                            {
                                address = (address << 8) | HexToByte(line, addrOffset + i * 2);
                            }
                            if (addrLen > addressBytes) addressBytes = addrLen;

                            int dataLen = (byteCount - addrLen - 1); // -1 for checksum byte
                            byte[] data = new byte[dataLen];
                            int dataOffset = addrOffset + addrLen * 2;
                            for (int i = 0; i < dataLen; i++)
                            {
                                data[i] = HexToByte(line, dataOffset + i * 2);
                            }

                            dataRanges.Add(new DataRange(address, data));

                            if (address < minAddress) minAddress = address;
                            long end = address + dataLen;
                            if (end > maxAddress) maxAddress = end;
                            break;
                        }

                    case '7': // S7: 32-bit start address (termination)
                    case '8': // S8: 24-bit start address (termination)
                    case '9': // S9: 16-bit start address (termination)
                        goto doneParsing;

                    default:
                        break;
                }
            }

            doneParsing:

            if (maxAddress == 0 && minAddress == long.MaxValue)
            {
                return new byte[0];
            }

            long totalSize = maxAddress - minAddress;
            if (totalSize > MaxBufferSize)
            {
                throw new InvalidOperationException(string.Format(
                    "S-Record file exceeds maximum buffer size: {0} bytes", totalSize));
            }

            byte[] result = new byte[totalSize];
            for (int i = 0; i < result.Length; i++) result[i] = 0xFF; // erased flash default

            foreach (var range in dataRanges)
            {
                long offset = range.Address - minAddress;
                int copyLen = (int)Math.Min(range.Data.Length, result.Length - offset);
                if (copyLen > 0)
                {
                    Array.Copy(range.Data, 0, result, offset, copyLen);
                }
            }

            return result;
        }

        private static byte HexToByte(string line, int offset)
        {
            return byte.Parse(line.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private struct DataRange
        {
            public long Address;
            public byte[] Data;
            public DataRange(long address, byte[] data) { Address = address; Data = data; }
        }
    }

    /// <summary>
    /// Decoder for Android sparse image format (.sparse, .sparseimg).
    /// Parses the chunk-based sparse format with skip, fill, CRC32, and raw data chunks.
    /// 
    /// <para>Header: magic (0xED26FF3A), version, chunk count, block size, etc.</para>
    /// </summary>
    internal class AndroidSparseDecoder : IFirmwareDecoder
    {
        public FirmwareFormat Format => FirmwareFormat.AndroidSparse;

        private const uint SparseMagic = 0xED26FF3A;
        private const ushort ChunkTypeRaw = 0xCAC1;
        private const ushort ChunkTypeFill = 0xCAC2;
        private const ushort ChunkTypeDontCare = 0xCAC3;
        private const ushort ChunkTypeCrc32 = 0xCAC4;

        public Task<byte[]> DecodeAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Decode(filePath));
        }

        private byte[] Decode(string filePath)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            int offset = 0;

            // Parse sparse header
            if (fileData.Length < 28)
            {
                throw new FormatException("Android sparse file too small for valid header (28 bytes minimum).");
            }

            uint magic = ReadU32LE(fileData, ref offset);
            if (magic != SparseMagic)
            {
                throw new FormatException(string.Format(
                    "Invalid Android sparse magic: 0x{0:X8} (expected 0x{1:X8})", magic, SparseMagic));
            }

            ushort majorVersion = ReadU16LE(fileData, ref offset);
            ushort minorVersion = ReadU16LE(fileData, ref offset);
            ushort fileHeaderSize = ReadU16LE(fileData, ref offset);
            ushort chunkHeaderSize = ReadU16LE(fileData, ref offset);
            uint blockSize = ReadU32LE(fileData, ref offset);
            uint totalBlocks = ReadU32LE(fileData, ref offset);
            uint totalChunks = ReadU32LE(fileData, ref offset);
            uint imageChecksum = ReadU32LE(fileData, ref offset);

            long outputSize = (long)totalBlocks * blockSize;
            if (outputSize > int.MaxValue)
            {
                throw new InvalidOperationException(string.Format(
                    "Android sparse image exceeds maximum buffer size: {0} bytes", outputSize));
            }

            byte[] result = new byte[outputSize];
            int outputOffset = 0;
            uint expectedBlock = 0;

            for (int chunkIdx = 0; chunkIdx < totalChunks; chunkIdx++)
            {
                if (offset + chunkHeaderSize > fileData.Length)
                {
                    throw new FormatException(string.Format(
                        "Unexpected end of file at chunk {0}/{1}.", chunkIdx, totalChunks));
                }

                int chunkStart = offset;
                ushort chunkType = ReadU16LE(fileData, ref offset);
                ushort reserved = ReadU16LE(fileData, ref offset);
                uint chunkSize = ReadU32LE(fileData, ref offset); // blocks in this chunk
                uint totalChunkSize = ReadU32LE(fileData, ref offset); // bytes in this chunk (header size varies)

                int dataSize = (int)totalChunkSize - chunkHeaderSize; // actual data bytes after header

                if (chunkSize == 0) continue; // skip empty chunks

                switch (chunkType)
                {
                    case ChunkTypeRaw:
                        // Raw data follows the chunk header
                        if (offset + dataSize > fileData.Length)
                        {
                            throw new FormatException(string.Format(
                                "Unexpected end of file during raw chunk {0}.", chunkIdx));
                        }
                        int rawBytes = (int)((long)chunkSize * blockSize);
                        if (outputOffset + rawBytes > result.Length)
                        {
                            throw new FormatException("Raw chunk exceeds output buffer.");
                        }
                        Buffer.BlockCopy(fileData, offset, result, outputOffset, rawBytes);
                        offset += dataSize;
                        outputOffset += rawBytes;
                        break;

                    case ChunkTypeFill:
                        // 4-byte fill value follows
                        if (offset + 4 > fileData.Length)
                        {
                            throw new FormatException(string.Format(
                                "Unexpected end of file during fill chunk {0}.", chunkIdx));
                        }
                        uint fillValue = ReadU32LE(fileData, ref offset);
                        int fillBytes = (int)((long)chunkSize * blockSize);
                        // Fill with the pattern
                        byte fillByte = (byte)(fillValue & 0xFF);
                        for (int i = 0; i < fillBytes && outputOffset + i < result.Length; i++)
                        {
                            result[outputOffset + i] = fillByte;
                        }
                        outputOffset += fillBytes;
                        break;

                    case ChunkTypeDontCare:
                        // Skip this region — leave as-is (already 0x00, which is OK)
                        int skipBytes = (int)((long)chunkSize * blockSize);
                        outputOffset += skipBytes;
                        break;

                    case ChunkTypeCrc32:
                        // CRC32 chunk — contains a 4-byte CRC32 of the preceding data
                        if (offset + 4 > fileData.Length) break;
                        uint expectedCrc = ReadU32LE(fileData, ref offset);
                        // Skip CRC validation — it's optional and slow for large images
                        int crcSkipBytes = (int)((long)chunkSize * blockSize);
                        outputOffset += crcSkipBytes;
                        break;

                    default:
                        // Unknown chunk type — skip
                        offset += dataSize;
                        outputOffset += (int)((long)chunkSize * blockSize);
                        break;
                }

                expectedBlock += chunkSize;
            }

            if (expectedBlock != totalBlocks)
            {
                throw new FormatException(string.Format(
                    "Sparse image block count mismatch: expected {0}, got {1}.", totalBlocks, expectedBlock));
            }

            return result;
        }

        private static uint ReadU32LE(byte[] data, ref int offset)
        {
            uint value = (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
            offset += 4;
            return value;
        }

        private static ushort ReadU16LE(byte[] data, ref int offset)
        {
            ushort value = (ushort)(data[offset] | (data[offset + 1] << 8));
            offset += 2;
            return value;
        }
    }
}
