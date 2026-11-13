using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region Columns going up

        /// <summary>
        /// Joins the MTEXTs of the columns of an MTEXT that is in columns by the extended data of ACAD, the form of R2013 and
        /// before, into the one MTEXT of R2018: the first column holds the handles of the MTEXTs of the later columns
        /// (<c>ACAD_MTEXT_COLUMNS_BEGIN</c>, 1005), each a text of its own that goes into the text of the first, at the end of it,
        /// as they stand (each column keeps the space it ended at: "One two " and "three four"), and out of the drawing
        /// (not told: AutoCAD makes them one text too). The list of handles goes with them.
        /// </summary>
        private void JoinColumns()
        {
            Dictionary<string, Record> texts = null;

            foreach (string name in new[] { "BLOCKS", "ENTITIES" })
            {
                Section section = Find(name);

                if (section == null)
                {
                    continue;
                }

                foreach (Record record in section.Records)
                {
                    if (record.Dropped || record.Type != "MTEXT")
                    {
                        continue;
                    }

                    int begin = record.Groups.FindIndex(g => g.Code == 1000 && g.Value == "ACAD_MTEXT_COLUMNS_BEGIN");

                    if (begin < 0)
                    {
                        continue;
                    }

                    int end = record.Groups.FindIndex(begin, g =>
                        g.Code == 1000 && g.Value == "ACAD_MTEXT_COLUMNS_END"
                    );

                    if (end < 0)
                    {
                        continue;
                    }

                    if (texts == null)
                    {
                        texts = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);

                        foreach (string held in new[] { "BLOCKS", "ENTITIES" })
                        {
                            foreach (Record one in Find(held)?.Records ?? new List<Record>())
                            {
                                string handle = one.Type == "MTEXT" ? HandleOf(one) : null;

                                if (handle != null)
                                {
                                    texts[handle] = one;
                                }
                            }
                        }
                    }

                    var joined = new StringBuilder(TextOf(record));

                    for (int i = begin + 1; i < end; i++)
                    {
                        if (
                            record.Groups[i].Code == 1005
                            && texts.TryGetValue(record.Groups[i].Value.Trim(), out Record column)
                            && column != record
                            && !column.Dropped
                        )
                        {
                            joined.Append(TextOf(column));
                            Drop(column, _quiet);
                        }
                    }

                    record.Groups.RemoveRange(begin, end - begin + 1);
                    SetText(record, joined.ToString());
                }
            }
        }

        /// <summary>The text of an MTEXT: its groups 3, each a piece of 250 characters, and its group 1, the last.</summary>
        private static string TextOf(Record record)
        {
            var text = new StringBuilder();

            for (int i = 1; i < record.Groups.Count; i++)
            {
                if (record.Groups[i].Code == 1001)
                {
                    break;
                }

                if (record.Groups[i].Code == 3 || record.Groups[i].Code == 1)
                {
                    text.Append(record.Groups[i].Value);
                }
            }

            return text.ToString();
        }

        /// <summary>Writes the text of an MTEXT in the place of its own, in pieces of 250 characters under group 3 and the rest under group 1.</summary>
        private static void SetText(Record record, string text)
        {
            List<Group> groups = record.Groups;
            int first = -1;

            for (int i = groups.Count - 1; i >= 1; i--)
            {
                if (groups[i].Code == 1001)
                {
                    continue;
                }

                if ((groups[i].Code == 3 || groups[i].Code == 1) && !InData(groups, i))
                {
                    first = i;
                    groups.RemoveAt(i);
                }
            }

            if (first < 0)
            {
                return;
            }

            var made = new List<Group>();
            int at = 0;

            while (text.Length - at > 250)
            {
                made.Add(new Group(3, text.Substring(at, 250)));
                at += 250;
            }

            made.Add(new Group(1, text.Substring(at)));
            groups.InsertRange(first, made);
        }

        /// <summary>Whether the group at an index is past the first group of extended data (1001).</summary>
        private static bool InData(List<Group> groups, int index)
        {
            for (int i = 1; i < index; i++)
            {
                if (groups[i].Code == 1001)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gives an MTEXT of the extended data of R2013 and before its columns as the embedded object of R2018 (group 101 on, after
        /// its text): the attachment (70), direction (10) and location (11) of the MTEXT, the width of all the columns (40, which
        /// the MTEXT's own 41 is given too), the height it was defined at (41), the type (71), count (72), width (44), gutter (45),
        /// whether the height is automatic (73) and the flow reversed (74), and a height (46) for each column the extended data
        /// gives one. The extended data of the columns go. An MTEXT with none is as it is.
        /// </summary>
        private void ColumnsUp(Record record)
        {
            List<Group> groups = record.Groups;
            int begin = groups.FindIndex(g => g.Code == 1000 && g.Value == "ACAD_MTEXT_COLUMN_INFO_BEGIN");

            if (begin < 0)
            {
                return;
            }

            int end = groups.FindIndex(begin, g => g.Code == 1000 && g.Value == "ACAD_MTEXT_COLUMN_INFO_END");

            if (end < 0 || !Subclass(groups, "AcDbMText", out int start, out int stop))
            {
                return;
            }

            string type = "1";
            string auto = "0";
            string count = "1";
            string reversed = "0";
            string width = "0.0";
            string gutter = "0.0";
            var heights = new List<string>();

            for (int i = begin + 1; i + 1 < end; i++)
            {
                if (groups[i].Code != 1070)
                {
                    continue;
                }

                string key = groups[i].Value.Trim();
                string value = groups[i + 1].Value;

                switch (key)
                {
                    case "75":
                        type = value.Trim();
                        break;
                    case "79":
                        auto = value.Trim();
                        break;
                    case "76":
                        count = value.Trim();
                        break;
                    case "78":
                        reversed = value.Trim();
                        break;
                    case "48":
                        width = value;
                        break;
                    case "49":
                        gutter = value;
                        break;
                    case "50":
                        // The count of heights, then each as a real: the value is the count, which the loop steps over with the heights.
                        i++;

                        while (i + 1 < end && groups[i + 1].Code == 1040)
                        {
                            heights.Add(groups[++i].Value);
                        }

                        continue;
                }

                i++;
            }

            double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out double columnWidth);
            double.TryParse(gutter, NumberStyles.Float, CultureInfo.InvariantCulture, out double columnGutter);
            Number(count, out int columns);

            double total = columns > 0 ? columns * columnWidth + (columns - 1) * columnGutter : 0.0;
            string attachment = ValueOf(groups, start, stop, 71, "1");
            string defined = ValueOf(groups, start, stop, 46, "0.0");
            var embedded = new List<Group>
            {
                new Group(101, "Embedded Object"),
                Whole(70, Whole(attachment, 1)),
                new Group(10, ValueOf(groups, start, stop, 11, "1.0")),
                new Group(20, ValueOf(groups, start, stop, 21, "0.0")),
                new Group(30, ValueOf(groups, start, stop, 31, "0.0")),
                new Group(11, ValueOf(groups, start, stop, 10, "0.0")),
                new Group(21, ValueOf(groups, start, stop, 20, "0.0")),
                new Group(31, ValueOf(groups, start, stop, 30, "0.0")),
                new Group(40, total.ToString("0.0###############", CultureInfo.InvariantCulture)),
                new Group(41, defined),
                new Group(42, "0.0"),
                new Group(43, "0.0"),
                Whole(71, Whole(type, 1)),
                Whole(72, columns),
                new Group(44, width),
                new Group(45, gutter),
                Whole(73, Whole(auto, 0)),
                Whole(74, Whole(reversed, 0)),
            };

            foreach (string height in heights)
            {
                embedded.Add(new Group(46, height));
            }

            // The width of the reference rectangle is that of all the columns from R2018.
            int main = groups.FindIndex(start, stop - start, g => g.Code == 41);

            if (main >= 0)
            {
                groups[main] = new Group(41, embedded[8].Value);
            }

            groups.InsertRange(stop, embedded);
            int nowBegin = groups.FindIndex(g => g.Code == 1000 && g.Value == "ACAD_MTEXT_COLUMN_INFO_BEGIN");
            int nowEnd = groups.FindIndex(nowBegin, g => g.Code == 1000 && g.Value == "ACAD_MTEXT_COLUMN_INFO_END");
            groups.RemoveRange(nowBegin, nowEnd - nowBegin + 1);

            // The application ACAD, with nothing more in its data, goes too.
            int app = groups.FindIndex(g => g.Code == 1001 && g.Value == "ACAD");

            if (app >= 0)
            {
                int next = groups.FindIndex(app + 1, g => g.Code == 1001);

                if ((next < 0 ? groups.Count : next) == app + 1)
                {
                    groups.RemoveAt(app);
                }
            }
        }

        /// <summary>A whole number read from a group's value; <paramref name="otherwise"/> where it is none.</summary>
        private static int Whole(string text, int otherwise) => Number(text, out int value) ? value : otherwise;

        /// <summary>The value of the first group of a code in a range of a record's groups; <paramref name="otherwise"/> where it has none.</summary>
        private static string ValueOf(List<Group> groups, int start, int stop, int code, string otherwise)
        {
            int at = groups.FindIndex(start, stop - start, g => g.Code == code);
            return at < 0 ? otherwise : groups[at].Value;
        }

        #endregion
    }
}
