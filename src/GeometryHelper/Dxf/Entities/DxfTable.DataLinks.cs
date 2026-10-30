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
    /// AutoCAD 2021 names a link in the TABLECONTENT it keeps for the table from R2007 on, which the table's extension
    /// dictionary holds under ACAD_XREC_ROUNDTRIP, through an XRECORD of ACAD_ROUNDTRIP_2008_TABLE_ENTITY: the range's top
    /// left cell there, after its custom data, has its 92 odd, then the DATALINK's handle (340), the range's rows (93) and
    /// columns (94) and a 96, its state (90) with the flag 4 of a linked cell. AutoCAD's table F6 linked over (1, 0) to (2, 1),
    /// which it read back as (1, 0) alone, has in its TABLECONTENT 149 the cell (1, 0) of <c>90 4</c> and <c>92 1</c>,
    /// <c>340 142</c>, <c>93 1</c>, <c>94 1</c>, <c>96 1</c>, and in its ACAD_TABLE no link at all, <c>92 0</c> in every
    /// cell. That is read here, while the TABLECONTENT has the table's rows and columns: one a program that does not know it
    /// left behind as the table changed names none.
    /// </para>
    /// <para>
    /// The same run straight after the 92 of a cell of the ACAD_TABLE, which the library first guessed, is read as well, as
    /// before; AutoCAD reads no link from it, and reads the cells after such a run awry (its 74-a, saved again, had them a
    /// cell along). Only a text cell (171 of 1) names a link so: a block cell's 340 names its block, whatever its 92, and a
    /// range whose top left cell comes to be a block cell, by rows or columns taken out before it, loses its link. Those
    /// links are moved and copied with their cells; a link of the TABLECONTENT is not: rows or columns put in or taken out
    /// leave the TABLECONTENT as it was, as before. A link the drawing has no DATALINK of is read as none.
    /// </para>
    /// <para>
    /// The library writes no new link: those of the file are read and taken away, as AutoCAD's <c>RemoveDataLink</c> takes
    /// them, the TABLECONTENT's cell made unlinked in place, its 92 even, its run out and its flag 4 cleared.
    /// </para>
    /// </remarks>
    public sealed partial class DxfTable
    {
        // The links of the layout read last, kept with it and with the count of the file's changes they were read at.
        private Layout _linksOf;
        private int _linksEdits;
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
        /// taken out and its 92 made even, in the TABLECONTENT, where the cell's flag 4 of a linked cell is cleared too, and in
        /// the cells of the ACAD_TABLE, in one writing of each record. The DATALINK let go of stays in the drawing, in the
        /// dictionary of data links, named or anonymous (<see cref="DxfDataLinkOption.Anonymous"/>), as AutoCAD 2021's
        /// <c>RemoveDataLink</c> keeps it, the table and what it owns taken out of the objects it lists itself tied to (its
        /// 94 and their 330s): AutoCAD's link of the table F6 alone, <c>94 2</c>, <c>330 F6</c>, <c>330 149</c>, comes out
        /// <c>94 0</c>. The cells keep what they hold. Nothing where the table has none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the table has links and is erased.</exception>
        public void RemoveDataLink()
        {
            Layout layout = ReadToWrite();
            List<LinkedRange> before = AllLinks(layout);
            WriteLinks(layout, new List<LinkedRange>());
            TakeOutOfContent(before.FindAll(l => l.Cell != null));
            LetGo(Handles(before));
        }

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
            List<LinkedRange> before = AllLinks(layout);
            LinkedRange taken = before.Find(l => l.Holds(row, column));

            if (taken == null)
            {
                return;
            }

            if (taken.Cell != null)
            {
                TakeOutOfContent(new List<LinkedRange> { taken });
            }
            else
            {
                WriteLinks(layout, before.FindAll(l => l.Cell == null && !ReferenceEquals(l, taken)));
            }

            LetGo(Handles(before));
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

        /// <summary>The links of the record and of its TABLECONTENT as they stand, read once for them while the file is not changed.</summary>
        private List<LinkedRange> CurrentLinks()
        {
            Layout layout = Read();
            int edits = IsResident ? Item.File.EditCount : 0;

            if (!ReferenceEquals(layout, _linksOf) || edits != _linksEdits)
            {
                _links = AllLinks(layout);
                _linksOf = layout;
                _linksEdits = edits;
            }

            return _links;
        }

        /// <summary>
        /// The links of the table, those its TABLECONTENT names and those its cells name, by their top left cells row by row, a
        /// link of the TABLECONTENT before one of the cells at the same cell, then by their bottom right cells and handles.
        /// </summary>
        private List<LinkedRange> AllLinks(Layout layout)
        {
            List<LinkedRange> links = ContentLinks(layout);
            links.AddRange(Links(layout));
            links.Sort((a, b) =>
            {
                int by = a.TopRow.CompareTo(b.TopRow);
                by = by != 0 ? by : a.LeftColumn.CompareTo(b.LeftColumn);
                by = by != 0 ? by : (a.Cell == null).CompareTo(b.Cell == null);
                by = by != 0 ? by : a.BottomRow.CompareTo(b.BottomRow);
                by = by != 0 ? by : a.RightColumn.CompareTo(b.RightColumn);
                return by != 0 ? by : string.CompareOrdinal(a.Handle, b.Handle);
            });
            return links;
        }

        /// <summary>The handles of links, each once, in their order.</summary>
        private static List<string> Handles(List<LinkedRange> links)
        {
            var handles = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (LinkedRange link in links)
            {
                if (seen.Add(link.Handle))
                {
                    handles.Add(link.Handle);
                }
            }

            return handles;
        }

        /// <summary>
        /// Writes the links given into the text cells in one writing of the record: each top left cell of one naming it, every
        /// other text cell naming none; a block cell, and a cell that holds what it is to, as it is. Nothing is written where
        /// the record comes out the same.
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
        /// Takes the table out of the list of the objects each DATALINK the cells let go of is tied to, as AutoCAD 2021's
        /// <c>RemoveDataLink</c> takes it out: the count (94) after the link's status and the handles (330) that follow it, the
        /// table's own and those of what it owns, through its extension dictionary, at any depth (AutoCAD's TABLECONTENT of the
        /// table), left out, every other kept, the count made to agree. A link this table still names is left as it is, and so
        /// is the rest of the link, its 360 and its custom data. AutoCAD's link tied to the table F6 alone, <c>94 2</c>,
        /// <c>330 F6</c>, <c>330 149</c>, comes out <c>94 0</c>.
        /// </summary>
        private void LetGo(List<string> dropped)
        {
            if (!IsResident || dropped.Count == 0)
            {
                return;
            }

            DxfFile file = Item.File;
            var held = new HashSet<string>(Handles(CurrentLinks()), StringComparer.OrdinalIgnoreCase);

            foreach (string handle in dropped)
            {
                DxfItem item = held.Contains(handle) ? null : file.ObjectItem(handle);

                if (item == null || item.IsRemoved || item.Type != "DATALINK")
                {
                    continue;
                }

                IReadOnlyList<DxfTypedValue> groups = file.GroupsOf(item);
                int count = TargetsAt(groups);

                if (count < 0)
                {
                    continue;
                }

                List<DxfTypedValue> tail = file.TailOf(item, count);
                var kept = new List<KeyValuePair<int, string>>();
                int next = 1;

                while (next < tail.Count && tail[next].TypeCode == 330)
                {
                    if (!OwnedByThis(file, tail[next].Value))
                    {
                        kept.Add(new KeyValuePair<int, string>(330, tail[next].Value));
                    }

                    next++;
                }

                if (kept.Count == next - 1)
                {
                    continue;
                }

                kept.Insert(0, new KeyValuePair<int, string>(94, kept.Count.ToString(CultureInfo.InvariantCulture)));

                for (int k = next; k < tail.Count; k++)
                {
                    kept.Add(new KeyValuePair<int, string>(tail[k].TypeCode, tail[k].Value));
                }

                file.ReplaceTail(item, count, kept);
            }
        }

        /// <summary>
        /// Where the count of the objects a DATALINK is tied to stands among its groups: the first 94 of AcDbDataLink, after its
        /// status (304); -1 where it has none.
        /// </summary>
        private static int TargetsAt(IReadOnlyList<DxfTypedValue> groups)
        {
            bool braced = false;
            bool own = false;

            for (int i = 0; i < groups.Count; i++)
            {
                int code = groups[i].TypeCode;

                if (code == 102)
                {
                    braced = groups[i].Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && code == 100)
                {
                    own = groups[i].Value.Trim() == "AcDbDataLink";
                }
                else if (own && code == 94)
                {
                    return i;
                }
                else if (code >= 1000)
                {
                    break;
                }
            }

            return -1;
        }

        /// <summary>Whether a handle is the table's, or that of an object it owns at any depth, its owners followed up (330), sixteen deep at most.</summary>
        private bool OwnedByThis(DxfFile file, string handle)
        {
            string at = handle?.Trim();

            for (int depth = 0; depth < 16 && !string.IsNullOrEmpty(at); depth++)
            {
                if (string.Equals(at, Handle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                DxfItem owner = file.ObjectItem(at);
                at = owner == null || owner.IsRemoved ? null : file.OwnerOf(owner)?.Trim();
            }

            return false;
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

                // The links of the table as it stands now, read once, and only where a link was let go of.
                held = held ?? new HashSet<string>(Handles(CurrentLinks()), StringComparer.OrdinalIgnoreCase);

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

        #region The links of the TABLECONTENT

        /// <summary>
        /// The links the TABLECONTENT of the table names, by their cells row by row: each cell whose 92 after its custom data is
        /// odd and followed by a handle (340), its range of the rows (93) and columns (94) after it, one where it gives none, cut
        /// at the table's last row and column; none where the table has no TABLECONTENT, or one of other rows or columns.
        /// </summary>
        private List<LinkedRange> ContentLinks(Layout layout)
        {
            var links = new List<LinkedRange>();
            DxfItem content = IsResident ? ContentItem() : null;

            if (content == null)
            {
                return links;
            }

            IReadOnlyList<DxfTypedValue> groups = Item.File.GroupsOf(content);
            var cells = new List<ContentCell>();
            var widths = new List<int>();

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode != 1)
                {
                    continue;
                }

                string marker = groups[i].Value.Trim();

                if (marker == "LINKEDTABLEDATAROW_BEGIN")
                {
                    widths.Add(0);
                }
                else if (marker == "LINKEDTABLEDATACELL_BEGIN" && widths.Count > 0)
                {
                    int row = widths.Count - 1;
                    int column = widths[row]++;
                    i = ReadCell(groups, i, row, column, cells);
                }
            }

            if (widths.Count != layout.Rows || widths.Exists(w => w != layout.Columns))
            {
                return links;
            }

            foreach (ContentCell cell in cells)
            {
                links.Add(new LinkedRange(cell.Row, cell.Column, (int)Math.Min(layout.Rows - 1, cell.Row + cell.Rows - 1), (int)Math.Min(layout.Columns - 1, cell.Column + cell.Columns - 1), cell.Handle, cell));
            }

            return links;
        }

        /// <summary>
        /// The field of a cell's first content in the TABLECONTENT of the table (its content of type 2 and the 340 after it),
        /// where AutoCAD keeps a formula; null where the table has no TABLECONTENT of its rows and columns, or the cell's first
        /// content is no field.
        /// </summary>
        private string ContentFieldOf(int row, int column)
        {
            Layout layout = Read();
            DxfItem content = ContentItem();

            if (content == null)
            {
                return null;
            }

            IReadOnlyList<DxfTypedValue> groups = Item.File.GroupsOf(content);
            var widths = new List<int>();
            string found = null;

            for (int i = 0; i < groups.Count; i++)
            {
                string marker = groups[i].TypeCode == 1 ? groups[i].Value.Trim() : null;

                if (marker == "LINKEDTABLEDATAROW_BEGIN")
                {
                    widths.Add(0);
                }
                else if (marker == "LINKEDTABLEDATACELL_BEGIN" && widths.Count > 0)
                {
                    int r = widths.Count - 1;
                    int c = widths[r]++;

                    if (r != row || c != column)
                    {
                        continue;
                    }

                    // The cell's first content after its 92: CONTENT, CELLCONTENT_BEGIN, its type 2 of a field, the field.
                    int at = i + 1;

                    while (at < groups.Count && !(groups[at].TypeCode == 309 && groups[at].Value.Trim() == "LINKEDTABLEDATACELL_END") && !(groups[at].TypeCode == 1 && groups[at].Value.Trim() == "CELLCONTENT_BEGIN"))
                    {
                        at++;
                    }

                    found = at + 2 < groups.Count && groups[at].TypeCode == 1 && groups[at + 1].TypeCode == 90 && groups[at + 1].Value.Trim() == "2" && groups[at + 2].TypeCode == 340 ? groups[at + 2].Value.Trim() : null;
                }
            }

            return widths.Count == layout.Rows && !widths.Exists(w => w != layout.Columns) ? found : null;
        }

        /// <summary>
        /// Reads a cell of a TABLECONTENT from its LINKEDTABLEDATACELL_BEGIN: its state (the 90 after it), and the 92 after the
        /// end of its custom data (DATAMAP_END) with the run of its link where that is odd; a cell that names a link is added.
        /// Gives where the reading stopped, its custom data's end or the cell's.
        /// </summary>
        private static int ReadCell(IReadOnlyList<DxfTypedValue> groups, int begin, int row, int column, List<ContentCell> cells)
        {
            int flagAt = begin + 1 < groups.Count && groups[begin + 1].TypeCode == 90 ? begin + 1 : -1;
            int end = begin + 1;

            while (end < groups.Count && !(groups[end].TypeCode == 309 && (groups[end].Value.Trim() == "DATAMAP_END" || groups[end].Value.Trim() == "LINKEDTABLEDATACELL_END")))
            {
                end++;
            }

            int at = end + 1;

            if (end >= groups.Count || groups[end].Value.Trim() != "DATAMAP_END" || at >= groups.Count || groups[at].TypeCode != 92
                || !int.TryParse(groups[at].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int linked) || (linked & 1) == 0
                || at + 1 >= groups.Count || groups[at + 1].TypeCode != 340 || groups[at + 1].Value.Trim().Length == 0)
            {
                return end;
            }

            int next = at + 1;

            foreach (int code in new[] { 340, 93, 94, 96 })
            {
                next += next < groups.Count && groups[next].TypeCode == code ? 1 : 0;
            }

            long rows = at + 2 < next && groups[at + 2].TypeCode == 93 ? Count(groups[at + 2].Value) : 1;
            int wideAt = at + 2 < next && groups[at + 2].TypeCode == 93 ? at + 3 : at + 2;
            long wide = wideAt < next && groups[wideAt].TypeCode == 94 ? Count(groups[wideAt].Value) : 1;
            cells.Add(new ContentCell(row, column, flagAt, at, next - at - 1, groups[at + 1].Value.Trim(), rows, wide));
            return end;
        }

        /// <summary>
        /// The TABLECONTENT AutoCAD keeps for the table: the one the XRECORD of ACAD_ROUNDTRIP_2008_TABLE_ENTITY points at
        /// (360), which the table's extension dictionary holds under ACAD_XREC_ROUNDTRIP; null where there is none.
        /// </summary>
        private DxfItem ContentItem()
        {
            DxfFile file = Item.File;
            DxfItem dictionary = file.ObjectItem(file.ExtensionDictionaryOf(Item));
            DxfItem record = dictionary == null || dictionary.Type != "DICTIONARY" ? null : file.ObjectItem(EntryOf(file.GroupsOf(dictionary), "ACAD_XREC_ROUNDTRIP"));

            if (record == null || record.Type != "XRECORD")
            {
                return null;
            }

            IReadOnlyList<DxfTypedValue> groups = file.GroupsOf(record);

            for (int i = 0; i + 1 < groups.Count; i++)
            {
                if (groups[i].TypeCode == 102 && groups[i].Value.Trim() == "ACAD_ROUNDTRIP_2008_TABLE_ENTITY")
                {
                    DxfItem content = groups[i + 1].TypeCode == 360 ? file.ObjectItem(groups[i + 1].Value) : null;
                    return content != null && content.Type == "TABLECONTENT" ? content : null;
                }
            }

            return null;
        }

        /// <summary>The handle of the entry of a name of a dictionary's groups: the 350 or 360 after its 3; null where it has none.</summary>
        private static string EntryOf(IReadOnlyList<DxfTypedValue> groups, string name)
        {
            for (int i = 0; i + 1 < groups.Count; i++)
            {
                if (groups[i].TypeCode == 3 && string.Equals(groups[i].Value, name, StringComparison.Ordinal)
                    && (groups[i + 1].TypeCode == 350 || groups[i + 1].TypeCode == 360))
                {
                    return groups[i + 1].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Makes cells of the TABLECONTENT unlinked, as AutoCAD's <c>RemoveDataLink</c>: each one's 92 made even and the run of
        /// its link (340, 93, 94, 96) taken out, its state's flag 4 of a linked cell cleared, in one writing of the record, every
        /// other group kept as it is.
        /// </summary>
        private void TakeOutOfContent(List<LinkedRange> links)
        {
            if (links.Count == 0)
            {
                return;
            }

            CheckEditable();
            DxfItem content = ContentItem();

            if (content == null)
            {
                return;
            }

            var cells = links.ConvertAll(l => l.Cell);
            cells.Sort((a, b) => b.LinkAt.CompareTo(a.LinkAt));
            int from = cells[cells.Count - 1].FlagAt >= 0 ? cells[cells.Count - 1].FlagAt : cells[cells.Count - 1].LinkAt;
            List<DxfTypedValue> tail = Item.File.TailOf(content, from);
            var groups = tail.ConvertAll(g => new KeyValuePair<int, string>(g.TypeCode, g.Value));

            foreach (ContentCell cell in cells)
            {
                int at = cell.LinkAt - from;
                int linked = int.TryParse(groups[at].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read) ? read : 1;
                groups[at] = new KeyValuePair<int, string>(92, (linked & ~1).ToString(CultureInfo.InvariantCulture));
                groups.RemoveRange(at + 1, cell.Run);

                if (cell.FlagAt >= 0 && int.TryParse(groups[cell.FlagAt - from].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int flags) && (flags & 4) != 0)
                {
                    groups[cell.FlagAt - from] = new KeyValuePair<int, string>(90, (flags & ~4).ToString(CultureInfo.InvariantCulture));
                }
            }

            Item.File.ReplaceTail(content, from, groups);
        }

        /// <summary>A cell of a TABLECONTENT that names a link: where its state (90) and its 92 stand among the record's groups, and the run of its link after the 92.</summary>
        private sealed class ContentCell
        {
            internal ContentCell(int row, int column, int flagAt, int linkAt, int run, string handle, long rows, long columns)
            {
                Row = row;
                Column = column;
                FlagAt = flagAt;
                LinkAt = linkAt;
                Run = run;
                Handle = handle;
                Rows = rows;
                Columns = columns;
            }

            internal int Row { get; }

            internal int Column { get; }

            internal int FlagAt { get; }

            internal int LinkAt { get; }

            internal int Run { get; }

            internal string Handle { get; }

            internal long Rows { get; }

            internal long Columns { get; }
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

        /// <summary>A range of cells linked to the DATALINK of a handle, by a cell of the TABLECONTENT or of the table's own.</summary>
        private sealed class LinkedRange
        {
            internal LinkedRange(int topRow, int leftColumn, int bottomRow, int rightColumn, string handle, ContentCell cell = null)
            {
                TopRow = topRow;
                LeftColumn = leftColumn;
                BottomRow = bottomRow;
                RightColumn = rightColumn;
                Handle = handle;
                Cell = cell;
            }

            internal int TopRow { get; }

            internal int LeftColumn { get; }

            internal int BottomRow { get; }

            internal int RightColumn { get; }

            internal string Handle { get; }

            /// <summary>The cell of the TABLECONTENT that names the link; null for a link a cell of the table's own names.</summary>
            internal ContentCell Cell { get; }

            internal DxfCellRange Range => new DxfCellRange(TopRow, LeftColumn, BottomRow, RightColumn);

            internal bool Holds(int row, int column) => row >= TopRow && row <= BottomRow && column >= LeftColumn && column <= RightColumn;

            internal LinkedRange With(int topRow, int leftColumn, int bottomRow, int rightColumn) => new LinkedRange(topRow, leftColumn, bottomRow, rightColumn, Handle);
        }

        #endregion
    }
}
