using System;
using System.Collections.Generic;
using System.Globalization;

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
        // borders' (top 0x4440, bottom 0x8880, left 0x2200, right 0x1100 and the cells between). 91 has bits more: the cell has content
        // (0x40000), and a cell with a border of its own on the right (0x1000), left (0x2000) or top (0x4000) has them again 4 bits up,
        // as AutoCAD writes it; the bottom's 0x8000 has none.
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

            for (int i = 0; i < starts.Count; i++)
            {
                int to = i + 1 < starts.Count ? starts[i + 1] : stop;

                if (!CellDown(groups.GetRange(starts[i], to - starts[i]), i + 1 == starts.Count, made))
                {
                    return false;
                }
            }

            var changes = new List<Action>();

            if (!ContentDown(table, changes))
            {
                return false;
            }

            made.AddRange(groups.GetRange(stop, groups.Count - stop));
            table.Groups = made;

            foreach (Action change in changes)
            {
                change();
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
        private bool CellDown(List<Group> cell, bool last, List<Group> made)
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

            if (!Value(cell, at + 1, end, out int type, text) || (!block && type != 0 && type != 4))
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
                made.AddRange(text.Count > 0 ? text : new List<Group> { new Group(1, string.Empty) });
            }

            made.AddRange(held);
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
                if (from >= cell.Count || cell[from].Code != 1)
                {
                    return false;
                }

                text = cell[from].Value;
                from++;
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

            flags &= 0xFFFF;
            made.Add(Whole(91, flags | (has ? ContentBit : 0) | ((flags & 0x7000) << 4)));
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
                made.Add(new Group(1, text));
            }

            made.Add(Whole(94, 0));
            made.Add(new Group(300, string.Empty));
            made.Add(new Group(302, text));
            made.Add(new Group(304, ValueEnd));
            return true;
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
        /// and type (90), the groups of its text (1, and 3 before it) where it has any, then 94, 300 and 302. False where it is of
        /// another shape.
        /// </summary>
        private static bool Value(List<Group> cell, int from, int end, out int type, List<Group> text)
        {
            type = -1;

            if (
                end - from < 5
                || cell[from].Code != 93
                || cell[from + 1].Code != 90
                || cell[end - 3].Code != 94
                || cell[end - 2].Code != 300
                || cell[end - 1].Code != 302
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

            for (int i = from + 2; i < end - 3; i++)
            {
                if (cell[i].Code != 1 && cell[i].Code != 3)
                {
                    return false;
                }

                text.Add(cell[i]);
            }

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

            int part = xrecord.Groups.FindIndex(g => g.Code == 102 && g.Value == "ACAD_ROUNDTRIP_PRE2007_TABLE");

            if (
                part >= 0
                && part + 4 < xrecord.Groups.Count
                && xrecord.Groups[part + 3].Code == 102
                && xrecord.Groups[part + 3].Value == "ACAD_ROUNDTRIP_PRE2007_TABLECELL"
                && xrecord.Groups[part + 4].Code == 360
            )
            {
                string data = xrecord.Groups[part + 4].Value.Trim();
                changes.Add(() =>
                {
                    xrecord.Groups.RemoveRange(part, 5);
                    _owning.Add(data);
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
