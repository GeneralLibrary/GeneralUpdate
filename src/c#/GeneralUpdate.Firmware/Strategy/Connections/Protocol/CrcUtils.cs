using System;

namespace GeneralUpdate.Firmware.Strategy.Connections.Protocol
{
    /// <summary>
    /// CRC computation utilities for serial transfer protocols.
    /// Provides CRC-8 (polynomial 0x07) and CRC-16 (polynomial 0x1021)
    /// with precomputed lookup tables.
    /// </summary>
    internal static class CrcUtils
    {
        // CRC-8 lookup table (polynomial 0x07)
        private static readonly byte[] Crc8Table = BuildCrc8Table();

        // CRC-16 lookup table (polynomial 0x1021)
        private static readonly ushort[] Crc16Table = BuildCrc16Table();

        /// <summary>
        /// Computes CRC-8 (polynomial 0x07) using precomputed lookup table.
        /// </summary>
        /// <param name="data">The data buffer.</param>
        /// <param name="offset">Start offset in the buffer.</param>
        /// <param name="length">Number of bytes to process.</param>
        /// <returns>8-bit CRC value.</returns>
        public static byte ComputeCrc8(byte[] data, int offset, int length)
        {
            byte crc = 0;
            for (int i = offset; i < offset + length; i++)
            {
                crc = Crc8Table[crc ^ data[i]];
            }
            return crc;
        }

        /// <summary>
        /// Computes CRC-16 (polynomial 0x1021, XMODEM/YMODEM standard)
        /// using precomputed lookup table.
        /// </summary>
        /// <param name="data">The data buffer.</param>
        /// <param name="offset">Start offset in the buffer.</param>
        /// <param name="length">Number of bytes to process.</param>
        /// <returns>16-bit CRC value.</returns>
        public static ushort ComputeCrc16(byte[] data, int offset, int length)
        {
            ushort crc = 0;
            for (int i = offset; i < offset + length; i++)
            {
                crc = (ushort)((Crc16Table[((crc >> 8) ^ data[i]) & 0xFF] ^ (crc << 8)) & 0xFFFF);
            }
            return crc;
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
    }
}
