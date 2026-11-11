using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A TOLERANCE laid out as AutoCAD 2021 lays out its feature control frame, and the parts the canvas draws it with: the
    /// lines of the frame, the symbols of <see cref="CadGdtGlyphs"/> and the words between them as TEXTs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured in AutoCAD 2021's console (probes GHTOL1 and GHTOL3, frames of DIMTXT 0.18 and 2.5, DIMGAP 0, 0.09, 0.625 and
    /// 2): every row is twice the text height high (DIMTXT times DIMSCALE), whatever DIMGAP; the insertion point is the middle
    /// of the left side of the first row; each next row stands under the one before on the same left side, its own width;
    /// each cell is as wide as its ink and the text height, half of it either side; a cell of nothing takes no room, and a row
    /// of nothing keeps its height. <c>{\Fgdt;j}%%v0.1</c> at 0.18 is 0.92296 wide in Arial: the position 0.27 and 0.18, the
    /// value 0.29296 and 0.18.
    /// </para>
    /// <para>
    /// A cell's ink runs from the first ink of its first letter to the last of its last, the letters between taking their
    /// advances, as AutoCAD measures an MTEXT: the words in the dimension style's text style, each symbol of the gdt font the
    /// width AutoCAD gives it (<see cref="CadGdtGlyphs.InkWidth"/>) with 2/21 of the height either side. A symbol is drawn
    /// centred in its room, its box of 24 four thirds of the height; the words on a baseline half the height below the middle
    /// of their row. The lines are in DIMCLRD and the words and symbols in DIMCLRT, each the frame's own colour where it is
    /// BYBLOCK.
    /// </para>
    /// </remarks>
    internal sealed class CadToleranceFrame
    {
        private readonly List<DxfEntity> _parts = new List<DxfEntity>();
        private readonly List<double> _widths = new List<double>();

        private CadToleranceFrame() { }

        /// <summary>The parts the frame is drawn with, in the world, each of the frame's look and its colour as the class says.</summary>
        internal IReadOnlyList<DxfEntity> Parts => _parts;

        /// <summary>The width of each row, the first on top.</summary>
        internal IReadOnlyList<double> RowWidths => _widths;

        /// <summary>The height of a row: twice the text height.</summary>
        internal double RowHeight { get; private set; }

        /// <summary>The width of the first row, which a leader on its left sets the frame back by; nought for a frame of none.</summary>
        internal double FirstWidth => _widths.Count > 0 ? _widths[0] : 0.0;

        /// <summary>
        /// Lays out a TOLERANCE by its own words, place and dimension style. <paramref name="layoutOf"/> lays out the words of
        /// an MTEXT in the drawing's fonts, as the canvas draws them.
        /// </summary>
        internal static CadToleranceFrame Of(DxfTolerance tolerance, Func<DxfMText, CadMTextLayout> layoutOf) =>
            Of(
                tolerance.Text,
                tolerance.Location,
                tolerance.Direction,
                tolerance.Normal,
                tolerance.GetDimstyleData(),
                tolerance,
                layoutOf
            );

        /// <summary>
        /// Lays out the words of a frame at a point along a direction in the plane of a normal, drawn by the variables of a
        /// dimension style; the parts take the look of <paramref name="look"/>, none where it is null.
        /// </summary>
        internal static CadToleranceFrame Of(
            string text,
            GeoPoint3 at,
            GeoVector3 direction,
            GeoVector3 normal,
            DxfDimStyleTableRecord style,
            DxfEntity look,
            Func<DxfMText, CadMTextLayout> layoutOf
        )
        {
            var frame = new CadToleranceFrame();
            double scale = style.Dimscale > 0.0 ? style.Dimscale : 1.0;
            double h = style.Dimtxt * scale;
            frame.RowHeight = 2.0 * h;
            string font = style.Dimtxsty?.Name ?? "Standard";
            GeoVector3 x = DxfOcsTransform.Normalized(direction);
            GeoVector3 z = DxfOcsTransform.Normalized(normal);
            GeoVector3 y = DxfOcsTransform.Normalized(z.CrossProduct(x));
            GeoPoint3 World(double u, double v) => at.Add(x.Multiply(u)).Add(y.Multiply(v));
            var lines = new List<DxfEntity>();
            var marks = new List<DxfEntity>();
            string[] rows = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');

            if (text == null || text.Length == 0 || !(h > 0.0))
            {
                return frame;
            }

            for (int r = 0; r < rows.Length; r++)
            {
                double middle = -r * frame.RowHeight;
                double top = middle + h;
                double bottom = middle - h;
                double left = 0.0;
                var walls = new List<double>();

                foreach (string cell in Cells(rows[r]))
                {
                    double width = Cell(cell, left, middle, h, font, World, x, z, layoutOf, marks);

                    if (width > 0.0)
                    {
                        left += width;
                        walls.Add(left);
                    }
                }

                frame._widths.Add(left);

                // The top of the row, as far as the wider of it and the row above, and the walls of its cells.
                double above = r > 0 ? frame._widths[r - 1] : left;
                AddLine(lines, World(0.0, top), World(Math.Max(above, left), top));

                foreach (double wall in walls)
                {
                    AddLine(lines, World(wall, top), World(wall, bottom));
                }
            }

            // A frame of no ink is drawn by nothing.
            if (frame._widths.TrueForAll(w => !(w > 0.0)))
            {
                frame._widths.Clear();
                return frame;
            }

            // The bottom of the last row, its own width across, and the left side down every row.
            double end = -(rows.Length - 1) * frame.RowHeight - h;
            AddLine(lines, World(0.0, end), World(frame._widths[rows.Length - 1], end));
            AddLine(lines, World(0.0, h), World(0.0, end));
            Finish(frame, lines, style.Dimclrd, look);
            Finish(frame, marks, style.Dimclrt, look);
            return frame;
        }

        private static void AddLine(List<DxfEntity> lines, GeoPoint3 from, GeoPoint3 to)
        {
            if (from.DistanceTo(to) > Tolerance.Default.EqualPoint)
            {
                lines.Add(new DxfLine(from, to));
            }
        }

        /// <summary>Gives each part the frame's look and the colour of the style where it is not BYBLOCK.</summary>
        private static void Finish(CadToleranceFrame frame, List<DxfEntity> parts, DxfColor color, DxfEntity look)
        {
            foreach (DxfEntity part in parts)
            {
                if (look != null)
                {
                    part.SetPropertiesFrom(look);
                }

                if (color != null && !color.IsByBlock)
                {
                    part.Color = color;
                }

                frame._parts.Add(part);
            }
        }

        /// <summary>The cells of a row, <c>%%v</c> between them, letters of either case.</summary>
        internal static List<string> Cells(string row)
        {
            var cells = new List<string>();
            int from = 0;

            for (int i = 0; i + 2 < row.Length; i++)
            {
                if (row[i] == '%' && row[i + 1] == '%' && (row[i + 2] == 'v' || row[i + 2] == 'V'))
                {
                    cells.Add(row.Substring(from, i - from));
                    from = i + 3;
                    i += 2;
                }
            }

            cells.Add(row.Substring(from));
            return cells;
        }

        /// <summary>
        /// The pieces of a cell: runs of words, and the letters of the gdt font in <c>{\Fgdt;...}</c> each a symbol of their
        /// own; a letter <see cref="CadGdtGlyphs"/> does not draw stays in the words, its font switch with it.
        /// </summary>
        internal static List<KeyValuePair<char?, string>> Pieces(string cell)
        {
            var pieces = new List<KeyValuePair<char?, string>>();
            int i = 0;
            int from = 0;

            while (i < cell.Length)
            {
                int end = cell.IndexOf('}', i);

                if (
                    cell[i] == '{'
                    && end > i
                    && string.Compare(cell, i, "{\\Fgdt;", 0, 7, StringComparison.OrdinalIgnoreCase) == 0
                    && Drawn(cell, i + 7, end)
                )
                {
                    if (i > from)
                    {
                        pieces.Add(new KeyValuePair<char?, string>(null, cell.Substring(from, i - from)));
                    }

                    for (int k = i + 7; k < end; k++)
                    {
                        pieces.Add(new KeyValuePair<char?, string>(cell[k], null));
                    }

                    i = end + 1;
                    from = i;
                    continue;
                }

                i++;
            }

            if (from < cell.Length)
            {
                pieces.Add(new KeyValuePair<char?, string>(null, cell.Substring(from)));
            }

            return pieces;
        }

        private static bool Drawn(string cell, int from, int end)
        {
            for (int k = from; k < end; k++)
            {
                if (!CadGdtGlyphs.Has(cell[k]))
                {
                    return false;
                }
            }

            return end > from;
        }

        /// <summary>
        /// Lays out a cell from its left side, its marks added; its width, its ink and the text height, nought for a cell of
        /// no ink.
        /// </summary>
        private static double Cell(
            string cell,
            double left,
            double middle,
            double h,
            string font,
            Func<double, double, GeoPoint3> world,
            GeoVector3 x,
            GeoVector3 z,
            Func<DxfMText, CadMTextLayout> layoutOf,
            List<DxfEntity> marks
        )
        {
            double pen = 0.0;
            double first = double.NaN;
            double last = 0.0;
            var placed = new List<KeyValuePair<double, object>>();

            foreach (KeyValuePair<char?, string> piece in Pieces(cell))
            {
                if (piece.Key.HasValue)
                {
                    char code = piece.Key.Value;
                    double ink = CadGdtGlyphs.InkWidth(code) * h;
                    double start = pen + CadGdtGlyphs.SideBearing * h;
                    first = double.IsNaN(first) ? start : first;
                    last = start + ink;
                    placed.Add(new KeyValuePair<double, object>(start + ink / 2.0, code));
                    pen = last + CadGdtGlyphs.SideBearing * h;
                    continue;
                }

                var words = new DxfMText { Contents = piece.Value, TextHeight = h };
                words.TextStyleName = font;
                CadMTextLayout layout = layoutOf(words);

                if (layout == null || layout.Pieces.Count == 0)
                {
                    continue;
                }

                double start0 = layout.Pieces[0].X;
                double advance = start0;

                foreach (CadMTextLayout.Piece run in layout.Pieces)
                {
                    advance = Math.Max(advance, run.X + run.Advance);
                }

                double[] box = layout.Ink;

                if (box != null && box[2] > box[0])
                {
                    first = double.IsNaN(first) ? pen + box[0] - start0 : first;
                    last = pen + box[2] - start0;
                }

                placed.Add(new KeyValuePair<double, object>(pen, piece.Value));
                pen += advance - start0;
            }

            if (double.IsNaN(first))
            {
                return 0.0;
            }

            // The cell's ink starts half the text height in from its left side.
            double shift = left + h / 2.0 - first;

            foreach (KeyValuePair<double, object> mark in placed)
            {
                if (mark.Value is char code)
                {
                    Glyph(code, shift + mark.Key, middle, h, font, world, x, z, marks);
                }
                else
                {
                    marks.Add(Words((string)mark.Value, world(shift + mark.Key, middle - h / 2.0), h, font, x, z));
                }
            }

            return last - first + h;
        }

        /// <summary>A symbol centred on a point of the frame: its strokes as polylines, its filled parts as SOLIDs, its letter as a TEXT.</summary>
        private static void Glyph(
            char code,
            double centre,
            double middle,
            double h,
            string font,
            Func<double, double, GeoPoint3> world,
            GeoVector3 x,
            GeoVector3 z,
            List<DxfEntity> marks
        )
        {
            // The box of 24 is four thirds of the text height, centred on the point, its y down.
            double unit = h * 4.0 / 3.0 / 24.0;
            GeoPoint3 Box(double[] p) => world(centre + (p[0] - 12.0) * unit, middle - (p[1] - 12.0) * unit);

            foreach (List<double[]> figure in CadGdtGlyphs.Figures(CadGdtGlyphs.StrokeOf(code)))
            {
                List<GeoPoint3> points = figure.ConvertAll(Box);
                DxfPolyline polyline = DxfLeader.PlanePolyline(points, z, Tolerance.Default.EqualPoint);

                if (polyline != null)
                {
                    marks.Add(polyline);
                }
            }

            string fill = CadGdtGlyphs.FillOf(code);

            if (fill.Length > 0)
            {
                foreach (List<double[]> figure in CadGdtGlyphs.Figures(fill))
                {
                    if (figure.Count >= 3)
                    {
                        marks.Add(new DxfSolid(Box(figure[0]), Box(figure[1]), Box(figure[2])));
                    }
                }
            }

            string letter = CadGdtGlyphs.LetterOf(code);

            if (letter != null)
            {
                DxfDBText words = Words(letter, world(centre, middle), h * 8.0 / 18.0, font, x, z);
                words.Justify = DxfAttachmentPoint.MiddleCenter;
                words.AlignmentPoint = world(centre, middle);
                marks.Add(words);
            }
        }

        /// <summary>A TEXT of words on a baseline from a point along the frame's x, in its plane.</summary>
        private static DxfDBText Words(
            string words,
            GeoPoint3 at,
            double height,
            string font,
            GeoVector3 x,
            GeoVector3 z
        )
        {
            var text = new DxfDBText { Normal = z };
            text.TextString = words;
            text.Height = height;
            text.TextStyleName = font;
            text.Position = at;
            Axes(z, out GeoVector3 u, out GeoVector3 v);
            text.Rotation = Math.Atan2(x.DotProduct(v), x.DotProduct(u));
            return text;
        }

        /// <summary>The axes of the system of a normal by the arbitrary axis algorithm, as a TEXT's turn is read in.</summary>
        private static void Axes(GeoVector3 normal, out GeoVector3 u, out GeoVector3 v)
        {
            bool nearZ = Math.Abs(normal.X) < 1.0 / 64.0 && Math.Abs(normal.Y) < 1.0 / 64.0;
            GeoVector3 axis = nearZ ? new GeoVector3(0.0, 1.0, 0.0) : new GeoVector3(0.0, 0.0, 1.0);
            u = DxfOcsTransform.Normalized(axis.CrossProduct(normal));
            v = DxfOcsTransform.Normalized(normal.CrossProduct(u));
        }
    }
}
