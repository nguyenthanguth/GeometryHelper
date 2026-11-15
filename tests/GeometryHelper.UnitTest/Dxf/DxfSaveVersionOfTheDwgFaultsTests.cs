using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A drawing saved as a DXF of an older version, three things AutoCAD 2021 does that the DWG writer learned first
    /// (<c>DwgWriter</c> saves through the same converter): a multileader and its style hold their version as extended data
    /// (<c>ACAD_MLEADERVER</c>, 2) before R2010, with the application in the APPID table; the vertex ids of a polyline
    /// (group 91) are of R2010 and later; and a letter written as <c>\U+1EDB</c> in a drawing of R2004 or before is the letter
    /// in R2007 and later, where AutoCAD finds the escape "Un-normalized" in a layer's name (AUDIT).
    /// </summary>
    public class DxfSaveVersionOfTheDwgFaultsTests
    {
        #region Drawings of a few records

        private static string G(params object[] pairs)
        {
            var text = new StringBuilder();

            for (int i = 0; i < pairs.Length; i += 2)
            {
                text.Append(((int)pairs[i]).ToString(CultureInfo.InvariantCulture).PadLeft(3))
                    .Append('\n')
                    .Append(Convert.ToString(pairs[i + 1], CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            return text.ToString();
        }

        private static string File(
            string version,
            string header = "",
            string tables = null,
            string entities = "",
            string objects = null
        )
        {
            var text = new StringBuilder();
            text.Append(G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, version)).Append(header).Append(G(0, "ENDSEC"));

            foreach (
                KeyValuePair<string, string> section in new[]
                {
                    new KeyValuePair<string, string>("TABLES", tables),
                    new KeyValuePair<string, string>("ENTITIES", entities),
                    new KeyValuePair<string, string>("OBJECTS", objects),
                }
            )
            {
                if (section.Value != null)
                {
                    text.Append(G(0, "SECTION", 2, section.Key)).Append(section.Value).Append(G(0, "ENDSEC"));
                }
            }

            return text.Append(G(0, "EOF")).ToString();
        }

        /// <summary>The APPID table with ACAD in it, its table's handle 9, and the version of a multileader too where <paramref name="withVersion"/>.</summary>
        private static string Applications(bool withVersion) =>
            G(0, "TABLE", 2, "APPID", 5, "9", 330, "0", 100, "AcDbSymbolTable", 70, withVersion ? 2 : 1)
            + G(
                0,
                "APPID",
                5,
                "56",
                330,
                "9",
                100,
                "AcDbSymbolTableRecord",
                100,
                "AcDbRegAppTableRecord",
                2,
                "ACAD",
                70,
                0
            )
            + (
                withVersion
                    ? G(
                        0,
                        "APPID",
                        5,
                        "C0",
                        330,
                        "9",
                        100,
                        "AcDbSymbolTableRecord",
                        100,
                        "AcDbRegAppTableRecord",
                        2,
                        "ACAD_MLEADERVER",
                        70,
                        0
                    )
                    : string.Empty
            )
            + G(0, "ENDTAB");

        private static string MultileaderStyle(string handle, string data = "") =>
            G(0, "MLEADERSTYLE", 5, handle, 330, "D", 100, "AcDbMLeaderStyle", 170, 2, 143, 0.125) + data;

        private static string Multileader(string handle, string data = "") =>
            G(0, "MULTILEADER", 5, handle, 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbMLeader", 170, 2, 40, 1.0)
            + data;

        private static string Version() => G(1001, "ACAD_MLEADERVER", 1070, 2);

        private static List<KeyValuePair<int, string>> Groups(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                groups.Add(
                    new KeyValuePair<int, string>(
                        int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture),
                        lines[i + 1].Trim()
                    )
                );
            }

            return groups;
        }

        /// <summary>The records of a section, each its groups from its first (the 0, or the 9 in the header).</summary>
        private static List<List<KeyValuePair<int, string>>> Records(string text, string section)
        {
            List<KeyValuePair<int, string>> groups = Groups(text);
            var records = new List<List<KeyValuePair<int, string>>>();
            int start = groups.FindIndex(g => g.Key == 2 && g.Value == section);

            if (start < 0)
            {
                return records;
            }

            int key = section == "HEADER" ? 9 : 0;

            for (int i = start + 1; i < groups.Count && !(groups[i].Key == 0 && groups[i].Value == "ENDSEC"); i++)
            {
                if (groups[i].Key == key)
                {
                    records.Add(new List<KeyValuePair<int, string>>());
                }

                records[records.Count - 1].Add(groups[i]);
            }

            return records;
        }

        private static List<List<KeyValuePair<int, string>>> Of(string text, string section, string type) =>
            Records(text, section).Where(r => r[0].Value == type).ToList();

        /// <summary>The drawing saved as the version, its file as UTF-8 text.</summary>
        private static string As(string text, string version)
        {
            DxfDatabase db = DxfDatabase.Parse(text);

            using (var stream = new MemoryStream())
            {
                db.Save(stream, version);
                return new UTF8Encoding(false).GetString(stream.ToArray());
            }
        }

        private static string Show(IEnumerable<KeyValuePair<int, string>> groups) =>
            string.Join(" ", groups.Select(g => g.Key + "=" + g.Value));

        /// <summary>The extended data at the end of a record, from its first group 1001.</summary>
        private static string Tail(List<KeyValuePair<int, string>> record)
        {
            int at = record.FindIndex(g => g.Key == 1001);
            return at < 0 ? string.Empty : Show(record.Skip(at));
        }

        #endregion

        #region The version of a multileader

        private static string Leaders(string seed = "FFF0") =>
            File(
                "AC1032",
                header: G(9, "$HANDSEED", 5, seed),
                tables: Applications(false),
                entities: Multileader("A6") + Multileader("A7"),
                objects: MultileaderStyle("A1")
            );

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        public void AMultileaderAndItsStyle_SavedBeforeR2010_HoldTheirVersionAsExtendedData(string version)
        {
            string saved = As(Leaders(), version);

            foreach (List<KeyValuePair<int, string>> leader in Of(saved, "ENTITIES", "MULTILEADER"))
            {
                Assert.Equal("1001=ACAD_MLEADERVER 1070=2", Tail(leader));
            }

            Assert.Equal(2, Of(saved, "ENTITIES", "MULTILEADER").Count);
            Assert.Equal("1001=ACAD_MLEADERVER 1070=2", Tail(Of(saved, "OBJECTS", "MLEADERSTYLE").Single()));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1021")]
        public void TheApplicationOfTheVersion_IsRegisteredOnceAtTheEndOfTheTableWithTheHandleAfterTheLast(
            string version
        )
        {
            string saved = As(Leaders(), version);

            List<List<KeyValuePair<int, string>>> table = Records(saved, "TABLES");
            List<KeyValuePair<int, string>> head = table.Single(r => r[0].Value == "TABLE");
            List<List<KeyValuePair<int, string>>> applications = table.Where(r => r[0].Value == "APPID").ToList();

            Assert.Equal("2", head.First(g => g.Key == 70).Value);
            Assert.Equal(new[] { "ACAD", "ACAD_MLEADERVER" }, applications.Select(a => a.First(g => g.Key == 2).Value));

            List<KeyValuePair<int, string>> added = applications.Last();
            Assert.Equal("A8", added.First(g => g.Key == 5).Value);
            Assert.Equal("9", added.First(g => g.Key == 330).Value);
            Assert.Equal("0", added.First(g => g.Key == 70).Value);
            Assert.Equal("FFF0", Records(saved, "HEADER").Single(r => r[0].Value == "$HANDSEED")[1].Value);
        }

        [Fact]
        public void ASeedThatLagsBehindTheHandles_MovesPastTheHandleTheApplicationTakes()
        {
            // The highest handle of the drawing is A7: the application takes A8, and the seed, 10, goes to A9.
            string saved = As(Leaders("10"), "AC1018");

            List<KeyValuePair<int, string>> added = Records(saved, "TABLES").Last(r => r[0].Value == "APPID");
            Assert.Equal("A8", added.First(g => g.Key == 5).Value);
            Assert.Equal("A9", Records(saved, "HEADER").Single(r => r[0].Value == "$HANDSEED")[1].Value);
        }

        [Theory]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        public void AMultileaderAndItsStyle_SavedAsR2010OrLater_HoldNoVersionAsExtendedData(string version)
        {
            string saved = As(Leaders(), version);

            Assert.DoesNotContain("ACAD_MLEADERVER", saved);
        }

        [Fact]
        public void AMultileaderThatHoldsItsVersionAlready_SavedAsR2004_HoldsItOnce()
        {
            string text = File(
                "AC1021",
                header: G(9, "$HANDSEED", 5, "FFF0"),
                tables: Applications(true),
                entities: Multileader("A6", Version()),
                objects: MultileaderStyle("A1", Version())
            );

            string saved = As(text, "AC1018");

            Assert.Equal("1001=ACAD_MLEADERVER 1070=2", Tail(Of(saved, "ENTITIES", "MULTILEADER").Single()));
            Assert.Equal(2, Records(saved, "TABLES").Count(r => r[0].Value == "APPID"));
            Assert.Equal("FFF0", Records(saved, "HEADER").Single(r => r[0].Value == "$HANDSEED")[1].Value);
        }

        #endregion

        #region The vertex ids of a polyline

        private static string Polyline(string handle) =>
            G(0, "LWPOLYLINE", 5, handle, 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbPolyline", 90, 3, 70, 0)
            + G(43, 0.0, 10, 0.0, 20, 0.0, 91, 21, 10, 10.0, 20, 0.0, 91, 22, 10, 10.0, 20, 10.0, 91, 23);

        [Theory]
        [InlineData("AC1015", 0)]
        [InlineData("AC1018", 0)]
        [InlineData("AC1021", 0)]
        [InlineData("AC1024", 3)]
        [InlineData("AC1027", 3)]
        public void TheVertexIdsOfALwPolyline_AreWrittenFromR2010Only(string version, int ids)
        {
            string saved = As(File("AC1032", entities: Polyline("A5")), version);

            List<KeyValuePair<int, string>> polyline = Of(saved, "ENTITIES", "LWPOLYLINE").Single();

            Assert.Equal(ids, polyline.Count(g => g.Key == 91));
            Assert.Equal(3, polyline.Count(g => g.Key == 10));
            Assert.Equal(3, polyline.Count(g => g.Key == 20));
        }

        #endregion

        #region The letters of an escape

        private static string Named(string version, string layer, string text) =>
            File(
                version,
                tables: G(0, "TABLE", 2, "LAYER", 5, "2", 330, "0", 100, "AcDbSymbolTable", 70, 1)
                    + G(
                        0,
                        "LAYER",
                        5,
                        "10",
                        330,
                        "2",
                        100,
                        "AcDbSymbolTableRecord",
                        100,
                        "AcDbLayerTableRecord",
                        2,
                        layer,
                        70,
                        0,
                        62,
                        7,
                        6,
                        "Continuous"
                    )
                    + G(0, "ENDTAB"),
                entities: G(0, "TEXT", 5, "A5", 330, "1F", 100, "AcDbEntity", 8, layer)
                    + G(100, "AcDbText", 10, 0.0, 20, 0.0, 30, 0.0, 40, 2.5, 1, text, 100, "AcDbText")
            );

        [Theory]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void AnEscapeOfR2004_SavedAsR2007OrLater_IsTheLetter(string version)
        {
            string saved = As(Named("AC1018", @"L\U+1EDBp \U+1EA9n", @"a\U+1EDBb\U+00E9"), version);

            Assert.DoesNotContain(@"\U+", saved);
            Assert.Equal("Lớp ẩn", Of(saved, "TABLES", "LAYER").Single().First(g => g.Key == 2).Value);
            Assert.Equal("Lớp ẩn", Of(saved, "ENTITIES", "TEXT").Single().First(g => g.Key == 8).Value);
            Assert.Equal("aớbé", Of(saved, "ENTITIES", "TEXT").Single().First(g => g.Key == 1).Value);
        }

        [Fact]
        public void AnEscapeOfR2004_SavedAsR2000_StaysAnEscape()
        {
            string saved = As(Named("AC1018", @"L\U+1EDBp", @"a\U+1EDBb"), "AC1015");

            Assert.Equal(@"L\U+1EDBp", Of(saved, "TABLES", "LAYER").Single().First(g => g.Key == 2).Value);
            Assert.Equal(@"a\U+1EDBb", Of(saved, "ENTITIES", "TEXT").Single().First(g => g.Key == 1).Value);
        }

        [Theory]
        [InlineData("AC1021", "AC1024")]
        [InlineData("AC1032", "AC1027")]
        public void AnEscapeTypedInAnR2007Text_StaysTheTextInR2007OrLater(string from, string to)
        {
            string saved = As(Named(from, "0", @"a\U+1EDBb"), to);

            Assert.Equal(@"a\U+1EDBb", Of(saved, "ENTITIES", "TEXT").Single().First(g => g.Key == 1).Value);
        }

        [Fact]
        public void AMultibyteCodeOfR2004_SavedAsR2007_IsTheLetter()
        {
            string saved = As(Named("AC1018", "0", @"a\M+182A0b"), "AC1021");

            Assert.Equal("aあb", Of(saved, "ENTITIES", "TEXT").Single().First(g => g.Key == 1).Value);
        }

        #endregion
    }
}
