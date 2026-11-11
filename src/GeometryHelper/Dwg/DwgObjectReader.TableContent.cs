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
        // Set where a table content read holds a part no test drawing has shown in DXF yet.
        private bool _unseen;

        // The cell styles of the table style read last, by id; and those of each table style a table asked for.
        private Dictionary<int, DwgCellStyle> _readStyles;
        private Dictionary<ulong, Dictionary<int, DwgCellStyle>> _stylesOf;

        /// <summary>
        /// The cell styles of the table style of a handle, by id, read once by a reader of its own; none where the object
        /// map is not given or the style cannot be read.
        /// </summary>
        private Dictionary<int, DwgCellStyle> StylesOf(ulong handle)
        {
            _stylesOf = _stylesOf ?? new Dictionary<ulong, Dictionary<int, DwgCellStyle>>();

            if (_stylesOf.TryGetValue(handle, out Dictionary<int, DwgCellStyle> found))
            {
                return found;
            }

            found = new Dictionary<int, DwgCellStyle>();
            long? at = OffsetOf?.Invoke(handle);

            if (at != null)
            {
                var other = new DwgObjectReader(_file, _classList, _versionName);

                try
                {
                    if (other.Read(handle, at.Value).Decoded && other._readStyles != null)
                    {
                        found = other._readStyles;
                    }
                }
                catch (FormatException)
                {
                    // A style that cannot be read gives no looks to push into the cells.
                }
            }

            _stylesOf[handle] = found;
            return found;
        }

        /// <summary>
        /// The CELLSTYLEMAP AutoCAD writes in DXF for a table style, which a DWG does not hold: each of its cell styles, its
        /// format, then its id, class and name; kept on the style for the transcoder to put under its extension dictionary.
        /// </summary>
        private void AddCellStyleMap(List<object[]> styles)
        {
            DwgObject style = _object;
            _object = new DwgObject { Name = "CELLSTYLEMAP", Decoded = true };
            _object.Add(100, "AcDbCellStyleMap");
            _object.Add(90, styles.Count);

            foreach (object[] one in styles)
            {
                _object.Add(300, "CELLSTYLE");
                AddTableFormat((DwgCellStyle)one[0]);
                _object.Add(1, "CELLSTYLE_BEGIN");
                _object.Add(90, (int)one[1]);
                _object.Add(91, (int)one[2]);
                _object.Add(300, Encoding.UTF8.GetBytes((string)one[3]));
                _object.Add(309, "CELLSTYLE_END");
            }

            style.CellStyleMap = _object;
            _object = style;
        }

        /// <summary>The properties of a cell's looks, in the order AutoCAD 2021 writes a cell's own: 170, 7, 63, 64, 283, 140.</summary>
        private sealed class Looks
        {
            public int? Alignment;
            public ulong? TextStyle;
            public int? Background;
            public int? Colour;
            public double? Height;

            /// <summary>Takes from a cell style or a content format the properties its flags override that are not taken yet.</summary>
            public void Take(int overrides, DwgContentFormat format, int background)
            {
                Alignment = Alignment ?? ((overrides & 0x10) != 0 ? format.Alignment : (int?)null);
                Colour = Colour ?? ((overrides & 0x20) != 0 ? format.Colour : (int?)null);
                TextStyle = TextStyle ?? ((overrides & 0x40) != 0 ? format.TextStyle : (ulong?)null);
                Height = Height ?? ((overrides & 0x80) != 0 ? format.Height : (double?)null);
                Background = Background ?? ((overrides & 0x200) != 0 ? background : (int?)null);
            }

            /// <summary>Takes the properties of a cell style of its own that differ from those of the kind of row, not taken yet.</summary>
            public void TakeDiffering(DwgCellStyle own, DwgCellStyle kind)
            {
                int differing =
                    (own.Format.Alignment != kind.Format.Alignment ? 0x10 : 0)
                    | (own.Format.Colour != kind.Format.Colour ? 0x20 : 0)
                    | (own.Format.TextStyle != kind.Format.TextStyle ? 0x40 : 0)
                    | (own.Format.Height != kind.Format.Height ? 0x80 : 0)
                    | (own.Background != kind.Background ? 0x200 : 0);
                Take(differing, own.Format, own.Background);
            }
        }

        private string Text() => Encoding.UTF8.GetString(T());

        /// <summary>
        /// An ACAD_TABLE of R2010 and later after its block reference: a version and two values of no known use, its
        /// content, its direction and its break data; written as DXF writes it, in the groups of R2007: the table's own
        /// overrides (93 to 96 and their groups), its rows and columns with their sizes, then each cell with how it is
        /// merged, its overrides and its value.
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
            AddTableOverrides(content);

            foreach (DwgTableLine row in content.Rows)
            {
                _object.Add(141, row.Size);
            }

            foreach (DwgTableLine column in content.Columns)
            {
                _object.Add(142, column.Size);
            }

            Dictionary<int, DwgCellStyle> styles = StylesOf(content.TableStyle);

            for (int r = 0; r < content.Rows.Count; r++)
            {
                for (int c = 0; c < content.Columns.Count; c++)
                {
                    AddTableCell(content, styles, r, c);
                }
            }
        }

        /// <summary>
        /// The table's own overrides of R2007, as AutoCAD 2021 writes them from R2010: the flags 93 to 96, then their groups
        /// in its head, the margins of the table's own cell style (40 horizontal, 41 vertical), then of the first row of each
        /// kind (title, header, data) the looks and the lines its cell style overrides, by property: heights, alignments,
        /// fills, colours, the lines' colours and showing, whether filled, text styles and the lines' lineweights; each line
        /// of a kind in the order top, inside horizontal, bottom, left, inside vertical, right.
        /// </summary>
        private void AddTableOverrides(DwgTableContent content)
        {
            var flags = new int[4];
            var groups = new List<DwgGroup>();
            DwgCellStyle table = content.Style;

            if (table.HasData && (table.Overrides & 0x400) != 0)
            {
                flags[0] |= 8;
                groups.Add(new DwgGroup(40, table.Margins[1]));
            }

            if (table.HasData && (table.Overrides & 0x800) != 0)
            {
                flags[0] |= 16;
                groups.Add(new DwgGroup(41, table.Margins[0]));
            }

            var kinds = new DwgCellStyle[3];

            foreach (DwgTableLine row in content.Rows)
            {
                if (row.StyleId >= 1 && row.StyleId <= 3 && kinds[row.StyleId - 1] == null && row.Style.HasData)
                {
                    kinds[row.StyleId - 1] = row.Style;
                }
            }

            int[] lineColours = { 64, 65, 66, 63, 68, 69 };

            // Each property in turn: 140 (bits 20 on), 170 (14), 63 (11), 64 (5), the lines' colours (94) and showing (96),
            // 283 (8), 7 (17), the lines' lineweights (95).
            for (int property = 0; property < 9; property++)
            {
                for (int k = 0; k < 3; k++)
                {
                    DwgCellStyle style = kinds[k];

                    if (style == null)
                    {
                        continue;
                    }

                    int o = style.Overrides;
                    DwgContentFormat format = style.Format;
                    bool none = ((style.Background >> 24) & 0xFF) == 0xC8;

                    switch (property)
                    {
                        case 0 when (o & 0x80) != 0:
                            flags[0] |= 1 << (20 + k);
                            groups.Add(new DwgGroup(140, format.Height));
                            break;
                        case 1 when (o & 0x10) != 0:
                            flags[0] |= 1 << (14 + k);
                            groups.Add(new DwgGroup(170, format.Alignment));
                            break;
                        case 2 when (o & 0x200) != 0 && !none:
                            flags[0] |= 1 << (11 + k);
                            groups.Add(new DwgGroup(63, IndexOf(style.Background)));
                            break;
                        case 3 when (o & 0x20) != 0:
                            flags[0] |= 1 << (5 + k);
                            groups.Add(new DwgGroup(64, IndexOf(format.Colour)));
                            break;
                        case 4:
                        case 5:
                        case 8:
                            int kind =
                                property == 4 ? 0
                                : property == 5 ? 2
                                : 1;
                            int bit =
                                property == 4 ? 8
                                : property == 5 ? 0x10
                                : 2;

                            for (int line = 0; line < 6; line++)
                            {
                                DwgCellBorder border = style.Borders.Find(b => b.Edge == 1 << line);

                                if (border == null || (border.Overrides & bit) == 0)
                                {
                                    continue;
                                }

                                flags[kind + 1] |= 1 << (k * 6 + line);
                                groups.Add(
                                    kind == 0 ? new DwgGroup(lineColours[line], IndexOf(border.Colour))
                                    : kind == 1 ? new DwgGroup(274 + line, border.LineWeight)
                                    : new DwgGroup(284 + line, border.Invisible)
                                );
                            }

                            break;
                        case 6 when (o & 0x200) != 0:
                            flags[0] |= 1 << (8 + k);
                            groups.Add(new DwgGroup(283, none ? 1 : 0));
                            break;
                        case 7 when (o & 0x40) != 0:
                            flags[0] |= 1 << (17 + k);
                            groups.Add(new DwgGroup(7, new DwgName(format.TextStyle, "Standard")));
                            break;
                    }
                }
            }

            for (int code = 93; code <= 96; code++)
            {
                _object.Add(code, flags[code - 93]);
            }

            _object.Body.AddRange(groups);
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

        /// <summary>The merged range (top row, left column, bottom row, right column) that holds a cell; null for none.</summary>
        private static int[] RangeOf(DwgTableContent content, int r, int c) =>
            content.Merged.Find(range => r >= range[0] && r <= range[2] && c >= range[1] && c <= range[3]);

        /// <summary>
        /// A cell in the groups of R2007: its kind, edges, merging, flags, rotation, block, the overrides AutoCAD 2021 writes
        /// for it and its value. Its looks are its content's own, else its cell style's, its row's, its column's, else those
        /// of a cell style of its own (101 on, of the cell, else its column, else its row) where they differ from those of
        /// its kind of row; written 170, 7, 63, 64, 283, 140. Its edges are its top and left, and its right and bottom in the
        /// last column and row: each its own border, else the one its neighbour shares, else its row's outer one where the
        /// edge is the row's, else, at the top of a row of another style than the cell above, the bottom border of that
        /// style; written right, bottom, left, top, each a lineweight, whether hidden where that is overridden (1 hidden)
        /// and a colour. A merged range gives its edges on its last cell alone, the borders of its first cell, without the
        /// edges inside the range: a title over three columns, its top and right on its third cell.
        /// </summary>
        private void AddTableCell(DwgTableContent table, Dictionary<int, DwgCellStyle> styles, int r, int c)
        {
            List<DwgTableCell> cells = table.Rows[r].Cells;
            DwgTableCell cell = c < cells.Count ? cells[c] : new DwgTableCell();
            int[] merge = MergeOf(table, r, c);
            DwgCellContent content = cell.Contents.Count > 0 ? cell.Contents[0] : null;
            bool block = content != null && content.Type == 4;
            DwgTableLine row = table.Rows[r];
            DwgTableLine column = table.Columns[c];
            var looks = new Looks();

            if (content?.Format != null)
            {
                looks.Take(content.Format.Overrides, content.Format, 0);
            }

            foreach (DwgCellStyle style in new[] { cell.Style, row.Style, column.Style })
            {
                if (style.HasData)
                {
                    looks.Take(style.Overrides, style.Format, style.Background);
                }
            }

            int own = OwnStyleOf(table, r, c);
            int kind =
                row.StyleId >= 1 && row.StyleId <= 3 ? row.StyleId
                : r == 0 ? 1
                : r == 1 ? 2
                : 3;

            if (
                own >= 101
                && styles.TryGetValue(own, out DwgCellStyle custom)
                && styles.TryGetValue(kind, out DwgCellStyle ofKind)
            )
            {
                looks.TakeDiffering(custom, ofKind);
            }

            // A cell a merged range covers gives no flag of its content's format.
            int flags = content?.Format != null && merge[0] == 0 ? 0x40000 : 0;
            var groups = new List<DwgGroup>();

            if (looks.Alignment != null)
            {
                flags |= 1;
                groups.Add(new DwgGroup(170, looks.Alignment.Value));
            }

            if (looks.TextStyle != null)
            {
                flags |= 0x10;
                groups.Add(new DwgGroup(7, new DwgName(looks.TextStyle.Value, "Standard")));
            }

            bool none = looks.Background != null && ((looks.Background.Value >> 24) & 0xFF) == 0xC8;

            if (looks.Background != null && !none)
            {
                flags |= 4;
                groups.Add(new DwgGroup(63, IndexOf(looks.Background.Value)));
            }

            if (looks.Colour != null)
            {
                flags |= 8;
                groups.Add(new DwgGroup(64, IndexOf(looks.Colour.Value)));
            }

            if (looks.Background != null)
            {
                flags |= 2;
                groups.Add(new DwgGroup(283, none ? 1 : 0));
            }

            if (looks.Height != null)
            {
                flags |= 0x20;
                groups.Add(new DwgGroup(140, looks.Height.Value));
            }

            // The edges right, bottom, left, top: their bit of 172, of the flags (colour; lineweight and showing on by 4
            // and 8 places), and their groups. A merged range gives them on its last cell alone, from its first, without
            // those inside it.
            int edges = 0;
            int[] range = RangeOf(table, r, c);
            DwgTableCell edged = range == null ? cell : CellAt(table, range[0], range[1]);
            DwgCellBorder[] borders =
            {
                c == table.Columns.Count - 1 ? Own(edged, 0x20) ?? Outer(row, 0x20) : null,
                r == table.Rows.Count - 1 ? Own(edged, 4) ?? Outer(row, 4) : null,
                range != null && c > range[1]
                    ? null
                    : Own(edged, 8) ?? (c > 0 ? Own(CellAt(table, r, c - 1), 0x20) : Outer(row, 8)),
                range != null && r > range[0]
                    ? null
                    : Own(edged, 1)
                        ?? (r > 0 ? Own(CellAt(table, r - 1, c), 4) : null)
                        ?? Outer(row, 1)
                        ?? (r > 0 ? Outer(table.Rows[r - 1], 4) : null)
                        ?? (r > 0 ? Boundary(table, styles, r, c, own) : null),
            };

            if (range != null && (r != range[2] || c != range[3]))
            {
                borders = new DwgCellBorder[4];
            }
            int[] edgeBits = { 2, 4, 8, 1 };
            int[] flagBits = { 0x80, 0x100, 0x200, 0x40 };
            int[] codes = { 5, 6, 8, 9 };

            for (int i = 0; i < 4; i++)
            {
                DwgCellBorder border = borders[i];

                if (border == null)
                {
                    continue;
                }

                edges |= edgeBits[i];
                flags |= flagBits[i] | (flagBits[i] << 4) | (flagBits[i] << 8);
                groups.Add(new DwgGroup(270 + codes[i], border.LineWeight));

                if ((border.Overrides & 0x10) != 0)
                {
                    groups.Add(new DwgGroup(280 + codes[i], border.Invisible));
                }

                groups.Add(new DwgGroup(60 + codes[i], IndexOf(border.Colour)));
            }

            _object.Add(171, block ? 2 : 1);
            _object.Add(172, edges);
            _object.Add(173, merge[0]);
            _object.Add(174, 0);
            _object.Add(175, merge[1]);
            _object.Add(176, merge[2]);
            _object.Add(91, flags);
            _object.Add(178, 0);
            _object.Add(145, content?.Format?.Rotation ?? 0.0);

            if (block)
            {
                _object.Add(340, content.Handle);
                _object.Add(144, content.Format?.Scale ?? 1.0);
            }

            _object.Body.AddRange(groups);
            _object.Add(92, 0);
            DwgCellValue value =
                content != null && content.Type == 1 && merge[0] == 0 ? content.Value : new DwgCellValue { Flags = 7 };
            _object.Add(301, "CELL_VALUE");
            AddValue(value);
        }

        /// <summary>The cell of a row and column; an empty one where the row holds fewer cells.</summary>
        private static DwgTableCell CellAt(DwgTableContent table, int r, int c)
        {
            List<DwgTableCell> cells = table.Rows[r].Cells;
            return c < cells.Count ? cells[c] : new DwgTableCell();
        }

        /// <summary>The cell style a cell takes: its own, else its column's, else its row's.</summary>
        private static int OwnStyleOf(DwgTableContent table, int r, int c)
        {
            int cell = CellAt(table, r, c).StyleId;
            int column = table.Columns[c].StyleId;
            return cell != 0 ? cell
                : column != 0 ? column
                : table.Rows[r].StyleId;
        }

        /// <summary>The border of an edge a cell overrides itself; null for none.</summary>
        private static DwgCellBorder Own(DwgTableCell cell, int edge) => Overridden(cell.Style, edge);

        /// <summary>The outer border of an edge a row's cell style overrides; null for none.</summary>
        private static DwgCellBorder Outer(DwgTableLine row, int edge) => Overridden(row.Style, edge);

        private static DwgCellBorder Overridden(DwgCellStyle style, int edge) =>
            style.HasData ? style.Borders.Find(b => b.Edge == edge && b.Overrides != 0) : null;

        /// <summary>
        /// The bottom border of the style of the cell above where it is not the default and not the top border of the
        /// cell's own style, a style of a kind of row, as AutoCAD writes the line under a title of lineweight 0.35 on each
        /// cell of the header below; null where there is none to write.
        /// </summary>
        private static DwgCellBorder Boundary(
            DwgTableContent table,
            Dictionary<int, DwgCellStyle> styles,
            int r,
            int c,
            int own
        )
        {
            int above = OwnStyleOf(table, r - 1, c);

            if (
                own > 3
                || above == own
                || !styles.TryGetValue(above, out DwgCellStyle upper)
                || !styles.TryGetValue(own, out DwgCellStyle lower)
            )
            {
                return null;
            }

            DwgCellBorder bottom = upper.Borders.Find(b => b.Edge == 4);
            DwgCellBorder top = lower.Borders.Find(b => b.Edge == 1);

            if (
                bottom == null
                || (bottom.LineWeight == -2 && bottom.Colour == unchecked((int)0xC1000000) && bottom.Invisible == 0)
                || (
                    top != null
                    && top.LineWeight == bottom.LineWeight
                    && top.Colour == bottom.Colour
                    && top.Invisible == bottom.Invisible
                )
            )
            {
                return null;
            }

            return new DwgCellBorder
            {
                Edge = 1,
                Colour = bottom.Colour,
                LineWeight = bottom.LineWeight,
            };
        }

        /// <summary>A value in DXF, after the group that names it: its flags, type, the value, unit, format and text.</summary>
        private void AddValue(DwgCellValue value)
        {
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
            var named = new List<object[]>();
            int count = Count(_data.BitLong());

            for (int i = 0; i < count; i++)
            {
                _data.BitLong();
                DwgCellStyle style = ReadCellStyle();
                int id = _data.BitLong();
                int kind = _data.BitLong();
                string name = Text();
                styles[id] = style;
                named.Add(new object[] { style, id, kind, name });
            }

            _readStyles = styles;

            if (named.Exists(one => (int)one[1] >= 101))
            {
                AddCellStyleMap(named);
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

            // The borders of R2007 (top, inside horizontal, bottom, left, inside vertical, right) by the lines of R2010, of
            // AutoCAD's GridLineType; a border not given is by block, of the default lineweight, shown.
            for (int i = 0; i < 6; i++)
            {
                DwgCellBorder border = style.Borders.Find(b => b.Edge == 1 << i);
                _object.Add(274 + i, border?.LineWeight ?? -2);
                _object.Add(284 + i, border != null && border.Invisible != 0 ? 0 : 1);
                _object.Add(64 + i, border != null ? IndexOf(border.Colour) : 0);
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
                ReadCustomData(column.Items);
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
                ReadCustomData(row.Items);
                row.Style = ReadCellStyle();
                row.StyleId = _data.BitLong();
                row.Size = _data.BitDouble();
                content.Rows.Add(row);
            }

            int fields = Count(_data.BitLong());
            _unseen |= fields > 0;

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
            ReadCustomData(cell.Items);

            if (_data.BitLong() == 1)
            {
                // Linked data: the data link, its rows, its columns and a number.
                _unseen = true;
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
                _unseen |= attributes > 0;

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
                _unseen = true;
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

        /// <summary>A collection of custom data: its count, then for each a name and a value.</summary>
        private void ReadCustomData(List<KeyValuePair<string, DwgCellValue>> items)
        {
            int count = Count(_data.BitLong());

            for (int i = 0; i < count; i++)
            {
                string name = Text();
                items.Add(new KeyValuePair<string, DwgCellValue>(name, ReadValue()));
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

        /// <summary>
        /// A TABLECONTENT, which AutoCAD keeps under a table's round-trip record (ACAD_ROUNDTRIP_2008_TABLE_ENTITY) as its
        /// content: the same content an ACAD_TABLE of R2010 holds, written as AutoCAD 2021 writes it in DXF, its columns,
        /// rows and cells each in their linked, formatted and table parts, from R2007 (an R2000 or R2004 file lays it out
        /// otherwise, not read yet). False where it holds what no test drawing has shown in DXF yet (a cell linked to data,
        /// the attributes of a block, fields or a cell's cached geometry), which is left out.
        /// </summary>
        private bool ReadTableContentObject()
        {
            _unseen = false;
            DwgTableContent content = ReadTableContent();

            if (_unseen)
            {
                return false;
            }

            _object.Add(100, "AcDbLinkedData");
            _object.Add(1, Encoding.UTF8.GetBytes(content.Name));
            _object.Add(300, Encoding.UTF8.GetBytes(content.Description));
            _object.Add(100, "AcDbLinkedTableData");
            _object.Add(90, content.Columns.Count);

            foreach (DwgTableLine column in content.Columns)
            {
                _object.Add(300, "COLUMN");
                _object.Add(1, "LINKEDTABLEDATACOLUMN_BEGIN");
                _object.Add(300, Encoding.UTF8.GetBytes(column.Name));
                _object.Add(91, column.Custom);
                AddCustomData(column.Items);
                _object.Add(309, "LINKEDTABLEDATACOLUMN_END");
                AddFormatted("FORMATTEDTABLEDATACOLUMN", "COLUMNTABLEFORMAT", column.Style);
                _object.Add(1, "TABLECOLUMN_BEGIN");
                _object.Add(90, column.StyleId);
                _object.Add(40, column.Size);
                _object.Add(309, "TABLECOLUMN_END");
            }

            _object.Add(91, content.Rows.Count);

            foreach (DwgTableLine row in content.Rows)
            {
                _object.Add(301, "ROW");
                _object.Add(1, "LINKEDTABLEDATAROW_BEGIN");
                _object.Add(90, row.Cells.Count);

                foreach (DwgTableCell cell in row.Cells)
                {
                    AddContentCell(cell);
                }

                _object.Add(91, row.Custom);
                AddCustomData(row.Items);
                _object.Add(309, "LINKEDTABLEDATAROW_END");
                AddFormatted("FORMATTEDTABLEDATAROW", "ROWTABLEFORMAT", row.Style);
                _object.Add(1, "TABLEROW_BEGIN");
                _object.Add(90, row.StyleId);
                _object.Add(40, row.Size);
                _object.Add(309, "TABLEROW_END");
            }

            _object.Add(92, 0);
            _object.Add(100, "AcDbFormattedTableData");
            _object.Add(300, "TABLEFORMAT");
            AddTableFormat(content.Style);
            _object.Add(90, content.Merged.Count);

            foreach (int[] range in content.Merged)
            {
                for (int i = 0; i < 4; i++)
                {
                    _object.Add(91 + i, range[i]);
                }
            }

            _object.Add(100, "AcDbTableContent");
            _object.Add(340, content.TableStyle);
            return true;
        }

        /// <summary>A cell of a TABLECONTENT in DXF: its linked part with its contents, its format and its style.</summary>
        private void AddContentCell(DwgTableCell cell)
        {
            _object.Add(300, "CELL");
            _object.Add(1, "LINKEDTABLEDATACELL_BEGIN");
            _object.Add(90, cell.State);
            _object.Add(300, Encoding.UTF8.GetBytes(cell.Tooltip));
            _object.Add(91, cell.Custom);
            AddCustomData(cell.Items);
            _object.Add(92, 0);
            _object.Add(95, cell.Contents.Count);

            foreach (DwgCellContent content in cell.Contents)
            {
                _object.Add(302, "CONTENT");
                _object.Add(1, "CELLCONTENT_BEGIN");
                _object.Add(90, content.Type);

                if (content.Type == 1)
                {
                    _object.Add(300, "VALUE");
                    AddValue(content.Value);
                }
                else
                {
                    _object.Add(340, content.Handle);
                }

                _object.Add(91, 0);
                _object.Add(309, "CELLCONTENT_END");
                _object.Add(1, "FORMATTEDCELLCONTENT_BEGIN");
                _object.Add(170, content.Format != null ? 1 : 0);

                if (content.Format != null)
                {
                    _object.Add(300, "CONTENTFORMAT");
                    AddContentFormat(content.Format);
                }

                _object.Add(309, "FORMATTEDCELLCONTENT_END");
            }

            _object.Add(309, "LINKEDTABLEDATACELL_END");
            AddFormatted("FORMATTEDTABLEDATACELL", "CELLTABLEFORMAT", cell.Style);
            _object.Add(1, "TABLECELL_BEGIN");
            _object.Add(90, cell.StyleId);
            _object.Add(91, 0);
            _object.Add(309, "TABLECELL_END");
        }

        /// <summary>The custom data in DXF: its count, then each item its name and its value.</summary>
        private void AddCustomData(List<KeyValuePair<string, DwgCellValue>> items)
        {
            _object.Add(301, "CUSTOMDATA");
            _object.Add(1, "DATAMAP_BEGIN");
            _object.Add(90, items.Count);

            foreach (KeyValuePair<string, DwgCellValue> item in items)
            {
                _object.Add(300, Encoding.UTF8.GetBytes(item.Key));
                _object.Add(301, "DATAMAP_VALUE");
                AddValue(item.Value);
            }

            _object.Add(309, "DATAMAP_END");
        }

        /// <summary>The formatted part of a column, row or cell: its cell style of overrides between its two marks.</summary>
        private void AddFormatted(string part, string name, DwgCellStyle style)
        {
            _object.Add(1, part + "_BEGIN");
            _object.Add(300, name);
            AddTableFormat(style);
            _object.Add(309, part + "_END");
        }

        /// <summary>
        /// A cell style in DXF (TABLEFORMAT): its type and, where it has data, its overrides, merging, background (257 for
        /// none), layout, content format, margins and borders, each border a GRIDFORMAT.
        /// </summary>
        private void AddTableFormat(DwgCellStyle style)
        {
            _object.Add(1, "TABLEFORMAT_BEGIN");
            _object.Add(90, style.Type);
            _object.Add(170, style.HasData ? 1 : 0);

            if (style.HasData)
            {
                _object.Add(91, style.Overrides);
                _object.Add(92, style.Merge);
                _object.Add(62, ((style.Background >> 24) & 0xFF) == 0xC8 ? 257 : IndexOf(style.Background));
                _object.Add(93, style.Layout);
                _object.Add(300, "CONTENTFORMAT");
                AddContentFormat(style.Format);
                _object.Add(171, style.MarginFlags);

                if ((style.MarginFlags & 1) != 0)
                {
                    _object.Add(301, "MARGIN");
                    _object.Add(1, "CELLMARGIN_BEGIN");

                    foreach (double margin in style.Margins)
                    {
                        _object.Add(40, margin);
                    }

                    _object.Add(309, "CELLMARGIN_END");
                }

                _object.Add(94, style.Borders.Count);

                foreach (DwgCellBorder border in style.Borders)
                {
                    _object.Add(95, border.Edge);

                    if (border.Edge == 0)
                    {
                        continue;
                    }

                    _object.Add(302, "GRIDFORMAT");
                    _object.Add(1, "GRIDFORMAT_BEGIN");
                    _object.Add(90, border.Overrides);
                    _object.Add(91, border.Type);
                    _object.Add(62, IndexOf(border.Colour));
                    _object.Add(92, border.LineWeight);
                    _object.Add(340, border.Linetype);
                    _object.Add(93, border.Invisible);
                    _object.Add(40, border.Spacing);
                    _object.Add(309, "GRIDFORMAT_END");
                }
            }

            _object.Add(309, "TABLEFORMAT_END");
        }

        /// <summary>A content format in DXF (CONTENTFORMAT) between its two marks.</summary>
        private void AddContentFormat(DwgContentFormat format)
        {
            _object.Add(1, "CONTENTFORMAT_BEGIN");
            _object.Add(90, format.Overrides);
            _object.Add(91, format.Flags);
            _object.Add(92, format.DataType);
            _object.Add(93, format.UnitType);
            _object.Add(300, Encoding.UTF8.GetBytes(format.FormatString));
            _object.Add(40, format.Rotation);
            _object.Add(140, format.Scale);
            _object.Add(94, format.Alignment);
            _object.Add(62, IndexOf(format.Colour));
            _object.Add(340, format.TextStyle);
            _object.Add(144, format.Height);
            _object.Add(309, "CONTENTFORMAT_END");
        }

        /// <summary>
        /// A TABLEGEOMETRY, which AutoCAD keeps beside a table's round-trip TABLECONTENT: its rows, columns and cells, each
        /// its flags, size, a handle and the places of its contents, as AutoCAD 2021 writes it in DXF.
        /// </summary>
        private void ReadTableGeometry()
        {
            _object.Add(100, "AcDbTableGeometry");
            _object.Add(90, _data.BitLong());
            _object.Add(91, _data.BitLong());
            int cells = Count(_data.BitLong());
            _object.Add(92, cells);

            for (int i = 0; i < cells; i++)
            {
                _object.Add(93, _data.BitLong());
                _object.Add(40, _data.BitDouble());
                _object.Add(41, _data.BitDouble());
                _object.Add(330, H());
                int contents = Count(_data.BitLong());
                _object.Add(94, contents);

                for (int j = 0; j < contents; j++)
                {
                    _object.AddPoint(10, _data.Point3());
                    _object.AddPoint(11, _data.Point3());

                    for (int code = 43; code <= 46; code++)
                    {
                        _object.Add(code, _data.BitDouble());
                    }

                    _object.Add(95, _data.BitLong());
                }
            }
        }
    }
}
