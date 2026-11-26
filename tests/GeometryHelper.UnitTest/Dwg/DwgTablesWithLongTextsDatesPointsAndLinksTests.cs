using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A table with a long text (over 250 characters), a date, a point or a link to data, saved as R2000, R2004 and R2007. Each
    /// of those used to leave the table as the block it is drawn with (the date, the point, and the long text of a table with a
    /// TABLECONTENT), or lose a part of it (the words of a long cell but their last chunk, a data link's own content). The five
    /// drawings were built in code inside AutoCAD 2021 and written as DXF of R2018, R2000, R2004 and R2007 by AutoCAD itself:
    /// <c>tlongp</c> (a plain table with texts of 250, 251, 500 and 600 characters), <c>tlong</c> (the same in a table style of
    /// its own, which gives it a TABLECONTENT), <c>tpoint</c> (a 3D and a 2D point), <c>tdate</c> (a date, both points and a
    /// number, in a table style of its own) and <c>tlink</c> (a table linked to a CSV file).
    /// </summary>
    public class DwgTablesWithLongTextsDatesPointsAndLinksTests
    {
        private static string KeyText(string set, string year) =>
            Encoding.UTF8.GetString(DwgAnswerKey.Key(set + "_AC" + year));

        private static List<DwgAnswerKey.Record> RecordsOf(string dxf, params string[] types) =>
            DwgAnswerKey.Parse(Encoding.UTF8.GetBytes(dxf)).Where(r => types.Contains(r.Type)).ToList();

        private static List<DwgAnswerKey.Record> RecordsOf(byte[] dwg, params string[] types) =>
            DwgAnswerKey.Parse(DwgReader.ToDxf(dwg)).Where(r => types.Contains(r.Type)).ToList();

        /// <summary>AutoCAD's DXF of R2018 of a drawing, opened as a database and made the text of a version, as saving it as that version does.</summary>
        private static string Converted(string set, int year)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(set + "_AC1032"), false)).ToText();
            return DxfVersionConverter.Convert(text, year, null);
        }

        private static byte[] DwgOf(string set, string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(set + "_AC1032"), false)).ToText();
            return DwgWriter.ToDwg(text, version, out _);
        }

        /// <summary>The groups of each cell of a table: those from each <c>171</c> to the next.</summary>
        private static List<List<KeyValuePair<int, string>>> CellsOf(DwgAnswerKey.Record table)
        {
            var cells = new List<List<KeyValuePair<int, string>>>();

            foreach (KeyValuePair<int, string> group in table.Groups.SkipWhile(g => g.Key != 171))
            {
                if (group.Key == 171)
                {
                    cells.Add(new List<KeyValuePair<int, string>>());
                }

                cells[cells.Count - 1].Add(group);
            }

            return cells;
        }

        /// <summary>A cell as lines <c>code=value</c>, the chunks of a long text joined (3 or 2 into the 1 after them, 303 into the 302).</summary>
        private static List<string> Lines(List<KeyValuePair<int, string>> cell, bool joined)
        {
            var lines = new List<string>();
            var chunks = new StringBuilder();

            foreach (KeyValuePair<int, string> group in cell)
            {
                string value = group.Value.Trim();

                if (joined && (group.Key == 2 || group.Key == 3 || group.Key == 303))
                {
                    chunks.Append(group.Value);
                    continue;
                }

                if (group.Key == 1 || group.Key == 302)
                {
                    value = (chunks.ToString() + group.Value).Trim();
                    chunks.Clear();
                }

                // AutoCAD writes the flags of a cell with no value as 7 for some cells and 3 for others (the flag 4).
                lines.Add(group.Key + "=" + (group.Key == 93 && (value == "3" || value == "7") ? "3 or 7" : value));
            }

            return lines;
        }

        private static List<List<string>> CellLines(DwgAnswerKey.Record table, bool joined) =>
            CellsOf(table).Select(cell => Lines(cell, joined)).ToList();

        /// <summary>The groups of the XRECORDs the data table of the tables holds for their cells, in the order of the cells.</summary>
        private static List<List<string>> CellRecordsOf(List<DwgAnswerKey.Record> all)
        {
            DwgAnswerKey.Record data = all.Single(r => r.Type == "ACDBDATATABLE" || r.Type == "DATATABLE");

            return data
                .Groups.Where(g => g.Key == 360)
                .Select(g => g.Value.Trim())
                .Select(handle =>
                    all.Single(r => r.Type == "XRECORD" && r.Groups.First(g => g.Key == 5).Value.Trim() == handle)
                )
                .Select(record =>
                    record
                        .Groups.SkipWhile(g => g.Key != 280)
                        .Skip(1)
                        .Select(g => g.Key + "=" + g.Value.Trim())
                        .ToList()
                )
                .ToList();
        }

        /// <summary>
        /// The values of the data table that this file is about: those of a date, a point or a text of over 250 characters. The
        /// other values depend on how the cell was made (a cell AutoCAD's own API set to empty words holds 93 of 6, one it left
        /// alone 7), which this is not about; nor do the flags (93) of the value, 4 or 6 as AutoCAD sees fit, so they are left out.
        /// </summary>
        private static List<string> Interesting(List<List<string>> values, List<List<string>> acad) =>
            values.Where((v, i) => IsInteresting(acad[i])).Select(v => string.Join(" ", v.Skip(1))).ToList();

        private static bool IsInteresting(List<string> value) =>
            value.Contains("90=8")
            || value.Contains("90=16")
            || value.Contains("90=32")
            || value.Any(g => g.Length > 250);

        private static List<DwgAnswerKey.Record> AllOf(string dxf) => DwgAnswerKey.Parse(Encoding.UTF8.GetBytes(dxf));

        private static string Describe(IEnumerable<List<string>> cells) =>
            string.Join("\n", cells.Select(c => string.Join(" ", c)));

        public static IEnumerable<object[]> SetsAndOldVersions()
        {
            foreach (string set in new[] { "tlongp", "tlong", "tpoint", "tdate", "tlink" })
            {
                foreach (string year in new[] { "1015", "1018" })
                {
                    yield return new object[] { set, year };
                }
            }
        }

        public static IEnumerable<object[]> SetsAndVersions()
        {
            foreach (string set in new[] { "tlongp", "tlong", "tpoint", "tdate", "tlink" })
            {
                foreach (string year in new[] { "1015", "1018", "1021" })
                {
                    yield return new object[] { set, year };
                }
            }
        }

        [Theory]
        [MemberData(nameof(SetsAndOldVersions))]
        public void TheTable_SavedAsR2000OrR2004_StaysATable_NotTheBlockItIsDrawnWith(string set, string year)
        {
            string text = Converted(set, int.Parse(year, CultureInfo.InvariantCulture));

            Assert.Single(RecordsOf(text, "ACAD_TABLE"));
        }

        [Theory]
        [MemberData(nameof(SetsAndOldVersions))]
        public void TheCellsOfTheTable_SavedAsR2000OrR2004_AreTheCellsAutoCADWritesForThem(string set, string year)
        {
            string text = Converted(set, int.Parse(year, CultureInfo.InvariantCulture));
            DwgAnswerKey.Record ours = Assert.Single(RecordsOf(text, "ACAD_TABLE"));
            DwgAnswerKey.Record acad = Assert.Single(RecordsOf(KeyText(set, year), "ACAD_TABLE"));

            Assert.Equal(Describe(CellLines(acad, false)), Describe(CellLines(ours, false)));
        }

        [Theory]
        [InlineData("tlongp", "1015")]
        [InlineData("tlongp", "1018")]
        [InlineData("tlong", "1015")]
        [InlineData("tlong", "1018")]
        [InlineData("tpoint", "1015")]
        [InlineData("tpoint", "1018")]
        [InlineData("tdate", "1015")]
        [InlineData("tdate", "1018")]
        public void TheDataTableValues_OfTheTableSavedAsR2000OrR2004_AreTheOnesAutoCADKeeps(string set, string year)
        {
            string text = Converted(set, int.Parse(year, CultureInfo.InvariantCulture));
            List<List<string>> ours = CellRecordsOf(AllOf(text));
            List<List<string>> acad = CellRecordsOf(AllOf(KeyText(set, year)));

            Assert.Equal(Interesting(acad, acad), Interesting(ours, acad));
        }

        [Theory]
        [InlineData("tlong", "1015")]
        [InlineData("tlong", "1018")]
        [InlineData("tdate", "1015")]
        [InlineData("tdate", "1018")]
        [InlineData("tlink", "1015")]
        [InlineData("tlink", "1018")]
        public void TheContentsOfTheTable_SavedAsR2000OrR2004_HoldTheValuesAsAutoCADWritesThem(string set, string year)
        {
            string text = Converted(set, int.Parse(year, CultureInfo.InvariantCulture));
            List<DwgAnswerKey.Record> ours = RecordsOf(text, "TABLECONTENT");
            List<DwgAnswerKey.Record> acad = RecordsOf(KeyText(set, year), "TABLECONTENT");

            // The order of the two contents (the table's, the link's) is the order of their handles, which differ.
            Assert.Equal(
                acad.SelectMany(r => Values(r)).OrderBy(v => v, StringComparer.Ordinal),
                ours.SelectMany(r => Values(r)).OrderBy(v => v, StringComparer.Ordinal)
            );
        }

        /// <summary>
        /// The values of a TABLECONTENT, in order, as lines of the groups between each mark of a value (<c>VALUE</c>, or
        /// <c>DATAMAP_VALUE</c>) and the group that closes it (309): a date of 8 bytes (R2000 and R2004) holds the seconds
        /// since 1970 of the local time AutoCAD was run in, so the bytes of it are left out.
        /// </summary>
        private static List<string> Values(DwgAnswerKey.Record content, bool joined = false)
        {
            var lines = new List<string>();
            List<KeyValuePair<int, string>> groups = content.Groups;

            for (int i = 0; i < groups.Count; i++)
            {
                bool mark =
                    (groups[i].Key == 300 && groups[i].Value.Trim() == "VALUE")
                    || (groups[i].Key == 301 && groups[i].Value.Trim() == "DATAMAP_VALUE");

                if (!mark)
                {
                    continue;
                }

                var one = new List<string>();

                var chunks = new StringBuilder();

                for (int j = i + 1; j < groups.Count && groups[j].Key != 309; j++)
                {
                    bool seconds = groups[j].Key == 310 && groups[j - 1].Key == 92 && groups[j - 1].Value.Trim() == "8";
                    string text = groups[j].Value.Trim();

                    // A DWG holds a text whole: the chunks of 250 characters are 2 before the 1 and 303 before the 302 of its DXF.
                    if (joined && (groups[j].Key == 2 || groups[j].Key == 303))
                    {
                        chunks.Append(groups[j].Value);
                        continue;
                    }

                    if (joined && (groups[j].Key == 1 || groups[j].Key == 302))
                    {
                        text = (chunks.ToString() + groups[j].Value).Trim();
                        chunks.Clear();
                    }

                    one.Add(groups[j].Key + "=" + (seconds ? "seconds" : text));
                }

                lines.Add(string.Join(" ", one));
            }

            return lines;
        }

        [Theory]
        [MemberData(nameof(SetsAndVersions))]
        public void TheTable_WrittenAsADwgOfR2000R2004OrR2007_IsATableWithAutoCADsCells(string set, string year)
        {
            byte[] dwg = DwgOf(set, "AC" + year);
            DwgAnswerKey.Record ours = Assert.Single(RecordsOf(dwg, "ACAD_TABLE"));
            DwgAnswerKey.Record acad = Assert.Single(RecordsOf(KeyText(set, year), "ACAD_TABLE"));

            // The reader gives the overrides of a cell before its words, AutoCAD's DXF after them, and leaves out a content colour
            // (64) of by block, which has nothing to do with the values.
            List<string> Sorted(List<string> cell) =>
                cell.Where(l => !l.StartsWith("64=", StringComparison.Ordinal))
                    .OrderBy(l => l, StringComparer.Ordinal)
                    .ToList();

            Assert.Equal(
                Describe(CellLines(acad, true).Select(Sorted)),
                Describe(CellLines(ours, true).Select(Sorted))
            );
        }

        [Theory]
        [InlineData("tlongp", "1015")]
        [InlineData("tlongp", "1018")]
        [InlineData("tlong", "1015")]
        [InlineData("tlong", "1018")]
        [InlineData("tpoint", "1015")]
        [InlineData("tpoint", "1018")]
        [InlineData("tdate", "1015")]
        [InlineData("tdate", "1018")]
        public void TheDataTable_WrittenAsADwgOfR2000OrR2004_HoldsAutoCADsValues(string set, string year)
        {
            byte[] dwg = DwgOf(set, "AC" + year);
            List<List<string>> ours = CellRecordsOf(DwgAnswerKey.Parse(DwgReader.ToDxf(dwg)));
            List<List<string>> acad = CellRecordsOf(AllOf(KeyText(set, year)));

            Assert.Equal(Interesting(acad, acad), Interesting(ours, acad));
        }

        [Theory]
        [InlineData("tlong", "1015")]
        [InlineData("tlong", "1018")]
        [InlineData("tlong", "1021")]
        [InlineData("tdate", "1015")]
        [InlineData("tdate", "1018")]
        [InlineData("tdate", "1021")]
        [InlineData("tlink", "1015")]
        [InlineData("tlink", "1018")]
        [InlineData("tlink", "1021")]
        public void TheContent_WrittenAsADwgOfR2000R2004OrR2007_HoldsAutoCADsValues(string set, string year)
        {
            byte[] dwg = DwgOf(set, "AC" + year);
            List<DwgAnswerKey.Record> ours = RecordsOf(dwg, "TABLECONTENT");
            List<DwgAnswerKey.Record> acad = RecordsOf(KeyText(set, year), "TABLECONTENT");

            // The order of the two contents (the table's, the link's) is the order of their handles, which differ.
            Assert.Equal(
                acad.SelectMany(r => Values(r, true)).OrderBy(v => v, StringComparer.Ordinal),
                ours.SelectMany(r => Values(r, true)).OrderBy(v => v, StringComparer.Ordinal)
            );
        }

        [Fact]
        public void ADateOf16Bytes_IsTheSecondsSince1970InUtcOf8Bytes_ForTheTimeZoneItWasTakenIn()
        {
            // 13:45:30 on 26 November 2026, as AutoCAD 2021 kept it in a cell (R2007: its day of the week 0) and in the R2000 file
            // of a machine at UTC+7 (8AD5076B00000000, 06:45:30 in UTC).
            const string systemTime = "EA070B0000001A000D002D001E000000";
            TimeZoneInfo plus7 = TimeZoneInfo.CreateCustomTimeZone("plus7", TimeSpan.FromHours(7), "plus7", "plus7");

            Assert.Equal("8AD5076B00000000", DxfVersionConverter.DateDown(systemTime, plus7));
            Assert.Equal("FA37086B00000000", DxfVersionConverter.DateDown(systemTime, TimeZoneInfo.Utc));

            // A text that is not 16 bytes is left as it is.
            Assert.Equal("EA07", DxfVersionConverter.DateDown("EA07", plus7));
        }
    }
}
