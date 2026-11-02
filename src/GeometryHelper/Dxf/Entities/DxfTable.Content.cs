using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The TABLECONTENT AutoCAD keeps for a table, kept in step with the table's rows, columns and merged cells, and made for a
    /// table of none when a cell of it is linked, as AutoCAD 2021 writes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AutoCAD 2021 reads a table's text, values, formats, heights, widths and merged cells from the ACAD_TABLE, whatever its
    /// TABLECONTENT holds: the text of a cell changed by the library, its checksum in the TABLECONTENT left as it was or made
    /// to agree, a merge taken out of the ACAD_TABLE alone or out of the TABLECONTENT alone, it read as the ACAD_TABLE has them.
    /// From the TABLECONTENT it reads the links, the style of each row (1 a title, 2 a header, 3 data) and what the ACAD_TABLE
    /// cannot hold, such as the borders of a cell, cell by cell as the TABLECONTENT's rows and columns stand: a row put in
    /// above a linked cell by a program that left the TABLECONTENT as it was took the link to the new row, and a row taken out
    /// left the next row the style of the one taken out. AutoCAD's own <c>InsertRows</c>, <c>DeleteRows</c>,
    /// <c>InsertColumns</c>, <c>DeleteColumns</c> and <c>SetSize</c> write the TABLECONTENT's rows and columns in step, each
    /// cell with its link and its format, and so does the library.
    /// </para>
    /// <para>
    /// AutoCAD reads the TABLECONTENT of a table only where the drawing's ACDB_RECOMPOSE_DATA lists the table: its own 73 with
    /// that record taken out it read with no link. A table of no TABLECONTENT linked by the library is given one as AutoCAD
    /// gave the ezdxf table of its 73 one, with ACDB_RECOMPOSE_DATA listing the table and its style; AutoCAD read the link of
    /// its own 73 with the TABLEGEOMETRY and every cell's content taken out of it, so the library writes neither.
    /// </para>
    /// </remarks>
    public sealed partial class DxfTable
    {
        #region The rows and columns of the TABLECONTENT

        /// <summary>
        /// Puts rows (or columns) into the TABLECONTENT as they are put into the table: before the row (or column) given, each
        /// of empty cells as AutoCAD 2021 writes a cell of no content, or of cells that take the look of those of the row (or
        /// column) given (<see cref="InheritedCellGroups"/>), a row of data (style 3) or of the style of the row it takes the
        /// look of, of the height (or width) given; the links and merged ranges after them moved on and those they go
        /// into grown, as the table's own. Nothing where the table has no TABLECONTENT, or the place is past its last row (or
        /// column), as a TABLECONTENT of fewer rows than its table AutoCAD read has. The first of the rows is of the header's
        /// style (2) where <paramref name="headerFirst"/> asks, as AutoCAD 2021 wrote the first row put into a table of one row
        /// (<see cref="NewKind"/>). The height (or width) is written as a real,
        /// 10.0, as AutoCAD writes it. Where the title of a table of the columns given (more than none) is merged across them all,
        /// the range of the title, (0, 0) to (0, last), grows by the columns put in wherever they go, as AutoCAD 2021 listed it.
        /// </summary>
        private void ContentInserted(bool rowsOf, int at, int count, string size, int inheritFrom, int titleColumns = 0, bool headerFirst = false)
        {
            size = ContentNumber(size);
            Func<int[], int[]> title = titleColumns <= 0 || rowsOf ? (Func<int[], int[]>)null
                : m => m[0] == 0 && m[1] == 0 && m[2] == 0 && m[3] == titleColumns - 1 ? new[] { 0, 0, 0, m[3] + count } : null;

            EditContent(rowsOf, (first, last) => new[] { at <= first ? first + count : first, at <= last ? last + count : last }, grid =>
            {
                if (rowsOf)
                {
                    if (at > grid.Rows.Count)
                    {
                        return;
                    }

                    ContentRow source = inheritFrom >= 0 && inheritFrom < grid.Rows.Count ? grid.Rows[inheritFrom] : null;
                    string style = source != null ? StyleOf(source) ?? "3" : "3";

                    for (int k = 0; k < count; k++)
                    {
                        // Each goes in before the one put in before it: the last put in is the first, the header of a table of one row.
                        ContentRow row = NewRow(grid.Columns.Count, headerFirst && k == count - 1 ? "2" : style, size);

                        for (int c = 0; source != null && c < row.Cells.Count && c < source.Cells.Count; c++)
                        {
                            row.Cells[c] = new ContentGridCell(InheritedCellGroups(source.Cells[c].Groups, true));
                        }

                        grid.Rows.Insert(at, row);
                    }
                }
                else
                {
                    if (at > grid.Columns.Count)
                    {
                        return;
                    }

                    for (int k = 0; k < count; k++)
                    {
                        grid.Columns.Insert(at, ColumnGroups(size));
                    }

                    for (int r = 0; r < grid.Rows.Count; r++)
                    {
                        ContentRow row = grid.Rows[r];
                        List<KeyValuePair<int, string>> source = inheritFrom >= 0 && inheritFrom < row.Cells.Count ? row.Cells[inheritFrom].Groups : null;

                        for (int k = 0; at <= row.Cells.Count && k < count; k++)
                        {
                            // A cell the title merged across the table takes in holds no content, as AutoCAD 2021 wrote it.
                            row.Cells.Insert(at, new ContentGridCell(source == null ? CellGroups("0.0") : InheritedCellGroups(source, r > 0 || titleColumns <= 0)));
                        }
                    }
                }

                grid.Changed = true;
            }, title);
        }

        /// <summary>
        /// Takes rows (or columns) out of the TABLECONTENT as they are taken out of the table: their cells, with the links they
        /// name, and the merged ranges and links of the rest shrunk to what is left of them; each DATALINK no cell names any
        /// more no longer tied to the TABLECONTENT, the table left among its targets, as AutoCAD 2021 wrote its link after its
        /// own <c>DeleteRows</c> of the linked row (<c>94 1</c>, <c>330 F6</c>). Nothing where the table has no TABLECONTENT.
        /// </summary>
        private void ContentDeleted(bool rowsOf, int at, int count)
        {
            EditContent(rowsOf, (first, last) =>
            {
                int newFirst = first < at ? first : first >= at + count ? first - count : at;
                int newLast = last < at ? last : last >= at + count ? last - count : at - 1;
                return newLast < newFirst ? null : new[] { newFirst, newLast };
            }, grid =>
            {
                if (rowsOf)
                {
                    for (int r = Math.Min(at + count, grid.Rows.Count) - 1; r >= at; r--)
                    {
                        grid.Rows[r].Cells.ForEach(c => c.Gone = true);
                        grid.Rows[r].Cells.ForEach(c => grid.Dropped.AddRange(FieldsOf(c.Groups)));
                        grid.Rows.RemoveAt(r);
                        grid.Changed = true;
                    }

                    return;
                }

                for (int c = Math.Min(at + count, grid.Columns.Count) - 1; c >= at; c--)
                {
                    grid.Columns.RemoveAt(c);
                    grid.Changed = true;
                }

                foreach (ContentRow row in grid.Rows)
                {
                    for (int c = Math.Min(at + count, row.Cells.Count) - 1; c >= at; c--)
                    {
                        row.Cells[c].Gone = true;
                        grid.Dropped.AddRange(FieldsOf(row.Cells[c].Groups));
                        row.Cells.RemoveAt(c);
                        grid.Changed = true;
                    }
                }
            });
        }

        /// <summary>
        /// Lists a range of cells merged in the TABLECONTENT, or takes the merged ranges it meets out, as AutoCAD 2021 listed and
        /// took out its merge of (1, 0) to (1, 1): every range the range meets taken out, and, merged, the range put in, the list
        /// row by row by the top left cells. Where <paramref name="empty"/> asks, the cells of the range but its top left are
        /// emptied (<see cref="EmptyContent"/>), as AutoCAD 2021's <c>MergeCells</c> emptied them. Nothing where the table has
        /// no TABLECONTENT, or it lists them so already.
        /// </summary>
        private void ContentMerged(DxfCellRange range, bool merged, bool empty = false)
        {
            EditContent(true, null, grid =>
            {
                for (int r = range.TopRow; empty && r <= range.BottomRow && r < grid.Rows.Count; r++)
                {
                    for (int c = range.LeftColumn; c <= range.RightColumn && c < grid.Rows[r].Cells.Count; c++)
                    {
                        grid.Changed |= (r != range.TopRow || c != range.LeftColumn) && EmptyContent(grid, grid.Rows[r].Cells[c].Groups);
                    }
                }

                if (grid.Merges == null)
                {
                    return;
                }

                var kept = grid.Merges.FindAll(m => m[0] > range.BottomRow || m[2] < range.TopRow || m[1] > range.RightColumn || m[3] < range.LeftColumn);

                if (merged)
                {
                    kept.Add(new[] { range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn });
                    kept.Sort(CompareRanges);
                }

                grid.Changed |= !SameRanges(kept, grid.Merges);
                grid.Merges = kept;
            });
        }

        /// <summary>
        /// Empties a cell of the TABLECONTENT (<see cref="EmptyContent"/>), as AutoCAD 2021's <c>ResetValue</c> of a block cell
        /// emptied it (D33c r04). Nothing where the table has no TABLECONTENT, or it holds no such cell.
        /// </summary>
        private void ContentEmptied(int row, int column)
        {
            EditContent(true, null, grid =>
            {
                if (row < grid.Rows.Count && column < grid.Rows[row].Cells.Count)
                {
                    grid.Changed |= EmptyContent(grid, grid.Rows[row].Cells[column].Groups);
                }
            });
        }

        /// <summary>
        /// Edits the TABLECONTENT of the table, read as rows of cells: the edit given made, then each merged range and each link
        /// a cell names taken through the lines given (rows or columns: a range's first and last line to their new ones, null
        /// where none is left), or through the map given, where it gives a range, the links of cells taken out let go of as
        /// <see cref="ContentDeleted"/> says; the fields the cells taken out held (a content of the kind 2, its 340) taken out of
        /// the list of fields after the rows (92 and its 360s) and erased where the TABLECONTENT owns them and no cell left holds
        /// them, as AutoCAD 2021's <c>DeleteRows</c> and <c>DeleteColumns</c> of its 78 left <c>92 0</c>; written in one writing
        /// of the record where anything changed. Nothing where the table has no TABLECONTENT, or one not in the form AutoCAD
        /// writes, which is left as it is.
        /// </summary>
        private void EditContent(bool rowsOf, Func<int, int, int[]> lines, Action<ContentGrid> edit, Func<int[], int[]> map = null)
        {
            DxfItem content = IsResident ? ContentItem() : null;
            ContentGrid grid = content == null ? null : ContentGrid.Read(Item.File, content);

            if (grid == null)
            {
                return;
            }

            // The links of the cells, with their places and ranges as they stand before the edit.
            var links = new List<KeyValuePair<ContentGridCell, int[]>>();

            for (int r = 0; r < grid.Rows.Count; r++)
            {
                for (int c = 0; c < grid.Rows[r].Cells.Count; c++)
                {
                    int[] run = LinkRunOf(grid.Rows[r].Cells[c].Groups);

                    if (run != null)
                    {
                        links.Add(new KeyValuePair<ContentGridCell, int[]>(grid.Rows[r].Cells[c], new[] { r, c, run[0], run[1] }));
                    }
                }
            }

            edit(grid);
            var dropped = new List<string>();

            if (lines != null)
            {
                if (grid.Merges != null)
                {
                    var moved = new List<int[]>();

                    foreach (int[] m in grid.Merges)
                    {
                        int[] mapped = map?.Invoke(m);

                        if (mapped != null)
                        {
                            moved.Add(mapped);
                            continue;
                        }

                        int[] span = lines(rowsOf ? m[0] : m[1], rowsOf ? m[2] : m[3]);

                        if (span != null)
                        {
                            moved.Add(rowsOf ? new[] { span[0], m[1], span[1], m[3] } : new[] { m[0], span[0], m[2], span[1] });
                        }
                    }

                    grid.Changed |= !SameRanges(moved, grid.Merges);
                    grid.Merges = moved;
                }

                foreach (KeyValuePair<ContentGridCell, int[]> link in links)
                {
                    string handle = LinkHandleOf(link.Key.Groups);

                    if (link.Key.Gone)
                    {
                        dropped.Add(handle);
                        continue;
                    }

                    int first = rowsOf ? link.Value[0] : link.Value[1];
                    int[] span = lines(first, first + (rowsOf ? link.Value[2] : link.Value[3]) - 1);
                    grid.Changed |= span != null && SetLinkCount(link.Key.Groups, rowsOf ? 93 : 94, span[1] - span[0] + 1);
                }
            }

            // The fields of the cells taken out that no cell left holds.
            var fields = new List<string>();

            if (grid.Dropped.Count > 0)
            {
                var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                grid.Rows.ForEach(r => r.Cells.ForEach(c => held.UnionWith(FieldsOf(c.Groups))));
                fields = grid.Dropped.FindAll(h => !held.Contains(h));
                grid.Changed |= grid.DropFields(fields);
            }

            if (!grid.Changed)
            {
                return;
            }

            CheckEditable();
            Item.File.ReplaceTail(content, grid.From, grid.Groups());

            foreach (string handle in dropped)
            {
                if (handle != null && !grid.Names(handle))
                {
                    Untie(handle, content.Handle);
                }
            }

            var owned = new List<DxfItem>();

            foreach (string handle in fields)
            {
                DxfItem field = Item.File.ObjectItem(handle);

                if (field != null && !field.IsRemoved && field.Type == "FIELD" && string.Equals(Item.File.OwnerOf(field), content.Handle, StringComparison.OrdinalIgnoreCase) && !owned.Contains(field))
                {
                    owned.Add(field);
                }
            }

            if (owned.Count > 0)
            {
                DxfField.Erase(Item.File, owned);
            }
        }

        /// <summary>
        /// Takes a cell's content out of the TABLECONTENT, as AutoCAD 2021 wrote the other cells of a block it merged (D28e p8,
        /// q1): no content (<c>95 0</c>), its checksum <c>140 0.0</c>, the fields it held let go; its format and lines kept.
        /// False where it holds none and its checksum is nought already, or it is not in the form AutoCAD writes.
        /// </summary>
        private static bool EmptyContent(ContentGrid grid, List<KeyValuePair<int, string>> cell)
        {
            int end = cell.FindIndex(g => g.Key == 309 && g.Value.Trim() == "LINKEDTABLEDATACELL_END");
            int mapEnd = cell.FindIndex(g => g.Key == 309 && g.Value.Trim() == "DATAMAP_END");
            int count = mapEnd < 0 || end < 0 ? -1 : cell.FindIndex(mapEnd, end - mapEnd, g => g.Key == 95);

            if (count < 0)
            {
                return false;
            }

            bool changed = false;
            int sum = cell.FindIndex(g => g.Key == 300 && g.Value.Trim() == "ACAD_ROUNDTRIP_2008_CELL_CHECKSUM");
            int real = sum < 0 ? -1 : cell.FindIndex(sum, mapEnd - sum, g => g.Key == 140);

            if (real >= 0 && cell[real].Value.Trim() != "0.0")
            {
                cell[real] = new KeyValuePair<int, string>(140, "0.0");
                changed = true;
            }

            if (end > count + 1 || cell[count].Value.Trim() != "0")
            {
                grid.Dropped.AddRange(FieldsOf(cell));
                cell.RemoveRange(count + 1, end - count - 1);
                cell[count] = new KeyValuePair<int, string>(95, "0");
                changed = true;
            }

            return changed;
        }

        /// <summary>The fields a cell of the TABLECONTENT holds: the 340 of each of its contents of the kind 2 (<c>1 CELLCONTENT_BEGIN</c>, <c>90 2</c>, <c>340</c>).</summary>
        private static List<string> FieldsOf(List<KeyValuePair<int, string>> cell)
        {
            var fields = new List<string>();

            for (int i = 0; i + 2 < cell.Count; i++)
            {
                if (cell[i].Key == 1 && cell[i].Value.Trim() == "CELLCONTENT_BEGIN" && cell[i + 1].Key == 90 && cell[i + 1].Value.Trim() == "2" && cell[i + 2].Key == 340)
                {
                    fields.Add(cell[i + 2].Value.Trim());
                }
            }

            return fields;
        }

        /// <summary>The style of a row of the TABLECONTENT: the 90 after its TABLEROW_BEGIN; null where it has none.</summary>
        private static string StyleOf(ContentRow row)
        {
            for (int i = 0; i + 1 < row.Tail.Count; i++)
            {
                if (row.Tail[i].Key == 1 && row.Tail[i].Value.Trim() == "TABLEROW_BEGIN" && row.Tail[i + 1].Key == 90)
                {
                    return row.Tail[i + 1].Value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// The rows and columns of the range a cell of the TABLECONTENT names a link over: those of its run after the odd 92 that
        /// follows its custom data (93, 94; one where it gives none); null where it names no link.
        /// </summary>
        private static int[] LinkRunOf(List<KeyValuePair<int, string>> cell)
        {
            int at = LinkFlagOf(cell);

            if (at < 0 || at + 1 >= cell.Count || cell[at + 1].Key != 340)
            {
                return null;
            }

            int rows = 1;
            int columns = 1;

            for (int i = at + 2; i < cell.Count && (cell[i].Key == 93 || cell[i].Key == 94); i++)
            {
                int count = (int)Count(cell[i].Value);
                rows = cell[i].Key == 93 ? count : rows;
                columns = cell[i].Key == 94 ? count : columns;
            }

            return new[] { rows, columns };
        }

        /// <summary>The DATALINK a cell of the TABLECONTENT names (its 340 after the odd 92); null where it names none.</summary>
        private static string LinkHandleOf(List<KeyValuePair<int, string>> cell)
        {
            int at = LinkFlagOf(cell);
            return at >= 0 && at + 1 < cell.Count && cell[at + 1].Key == 340 ? cell[at + 1].Value.Trim() : null;
        }

        /// <summary>Where the 92 after a cell's custom data stands, odd; -1 where it is even or the cell has none.</summary>
        private static int LinkFlagOf(List<KeyValuePair<int, string>> cell)
        {
            int end = cell.FindIndex(g => g.Key == 309 && g.Value.Trim() == "DATAMAP_END");

            if (end < 0 || end + 1 >= cell.Count || cell[end + 1].Key != 92)
            {
                return -1;
            }

            return int.TryParse(cell[end + 1].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int linked) && (linked & 1) != 0 ? end + 1 : -1;
        }

        /// <summary>
        /// Makes the rows (93) or columns (94) of the run of a cell's link the count given, put in after the 340 (and the 93) where
        /// the run has none and the count is not one; whether anything changed.
        /// </summary>
        private static bool SetLinkCount(List<KeyValuePair<int, string>> cell, int code, int count)
        {
            int at = LinkFlagOf(cell) + 2;
            int place = at;

            for (; place < cell.Count && (cell[place].Key == 93 || cell[place].Key == 94); place++)
            {
                if (cell[place].Key == code)
                {
                    if (Count(cell[place].Value) == count)
                    {
                        return false;
                    }

                    cell[place] = new KeyValuePair<int, string>(code, count.ToString(CultureInfo.InvariantCulture));
                    return true;
                }
            }

            if (count == 1)
            {
                return false;
            }

            cell.Insert(code == 93 ? at : place, new KeyValuePair<int, string>(code, count.ToString(CultureInfo.InvariantCulture)));
            return true;
        }

        /// <summary>
        /// Takes the TABLECONTENT out of the objects a DATALINK is tied to: its 330 left out of the list after the link's count
        /// (94), the count made to agree, every other kept, as AutoCAD 2021 wrote its link after its own <c>DeleteRows</c> of the
        /// linked row. Nothing where the link is not tied to it.
        /// </summary>
        private void Untie(string link, string content)
        {
            DxfFile file = Item.File;
            DxfItem item = file.ObjectItem(link);

            if (item == null || item.IsRemoved || item.Type != "DATALINK")
            {
                return;
            }

            int count = TargetsAt(file.GroupsOf(item));

            if (count < 0)
            {
                return;
            }

            List<DxfTypedValue> tail = file.TailOf(item, count);
            var kept = new List<KeyValuePair<int, string>>();
            int next = 1;

            while (next < tail.Count && tail[next].TypeCode == 330)
            {
                if (!string.Equals(tail[next].Value.Trim(), content, StringComparison.OrdinalIgnoreCase))
                {
                    kept.Add(new KeyValuePair<int, string>(330, tail[next].Value));
                }

                next++;
            }

            if (kept.Count == next - 1)
            {
                return;
            }

            kept.Insert(0, new KeyValuePair<int, string>(94, kept.Count.ToString(CultureInfo.InvariantCulture)));

            for (int k = next; k < tail.Count; k++)
            {
                kept.Add(new KeyValuePair<int, string>(tail[k].TypeCode, tail[k].Value));
            }

            file.ReplaceTail(item, count, kept);
        }

        /// <summary>Orders merged ranges row by row by their top left cells, then by their bottom right cells.</summary>
        private static int CompareRanges(int[] a, int[] b)
        {
            for (int k = 0; k < 4; k++)
            {
                int by = a[k].CompareTo(b[k]);

                if (by != 0)
                {
                    return by;
                }
            }

            return 0;
        }

        /// <summary>Whether two lists of merged ranges are the same, range by range.</summary>
        private static bool SameRanges(List<int[]> a, List<int[]> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int k = 0; k < a.Count; k++)
            {
                if (CompareRanges(a[k], b[k]) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region A TABLECONTENT made

        /// <summary>
        /// Makes the TABLECONTENT of a table of none, as AutoCAD 2021 made one when it linked the ezdxf table of its 73: in the
        /// table's extension dictionary, made where it has none, an XRECORD under ACAD_XREC_ROUNDTRIP of
        /// ACAD_ROUNDTRIP_2008_TABLE_ENTITY pointing at it (360) with the groups AutoCAD writes after (70 2, 90 1, a point of
        /// nought, 90 0, 90 2); the TABLECONTENT owned by the XRECORD, its columns of the table's widths, its rows of their
        /// styles (1 the title, 2 the header, 3 data) and heights, each cell one of no content of the checksum of its text, the
        /// table's merged cells and its table style; the table and its style listed in ACDB_RECOMPOSE_DATA, which AutoCAD reads
        /// the TABLECONTENT by, and the class of TABLECONTENT in the CLASSES section. No TABLEGEOMETRY is written: AutoCAD lays the
        /// table out anew.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the drawing is one before R2007, which has no TABLECONTENT, or
        /// the table's record does not hold its cells, heights and widths.</exception>
        private DxfItem NewContent()
        {
            DxfFile file = Item.File;

            if (!file.Supports(1021))
            {
                throw new InvalidOperationException("The table " + Handle + " is of a drawing before R2007 (AC1021), which holds no TABLECONTENT to name a link in.");
            }

            Layout layout = Structured();
            string style = StyleHandle();
            List<KeyValuePair<int, string>> groups = NewContentGroups(layout, style);
            CreateExtensionDictionary();
            DxfItem record = ExtensionDictionary.Add("ACAD_XREC_ROUNDTRIP", "XRECORD", b =>
            {
                b.Group(100, "AcDbXrecord");
                b.Group(280, "1");
                b.Group(102, "ACAD_ROUNDTRIP_2008_TABLE_ENTITY");
                b.Group(360, "0");
                b.Group(70, "2");
                b.Group(90, "1");
                b.Group(10, "0.0");
                b.Group(20, "0.0");
                b.Group(30, "0.0");
                b.Group(90, "0");
                b.Group(90, "2");
            });

            DxfItem content = file.AddObject("TABLECONTENT", null, b =>
            {
                b.Group(330, record.Handle);
                b.Group(100, "AcDbLinkedData");
                b.Group(1, string.Empty);
                b.Group(300, string.Empty);
                b.Group(100, "AcDbLinkedTableData");

                foreach (KeyValuePair<int, string> group in groups)
                {
                    b.Group(group.Key, group.Value);
                }
            });

            // The XRECORD's 360 made the TABLECONTENT's, its handle known now.
            IReadOnlyList<DxfTypedValue> made = file.GroupsOf(record);
            int at = 0;

            while (at < made.Count && !(made[at].TypeCode == 360 && made[at].Value.Trim() == "0"))
            {
                at++;
            }

            List<KeyValuePair<int, string>> tail = file.TailOf(record, at).ConvertAll(g => new KeyValuePair<int, string>(g.TypeCode, g.Value));
            tail[0] = new KeyValuePair<int, string>(360, content.Handle);
            file.ReplaceTail(record, at, tail);
            Recompose(style);
            file.AddClass("TABLECONTENT", "AcDbTableContent", 1152);
            return content;
        }

        /// <summary>The handle of the table's style: its 342 as written, else the drawing's style Standard, else nought.</summary>
        private string StyleHandle()
        {
            DxfDimGroups g = Own();
            string handle = g.Text(342, g.Sub)?.Trim();

            if (!string.IsNullOrEmpty(handle))
            {
                return handle;
            }

            DxfDBDictionary styles = Item.File.Database.TableStyleDictionary;
            return styles != null && styles.Contains("Standard") ? styles.GetAt("Standard")?.Handle ?? "0" : "0";
        }

        /// <summary>
        /// The groups of a TABLECONTENT for the table as it stands, from the count of its columns on, as AutoCAD 2021 wrote them
        /// for its 73: each column of its width, each row of its style and height and of one cell of no content for each column,
        /// the checksum of each text cell that of its text (Parts 1 655), of a block cell nought; no field; the merged cells,
        /// read from the spans (175, 176) of their top left cells, as AutoCAD reads them; the table style.
        /// </summary>
        private List<KeyValuePair<int, string>> NewContentGroups(Layout layout, string style)
        {
            var groups = new List<KeyValuePair<int, string>> { G(90, layout.Columns) };

            foreach (int place in layout.ColumnAt)
            {
                groups.AddRange(ColumnGroups(ContentNumber(layout.Groups.List[place].Value)));
            }

            groups.Add(G(91, layout.Rows));
            var merges = new List<int[]>();

            for (int r = 0; r < layout.Rows; r++)
            {
                var cells = new List<ContentGridCell>();

                for (int c = 0; c < layout.Columns; c++)
                {
                    CellView cell = layout.CellAt(r, c);
                    bool text = cell.Whole(171, 1) != (int)DxfTableCellType.BlockCell;
                    cells.Add(new ContentGridCell(CellGroups(text ? Checksum(DxfReader.Decode(cell.Text())) : "0.0")));
                    int wide = cell.Whole(175, 0);
                    int high = cell.Whole(176, 0);

                    if (wide > 1 || high > 1)
                    {
                        merges.Add(new[] { r, c, Math.Min(layout.Rows - 1, r + Math.Max(1, high) - 1), Math.Min(layout.Columns - 1, c + Math.Max(1, wide) - 1) });
                    }
                }

                DxfRowType type = RowType(r);
                string kind = type == DxfRowType.TitleRow ? "1" : type == DxfRowType.HeaderRow ? "2" : "3";
                var row = new ContentRow(new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(301, "ROW"), new KeyValuePair<int, string>(1, "LINKEDTABLEDATAROW_BEGIN") }, null, cells, RowTail(kind, ContentNumber(layout.Groups.List[layout.RowAt[r]].Value)));
                row.AddTo(groups);
            }

            groups.Add(G(92, 0));
            groups.Add(new KeyValuePair<int, string>(100, "AcDbFormattedTableData"));
            groups.Add(new KeyValuePair<int, string>(300, "TABLEFORMAT"));
            groups.Add(new KeyValuePair<int, string>(1, "TABLEFORMAT_BEGIN"));
            groups.Add(G(90, 4));
            groups.Add(G(170, 0));
            groups.Add(new KeyValuePair<int, string>(309, "TABLEFORMAT_END"));
            ContentGrid.AddMerges(groups, merges);
            groups.Add(new KeyValuePair<int, string>(100, "AcDbTableContent"));
            groups.Add(new KeyValuePair<int, string>(340, style));
            return groups;
        }

        /// <summary>
        /// Lists the table and its style in the drawing's ACDB_RECOMPOSE_DATA, as AutoCAD 2021 lists every table it keeps a
        /// TABLECONTENT for: an XRECORD of the named object dictionary, its 90 of 1 and then a 330 for each object, by their
        /// handles (<c>330 F6</c>, <c>330 127</c>, <c>330 165</c> for two tables of the style 127), made where the drawing has
        /// none; a handle listed already is not listed again.
        /// </summary>
        private void Recompose(string style)
        {
            DxfFile file = Item.File;
            DxfDBDictionary root = file.Database.NamedObjectsDictionary;

            if (root == null)
            {
                return;
            }

            var wanted = new List<string> { Handle };

            if (style != "0")
            {
                wanted.Add(style);
            }

            DxfDBObject held = root.Contains("ACDB_RECOMPOSE_DATA") ? root.GetAt("ACDB_RECOMPOSE_DATA") : null;

            if (held == null || held.Item.Type != "XRECORD")
            {
                wanted.Sort(CompareHandles);
                root.Add("ACDB_RECOMPOSE_DATA", "XRECORD", b =>
                {
                    b.Group(100, "AcDbXrecord");
                    b.Group(280, "1");
                    b.Group(90, "1");
                    wanted.ForEach(h => b.Group(330, h));
                });
                return;
            }

            IReadOnlyList<DxfTypedValue> groups = file.GroupsOf(held.Item);
            int at = 0;

            while (at < groups.Count && !(groups[at].TypeCode == 100 && groups[at].Value.Trim() == "AcDbXrecord"))
            {
                at++;
            }

            while (at < groups.Count && groups[at].TypeCode != 90)
            {
                at++;
            }

            if (at >= groups.Count)
            {
                return;
            }

            List<DxfTypedValue> tail = file.TailOf(held.Item, at + 1);
            var listed = new List<string>();
            int next = 0;

            while (next < tail.Count && tail[next].TypeCode == 330)
            {
                listed.Add(tail[next].Value.Trim());
                next++;
            }

            int before = listed.Count;

            foreach (string handle in wanted)
            {
                if (!listed.Exists(h => string.Equals(h, handle, StringComparison.OrdinalIgnoreCase)))
                {
                    listed.Add(handle);
                }
            }

            if (listed.Count == before)
            {
                return;
            }

            listed.Sort(CompareHandles);
            var written = listed.ConvertAll(h => new KeyValuePair<int, string>(330, h));

            for (int k = next; k < tail.Count; k++)
            {
                written.Add(new KeyValuePair<int, string>(tail[k].TypeCode, tail[k].Value));
            }

            file.ReplaceTail(held.Item, at + 1, written);
        }

        /// <summary>Orders handles by their numbers, then as written.</summary>
        private static int CompareHandles(string a, string b)
        {
            bool aRead = ulong.TryParse(a, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong x);
            bool bRead = ulong.TryParse(b, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong y);
            int by = aRead && bRead ? x.CompareTo(y) : aRead.CompareTo(bRead);
            return by != 0 ? by : string.CompareOrdinal(a, b);
        }

        /// <summary>The checksum AutoCAD 2021 keeps for a cell's text in its TABLECONTENT: the sum of each letter's code times its place from one, as a real (Parts 1 655.0).</summary>
        private static string Checksum(string text) => ChecksumOf(text).ToString(CultureInfo.InvariantCulture) + ".0";

        /// <summary>
        /// A height or a width as AutoCAD 2021 writes one in a TABLECONTENT, a real: 10 written 10.0, 7.5 as it is; a text that
        /// reads as no finite number as it is.
        /// </summary>
        private static string ContentNumber(string text)
        {
            if (!Numbers.TryRead(text, out double value) || !Guard.IsFinite(value))
            {
                return text;
            }

            string exact = Numbers.Exact(value + 0.0);
            return exact.IndexOf('.') >= 0 || exact.IndexOf('E') >= 0 || exact.IndexOf('e') >= 0 ? exact : exact + ".0";
        }

        /// <summary>The checksum of a cell's text, as <see cref="Checksum"/> writes it, as a number (Parts 1 655).</summary>
        private static long ChecksumOf(string text)
        {
            long sum = 0;

            for (int k = 0; k < text.Length; k++)
            {
                sum += text[k] * (long)(k + 1);
            }

            return sum;
        }

        #endregion

        #region The groups AutoCAD 2021 writes

        private static KeyValuePair<int, string> G(int code, int value) => new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>A column of a TABLECONTENT as AutoCAD 2021 writes one: of no custom data, its formats of nought, its style 0 and its width.</summary>
        private static List<KeyValuePair<int, string>> ColumnGroups(string width) => new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(300, "COLUMN"), new KeyValuePair<int, string>(1, "LINKEDTABLEDATACOLUMN_BEGIN"), new KeyValuePair<int, string>(300, string.Empty),
            G(91, 0), new KeyValuePair<int, string>(301, "CUSTOMDATA"), new KeyValuePair<int, string>(1, "DATAMAP_BEGIN"), G(90, 0),
            new KeyValuePair<int, string>(309, "DATAMAP_END"), new KeyValuePair<int, string>(309, "LINKEDTABLEDATACOLUMN_END"),
            new KeyValuePair<int, string>(1, "FORMATTEDTABLEDATACOLUMN_BEGIN"), new KeyValuePair<int, string>(300, "COLUMNTABLEFORMAT"),
            new KeyValuePair<int, string>(1, "TABLEFORMAT_BEGIN"), G(90, 3), G(170, 0), new KeyValuePair<int, string>(309, "TABLEFORMAT_END"),
            new KeyValuePair<int, string>(309, "FORMATTEDTABLEDATACOLUMN_END"), new KeyValuePair<int, string>(1, "TABLECOLUMN_BEGIN"), G(90, 0),
            new KeyValuePair<int, string>(40, width), new KeyValuePair<int, string>(309, "TABLECOLUMN_END"),
        };

        /// <summary>
        /// A cell of a TABLECONTENT as AutoCAD 2021 writes one of no content (the cells of the row it put in): of the state 0,
        /// its custom data of one checksum, the one given, its 92 of 0, no content, its format of nought.
        /// </summary>
        private static List<KeyValuePair<int, string>> CellGroups(string checksum) => new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(300, "CELL"), new KeyValuePair<int, string>(1, "LINKEDTABLEDATACELL_BEGIN"), G(90, 0),
            new KeyValuePair<int, string>(300, string.Empty), G(91, 0), new KeyValuePair<int, string>(301, "CUSTOMDATA"),
            new KeyValuePair<int, string>(1, "DATAMAP_BEGIN"), G(90, 1), new KeyValuePair<int, string>(300, "ACAD_ROUNDTRIP_2008_CELL_CHECKSUM"),
            new KeyValuePair<int, string>(301, "DATAMAP_VALUE"), G(93, 2), G(90, 2), new KeyValuePair<int, string>(140, checksum), G(94, 0),
            new KeyValuePair<int, string>(300, string.Empty), new KeyValuePair<int, string>(302, string.Empty), new KeyValuePair<int, string>(304, "ACVALUE_END"),
            new KeyValuePair<int, string>(309, "DATAMAP_END"), G(92, 0), G(95, 0), new KeyValuePair<int, string>(309, "LINKEDTABLEDATACELL_END"),
            new KeyValuePair<int, string>(1, "FORMATTEDTABLEDATACELL_BEGIN"), new KeyValuePair<int, string>(300, "CELLTABLEFORMAT"),
            new KeyValuePair<int, string>(1, "TABLEFORMAT_BEGIN"), G(90, 1), G(170, 1), G(91, 0), G(92, 0), G(62, 257), G(93, 1),
            new KeyValuePair<int, string>(300, "CONTENTFORMAT"), new KeyValuePair<int, string>(1, "CONTENTFORMAT_BEGIN"), G(90, 0), G(91, 0), G(92, 512),
            G(93, 0), new KeyValuePair<int, string>(300, string.Empty), new KeyValuePair<int, string>(40, "0.0"), new KeyValuePair<int, string>(140, "1.0"),
            G(94, 1), G(62, 0), new KeyValuePair<int, string>(340, "0"), new KeyValuePair<int, string>(144, "0.18"),
            new KeyValuePair<int, string>(309, "CONTENTFORMAT_END"), G(171, 0), G(94, 0), new KeyValuePair<int, string>(309, "TABLEFORMAT_END"),
            new KeyValuePair<int, string>(309, "FORMATTEDTABLEDATACELL_END"), new KeyValuePair<int, string>(1, "TABLECELL_BEGIN"), G(90, 0), G(91, 0),
            new KeyValuePair<int, string>(309, "TABLECELL_END"),
        };

        /// <summary>
        /// A cell of a TABLECONTENT as AutoCAD 2021's <c>InsertRowsAndInherit</c> and <c>InsertColumnsAndInherit</c> write one
        /// that takes the look of another (D28c i1 to i7, D28 b03 and b06): of the state 0, its checksum 0.0 and no link; where
        /// the other holds a content and the cell is to, an empty value of the flags 5 (93 5, the kind of the other's value,
        /// <see cref="InheritedKind"/>, 4 for one of a block or a field) with the format of the other's content
        /// (FORMATTEDCELLCONTENT), else no content; the other's format (FORMATTEDTABLEDATACELL), its margins and lines, as it
        /// stands. A cell of no format in the form AutoCAD writes gives a cell of no content (<see cref="CellGroups"/>).
        /// </summary>
        private static List<KeyValuePair<int, string>> InheritedCellGroups(List<KeyValuePair<int, string>> other, bool content)
        {
            int formatFrom = other.FindIndex(g => g.Key == 1 && g.Value.Trim() == "FORMATTEDTABLEDATACELL_BEGIN");
            int formatTo = formatFrom < 0 ? -1 : other.FindIndex(formatFrom, g => g.Key == 309 && g.Value.Trim() == "FORMATTEDTABLEDATACELL_END");

            if (formatTo < 0)
            {
                return CellGroups("0.0");
            }

            int contentAt = other.FindIndex(0, formatFrom, g => g.Key == 1 && g.Value.Trim() == "CELLCONTENT_BEGIN");
            List<KeyValuePair<int, string>> cell = CellGroups("0.0");
            int ownFormat = cell.FindIndex(g => g.Key == 1 && g.Value.Trim() == "FORMATTEDTABLEDATACELL_BEGIN");
            int ownFormatTo = cell.FindIndex(g => g.Key == 309 && g.Value.Trim() == "FORMATTEDTABLEDATACELL_END");
            cell.RemoveRange(ownFormat, ownFormatTo + 1 - ownFormat);
            cell.InsertRange(ownFormat, other.GetRange(formatFrom, formatTo + 1 - formatFrom));

            if (!content || contentAt < 0)
            {
                return cell;
            }

            // The kind of the other's value, where its content is one (90 1, 300 VALUE, 93, 90 its kind).
            string kind = "4";

            if (contentAt + 5 < formatFrom && other[contentAt + 1].Value.Trim() == "1" && other[contentAt + 2].Value.Trim() == "VALUE" && other[contentAt + 4].Key == 90)
            {
                kind = InheritedKind(other[contentAt + 4].Value.Trim());
            }

            int lookFrom = other.FindIndex(contentAt, formatFrom - contentAt, g => g.Key == 1 && g.Value.Trim() == "FORMATTEDCELLCONTENT_BEGIN");
            int lookTo = lookFrom < 0 ? -1 : other.FindIndex(lookFrom, formatFrom - lookFrom, g => g.Key == 309 && g.Value.Trim() == "FORMATTEDCELLCONTENT_END");
            var made = new List<KeyValuePair<int, string>>
            {
                G(95, 1), new KeyValuePair<int, string>(302, "CONTENT"), new KeyValuePair<int, string>(1, "CELLCONTENT_BEGIN"), G(90, 1),
                new KeyValuePair<int, string>(300, "VALUE"), G(93, 5), new KeyValuePair<int, string>(90, kind), G(94, 0),
                new KeyValuePair<int, string>(300, string.Empty), new KeyValuePair<int, string>(302, string.Empty),
                new KeyValuePair<int, string>(304, "ACVALUE_END"), G(91, 0), new KeyValuePair<int, string>(309, "CELLCONTENT_END"),
            };

            if (lookTo >= 0)
            {
                made.AddRange(other.GetRange(lookFrom, lookTo + 1 - lookFrom));
            }

            int none = cell.FindIndex(g => g.Key == 95);
            cell.RemoveAt(none);
            cell.InsertRange(none, made);
            return cell;
        }

        /// <summary>The end of a row of a TABLECONTENT as AutoCAD 2021 writes it, after its cells: no custom data, its format of nought, its style and height.</summary>
        private static List<KeyValuePair<int, string>> RowTail(string style, string height) => new List<KeyValuePair<int, string>>
        {
            G(91, 0), new KeyValuePair<int, string>(301, "CUSTOMDATA"), new KeyValuePair<int, string>(1, "DATAMAP_BEGIN"), G(90, 0),
            new KeyValuePair<int, string>(309, "DATAMAP_END"), new KeyValuePair<int, string>(309, "LINKEDTABLEDATAROW_END"),
            new KeyValuePair<int, string>(1, "FORMATTEDTABLEDATAROW_BEGIN"), new KeyValuePair<int, string>(300, "ROWTABLEFORMAT"),
            new KeyValuePair<int, string>(1, "TABLEFORMAT_BEGIN"), G(90, 2), G(170, 0), new KeyValuePair<int, string>(309, "TABLEFORMAT_END"),
            new KeyValuePair<int, string>(309, "FORMATTEDTABLEDATAROW_END"), new KeyValuePair<int, string>(1, "TABLEROW_BEGIN"),
            new KeyValuePair<int, string>(90, style), new KeyValuePair<int, string>(40, height), new KeyValuePair<int, string>(309, "TABLEROW_END"),
        };

        /// <summary>A row of a TABLECONTENT of empty cells as AutoCAD 2021 writes the rows it puts in.</summary>
        private static ContentRow NewRow(int columns, string style, string height)
        {
            var cells = new List<ContentGridCell>();

            for (int c = 0; c < columns; c++)
            {
                cells.Add(new ContentGridCell(CellGroups("0.0")));
            }

            return new ContentRow(new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(301, "ROW"), new KeyValuePair<int, string>(1, "LINKEDTABLEDATAROW_BEGIN") }, null, cells, RowTail(style, height));
        }

        #endregion

        #region The TABLECONTENT read as rows of cells

        /// <summary>A cell of the TABLECONTENT, its groups from its 300 CELL to its TABLECELL_END, and whether it was taken out.</summary>
        private sealed class ContentGridCell
        {
            internal ContentGridCell(List<KeyValuePair<int, string>> groups) => Groups = groups;

            internal List<KeyValuePair<int, string>> Groups { get; }

            internal bool Gone { get; set; }
        }

        /// <summary>A row of the TABLECONTENT: its 301 and its LINKEDTABLEDATAROW_BEGIN, its count of cells as read, its cells, and the rest to its TABLEROW_END.</summary>
        private sealed class ContentRow
        {
            internal ContentRow(List<KeyValuePair<int, string>> head, KeyValuePair<int, string>? count, List<ContentGridCell> cells, List<KeyValuePair<int, string>> tail)
            {
                Head = head;
                ReadCount = count;
                Cells = cells;
                Tail = tail;
            }

            internal List<KeyValuePair<int, string>> Head { get; }

            internal KeyValuePair<int, string>? ReadCount { get; }

            internal List<ContentGridCell> Cells { get; }

            internal List<KeyValuePair<int, string>> Tail { get; }

            internal void AddTo(List<KeyValuePair<int, string>> groups)
            {
                groups.AddRange(Head);
                groups.Add(ContentGrid.Counted(ReadCount, 90, Cells.Count));
                Cells.ForEach(c => groups.AddRange(c.Groups));
                groups.AddRange(Tail);
            }
        }

        /// <summary>
        /// A TABLECONTENT read as AutoCAD writes it, from the count of its columns (the 90 after AcDbLinkedTableData) on: its
        /// columns, each from its 300 COLUMN to its TABLECOLUMN_END; the count of its rows (91); its rows; what follows them, in
        /// which the merged ranges after the TABLEFORMAT_END of AcDbFormattedTableData, a count (90) and each range's top row,
        /// left column, bottom row and right column (91 to 94), are read where they stand so before AcDbTableContent.
        /// </summary>
        private sealed class ContentGrid
        {
            private KeyValuePair<int, string>? _columnCount;
            private KeyValuePair<int, string>? _rowCount;
            private KeyValuePair<int, string>? _mergeCount;
            private List<KeyValuePair<int, string>> _tail;
            private int _mergesFrom;
            private int _mergesTo;

            internal int From { get; private set; }

            internal List<List<KeyValuePair<int, string>>> Columns { get; } = new List<List<KeyValuePair<int, string>>>();

            internal List<ContentRow> Rows { get; } = new List<ContentRow>();

            /// <summary>The merged ranges, each its top row, left column, bottom row and right column; null where they could not be read.</summary>
            internal List<int[]> Merges { get; set; }

            internal bool Changed { get; set; }

            /// <summary>The fields the cells taken out held, by their handles, as they were taken out.</summary>
            internal List<string> Dropped { get; } = new List<string>();

            /// <summary>
            /// Takes fields out of the list after the rows, a count (92) and a 360 for each, the count made to agree; whether
            /// anything was taken out. Nothing where what follows the rows starts with no such list.
            /// </summary>
            internal bool DropFields(List<string> fields)
            {
                if (fields.Count == 0 || _tail.Count == 0 || _tail[0].Key != 92)
                {
                    return false;
                }

                int end = 1;

                while (end < _tail.Count && _tail[end].Key == 360)
                {
                    end++;
                }

                var kept = _tail.GetRange(1, end - 1).FindAll(g => !fields.Exists(f => string.Equals(f, g.Value.Trim(), StringComparison.OrdinalIgnoreCase)));

                if (kept.Count == end - 1)
                {
                    return false;
                }

                kept.Insert(0, new KeyValuePair<int, string>(92, kept.Count.ToString(CultureInfo.InvariantCulture)));
                _tail.RemoveRange(0, end);
                _tail.InsertRange(0, kept);
                int shift = end - kept.Count;
                _mergesFrom -= Merges == null ? 0 : shift;
                _mergesTo -= Merges == null ? 0 : shift;
                return true;
            }

            /// <summary>The TABLECONTENT read; null where it is not in the form AutoCAD writes.</summary>
            internal static ContentGrid Read(DxfFile file, DxfItem content)
            {
                List<DxfTypedValue> all = file.TailOf(content, 0);
                int from = all.FindIndex(x => x.TypeCode == 100 && x.Value.Trim() == "AcDbLinkedTableData") + 1;

                if (from <= 0 || from >= all.Count || all[from].TypeCode != 90)
                {
                    return null;
                }

                var g = new List<KeyValuePair<int, string>>(all.Count - from);

                for (int k = from; k < all.Count; k++)
                {
                    g.Add(new KeyValuePair<int, string>(all[k].TypeCode, all[k].Value));
                }
                var grid = new ContentGrid { From = from, _columnCount = g[0] };
                int i = 1;

                for (int k = Number(g[0].Value); grid.Columns.Count < k; )
                {
                    int end = Block(g, i, 300, "COLUMN", "TABLECOLUMN_END");

                    if (end < 0)
                    {
                        return null;
                    }

                    grid.Columns.Add(g.GetRange(i, end - i));
                    i = end;
                }

                if (i >= g.Count || g[i].Key != 91)
                {
                    return null;
                }

                grid._rowCount = g[i];
                int rows = Number(g[i].Value);
                i++;

                for (int r = 0; r < rows; r++)
                {
                    if (i + 2 >= g.Count || !Is(g[i], 301, "ROW") || !Is(g[i + 1], 1, "LINKEDTABLEDATAROW_BEGIN") || g[i + 2].Key != 90)
                    {
                        return null;
                    }

                    var head = g.GetRange(i, 2);
                    KeyValuePair<int, string> count = g[i + 2];
                    var cells = new List<ContentGridCell>();
                    i += 3;

                    for (int k = Number(count.Value); cells.Count < k; )
                    {
                        int end = Block(g, i, 300, "CELL", "TABLECELL_END");

                        if (end < 0)
                        {
                            return null;
                        }

                        cells.Add(new ContentGridCell(g.GetRange(i, end - i)));
                        i = end;
                    }

                    int last = i;

                    while (last < g.Count && !Is(g[last], 309, "TABLEROW_END"))
                    {
                        last++;
                    }

                    if (last >= g.Count)
                    {
                        return null;
                    }

                    grid.Rows.Add(new ContentRow(head, count, cells, g.GetRange(i, last + 1 - i)));
                    i = last + 1;
                }

                grid._tail = g.GetRange(i, g.Count - i);
                grid.ReadMerges();
                return grid;
            }

            /// <summary>Where a block of a TABLECONTENT that begins at a place with the group given ends, after its 309 of the end given; -1 where it is not so.</summary>
            private static int Block(List<KeyValuePair<int, string>> g, int at, int code, string begin, string end)
            {
                if (at >= g.Count || !Is(g[at], code, begin))
                {
                    return -1;
                }

                for (int i = at + 1; i < g.Count; i++)
                {
                    if (Is(g[i], 309, end))
                    {
                        return i + 1;
                    }
                }

                return -1;
            }

            private static bool Is(KeyValuePair<int, string> group, int code, string value) => group.Key == code && group.Value.Trim() == value;

            /// <summary>A count as written, nought or more; -1 where it is none.</summary>
            private static int Number(string text) => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0 ? n : -1;

            /// <summary>Reads the merged ranges of what follows the rows, where they stand as AutoCAD writes them.</summary>
            private void ReadMerges()
            {
                int table = _tail.FindLastIndex(x => Is(x, 100, "AcDbTableContent"));
                int formatted = _tail.FindIndex(x => Is(x, 100, "AcDbFormattedTableData"));
                int end = table < 0 ? -1 : _tail.FindLastIndex(table, x => Is(x, 309, "TABLEFORMAT_END"));

                if (formatted < 0 || end < formatted || end + 1 >= table || _tail[end + 1].Key != 90)
                {
                    return;
                }

                int count = Number(_tail[end + 1].Value);

                if (count < 0 || end + 2 + 4L * count != table)
                {
                    return;
                }

                var merges = new List<int[]>();

                for (int k = 0; k < count; k++)
                {
                    var m = new int[4];

                    for (int j = 0; j < 4; j++)
                    {
                        KeyValuePair<int, string> group = _tail[end + 2 + 4 * k + j];

                        if (group.Key != 91 + j || !int.TryParse(group.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out m[j]))
                        {
                            return;
                        }
                    }

                    merges.Add(m);
                }

                _mergeCount = _tail[end + 1];
                _mergesFrom = end + 1;
                _mergesTo = table;
                Merges = merges;
            }

            /// <summary>Whether a cell of the TABLECONTENT names the DATALINK of a handle.</summary>
            internal bool Names(string handle)
            {
                foreach (ContentRow row in Rows)
                {
                    foreach (ContentGridCell cell in row.Cells)
                    {
                        if (string.Equals(LinkHandleOf(cell.Groups), handle, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>The groups of the TABLECONTENT as it stands, from the count of its columns on; each count as it was read where it is the same.</summary>
            internal List<KeyValuePair<int, string>> Groups()
            {
                var groups = new List<KeyValuePair<int, string>> { Counted(_columnCount, 90, Columns.Count) };
                Columns.ForEach(c => groups.AddRange(c));
                groups.Add(Counted(_rowCount, 91, Rows.Count));
                Rows.ForEach(r => r.AddTo(groups));

                if (Merges == null)
                {
                    groups.AddRange(_tail);
                    return groups;
                }

                groups.AddRange(_tail.GetRange(0, _mergesFrom));
                groups.Add(Counted(_mergeCount, 90, Merges.Count));
                AddRanges(groups, Merges);
                groups.AddRange(_tail.GetRange(_mergesTo, _tail.Count - _mergesTo));
                return groups;
            }

            /// <summary>Adds merged ranges after their count: the count (90), then each range's 91 to 94.</summary>
            internal static void AddMerges(List<KeyValuePair<int, string>> groups, List<int[]> merges)
            {
                groups.Add(Counted(null, 90, merges.Count));
                AddRanges(groups, merges);
            }

            private static void AddRanges(List<KeyValuePair<int, string>> groups, List<int[]> merges)
            {
                foreach (int[] m in merges)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        groups.Add(new KeyValuePair<int, string>(91 + j, m[j].ToString(CultureInfo.InvariantCulture)));
                    }
                }
            }

            /// <summary>A count, the group as it was read where it gives the same number, else written anew.</summary>
            internal static KeyValuePair<int, string> Counted(KeyValuePair<int, string>? read, int code, int count)
                => read.HasValue && int.TryParse(read.Value.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int was) && was == count
                    ? read.Value
                    : new KeyValuePair<int, string>(code, count.ToString(CultureInfo.InvariantCulture));
        }

        #endregion
    }
}
