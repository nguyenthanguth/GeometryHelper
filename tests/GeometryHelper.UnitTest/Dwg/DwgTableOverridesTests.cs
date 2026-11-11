using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The overrides of a table and of its cells both ways, read from AutoCAD's DWG of R2018 to the groups of R2007 AutoCAD
    /// writes in DXF, and written from them to the content AutoCAD keeps; the cell styles of a table style's own, read; the
    /// TABLECONTENT and TABLEGEOMETRY a table keeps for its round trip, read. Each drawing was made by the probe GhDwgT in
    /// AutoCAD 2021, which AUDITs each with 0 errors and dumps the DWG the writer makes of each to the DXF it came from:
    /// tcell, cells of Standard each with an override of its own; ttable, the overrides of R2007 a table gives a kind of
    /// row, and a margin; tstyles and tclean, a clean table style of its own with borders, margins and a merged range;
    /// tall, a cell of all six overrides and cells of four edges each; tcustom, two cell styles of the style's own (101
    /// and 102) taken by a row, a column and a cell.
    /// </summary>
    public class DwgTableOverridesTests
    {
        /// <summary>The types of AutoCAD's new drawing of R2018 that the reader and the writer still leave out.</summary>
        private static readonly string[] StillLeftOut =
        {
            "ACDBDETAILVIEWSTYLE",
            "ACDBSECTIONVIEWSTYLE",
            "MATERIAL",
            "VISUALSTYLE",
            "CLASS CELLSTYLEMAP",
        };

        private static byte[] Written(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream);
                return stream.ToArray();
            }
        }

        private static DxfDatabase Source(string name) =>
            DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(name), false));

        private static DxfTable TableOf(byte[] dwg) =>
            DwgReader.Open(new MemoryStream(dwg, false)).ModelSpace.OfType<DxfTable>().Single();

        [Theory]
        [InlineData("tcell_AC1032")]
        [InlineData("ttable_AC1032")]
        [InlineData("tstyles_AC1032")]
        [InlineData("tclean_AC1032")]
        [InlineData("tall_AC1032")]
        public void ATableOfOverrides_ReadFromAutoCadsDwg_IsTheDxfAutoCadWroteOfIt(string name)
        {
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                DwgAnswerKey.Key(name),
                DwgReader.ToDxf(DwgAnswerKey.Dwg(name))
            );

            Assert.Empty(result.Differences);
            Assert.All(result.Missing.Keys, type => Assert.Contains(type, StillLeftOut));
            Assert.True(result.Paired > 370, result.Paired + " records paired");
        }

        [Theory]
        [InlineData("tcell_AC1032")]
        [InlineData("ttable_AC1032")]
        [InlineData("tstyles_AC1032")]
        [InlineData("tclean_AC1032")]
        [InlineData("tall_AC1032")]
        public void ATableOfOverrides_WrittenAsDwg_ReadsBackToTheSameRecords(string name)
        {
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                DwgAnswerKey.Key(name),
                DwgReader.ToDxf(Written(Source(name)))
            );

            // What the types left out lead to: the dictionaries that listed them and the material the header names.
            Assert.All(result.Differences, difference =>
                Assert.True(difference.Contains("$CMATERIAL") || difference.Contains("OBJECTS DICTIONARY"), difference)
            );
            Assert.All(result.Missing.Keys, type => Assert.Contains(type, StillLeftOut));
            Assert.True(result.Paired > 370, result.Paired + " records paired");
        }

        [Fact]
        public void TheOverridesOfACell_WrittenAsDwg_ComeBackAsTheCellsOwn()
        {
            DxfTable table = TableOf(Written(Source("tall_AC1032")));

            // The cell (2, 1): text 0.4 high at its top right in red on yellow, in GhMono; (1, 1) its top 0.30 thick and
            // red, its left hidden; (3, 2), the last, its right 0.50 thick.
            Assert.Equal(0.4, table.TextHeight(2, 1).Value, 12);
            Assert.Equal(DxfCellAlignment.TopRight, table.Alignment(2, 1));
            Assert.Equal(1, table.ContentColor(2, 1).ColorIndex);
            Assert.Equal(2, table.BackgroundColor(2, 1).ColorIndex);
            Assert.Equal("GhMono", table.TextStyle(2, 1).Name);
            Assert.Equal(DxfLineWeight.LineWeight030, table.GetGridLineWeight(1, 1, DxfGridLineType.HorizontalTop));
            Assert.Equal(1, table.GetGridColor(1, 1, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight050, table.GetGridLineWeight(3, 2, DxfGridLineType.VerticalRight));
        }

        [Fact]
        public void TheOverridesOfATableByItsKindsOfRow_WrittenAsDwg_KeepItsRowHeights()
        {
            // The table's own groups stand in its head before the heights: four rows of 10, three columns of 30.
            DxfTable table = TableOf(Written(Source("ttable_AC1032")));

            Assert.Equal(4, table.NumRows);
            Assert.All(Enumerable.Range(0, 4), row => Assert.Equal(10.0, table.RowHeight(row), 12));
            Assert.Equal(0.3, table.TextHeight(2, 1).Value, 12);
        }

        [Fact]
        public void TheBordersOfATableStyle_AreReadAndWritten_ByTheirLines()
        {
            // GhClean: the data's bottom line red (66), the header's left line hidden (287 0), the title's lines 0.35.
            List<DwgAnswerKey.Record> records = DwgAnswerKey.Parse(DwgReader.ToDxf(Written(Source("tstyles_AC1032"))));
            DwgAnswerKey.Record style = Assert.Single(records, r =>
                r.Type == "TABLESTYLE" && r.Groups.Any(g => g.Key == 3 && g.Value == "GH clean")
            );

            Assert.Equal(new[] { "1", "0", "0" }, style.Groups.Where(g => g.Key == 66).Select(g => g.Value));
            Assert.Equal(new[] { "1", "1", "0" }, style.Groups.Where(g => g.Key == 287).Select(g => g.Value));
            Assert.Equal(new[] { "-2", "35", "-2" }, style.Groups.Where(g => g.Key == 274).Select(g => g.Value));
        }

        [Fact]
        public void TheContentAndGeometryATableKeepsForItsRoundTrip_AreRead()
        {
            List<DwgAnswerKey.Record> records = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgAnswerKey.Dwg("ttable_AC1032")));

            DwgAnswerKey.Record content = Assert.Single(records, r => r.Type == "TABLECONTENT");
            DwgAnswerKey.Record geometry = Assert.Single(records, r => r.Type == "TABLEGEOMETRY");
            Assert.Equal(4, content.Groups.Count(g => g.Key == 1 && g.Value == "TABLEROW_BEGIN"));
            Assert.Equal("12", geometry.Groups.First(g => g.Key == 92).Value);
        }

        [Fact]
        public void TheCellStylesOfAStylesOwn_AreRead_IntoTheCellStyleMapAutoCadWritesInDxf()
        {
            List<DwgAnswerKey.Record> records = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgAnswerKey.Dwg("tcustom_AC1032")));

            // GhNote (101) and GhWide (102) beside _TITLE, _HEADER and _DATA, under the style's extension dictionary.
            DwgAnswerKey.Record map = Assert.Single(records, r => r.Type == "CELLSTYLEMAP");
            Assert.Equal(
                new[] { "_TITLE", "_HEADER", "_DATA", "GhNote", "GhWide" },
                map.Groups.Where((g, i) => g.Key == 300 && i > 0 && map.Groups[i - 3].Value == "CELLSTYLE_BEGIN")
                    .Select(g => g.Value)
            );
            DwgAnswerKey.Record dictionary = Assert.Single(records, r =>
                r.Type == "DICTIONARY" && r.Groups.Any(g => g.Value == "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP")
            );
            DwgAnswerKey.Record style = Assert.Single(records, r =>
                r.Type == "TABLESTYLE" && r.Groups.Any(g => g.Key == 360 && "X:" + g.Value == dictionary.Key)
            );
            Assert.Contains(style.Groups, g => g.Key == 3 && g.Value == "GH clean");
        }

        [Fact]
        public void TheCellStylesOfAStylesOwn_AreLeftOutOfTheDwg_AndNoCellTakesThem()
        {
            // A DWG that held them crashed AutoCAD 2021 as it read it: until that is proven they are not written, and a row,
            // column or cell that took one takes the data's or none, its looks kept as the cell's own of the DXF.
            byte[] dwg = Written(Source("tcustom_AC1032"));
            List<DwgAnswerKey.Record> records = DwgAnswerKey.Parse(DwgReader.ToDxf(dwg));

            Assert.DoesNotContain(records, r => r.Type == "CELLSTYLEMAP");
            DwgAnswerKey.Record content = Assert.Single(records, r => r.Type == "TABLECONTENT");
            Assert.DoesNotContain(
                content.Groups.Where((g, i) => g.Key == 90 && i > 0 && content.Groups[i - 1].Value.EndsWith("_BEGIN")),
                g => g.Value == "101" || g.Value == "102"
            );
            DxfTable table = TableOf(dwg);
            Assert.Equal(2.5, table.TextHeight(2, 0).Value, 12);
        }

        [Fact]
        public void ABlockOfAMaterialInTheFile_IsReadWithoutIt_AsAutoCadWritesItsDxf()
        {
            // AutoCAD's DWG of a drawing it read from DXF gives its blocks the material 11; its DXF gives them none.
            List<DwgAnswerKey.Record> records = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgAnswerKey.Dwg("tcustom_AC1032")));

            Assert.Contains(records, r => r.Type == "BLOCK");
            Assert.DoesNotContain(records, r =>
                (r.Type == "BLOCK" || r.Type == "ENDBLK") && r.Groups.Any(g => g.Key == 347)
            );
        }

        [Fact]
        public void ADrawingWithOverrides_IsWrittenInTheSameBytes()
        {
            Assert.Equal(Written(Source("ttable_AC1032")), Written(Source("ttable_AC1032")));
            Assert.Equal(Written(Source("tall_AC1032")), Written(Source("tall_AC1032")));
        }

        [Fact]
        public void AnMTextOfAnAngleAndNoDirection_IsWritten_ReadingAlongItsOwnPlane()
        {
            // An MTEXT in the plane of the normal (1, 0, 0) turned 30 degrees: it reads along the plane's x axis, (0, 1, 0),
            // turned by 30 degrees about the normal, (0, 0.866, 0.5); before, it was written along (0.866, 0.5, 0).
            DxfDatabase database = Source("curves_AC1032");
            var text = new DxfMText { Contents = "turned" };
            database.ModelSpace.AppendEntity(text);
            text.Normal = new GeometryHelper.Geometry.GeoVector3(1, 0, 0);
            text.Rotation = Math.PI / 6;
            Assert.DoesNotContain(" 11\r\n", ToGroupsOf(database, text));

            DxfMText back = DwgReader
                .Open(new MemoryStream(Written(database), false))
                .ModelSpace.OfType<DxfMText>()
                .Single(t => t.Contents == "turned");

            Assert.Equal(0.0, back.Direction.X, 9);
            Assert.Equal(Math.Cos(Math.PI / 6), back.Direction.Y, 9);
            Assert.Equal(0.5, back.Direction.Z, 9);
        }

        /// <summary>The groups of one entity of a database's DXF text, from its handle to the next record.</summary>
        private static string ToGroupsOf(DxfDatabase database, DxfEntity entity)
        {
            string text = database.ToText().Replace("\r\n", "\n").Replace("\n", "\r\n");
            int at = text.IndexOf("\r\n  5\r\n" + entity.Handle + "\r\n", StringComparison.Ordinal);
            int end = text.IndexOf("\r\n  0\r\n", at + 1, StringComparison.Ordinal);
            return text.Substring(at, end - at);
        }
    }
}
