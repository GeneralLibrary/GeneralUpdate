using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;

namespace GeneralUpdate.Differential.Differ
{
    /// <summary>
    /// BSDIFF 4.0 file binary differential algorithm.
    /// Implements <see cref="IBinaryDiffer"/> for pluggable architecture compatibility.
    /// </summary>
    /// <remarks>
    /// Supports pluggable compression via <see cref="ICompressionProvider"/>.
    /// Default: <see cref="BZip2CompressionProvider"/> (backward compatible).
    /// Use <see cref="DeflateCompressionProvider"/> (0x01) for faster decompression.
    ///
    /// Thread-safety: this class is stateless beyond the compression provider.
    /// A single instance is safe for concurrent calls.
    /// </remarks>
    public class BsdiffDiffer : IBinaryDiffer
    {
        #region Private Members

        private const long FileSignature = 0x3034464649445342L; // "BSDIFF40"
        private const int HeaderSize = 32;
        // Extended header adds 1 byte for compression format version after the 32-byte BSDIFF header.
        // Legacy patches have exactly 32-byte headers -> treated as BZip2 (0x00).
        // New patches have 33-byte headers -> the 33rd byte selects compression: 0x00=BZip2, 0x01=Brotli.
        private const int ExtendedHeaderSize = 33;

        private readonly ICompressionProvider _compressionProvider;

        #endregion Private Members

        #region Constructors

        /// <summary>
        /// Initialises a new BsdiffDiffer with BZip2 compression (backward compatible).
        /// </summary>
        public BsdiffDiffer()
            : this(new BZip2CompressionProvider())
        {
        }

        /// <summary>
        /// Initialises a new BsdiffDiffer with the specified compression provider.
        /// </summary>
        /// <param name="compressionProvider">The compression strategy to use for patch data.</param>
        public BsdiffDiffer(ICompressionProvider compressionProvider)
        {
            _compressionProvider = compressionProvider ?? throw new ArgumentNullException(nameof(compressionProvider));
        }

        #endregion Constructors

        #region IBinaryDiffer Implementation

        /// <inheritdoc/>
        public Task CleanAsync(string oldFilePath, string newFilePath, string patchFilePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Clean(oldFilePath, newFilePath, patchFilePath);
        }

        /// <inheritdoc/>
        public Task DirtyAsync(string oldFilePath, string newFilePath, string patchFilePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Dirty(oldFilePath, newFilePath, patchFilePath);
        }

        #endregion IBinaryDiffer Implementation

        #region Public Methods

        /// <summary>
        /// Generates a BSDIFF patch from oldfilePath to patchPath.
        /// </summary>
        public async Task Clean(string oldfilePath, string newfilePath, string patchPath)
        {
            await Task.Run(() =>
            {
                ValidationParameters(oldfilePath, newfilePath, patchPath);

                using (var output = new FileStream(patchPath, FileMode.Create))
                {
                    var oldBytes = File.ReadAllBytes(oldfilePath);
                    var newBytes = File.ReadAllBytes(newfilePath);

                    // Header layout:
                    //   0    8   "BSDIFF40" (magic)
                    //   8    8   length of compressed ctrl block
                    //  16    8   length of compressed diff block
                    //  24    8   length of new file
                    //  32    1   compression format version (0x00=BZip2, 0x01=Brotli)
                    byte[] header = new byte[ExtendedHeaderSize];
                    WriteInt64(FileSignature, header, 0);
                    WriteInt64(0, header, 8);
                    WriteInt64(0, header, 16);
                    WriteInt64(newBytes.Length, header, 24);
                    header[32] = _compressionProvider.FormatVersion;

                    long startPosition = output.Position;
                    output.Write(header, 0, header.Length);

                    int[] I = SuffixSort(oldBytes);

                    byte[] db = new byte[newBytes.Length];
                    byte[] eb = new byte[newBytes.Length];

                    int dblen = 0;
                    int eblen = 0;

                    using (var compressStream = _compressionProvider.CreateCompressStream(output))
                    {
                        // Compute the differences, writing ctrl as we go
                        int scan = 0;
                        int pos = 0;
                        int len = 0;
                        int lastscan = 0;
                        int lastpos = 0;
                        int lastoffset = 0;
                        while (scan < newBytes.Length)
                        {
                            int oldscore = 0;

                            for (int scsc = scan += len; scan < newBytes.Length; scan++)
                            {
                                len = Search(I, oldBytes, newBytes, scan, 0, oldBytes.Length, out pos);

                                for (; scsc < scan + len; scsc++)
                                {
                                    if ((scsc + lastoffset < oldBytes.Length) &&
                                        (oldBytes[scsc + lastoffset] == newBytes[scsc]))
                                        oldscore++;
                                }

                                if ((len == oldscore && len != 0) || (len > oldscore + 8))
                                    break;

                                if ((scan + lastoffset < oldBytes.Length) &&
                                    (oldBytes[scan + lastoffset] == newBytes[scan]))
                                    oldscore--;
                            }

                            if (len != oldscore || scan == newBytes.Length)
                            {
                                int s = 0;
                                int sf = 0;
                                int lenf = 0;
                                for (int i = 0; (lastscan + i < scan) && (lastpos + i < oldBytes.Length);)
                                {
                                    if (oldBytes[lastpos + i] == newBytes[lastscan + i])
                                        s++;
                                    i++;
                                    if (s * 2 - i > sf * 2 - lenf)
                                    {
                                        sf = s;
                                        lenf = i;
                                    }
                                }

                                int lenb = 0;
                                if (scan < newBytes.Length)
                                {
                                    s = 0;
                                    int sb = 0;
                                    for (int i = 1; (scan >= lastscan + i) && (pos >= i); i++)
                                    {
                                        if (oldBytes[pos - i] == newBytes[scan - i])
                                            s++;
                                        if (s * 2 - i > sb * 2 - lenb)
                                        {
                                            sb = s;
                                            lenb = i;
                                        }
                                    }
                                }

                                if (lastscan + lenf > scan - lenb)
                                {
                                    int overlap = (lastscan + lenf) - (scan - lenb);
                                    s = 0;
                                    int ss = 0;
                                    int lens = 0;
                                    for (int i = 0; i < overlap; i++)
                                    {
                                        if (newBytes[lastscan + lenf - overlap + i] ==
                                            oldBytes[lastpos + lenf - overlap + i])
                                            s++;
                                        if (newBytes[scan - lenb + i] == oldBytes[pos - lenb + i])
                                            s--;
                                        if (s > ss)
                                        {
                                            ss = s;
                                            lens = i + 1;
                                        }
                                    }

                                    lenf += lens - overlap;
                                    lenb -= lens;
                                }

                                for (int i = 0; i < lenf; i++)
                                    db[dblen + i] = (byte)(newBytes[lastscan + i] - oldBytes[lastpos + i]);
                                for (int i = 0; i < (scan - lenb) - (lastscan + lenf); i++)
                                    eb[eblen + i] = newBytes[lastscan + lenf + i];

                                dblen += lenf;
                                eblen += (scan - lenb) - (lastscan + lenf);

                                byte[] buf = new byte[8];
                                WriteInt64(lenf, buf, 0);
                                compressStream.Write(buf, 0, 8);

                                WriteInt64((scan - lenb) - (lastscan + lenf), buf, 0);
                                compressStream.Write(buf, 0, 8);

                                WriteInt64((pos - lenb) - (lastpos + lenf), buf, 0);
                                compressStream.Write(buf, 0, 8);

                                lastscan = scan - lenb;
                                lastpos = pos - lenb;
                                lastoffset = pos - scan;
                            }
                        }
                    }

                    // Compute size of compressed ctrl data
                    long controlEndPosition = output.Position;
                    WriteInt64(controlEndPosition - startPosition - ExtendedHeaderSize, header, 8);

                    // Write compressed diff data
                    using (var compressStream = _compressionProvider.CreateCompressStream(output))
                    {
                        compressStream.Write(db, 0, dblen);
                    }

                    // Compute size of compressed diff data
                    long diffEndPosition = output.Position;
                    WriteInt64(diffEndPosition - controlEndPosition, header, 16);

                    // Write compressed extra data
                    using (var compressStream = _compressionProvider.CreateCompressStream(output))
                    {
                        compressStream.Write(eb, 0, eblen);
                    }

                    // Seek to the beginning, write the header, then seek back to end
                    long endPosition = output.Position;
                    output.Position = startPosition;
                    output.Write(header, 0, header.Length);
                    output.Position = endPosition;
                }
            });
        }

        /// <summary>
        /// Applies a BSDIFF patch to produce the new file.
        /// Supports both legacy (32-byte header) and extended (33-byte header) patches.
        /// </summary>
        public async Task Dirty(string oldfilePath, string newfilePath, string patchPath)
        {
            await Task.Run(() =>
            {
                ValidationParameters(oldfilePath, newfilePath, patchPath);

                using (var input = new FileStream(oldfilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(newfilePath, FileMode.Create))
                {
                    long controlLength, diffLength, newSize;
                    byte formatVersion;
                    int actualHeaderSize;

                    // Read and detect header format
                    using (var patchStream = OpenPatchStream(patchPath))
                    {
                        if (!patchStream.CanRead)
                            throw new ArgumentException("Patch stream must be readable.", nameof(patchPath));
                        if (!patchStream.CanSeek)
                            throw new ArgumentException("Patch stream must be seekable.", nameof(patchPath));

                        // Read 32-byte BSDIFF header
                        byte[] header = ReadExactly(patchStream, HeaderSize);

                        // Check magic
                        long signature = ReadInt64(header, 0);
                        if (signature != FileSignature)
                            throw new InvalidOperationException("Corrupt patch: invalid magic signature.");

                        // Read lengths from header
                        controlLength = ReadInt64(header, 8);
                        diffLength = ReadInt64(header, 16);
                        newSize = ReadInt64(header, 24);
                        if (controlLength < 0 || diffLength < 0 || newSize < 0)
                            throw new InvalidOperationException("Corrupt patch: negative length in header.");

                        // Detect compression format version:
                        // Peek the byte at offset 32. If it looks like a valid format version
                        // (0x00 or 0x01), treat it as an extended header. Otherwise, it is a
                        // legacy patch and the byte is the start of compressed data.
                        if (patchStream.Position < patchStream.Length)
                        {
                            byte[] formatByte = ReadExactly(patchStream, 1);
                            byte candidate = formatByte[0];

                            if (candidate == BZip2FormatVersion || candidate == DeflateFormatVersion)
                            {
                                formatVersion = candidate;
                                actualHeaderSize = ExtendedHeaderSize;
                            }
                            else
                            {
                                // Legacy patch: byte 32 is compressed data, not a format marker.
                                formatVersion = BZip2FormatVersion;
                                actualHeaderSize = HeaderSize;
                            }
                        }
                        else
                        {
                            // File ends exactly at 32 bytes (degenerate case).
                            formatVersion = BZip2FormatVersion;
                            actualHeaderSize = HeaderSize;
                        }
                    }

                    // Select decompression provider based on detected format version
                    ICompressionProvider decompressionProvider = formatVersion switch
                    {
                        BZip2FormatVersion => new BZip2CompressionProvider(),
                        DeflateFormatVersion => new DeflateCompressionProvider(),
                        _ => throw new InvalidOperationException(
                            $"Unsupported patch compression format version: 0x{formatVersion:X2}")
                    };

                    const int c_bufferSize = 1048576;
                    byte[] newData = new byte[c_bufferSize];
                    byte[] oldData = new byte[c_bufferSize];

                    // Read three parts of the patch in parallel
                    using (var compressedControlStream = OpenPatchStream(patchPath))
                    using (var compressedDiffStream = OpenPatchStream(patchPath))
                    using (var compressedExtraStream = OpenPatchStream(patchPath))
                    {
                        // Seek to the start of each part (using the correct header size)
                        compressedControlStream.Seek(actualHeaderSize, SeekOrigin.Current);
                        compressedDiffStream.Seek(actualHeaderSize + controlLength, SeekOrigin.Current);
                        compressedExtraStream.Seek(actualHeaderSize + controlLength + diffLength, SeekOrigin.Current);

                        // Decompress each part
                        using (var controlStream = decompressionProvider.CreateDecompressStream(compressedControlStream))
                        using (var diffStream = decompressionProvider.CreateDecompressStream(compressedDiffStream))
                        using (var extraStream = decompressionProvider.CreateDecompressStream(compressedExtraStream))
                        {
                            long[] control = new long[3];
                            byte[] buffer = new byte[8];

                            int oldPosition = 0;
                            int newPosition = 0;

                            while (newPosition < newSize)
                            {
                                // Read control data
                                for (int i = 0; i < 3; i++)
                                {
                                    ReadExactly(controlStream, buffer, 0, 8);
                                    control[i] = ReadInt64(buffer, 0);
                                }

                                // Sanity-check
                                if (newPosition + control[0] > newSize)
                                    throw new InvalidOperationException("Corrupt patch: diff data exceeds new file size.");

                                // Seek old file to the position that the new data is diffed against
                                input.Position = oldPosition;

                                int bytesToCopy = (int)control[0];
                                while (bytesToCopy > 0)
                                {
                                    int actualBytesToCopy = Math.Min(bytesToCopy, c_bufferSize);

                                    // Read diff string
                                    ReadExactly(diffStream, newData, 0, actualBytesToCopy);

                                    // Add old data to diff string
                                    int availableInputBytes = Math.Min(actualBytesToCopy,
                                        (int)(input.Length - input.Position));
                                    ReadExactly(input, oldData, 0, availableInputBytes);

                                    for (int index = 0; index < availableInputBytes; index++)
                                        newData[index] += oldData[index];

                                    output.Write(newData, 0, actualBytesToCopy);

                                    // Adjust counters
                                    newPosition += actualBytesToCopy;
                                    oldPosition += actualBytesToCopy;
                                    bytesToCopy -= actualBytesToCopy;
                                }

                                // Sanity-check
                                if (newPosition + control[1] > newSize)
                                    throw new InvalidOperationException("Corrupt patch: extra data exceeds new file size.");

                                // Read extra string
                                bytesToCopy = (int)control[1];
                                while (bytesToCopy > 0)
                                {
                                    int actualBytesToCopy = Math.Min(bytesToCopy, c_bufferSize);

                                    ReadExactly(extraStream, newData, 0, actualBytesToCopy);
                                    output.Write(newData, 0, actualBytesToCopy);

                                    newPosition += actualBytesToCopy;
                                    bytesToCopy -= actualBytesToCopy;
                                }

                                // Adjust position in old file
                                oldPosition = (int)(oldPosition + control[2]);
                            }
                        }
                    }
                }

                // Atomic replacement (if needed) is handled by the caller
                // (e.g. DefaultDirtyStrategy.ApplyPatch).
            });
        }

        #endregion Public Methods

        #region Private Methods

        private const byte BZip2FormatVersion = 0x00;
        private const byte DeflateFormatVersion = 0x01;

        private static FileStream OpenPatchStream(string patchPath)
        {
            return new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static void ValidationParameters(string oldfilePath, string newfilePath, string patchPath)
        {
            if (string.IsNullOrWhiteSpace(oldfilePath))
                throw new ArgumentNullException(nameof(oldfilePath), "Old file path must not be empty.");
            if (string.IsNullOrWhiteSpace(newfilePath))
                throw new ArgumentNullException(nameof(newfilePath), "New file path must not be empty.");
            if (string.IsNullOrWhiteSpace(patchPath))
                throw new ArgumentNullException(nameof(patchPath), "Patch path must not be empty.");
        }

        private static int CompareBytes(byte[] left, int leftOffset, byte[] right, int rightOffset)
        {
            for (int index = 0; index < left.Length - leftOffset && index < right.Length - rightOffset; index++)
            {
                int diff = left[index + leftOffset] - right[index + rightOffset];
                if (diff != 0) return diff;
            }
            return 0;
        }

        private static int MatchLength(byte[] oldData, int oldOffset, byte[] newData, int newOffset)
        {
            int i;
            for (i = 0; i < oldData.Length - oldOffset && i < newData.Length - newOffset; i++)
            {
                if (oldData[i + oldOffset] != newData[i + newOffset]) break;
            }
            return i;
        }

        private static int Search(int[] I, byte[] oldData, byte[] newData, int newOffset, int start, int end, out int pos)
        {
            if (end - start < 2)
            {
                int x = MatchLength(oldData, I[start], newData, newOffset);
                int y = MatchLength(oldData, I[end], newData, newOffset);

                if (x > y)
                {
                    pos = I[start];
                    return x;
                }
                else
                {
                    pos = I[end];
                    return y;
                }
            }

            int mid = start + (end - start) / 2;
            if (CompareBytes(oldData, I[mid], newData, newOffset) < 0)
            {
                return Search(I, oldData, newData, newOffset, mid, end, out pos);
            }
            else
            {
                return Search(I, oldData, newData, newOffset, start, mid, out pos);
            }
        }

        private static void Split(int[] I, int[] v, int start, int len, int h)
        {
            if (len < 16)
            {
                int j;
                for (int k = start; k < start + len; k += j)
                {
                    j = 1;
                    int x = v[I[k] + h];
                    for (int i = 1; k + i < start + len; i++)
                    {
                        if (v[I[k + i] + h] < x)
                        {
                            x = v[I[k + i] + h];
                            j = 0;
                        }
                        if (v[I[k + i] + h] == x)
                        {
                            int tmp = I[k + j];
                            I[k + j] = I[k + i];
                            I[k + i] = tmp;
                            j++;
                        }
                    }
                    for (int i = 0; i < j; i++)
                        v[I[k + i]] = k + j - 1;
                    if (j == 1)
                        I[k] = -1;
                }
                return;
            }

            int x2 = v[I[start + len / 2] + h];
            int jj = 0;
            int kk = 0;
            for (int i2 = 0; i2 < len; i2++)
            {
                if (v[I[start + i2] + h] < x2) jj++;
                if (v[I[start + i2] + h] == x2) kk++;
            }
            jj += start;
            kk += jj;

            int i3 = start;
            int j3 = 0;
            int k3 = 0;
            while (i3 < jj)
            {
                if (v[I[i3] + h] < x2)
                {
                    i3++;
                }
                else if (v[I[i3] + h] == x2)
                {
                    int tmp = I[i3];
                    I[i3] = I[jj + j3];
                    I[jj + j3] = tmp;
                    j3++;
                }
                else
                {
                    int tmp = I[i3];
                    I[i3] = I[kk + k3];
                    I[kk + k3] = tmp;
                    k3++;
                }
            }

            while (jj + j3 < kk)
            {
                if (v[I[jj + j3] + h] == x2)
                {
                    j3++;
                }
                else
                {
                    int tmp = I[jj + j3];
                    I[jj + j3] = I[kk + k3];
                    I[kk + k3] = tmp;
                    k3++;
                }
            }

            if (jj > start)
                Split(I, v, start, jj - start, h);

            for (int i2 = 0; i2 < kk - jj; i2++)
                v[I[jj + i2]] = kk - 1;
            if (jj == kk - 1)
                I[jj] = -1;

            if (start + len > kk)
                Split(I, v, kk, start + len - kk, h);
        }

        private static int[] SuffixSort(byte[] oldData)
        {
            // Empty input: no suffixes to sort, return sentinel-only array.
            if (oldData.Length == 0)
                return new int[1] { 0 };

            var buckets = new int[256];

            for (int i = 0; i < oldData.Length; i++)
                buckets[oldData[i]]++;
            for (int i = 1; i < 256; i++)
                buckets[i] += buckets[i - 1];
            for (int i = 255; i > 0; i--)
                buckets[i] = buckets[i - 1];
            buckets[0] = 0;

            var I = new int[oldData.Length + 1];
            for (int i = 0; i < oldData.Length; i++)
                I[++buckets[oldData[i]]] = i;

            // Allocate extra space for h-doubling access pattern in Split.
            // During suffix sort, v[I[k] + h] is accessed where h doubles
            // from 1 each iteration. I[k] is in [0, oldsize] and h can reach
            // the next power of 2 >= oldsize (e.g. oldsize=3 -> h reaches 4).
            // The original C BSDIFF relies on undefined behaviour here;
            // in managed code we must size the buffer to oldsize + maxH + 1.
            int maxH = 1;
            while (maxH < oldData.Length)
                maxH <<= 1;
            var v = new int[oldData.Length + maxH + 1];
            for (int i = 0; i < oldData.Length; i++)
                v[i] = buckets[oldData[i]];

            for (int i = 1; i < 256; i++)
            {
                if (buckets[i] == buckets[i - 1] + 1)
                {
                    I[buckets[i]] = -1;
                }
            }

            I[0] = oldData.Length;
            v[oldData.Length] = 0;

            for (int h = 1; I[0] != -(oldData.Length + 1); h += h)
            {
                int len = 0;
                int i = 0;
                while (i < oldData.Length + 1)
                {
                    if (I[i] < 0)
                    {
                        len -= I[i];
                        i -= I[i];
                    }
                    else
                    {
                        if (len != 0)
                            I[i - len] = -len;
                        len = v[I[i]] + 1 - i;
                        Split(I, v, i, len, h);
                        i += len;
                        len = 0;
                    }
                }
                if (len != 0)
                    I[i - len] = -len;
            }

            for (int i = 0; i < oldData.Length + 1; i++)
                I[v[i]] = i;

            return I;
        }

        /// <summary>
        /// Writes a 64-bit signed integer in BSDIFF sign-magnitude encoding.
        /// Bytes 0-6: magnitude bits 0-55. Byte 7 lower 7 bits: magnitude bits 56-62.
        /// Byte 7 upper bit: sign flag (1 = negative).
        /// </summary>
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

        /// <summary>
        /// Reads a 64-bit signed integer in BSDIFF sign-magnitude encoding.
        /// Inverse of <see cref="WriteInt64"/>.
        /// </summary>
        private static long ReadInt64(byte[] buf, int offset)
        {
            long value =
                ((long)buf[offset + 0]) |
                ((long)buf[offset + 1] << 8) |
                ((long)buf[offset + 2] << 16) |
                ((long)buf[offset + 3] << 24) |
                ((long)buf[offset + 4] << 32) |
                ((long)buf[offset + 5] << 40) |
                ((long)buf[offset + 6] << 48) |
                ((long)(buf[offset + 7] & 0x7F) << 56);
            if ((buf[offset + 7] & 0x80) != 0)
                value = -value;
            return value;
        }

        private static byte[] ReadExactly(Stream stream, int count)
        {
            byte[] data = new byte[count];
            int offset = 0;
            while (count > 0)
            {
                int bytesRead = stream.Read(data, offset, count);
                if (bytesRead == 0)
                    throw new EndOfStreamException("Unexpected end of stream.");
                offset += bytesRead;
                count -= bytesRead;
            }
            return data;
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int bytesRead = stream.Read(buffer, offset, count);
                if (bytesRead == 0)
                    throw new EndOfStreamException("Unexpected end of stream.");
                offset += bytesRead;
                count -= bytesRead;
            }
        }

        #endregion Private Methods
    }
}
