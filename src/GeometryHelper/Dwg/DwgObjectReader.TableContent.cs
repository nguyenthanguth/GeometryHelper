using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The table content of R2010 and later (<see cref="DwgTableContent"/>), which an ACAD_TABLE holds in place of the cells
    /// of R2007, and the cell styles a TABLESTYLE holds.
    /// </content>
    internal sealed partial class DwgObjectReader
    {
        private string Text() => Encoding.UTF8.GetString(T());

        /// <summary>
        /// An ACAD_TABLE of R2010 and later after its block reference: a version and two values of no known use, its
        /// content, its direction and its break data; written as DXF writes it, in the groups of R2007 (93 to 96 no
        /// overrides): its rows and columns with their sizes, then each cell with how it is merged and its value.
        /// </summary>
        private void ReadTable2010(ulong block)
        {
            int version = _data.RawChar();
            H();
            _data.BitLong();

            if (R2013Plus)
            {
                _data.BitLong();
            }
            else
            {
                _data.Bit();
            }

            DwgTableContent content = ReadTableContent();
            _data.BitShort();
            double[] direction = _data.Point3();

            if (_data.BitLong() != 0)
            {
                _data.BitLong();
                _data.BitLong();
                _data.BitDouble();
                _data.BitLong();
                _data.BitLong();
                int heights = Count(_data.BitLong());

                for (int i = 0; i < heights; i++)
                {
                    _data.Point3();
                    _data.BitDouble();
                    _data.BitLong();
                }
            }

            int ranges = Count(_data.BitLong());

            for (int i = 0; i < ranges; i++)
            {
                _data.Point3();
                _data.BitLong();
                _data.BitLong();
            }

            _object.Add(100, "AcDbTable");
            _object.Add(280, version);
            _object.Add(342, content.TableStyle);
            _object.Add(343, block);
            _object.AddPoint(11, direction);
            _object.Add(90, 22);
            _object.Add(91, content.Rows.Count);
            _object.Add(92, content.Columns.Count);

            for (int code = 93; code <= 96; code++)
            {
                _object.Add(code, 0);
            }

            foreach (DwgTableLine row in content.Rows)
            {
                _object.Add(141, row.Size);
            }

            foreach (DwgTableLine column in content.Columns)
            {
                _object.Add(142, column.Size);
            }

            for (int r = 0; r < content.Rows.Count; r++)
            {
                List<DwgTableCell> cells = content.Rows[r].Cells;

                for (int c = 0; c < content.Columns.Count; c++)
                {
                    AddTableCell(c < cells.Count ? cells[c] : new DwgTableCell(), MergeOf(content, r, c));
                }
            }
        }

        /// <summary>
        /// How the cell of row <paramref name="r"/> and column <paramref name="c"/> is merged, as groups 173, 175 and 176
        /// give it: whether another cell covers it, and the columns and rows it spans.
        /// </summary>
        private static int[] MergeOf(DwgTableContent content, int r, int c)
        {
            foreach (int[] range in content.Merged)
            {
                if (r < range[0] || r > range[2] || c < range[1] || c > range[3])
                {
                    continue;
                }

                return r == range[0] && c == range[1]
                    ? new[] { 0, range[3] - range[1] + 1, range[2] - range[0] + 1 }
                    : new[] { 1, 1, 1 };
            }

            return new[] { 0, 1, 1 };
        }

        /// <summary>A cell in the groups of R2007: its kind, edges, merging, flags, rotation, block and value.</summary>
        private void AddTableCell(DwgTableCell cell, int[] merge)
        {
            DwgCellContent content = cell.Contents.Count > 0 ? cell.Contents[0] : null;
            bool block = content != null && content.Type == 4;
            _object.Add(171, block ? 2 : 1);
            _object.Add(172, 0);
            _object.Add(173, merge[0]);
            _object.Add(174, 0);
            _object.Add(175, merge[1]);
            _object.Add(176, merge[2]);
            _object.Add(91, content?.Format != null ? 0x40000 : 0);
            _object.Add(178, 0);
            _object.Add(145, content?.Format?.Rotation ?? 0.0);

            if (block)
            {
                _object.Add(340, content.Handle);
                _object.Add(144, content.Format?.Scale ?? 1.0);
            }

            _object.Add(92, 0);
            DwgCellValue value = content != null && content.Type == 1 ? content.Value : new DwgCellValue { Flags = 7 };
            _object.Add(301, "CELL_VALUE");
            _object.Add(93, value.Flags);
            _object.Add(90, value.Type);

            if ((value.Flags & 1) == 0)
            {
                switch (value.Type)
                {
                    case 0:
                    case 1:
                        _object.Add(91, value.Long);
                        break;
                    case 2:
                        _object.Add(140, value.Double);
                        break;
                    default:
                        _object.Add(1, Encoding.UTF8.GetBytes(value.Text));
                        break;
                }
            }

            _object.Add(94, value.Unit);
            _object.Add(300, Encoding.UTF8.GetBytes(value.Format));
            _object.Add(302, Encoding.UTF8.GetBytes(value.Shown));
            _object.Add(304, "ACVALUE_END");
        }

        /// <summary>
        /// A TABLESTYLE of R2010 and later: a version, its description, its flags, the next number of a cell style of its
        /// own and the cell styles Table, _TITLE, _HEADER and _DATA; written as DXF writes it, in the groups of R2007: its
        /// direction and margins from Table, then the data, title and header rows, each its text, colours, format and six
        /// borders (top, inside horizontal, bottom, left, inside vertical, right).
        /// </summary>
        private void ReadTableStyle2010()
        {
            int version = _data.RawChar();
            byte[] description = T();
            int flags = _data.BitLong();
            _data.BitLong();
            H();
            DwgCellStyle table = ReadCellStyle();
            _data.BitLong();
            _data.BitLong();
            T();
            var styles = new Dictionary<int, DwgCellStyle>();
            int count = Count(_data.BitLong());

            for (int i = 0; i < count; i++)
            {
                _data.BitLong();
                DwgCellStyle style = ReadCellStyle();
                int id = _data.BitLong();
                _data.BitLong();
                T();
                styles[id] = style;
            }

            _object.Add(100, "AcDbTableStyle");
            _object.Add(280, version);
            _object.Add(3, description);
            _object.Add(70, (table.Merge & 0x10000) != 0 ? 1 : 0);
            _object.Add(71, flags);
            bool margins = (table.MarginFlags & 1) != 0;
            _object.Add(40, margins ? table.Margins[1] : 0.06);
            _object.Add(41, margins ? table.Margins[0] : 0.06);
            _object.Add(280, 0);
            _object.Add(281, 0);

            // The rows of R2007 in their order: data (3), title (1), header (2).
            foreach (int id in new[] { 3, 1, 2 })
            {
                styles.TryGetValue(id, out DwgCellStyle style);
                AddRowStyle(style ?? new DwgCellStyle());
            }
        }

        /// <summary>A row style of R2007 from a cell style: its text, colours, format and borders.</summary>
        private void AddRowStyle(DwgCellStyle style)
        {
            DwgContentFormat format = style.Format;

            if (format.TextStyle != 0)
            {
                _object.Add(7, new DwgName(format.TextStyle, "Standard"));
            }

            _object.Add(140, format.Height);
            _object.Add(170, format.Alignment);
            _object.Add(62, IndexOf(format.Colour));
            bool filled = ((style.Background >> 24) & 0xFF) != 0xC8;
            _object.Add(63, filled ? IndexOf(style.Background) : 7);
            _object.Add(283, filled ? 1 : 0);
            _object.Add(90, format.DataType);
            _object.Add(91, format.UnitType);
            _object.Add(1, Encoding.UTF8.GetBytes(format.FormatString));

            // The borders of R2007 by the edges of R2010; a border not given is by block, of the default lineweight, shown.
            int[] edges = { 1, 0x20, 4, 8, 0x10, 2 };

            for (int i = 0; i < 6; i++)
            {
                DwgCellBorder border = style.Borders.Find(b => b.Edge == edges[i]) ?? new DwgCellBorder();
                _object.Add(274 + i, border.LineWeight);
                _object.Add(284 + i, border.Invisible != 0 ? 0 : 1);
                _object.Add(64 + i, (border.Overrides & 8) != 0 ? IndexOf(border.Colour) : 0);
            }
        }

        /// <summary>The colour index of a CMC colour given as its one number: 256 by layer, 0 by block, else its index.</summary>
        private static int IndexOf(int raw)
        {
            switch ((raw >> 24) & 0xFF)
            {
                case 0xC0:
                    return 256;
                case 0xC1:
                    return 0;
                case 0xC2:
                    return GeometryHelper.Dxf.DxfColor.OfRgb(raw & 0xFFFFFF, null).ColorIndex;
                default:
                    return raw & 0xFF;
            }
        }

        private DwgTableContent ReadTableContent()
        {
            var content = new DwgTableContent { Name = Text(), Description = Text() };
            int columns = Count(_data.BitLong());

            for (int i = 0; i < columns; i++)
            {
                var column = new DwgTableLine { Name = Text(), Custom = _data.BitLong() };
                ReadCustomData();
                column.Style = ReadCellStyle();
                column.StyleId = _data.BitLong();
                column.Size = _data.BitDouble();
                content.Columns.Add(column);
            }

            int rows = Count(_data.BitLong());

            for (int i = 0; i < rows; i++)
            {
                var row = new DwgTableLine();
                int cells = Count(_data.BitLong());

                for (int j = 0; j < cells; j++)
                {
                    row.Cells.Add(ReadTableCell2010());
                }

                row.Custom = _data.BitLong();
                ReadCustomData();
                row.Style = ReadCellStyle();
                row.StyleId = _data.BitLong();
                row.Size = _data.BitDouble();
                content.Rows.Add(row);
            }

            int fields = Count(_data.BitLong());

            for (int i = 0; i < fields; i++)
            {
                H();
            }

            content.Style = ReadCellStyle();
            int merged = Count(_data.BitLong());

            for (int i = 0; i < merged; i++)
            {
                content.Merged.Add(new[] { _data.BitLong(), _data.BitLong(), _data.BitLong(), _data.BitLong() });
            }

            content.TableStyle = H();
            return content;
        }

        private DwgTableCell ReadTableCell2010()
        {
            var cell = new DwgTableCell
            {
                State = _data.BitLong(),
                Tooltip = Text(),
                Custom = _data.BitLong(),
            };
            ReadCustomData();

            if (_data.BitLong() == 1)
            {
                // Linked data: the data link, its rows, its columns and a number.
                H();
                _data.BitLong();
                _data.BitLong();
                _data.BitLong();
            }

            int contents = Count(_data.BitLong());

            for (int i = 0; i < contents; i++)
            {
                var content = new DwgCellContent { Type = _data.BitLong() };

                switch (content.Type)
                {
                    case 1:
                        content.Value = ReadValue();
                        break;
                    case 2:
                    case 4:
                        content.Handle = H();
                        break;
                }

                int attributes = Count(_data.BitLong());

                for (int j = 0; j < attributes; j++)
                {
                    content.Attributes.Add(new object[] { H(), Text(), _data.BitLong() });
                }

                if (_data.BitShort() != 0)
                {
                    content.Format = ReadContentFormat();
                }

                cell.Contents.Add(content);
            }

            cell.Style = ReadCellStyle();
            cell.StyleId = _data.BitLong();

            if (_data.BitLong() != 0)
            {
                _data.BitLong();
                _data.BitDouble();
                _data.BitDouble();
                int geometry = _data.BitLong();
                H();

                if (geometry != 0)
                {
                    _data.Point3();
                    _data.Point3();

                    for (int i = 0; i < 4; i++)
                    {
                        _data.BitDouble();
                    }

                    _data.BitLong();
                }
            }

            return cell;
        }

        /// <summary>A collection of custom data, read past: its count, then for each a name and a value.</summary>
        private void ReadCustomData()
        {
            int count = Count(_data.BitLong());

            for (int i = 0; i < count; i++)
            {
                T();
                ReadValue();
            }
        }

        /// <summary>A value (20.4.99): its flags, its type, the value unless it holds nothing, its unit, format and text.</summary>
        private DwgCellValue ReadValue()
        {
            var value = new DwgCellValue { Flags = _data.BitLong(), Type = _data.BitLong() };

            if ((value.Flags & 1) == 0)
            {
                switch (value.Type)
                {
                    case 0:
                    case 1:
                        value.Long = _data.BitLong();
                        break;
                    case 2:
                        value.Double = _data.BitDouble();
                        break;
                    case 4:
                    case 512:
                        int length = _data.BitLong();

                        if (length < 0 || length * 8L > _data.Remaining)
                        {
                            throw new FormatException("The value of the table cell runs past its end.");
                        }

                        byte[] utf16 = _data.Bytes(length);
                        value.Text = Encoding.Unicode.GetString(utf16, 0, Math.Max(0, length - 2));
                        break;
                    default:
                        throw new FormatException(
                            "The table cell has a value of the kind " + value.Type + ", which is not read."
                        );
                }
            }

            value.Unit = _data.BitLong();
            value.Format = Text();
            value.Shown = Text();
            return value;
        }

        /// <summary>A content format (20.4.101.3).</summary>
        private DwgContentFormat ReadContentFormat()
        {
            var format = new DwgContentFormat
            {
                Overrides = _data.BitLong(),
                Flags = _data.BitLong(),
                DataType = _data.BitLong(),
                UnitType = _data.BitLong(),
                FormatString = Text(),
                Rotation = _data.BitDouble(),
                Scale = _data.BitDouble(),
                Alignment = _data.BitLong(),
                Colour = ReadColour().Raw,
                TextStyle = H(),
                Height = _data.BitDouble(),
            };
            return format;
        }

        /// <summary>A cell style (20.4.101.4): its type and, where it has data, what it overrides.</summary>
        private DwgCellStyle ReadCellStyle()
        {
            var style = new DwgCellStyle { Type = _data.BitLong(), HasData = _data.BitShort() != 0 };

            if (!style.HasData)
            {
                return style;
            }

            style.Overrides = _data.BitLong();
            style.Merge = _data.BitLong();
            style.Background = ReadColour().Raw;
            style.Layout = _data.BitLong();
            style.Format = ReadContentFormat();
            style.MarginFlags = _data.BitShort();

            if ((style.MarginFlags & 1) != 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    style.Margins[i] = _data.BitDouble();
                }
            }

            int borders = Count(_data.BitLong());

            for (int i = 0; i < borders; i++)
            {
                var border = new DwgCellBorder { Edge = _data.BitLong() };

                if (border.Edge != 0)
                {
                    border.Overrides = _data.BitLong();
                    border.Type = _data.BitLong();
                    border.Colour = ReadColour().Raw;
                    border.LineWeight = _data.BitLong();
                    border.Linetype = H();
                    border.Invisible = _data.BitLong();
                    border.Spacing = _data.BitDouble();
                }

                style.Borders.Add(border);
            }

            return style;
        }
    }
}
