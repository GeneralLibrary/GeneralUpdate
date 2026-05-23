using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Differential.Abstractions;

namespace GeneralUpdate.Differential.Binary
{
    /// <summary>
    /// BSDIFF 4.0 file binary differential processing.
    /// Implements <see cref="IBinaryDiffer"/> for pluggable architecture compatibility.
    /// Uses BZip2 compression for patch data (see <see cref="BZip2CompressionProvider"/>).
    /// </summary>
    public class BinaryHandler : IBinaryDiffer
    {
        #region Private Members

        private const long c_fileSignature = 0x3034464649445342L;
        private const int c_headerSize = 32;
        private string _oldfilePath, _newfilePath, _patchPath;

        #endregion Private Members

        #region IBinaryDiffer Implementation

        /// <inheritdoc/>
        public Task CleanAsync(string oldFilePath, string newFilePath, string patchFilePath, CancellationToken cancellationToken = default)
        {
            return Clean(oldFilePath, newFilePath, patchFilePath);
        }

        /// <inheritdoc/>
        public Task DirtyAsync(string oldFilePath, string newFilePath, string patchFilePath, CancellationToken cancellationToken = default)
        {
            return Dirty(oldFilePath, newFilePath, patchFilePath);
        }

        #endregion IBinaryDiffer Implementation

        #region Public Methods

        /// <summary>
        /// Clean out the files that need to be updated and generate the update package.
        /// </summary>
        /// <param name="oldfilePath">Old version file path.</param>
        /// <param name="newfilePath">New version file path</param>
        /// <param name="patchPath">Patch file generation path.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Clean(string oldfilePath, string newfilePath, string patchPath)
        {
            await Task.Run(() =>
            {
                _oldfilePath = oldfilePath;
                _newfilePath = newfilePath;
                _patchPath = patchPath;
                ValidationParameters();

                using (FileStream output = new FileStream(patchPath, FileMode.Create))
                {
                    var oldBytes = File.ReadAllBytes(_oldfilePath);
                    var newBytes = File.ReadAllBytes(_newfilePath);

                    /* Header is
                        0   8    "BSDIFF40"
                        8   8   length of bzip2ed ctrl block
                        16  8   length of bzip2ed diff block
                        24  8   length of new file */
                    /* File is
                        0   32  Header
                        32  ??  Bzip2ed ctrl block
                        ??  ??  Bzip2ed diff block
                        ??  ??  Bzip2ed extra block */
                    byte[] header = new byte[c_headerSize];
                    WriteInt64(c_fileSignature, header, 0); // "BSDIFF40"
                    WriteInt64(0, header, 8);
                    WriteInt64(0, header, 16);
                    WriteInt64(newBytes.Length, header, 24);

                    long startPosition = output.Position;
                    output.Write(header, 0, header.Length);

                    int[] I = SuffixSort(oldBytes);

                    byte[] db = new byte[newBytes.Length];
                    byte[] eb = new byte[newBytes.Length];

                    int dblen = 0;
                    int eblen = 0;

                    using (var bz2Stream = new BZip2OutputStream(output) { IsStreamOwner = false })
                    {
                        // compute the differences, writing ctrl as we go
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
                                bz2Stream.Write(buf, 0, 8);

                                WriteInt64((scan - lenb) - (lastscan + lenf), buf, 0);
                                bz2Stream.Write(buf, 0, 8);

                                WriteInt64((pos - lenb) - (lastpos + lenf), buf, 0);
                                bz2Stream.Write(buf, 0, 8);

                                lastscan = scan - lenb;
                                lastpos = pos - lenb;
                                lastoffset = pos - scan;
                            }
                        }
                    }

                    // compute size of compressed ctrl data
                    long controlEndPosition = output.Position;
                    WriteInt64(controlEndPosition - startPosition - c_headerSize, header, 8);

                    // write compressed diff data
                    using (var bz2Stream = new BZip2OutputStream(output) { IsStreamOwner = false })
                    {
                        bz2Stream.Write(db, 0, dblen);
                    }

                    // compute size of compressed diff data
                    long diffEndPosition = output.Position;
                    WriteInt64(diffEndPosition - controlEndPosition, header, 16);

                    // write compressed extra data
                    using (var bz2Stream = new BZip2OutputStream(output) { IsStreamOwner = false })
                    {
                        bz2Stream.Write(eb, 0, eblen);
                    }

                    // seek to the beginning, write the header, then seek back to end
                    long endPosition = output.Position;
                    output.Position = startPosition;
                    output.Write(header, 0, header.Length);
                    output.Position = endPosition;
                }
            });
        }

        /// <summary>
        /// Update the patch file to the client application.
        /// </summary>
        /// <param name="oldfilePath"></param>
        /// <param name="newfilePath"></param>
        /// <param name="patchPath"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task Dirty(string oldfilePath, string newfilePath, string patchPath)
        {
            await Task.Run(() =>
            {
                _oldfilePath = oldfilePath;
                _newfilePath = newfilePath;
                _patchPath = patchPath;
                ValidationParameters();
                using (FileStream input =
                       new FileStream(_oldfilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (FileStream output = new FileStream(_newfilePath, FileMode.Create))
                    {
                        Func<Stream> openPatchStream = () =>
                            new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        //File format:
                        //  0   8   "BSDIFF40"
                        //  8   8   X
                        //  16  8   Y
                        //  24  8   sizeof(newfile)
                        //  32  X   bzip2(control block)
                        //  32 + X  Y   bzip2(diff block)
                        //  32 + X + Y ??? bzip2(extra block)
                        //with control block a set of triples(x, y, z) meaning "add x bytes
                        //from oldfile to x bytes from the diff block; copy y bytes from the
                        //extra block; seek forwards in oldfile by z bytes".
                        // read header
                        long controlLength, diffLength, newSize;
                        using (Stream patchStream = openPatchStream())
                        {
                            // check patch stream capabilities
                            if (!patchStream.CanRead)
                                throw new ArgumentException("Patch stream must be readable.", "openPatchStream");
                            if (!patchStream.CanSeek)
                                throw new ArgumentException("Patch stream must be seekable.", "openPatchStream");

                            byte[] header = ReadExactly(patchStream, c_headerSize);

                            // check for appropriate magic
                            long signature = ReadInt64(header, 0);
                            if (signature != c_fileSignature)
                                throw new InvalidOperationException("Corrupt patch.");

                            // read lengths from header
                            controlLength = ReadInt64(header, 8);
                            diffLength = ReadInt64(header, 16);
                            newSize = ReadInt64(header, 24);
                            if (controlLength < 0 || diffLength < 0 || newSize < 0)
                                throw new InvalidOperationException("Corrupt patch.");
                        }

                        // preallocate buffers for reading and writing
                        const int c_bufferSize = 1048576;
                        byte[] newData = new byte[c_bufferSize];
                        byte[] oldData = new byte[c_bufferSize];

                        // prepare to read three parts of the patch in parallel
                        using (Stream compressedControlStream = openPatchStream())
                        using (Stream compressedDiffStream = openPatchStream())
                        using (Stream compressedExtraStream = openPatchStream())
                        {
                            // seek to the start of each part
                            compressedControlStream.Seek(c_headerSize, SeekOrigin.Current);
                            compressedDiffStream.Seek(c_headerSize + controlLength, SeekOrigin.Current);
                            compressedExtraStream.Seek(c_headerSize + controlLength + diffLength,
                                SeekOrigin.Current);

                            // decompress each part (to read it)
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
                                    // read control data
                                    for (int i = 0; i < 3; i++)
                                    {
                                        ReadExactly(controlStream, buffer, 0, 8);
                                        control[i] = ReadInt64(buffer, 0);
                                    }

                                    // sanity-check
                                    if (newPosition + control[0] > newSize)
                                        throw new InvalidOperationException("Corrupt patch.");

                                    // seek old file to the position that the new data is diffed against
                                    input.Position = oldPosition;

                                    int bytesToCopy = (int)control[0];
                                    while (bytesToCopy > 0)
                                    {
                                        int actualBytesToCopy = Math.Min(bytesToCopy, c_bufferSize);

                                        // read diff string
                                        ReadExactly(diffStream, newData, 0, actualBytesToCopy);

                                        // add old data to diff string
                                        int availableInputBytes = Math.Min(actualBytesToCopy,
                                            (int)(input.Length - input.Position));
                                        ReadExactly(input, oldData, 0, availableInputBytes);

                                        for (int index = 0; index < availableInputBytes; index++)
                                            newData[index] += oldData[index];

                                        output.Write(newData, 0, actualBytesToCopy);

                                        // adjust counters
                                        newPosition += actualBytesToCopy;
                                        oldPosition += actualBytesToCopy;
                                        bytesToCopy -= actualBytesToCopy;
                                    }

                                    // sanity-check
                                    if (newPosition + control[1] > newSize)
                                        throw new InvalidOperationException("Corrupt patch.");

                                    // read extra string
                                    bytesToCopy = (int)control[1];
                                    while (bytesToCopy > 0)
                                    {
                                        int actualBytesToCopy = Math.Min(bytesToCopy, c_bufferSize);

                                        ReadExactly(extraStream, newData, 0, actualBytesToCopy);
                                        output.Write(newData, 0, actualBytesToCopy);

                                        newPosition += actualBytesToCopy;
                                        bytesToCopy -= actualBytesToCopy;
                                    }

                                    // adjust position
                                    oldPosition = (int)(oldPosition + control[2]);
                                }
                            }
                        }
                    }
                }

                if (File.Exists(_oldfilePath))
                {
                    File.SetAttributes(_oldfilePath, FileAttributes.Normal);
                    File.Delete(_oldfilePath);
                }

                if (File.Exists(_newfilePath))
                {
                    File.SetAttributes(_newfilePath, FileAttributes.Normal);
                    File.Copy(_newfilePath, _oldfilePath, true);
                    File.Delete(_newfilePath);
                }
            });
        }

        #endregion Public Methods

        #region Private Methods

        private void ValidationParameters()
        {
            if (string.IsNullOrWhiteSpace(_oldfilePath)) throw new ArgumentNullException("'oldfilePath' This parameter cannot be empty .");
            if (string.IsNullOrWhiteSpace(_newfilePath)) throw new ArgumentNullException("'newfilePath' This parameter cannot be empty .");
            if (string.IsNullOrWhiteSpace(_patchPath)) throw new ArgumentNullException("'patchPath' This parameter cannot be empty .");
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

            var v = new int[oldData.Length + 1];
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

        private static long ReadInt64(byte[] buf, int offset)
        {
            return (long)(
                   ((ulong)buf[offset + 0]) |
                   ((ulong)buf[offset + 1] << 8) |
                   ((ulong)buf[offset + 2] << 16) |
                   ((ulong)buf[offset + 3] << 24) |
                   ((ulong)buf[offset + 4] << 32) |
                   ((ulong)buf[offset + 5] << 40) |
                   ((ulong)buf[offset + 6] << 48) |
                   ((ulong)buf[offset + 7] << 56));
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
