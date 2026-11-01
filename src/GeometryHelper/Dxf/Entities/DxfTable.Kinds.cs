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
        private static List<KeyValuePair<int, string>> WithLine(List<KeyValuePair<int, string>> groups, int edge, CellLine line, bool valued)
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
                cell.SetHead(cell.Head(91) != null ? 91 : 177, (flags.Value | flagged).ToString(CultureInfo.InvariantCulture));
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
            internal List<KeyValuePair<int, string>> NewCell(List<KeyValuePair<int, string>> cell, int row, bool valued)
                => row == 0 ? WithLine(cell, 0, Top, valued) : row == 1 ? WithLine(cell, 0, Under, valued) : cell;
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
        private static List<DxfCellRange> MergesAlong(List<DxfCellRange> found, bool rowsOf, int inheritFrom, int at, int count, List<DxfCellRange> blocks)
        {
            var copied = new List<DxfCellRange>();

            foreach (DxfCellRange b in found)
            {
                bool along = rowsOf ? b.TopRow == inheritFrom && b.BottomRow == inheritFrom : b.LeftColumn == inheritFrom && b.RightColumn == inheritFrom;

                for (int line = at; along && line < at + count; line++)
                {
                    var block = rowsOf ? new DxfCellRange(line, b.LeftColumn, line, b.RightColumn) : new DxfCellRange(b.TopRow, line, b.BottomRow, line);

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
        /// AutoCAD reads the table's looks of a kind from its first row of that kind (D28d d1 to d3, D28c o7). The cells are
        /// changed in place; the head is written after the record (<see cref="RowKinds.WriteHead"/>). Null where nothing changes.
        /// </summary>
        private RowKinds Kinds(Layout layout, List<KeyValuePair<int, string>>[] cells, int columns, int at, int count, int inheritFrom, bool valued)
        {
            bool titleOn = !IsTitleSuppressed;
            bool headerOn = !IsHeaderSuppressed;
            int rows = layout.Rows + count;
            int firstData = (titleOn ? 1 : 0) + (headerOn ? 1 : 0);
            Func<int, int> kindOf = r => titleOn && r == 0 ? 0 : headerOn && r == (titleOn ? 1 : 0) ? 1 : 2;
            int source = inheritFrom >= 0 ? kindOf(inheritFrom) : 2;
            var kinds = new int[rows];

            for (int r = 0; r < rows; r++)
            {
                kinds[r] = r < at ? kindOf(r) : r < at + count ? source : kindOf(r - count);
            }

            bool titleNow = titleOn && kinds[0] == 0;
            int headerAt = titleNow ? 1 : 0;
            bool headerNow = headerOn && headerAt < rows && kinds[headerAt] == 1;
            bool outOfPlace = false;

            for (int r = 0; r < rows && !outOfPlace; r++)
            {
                outOfPlace = kinds[r] != 2 && kinds[r] != (titleNow && r == 0 ? 0 : headerNow && r == headerAt ? 1 : 2);
            }

            List<KeyValuePair<int, string>>[] own = (inheritFrom < 0 && at <= firstData) || outOfPlace ? OwnLooks(layout) : null;
            bool drop = own != null && inheritFrom < 0 && at <= firstData && own[2].Count > 0;

            if (!outOfPlace && !drop && titleNow == titleOn && headerNow == headerOn)
            {
                return null;
            }

            var looks = new List<KeyValuePair<int, string>>[3];

            for (int r = 0; r < rows; r++)
            {
                int place = titleNow && r == 0 ? 0 : headerNow && r == headerAt ? 1 : 2;
                bool added = r >= at && r < at + count;
                List<KeyValuePair<int, string>> give = kinds[r] != 2 && kinds[r] != place ? looks[kinds[r]] ?? (looks[kinds[r]] = KindLooks(kinds[r], own)) : drop && kinds[r] == 2 && !added ? own[2] : null;

                for (int c = 0; give != null && give.Count > 0 && c < columns; c++)
                {
                    cells[r * columns + c] = WithMissing(cells[r * columns + c], give, valued);
                }
            }

            return new RowKinds(titleOn && !titleNow, headerOn && !headerNow, drop);
        }

        /// <summary>A cell's groups given as its own each look given that it has not of its own.</summary>
        private static List<KeyValuePair<int, string>> WithMissing(List<KeyValuePair<int, string>> groups, List<KeyValuePair<int, string>> looks, bool valued)
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
        /// 0 where the style's 283 turns it on, else 1), text colour (its 62, as a cell's 64) and text height (140); each kind of
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
                looks[rowKind] = new[] { rowKind == 2 ? "2" : "5", "Standard", "7", "1", "0", Numbers.Exact(heights[rowKind] + 0.0) };
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

                int slot = group.TypeCode == 170 ? 0 : group.TypeCode == 7 ? 1 : group.TypeCode == 63 ? 2 : group.TypeCode == 283 ? 3 : group.TypeCode == 62 ? 4 : -1;

                if (slot >= 0)
                {
                    string text = group.Value.Trim();
                    looks[kinds[block]][slot] = slot == 3 ? (text == "1" ? "0" : "1") : text;
                }
            }

            return looks;
        }

        /// <summary>What <see cref="Kinds"/> found to write in the table's head: its title and its header left out, and its own looks of the data taken away.</summary>
        private sealed class RowKinds
        {
            private readonly bool _titleOff;
            private readonly bool _headerOff;
            private readonly bool _dropData;

            internal RowKinds(bool titleOff, bool headerOff, bool dropData)
            {
                _titleOff = titleOff;
                _headerOff = headerOff;
                _dropData = dropData;
            }

            /// <summary>
            /// Writes them in the table's head as AutoCAD 2021 wrote them: the table's own looks of the data taken out wherever
            /// they stand, with their flags in 93; 280 and 281 of 1 set where the table holds them, else put in its head after
            /// its flags with theirs (93 bits 0 and 1), <c>93 3</c> ... <c>96 0</c>, <c>280 1</c>, <c>281 1</c>, <c>141 ...</c>;
            /// the record written once, nothing where it comes out the same.
            /// </summary>
            internal void WriteHead(DxfTable table)
            {
                if (!_titleOff && !_headerOff && !_dropData)
                {
                    return;
                }

                Layout layout = table.ReadToWrite();
                DxfDimGroups g = layout.Groups;
                int headTo = layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd;
                int cellsEnd = layout.CellsEnd;

                if (_dropData)
                {
                    int[] tail = TailRanks(g, headTo, cellsEnd, out int to);
                    int[] head = HeadRanks(g, headTo, out int ownFrom, out int ownTo);
                    var marked = new bool[g.List.Count];
                    int bits = 0;

                    foreach (int code in KindLookCodes)
                    {
                        int rank = RowLooks[code] + 2;
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

                foreach (int code in new[] { 280, 281 })
                {
                    if (code == 280 ? !_titleOff : !_headerOff)
                    {
                        continue;
                    }

                    int wanted = code - 280;
                    int[] tail = TailRanks(g, headTo, cellsEnd, out _);
                    int[] head = HeadRanks(g, headTo, out int ownFrom, out int ownTo);
                    int inTail = Array.IndexOf(tail, wanted);
                    int inHead = Array.IndexOf(head, wanted);

                    if (inTail >= 0 || inHead >= 0)
                    {
                        int at = inTail >= 0 ? cellsEnd + inTail : ownFrom + inHead;
                        g.List[at] = new KeyValuePair<int, string>(code, "1");
                        continue;
                    }

                    g.Insert(PlaceOf(g, head, ownFrom, ownTo, code, wanted), new KeyValuePair<int, string>(code, "1"));
                    headTo++;
                    cellsEnd++;
                    var range = new DxfDimGroups.Range(g.Sub.From, headTo);
                    bool had = g.Find(93, range) >= 0;
                    g.Set(93, (g.Whole(93, range, 0) | (1 << wanted)).ToString(CultureInfo.InvariantCulture), range, Order);
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
