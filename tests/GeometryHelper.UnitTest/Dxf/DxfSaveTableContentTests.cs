using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The round trip AutoCAD 2021 builds of a table made in code when it reads the file, written with the table: its extension
    /// dictionary, the XRECORD of <c>ACAD_ROUNDTRIP_2008_TABLE_ENTITY</c>, its TABLECONTENT and TABLEGEOMETRY, and for each table
    /// style the map of its cell styles. Every expected group here is what AutoCAD 2021 wrote (DxfIn then DxfOut in its console) of
    /// a table of the same shape made by this library with none of them: 6 tables (plain, merged, with borders, in no style set,
    /// with the title left out, with looks of their cells), 8 table styles, the cells of 12 looks and the lines of 8 pairs of
    /// cells, in R2007, R2010, R2013 and R2018, and in R2000 and R2004 going down.
    /// </summary>
    public class DxfSaveTableContentTests
    {
        #region Helpers

        private static DxfColor Colour(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        /// <summary>A drawing of the version in millimetres with AutoCAD's Standard table style and the text style GH_TXT.</summary>
        private static DxfDatabase Metric(string version = "AC1032")
        {
            var db = new DxfDatabase(version);
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));

            if (db.TableStyleDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_TXT", FileName = "arial.ttf" });
            return db;
        }

        private static DxfTableStyle AddStyle(DxfDatabase db, string name, Action<DxfTableStyle> set = null)
        {
            var style = new DxfTableStyle();
            db.TableStyleDictionary.SetAt(name, style);
            set?.Invoke(style);
            return style;
        }

        /// <summary>A table of 4 rows and 3 columns (9 high, 30 wide) with a few words, its title merged across, as the library makes one.</summary>
        private static DxfTable Table(
            DxfDatabase db,
            double x = 0.0,
            string style = null,
            int rows = 4,
            int columns = 3
        )
        {
            var table = new DxfTable(new GeoPoint3(x, 0.0, 0.0), rows, columns, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            if (style != null)
            {
                table.TableStyle = db.TableStyleDictionary[style];
            }

            table.SetTextString(0, 0, "STEEL");
            table.SetTextString(1, 0, "Mark");
            table.SetTextString(1, 1, "Section");
            table.SetTextString(2, 0, "B-12");
            table.SetTextString(2, 1, "HEA300");
            table.SetTextString(3, 2, "42");
            return table;
        }

        private static byte[] Saved(DxfDatabase db, string version = null)
        {
            using (var stream = new MemoryStream())
            {
                if (version == null)
                {
                    db.Save(stream);
                }
                else
                {
                    Assert.Empty(db.Save(stream, version));
                }

                return stream.ToArray();
            }
        }

        private static List<DwgAnswerKey.Record> Objects(byte[] dxf) => DwgAnswerKey.Parse(dxf);

        private static DwgAnswerKey.Record Find(List<DwgAnswerKey.Record> all, string handle) =>
            all.SingleOrDefault(r => r.Key == "X:" + handle.ToUpperInvariant());

        private static string Flat(IEnumerable<KeyValuePair<int, string>> groups) =>
            " "
            + string.Join(" ", groups.Select(g => g.Key.ToString(CultureInfo.InvariantCulture) + "=" + g.Value.Trim()))
            + " ";

        private static string After(DwgAnswerKey.Record record, string value, int code)
        {
            int at = record.Groups.FindIndex(g => g.Value.Trim() == value);
            return at < 0 ? null : record.Groups.Skip(at).FirstOrDefault(g => g.Key == code).Value?.Trim();
        }

        private static List<DwgAnswerKey.Record> Tables(List<DwgAnswerKey.Record> all) =>
            all.Where(r => r.Section == "ENTITIES" && r.Type == "ACAD_TABLE").ToList();

        /// <summary>The objects a table's round trip is made of: its dictionary, the XRECORD, the TABLECONTENT and the TABLEGEOMETRY; null for each it has not.</summary>
        private sealed class Chain
        {
            internal DwgAnswerKey.Record Table;
            internal DwgAnswerKey.Record Dictionary;
            internal DwgAnswerKey.Record XRecord;
            internal DwgAnswerKey.Record Content;
            internal DwgAnswerKey.Record Geometry;
        }

        private static Chain ChainOf(List<DwgAnswerKey.Record> all, DwgAnswerKey.Record table)
        {
            var chain = new Chain { Table = table };
            string dictionary = After(table, "{ACAD_XDICTIONARY", 360);
            chain.Dictionary = dictionary == null ? null : Find(all, dictionary);
            string xrecord = chain.Dictionary == null ? null : After(chain.Dictionary, "ACAD_XREC_ROUNDTRIP", 360);
            chain.XRecord = xrecord == null ? null : Find(all, xrecord);

            if (chain.XRecord != null)
            {
                string content = After(chain.XRecord, "ACAD_ROUNDTRIP_2008_TABLE_ENTITY", 360);
                chain.Content = content == null ? null : Find(all, content);
                string geometry = chain.XRecord.Groups.Where(g => g.Key == 361).Select(g => g.Value).FirstOrDefault();
                chain.Geometry = geometry == null ? null : Find(all, geometry);
            }

            return chain;
        }

        private static List<Chain> Chains(byte[] dxf)
        {
            List<DwgAnswerKey.Record> all = Objects(dxf);
            return Tables(all).Select(t => ChainOf(all, t)).ToList();
        }

        /// <summary>The cells of a TABLECONTENT, row by row, each as its flat groups from its 300 CELL to its TABLECELL_END.</summary>
        private static List<string> ContentCells(DwgAnswerKey.Record content)
        {
            var cells = new List<string>();
            List<KeyValuePair<int, string>> groups = content.Groups;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Key == 300 && groups[i].Value.Trim() == "CELL")
                {
                    int end = groups.FindIndex(i, g => g.Key == 309 && g.Value.Trim() == "TABLECELL_END");
                    cells.Add(Flat(groups.GetRange(i, end + 1 - i)));
                }
            }

            return cells;
        }

        /// <summary>The kind of each row of a TABLECONTENT (the 90 of its TABLEROW: 1 the title, 2 the header, 3 data) and its height (40).</summary>
        private static List<string> ContentRows(DwgAnswerKey.Record content)
        {
            var rows = new List<string>();
            List<KeyValuePair<int, string>> groups = content.Groups;

            for (int i = 0; i + 2 < groups.Count; i++)
            {
                if (groups[i].Key == 1 && groups[i].Value.Trim() == "TABLEROW_BEGIN")
                {
                    rows.Add(groups[i + 1].Value.Trim() + "/" + groups[i + 2].Value.Trim());
                }
            }

            return rows;
        }

        /// <summary>The cells of an ACAD_TABLE record, each as its flat groups from its 171 to the next 171 (the last to the end).</summary>
        private static List<string> RecordCells(DwgAnswerKey.Record table)
        {
            var cells = new List<List<KeyValuePair<int, string>>>();

            foreach (KeyValuePair<int, string> group in table.Groups)
            {
                if (group.Key == 171)
                {
                    cells.Add(new List<KeyValuePair<int, string>>());
                }

                cells.LastOrDefault()?.Add(group);
            }

            return cells.Select(Flat).ToList();
        }

        /// <summary>The lines a cell of the TABLECONTENT lists after its count (94 of the table format), each by the number of the line and its properties.</summary>
        private static string Lines(string cell)
        {
            int at = cell.IndexOf(" 171=0 94=", StringComparison.Ordinal);
            int end = cell.IndexOf(" 309=TABLEFORMAT_END", at, StringComparison.Ordinal);
            return cell.Substring(at + 1, end - at - 1);
        }

        private const string Plain =
            " 300=CELL 1=LINKEDTABLEDATACELL_BEGIN 90=0 300= 91=0 301=CUSTOMDATA 1=DATAMAP_BEGIN 90=1 300=ACAD_ROUNDTRIP_2008_CELL_CHECKSUM 301=DATAMAP_VALUE 93=2 90=2 140={0} 94=0 300= 302= 304=ACVALUE_END 309=DATAMAP_END 92=0 95=1 302=CONTENT 1=CELLCONTENT_BEGIN 90=1 300=VALUE 93=6 90=4 1={1} 94=0 300= 302={1} 304=ACVALUE_END 91=0 309=CELLCONTENT_END 1=FORMATTEDCELLCONTENT_BEGIN 170=1 300=CONTENTFORMAT 1=CONTENTFORMAT_BEGIN 90=3 91=0 92=4 93=0 300= 40=0.0 140=1.0 94=1 62=0 340=0 144=0.18 309=CONTENTFORMAT_END 309=FORMATTEDCELLCONTENT_END 309=LINKEDTABLEDATACELL_END 1=FORMATTEDTABLEDATACELL_BEGIN 300=CELLTABLEFORMAT 1=TABLEFORMAT_BEGIN 90=1 170=1 91=0 92=0 62=257 93=1 300=CONTENTFORMAT 1=CONTENTFORMAT_BEGIN 90=0 91=0 92=512 93=0 300= 40=0.0 140=1.0 94=1 62=0 340=0 144=0.18 309=CONTENTFORMAT_END 171=0 94=0 309=TABLEFORMAT_END 309=FORMATTEDTABLEDATACELL_END 1=TABLECELL_BEGIN 90=0 91=0 309=TABLECELL_END ";

        private const string Hidden =
            " 300=CELL 1=LINKEDTABLEDATACELL_BEGIN 90=0 300= 91=0 301=CUSTOMDATA 1=DATAMAP_BEGIN 90=1 300=ACAD_ROUNDTRIP_2008_CELL_CHECKSUM 301=DATAMAP_VALUE 93=2 90=2 140=0.0 94=0 300= 302= 304=ACVALUE_END 309=DATAMAP_END 92=0 95=0 309=LINKEDTABLEDATACELL_END 1=FORMATTEDTABLEDATACELL_BEGIN 300=CELLTABLEFORMAT 1=TABLEFORMAT_BEGIN 90=1 170=1 91=0 92=0 62=257 93=1 300=CONTENTFORMAT 1=CONTENTFORMAT_BEGIN 90=0 91=0 92=512 93=0 300= 40=0.0 140=1.0 94=1 62=0 340=0 144=0.18 309=CONTENTFORMAT_END 171=0 94=0 309=TABLEFORMAT_END 309=FORMATTEDTABLEDATACELL_END 1=TABLECELL_BEGIN 90=0 91=0 309=TABLECELL_END ";

        private const string Empty =
            " 300=CELL 1=LINKEDTABLEDATACELL_BEGIN 90=0 300= 91=0 301=CUSTOMDATA 1=DATAMAP_BEGIN 90=1 300=ACAD_ROUNDTRIP_2008_CELL_CHECKSUM 301=DATAMAP_VALUE 93=2 90=2 140=0.0 94=0 300= 302= 304=ACVALUE_END 309=DATAMAP_END 92=0 95=1 302=CONTENT 1=CELLCONTENT_BEGIN 90=1 300=VALUE 93=7 90=0 94=0 300= 302= 304=ACVALUE_END 91=0 309=CELLCONTENT_END 1=FORMATTEDCELLCONTENT_BEGIN 170=1 300=CONTENTFORMAT 1=CONTENTFORMAT_BEGIN 90=0 91=0 92=512 93=0 300= 40=0.0 140=1.0 94=1 62=0 340=0 144=0.18 309=CONTENTFORMAT_END 309=FORMATTEDCELLCONTENT_END 309=LINKEDTABLEDATACELL_END 1=FORMATTEDTABLEDATACELL_BEGIN 300=CELLTABLEFORMAT 1=TABLEFORMAT_BEGIN 90=1 170=1 91=0 92=0 62=257 93=1 300=CONTENTFORMAT 1=CONTENTFORMAT_BEGIN 90=0 91=0 92=512 93=0 300= 40=0.0 140=1.0 94=1 62=0 340=0 144=0.18 309=CONTENTFORMAT_END 171=0 94=0 309=TABLEFORMAT_END 309=FORMATTEDTABLEDATACELL_END 1=TABLECELL_BEGIN 90=0 91=0 309=TABLECELL_END ";

        /// <summary>A text cell of the TABLECONTENT of Standard's looks: the checksum of its text (the sum of each letter times its place) and its text.</summary>
        private static string PlainCell(string text)
        {
            long sum = 0;

            for (int k = 0; k < text.Length; k++)
            {
                sum += text[k] * (long)(k + 1);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                Plain,
                sum.ToString(CultureInfo.InvariantCulture) + ".0",
                text
            );
        }

        #endregion

        #region A table made in code

        [Fact]
        public void ATableMadeInCode_SavedInItsOwnVersion_HoldsTheRoundTripAutoCadBuildsOfIt()
        {
            DxfDatabase db = Metric();
            Table(db);
            byte[] dxf = Saved(db);
            List<DwgAnswerKey.Record> all = Objects(dxf);
            Chain chain = Chains(dxf).Single();

            Assert.NotNull(chain.Dictionary);
            Assert.NotNull(chain.XRecord);
            Assert.NotNull(chain.Content);
            Assert.NotNull(chain.Geometry);

            // The extension dictionary names no reactor, the XRECORD the table entity: 2 (a table of columns), 3 columns.
            Assert.DoesNotContain(chain.Dictionary.Groups, g => g.Key == 102);
            Assert.Equal(chain.Table.Groups.First(g => g.Key == 5).Value, chain.Dictionary.Owner);
            Assert.Contains(
                " 100=AcDbXrecord 280=1 102=ACAD_ROUNDTRIP_2008_TABLE_ENTITY 360="
                    + chain.Content.Groups.First(g => g.Key == 5).Value
                    + " 70=2 90=1 10=0.0 20=0.0 30=0.0 90=0 90=3 361="
                    + chain.Geometry.Groups.First(g => g.Key == 5).Value
                    + " ",
                Flat(chain.XRecord.Groups)
            );

            // The table says what it said: its empty cells keep the flags 7 of an empty value.
            Assert.Equal(6, RecordCells(chain.Table).Count(c => c.Contains(" 93=7 90=0 ")));
        }

        [Fact]
        public void TheTableContent_HoldsEachCellsTextAndChecksum_AsAutoCadBuildsIt()
        {
            DxfDatabase db = Metric();
            Table(db);
            Chain chain = Chains(Saved(db)).Single();
            List<string> cells = ContentCells(chain.Content);

            Assert.Equal(12, cells.Count);
            Assert.Equal(new[] { "1/9.0", "2/9.0", "3/9.0", "3/9.0" }, ContentRows(chain.Content).ToArray());
            Assert.Equal(PlainCell("STEEL"), cells[0]);
            Assert.Equal(Hidden, cells[1]);
            Assert.Equal(Hidden, cells[2]);
            Assert.Equal(PlainCell("Mark"), cells[3]);
            Assert.Equal(PlainCell("Section"), cells[4]);
            Assert.Equal(Empty, cells[5]);
            Assert.Equal(PlainCell("B-12"), cells[6]);
            Assert.Equal(PlainCell("HEA300"), cells[7]);
            Assert.Equal(PlainCell("42"), cells[11]);

            // The columns of their widths, then the rows, and after them one merged block, the title.
            string content = Flat(chain.Content.Groups);
            Assert.Contains(" 100=AcDbLinkedTableData 90=3 300=COLUMN ", content);
            Assert.Equal(
                3,
                content
                    .Split(new[] { " 1=TABLECOLUMN_BEGIN 90=0 40=30.0 309=TABLECOLUMN_END" }, StringSplitOptions.None)
                    .Length - 1
            );
            Assert.EndsWith(
                " 92=0 100=AcDbFormattedTableData 300=TABLEFORMAT 1=TABLEFORMAT_BEGIN 90=4 170=0 309=TABLEFORMAT_END 90=1 91=0 92=0 93=0 94=2 100=AcDbTableContent 340="
                    + After(chain.Table, "AcDbTable", 342)
                    + " ",
                content
            );
        }

        [Fact]
        public void TheTableGeometry_HoldsACellOfNoSizeForEachCell_AsAutoCadBuildsIt()
        {
            DxfDatabase db = Metric();
            Table(db);
            Chain chain = Chains(Saved(db)).Single();
            string cell = " 93=0 40=0.0 41=0.0 330=0 94=0";

            Assert.Equal(
                " 330="
                    + chain.XRecord.Groups.First(g => g.Key == 5).Value
                    + " 100=AcDbTableGeometry 90=4 91=3 92=12"
                    + string.Concat(Enumerable.Repeat(cell, 12))
                    + " ",
                Flat(chain.Geometry.Groups.Where(g => g.Key != 5))
            );
        }

        [Fact]
        public void TheTableAndItsStyle_AreListedForRecomposing_AndTheirClassesMade()
        {
            DxfDatabase db = Metric();
            Table(db);
            byte[] dxf = Saved(db);
            List<DwgAnswerKey.Record> all = Objects(dxf);
            Chain chain = Chains(dxf).Single();
            DwgAnswerKey.Record recompose = all.Single(r =>
                r.Type == "XRECORD"
                && r.Groups.Any(g => g.Key == 90)
                && r.Groups.Count(g => g.Key == 330) >= 3
                && r.Owner
                    == all.Single(d => d.Type == "DICTIONARY" && d.Groups.Any(g => g.Value == "ACDB_RECOMPOSE_DATA"))
                        .Groups.First(g => g.Key == 5)
                        .Value
            );
            string[] listed = recompose.Groups.Where(g => g.Key == 330).Skip(2).Select(g => g.Value).ToArray();

            Assert.Contains(chain.Table.Groups.First(g => g.Key == 5).Value, listed);
            Assert.Contains(After(chain.Table, "AcDbTable", 342), listed);

            foreach (string name in new[] { "TABLECONTENT", "TABLEGEOMETRY", "CELLSTYLEMAP" })
            {
                DwgAnswerKey.Record made = all.Single(r =>
                    r.Type == "CLASS" && r.Groups.First(g => g.Key == 1).Value == name
                );
                Assert.Equal("1152", made.Groups.First(g => g.Key == 90).Value);
                Assert.Equal(
                    all.Count(r => r.Section == "OBJECTS" && r.Type == name).ToString(CultureInfo.InvariantCulture),
                    made.Groups.First(g => g.Key == 91).Value
                );
            }
        }

        [Fact]
        public void SavingADrawing_LeavesTheDrawingAsItWas_AndSavesTheSameBytesAgain()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db);
            string before = db.ToText();

            byte[] first = Saved(db);
            byte[] second = Saved(db);

            Assert.Equal(first, second);
            Assert.Equal(before, db.ToText());
            Assert.DoesNotContain("TABLECONTENT", before, StringComparison.Ordinal);
            Assert.Null(table.ExtensionDictionary);
        }

        [Theory]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void ATableSavedAsEachVersionFromR2007_HoldsTheSameContent(string version)
        {
            DxfDatabase db = Metric();
            Table(db);
            Chain chain = Chains(Saved(db, version)).Single();
            Chain own = Chains(Saved(db)).Single();

            Assert.NotNull(chain.Content);
            Assert.NotNull(chain.Geometry);
            Assert.Equal(ContentCells(own.Content), ContentCells(chain.Content));
            Assert.Equal(ContentRows(own.Content), ContentRows(chain.Content));
            Assert.Equal(own.Geometry.Groups.Count, chain.Geometry.Groups.Count);
        }

        [Theory]
        [InlineData("AC1015", "ACDBDATATABLE")]
        [InlineData("AC1018", "DATATABLE")]
        public void ATableSavedDownToR2000OrR2004_HoldsItsContentInTheFormBeforeR2007_BesideItsCells(
            string version,
            string dataTable
        )
        {
            DxfDatabase db = Metric();
            Table(db);
            byte[] dxf = Saved(db, version);
            List<DwgAnswerKey.Record> all = Objects(dxf);
            Chain chain = Chains(dxf).Single();

            Assert.NotNull(chain.Content);
            Assert.NotNull(chain.Geometry);
            Assert.Contains(all, r => r.Type == dataTable);
            List<string> cells = ContentCells(chain.Content);
            Assert.Contains(" 300=VALUE 90=4 1=Mark 91=0 309=CELLCONTENT_END ", cells[3]);
            Assert.Contains(" 300=VALUE 90=0 91=0 91=0 309=CELLCONTENT_END ", cells[5]);
        }

        [Fact]
        public void ATableSavedDownToR2004_AndUpAgain_StaysATable_WithItsContent()
        {
            DxfDatabase db = Metric();
            Table(db);
            DxfDatabase down = DxfDatabase.Parse(Encoding.GetEncoding(28591).GetString(Saved(db, "AC1018")));

            using (var stream = new MemoryStream())
            {
                IReadOnlyList<string> said = down.Save(stream, "AC1032");
                Assert.Empty(said);
                Chain chain = Chains(stream.ToArray()).Single();
                Assert.NotNull(chain.Content);
                Assert.Equal(PlainCell("Mark"), ContentCells(chain.Content)[3]);
                Assert.Equal(Empty, ContentCells(chain.Content)[5]);
            }
        }

        #endregion

        #region What the drawing already holds

        [Fact]
        public void ATableThatHoldsItsContent_IsSavedAsItWas_NoSecondContentMade()
        {
            DxfDatabase db = DxfDatabase.Parse(Encoding.GetEncoding(28591).GetString(Saved(Metric_WithTable())));
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            table.SetTextString(1, 0, "Mark 2");
            byte[] dxf = Saved(db);
            List<DwgAnswerKey.Record> all = Objects(dxf);

            Assert.Equal(1, all.Count(r => r.Type == "TABLECONTENT"));
            Assert.Equal(1, all.Count(r => r.Type == "TABLEGEOMETRY"));
            Assert.Equal(1, all.Count(r => r.Type == "CELLSTYLEMAP"));
        }

        private static DxfDatabase Metric_WithTable()
        {
            DxfDatabase db = Metric();
            Table(db);
            return db;
        }

        [Fact]
        public void ADrawingNothingWasWrittenTo_IsSavedByteForByteAsItWasRead()
        {
            byte[] read = Saved(Metric_WithTable());
            DxfDatabase db = DxfDatabase.Parse(Encoding.GetEncoding(28591).GetString(read));
            Assert.Equal(read, Saved(db));
        }

        [Fact]
        public void ATableWithLinesOfItsOwnForItsRows_OrAField_IsLeftAsItIs()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db);
            table.SetGridColor(Colour(1), DxfGridLineType.HorizontalInside, DxfRowType.DataRow);
            Chain chain = Chains(Saved(db)).Single();

            Assert.Null(chain.Dictionary);
            Assert.Null(chain.Content);
        }

        [Fact]
        public void ATableOfADrawingBeforeR2007_IsLeftAsItIs_AndHasNoVersionNumberOfItsOwn()
        {
            DxfDatabase db = Metric("AC1015");
            Table(db);
            byte[] dxf = Saved(db);
            Chain chain = Chains(dxf).Single();

            Assert.Null(chain.Dictionary);
            Assert.DoesNotContain(" 100=AcDbTable 280=", Flat(chain.Table.Groups));
        }

        [Fact]
        public void ATableOfR2007_HasNoVersionNumberOfItsOwn_AndOneOfR2010Has()
        {
            DxfDatabase r2007 = Metric("AC1021");
            Table(r2007);
            DxfDatabase r2010 = Metric("AC1024");
            Table(r2010);

            Assert.DoesNotContain(" 100=AcDbTable 280=", Flat(Chains(Saved(r2007)).Single().Table.Groups));
            Assert.Contains(" 100=AcDbTable 280=0 ", Flat(Chains(Saved(r2010)).Single().Table.Groups));
        }

        #endregion
    }
}
