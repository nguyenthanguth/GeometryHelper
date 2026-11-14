using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// One shape a Windows Metafile draws, in the logical units of the file with y down: a polyline of points, or a text.
    /// </summary>
    internal sealed class CadWmfShape
    {
        /// <summary>The points of a polyline: a line, a polygon, an ellipse or an arc played out in straight pieces; empty for a text.</summary>
        public List<double[]> Points { get; } = new List<double[]>();

        /// <summary>Whether a segment runs from the last point back to the first.</summary>
        public bool Closed { get; set; }

        /// <summary>The colour as 0xRRGGBB: the pen's for a line, an arc and an ellipse; the brush's for a filled shape, the pen's where the brush is hollow.</summary>
        public int Rgb { get; set; }

        /// <summary>The width of the pen in logical units; nought for a null pen and a cosmetic one.</summary>
        public double Width { get; set; }

        /// <summary>The words of a text; null for a polyline.</summary>
        public string Text { get; set; }

        /// <summary>The reference point of a text.</summary>
        public double X { get; set; }

        /// <summary>The reference point of a text.</summary>
        public double Y { get; set; }

        /// <summary>The face of a text's font: Arial.</summary>
        public string Face { get; set; }

        /// <summary>Whether the font is bold: a weight of 600 or more.</summary>
        public bool Bold { get; set; }

        /// <summary>Whether the font is italic.</summary>
        public bool Italic { get; set; }

        /// <summary>The height of the font's em in logical units, above nought.</summary>
        public double Em { get; set; }

        /// <summary>How far a text is turned, in radians counter-clockwise on the screen.</summary>
        public double Turn { get; set; }

        /// <summary>The text alignment flags (SetTextAlign): 0 left and top, 2 right, 6 centre, 8 bottom, 24 baseline.</summary>
        public int Align { get; set; }
    }

    /// <summary>What <see cref="CadWmf.Read"/> takes from a Windows Metafile: the extents of its window and its shapes.</summary>
    internal sealed class CadWmfPicture
    {
        /// <summary>The width of the window (SetWindowExt), as a number above nought; 1 where the file sets none.</summary>
        public double ExtentX { get; set; } = 1.0;

        /// <summary>The height of the window, as a number above nought; 1 where the file sets none.</summary>
        public double ExtentY { get; set; } = 1.0;

        /// <summary>The shapes in the order the file draws them.</summary>
        public List<CadWmfShape> Shapes { get; } = new List<CadWmfShape>();
    }

    /// <summary>
    /// A reader of Windows Metafiles of its own, for WMFIN: the placeable header and the records of [MS-WMF] that draw lines,
    /// polygons, ellipses, arcs and text with the pens, brushes and fonts selected, which is what the metafiles of a drawing
    /// program hold. Bitmaps, regions, clipping, hatch and pattern brushes, the mapping mode, the viewport and the device
    /// context's save and restore are read past and not played.
    /// </summary>
    internal static class CadWmf
    {
        private const uint Placeable = 0x9AC6CDD7;
        private const int Segments = 100;
        private const int RoundSegments = 8;

        private sealed class Pen
        {
            public int Style;
            public double Width;
            public int Rgb;
        }

        private sealed class Brush
        {
            public bool Hollow;
            public int Rgb;
        }

        private sealed class Font
        {
            public double Height;
            public int Escapement;
            public bool Bold;
            public bool Italic;
            public string Face;
        }

        private sealed class Reader
        {
            public readonly CadWmfPicture Picture = new CadWmfPicture();
            public readonly List<object> Objects = new List<object>();
            public Pen Pen = new Pen { Rgb = 0 };
            public Brush Brush = new Brush { Rgb = 0xFFFFFF };
            public Font Font;
            public int TextRgb;
            public int Align;
            public double X;
            public double Y;
            public bool Sized;
        }

        /// <summary>Reads the bytes of a metafile, placeable or not.</summary>
        /// <param name="bytes">The file.</param>
        /// <returns>The picture; a file cut short gives what was read before the cut.</returns>
        /// <exception cref="InvalidDataException">Thrown when the bytes hold no metafile header.</exception>
        internal static CadWmfPicture Read(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            int at = bytes.Length >= 22 && U32(bytes, 0) == Placeable ? 22 : 0;

            if (bytes.Length < at + 18)
            {
                throw new InvalidDataException("it is shorter than a metafile header.");
            }

            int type = U16(bytes, at);

            if ((type != 1 && type != 2) || U16(bytes, at + 2) != 9)
            {
                throw new InvalidDataException("its header is not a metafile's.");
            }

            at += 18;
            var reader = new Reader();

            while (at + 6 <= bytes.Length)
            {
                long words = U32(bytes, at);
                int function = U16(bytes, at + 4);

                if (words < 3 || function == 0)
                {
                    break;
                }

                long end = at + words * 2;

                if (end > bytes.Length)
                {
                    break;
                }

                Play(reader, function, bytes, at + 6, (int)end);
                at = (int)end;
            }

            return reader.Picture;
        }

        private static void Play(Reader r, int function, byte[] b, int start, int end)
        {
            int size = end - start;

            switch (function)
            {
                case 0x020B: // SetWindowOrg: read past, AutoCAD's reader does not take the origin.
                    break;
                case 0x020C: // SetWindowExt, y then x.
                    if (size >= 4 && !r.Sized)
                    {
                        double y = Math.Abs(S16(b, start));
                        double x = Math.Abs(S16(b, start + 2));
                        r.Picture.ExtentX = x > 0.0 ? x : 1.0;
                        r.Picture.ExtentY = y > 0.0 ? y : 1.0;
                        r.Sized = true;
                    }

                    break;
                case 0x00F7: // CreatePalette
                case 0x01F9: // CreatePatternBrush
                case 0x06FF: // CreateRegion
                case 0x0142: // DibCreatePatternBrush
                    Add(r, new object());
                    break;
                case 0x02FA when size >= 10: // CreatePenIndirect
                    Add(
                        r,
                        new Pen
                        {
                            Style = U16(b, start) & 0xFF,
                            Width = Math.Abs((int)S16(b, start + 2)),
                            Rgb = Rgb(b, start + 6),
                        }
                    );
                    break;
                case 0x02FC when size >= 8: // CreateBrushIndirect
                    Add(r, new Brush { Hollow = U16(b, start) == 1, Rgb = Rgb(b, start + 2) });
                    break;
                case 0x02FB when size >= 18: // CreateFontIndirect
                    Add(
                        r,
                        new Font
                        {
                            Height = Math.Abs((int)S16(b, start)),
                            Escapement = S16(b, start + 4),
                            Bold = S16(b, start + 8) >= 600,
                            Italic = b[start + 10] != 0,
                            Face = Face(b, start + 18, end),
                        }
                    );
                    break;
                case 0x012D when size >= 2: // SelectObject
                    Select(r, U16(b, start));
                    break;
                case 0x01F0 when size >= 2: // DeleteObject
                    int gone = U16(b, start);

                    if (gone < r.Objects.Count)
                    {
                        r.Objects[gone] = null;
                    }

                    break;
                case 0x0209 when size >= 4: // SetTextColor
                    r.TextRgb = Rgb(b, start);
                    break;
                case 0x012E when size >= 2: // SetTextAlign
                    r.Align = U16(b, start);
                    break;
                case 0x0214 when size >= 4: // MoveTo, y then x
                    r.Y = S16(b, start);
                    r.X = S16(b, start + 2);
                    break;
                case 0x0213 when size >= 4: // LineTo, y then x
                    LineTo(r, S16(b, start + 2), S16(b, start));
                    break;
                case 0x0325 when size >= 2: // Polyline
                    Poly(r, b, start, end, false);
                    break;
                case 0x0324 when size >= 2: // Polygon
                    Poly(r, b, start, end, true);
                    break;
                case 0x0538 when size >= 2: // PolyPolygon
                    PolyPolygon(r, b, start, end);
                    break;
                case 0x041B when size >= 8: // Rectangle: bottom, right, top, left
                    Rectangle(r, S16(b, start + 6), S16(b, start + 4), S16(b, start + 2), S16(b, start));
                    break;
                case 0x0418 when size >= 8: // Ellipse
                    Ellipse(r, S16(b, start + 6), S16(b, start + 4), S16(b, start + 2), S16(b, start));
                    break;
                case 0x061C when size >= 12: // RoundRect: height, width, then the box
                    RoundRect(
                        r,
                        S16(b, start + 10),
                        S16(b, start + 8),
                        S16(b, start + 6),
                        S16(b, start + 4),
                        S16(b, start + 2),
                        S16(b, start)
                    );
                    break;
                case 0x0817 when size >= 16: // Arc
                    Sector(r, b, start, 0);
                    break;
                case 0x081A when size >= 16: // Pie
                    Sector(r, b, start, 1);
                    break;
                case 0x0830 when size >= 16: // Chord
                    Sector(r, b, start, 2);
                    break;
                case 0x0521 when size >= 2: // TextOut
                    TextOut(r, b, start, end);
                    break;
                case 0x0A32 when size >= 8: // ExtTextOut
                    ExtTextOut(r, b, start, end);
                    break;
            }
        }

        private static void Add(Reader r, object made)
        {
            int free = r.Objects.IndexOf(null);

            if (free < 0)
            {
                r.Objects.Add(made);
            }
            else
            {
                r.Objects[free] = made;
            }
        }

        private static void Select(Reader r, int index)
        {
            object chosen = index < r.Objects.Count ? r.Objects[index] : null;

            if (chosen is Pen pen)
            {
                r.Pen = pen;
            }
            else if (chosen is Brush brush)
            {
                r.Brush = brush;
            }
            else if (chosen is Font font)
            {
                r.Font = font;
            }
        }

        private static bool Visible(Pen pen) => pen.Style != 5;

        private static CadWmfShape Stroked(Reader r, bool closed)
        {
            var shape = new CadWmfShape
            {
                Closed = closed,
                Rgb = r.Pen.Rgb,
                Width = Visible(r.Pen) ? r.Pen.Width : 0.0,
            };
            r.Picture.Shapes.Add(shape);
            return shape;
        }

        private static CadWmfShape Filled(Reader r)
        {
            CadWmfShape shape = Stroked(r, true);
            shape.Rgb = r.Brush.Hollow ? r.Pen.Rgb : r.Brush.Rgb;
            return shape;
        }

        private static void LineTo(Reader r, double x, double y)
        {
            if (Visible(r.Pen))
            {
                CadWmfShape shape = Stroked(r, false);
                shape.Points.Add(new[] { r.X, r.Y });
                shape.Points.Add(new[] { x, y });
            }

            r.X = x;
            r.Y = y;
        }

        private static void Poly(Reader r, byte[] b, int start, int end, bool closed)
        {
            int count = Math.Min(U16(b, start), (end - start - 2) / 4);

            if (count < 2 || (!closed && !Visible(r.Pen)))
            {
                return;
            }

            CadWmfShape shape = closed ? Filled(r) : Stroked(r, false);

            for (int i = 0; i < count; i++)
            {
                shape.Points.Add(new double[] { S16(b, start + 2 + i * 4), S16(b, start + 4 + i * 4) });
            }
        }

        private static void PolyPolygon(Reader r, byte[] b, int start, int end)
        {
            int polygons = U16(b, start);
            int at = start + 2 + polygons * 2;

            if (at > end)
            {
                return;
            }

            for (int p = 0; p < polygons; p++)
            {
                int count = U16(b, start + 2 + p * 2);

                if (at + count * 4 > end)
                {
                    return;
                }

                if (count >= 2)
                {
                    CadWmfShape shape = Filled(r);

                    for (int i = 0; i < count; i++)
                    {
                        shape.Points.Add(new double[] { S16(b, at + i * 4), S16(b, at + 2 + i * 4) });
                    }
                }

                at += count * 4;
            }
        }

        private static void Rectangle(Reader r, double left, double top, double right, double bottom)
        {
            CadWmfShape shape = Filled(r);
            shape.Points.Add(new[] { left, top });
            shape.Points.Add(new[] { right, top });
            shape.Points.Add(new[] { right, bottom });
            shape.Points.Add(new[] { left, bottom });
        }

        private static void Ellipse(Reader r, double left, double top, double right, double bottom)
        {
            CadWmfShape shape = Stroked(r, false);
            Arc(shape, left, top, right, bottom, 0.0, 2.0 * Math.PI);
            shape.Points[shape.Points.Count - 1] = (double[])shape.Points[0].Clone();
        }

        private static void RoundRect(
            Reader r,
            double left,
            double top,
            double right,
            double bottom,
            double width,
            double height
        )
        {
            double x0 = Math.Min(left, right);
            double x1 = Math.Max(left, right);
            double y0 = Math.Min(top, bottom);
            double y1 = Math.Max(top, bottom);
            double rx = Math.Min(Math.Abs(width) / 2.0, (x1 - x0) / 2.0);
            double ry = Math.Min(Math.Abs(height) / 2.0, (y1 - y0) / 2.0);
            CadWmfShape shape = Filled(r);

            // The corners counter-clockwise on the screen from the top of the top left one, each a quarter of an ellipse of
            // the rounding, given as its centre and the angle it starts at.
            double[][] corners =
            {
                new[] { x0 + rx, y0 + ry, Math.PI / 2.0 },
                new[] { x0 + rx, y1 - ry, Math.PI },
                new[] { x1 - rx, y1 - ry, 3.0 * Math.PI / 2.0 },
                new[] { x1 - rx, y0 + ry, 0.0 },
            };

            foreach (double[] corner in corners)
            {
                for (int i = 0; i <= RoundSegments; i++)
                {
                    double t = corner[2] + (Math.PI / 2.0) * i / RoundSegments;
                    shape.Points.Add(new[] { corner[0] + rx * Math.Cos(t), corner[1] - ry * Math.Sin(t) });
                }
            }
        }

        /// <summary>An arc (0), a pie (1) or a chord (2) of the ellipse in a box, from a start to an end point counter-clockwise on the screen.</summary>
        private static void Sector(Reader r, byte[] b, int start, int kind)
        {
            double ye = S16(b, start);
            double xe = S16(b, start + 2);
            double ys = S16(b, start + 4);
            double xs = S16(b, start + 6);
            double bottom = S16(b, start + 8);
            double right = S16(b, start + 10);
            double top = S16(b, start + 12);
            double left = S16(b, start + 14);
            double a = Math.Abs(right - left) / 2.0;
            double h = Math.Abs(bottom - top) / 2.0;
            double cx = (left + right) / 2.0;
            double cy = (top + bottom) / 2.0;

            if (a <= 0.0 || h <= 0.0 || (kind == 0 && !Visible(r.Pen)))
            {
                return;
            }

            double from = Angle(xs - cx, cy - ys, a, h);
            double sweep = Angle(xe - cx, cy - ye, a, h) - from;

            while (sweep <= 0.0)
            {
                sweep += 2.0 * Math.PI;
            }

            CadWmfShape shape = kind == 0 ? Stroked(r, false) : Filled(r);

            if (kind == 1)
            {
                shape.Points.Add(new[] { cx, cy });
            }

            Arc(shape, left, top, right, bottom, from, sweep);
        }

        /// <summary>The parameter angle of the point of an ellipse of semi-axes a and h that lies in the direction (dx, dy), y up.</summary>
        private static double Angle(double dx, double dy, double a, double h) =>
            dx == 0.0 && dy == 0.0 ? 0.0 : Math.Atan2(dy / h, dx / a);

        /// <summary>Adds the points of an ellipse from an angle through a sweep, counter-clockwise on the screen, 100 pieces to the turn.</summary>
        private static void Arc(
            CadWmfShape shape,
            double left,
            double top,
            double right,
            double bottom,
            double from,
            double sweep
        )
        {
            double cx = (left + right) / 2.0;
            double cy = (top + bottom) / 2.0;
            double a = Math.Abs(right - left) / 2.0;
            double h = Math.Abs(bottom - top) / 2.0;
            int pieces = Math.Max(2, (int)Math.Round(Segments * sweep / (2.0 * Math.PI)));

            for (int i = 0; i <= pieces; i++)
            {
                double t = from + sweep * i / pieces;
                shape.Points.Add(new[] { cx + a * Math.Cos(t), cy - h * Math.Sin(t) });
            }
        }

        private static void TextOut(Reader r, byte[] b, int start, int end)
        {
            int count = U16(b, start);
            int words = start + 2 + count + count % 2;

            if (words + 4 > end)
            {
                return;
            }

            Text(r, b, start + 2, count, S16(b, words + 2), S16(b, words));
        }

        private static void ExtTextOut(Reader r, byte[] b, int start, int end)
        {
            int y = S16(b, start);
            int x = S16(b, start + 2);
            int count = U16(b, start + 4);
            int options = U16(b, start + 6);
            int at = start + 8 + ((options & 6) != 0 ? 8 : 0);

            if (at + count > end)
            {
                return;
            }

            Text(r, b, at, count, x, y);
        }

        private static void Text(Reader r, byte[] b, int at, int count, double x, double y)
        {
            if (count == 0)
            {
                return;
            }

            Font font = r.Font;
            double em = font != null ? font.Height : 0.0;

            if (em <= 0.0)
            {
                // A font of no height is the device's default; of a drawing of no size of its own, a fiftieth of it.
                em = Math.Max(r.Picture.ExtentX, r.Picture.ExtentY) / 50.0;
            }

            r.Picture.Shapes.Add(
                new CadWmfShape
                {
                    Text = Encoding.GetEncoding(28591).GetString(b, at, count),
                    X = x,
                    Y = y,
                    Rgb = r.TextRgb,
                    Face = font != null && font.Face.Length > 0 ? font.Face : "Arial",
                    Bold = font != null && font.Bold,
                    Italic = font != null && font.Italic,
                    Em = em,
                    Turn = font == null ? 0.0 : font.Escapement / 10.0 * Math.PI / 180.0,
                    Align = r.Align,
                }
            );
        }

        private static string Face(byte[] b, int at, int end)
        {
            int length = 0;

            while (at + length < end && length < 32 && b[at + length] != 0)
            {
                length++;
            }

            return Encoding.GetEncoding(28591).GetString(b, at, length);
        }

        private static int Rgb(byte[] b, int at) => (b[at] << 16) | (b[at + 1] << 8) | b[at + 2];

        private static int U16(byte[] b, int at) => b[at] | (b[at + 1] << 8);

        private static short S16(byte[] b, int at) => unchecked((short)(b[at] | (b[at + 1] << 8)));

        private static uint U32(byte[] b, int at) =>
            (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16)) | ((uint)b[at + 3] << 24);
    }
}
