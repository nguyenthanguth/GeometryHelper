using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The links of a table's cells to data, as AutoCAD's <c>Table</c> keeps them: a range of cells filled from a
    /// <see cref="DxfDataLink"/> of the drawing, the link named by the range's top left cell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A range's top left cell names its link after its 92, made odd: the DATALINK's handle (340), the range's rows (93) and
    /// columns (94), and a 96 of nought, before the cell's value (<c>301 CELL_VALUE</c>). The cell (1, 0) of a table linked
    /// over (1, 0) to (2, 1) holds <c>92 1</c>, <c>340 2F</c>, <c>93 2</c>, <c>94 2</c>, <c>96 0</c>; its other cells hold
    /// nothing of it. That run is the one LibreDWG gives a cell of a TABLECONTENT, whose 92 says it has linked data; that a cell
    /// of an ACAD_TABLE names its link so, after the 92 of R2007, is the library's guess, which no file of AutoCAD has yet
    /// borne out. A link the drawing has no DATALINK of is read as none.
    /// </para>
    /// <para>
    /// Only a text cell (171 of 1) names a link: a block cell's 340 names its block, whatever its 92, and a range whose top
    /// left cell comes to be a block cell, by rows or columns taken out before it, loses its link. The library writes no new
    /// link: those of the file are read, moved and copied with their cells, and taken away.
    /// </para>
    /// </remarks>
    public sealed partial class DxfTable
    {
        // The links of the layout read last, kept with it.
        private Layout _linksOf;
        private List<LinkedRange> _links;

        #region Data links

        /// <summary>
        /// Gets the data link that fills a cell, as AutoCAD's <c>GetDataLink(row, column)</c> gives its id: that of the linked
        /// range the cell is in, the first by its top left cell row by row; null where the cell is in none.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The link, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfDataLink GetDataLink(int row, int column)
        {
            LinkedRange link = Covering(row, column, out DxfDataLink found);
            return link == null ? null : found;
        }

        /// <summary>
        /// Gets the data links of the table, as AutoCAD's <c>GetDataLink()</c> gives their ids: each once, in the order of the
        /// top left cells of their ranges, row by row; empty for a table of none.
        /// </summary>
        /// <returns>The links, a new list.</returns>
        public IReadOnlyList<DxfDataLink> GetDataLink() => LinksMeeting(null);

        /// <summary>
        /// Gets the data links of the ranges a range of cells meets, as AutoCAD's <c>GetDataLink(range)</c> gives their ids:
        /// each once, in the order of the top left cells of their ranges, row by row.
        /// </summary>
        /// <param name="range">The cells, within the table.</param>
        /// <returns>The links, a new list; empty where the range meets no linked range.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the range is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the range reaches past the table's rows or columns.</exception>
        public IReadOnlyList<DxfDataLink> GetDataLink(DxfCellRange range)
        {
            CheckRange(range, Read(), nameof(range));
            return LinksMeeting(range);
        }

        /// <summary>
        /// Gets the range of cells a cell's data link fills, as AutoCAD's <c>GetDataLinkRange</c>: from the top left cell that
        /// names the link by its rows and columns (93, 94), cut at the table's last row and column; null where the cell is in
        /// no linked range. The cell (2, 1) of a table linked over (1, 0) to (2, 1) gives that range.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The range, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfCellRange GetDataLinkRange(int row, int column)
        {
            LinkedRange link = Covering(row, column, out _);
            return link == null ? null : new DxfCellRange(this, link.TopRow, link.LeftColumn, link.BottomRow, link.RightColumn);
        }

        /// <summary>Gets whether a cell is filled from a data link, as AutoCAD's <c>IsLinked</c>: it is in a linked range, as <see cref="GetDataLinkRange"/> finds one.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>Whether it is.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public bool IsLinked(int row, int column) => Covering(row, column, out _) != null;

        /// <summary>
        /// Takes every data link of the table away, as AutoCAD's <c>RemoveDataLink()</c>: each top left cell's link groups
        /// taken out, its 92 made even, in one writing of the record. The DATALINK let go of stays in the drawing, in the
        /// dictionary of data links, named or anonymous (<see cref="DxfDataLinkOption.Anonymous"/>), as AutoCAD 2021's
        /// <c>RemoveDataLink</c> keeps it. The cells keep what they hold. Nothing where the table has none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the table has links and is erased.</exception>
        public void RemoveDataLink() => WriteLinks(ReadToWrite(), new List<LinkedRange>());

        /// <summary>
        /// Takes away the data link of the range a cell is in, as AutoCAD's <c>RemoveDataLink(row, column)</c>, as
        /// <see cref="RemoveDataLink()"/> takes every one away: the first range by its top left cell, row by row, that holds the
        /// cell. Nothing where it is in none.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is linked and the table is erased.</exception>
        public void RemoveDataLink(int row, int column)
        {
            Layout layout = ReadToWrite();
            layout.CellOf(row, column);
            var links = new List<LinkedRange>();
            bool taken = false;

            foreach (LinkedRange link in Links(layout))
            {
                if (!taken && link.Holds(row, column))
                {
                    taken = true;
                    continue;
                }

                links.Add(link);
            }

            if (taken)
            {
                WriteLinks(layout, links);
            }
        }

        /// <summary>Whether a range of the table's cells names the DATALINK of a handle.</summary>
        internal bool LinksTo(string handle)
        {
            foreach (LinkedRange link in CurrentLinks())
            {
                if (string.Equals(link.Handle, handle?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The first linked range, by its top left cell row by row, that holds a cell and whose DATALINK the drawing has; null where none does.</summary>
        private LinkedRange Covering(int row, int column, out DxfDataLink found)
        {
            Read().CellOf(row, column);
            found = null;

            foreach (LinkedRange link in CurrentLinks())
            {
                if (link.Holds(row, column) && (found = LinkObject(link.Handle)) != null)
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>The DATALINKs of the ranges a range meets, or of every range for null, each once, by their top left cells row by row.</summary>
        private IReadOnlyList<DxfDataLink> LinksMeeting(DxfCellRange range)
        {
            var found = new List<DxfDataLink>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (LinkedRange link in CurrentLinks())
            {
                DxfDataLink value = (range == null || Meets(link.Range, range)) && !seen.Contains(link.Handle) ? LinkObject(link.Handle) : null;

                if (value != null && seen.Add(link.Handle))
                {
                    found.Add(value);
                }
            }

            return found.AsReadOnly();
        }

        /// <summary>The DATALINK of a handle in the table's drawing; null where it has none, or it is erased.</summary>
        private DxfDataLink LinkObject(string handle)
        {
            DxfItem item = IsResident ? Item.File.ObjectItem(handle) : null;
            return item == null || item.IsRemoved || item.Type != "DATALINK" ? null : OfObject(item) as DxfDataLink;
        }

        /// <summary>The links of the record as it stands, read once for it, as its layout is.</summary>
        private List<LinkedRange> CurrentLinks()
        {
            Layout layout = Read();

            if (!ReferenceEquals(layout, _linksOf))
            {
                _links = Links(layout);
                _linksOf = layout;
            }

            return _links;
        }

        /// <summary>
        /// Writes the links given into the text cells in one writing of the record: each top left cell of one naming it, every
        /// other text cell naming none; a block cell, and a cell that holds what it is to, as it is. A DATALINK the cells let go
        /// of stays in the drawing, as AutoCAD's <c>RemoveDataLink</c> leaves it. Nothing is written where the record comes out
        /// the same.
        /// </summary>
        private void WriteLinks(Layout layout, IList<LinkedRange> links)
        {
            DxfDimGroups g = layout.Groups;
            LinkedRange[] anchored = Anchors(layout.CellStart.Count, layout.Columns, links);
            int first = layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd;
            var list = new List<KeyValuePair<int, string>>(g.List.Count + 5 * links.Count);

            for (int i = 0; i < first; i++)
            {
                list.Add(g.List[i]);
            }

            for (int k = 0; k < layout.CellStart.Count; k++)
            {
                int from = layout.CellStart[k];
                int to = k + 1 < layout.CellStart.Count ? layout.CellStart[k + 1] : layout.CellsEnd;
                List<KeyValuePair<int, string>> cell = g.List.GetRange(from, to - from);
                list.AddRange(IsTextCell(cell, 0) ? Linked(cell, anchored[k]) : cell);
            }

            for (int i = layout.CellsEnd; i < g.List.Count; i++)
            {
                list.Add(g.List[i]);
            }

            g.List.Clear();
            g.List.AddRange(list);

            if (!g.Same())
            {
                CheckEditable();
                g.Write(Order);
            }
        }

        /// <summary>
        /// Erases the DATALINKs of handles given that the cells let go of, each with what it owns, where it is anonymous: a named
        /// one, one this table still names, or one that any other record of the file points at (another table, a copy of this
        /// one), the dictionary that holds it and the link itself aside, is left. The file is read once for them all.
        /// </summary>
        private void EraseLinks(IEnumerable<string> dropped)
        {
            if (!IsResident)
            {
                return;
            }

            DxfFile file = Item.File;
            HashSet<string> held = null;
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<DxfItem>();
            var handles = new List<string>();
            var aside = new List<string>();

            foreach (string given in dropped)
            {
                string handle = given?.Trim();

                if (string.IsNullOrEmpty(handle) || !taken.Add(handle))
                {
                    continue;
                }

                // The cells of the table as it stands now, read once, and only where a link was let go of.
                held = held ?? new HashSet<string>(LinkHandles(Read()), StringComparer.OrdinalIgnoreCase);

                if (held.Contains(handle))
                {
                    continue;
                }

                DxfItem item = file.ObjectItem(handle);

                if (item != null && !item.IsRemoved && item.Type == "DATALINK" && OfObject(item) is DxfDataLink link
                    && (link.DataLinkOption & DxfDataLinkOption.Anonymous) != 0)
                {
                    items.Add(item);
                    handles.Add(handle);
                    aside.Add(handle);
                    aside.Add(file.OwnerOf(item));
                }
            }

            if (items.Count == 0)
            {
                return;
            }

            Dictionary<string, string> pointed = file.PointedAmong(handles, aside, new string[0], out _);

            for (int k = 0; k < items.Count; k++)
            {
                if (!pointed.ContainsKey(handles[k]) && !items[k].IsRemoved)
                {
                    DxfDBDictionary.EraseFromOwner(OfObject(items[k]), file);
                }
            }
        }

        #endregion

        #region The links of the cells

        /// <summary>
        /// The links the cells of a layout name, by their top left cells row by row: each a text cell whose first 92 before its
        /// value is odd and followed by a handle (340), its rows (93) and columns (94) after it, one where it gives none, the
        /// range cut at the table's last row and column; a block cell, and a cell past the table's rows, names none.
        /// </summary>
        private static List<LinkedRange> Links(Layout layout)
        {
            var links = new List<LinkedRange>();
            int columns = layout.Columns;

            for (int k = 0; columns > 0 && k < layout.CellStart.Count && k / columns < layout.Rows; k++)
            {
                int from = layout.CellStart[k];
                int to = k + 1 < layout.CellStart.Count ? layout.CellStart[k + 1] : layout.CellsEnd;
                int at = IsTextCell(layout.Groups.List, from) ? LinkFlagAt(layout.Groups.List, from, to) : -1;

                if (at < 0 || LinkRun(layout.Groups.List, at, to) == 0 || layout.Groups.List[at + 1].Key != 340)
                {
                    continue;
                }

                List<KeyValuePair<int, string>> list = layout.Groups.List;
                string handle = list[at + 1].Value.Trim();
                int next = at + 2;
                long rows = 1;
                long wide = 1;

                if (next < to && list[next].Key == 93)
                {
                    rows = Count(list[next].Value);
                    next++;
                }

                if (next < to && list[next].Key == 94)
                {
                    wide = Count(list[next].Value);
                }

                int row = k / columns;
                int column = k % columns;

                if (handle.Length > 0)
                {
                    links.Add(new LinkedRange(row, column, (int)Math.Min(layout.Rows - 1, row + rows - 1), (int)Math.Min(columns - 1, column + wide - 1), handle));
                }
            }

            return links;
        }

        /// <summary>The handles of the links a layout's cells name, each once, by their top left cells row by row.</summary>
        private static List<string> LinkHandles(Layout layout)
        {
            var handles = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (LinkedRange link in Links(layout))
            {
                if (seen.Add(link.Handle))
                {
                    handles.Add(link.Handle);
                }
            }

            return handles;
        }

        /// <summary>The link each cell is to name, by its index row by row: the first of those given whose top left cell it is; null for the rest.</summary>
        private static LinkedRange[] Anchors(int cells, int columns, IList<LinkedRange> links)
        {
            var anchored = new LinkedRange[cells];

            foreach (LinkedRange link in links)
            {
                long index = (long)link.TopRow * columns + link.LeftColumn;

                if (columns > 0 && link.LeftColumn < columns && index < cells && anchored[index] == null)
                {
                    anchored[index] = link;
                }
            }

            return anchored;
        }

        /// <summary>
        /// A cell's groups naming a link as asked: the link's handle, rows and columns (340, 93, 94) and a 96 of nought after
        /// its 92, made odd, put in after its turn where it has none; or, for null, the link's groups taken out and its 92 made
        /// even. The groups as they are where they name it so already, a link of no 96 among them.
        /// </summary>
        private static List<KeyValuePair<int, string>> Linked(List<KeyValuePair<int, string>> groups, LinkedRange link)
        {
            int at = LinkFlagAt(groups, 0, groups.Count);
            int run = at < 0 ? 0 : LinkRun(groups, at, groups.Count);

            if (link == null && run == 0)
            {
                return groups;
            }

            string rows = link == null ? null : (link.BottomRow - link.TopRow + 1).ToString(CultureInfo.InvariantCulture);
            string columns = link == null ? null : (link.RightColumn - link.LeftColumn + 1).ToString(CultureInfo.InvariantCulture);

            if (link != null && run >= 3 && groups[at + 1].Key == 340 && groups[at + 2].Key == 93 && groups[at + 3].Key == 94
                && string.Equals(groups[at + 1].Value.Trim(), link.Handle, StringComparison.OrdinalIgnoreCase)
                && Count(groups[at + 2].Value) == link.BottomRow - link.TopRow + 1 && Count(groups[at + 3].Value) == link.RightColumn - link.LeftColumn + 1)
            {
                return groups;
            }

            var made = new List<KeyValuePair<int, string>>(groups);
            made.RemoveRange(at < 0 ? 0 : at + 1, run);

            if (at < 0)
            {
                CellView view = View(made);
                view.SetHead(92, "0");
                made = view.Groups.List;
                at = LinkFlagAt(made, 0, made.Count);
            }

            int flags = int.TryParse(made[at].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read) ? read : 0;
            made[at] = new KeyValuePair<int, string>(92, (link == null ? flags & ~1 : flags | 1).ToString(CultureInfo.InvariantCulture));

            if (link != null)
            {
                made.InsertRange(at + 1, new[]
                {
                    new KeyValuePair<int, string>(340, link.Handle),
                    new KeyValuePair<int, string>(93, rows),
                    new KeyValuePair<int, string>(94, columns),
                    new KeyValuePair<int, string>(96, "0"),
                });
            }

            return made;
        }

        /// <summary>Whether the cell whose 171 stands at a place is a text cell: its 171 of 1, the only kind of cell that names a link.</summary>
        private static bool IsTextCell(List<KeyValuePair<int, string>> list, int at)
            => at < list.Count && list[at].Key == 171 && list[at].Value.Trim() == "1";

        /// <summary>Where a cell's first 92 stands, before its value (<c>301 CELL_VALUE</c>); -1 where it has none.</summary>
        private static int LinkFlagAt(List<KeyValuePair<int, string>> list, int from, int to)
        {
            for (int i = from + 1; i < to; i++)
            {
                int code = list[i].Key;

                if (code == 301 && list[i].Value.Trim() == "CELL_VALUE")
                {
                    return -1;
                }

                if (code == 92)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// How many groups of a link follow a cell's 92 where it is odd: a 340, a 93, a 94 and a 96, each where it stands in
        /// that order; nought for a 92 that is even or followed by none of them.
        /// </summary>
        private static int LinkRun(List<KeyValuePair<int, string>> list, int at, int to)
        {
            if (!int.TryParse(list[at].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int flags) || (flags & 1) == 0)
            {
                return 0;
            }

            int next = at + 1;

            foreach (int code in new[] { 340, 93, 94, 96 })
            {
                next += next < to && list[next].Key == code ? 1 : 0;
            }

            return next - at - 1;
        }

        /// <summary>A count of rows or columns of a link, one where it gives none above nought.</summary>
        private static long Count(string text)
            => long.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long count) && count >= 1 ? count : 1;

        /// <summary>The links as rows (or columns) are put in at a place: one they go above or left of moves with its cells, one they go into grows by them, as a block of merged cells does.</summary>
        private static List<LinkedRange> LinksInserted(Layout layout, bool rowsOf, int at, int count)
        {
            var links = new List<LinkedRange>();

            foreach (LinkedRange link in Links(layout))
            {
                int first = rowsOf ? link.TopRow : link.LeftColumn;
                int last = rowsOf ? link.BottomRow : link.RightColumn;
                int newFirst = at <= first ? first + count : first;
                int newLast = at <= last ? last + count : last;
                links.Add(rowsOf ? link.With(newFirst, link.LeftColumn, newLast, link.RightColumn) : link.With(link.TopRow, newFirst, link.BottomRow, newLast));
            }

            return links;
        }

        /// <summary>The links as rows (or columns) are taken out from a place: each shrunk to what is left of it, and gone where nothing is, as a block of merged cells is.</summary>
        private static List<LinkedRange> LinksDeleted(Layout layout, bool rowsOf, int at, int count)
        {
            var links = new List<LinkedRange>();

            foreach (LinkedRange link in Links(layout))
            {
                int first = rowsOf ? link.TopRow : link.LeftColumn;
                int last = rowsOf ? link.BottomRow : link.RightColumn;
                int newFirst = first < at ? first : first >= at + count ? first - count : at;
                int newLast = last < at ? last : last >= at + count ? last - count : at - 1;

                if (newLast >= newFirst)
                {
                    links.Add(rowsOf ? link.With(newFirst, link.LeftColumn, newLast, link.RightColumn) : link.With(link.TopRow, newFirst, link.BottomRow, newLast));
                }
            }

            return links;
        }

        /// <summary>The links of a table given a size: each cut at its last row and column, and gone where its top left cell is past them.</summary>
        private static List<LinkedRange> LinksCut(Layout layout, int rows, int columns)
        {
            var links = new List<LinkedRange>();

            foreach (LinkedRange link in Links(layout))
            {
                if (link.TopRow < rows && link.LeftColumn < columns)
                {
                    links.Add(link.With(link.TopRow, link.LeftColumn, Math.Min(link.BottomRow, rows - 1), Math.Min(link.RightColumn, columns - 1)));
                }
            }

            return links;
        }

        /// <summary>A range of cells linked to the DATALINK of a handle.</summary>
        private sealed class LinkedRange
        {
            internal LinkedRange(int topRow, int leftColumn, int bottomRow, int rightColumn, string handle)
            {
                TopRow = topRow;
                LeftColumn = leftColumn;
                BottomRow = bottomRow;
                RightColumn = rightColumn;
                Handle = handle;
            }

            internal int TopRow { get; }

            internal int LeftColumn { get; }

            internal int BottomRow { get; }

            internal int RightColumn { get; }

            internal string Handle { get; }

            internal DxfCellRange Range => new DxfCellRange(TopRow, LeftColumn, BottomRow, RightColumn);

            internal bool Holds(int row, int column) => row >= TopRow && row <= BottomRow && column >= LeftColumn && column <= RightColumn;

            internal LinkedRange With(int topRow, int leftColumn, int bottomRow, int rightColumn) => new LinkedRange(topRow, leftColumn, bottomRow, rightColumn, Handle);
        }

        #endregion
    }
}
