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
        /// of its extension dictionary, ACAD_ROUNDTRIP_2008_TABLE_ENTITY) keeps it with the values in the form of R2004. A number,
        /// a general value, a date, a point and a field are in the cell as the words they show and the field's handle (see
        /// <see cref="CellDown"/>). False, the table left as it is, where a cell holds anything but those, text or a block (a value
        /// of another kind): the caller then draws the table as the block it is drawn with.
        /// </summary>
        private bool TableDown(Record table)
        {
            if (!Cells(table, out List<Group> groups, out int first, out int stop, out List<int> starts))
            {
                return false;
            }

            var made = new List<Group>(groups.GetRange(0, first));
            made.Capacity = Math.Max(made.Capacity, groups.Count);
            var data = new List<CellData>(starts.Count);
            var cell = new List<Group>(64);
            var text = new List<Group>(2);

            for (int i = 0; i < starts.Count; i++)
            {
                int to = i + 1 < starts.Count ? starts[i + 1] : stop;
                cell.Clear();

                for (int g = starts[i]; g < to; g++)
                {
                    cell.Add(groups[g]);
                }

                text.Clear();

                if (!CellDown(cell, i + 1 == starts.Count, made, data, text))
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
        /// the block and its attributes). A number (a whole number or a real), a date, a point (2D or 3D) or a general value is
        /// written as the text it shows (the 302 of its value: <c>12</c>, <c>2.500000</c>, <c>11/26/2026</c>, nothing), a field (344)
        /// as its handle where the words would stand. False where the cell is not made of these: a 92 of a link, a value that is
        /// none of those, or groups after the value of the last cell, which may be the table's own.
        /// </summary>
        private bool CellDown(List<Group> cell, bool last, List<Group> made, List<CellData> data, List<Group> text)
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
            int field = -1;
            int dangling = -1;

            for (int i = NewCellHead.Length; i < at; i++)
            {
                if (cell[i].Code == 92)
                {
                    if (cell[i].Value.Trim() != "0")
                    {
                        return false;
                    }
                }
                else if (cell[i].Code == 344 && field < 0 && dangling < 0)
                {
                    // A field the drawing does not hold is no field: AutoCAD keeps none of it either and writes the cell with no
                    // words (the handle would be a null one in the file, and the cell would then be read as one with words).
                    if (ObjectAt(cell[i].Value)?.Type == "FIELD")
                    {
                        field = i;
                    }
                    else
                    {
                        dangling = i;
                    }
                }
                else if (cell[i].Code == 344 || cell[i].Code >= 1000)
                {
                    return false;
                }
            }

            for (int i = end + 1; i < cell.Count; i++)
            {
                if (cell[i].Code == 344 || cell[i].Code >= 1000)
                {
                    return false;
                }
            }

            bool block = cell[0].Value.Trim() == "2";

            if (
                !Value(cell, at + 1, end, out int type, text, out string whole, out List<Group> kept)
                || (!block && field < 0 && type != 0 && type != 4 && kept == null)
            )
            {
                return false;
            }

            if (block)
            {
                kept = null;
            }

            for (int g = 0; g < 6; g++)
            {
                made.Add(cell[g]);
            }

            made.Add(Whole(177, (short)(flags & 0xFFFF)));
            made.Add(cell[7]);
            made.Add(cell[8]);

            if (field >= 0)
            {
                made.Add(cell[field]);
            }
            else if (!block)
            {
                // A text cut into chunks of 250 characters is in 2 before its last part from R2007, and in 3 before R2007.
                foreach (Group part in text)
                {
                    made.Add(part.Code == 2 ? new Group(3, part.Value) : part);
                }

                // A number or a general value shows the text its 302 holds.
                if (kept != null)
                {
                    made.Add(new Group(1, whole));
                }
                else if (text.Count == 0)
                {
                    made.Add(new Group(1, string.Empty));
                }
            }

            for (int i = NewCellHead.Length; i < at; i++)
            {
                if (cell[i].Code != 92 && i != field && i != dangling)
                {
                    made.Add(cell[i]);
                }
            }

            for (int i = end + 1; i < cell.Count; i++)
            {
                made.Add(cell[i]);
            }

            data.Add(
                new CellData(
                    flags & 0xFFFF,
                    field >= 0 ? string.Empty : whole,
                    block,
                    field < 0 && (type == 4 || text.Count > 0),
                    field < 0 ? kept : FieldValue(cell[field].Value),
                    MaskOverrides(cell, NewCellHead.Length, at, end)
                )
            );
            return true;
        }

        /// <summary>
        /// Gives one cell of the old form its new form, written to <paramref name="made"/>. False where the cell has an override the
        /// new form is not known to flag (a border's), a group that is none of those of a text or a block cell, or text in several
        /// groups.
        /// </summary>
        private bool CellUp(List<Group> cell, List<Group> made)
        {
            // The reader of a DWG gives the overrides of a cell between its state (178) and its rotation (145), AutoCAD's DXF after
            // its words: put them there.
            int rotation = cell.FindIndex(g => g.Code == 145);

            if (rotation > OldCellHead.Length - 1 && cell.Count >= OldCellHead.Length)
            {
                var ordered = new List<Group>(cell.GetRange(0, OldCellHead.Length - 1));
                ordered.AddRange(cell.GetRange(rotation, cell.Count - rotation));
                ordered.AddRange(cell.GetRange(OldCellHead.Length - 1, rotation - (OldCellHead.Length - 1)));
                cell = ordered;
            }

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
        /// text in one piece. A whole number (type 1, its 91), a real (type 2, its 140), a date (type 8, its size in 92 and its
        /// bytes in 310), a point (type 16, 10 and 20; type 32, 11, 21 and 31) and a general value (type 512, nothing)
        /// hold no text: <paramref name="whole"/> is the text they show (the 302) and <paramref name="kept"/> their groups from
        /// the flags (93) up to the 302, null for any other. False where it is of another shape.
        /// </summary>
        private static bool Value(
            List<Group> cell,
            int from,
            int end,
            out int type,
            List<Group> text,
            out string whole,
            out List<Group> kept
        )
        {
            type = -1;
            whole = string.Empty;
            kept = null;

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

            int i = from + 2;

            for (; i < end && (cell[i].Code == 1 || cell[i].Code == 2 || cell[i].Code == 3); i++)
            {
                text.Add(cell[i]);
            }

            int data = text.Count == 0 ? DataEnd(cell, i, end, type) : i;
            bool number = data > i;
            i = data;

            if (i + 2 >= end || cell[i].Code != 94 || cell[i + 1].Code != 300)
            {
                return false;
            }

            for (i += 2; i < end && cell[i].Code == 303; i++) { }

            if (i != end - 1 || cell[i].Code != 302)
            {
                return false;
            }

            if (number || (type == 512 && text.Count == 0))
            {
                kept = cell.GetRange(from, end - from);
                whole = cell[i].Value;
            }
            else if (text.Count == 1)
            {
                whole = text[0].Value;
            }
            else if (text.Count > 1)
            {
                var all = new StringBuilder();

                foreach (Group part in text)
                {
                    all.Append(part.Value);
                }

                whole = all.ToString();
            }

            return true;
        }

        /// <summary>
        /// The index past the data of a value of <paramref name="type"/> that stands at <paramref name="at"/>: a whole number (1: 91),
        /// a real (2: 140), a date (8: its size in 92 and its bytes in 310), a 2D point (16: 10 and 20) or a 3D point (32: 11, 21 and
        /// 31); <paramref name="at"/> where the value has none of those.
        /// </summary>
        private static int DataEnd(List<Group> groups, int at, int end, int type)
        {
            int[] codes;

            switch (type)
            {
                case 1:
                    codes = new[] { 91 };
                    break;
                case 2:
                    codes = new[] { 140 };
                    break;
                case 8:
                    codes = new[] { 92, 310 };
                    break;
                case 16:
                    codes = new[] { 10, 20 };
                    break;
                case 32:
                    codes = new[] { 11, 21, 31 };
                    break;
                default:
                    return at;
            }

            int i = at;

            foreach (int code in codes)
            {
                if (i >= end || groups[i].Code != code)
                {
                    return at;
                }

                i++;
            }

            return i;
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

        /// <summary>
        /// What the data table of a cell keeps: the low 16 bits of its flags, its text, whether it is a block or has a text, for a
        /// number or a general value the groups of its value (from the flags 93 to the shown text 302), and the values it
        /// overrides that the mask counts (<see cref="Overrides"/>).
        /// </summary>
        private sealed class CellData
        {
            internal CellData(
                int flags,
                string text,
                bool block,
                bool hasText,
                List<Group> value,
                Dictionary<int, string> overrides
            )
            {
                Flags = flags;
                Text = text;
                Block = block;
                HasText = hasText;
                Value = value;
                Overrides = overrides;
            }

            internal List<Group> Value { get; }

            /// <summary>The alignment (170), fill (283), background colour (63) and the colour, lineweight and visibility of each border the cell overrides, by group.</summary>
            internal Dictionary<int, string> Overrides { get; }

            internal int Flags { get; }

            internal string Text { get; }

            internal bool Block { get; }

            internal bool HasText { get; }
        }

        // The borders of a cell, top, right, bottom and left: the groups of the colour, lineweight and visibility it overrides in the
        // cell (69 279 289, 65 275 285, 66 276 286, 68 278 288), and the bits each has in the mask of its data table (the colour 0x40
        // to 0x200, the lineweight 0x400 to 0x2000, the visibility 0x4000 to 0x20000, side after side).
        private static readonly int[][] BorderGroups =
        {
            new[] { 69, 279, 289 },
            new[] { 65, 275, 285 },
            new[] { 66, 276, 286 },
            new[] { 68, 278, 288 },
        };

        // The bits of each side in the mask (colour, lineweight, visibility together), and the flags of the cell that name the side
        // (the third of its bits, four places up, is beyond the 16 bits of the flags of R2000 for the bottom and the left).
        private static readonly int[] SideMask = { 0x4440, 0x8880, 0x11100, 0x22200 };

        private static readonly int[] SideFlags = { 0x4440, 0x8880, 0x1100, 0x2200 };

        /// <summary>
        /// The values a cell overrides that the mask of its data table counts, from its groups before its value (from
        /// <paramref name="from"/> to <paramref name="at"/>) and after it (past <paramref name="end"/>): the alignment, fill and
        /// background colour and the three of each border, by group (the first of each).
        /// </summary>
        private static Dictionary<int, string> MaskOverrides(List<Group> cell, int from, int at, int end)
        {
            var found = new Dictionary<int, string>();

            for (int i = from; i < cell.Count; i++)
            {
                if (i == at)
                {
                    i = end;
                    continue;
                }

                int code = cell[i].Code;

                if ((code == 170 || code == 283 || code == 63 || IsBorderGroup(code)) && !found.ContainsKey(code))
                {
                    found.Add(code, cell[i].Value.Trim());
                }
            }

            return found;
        }

        private static bool IsBorderGroup(int code)
        {
            foreach (int[] side in BorderGroups)
            {
                if (Array.IndexOf(side, code) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What the table's own overrides give every cell, and the table style each kind of row (data, title and header, as the
        /// style lists them), by group, to tell what a cell sets itself from what it takes from them (a table with a lineweight of
        /// 0.35 given to its title and a header row has that lineweight in the cells of both where they meet, without their having
        /// set it). Empty where the style is not found or flows upwards.
        /// </summary>
        private sealed class RowDefaults
        {
            internal readonly Dictionary<int, string>[] Kinds =
            {
                new Dictionary<int, string>(),
                new Dictionary<int, string>(),
                new Dictionary<int, string>(),
            };

            internal readonly Dictionary<int, HashSet<string>> Table = new Dictionary<int, HashSet<string>>();

            internal bool NoTitle;

            internal bool NoHeader;

            /// <summary>What the style gives the row: its title at the top (where the style has one), then its header, then its data.</summary>
            internal Dictionary<int, string> KindOfRow(int row)
            {
                int next = 0;

                if (!NoTitle)
                {
                    if (row == 0)
                    {
                        return Kinds[1];
                    }

                    next = 1;
                }

                return !NoHeader && row == next ? Kinds[2] : Kinds[0];
            }

            /// <summary>
            /// Whether a value of a cell is one the table's own overrides give every cell, or, of a border it shares with the row
            /// next to it (<paramref name="across"/>, where it has one), the style gives that row on the same edge
            /// (<paramref name="acrossCode"/>). A value the style gives the cell's own row is not here: the style does not put it in
            /// the cell, so that a cell that holds it set it.
            /// </summary>
            internal bool Gives(int code, string value, Dictionary<int, string> across, int acrossCode)
            {
                if (Table.TryGetValue(code, out HashSet<string> set) && set.Contains(value))
                {
                    return true;
                }

                // The lineweights of the table's overrides stand for every border.
                if (code >= 274 && code <= 279)
                {
                    foreach (int other in new[] { 274, 275, 276, 277, 278, 279 })
                    {
                        if (Table.TryGetValue(other, out HashSet<string> any) && any.Contains(value))
                        {
                            return true;
                        }
                    }
                }

                return across != null && across.TryGetValue(acrossCode, out string given) && given == value;
            }
        }

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
        /// What the table's style and its own overrides (the groups between its flags and its first cell) give each kind of row;
        /// nothing where the table has no style in the drawing or the style flows upwards.
        /// </summary>
        private RowDefaults DefaultsOf(Record table)
        {
            var defaults = new RowDefaults();
            List<Group> groups = table.Groups;
            int start = groups.FindIndex(g => g.Code == 100 && g.Value == "AcDbTable");
            int first = groups.FindIndex(g => g.Code == 171);

            if (start < 0 || first < start)
            {
                return defaults;
            }

            for (int i = start; i < first; i++)
            {
                int code = groups[i].Code;

                if (
                    code == 170
                    || code == 63
                    || code == 65
                    || code == 66
                    || code == 68
                    || code == 69
                    || (code >= 274 && code <= 279)
                )
                {
                    if (!defaults.Table.TryGetValue(code, out HashSet<string> values))
                    {
                        defaults.Table.Add(code, values = new HashSet<string>());
                    }

                    values.Add(groups[i].Value.Trim());
                }
            }

            int named = groups.FindIndex(start, first - start, g => g.Code == 342);
            Record style = named < 0 ? null : ObjectAt(groups[named].Value);

            if (style == null || style.Type != "TABLESTYLE")
            {
                return defaults;
            }

            List<Group> rows = style.Groups;
            int sub = rows.FindIndex(g => g.Code == 100 && g.Value == "AcDbTableStyle");
            int header = sub < 0 ? -1 : rows.FindIndex(sub, g => g.Code == 281);
            int flow = sub < 0 ? -1 : rows.FindIndex(sub, g => g.Code == 70);

            if (header < 1 || flow < 0 || Parse(rows[flow].Value) != 0)
            {
                return defaults;
            }

            defaults.NoTitle = Parse(rows[header - 1].Value) != 0;
            defaults.NoHeader = Parse(rows[header].Value) != 0;
            int kind = -1;

            // The three kinds of row follow, each from its text style (7): data, title, header.
            for (int i = header + 1; i < rows.Count && rows[i].Code != 100 && rows[i].Code < 1000; i++)
            {
                if (rows[i].Code == 7 && ++kind > 2)
                {
                    break;
                }

                if (kind >= 0 && !defaults.Kinds[kind].ContainsKey(rows[i].Code))
                {
                    defaults.Kinds[kind].Add(rows[i].Code, rows[i].Value.Trim());
                }
            }

            return defaults;
        }

        /// <summary>
        /// The mask of what each cell sets itself that AutoCAD keeps in its data table: its alignment (0x01), its background colour
        /// with the fill (0x06), and each side that has a border of its own (the top 0x4440, right 0x8880, bottom 0x11100, left
        /// 0x22200), with the opposite side on the cell across the edge. The content colour, text style and text height are in no
        /// mask. A cell that holds only what the table gives it is set nothing: a value that the table's own overrides give every
        /// cell, or that the style gives the row across the edge where two kinds of row meet (a lineweight under the title that is
        /// also the lineweight over the header), is in the cell's flags and groups as one it set, and AutoCAD's masks of such cells
        /// are 0 (<c>ttable</c>, <c>tstyles</c>, <c>tclean</c>; see <see cref="RowDefaults"/>). AutoCAD keeps the mask of a cell by
        /// what was set on it one property at a time (a lineweight alone sets 0x400 on a top border, a colour 0x40), which the file
        /// does not tell from a border set whole; the whole side is taken, as the cell's flags show it.
        /// </summary>
        private static int[] Masks(List<CellData> cells, int rows, int columns, RowDefaults defaults)
        {
            var masks = new int[cells.Count];

            for (int i = 0; i < cells.Count; i++)
            {
                Dictionary<int, string> set = cells[i].Overrides;
                int row = i / columns;
                int column = i % columns;

                if (set.TryGetValue(170, out string alignment) && !defaults.Gives(170, alignment, null, 0))
                {
                    masks[i] |= 1;
                }

                if (set.TryGetValue(63, out string background) && !defaults.Gives(63, background, null, 0))
                {
                    masks[i] |= 6;
                }

                for (int side = 0; side < 4; side++)
                {
                    // The cell across the side: above, to the right, below, to the left, where there is one.
                    int nextRow =
                        row
                        + (
                            side == 0 ? -1
                            : side == 2 ? 1
                            : 0
                        );
                    int nextColumn =
                        column
                        + (
                            side == 1 ? 1
                            : side == 3 ? -1
                            : 0
                        );
                    bool inside = nextRow >= 0 && nextRow < rows && nextColumn >= 0 && nextColumn < columns;
                    int opposite = (side + 2) & 3;

                    // A side the cell sets holds all three of its bits; one whose values are all those the table gives it (or the
                    // defaults) is not set.
                    bool counted = false;
                    bool given = false;

                    for (int property = 0; property < 3; property++)
                    {
                        int code = BorderGroups[side][property];

                        if (!set.TryGetValue(code, out string value) || Parse(value) == (property == 1 ? -2 : 0))
                        {
                            continue;
                        }

                        if (
                            property < 2
                            && defaults.Gives(
                                code,
                                value,
                                inside ? defaults.KindOfRow(nextRow) : null,
                                BorderGroups[opposite][property]
                            )
                        )
                        {
                            given = true;
                        }
                        else
                        {
                            counted = true;
                        }
                    }

                    if (!counted && (given || (cells[i].Flags & SideFlags[side]) == 0))
                    {
                        continue;
                    }

                    masks[i] |= SideMask[side];

                    if (inside)
                    {
                        masks[nextRow * columns + nextColumn] |= SideMask[opposite];
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
            AddDataTable(data, xrecord, cells, Masks(cells, rows, columns, DefaultsOf(table)));
        }

        /// <summary>
        /// The data table of a table's cells and the XRECORDs of their values, put among the objects made: the table is named
        /// <c>ACDBDATATABLE</c> (in the subclass of the same name) in R2000 and <c>DATATABLE</c> (<c>AcDbDataTable</c>) from R2004,
        /// and has four columns: a real (the checksum), two whole numbers (the mask, and 0) and a handle (the value).
        /// </summary>
        private void AddDataTable(string handle, string owner, List<CellData> cells, int[] masks)
        {
            bool old = _target < 1018;
            string[] values = NewHandles(cells.Count);

            if (values == null)
            {
                return;
            }

            var table = new Record();
            table.Groups.Capacity = 20 + 4 * cells.Count;
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

            // A few masks stand for all the cells: each is formatted once.
            var maskGroups = new Dictionary<int, Group>();

            foreach (int mask in masks)
            {
                if (!maskGroups.TryGetValue(mask, out Group group))
                {
                    maskGroups.Add(mask, group = Whole(93, mask));
                }

                table.Groups.Add(group);
            }

            table.Groups.Add(Whole(92, 1));
            table.Groups.Add(new Group(2, string.Empty));
            Group zero = Whole(93, 0);

            for (int i = 0; i < cells.Count; i++)
            {
                table.Groups.Add(zero);
            }

            table.Groups.Add(Whole(92, 6));
            table.Groups.Add(new Group(2, string.Empty));

            foreach (string value in values)
            {
                table.Groups.Add(new Group(360, value));
            }

            _added.Add(table);

            // The groups every cell's XRECORD shares, made once: a block holds no value (93 of 3), a text its whole text (6, and 90
            // of 4), a cell with none is not set (7).
            var owned = new Group(330, handle);
            var marker = new Group(100, "AcDbXrecord");
            Group version = Whole(280, 1);
            Group blockFlag = Whole(93, 3);
            Group textFlag = Whole(93, 6);
            Group unsetFlag = Whole(93, 7);
            Group textType = Whole(90, 4);
            Group noType = Whole(90, 0);
            Group noUnit = Whole(94, 0);
            var noFormat = new Group(300, string.Empty);
            var noText = new Group(302, string.Empty);
            var recordType = new Group(0, "XRECORD");

            for (int i = 0; i < cells.Count; i++)
            {
                CellData cell = cells[i];
                bool text = cell.HasText && !cell.Block;
                var record = new Record();
                List<Group> groups = record.Groups;
                groups.Add(recordType);
                groups.Add(new Group(5, values[i]));
                groups.Add(owned);
                groups.Add(marker);
                groups.Add(version);

                // A number or a general value is kept as it is: its flags, type, data, unit, format and shown text.
                if (cell.Value != null)
                {
                    groups.AddRange(RecordValue(cell.Value));
                    _added.Add(record);
                    continue;
                }

                groups.Add(
                    cell.Block ? blockFlag
                    : cell.HasText ? textFlag
                    : unsetFlag
                );
                groups.Add(text ? textType : noType);

                if (text)
                {
                    groups.Add(new Group(1, cell.Text));
                }

                groups.Add(noUnit);
                groups.Add(noFormat);
                groups.Add(text ? new Group(302, cell.Text) : noText);
                _added.Add(record);
            }
        }

        /// <summary>
        /// The value of a cell as its XRECORD in the data table holds it: as the cell does, but a date has no size in 92 and its day
        /// of the week set, and a 2D point has a third coordinate (30) of nought.
        /// </summary>
        private static List<Group> RecordValue(List<Group> value)
        {
            int type = value.Count > 1 && int.TryParse(value[1].Value.Trim(), out int found) ? found : 0;

            if (type != 8 && type != 16)
            {
                return value;
            }

            var made = new List<Group>(value.Count + 1);

            foreach (Group group in value)
            {
                if (type == 8 && group.Code == 92)
                {
                    continue;
                }

                made.Add(type == 8 && group.Code == 310 ? new Group(310, WithWeekday(group.Value)) : group);

                if (type == 16 && group.Code == 20)
                {
                    made.Add(new Group(30, "0.0"));
                }
            }

            return made;
        }

        /// <summary>The 16 bytes of a date in hex (a SYSTEMTIME) with its day of the week (2 bytes, the third) the date has.</summary>
        private static string WithWeekday(string hex)
        {
            string trimmed = hex.Trim();

            if (trimmed.Length != 32)
            {
                return hex;
            }

            try
            {
                int year = int.Parse(
                    trimmed.Substring(2, 2) + trimmed.Substring(0, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                );
                int month = int.Parse(
                    trimmed.Substring(6, 2) + trimmed.Substring(4, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                );
                int day = int.Parse(
                    trimmed.Substring(14, 2) + trimmed.Substring(12, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                );
                int weekday = (int)new DateTime(year, month, day).DayOfWeek;
                return trimmed.Substring(0, 8)
                    + weekday.ToString("X2", CultureInfo.InvariantCulture)
                    + "00"
                    + trimmed.Substring(12);
            }
            catch (ArgumentOutOfRangeException)
            {
                return hex;
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
        /// The value a field holds, as the data table of the cell that shows it keeps it: that of the last field in the chain of
        /// the first children from the field of the cell (a table's field wraps the one that works out the text), its groups from
        /// the flags (93) to the shown text (302), without the 92 that R2018 has beside the type. Null where the field, its value
        /// or a child is not in the drawing.
        /// </summary>
        private List<Group> FieldValue(string handle)
        {
            Record field = ObjectAt(handle);

            for (int depth = 0; field != null && depth < 8; depth++)
            {
                List<Group> groups = field.Groups;
                int own = groups.FindIndex(g => g.Code == 100 && g.Value == "AcDbField");
                int count = own < 0 ? -1 : groups.FindIndex(own, g => g.Code == 90);

                if (count < 0 || count + 1 >= groups.Count)
                {
                    return null;
                }

                if (Parse(groups[count].Value) > 0 && groups[count + 1].Code == 360)
                {
                    field = ObjectAt(groups[count + 1].Value);
                    continue;
                }

                int value = groups.FindIndex(count, g => g.Code == 7 && g.Value == "ACFD_FIELD_VALUE");
                int end = value < 0 ? -1 : groups.FindIndex(value, g => g.Code == 304 && g.Value == ValueEnd);

                if (end < 0 || end - value < 5 || groups[value + 1].Code != 93)
                {
                    return null;
                }

                return groups.GetRange(value + 1, end - value - 1).FindAll(g => g.Code != 92);
            }

            return null;
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
        /// known to be converted: the flags (93) before each value's type are left out, and the 94, 300, 303 (the chunks of a long
        /// text it shows), 302 and ACVALUE_END after its data. A value of the type 512 gets the words it shows in 1 (the 302 it
        /// leaves out), a date its 8 bytes (the seconds since 1970) for its 16 (see <see cref="DateDown"/>), and the chunks of a
        /// long text stay in 2 before its last part in 1. False where a value is of a shape that is not known.
        /// </summary>
        private bool ContentDown(Record table, List<Action> changes)
        {
            if (!RoundTrip(table, out Record xrecord, out int entity) || entity < 0)
            {
                return true;
            }

            Record content = ContentOf(xrecord, entity);
            return content == null || ValuesDown(content, changes);
        }

        /// <summary>
        /// Puts the values of a record that holds some (a TABLECONTENT, or a data link with its custom data) in the form of R2004 and
        /// before, as <see cref="ContentDown"/> says.
        /// </summary>
        private bool ValuesDown(Record content, List<Action> changes)
        {
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

                if (end < 0 || groups[end - 1].Code != 302)
                {
                    return false;
                }

                // The text it shows may be in chunks of 250 characters in 303 before the 302.
                int shown = end - 1;

                while (shown - 1 > i + 3 && groups[shown - 1].Code == 303)
                {
                    shown--;
                }

                int unit = shown - 2;

                if (unit <= i + 2 || groups[unit].Code != 94 || groups[unit + 1].Code != 300)
                {
                    return false;
                }

                bool date = groups[i + 2].Value.Trim() == "8";

                for (int g = i + 2; g < unit; g++)
                {
                    Group group = groups[g];

                    if (date && group.Code == 92)
                    {
                        group = Whole(92, 8);
                    }
                    else if (date && group.Code == 310)
                    {
                        group = new Group(310, DateDown(group.Value, TimeZoneInfo.Local));
                    }

                    made.Add(group);
                }

                // A value of no kind (93 of 7, 90 of 0) is written 90 of 0 and a 91 of 0 in the form before R2007.
                if (groups[i + 2].Value.Trim() == "0" && unit == i + 3)
                {
                    made.Add(Whole(91, 0));
                }
                else if (groups[i + 2].Value.Trim() == "512" && unit == i + 3)
                {
                    // A general value holds the words it shows, as its type 4 does (none, in a table drawn with nothing in it).
                    made.Add(new Group(1, groups[end - 1].Value));
                }

                i = end;
            }

            changes.Add(() => content.Groups = made);
            return true;
        }

        /// <summary>
        /// A data link's custom data and the TABLECONTENT it owns (360), in the form of R2004 and before; what is not of a shape
        /// that is known is left as it is.
        /// </summary>
        private void LinkDown(Record link)
        {
            var changes = new List<Action>();
            int at = link.Groups.FindIndex(g => g.Code == 360);
            Record owned = at < 0 ? null : ObjectAt(link.Groups[at].Value);
            bool done = ValuesDown(link, changes);

            if (owned != null && owned.Type == "TABLECONTENT")
            {
                done = ValuesDown(owned, changes) && done;
            }

            if (done)
            {
                foreach (Action change in changes)
                {
                    change();
                }
            }
        }

        /// <summary>
        /// The bytes of a date as AutoCAD 2021 writes it before R2007 in a value (8 bytes, the seconds since 1970 in UTC as a long,
        /// 8AD5076B00000000 for 13:45:30 on 26 November 2026 at UTC+7) for the 16 bytes of R2007 and later (a SYSTEMTIME: year,
        /// month, day of the week, day, hour, minute, second and milliseconds, 2 bytes each: EA070B0004001A000D002D001E000000), in
        /// hex. AutoCAD takes the time of its own time zone; so does this, by <paramref name="zone"/>.
        /// </summary>
        internal static string DateDown(string hex, TimeZoneInfo zone)
        {
            string trimmed = hex.Trim();

            if (trimmed.Length != 32)
            {
                return hex;
            }

            var part = new int[8];

            for (int k = 0; k < 8; k++)
            {
                part[k] = int.Parse(
                    trimmed.Substring(4 * k + 2, 2) + trimmed.Substring(4 * k, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                );
            }

            DateTime local;

            try
            {
                local = new DateTime(part[0], part[1], part[3], part[4], part[5], part[6], DateTimeKind.Unspecified);
            }
            catch (ArgumentOutOfRangeException)
            {
                return hex;
            }

            TimeSpan offset = zone.IsInvalidTime(local) ? zone.BaseUtcOffset : zone.GetUtcOffset(local);
            long seconds = (long)
                (local - offset - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)).TotalSeconds;
            var bytes = BitConverter.GetBytes(seconds);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            var text = new StringBuilder(16);

            foreach (byte one in bytes)
            {
                text.Append(one.ToString("X2", CultureInfo.InvariantCulture));
            }

            return text.ToString();
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
                bool none = type == "0" && groups[i + 2].Code == 91 && groups[i + 2].Value.Trim() == "0";

                if (none)
                {
                    made.Add(Whole(93, 7));
                    made.Add(groups[i + 1]);
                    made.Add(Whole(94, 0));
                    made.Add(new Group(300, string.Empty));
                    made.Add(new Group(302, string.Empty));
                    made.Add(new Group(304, ValueEnd));
                    i += 2;
                    continue;
                }

                // The value of a cell is a whole number (91), a real (140), words (1) or a general value that holds its words in 1
                // and shows them in 302; the checksum of the cell (a DATAMAP_VALUE) is a real with the flags 2.
                bool cell = groups[i].Code == 300;
                bool whole = cell && type == "1" && groups[i + 2].Code == 91;
                bool general = cell && type == "512" && groups[i + 2].Code == 1;

                if (!text && !whole && !general && !(type == "2" && groups[i + 2].Code == 140))
                {
                    return false;
                }

                made.Add(
                    Whole(
                        93,
                        text || whole || (cell && !general) ? 6
                            : general ? 5
                            : 2
                    )
                );
                made.Add(groups[i + 1]);

                if (!general)
                {
                    made.Add(groups[i + 2]);
                }

                made.Add(Whole(94, 0));
                made.Add(new Group(300, string.Empty));
                made.Add(new Group(302, text || whole || general ? groups[i + 2].Value : string.Empty));
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

        #region The formats of a table style, kept for the old releases

        private const string StyleRoundTripName = "ACAD_ROUNDTRIP_PRE2007_TABLESTYLE";

        /// <summary>
        /// Finds what a table style keeps its round trip in: its extension dictionary, and the XRECORD that dictionary names as
        /// <c>ACAD_XREC_ROUNDTRIP</c> (null where it names none). False where the style has no extension dictionary of its own.
        /// </summary>
        private bool StyleRoundTrip(Record style, out Record dictionary, out Record xrecord)
        {
            dictionary = null;
            xrecord = null;
            int at = style.Groups.FindIndex(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY");

            if (at < 0 || at + 1 >= style.Groups.Count || style.Groups[at + 1].Code != 360)
            {
                return false;
            }

            dictionary = ObjectAt(style.Groups[at + 1].Value);

            if (dictionary == null || dictionary.Type != "DICTIONARY")
            {
                dictionary = null;
                return false;
            }

            int entry = dictionary.Groups.FindIndex(g => g.Code == 3 && g.Value == "ACAD_XREC_ROUNDTRIP");

            if (entry >= 0 && entry + 1 < dictionary.Groups.Count)
            {
                Record found = ObjectAt(dictionary.Groups[entry + 1].Value);
                xrecord = found != null && found.Type == "XRECORD" ? found : null;
            }

            return true;
        }

        /// <summary>
        /// The formats of the three kinds of row of a table style, as AutoCAD keeps them in the round trip of the style
        /// (<c>ACAD_ROUNDTRIP_PRE2007_TABLESTYLE</c>): for the data, title and header rows in that order, the type of the value (90,
        /// 92, 94: 512 for any), its unit (91, 93, 95: 0 for none) and its format (1, 2, 3), which a style of R2007 and later holds
        /// after the fill of each row (283) in the groups 90, 91 and 1 and one of R2004 and before has no place for. A row without
        /// them is any value, no unit and no format.
        /// </summary>
        private List<Group> StyleFormats(Record style)
        {
            var made = new List<Group>(9);
            List<Group> groups = style.Groups;
            int row = 0;

            if (Subclass(groups, "AcDbTableStyle", out int start, out int end))
            {
                for (int i = start; i < end && row < 3; i++)
                {
                    if (groups[i].Code != 283)
                    {
                        continue;
                    }

                    bool held =
                        i + 3 < end && groups[i + 1].Code == 90 && groups[i + 2].Code == 91 && groups[i + 3].Code == 1;

                    made.Add(Whole(90 + 2 * row, held ? Parse(groups[i + 1].Value) : 512));
                    made.Add(Whole(91 + 2 * row, held ? Parse(groups[i + 2].Value) : 0));
                    made.Add(new Group(1 + row, held ? groups[i + 3].Value : string.Empty));
                    row++;
                }
            }

            for (; row < 3; row++)
            {
                made.Add(Whole(90 + 2 * row, 512));
                made.Add(Whole(91 + 2 * row, 0));
                made.Add(new Group(1 + row, string.Empty));
            }

            return made;
        }

        /// <summary>A whole number written in a group, 0 where it is not one.</summary>
        private static int Parse(string value) =>
            int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : 0;

        /// <summary>
        /// Keeps the formats of a table style of R2007 or later that goes to R2004 or before, which the style has no place for, as
        /// AutoCAD does: in its extension dictionary, the XRECORD <c>ACAD_XREC_ROUNDTRIP</c> (made where the dictionary names none)
        /// holds <c>ACAD_ROUNDTRIP_PRE2007_TABLESTYLE</c> and the formats of its three kinds of row. A style with no extension
        /// dictionary keeps none.
        /// </summary>
        private void KeepStyle(Record style)
        {
            if (!StyleRoundTrip(style, out Record dictionary, out Record xrecord))
            {
                return;
            }

            if (xrecord != null && xrecord.Groups.Exists(g => g.Code == 102 && g.Value == StyleRoundTripName))
            {
                return;
            }

            string owner = HandleOf(dictionary);

            if (owner == null)
            {
                return;
            }

            if (xrecord == null)
            {
                string handle = NewHandle();

                if (handle == null)
                {
                    return;
                }

                xrecord = new Record();
                xrecord.Groups.AddRange(
                    new[]
                    {
                        new Group(0, "XRECORD"),
                        new Group(5, handle),
                        new Group(102, "{ACAD_REACTORS"),
                        new Group(330, owner),
                        new Group(102, "}"),
                        new Group(330, owner),
                        new Group(100, "AcDbXrecord"),
                        Whole(280, 1),
                    }
                );
                _added.Add(xrecord);
                dictionary.Groups.Add(new Group(3, "ACAD_XREC_ROUNDTRIP"));
                dictionary.Groups.Add(new Group(360, handle));
            }

            xrecord.Groups.Add(new Group(102, StyleRoundTripName));
            xrecord.Groups.AddRange(StyleFormats(style));
        }

        /// <summary>
        /// Gives a table style of R2004 or before that goes to R2007 or later the formats of its rows (90, 91 and 1 after the fill
        /// of each, the groups R2007 added) from the round trip AutoCAD kept for them, or any value, no unit and no format where it
        /// has none, and takes that part of the round trip out of the extension dictionary, as AutoCAD does.
        /// </summary>
        private void StyleUp(Record style)
        {
            StyleRoundTrip(style, out Record dictionary, out Record xrecord);
            var formats = new List<Group>(0);
            int part =
                xrecord == null ? -1 : xrecord.Groups.FindIndex(g => g.Code == 102 && g.Value == StyleRoundTripName);

            if (part >= 0 && part + 9 < xrecord.Groups.Count)
            {
                formats = xrecord.Groups.GetRange(part + 1, 9);
            }

            if (formats.Count != 9 || formats[0].Code != 90 || formats[8].Code != 3)
            {
                formats = new List<Group>(0);
            }

            List<Group> groups = style.Groups;

            if (Subclass(groups, "AcDbTableStyle", out int start, out int end))
            {
                int row = 2;

                for (int i = end - 1; i >= start && row >= 0; i--)
                {
                    if (groups[i].Code != 283)
                    {
                        continue;
                    }

                    if (i + 1 < end && groups[i + 1].Code == 90)
                    {
                        row--;
                        continue;
                    }

                    groups.Insert(i + 1, new Group(1, formats.Count == 0 ? string.Empty : formats[3 * row + 2].Value));
                    groups.Insert(
                        i + 1,
                        formats.Count == 0 ? Whole(91, 0) : Whole(91, Parse(formats[3 * row + 1].Value))
                    );
                    groups.Insert(
                        i + 1,
                        formats.Count == 0 ? Whole(90, 512) : Whole(90, Parse(formats[3 * row].Value))
                    );
                    row--;
                }
            }

            if (part < 0)
            {
                return;
            }

            xrecord.Groups.RemoveRange(part, Math.Min(10, xrecord.Groups.Count - part));

            // The XRECORD that held nothing else goes, and its name in the dictionary with it.
            if (!xrecord.Groups.Exists(g => g.Code == 102 && g.Value.StartsWith("ACAD_", StringComparison.Ordinal)))
            {
                int entry = dictionary.Groups.FindIndex(g => g.Code == 3 && g.Value == "ACAD_XREC_ROUNDTRIP");

                if (entry >= 0)
                {
                    dictionary.Groups.RemoveRange(entry, 2);
                }

                xrecord.Dropped = true;
            }
        }

        /// <summary>Gives a table style the group 280 R2010 and later write first in it, after its subclass marker (the version of its data, nought), where it has none.</summary>
        private void StyleVersion(Record style)
        {
            List<Group> groups = style.Groups;

            if (
                Subclass(groups, "AcDbTableStyle", out int start, out _)
                && start + 1 < groups.Count
                && groups[start + 1].Code != 280
            )
            {
                groups.Insert(start + 1, Whole(280, 0));
            }
        }

        #endregion
    }
}
