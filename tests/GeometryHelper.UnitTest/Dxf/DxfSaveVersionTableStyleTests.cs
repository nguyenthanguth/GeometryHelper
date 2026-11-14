using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The table styles of a drawing saved as another version across R2007 (<c>ACAD_ROUNDTRIP_PRE2007_TABLESTYLE</c>): the formats
    /// of the data, title and header rows (the type of the value, its unit and its format), which R2007 and later hold in the
    /// style and R2004 and before keep in the round trip of its extension dictionary. The drawings are AutoCAD 2021's own table
    /// drawing of R2000, R2004, R2007 and R2018 (two tables, so two styles), and the same with the formats of a style changed
    /// as AutoCAD 2021 writes them when its table style holds a distance of two decimals for its data and an angle for its
    /// header: its R2000 file says <c>90=2 91=1 1=%lu2%pr2 92=512 93=0 2= 94=1 95=2 3=%lu1%pr0</c>.
    /// </summary>
    public class DxfSaveVersionTableStyleTests
    {
        #region Helpers

        private const string Name = "ACAD_ROUNDTRIP_PRE2007_TABLESTYLE";

        private static byte[] Saved(string text, string version)
        {
            DxfDatabase db = DxfDatabase.Parse(text);

            using (var stream = new MemoryStream())
            {
                IReadOnlyList<string> said = db.Save(stream, version);
                Assert.Empty(said);
                return stream.ToArray();
            }
        }

        private static string Text(byte[] dxf) => Encoding.GetEncoding(28591).GetString(dxf);

        private static string Show(IEnumerable<KeyValuePair<int, string>> groups) =>
            string.Join(" ", groups.Select(g => g.Key + "=" + g.Value.Trim()));

        private static DwgAnswerKey.Record Record(List<DwgAnswerKey.Record> all, string handle) =>
            all.SingleOrDefault(r => r.Key == "X:" + handle);

        private static List<DwgAnswerKey.Record> Styles(List<DwgAnswerKey.Record> all) =>
            all.Where(r => r.Section == "OBJECTS" && r.Type == "TABLESTYLE").ToList();

        /// <summary>The first group of the code after the group with the value; null where the value is not there.</summary>
        private static string After(DwgAnswerKey.Record record, string value, int code)
        {
            int at = record.Groups.FindIndex(g => g.Value.Trim() == value);
            return at < 0 ? null : record.Groups.Skip(at).FirstOrDefault(g => g.Key == code).Value?.Trim();
        }

        private static DwgAnswerKey.Record Dictionary(List<DwgAnswerKey.Record> all, DwgAnswerKey.Record style) =>
            Record(all, After(style, "{ACAD_XDICTIONARY", 360));

        /// <summary>The groups after the name of the round trip in the XRECORD a style keeps it in; null where it keeps none.</summary>
        private static string Formats(List<DwgAnswerKey.Record> all, DwgAnswerKey.Record style)
        {
            DwgAnswerKey.Record dictionary = Dictionary(all, style);
            string pointer = dictionary == null ? null : After(dictionary, "ACAD_XREC_ROUNDTRIP", 360);
            DwgAnswerKey.Record xrecord = pointer == null ? null : Record(all, pointer);

            return xrecord == null ? null : Show(xrecord.Groups.SkipWhile(g => g.Value.Trim() != Name).Skip(1));
        }

        /// <summary>The groups of a style but its handle, its owner and its pointers, from its subclass on.</summary>
        private static string Look(DwgAnswerKey.Record style) =>
            Show(
                style
                    .Groups.SkipWhile(g => g.Value.Trim() != "AcDbTableStyle")
                    .Where(g => g.Key != 5 && g.Key != 330 && g.Key != 360)
            );

        /// <summary>
        /// The R2018 drawing with the formats of the first style's three rows given (type, unit, format): the groups 90, 91 and 1
        /// after the fill (283) of each row, written as the file pads them.
        /// </summary>
        private static string WithFormats(string text, params object[][] rows)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int at = Array.IndexOf(lines, "AcDbTableStyle");
            int row = 0;

            for (int i = at; i < lines.Length - 12 && row < rows.Length; i++)
            {
                if (lines[i].Trim() != "283" || lines[i + 2].Trim() != "90")
                {
                    continue;
                }

                lines[i + 3] = Convert
                    .ToString(rows[row][0], CultureInfo.InvariantCulture)
                    .PadLeft(lines[i + 3].Length);
                lines[i + 5] = Convert
                    .ToString(rows[row][1], CultureInfo.InvariantCulture)
                    .PadLeft(lines[i + 5].Length);
                lines[i + 7] = (string)rows[row][2];
                row++;
            }

            return string.Join("\r\n", lines);
        }

        private static readonly string R2018 = Text(DwgAnswerKey.Key("table_AC1032"));

        #endregion

        #region Going down

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void TheStylesOfAnR2018DrawingAutoCadWrote_GoingDown_KeepTheFormatsOfTheirRows_AsAutoCadKeepsThem(
            string version
        )
        {
            byte[] answer = DwgAnswerKey.Key("table_" + version);
            List<DwgAnswerKey.Record> key = DwgAnswerKey.Parse(answer);
            List<DwgAnswerKey.Record> saved = DwgAnswerKey.Parse(Saved(R2018, version));

            List<DwgAnswerKey.Record> styles = Styles(saved);

            Assert.Equal(Styles(key).Count, styles.Count);
            Assert.All(styles, style => Assert.NotNull(Formats(saved, style)));
            Assert.Equal(
                Styles(key).Select(s => Formats(key, s)).OrderBy(s => s, StringComparer.Ordinal),
                styles.Select(s => Formats(saved, s)).OrderBy(s => s, StringComparer.Ordinal)
            );
            Assert.Equal("90=512 91=0 1= 92=512 93=0 2= 94=512 95=0 3=", Formats(saved, styles[0]));
        }

        [Fact]
        public void ARoundTripOfAStyle_IsMadeOfItsOwn_AndTheDictionaryNamesItAfterTheCellStyleMap()
        {
            List<DwgAnswerKey.Record> saved = DwgAnswerKey.Parse(Saved(R2018, "AC1015"));
            DwgAnswerKey.Record style = Styles(saved)[0];
            DwgAnswerKey.Record dictionary = Dictionary(saved, style);
            string[] names = dictionary.Groups.Where(g => g.Key == 3).Select(g => g.Value.Trim()).ToArray();
            DwgAnswerKey.Record xrecord = Record(saved, After(dictionary, "ACAD_XREC_ROUNDTRIP", 360));

            Assert.Equal(new[] { "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP", "ACAD_XREC_ROUNDTRIP" }, names);
            Assert.Equal("XRECORD", xrecord.Type);
            Assert.Equal(dictionary.Key.Substring(2), xrecord.Owner);
        }

        [Fact]
        public void AStyleWithFormatsOfItsOwn_KeepsThemInTheOrderOfDataTitleAndHeader()
        {
            // 2 = a real, 1 = a distance, 0 = none: AutoCAD 2021's R2000 file of the style.
            string text = WithFormats(
                R2018,
                new object[] { 2, 1, "%lu2%pr2" },
                new object[] { 512, 0, "" },
                new object[] { 1, 2, "%lu1%pr0" }
            );
            List<DwgAnswerKey.Record> saved = DwgAnswerKey.Parse(Saved(text, "AC1015"));

            Assert.Equal("90=2 91=1 1=%lu2%pr2 92=512 93=0 2= 94=1 95=2 3=%lu1%pr0", Formats(saved, Styles(saved)[0]));
            Assert.Equal("90=512 91=0 1= 92=512 93=0 2= 94=512 95=0 3=", Formats(saved, Styles(saved)[1]));
        }

        [Fact]
        public void TheFormatsOfAStyle_AreNotInTheStyleItselfBeforeR2007()
        {
            string text = WithFormats(R2018, new object[] { 2, 1, "%lu2%pr2" });
            DwgAnswerKey.Record style = Styles(DwgAnswerKey.Parse(Saved(text, "AC1018")))[0];

            Assert.DoesNotContain(style.Groups.SkipWhile(g => g.Value.Trim() != "AcDbTableStyle"), g =>
                g.Key == 90 || g.Key == 91 || g.Key == 1
            );
        }

        #endregion

        #region Going up

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void TheStylesOfAnOldDrawingAutoCadWrote_GoingToR2007_AreTheStylesAutoCadWritesThereOfThem(string old)
        {
            List<DwgAnswerKey.Record> key = DwgAnswerKey.Parse(DwgAnswerKey.Key("table_AC1021"));
            List<DwgAnswerKey.Record> saved = DwgAnswerKey.Parse(
                Saved(Text(DwgAnswerKey.Key("table_" + old)), "AC1021")
            );

            Assert.Equal(
                Styles(key).Select(Look).OrderBy(s => s, StringComparer.Ordinal),
                Styles(saved).Select(Look).OrderBy(s => s, StringComparer.Ordinal)
            );
            Assert.All(Styles(saved), style => Assert.Null(Formats(saved, style)));
            Assert.DoesNotContain(saved, r => r.Type == "XRECORD" && r.Groups.Any(g => g.Value.Trim() == Name));
        }

        [Fact]
        public void AStyleOfR2000_GoingToR2018_HasTheFormatsOfItsRowsFromItsRoundTrip_AndItsVersionGroup()
        {
            // AutoCAD 2021's R2018 file of the style whose R2000 file is the one above.
            string old = Text(
                Saved(
                    WithFormats(
                        R2018,
                        new object[] { 2, 1, "%lu2%pr2" },
                        new object[] { 512, 0, "" },
                        new object[] { 1, 2, "%lu1%pr0" }
                    ),
                    "AC1015"
                )
            );
            DwgAnswerKey.Record style = Styles(DwgAnswerKey.Parse(Saved(old, "AC1032")))[0];
            string look = Look(style);

            Assert.StartsWith("100=AcDbTableStyle 280=0 3=", look, StringComparison.Ordinal);
            Assert.Contains("283=0 90=2 91=1 1=%lu2%pr2 274=", look, StringComparison.Ordinal);
            Assert.Contains("283=0 90=512 91=0 1= 274=", look, StringComparison.Ordinal);
            Assert.Contains("283=0 90=1 91=2 1=%lu1%pr0 274=", look, StringComparison.Ordinal);
        }

        [Fact]
        public void AStyleOfR2007_GoingToR2010_HasItsVersionGroup_AndOneOfR2000GoingToR2007HasNone()
        {
            byte[] r2000 = DwgAnswerKey.Key("table_AC1015");

            Assert.All(Styles(DwgAnswerKey.Parse(Saved(Text(r2000), "AC1024"))), style =>
                Assert.StartsWith("100=AcDbTableStyle 280=0 3=", Look(style), StringComparison.Ordinal)
            );
            Assert.All(Styles(DwgAnswerKey.Parse(Saved(Text(r2000), "AC1021"))), style =>
                Assert.StartsWith("100=AcDbTableStyle 3=", Look(style), StringComparison.Ordinal)
            );
        }

        [Fact]
        public void ATableOfR2000_WithA280OfItsOwnAfterItsFlags_GoingToR2010_HasTheVersionGroupFirst()
        {
            // AutoCAD 2021 writes a 280 of nought after the 96 of a table whose title or header it left out, in R2000 too; it is
            // not the version of the table's data, which R2010 and later write first in it.
            string[] lines = Text(DwgAnswerKey.Key("table_AC1015")).Replace("\r\n", "\n").Split('\n');
            var changed = new List<string>(lines);
            int table = changed.LastIndexOf("AcDbTable");
            int at = changed.FindIndex(table, l => l.Trim() == "96");
            changed.InsertRange(at + 2, new[] { "280", "        0" });

            byte[] saved = Saved(string.Join("\r\n", changed), "AC1024");
            DwgAnswerKey.Record last = DwgAnswerKey
                .Parse(saved)
                .Last(r => r.Section == "ENTITIES" && r.Type == "ACAD_TABLE");
            List<KeyValuePair<int, string>> groups = last.Groups.SkipWhile(g => g.Value.Trim() != "AcDbTable").ToList();

            Assert.Equal(280, groups[1].Key);
            Assert.Equal(2, groups.Count(g => g.Key == 280));
        }

        [Fact]
        public void AStyleGoingDownAndUp_HasTheFormatsItHad()
        {
            string text = WithFormats(
                R2018,
                new object[] { 2, 1, "%lu2%pr2" },
                new object[] { 8, 0, "%dt4" },
                new object[] { 1, 2, "%lu1%pr0" }
            );
            string original = Look(Styles(DwgAnswerKey.Parse(Encoding.GetEncoding(28591).GetBytes(text)))[0]);

            foreach (string version in new[] { "AC1015", "AC1018" })
            {
                byte[] up = Saved(Text(Saved(text, version)), "AC1032");

                Assert.Equal(original, Look(Styles(DwgAnswerKey.Parse(up))[0]));
            }
        }

        #endregion
    }
}
