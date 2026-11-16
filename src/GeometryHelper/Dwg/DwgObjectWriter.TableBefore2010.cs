using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The ACAD_TABLE of R2000, R2004 and R2007, the mirror of <c>DwgObjectReader.ReadTable</c>: DXF keeps it as its block
    /// reference, its flags, rows and columns, the overrides of the whole table, the sizes and then each cell with its flags
    /// and the values the flags name; the file keeps the sizes first, then the cells, then the overrides of the whole
    /// table. R2010 keeps a table as a TABLECONTENT instead (<c>WriteTable</c>).
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        /// <summary>The overrides of a cell that name a value, by the bit of the flags that sets them, in the order the file holds them: bit, code, kind.</summary>
        private static readonly int[][] CellOverrides =
        {
            new[] { 0x01, 170, 'S' },
            new[] { 0x02, 283, 'B' },
            new[] { 0x04, 63, 'C' },
            new[] { 0x08, 64, 'C' },
            new[] { 0x10, 7, 'H' },
            new[] { 0x20, 140, 'D' },
            new[] { 0x40, 69, 'C' },
            new[] { 0x400, 279, 'S' },
            new[] { 0x400, 289, 'S' },
            new[] { 0x80, 65, 'C' },
            new[] { 0x800, 275, 'S' },
            new[] { 0x800, 285, 'S' },
            new[] { 0x100, 66, 'C' },
            new[] { 0x1000, 276, 'S' },
            new[] { 0x1000, 286, 'S' },
            new[] { 0x200, 68, 'C' },
            new[] { 0x2000, 278, 'S' },
            new[] { 0x2000, 288, 'S' },
        };

        /// <summary>The overrides of the whole table that name a value, by the bit of the flags of 93: bit, code, kind.</summary>
        private static readonly int[][] TableOverrides93 =
        {
            new[] { 0x01, 280, 'B' },
            new[] { 0x04, 70, 'S' },
            new[] { 0x08, 40, 'D' },
            new[] { 0x10, 41, 'D' },
            new[] { 0x20, 64, 'C' },
            new[] { 0x40, 64, 'C' },
            new[] { 0x80, 64, 'C' },
            new[] { 0x100, 283, 'B' },
            new[] { 0x200, 283, 'B' },
            new[] { 0x400, 283, 'B' },
            new[] { 0x800, 63, 'C' },
            new[] { 0x1000, 63, 'C' },
            new[] { 0x2000, 63, 'C' },
            new[] { 0x4000, 170, 'S' },
            new[] { 0x8000, 170, 'S' },
            new[] { 0x10000, 170, 'S' },
            new[] { 0x20000, 7, 'H' },
            new[] { 0x40000, 7, 'H' },
            new[] { 0x80000, 7, 'H' },
            new[] { 0x100000, 140, 'D' },
            new[] { 0x200000, 140, 'D' },
            new[] { 0x400000, 140, 'D' },
        };

        /// <summary>
        /// An ACAD_TABLE before R2010: its block reference, then its flags, direction, columns and rows with their sizes, its
        /// style, its cells and the overrides of the whole table, as the reader reads them.
        /// </summary>
        private void WriteTableBefore2010()
        {
            WriteInsert(false);
            var groups = new List<KeyValuePair<int, string>>(_record.Of("AcDbTable").All);
            var head = new Cursor(groups);
            head.Int(280);
            ulong style = head.Handle(342);
            head.Handle(343);
            double[] direction = head.At(11) ? head.Point(11) : new[] { 1.0, 0.0, 0.0 };
            int flags = head.Int(90);
            int rows = head.Int(91);
            int columns = head.Int(92);
            var overrideFlags = new int[4];

            for (int i = 0; i < 4; i++)
            {
                overrideFlags[i] = head.Int(93 + i);
            }

            // The overrides of the whole table stand between those flags and the sizes, as AutoCAD writes them.
            var own = new List<KeyValuePair<int, string>>();

            while (head.Code >= 0 && head.Code != 141 && head.Code != 142 && head.Code != 171)
            {
                int code = head.Code;
                own.Add(new KeyValuePair<int, string>(code, head.Text(code)));
            }

            var heights = new List<double>();
            var widths = new List<double>();

            while (head.At(141))
            {
                heights.Add(head.Double(141));
            }

            while (head.At(142))
            {
                widths.Add(head.Double(142));
            }

            var cells = new List<List<KeyValuePair<int, string>>>();
            int first = groups.FindIndex(g => g.Key == 171);

            for (int i = first < 0 ? groups.Count : first; i < groups.Count; i++)
            {
                if (groups[i].Key == 171)
                {
                    cells.Add(new List<KeyValuePair<int, string>>());
                }

                cells[cells.Count - 1].Add(groups[i]);
            }

            // A table that names far more cells than its groups hold is written with none.
            if (rows < 0 || columns < 0 || (long)rows * columns > cells.Count + 1024L)
            {
                rows = 0;
                columns = 0;
            }

            _data.BitShort(flags);
            _data.Point3(direction);
            _data.BitLong(columns);
            _data.BitLong(rows);

            for (int c = 0; c < columns; c++)
            {
                _data.BitDouble(c < widths.Count ? widths[c] : 0.0);
            }

            for (int r = 0; r < rows; r++)
            {
                _data.BitDouble(r < heights.Count ? heights[r] : 0.0);
            }

            H(5, style);

            for (int i = 0; i < rows * columns; i++)
            {
                WriteTableCellBefore2010(i < cells.Count ? cells[i] : new List<KeyValuePair<int, string>>());
            }

            WriteTableOverrides(own, overrideFlags);
        }

        /// <summary>
        /// A DATATABLE (ACDBDATATABLE in a DXF of R2000), which a table of R2000 and R2004 keeps under its round-trip record where
        /// R2007 keeps a TABLECONTENT: its version, columns and rows, its name, then each column its kind, its name and one
        /// value per row (kinds 1 a whole number, 2 a real, 6 a handle).
        /// </summary>
        private void WriteDataTable()
        {
            DwgGroups held = _record.Of(_record.Type == "ACDBDATATABLE" ? "ACDBDATATABLE" : "AcDbDataTable");
            var groups = new Cursor(held);
            _data.BitShort(groups.Int(70));
            int columns = groups.Int(90);
            int rows = groups.Int(91);

            // A data table that names more values than its groups hold is written empty.
            if (columns < 0 || rows < 0 || (long)columns * rows > held.All.Count() + 1024L)
            {
                columns = 0;
                rows = 0;
            }

            _data.BitLong(columns);
            _data.BitLong(rows);
            T(groups.Text(1));

            for (int c = 0; c < columns; c++)
            {
                int kind = groups.Int(92);
                _data.BitLong(kind);
                T(groups.Text(2));

                for (int r = 0; r < rows; r++)
                {
                    switch (kind)
                    {
                        case 1:
                            _data.BitLong(groups.Int(93));
                            break;
                        case 2:
                            _data.BitDouble(groups.Double(40));
                            break;
                        default:
                            H(3, groups.Handle(360));
                            break;
                    }
                }
            }
        }

        /// <summary>The first value of <paramref name="code"/> among <paramref name="groups"/>; <paramref name="otherwise"/> where there is none.</summary>
        private static string FirstOf(List<KeyValuePair<int, string>> groups, int code, string otherwise = "")
        {
            int at = groups.FindIndex(g => g.Key == code);
            return at < 0 ? otherwise : groups[at].Value;
        }

        /// <summary>
        /// One cell before R2010 from its groups: its kind, edges, merging, size and turn, the handle of its field or block (or,
        /// before R2007, its text), the values its flags name, and from R2007 its value.
        /// </summary>
        private void WriteTableCellBefore2010(List<KeyValuePair<int, string>> cell)
        {
            // The value of a cell from R2007 starts at 301; what is before it is the cell's own groups.
            int valueAt = cell.FindIndex(g => g.Key == 301);
            List<KeyValuePair<int, string>> own = valueAt < 0 ? cell : cell.GetRange(0, valueAt);
            var at = new Cursor(own);
            int type = at.Int(171, 1);
            int edges = at.Int(172);
            bool merged = at.Int(173) != 0;
            bool autoFit = at.Int(174) != 0;
            int width = at.Int(175, 1);
            int height = at.Int(176, 1);
            int flags = at.Int(R2007Plus ? 91 : 177);
            int state = at.Int(178);
            ulong block = DwgRecord.Hex(FirstOf(own, 340, "0"));
            ulong field = DwgRecord.Hex(FirstOf(own, 344, "0"));
            _data.BitShort(type);
            _data.RawChar(edges);
            _data.Bit(merged);
            _data.Bit(autoFit);
            _data.BitLong(width);
            _data.BitLong(height);
            _data.BitDouble(DwgRecord.Double(FirstOf(own, 145, "0")));
            H(5, type == 2 ? block : field);
            bool content = type == 2 ? block != 0 : field != 0;

            if (type == 1 && field == 0 && !R2007Plus)
            {
                string text = DxfReader.Carets(FirstOf(own, 1));
                content = text.Length > 0;
                T(text);
            }
            else if (type == 2)
            {
                _data.BitDouble(DwgRecord.Double(FirstOf(own, 144, "1")));
                List<ulong> definitions = own.FindAll(g => g.Key == 331).ConvertAll(g => DwgRecord.Hex(g.Value));
                _data.Bit(definitions.Count > 0);

                if (definitions.Count > 0)
                {
                    List<string> names = own.FindAll(g => g.Key == 300).ConvertAll(g => g.Value);
                    _data.BitShort(definitions.Count);

                    for (int i = 0; i < definitions.Count; i++)
                    {
                        H(5, definitions[i]);
                        _data.BitShort(0);
                        T(i < names.Count ? names[i] : string.Empty);
                    }
                }
            }

            // The DXF AutoCAD writes of R2000 and R2004 holds the flags as a signed short, without the bits the file has beyond 16:
            // the file has, 4 places up of each of the flags 0x400 to 0x2000 that name the second value of a border, a bit of its
            // own (0x20000 for 0x2000), and the bit of a later release (0x40000) in a cell that has content.
            int named = flags < 0 ? flags & 0xFFFF : flags & 0x3FFFF;
            named |= ((named & 0x3C00) << 4) | (content ? 0x40000 : 0);
            named = R2007Plus ? flags : named;
            _data.Bit(named != 0);

            if (named != 0)
            {
                _data.BitLong(named);
                _data.RawChar(state);

                foreach (int[] slot in CellOverrides)
                {
                    if ((named & slot[0]) != 0)
                    {
                        WriteFlagged((char)slot[2], FirstOf(own, slot[1], "0"));
                    }
                }
            }

            if (R2007Plus)
            {
                _data.BitLong(DwgRecord.Int(FirstOf(own, 92, "0")));
                WriteCellValueBefore2010(cell, valueAt);
            }
        }

        /// <summary>A value that a flag names, of the kind of the flag: <c>S</c> a bit short, <c>B</c> a bit, <c>C</c> a colour, <c>H</c> a text style, <c>D</c> a bit double.</summary>
        private void WriteFlagged(char kind, string text)
        {
            switch (kind)
            {
                case 'S':
                    _data.BitShort(DwgRecord.Int(text));
                    break;
                case 'B':
                    _data.Bit(DwgRecord.Int(text) != 0);
                    break;
                case 'C':
                    WriteColour(DwgRecord.Int(text), -1, null, true);
                    break;
                case 'H':
                    // DXF names the text style; a file read by this library names its handle.
                    ulong style = _drawing.HandleOf("STYLE", text);
                    H(5, style != 0 ? style : DwgRecord.Hex(text));
                    break;
                default:
                    _data.BitDouble(DwgRecord.Double(text));
                    break;
            }
        }

        /// <summary>
        /// The value of a cell of R2007, from <c>301 CELL_VALUE</c> to <c>304 ACVALUE_END</c>: its flags, its type, the value
        /// unless the flags say it is empty (a string as UTF-16 counted in bytes), its unit, format and the text it shows.
        /// </summary>
        private void WriteCellValueBefore2010(List<KeyValuePair<int, string>> cell, int valueAt)
        {
            List<KeyValuePair<int, string>> value =
                valueAt < 0 ? new List<KeyValuePair<int, string>>() : cell.GetRange(valueAt, cell.Count - valueAt);
            int flags = DwgRecord.Int(FirstOf(value, 93, "1"));
            int type = DwgRecord.Int(FirstOf(value, 90, "0"));
            _data.BitLong(flags);
            _data.BitLong(type);

            if ((flags & 1) == 0)
            {
                switch (type)
                {
                    case 0:
                    case 1:
                        _data.BitLong(DwgRecord.Int(FirstOf(value, 91, "0")));
                        break;
                    case 2:
                        _data.BitDouble(DwgRecord.Double(FirstOf(value, 140, "0")));
                        break;
                    default:
                        var text = new StringBuilder();

                        foreach (KeyValuePair<int, string> group in value)
                        {
                            if (group.Key == 2 || group.Key == 1)
                            {
                                text.Append(group.Value);
                            }
                        }

                        byte[] utf16 = Encoding.Unicode.GetBytes(DxfReader.Carets(text.ToString()) + "\0");
                        _data.BitLong(utf16.Length);
                        _data.Bytes(utf16);
                        break;
                }
            }

            _data.BitLong(DwgRecord.Int(FirstOf(value, 94, "0")));
            T(FirstOf(value, 300));
            T(FirstOf(value, 302));
        }

        /// <summary>
        /// The overrides of the whole table, the four sets of flags with what they name: those of 93 (the looks of the kinds of
        /// row and the margins), then the colours (94), lineweights (95) and whether hidden (96) of the lines of each kind of
        /// row. The groups of a code are taken in order by the flags that name that code: those of 93 first, then the lines.
        /// </summary>
        private void WriteTableOverrides(List<KeyValuePair<int, string>> own, int[] flags)
        {
            var queues = new Dictionary<int, Queue<string>>();

            foreach (KeyValuePair<int, string> group in own)
            {
                if (!queues.TryGetValue(group.Key, out Queue<string> queue))
                {
                    queues[group.Key] = queue = new Queue<string>();
                }

                queue.Enqueue(group.Value);
            }

            string Next(int code) =>
                queues.TryGetValue(code, out Queue<string> queue) && queue.Count > 0 ? queue.Dequeue() : "0";

            _data.Bit(flags[0] != 0);

            if (flags[0] != 0)
            {
                _data.BitLong(flags[0]);

                foreach (int[] slot in TableOverrides93)
                {
                    if ((flags[0] & slot[0]) != 0)
                    {
                        WriteFlagged((char)slot[2], Next(slot[1]));
                    }
                }
            }

            int[] colourCodes = { 64, 65, 66, 63, 68, 69 };

            for (int kind = 0; kind < 3; kind++)
            {
                int set = flags[kind + 1];
                _data.Bit(set != 0);

                if (set == 0)
                {
                    continue;
                }

                _data.BitLong(set);

                for (int i = 0; i < 18; i++)
                {
                    if ((set & (1 << i)) != 0)
                    {
                        int code =
                            kind == 0 ? colourCodes[i % 6]
                            : kind == 1 ? 274 + (i % 6)
                            : 284 + (i % 6);
                        WriteFlagged(kind == 0 ? 'C' : 'S', Next(code));
                    }
                }
            }
        }
    }
}
