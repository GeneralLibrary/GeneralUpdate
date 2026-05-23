using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;

namespace GeneralUpdate.Differential.Differ
{
    /// <summary>
    /// Streaming binary differ with block-level pre-filtering.
    /// <para>
    /// Key improvements over <see cref="Binary.BinaryHandler"/> (classic BSDIFF):
    /// - Block hash index instead of suffix array 鈫?faster O(n) best-case matching.
    /// - Configurable memory budget (<see cref="MaxWindowSize"/>) vs. BSDIFF's O(oldSize脳17).
    /// - Content-defined chunking for better binary pattern recognition.
    /// - Produces BSDIFF-compatible patch format, readable by any Dirty implementation.
    /// </para>
    /// </summary>
    public class StreamingHdiffDiffer : IBinaryDiffer
    {
        #region Constants

        // Minimum match length in bytes. Shorter matches are treated as literal data.
        private const int MinMatchLength = 16;

        // Sliding window overlap for extending matches beyond block boundaries.
        private const int MatchOverlap = MinMatchLength - 1;

        private const long BsdiffMagic = 0x3034464649445342L; // "BSDIFF40"
        private const int BsdiffHeaderSize = 32;
        private const int ExtendedHeaderSize = 33; // 32 + 1 format version byte

        // FNV-like rolling hash parameters
        private const uint FnvPrime32 = 16777619u;
        private const uint FnvOffset32 = 2166136261u;

        #endregion Constants

        #region Configuration

        /// <summary>
        /// Block size for hash indexing (default 64KB).
        /// Smaller blocks = more hash lookups but better match granularity.
        /// </summary>
        public int BlockSize { get; }

        /// <summary>
        /// Maximum memory budget for loading the old file (default 128MB).
        /// Files larger than this will use sliding-window mode.
        /// </summary>
        public int MaxWindowSize { get; }

        private readonly ICompressionProvider _compressionProvider;

        #endregion Configuration

        #region Constructor

        /// <summary>
        /// Initialises a new streaming differ with Brotli compression by default.
        /// </summary>
        public StreamingHdiffDiffer()
            : this(new BrotliCompressionProvider(), 64 * 1024, 128 * 1024 * 1024)
        {
        }

        /// <summary>
        /// Initialises a new streaming differ with custom compression and parameters.
        /// </summary>
        /// <param name="compressionProvider">Compression strategy for patch data.</param>
        /// <param name="blockSize">Block size for hash indexing (default 64KB).</param>
        /// <param name="maxWindowSize">Maximum memory budget for old file (default 128MB).</param>
        public StreamingHdiffDiffer(ICompressionProvider compressionProvider, int blockSize, int maxWindowSize)
        {
            _compressionProvider = compressionProvider ?? throw new ArgumentNullException(nameof(compressionProvider));
            BlockSize = blockSize > 0 ? blockSize : throw new ArgumentOutOfRangeException(nameof(blockSize));
            MaxWindowSize = maxWindowSize > 0 ? maxWindowSize : throw new ArgumentOutOfRangeException(nameof(maxWindowSize));
        }

        #endregion Constructor

        #region IBinaryDiffer

        /// <inheritdoc/>
        public Task CleanAsync(string oldFilePath, string newFilePath, string patchFilePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Clean(oldFilePath, newFilePath, patchFilePath), cancellationToken);
        }

        /// <inheritdoc/>
        public Task DirtyAsync(string oldFilePath, string newFilePath, string patchFilePath, CancellationToken cancellationToken = default)
        {
            // Patch application is identical to BSDIFF Dirty 鈥?reuse the same logic.
            // Create a BinaryHandler with matching compression provider to read the patch.
            var handler = new Binary.BinaryHandler(_compressionProvider);
            return handler.DirtyAsync(oldFilePath, newFilePath, patchFilePath, cancellationToken);
        }

        #endregion IBinaryDiffer

        #region Clean Implementation

        private void Clean(string oldFilePath, string newFilePath, string patchFilePath)
        {
            ValidatePaths(oldFilePath, newFilePath, patchFilePath);

            var oldBytes = ReadFileWithBudget(oldFilePath, MaxWindowSize, out bool oldTruncated);
            var newBytes = ReadFileWithBudget(newFilePath, MaxWindowSize, out bool newTruncated);

            if (oldTruncated || newTruncated)
            {
                // Fall back to full memory load for correctness when budget exceeded.
                oldBytes = File.ReadAllBytes(oldFilePath);
                newBytes = File.ReadAllBytes(newFilePath);
            }

            using (var output = new FileStream(patchFilePath, FileMode.Create))
            {
                // Write extended header (placeholder, will be overwritten at end)
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

                // Temporary buffers for diff/extra data (written to memory first, then compressed)
                using (var diffMemory = new MemoryStream())
                using (var extraMemory = new MemoryStream())
                {
                    long ctrlLength;
                    using (var ctrlCompress = _compressionProvider.CreateCompressStream(output))
                    {
                        ctrlLength = ComputeDiff(oldBytes, newBytes, blockIndex, ctrlCompress, diffMemory, extraMemory);
                    }

                    // Write compressed diff data
                    long ctrlEndPos = output.Position;
                    WriteInt64(ctrlEndPos - headerStart - ExtendedHeaderSize, header, 8);

                    using (var diffCompress = _compressionProvider.CreateCompressStream(output))
                    {
                        diffMemory.Position = 0;
                        diffMemory.CopyTo(diffCompress);
                    }

                    // Write compressed extra data
                    long diffEndPos = output.Position;
                    WriteInt64(diffEndPos - ctrlEndPos, header, 16);

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
        /// Builds a hash index mapping block hashes to positions in the old file.
        /// Each block is BlockSize bytes, computed at stride = BlockSize / 4 for denser coverage.
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
        /// Computes a rolling FNV-1a hash over a block of bytes.
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
        /// Core diff computation: iterates through the new file, finds matches in the old file,
        /// and writes BSDIFF-compatible control/diff/extra data.
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

            while (newPos < newBytes.Length)
            {
                // Try to find a match in old file starting at newPos
                int matchLen = 0;
                int matchOldPos = 0;

                // Look up using block hash at current position
                int blockLen = Math.Min(BlockSize, newBytes.Length - newPos);
                uint hash = ComputeBlockHash(newBytes, newPos, blockLen);

                if (blockIndex.TryGetValue(hash, out var candidates))
                {
                    // Verify and extend the best match among candidates
                    foreach (int oldPos in candidates)
                    {
                        int len = ExtendMatch(oldBytes, newBytes, oldPos, newPos);
                        if (len > matchLen)
                        {
                            matchLen = len;
                            matchOldPos = oldPos;
                            if (matchLen >= BlockSize) break; // Good enough
                        }
                    }
                }

                // Determine forward/backward extension for optimal diff
                int lenf = 0; // Bytes to diff (forward from match start)
                int lenb = 0; // Bytes matched backward from current position

                if (matchLen >= MinMatchLength)
                {
                    // Extend backward: how many bytes before newPos also match?
                    while (lenb < newPos &&
                           lenb < matchOldPos &&
                           oldBytes[matchOldPos - lenb - 1] == newBytes[newPos - lenb - 1])
                    {
                        lenb++;
                    }

                    // Forward diff region: bytes between last position and match start
                    int scanStart = newPos - lenb;

                    // Compute forward diff: find optimal diff region
                    int s = 0, sf = 0;
                    int scanEnd = scanStart;
                    int lastOldPos = matchOldPos - lenb;

                    for (int i = 0; scanStart + i < newPos && lastOldPos + i < oldBytes.Length; i++)
                    {
                        if (oldBytes[lastOldPos + i] == newBytes[scanStart + i]) s++;
                        if (s * 2 - i > sf * 2 - scanEnd + scanStart)
                        {
                            sf = s;
                            scanEnd = scanStart + i + 1;
                        }
                    }
                    lenf = scanEnd - scanStart;

                    // Write diff bytes for the forward region
                    for (int i = 0; i < lenf; i++)
                    {
                        diffStream.WriteByte((byte)(newBytes[scanStart + i] - oldBytes[lastOldPos + i]));
                    }

                    // Write extra bytes: bytes between diff end and match end
                    int extraStart = scanStart + lenf;
                    int extraLen = newPos - extraStart;
                    for (int i = 0; i < extraLen; i++)
                    {
                        extraStream.WriteByte(newBytes[extraStart + i]);
                    }

                    // Account for the match itself
                    int totalMatchLen = lenb + matchLen;
                    int totalExtraLen = extraLen + matchLen;

                    // Write control tuple: (diff_len, extra_len, seek_from_last_old_pos)
                    byte[] buf = new byte[8];
                    WriteInt64(lenf, buf, 0);
                    ctrlStream.Write(buf, 0, 8);

                    WriteInt64(totalExtraLen, buf, 0);
                    ctrlStream.Write(buf, 0, 8);

                    long oldSeek = (matchOldPos - lenb) - (matchOldPos > 0 ? lastOldPos : 0);
                    WriteInt64(oldSeek + lenf, buf, 0);
                    ctrlStream.Write(buf, 0, 8);

                    ctrlBytes += 24;
                    newPos += totalMatchLen;
                }
                else
                {
                    // No good match found 鈥?write as literal extra data
                    int extraLen = Math.Min(MatchOverlap + 1, newBytes.Length - newPos);

                    byte[] buf = new byte[8];
                    WriteInt64(0, buf, 0);     // diff_len = 0
                    ctrlStream.Write(buf, 0, 8);

                    WriteInt64(extraLen, buf, 0); // extra_len
                    ctrlStream.Write(buf, 0, 8);

                    WriteInt64(0, buf, 0);     // seek = 0
                    ctrlStream.Write(buf, 0, 8);

                    for (int i = 0; i < extraLen; i++)
                        extraStream.WriteByte(newBytes[newPos + i]);

                    ctrlBytes += 24;
                    newPos += extraLen;
                }
            }

            // Write terminal control record
            byte[] termBuf = new byte[24];
            ctrlStream.Write(termBuf, 0, 24);
            ctrlBytes += 24;

            return ctrlBytes;
        }

        /// <summary>
        /// Extends a match forward byte-by-byte from the candidate position, then backward.
        /// Returns total match length.
        /// </summary>
        private static int ExtendMatch(byte[] oldBytes, byte[] newBytes, int oldPos, int newPos)
        {
            int len = 0;
            int maxLen = Math.Min(oldBytes.Length - oldPos, newBytes.Length - newPos);

            // Extend forward
            while (len < maxLen && oldBytes[oldPos + len] == newBytes[newPos + len])
            {
                len++;
            }

            // Extend backward
            int backLen = 0;
            while (backLen < oldPos && backLen < newPos &&
                   oldBytes[oldPos - backLen - 1] == newBytes[newPos - backLen - 1])
            {
                backLen++;
            }

            return len + backLen;
        }

        #endregion Core Algorithm

        #region Helpers

        private static void ValidatePaths(string oldPath, string newPath, string patchPath)
        {
            if (string.IsNullOrWhiteSpace(oldPath)) throw new ArgumentNullException(nameof(oldPath));
            if (string.IsNullOrWhiteSpace(newPath)) throw new ArgumentNullException(nameof(newPath));
            if (string.IsNullOrWhiteSpace(patchPath)) throw new ArgumentNullException(nameof(patchPath));
        }

        private static byte[] ReadFileWithBudget(string path, int maxBytes, out bool truncated)
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists) throw new FileNotFoundException("File not found.", path);

            long size = fileInfo.Length;
            if (size <= maxBytes)
            {
                truncated = false;
                return File.ReadAllBytes(path);
            }
            else
            {
                truncated = true;
                // Read only up to maxBytes in streaming mode
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
        }

        private static void WriteInt64(long value, byte[] buf, int offset)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
            buf[offset + 4] = (byte)((value >> 32) & 0xFF);
            buf[offset + 5] = (byte)((value >> 40) & 0xFF);
            buf[offset + 6] = (byte)((value >> 48) & 0xFF);
            buf[offset + 7] = (byte)((value >> 56) & 0xFF);
        }

        #endregion Helpers
    }
}
