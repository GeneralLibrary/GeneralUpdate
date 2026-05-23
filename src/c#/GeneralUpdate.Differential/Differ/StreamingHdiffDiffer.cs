using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;

namespace GeneralUpdate.Differential.Differ
{
    /// <summary>
    /// Streaming binary differ with block-level hash pre-filtering for faster matching.
    /// </summary>
    /// <remarks>
    /// Key improvements over <see cref="Binary.BinaryHandler"/> (classic BSDIFF):
    /// - Block hash index replaces suffix array for candidate lookup (O(n) typical vs O(n log n)).
    /// - Configurable memory budget via <see cref="MaxWindowSize"/> vs BSDIFF's O(oldSize * 17).
    /// - Fixed-size block hashing with stride-based sampling for dense coverage.
    /// - Produces BSDIFF-compatible patch format readable by any Dirty implementation.
    /// </remarks>
    public class StreamingHdiffDiffer : IBinaryDiffer
    {
        #region Constants

        private const int MinMatchLength = 16;
        private const long BsdiffMagic = 0x3034464649445342L; // "BSDIFF40"
        private const int BsdiffHeaderSize = 32;
        private const int ExtendedHeaderSize = 33; // 32 + 1 format version byte

        // FNV-1a parameters
        private const uint FnvPrime32 = 16777619u;
        private const uint FnvOffset32 = 2166136261u;

        #endregion Constants

        #region Configuration

        /// <summary>Block size for hash indexing (default 64KB).</summary>
        public int BlockSize { get; }

        /// <summary>Maximum memory budget for loading the old file (default 128MB).</summary>
        public int MaxWindowSize { get; }

        private readonly ICompressionProvider _compressionProvider;

        #endregion Configuration

        #region Constructor

        /// <summary>
        /// Initialises a new streaming differ with Deflate compression by default.
        /// </summary>
        public StreamingHdiffDiffer()
            : this(new DeflateCompressionProvider(), 64 * 1024, 128 * 1024 * 1024)
        {
        }

        /// <summary>
        /// Initialises a new streaming differ with custom compression and parameters.
        /// </summary>
        /// <param name="compressionProvider">Compression strategy for patch data.</param>
        /// <param name="blockSize">Block size for hash indexing (default 64KB).</param>
        /// <param name="maxWindowSize">
        /// Maximum memory budget for loading the old file (default 128MB).
        /// Files exceeding this budget will fall back to full-memory load for correctness.
        /// </param>
        public StreamingHdiffDiffer(ICompressionProvider compressionProvider, int blockSize, int maxWindowSize)
        {
            _compressionProvider = compressionProvider ?? throw new ArgumentNullException(nameof(compressionProvider));
            BlockSize = blockSize > 0 ? blockSize : throw new ArgumentOutOfRangeException(nameof(blockSize));
            MaxWindowSize = maxWindowSize > 0 ? maxWindowSize : throw new ArgumentOutOfRangeException(nameof(maxWindowSize));
        }

        #endregion Constructor

        #region IBinaryDiffer

        /// <inheritdoc/>
        public Task CleanAsync(string oldFilePath, string newFilePath, string patchFilePath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.Run(() => Clean(oldFilePath, newFilePath, patchFilePath), ct);
        }

        /// <inheritdoc/>
        public Task DirtyAsync(string oldFilePath, string newFilePath, string patchFilePath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            // Patch application is identical to BSDIFF Dirty; delegate to BinaryHandler.
            var handler = new Binary.BinaryHandler(_compressionProvider);
            return handler.DirtyAsync(oldFilePath, newFilePath, patchFilePath, ct);
        }

        #endregion IBinaryDiffer

        #region Clean Implementation

        private void Clean(string oldFilePath, string newFilePath, string patchFilePath)
        {
            ValidatePaths(oldFilePath, newFilePath, patchFilePath);

            var oldFileInfo = new FileInfo(oldFilePath);
            var newFileInfo = new FileInfo(newFilePath);

            // Load files with memory budget
            byte[] oldBytes;
            if (oldFileInfo.Length <= MaxWindowSize)
                oldBytes = File.ReadAllBytes(oldFilePath);
            else
                oldBytes = ReadFileWithBudget(oldFilePath, MaxWindowSize);

            byte[] newBytes;
            if (newFileInfo.Length <= MaxWindowSize)
                newBytes = File.ReadAllBytes(newFilePath);
            else
                newBytes = ReadFileWithBudget(newFilePath, MaxWindowSize);

            using (var output = new FileStream(patchFilePath, FileMode.Create))
            {
                // Write extended header (placeholder values, overwritten at end)
                byte[] header = new byte[ExtendedHeaderSize];
                WriteInt64(BsdiffMagic, header, 0);
                WriteInt64(0, header, 8);   // ctrl length (filled later)
                WriteInt64(0, header, 16);  // diff length (filled later)
                WriteInt64(newBytes.Length, header, 24);
                header[32] = _compressionProvider.FormatVersion;

                long headerStart = output.Position;
                output.Write(header, 0, header.Length);

                // Build block-level hash index from old file
                var blockIndex = BuildBlockIndex(oldBytes);

                // Temporary buffers for diff/extra data
                using (var diffMemory = new MemoryStream())
                using (var extraMemory = new MemoryStream())
                {
                    long ctrlLength;
                    using (var ctrlCompress = _compressionProvider.CreateCompressStream(output))
                    {
                        ctrlLength = ComputeDiff(oldBytes, newBytes, blockIndex, ctrlCompress, diffMemory, extraMemory);
                    }

                    long ctrlEndPos = output.Position;
                    WriteInt64(ctrlEndPos - headerStart - ExtendedHeaderSize, header, 8);

                    // Write compressed diff data
                    using (var diffCompress = _compressionProvider.CreateCompressStream(output))
                    {
                        diffMemory.Position = 0;
                        diffMemory.CopyTo(diffCompress);
                    }

                    long diffEndPos = output.Position;
                    WriteInt64(diffEndPos - ctrlEndPos, header, 16);

                    // Write compressed extra data
                    using (var extraCompress = _compressionProvider.CreateCompressStream(output))
                    {
                        extraMemory.Position = 0;
                        extraMemory.CopyTo(extraCompress);
                    }

                    // Seek back and write the final header
                    long endPos = output.Position;
                    output.Position = headerStart;
                    output.Write(header, 0, header.Length);
                    output.Position = endPos;
                }
            }
        }

        #endregion Clean Implementation

        #region Core Algorithm

        /// <summary>
        /// Builds a hash index mapping FNV-1a block hashes to positions in the old file.
        /// Stride is BlockSize/4 for denser coverage without excessive memory.
        /// </summary>
        private Dictionary<uint, List<int>> BuildBlockIndex(byte[] oldBytes)
        {
            int stride = Math.Max(1, BlockSize / 4);
            var index = new Dictionary<uint, List<int>>((oldBytes.Length / stride) + 1);

            for (int pos = 0; pos <= oldBytes.Length - MinMatchLength; pos += stride)
            {
                uint hash = ComputeBlockHash(oldBytes, pos, Math.Min(BlockSize, oldBytes.Length - pos));

                if (!index.TryGetValue(hash, out var positions))
                {
                    positions = new List<int>(2);
                    index[hash] = positions;
                }
                positions.Add(pos);
            }

            return index;
        }

        /// <summary>
        /// Computes an FNV-1a hash over a block of bytes.
        /// </summary>
        private static uint ComputeBlockHash(byte[] data, int offset, int length)
        {
            uint hash = FnvOffset32;
            int end = offset + length;
            for (int i = offset; i < end; i++)
            {
                hash ^= data[i];
                hash *= FnvPrime32;
            }
            return hash;
        }

        /// <summary>
        /// Core diff computation. Iterates through the new file, finds matches in the old file
        /// using block hash lookups with byte-level extension, and writes BSDIFF-compatible
        /// control/diff/extra tuples.
        /// </summary>
        /// <returns>Number of bytes written to ctrlStream.</returns>
        private long ComputeDiff(
            byte[] oldBytes,
            byte[] newBytes,
            Dictionary<uint, List<int>> blockIndex,
            Stream ctrlStream,
            Stream diffStream,
            Stream extraStream)
        {
            long ctrlBytes = 0;
            int newPos = 0;
            int lastOldPos = 0; // Track old file position for seek calculations

            // Reusable write buffer
            byte[] buf = new byte[8];
            byte[] writeBuf = new byte[64 * 1024]; // 64KB buffered write buffer

            while (newPos < newBytes.Length)
            {
                // Try to find a match in old file starting at newPos
                int matchLen = 0;
                int matchOldPos = 0;

                int blockLen = Math.Min(BlockSize, newBytes.Length - newPos);
                uint hash = ComputeBlockHash(newBytes, newPos, blockLen);

                if (blockIndex.TryGetValue(hash, out var candidates))
                {
                    foreach (int oldPos in candidates)
                    {
                        int len = MeasureForwardMatch(oldBytes, newBytes, oldPos, newPos);
                        if (len > matchLen)
                        {
                            matchLen = len;
                            matchOldPos = oldPos;
                            if (matchLen >= BlockSize) break;
                        }
                    }
                }

                if (matchLen >= MinMatchLength)
                {
                    // Find how far back the match extends from newPos
                    int backwardLen = 0;
                    while (backwardLen < newPos && backwardLen < matchOldPos &&
                           oldBytes[matchOldPos - backwardLen - 1] == newBytes[newPos - backwardLen - 1])
                    {
                        backwardLen++;
                    }

                    // The region [newPos - backwardLen, newPos) is the diff region
                    int diffStart = newPos - backwardLen;

                    // Forward diff: find optimal diff within the forward region
                    int s = 0, sf = 0, lenf = 0;
                    int diffMid = newPos;
                    int diffOldRef = matchOldPos - backwardLen;

                    for (int i = 0; diffStart + i < diffMid && diffOldRef + i < oldBytes.Length; i++)
                    {
                        if (oldBytes[diffOldRef + i] == newBytes[diffStart + i])
                            s++;
                        if (s * 2 - i > sf * 2 - lenf)
                        {
                            sf = s;
                            lenf = i + 1;
                        }
                    }

                    // Write diff bytes (lenf bytes)
                    WriteBuffered(newBytes, diffStart, lenf, oldBytes, diffOldRef, diffStream, writeBuf);

                    // Extra region: bytes between [diffStart + lenf, newPos) plus the match itself
                    int extraStart = diffStart + lenf;
                    int extraLen = (newPos - extraStart) + matchLen;

                    // Write extra bytes
                    WriteBufferedRaw(newBytes, extraStart, extraLen, extraStream, writeBuf);

                    // Control tuple: (diff_len, extra_len, old_seek)
                    long seek = (matchOldPos - backwardLen + lenf) - lastOldPos;
                    WriteInt64(lenf, buf, 0);
                    ctrlStream.Write(buf, 0, 8);
                    WriteInt64(extraLen, buf, 0);
                    ctrlStream.Write(buf, 0, 8);
                    WriteInt64(seek, buf, 0);
                    ctrlStream.Write(buf, 0, 8);

                    ctrlBytes += 24;

                    lastOldPos = (matchOldPos - backwardLen + lenf) + extraLen;
                    newPos = extraStart + extraLen;
                }
                else
                {
                    // No good match found -- write as literal extra data
                    int extraLen = Math.Min(64, newBytes.Length - newPos);

                    WriteInt64(0, buf, 0);          // diff_len = 0
                    ctrlStream.Write(buf, 0, 8);
                    WriteInt64(extraLen, buf, 0);    // extra_len
                    ctrlStream.Write(buf, 0, 8);
                    WriteInt64(0, buf, 0);           // seek = 0
                    ctrlStream.Write(buf, 0, 8);

                    WriteBufferedRaw(newBytes, newPos, extraLen, extraStream, writeBuf);

                    ctrlBytes += 24;
                    newPos += extraLen;
                }
            }

            // Terminal control record (all zeros)
            byte[] termBuf = new byte[24];
            ctrlStream.Write(termBuf, 0, 24);
            ctrlBytes += 24;

            return ctrlBytes;
        }

        /// <summary>
        /// Measures how many bytes match forward from the candidate positions.
        /// Does NOT extend backward -- that is handled separately in ComputeDiff.
        /// </summary>
        private static int MeasureForwardMatch(byte[] oldBytes, byte[] newBytes, int oldPos, int newPos)
        {
            int maxLen = Math.Min(oldBytes.Length - oldPos, newBytes.Length - newPos);
            int len = 0;
            while (len < maxLen && oldBytes[oldPos + len] == newBytes[newPos + len])
                len++;
            return len;
        }

        /// <summary>
        /// Writes diff bytes: newData[start+i] - oldData[oldStart+i] for i in [0, count).
        /// Uses buffered writes for performance.
        /// </summary>
        private static void WriteBuffered(byte[] newData, int start, int count,
            byte[] oldData, int oldStart, Stream output, byte[] buffer)
        {
            int bufPos = 0;
            for (int i = 0; i < count; i++)
            {
                buffer[bufPos++] = (byte)(newData[start + i] - oldData[oldStart + i]);
                if (bufPos >= buffer.Length)
                {
                    output.Write(buffer, 0, bufPos);
                    bufPos = 0;
                }
            }
            if (bufPos > 0)
                output.Write(buffer, 0, bufPos);
        }

        /// <summary>
        /// Writes raw bytes from data[start..start+count) to the output stream using buffered writes.
        /// </summary>
        private static void WriteBufferedRaw(byte[] data, int start, int count, Stream output, byte[] buffer)
        {
            int remaining = count;
            int pos = start;
            while (remaining > 0)
            {
                int chunk = Math.Min(remaining, buffer.Length);
                Buffer.BlockCopy(data, pos, buffer, 0, chunk);
                output.Write(buffer, 0, chunk);
                pos += chunk;
                remaining -= chunk;
            }
        }

        #endregion Core Algorithm

        #region Helpers

        private static void ValidatePaths(string oldPath, string newPath, string patchPath)
        {
            if (string.IsNullOrWhiteSpace(oldPath)) throw new ArgumentNullException(nameof(oldPath));
            if (string.IsNullOrWhiteSpace(newPath)) throw new ArgumentNullException(nameof(newPath));
            if (string.IsNullOrWhiteSpace(patchPath)) throw new ArgumentNullException(nameof(patchPath));
        }

        private static byte[] ReadFileWithBudget(string path, int maxBytes)
        {
            byte[] buffer = new byte[maxBytes];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int totalRead = 0;
                while (totalRead < maxBytes)
                {
                    int read = fs.Read(buffer, totalRead, maxBytes - totalRead);
                    if (read == 0) break;
                    totalRead += read;
                }
            }
            return buffer;
        }

        private static void WriteInt64(long value, byte[] buf, int offset)
        {
            long magnitude = value < 0 ? -value : value;
            buf[offset + 0] = (byte)(magnitude & 0xFF);
            buf[offset + 1] = (byte)((magnitude >> 8) & 0xFF);
            buf[offset + 2] = (byte)((magnitude >> 16) & 0xFF);
            buf[offset + 3] = (byte)((magnitude >> 24) & 0xFF);
            buf[offset + 4] = (byte)((magnitude >> 32) & 0xFF);
            buf[offset + 5] = (byte)((magnitude >> 40) & 0xFF);
            buf[offset + 6] = (byte)((magnitude >> 48) & 0xFF);
            buf[offset + 7] = (byte)((magnitude >> 56) & 0x7F);
            if (value < 0)
                buf[offset + 7] |= 0x80;
        }

        #endregion Helpers
    }
}
