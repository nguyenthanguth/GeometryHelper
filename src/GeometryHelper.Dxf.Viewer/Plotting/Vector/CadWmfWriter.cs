using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A writer of Windows Metafiles of its own, for WMFOUT: a placeable metafile (the Aldus header, then the records of
    /// [MS-WMF]) of a <see cref="CadVectorPicture"/>, in vectors. The picture is put into whole numbers, the longer side of its
    /// box 20 000 logical units long, y down, the placeable header giving 1 440 units to the inch (so the longer side is 13.9
    /// inches); a box a 5 000 by 2 000 drawing takes is a window of 20 000 by 8 000. A line is a Polyline with a pen of its
    /// colour whose width is its line weight (0.25 millimetres: 14 units); a dashed line has a pen of no width and the pen
    /// style dash or dot, as GDI draws no dash with a wide pen; a fill is a PolyPolygon with a solid brush of its colour and
    /// no pen, filled by the even-odd rule; a text is a TextOut of a font of its face, its height and angle, aligned to the
    /// left, the centre or the right of its point on the baseline. Left unwritten: a text's width factor and slant (the font
    /// is of its height and angle alone), and the hatch tints, images and viewport pictures the picture does not hold.
    /// </summary>
    internal static class CadWmfWriter
    {
        /// <summary>The length of the longer side of the picture, in logical units.</summary>
        internal const int Longer = 20000;

        /// <summary>The logical units to the inch the placeable header gives.</summary>
        internal const int UnitsPerInch = 1440;

        private const uint Placeable = 0x9AC6CDD7;

        /// <summary>Windows-1252, which a host with no code pages registered lacks: Latin-1 stands in for it there.</summary>
        private static readonly Encoding Ansi = AnsiEncoding();

        private static Encoding AnsiEncoding()
        {
            try
            {
                return Encoding.GetEncoding(1252);
            }
            catch (ArgumentException)
            {
                return Encoding.GetEncoding(28591);
            }
            catch (NotSupportedException)
            {
                return Encoding.GetEncoding(28591);
            }
        }

        /// <summary>Writes a picture as a placeable Windows Metafile.</summary>
        /// <param name="picture">The picture.</param>
        /// <returns>The bytes of the file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the picture is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the picture is empty.</exception>
        public static byte[] Write(CadVectorPicture picture)
        {
            if (picture == null)
            {
                throw new ArgumentNullException(nameof(picture));
            }

            if (picture.IsEmpty)
            {
                throw new ArgumentException("A metafile of nothing is not written.", nameof(picture));
            }

            DisplayBox box = picture.Box;
            double longer = Math.Max(box.Width, box.Height);
            double f = longer > 0.0 ? Longer / longer : 1.0;
            int width = (int)Math.Round(box.Width * f);
            int height = (int)Math.Round(box.Height * f);
            var records = new Records();
            records.SetMapMode(8);
            records.SetWindowOrg(0, 0);
            records.SetWindowExt(width, height);
            records.SetBkMode(1);
            records.SetPolyFillMode(1);
            records.SetTextAlign(24);

            foreach (CadVectorItem item in picture.Items)
            {
                switch (item)
                {
                    case CadVectorStroke stroke:
                        WriteStroke(records, stroke, box, f);
                        break;
                    case CadVectorFill fill:
                        WriteFill(records, fill, box, f);
                        break;
                    case CadVectorText text:
                        WriteText(records, text, box, f);
                        break;
                }
            }

            records.Release();
            records.End();
            return records.ToFile(width, height);
        }

        private static short Clamp(double value) =>
            (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(value)));

        private static short X(double x, DisplayBox box, double f) => Clamp((x - box.MinX) * f);

        private static short Y(double y, DisplayBox box, double f) => Clamp((box.MaxY - y) * f);

        private static void WriteStroke(Records records, CadVectorStroke stroke, DisplayBox box, double f)
        {
            int count = stroke.Xy.Length / 2;
            bool dashed = stroke.Dashes != null && stroke.Dashes.Length > 0;
            int style = 0;
            int width = (int)Math.Round(stroke.Weight * UnitsPerInch / 25.4);

            if (dashed)
            {
                bool dots = true;

                foreach (double on in OnLengths(stroke.Dashes))
                {
                    dots &= on <= 0.0;
                }

                style = dots ? 2 : 1;
                width = 0;
            }

            records.UsePen(style, width, stroke.Rgb);
            var points = new List<short>(2 * (count + 1));

            for (int i = 0; i < count; i++)
            {
                points.Add(X(stroke.Xy[2 * i], box, f));
                points.Add(Y(stroke.Xy[2 * i + 1], box, f));
            }

            if (stroke.Closed)
            {
                points.Add(points[0]);
                points.Add(points[1]);
            }

            records.Polyline(points);
        }

        /// <summary>The lengths of a dash pattern at its even places, which are the dashes that draw.</summary>
        private static IEnumerable<double> OnLengths(double[] dashes)
        {
            for (int i = 0; i < dashes.Length; i += 2)
            {
                yield return dashes[i];
            }
        }

        private static void WriteFill(Records records, CadVectorFill fill, DisplayBox box, double f)
        {
            records.UsePen(5, 0, 0);
            records.UseBrush(fill.Rgb);
            var rings = new List<List<short>>();

            foreach (double[] ring in fill.Rings)
            {
                if (ring.Length < 6)
                {
                    continue;
                }

                var points = new List<short>(ring.Length);

                for (int i = 0; i + 1 < ring.Length; i += 2)
                {
                    points.Add(X(ring[i], box, f));
                    points.Add(Y(ring[i + 1], box, f));
                }

                rings.Add(points);
            }

            if (rings.Count > 0)
            {
                records.PolyPolygon(rings);
            }
        }

        private static void WriteText(Records records, CadVectorText text, DisplayBox box, double f)
        {
            int height = Math.Max(1, (int)Math.Round(text.Em * f));
            records.UseFont(
                text.Face,
                height,
                (int)Math.Round(text.Rotation * 1800.0 / Math.PI),
                text.Bold,
                text.Italic
            );
            records.SetTextColor(text.Rgb);
            records.SetTextAlign(
                24
                    | (
                        text.Align == 1 ? 6
                        : text.Align == 2 ? 2
                        : 0
                    )
            );
            records.TextOut(text.Text, X(text.X, box, f), Y(text.Y, box, f));
        }

        /// <summary>The records of a metafile as they are written, with the pen, brush and font in use kept so that a change alone writes a record.</summary>
        private sealed class Records
        {
            private readonly MemoryStream _bytes = new MemoryStream();
            private readonly BinaryWriter _writer;
            private int _largest;
            private int _most;
            private int _alive;
            private int _words;
            private readonly bool[] _used = new bool[8];

            private string _pen;
            private int _penSlot = -1;
            private string _brush;
            private int _brushSlot = -1;
            private string _font;
            private int _fontSlot = -1;
            private int _color = -1;
            private int _align = -1;

            public Records()
            {
                _writer = new BinaryWriter(_bytes);
            }

            private void Record(ushort function, params byte[][] parts)
            {
                int length = 0;

                foreach (byte[] part in parts)
                {
                    length += part.Length;
                }

                int words = 3 + length / 2;
                _largest = Math.Max(_largest, words);
                _words += words;
                _writer.Write((uint)words);
                _writer.Write(function);

                foreach (byte[] part in parts)
                {
                    _writer.Write(part);
                }
            }

            private static byte[] Words(params int[] values)
            {
                var bytes = new byte[2 * values.Length];

                for (int i = 0; i < values.Length; i++)
                {
                    bytes[2 * i] = (byte)(values[i] & 0xFF);
                    bytes[2 * i + 1] = (byte)((values[i] >> 8) & 0xFF);
                }

                return bytes;
            }

            private static byte[] Color(int rgb) =>
                new[] { (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), (byte)0 };

            public void SetMapMode(int mode) => Record(0x0103, Words(mode));

            public void SetWindowOrg(int x, int y) => Record(0x020B, Words(y, x));

            public void SetWindowExt(int x, int y) => Record(0x020C, Words(y, x));

            public void SetBkMode(int mode) => Record(0x0102, Words(mode));

            public void SetPolyFillMode(int mode) => Record(0x0106, Words(mode));

            public void SetTextAlign(int mode)
            {
                if (_align != mode)
                {
                    _align = mode;
                    Record(0x012E, Words(mode, 0));
                }
            }

            public void SetTextColor(int rgb)
            {
                if (_color != rgb)
                {
                    _color = rgb;
                    Record(0x0209, Color(rgb));
                }
            }

            /// <summary>Creates an object in the lowest free slot of the object table and gives its slot.</summary>
            private int Create(ushort function, params byte[][] parts)
            {
                int slot = 0;

                while (_used[slot])
                {
                    slot++;
                }

                _used[slot] = true;
                _alive++;
                _most = Math.Max(_most, _alive);
                Record(function, parts);
                return slot;
            }

            private void Select(int slot) => Record(0x012D, Words(slot));

            private void Delete(ref int slot)
            {
                if (slot >= 0)
                {
                    Record(0x01F0, Words(slot));
                    _used[slot] = false;
                    _alive--;
                    slot = -1;
                }
            }

            /// <summary>Selects the pen of a style (0 solid, 1 dash, 2 dot, 5 none), a width in logical units and a colour, creating it where it is not the one in use.</summary>
            public void UsePen(int style, int width, int rgb)
            {
                string key = style + "/" + width + "/" + rgb;

                if (_pen == key)
                {
                    return;
                }

                Delete(ref _penSlot);
                _pen = key;
                _penSlot = Create(0x02FA, Words(style, width, 0), Color(rgb));
                Select(_penSlot);
            }

            /// <summary>Selects a solid brush of a colour, creating it where it is not the one in use.</summary>
            public void UseBrush(int rgb)
            {
                string key = rgb.ToString("X6");

                if (_brush == key)
                {
                    return;
                }

                Delete(ref _brushSlot);
                _brush = key;
                _brushSlot = Create(0x02FC, Words(0), Color(rgb), Words(0));
                Select(_brushSlot);
            }

            /// <summary>Selects a font of a face, a height in logical units (an em, as a negative lfHeight asks), an angle in tenths of a degree, bold and italic.</summary>
            public void UseFont(string face, int height, int tenths, bool bold, bool italic)
            {
                string key = face + "/" + height + "/" + tenths + "/" + bold + "/" + italic;

                if (_font == key)
                {
                    return;
                }

                Delete(ref _fontSlot);
                _font = key;
                var name = new byte[32];
                byte[] letters = Ansi.GetBytes(face ?? "Arial");
                Array.Copy(letters, name, Math.Min(letters.Length, 31));
                byte[] head = Words(-height, 0, tenths, tenths, bold ? 700 : 400);
                byte[] flags = { (byte)(italic ? 1 : 0), 0, 0, 0, 0, 0, 0, 0x22 };
                _fontSlot = Create(0x02FB, head, flags, name);
                Select(_fontSlot);
            }

            /// <summary>A Polyline of points as X and Y in turn; a record holds 65 535 points at most, so a longer line is several, each starting where the one before ended.</summary>
            public void Polyline(List<short> points)
            {
                const int Most = 30000;

                for (int from = 0; from + 3 < points.Count; from += 2 * (Most - 1))
                {
                    int count = Math.Min(Most, (points.Count - from) / 2);
                    Record(0x0325, Words(count), Shorts(points.GetRange(from, 2 * count)));
                }
            }

            public void PolyPolygon(List<List<short>> rings)
            {
                var counts = new int[rings.Count];
                var all = new List<short>();

                for (int i = 0; i < rings.Count; i++)
                {
                    counts[i] = rings[i].Count / 2;
                    all.AddRange(rings[i]);
                }

                var head = new int[1 + counts.Length];
                head[0] = counts.Length;
                Array.Copy(counts, 0, head, 1, counts.Length);
                Record(0x0538, Words(head), Shorts(all));
            }

            public void TextOut(string text, short x, short y)
            {
                byte[] letters = Ansi.GetBytes(text);
                var padded = new byte[(letters.Length + 1) / 2 * 2];
                Array.Copy(letters, padded, letters.Length);
                Record(0x0521, Words(letters.Length), padded, Words(y, x));
            }

            private static byte[] Shorts(List<short> values)
            {
                var ints = new int[values.Count];

                for (int i = 0; i < ints.Length; i++)
                {
                    ints[i] = values[i];
                }

                return Words(ints);
            }

            /// <summary>Deletes the objects still in the table, as a program that ran to its end does.</summary>
            public void Release()
            {
                Delete(ref _penSlot);
                Delete(ref _brushSlot);
                Delete(ref _fontSlot);
            }

            public void End() => Record(0x0000);

            /// <summary>The placeable header, the header of the metafile, then the records.</summary>
            public byte[] ToFile(int width, int height)
            {
                byte[] body = _bytes.ToArray();
                const int headerWords = 9;
                int totalWords = headerWords + body.Length / 2;
                var file = new MemoryStream();
                var writer = new BinaryWriter(file);

                // The placeable header: its key, no handle, the box, the units to the inch, a reserved word, and the checksum, the
                // exclusive or of the ten words before it.
                var head = new ushort[]
                {
                    (ushort)(Placeable & 0xFFFF),
                    (ushort)(Placeable >> 16),
                    0,
                    0,
                    0,
                    (ushort)width,
                    (ushort)height,
                    UnitsPerInch,
                    0,
                    0,
                };
                ushort check = 0;

                for (int i = 0; i < 10; i++)
                {
                    check ^= head[i];
                }

                foreach (ushort word in head)
                {
                    writer.Write(word);
                }

                writer.Write(check);
                writer.Write((ushort)1);
                writer.Write((ushort)headerWords);
                writer.Write((ushort)0x0300);
                writer.Write((uint)totalWords);
                writer.Write((ushort)_most);
                writer.Write((uint)_largest);
                writer.Write((ushort)0);
                writer.Write(body);
                return file.ToArray();
            }
        }
    }
}
