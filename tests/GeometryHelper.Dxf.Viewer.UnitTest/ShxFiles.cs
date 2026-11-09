using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Small compiled SHX fonts made in code, byte by byte as AutoCAD compiles them, so that no font file of Autodesk's is
    /// shipped: a unifont, a font of shapes and a big font, each of a few letters whose strokes are worked out by hand.
    /// </summary>
    internal static class ShxFiles
    {
        /// <summary>
        /// A unifont 10 above and 2 below: I a line up from (1, 0) moving the pen 4; O a whole circle of radius 3 about (3, 3)
        /// moving it 9; D a half circle by a bulge of 127 from (0, 0) to (6, 0) below its chord, moving it 7; S the I as a shape
        /// within, then at half the scale a line up 2 from (4, 0), moving it 5; E two lines by moves of signed bytes, the pen's
        /// place pushed and popped and a code for vertical text alone passed over, moving it 6; F a quarter arc of fractions of
        /// octants of radius 3 from (0, 0) to (-3, 3); a space moving the pen 5 and a question mark moving it 4.
        /// </summary>
        internal static byte[] Unifont()
        {
            var shapes = new SortedDictionary<int, byte[]>
            {
                ['I'] = new byte[] { 2, 0x10, 1, 0xA4, 2, 0xAC, 0x30, 0 },
                ['O'] = new byte[] { 2, 0x30, 1, 10, 3, 0x60, 2, 0x60, 0 },
                ['D'] = new byte[] { 1, 12, 6, 0, 127, 2, 0x10, 0 },
                ['S'] = new byte[] { 7, 0, (byte)'I', 3, 2, 1, 0x44, 2, 0x20, 0 },
                ['E'] = new byte[] { 1, 5, 9, 0, 10, 4, 0, 0, 0, 6, 14, 0x24, 2, 0x60, 0 },
                ['F'] = new byte[] { 1, 11, 0, 0, 0, 3, 0x02, 0 },
                [' '] = new byte[] { 2, 0x50, 0 },
                ['?'] = new byte[] { 2, 0x40, 0 },
            };
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\x1A"));
            byte[] info = Encoding.ASCII.GetBytes("TEST\0").Concat(new byte[] { 10, 2, 0, 0, 0, 0 }).ToArray();
            U16(bytes, shapes.Count + 1);
            U16(bytes, 0);
            U16(bytes, info.Length);
            bytes.AddRange(info);

            foreach (KeyValuePair<int, byte[]> shape in shapes)
            {
                byte[] record = new byte[] { (byte)'n', 0 }.Concat(shape.Value).ToArray();
                U16(bytes, shape.Key);
                U16(bytes, record.Length);
                bytes.AddRange(record);
            }

            return bytes.ToArray();
        }

        /// <summary>A font of shapes 6 above and 2 below: A a line up 6 from (0, 0) moving the pen 4; 127, the degree sign, a line across 2 at the top; B the A drawn as a shape within.</summary>
        internal static byte[] Shapes()
        {
            var shapes = new SortedDictionary<int, byte[]>
            {
                [0] = new byte[] { 6, 2, 0, 0 },
                ['A'] = new byte[] { 1, 0x64, 2, 0x6C, 0x40, 0 },
                ['B'] = new byte[] { 7, (byte)'A', 0 },
                [127] = new byte[] { 2, 0x64, 1, 0x20, 2, 0x6C, 0 },
            };
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 shapes 1.0\r\n\x1A"));
            U16(bytes, shapes.Keys.First());
            U16(bytes, shapes.Keys.Last());
            U16(bytes, shapes.Count);
            var records = shapes.ToDictionary(s => s.Key, s => Encoding.ASCII.GetBytes("n\0").Concat(s.Value).ToArray());

            foreach (KeyValuePair<int, byte[]> record in records.OrderBy(r => r.Key))
            {
                U16(bytes, record.Key);
                U16(bytes, record.Value.Length);
            }

            foreach (KeyValuePair<int, byte[]> record in records.OrderBy(r => r.Key))
            {
                bytes.AddRange(record.Value);
            }

            bytes.AddRange(Encoding.ASCII.GetBytes("EOF"));
            return bytes.ToArray();
        }

        /// <summary>
        /// A big font whose shape 0 gives no height above, drawn 8 to its height as AutoCAD draws gbcbig.shx; its pairs opened
        /// by 0x81 to 0xFE; D6D0, 中 in GBK, a line up 8 from (2, 0) moving the pen 10.
        /// </summary>
        internal static byte[] Big()
        {
            var shapes = new SortedDictionary<int, byte[]>
            {
                [0] = new byte[] { 0, 64, 0, 0 },
                [0xD6D0] = new byte[] { 2, 0x20, 1, 0x84, 2, 0x8C, 0x80, 0 },
            };
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 bigfont 1.0\r\n\x1A"));
            U16(bytes, 8);
            U16(bytes, shapes.Count);
            U16(bytes, 1);
            U16(bytes, 0x81);
            U16(bytes, 0xFE);
            int place = bytes.Count + (shapes.Count * 8);
            var records = shapes.Select(s => new KeyValuePair<int, byte[]>(s.Key, Encoding.ASCII.GetBytes("n\0").Concat(s.Value).ToArray())).ToList();

            foreach (KeyValuePair<int, byte[]> record in records)
            {
                U16(bytes, record.Key);
                U16(bytes, record.Value.Length);
                U16(bytes, place & 0xFFFF);
                U16(bytes, place >> 16);
                place += record.Value.Length;
            }

            foreach (KeyValuePair<int, byte[]> record in records)
            {
                bytes.AddRange(record.Value);
            }

            return bytes.ToArray();
        }

        private static void U16(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value & 0xFF));
            bytes.Add((byte)((value >> 8) & 0xFF));
        }
    }

    /// <summary>A folder of its own under the temporary folder, deleted after the test.</summary>
    internal sealed class FontFolder : IDisposable
    {
        public FontFolder()
        {
            FullPath = Path.Combine(Path.GetTempPath(), "gh-fonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(FullPath);
        }

        public string FullPath { get; }

        public string Write(string name, byte[] bytes)
        {
            string path = Path.Combine(FullPath, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(FullPath, true);
            }
            catch (IOException)
            {
                // A folder still in use is left to the system's clean-up.
            }
        }
    }
}
