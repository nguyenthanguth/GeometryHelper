using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Binary data as DXF writes it: groups 310 to 319 of hexadecimal, two letters a byte, up to 127 bytes a line as AutoCAD
    /// writes them.
    /// </summary>
    internal static class DxfBinary
    {
        /// <summary>The most bytes AutoCAD writes on one line of binary data: 254 letters.</summary>
        internal const int LineBytes = 127;

        /// <summary>The bytes of a line of hexadecimal, letters of either case; a letter that is no digit, and an odd last one, read past.</summary>
        internal static byte[] Bytes(string hex)
        {
            var bytes = new List<byte>(hex.Length / 2);
            int high = -1;

            foreach (char c in hex)
            {
                int digit = c >= '0' && c <= '9' ? c - '0' : c >= 'A' && c <= 'F' ? c - 'A' + 10 : c >= 'a' && c <= 'f' ? c - 'a' + 10 : -1;

                if (digit < 0)
                {
                    continue;
                }

                if (high < 0)
                {
                    high = digit;
                }
                else
                {
                    bytes.Add((byte)((high << 4) | digit));
                    high = -1;
                }
            }

            return bytes.ToArray();
        }

        /// <summary>The lines of hexadecimal of bytes, upper case, 127 bytes a line, as AutoCAD writes them; none for no bytes.</summary>
        internal static List<string> Lines(byte[] bytes)
        {
            var lines = new List<string>();
            const string Digits = "0123456789ABCDEF";

            for (int at = 0; at < bytes.Length; at += LineBytes)
            {
                int count = System.Math.Min(LineBytes, bytes.Length - at);
                var line = new StringBuilder(count * 2);

                for (int i = at; i < at + count; i++)
                {
                    line.Append(Digits[bytes[i] >> 4]).Append(Digits[bytes[i] & 0xF]);
                }

                lines.Add(line.ToString());
            }

            return lines;
        }

        /// <summary>Whether two runs of bytes are the same, byte for byte.</summary>
        internal static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
