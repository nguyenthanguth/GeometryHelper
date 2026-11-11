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
            if (record.Type == "CELLSTYLEMAP")
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

            for (int code = 93; code <= 96; code++)
            {
                table.Int(code);
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

            return content;
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

            return cell;
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

            _data.BitLong(0);
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
            _data.BitLong(0);
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
        /// the default (by block, the default lineweight, shown).
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
            _data.BitLong(3);

            // The rows of DXF are data, title and header; the file holds title (1), header (2) and data (3).
            DwgCellStyle[] ordered = { rows[1], rows[2], rows[0] };
            string[] names = { "_TITLE", "_HEADER", "_DATA" };

            for (int i = 0; i < 3; i++)
            {
                if (i == 0)
                {
                    ordered[i].Merge = 0x8000;
                }

                _data.BitLong(i + 1);
                WriteCellStyle(ordered[i]);
                _data.BitLong(i + 1);
                _data.BitLong(i == 2 ? 2 : 1);
                T(names[i]);
            }
        }

        /// <summary>A row of a table style, its groups 7 to the last border, as a cell style.</summary>
        private DwgCellStyle RowStyleOf(Cursor style)
        {
            ulong textStyle = style.At(7) ? _drawing.HandleOf("STYLE", style.Text(7)) : 0;
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

            // The borders of DXF: top, inside horizontal, bottom, left, inside vertical, right.
            int[] edges = { 1, 0x20, 4, 8, 0x10, 2 };

            for (int i = 0; i < 6; i++)
            {
                int weight = style.Int(274 + i, -2);
                bool shown = style.Int(284 + i, 1) != 0;
                int colour = style.Int(64 + i);
                int overrides = (weight != -2 ? 2 : 0) | (colour != 0 ? 8 : 0) | (shown ? 0 : 0x10);

                if (overrides != 0)
                {
                    row.Borders.Add(
                        new DwgCellBorder
                        {
                            Edge = edges[i],
                            Overrides = overrides,
                            Colour = RawOf(colour),
                            LineWeight = weight,
                            Invisible = shown ? 0 : 1,
                        }
                    );
                }
            }

            return row;
        }

        /// <summary>The one number of a CMC colour of an index: 0xC0 by layer (256), 0xC1 by block (0), else 0xC3 and the index.</summary>
        private static int RawOf(int index) =>
            index == 256 ? unchecked((int)0xC0000000)
            : index == 0 ? unchecked((int)0xC1000000)
            : unchecked((int)0xC3000000) | (index & 0xFF);
    }
}
