using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region A table in the other form of its cells

        // The groups a cell begins with in both forms. The seventh is 177 (the flags of its overrides) in the form of R2000 and R2004
        // and 91 (the same flags and bits more) from R2007.
        private static readonly int[] OldCellHead = { 171, 172, 173, 174, 175, 176, 177, 178, 145 };
        private static readonly int[] NewCellHead = { 171, 172, 173, 174, 175, 176, 91, 178, 145 };

        // The flags of 177 are the low 16 bits of 91, written as a signed number (0x8880 is -30592): 0x01 alignment (170), 0x02 and 0x04
        // background colour and fill (63, 283), 0x08 content colour (64), 0x10 text style (7), 0x20 height (140), the others the
        // borders' (top 0x4440 in 279, left 0x2200 in 278, right 0x8880 in 275, bottom 0x1100 in 276). 91 has bits more: the cell
        // has content (0x40000), and a cell with a border of its own on the bottom (0x1000) or the left (0x2000) has them again 4
        // bits up, as AutoCAD writes it; the top's 0x4000 four bits up would be the content's, and the right's 0x8000 has none.
        private const int ContentBit = 0x40000;

        // The groups that follow the text of a cell of the old form: the overrides (the borders' are 65 to 69 and 275 to 279, 285 to
        // 289) and the block of a block cell with its attributes.
        private static readonly HashSet<int> UpCellGroups = new HashSet<int>
        {
            7,
            63,
            64,
            65,
            66,
            67,
            68,
            69,
            140,
            144,
            170,
            179,
            275,
            276,
            277,
            278,
            279,
            283,
            285,
            286,
            287,
            288,
            289,
            300,
            331,
            340,
        };

        private const string ValueEnd = "ACVALUE_END";

        /// <summary>
        /// Gives a table of R2007 or later the form of R2004 and before, where each cell is the groups 171 to 178 and 145, its text
        /// (1) and the groups of its overrides: the cell's value (301 CELL_VALUE to 304 ACVALUE_END) and its 92 of the later form
        /// are left out, its 177 is its 91 less the bits the older form has not. A table that holds its TABLECONTENT (the round trip
        /// of its extension dictionary, ACAD_ROUNDTRIP_2008_TABLE_ENTITY) keeps it with the values in the form of R2004. False, the
        /// table left as it is, where a cell holds anything but text or a block (a field, a link, a value of another kind): the
        /// caller then draws the table as the block it is drawn with.
        /// </summary>
        private bool TableDown(Record table)
        {
            if (!Cells(table, out List<Group> groups, out int first, out int stop, out List<int> starts))
            {
                return false;
            }

            var made = new List<Group>(groups.GetRange(0, first));
            var data = new List<CellData>(starts.Count);

            for (int i = 0; i < starts.Count; i++)
            {
                int to = i + 1 < starts.Count ? starts[i + 1] : stop;

                if (!CellDown(groups.GetRange(starts[i], to - starts[i]), i + 1 == starts.Count, made, data))
                {
                    return false;
                }
            }

            var changes = new List<Action>();

            if (!ContentDown(table, changes))
            {
                return false;
            }

            int rows = Count(groups, first, 91);
            int columns = Count(groups, first, 92);
            made.AddRange(groups.GetRange(stop, groups.Count - stop));
            table.Groups = made;

            foreach (Action change in changes)
            {
                change();
            }

            // AutoCAD keeps the cells of the form before R2007 beside the table, to know a cell the old release changed.
            if (rows * columns == data.Count)
            {
                KeepCells(table, data, rows, columns);
            }

            return true;
        }

        /// <summary>
        /// Gives a table of R2004 or before the form of R2007 and later: each cell with its flags (91), the groups of its overrides
        /// before its 92, and its value (301 CELL_VALUE, 93, 90, the text in 1, 94, 300, 302 and 304 ACVALUE_END): where the cell has
        /// text 93 is 6, 90 is 4 and 91 has the flag of text, where it has none 93 is 7 and 90 is 0, and a block cell has 93 1 and 90
        /// 4. A table that holds its TABLECONTENT has the values of it in the new form and loses the part of its round trip that
        /// holds the cells of the form before R2007 (ACAD_ROUNDTRIP_PRE2007_TABLE, with its data table); one that holds none loses
        /// the extension dictionary of the round trip (ACAD_XREC_ROUNDTRIP). False, the table left as it is, where a cell holds more
        /// than text or a block, or an override the new form is not known to flag (a border's).
        /// </summary>
        private bool TableUp(Record table)
        {
            if (!Cells(table, out List<Group> groups, out int first, out int stop, out List<int> starts))
            {
                return false;
            }

            var made = new List<Group>(groups.GetRange(0, first));

            for (int i = 0; i < starts.Count; i++)
            {
                int to = i + 1 < starts.Count ? starts[i + 1] : stop;

                if (!CellUp(groups.GetRange(starts[i], to - starts[i]), made))
                {
                    return false;
                }
            }

            var changes = new List<Action>();
            bool content = HasContent(table);

            if (!ContentUp(table, changes))
            {
                return false;
            }

            made.AddRange(groups.GetRange(stop, groups.Count - stop));
            table.Groups = made;

            foreach (Action change in changes)
            {
                change();
            }

            if (!content)
            {
                ForgetRoundTrip(table);
            }

            return true;
        }

        /// <summary>
        /// Gives one cell of the new form its old form, written to <paramref name="made"/>: its head with 177 for 91, its text (1,
        /// empty where it has none; a block cell has none), then the groups it holds before its value and after it (the overrides,
        /// the block and its attributes). False where the cell is not made of these: a 92 of a link, a field, a value that is neither
        /// text nor empty, or groups after the value of the last cell, which may be the table's own.
        /// </summary>
        private bool CellDown(List<Group> cell, bool last, List<Group> made, List<CellData> data)
        {
            if (!Has(cell, NewCellHead))
            {
                return false;
            }

            int at = cell.FindIndex(NewCellHead.Length, g => g.Code == 301 && g.Value == "CELL_VALUE");
            int end = at < 0 ? -1 : cell.FindIndex(at, g => g.Code == 304 && g.Value == ValueEnd);

            if (
                end < 0
                || (last && end + 1 < cell.Count)
                || !int.TryParse(
                    cell[6].Value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int flags
                )
            )
            {
                return false;
            }

            // What the cell holds besides its value: its groups before it but the 92 (a link where it is not nought) and those after.
            var held = new List<Group>();

            for (int i = NewCellHead.Length; i < at; i++)
            {
                if (cell[i].Code == 92 && cell[i].Value.Trim() != "0")
                {
                    return false;
                }

                if (cell[i].Code != 92)
                {
                    held.Add(cell[i]);
                }
            }

            held.AddRange(cell.GetRange(end + 1, cell.Count - end - 1));

            if (held.Exists(g => g.Code == 344 || g.Code >= 1000))
            {
                return false;
            }

            bool block = cell[0].Value.Trim() == "2";
            var text = new List<Group>();

            if (!Value(cell, at + 1, end, out int type, text, out string whole) || (!block && type != 0 && type != 4))
            {
                return false;
            }

            for (int g = 0; g < 6; g++)
            {
                made.Add(cell[g]);
            }

            made.Add(Whole(177, (short)(flags & 0xFFFF)));
            made.Add(cell[7]);
            made.Add(cell[8]);

            if (!block)
            {
                // A text cut into chunks of 250 characters is in 2 before its last part from R2007, and in 3 before R2007.
                foreach (Group part in text)
                {
                    made.Add(part.Code == 2 ? new Group(3, part.Value) : part);
                }

                if (text.Count == 0)
                {
                    made.Add(new Group(1, string.Empty));
                }
            }

            made.AddRange(held);
            data.Add(new CellData(flags & 0xFFFF, whole, block, type == 4 || text.Count > 0));
            return true;
        }

        /// <summary>
        /// Gives one cell of the old form its new form, written to <paramref name="made"/>. False where the cell has an override the
        /// new form is not known to flag (a border's), a group that is none of those of a text or a block cell, or text in several
        /// groups.
        /// </summary>
        private bool CellUp(List<Group> cell, List<Group> made)
        {
            if (
                !Has(cell, OldCellHead)
                || !int.TryParse(
                    cell[6].Value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int flags
                )
            )
            {
                return false;
            }

            bool block = cell[0].Value.Trim() == "2";
            int from = OldCellHead.Length;
            string text = string.Empty;

            if (!block)
            {
                // A text longer than 250 characters is cut into chunks in 3 and a last part in 1.
                var all = new StringBuilder();

                while (from < cell.Count && cell[from].Code == 3)
                {
                    all.Append(cell[from++].Value);
                }

                if (from >= cell.Count || cell[from].Code != 1)
                {
                    return false;
                }

                all.Append(cell[from++].Value);
                text = all.ToString();
            }

            for (int i = from; i < cell.Count; i++)
            {
                if (!UpCellGroups.Contains(cell[i].Code))
                {
                    return false;
                }
            }

            bool has = block || text.Length > 0;

            for (int g = 0; g < 6; g++)
            {
                made.Add(cell[g]);
            }

            // The 0x40000 of a cell with text is the cell's content; a block cell has none (AutoCAD 2021 writes 91 as 0 for it, with an
            // attribute value or without), and a cell with a border of its own on the right has it too, text or none. The top border's
            // flag has no copy four bits up (that bit is the content's), nor the cell with only a top border: 17 472 stays 17 472.
            flags &= 0xFFFF;
            bool content = (!block && text.Length > 0) || (flags & 0x8800) != 0;
            made.Add(Whole(91, flags | (content ? ContentBit : 0) | ((flags & 0x3000) << 4)));
            made.Add(cell[7]);
            made.Add(cell[8]);
            made.AddRange(cell.GetRange(from, cell.Count - from));
            made.Add(Whole(92, 0));
            made.Add(new Group(301, "CELL_VALUE"));
            made.Add(
                Whole(
                    93,
                    block ? 1
                        : has ? 6
                        : 7
                )
            );
            made.Add(Whole(90, has ? 4 : 0));

            if (!block && has)
            {
                foreach (Group part in Chunked(text, 2))
                {
                    made.Add(part);
                }
            }

            made.Add(Whole(94, 0));
            made.Add(new Group(300, string.Empty));

            foreach (Group part in Chunked(text, 303))
            {
                made.Add(part);
            }

            made.Add(new Group(304, ValueEnd));
            return true;
        }

        /// <summary>
        /// The groups of a text as AutoCAD writes it in a cell of R2007 and later: the chunks of 250 characters of a text that
        /// is longer in <paramref name="chunk"/> (2, or 303 in the second copy of the value), then its last part in 1 (302 in the
        /// copy); a short text is that last part alone.
        /// </summary>
        private static List<Group> Chunked(string text, int chunk)
        {
            var groups = new List<Group>();
            int at = 0;

            for (; text.Length - at > 250; at += 250)
            {
                groups.Add(new Group(chunk, text.Substring(at, 250)));
            }

            groups.Add(new Group(chunk == 2 ? 1 : 302, text.Substring(at)));
            return groups;
        }

        /// <summary>Whether the groups of a cell begin with the codes of a form's head, in its order.</summary>
        private static bool Has(List<Group> cell, int[] head)
        {
            if (cell.Count < head.Length)
            {
                return false;
            }

            for (int i = 0; i < head.Length; i++)
            {
                if (cell[i].Code != head[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads the value of a cell, the groups from <paramref name="from"/> up to <paramref name="end"/> (the 304): its flags (93)
        /// and type (90), the groups of its text where it has any (the chunks of 250 characters of a longer one in 2, then the last
        /// part in 1), then 94, 300, the value again (the chunks in 303, the last part in 302). <paramref name="whole"/> is the
        /// text in one piece. False where it is of another shape.
        /// </summary>
        private static bool Value(List<Group> cell, int from, int end, out int type, List<Group> text, out string whole)
        {
            type = -1;
            whole = string.Empty;

            if (
                end - from < 5
                || cell[from].Code != 93
                || cell[from + 1].Code != 90
                || !int.TryParse(
                    cell[from + 1].Value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out type
                )
            )
            {
                return false;
            }

            var all = new StringBuilder();
            int i = from + 2;

            for (; i < end && (cell[i].Code == 1 || cell[i].Code == 2 || cell[i].Code == 3); i++)
            {
                text.Add(cell[i]);
                all.Append(cell[i].Value);
            }

            if (i + 2 >= end || cell[i].Code != 94 || cell[i + 1].Code != 300)
            {
                return false;
            }

            for (i += 2; i < end && cell[i].Code == 303; i++) { }

            if (i != end - 1 || cell[i].Code != 302)
            {
                return false;
            }

            whole = all.ToString();
            return true;
        }

        /// <summary>
        /// Where the cells of a table stand: the groups of the record, the index of the first cell (its first 171), the index past
        /// the last (the next subclass or the extended data), and where each cell starts. False where the table has no cell or
        /// groups follow its last that are not the next subclass.
        /// </summary>
        private static bool Cells(
            Record table,
            out List<Group> groups,
            out int first,
            out int stop,
            out List<int> starts
        )
        {
            groups = table.Groups;
            starts = new List<int>();
            first = -1;
            stop = groups.Count;

            if (!Subclass(groups, "AcDbTable", out int start, out int end))
            {
                return false;
            }

            stop = end;

            for (int i = start + 1; i < end; i++)
            {
                if (groups[i].Code == 171)
                {
                    if (first < 0)
                    {
                        first = i;
                    }

                    starts.Add(i);
                }
            }

            return starts.Count > 0;
        }

        #endregion

        #region The cells of a table, kept for the old releases

        /// <summary>What the data table of a cell keeps: the low 16 bits of its flags, its text, and whether it is a block or has a text.</summary>
        private sealed class CellData
        {
            internal CellData(int flags, string text, bool block, bool hasText)
            {
                Flags = flags;
                Text = text;
                Block = block;
                HasText = hasText;
            }

            internal int Flags { get; }

            internal string Text { get; }

            internal bool Block { get; }

            internal bool HasText { get; }
        }

        // The sides of a cell in the mask of its data table, as the flags of its borders are in 91 (the groups 279 and 69 are the top
        // 0x4440, 278 and 68 the left 0x2200 and 0x20000 more, 275 and 65 the right 0x8880, 276 and 66 the bottom 0x1100 and 0x10000
        // more). The cell across an edge that has an override of its own holds the opposite side.
        private const int MaskTop = 0x4440;
        private const int MaskBottom = 0x11100;
        private const int MaskLeft = 0x22200;
        private const int MaskRight = 0x8880;

        /// <summary>The number in the group of the code (91 rows, 92 columns) that stands after the table's <c>AcDbTable</c> marker and before its cells; 0 where there is none.</summary>
        private static int Count(List<Group> groups, int first, int code)
        {
            int table = groups.FindIndex(0, first, g => g.Code == 100 && g.Value == "AcDbTable");

            if (table < 0)
            {
                return 0;
            }

            int at = groups.FindIndex(table, first - table, g => g.Code == code);

            return
                at >= 0
                && int.TryParse(
                    groups[at].Value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int count
                )
                && count > 0
                ? count
                : 0;
        }

        /// <summary>
        /// The mask of what each cell overrides that AutoCAD keeps in its data table: its alignment, background colour and fill (the
        /// low three flags), the sides that have a border of their own and, on the cell across each such side, the opposite one.
        /// </summary>
        private static int[] Masks(List<CellData> cells, int rows, int columns)
        {
            var masks = new int[cells.Count];

            for (int i = 0; i < cells.Count; i++)
            {
                int flags = cells[i].Flags;
                int row = i / columns;
                int column = i % columns;
                masks[i] |= flags & 7;

                if ((flags & 0x4440) != 0)
                {
                    masks[i] |= MaskTop;

                    if (row > 0)
                    {
                        masks[i - columns] |= MaskBottom;
                    }
                }

                if ((flags & 0x1100) != 0)
                {
                    masks[i] |= MaskBottom;

                    if (row < rows - 1)
                    {
                        masks[i + columns] |= MaskTop;
                    }
                }

                if ((flags & 0x2200) != 0)
                {
                    masks[i] |= MaskLeft;

                    if (column > 0)
                    {
                        masks[i - 1] |= MaskRight;
                    }
                }

                if ((flags & 0x8880) != 0)
                {
                    masks[i] |= MaskRight;

                    if (column < columns - 1)
                    {
                        masks[i + 1] |= MaskLeft;
                    }
                }
            }

            return masks;
        }

        /// <summary>
        /// Keeps the cells of a table that went to R2000 or R2004 as AutoCAD does, to give the old releases the cells they know: the
        /// round trip <c>ACAD_ROUNDTRIP_PRE2007_TABLE</c> (the rows and columns, and a pointer to the data table) in the XRECORD
        /// <c>ACAD_XREC_ROUNDTRIP</c> of the table's extension dictionary, made where the table has none, and the data table with a row
        /// for each cell: a checksum of its text (the sum of its characters, each times its place from 1), its mask, 0, and an XRECORD
        /// that holds its value. Nothing is made where the drawing has no OBJECTS or no handle to give.
        /// </summary>
        private void KeepCells(Record table, List<CellData> cells, int rows, int columns)
        {
            Section objects = Find("OBJECTS");
            string owner = HandleOf(table);

            if (objects == null || owner == null || (table.Groups.Count > 1 && table.Groups[1].Code != 5))
            {
                return;
            }

            // Where the round trip goes: the XRECORD the table has under its extension dictionary, one made in a dictionary that has
            // none, or both made with the dictionary.
            string xrecord = null;
            Record keeper = null;
            Record dictionary = null;
            int at = table.Groups.FindIndex(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY");

            if (at >= 0 && at + 1 < table.Groups.Count && table.Groups[at + 1].Code == 360)
            {
                dictionary = ObjectAt(table.Groups[at + 1].Value);

                if (dictionary == null || dictionary.Type != "DICTIONARY")
                {
                    return;
                }
            }

            if (RoundTrip(table, out Record found, out _))
            {
                if (found.Groups.Exists(g => g.Code == 102 && g.Value == "ACAD_ROUNDTRIP_PRE2007_TABLE"))
                {
                    return;
                }

                keeper = found;
                xrecord = HandleOf(found);

                if (xrecord == null)
                {
                    return;
                }
            }

            if (keeper == null)
            {
                string dictionaryHandle = dictionary == null ? NewHandle() : HandleOf(dictionary);
                xrecord = NewHandle();

                if (dictionaryHandle == null || xrecord == null)
                {
                    return;
                }

                keeper = new Record();
                keeper.Groups.AddRange(
                    new[]
                    {
                        new Group(0, "XRECORD"),
                        new Group(5, xrecord),
                        new Group(102, "{ACAD_REACTORS"),
                        new Group(330, dictionaryHandle),
                        new Group(102, "}"),
                        new Group(330, dictionaryHandle),
                        new Group(100, "AcDbXrecord"),
                        Whole(280, 1),
                    }
                );
                _added.Add(keeper);

                if (dictionary == null)
                {
                    var made = new Record();
                    made.Groups.AddRange(
                        new[]
                        {
                            new Group(0, "DICTIONARY"),
                            new Group(5, dictionaryHandle),
                            new Group(330, owner),
                            new Group(100, "AcDbDictionary"),
                            Whole(280, 1),
                            Whole(281, 1),
                            new Group(3, "ACAD_XREC_ROUNDTRIP"),
                            new Group(360, xrecord),
                        }
                    );
                    _added.Add(made);
                    table.Groups.InsertRange(
                        2,
                        new[]
                        {
                            new Group(102, "{ACAD_XDICTIONARY"),
                            new Group(360, dictionaryHandle),
                            new Group(102, "}"),
                        }
                    );
                }
                else
                {
                    dictionary.Groups.Add(new Group(3, "ACAD_XREC_ROUNDTRIP"));
                    dictionary.Groups.Add(new Group(360, xrecord));
                }
            }

            string data = NewHandle();

            if (data == null)
            {
                return;
            }

            keeper.Groups.Add(new Group(102, "ACAD_ROUNDTRIP_PRE2007_TABLE"));
            keeper.Groups.Add(Whole(90, rows));
            keeper.Groups.Add(Whole(91, columns));
            keeper.Groups.Add(new Group(102, "ACAD_ROUNDTRIP_PRE2007_TABLECELL"));
            keeper.Groups.Add(new Group(360, data));
            AddDataTable(data, xrecord, cells, Masks(cells, rows, columns));
        }

        /// <summary>
        /// The data table of a table's cells and the XRECORDs of their values, put among the objects made: the table is named
        /// <c>ACDBDATATABLE</c> (in the subclass of the same name) in R2000 and <c>DATATABLE</c> (<c>AcDbDataTable</c>) from R2004,
        /// and has four columns: a real (the checksum), two whole numbers (the mask, and 0) and a handle (the value).
        /// </summary>
        private void AddDataTable(string handle, string owner, List<CellData> cells, int[] masks)
        {
            bool old = _target < 1018;
            var values = new List<string>(cells.Count);

            foreach (CellData cell in cells)
            {
                string value = NewHandle();

                if (value == null)
                {
                    return;
                }

                values.Add(value);
            }

            var table = new Record();
            table.Groups.AddRange(
                new[]
                {
                    new Group(0, old ? "ACDBDATATABLE" : "DATATABLE"),
                    new Group(5, handle),
                    new Group(330, owner),
                    new Group(100, old ? "ACDBDATATABLE" : "AcDbDataTable"),
                    Whole(70, 2),
                    Whole(90, 4),
                    Whole(91, cells.Count),
                    new Group(1, string.Empty),
                    Whole(92, 2),
                    new Group(2, string.Empty),
                }
            );

            foreach (CellData cell in cells)
            {
                long sum = 0;

                for (int i = 0; i < cell.Text.Length; i++)
                {
                    sum += (i + 1) * (long)cell.Text[i];
                }

                table.Groups.Add(new Group(40, sum.ToString(CultureInfo.InvariantCulture) + ".0"));
            }

            table.Groups.Add(Whole(92, 1));
            table.Groups.Add(new Group(2, string.Empty));

            foreach (int mask in masks)
            {
                table.Groups.Add(Whole(93, mask));
            }

            table.Groups.Add(Whole(92, 1));
            table.Groups.Add(new Group(2, string.Empty));

            foreach (CellData cell in cells)
            {
                table.Groups.Add(Whole(93, 0));
            }

            table.Groups.Add(Whole(92, 6));
            table.Groups.Add(new Group(2, string.Empty));

            foreach (string value in values)
            {
                table.Groups.Add(new Group(360, value));
            }

            _added.Add(table);

            for (int i = 0; i < cells.Count; i++)
            {
                CellData cell = cells[i];
                var record = new Record();
                record.Groups.AddRange(
                    new[]
                    {
                        new Group(0, "XRECORD"),
                        new Group(5, values[i]),
                        new Group(330, handle),
                        new Group(100, "AcDbXrecord"),
                        Whole(280, 1),
                    }
                );

                // A block holds no value, a text its whole text (in one group, as the checksum is of it), a cell with none is not set.
                record.Groups.Add(
                    Whole(
                        93,
                        cell.Block ? 3
                            : cell.HasText ? 6
                            : 7
                    )
                );
                record.Groups.Add(Whole(90, cell.HasText && !cell.Block ? 4 : 0));

                if (cell.HasText && !cell.Block)
                {
                    record.Groups.Add(new Group(1, cell.Text));
                }

                record.Groups.Add(Whole(94, 0));
                record.Groups.Add(new Group(300, string.Empty));
                record.Groups.Add(new Group(302, cell.HasText && !cell.Block ? cell.Text : string.Empty));
                _added.Add(record);
            }
        }

        #endregion

        #region The content of a table, kept under its round trip

        private Dictionary<string, Record> _byHandle;

        /// <summary>The object of the OBJECTS section that has the handle and is still there; null where there is none.</summary>
        private Record ObjectAt(string handle)
        {
            if (_byHandle == null)
            {
                _byHandle = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);

                foreach (Record record in Find("OBJECTS")?.Records ?? new List<Record>())
                {
                    string own = HandleOf(record);

                    if (own != null)
                    {
                        _byHandle[own] = record;
                    }
                }
            }

            return _byHandle.TryGetValue(handle.Trim(), out Record found) && !found.Dropped ? found : null;
        }

        /// <summary>
        /// The XRECORD a table keeps its round trip in (ACAD_XREC_ROUNDTRIP of its extension dictionary), and where its
        /// <c>ACAD_ROUNDTRIP_2008_TABLE_ENTITY</c> stands in it (-1 where it does not name a table content). False where the table
        /// has no such record.
        /// </summary>
        private bool RoundTrip(Record table, out Record xrecord, out int entity)
        {
            xrecord = null;
            entity = -1;
            int at = table.Groups.FindIndex(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY");

            if (at < 0 || at + 1 >= table.Groups.Count || table.Groups[at + 1].Code != 360)
            {
                return false;
            }

            Record dictionary = ObjectAt(table.Groups[at + 1].Value);

            if (dictionary == null || dictionary.Type != "DICTIONARY")
            {
                return false;
            }

            int entry = dictionary.Groups.FindIndex(g => g.Code == 3 && g.Value == "ACAD_XREC_ROUNDTRIP");

            if (entry < 0 || entry + 1 >= dictionary.Groups.Count)
            {
                return false;
            }

            xrecord = ObjectAt(dictionary.Groups[entry + 1].Value);

            if (xrecord == null || xrecord.Type != "XRECORD")
            {
                return false;
            }

            entity = xrecord.Groups.FindIndex(g => g.Code == 102 && g.Value == "ACAD_ROUNDTRIP_2008_TABLE_ENTITY");
            return true;
        }

        /// <summary>The TABLECONTENT the round trip of a table points at (the first 360 after its entity); null where there is none.</summary>
        private Record ContentOf(Record xrecord, int entity)
        {
            int at = xrecord.Groups.FindIndex(entity + 1, g => g.Code == 360);
            Record content = at < 0 ? null : ObjectAt(xrecord.Groups[at].Value);
            return content != null && content.Type == "TABLECONTENT" ? content : null;
        }

        /// <summary>Whether the table keeps a TABLECONTENT under its round trip.</summary>
        private bool HasContent(Record table) =>
            RoundTrip(table, out Record xrecord, out int entity) && entity >= 0 && ContentOf(xrecord, entity) != null;

        /// <summary>
        /// Puts the values of the table's TABLECONTENT in the form of R2004 and before, as a change to make once the whole table is
        /// known to be converted: the flags (93) before each value's type are left out, and the 94, 300, 302 and ACVALUE_END after
        /// its data. False where a value is of a shape that is not known.
        /// </summary>
        private bool ContentDown(Record table, List<Action> changes)
        {
            if (!RoundTrip(table, out Record xrecord, out int entity) || entity < 0)
            {
                return true;
            }

            Record content = ContentOf(xrecord, entity);

            if (content == null)
            {
                return true;
            }

            List<Group> groups = content.Groups;
            var made = new List<Group>(groups.Count);

            for (int i = 0; i < groups.Count; i++)
            {
                made.Add(groups[i]);

                if (
                    !IsValueMark(groups[i])
                    || i + 2 >= groups.Count
                    || groups[i + 1].Code != 93
                    || groups[i + 2].Code != 90
                )
                {
                    continue;
                }

                int end = groups.FindIndex(i + 3, g => g.Code == 304 && g.Value == ValueEnd);

                if (end < 0 || groups[end - 3].Code != 94 || groups[end - 2].Code != 300 || groups[end - 1].Code != 302)
                {
                    return false;
                }

                made.AddRange(groups.GetRange(i + 2, end - 3 - (i + 2)));
                i = end;
            }

            changes.Add(() => content.Groups = made);
            return true;
        }

        /// <summary>
        /// Puts the values of the table's TABLECONTENT in the form of R2007 and later (the opposite of
        /// <see cref="ContentDown"/>), and takes the part of the round trip that holds the cells of the form before it (its data
        /// table, with what it owns) out. False where a value is of a kind that is not known: a text and a number are.
        /// </summary>
        private bool ContentUp(Record table, List<Action> changes)
        {
            if (!RoundTrip(table, out Record xrecord, out int entity) || entity < 0)
            {
                return true;
            }

            Record content = ContentOf(xrecord, entity);

            if (content == null)
            {
                return true;
            }

            List<Group> groups = content.Groups;
            var made = new List<Group>(groups.Count + 64);

            for (int i = 0; i < groups.Count; i++)
            {
                made.Add(groups[i]);

                if (!IsValueMark(groups[i]) || i + 2 >= groups.Count || groups[i + 1].Code != 90)
                {
                    continue;
                }

                string type = groups[i + 1].Value.Trim();
                bool text = type == "4" && groups[i + 2].Code == 1;

                if (!text && !(type == "2" && groups[i + 2].Code == 140))
                {
                    return false;
                }

                made.Add(Whole(93, text ? 6 : 2));
                made.Add(groups[i + 1]);
                made.Add(groups[i + 2]);
                made.Add(Whole(94, 0));
                made.Add(new Group(300, string.Empty));
                made.Add(new Group(302, text ? groups[i + 2].Value : string.Empty));
                made.Add(new Group(304, ValueEnd));
                i += 2;
            }

            changes.Add(() => content.Groups = made);

            // Every part of the round trip of the form before R2007 goes (a table has one), with the data table it points at.
            var parts = new List<int>();

            for (
                int part = xrecord.Groups.FindIndex(g => g.Code == 102 && g.Value == "ACAD_ROUNDTRIP_PRE2007_TABLE");
                part >= 0;
                part = xrecord.Groups.FindIndex(part + 1, g =>
                        g.Code == 102 && g.Value == "ACAD_ROUNDTRIP_PRE2007_TABLE"
                    )
            )
            {
                if (
                    part + 4 < xrecord.Groups.Count
                    && xrecord.Groups[part + 3].Code == 102
                    && xrecord.Groups[part + 3].Value == "ACAD_ROUNDTRIP_PRE2007_TABLECELL"
                    && xrecord.Groups[part + 4].Code == 360
                )
                {
                    parts.Add(part);
                }
            }

            if (parts.Count > 0)
            {
                changes.Add(() =>
                {
                    for (int i = parts.Count - 1; i >= 0; i--)
                    {
                        _owning.Add(xrecord.Groups[parts[i] + 4].Value.Trim());
                        xrecord.Groups.RemoveRange(parts[i], 5);
                    }
                });
            }

            return true;
        }

        /// <summary>Whether a group opens the value of a cell, or of an entry of its custom data, in a TABLECONTENT.</summary>
        private static bool IsValueMark(Group group) =>
            (group.Code == 300 && group.Value == "VALUE") || (group.Code == 301 && group.Value == "DATAMAP_VALUE");

        /// <summary>
        /// Takes the extension dictionary of a table that holds the round trip of the form before R2007 (ACAD_XREC_ROUNDTRIP and
        /// nothing else) out of the record, and the objects it owns with it.
        /// </summary>
        private void ForgetRoundTrip(Record table)
        {
            int at = table.Groups.FindIndex(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY");

            if (at < 0 || at + 2 >= table.Groups.Count || table.Groups[at + 1].Code != 360)
            {
                return;
            }

            string handle = table.Groups[at + 1].Value.Trim();
            Section objects = Find("OBJECTS");
            Record dictionary = null;

            foreach (Record record in objects?.Records ?? new List<Record>())
            {
                if (
                    record.Type == "DICTIONARY"
                    && string.Equals(HandleOf(record), handle, StringComparison.OrdinalIgnoreCase)
                )
                {
                    dictionary = record;
                    break;
                }
            }

            if (dictionary == null)
            {
                return;
            }

            foreach (Group group in dictionary.Groups)
            {
                if (group.Code == 3 && group.Value != "ACAD_XREC_ROUNDTRIP")
                {
                    return;
                }
            }

            _owning.Add(handle);
            table.Groups.RemoveRange(at, 3);
        }

        #endregion
    }
}
