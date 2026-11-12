using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A drawing saved as another version of AutoCAD, <see cref="DxfDatabase.Save(string, string)"/>: the version it names, the
    /// header variables, the groups of each record and the kinds of record that version holds, as AutoCAD 2021 writes one drawing
    /// of 1 300 lines, 130 hatches, a table and 15 multileaders in each version (R2000 to R2018), which the rules were read from;
    /// the drawings here are written in the groups of that comparison, a few records each.
    /// </summary>
    public class DxfSaveVersionTests
    {
        private static readonly string[] Versions = { "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" };

        #region Drawings of a few records

        /// <summary>Groups given as pairs, a code and a value, as the lines of a file: "  0\nLINE\n".</summary>
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

        /// <summary>A file of the version with the sections given, each of its records as <see cref="G"/> writes it.</summary>
        private static string File(
            string version,
            string header = "",
            string classes = null,
            string tables = null,
            string blocks = null,
            string entities = "",
            string objects = null
        )
        {
            var text = new StringBuilder();
            Section(text, "HEADER", G(9, "$ACADVER", 1, version) + header);

            foreach (
                KeyValuePair<string, string> section in new[]
                {
                    new KeyValuePair<string, string>("CLASSES", classes),
                    new KeyValuePair<string, string>("TABLES", tables),
                    new KeyValuePair<string, string>("BLOCKS", blocks),
                    new KeyValuePair<string, string>("ENTITIES", entities),
                    new KeyValuePair<string, string>("OBJECTS", objects),
                }
            )
            {
                if (section.Value != null)
                {
                    Section(text, section.Key, section.Value);
                }
            }

            return text.Append(G(0, "EOF")).ToString();
        }

        private static void Section(StringBuilder text, string name, string records) =>
            text.Append(G(0, "SECTION", 2, name)).Append(records).Append(G(0, "ENDSEC"));

        /// <summary>The groups of a text as (code, value) pairs, the values trimmed of the padding of whole numbers.</summary>
        private static List<KeyValuePair<int, string>> Groups(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                groups.Add(
                    new KeyValuePair<int, string>(
                        int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture),
                        lines[i + 1]
                    )
                );
            }

            return groups;
        }

        /// <summary>The records of a section of a text, each its groups from its first (the 0, or the 9 in the header).</summary>
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

        private static List<KeyValuePair<int, string>> Record(
            string text,
            string section,
            string type,
            int index = 0
        ) => Records(text, section).Where(r => r[0].Value == type).ElementAt(index);

        private static bool Has(List<KeyValuePair<int, string>> record, int code) => record.Any(g => g.Key == code);

        private static string Value(List<KeyValuePair<int, string>> record, int code) =>
            record.First(g => g.Key == code).Value.Trim();

        /// <summary>The drawing saved as the version and read again as text; what was said of it comes out.</summary>
        private static string As(string text, string version, out IReadOnlyList<string> said)
        {
            DxfDatabase db = DxfDatabase.Parse(text);

            using (var stream = new MemoryStream())
            {
                said = db.Save(stream, version);
                return TextOf(stream.ToArray());
            }
        }

        private static string As(string text, string version) => As(text, version, out _);

        /// <summary>An entity of the group 0 and its handle, the layer 0, on the owner 1F.</summary>
        private static string Head(string type, string handle) =>
            G(0, type, 5, handle, 330, "1F", 100, "AcDbEntity", 8, "0");

        #endregion

        #region What is saved

        [Fact]
        public void ADrawingSavedInItsOwnVersion_IsWrittenByteForByteAsSaveWritesIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            byte[] plain = Saved(db);

            using (var stream = new MemoryStream())
            {
                IReadOnlyList<string> said = db.Save(stream, "AC1032");

                Assert.Equal(plain, stream.ToArray());
                Assert.Empty(said);
            }
        }

        [Fact]
        public void ADrawingSavedInItsOwnVersionToAPath_IsTheFileSaveWrites()
        {
            string path = Path.Combine(Path.GetTempPath(), "gh-save-own-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
                db.Save(path, "ac1015");

                Assert.Equal(Saved(db), System.IO.File.ReadAllBytes(path));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void ADrawingOfR2018_SavedAsEachVersion_IsOfThatVersionAndKeepsItsEntities()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            string[] kinds = Everything(source)
                .Select(e => e.GetType().Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            Assert.NotEmpty(kinds);

            foreach (string version in Versions)
            {
                using (var stream = new MemoryStream())
                {
                    source.Save(stream, version);
                    DxfDatabase saved = Opened(stream.ToArray());

                    Assert.Equal(version, saved.Version);
                    Assert.Equal(
                        kinds,
                        Everything(saved)
                            .Select(e => e.GetType().Name)
                            .OrderBy(n => n, StringComparer.Ordinal)
                            .ToArray()
                    );
                }
            }
        }

        [Fact]
        public void ADrawingOfR2000_SavedAsEachVersion_IsOfThatVersionAndKeepsItsEntities()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000);
            string[] kinds = Everything(source)
                .Select(e => e.GetType().Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            foreach (string version in Versions)
            {
                using (var stream = new MemoryStream())
                {
                    source.Save(stream, version);
                    DxfDatabase saved = Opened(stream.ToArray());

                    Assert.Equal(version, saved.Version);
                    Assert.Equal(
                        kinds,
                        Everything(saved)
                            .Select(e => e.GetType().Name)
                            .OrderBy(n => n, StringComparer.Ordinal)
                            .ToArray()
                    );
                }
            }
        }

        [Fact]
        public void TheDrawingInTheMemory_StaysOfItsOwnVersionAndItsFilenameBecomesThePath()
        {
            string path = Path.Combine(Path.GetTempPath(), "gh-save-name-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
                db.Save(path, "AC1024");

                Assert.Equal("AC1032", db.Version);
                Assert.Equal(Path.GetFullPath(path), db.Filename);
                Assert.Equal("AC1024", DxfDatabase.Open(path).Version);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void ANullPathVersionOrStream_IsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

            Assert.Throws<ArgumentNullException>(() => db.Save((string)null, "AC1015"));
            Assert.Throws<ArgumentNullException>(() => db.Save("any.dxf", null));
            Assert.Throws<ArgumentNullException>(() => db.Save((Stream)null, "AC1015"));
            Assert.Throws<ArgumentNullException>(() => db.Save(new MemoryStream(), null));
        }

        [Theory]
        [InlineData("AC1014")]
        [InlineData("AC1033")]
        [InlineData("R2000")]
        [InlineData("")]
        public void AVersionThatIsNoneOfTheSeven_IsRefused(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

            using (var stream = new MemoryStream())
            {
                ArgumentException refused = Assert.Throws<ArgumentException>(() => db.Save(stream, version));
                Assert.Equal("version", refused.ParamName);
                Assert.Equal(0, stream.Length);
            }
        }

        [Fact]
        public void ADrawingOfR12_SavedAsAnotherVersion_IsRefusedAndSavedAsItIsInItsOwn()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);

            using (var stream = new MemoryStream())
            {
                Assert.Throws<NotSupportedException>(() => db.Save(stream, "AC1015"));
                Assert.Equal(0, stream.Length);
            }
        }

        #endregion

        #region The header

        private const string Header2018 =
            "  9\n$ACADMAINTVER\n 90\n      329\n  9\n$LASTSAVEDBY\n  1\ntester\n  9\n$DIMFXL\n 40\n1.0\n"
            + "  9\n$DIMTXTDIRECTION\n 70\n     0\n  9\n$REQUIREDVERSIONS\n160\n0\n  9\n$XCLIPFRAME\n280\n     2\n"
            + "  9\n$INSUNITS\n 70\n     4\n";

        [Fact]
        public void TheHeaderVariables_AreThoseTheVersionHas()
        {
            string text = File("AC1032", Header2018);

            Func<string, string, bool> has = (version, name) =>
                Records(As(text, version), "HEADER").Any(r => r[0].Value == name);

            Assert.False(has("AC1015", "$LASTSAVEDBY"));
            Assert.True(has("AC1018", "$LASTSAVEDBY"));
            Assert.False(has("AC1018", "$DIMFXL"));
            Assert.True(has("AC1021", "$DIMFXL"));
            Assert.False(has("AC1021", "$DIMTXTDIRECTION"));
            Assert.True(has("AC1024", "$DIMTXTDIRECTION"));
            Assert.False(has("AC1024", "$REQUIREDVERSIONS"));
            Assert.True(has("AC1027", "$REQUIREDVERSIONS"));

            foreach (string version in Versions)
            {
                Assert.True(has(version, "$INSUNITS"));
                Assert.True(has(version, "$ACADVER"));
                Assert.Equal(version, Value(Record(As(text, version), "HEADER", "$ACADVER"), 1));
            }
        }

        [Fact]
        public void TheMaintenanceVersion_IsAutoCAD2021sOfEachVersionAndGroup90FromR2018()
        {
            string text = File("AC1032", Header2018);
            var expected = new Dictionary<string, KeyValuePair<int, string>>
            {
                ["AC1015"] = new KeyValuePair<int, string>(70, "18"),
                ["AC1018"] = new KeyValuePair<int, string>(70, "104"),
                ["AC1021"] = new KeyValuePair<int, string>(70, "50"),
                ["AC1024"] = new KeyValuePair<int, string>(70, "226"),
                ["AC1027"] = new KeyValuePair<int, string>(70, "125"),
                ["AC1032"] = new KeyValuePair<int, string>(90, "174"),
            };

            foreach (string version in Versions.Where(v => v != "AC1032"))
            {
                List<KeyValuePair<int, string>> record = Record(As(text, version), "HEADER", "$ACADMAINTVER");
                Assert.Equal(expected[version].Key, record[1].Key);
                Assert.Equal(expected[version].Value, record[1].Value.Trim());
            }

            // R2000 saved as R2018 is given R2018's group.
            string old = File("AC1015", "  9\n$ACADMAINTVER\n 70\n    18\n");
            List<KeyValuePair<int, string>> grown = Record(As(old, "AC1032"), "HEADER", "$ACADMAINTVER");
            Assert.Equal(90, grown[1].Key);
            Assert.Equal("174", grown[1].Value.Trim());
        }

        [Fact]
        public void TheFrameOfAnXrefClip_IsAFlagBeforeR2010AndANumberFromThen()
        {
            string text = File("AC1032", Header2018);

            Assert.Equal(290, Record(As(text, "AC1018"), "HEADER", "$XCLIPFRAME")[1].Key);
            Assert.Equal("0", Value(Record(As(text, "AC1018"), "HEADER", "$XCLIPFRAME"), 290));
            Assert.Equal(280, Record(As(text, "AC1024"), "HEADER", "$XCLIPFRAME")[1].Key);
            Assert.Equal("2", Value(Record(As(text, "AC1027"), "HEADER", "$XCLIPFRAME"), 280));
        }

        #endregion

        #region The groups of a record

        [Fact]
        public void TrueColourAndTransparency_AreLeftOutBeforeR2004()
        {
            string text = File(
                "AC1032",
                entities: Head("LINE", "A1")
                    + G(
                        62,
                        1,
                        420,
                        16711680,
                        440,
                        33554432,
                        370,
                        25,
                        100,
                        "AcDbLine",
                        10,
                        0.0,
                        20,
                        0.0,
                        30,
                        0.0,
                        11,
                        1.0,
                        21,
                        0.0,
                        31,
                        0.0
                    )
            );

            List<KeyValuePair<int, string>> r2000 = Record(As(text, "AC1015"), "ENTITIES", "LINE");
            Assert.False(Has(r2000, 420));
            Assert.False(Has(r2000, 440));
            Assert.Equal("1", Value(r2000, 62));
            Assert.Equal("25", Value(r2000, 370));

            List<KeyValuePair<int, string>> r2004 = Record(As(text, "AC1018"), "ENTITIES", "LINE");
            Assert.Equal("16711680", Value(r2004, 420));
            Assert.Equal("33554432", Value(r2004, 440));
        }

        [Fact]
        public void TheGroupsOfASubclass_AreThoseTheVersionHas()
        {
            // 347 of an entity and 63, 90, 421 of an MTEXT background came with R2004 and R2007; 46, the defined height, with R2007.
            string text = File(
                "AC1032",
                entities: Head("MTEXT", "A2")
                    + G(
                        347,
                        "9A",
                        100,
                        "AcDbMText",
                        10,
                        0.0,
                        20,
                        0.0,
                        30,
                        0.0,
                        40,
                        2.5,
                        41,
                        20.0,
                        46,
                        5.0,
                        71,
                        1,
                        72,
                        5,
                        1,
                        "words"
                    )
                    + G(45, 1.5, 90, 1, 63, 5, 421, 255, 441, 0)
            );

            List<KeyValuePair<int, string>> r2007 = Record(As(text, "AC1021"), "ENTITIES", "MTEXT");
            Assert.True(Has(r2007, 347));
            Assert.True(Has(r2007, 46));
            Assert.True(Has(r2007, 90));

            List<KeyValuePair<int, string>> r2004 = Record(As(text, "AC1018"), "ENTITIES", "MTEXT");
            Assert.False(Has(r2004, 46));
            Assert.True(Has(r2004, 90));
            Assert.True(Has(r2004, 421));

            List<KeyValuePair<int, string>> r2000 = Record(As(text, "AC1015"), "ENTITIES", "MTEXT");
            Assert.False(Has(r2000, 45));
            Assert.False(Has(r2000, 63));
            Assert.False(Has(r2000, 90));
            Assert.False(Has(r2000, 421));
            Assert.False(Has(r2000, 441));
            Assert.False(Has(r2000, 347));
            Assert.Equal("words", Value(r2000, 1));
        }

        [Fact]
        public void TheSizeOfTheGraphicsOfAProxy_IsGroup92BeforeR2010AndGroup160FromThen()
        {
            string text = File(
                "AC1032",
                entities: Head("MESH", "A3") + G(160, 4, 310, "AABBCCDD", 100, "AcDbSubDMesh", 71, 2)
            );

            Assert.True(Has(Record(As(text, "AC1021"), "ENTITIES", "MESH"), 92));
            Assert.False(Has(Record(As(text, "AC1021"), "ENTITIES", "MESH"), 160));
            Assert.Equal("4", Value(Record(As(text, "AC1021"), "ENTITIES", "MESH"), 92));
            Assert.True(Has(Record(As(text, "AC1024"), "ENTITIES", "MESH"), 160));
            Assert.Equal("AABBCCDD", Value(Record(As(text, "AC1015"), "ENTITIES", "MESH"), 310));

            string old = File(
                "AC1021",
                entities: Head("MESH", "A3") + G(92, 4, 310, "AABBCCDD", 100, "AcDbSubDMesh", 71, 2)
            );
            Assert.True(Has(Record(As(old, "AC1032"), "ENTITIES", "MESH"), 160));
            Assert.False(Has(Record(As(old, "AC1032"), "ENTITIES", "MESH"), 92));
        }

        [Fact]
        public void TheGroupsOfADimensionStyle_AreThoseOfTheVariablesTheVersionHas()
        {
            // DIMTFILL (69) and the second 70, DIMTFILLCLR, and the handles of the line types (345 to 347) came with R2007.
            string tables = G(
                0,
                "TABLE",
                2,
                "DIMSTYLE",
                5,
                "A",
                330,
                0,
                100,
                "AcDbSymbolTable",
                70,
                1,
                0,
                "DIMSTYLE",
                105,
                "E1",
                330,
                "A",
                100,
                "AcDbSymbolTableRecord",
                100,
                "AcDbDimStyleTableRecord",
                2,
                "Std",
                70,
                0,
                40,
                1.0,
                69,
                1,
                70,
                5,
                345,
                "11",
                340,
                "12",
                0,
                "ENDTAB"
            );
            string text = File("AC1032", tables: tables);

            List<KeyValuePair<int, string>> style = Record(As(text, "AC1015"), "TABLES", "DIMSTYLE");
            Assert.False(Has(style, 69));
            Assert.False(Has(style, 345));
            Assert.Single(style, g => g.Key == 70);
            Assert.Equal("0", style.First(g => g.Key == 70).Value.Trim());
            Assert.True(Has(style, 340));
            Assert.True(Has(style, 40));

            Assert.True(Has(Record(As(text, "AC1021"), "TABLES", "DIMSTYLE"), 69));
            Assert.Equal(2, Record(As(text, "AC1021"), "TABLES", "DIMSTYLE").Count(g => g.Key == 70));
        }

        [Fact]
        public void TheOverridesOfADimension_OfVariablesALaterVersionBroughtAreLeftOut()
        {
            // DIMLTYPE (345) and DIMFXLEN (49) came with R2007; DIMASZ (41) was R12's.
            string dimension =
                Head("DIMENSION", "A4")
                + G(100, "AcDbDimension", 2, "*D1", 10, 0.0, 20, 0.0, 30, 0.0, 70, 32, 100, "AcDbAlignedDimension")
                + G(
                    1001,
                    "ACAD",
                    1000,
                    "DSTYLE",
                    1002,
                    "{",
                    1070,
                    41,
                    1040,
                    3.0,
                    1070,
                    49,
                    1040,
                    2.0,
                    1070,
                    345,
                    1005,
                    "12",
                    1070,
                    140,
                    1040,
                    1.5,
                    1002,
                    "}"
                );
            string text = File("AC1032", entities: dimension);

            List<KeyValuePair<int, string>> r2000 = Record(As(text, "AC1015"), "ENTITIES", "DIMENSION");
            string[] codes = r2000.Where(g => g.Key == 1070).Select(g => g.Value.Trim()).ToArray();
            Assert.Equal(new[] { "41", "140" }, codes);
            Assert.Equal(1, r2000.Count(g => g.Key == 1002 && g.Value == "{"));
            Assert.Equal(1, r2000.Count(g => g.Key == 1002 && g.Value == "}"));

            Assert.Equal(
                new[] { "41", "49", "345", "140" },
                Record(As(text, "AC1021"), "ENTITIES", "DIMENSION")
                    .Where(g => g.Key == 1070)
                    .Select(g => g.Value.Trim())
                    .ToArray()
            );
        }

        [Fact]
        public void TheDimensionVersionOfR2010AndTheTypeOfAnArcDimension_AreThoseOfTheVersion()
        {
            string dimension =
                Head("ARC_DIMENSION", "A5")
                + G(100, "AcDbDimension", 2, "*D2", 70, 40, 280, 0, 100, "AcDbArcDimension", 40, 1.0);
            string text = File("AC1032", entities: dimension);

            List<KeyValuePair<int, string>> r2010 = Record(As(text, "AC1024"), "ENTITIES", "ARC_DIMENSION");
            Assert.True(Has(r2010, 280));
            Assert.Equal("37", r2010.First(g => g.Key == 70).Value.Trim());

            List<KeyValuePair<int, string>> r2007 = Record(As(text, "AC1021"), "ENTITIES", "ARC_DIMENSION");
            Assert.False(Has(r2007, 280));
            Assert.Equal("37", r2007.First(g => g.Key == 70).Value.Trim());

            string older = File(
                "AC1021",
                entities: Head("ARC_DIMENSION", "A5")
                    + G(100, "AcDbDimension", 2, "*D2", 70, 37, 100, "AcDbArcDimension", 40, 1.0)
            );
            Assert.Equal(
                "40",
                Record(As(older, "AC1032"), "ENTITIES", "ARC_DIMENSION").First(g => g.Key == 70).Value.Trim()
            );
        }

        [Fact]
        public void AMultileaderOfR2013OrLater_LosesWhatTheOlderVersionsHaveNot()
        {
            // 271 to 273 came with R2010, and the 295 after them, whether the leader extends to the text, with R2013; the
            // 295 before them, whether the text is a block, is every version's.
            string text = File(
                "AC1032",
                entities: Head("MULTILEADER", "A6")
                    + G(
                        100,
                        "AcDbMLeader",
                        270,
                        2,
                        300,
                        "CONTEXT_DATA{",
                        294,
                        0,
                        295,
                        0,
                        296,
                        0,
                        301,
                        "}",
                        271,
                        0,
                        272,
                        9,
                        273,
                        9,
                        295,
                        1
                    )
            );

            List<KeyValuePair<int, string>> r2013 = Record(As(text, "AC1027"), "ENTITIES", "MULTILEADER");
            Assert.Equal(2, r2013.Count(g => g.Key == 295));
            Assert.True(Has(r2013, 272));

            List<KeyValuePair<int, string>> r2010 = Record(As(text, "AC1024"), "ENTITIES", "MULTILEADER");
            Assert.Single(r2010, g => g.Key == 295);
            Assert.True(Has(r2010, 272));

            List<KeyValuePair<int, string>> r2007 = Record(As(text, "AC1021"), "ENTITIES", "MULTILEADER");
            Assert.Single(r2007, g => g.Key == 295);
            Assert.False(Has(r2007, 270));
            Assert.False(Has(r2007, 271));
            Assert.False(Has(r2007, 272));
            Assert.False(Has(r2007, 273));
        }

        #endregion

        #region Text

        [Fact]
        public void TheParagraphsOfAnMText_AreEndedByBackslashPInR2000AndByTheLineFeedAfter()
        {
            string text = File(
                "AC1032",
                entities: Head("MTEXT", "A7")
                    + G(100, "AcDbMText", 10, 0.0, 20, 0.0, 30, 0.0, 40, 2.5, 41, 20.0, 71, 1, 72, 5, 1, "one^Jtwo")
            );

            Assert.Equal("one\\Ptwo", Value(Record(As(text, "AC1015"), "ENTITIES", "MTEXT"), 1));
            Assert.Equal("one^Jtwo", Value(Record(As(text, "AC1018"), "ENTITIES", "MTEXT"), 1));
            Assert.Equal("one^Jtwo", Value(Record(As(text, "AC1032"), "ENTITIES", "MTEXT"), 1));
        }

        [Fact]
        public void TheColumnsOfAnMTextOfR2018_AreInItsExtendedDataBeforeIt()
        {
            string mtext =
                Head("MTEXT", "A8")
                + G(
                    100,
                    "AcDbMText",
                    10,
                    0.0,
                    20,
                    0.0,
                    30,
                    0.0,
                    40,
                    2.5,
                    41,
                    20.0,
                    71,
                    1,
                    72,
                    5,
                    1,
                    "a",
                    73,
                    1,
                    44,
                    1.0
                )
                + G(
                    101,
                    "Embedded Object",
                    70,
                    1,
                    10,
                    1.0,
                    20,
                    0.0,
                    30,
                    0.0,
                    11,
                    0.0,
                    21,
                    0.0,
                    31,
                    0.0,
                    40,
                    20.0,
                    41,
                    0.0,
                    42,
                    13.5,
                    43,
                    2.9
                )
                + G(71, 2, 72, 3, 44, 15.0, 45, 5.0, 73, 0, 74, 0, 46, 7.0, 46, 8.0, 46, 9.0);
            string text = File("AC1032", entities: mtext);

            List<KeyValuePair<int, string>> r2018 = Record(As(text, "AC1032"), "ENTITIES", "MTEXT");
            Assert.True(Has(r2018, 101));

            foreach (string version in new[] { "AC1027", "AC1021", "AC1015" })
            {
                List<KeyValuePair<int, string>> record = Record(As(text, version), "ENTITIES", "MTEXT");

                Assert.False(Has(record, 101));
                Assert.False(Has(record, 42));
                Assert.Equal("a", Value(record, 1));
                int app = record.FindIndex(g => g.Key == 1001 && g.Value == "ACAD");
                Assert.True(app > 0);
                string[] data = record.Skip(app + 1).Select(g => g.Key + "=" + g.Value.Trim()).ToArray();
                Assert.Equal(
                    new[]
                    {
                        "1000=ACAD_MTEXT_COLUMN_INFO_BEGIN",
                        "1070=75",
                        "1070=2",
                        "1070=79",
                        "1070=0",
                        "1070=76",
                        "1070=3",
                        "1070=78",
                        "1070=0",
                        "1070=48",
                        "1040=15",
                        "1070=49",
                        "1040=5",
                        "1070=50",
                        "1070=3",
                        "1040=7",
                        "1040=8",
                        "1040=9",
                        "1000=ACAD_MTEXT_COLUMN_INFO_END",
                    },
                    data
                );
            }
        }

        [Fact]
        public void TheColumnsOfAnMTextWithExtendedDataOfItsOwn_JoinThemInTheOnesOfACAD()
        {
            string mtext =
                Head("MTEXT", "A9")
                + G(100, "AcDbMText", 10, 0.0, 20, 0.0, 30, 0.0, 40, 2.5, 41, 20.0, 71, 1, 72, 5, 1, "a")
                + G(101, "Embedded Object", 70, 1, 71, 2, 72, 1, 44, 15.0, 45, 5.0, 73, 0, 74, 0, 46, 0.0)
                + G(1001, "ACAD", 1000, "MARK", 1070, 7, 1001, "OTHER", 1000, "kept");
            string text = File("AC1032", entities: mtext);

            List<KeyValuePair<int, string>> record = Record(As(text, "AC1027"), "ENTITIES", "MTEXT");
            string[] data = record.SkipWhile(g => g.Key != 1001).Select(g => g.Key + "=" + g.Value.Trim()).ToArray();

            Assert.Equal("1001=ACAD", data[0]);
            Assert.Equal("1000=MARK", data[1]);
            Assert.Equal("1000=ACAD_MTEXT_COLUMN_INFO_BEGIN", data[3]);
            Assert.Equal("1000=ACAD_MTEXT_COLUMN_INFO_END", data[data.Length - 3]);
            Assert.Equal(new[] { "1001=OTHER", "1000=kept" }, data.Skip(data.Length - 2).ToArray());
        }

        [Fact]
        public void AnAttributeOfSeveralLinesOfR2018_IsAnAttributeOfOneTextBeforeIt()
        {
            string attribute =
                Head("ATTRIB", "AA")
                + G(100, "AcDbText", 10, 1.0, 20, 2.0, 30, 0.0, 40, 2.5, 1, "")
                + G(100, "AcDbAttribute", 280, 0, 2, "NOTE", 70, 0, 280, 1, 71, 2, 72, 0, 11, 1.0, 21, 2.0, 31, 0.0)
                + G(
                    101,
                    "Embedded Object",
                    10,
                    1.0,
                    20,
                    2.0,
                    30,
                    0.0,
                    40,
                    2.5,
                    41,
                    0.0,
                    46,
                    0.0,
                    71,
                    1,
                    72,
                    5,
                    1,
                    "two\\Plines",
                    73,
                    1,
                    44,
                    1.0
                )
                + G(1001, "AcDbXdata", 1000, "kept");
            string text = File("AC1032", entities: attribute);

            List<KeyValuePair<int, string>> r2018 = Record(As(text, "AC1032"), "ENTITIES", "ATTRIB");
            Assert.True(Has(r2018, 101));

            foreach (string version in new[] { "AC1027", "AC1024", "AC1021", "AC1018", "AC1015" })
            {
                List<KeyValuePair<int, string>> record = Record(As(text, version), "ENTITIES", "ATTRIB");

                Assert.False(Has(record, 101));
                Assert.False(Has(record, 71));
                Assert.Equal("two\\Plines", Value(record, 1));
                Assert.Equal("NOTE", Value(record, 2));
                Assert.Equal("kept", Value(record, 1000));
            }

            // The attribute's version (280) came with R2010 and its lock (280) with R2007.
            Assert.Equal(2, Record(As(text, "AC1027"), "ENTITIES", "ATTRIB").Count(g => g.Key == 280));
            Assert.Single(Record(As(text, "AC1021"), "ENTITIES", "ATTRIB"), g => g.Key == 280);
            Assert.False(Has(Record(As(text, "AC1015"), "ENTITIES", "ATTRIB"), 280));
        }

        [Fact]
        public void TextBeyondTheCodePage_IsEscapedBeforeR2007AndUtf8AfterIt()
        {
            string text = File(
                "AC1032",
                "  9\n$DWGCODEPAGE\n  3\nANSI_1252\n",
                entities: Head("MTEXT", "AB")
                    + G(100, "AcDbMText", 10, 0.0, 20, 0.0, 30, 0.0, 40, 2.5, 41, 20.0, 71, 1, 72, 5, 1, "café Đ")
            );
            DxfDatabase source = DxfDatabase.Parse(text);

            foreach (string version in Versions)
            {
                using (var stream = new MemoryStream())
                {
                    source.Save(stream, version);
                    byte[] bytes = stream.ToArray();
                    DxfDatabase saved = Opened(bytes);
                    string kept = Value(Record(saved.ToText(), "ENTITIES", "MTEXT"), 1);

                    if (version == "AC1015" || version == "AC1018")
                    {
                        Assert.Equal("café \\U+0110", kept);
                        Assert.Contains((byte)0xE9, bytes);
                        Assert.DoesNotContain((byte)0xC3, bytes);
                    }
                    else
                    {
                        Assert.Equal("café Đ", kept);
                        Assert.Contains("café Đ", Encoding.UTF8.GetString(bytes));
                    }
                }
            }
        }

        #endregion

        #region Hatches

        // A hatch of one path of one spline edge, the edge with no fit points: 94 degree, 73 rational, 74 periodic, 95 knots,
        // 96 control points, the knots, the control points, then (R2010 on) the count of fit points, 97 of nought.
        private static string Hatch(bool gradient, bool fitCount, string colour = "")
        {
            string entity =
                Head("HATCH", "AC")
                + colour
                + G(
                    100,
                    "AcDbHatch",
                    10,
                    0.0,
                    20,
                    0.0,
                    30,
                    0.0,
                    210,
                    0.0,
                    220,
                    0.0,
                    230,
                    1.0,
                    2,
                    "SOLID",
                    70,
                    1,
                    71,
                    0,
                    91,
                    1
                );
            entity += G(92, 1, 93, 1, 72, 4, 73, 0, 74, 0, 94, 1, 95, 4, 96, 2);
            entity += G(40, 0.0, 40, 0.0, 40, 1.0, 40, 1.0, 10, 0.0, 20, 0.0, 10, 5.0, 20, 5.0);
            entity += fitCount ? G(97, 0) : string.Empty;
            entity += G(97, 0, 75, 1, 76, 1, 47, 0.1, 98, 0);
            entity += gradient
                ? G(
                    450,
                    1,
                    451,
                    0,
                    460,
                    0.0,
                    461,
                    0.0,
                    452,
                    0,
                    462,
                    0.0,
                    453,
                    2,
                    463,
                    0.0,
                    63,
                    5,
                    421,
                    255,
                    463,
                    1.0,
                    63,
                    2,
                    421,
                    16776960,
                    470,
                    "LINEAR"
                )
                : string.Empty;
            return entity;
        }

        [Fact]
        public void ASplineEdgeOfAHatch_HasItsCountOfFitPointsFromR2010()
        {
            string r2010 = File("AC1024", entities: Hatch(false, true));
            string r2007 = File("AC1021", entities: Hatch(false, false));

            foreach (string version in new[] { "AC1015", "AC1018", "AC1021" })
            {
                // The count of the path's source objects (97) stays, the spline's own goes.
                Assert.Single(Record(As(r2010, version), "ENTITIES", "HATCH"), g => g.Key == 97);
            }

            foreach (string version in new[] { "AC1024", "AC1027", "AC1032" })
            {
                Assert.Equal(2, Record(As(r2010, version), "ENTITIES", "HATCH").Count(g => g.Key == 97));
                Assert.Equal(2, Record(As(r2007, version), "ENTITIES", "HATCH").Count(g => g.Key == 97));
            }

            List<KeyValuePair<int, string>> grown = Record(As(r2007, "AC1024"), "ENTITIES", "HATCH");
            int fit = grown.FindIndex(g => g.Key == 97);
            Assert.Equal(20, grown[fit - 1].Key);
            Assert.Equal("5", grown[fit - 1].Value);
            Assert.Equal("0", grown[fit].Value.Trim());
            Assert.Equal(97, grown[fit + 1].Key);
        }

        [Fact]
        public void AHatchWithAGradient_IsFilledSolidWithItsFirstColourBeforeR2004()
        {
            string text = File("AC1032", entities: Hatch(true, true));

            List<KeyValuePair<int, string>> r2000 = Record(As(text, "AC1015"), "ENTITIES", "HATCH");
            foreach (int code in new[] { 450, 451, 452, 453, 460, 461, 462, 463, 470, 63, 421 })
            {
                Assert.False(Has(r2000, code), "group " + code);
            }

            Assert.Equal("5", Value(r2000, 62));
            Assert.True(
                r2000.FindIndex(g => g.Key == 62) < r2000.FindIndex(g => g.Key == 100 && g.Value == "AcDbHatch")
            );

            List<KeyValuePair<int, string>> r2004 = Record(As(text, "AC1018"), "ENTITIES", "HATCH");
            Assert.Equal("LINEAR", Value(r2004, 470));
            Assert.False(Has(r2004, 62));
        }

        [Fact]
        public void AHatchWithAGradientAndAColourOfItsOwn_TakesTheColourOfTheGradientBeforeR2004()
        {
            string text = File("AC1032", entities: Hatch(true, true, G(62, 3)));

            Assert.Equal(
                new[] { "5" },
                Record(As(text, "AC1015"), "ENTITIES", "HATCH")
                    .Where(g => g.Key == 62)
                    .Select(g => g.Value.Trim())
                    .ToArray()
            );
        }

        #endregion

        #region Records that go, and what points at them

        private static string Classes(int count) =>
            G(
                0,
                "CLASS",
                1,
                "DICTIONARYVAR",
                2,
                "AcDbDictionaryVar",
                3,
                "ObjectDBX Classes",
                90,
                0,
                91,
                count,
                280,
                0,
                281,
                0
            ) + G(0, "CLASS", 1, "XRECORD", 2, "AcDbXrecord", 3, "ObjectDBX Classes", 90, 0, 91, 9, 280, 0, 281, 0);

        [Fact]
        public void TheClassesAreCountedFromR2004AndNotBefore()
        {
            string objects =
                G(0, "DICTIONARYVAR", 5, "B1", 330, "A", 100, "DictionaryVariables", 280, 0, 1, "1")
                + G(0, "DICTIONARYVAR", 5, "B2", 330, "A", 100, "DictionaryVariables", 280, 0, 1, "2")
                + G(0, "XRECORD", 5, "B3", 330, "A", 100, "AcDbXrecord", 280, 1);
            string text = File("AC1032", classes: Classes(7), objects: objects);

            foreach (string version in new[] { "AC1018", "AC1021", "AC1024", "AC1027" })
            {
                List<List<KeyValuePair<int, string>>> classes = Records(As(text, version), "CLASSES");
                Assert.Equal("2", Value(classes.Single(c => Value(c, 1) == "DICTIONARYVAR"), 91));
                Assert.Equal("1", Value(classes.Single(c => Value(c, 1) == "XRECORD"), 91));
            }

            Assert.All(Records(As(text, "AC1015"), "CLASSES"), c => Assert.False(Has(c, 91)));

            // R2000 saved as R2004 is counted too, which AutoCAD opens no drawing of R2004 without.
            string old = File(
                "AC1015",
                classes: G(0, "CLASS", 1, "XRECORD", 2, "AcDbXrecord", 3, "ObjectDBX Classes", 90, 0, 280, 0, 281, 0),
                objects: objects
            );
            List<KeyValuePair<int, string>> counted = Records(As(old, "AC1018"), "CLASSES").Single();
            Assert.Equal("1", Value(counted, 91));
            Assert.True(counted.FindIndex(g => g.Key == 91) == counted.FindIndex(g => g.Key == 90) + 1);
        }

        [Fact]
        public void TheClassesOfTheLayoutsAndPlaceHolders_AreListedInR2000AndNotAfterIt()
        {
            string objects =
                G(0, "LAYOUT", 5, "B1", 330, "A", 100, "AcDbPlotSettings", 1, "")
                + G(0, "ACDBPLACEHOLDER", 5, "B2", 330, "A");
            string plain = File(
                "AC1032",
                classes: G(
                    0,
                    "CLASS",
                    1,
                    "XRECORD",
                    2,
                    "AcDbXrecord",
                    3,
                    "ObjectDBX Classes",
                    90,
                    0,
                    91,
                    0,
                    280,
                    0,
                    281,
                    0
                ),
                objects: objects
            );

            string[] names = Records(As(plain, "AC1015"), "CLASSES").Select(c => Value(c, 1)).ToArray();
            Assert.Equal(new[] { "XRECORD", "ACDBPLACEHOLDER", "LAYOUT" }, names);
            Assert.Equal(
                "AcDbLayout",
                Value(Records(As(plain, "AC1015"), "CLASSES").Single(c => Value(c, 1) == "LAYOUT"), 2)
            );

            string listed = File(
                "AC1015",
                classes: Classes(0)
                    + G(
                        0,
                        "CLASS",
                        1,
                        "ACDBPLACEHOLDER",
                        2,
                        "AcDbPlaceHolder",
                        3,
                        "ObjectDBX Classes",
                        90,
                        0,
                        280,
                        0,
                        281,
                        0
                    ),
                objects: objects
            );
            Assert.Equal(
                new[] { "DICTIONARYVAR", "XRECORD" },
                Records(As(listed, "AC1018"), "CLASSES").Select(c => Value(c, 1)).ToArray()
            );
        }

        private const string RootAndStyles =
            "  0\nDICTIONARY\n  5\nC\n330\n0\n100\nAcDbDictionary\n281\n     1\n  3\nACAD_VISUALSTYLE\n350\nD1\n  3\nACAD_GROUP\n350\nD2\n"
            + "  0\nDICTIONARY\n  5\nD1\n102\n{ACAD_REACTORS\n330\nC\n102\n}\n330\nC\n100\nAcDbDictionary\n281\n     1\n  3\n2dWireframe\n350\nV1\n  3\nConceptual\n350\nV2\n"
            + "  0\nVISUALSTYLE\n  5\nV1\n102\n{ACAD_REACTORS\n330\nD1\n102\n}\n330\nD1\n100\nAcDbVisualStyle\n  2\n2dWireframe\n 70\n     4\n"
            + "  0\nVISUALSTYLE\n  5\nV2\n330\nD1\n100\nAcDbVisualStyle\n  2\nConceptual\n 70\n     4\n"
            + "  0\nDICTIONARY\n  5\nD2\n330\nC\n100\nAcDbDictionary\n281\n     1\n";

        [Fact]
        public void TheVisualStyles_AreLeftOutUnlessTheVersionsAreBothR2013OrLater()
        {
            string text = File("AC1032", objects: RootAndStyles);

            foreach (string version in new[] { "AC1015", "AC1018", "AC1021", "AC1024" })
            {
                IReadOnlyList<string> said;
                string saved = As(text, version, out said);
                List<List<KeyValuePair<int, string>>> objects = Records(saved, "OBJECTS");

                Assert.DoesNotContain(objects, o => o[0].Value == "VISUALSTYLE");
                Assert.Contains(said, s => s.Contains("VISUALSTYLE 2"));

                // The dictionary of them, emptied, goes with its name in the root, and the groups dictionary stays.
                Assert.DoesNotContain(objects, o => Has(o, 5) && Value(o, 5) == "D1");
                List<KeyValuePair<int, string>> root = objects.Single(o => Value(o, 5) == "C");
                Assert.DoesNotContain(root, g => g.Value == "ACAD_VISUALSTYLE");
                Assert.Contains(root, g => g.Value == "ACAD_GROUP");
                Assert.Contains(objects, o => Has(o, 5) && Value(o, 5) == "D2");
            }

            IReadOnlyList<string> kept;
            string as2013 = As(text, "AC1027", out kept);
            Assert.Equal(2, Records(as2013, "OBJECTS").Count(o => o[0].Value == "VISUALSTYLE"));
            Assert.Empty(kept);
        }

        [Fact]
        public void TheVisualStylesOfR2000ToR2007_AreKeptBetweenThemWhenTheVersionIsOlder()
        {
            string text = File(
                "AC1021",
                objects: RootAndStyles.Replace(
                    "AcDbVisualStyle\n  2\n2dWireframe",
                    "AcDbVisualStyle\n  2\n2dWireframe\n421\n16777215"
                )
            );

            string as2000 = As(text, "AC1015");
            Assert.Equal(2, Records(as2000, "OBJECTS").Count(o => o[0].Value == "VISUALSTYLE"));
            Assert.False(Has(Records(as2000, "OBJECTS").First(o => o[0].Value == "VISUALSTYLE"), 421));
            Assert.Equal(2, Records(As(text, "AC1018"), "OBJECTS").Count(o => o[0].Value == "VISUALSTYLE"));

            // Between R2007 and R2010 the visual styles are written in another form each.
            Assert.DoesNotContain(Records(As(text, "AC1024"), "OBJECTS"), o => o[0].Value == "VISUALSTYLE");
            // From R2000 up they are not turned into the form of the newer one, so they go.
            Assert.DoesNotContain(Records(As(File("AC1015", objects: RootAndStyles), "AC1021"), "OBJECTS"), o =>
                o[0].Value == "VISUALSTYLE"
            );
        }

        [Fact]
        public void APointerToARecordThatWentAndTheExtensionDictionaryOfOnlyThat_GoWithIt()
        {
            string entity = G(
                0,
                "LINE",
                5,
                "E1",
                102,
                "{ACAD_XDICTIONARY",
                360,
                "X1",
                102,
                "}",
                330,
                "1F",
                100,
                "AcDbEntity",
                8,
                "0",
                348,
                "V1",
                100,
                "AcDbLine",
                10,
                0.0,
                20,
                0.0,
                30,
                0.0,
                11,
                1.0,
                21,
                0.0,
                31,
                0.0
            );
            string objects =
                RootAndStyles
                + G(0, "DICTIONARY", 5, "X1", 330, "E1", 100, "AcDbDictionary", 281, 1, 3, "STYLE", 360, "V2");
            string text = File("AC1032", entities: entity, objects: objects);

            string saved = As(text, "AC1021");
            List<KeyValuePair<int, string>> line = Record(saved, "ENTITIES", "LINE");

            Assert.False(Has(line, 348));
            Assert.False(Has(line, 102));
            Assert.False(Has(line, 360));
            Assert.DoesNotContain(Records(saved, "OBJECTS"), o => Has(o, 5) && Value(o, 5) == "X1");
            Assert.Equal("E1", Value(line, 5));
        }

        [Fact]
        public void ABodyOfR2013OrLater_HasNoTextToWriteForR2010AndIsLeftOutThere()
        {
            // A body of R2013 holds its identity (2) and its data lives in the data store; one of R2010 holds its text (1).
            string binary = G(
                0,
                "3DSOLID",
                5,
                "F1",
                330,
                "1F",
                100,
                "AcDbEntity",
                8,
                "0",
                100,
                "AcDbModelerGeometry",
                290,
                1,
                2,
                "{abc}",
                100,
                "AcDb3dSolid",
                350,
                "H1"
            );
            string text = File(
                "AC1032",
                entities: binary
                    + G(
                        0,
                        "LINE",
                        5,
                        "F2",
                        330,
                        "1F",
                        100,
                        "AcDbEntity",
                        8,
                        "0",
                        100,
                        "AcDbLine",
                        10,
                        0.0,
                        20,
                        0.0,
                        30,
                        0.0,
                        11,
                        1.0,
                        21,
                        0.0,
                        31,
                        0.0
                    )
            );

            string as2010 = As(text, "AC1024", out IReadOnlyList<string> said);
            Assert.DoesNotContain(Records(as2010, "ENTITIES"), r => r[0].Value == "3DSOLID");
            Assert.Contains(Records(as2010, "ENTITIES"), r => r[0].Value == "LINE");
            Assert.Contains(said, s => s.Contains("3DSOLID 1"));

            string as2013 = As(text, "AC1027", out said);
            Assert.Contains(Records(as2013, "ENTITIES"), r => r[0].Value == "3DSOLID");
            Assert.Empty(said);

            // One of R2010 written in text goes up as it is, and loses its history (350, AcDb3dSolid) going down to R2004.
            string sat = G(
                0,
                "3DSOLID",
                5,
                "F1",
                330,
                "1F",
                100,
                "AcDbEntity",
                8,
                "0",
                100,
                "AcDbModelerGeometry",
                70,
                1,
                1,
                "400 0 1 0",
                100,
                "AcDb3dSolid",
                350,
                "H1"
            );
            string older = File("AC1024", entities: sat);
            Assert.Contains(Records(As(older, "AC1015"), "ENTITIES"), r => r[0].Value == "3DSOLID");
            Assert.DoesNotContain(Records(As(older, "AC1015"), "ENTITIES")[0], g =>
                g.Key == 100 && g.Value == "AcDb3dSolid"
            );
            Assert.DoesNotContain(Records(As(older, "AC1015"), "ENTITIES")[0], g => g.Key == 350);
            Assert.Contains(Records(As(older, "AC1021"), "ENTITIES")[0], g => g.Key == 350);
        }

        [Fact]
        public void TheDataStoreOfTheFile_IsKeptFromR2013AndLeftOutBefore()
        {
            string text = File("AC1032", entities: string.Empty) + string.Empty;
            string withStore = text.Replace(
                G(0, "EOF"),
                G(0, "SECTION", 2, "ACDSDATA", 70, 2, 71, 6, 0, "ACDSSCHEMA", 90, 0, 0, "ENDSEC") + G(0, "EOF")
            );

            Assert.Contains("ACDSDATA", As(withStore, "AC1027"));
            Assert.DoesNotContain("ACDSDATA", As(withStore, "AC1024"));
            Assert.DoesNotContain("ACDSDATA", As(withStore, "AC1015"));
        }

        #endregion

        #region Tables

        [Fact]
        public void ATableOfTheDrawing_IsTheBlockItIsDrawnWithBeforeR2007AndATableFromThen()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = source.ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal("F6", table.Handle);

            foreach (string version in new[] { "AC1015", "AC1018" })
            {
                using (var stream = new MemoryStream())
                {
                    IReadOnlyList<string> said = source.Save(stream, version);
                    DxfDatabase saved = Opened(stream.ToArray());

                    Assert.Empty(saved.ModelSpace.OfType<DxfTable>());
                    DxfBlockReference reference = saved
                        .ModelSpace.OfType<DxfBlockReference>()
                        .Single(r => r.Handle == "F6");
                    Assert.Equal("*T12", reference.BlockTableRecord.Name);
                    Assert.Equal(Bits(table.Position), Bits(reference.Position));
                    Assert.Contains(said, s => s.Contains("ACAD_TABLE 1"));
                }
            }

            foreach (string version in new[] { "AC1021", "AC1024", "AC1027" })
            {
                using (var stream = new MemoryStream())
                {
                    IReadOnlyList<string> said = source.Save(stream, version);
                    DxfDatabase saved = Opened(stream.ToArray());

                    Assert.Single(saved.ModelSpace.OfType<DxfTable>());
                    Assert.Empty(said);
                }
            }
        }

        [Fact]
        public void TheBlockOfATableThatIsOneNoMoreAndTheObjectsItOwned_AreLeftWhereTheyWere()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);

            using (var stream = new MemoryStream())
            {
                source.Save(stream, "AC1015");
                DxfDatabase saved = Opened(stream.ToArray());

                Assert.NotNull(saved.BlockTable["*T12"]);
                Assert.Equal(source.BlockTable["*T12"].Count(), saved.BlockTable["*T12"].Count());
            }
        }

        [Fact]
        public void TheSpaceOfABlockRecordAndTheShadePlotOfALayout_AreGivenWhenTheVersionHasThem()
        {
            string tables = G(
                0,
                "TABLE",
                2,
                "BLOCK_RECORD",
                5,
                "1",
                330,
                0,
                100,
                "AcDbSymbolTable",
                70,
                1,
                0,
                "BLOCK_RECORD",
                5,
                "1F",
                330,
                "1",
                100,
                "AcDbSymbolTableRecord",
                100,
                "AcDbBlockTableRecord",
                2,
                "*Model_Space",
                340,
                "22",
                0,
                "ENDTAB"
            );
            string objects = G(
                0,
                "LAYOUT",
                5,
                "22",
                330,
                "A",
                100,
                "AcDbPlotSettings",
                1,
                "",
                70,
                1024,
                147,
                1.0,
                148,
                0.0,
                149,
                0.0,
                100,
                "AcDbLayout",
                1,
                "Model",
                70,
                1
            );
            string text = File("AC1015", tables: tables, objects: objects);

            List<KeyValuePair<int, string>> block2000 = Record(As(text, "AC1015"), "TABLES", "BLOCK_RECORD");
            Assert.False(Has(block2000, 280));

            List<KeyValuePair<int, string>> block2007 = Record(As(text, "AC1021"), "TABLES", "BLOCK_RECORD");
            Assert.Equal(
                new[] { "70=0", "280=1", "281=0" },
                block2007
                    .Skip(block2007.FindIndex(g => g.Key == 340) + 1)
                    .Select(g => g.Key + "=" + g.Value.Trim())
                    .Where(s => !s.StartsWith("0=", StringComparison.Ordinal))
                    .Take(3)
                    .ToArray()
            );

            Assert.False(Has(Record(As(text, "AC1015"), "OBJECTS", "LAYOUT"), 76));
            List<KeyValuePair<int, string>> layout = Record(As(text, "AC1018"), "OBJECTS", "LAYOUT");
            int shade = layout.FindIndex(g => g.Key == 76);
            Assert.True(shade > layout.FindIndex(g => g.Key == 147));
            Assert.Equal(new[] { "0", "2", "300" }, layout.Skip(shade).Take(3).Select(g => g.Value.Trim()).ToArray());
        }

        #endregion
    }
}
