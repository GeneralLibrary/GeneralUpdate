using System;
using System.IO;

namespace GeneralUpdate.Differential.GStream
{
    public class BZip2OutputStream : Stream
    {
        private struct StackElement
        {
            public int ll;

            public int hh;

            public int dd;
        }

        private const int SETMASK = 2097152;

        private const int CLEARMASK = -2097153;

        private const int GREATER_ICOST = 15;

        private const int LESSER_ICOST = 0;

        private const int SMALL_THRESH = 20;

        private const int DEPTH_THRESH = 10;

        private const int QSORT_STACK_SIZE = 1000;

        private readonly int[] increments = new int[14]
        {
            1, 4, 13, 40, 121, 364, 1093, 3280, 9841, 29524,
            88573, 265720, 797161, 2391484
        };

        private bool isStreamOwner = true;

        private int last;

        private int origPtr;

        private int blockSize100k;

        private bool blockRandomised;

        private int bytesOut;

        private int bsBuff;

        private int bsLive;

        private IChecksum mCrc = new StrangeCRC();

        private bool[] inUse = new bool[256];

        private int nInUse;

        private char[] seqToUnseq = new char[256];

        private char[] unseqToSeq = new char[256];

        private char[] selector = new char[18002];

        private char[] selectorMtf = new char[18002];

        private byte[] block;

        private int[] quadrant;

        private int[] zptr;

        private short[] szptr;

        private int[] ftab;

        private int nMTF;

        private int[] mtfFreq = new int[258];

        private int workFactor;

        private int workDone;

        private int workLimit;

        private bool firstAttempt;

        private int nBlocksRandomised;

        private int currentChar = -1;

        private int runLength;

        private uint blockCRC;

        private uint combinedCRC;

        private int allowableBlockSize;

        private Stream baseStream;

        private bool disposed_;

        public bool IsStreamOwner
        {
            get
            {
                return isStreamOwner;
            }
            set
            {
                isStreamOwner = value;
            }
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => baseStream.CanWrite;

        public override long Length => baseStream.Length;

        public override long Position
        {
            get
            {
                return baseStream.Position;
            }
            set
            {
                throw new NotSupportedException("BZip2OutputStream position cannot be set");
            }
        }

        public int BytesWritten => bytesOut;

        public BZip2OutputStream(Stream stream)
            : this(stream, 9)
        {
        }

        public BZip2OutputStream(Stream stream, int blockSize)
        {
            BsSetStream(stream);
            workFactor = 50;
            if (blockSize > 9)
            {
                blockSize = 9;
            }

            if (blockSize < 1)
            {
                blockSize = 1;
            }

            blockSize100k = blockSize;
            AllocateCompressStructures();
            Initialize();
            InitBlock();
        }

        ~BZip2OutputStream()
        {
            Dispose(disposing: false);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("BZip2OutputStream Seek not supported");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("BZip2OutputStream SetLength not supported");
        }

        public override int ReadByte()
        {
            throw new NotSupportedException("BZip2OutputStream ReadByte not supported");
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("BZip2OutputStream Read not supported");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (buffer.Length - offset < count)
            {
                throw new ArgumentException("Offset/count out of range");
            }

            for (int i = 0; i < count; i++)
            {
                WriteByte(buffer[offset + i]);
            }
        }

        public override void WriteByte(byte value)
        {
            int num = (256 + value) % 256;
            if (currentChar != -1)
            {
                if (currentChar == num)
                {
                    runLength++;
                    if (runLength > 254)
                    {
                        WriteRun();
                        currentChar = -1;
                        runLength = 0;
                    }
                }
                else
                {
                    WriteRun();
                    runLength = 1;
                    currentChar = num;
                }
            }
            else
            {
                currentChar = num;
                runLength++;
            }
        }

        public override void Close()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void MakeMaps()
        {
            nInUse = 0;
            for (int i = 0; i < 256; i++)
            {
                if (inUse[i])
                {
                    seqToUnseq[nInUse] = (char)i;
                    unseqToSeq[i] = (char)nInUse;
                    nInUse++;
                }
            }
        }

        private void WriteRun()
        {
            if (last < allowableBlockSize)
            {
                inUse[currentChar] = true;
                for (int i = 0; i < runLength; i++)
                {
                    mCrc.Update(currentChar);
                }

                switch (runLength)
                {
                    case 1:
                        last++;
                        block[last + 1] = (byte)currentChar;
                        break;

                    case 2:
                        last++;
                        block[last + 1] = (byte)currentChar;
                        last++;
                        block[last + 1] = (byte)currentChar;
                        break;

                    case 3:
                        last++;
                        block[last + 1] = (byte)currentChar;
                        last++;
                        block[last + 1] = (byte)currentChar;
                        last++;
                        block[last + 1] = (byte)currentChar;
                        break;

                    default:
                        inUse[runLength - 4] = true;
                        last++;
                        block[last + 1] = (byte)currentChar;
                        last++;
                        block[last + 1] = (byte)currentChar;
                        last++;
                        block[last + 1] = (byte)currentChar;
                        last++;
                        block[last + 1] = (byte)currentChar;
                        last++;
                        block[last + 1] = (byte)(runLength - 4);
                        break;
                }
            }
            else
            {
                EndBlock();
                InitBlock();
                WriteRun();
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
                if (!disposed_)
                {
                    disposed_ = true;
                    if (runLength > 0)
                    {
                        WriteRun();
                    }

                    currentChar = -1;
                    EndBlock();
                    EndCompression();
                    Flush();
                }
            }
            finally
            {
                if (disposing && IsStreamOwner)
                {
                    baseStream.Close();
                }
            }
        }

        public override void Flush()
        {
            baseStream.Flush();
        }

        private void Initialize()
        {
            bytesOut = 0;
            nBlocksRandomised = 0;
            BsPutUChar(66);
            BsPutUChar(90);
            BsPutUChar(104);
            BsPutUChar(48 + blockSize100k);
            combinedCRC = 0u;
        }

        private void InitBlock()
        {
            mCrc.Reset();
            last = -1;
            for (int i = 0; i < 256; i++)
            {
                inUse[i] = false;
            }

            allowableBlockSize = 100000 * blockSize100k - 20;
        }

        private void EndBlock()
        {
            if (last >= 0)
            {
                blockCRC = (uint)mCrc.Value;
                combinedCRC = (combinedCRC << 1) | (combinedCRC >> 31);
                combinedCRC ^= blockCRC;
                DoReversibleTransformation();
                BsPutUChar(49);
                BsPutUChar(65);
                BsPutUChar(89);
                BsPutUChar(38);
                BsPutUChar(83);
                BsPutUChar(89);
                BsPutint((int)blockCRC);
                if (blockRandomised)
                {
                    BsW(1, 1);
                    nBlocksRandomised++;
                }
                else
                {
                    BsW(1, 0);
                }

                MoveToFrontCodeAndSend();
            }
        }

        private void EndCompression()
        {
            BsPutUChar(23);
            BsPutUChar(114);
            BsPutUChar(69);
            BsPutUChar(56);
            BsPutUChar(80);
            BsPutUChar(144);
            BsPutint((int)combinedCRC);
            BsFinishedWithStream();
        }

        private void BsSetStream(Stream stream)
        {
            baseStream = stream;
            bsLive = 0;
            bsBuff = 0;
            bytesOut = 0;
        }

        private void BsFinishedWithStream()
        {
            while (bsLive > 0)
            {
                int num = bsBuff >> 24;
                baseStream.WriteByte((byte)num);
                bsBuff <<= 8;
                bsLive -= 8;
                bytesOut++;
            }
        }

        private void BsW(int n, int v)
        {
            while (bsLive >= 8)
            {
                int num = bsBuff >> 24;
                baseStream.WriteByte((byte)num);
                bsBuff <<= 8;
                bsLive -= 8;
                bytesOut++;
            }

            bsBuff |= v << 32 - bsLive - n;
            bsLive += n;
        }

        private void BsPutUChar(int c)
        {
            BsW(8, c);
        }

        private void BsPutint(int u)
        {
            BsW(8, (u >> 24) & 0xFF);
            BsW(8, (u >> 16) & 0xFF);
            BsW(8, (u >> 8) & 0xFF);
            BsW(8, u & 0xFF);
        }

        private void BsPutIntVS(int numBits, int c)
        {
            BsW(numBits, c);
        }

        private void SendMTFValues()
        {
            char[][] array = new char[6][];
            for (int i = 0; i < 6; i++)
            {
                array[i] = new char[258];
            }

            int num = 0;
            int num2 = nInUse + 2;
            for (int j = 0; j < 6; j++)
            {
                for (int k = 0; k < num2; k++)
                {
                    array[j][k] = '\u000f';
                }
            }

            if (nMTF <= 0)
            {
                Panic();
            }

            int num3 = ((nMTF < 200) ? 2 : ((nMTF < 600) ? 3 : ((nMTF < 1200) ? 4 : ((nMTF >= 2400) ? 6 : 5))));
            int num4 = num3;
            int num5 = nMTF;
            int num6 = 0;
            while (num4 > 0)
            {
                int num7 = num5 / num4;
                int l = 0;
                int num8;
                for (num8 = num6 - 1; l < num7; l += mtfFreq[num8])
                {
                    if (num8 >= num2 - 1)
                    {
                        break;
                    }

                    num8++;
                }

                if (num8 > num6 && num4 != num3 && num4 != 1 && (num3 - num4) % 2 == 1)
                {
                    l -= mtfFreq[num8];
                    num8--;
                }

                for (int m = 0; m < num2; m++)
                {
                    if (m >= num6 && m <= num8)
                    {
                        array[num4 - 1][m] = '\0';
                    }
                    else
                    {
                        array[num4 - 1][m] = '\u000f';
                    }
                }

                num4--;
                num6 = num8 + 1;
                num5 -= l;
            }

            int[][] array2 = new int[6][];
            for (int n = 0; n < 6; n++)
            {
                array2[n] = new int[258];
            }

            int[] array3 = new int[6];
            short[] array4 = new short[6];
            for (int num9 = 0; num9 < 4; num9++)
            {
                for (int num10 = 0; num10 < num3; num10++)
                {
                    array3[num10] = 0;
                }

                for (int num11 = 0; num11 < num3; num11++)
                {
                    for (int num12 = 0; num12 < num2; num12++)
                    {
                        array2[num11][num12] = 0;
                    }
                }

                num = 0;
                int num13 = 0;
                num6 = 0;
                while (num6 < nMTF)
                {
                    int num8 = num6 + 50 - 1;
                    if (num8 >= nMTF)
                    {
                        num8 = nMTF - 1;
                    }

                    for (int num14 = 0; num14 < num3; num14++)
                    {
                        array4[num14] = 0;
                    }

                    if (num3 == 6)
                    {
                        short num19;
                        short num18;
                        short num17;
                        short num16;
                        short num15;
                        short num20 = (num19 = (num18 = (num17 = (num16 = (num15 = 0)))));
                        for (int num21 = num6; num21 <= num8; num21++)
                        {
                            short num22 = szptr[num21];
                            num20 = (short)(num20 + (short)array[0][num22]);
                            num19 = (short)(num19 + (short)array[1][num22]);
                            num18 = (short)(num18 + (short)array[2][num22]);
                            num17 = (short)(num17 + (short)array[3][num22]);
                            num16 = (short)(num16 + (short)array[4][num22]);
                            num15 = (short)(num15 + (short)array[5][num22]);
                        }

                        array4[0] = num20;
                        array4[1] = num19;
                        array4[2] = num18;
                        array4[3] = num17;
                        array4[4] = num16;
                        array4[5] = num15;
                    }
                    else
                    {
                        for (int num23 = num6; num23 <= num8; num23++)
                        {
                            short num24 = szptr[num23];
                            for (int num25 = 0; num25 < num3; num25++)
                            {
                                array4[num25] += (short)array[num25][num24];
                            }
                        }
                    }

                    int num26 = 999999999;
                    int num27 = -1;
                    for (int num28 = 0; num28 < num3; num28++)
                    {
                        if (array4[num28] < num26)
                        {
                            num26 = array4[num28];
                            num27 = num28;
                        }
                    }

                    num13 += num26;
                    array3[num27]++;
                    selector[num] = (char)num27;
                    num++;
                    for (int num29 = num6; num29 <= num8; num29++)
                    {
                        array2[num27][szptr[num29]]++;
                    }

                    num6 = num8 + 1;
                }

                for (int num30 = 0; num30 < num3; num30++)
                {
                    HbMakeCodeLengths(array[num30], array2[num30], num2, 20);
                }
            }

            array2 = null;
            array3 = null;
            array4 = null;
            if (num3 >= 8)
            {
                Panic();
            }

            if (num >= 32768 || num > 18002)
            {
                Panic();
            }

            char[] array5 = new char[6];
            for (int num31 = 0; num31 < num3; num31++)
            {
                array5[num31] = (char)num31;
            }

            for (int num32 = 0; num32 < num; num32++)
            {
                char c = selector[num32];
                int num33 = 0;
                char c2 = array5[num33];
                while (c != c2)
                {
                    num33++;
                    char c3 = c2;
                    c2 = array5[num33];
                    array5[num33] = c3;
                }

                array5[0] = c2;
                selectorMtf[num32] = (char)num33;
            }

            int[][] array6 = new int[6][];
            for (int num34 = 0; num34 < 6; num34++)
            {
                array6[num34] = new int[258];
            }

            for (int num35 = 0; num35 < num3; num35++)
            {
                int num36 = 32;
                int num37 = 0;
                for (int num38 = 0; num38 < num2; num38++)
                {
                    if (array[num35][num38] > num37)
                    {
                        num37 = array[num35][num38];
                    }

                    if (array[num35][num38] < num36)
                    {
                        num36 = array[num35][num38];
                    }
                }

                if (num37 > 20)
                {
                    Panic();
                }

                if (num36 < 1)
                {
                    Panic();
                }

                HbAssignCodes(array6[num35], array[num35], num36, num37, num2);
            }

            bool[] array7 = new bool[16];
            for (int num39 = 0; num39 < 16; num39++)
            {
                array7[num39] = false;
                for (int num40 = 0; num40 < 16; num40++)
                {
                    if (inUse[num39 * 16 + num40])
                    {
                        array7[num39] = true;
                    }
                }
            }

            for (int num41 = 0; num41 < 16; num41++)
            {
                if (array7[num41])
                {
                    BsW(1, 1);
                }
                else
                {
                    BsW(1, 0);
                }
            }

            for (int num42 = 0; num42 < 16; num42++)
            {
                if (!array7[num42])
                {
                    continue;
                }

                for (int num43 = 0; num43 < 16; num43++)
                {
                    if (inUse[num42 * 16 + num43])
                    {
                        BsW(1, 1);
                    }
                    else
                    {
                        BsW(1, 0);
                    }
                }
            }

            BsW(3, num3);
            BsW(15, num);
            for (int num44 = 0; num44 < num; num44++)
            {
                for (int num45 = 0; num45 < selectorMtf[num44]; num45++)
                {
                    BsW(1, 1);
                }

                BsW(1, 0);
            }

            for (int num46 = 0; num46 < num3; num46++)
            {
                int num47 = array[num46][0];
                BsW(5, num47);
                for (int num48 = 0; num48 < num2; num48++)
                {
                    for (; num47 < array[num46][num48]; num47++)
                    {
                        BsW(2, 2);
                    }

                    while (num47 > array[num46][num48])
                    {
                        BsW(2, 3);
                        num47--;
                    }

                    BsW(1, 0);
                }
            }

            int num49 = 0;
            num6 = 0;
            while (num6 < nMTF)
            {
                int num8 = num6 + 50 - 1;
                if (num8 >= nMTF)
                {
                    num8 = nMTF - 1;
                }

                for (int num50 = num6; num50 <= num8; num50++)
                {
                    BsW(array[(uint)selector[num49]][szptr[num50]], array6[(uint)selector[num49]][szptr[num50]]);
                }

                num6 = num8 + 1;
                num49++;
            }

            if (num49 != num)
            {
                Panic();
            }
        }

        private void MoveToFrontCodeAndSend()
        {
            BsPutIntVS(24, origPtr);
            GenerateMTFValues();
            SendMTFValues();
        }

        private void SimpleSort(int lo, int hi, int d)
        {
            int num = hi - lo + 1;
            if (num < 2)
            {
                return;
            }

            int i;
            for (i = 0; increments[i] < num; i++)
            {
            }

            for (i--; i >= 0; i--)
            {
                int num2 = increments[i];
                int num3 = lo + num2;
                while (num3 <= hi)
                {
                    int num4 = zptr[num3];
                    int num5 = num3;
                    while (FullGtU(zptr[num5 - num2] + d, num4 + d))
                    {
                        zptr[num5] = zptr[num5 - num2];
                        num5 -= num2;
                        if (num5 <= lo + num2 - 1)
                        {
                            break;
                        }
                    }

                    zptr[num5] = num4;
                    num3++;
                    if (num3 > hi)
                    {
                        break;
                    }

                    num4 = zptr[num3];
                    num5 = num3;
                    while (FullGtU(zptr[num5 - num2] + d, num4 + d))
                    {
                        zptr[num5] = zptr[num5 - num2];
                        num5 -= num2;
                        if (num5 <= lo + num2 - 1)
                        {
                            break;
                        }
                    }

                    zptr[num5] = num4;
                    num3++;
                    if (num3 > hi)
                    {
                        break;
                    }

                    num4 = zptr[num3];
                    num5 = num3;
                    while (FullGtU(zptr[num5 - num2] + d, num4 + d))
                    {
                        zptr[num5] = zptr[num5 - num2];
                        num5 -= num2;
                        if (num5 <= lo + num2 - 1)
                        {
                            break;
                        }
                    }

                    zptr[num5] = num4;
                    num3++;
                    if (workDone > workLimit && firstAttempt)
                    {
                        return;
                    }
                }
            }
        }

        private void Vswap(int p1, int p2, int n)
        {
            int num = 0;
            while (n > 0)
            {
                num = zptr[p1];
                zptr[p1] = zptr[p2];
                zptr[p2] = num;
                p1++;
                p2++;
                n--;
            }
        }

        private void QSort3(int loSt, int hiSt, int dSt)
        {
            StackElement[] array = new StackElement[1000];
            int num = 0;
            array[num].ll = loSt;
            array[num].hh = hiSt;
            array[num].dd = dSt;
            num++;
            while (num > 0)
            {
                if (num >= 1000)
                {
                    Panic();
                }

                num--;
                int ll = array[num].ll;
                int hh = array[num].hh;
                int dd = array[num].dd;
                if (hh - ll < 20 || dd > 10)
                {
                    SimpleSort(ll, hh, dd);
                    if (workDone > workLimit && firstAttempt)
                    {
                        break;
                    }

                    continue;
                }

                int num2 = Med3(block[zptr[ll] + dd + 1], block[zptr[hh] + dd + 1], block[zptr[ll + hh >> 1] + dd + 1]);
                int num3;
                int num4 = (num3 = ll);
                int num5;
                int num6 = (num5 = hh);
                int num7;
                while (true)
                {
                    if (num4 <= num6)
                    {
                        num7 = block[zptr[num4] + dd + 1] - num2;
                        if (num7 == 0)
                        {
                            int num8 = zptr[num4];
                            zptr[num4] = zptr[num3];
                            zptr[num3] = num8;
                            num3++;
                            num4++;
                            continue;
                        }

                        if (num7 <= 0)
                        {
                            num4++;
                            continue;
                        }
                    }

                    while (num4 <= num6)
                    {
                        num7 = block[zptr[num6] + dd + 1] - num2;
                        if (num7 == 0)
                        {
                            int num9 = zptr[num6];
                            zptr[num6] = zptr[num5];
                            zptr[num5] = num9;
                            num5--;
                            num6--;
                        }
                        else
                        {
                            if (num7 < 0)
                            {
                                break;
                            }

                            num6--;
                        }
                    }

                    if (num4 > num6)
                    {
                        break;
                    }

                    int num10 = zptr[num4];
                    zptr[num4] = zptr[num6];
                    zptr[num6] = num10;
                    num4++;
                    num6--;
                }

                if (num5 < num3)
                {
                    array[num].ll = ll;
                    array[num].hh = hh;
                    array[num].dd = dd + 1;
                    num++;
                    continue;
                }

                num7 = ((num3 - ll < num4 - num3) ? (num3 - ll) : (num4 - num3));
                Vswap(ll, num4 - num7, num7);
                int num11 = ((hh - num5 < num5 - num6) ? (hh - num5) : (num5 - num6));
                Vswap(num4, hh - num11 + 1, num11);
                num7 = ll + num4 - num3 - 1;
                num11 = hh - (num5 - num6) + 1;
                array[num].ll = ll;
                array[num].hh = num7;
                array[num].dd = dd;
                num++;
                array[num].ll = num7 + 1;
                array[num].hh = num11 - 1;
                array[num].dd = dd + 1;
                num++;
                array[num].ll = num11;
                array[num].hh = hh;
                array[num].dd = dd;
                num++;
            }
        }

        private void MainSort()
        {
            int[] array = new int[256];
            int[] array2 = new int[256];
            bool[] array3 = new bool[256];
            for (int i = 0; i < 20; i++)
            {
                block[last + i + 2] = block[i % (last + 1) + 1];
            }

            for (int i = 0; i <= last + 20; i++)
            {
                quadrant[i] = 0;
            }

            block[0] = block[last + 1];
            if (last < 4000)
            {
                for (int i = 0; i <= last; i++)
                {
                    zptr[i] = i;
                }

                firstAttempt = false;
                workDone = (workLimit = 0);
                SimpleSort(0, last, 0);
                return;
            }

            int num = 0;
            for (int i = 0; i <= 255; i++)
            {
                array3[i] = false;
            }

            for (int i = 0; i <= 65536; i++)
            {
                ftab[i] = 0;
            }

            int num2 = block[0];
            for (int i = 0; i <= last; i++)
            {
                int num3 = block[i + 1];
                ftab[(num2 << 8) + num3]++;
                num2 = num3;
            }

            for (int i = 1; i <= 65536; i++)
            {
                ftab[i] += ftab[i - 1];
            }

            num2 = block[1];
            int num4;
            for (int i = 0; i < last; i++)
            {
                int num3 = block[i + 2];
                num4 = (num2 << 8) + num3;
                num2 = num3;
                ftab[num4]--;
                zptr[ftab[num4]] = i;
            }

            num4 = (block[last + 1] << 8) + block[1];
            ftab[num4]--;
            zptr[ftab[num4]] = last;
            for (int i = 0; i <= 255; i++)
            {
                array[i] = i;
            }

            int num5 = 1;
            do
            {
                num5 = 3 * num5 + 1;
            }
            while (num5 <= 256);
            do
            {
                num5 /= 3;
                for (int i = num5; i <= 255; i++)
                {
                    int num6 = array[i];
                    num4 = i;
                    while (ftab[array[num4 - num5] + 1 << 8] - ftab[array[num4 - num5] << 8] > ftab[num6 + 1 << 8] - ftab[num6 << 8])
                    {
                        array[num4] = array[num4 - num5];
                        num4 -= num5;
                        if (num4 <= num5 - 1)
                        {
                            break;
                        }
                    }

                    array[num4] = num6;
                }
            }
            while (num5 != 1);
            for (int i = 0; i <= 255; i++)
            {
                int num7 = array[i];
                for (num4 = 0; num4 <= 255; num4++)
                {
                    int num8 = (num7 << 8) + num4;
                    if ((ftab[num8] & 0x200000) == 2097152)
                    {
                        continue;
                    }

                    int num9 = ftab[num8] & -2097153;
                    int num10 = (ftab[num8 + 1] & -2097153) - 1;
                    if (num10 > num9)
                    {
                        QSort3(num9, num10, 2);
                        num += num10 - num9 + 1;
                        if (workDone > workLimit && firstAttempt)
                        {
                            return;
                        }
                    }

                    ftab[num8] |= 2097152;
                }

                array3[num7] = true;
                if (i < 255)
                {
                    int num11 = ftab[num7 << 8] & -2097153;
                    int num12 = (ftab[num7 + 1 << 8] & -2097153) - num11;
                    int j;
                    for (j = 0; num12 >> j > 65534; j++)
                    {
                    }

                    for (num4 = 0; num4 < num12; num4++)
                    {
                        int num13 = zptr[num11 + num4];
                        int num14 = num4 >> j;
                        quadrant[num13] = num14;
                        if (num13 < 20)
                        {
                            quadrant[num13 + last + 1] = num14;
                        }
                    }

                    if (num12 - 1 >> j > 65535)
                    {
                        Panic();
                    }
                }

                for (num4 = 0; num4 <= 255; num4++)
                {
                    array2[num4] = ftab[(num4 << 8) + num7] & -2097153;
                }

                for (num4 = ftab[num7 << 8] & -2097153; num4 < (ftab[num7 + 1 << 8] & -2097153); num4++)
                {
                    num2 = block[zptr[num4]];
                    if (!array3[num2])
                    {
                        zptr[array2[num2]] = ((zptr[num4] == 0) ? last : (zptr[num4] - 1));
                        array2[num2]++;
                    }
                }

                for (num4 = 0; num4 <= 255; num4++)
                {
                    ftab[(num4 << 8) + num7] |= 2097152;
                }
            }
        }

        private void RandomiseBlock()
        {
            int num = 0;
            int num2 = 0;
            for (int i = 0; i < 256; i++)
            {
                inUse[i] = false;
            }

            for (int i = 0; i <= last; i++)
            {
                if (num == 0)
                {
                    num = BZip2Constants.RandomNumbers[num2];
                    num2++;
                    if (num2 == 512)
                    {
                        num2 = 0;
                    }
                }

                num--;
                block[i + 1] ^= (byte)((num == 1) ? 1 : 0);
                block[i + 1] &= byte.MaxValue;
                inUse[block[i + 1]] = true;
            }
        }

        private void DoReversibleTransformation()
        {
            workLimit = workFactor * last;
            workDone = 0;
            blockRandomised = false;
            firstAttempt = true;
            MainSort();
            if (workDone > workLimit && firstAttempt)
            {
                RandomiseBlock();
                workLimit = (workDone = 0);
                blockRandomised = true;
                firstAttempt = false;
                MainSort();
            }

            origPtr = -1;
            for (int i = 0; i <= last; i++)
            {
                if (zptr[i] == 0)
                {
                    origPtr = i;
                    break;
                }
            }

            if (origPtr == -1)
            {
                Panic();
            }
        }

        private bool FullGtU(int i1, int i2)
        {
            byte b = block[i1 + 1];
            byte b2 = block[i2 + 1];
            if (b != b2)
            {
                return b > b2;
            }

            i1++;
            i2++;
            b = block[i1 + 1];
            b2 = block[i2 + 1];
            if (b != b2)
            {
                return b > b2;
            }

            i1++;
            i2++;
            b = block[i1 + 1];
            b2 = block[i2 + 1];
            if (b != b2)
            {
                return b > b2;
            }

            i1++;
            i2++;
            b = block[i1 + 1];
            b2 = block[i2 + 1];
            if (b != b2)
            {
                return b > b2;
            }

            i1++;
            i2++;
            b = block[i1 + 1];
            b2 = block[i2 + 1];
            if (b != b2)
            {
                return b > b2;
            }

            i1++;
            i2++;
            b = block[i1 + 1];
            b2 = block[i2 + 1];
            if (b != b2)
            {
                return b > b2;
            }

            i1++;
            i2++;
            int num = last + 1;
            do
            {
                b = block[i1 + 1];
                b2 = block[i2 + 1];
                if (b != b2)
                {
                    return b > b2;
                }

                int num2 = quadrant[i1];
                int num3 = quadrant[i2];
                if (num2 != num3)
                {
                    return num2 > num3;
                }

                i1++;
                i2++;
                b = block[i1 + 1];
                b2 = block[i2 + 1];
                if (b != b2)
                {
                    return b > b2;
                }

                num2 = quadrant[i1];
                num3 = quadrant[i2];
                if (num2 != num3)
                {
                    return num2 > num3;
                }

                i1++;
                i2++;
                b = block[i1 + 1];
                b2 = block[i2 + 1];
                if (b != b2)
                {
                    return b > b2;
                }

                num2 = quadrant[i1];
                num3 = quadrant[i2];
                if (num2 != num3)
                {
                    return num2 > num3;
                }

                i1++;
                i2++;
                b = block[i1 + 1];
                b2 = block[i2 + 1];
                if (b != b2)
                {
                    return b > b2;
                }

                num2 = quadrant[i1];
                num3 = quadrant[i2];
                if (num2 != num3)
                {
                    return num2 > num3;
                }

                i1++;
                i2++;
                if (i1 > last)
                {
                    i1 -= last;
                    i1--;
                }

                if (i2 > last)
                {
                    i2 -= last;
                    i2--;
                }

                num -= 4;
                workDone++;
            }
            while (num >= 0);
            return false;
        }

        private void AllocateCompressStructures()
        {
            int num = 100000 * blockSize100k;
            block = new byte[num + 1 + 20];
            quadrant = new int[num + 20];
            zptr = new int[num];
            ftab = new int[65537];
            if (block != null && quadrant != null && zptr != null)
            {
                _ = ftab;
            }

            szptr = new short[2 * num];
        }

        private void GenerateMTFValues()
        {
            char[] array = new char[256];
            MakeMaps();
            int num = nInUse + 1;
            for (int i = 0; i <= num; i++)
            {
                mtfFreq[i] = 0;
            }

            int num2 = 0;
            int num3 = 0;
            for (int i = 0; i < nInUse; i++)
            {
                array[i] = (char)i;
            }

            for (int i = 0; i <= last; i++)
            {
                char c = unseqToSeq[block[zptr[i]]];
                int num4 = 0;
                char c2 = array[num4];
                while (c != c2)
                {
                    num4++;
                    char c3 = c2;
                    c2 = array[num4];
                    array[num4] = c3;
                }

                array[0] = c2;
                if (num4 == 0)
                {
                    num3++;
                    continue;
                }

                if (num3 > 0)
                {
                    num3--;
                    while (true)
                    {
                        switch (num3 % 2)
                        {
                            case 0:
                                szptr[num2] = 0;
                                num2++;
                                mtfFreq[0]++;
                                break;

                            case 1:
                                szptr[num2] = 1;
                                num2++;
                                mtfFreq[1]++;
                                break;
                        }

                        if (num3 < 2)
                        {
                            break;
                        }

                        num3 = (num3 - 2) / 2;
                    }

                    num3 = 0;
                }

                szptr[num2] = (short)(num4 + 1);
                num2++;
                mtfFreq[num4 + 1]++;
            }

            if (num3 > 0)
            {
                num3--;
                while (true)
                {
                    switch (num3 % 2)
                    {
                        case 0:
                            szptr[num2] = 0;
                            num2++;
                            mtfFreq[0]++;
                            break;

                        case 1:
                            szptr[num2] = 1;
                            num2++;
                            mtfFreq[1]++;
                            break;
                    }

                    if (num3 < 2)
                    {
                        break;
                    }

                    num3 = (num3 - 2) / 2;
                }
            }

            szptr[num2] = (short)num;
            num2++;
            mtfFreq[num]++;
            nMTF = num2;
        }

        private static void Panic()
        {
            throw new Exception("BZip2 output stream panic");
        }

        private static void HbMakeCodeLengths(char[] len, int[] freq, int alphaSize, int maxLen)
        {
            int[] array = new int[260];
            int[] array2 = new int[516];
            int[] array3 = new int[516];
            for (int i = 0; i < alphaSize; i++)
            {
                array2[i + 1] = ((freq[i] == 0) ? 1 : freq[i]) << 8;
            }

            while (true)
            {
                int num = alphaSize;
                int num2 = 0;
                array[0] = 0;
                array2[0] = 0;
                array3[0] = -2;
                for (int j = 1; j <= alphaSize; j++)
                {
                    array3[j] = -1;
                    num2++;
                    array[num2] = j;
                    int num3 = num2;
                    int num4 = array[num3];
                    while (array2[num4] < array2[array[num3 >> 1]])
                    {
                        array[num3] = array[num3 >> 1];
                        num3 >>= 1;
                    }

                    array[num3] = num4;
                }

                if (num2 >= 260)
                {
                    Panic();
                }

                while (num2 > 1)
                {
                    int num5 = array[1];
                    array[1] = array[num2];
                    num2--;
                    int num6 = 1;
                    int num7 = 0;
                    int num8 = array[num6];
                    while (true)
                    {
                        num7 = num6 << 1;
                        if (num7 > num2)
                        {
                            break;
                        }

                        if (num7 < num2 && array2[array[num7 + 1]] < array2[array[num7]])
                        {
                            num7++;
                        }

                        if (array2[num8] < array2[array[num7]])
                        {
                            break;
                        }

                        array[num6] = array[num7];
                        num6 = num7;
                    }

                    array[num6] = num8;
                    int num9 = array[1];
                    array[1] = array[num2];
                    num2--;
                    num6 = 1;
                    num7 = 0;
                    num8 = array[num6];
                    while (true)
                    {
                        num7 = num6 << 1;
                        if (num7 > num2)
                        {
                            break;
                        }

                        if (num7 < num2 && array2[array[num7 + 1]] < array2[array[num7]])
                        {
                            num7++;
                        }

                        if (array2[num8] < array2[array[num7]])
                        {
                            break;
                        }

                        array[num6] = array[num7];
                        num6 = num7;
                    }

                    array[num6] = num8;
                    num++;
                    array3[num5] = (array3[num9] = num);
                    array2[num] = (int)((array2[num5] & 0xFFFFFF00u) + (array2[num9] & 0xFFFFFF00u)) | (1 + (((array2[num5] & 0xFF) > (array2[num9] & 0xFF)) ? (array2[num5] & 0xFF) : (array2[num9] & 0xFF)));
                    array3[num] = -1;
                    num2++;
                    array[num2] = num;
                    num6 = num2;
                    num8 = array[num6];
                    while (array2[num8] < array2[array[num6 >> 1]])
                    {
                        array[num6] = array[num6 >> 1];
                        num6 >>= 1;
                    }

                    array[num6] = num8;
                }

                if (num >= 516)
                {
                    Panic();
                }

                bool flag = false;
                for (int k = 1; k <= alphaSize; k++)
                {
                    int num10 = 0;
                    int num11 = k;
                    while (array3[num11] >= 0)
                    {
                        num11 = array3[num11];
                        num10++;
                    }

                    len[k - 1] = (char)num10;
                    if (num10 > maxLen)
                    {
                        flag = true;
                    }
                }

                if (!flag)
                {
                    break;
                }

                for (int l = 1; l < alphaSize; l++)
                {
                    int num10 = array2[l] >> 8;
                    num10 = 1 + num10 / 2;
                    array2[l] = num10 << 8;
                }
            }
        }

        private static void HbAssignCodes(int[] code, char[] length, int minLen, int maxLen, int alphaSize)
        {
            int num = 0;
            for (int i = minLen; i <= maxLen; i++)
            {
                for (int j = 0; j < alphaSize; j++)
                {
                    if (length[j] == i)
                    {
                        code[j] = num;
                        num++;
                    }
                }

                num <<= 1;
            }
        }

        private static byte Med3(byte a, byte b, byte c)
        {
            if (a > b)
            {
                byte b2 = a;
                a = b;
                b = b2;
            }

            if (b > c)
            {
                byte b2 = b;
                b = c;
                c = b2;
            }

            if (a > b)
            {
                b = a;
            }

            return b;
        }
    }
}