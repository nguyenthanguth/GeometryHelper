using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>A table made in code, and the anonymous block it is drawn with drawn anew from its cells.</summary>
    public sealed partial class DxfTable
    {
        /// <summary>
        /// Initializes a table in no file, as AutoCAD's <c>new Table()</c> given its <c>Position</c> and <c>SetSize</c>: rows of
        /// one height and columns of one width, every cell an empty text cell that takes its look from the table style, to be
        /// added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>. Added, it names the drawing's current
        /// table style (<see cref="DxfDatabase.Tablestyle"/>, where the drawing has it), its first row is merged across the table
        /// as AutoCAD's title is, and it is drawn in a new anonymous block (<c>*T1</c> and on) by
        /// <see cref="RecomputeTableBlock"/>: <c>new DxfTable(new GeoPoint3(0, 0, 0), 6, 5, 9, 63.5)</c> is a table 317.5 wide
        /// and 54 high, its top left at the origin, its title, header and four rows of data in five columns.
        /// </summary>
        /// <param name="position">The top left corner, in world coordinates.</param>
        /// <param name="numRows">The number of rows, one or more.</param>
        /// <param name="numColumns">The number of columns, one or more.</param>
        /// <param name="rowHeight">The height of each row, a finite number above nought.</param>
        /// <param name="columnWidth">The width of each column, a finite number above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the rows or the columns are fewer than one, or a size is not a finite number above nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the point is not finite.</exception>
        public DxfTable(GeoPoint3 position, int numRows, int numColumns, double rowHeight, double columnWidth)
            : base("ACAD_TABLE", MadeGroups(position, numRows, numColumns, rowHeight, columnWidth))
        {
        }

        /// <summary>
        /// Draws the table anew in the anonymous block it is drawn with, as AutoCAD's <c>RecomputeTableBlock</c>: the block's
        /// entities erased and, from its cells, the fill of each filled cell (a SOLID), the lines of its grid (a LINE for each
        /// run of a line of one look along a row or column of cells, none inside a block of merged cells), the text of each cell
        /// (an MTEXT at the cell's attachment point, as wide as the cell less its margins) and the block of each block cell (an
        /// INSERT), all on layer 0. Each look is the cell's own, else the table's own of its kind of row, else its table
        /// style's (<see cref="DxfTableStyle"/>), else AutoCAD's Standard's. A table that names no block of the drawing, or one
        /// another entity names too, is given a block of its own. The table of rows 10, 8 and 8 high and columns 40 and 30 wide,
        /// its title merged across, is drawn with 6 lines and 4 texts.
        /// </summary>
        /// <remarks>
        /// Rows keep their heights: a text taller than its row is not made room for, as AutoCAD would make its row taller. A
        /// table of 100 rows of 20 columns is drawn in some tens of milliseconds.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the table is in no drawing, or is erased.</exception>
        public void RecomputeTableBlock()
        {
            CheckEditable();

            if (!IsResident || Item.File.Database == null)
            {
                throw new InvalidOperationException("The table is in no drawing to draw it in: add it to one first.");
            }

            DxfDatabase database = Item.File.Database;
            DxfBlockTableRecord picture = BlockTableRecord;

            if (picture == null || !PictureAlone(picture))
            {
                picture = new DxfBlockTableRecord { Name = "*T" };
                database.BlockTable.Add(picture);
                DxfDimGroups g = Own();
                g.Set(2, picture.Name, g.Base, Order);
                g.Set(343, picture.Handle, g.Sub, Order);
                g.Write(Order);
            }

            foreach (DxfEntity old in picture.ToList())
            {
                old.Erase();
            }

            foreach (DxfEntity part in Picture())
            {
                picture.AppendEntity(part);
            }
        }

        /// <summary>
        /// Gives a table made in code what its drawing holds for it before it is written there: its block (<c>*T</c>), named in its
        /// 2 and 343, and the drawing's current table style in its 342; its cells written in the form of R2000 in a file before
        /// R2007; the classes ACAD_TABLE listed. A copy keeps what it names.
        /// </summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (!Item.File.IsMadeInCode)
            {
                return;
            }

            DxfFile file = owner.File;
            DxfDatabase database = file?.Database;

            if (database == null || !file.HasMarkers)
            {
                throw new ArgumentException("A DXF " + (file?.Version ?? "R12") + " has no tables, which came with R13 (AC1012) and later.", "entity");
            }

            var picture = new DxfBlockTableRecord { Name = "*T" };
            database.BlockTable.Add(picture);
            DxfDimGroups g = Own();
            g.Set(2, picture.Name, g.Base, Order);
            DxfDBDictionary styles = database.TableStyleDictionary;
            string current = database.Tablestyle;

            if (styles != null && styles.Contains(current) && styles[current] != null)
            {
                g.Set(342, styles[current].Handle, g.Sub, Order);
            }

            g.Set(343, picture.Handle, g.Sub, Order);

            if (!file.Supports(1021))
            {
                // Before R2007 a cell holds its text alone, in 1.
                var layout = new Layout(g);

                for (int k = layout.CellStart.Count - 1; k >= 0; k--)
                {
                    int to = k + 1 < layout.CellStart.Count ? layout.CellStart[k + 1] : layout.CellsEnd;
                    g.List.RemoveRange(layout.CellStart[k], to - layout.CellStart[k]);
                    g.List.InsertRange(layout.CellStart[k], NewCell(false));
                }
            }

            g.Write(Order);
            file.AddClass("ACAD_TABLE", "AcDbTable", 1025, entity: true);
            file.CountClass("ACAD_TABLE");
        }

        /// <summary>A table made in code, now in its drawing: its first row merged across as AutoCAD's title, and drawn in its block.</summary>
        internal override void Appended(bool madeInCode)
        {
            if (!madeInCode)
            {
                return;
            }

            if (NumColumns > 1 && !IsTitleSuppressed)
            {
                MergeCells(new DxfCellRange(0, 0, 0, NumColumns - 1));
            }

            RecomputeTableBlock();
        }

        /// <summary>
        /// Whether this table is the one entity of the drawing that names its block, as <see cref="NamesAlone"/> tells it, the
        /// entities of the block itself passed over, which name no block of a table.
        /// </summary>
        private bool PictureAlone(DxfBlockTableRecord picture)
        {
            int count = 0;

            foreach (DxfBlockTableRecord owner in Item.File.Database.BlockTable)
            {
                if (ReferenceEquals(owner, picture))
                {
                    continue;
                }

                foreach (DxfEntity entity in owner)
                {
                    string named = entity is DxfTable table ? table.PictureName : entity is DxfBlockReference insert ? insert.Name : entity is DxfDimension dimension ? dimension.PictureName : null;

                    if (named != null && string.Equals(named, picture.Name, StringComparison.OrdinalIgnoreCase) && ++count > 1)
                    {
                        return false;
                    }
                }
            }

            return count == 1;
        }

        /// <summary>The groups of a table made in code after its AcDbEntity: from R2007 on in form, as AutoCAD writes a new one.</summary>
        private static IEnumerable<DxfTypedValue> MadeGroups(GeoPoint3 position, int numRows, int numColumns, double rowHeight, double columnWidth)
        {
            Guard.Argument(Guard.IsFinite(position.X) && Guard.IsFinite(position.Y) && Guard.IsFinite(position.Z), nameof(position), "A table stands at a finite point.");

            if (numRows < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(numRows), numRows, "A table has one row or more.");
            }

            if (numColumns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(numColumns), numColumns, "A table has one column or more.");
            }

            Guard.Positive(rowHeight, nameof(rowHeight), "A row's height has to be a finite number above nought.");
            Guard.Positive(columnWidth, nameof(columnWidth), "A column's width has to be a finite number above nought.");
            string Text(double value) => Numbers.Exact(value + 0.0);
            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDbBlockReference"),
                new DxfTypedValue(2, string.Empty),
                new DxfTypedValue(10, Text(position.X)),
                new DxfTypedValue(20, Text(position.Y)),
                new DxfTypedValue(30, Text(position.Z)),
                new DxfTypedValue(100, "AcDbTable"),
                new DxfTypedValue(280, "0"),
                new DxfTypedValue(11, "1.0"),
                new DxfTypedValue(21, "0.0"),
                new DxfTypedValue(31, "0.0"),
                new DxfTypedValue(90, "22"),
                new DxfTypedValue(91, numRows.ToString(CultureInfo.InvariantCulture)),
                new DxfTypedValue(92, numColumns.ToString(CultureInfo.InvariantCulture)),
                new DxfTypedValue(93, "0"),
                new DxfTypedValue(94, "0"),
                new DxfTypedValue(95, "0"),
                new DxfTypedValue(96, "0"),
            };

            for (int row = 0; row < numRows; row++)
            {
                groups.Add(new DxfTypedValue(141, AsReal(rowHeight)));
            }

            for (int column = 0; column < numColumns; column++)
            {
                groups.Add(new DxfTypedValue(142, AsReal(columnWidth)));
            }

            List<KeyValuePair<int, string>> cell = NewCell(true);

            for (int k = 0; k < numRows * numColumns; k++)
            {
                groups.AddRange(cell.Select(g => new DxfTypedValue(g.Key, g.Value)));
            }

            return groups;
        }

        /// <summary>The entities the table is drawn with, in no file, in the system of the block placed at the table's point: fills, then lines, then texts and blocks.</summary>
        private List<DxfEntity> Picture()
        {
            Layout layout = Read();
            DxfDatabase database = Item.File.Database;
            int rows = layout.Rows;
            int columns = layout.Columns;
            Frame(layout, out GeoVector3 normal, out GeoPoint3 origin, out GeoVector3 along, out GeoVector3 down);
            GeoPoint3 seenOrigin = WorldToOcs(normal, origin);
            GeoPoint3 tip = WorldToOcs(normal, origin.Add(along));
            double turn = Math.Atan2(tip.Y - seenOrigin.Y, tip.X - seenOrigin.X);

            // A point of the table, so far along its rows and so far down them, in the block's system.
            GeoPoint3 At(double x, double y)
            {
                GeoPoint3 p = WorldToOcs(normal, origin.Add(along.Multiply(x)).Add(down.Multiply(y)));
                return new GeoPoint3(p.X - seenOrigin.X, p.Y - seenOrigin.Y, 0.0);
            }

            var xs = new double[columns + 1];
            var ys = new double[rows + 1];

            for (int c = 0; c < columns; c++)
            {
                xs[c + 1] = xs[c] + Real(layout.Groups, layout.ColumnAt[c]);
            }

            for (int r = 0; r < rows; r++)
            {
                ys[r + 1] = ys[r] + Real(layout.Groups, layout.RowAt[r]);
            }

            // The top left cell of the block of merged cells each cell is in, by row * columns + column.
            int count = Math.Min(rows * columns, layout.CellStart.Count);
            var owner = new int[rows * columns];
            int[] spans = layout.Spans();

            for (int k = 0; k < owner.Length; k++)
            {
                owner[k] = k;
            }

            for (int k = 0; k < count; k++)
            {
                int width = Math.Max(1, spans[2 * k]);
                int height = Math.Max(1, spans[2 * k + 1]);

                if ((width > 1 || height > 1) && owner[k] == k)
                {
                    int r0 = k / columns;
                    int c0 = k % columns;

                    for (int r = r0; r < Math.Min(rows, r0 + height); r++)
                    {
                        for (int c = c0; c < Math.Min(columns, c0 + width); c++)
                        {
                            owner[r * columns + c] = k;
                        }
                    }
                }
            }

            // The kind of each row as RowType tells it: the title first and the header next, where the table shows them.
            var kinds = new int[rows];
            bool titleOn = !IsTitleSuppressed;
            bool headerOn = !IsHeaderSuppressed;

            for (int r = 0; r < rows; r++)
            {
                int first = titleOn ? 1 : 0;
                kinds[r] = titleOn && r == 0 ? 0 : headerOn && r == first ? 1 : 2;
            }

            // The looks of each kind: the table's own, read once, else its style's.
            string[][] style = StyleLooks();
            List<KeyValuePair<int, string>>[] own = OwnLooks(layout);
            string[][][] lines = StyleLines();

            for (int kind = 0; kind < 3; kind++)
            {
                for (int slot = 0; slot < KindLookCodes.Length; slot++)
                {
                    int code = KindLookCodes[slot];
                    int at = own[kind].FindIndex(g => g.Key == code);
                    style[kind][slot] = at >= 0 ? own[kind][at].Value : style[kind][slot];
                }

                for (int part = 0; part < 3; part++)
                {
                    for (int line = 0; line < 6; line++)
                    {
                        lines[kind][part][line] = TableLine(part, line, kind) ?? lines[kind][part][line];
                    }
                }
            }

            double hMargin = HorizontalCellMargin;
            double vMargin = VerticalCellMargin;
            bool up = FlowDirection == DxfFlowDirection.BottomToTop;
            var fills = new List<DxfEntity>();
            var texts = new List<DxfEntity>();

            for (int k = 0; k < count; k++)
            {
                if (owner[k] != k)
                {
                    continue;
                }

                int r0 = k / columns;
                int c0 = k % columns;
                int r1 = r0, c1 = c0;

                while (r1 + 1 < rows && owner[(r1 + 1) * columns + c0] == k)
                {
                    r1++;
                }

                while (c1 + 1 < columns && owner[r0 * columns + c1 + 1] == k)
                {
                    c1++;
                }

                CellView cell = layout.CellAt(r0, c0);
                int kind = kinds[r0];
                string Look(int code, int bit, int slot) => cell.Override(code, bit) ?? style[kind][slot];
                double left = xs[c0], right = xs[c1 + 1], top = ys[r0], bottom = ys[r1 + 1];

                if ((Look(283, 0x02, 3) ?? "1").Trim() == "0")
                {
                    var fill = new DxfSolid(At(left, top), At(right, top), At(left, bottom), At(right, bottom));
                    fill.Color = DxfColor.OfIndex(WholeOf(Look(63, 0x04, 2), 7));
                    fills.Add(fill);
                }

                // The content stands in from the cell's sides by the margins, as its alignment says, the top the cell's top as seen.
                int alignment = WholeOf(Look(170, 0x01, 0), kind == 2 ? 2 : 5);
                alignment = alignment >= 1 && alignment <= 9 ? alignment : 1;
                int across = (alignment - 1) % 3;
                int upDown = (alignment - 1) / 3;
                double x = across == 0 ? left + hMargin : across == 1 ? (left + right) / 2.0 : right - hMargin;
                double seenTop = up ? bottom : top;
                double sign = up ? -1.0 : 1.0;
                double y = upDown == 0 ? seenTop + sign * vMargin : upDown == 1 ? (top + bottom) / 2.0 : seenTop + sign * (bottom - top - vMargin);
                GeoPoint3 at = At(x, y);

                if (cell.Whole(171, 1) == 2)
                {
                    DxfBlockTableRecord block = GetBlockTableRecord(r0, c0);

                    if (block != null)
                    {
                        double scale = BlockScale(r0, c0);
                        var insert = new DxfBlockReference(at, block)
                        {
                            Rotation = turn + BlockRotation(r0, c0),
                            ScaleFactors = new GeoVector3(scale, scale, scale),
                        };
                        texts.Add(insert);
                    }

                    continue;
                }

                string words = DxfReader.Decode(cell.Text());

                if (words.Length == 0)
                {
                    continue;
                }

                double height = Numbers.TryRead(Look(140, 0x20, 5), out double h) && Guard.IsFinite(h) && h > 0.0 ? h : 4.5;
                string styleName = DxfReader.NameOf(Look(7, 0x10, 1) ?? "Standard").Trim();
                var groups = new List<DxfTypedValue>
                {
                    new DxfTypedValue(62, WholeOf(Look(64, 0x08, 4), 0).ToString(CultureInfo.InvariantCulture)),
                    new DxfTypedValue(100, "AcDbMText"),
                    new DxfTypedValue(10, Numbers.Exact(at.X + 0.0)),
                    new DxfTypedValue(20, Numbers.Exact(at.Y + 0.0)),
                    new DxfTypedValue(30, "0"),
                    new DxfTypedValue(40, Numbers.Exact(height)),
                    new DxfTypedValue(41, Numbers.Exact(Math.Max(0.0, right - left - 2.0 * hMargin) + 0.0)),
                    new DxfTypedValue(71, alignment.ToString(CultureInfo.InvariantCulture)),
                };

                // Words of one piece of ASCII are written with the rest; others by Contents, in pieces as it writes them.
                string written = DxfReader.InCaretForm(words);
                bool onePiece = written.Length <= 250 && written.All(ch => ch >= ' ' && ch < 127);
                groups.Add(new DxfTypedValue(1, onePiece ? written : string.Empty));

                if (styleName.Length > 0 && !string.Equals(styleName, "Standard", StringComparison.OrdinalIgnoreCase) && database.TextStyleTable.Has(styleName))
                {
                    groups.Add(new DxfTypedValue(7, styleName));
                }

                DxfMText mtext = DxfMText.OfGroups(groups);

                if (!onePiece)
                {
                    mtext.Contents = words;
                }

                if (turn != 0.0)
                {
                    mtext.Rotation = turn;
                }

                texts.Add(mtext);
            }

            var parts = new List<DxfEntity>(fills.Count + texts.Count + rows + columns + 2);
            parts.AddRange(fills);
            parts.AddRange(GridLines(layout, rows, columns, xs, ys, owner, kinds, lines, At));
            parts.AddRange(texts);
            return parts;
        }

        /// <summary>
        /// The lines of the grid: along each line across between rows (and down between columns), the run of cells' edges of one
        /// look drawn as one LINE, an edge inside a block of merged cells not drawn. The edge between two cells is the lower (or
        /// right) cell's top (or left) where that is drawn, else the other's bottom (or right); each the cell's own where it holds
        /// one, else the table's own of its row's kind, else its style's: a row's top line where it is the first of its kind, its
        /// bottom where the last, else its line inside; a column's left at the table's left, its right at its right.
        /// </summary>
        private IEnumerable<DxfEntity> GridLines(Layout layout, int rows, int columns, double[] xs, double[] ys, int[] owner, int[] kinds, string[][][] style, Func<double, double, GeoPoint3> at)
        {
            var made = new List<DxfEntity>();
            int count = Math.Min(rows * columns, layout.CellStart.Count);

            // Each cell's own edges, read once: whether it holds each of its four, top, right, bottom and left, and the colour,
            // lineweight and showing it gives each.
            var holds = new bool[count * 4];
            var texts = new string[count * 12];

            for (int k = 0; k < count; k++)
            {
                CellView cell = layout.CellAt(k / columns, k % columns);

                for (int edge = 0; edge < 4; edge++)
                {
                    holds[4 * k + edge] = cell.HoldsEdge(edge);

                    for (int part = 0; part < 3 && holds[4 * k + edge]; part++)
                    {
                        texts[12 * k + 3 * edge + part] = cell.EdgeText(edge, part);
                    }
                }
            }

            // The own text of a part of an edge of a cell, as EdgeOverride reads it: the line between two cells is the edge of
            // the one below or right of it where that holds it, else the other's.
            string Held(int row, int column, int edge, int part)
            {
                int r = row + (edge == 0 ? -1 : edge == 2 ? 1 : 0);
                int c = column + (edge == 3 ? -1 : edge == 1 ? 1 : 0);
                int own = row * columns + column;
                int beside = r >= 0 && r < rows && c >= 0 && c < columns ? r * columns + c : -1;
                bool ownFirst = edge == 0 || edge == 3;
                int first = ownFirst ? own : beside;
                int then = ownFirst ? beside : own;
                int firstEdge = ownFirst ? edge : (edge + 2) % 4;
                int thenEdge = ownFirst ? (edge + 2) % 4 : edge;

                if (first >= 0 && first < count && holds[4 * first + firstEdge])
                {
                    return texts[12 * first + 3 * firstEdge + part];
                }

                return then >= 0 && then < count && holds[4 * then + thenEdge] ? texts[12 * then + 3 * thenEdge + part] : null;
            }

            // An edge's look: drawn or not, its colour index and its lineweight.
            Edge Of(int row, int column, int edge)
            {
                int kind = kinds[row];
                int line = edge == 0 ? (row == 0 || kinds[row - 1] != kind ? 0 : 1)
                    : edge == 2 ? (row == rows - 1 || kinds[row + 1] != kind ? 2 : 1)
                    : edge == 3 ? (column == 0 ? 3 : 4)
                    : (column == columns - 1 ? 5 : 4);
                string Part(int part) => Held(row, column, edge, part) ?? style[kind][part][line];
                return new Edge(WholeOf(Part(2), 1) != 0, WholeOf(Part(0), 0), WholeOf(Part(1), (int)DxfLineWeight.ByBlock));
            }

            // Across: between row r - 1 and row r, column by column.
            for (int r = 0; r <= rows; r++)
            {
                Edge? run = null;
                int from = 0;

                for (int c = 0; c <= columns; c++)
                {
                    Edge? here = null;

                    if (c < columns && !(r > 0 && r < rows && owner[(r - 1) * columns + c] == owner[r * columns + c]))
                    {
                        Edge below = r < rows ? Of(r, c, 0) : default;
                        Edge above = r > 0 ? Of(r - 1, c, 2) : default;
                        here = r < rows && below.Shown ? below : r > 0 && above.Shown ? above : (Edge?)null;
                    }

                    if (run.HasValue && (!here.HasValue || !run.Value.Equals(here.Value)))
                    {
                        made.Add(LineOf(at(xs[from], ys[r]), at(xs[c], ys[r]), run.Value));
                        run = null;
                    }

                    if (here.HasValue && !run.HasValue)
                    {
                        run = here;
                        from = c;
                    }
                }
            }

            // Down: between column c - 1 and column c, row by row.
            for (int c = 0; c <= columns; c++)
            {
                Edge? run = null;
                int from = 0;

                for (int r = 0; r <= rows; r++)
                {
                    Edge? here = null;

                    if (r < rows && !(c > 0 && c < columns && owner[r * columns + c - 1] == owner[r * columns + c]))
                    {
                        Edge right = c < columns ? Of(r, c, 3) : default;
                        Edge left = c > 0 ? Of(r, c - 1, 1) : default;
                        here = c < columns && right.Shown ? right : c > 0 && left.Shown ? left : (Edge?)null;
                    }

                    if (run.HasValue && (!here.HasValue || !run.Value.Equals(here.Value)))
                    {
                        made.Add(LineOf(at(xs[c], ys[from]), at(xs[c], ys[r]), run.Value));
                        run = null;
                    }

                    if (here.HasValue && !run.HasValue)
                    {
                        run = here;
                        from = r;
                    }
                }
            }

            return made;
        }

        private static DxfLine LineOf(GeoPoint3 start, GeoPoint3 end, Edge look)
        {
            var line = new DxfLine(start, end) { Color = DxfColor.OfIndex(look.Colour) };

            if (look.Weight != (int)DxfLineWeight.ByLayer)
            {
                line.LineWeight = (DxfLineWeight)look.Weight;
            }

            return line;
        }

        /// <summary>
        /// The lines the table style gives each kind of row, the title's, the header's and the data's, each its colours (0),
        /// lineweights (1) and showing (2) of the six lines, as the table's own are read; AutoCAD's Standard's, every line drawn
        /// by block, where the drawing has no style.
        /// </summary>
        private string[][][] StyleLines()
        {
            var lines = new string[3][][];
            DxfItem item = IsResident ? StyleItem() : null;
            var style = item == null ? null : OfObject(item) as DxfTableStyle;
            DxfRowType[] types = { DxfRowType.TitleRow, DxfRowType.HeaderRow, DxfRowType.DataRow };

            for (int kind = 0; kind < 3; kind++)
            {
                lines[kind] = new[] { new string[6], new string[6], new string[6] };

                for (int line = 0; line < 6; line++)
                {
                    var type = (DxfGridLineType)(1 << line);
                    lines[kind][0][line] = style == null ? "0" : style.GridColor(type, types[kind]).ColorIndex.ToString(CultureInfo.InvariantCulture);
                    lines[kind][1][line] = style == null ? "-2" : ((int)style.GridLineWeight(type, types[kind])).ToString(CultureInfo.InvariantCulture);
                    lines[kind][2][line] = style == null ? "1" : style.GridVisibility(type, types[kind]) ? "1" : "0";
                }
            }

            return lines;
        }

        private static int WholeOf(string text, int otherwise)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : otherwise;

        /// <summary>The look of an edge of a cell: whether it is drawn, its colour index and its lineweight.</summary>
        private readonly struct Edge : IEquatable<Edge>
        {
            internal Edge(bool shown, int colour, int weight)
            {
                Shown = shown;
                Colour = colour;
                Weight = weight;
            }

            internal bool Shown { get; }

            internal int Colour { get; }

            internal int Weight { get; }

            public bool Equals(Edge other) => Shown == other.Shown && Colour == other.Colour && Weight == other.Weight;

            public override bool Equals(object obj) => obj is Edge other && Equals(other);

            public override int GetHashCode() => (Colour * 397) ^ Weight ^ (Shown ? 1 : 0);
        }
    }
}
