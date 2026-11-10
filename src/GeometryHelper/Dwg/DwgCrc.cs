namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The 16-bit check sum of a DWG file, which the Open Design Specification calls the 8-bit CRC after its table of 256
    /// entries: the reflected polynomial 0xA001, each byte folded in as <c>(crc >> 8) ^ table[(crc ^ b) &amp; 0xFF]</c>.
    /// Sections and objects start it from 0xC0C1, the file header from 0.
    /// </summary>
    internal static class DwgCrc
    {
        /// <summary>The start value of the check sum of a section and of an object.</summary>
        public const int Seed = 0xC0C1;

        private static readonly ushort[] Table = MakeTable();

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
