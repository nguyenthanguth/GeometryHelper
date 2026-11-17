using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The spreadsheets a data link reads and writes: ranges in a spreadsheet's words, the words a number format makes of a
    /// number, comma separated files, and .xlsx workbooks built in code (shared strings, a style with a percent, a date and a
    /// fill, a named range) read back with their values, written new, and written into without touching the rest.
    /// </summary>
    public sealed class DataLinkSpreadsheetTests
    {
        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-dl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// A workbook of two sheets as a program other than Excel writes one: Parts has shared strings, a whole number, a
        /// percent (style 1), a date (style 2), a right-aligned yellow cell (style 3), a formula with its kept value and a
        /// boolean; Prices holds one cell; the name Rates stands for Parts!$B$2:$C$3.
        /// </summary>
        internal static string Workbook(string folder)
        {
            string path = Path.Combine(folder, "parts.xlsx");
            const string main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            using (var file = new FileStream(path, FileMode.Create))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                void Put(string name, string xml)
                {
                    using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
                    {
                        writer.Write(xml);
                    }
                }

                Put(
                    "[Content_Types].xml",
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>"
                );
                Put(
                    "xl/workbook.xml",
                    "<workbook xmlns=\""
                        + main
                        + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                        + "<sheets><sheet name=\"Parts\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Prices\" sheetId=\"2\" r:id=\"rId2\"/></sheets>"
                        + "<definedNames><definedName name=\"Rates\">Parts!$B$2:$C$3</definedName></definedNames></workbook>"
                );
                Put(
                    "xl/_rels/workbook.xml.rels",
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                        + "<Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\" Type=\"worksheet\"/>"
                        + "<Relationship Id=\"rId2\" Target=\"/xl/worksheets/sheet2.xml\" Type=\"worksheet\"/></Relationships>"
                );
                Put(
                    "xl/sharedStrings.xml",
                    "<sst xmlns=\""
                        + main
                        + "\"><si><t>Mark</t></si><si><t>Qty</t></si><si><r><t>Bolt </t></r><r><t>M8</t></r></si><si><t>Nut</t></si></sst>"
                );
                Put(
                    "xl/styles.xml",
                    "<styleSheet xmlns=\""
                        + main
                        + "\"><numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"0.0&quot; mm&quot;\"/></numFmts>"
                        + "<fonts count=\"2\"><font/><font><color rgb=\"FFFF0000\"/></font></fonts>"
                        + "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>"
                        + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFFF00\"/></patternFill></fill></fills>"
                        + "<cellXfs count=\"5\"><xf numFmtId=\"0\"/><xf numFmtId=\"9\"/><xf numFmtId=\"14\"/>"
                        + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\"><alignment horizontal=\"right\"/></xf><xf numFmtId=\"164\"/></cellXfs></styleSheet>"
                );
                Put(
                    "xl/worksheets/sheet1.xml",
                    "<worksheet xmlns=\""
                        + main
                        + "\"><sheetData>"
                        + "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"C1\" t=\"inlineStr\"><is><t>Rate</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>Date</t></is></c></row>"
                        + "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>2</v></c><c r=\"B2\"><v>12</v></c><c r=\"C2\" s=\"1\"><v>0.25</v></c><c r=\"D2\" s=\"2\"><v>44927</v></c></row>"
                        + "<row r=\"3\"><c r=\"A3\" t=\"s\"><v>3</v></c><c r=\"B3\" s=\"4\"><v>7.5</v></c><c r=\"C3\"><f>B3*2</f><v>15</v></c><c r=\"D3\" t=\"b\"><v>1</v></c></row>"
                        + "<row r=\"5\"><c r=\"B5\" s=\"3\" t=\"inlineStr\"><is><t>Total</t></is></c></row>"
                        + "</sheetData></worksheet>"
                );
                Put(
                    "xl/worksheets/sheet2.xml",
                    "<worksheet xmlns=\""
                        + main
                        + "\"><sheetData><row r=\"1\"><c r=\"A1\"><v>3.5</v></c></row></sheetData></worksheet>"
                );
            }

            return path;
        }

        [Theory]
        [InlineData("A1:D10", 0, 0, 9, 3)]
        [InlineData("b2", 1, 1, 1, 1)]
        [InlineData("$C$3:$A$1", 0, 0, 2, 2)]
        [InlineData("A:A", 0, 0, -1, 0)]
        [InlineData("B:D", 0, 1, -1, 3)]
        [InlineData("AA10", 9, 26, 9, 26)]
        public void ARange_ReadsInASpreadsheetsWords_EitherEndFirst(
            string text,
            int top,
            int left,
            int bottom,
            int right
        )
        {
            Assert.True(CadSheetRange.TryParse(text, out CadSheetRange range));
            Assert.Equal(
                (top, left, bottom, right),
                (range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn)
            );
        }

        [Theory]
        [InlineData("")]
        [InlineData("A1:")]
        [InlineData("1:3")]
        [InlineData("A1:B")]
        [InlineData("ABCD1")]
        [InlineData("A0")]
        public void AnythingElse_IsNoRange(string text) => Assert.False(CadSheetRange.TryParse(text, out _));

        [Fact]
        public void ARangeAsWords_NamesItsColumnsInLetters_AndWholeColumnsWithNoRows()
        {
            Assert.Equal("A1:D10", new CadSheetRange(0, 0, 9, 3).ToString());
            Assert.Equal("B2", new CadSheetRange(1, 1, 1, 1).ToString());
            Assert.Equal("B:C", new CadSheetRange(0, 1, -1, 2).ToString());
            Assert.Equal("AA1", new CadSheetRange(0, 26, 0, 26).ToString());
            Assert.Equal("Z", CadSheetRange.ColumnName(25));
        }

        [Fact]
        public void ABlockCutFromASheet_KeepsItsSizeWhereItsLastCellsAreEmpty_AndWholeColumnsRunToTheLastRow()
        {
            var sheet = new CadSheet("S");
            sheet.Set(0, 0, CadSheetCell.OfText("a"));
            sheet.Set(2, 1, CadSheetCell.OfText("z"));

            CadSheet block = sheet.Cut(new CadSheetRange(1, 0, 4, 2));
            Assert.Equal((4, 3), (block.RowCount, block.ColumnCount));
            Assert.Equal("z", block[1, 1].Text);

            CadSheet column = sheet.Cut(CadSheetRange.TryParse("B:B", out CadSheetRange b) ? b : null);
            Assert.Equal((3, 1), (column.RowCount, column.ColumnCount));
            Assert.Equal("z", column[2, 0].Text);
        }

        [Theory]
        [InlineData(0.25, "0%", "25%")]
        [InlineData(0.256, "0.0%", "25.6%")]
        [InlineData(1234.5, "#,##0.00", "1,234.50")]
        [InlineData(1234.5, "#,##0", "1,235")]
        [InlineData(3.14159, "0.00", "3.14")]
        [InlineData(7, "000", "007")]
        [InlineData(7.5, "0.0\" mm\"", "7.5 mm")]
        [InlineData(1234.5, "0.00E+00", "1.23E+03")]
        [InlineData(-3.5, "0.0", "-3.5")]
        [InlineData(-3.5, "0.0;(0.0)", "(3.5)")]
        [InlineData(0.5, "General", "0.5")]
        [InlineData(0.30000000000000004, "General", "0.3")]
        [InlineData(1234567.891, "General", "1234567.891")]
        [InlineData(1.5, "0.00#", "1.50")]
        [InlineData(1.2345, "0.00#", "1.235")]
        public void ANumber_ShowsAsItsFormatMakesIt(double value, string code, string shown) =>
            Assert.Equal(shown, CadNumberFormat.Format(value, code, false));

        [Theory]
        [InlineData(44927.0, "m/d/yyyy", "1/1/2023")]
        [InlineData(44927.0, "yyyy-mm-dd", "2023-01-01")]
        [InlineData(44927.0, "d-mmm-yy", "1-Jan-23")]
        [InlineData(44927.0, "dddd, mmmm d", "Sunday, January 1")]
        [InlineData(44927.75, "h:mm AM/PM", "6:00 PM")]
        [InlineData(44927.75, "m/d/yyyy h:mm", "1/1/2023 18:00")]
        [InlineData(0.5, "hh:mm:ss", "12:00:00")]
        [InlineData(1.5, "[h]:mm", "36:00")]
        [InlineData(44927.0, "yyyy\\-mm", "2023-01")]
        public void ADate_ShowsAsItsFormatMakesIt_MinutesAfterAnHour(double serial, string code, string shown)
        {
            Assert.True(CadNumberFormat.IsDate(code));
            Assert.Equal(shown, CadNumberFormat.Format(serial, code, false));
        }

        [Fact]
        public void ACodeThatIsNoDate_AndADateCountedFrom1904()
        {
            Assert.False(CadNumberFormat.IsDate("0.00"));
            Assert.False(CadNumberFormat.IsDate("0.0\" mm\""));
            Assert.False(CadNumberFormat.IsDate("0.00E+00"));
            Assert.False(CadNumberFormat.IsDate("General"));
            Assert.Equal("1/2/1904", CadNumberFormat.Format(1.0, "m/d/yyyy", true));
        }

        [Fact]
        public void ACsvFile_ReadsQuotesCommasAndLineBreaks_NumbersKeepTheirWords_AndWritesBackTheSame()
        {
            const string text = "Mark,Qty,Note\r\nB-12,007,\"Nut, M8\"\r\nB-13,2.5,\"say \"\"hi\"\"\r\nthere\"\r\n";
            CadSheet sheet = CadCsvFile.Parse(text, "parts");

            Assert.Equal((3, 3), (sheet.RowCount, sheet.ColumnCount));
            Assert.Equal(CadSheetCellKind.Number, sheet[1, 1].Kind);
            Assert.Equal("007", sheet[1, 1].Text);
            Assert.Equal(7.0, sheet[1, 1].Number);
            Assert.Equal("Nut, M8", sheet[1, 2].Text);
            Assert.Equal("say \"hi\"\r\nthere", sheet[2, 2].Text);
            Assert.Equal(text, CadCsvFile.ToText(sheet));
        }

        [Fact]
        public void ACsvFile_OfNoLineBreakAtItsEnd_AndShortRows_ReadsEveryRow()
        {
            CadSheet sheet = CadCsvFile.Parse("a,b\r\nc", "x");

            Assert.Equal((2, 2), (sheet.RowCount, sheet.ColumnCount));
            Assert.Equal("c", sheet[1, 0].Text);
            Assert.Equal(CadSheetCellKind.Empty, sheet[1, 1].Kind);
        }

        [Fact]
        public void ACsvFileWithAByteOrderMark_IsReadAsUtf8_AndAWriteBackKeepsTheMark()
        {
            string folder = Folder();
            string path = Path.Combine(folder, "m.csv");
            File.WriteAllBytes(
                path,
                new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("Tên,1\r\n")).ToArray()
            );

            Assert.Equal("Tên", CadCsvFile.Read(path)[0, 0].Text);
            Assert.True(CadCsvFile.HasByteOrderMark(path));
            Assert.Equal("m", CadCsvFile.Read(path).Name);
        }

        [Fact]
        public void AWorkbook_ReadsItsSheetsAndItsNamedRanges()
        {
            CadWorkbook book = CadWorkbook.Open(Workbook(Folder()));

            Assert.Equal(new[] { "Parts", "Prices" }, book.SheetNames);
            Assert.Equal("Parts!$B$2:$C$3", book.NamedRanges["Rates"]);
            Assert.Equal("3.5", book.ReadSheet("prices")[0, 0].Text);
        }

        [Fact]
        public void AWorkbooksCells_ReadAsTheirFormatsShowThem_WithTheLookTheTableTakesOver()
        {
            CadSheet sheet = CadWorkbook.Open(Workbook(Folder())).ReadSheet("Parts");

            Assert.Equal((5, 4), (sheet.RowCount, sheet.ColumnCount));
            Assert.Equal("Mark", sheet[0, 0].Text);
            Assert.Equal("Rate", sheet[0, 2].Text);
            Assert.Equal("Bolt M8", sheet[1, 0].Text);
            Assert.Equal("12", sheet[1, 1].Text);
            Assert.Equal(CadSheetCellKind.Number, sheet[1, 1].Kind);
            Assert.Equal("25%", sheet[1, 2].Text);
            Assert.Equal(0.25, sheet[1, 2].Number);
            Assert.Equal(CadSheetCellKind.Date, sheet[1, 3].Kind);
            Assert.Equal("1/1/2023", sheet[1, 3].Text);
            Assert.Equal("7.5 mm", sheet[2, 1].Text);
            Assert.Equal("15", sheet[2, 2].Text);
            Assert.Equal(CadSheetCellKind.Boolean, sheet[2, 3].Kind);
            Assert.Equal("TRUE", sheet[2, 3].Text);
            Assert.Equal(CadSheetCellKind.Empty, sheet[3, 0].Kind);

            CadSheetCell total = sheet[4, 1];
            Assert.Equal("Total", total.Text);
            Assert.Equal(3, total.Alignment);
            Assert.Equal(0xFFFF00, total.Fill);
            Assert.Equal(0xFF0000, total.FontColor);
        }

        [Fact]
        public void ASheetsRangeAndANamedRange_AreCutFromTheWorkbookByTheConnectionString()
        {
            string folder = Folder();
            string path = Workbook(folder);

            CadSheet entire = CadDataSource.Read(CadDataSource.Compose(path, "Parts", null), null);
            Assert.Equal((5, 4), (entire.RowCount, entire.ColumnCount));

            CadSheet range = CadDataSource.Read(CadDataSource.Compose(path, "Parts", "A1:B2"), null);
            Assert.Equal((2, 2), (range.RowCount, range.ColumnCount));
            Assert.Equal("12", range[1, 1].Text);

            CadSheet named = CadDataSource.Read(CadDataSource.Compose(path, "Rates", null), null);
            Assert.Equal((2, 2), (named.RowCount, named.ColumnCount));
            Assert.Equal("25%", named[0, 1].Text);

            // A relative path is of the drawing's folder, and a file alone ("no path") too.
            Assert.Equal("Bolt M8", CadDataSource.Read("parts.xlsx!Parts!A2", folder)[0, 0].Text);
        }

        [Fact]
        public void ASourceThatIsNotThere_IsToldInAutoCadsWords()
        {
            string folder = Folder();
            string path = Workbook(folder);

            Assert.Contains(
                "Excel Sheet Not Found",
                Assert.Throws<CadDataSourceException>(() => CadDataSource.Read(path + "!Missing!A1", null)).Message
            );
            Assert.Contains(
                "Named range not found",
                Assert.Throws<CadDataSourceException>(() => CadDataSource.Read(path + "!Nothing", null)).Message
            );
            Assert.Contains(
                "Invalid Range",
                Assert.Throws<CadDataSourceException>(() => CadDataSource.Read(path + "!Parts!A1:", null)).Message
            );
            Assert.Contains(
                "cannot be found",
                Assert
                    .Throws<CadDataSourceException>(() =>
                        CadDataSource.Read(Path.Combine(folder, "none.xlsx") + "!Parts", null)
                    )
                    .Message
            );
            Assert.Contains(
                "not a valid Excel file",
                Assert.Throws<CadDataSourceException>(() => CadDataSource.Read("table.txt", folder)).Message
            );

            string broken = Path.Combine(folder, "broken.xlsx");
            File.WriteAllText(broken, "not a zip");
            Assert.Contains(
                "not a valid Excel file",
                Assert.Throws<CadDataSourceException>(() => CadDataSource.Read(broken + "!Sheet1", null)).Message
            );
            Assert.Contains(
                "no longer supported",
                Assert
                    .Throws<CadDataSourceException>(() =>
                        CadDataSource.Read(Path.Combine(folder, "old.xls") + "!Sheet1", null)
                    )
                    .Message
            );
        }

        [Fact]
        public void ANewWorkbook_WrittenForAnExtraction_ReadsBackWithNumbersAsNumbers()
        {
            string folder = Folder();
            string path = Path.Combine(folder, "out.xlsx");
            var sheet = new CadSheet("Sheet1");
            sheet.Set(0, 0, CadSheetCell.OfText("Name"));
            sheet.Set(0, 1, CadSheetCell.OfText("Length"));
            sheet.Set(1, 0, CadSheetCell.OfText("Line <1> & \"two\""));
            sheet.Set(1, 1, CadSheetCell.OfNumber(12.5));
            sheet.Set(2, 0, CadSheetCell.OfText("line\nbreak"));

            CadWorkbook.Write(path, "Sheet1", sheet);
            CadSheet back = CadWorkbook.Open(path).ReadSheet("Sheet1");

            Assert.Equal((3, 2), (back.RowCount, back.ColumnCount));
            Assert.Equal("Line <1> & \"two\"", back[1, 0].Text);
            Assert.Equal(CadSheetCellKind.Number, back[1, 1].Kind);
            Assert.Equal(12.5, back[1, 1].Number);
            Assert.Equal("line\nbreak", back[2, 0].Text);
            Assert.Throws<ArgumentException>(() => CadWorkbook.Write(path, "bad:name", sheet));
        }

        [Fact]
        public void WritingCellsBack_PutsThemInTheSheetAndLeavesEveryOtherPartAndFormulaAsItWas()
        {
            string folder = Folder();
            string path = Workbook(folder);
            var values = new CadSheet("x");
            values.Set(0, 0, CadSheetCell.OfText("Washer"));
            values.Set(0, 1, CadSheetCell.OfNumber(99));
            values.Set(1, 0, CadSheetCell.OfNumber(1));
            values.Set(1, 1, CadSheetCell.OfNumber(5));
            values.Set(2, 0, CadSheetCell.OfText("added"));
            values.Set(2, 1, CadSheetCell.OfText("x"));

            // The block goes in from B2; C3 holds a formula and is left; B3 keeps its format; row 4 is new.
            int written = CadWorkbook.WriteBack(path, "parts", 1, 1, values);

            Assert.Equal(5, written);
            CadSheet back = CadWorkbook.Open(path).ReadSheet("Parts");
            Assert.Equal("Washer", back[1, 1].Text);
            Assert.Equal(99.0, back[1, 2].Number);
            Assert.Equal("1.0 mm", back[2, 1].Text);
            Assert.Equal("15", back[2, 2].Text);
            Assert.Equal("added", back[3, 1].Text);
            Assert.Equal("x", back[3, 2].Text);
            Assert.Equal("Mark", back[0, 0].Text);
            Assert.Equal("Bolt M8", back[1, 0].Text);
            Assert.Equal(3, back[4, 1].Alignment);
            Assert.Equal("3.5", CadWorkbook.Open(path).ReadSheet("Prices")[0, 0].Text);

            using (var zip = ZipFile.OpenRead(path))
            {
                Assert.NotNull(zip.GetEntry("xl/styles.xml"));
                Assert.NotNull(zip.GetEntry("xl/sharedStrings.xml"));
            }
        }

        [Fact]
        public void AConnectionStringWritesThroughACsvFile_KeepingItsOtherCells()
        {
            string folder = Folder();
            string path = Path.Combine(folder, "parts.csv");
            File.WriteAllText(path, "Mark,Qty\r\nB-12,2\r\nB-13,3\r\n");
            var values = new CadSheet("v");
            values.Set(0, 0, CadSheetCell.OfText("N-1"));
            values.Set(0, 1, CadSheetCell.OfNumber(9));

            Assert.Equal(2, CadDataSource.Write(path + "!parts!A2", null, values));

            Assert.Equal("Mark,Qty\r\nN-1,9\r\nB-13,3\r\n", File.ReadAllText(path));
        }
    }
}
