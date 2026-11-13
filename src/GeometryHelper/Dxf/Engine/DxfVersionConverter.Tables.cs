using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region A table in the other form of its cells

        // The groups of a cell of text in the form of R2000 and R2004, and in the form of R2007 and later (the text, group 1, only
        // where the cell has any), as AutoCAD 2021 writes them.
        private static readonly int[] OldCell = { 171, 172, 173, 174, 175, 176, 177, 178, 145, 1 };
        private static readonly int[] NewCell =
        {
            171,
            172,
            173,
            174,
            175,
            176,
            91,
            178,
            145,
            92,
            301,
            93,
            90,
            1,
            94,
            300,
            302,
            304,
        };

        /// <summary>
        /// Gives a table of R2007 or later the form of R2004 and before, where each cell is the groups 171 to 178 and 145 and its
        /// text (1), the cell's value (301 CELL_VALUE to 304 ACVALUE_END) and its flags (91, 92) of the later form left out; an
        /// empty cell has an empty text. False, the table left as it is, where a cell holds anything but text of AutoCAD's plain
        /// cell (a block, a format of its own, a field, a link): the caller then draws the table as the block it is drawn with.
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
                List<Group> cell = groups.GetRange(starts[i], to - starts[i]);

                if (
                    !Matches(cell, NewCell, out string text)
                    || cell[10].Value != "CELL_VALUE"
                    || cell[cell.Count - 1].Value != "ACVALUE_END"
                )
                {
                    return false;
                }

                for (int g = 0; g < 6; g++)
                {
                    made.Add(cell[g]);
                }

                made.Add(Whole(177, 0));
                made.Add(cell[7]);
                made.Add(cell[8]);
                made.Add(new Group(1, text));
            }

            made.AddRange(groups.GetRange(stop, groups.Count - stop));
            table.Groups = made;
            return true;
        }

        /// <summary>
        /// Gives a table of R2004 or before the form of R2007 and later: each cell of text with its flags (91, 92) and its value
        /// (301 CELL_VALUE, 93, 90, the text in 1, 94, 300, 302 and 304 ACVALUE_END), where the cell has text 93 is 6, 90 is 4 and 91
        /// is 262144, where it has none 93 is 7 and 90 and 91 are 0. The extension dictionary of the round trip of the form
        /// before it (ACAD_XREC_ROUNDTRIP) goes. False, the table left as it is, where a cell holds more than its text.
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
                List<Group> cell = groups.GetRange(starts[i], to - starts[i]);

                if (!Matches(cell, OldCell, out string text))
                {
                    return false;
                }

                bool has = text.Length > 0;

                for (int g = 0; g < 6; g++)
                {
                    made.Add(cell[g]);
                }

                made.Add(Whole(91, has ? 262144 : 0));
                made.Add(cell[7]);
                made.Add(cell[8]);
                made.Add(Whole(92, 0));
                made.Add(new Group(301, "CELL_VALUE"));
                made.Add(Whole(93, has ? 6 : 7));
                made.Add(Whole(90, has ? 4 : 0));

                if (has)
                {
                    made.Add(new Group(1, text));
                }

                made.Add(Whole(94, 0));
                made.Add(new Group(300, string.Empty));
                made.Add(new Group(302, text));
                made.Add(new Group(304, "ACVALUE_END"));
            }

            made.AddRange(groups.GetRange(stop, groups.Count - stop));
            table.Groups = made;
            ForgetRoundTrip(table);
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

        /// <summary>
        /// Whether the groups of a cell are exactly those of a cell of the form, in its order, the text (1) being there or not in the
        /// form of R2007 and later where the cell has none; the text itself in <paramref name="text"/>.
        /// </summary>
        private static bool Matches(List<Group> cell, int[] form, out string text)
        {
            text = string.Empty;
            int at = 0;

            foreach (int code in form)
            {
                if (at < cell.Count && cell[at].Code == code)
                {
                    if (code == 1)
                    {
                        text = cell[at].Value;
                    }

                    at++;
                }
                else if (code != 1 || form.Length == OldCell.Length)
                {
                    return false;
                }
            }

            return at == cell.Count;
        }

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
