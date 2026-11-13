using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The ACAD_TABLE and the TABLESTYLE of R2010 and later, the mirror of the reader's: DXF keeps a table in the groups of
    /// R2007, its cells one by one, and the file keeps its content (<see cref="DwgTableContent"/>) with rows, columns,
    /// cells and merged ranges; a table style keeps its rows as the cell styles _TITLE, _HEADER and _DATA beside Table.
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        /// <summary>
        /// Whether a record is one AutoCAD writes in DXF alone, not in a DWG of R2010 and later, where the table style holds
        /// its cell styles: a CELLSTYLEMAP, and the extension dictionary of a table style that holds it under
        /// <c>ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP</c>.
        /// </summary>
        public static bool IsOfDxfAlone(DwgRecord record)
        {
            // The DATATABLE of a table of AutoCAD 2000 or 2004 is a TABLECONTENT from R2007.
            if (record.Type == "CELLSTYLEMAP" || record.Type == "DATATABLE" || record.Type == "ACDBDATATABLE")
            {
                return true;
            }

            if (record.Type != "DICTIONARY")
            {
                return false;
            }

            List<string> keys = record.Of("AcDbDictionary").Each(3);
            return keys.Count > 0
                && keys.TrueForAll(key => key.Trim() == "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP");
        }

        /// <summary>
        /// An ACAD_TABLE from R2010: its block reference, a version and two values of no known use, its content made of the
        /// groups of R2007, its direction, no break data and one range of rows.
        /// </summary>
        private void WriteTable()
        {
            WriteInsert(false);
            var table = new Cursor(_record.Of("AcDbTable"));
            int version = table.Int(280);
            ulong style = table.Handle(342);
            table.Handle(343);
            double[] direction = table.At(11) ? table.Point(11) : new[] { 1.0, 0.0, 0.0 };
            table.Int(90);
            DwgTableContent content = TableContentOf(table, style);
            _data.RawChar(version);
            _handles.Handle(4, 0);
            _data.BitLong(0);

            if (R2013Plus)
            {
                _data.BitLong(0);
            }
            else
            {
                _data.Bit(true);
            }

            WriteTableContent(content);
            _data.BitShort(6);
            _data.Point3(direction);
            _data.BitLong(0);
            _data.BitLong(1);
            _data.Point3(new double[3]);
            _data.BitLong(0);
            _data.BitLong(Math.Max(0, content.Rows.Count - 1));
        }

        /// <summary>
        /// The content of a table from its groups of R2007, at the row count (91): its sizes, then each cell, its merging
        /// (173, 175, 176) giving the merged ranges and its CELL_VALUE its content; rows by the cell style of their place,
        /// the first the title and the second the header unless the table style suppresses them.
        /// </summary>
        private DwgTableContent TableContentOf(Cursor table, ulong style)
        {
            int rows = table.Int(91);
            int columns = table.Int(92);
            var content = new DwgTableContent { TableStyle = style };
            var flags = new int[4];

            for (int code = 93; code <= 96; code++)
            {
                flags[code - 93] = table.Int(code);
            }

            // The table's own overrides, which AutoCAD 2021 writes in the head before the heights.
            var own = new List<KeyValuePair<int, string>>();

            while (table.Code >= 0 && table.Code != 141 && table.Code != 142 && table.Code != 171)
            {
                int code = table.Code;
                own.Add(new KeyValuePair<int, string>(code, table.Text(code)));
            }

            DwgGroups styleGroups = (_drawing.Find(style) ?? _record).Of("AcDbTableStyle");
            List<string> suppressed = styleGroups.Each(280);
            bool noTitle = suppressed.Count > 0 && DwgRecord.Int(suppressed[suppressed.Count - 1]) != 0;
            bool noHeader = styleGroups.Int(281) != 0;
            var ids = new List<int>();

            if (!noTitle)
            {
                ids.Add(1);
            }

            if (!noHeader)
            {
                ids.Add(2);
            }

            for (int r = 0; r < rows; r++)
            {
                content.Rows.Add(
                    new DwgTableLine
                    {
                        Size = table.Double(141),
                        StyleId = r < ids.Count ? ids[r] : 3,
                        Style = new DwgCellStyle { Type = 2 },
                    }
                );
            }

            for (int c = 0; c < columns; c++)
            {
                content.Columns.Add(
                    new DwgTableLine
                    {
                        Size = table.Double(142),
                        Style = new DwgCellStyle { Type = 3 },
                    }
                );
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    content.Rows[r].Cells.Add(TableCellOf(table, content, r, c));
                }
            }

            DwgRecord roundTrip = RoundTripOf(_record);
            DwgTableContent kept = roundTrip == null ? null : ContentOfDxf(roundTrip);

            if (kept != null && kept.Rows.Count == rows && kept.Columns.Count == columns)
            {
                TakeStyles(content, kept, OwnStyleIdsOf(style));
            }
            else
            {
                TableOverrides(content, flags, own);
            }

            // The table owns the fields of its cells: those its round trip record lists, else those the cells name.
            if (content.Fields.Count == 0)
            {
                foreach (DwgTableLine row in content.Rows)
                {
                    foreach (DwgTableCell cell in row.Cells)
                    {
                        content.Fields.AddRange(
                            cell.Contents.FindAll(one => one.Type == 2).ConvertAll(one => one.Handle)
                        );
                    }
                }
            }

            return content;
        }

        /// <summary>
        /// What the groups of R2007 cannot hold, from the TABLECONTENT AutoCAD keeps for the table's round trip, as AutoCAD
        /// reads it: the cell style and overrides of the whole table, of each row and column, and of each cell that gives
        /// none of its own in its groups, and the cell style each takes; one of the table style's own (101 on) that the
        /// style does not hold (<paramref name="held"/>) is left for a row as it stands by its place, for a column or a cell
        /// as none.
        /// </summary>
        private static void TakeStyles(DwgTableContent content, DwgTableContent kept, HashSet<int> held)
        {
            content.Style = kept.Style;

            for (int c = 0; c < content.Columns.Count; c++)
            {
                content.Columns[c].Style = kept.Columns[c].Style;
                content.Columns[c].StyleId = Held(kept.Columns[c].StyleId, held) ? kept.Columns[c].StyleId : 0;
            }

            for (int r = 0; r < content.Rows.Count; r++)
            {
                DwgTableLine row = content.Rows[r];
                row.Style = kept.Rows[r].Style;
                row.StyleId = Held(kept.Rows[r].StyleId, held) ? kept.Rows[r].StyleId : row.StyleId;

                for (int c = 0; c < row.Cells.Count && c < kept.Rows[r].Cells.Count; c++)
                {
                    DwgTableCell cell = row.Cells[c];
                    DwgTableCell was = kept.Rows[r].Cells[c];
                    cell.StyleId = Held(was.StyleId, held) ? was.StyleId : 0;

                    if (!cell.Style.HasData)
                    {
                        cell.Style = was.Style;
                    }

                    // The link to data is not in the groups of R2007; a field's looks are the content format it kept.
                    cell.Linked = was.Linked;
                    cell.LinkHandle = was.LinkHandle;
                    cell.LinkRows = was.LinkRows;
                    cell.LinkColumns = was.LinkColumns;
                    cell.LinkExtra = was.LinkExtra;

                    if (
                        cell.Contents.Count == was.Contents.Count
                        && cell.Contents.Count > 0
                        && cell.Contents[0].Type == 2
                        && was.Contents[0].Type == 2
                        && was.Contents[0].Format != null
                    )
                    {
                        cell.Contents[0].Format = was.Contents[0].Format;
                    }
                }
            }

            content.Fields.AddRange(kept.Fields);
        }

        /// <summary>
        /// The table's own overrides of R2007 (93 to 96 and their groups) as AutoCAD 2021 keeps them from R2010: the margins
        /// in the table's own cell style, the looks and lines of a kind of row in the cell style of each row of that kind.
        /// The groups are read as AutoCAD writes them, each group of a code taking the next flag of that code: those of 93
        /// first (64 the colours of the rows, 63 their fills), then the lines' colours (94), lineweights (95) and showing
        /// (96, 1 hidden), each line of a kind of row in the order top, inside horizontal, bottom, left, inside vertical,
        /// right.
        /// </summary>
        private void TableOverrides(DwgTableContent content, int[] flags, List<KeyValuePair<int, string>> own)
        {
            int[] looks = { 64, 283, 63, 170, 7, 140 };
            int[][] lines =
            {
                new[] { 64, 65, 66, 63, 68, 69 },
                new[] { 274, 275, 276, 277, 278, 279 },
                new[] { 284, 285, 286, 287, 288, 289 },
            };
            var seen = new Dictionary<int, int>();
            var kinds = new DwgCellStyle[3];

            for (int i = 0; i < 3; i++)
            {
                kinds[i] = new DwgCellStyle { Type = 2 };
            }

            foreach (KeyValuePair<int, string> group in own)
            {
                // The slots of the code: the single ones, those of 93, then those of the lines.
                var slots = new List<int>();
                int single = Array.IndexOf(new[] { 280, 281, 70, 40, 41 }, group.Key);

                if (single >= 0)
                {
                    slots.Add(single);
                }

                int look = Array.IndexOf(looks, group.Key);

                for (int bit = 5 + 3 * look; look >= 0 && bit < 8 + 3 * look; bit++)
                {
                    if ((flags[0] & (1 << bit)) != 0)
                    {
                        slots.Add(bit);
                    }
                }

                for (int kind = 0; kind < 3; kind++)
                {
                    int line = Array.IndexOf(lines[kind], group.Key);

                    for (int rowKind = 0; line >= 0 && rowKind < 3; rowKind++)
                    {
                        if ((flags[kind + 1] & (1 << (rowKind * 6 + line))) != 0)
                        {
                            slots.Add((kind + 1) * 100 + rowKind * 6 + line);
                        }
                    }
                }

                seen.TryGetValue(group.Key, out int n);
                seen[group.Key] = n + 1;

                if (n < slots.Count)
                {
                    TableOverride(content, kinds, slots[n], group.Value);
                }
            }

            foreach (DwgTableLine row in content.Rows)
            {
                if (row.StyleId >= 1 && row.StyleId <= 3 && kinds[row.StyleId - 1].HasData)
                {
                    row.Style = kinds[row.StyleId - 1];
                    row.Style.Borders.Sort((a, b) => a.Edge.CompareTo(b.Edge));
                }
            }
        }

        /// <summary>One override of the table's own, of its slot: 3 and 4 the margins, 5 to 22 the looks of 93, 100 on the lines.</summary>
        private void TableOverride(DwgTableContent content, DwgCellStyle[] kinds, int slot, string text)
        {
            if (slot == 3 || slot == 4)
            {
                DwgCellStyle table = content.Style;

                if (!table.HasData)
                {
                    table.HasData = true;
                    table.Format = new DwgContentFormat
                    {
                        DataType = 512,
                        Alignment = 1,
                        Height = 0.18,
                    };
                    table.Margins = new[] { 0.06, 0.06, 0.06, 0.06, 0.18, 0.18 };
                }

                double margin = DwgRecord.Double(text);
                table.MarginFlags = 1;
                table.Overrides |= slot == 3 ? 0x1400 : 0x2800;
                table.Margins[slot == 3 ? 1 : 0] = margin;
                table.Margins[slot == 3 ? 3 : 2] = margin;
                return;
            }

            if (slot < 5 || (slot > 22 && slot < 100))
            {
                return;
            }

            if (slot < 100)
            {
                DwgCellStyle looks = KindOf(kinds[(slot - 5) % 3]);
                int value = DwgRecord.Int(text);

                switch ((slot - 5) / 3)
                {
                    case 0:
                        looks.Overrides |= 0x20;
                        looks.Format.Colour = RawOf(value);
                        break;
                    case 1:
                        looks.Overrides |= 0x200;

                        if (value != 0)
                        {
                            looks.Background = unchecked((int)0xC8000000);
                        }

                        break;
                    case 2:
                        looks.Overrides |= 0x200;
                        looks.Background = RawOf(value);
                        break;
                    case 3:
                        looks.Overrides |= 0x10;
                        looks.Format.Alignment = value;
                        break;
                    case 4:
                        looks.Overrides |= 0x40;
                        looks.Format.TextStyle = _drawing.HandleOf("STYLE", text);
                        break;
                    default:
                        looks.Overrides |= 0x80;
                        looks.Format.Height = DwgRecord.Double(text);
                        break;
                }

                return;
            }

            int kind = slot / 100 - 1;
            int bit = slot % 100;
            DwgCellBorder border = BorderOf(KindOf(kinds[bit / 6]), 1 << (bit % 6));
            SetBorder(border, kind, text);
        }

        /// <summary>The cell style of a kind of row, given data on first use.</summary>
        private static DwgCellStyle KindOf(DwgCellStyle style)
        {
            if (!style.HasData)
            {
                style.HasData = true;
                style.Format = new DwgContentFormat
                {
                    DataType = 512,
                    Alignment = 1,
                    Height = 0.18,
                };
            }

            return style;
        }

        /// <summary>The border of an edge of a cell style, put in where it has none.</summary>
        private static DwgCellBorder BorderOf(DwgCellStyle style, int edge)
        {
            DwgCellBorder border = style.Borders.Find(b => b.Edge == edge);

            if (border == null)
            {
                border = new DwgCellBorder { Edge = edge, Colour = unchecked((int)0xC1000000) };
                style.Borders.Add(border);
            }

            return border;
        }

        /// <summary>One property of a border from a group of R2007: its colour (0), lineweight (1) or whether hidden (2, 1 hidden).</summary>
        private static void SetBorder(DwgCellBorder border, int kind, string text)
        {
            int value = DwgRecord.Int(text);

            switch (kind)
            {
                case 0:
                    border.Overrides |= 8;
                    border.Colour = RawOf(value);
                    break;
                case 1:
                    border.Overrides |= 2;
                    border.LineWeight = value;
                    break;
                default:
                    border.Overrides |= 0x10;
                    border.Invisible = value != 0 ? 1 : 0;
                    break;
            }
        }

        /// <summary>One cell from its groups of R2007, 171 to <c>304 ACVALUE_END</c>, its merging added to the ranges.</summary>
        private DwgTableCell TableCellOf(Cursor table, DwgTableContent content, int r, int c)
        {
            var cell = new DwgTableCell();
            int type = table.Int(171, 1);
            table.Int(172);
            int covered = table.Int(173);
            table.Int(174);
            int across = table.Int(175, 1);
            int down = table.Int(176, 1);

            if (covered == 0 && (across > 1 || down > 1))
            {
                content.Merged.Add(new[] { r, c, r + Math.Max(1, down) - 1, c + Math.Max(1, across) - 1 });
            }

            int flags = table.Int(91);
            table.SkipTo(145, 171, 301);
            double rotation = table.Double(145);
            ulong block = table.Handle(340);
            double scale = table.Double(144, 1.0);
            ulong field = table.Handle(344);
            var looks = new DwgContentFormat();
            int lookFlags = OwnOverrides(table, cell, looks);

            // A table of a DXF before AutoCAD 2007 gives a cell's words in group 1, with no CELL_VALUE.
            table.SkipTo(1, 171, 301);
            string words = table.At(1) ? table.Text(1) : null;
            table.SkipTo(301, 171);
            var value = new DwgCellValue();

            if (!table.At(301) && words != null)
            {
                value = new DwgCellValue
                {
                    Flags = 0,
                    Type = 4,
                    Text = words,
                    Shown = words,
                };
            }
            else if (table.At(301))
            {
                table.Text(301);
                value.Flags = table.Int(93);
                value.Type = table.Int(90);

                if ((value.Flags & 1) == 0)
                {
                    switch (value.Type)
                    {
                        case 0:
                        case 1:
                            value.Long = table.Int(91);
                            break;
                        case 2:
                            value.Double = table.Double(140);
                            break;
                        default:
                            var text = new StringBuilder();

                            while (table.At(2))
                            {
                                text.Append(table.Text(2));
                            }

                            value.Text = text.Append(table.Text(1)).ToString();
                            break;
                    }
                }

                value.Unit = table.Int(94);
                value.Format = table.Text(300);
                value.Shown = table.Text(302);
                table.SkipTo(304, 171);
                table.Text(304);
            }

            DwgContentFormat format =
                (flags & 0x40000) == 0
                    ? null
                    : new DwgContentFormat
                    {
                        Overrides = 3,
                        DataType = value.Type,
                        Rotation = rotation,
                        Scale = scale,
                        Alignment = 1,
                        Height = 0.18,
                    };

            if (type == 2 && block != 0)
            {
                cell.Contents.Add(
                    new DwgCellContent
                    {
                        Type = 4,
                        Handle = block,
                        Format = format,
                    }
                );
            }
            else if (field != 0)
            {
                cell.Contents.Add(
                    new DwgCellContent
                    {
                        Type = 2,
                        Handle = field,
                        Format = format,
                    }
                );
            }
            else if ((value.Flags & 1) == 0)
            {
                cell.Contents.Add(
                    new DwgCellContent
                    {
                        Type = 1,
                        Value = value,
                        Format = format,
                    }
                );
            }

            // The colour, text style and height of a cell's own are its content's where it has one, as AutoCAD keeps them;
            // else its cell style's.
            if (lookFlags != 0)
            {
                DwgContentFormat into;
                DwgCellContent first = cell.Contents.Count > 0 ? cell.Contents[0] : null;

                if (first != null)
                {
                    first.Format = first.Format
                        ?? new DwgContentFormat
                        {
                            DataType = value.Type,
                            Rotation = rotation,
                            Scale = scale,
                            Alignment = 1,
                            Height = 0.18,
                        };
                    first.Format.Overrides |= lookFlags;
                    into = first.Format;
                }
                else
                {
                    KindOf(cell.Style).Overrides |= lookFlags;
                    into = cell.Style.Format;
                }

                into.Colour = (lookFlags & 0x20) != 0 ? looks.Colour : into.Colour;
                into.TextStyle = (lookFlags & 0x40) != 0 ? looks.TextStyle : into.TextStyle;
                into.Height = (lookFlags & 0x80) != 0 ? looks.Height : into.Height;
            }

            return cell;
        }

        /// <summary>
        /// The overrides of a cell's own after its turn (and its block), as AutoCAD 2021 writes them in the order 170, 7, 63,
        /// 64, 283, 140 and its edges right, bottom, left, top, each a lineweight, whether hidden (1 hidden) and a colour:
        /// its alignment, fill and borders into its cell style, its colour, text style and height into
        /// <paramref name="looks"/>, whose flags of R2010 it returns. An edge takes an override where its group is not the
        /// default: a colour not by block, a lineweight not -2, and whether hidden where it is given at all.
        /// </summary>
        private int OwnOverrides(Cursor table, DwgTableCell cell, DwgContentFormat looks)
        {
            int lookFlags = 0;
            int background = -1;
            int fill = -1;
            int[] codes = { 69, 65, 66, 68, 279, 275, 276, 278, 289, 285, 286, 288 };
            int[] edges = { 1, 0x20, 4, 8 };

            while (table.Code >= 0)
            {
                int code = table.Code;
                int edge = Array.IndexOf(codes, code);

                if (code == 170)
                {
                    KindOf(cell.Style).Overrides |= 0x10;
                    cell.Style.Format.Alignment = table.Int(170);
                }
                else if (code == 63)
                {
                    background = table.Int(63);
                }
                else if (code == 283)
                {
                    fill = table.Int(283);
                }
                else if (code == 64)
                {
                    lookFlags |= 0x20;
                    looks.Colour = RawOf(table.Int(64));
                }
                else if (code == 7)
                {
                    lookFlags |= 0x40;
                    looks.TextStyle = _drawing.HandleOf("STYLE", table.Text(7));
                }
                else if (code == 140)
                {
                    lookFlags |= 0x80;
                    looks.Height = table.Double(140);
                }
                else if (edge >= 0)
                {
                    string text = table.Text(code);
                    int value = DwgRecord.Int(text);
                    int kind = edge / 4;

                    if (kind == 2 || (kind == 0 && value != 0) || (kind == 1 && value != -2))
                    {
                        SetBorder(BorderOf(KindOf(cell.Style), edges[edge % 4]), kind, text);
                    }
                }
                else
                {
                    break;
                }
            }

            if (background >= 0 || fill >= 0)
            {
                KindOf(cell.Style).Overrides |= 0x200;
                cell.Style.Background = fill > 0 || background < 0 ? unchecked((int)0xC8000000) : RawOf(background);
            }

            cell.Style.Borders.Sort((a, b) => a.Edge.CompareTo(b.Edge));
            return lookFlags;
        }

        private void WriteTableContent(DwgTableContent content)
        {
            T(content.Name);
            T(content.Description);
            _data.BitLong(content.Columns.Count);

            foreach (DwgTableLine column in content.Columns)
            {
                T(column.Name);
                _data.BitLong(column.Custom);
                _data.BitLong(0);
                WriteCellStyle(column.Style);
                _data.BitLong(column.StyleId);
                _data.BitDouble(column.Size);
            }

            _data.BitLong(content.Rows.Count);

            foreach (DwgTableLine row in content.Rows)
            {
                _data.BitLong(row.Cells.Count);

                foreach (DwgTableCell cell in row.Cells)
                {
                    WriteTableCell(cell);
                }

                _data.BitLong(row.Custom);
                _data.BitLong(0);
                WriteCellStyle(row.Style);
                _data.BitLong(row.StyleId);
                _data.BitDouble(row.Size);
            }

            _data.BitLong(content.Fields.Count);

            foreach (ulong field in content.Fields)
            {
                H(3, field);
            }

            WriteCellStyle(content.Style);
            _data.BitLong(content.Merged.Count);

            foreach (int[] range in content.Merged)
            {
                foreach (int index in range)
                {
                    _data.BitLong(index);
                }
            }

            H(5, content.TableStyle);
        }

        private void WriteTableCell(DwgTableCell cell)
        {
            _data.BitLong(cell.State);
            T(cell.Tooltip);
            _data.BitLong(cell.Custom);
            WriteCustomData(cell.Items);

            if (cell.Linked)
            {
                // AutoCAD 2021 writes 3 for linked data, where the specification says 1.
                _data.BitLong(3);
                H(5, cell.LinkHandle);
                _data.BitLong(cell.LinkRows);
                _data.BitLong(cell.LinkColumns);
                _data.BitLong(cell.LinkExtra);
            }
            else
            {
                _data.BitLong(0);
            }

            _data.BitLong(cell.Contents.Count);

            foreach (DwgCellContent content in cell.Contents)
            {
                _data.BitLong(content.Type);

                if (content.Type == 1)
                {
                    WriteValue(content.Value);
                }
                else
                {
                    H(5, content.Handle);
                }

                _data.BitLong(0);
                _data.BitShort(content.Format != null ? 1 : 0);

                if (content.Format != null)
                {
                    WriteContentFormat(content.Format);
                }
            }

            WriteCellStyle(cell.Style);
            _data.BitLong(cell.StyleId);
            _data.BitLong(0);
        }

        /// <summary>
        /// A TABLECONTENT of the round-trip data of a table (its extension dictionary's ACAD_XREC_ROUNDTRIP, an XRECORD
        /// naming it): the content of that table, made of the table's own groups as its ACAD_TABLE is.
        /// </summary>
        private void WriteTableContentObject()
        {
            // Its own groups where they are in AutoCAD's order: the content as it was, cell styles and borders with it.
            DwgTableContent parsed = ContentOfDxf(_record);

            if (parsed != null)
            {
                WriteTableContent(WithoutOwnStyles(parsed, OwnStyleIdsOf(parsed.TableStyle)));
                return;
            }

            DwgRecord table = _drawing.Find(_record.Owner);

            for (int up = 0; up < 3 && table != null && table.Type != "ACAD_TABLE"; up++)
            {
                table = _drawing.Find(table.Owner);
            }

            if (table == null)
            {
                WriteTableContent(new DwgTableContent());
                return;
            }

            DwgRecord own = _record;
            _record = table;
            var groups = new Cursor(table.Of("AcDbTable"));
            groups.Int(280);
            ulong style = groups.Handle(342);
            groups.SkipTo(91);
            DwgTableContent content = TableContentOf(groups, style);
            _record = own;
            List<List<KeyValuePair<string, DwgCellValue>>> items = CellItemsOf(own);
            int at = 0;

            foreach (DwgTableLine row in content.Rows)
            {
                foreach (DwgTableCell cell in row.Cells)
                {
                    if (at < items.Count)
                    {
                        cell.Items.AddRange(items[at++]);
                    }
                }
            }

            WriteTableContent(content);
        }

        /// <summary>
        /// The custom data of each cell of a TABLECONTENT in DXF, in the order of its cells: between
        /// <c>LINKEDTABLEDATACELL_BEGIN</c> and its end, the items of its <c>DATAMAP</c>, each a name (300) and a value
        /// between <c>DATAMAP_VALUE</c> and <c>ACVALUE_END</c>.
        /// </summary>
        private static List<List<KeyValuePair<string, DwgCellValue>>> CellItemsOf(DwgRecord content)
        {
            var cells = new List<List<KeyValuePair<string, DwgCellValue>>>();
            List<KeyValuePair<int, string>> groups = content.Body;
            List<KeyValuePair<string, DwgCellValue>> cell = null;

            for (int i = 0; i < groups.Count; i++)
            {
                string value = groups[i].Value.Trim();

                if (groups[i].Key == 1 && value == "LINKEDTABLEDATACELL_BEGIN")
                {
                    cell = new List<KeyValuePair<string, DwgCellValue>>();
                    cells.Add(cell);
                }
                else if (groups[i].Key == 309 && value == "LINKEDTABLEDATACELL_END")
                {
                    cell = null;
                }
                else if (
                    cell != null
                    && groups[i].Key == 300
                    && i + 1 < groups.Count
                    && groups[i + 1].Key == 301
                    && groups[i + 1].Value.Trim() == "DATAMAP_VALUE"
                )
                {
                    string name = groups[i].Value;
                    var item = new Cursor(groups.GetRange(i + 2, groups.Count - i - 2));
                    var found = new DwgCellValue { Flags = item.Int(93), Type = item.Int(90) };

                    switch (found.Type)
                    {
                        case 0:
                        case 1:
                            found.Long = item.Int(91);
                            break;
                        case 2:
                            found.Double = item.Double(140);
                            break;
                        default:
                            found.Text = item.Text(1);
                            break;
                    }

                    found.Unit = item.Int(94);
                    found.Format = item.Text(300);
                    found.Shown = item.Text(302);
                    cell.Add(new KeyValuePair<string, DwgCellValue>(name, found));
                }
            }

            return cells;
        }

        /// <summary>A collection of custom data: its count, then for each item its name and its value.</summary>
        private void WriteCustomData(List<KeyValuePair<string, DwgCellValue>> items)
        {
            _data.BitLong(items.Count);

            foreach (KeyValuePair<string, DwgCellValue> item in items)
            {
                T(item.Key);
                WriteValue(item.Value);
            }
        }

        /// <summary>A TABLEGEOMETRY: its rows, columns and cells, each its flags, sizes and the places of its contents.</summary>
        private void WriteTableGeometry()
        {
            var geometry = new Cursor(_record.Of("AcDbTableGeometry"));
            _data.BitLong(geometry.Int(90));
            _data.BitLong(geometry.Int(91));
            int cells = geometry.Int(92);
            _data.BitLong(cells);

            for (int i = 0; i < cells; i++)
            {
                _data.BitLong(geometry.Int(93));
                _data.BitDouble(geometry.Double(40));
                _data.BitDouble(geometry.Double(41));
                H(4, geometry.Handle(330));
                int contents = geometry.Int(94);
                _data.BitLong(contents);

                for (int j = 0; j < contents; j++)
                {
                    _data.Point3(geometry.Point(10));
                    _data.Point3(geometry.Point(11));
                    _data.BitDouble(geometry.Double(43));
                    _data.BitDouble(geometry.Double(44));
                    _data.BitDouble(geometry.Double(45));
                    _data.BitDouble(geometry.Double(46));
                    _data.BitLong(geometry.Int(95));
                }
            }
        }

        /// <summary>A value (20.4.99): its flags, type and, unless flag 1 says it holds nothing, the value; its unit, format and text.</summary>
        private void WriteValue(DwgCellValue value)
        {
            _data.BitLong(value.Flags);
            _data.BitLong(value.Type);

            if ((value.Flags & 1) == 0)
            {
                switch (value.Type)
                {
                    case 0:
                    case 1:
                        _data.BitLong(value.Long);
                        break;
                    case 2:
                        _data.BitDouble(value.Double);
                        break;
                    default:
                        // A string as UTF-16 with its closing zero, counted in bytes.
                        byte[] utf16 = Encoding.Unicode.GetBytes(value.Text + "\0");
                        _data.BitLong(utf16.Length);
                        _data.Bytes(utf16);
                        break;
                }
            }

            _data.BitLong(value.Unit);
            T(value.Format);
            T(value.Shown);
        }

        private void WriteContentFormat(DwgContentFormat format)
        {
            _data.BitLong(format.Overrides);
            _data.BitLong(format.Flags);
            _data.BitLong(format.DataType);
            _data.BitLong(format.UnitType);
            T(format.FormatString);
            _data.BitDouble(format.Rotation);
            _data.BitDouble(format.Scale);
            _data.BitLong(format.Alignment);
            WriteRawColour(format.Colour);
            H(5, format.TextStyle);
            _data.BitDouble(format.Height);
        }

        private void WriteCellStyle(DwgCellStyle style)
        {
            _data.BitLong(style.Type);
            _data.BitShort(style.HasData ? 1 : 0);

            if (!style.HasData)
            {
                return;
            }

            _data.BitLong(style.Overrides);
            _data.BitLong(style.Merge);
            WriteRawColour(style.Background);
            _data.BitLong(style.Layout);
            WriteContentFormat(style.Format);
            _data.BitShort(style.MarginFlags);

            if ((style.MarginFlags & 1) != 0)
            {
                foreach (double margin in style.Margins)
                {
                    _data.BitDouble(margin);
                }
            }

            _data.BitLong(style.Borders.Count);

            foreach (DwgCellBorder border in style.Borders)
            {
                _data.BitLong(border.Edge);

                if (border.Edge == 0)
                {
                    continue;
                }

                _data.BitLong(border.Overrides);
                _data.BitLong(border.Type);
                WriteRawColour(border.Colour);
                _data.BitLong(border.LineWeight);
                H(5, border.Linetype);
                _data.BitLong(border.Invisible);
                _data.BitDouble(border.Spacing);
            }
        }

        /// <summary>
        /// A TABLESTYLE from R2010, made of its groups of R2007: its direction, flags and margins in the cell style Table,
        /// its data, title and header rows as the cell styles _DATA, _TITLE and _HEADER, a border written where it is not
        /// the default (by block, the default lineweight, shown); then the cell styles of its own (101 on) its CELLSTYLEMAP
        /// holds, as the map gives them.
        /// </summary>
        private void WriteTableStyle()
        {
            var style = new Cursor(_record.Of("AcDbTableStyle"));
            int version = style.Int(280);
            string description = style.Text(3);
            int flow = style.Int(70);
            int flags = style.Int(71);
            double horizontal = style.Double(40, 0.06);
            double vertical = style.Double(41, 0.06);
            style.Int(280);
            style.Int(281);
            bool margins = horizontal != 0.06 || vertical != 0.06;
            var rows = new DwgCellStyle[3];

            for (int i = 0; i < 3; i++)
            {
                rows[i] = RowStyleOf(style);
            }

            var table = new DwgCellStyle
            {
                Type = 5,
                HasData = true,
                Merge = flow != 0 ? 0x10000 : 0,
                Format = new DwgContentFormat
                {
                    DataType = 512,
                    Alignment = 1,
                    Height = 0.18,
                },
            };

            // AutoCAD 2021 writes 101 here also where the style holds 101 and 102.
            _data.RawChar(version);
            T(description);
            _data.BitLong(flags);
            _data.BitLong(101);
            _handles.Handle(3, 0);

            foreach (DwgCellStyle one in new[] { table, rows[0], rows[1], rows[2] })
            {
                one.MarginFlags = margins ? 1 : 0;
                one.Margins = new[] { vertical, horizontal, vertical, horizontal, 0.18, 0.18 };
            }

            WriteCellStyle(table);
            _data.BitLong(4);
            _data.BitLong(2);
            T("Table");

            // The rows of DXF are data, title and header; the file holds title (1), header (2) and data (3), always from
            // the groups, which an edit changes; then the cell styles of its own (101 on) its CELLSTYLEMAP holds.
            DwgCellStyle[] ordered = { rows[1], rows[2], rows[0] };
            string[] names = { "_TITLE", "_HEADER", "_DATA" };
            ordered[0].Merge = 0x8000;
            var named = new List<NamedCellStyle>();

            for (int i = 0; i < 3; i++)
            {
                named.Add(
                    new NamedCellStyle
                    {
                        Id = i + 1,
                        Kind = i == 2 ? 2 : 1,
                        Name = names[i],
                        Style = ordered[i],
                    }
                );
            }

            named.AddRange(CellStylesOf(_record).FindAll(one => one.Id >= 101));

            // Each cell style after its own id, as AutoCAD writes it: the ids 4 and 5 there crashed AutoCAD 2021.
            _data.BitLong(named.Count);

            foreach (NamedCellStyle one in named)
            {
                _data.BitLong(one.Id);
                WriteCellStyle(one.Style);
                _data.BitLong(one.Id);
                _data.BitLong(one.Kind);
                T(one.Name);
            }
        }

        /// <summary>A cell style of a table style: its id (1 to 3 its rows, 101 on its own), kind, name and looks.</summary>
        private sealed class NamedCellStyle
        {
            public int Id;
            public int Kind;
            public string Name;
            public DwgCellStyle Style;
        }

        /// <summary>
        /// The cell styles of the CELLSTYLEMAP a table style holds in DXF under its extension dictionary
        /// (<c>ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP</c>), in its order; none where it has none or its groups are
        /// not of AutoCAD's order.
        /// </summary>
        private List<NamedCellStyle> CellStylesOf(DwgRecord style)
        {
            var named = new List<NamedCellStyle>();
            DwgRecord map = EntryOf(_drawing.Find(style.XDictionary), 3, "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP");

            if (map == null || map.Type != "CELLSTYLEMAP")
            {
                return named;
            }

            try
            {
                var g = new Cursor(map.Of("AcDbCellStyleMap"));
                int count = g.Int(90);

                for (int i = 0; i < count; i++)
                {
                    Need(g, 300);
                    DwgCellStyle looks = TableFormatOf(g);
                    Need(g, 1);
                    named.Add(
                        new NamedCellStyle
                        {
                            Style = looks,
                            Id = g.Int(90),
                            Kind = g.Int(91),
                            Name = g.Text(300),
                        }
                    );
                    Need(g, 309);
                }
            }
            catch (FormatException)
            {
                named.Clear();
            }

            return named;
        }

        /// <summary>A row of a table style, its groups 7 to the last border, as a cell style.</summary>
        private DwgCellStyle RowStyleOf(Cursor style)
        {
            // A row that names no text style (a style made in code without one) is one AUDIT cannot repair: it has Standard.
            ulong textStyle = _drawing.HandleOf("STYLE", style.At(7) ? style.Text(7) : "Standard");
            var format = new DwgContentFormat
            {
                Height = style.Double(140),
                Alignment = style.Int(170),
                Colour = RawOf(style.Int(62)),
                TextStyle = textStyle,
            };
            int fill = style.Int(63, 7);
            bool filled = style.Int(283) != 0;
            format.DataType = style.Int(90, 512);
            format.UnitType = style.Int(91);
            format.FormatString = style.Text(1);
            var row = new DwgCellStyle
            {
                Type = 5,
                HasData = true,
                Background = filled ? RawOf(fill) : unchecked((int)0xC8000000),
                Format = format,
            };

            // The borders of DXF: top, inside horizontal, bottom, left, inside vertical, right, each its line of AutoCAD's
            // GridLineType; a cell style of a table style holds a border that is not the default with no flags of overrides.
            for (int i = 0; i < 6; i++)
            {
                int weight = style.Int(274 + i, -2);
                bool shown = style.Int(284 + i, 1) != 0;
                int colour = style.Int(64 + i);

                if (weight != -2 || colour != 0 || !shown)
                {
                    row.Borders.Add(
                        new DwgCellBorder
                        {
                            Edge = 1 << i,
                            Colour = RawOf(colour),
                            LineWeight = weight,
                            Invisible = shown ? 0 : 1,
                        }
                    );
                }
            }

            return row;
        }

        /// <summary>
        /// The TABLECONTENT AutoCAD keeps for a table's round trip: under its extension dictionary's
        /// <c>ACAD_XREC_ROUNDTRIP</c>, the XRECORD's <c>ACAD_ROUNDTRIP_2008_TABLE_ENTITY</c>. Null where there is none.
        /// </summary>
        private DwgRecord RoundTripOf(DwgRecord table)
        {
            DwgRecord found = EntryOf(_drawing.Find(table.XDictionary), 3, "ACAD_XREC_ROUNDTRIP");
            found = EntryOf(found, 102, "ACAD_ROUNDTRIP_2008_TABLE_ENTITY");
            return found != null && found.Type == "TABLECONTENT" ? found : null;
        }

        /// <summary>The record named by the 360 after the group of <paramref name="code"/> that holds <paramref name="name"/>.</summary>
        private DwgRecord EntryOf(DwgRecord record, int code, string name)
        {
            List<KeyValuePair<int, string>> groups = record?.Body ?? new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < groups.Count; i++)
            {
                if (groups[i].Key == code && groups[i].Value.Trim() == name && groups[i + 1].Key == 360)
                {
                    return _drawing.Find(DwgRecord.Hex(groups[i + 1].Value));
                }
            }

            return null;
        }

        /// <summary>
        /// The content of a TABLECONTENT from its DXF, the mirror of the reader's: its columns, rows, cells with their values,
        /// formats and custom data, its own cell style, merged ranges and table style. Null where it holds what no test drawing
        /// has shown (attributes of a block, a cell's geometry) or its groups are not of that order.
        /// </summary>
        private static DwgTableContent ContentOfDxf(DwgRecord record)
        {
            try
            {
                var g = new Cursor(record.Body);
                g.SkipTo(100);
                Need(g, 100);
                var content = new DwgTableContent { Name = g.Text(1), Description = g.Text(300) };
                g.SkipTo(100);
                Need(g, 100);
                int columns = g.Int(90);

                for (int i = 0; i < columns; i++)
                {
                    Need(g, 300);
                    Need(g, 1);
                    var column = new DwgTableLine { Name = g.Text(300), Custom = g.Int(91) };
                    CustomDataOf(g, column.Items);
                    Need(g, 309);
                    column.Style = FormattedOf(g);
                    Need(g, 1);
                    column.StyleId = g.Int(90);
                    column.Size = g.Double(40);
                    Need(g, 309);
                    content.Columns.Add(column);
                }

                int rows = g.Int(91);

                for (int i = 0; i < rows; i++)
                {
                    Need(g, 301);
                    Need(g, 1);
                    var row = new DwgTableLine();
                    int cells = g.Int(90);

                    for (int j = 0; j < cells; j++)
                    {
                        row.Cells.Add(CellOfDxf(g));
                    }

                    row.Custom = g.Int(91);
                    CustomDataOf(g, row.Items);
                    Need(g, 309);
                    row.Style = FormattedOf(g);
                    Need(g, 1);
                    row.StyleId = g.Int(90);
                    row.Size = g.Double(40);
                    Need(g, 309);
                    content.Rows.Add(row);
                }

                int fields = g.Int(92);

                for (int i = 0; i < fields; i++)
                {
                    content.Fields.Add(g.Handle(360));
                }

                Need(g, 100);
                Need(g, 300);
                content.Style = TableFormatOf(g);
                int merged = g.Int(90);

                for (int i = 0; i < merged; i++)
                {
                    content.Merged.Add(new[] { g.Int(91), g.Int(92), g.Int(93), g.Int(94) });
                }

                Need(g, 100);
                content.TableStyle = g.Handle(340);
                return content;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>A cell of a TABLECONTENT from its DXF: its linked part with its contents, its format and its style.</summary>
        private static DwgTableCell CellOfDxf(Cursor g)
        {
            Need(g, 300);
            Need(g, 1);
            var cell = new DwgTableCell
            {
                State = g.Int(90),
                Tooltip = g.Text(300),
                Custom = g.Int(91),
            };
            CustomDataOf(g, cell.Items);

            if (g.Int(92) != 0)
            {
                cell.Linked = true;
                cell.LinkHandle = g.Handle(340);
                cell.LinkRows = g.Int(93);
                cell.LinkColumns = g.Int(94);
                cell.LinkExtra = g.Int(96);
            }

            int contents = g.Int(95);

            for (int i = 0; i < contents; i++)
            {
                Need(g, 302);
                Need(g, 1);
                var content = new DwgCellContent { Type = g.Int(90) };

                if (content.Type == 1)
                {
                    Need(g, 300);
                    content.Value = ValueOf(g);
                }
                else
                {
                    content.Handle = g.Handle(340);
                }

                if (g.Int(91) != 0)
                {
                    throw new FormatException("The attributes of a block content are not read.");
                }

                Need(g, 309);
                Need(g, 1);

                if (g.Int(170) != 0)
                {
                    Need(g, 300);
                    content.Format = ContentFormatOf(g);
                }

                Need(g, 309);
                cell.Contents.Add(content);
            }

            Need(g, 309);
            cell.Style = FormattedOf(g);
            Need(g, 1);
            cell.StyleId = g.Int(90);

            if (g.Int(91) != 0)
            {
                throw new FormatException("The geometry of a cell is not read.");
            }

            Need(g, 309);
            return cell;
        }

        /// <summary>Custom data from DXF: <c>CUSTOMDATA</c>, its count, then each item its name and its value.</summary>
        private static void CustomDataOf(Cursor g, List<KeyValuePair<string, DwgCellValue>> items)
        {
            Need(g, 301);
            Need(g, 1);
            int count = g.Int(90);

            for (int i = 0; i < count; i++)
            {
                string name = g.Text(300);
                Need(g, 301);
                items.Add(new KeyValuePair<string, DwgCellValue>(name, ValueOf(g)));
            }

            Need(g, 309);
        }

        /// <summary>A value from DXF, from its flags (93) to <c>ACVALUE_END</c>.</summary>
        private static DwgCellValue ValueOf(Cursor g)
        {
            var value = new DwgCellValue { Flags = g.Int(93), Type = g.Int(90) };

            if ((value.Flags & 1) == 0)
            {
                switch (value.Type)
                {
                    case 0:
                    case 1:
                        value.Long = g.Int(91);
                        break;
                    case 2:
                        value.Double = g.Double(140);
                        break;
                    default:
                        var text = new StringBuilder();

                        while (g.At(2))
                        {
                            text.Append(g.Text(2));
                        }

                        value.Text = text.Append(g.Text(1)).ToString();
                        break;
                }
            }

            value.Unit = g.Int(94);
            value.Format = g.Text(300);
            value.Shown = g.Text(302);
            Need(g, 304);
            return value;
        }

        /// <summary>The formatted part of a column, row or cell from DXF: its name and its cell style between two marks.</summary>
        private static DwgCellStyle FormattedOf(Cursor g)
        {
            Need(g, 1);
            Need(g, 300);
            DwgCellStyle style = TableFormatOf(g);
            Need(g, 309);
            return style;
        }

        /// <summary>A cell style from its DXF (TABLEFORMAT), the mirror of the reader's.</summary>
        private static DwgCellStyle TableFormatOf(Cursor g)
        {
            Need(g, 1);
            var style = new DwgCellStyle { Type = g.Int(90), HasData = g.Int(170) != 0 };

            if (style.HasData)
            {
                style.Overrides = g.Int(91);
                style.Merge = g.Int(92);
                int background = g.Int(62);
                style.Background = background == 257 ? unchecked((int)0xC8000000) : RawOf(background);
                style.Layout = g.Int(93);
                Need(g, 300);
                style.Format = ContentFormatOf(g);
                style.MarginFlags = g.Int(171);

                if ((style.MarginFlags & 1) != 0)
                {
                    Need(g, 301);
                    Need(g, 1);

                    for (int i = 0; i < 6; i++)
                    {
                        style.Margins[i] = g.Double(40);
                    }

                    Need(g, 309);
                }

                int borders = g.Int(94);

                for (int i = 0; i < borders; i++)
                {
                    var border = new DwgCellBorder { Edge = g.Int(95) };

                    if (border.Edge != 0)
                    {
                        Need(g, 302);
                        Need(g, 1);
                        border.Overrides = g.Int(90);
                        border.Type = g.Int(91);
                        border.Colour = RawOf(g.Int(62));
                        border.LineWeight = g.Int(92);
                        border.Linetype = g.Handle(340);
                        border.Invisible = g.Int(93);
                        border.Spacing = g.Double(40);
                        Need(g, 309);
                    }

                    style.Borders.Add(border);
                }
            }

            Need(g, 309);
            return style;
        }

        /// <summary>A content format from its DXF (CONTENTFORMAT), between its two marks.</summary>
        private static DwgContentFormat ContentFormatOf(Cursor g)
        {
            Need(g, 1);
            var format = new DwgContentFormat
            {
                Overrides = g.Int(90),
                Flags = g.Int(91),
                DataType = g.Int(92),
                UnitType = g.Int(93),
                FormatString = g.Text(300),
                Rotation = g.Double(40),
                Scale = g.Double(140, 1.0),
                Alignment = g.Int(94),
                Colour = RawOf(g.Int(62)),
                TextStyle = g.Handle(340),
                Height = g.Double(144),
            };
            Need(g, 309);
            return format;
        }

        /// <summary>
        /// The content without the cell styles of a table style's own (101 on) that the style does not hold
        /// (<paramref name="held"/>, see <see cref="CellStylesOf"/>): a row of one takes the data's (3), a column or a cell
        /// none.
        /// </summary>
        private static DwgTableContent WithoutOwnStyles(DwgTableContent content, HashSet<int> held)
        {
            foreach (DwgTableLine column in content.Columns)
            {
                column.StyleId = Held(column.StyleId, held) ? column.StyleId : 0;
            }

            foreach (DwgTableLine row in content.Rows)
            {
                row.StyleId = Held(row.StyleId, held) ? row.StyleId : 3;

                foreach (DwgTableCell cell in row.Cells)
                {
                    cell.StyleId = Held(cell.StyleId, held) ? cell.StyleId : 0;
                }
            }

            return content;
        }

        /// <summary>Whether a cell style id is written: one of the rows (1 to 3), none (0), or one the style holds.</summary>
        private static bool Held(int id, HashSet<int> held) => id < 101 || held.Contains(id);

        /// <summary>The ids of the cell styles a table style of a handle holds in its CELLSTYLEMAP; none where it has none.</summary>
        private HashSet<int> OwnStyleIdsOf(ulong style)
        {
            var ids = new HashSet<int>();
            DwgRecord record = _drawing.Find(style);

            if (record != null)
            {
                foreach (NamedCellStyle one in CellStylesOf(record))
                {
                    ids.Add(one.Id);
                }
            }

            return ids;
        }

        /// <summary>Takes the next group, which must be of <paramref name="code"/>.</summary>
        private static void Need(Cursor g, int code)
        {
            if (!g.At(code))
            {
                throw new FormatException("The table content is not in the order of AutoCAD's DXF.");
            }

            g.Text(code);
        }

        /// <summary>The one number of a CMC colour of an index: 0xC0 by layer (256), 0xC1 by block (0), else 0xC3 and the index.</summary>
        private static int RawOf(int index) =>
            index == 256 ? unchecked((int)0xC0000000)
            : index == 0 ? unchecked((int)0xC1000000)
            : unchecked((int)0xC3000000) | (index & 0xFF);
    }
}
