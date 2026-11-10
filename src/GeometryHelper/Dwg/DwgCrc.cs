using System;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The check sums of a DWG file. The 16-bit one, which the Open Design Specification calls the 8-bit CRC after its
    /// table of 256 entries: the reflected polynomial 0xA001, each byte folded in as
    /// <c>(crc >> 8) ^ table[(crc ^ b) &amp; 0xFF]</c>; sections and objects start it from 0xC0C1, the file header of R2000
    /// from 0. From R2004 on also the 32-bit CRC of the file header and the sums of the pages.
    /// </summary>
    internal static class DwgCrc
    {
        /// <summary>The start value of the check sum of a section and of an object.</summary>
        public const int Seed = 0xC0C1;

        private static readonly ushort[] Table = MakeTable();

        private static readonly uint[] Table32 = MakeTable32();

        /// <summary>The check sum of <paramref name="count"/> bytes from <paramref name="start"/>, begun from <paramref name="seed"/>.</summary>
        public static int Of(byte[] data, long start, long count, int seed)
        {
            int crc = seed & 0xFFFF;

            for (long i = start; i < start + count; i++)
            {
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
            }

            return crc;
        }

        /// <summary>
        /// The 32-bit check sum of R2004 and later, the CRC-32 of zip (the reflected polynomial 0xEDB88320, inverted before
        /// and after), of <paramref name="count"/> bytes from <paramref name="start"/>, begun from <paramref name="seed"/>.
        /// </summary>
        public static uint Of32(byte[] data, long start, long count, uint seed)
        {
            uint crc = ~seed;

            for (long i = start; i < start + count; i++)
            {
                crc = (crc >> 8) ^ Table32[(crc ^ data[i]) & 0xFF];
            }

            return ~crc;
        }

        /// <summary>
        /// The check sum of a page of R2004 and later, which the Open Design Specification calls the section page
        /// checksum: two running sums of the bytes, each taken modulo 0xFFF1 every 0x15B0 bytes, the second in the high half.
        /// </summary>
        public static uint OfPage(byte[] data, long start, long count, uint seed)
        {
            uint sum1 = seed & 0xFFFF;
            uint sum2 = seed >> 16;
            long at = start;

            while (count > 0)
            {
                long chunk = Math.Min(0x15B0, count);
                count -= chunk;

                for (long i = 0; i < chunk; i++)
                {
                    sum1 += data[at++];
                    sum2 += sum1;
                }

                sum1 %= 0xFFF1;
                sum2 %= 0xFFF1;
            }

            return (sum2 << 16) | (sum1 & 0xFFFF);
        }

        private static uint[] MakeTable32()
        {
            var table = new uint[256];

            for (uint i = 0; i < 256; i++)
            {
                uint value = i;

                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? (value >> 1) ^ 0xEDB88320 : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }

        private static ushort[] MakeTable()
        {
            var table = new ushort[256];

            for (int i = 0; i < 256; i++)
            {
                int value = i;

                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? (value >> 1) ^ 0xA001 : value >> 1;
                }

                table[i] = (ushort)value;
            }

            return table;
        }
    }
}
