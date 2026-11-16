using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The tables a DWG of R2000, R2004 and R2007 holds, as AutoCAD 2021 writes them: the ACAD_TABLE with its cells in the
    /// form of those versions (from R2007 each with its value), the round-trip objects under the table's extension dictionary
    /// (TABLECONTENT and TABLEGEOMETRY, and in R2000 and R2004 a DATATABLE with an XRECORD for each cell), and the table style
    /// that names the table among its reactors. The writer left the tables out of those versions, so that the table style
    /// lacked its reactor and the drawing its tables; and the file header of R2010 and R2013 named the format's own version
    /// where AutoCAD names itself.
    /// </summary>
    public class DwgTablesOfOlderVersionsTests
    {
        /// <summary>The DWG of the DXF the reader makes of one of AutoCAD's own files, written as <paramref name="version"/>.</summary>
        private static byte[] OursOf(string answer, string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(DwgAnswerKey.Dwg(answer)), false)).ToText();
            return DwgWriter.ToDwg(text, version, out _);
        }

        /// <summary>The DWG of a drawing of AutoCAD's own DXF of R2018, written as <paramref name="version"/>.</summary>
        private static byte[] OursFromR2018(string answer, string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(answer), false)).ToText();
            return DwgWriter.ToDwg(text, version, out _);
        }

        /// <summary>The bytes of an object of a file by its handle: its size, its data and its check sum.</summary>
        private static byte[] ObjectOf(byte[] file, ulong handle)
        {
            DwgContainer container = DwgContainer.Read(file);
            long at = container.ObjectMap.Single(pair => pair.Key == handle).Value;
            var reader = new DwgBitReader(container.Objects, at * 8, container.Objects.LongLength * 8);
            long size = reader.ModularShort();

            if (DwgContainer.NumberOf(container.Version) >= 1024)
            {
                reader.ModularChar(false);
            }

            int length = (int)(((reader.Position - (at * 8)) / 8) + size + 2);
            return container.Objects.Skip((int)at).Take(length).ToArray();
        }

        private static List<DwgAnswerKey.Record> RecordsOf(byte[] dwg, params string[] types) =>
            DwgAnswerKey.Parse(DwgReader.ToDxf(dwg)).Where(r => types.Contains(r.Type)).ToList();

        private static ulong HandleOf(DwgAnswerKey.Record record) =>
            ulong.Parse(record.Groups.First(g => g.Key == 5).Value.Trim(), NumberStyles.HexNumber);

        /// <summary>The handles a record lists as its reactors (330 between <c>{ACAD_REACTORS</c> and <c>}</c>).</summary>
        private static List<ulong> ReactorsOf(DwgAnswerKey.Record record)
        {
            var found = new List<ulong>();
            bool inside = false;

            foreach (KeyValuePair<int, string> group in record.Groups)
            {
                if (group.Key == 102)
                {
                    inside = group.Value.Trim() == "{ACAD_REACTORS";
                }
                else if (inside && group.Key == 330)
                {
                    found.Add(ulong.Parse(group.Value.Trim(), NumberStyles.HexNumber));
                }
            }

            return found;
        }

        /// <summary>The cells of a table in the groups of its DXF: the groups from each <c>171</c> to the next.</summary>
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

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        public void TheTablesOfADrawingOfR2018_AreWrittenAsTablesOfTheVersion_AndTheirStyleNamesThem(string version)
        {
            byte[] ours = OursFromR2018("table_AC1032", version);
            List<DwgAnswerKey.Record> tables = RecordsOf(ours, "ACAD_TABLE");

            // The two tables of the drawing (the first of 3 rows by 3 columns, its words Title and a, the second of 2 by 4).
            Assert.Equal(2, tables.Count);

            Assert.Equal(
                new[] { "Title", "a" },
                tables
                    .Select(t => CellsOf(t))
                    .SelectMany(cells => cells)
                    .SelectMany(cell => cell.Where(g => g.Key == 1).Take(1))
                    .Select(g => g.Value.Trim())
                    .Where(text => text.Length > 0)
                    .Take(2)
                    .ToArray()
            );

            // The style of each table names it among its reactors (the table style lacked that reactor).
            List<DwgAnswerKey.Record> styles = RecordsOf(ours, "TABLESTYLE");

            Assert.All(tables, table =>
            {
                string named = table.Groups.First(g => g.Key == 342).Value.Trim();
                DwgAnswerKey.Record style = styles.Single(s =>
                    HandleOf(s).ToString("X", CultureInfo.InvariantCulture) == named
                );

                Assert.Contains(HandleOf(table), ReactorsOf(style));
            });

            // The round-trip objects: the content and the geometry of each table, and before R2007 a data table of the cells.
            Assert.Single(RecordsOf(ours, "TABLECONTENT"));
            Assert.Single(RecordsOf(ours, "TABLEGEOMETRY"));
            Assert.Equal(version == "AC1021" ? 0 : 2, RecordsOf(ours, "ACDBDATATABLE", "DATATABLE").Count);
        }

        [Theory]
        [InlineData("table_AC1015")]
        [InlineData("table_AC1018")]
        [InlineData("table_AC1021")]
        public void TheTablesAndTheirRoundTripObjectsOfAutoCADsOwnFile_WrittenAgain_AreItsBytes(string answer)
        {
            byte[] acad = DwgAnswerKey.Dwg(answer);
            string version = "AC" + answer.Substring(answer.Length - 4);
            byte[] ours = OursOf(answer, version);
            List<DwgAnswerKey.Record> objects = RecordsOf(
                acad,
                "ACAD_TABLE",
                "TABLECONTENT",
                "TABLEGEOMETRY",
                "ACDBDATATABLE",
                "DATATABLE"
            );

            Assert.Equal(2, objects.Count(r => r.Type == "ACAD_TABLE"));
            Assert.Single(objects, r => r.Type == "TABLEGEOMETRY");
            Assert.Single(objects, r => r.Type == "TABLECONTENT");
            Assert.Equal(version == "AC1021" ? 0 : 2, objects.Count(r => r.Type.EndsWith("DATATABLE")));
            Assert.All(objects, record =>
                Assert.Equal(ObjectOf(acad, HandleOf(record)), ObjectOf(ours, HandleOf(record)))
            );
        }

        [Fact]
        public void ACellWithBordersOfItsOwn_HoldsTheFlagsOfTheBordersTwiceInR2000_AsAutoCADWritesThem()
        {
            // The DXF of R2000 holds the flags of a cell in 16 bits, and AutoCAD's file keeps beside each of the flags 0x400 to
            // 0x2000 (the second value of a border) the bit 4 places up, and 0x40000 in a cell that has words. AutoCAD refuses a
            // file whose flags lack them (ErrorStatus 53).
            byte[] ours = OursFromR2018("ttable_AC1032", "AC1015");
            DwgAnswerKey.Record table = Assert.Single(RecordsOf(ours, "ACAD_TABLE"));
            List<List<KeyValuePair<int, string>>> cells = CellsOf(table);
            List<int> flags = cells.Select(cell => int.Parse(cell.First(g => g.Key == 177).Value.Trim())).ToList();

            Assert.Equal(12, cells.Count);
            Assert.Contains(157272, flags);
            Assert.Contains(52440, flags);
            Assert.Contains(17505, flags);

            // The overrides of the whole table stand before the sizes of the rows.
            int firstSize = table.Groups.FindIndex(g => g.Key == 141);
            int override140 = table.Groups.FindIndex(g => g.Key == 140 && g.Value.Trim() == "0.3");

            Assert.InRange(override140, 0, firstSize - 1);

            // The text style the overrides and the cells name is a name, as DXF gives it, not a handle.
            Assert.Contains(table.Groups, g => g.Key == 7 && g.Value.Trim() == "GhMono");
            Assert.DoesNotContain(table.Groups, g => g.Key == 7 && g.Value.Trim() == "78");
        }

        [Theory]
        [InlineData("props_AC1024", "AC1024")]
        [InlineData("matvis_AC1024", "AC1024")]
        [InlineData("props_AC1027", "AC1027")]
        [InlineData("matvis_AC1027", "AC1027")]
        public void TheFileHeaderOfR2010AndR2013_NamesTheApplicationThatSavedIt_AsAutoCAD2021Does(
            string answer,
            string version
        )
        {
            byte[] acad = DwgAnswerKey.Dwg(answer);
            byte[] ours = OursOf(answer, version);

            Assert.Equal(new byte[] { 33, 174, 33, 174 }, new[] { acad[0x11], acad[0x12], acad[0x16], acad[0x17] });
            Assert.Equal(
                new[] { acad[0x11], acad[0x12], acad[0x16], acad[0x17] },
                new[] { ours[0x11], ours[0x12], ours[0x16], ours[0x17] }
            );
        }

        [Fact]
        public void TheColoursOfASectionViewStyleOfR2018_WrittenAsR2000_AreTheIndexesAutoCADKeeps()
        {
            // The section style of its own of views (handle 0x7F): AutoCAD's R2000 file holds each colour of a view style as its
            // index; the DXF of R2000 has no group for it. The object is its size (2 bytes), 175 bytes of data and then the
            // handles, which name the extension dictionary, numbered anew by the converter: the bytes of the data are compared,
            // not the size nor the handles.
            byte[] acad = ObjectOf(DwgAnswerKey.Dwg("views_AC1015"), 0x7F);
            byte[] ours = ObjectOf(OursFromR2018("views_AC1032", "AC1015"), 0x7F);

            Assert.Equal(acad.Skip(2).Take(170), ours.Skip(2).Take(170));
        }

        [Fact]
        public void TheColoursOfAViewStyle_AreNotInADxfOfR2000_AndTheirIndexesAreInTheTextMadeForADwg()
        {
            string text = System.Text.Encoding.UTF8.GetString(DwgAnswerKey.Key("views_AC1032"));

            int Colours(string converted, int code) =>
                DwgAnswerKey
                    .Parse(System.Text.Encoding.UTF8.GetBytes(converted))
                    .Where(r => r.Type == "ACDBDETAILVIEWSTYLE" || r.Type == "ACDBSECTIONVIEWSTYLE")
                    .Sum(r => r.Groups.Count(g => g.Key == code));

            // AutoCAD's DXF of R2000 holds neither the index (62) nor the true colour (420) of a part of a view style.
            string dxf = DxfVersionConverter.Convert(text, 1015, null);
            string dwg = DxfVersionConverter.Convert(text, 1015, null, null, true);

            Assert.Equal(0, Colours(dxf, 62));
            Assert.Equal(0, Colours(dxf, 420));
            Assert.Equal(0, Colours(dwg, 420));
            Assert.InRange(Colours(dwg, 62), 20, 40);
        }

        [Theory]
        [InlineData("AC1018", "matvis_AC1018")]
        [InlineData("AC1021", "matvis_AC1021")]
        [InlineData("AC1024", "matvis_AC1024")]
        public void TheFaceLightingModelOfRealistic_IsAsAutoCADsFileOfTheVersionHoldsIt(string version, string answer)
        {
            // 3 from R2010; before it AutoCAD saves it as 2.
            string Model(byte[] dxf) =>
                DwgAnswerKey
                    .Parse(dxf)
                    .Single(r =>
                        r.Type == "VISUALSTYLE" && r.Groups.Any(g => g.Key == 2 && g.Value.Trim() == "Realistic")
                    )
                    .Groups.First(g => g.Key == 72)
                    .Value.Trim();

            string expected = Model(DwgAnswerKey.Key(answer));
            byte[] ours = OursFromR2018("matvis_AC1032", version);

            Assert.Equal(version == "AC1024" ? "3" : "2", expected);
            Assert.Equal(expected, Model(DwgReader.ToDxf(ours)));
        }
    }
}
