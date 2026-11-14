using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What rows and columns put in change beyond their own cells, as AutoCAD 2021's <c>Table</c> changes it: the title merged
    /// anew over columns put in at its edge, and the kinds of rows a row put in at the title or the header moves on.
    /// </summary>
    public sealed partial class DxfTable
    {
        // The codes of a cell's looks of a kind of row, in the order AutoCAD 2021 writes them in a cell: its alignment, text
        // style, background (63, 283), colour and text height.
        private static readonly int[] KindLookCodes = { 170, 7, 63, 283, 64, 140 };

        // The styles of the rows of the TABLECONTENT, read once for its record as it stands (ContentKindsOf).
        private ContentKinds _contentKinds;

        // The looks of its own a row put in keeps: none (_rowLooks). Never changed.
        private static readonly List<KeyValuePair<int, string>> NoLooks = new List<KeyValuePair<int, string>>();

        #region Lines of a cell

        /// <summary>A line a cell holds at an edge: its colour, lineweight and showing (null for one it holds not), and which of the three it flags.</summary>
        private sealed class CellLine
        {
            internal string[] Texts { get; } = new string[3];

            internal bool[] Flagged { get; } = new bool[3];
        }

        /// <summary>The line a cell holds at an edge, top (0), right, bottom or left (<see cref="CellView.HoldsEdge"/>); null where it holds none.</summary>
        private static CellLine LineOf(CellView cell, int edge)
        {
            if (!cell.HoldsEdge(edge))
            {
                return null;
            }

            var line = new CellLine();
            int flags = cell.Flags ?? cell.Held();

            for (int kind = 0; kind < 3; kind++)
            {
                line.Texts[kind] = cell.EdgeText(edge, kind);
                line.Flagged[kind] = (flags & Layout.CellOverrides[EdgeCodes[kind][edge]]) != 0;
            }

            return line;
        }

        /// <summary>
        /// A cell's groups with a line at an edge in the place of the one it held: its overrides of the edge, their flags and
        /// its edge flag (172) taken out, then the line's put in with their flags and the edge flag; the groups as they are
        /// where it held none and is given none.
        /// </summary>
        private static List<KeyValuePair<int, string>> WithLine(
            List<KeyValuePair<int, string>> groups,
            int edge,
            CellLine line,
            bool valued
        )
        {
            CellView cell = View(groups);
            int mask = CellView.EdgeFlags(edge);

            if (((((cell.Flags ?? 0) | cell.Held()) & mask) != 0) || (cell.Whole(172, 0) & (1 << edge)) != 0)
            {
                cell.RemoveOverrides(mask);
                cell = View(cell.Groups.List);
                int edges = cell.Whole(172, 0);

                if ((edges & (1 << edge)) != 0)
                {
                    cell.SetHead(172, (edges & ~(1 << edge)).ToString(CultureInfo.InvariantCulture));
                }
            }
            else if (line == null)
            {
                return groups;
            }

            if (line == null)
            {
                return cell.Groups.List;
            }

            int flagsCode = valued ? 91 : 177;
            int flagged = 0;

            for (int kind = 0; kind < 3; kind++)
            {
                int code = EdgeCodes[kind][edge];

                if (line.Texts[kind] != null)
                {
                    cell.SetOverride(code, line.Texts[kind], flagsCode);
                    cell = View(cell.Groups.List);
                }

                flagged |= line.Flagged[kind] ? Layout.CellOverrides[code] : 0;
            }

            int? flags = cell.Flags;

            if (flags.HasValue && (flags.Value | flagged) != flags.Value)
            {
                cell.SetHead(
                    cell.Head(91) != null ? 91 : 177,
                    (flags.Value | flagged).ToString(CultureInfo.InvariantCulture)
                );
                cell = View(cell.Groups.List);
            }

            cell.SetHead(172, (cell.Whole(172, 0) | (1 << edge)).ToString(CultureInfo.InvariantCulture));
            return cell.Groups.List;
        }

        #endregion

        #region The title merged anew

        /// <summary>
        /// What the title merged across the table leaves when columns go in at its edge, before its first or after its last, as
        /// AutoCAD 2021 merged it anew (D28d g1 to g5, b1, b3, b8, b9, D28c i5): each cell of it but its first an empty cell
        /// of no value (91 0, 93 7, 90 0) that keeps the lines of its other edges, under the title's top line; and each cell
        /// of the row under it under the title's bottom line, the line its first cell reads there (none where it reads none),
        /// so that a green line under Qty went where the line under Mark was yellow. Within the title a column grows it as it is.
        /// </summary>
        private sealed class TitleMerge
        {
            /// <summary>The cells of the table changed, by their index row by row; null for one left as it is.</summary>
            internal List<KeyValuePair<int, string>>[] Cells { get; set; }

            /// <summary>The title's top line; null for none.</summary>
            internal CellLine Top { get; set; }

            /// <summary>The line under the title; null for none.</summary>
            internal CellLine Under { get; set; }

            /// <summary>The line right of the title's first cell; null for none.</summary>
            internal CellLine RightOfCorner { get; set; }

            /// <summary>A new cell of a column put in, given the title's top line in the title's row and the line under it in the next.</summary>
            internal List<KeyValuePair<int, string>> NewCell(
                List<KeyValuePair<int, string>> cell,
                int row,
                bool valued
            ) =>
                row == 0 ? WithLine(cell, 0, Top, valued)
                : row == 1 ? WithLine(cell, 0, Under, valued)
                : cell;
        }

        /// <summary>The title merged across the table merged anew, as <see cref="TitleMerge"/> tells it.</summary>
        private static TitleMerge MergeTitleAgain(Layout layout, bool valued)
        {
            CellView corner = layout.CellAt(0, 0);
            var merge = new TitleMerge
            {
                Cells = new List<KeyValuePair<int, string>>[layout.Rows * layout.Columns],
                Top = LineOf(corner, 0),
                Under = layout.Rows > 1 ? LineOf(layout.CellAt(1, 0), 0) ?? LineOf(corner, 2) : null,
                RightOfCorner = layout.Columns > 1 ? LineOf(layout.CellAt(0, 1), 3) ?? LineOf(corner, 1) : null,
            };

            for (int c = 1; c < layout.Columns; c++)
            {
                CellView old = layout.CellAt(0, c);
                List<KeyValuePair<int, string>> cell = NewCell(valued);

                for (int edge = 1; edge < 4; edge++)
                {
                    cell = WithLine(cell, edge, LineOf(old, edge), valued);
                }

                merge.Cells[c] = WithLine(cell, 0, merge.Top, valued);
            }

            for (int c = 0; layout.Rows > 1 && c < layout.Columns; c++)
            {
                merge.Cells[layout.Columns + c] = WithLine(Slice(layout, layout.Columns + c), 0, merge.Under, valued);
            }

            return merge;
        }

        /// <summary>
        /// The blocks of merged cells wholly along the row (or column) a new one takes the look of, copied along each new one
        /// and added to the blocks given where they meet none of them, as AutoCAD 2021's <c>InsertRowsAndInherit</c> merged the
        /// new row across as its row 2 or its title was, and its <c>InsertColumnsAndInherit</c> the new column down as its (1, 1)
        /// was (D28d m1 to m3, i1, i3, i4); those copied, as they stand in the table after.
        /// </summary>
        private static List<DxfCellRange> MergesAlong(
            List<DxfCellRange> found,
            bool rowsOf,
            int inheritFrom,
            int at,
            int count,
            List<DxfCellRange> blocks
        )
        {
            var copied = new List<DxfCellRange>();

            foreach (DxfCellRange b in found)
            {
                bool along = rowsOf
                    ? b.TopRow == inheritFrom && b.BottomRow == inheritFrom
                    : b.LeftColumn == inheritFrom && b.RightColumn == inheritFrom;

                for (int line = at; along && line < at + count; line++)
                {
                    var block = rowsOf
                        ? new DxfCellRange(line, b.LeftColumn, line, b.RightColumn)
                        : new DxfCellRange(b.TopRow, line, b.BottomRow, line);

                    if (!blocks.Exists(other => Meets(other, block)))
                    {
                        blocks.Add(block);
                        copied.Add(block);
                    }
                }
            }

            return copied;
        }

        #endregion

        #region The kinds of rows

        /// <summary>
        /// What rows put in change of the kinds of the table's rows, which its record tells by place alone: the title first and
        /// the header next, each where the table does not leave it out (280, 281). AutoCAD 2021 keeps a kind with each row, a
        /// new row of data or of the kind of the row whose look it takes, and writes a title or a header no longer in its place
        /// as data, the table leaving it out (<c>93 3</c>, <c>280 1</c>, <c>281 1</c> for <c>InsertRows(0)</c>; <c>93 2</c>,
        /// <c>281 1</c> for <c>InsertRows(1)</c>), and its cells given as their own the looks of their kind the data's do not
        /// have (<see cref="KindLooks"/>; D28d r1 to r7, i1 to i9, d2, d5). A row put in by <c>InsertRows</c> at or before the
        /// first row of data took the table's own looks of the data away, the old rows of data given them as their own, as
        /// AutoCAD reads the table's looks of a kind from its first row of that kind (D28d d1 to d3, D28c o7); not by
        /// <c>SetSize</c> (<paramref name="mayDrop"/> false). The kinds of the rows are those AutoCAD keeps
        /// (<see cref="KeptKinds"/>), a new row's <see cref="NewKind"/>, so that a header put into a table of one row is shown
        /// again (D28e s4). The cells are changed in place; the head is written after the record
        /// (<see cref="RowKinds.WriteHead"/>). Null where nothing changes.
        /// </summary>
        private RowKinds Kinds(
            Layout layout,
            List<KeyValuePair<int, string>>[] cells,
            int columns,
            int at,
            int count,
            int inheritFrom,
            bool valued,
            bool mayDrop = true
        )
        {
            bool titleOn = !IsTitleSuppressed;
            bool headerOn = !IsHeaderSuppressed;
            int rows = layout.Rows + count;
            int firstData = (titleOn ? 1 : 0) + (headerOn ? 1 : 0);

            // The TABLECONTENT is read where the rows put in reach the first two rows of a table that leaves its title or header
            // out, or take the kind of a row after them: elsewhere the first two rows are of the kinds their places say, as the
            // TABLECONTENT must agree, no other row moves into or out of the title's or the header's place, and a table of
            // 4 000 rows is not read twice.
            bool read = inheritFrom >= 2 || ((at <= 1 || layout.Rows <= 2) && !(titleOn && headerOn));
            int[] kept = read ? KeptKinds(layout) : PlaceKinds(layout.Rows);
            var kinds = new int[rows];

            for (int r = 0; r < rows; r++)
            {
                kinds[r] =
                    r < at ? kept[r]
                    : r >= at + count ? kept[r - count]
                    : inheritFrom >= 0 ? kept[inheritFrom]
                    : NewKind(layout.Rows, r - at);
            }

            bool titleNow = kinds[0] == 0;
            int headerAt = titleNow ? 1 : 0;
            bool headerNow = headerAt < rows && kinds[headerAt] == 1;
            bool outOfPlace = false;

            // A row out of the place of its kind takes the looks of its kind where it goes out of it now, a new row or one that
            // was in its place; one out of it already has them, as AutoCAD gave them when it went out.
            Func<int, bool> movedOut = r =>
            {
                int place =
                    titleNow && r == 0 ? 0
                    : headerNow && r == headerAt ? 1
                    : 2;
                int old = r < at ? r : r - count;
                bool added = r >= at && r < at + count;
                return kinds[r] != 2
                    && kinds[r] != place
                    && (
                        added
                        || kinds[r]
                            == (
                                titleOn && old == 0 ? 0
                                : headerOn && old == (titleOn ? 1 : 0) ? 1
                                : 2
                            )
                    );
            };

            for (int r = 0; r < rows && !outOfPlace; r++)
            {
                outOfPlace = movedOut(r);
            }

            List<KeyValuePair<int, string>>[] own =
                (mayDrop && inheritFrom < 0 && at <= firstData) || outOfPlace ? OwnLooks(layout) : null;
            bool drop = own != null && mayDrop && inheritFrom < 0 && at <= firstData && own[2].Count > 0;

            if (!outOfPlace && !drop && titleNow == titleOn && headerNow == headerOn)
            {
                return null;
            }

            var looks = new List<KeyValuePair<int, string>>[3];
            bool[] dropped = drop ? new bool[rows] : null;

            // The rows of data the table's own looks of the data go to are those that keep the table's: a row that keeps looks of
            // its own, as a row put in after the data keeps none, keeps them (D33g k01, d02).
            for (int r = 0; r < rows; r++)
            {
                bool added = r >= at && r < at + count;
                bool keepsTables = !added && RowLooksOf(layout, r < at ? r : r - count) == null;
                List<KeyValuePair<int, string>> give =
                    movedOut(r) ? looks[kinds[r]] ?? (looks[kinds[r]] = KindLooks(kinds[r], own))
                    : drop && kinds[r] == 2 && keepsTables ? own[2]
                    : null;

                for (int c = 0; give != null && give.Count > 0 && c < columns; c++)
                {
                    cells[r * columns + c] = WithMissing(cells[r * columns + c], give, valued);
                }

                if (dropped != null)
                {
                    dropped[r] = give == own[2];
                }
            }

            return new RowKinds(titleOn, titleNow, headerOn, headerNow, drop ? 4 : 0, dropped, drop ? own[2] : null);
        }

        /// <summary>The looks a row keeps of its own (<see cref="_rowLooks"/>), by its place in the layout; null where it keeps the table's own of its kind.</summary>
        private List<KeyValuePair<int, string>> RowLooksOf(Layout layout, int row) =>
            _rowLooks != null && _rowLooks.Length == layout.Rows ? _rowLooks[row] : null;

        /// <summary>
        /// Moves the looks each row keeps of its own (<see cref="_rowLooks"/>) with the rows of a table of the rows given, as
        /// rows are taken out and put in at a place: those of the rows taken out go, and each row put in keeps those given, null
        /// for the table's own of its kind.
        /// </summary>
        private void MoveRowLooks(int rows, int at, int taken, int put, List<KeyValuePair<int, string>> looks)
        {
            List<KeyValuePair<int, string>>[] old = _rowLooks != null && _rowLooks.Length == rows ? _rowLooks : null;

            if (old == null && (looks == null || put == 0))
            {
                _rowLooks = null;
                return;
            }

            var moved = new List<KeyValuePair<int, string>>[rows - taken + put];

            for (int r = 0; r < moved.Length; r++)
            {
                moved[r] =
                    r < at ? old?[r]
                    : r < at + put ? looks
                    : old?[r - put + taken];
            }

            _rowLooks = moved;
        }

        /// <summary>
        /// The looks the cells of each row are given as their own, row by row, those of <paramref name="looks"/> (of the table's
        /// own of the row's kind, and of its kind for a row out of its place, as <see cref="ContentLooks"/> reads them), each
        /// row in its kind's place that keeps looks of its own (<see cref="_rowLooks"/>) given those instead, as AutoCAD 2021
        /// gave each cell the looks of its row (D33g); null where no row has looks.
        /// </summary>
        private List<KeyValuePair<int, string>>[] WithRowLooks(
            Layout layout,
            List<KeyValuePair<int, string>>[] looks,
            bool[] moved
        )
        {
            for (int r = 0; _rowLooks != null && _rowLooks.Length == layout.Rows && r < layout.Rows; r++)
            {
                if (_rowLooks[r] != null && (moved == null || !moved[r]))
                {
                    looks = looks ?? new List<KeyValuePair<int, string>>[layout.Rows];
                    looks[r] = _rowLooks[r];
                }
            }

            return looks;
        }

        /// <summary>
        /// The kind of a row put in by <c>InsertRows</c> or <c>SetSize</c>, 1 the header and 2 data, as AutoCAD 2021 gave it:
        /// the first of those put into a table of one row is of the header, wherever it goes and whatever the one row is, the
        /// rest of data (D28e s4 to s17, e01 to e22): a table of its title alone given two rows has a header and a row of data,
        /// the header shown again where the table had left it out.
        /// </summary>
        private static int NewKind(int rowsBefore, int newIndex) => rowsBefore == 1 && newIndex == 0 ? 1 : 2;

        /// <summary>
        /// The kind of each row as AutoCAD 2021 keeps it, 0 the title, 1 the header and 2 data: the style of its row in the
        /// TABLECONTENT (<c>TABLEROW_BEGIN</c>, <c>90 1</c>, <c>90 2</c>, else data), where the TABLECONTENT lists each of the
        /// table's rows and agrees with its head on whether the title and the header are shown, the title moved down by a row put
        /// in before it kept as the title (D28e x12 to x15); else by its place, as <see cref="RowType"/> reads it. The
        /// TABLECONTENT is read whole only where it holds more rows of the title's or the header's style than the table shows,
        /// and once for its record as it stands (<see cref="ContentKinds"/>). With <paramref name="shownOf"/>, whether the title
        /// and the header are shown is read of the layout, one of the record as it stands, not of the record again.
        /// </summary>
        private int[] KeptKinds(Layout layout, bool shownOf = false)
        {
            bool titleOn = shownOf ? !LeftOut(layout, 280) : !IsTitleSuppressed;
            bool headerOn = shownOf ? !LeftOut(layout, 281) : !IsHeaderSuppressed;
            int[] kinds = PlaceKinds(layout.Rows, titleOn, headerOn);
            DxfItem content = IsResident ? ContentItem() : null;
            ContentKinds read = content == null ? null : ContentKindsOf(content);

            // A row of the TABLECONTENT is of the style of its first TABLEROW_BEGIN, so that no row is of the title's or the
            // header's kind out of its place where no more of their styles are found than the table shows: the kinds are those
            // of the places, as a reading of every row would give them.
            if (read == null || (read.Titles <= (titleOn ? 1 : 0) && read.Headers <= (headerOn ? 1 : 0)))
            {
                return kinds;
            }

            string[] styles = read.Styles(Item.File, content);

            if (styles == null || styles.Length != layout.Rows)
            {
                return kinds;
            }

            var kept = new int[layout.Rows];

            for (int r = 0; r < kept.Length; r++)
            {
                string style = styles[r];
                kept[r] =
                    style == "1" ? 0
                    : style == "2" ? 1
                    : 2;
            }

            bool keptTitle = kept.Length > 0 && kept[0] == 0;
            int headerAt = keptTitle ? 1 : 0;
            bool keptHeader = headerAt < kept.Length && kept[headerAt] == 1;
            return keptTitle == titleOn && keptHeader == headerOn ? kept : kinds;
        }

        /// <summary>
        /// The styles of the rows of the table's TABLECONTENT, kept while its record's bytes are the same, as every writing gives
        /// a record new ones: the cells of a table of 400 rows cleared one by one read it once, where each read it whole, which
        /// made the overrides of 397 cells taken away one by one twice as slow (D33e, D33f).
        /// </summary>
        private ContentKinds ContentKindsOf(DxfItem content)
        {
            DxfFile file = Item.File;
            DxfFile.Rec record = file.RecordAt(content, 0);
            ContentKinds kept = _contentKinds;

            if (
                kept != null
                && ReferenceEquals(kept.File, file)
                && ReferenceEquals(kept.Bytes, record.Bytes)
                && kept.Start == record.Start
                && kept.End == record.End
            )
            {
                return kept;
            }

            int titles = 0;
            int headers = 0;

            foreach (string style in file.ValuesAfter(content, "TABLEROW_BEGIN", 'W', 90))
            {
                titles += style == "1" ? 1 : 0;
                headers += style == "2" ? 1 : 0;
            }

            kept = new ContentKinds(file, record, titles, headers);
            _contentKinds = kept;
            return kept;
        }

        /// <summary>
        /// The styles of the rows of a TABLECONTENT as its record stands: how many of its TABLEROW_BEGIN give the title's style
        /// (<c>90 1</c>) and the header's (<c>90 2</c>), wherever they stand, and the style of each row, read whole the first
        /// time it is asked for.
        /// </summary>
        private sealed class ContentKinds
        {
            private string[] _styles;
            private bool _read;

            internal ContentKinds(DxfFile file, DxfFile.Rec record, int titles, int headers)
            {
                File = file;
                Bytes = record.Bytes;
                Start = record.Start;
                End = record.End;
                Titles = titles;
                Headers = headers;
            }

            internal DxfFile File { get; }

            internal byte[] Bytes { get; }

            internal int Start { get; }

            internal int End { get; }

            internal int Titles { get; }

            internal int Headers { get; }

            /// <summary>The style of each row, as <see cref="StyleOf"/> reads it; null where the TABLECONTENT is not in the form AutoCAD writes.</summary>
            internal string[] Styles(DxfFile file, DxfItem content)
            {
                if (!_read)
                {
                    ContentGrid grid = ContentGrid.Read(file, content);
                    _styles = grid?.Rows.ConvertAll(StyleOf).ToArray();
                    _read = true;
                }

                return _styles;
            }
        }

        /// <summary>The kind of each row by its place, as <see cref="RowType"/> reads it: the title first and the header next, each where the table does not leave it out.</summary>
        private int[] PlaceKinds(int rows) => PlaceKinds(rows, !IsTitleSuppressed, !IsHeaderSuppressed);

        /// <summary>The kind of each row by its place, the title and the header shown as given.</summary>
        private static int[] PlaceKinds(int rows, bool titleOn, bool headerOn)
        {
            var kinds = new int[rows];

            for (int r = 0; r < rows; r++)
            {
                kinds[r] =
                    titleOn && r == 0 ? 0
                    : headerOn && r == (titleOn ? 1 : 0) ? 1
                    : 2;
            }

            return kinds;
        }

        /// <summary>
        /// What rows taken out change of the kinds of the rows left, as AutoCAD 2021 wrote it (D28e x1 to x17): the title shown
        /// where the first row left is of the title, the header where the row after it (or the first, the title left out) is
        /// of the header, each written in the head (280, 281 and their flags in 93) where it changes, a title or header moved up
        /// into its place by the rows taken shown again; and the table's own looks of a kind (<see cref="KindLookCodes"/>) of
        /// which no row is left taken out of its head with their flags, as AutoCAD took the header's out with the header (x8,
        /// x16) and the data's with the last row of data (x9). Where the first row of data goes and the next keeps looks of its
        /// own (<see cref="_rowLooks"/>), the table's own looks of the data are those, as AutoCAD 2021 reads them from its first
        /// row of data: the next row put in after the data took them away (93 2101760), the row at 0 put in and taken out again
        /// gave them back as they were (93 6886016), and the rows of data left that keep the table's are given them as their
        /// own in <paramref name="cells"/>, the cells of the table after, of the columns given (D33g k02, k05, k06). Null where
        /// nothing changes.
        /// </summary>
        private RowKinds DeletedKinds(
            Layout layout,
            int at,
            int count,
            List<KeyValuePair<int, string>>[] cells,
            int columns
        )
        {
            List<KeyValuePair<int, string>>[] own = OwnLooks(layout);
            bool keepsOwn =
                _rowLooks != null && _rowLooks.Length == layout.Rows && Array.Exists(_rowLooks, looks => looks != null);

            // Rows taken out after the first two leave the title and the header as they are: the TABLECONTENT is read only where
            // the table has looks of its own a kind could take with it, or a row keeps looks of its own.
            if (at >= 2 && own[0].Count == 0 && own[1].Count == 0 && own[2].Count == 0 && !keepsOwn)
            {
                return null;
            }

            int[] kept = KeptKinds(layout);
            var kinds = new List<int>();
            var before = new bool[3];
            var after = new bool[3];

            for (int r = 0; r < kept.Length; r++)
            {
                before[kept[r]] = true;

                if (r < at || r >= at + count)
                {
                    kinds.Add(kept[r]);
                    after[kept[r]] = true;
                }
            }

            bool titleOn = !IsTitleSuppressed;
            bool headerOn = !IsHeaderSuppressed;
            bool titleNow = kinds.Count > 0 && kinds[0] == 0;
            int headerAt = titleNow ? 1 : 0;
            bool headerNow = headerAt < kinds.Count && kinds[headerAt] == 1;
            int drop = 0;

            for (int kind = 0; kind < 3; kind++)
            {
                drop |= before[kind] && !after[kind] && own[kind].Count > 0 ? 1 << kind : 0;
            }

            // The first row of data taken out, the next row of data, where it keeps looks of its own other than the table's,
            // gives the table its looks of the data; the rows of data left that keep the table's keep those it had.
            int first = Array.IndexOf(kept, 2);
            int next = first >= at && first < at + count ? Array.IndexOf(kept, 2, at + count) : -1;
            List<KeyValuePair<int, string>> nextLooks = next >= 0 ? RowLooksOf(layout, next) : null;
            bool[] dropped = null;

            if (nextLooks != null && !SameLooks(nextLooks, own[2]))
            {
                drop |= own[2].Count > 0 ? 4 : 0;
                dropped = new bool[kinds.Count];

                for (int r = 0; own[2].Count > 0 && r < kinds.Count; r++)
                {
                    int old = r < at ? r : r + count;

                    if (kinds[r] != 2 || old == next || RowLooksOf(layout, old) != null)
                    {
                        continue;
                    }

                    dropped[r] = true;

                    for (int c = 0; c < columns; c++)
                    {
                        cells[r * columns + c] = WithMissing(cells[r * columns + c], own[2], Item.File.Supports(1021));
                    }
                }
            }

            return drop == 0 && dropped == null && titleNow == titleOn && headerNow == headerOn
                ? null
                : new RowKinds(
                    titleOn,
                    titleNow,
                    headerOn,
                    headerNow,
                    drop,
                    dropped,
                    own[2],
                    dropped == null ? null : nextLooks,
                    next - count
                );
        }

        /// <summary>Whether two lists of looks give the same, each code of the one value, whatever their order.</summary>
        private static bool SameLooks(List<KeyValuePair<int, string>> a, List<KeyValuePair<int, string>> b) =>
            a.Count == b.Count
            && a.TrueForAll(look => b.Exists(other => other.Key == look.Key && SameLook(other.Value, look.Value)));

        /// <summary>A cell's groups given as its own each look given that it has not of its own.</summary>
        private static List<KeyValuePair<int, string>> WithMissing(
            List<KeyValuePair<int, string>> groups,
            List<KeyValuePair<int, string>> looks,
            bool valued
        )
        {
            CellView cell = View(groups);

            foreach (KeyValuePair<int, string> look in looks)
            {
                if (cell.Override(look.Key, Layout.CellOverrides[look.Key]) == null)
                {
                    cell.SetOverride(look.Key, look.Value, valued ? 91 : 177);
                    cell = View(cell.Groups.List);
                }
            }

            return cell.Groups.List;
        }

        /// <summary>The table's own looks of each kind of row, the title's, the header's and the data's, as overrides of a cell in the order AutoCAD 2021 writes them (<see cref="KindLookCodes"/>).</summary>
        private static List<KeyValuePair<int, string>>[] OwnLooks(Layout layout)
        {
            var looks = new List<KeyValuePair<int, string>>[3];

            for (int rowKind = 0; rowKind < 3; rowKind++)
            {
                looks[rowKind] = new List<KeyValuePair<int, string>>();

                foreach (int code in KindLookCodes)
                {
                    string text = OwnText(layout, RowLooks[code] + rowKind);

                    if (text != null)
                    {
                        looks[rowKind].Add(new KeyValuePair<int, string>(code, text));
                    }
                }
            }

            return looks;
        }

        /// <summary>
        /// The text height and the text style the table keeps of its own for the cells of each kind of row, the title's, the
        /// header's and the data's in that order, as AutoCAD's <c>TextHeight(RowType)</c> and <c>TextStyle(RowType)</c> give
        /// them over the table style's; null for a kind the table keeps none for. A cell's own, <see cref="TextHeight(int, int)"/>
        /// and <see cref="TextStyle(int, int)"/>, comes before them.
        /// </summary>
        internal void OwnTextLooks(out double?[] heights, out string[] styles)
        {
            List<KeyValuePair<int, string>>[] own = OwnLooks(Read());
            heights = new double?[3];
            styles = new string[3];

            for (int kind = 0; kind < 3; kind++)
            {
                int at = own[kind].FindIndex(g => g.Key == 140);

                if (
                    at >= 0
                    && Numbers.TryRead(own[kind][at].Value, out double height)
                    && Guard.IsFinite(height)
                    && height > 0.0
                )
                {
                    heights[kind] = height;
                }

                at = own[kind].FindIndex(g => g.Key == 7);
                styles[kind] = at >= 0 ? DxfReader.NameOf(own[kind][at].Value).Trim() : null;
            }
        }

        /// <summary>
        /// The looks a title (0) or a header (1) written as data takes as its own, as AutoCAD 2021 gave them: the table's own of
        /// that kind, and of the rest those its table style gives that kind that are not the ones it gives the data, the title
        /// of AutoCAD's Standard <c>170 5</c> and <c>140 6.0</c>, the header <c>170 5</c> (D28d r1, i1, d2, d4); in the order
        /// AutoCAD writes them.
        /// </summary>
        private List<KeyValuePair<int, string>> KindLooks(int rowKind, List<KeyValuePair<int, string>>[] own)
        {
            string[][] style = StyleLooks();
            string[] kind = style[rowKind];
            string[] data = style[2];
            bool fillDiffers = kind[3] != data[3] || (kind[3] == "0" && !SameLook(kind[2], data[2]));
            var looks = new List<KeyValuePair<int, string>>();

            for (int slot = 0; slot < KindLookCodes.Length; slot++)
            {
                int code = KindLookCodes[slot];
                int at = own[rowKind].FindIndex(g => g.Key == code);

                if (at >= 0)
                {
                    looks.Add(own[rowKind][at]);
                }
                else if (code == 63 || code == 283 ? fillDiffers : !SameLook(kind[slot], data[slot]))
                {
                    looks.Add(new KeyValuePair<int, string>(code, kind[slot]));
                }
            }

            return looks;
        }

        /// <summary>Whether two looks of a style are the one: two numbers of the one value (a text height, an alignment, a colour), as AutoCAD compares a style's values, else two names alike.</summary>
        private static bool SameLook(string a, string b)
        {
            bool numbers = Numbers.TryRead(a, out double x) & Numbers.TryRead(b, out double y);
            return numbers ? x.Equals(y) : string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The looks the table style gives each kind of row, the title, the header and the data, in the order and the form of a
        /// cell's own (<see cref="KindLookCodes"/>): its alignment (170), text style (7), background (its 63, and a cell's 283 of
        /// 0 where the style's 283 turns it on, else 1), text colour (its 62, as a cell's 64) and text height (140, a real, as
        /// AutoCAD 2021 wrote the title's <c>140 6.0</c>, D33g); each kind of
        /// cell of a TABLESTYLE opens with its 7, the data's, the title's and the header's in that order. AutoCAD's own
        /// Standard's where the drawing has no style or the style gives none: top centre for the data and middle centre for the
        /// rest, Standard, no background, by block, and the heights <see cref="StyleTextHeights"/> reads.
        /// </summary>
        private string[][] StyleLooks()
        {
            double[] heights = StyleTextHeights();
            var looks = new string[3][];

            for (int rowKind = 0; rowKind < 3; rowKind++)
            {
                looks[rowKind] = new[]
                {
                    rowKind == 2 ? "2" : "5",
                    "Standard",
                    "7",
                    "1",
                    "0",
                    ContentNumber(Numbers.Exact(heights[rowKind] + 0.0)),
                };
            }

            DxfItem style = IsResident ? StyleItem() : null;

            if (style == null)
            {
                return looks;
            }

            bool inStyle = false;
            int block = -1;
            int[] kinds = { 2, 0, 1 };

            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(style))
            {
                inStyle = group.TypeCode == 100 ? group.Value.Trim() == "AcDbTableStyle" : inStyle;
                block += inStyle && group.TypeCode == 7 ? 1 : 0;

                if (!inStyle || block < 0 || block > 2)
                {
                    continue;
                }

                int slot =
                    group.TypeCode == 170 ? 0
                    : group.TypeCode == 7 ? 1
                    : group.TypeCode == 63 ? 2
                    : group.TypeCode == 283 ? 3
                    : group.TypeCode == 62 ? 4
                    : -1;

                if (slot >= 0)
                {
                    string text = group.Value.Trim();
                    looks[kinds[block]][slot] = slot == 3 ? (text == "1" ? "0" : "1") : text;
                }
            }

            return looks;
        }

        /// <summary>
        /// What <see cref="Kinds"/> and <see cref="DeletedKinds"/> found to write in the table's head: whether its title and its
        /// header are shown before and after, and the kinds of rows (bit 0 the title, 1 the header, 2 the data) whose own looks
        /// go.
        /// </summary>
        private sealed class RowKinds
        {
            private readonly bool _titleWas;
            private readonly bool _titleNow;
            private readonly bool _headerWas;
            private readonly bool _headerNow;
            private readonly int _drop;
            private readonly bool[] _dropped;
            private readonly List<KeyValuePair<int, string>> _dataLooks;
            private readonly List<KeyValuePair<int, string>> _restore;
            private readonly int _first;

            internal RowKinds(
                bool titleWas,
                bool titleNow,
                bool headerWas,
                bool headerNow,
                int drop,
                bool[] dropped = null,
                List<KeyValuePair<int, string>> dataLooks = null,
                List<KeyValuePair<int, string>> restore = null,
                int first = -1
            )
            {
                _titleWas = titleWas;
                _titleNow = titleNow;
                _headerWas = headerWas;
                _headerNow = headerNow;
                _drop = drop;
                _dropped = dropped;
                _dataLooks = dataLooks;
                _restore = restore;
                _first = first;
            }

            /// <summary>
            /// Gives the rows of data the table's own looks of the data were given to as the table lost them those looks as their
            /// own (<see cref="_rowLooks"/>), the rows by their places after the edit, as AutoCAD 2021 kept them with each such
            /// row, so that a clear of the cells' overrides gave them back (D33e n05, D33f a01 to a14); and the row that gave the
            /// table its looks of the data those of the table again (D33g k02).
            /// </summary>
            internal void MarkRows(DxfTable table, int rows)
            {
                if (_dropped == null)
                {
                    return;
                }

                if (table._rowLooks == null || table._rowLooks.Length != rows)
                {
                    table._rowLooks = new List<KeyValuePair<int, string>>[rows];
                }

                for (int r = 0; r < rows; r++)
                {
                    table._rowLooks[r] = _dropped[r] ? _dataLooks : table._rowLooks[r];
                }

                if (_restore != null)
                {
                    table._rowLooks[_first] = null;
                }
            }

            /// <summary>
            /// Writes them in the table's head as AutoCAD 2021 wrote them: the table's own looks of the kinds that go taken out
            /// wherever they stand, with their flags in 93; a title or header left out given 280 or 281 of 1 where the table
            /// holds it, else put in its head after its flags with theirs (93 bits 0 and 1), <c>93 3</c> ... <c>96 0</c>,
            /// <c>280 1</c>, <c>281 1</c>, <c>141 ...</c>; one shown again its 280 or 281 taken out with its flag, as AutoCAD
            /// wrote <c>93 0</c> and neither (D28e s4, x12); the record written once, nothing where it comes out the same.
            /// </summary>
            internal void WriteHead(DxfTable table)
            {
                if (
                    _titleWas == _titleNow
                    && _headerWas == _headerNow
                    && _drop == 0
                    && (_restore == null || _restore.Count == 0)
                )
                {
                    return;
                }

                Layout layout = table.ReadToWrite();
                DxfDimGroups g = layout.Groups;
                int headTo = layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd;
                int cellsEnd = layout.CellsEnd;

                if (_drop != 0)
                {
                    int[] tail = TailRanks(g, headTo, cellsEnd, out int to);
                    int[] head = HeadRanks(g, headTo, out int ownFrom, out int ownTo);
                    var marked = new bool[g.List.Count];
                    int bits = 0;

                    for (int kind = 0; kind < 3; kind++)
                    {
                        foreach (int code in KindLookCodes)
                        {
                            if ((_drop & (1 << kind)) == 0)
                            {
                                continue;
                            }

                            int rank = RowLooks[code] + kind;
                            bits |= 1 << rank;

                            for (int i = cellsEnd; i < to; i++)
                            {
                                marked[i] |= tail[i - cellsEnd] == rank;
                            }

                            for (int i = ownFrom; i < ownTo; i++)
                            {
                                marked[i] |= head[i - ownFrom] == rank;
                            }
                        }
                    }

                    int inHead = 0;

                    for (int i = ownFrom; i < ownTo; i++)
                    {
                        inHead += marked[i] ? 1 : 0;
                    }

                    g.RemoveMarked(marked);
                    headTo -= inHead;
                    cellsEnd -= inHead;
                    var range = new DxfDimGroups.Range(g.Sub.From, headTo);
                    g.Set(93, (g.Whole(93, range, 0) & ~bits).ToString(CultureInfo.InvariantCulture), range, Order);
                }

                // The table's own looks of the data given back, each in its head after its flags where AutoCAD 2021 writes them
                // (after its last cell where it holds its own there alone), its flag in 93 (D33g k02).
                foreach (KeyValuePair<int, string> look in _restore ?? new List<KeyValuePair<int, string>>())
                {
                    int rank = RowLooks[look.Key] + 2;
                    int[] tail = TailRanks(g, headTo, cellsEnd, out int to);
                    int[] head = HeadRanks(g, headTo, out int ownFrom, out int ownTo);
                    bool inTail = ownTo == ownFrom && Array.Exists(tail, k => k >= 0);
                    g.Insert(
                        inTail
                            ? PlaceOf(g, tail, cellsEnd, to, look.Key, rank)
                            : PlaceOf(g, head, ownFrom, ownTo, look.Key, rank),
                        look
                    );
                    headTo += inTail ? 0 : 1;
                    cellsEnd += inTail ? 0 : 1;
                    var range = new DxfDimGroups.Range(g.Sub.From, headTo);
                    bool had = g.Find(93, range) >= 0;
                    g.Set(
                        93,
                        (g.Whole(93, range, 0) | (1 << rank)).ToString(CultureInfo.InvariantCulture),
                        range,
                        Order
                    );
                    headTo += had ? 0 : 1;
                    cellsEnd += had ? 0 : 1;
                }

                foreach (int code in new[] { 280, 281 })
                {
                    bool was = code == 280 ? _titleWas : _headerWas;
                    bool now = code == 280 ? _titleNow : _headerNow;

                    if (was == now)
                    {
                        continue;
                    }

                    int wanted = code - 280;
                    int[] tail = TailRanks(g, headTo, cellsEnd, out _);
                    int[] head = HeadRanks(g, headTo, out int ownFrom, out int ownTo);
                    int inTail = Array.IndexOf(tail, wanted);
                    int inHead = Array.IndexOf(head, wanted);
                    int at =
                        inTail >= 0 ? cellsEnd + inTail
                        : inHead >= 0 ? ownFrom + inHead
                        : -1;
                    var range = new DxfDimGroups.Range(g.Sub.From, headTo);

                    if (now)
                    {
                        // Shown again: its group and its flag taken out.
                        if (at >= 0)
                        {
                            g.RemoveAt(at);
                            headTo -= inTail >= 0 ? 0 : 1;
                            cellsEnd -= inTail >= 0 ? 0 : 1;
                            range = new DxfDimGroups.Range(g.Sub.From, headTo);
                        }

                        int flags = g.Whole(93, range, 0);

                        if ((flags & (1 << wanted)) != 0)
                        {
                            g.Set(93, (flags & ~(1 << wanted)).ToString(CultureInfo.InvariantCulture), range, Order);
                        }

                        continue;
                    }

                    if (at >= 0)
                    {
                        g.List[at] = new KeyValuePair<int, string>(code, "1");
                        continue;
                    }

                    g.Insert(PlaceOf(g, head, ownFrom, ownTo, code, wanted), new KeyValuePair<int, string>(code, "1"));
                    headTo++;
                    cellsEnd++;
                    range = new DxfDimGroups.Range(g.Sub.From, headTo);
                    bool had = g.Find(93, range) >= 0;
                    g.Set(
                        93,
                        (g.Whole(93, range, 0) | (1 << wanted)).ToString(CultureInfo.InvariantCulture),
                        range,
                        Order
                    );
                    headTo += had ? 0 : 1;
                    cellsEnd += had ? 0 : 1;
                }

                if (!g.Same())
                {
                    table.CheckEditable();
                    g.Write(Order);
                }
            }
        }

        #endregion
    }
}
