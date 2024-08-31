using GeneralUpdate.Differential.GStream;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GeneralUpdate.Differential.Binary
{
    /// <summary>
    /// File binary differential processing.
    /// </summary>
    public class BinaryHandler
    {
        #region Private Members

        private const long FileSignature = 0x3034464649445342L;
        private const int HeaderSize = 32;
        private string _oldFilePath, _newFilePath, _patchPath;

        #endregion Private Members

        #region Public Methods

        /// <summary>
        /// Clean out the files that need to be updated and generate the update package.
        /// </summary>
        /// <param name="oldFilePath">Old version file path.</param>
        /// <param name="newFilePath">New version file path</param>
        /// <param name="patchPath">Patch file generation path.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Clean(string oldFilePath, string newFilePath, string patchPath)
        {
            _oldFilePath = oldFilePath;
            _newFilePath = newFilePath;
            _patchPath = patchPath;
            ValidateParameters();

            try
            {
                await Task.Run(() => GeneratePatch());
            }
            catch (Exception ex)
            {
                throw new Exception($"Clean error: {ex.Message}", ex.InnerException);
            }
        }

        /// <summary>
        /// Update the patch file to the client application.
        /// </summary>
        /// <param name="oldFilePath">Old version file path.</param>
        /// <param name="newFilePath">New version file path</param>
        /// <param name="patchPath">Patch file path.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task Dirty(string oldFilePath, string newFilePath, string patchPath)
        {
            _oldFilePath = oldFilePath;
            _newFilePath = newFilePath;
            _patchPath = patchPath;
            ValidateParameters();

            await Task.Run(() => ApplyPatch());
        }

        #endregion Public Methods

        #region Private Methods

        private void ValidateParameters()
        {
            if (string.IsNullOrWhiteSpace(_oldFilePath)) throw new ArgumentNullException(nameof(_oldFilePath), "This parameter cannot be empty.");
            if (string.IsNullOrWhiteSpace(_newFilePath)) throw new ArgumentNullException(nameof(_newFilePath), "This parameter cannot be empty.");
            if (string.IsNullOrWhiteSpace(_patchPath)) throw new ArgumentNullException(nameof(_patchPath), "This parameter cannot be empty.");
        }

        private void GeneratePatch()
        {
            using (FileStream output = new FileStream(_patchPath, FileMode.Create))
            {
                var oldBytes = File.ReadAllBytes(_oldFilePath);
                var newBytes = File.ReadAllBytes(_newFilePath);

                byte[] header = new byte[HeaderSize];
                WriteInt64(FileSignature, header, 0); // "BSDIFF40"
                WriteInt64(0, header, 8);
                WriteInt64(0, header, 16);
                WriteInt64(newBytes.Length, header, 24);

                long startPosition = output.Position;
                output.Write(header, 0, header.Length);

                int[] suffixArray = SuffixSort(oldBytes);

                byte[] diffBytes = new byte[newBytes.Length];
                byte[] extraBytes = new byte[newBytes.Length];

                int diffLength = 0;
                int extraLength = 0;

                using (var bz2Stream = new BZip2OutputStream(output) { IsStreamOwner = false })
                {
                    ComputeDifferences(oldBytes, newBytes, suffixArray, diffBytes, extraBytes, ref diffLength, ref extraLength, bz2Stream);
                }

                long controlEndPosition = output.Position;
                WriteInt64(controlEndPosition - startPosition - HeaderSize, header, 8);

                using (var bz2Stream = new BZip2OutputStream(output) { IsStreamOwner = false })
                {
                    bz2Stream.Write(diffBytes, 0, diffLength);
                }

                long diffEndPosition = output.Position;
                WriteInt64(diffEndPosition - controlEndPosition, header, 16);

                using (var bz2Stream = new BZip2OutputStream(output) { IsStreamOwner = false })
                {
                    bz2Stream.Write(extraBytes, 0, extraLength);
                }

                long endPosition = output.Position;
                output.Position = startPosition;
                output.Write(header, 0, header.Length);
                output.Position = endPosition;
            }
        }

        private void ComputeDifferences(byte[] oldBytes, byte[] newBytes, int[] suffixArray, byte[] diffBytes, byte[] extraBytes, ref int diffLength, ref int extraLength, BZip2OutputStream bz2Stream)
        {
            int scan = 0;
            int pos = 0;
            int len = 0;
            int lastScan = 0;
            int lastPos = 0;
            int lastOffset = 0;

            while (scan < newBytes.Length)
            {
                int oldScore = 0;

                for (int scsc = scan += len; scan < newBytes.Length; scan++)
                {
                    len = Search(suffixArray, oldBytes, newBytes, scan, 0, oldBytes.Length, out pos);

                    for (; scsc < scan + len; scsc++)
                    {
                        if ((scsc + lastOffset < oldBytes.Length) && (oldBytes[scsc + lastOffset] == newBytes[scsc]))
                            oldScore++;
                    }

                    if ((len == oldScore && len != 0) || (len > oldScore + 8))
                        break;

                    if ((scan + lastOffset < oldBytes.Length) && (oldBytes[scan + lastOffset] == newBytes[scan]))
                        oldScore--;
                }

                if (len != oldScore || scan == newBytes.Length)
                {
                    int s = 0;
                    int sf = 0;
                    int lenf = 0;
                    for (int i = 0; (lastScan + i < scan) && (lastPos + i < oldBytes.Length);)
                    {
                        if (oldBytes[lastPos + i] == newBytes[lastScan + i])
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
                        for (int i = 1; (scan >= lastScan + i) && (pos >= i); i++)
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

                    if (lastScan + lenf > scan - lenb)
                    {
                        int overlap = (lastScan + lenf) - (scan - lenb);
                        s = 0;
                        int ss = 0;
                        int lens = 0;
                        for (int i = 0; i < overlap; i++)
                        {
                            if (newBytes[lastScan + lenf - overlap + i] == oldBytes[lastPos + lenf - overlap + i])
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
                        diffBytes[diffLength + i] = (byte)(newBytes[lastScan + i] - oldBytes[lastPos + i]);
                    for (int i = 0; i < (scan - lenb) - (lastScan + lenf); i++)
                        extraBytes[extraLength + i] = newBytes[lastScan + lenf + i];

                    diffLength += lenf;
                    extraLength += (scan - lenb) - (lastScan + lenf);

                    byte[] buffer = new byte[8];
                    WriteInt64(lenf, buffer, 0);
                    bz2Stream.Write(buffer, 0, 8);

                    WriteInt64((scan - lenb) - (lastScan + lenf), buffer, 0);
                    bz2Stream.Write(buffer, 0, 8);

                    WriteInt64((pos - lenb) - (lastPos + lenf), buffer, 0);
                    bz2Stream.Write(buffer, 0, 8);

                    lastScan = scan - lenb;
                    lastPos = pos - lenb;
                    lastOffset = pos - scan;
                }
            }
        }

        private void ApplyPatch()
        {
            using (FileStream input = new FileStream(_oldFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (FileStream output = new FileStream(_newFilePath, FileMode.Create))
                {
                    Func<Stream> openPatchStream = () => new FileStream(_patchPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    // Read header
                    long controlLength, diffLength, newSize;
                    using (Stream patchStream = openPatchStream())
                    {
                        // Check patch stream capabilities
                        if (!patchStream.CanRead)
                            throw new ArgumentException("Patch stream must be readable.", nameof(openPatchStream));
                        if (!patchStream.CanSeek)
                            throw new ArgumentException("Patch stream must be seekable.", nameof(openPatchStream));

                        byte[] header = ReadExactly(patchStream, HeaderSize);

                        // Check for appropriate magic
                        long signature = ReadInt64(header, 0);
                        if (signature != FileSignature)
                            throw new InvalidOperationException("Corrupt patch.");

                        // Read lengths from header
                        controlLength = ReadInt64(header, 8);
                        diffLength = ReadInt64(header, 16);
                        newSize = ReadInt64(header, 24);
                        if (controlLength < 0 || diffLength < 0 || newSize < 0)
                            throw new InvalidOperationException("Corrupt patch.");
                    }

                    // Preallocate buffers for reading and writing
                    const int BufferSize = 1048576;
                    byte[] newData = new byte[BufferSize];
                    byte[] oldData = new byte[BufferSize];

                    // Prepare to read three parts of the patch in parallel
                    using (Stream compressedControlStream = openPatchStream())
                    using (Stream compressedDiffStream = openPatchStream())
                    using (Stream compressedExtraStream = openPatchStream())
                    {
                        // Seek to the start of each part
                        compressedControlStream.Seek(HeaderSize, SeekOrigin.Current);
                        compressedDiffStream.Seek(HeaderSize + controlLength, SeekOrigin.Current);
                        compressedExtraStream.Seek(HeaderSize + controlLength + diffLength, SeekOrigin.Current);

                        // Decompress each part (to read it)
                        using (BZip2InputStream controlStream = new BZip2InputStream(compressedControlStream))
                        using (BZip2InputStream diffStream = new BZip2InputStream(compressedDiffStream))
                        using (BZip2InputStream extraStream = new BZip2InputStream(compressedExtraStream))
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
                                    throw new InvalidOperationException("Corrupt patch.");

                                // Seek old file to the position that the new data is diffed against
                                input.Position = oldPosition;

                                int bytesToCopy = (int)control[0];
                                while (bytesToCopy > 0)
                                {
                                    int actualBytesToCopy = Math.Min(bytesToCopy, BufferSize);

                                    // Read diff string
                                    ReadExactly(diffStream, newData, 0, actualBytesToCopy);

                                    // Add old data to diff string
                                    int availableInputBytes = Math.Min(actualBytesToCopy, (int)(input.Length - input.Position));
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
                                    throw new InvalidOperationException("Corrupt patch.");

                                // Read extra string
                                bytesToCopy = (int)control[1];
                                while (bytesToCopy > 0)
                                {
                                    int actualBytesToCopy = Math.Min(bytesToCopy, BufferSize);

                                    ReadExactly(extraStream, newData, 0, actualBytesToCopy);
                                    output.Write(newData, 0, actualBytesToCopy);

                                    newPosition += actualBytesToCopy;
                                    bytesToCopy -= actualBytesToCopy;
                                }

                                // Adjust position
                                oldPosition = (int)(oldPosition + control[2]);
                            }
                        }
                    }
                }
            }

            File.Delete(_oldFilePath);
            File.Move(_newFilePath, _oldFilePath);
        }

        private int CompareBytes(byte[] left, int leftOffset, byte[] right, int rightOffset)
        {
            for (int index = 0; index < left.Length - leftOffset && index < right.Length - rightOffset; index++)
            {
                int diff = left[index + leftOffset] - right[index + rightOffset];
                if (diff != 0) return diff;
            }
            return 0;
        }

        private int MatchLength(byte[] oldBytes, int oldOffset, byte[] newBytes, int newOffset)
        {
            int i;
            for (i = 0; i < oldBytes.Length - oldOffset && i < newBytes.Length - newOffset; i++)
            {
                if (oldBytes[i + oldOffset] != newBytes[i + newOffset])
                    break;
            }
            return i;
        }

        private int Search(int[] suffixArray, byte[] oldBytes, byte[] newBytes, int newOffset, int start, int end, out int pos)
        {
            if (end - start < 2)
            {
                int startLength = MatchLength(oldBytes, suffixArray[start], newBytes, newOffset);
                int endLength = MatchLength(oldBytes, suffixArray[end], newBytes, newOffset);

                if (startLength > endLength)
                {
                    pos = suffixArray[start];
                    return startLength;
                }
                else
                {
                    pos = suffixArray[end];
                    return endLength;
                }
            }
            else
            {
                int midPoint = start + (end - start) / 2;
                return CompareBytes(oldBytes, suffixArray[midPoint], newBytes, newOffset) < 0 ?
                    Search(suffixArray, oldBytes, newBytes, newOffset, midPoint, end, out pos) :
                    Search(suffixArray, oldBytes, newBytes, newOffset, start, midPoint, out pos);
            }
        }

        private void Split(int[] suffixArray, int[] rankArray, int start, int len, int h)
        {
            if (len < 16)
            {
                int j;
                for (int k = start; k < start + len; k += j)
                {
                    j = 1;
                    int x = rankArray[suffixArray[k] + h];
                    for (int i = 1; k + i < start + len; i++)
                    {
                        if (rankArray[suffixArray[k + i] + h] < x)
                        {
                            x = rankArray[suffixArray[k + i] + h];
                            j = 0;
                        }
                        if (rankArray[suffixArray[k + i] + h] == x)
                        {
                            Swap(ref suffixArray[k + j], ref suffixArray[k + i]);
                            j++;
                        }
                    }
                    for (int i = 0; i < j; i++)
                        rankArray[suffixArray[k + i]] = k + j - 1;
                    if (j == 1)
                        suffixArray[k] = -1;
                }
            }
            else
            {
                int x = rankArray[suffixArray[start + len / 2] + h];
                int jj = 0;
                int kk = 0;
                for (int i2 = start; i2 < start + len; i2++)
                {
                    if (rankArray[suffixArray[i2] + h] < x) jj++;
                    if (rankArray[suffixArray[i2] + h] == x) kk++;
                }
                jj += start;
                kk += jj;

                int i = start;
                int j = 0;
                int k = 0;
                while (i < jj)
                {
                    if (rankArray[suffixArray[i] + h] < x)
                    {
                        i++;
                    }
                    else if (rankArray[suffixArray[i] + h] == x)
                    {
                        Swap(ref suffixArray[i], ref suffixArray[jj + j]);
                        j++;
                    }
                    else
                    {
                        Swap(ref suffixArray[i], ref suffixArray[kk + k]);
                        k++;
                    }
                }

                while (jj + j < kk)
                {
                    if (rankArray[suffixArray[jj + j] + h] == x)
                    {
                        j++;
                    }
                    else
                    {
                        Swap(ref suffixArray[jj + j], ref suffixArray[kk + k]);
                        k++;
                    }
                }

                if (jj > start) Split(suffixArray, rankArray, start, jj - start, h);

                for (i = 0; i < kk - jj; i++)
                    rankArray[suffixArray[jj + i]] = kk - 1;
                if (jj == kk - 1) suffixArray[jj] = -1;

                if (start + len > kk) Split(suffixArray, rankArray, kk, start + len -kk, h);
            }
        }

        private int[] SuffixSort(byte[] oldBytes)
        {
            int[] buckets = new int[256];
            foreach (byte oldByte in oldBytes)
                buckets[oldByte]++;
            for (int i = 1; i < 256; i++)
                buckets[i] += buckets[i - 1];
            for (int i = 255; i > 0; i--)
                buckets[i] = buckets[i - 1];
            buckets[0] = 0;

            int[] suffixArray = new int[oldBytes.Length + 1];
            for (int i = 0; i < oldBytes.Length; i++)
                suffixArray[++buckets[oldBytes[i]]] = i;

            int[] rankArray = new int[oldBytes.Length + 1];
            for (int i = 0; i < oldBytes.Length; i++)
                rankArray[i] = buckets[oldBytes[i]];

            for (int i = 1; i < 256; i++)
                if (buckets[i] == buckets[i - 1] + 1) suffixArray[buckets[i]] = -1;
            suffixArray[0] = -1;
            for (int h = 1; suffixArray[0] != -(oldBytes.Length + 1); h += h)
            {
                int len = 0;
                int i = 0;
                while (i < oldBytes.Length + 1)
                {
                    if (suffixArray[i] < 0)
                    {
                        len -= suffixArray[i];
                        i -= suffixArray[i];
                    }
                    else
                    {
                        if (len != 0) suffixArray[i - len] = -len;
                        len = rankArray[suffixArray[i]] + 1 - i;
                        Split(suffixArray, rankArray, i, len, h);
                        i += len;
                        len = 0;
                    }
                }
                if (len != 0) suffixArray[i - len] = -len;
            }

            for (int i = 0; i < oldBytes.Length + 1; i++)
                suffixArray[rankArray[i]] = i;

            return suffixArray;
        }

        private void Swap(ref int first, ref int second)
        {
            (first, second) = (second, first);
        }

        private long ReadInt64(byte[] buf, int offset)
        {
            long value = buf[offset + 7] & 0x7F;
            for (int index = 6; index >= 0; index--)
            {
                value *= 256;
                value += buf[offset + index];
            }
            if ((buf[offset + 7] & 0x80) != 0) value = -value;
            return value;
        }

        private void WriteInt64(long value, byte[] buf, int offset)
        {
            long valueToWrite = value < 0 ? -value : value;
            for (int byteIndex = 0; byteIndex < 8; byteIndex++)
            {
                buf[offset + byteIndex] = unchecked((byte)valueToWrite);
                valueToWrite >>= 8;
            }
            if (value < 0) buf[offset + 7] |= 0x80;
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="count">The count of bytes to read.</param>
        /// <returns>A new byte array containing the data read from the stream.</returns>
        private byte[] ReadExactly(Stream stream, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            byte[] buffer = new byte[count];
            ReadExactly(stream, buffer, 0, count);
            return buffer;
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/> into
        /// <paramref name="buffer"/>, starting at the byte given by <paramref name="offset"/>.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer to read data into.</param>
        /// <param name="offset">The offset within the buffer at which data is first written.</param>
        /// <param name="count">The count of bytes to read.</param>
        private void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            // Check arguments
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || buffer.Length - offset < count) throw new ArgumentOutOfRangeException(nameof(count));

            while (count > 0)
            {
                // Read data
                int bytesRead = stream.Read(buffer, offset, count);
                // Check for failure to read
                if (bytesRead == 0) throw new EndOfStreamException();
                // Move to next block
                offset += bytesRead;
                count -= bytesRead;
            }
        }

        #endregion Private Methods
    }
}