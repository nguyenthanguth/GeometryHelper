using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A Windows Metafile built in code, record by record, for the WMFIN tests: no file of anyone's is kept in the repository.
    /// The records are those of [MS-WMF] in the order GDI wrote them; a call gives the parameters in the order the file holds
    /// them, so that <see cref="LineTo"/> takes x then y and writes y then x, as the file does.
    /// </summary>
    internal sealed class WmfBytes
    {
        private readonly MemoryStream _records = new MemoryStream();
        private readonly BinaryWriter _writer;
        private int _largest;

        public WmfBytes()
        {
            _writer = new BinaryWriter(_records);
        }

        private void Record(ushort function, byte[] parameters)
        {
            int words = 3 + parameters.Length / 2;
            _largest = Math.Max(_largest, words);
            _writer.Write((uint)words);
            _writer.Write(function);
            _writer.Write(parameters);
        }

        private static byte[] Words(params int[] values)
        {
            var bytes = new MemoryStream();
            var writer = new BinaryWriter(bytes);

            foreach (int value in values)
            {
                writer.Write(unchecked((short)value));
            }

            return bytes.ToArray();
        }

        private static byte[] Join(params byte[][] parts)
        {
            var bytes = new MemoryStream();

            foreach (byte[] part in parts)
            {
                bytes.Write(part, 0, part.Length);
            }

            return bytes.ToArray();
        }

        private static byte[] Padded(string text)
        {
            byte[] letters = Encoding.GetEncoding(28591).GetBytes(text);
            Array.Resize(ref letters, letters.Length + (letters.Length % 2));
            return letters;
        }

        public WmfBytes WindowOrg(int x, int y)
        {
            Record(0x020B, Words(y, x));
            return this;
        }

        public WmfBytes WindowExt(int x, int y)
        {
            Record(0x020C, Words(y, x));
            return this;
        }

        public WmfBytes Pen(int style, int width, byte red, byte green, byte blue)
        {
            Record(0x02FA, Join(Words(style, width, 0), new[] { red, green, blue, (byte)0 }));
            return this;
        }

        public WmfBytes Brush(int style, byte red, byte green, byte blue)
        {
            Record(0x02FC, Join(Words(style), new[] { red, green, blue, (byte)0 }, Words(0)));
            return this;
        }

        public WmfBytes Font(
            int height,
            int weight,
            bool italic,
            int escapement,
            string face,
            int charset = 0,
            int pitchAndFamily = 0
        )
        {
            byte[] name = new byte[32];
            Encoding.ASCII.GetBytes(face).CopyTo(name, 0);
            Record(
                0x02FB,
                Join(
                    Words(height, 0, escapement, 0, weight),
                    new byte[] { (byte)(italic ? 1 : 0), 0, 0, (byte)charset, 0, 0, 0, (byte)pitchAndFamily },
                    name
                )
            );
            return this;
        }

        public WmfBytes SaveDC()
        {
            Record(0x001E, Array.Empty<byte>());
            return this;
        }

        /// <summary>Gives RestoreDC a number: below nought that many saves back, above it the save of that number.</summary>
        public WmfBytes RestoreDC(int number)
        {
            Record(0x0127, Words(number));
            return this;
        }

        public WmfBytes IntersectClipRect(int left, int top, int right, int bottom)
        {
            Record(0x0416, Words(bottom, right, top, left));
            return this;
        }

        public WmfBytes ExcludeClipRect(int left, int top, int right, int bottom)
        {
            Record(0x0415, Words(bottom, right, top, left));
            return this;
        }

        /// <summary>A TextOut of bytes as they are, for a text in a code page: the bytes of the text, not the letters.</summary>
        public WmfBytes TextOutBytes(int x, int y, params byte[] text)
        {
            byte[] letters = text.Length % 2 == 0 ? text : Join(text, new byte[] { 0 });
            Record(0x0521, Join(Words(text.Length), letters, Words(y, x)));
            return this;
        }

        public WmfBytes Select(int index)
        {
            Record(0x012D, Words(index));
            return this;
        }

        public WmfBytes Delete(int index)
        {
            Record(0x01F0, Words(index));
            return this;
        }

        public WmfBytes MoveTo(int x, int y)
        {
            Record(0x0214, Words(y, x));
            return this;
        }

        public WmfBytes LineTo(int x, int y)
        {
            Record(0x0213, Words(y, x));
            return this;
        }

        public WmfBytes Polyline(params int[] xy)
        {
            Record(0x0325, Join(Words(xy.Length / 2), Words(xy)));
            return this;
        }

        public WmfBytes Polygon(params int[] xy)
        {
            Record(0x0324, Join(Words(xy.Length / 2), Words(xy)));
            return this;
        }

        public WmfBytes PolyPolygon(int[] counts, params int[] xy)
        {
            Record(0x0538, Join(Words(counts.Length), Words(counts), Words(xy)));
            return this;
        }

        public WmfBytes Rectangle(int left, int top, int right, int bottom)
        {
            Record(0x041B, Words(bottom, right, top, left));
            return this;
        }

        public WmfBytes Ellipse(int left, int top, int right, int bottom)
        {
            Record(0x0418, Words(bottom, right, top, left));
            return this;
        }

        public WmfBytes RoundRect(int left, int top, int right, int bottom, int width, int height)
        {
            Record(0x061C, Words(height, width, bottom, right, top, left));
            return this;
        }

        private WmfBytes Sector(
            ushort function,
            int left,
            int top,
            int right,
            int bottom,
            int xs,
            int ys,
            int xe,
            int ye
        )
        {
            Record(function, Words(ye, xe, ys, xs, bottom, right, top, left));
            return this;
        }

        public WmfBytes Arc(int left, int top, int right, int bottom, int xs, int ys, int xe, int ye) =>
            Sector(0x0817, left, top, right, bottom, xs, ys, xe, ye);

        public WmfBytes Pie(int left, int top, int right, int bottom, int xs, int ys, int xe, int ye) =>
            Sector(0x081A, left, top, right, bottom, xs, ys, xe, ye);

        public WmfBytes Chord(int left, int top, int right, int bottom, int xs, int ys, int xe, int ye) =>
            Sector(0x0830, left, top, right, bottom, xs, ys, xe, ye);

        public WmfBytes TextOut(int x, int y, string text)
        {
            Record(0x0521, Join(Words(text.Length), Padded(text), Words(y, x)));
            return this;
        }

        public WmfBytes SetTextColor(byte red, byte green, byte blue)
        {
            Record(0x0209, new[] { red, green, blue, (byte)0 });
            return this;
        }

        public WmfBytes SetTextAlign(int flags)
        {
            Record(0x012E, Words(flags, 0));
            return this;
        }

        /// <summary>Gets the file: an Aldus placeable header of the box given (1000 units to the inch), the metafile header, the records and the end record.</summary>
        public byte[] ToBytes(bool placeable = true, int right = 1000, int bottom = 1000)
        {
            Record(0x0000, Array.Empty<byte>());
            byte[] records = _records.ToArray();
            var file = new MemoryStream();
            var writer = new BinaryWriter(file);

            if (placeable)
            {
                var header = new MemoryStream();
                var head = new BinaryWriter(header);
                head.Write(0x9AC6CDD7u);
                head.Write((ushort)0);
                head.Write((short)0);
                head.Write((short)0);
                head.Write((short)right);
                head.Write((short)bottom);
                head.Write((ushort)1000);
                head.Write(0u);
                int check = 0;
                byte[] bytes = header.ToArray();

                for (int i = 0; i < 20; i += 2)
                {
                    check ^= BitConverter.ToUInt16(bytes, i);
                }

                writer.Write(bytes);
                writer.Write((ushort)check);
            }

            writer.Write((ushort)1);
            writer.Write((ushort)9);
            writer.Write((ushort)0x0300);
            writer.Write((uint)((18 + records.Length) / 2));
            writer.Write((ushort)8);
            writer.Write((uint)_largest);
            writer.Write((ushort)0);
            writer.Write(records);
            return file.ToArray();
        }

        /// <summary>Writes the file to a folder and gives its path.</summary>
        public string Save(string folder, string name, bool placeable = true, int right = 1000, int bottom = 1000)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllBytes(path, ToBytes(placeable, right, bottom));
            return path;
        }
    }
}
