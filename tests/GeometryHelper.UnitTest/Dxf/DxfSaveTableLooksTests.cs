using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What a table's cells, rows and styles give the TABLECONTENT and the map of cell styles AutoCAD 2021 builds when it reads a
    /// table made in code, and the lines of the cells it writes in the table itself: the looks of 8 table styles, the alignment,
    /// text height, text style, colour, fill, turn and value of a cell, the kinds of rows, merged cells, and the lines between
    /// cells (a line is the top or the left of the cell below or right of it, a line with nothing but defaults is none, the line
    /// between a title and a header is the title's bottom where the header's top is a default). Every expected group is what
    /// AutoCAD 2021 wrote, DxfIn then DxfOut in its console, of the same table without them.
    /// </summary>
    public class DxfSaveTableLooksTests
    {
        #region Helpers

        private static DxfColor Colour(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        private static DxfDatabase Metric()
        {
            var db = new DxfDatabase("AC1032");
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));

            if (db.TableStyleDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_TXT", FileName = "arial.ttf" });
            return db;
        }

        private static DxfTable Table(DxfDatabase db, int rows = 5, string style = null)
        {
            var table = new DxfTable(new GeoPoint3(0.0, 0.0, 0.0), rows, 3, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            if (style != null)
            {
                table.TableStyle = db.TableStyleDictionary[style];
            }

            for (int row = 0; row < rows; row++)
            {
                table.SetTextString(row, 0, "r" + row.ToString(CultureInfo.InvariantCulture));
            }

            return table;
        }

        private static string Flat(IEnumerable<KeyValuePair<int, string>> groups) =>
            " "
            + string.Join(" ", groups.Select(g => g.Key.ToString(CultureInfo.InvariantCulture) + "=" + g.Value.Trim()))
            + " ";

        private static List<DwgAnswerKey.Record> Saved(DxfDatabase db)
        {
            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                return DwgAnswerKey.Parse(stream.ToArray());
            }
        }

        private static DwgAnswerKey.Record Find(List<DwgAnswerKey.Record> all, string handle) =>
            all.Single(r => r.Key == "X:" + handle.ToUpperInvariant());

        private static string After(DwgAnswerKey.Record record, string value, int code)
        {
            int at = record.Groups.FindIndex(g => g.Value.Trim() == value);
            return record.Groups.Skip(at).First(g => g.Key == code).Value.Trim();
        }

        private static DwgAnswerKey.Record TableOf(List<DwgAnswerKey.Record> all, int index = 0) =>
            all.Where(r => r.Section == "ENTITIES" && r.Type == "ACAD_TABLE").ElementAt(index);

        private static DwgAnswerKey.Record ContentOf(List<DwgAnswerKey.Record> all, DwgAnswerKey.Record table)
        {
            DwgAnswerKey.Record dictionary = Find(all, After(table, "{ACAD_XDICTIONARY", 360));
            DwgAnswerKey.Record xrecord = Find(all, After(dictionary, "ACAD_XREC_ROUNDTRIP", 360));
            return Find(all, After(xrecord, "ACAD_ROUNDTRIP_2008_TABLE_ENTITY", 360));
        }

        /// <summary>The cells of an ACAD_TABLE record, each as its flat groups from its 171 to the next 171.</summary>
        private static List<string> Cells(DwgAnswerKey.Record table)
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

        /// <summary>The cells of a TABLECONTENT, each as its flat groups from its 300 CELL to its TABLECELL_END.</summary>
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

        /// <summary>The cell of a row and a column of a table of 3 columns.</summary>
        private static string Cell(List<string> cells, int row, int column) => cells[row * 3 + column];

        /// <summary>The lines a cell of the TABLECONTENT lists: its count and each of its lines (the number of the line, its properties).</summary>
        private static string Lines(string cell)
        {
            int at = cell.IndexOf(" 171=0 94=", StringComparison.Ordinal);
            int end = cell.IndexOf(" 309=TABLEFORMAT_END", at, StringComparison.Ordinal);
            return Regex
                .Replace(
                    cell.Substring(at + 7, end - at - 7)
                        .Replace(" 302=GRIDFORMAT 1=GRIDFORMAT_BEGIN", string.Empty)
                        .Replace(" 91=1 ", " ")
                        .Replace(" 340=0 ", " ")
                        .Replace(" 40=0.045 309=GRIDFORMAT_END", string.Empty),
                    " +",
                    " "
                )
                .Trim();
        }

        private static string Look(string cell)
        {
            int at = cell.IndexOf(" 1=FORMATTEDTABLEDATACELL_BEGIN", StringComparison.Ordinal);
            return cell.Substring(at, cell.IndexOf(" 171=0 94=", at, StringComparison.Ordinal) - at);
        }

        #endregion

        #region Table styles

        /// <summary>The cell styles of a style's map as a line each: the fill, alignment, colour, text style and height, the margins, the six lines.</summary>
        private static string[] Styles(List<DwgAnswerKey.Record> all, string name, string standardText)
        {
            DwgAnswerKey.Record dictionary = all.Single(r =>
                r.Type == "DICTIONARY" && r.Groups.Any(g => g.Key == 3 && g.Value.Trim() == "ACAD_TABLESTYLE")
            );
            DwgAnswerKey.Record style = Find(
                all,
                After(Find(all, After(dictionary, "ACAD_TABLESTYLE", 350)), name, 350)
            );
            DwgAnswerKey.Record holder = Find(all, After(style, "{ACAD_XDICTIONARY", 360));
            DwgAnswerKey.Record map = Find(all, After(holder, "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP", 360));
            string flat = Flat(map.Groups);
            var lines = new List<string>();

            foreach (
                string part in flat.Split(new[] { " 300=CELLSTYLE 1=TABLEFORMAT_BEGIN " }, StringSplitOptions.None)
                    .Skip(1)
            )
            {
                string[] top = Regex
                    .Match(part, @"^90=5 170=1 91=0 92=(\d+) 62=(\d+) 93=1 ")
                    .Groups.Cast<Group>()
                    .Select(g => g.Value)
                    .ToArray();
                Match format = Regex.Match(part, @" 94=(\d+) 62=(\d+) 340=(\w+) 144=([\d.]+) 309=CONTENTFORMAT_END ");
                MatchCollection margins = Regex.Matches(
                    Regex.Match(part, @"1=CELLMARGIN_BEGIN (.*?) 309=CELLMARGIN_END").Groups[1].Value,
                    @"40=([\d.]+)"
                );
                var grid = new List<string>();

                foreach (
                    Match line in Regex.Matches(
                        part,
                        @"95=(\d+) 302=GRIDFORMAT 1=GRIDFORMAT_BEGIN 90=0 91=1 62=(\d+) 92=(-?\d+) 340=0 93=(\d) 40=0.045 309=GRIDFORMAT_END"
                    )
                )
                {
                    grid.Add(
                        line.Groups[1].Value
                            + ":"
                            + line.Groups[2].Value
                            + "/"
                            + line.Groups[3].Value
                            + "/"
                            + line.Groups[4].Value
                    );
                }

                lines.Add(
                    Regex.Match(part, @"300=(_\w+) 309=CELLSTYLE_END").Groups[1].Value
                        + " fill "
                        + top[2]
                        + " align "
                        + format.Groups[1].Value
                        + " colour "
                        + format.Groups[2].Value
                        + " font "
                        + (format.Groups[3].Value == standardText ? "Standard" : format.Groups[3].Value)
                        + " height "
                        + format.Groups[4].Value
                        + " margins "
                        + string.Join(",", margins.Cast<Match>().Select(m => m.Groups[1].Value))
                        + " lines "
                        + string.Join(" ", grid)
                );
            }

            return lines.ToArray();
        }

        [Fact]
        public void EachStyleOfADrawing_GetsTheMapOfItsCellStyles_AsAutoCadBuildsIt()
        {
            DxfDatabase db = Metric();
            AddStyle(db, "GH_BIG", s =>
            {
                s.SetTextHeight(8.0, DxfRowType.TitleRow);
                s.SetTextHeight(6.0, DxfRowType.HeaderRow);
                s.SetTextHeight(5.0, DxfRowType.DataRow);
                s.SetAlignment(DxfCellAlignment.TopLeft, DxfRowType.TitleRow);
                s.SetAlignment(DxfCellAlignment.MiddleRight, DxfRowType.HeaderRow);
                s.SetAlignment(DxfCellAlignment.BottomCenter, DxfRowType.DataRow);
            });
            AddStyle(db, "GH_COLOURS", s =>
            {
                s.SetBackgroundColor(Colour(8), DxfRowType.HeaderRow);
                s.SetBackgroundColor(Colour(3), DxfRowType.DataRow);
                s.SetColor(Colour(1), DxfRowType.TitleRow);
                s.SetColor(Colour(5), DxfRowType.DataRow);
            });
            AddStyle(db, "GH_GRID", s =>
            {
                s.SetGridColor(Colour(1), DxfGridLineType.OuterGridLines, DxfRowType.TitleRow);
                s.SetGridVisibility(false, DxfGridLineType.HorizontalInside, DxfRowType.DataRow);
                s.SetGridLineWeight(DxfLineWeight.LineWeight050, DxfGridLineType.HorizontalBottom, DxfRowType.DataRow);
            });
            AddStyle(db, "GH_MARGINS", s =>
            {
                s.HorizontalCellMargin = 3.0;
                s.VerticalCellMargin = 2.0;
            });
            AddStyle(db, "GH_TEXT", s => s.SetTextStyle("GH_TXT", DxfRowType.DataRow));
            List<DwgAnswerKey.Record> all = Saved(db);
            string standard = db.TextStyleTable["Standard"].Handle;
            string gh = db.TextStyleTable["GH_TXT"].Handle;
            const string none = "0:0/-2/0 0:0/-2/0 0:0/-2/0 0:0/-2/0 0:0/-2/0 0:0/-2/0";

            Assert.Equal(
                new[]
                {
                    "_TITLE fill 257 align 5 colour 0 font Standard height 6.0 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                    "_HEADER fill 257 align 5 colour 0 font Standard height 4.5 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                    "_DATA fill 257 align 2 colour 0 font Standard height 4.5 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                },
                Styles(all, "Standard", standard)
            );
            Assert.Equal(
                new[]
                {
                    "_TITLE fill 257 align 1 colour 0 font Standard height 8.0 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                    "_HEADER fill 257 align 6 colour 0 font Standard height 6.0 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                    "_DATA fill 257 align 8 colour 0 font Standard height 5.0 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                },
                Styles(all, "GH_BIG", standard)
            );
            Assert.Equal(
                new[]
                {
                    "_TITLE fill 257 align 5 colour 1 font Standard height 6.0 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                    "_HEADER fill 8 align 5 colour 0 font Standard height 4.5 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                    "_DATA fill 3 align 2 colour 5 font Standard height 4.5 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                },
                Styles(all, "GH_COLOURS", standard)
            );
            Assert.Equal(
                new[]
                {
                    "_TITLE fill 257 align 5 colour 0 font Standard height 6.0 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines 1:1/-2/0 2:0/-2/0 4:1/-2/0 8:1/-2/0 16:0/-2/0 32:1/-2/0",
                    "_HEADER fill 257 align 5 colour 0 font Standard height 4.5 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                        + Number(none),
                    "_DATA fill 257 align 2 colour 0 font Standard height 4.5 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines 1:0/-2/0 2:0/-2/1 4:0/50/0 8:0/-2/0 16:0/-2/0 32:0/-2/0",
                },
                Styles(all, "GH_GRID", standard)
            );
            Assert.Equal(
                new[]
                {
                    "_TITLE fill 257 align 5 colour 0 font Standard height 6.0 margins 2.0,3.0,2.0,3.0,0.18,0.18 lines "
                        + Number(none),
                    "_HEADER fill 257 align 5 colour 0 font Standard height 4.5 margins 2.0,3.0,2.0,3.0,0.18,0.18 lines "
                        + Number(none),
                    "_DATA fill 257 align 2 colour 0 font Standard height 4.5 margins 2.0,3.0,2.0,3.0,0.18,0.18 lines "
                        + Number(none),
                },
                Styles(all, "GH_MARGINS", standard)
            );
            Assert.Equal(
                "_DATA fill 257 align 2 colour 0 font "
                    + gh
                    + " height 4.5 margins 1.5,1.5,1.5,1.5,0.18,0.18 lines "
                    + Number(none),
                Styles(all, "GH_TEXT", standard)[2]
            );
        }

        /// <summary>The lines of a cell style as the list of its six, numbered by their lines (1, 2, 4, 8, 16, 32).</summary>
        private static string Number(string lines) =>
            string.Join(
                " ",
                lines
                    .Split(' ')
                    .Select((line, k) => (1 << k).ToString(CultureInfo.InvariantCulture) + ":" + line.Substring(2))
            );

        private static DxfTableStyle AddStyle(DxfDatabase db, string name, Action<DxfTableStyle> set)
        {
            var style = new DxfTableStyle();
            db.TableStyleDictionary.SetAt(name, style);
            set(style);
            return style;
        }

        [Fact]
        public void AStyleAddedAloneOrNamedByNoTable_IsGivenItsMapToo_AndOneThatHasItsOwnIsNot()
        {
            DxfDatabase db = Metric();
            AddStyle(db, "GH_ALONE", s => s.SetTextHeight(3.0, DxfRowType.DataRow));
            List<DwgAnswerKey.Record> once = Saved(db);
            Assert.Equal(2, once.Count(r => r.Type == "CELLSTYLEMAP"));

            // Read back, both styles hold theirs: saving them again makes none.
            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                DxfDatabase read = DxfDatabase.Parse(
                    System.Text.Encoding.GetEncoding(28591).GetString(stream.ToArray())
                );
                ((DxfTableStyle)read.TableStyleDictionary["GH_ALONE"]).SetTextHeight(4.0, DxfRowType.DataRow);

                using (var again = new MemoryStream())
                {
                    read.Save(again);
                    Assert.Equal(2, DwgAnswerKey.Parse(again.ToArray()).Count(r => r.Type == "CELLSTYLEMAP"));
                }
            }
        }

        #endregion

        #region The cells of the table

        [Fact]
        public void TheKindsOfRows_AreTheTitleTheHeaderAndTheData_UnlessTheStyleLeavesOutTheTitle()
        {
            DxfDatabase db = Metric();
            AddStyle(db, "GH_NOTITLE", s => s.IsTitleSuppressed = true);
            Table(db, 4);
            Table(db, 4, "GH_NOTITLE");
            List<DwgAnswerKey.Record> all = Saved(db);

            string[] kinds(DwgAnswerKey.Record table) =>
                Regex
                    .Matches(Flat(ContentOf(all, table).Groups), @"1=TABLEROW_BEGIN 90=(\d) 40=([\d.]+)")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value + "/" + m.Groups[2].Value)
                    .ToArray();

            Assert.Equal(new[] { "1/9.0", "2/9.0", "3/9.0", "3/9.0" }, kinds(TableOf(all, 0)));
            Assert.Equal(new[] { "2/9.0", "3/9.0", "3/9.0", "3/9.0" }, kinds(TableOf(all, 1)));
        }

        [Fact]
        public void AMergedBlock_IsListedAtTheEndOfTheContent_AndItsHiddenCellsHoldNoContent()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db, 5);
            table.MergeCells(new DxfCellRange(1, 1, 2, 2));
            table.MergeCells(new DxfCellRange(3, 0, 4, 0));
            List<DwgAnswerKey.Record> all = Saved(db);
            DwgAnswerKey.Record content = ContentOf(all, TableOf(all));
            List<string> cells = ContentCells(content);

            Assert.EndsWith(
                " 90=3 91=0 92=0 93=0 94=2 91=1 92=1 93=2 94=2 91=3 92=0 93=4 94=0 100=AcDbTableContent 340="
                    + content.Groups.Last(g => g.Key == 340).Value
                    + " ",
                Flat(content.Groups)
            );
            Assert.Contains(" 92=0 95=0 309=LINKEDTABLEDATACELL_END ", Cell(cells, 2, 1));
            Assert.Contains(" 92=0 95=0 309=LINKEDTABLEDATACELL_END ", Cell(cells, 2, 2));
            Assert.Contains(" 92=0 95=0 309=LINKEDTABLEDATACELL_END ", Cell(cells, 4, 0));
            Assert.Contains(" 92=0 95=1 302=CONTENT ", Cell(cells, 3, 0));
        }

        [Fact]
        public void TheLooksOfACell_AreItsContentFormatAndItsTableFormat_AsAutoCadBuildsThem()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db, 6);

            foreach (
                int[] at in new[] { new[] { 2, 1 }, new[] { 2, 2 }, new[] { 3, 1 }, new[] { 3, 2 }, new[] { 4, 1 } }
            )
            {
                table.SetTextString(at[0], at[1], "x");
            }

            table.SetAlignment(2, 0, DxfCellAlignment.TopLeft);
            table.SetAlignment(2, 1, DxfCellAlignment.MiddleCenter);
            table.SetTextHeight(2, 2, 3.5);
            table.SetTextStyle(3, 0, db.TextStyleTable["GH_TXT"]);
            table.SetContentColor(3, 1, Colour(1));
            table.SetContentColor(3, 2, DxfColor.ByBlock);
            table.SetBackgroundColor(4, 0, Colour(2));
            table.SetTextRotation(4, 1, DxfRotationAngle.Degrees090);
            table.SetValue(4, 2, 12.5);
            table.SetAlignment(5, 0, DxfCellAlignment.TopRight);
            table.SetContentColor(5, 0, Colour(2));
            table.SetTextHeight(5, 0, 2.0);
            List<DwgAnswerKey.Record> all = Saved(db);
            List<string> cells = ContentCells(ContentOf(all, TableOf(all)));
            string font = db.TextStyleTable["GH_TXT"].Handle;

            // The content format: what the cell overrides of its text (the flags 3 of its kind, 4 its turn, 32 colour, 64 text style, 128 height).
            Assert.Contains(
                " 1=CONTENTFORMAT_BEGIN 90=131 91=0 92=4 93=0 300= 40=0.0 140=1.0 94=1 62=0 340=0 144=3.5 309=",
                Cell(cells, 2, 2)
            );
            Assert.Contains(
                " 90=67 91=0 92=4 93=0 300= 40=0.0 140=1.0 94=1 62=0 340=" + font + " 144=0.18 ",
                Cell(cells, 3, 0)
            );
            Assert.Contains(" 90=35 91=0 92=4 93=0 300= 40=0.0 140=1.0 94=1 62=1 340=0 144=0.18 ", Cell(cells, 3, 1));
            Assert.Contains(" 90=35 91=0 92=4 93=0 300= 40=0.0 140=1.0 94=1 62=0 340=0 144=0.18 ", Cell(cells, 3, 2));
            Assert.Matches(
                @" 90=7 91=0 92=4 93=0 300= 40=1\.570796326794\d+ 140=1\.0 94=1 62=0 340=0 144=0\.18 ",
                Cell(cells, 4, 1)
            );
            Assert.Contains(" 90=163 91=0 92=4 93=0 300= 40=0.0 140=1.0 94=1 62=2 340=0 144=2.0 ", Cell(cells, 5, 0));

            // The table format: the alignment (flag 16) and the fill (flag 512, the colour of the fill in 62; 257 none).
            Assert.Contains(
                " 90=1 170=1 91=16 92=0 62=257 93=1 300=CONTENTFORMAT 1=CONTENTFORMAT_BEGIN 90=0 91=0 92=512 93=0 300= 40=0.0 140=1.0 94=1 ",
                Look(Cell(cells, 2, 0)) + " "
            );
            Assert.Contains(
                " 91=16 92=0 62=257 93=1 300=CONTENTFORMAT 1=CONTENTFORMAT_BEGIN 90=0 91=0 92=512 93=0 300= 40=0.0 140=1.0 94=5 ",
                Look(Cell(cells, 2, 1))
            );
            Assert.Contains(" 91=512 92=0 62=2 93=1 ", Look(Cell(cells, 4, 0)));
            Assert.Contains(
                " 91=16 92=0 62=257 93=1 300=CONTENTFORMAT 1=CONTENTFORMAT_BEGIN 90=0 91=0 92=512 93=0 300= 40=0.0 140=1.0 94=3 ",
                Look(Cell(cells, 5, 0))
            );
        }

        [Fact]
        public void ANumberWithItsFormat_KeepsItsKindAndItsFormatInTheContentFormat()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db, 3);
            table.SetValue(2, 1, 7.25);
            table.SetFormat(2, 1, "%lu2%pr2");
            table.SetValue(2, 2, 42);
            List<DwgAnswerKey.Record> all = Saved(db);
            List<string> cells = ContentCells(ContentOf(all, TableOf(all)));

            Assert.Contains(
                " 300=VALUE 93=4 90=2 140=7.25 94=0 300=%lu2%pr2 302=7.25 304=ACVALUE_END ",
                Cell(cells, 2, 1)
            );
            Assert.Contains(" 1=CONTENTFORMAT_BEGIN 90=3 91=0 92=2 93=0 300=%lu2%pr2 40=0.0 ", Cell(cells, 2, 1));
            Assert.Contains(" 300=VALUE 93=6 90=1 91=42 94=0 300= 302=42 304=ACVALUE_END ", Cell(cells, 2, 2));
            Assert.Contains(" 1=CONTENTFORMAT_BEGIN 90=3 91=0 92=1 93=0 300= 40=0.0 ", Cell(cells, 2, 2));
        }

        [Fact]
        public void ACellThatGivesABackgroundColour_IsFilledWithIt_AsAutoCadReadsIt()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db, 3);
            table.SetBackgroundColor(2, 1, Colour(2));
            List<DwgAnswerKey.Record> all = Saved(db);
            string cell = Cells(TableOf(all))[7];

            Assert.Contains(" 91=6 ", cell);
            Assert.Contains(" 63=2 ", cell);
            Assert.Contains(" 283=0 ", cell);
        }

        #endregion

        #region The lines between cells

        [Fact]
        public void TheBottomOfACell_IsTheTopOfTheCellBelow_InTheTableAndListedWithTheCellAboveInTheContent()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db, 5);
            table.SetGridColor(2, 1, DxfGridLineType.HorizontalBottom, Colour(1));
            List<DwgAnswerKey.Record> all = Saved(db);
            DwgAnswerKey.Record saved = TableOf(all);
            List<string> record = Cells(saved);
            List<string> content = ContentCells(ContentOf(all, saved));

            Assert.Contains(" 172=0 ", record[7]);
            Assert.DoesNotContain(" 66=", record[7]);
            Assert.Contains(" 172=1 ", record[10]);
            Assert.Contains(" 91=17472 ", record[10]);
            Assert.Contains(" 279=-2 69=1 ", record[10]);
            Assert.Equal("94=1 95=4 90=8 62=1 92=0 93=0", Lines(Cell(content, 2, 1)));
            Assert.Equal("94=0", Lines(Cell(content, 3, 1)));
        }

        [Fact]
        public void TwoLinesOnOneEdge_ReadFromTheCellBelow_ThatHoldsIt_AsAutoCadReadsThem()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db, 5);
            table.SetGridColor(2, 0, DxfGridLineType.HorizontalBottom, Colour(1));
            table.SetGridColor(3, 0, DxfGridLineType.HorizontalTop, Colour(5));
            table.SetGridColor(2, 0, DxfGridLineType.VerticalRight, Colour(3));
            table.SetGridColor(2, 1, DxfGridLineType.VerticalLeft, Colour(4));
            List<DwgAnswerKey.Record> all = Saved(db);
            DwgAnswerKey.Record saved = TableOf(all);
            List<string> record = Cells(saved);
            List<string> content = ContentCells(ContentOf(all, saved));

            Assert.Contains(" 279=-2 69=5 ", record[9]);
            Assert.Contains(" 278=-2 68=4 ", record[7]);
            Assert.DoesNotContain(" 66=", record[6]);
            Assert.DoesNotContain(" 65=", record[6]);
            Assert.Equal("94=2 95=4 90=8 62=5 92=0 93=0 95=32 90=8 62=4 92=0 93=0", Lines(Cell(content, 2, 0)));
            Assert.Equal("94=0", Lines(Cell(content, 3, 0)));
            Assert.Equal("94=0", Lines(Cell(content, 2, 1)));
        }

        [Fact]
        public void ALineOnTheRimOfTheTable_StaysWithItsCell_ItsLineweightAndColourWritten()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db, 5);
            table.SetGridColor(4, 1, DxfGridLineType.HorizontalBottom, Colour(5));
            table.SetGridColor(3, 2, DxfGridLineType.VerticalRight, Colour(6));
            List<DwgAnswerKey.Record> all = Saved(db);
            List<string> record = Cells(TableOf(all));

            Assert.Contains(" 172=4 ", record[13]);
            Assert.Contains(" 91=69888 ", record[13]);
            Assert.Contains(" 276=-2 66=5 ", record[13]);
            Assert.Contains(" 172=2 ", record[11]);
            Assert.Contains(" 275=-2 65=6 ", record[11]);
        }

        [Fact]
        public void AHiddenLine_IsWrittenWithItsShowing_AndALineOfDefaultsIsNoLine()
        {
            DxfDatabase db = Metric();
            DxfTable table = Table(db, 5);
            table.SetGridVisibility(2, 2, DxfGridLineType.HorizontalBottom, false);
            table.SetGridVisibility(1, 0, DxfGridLineType.HorizontalBottom, true);
            List<DwgAnswerKey.Record> all = Saved(db);
            DwgAnswerKey.Record saved = TableOf(all);
            List<string> record = Cells(saved);
            List<string> content = ContentCells(ContentOf(all, saved));

            Assert.Contains(" 172=1 ", record[11]);
            Assert.Contains(" 279=-2 289=0 69=0 ", record[11]);
            Assert.Contains(" 172=0 ", record[3]);
            Assert.Contains(" 172=0 ", record[6]);
            Assert.Equal("94=1 95=4 90=16 62=0 92=0 93=1", Lines(Cell(content, 2, 2)));
            Assert.Equal("94=1 95=4 90=16 62=0 92=0 93=0", Lines(Cell(content, 1, 0)));
        }

        [Fact]
        public void TheLineUnderTheTitle_IsTheTitlesBottom_WhereTheHeadersTopIsADefault()
        {
            DxfDatabase db = Metric();
            AddStyle(db, "GH_TB", s =>
                s.SetGridColor(Colour(1), DxfGridLineType.HorizontalBottom, DxfRowType.TitleRow)
            );
            AddStyle(db, "GH_HT", s => s.SetGridColor(Colour(2), DxfGridLineType.HorizontalTop, DxfRowType.HeaderRow));
            AddStyle(db, "GH_HB", s =>
                s.SetGridColor(Colour(3), DxfGridLineType.HorizontalBottom, DxfRowType.HeaderRow)
            );
            AddStyle(db, "GH_BOTH", s =>
            {
                s.SetGridColor(Colour(1), DxfGridLineType.HorizontalBottom, DxfRowType.TitleRow);
                s.SetGridColor(Colour(2), DxfGridLineType.HorizontalTop, DxfRowType.HeaderRow);
            });
            AddStyle(db, "GH_HIDE", s =>
                s.SetGridVisibility(false, DxfGridLineType.HorizontalBottom, DxfRowType.TitleRow)
            );
            Table(db, 4, "GH_TB");
            Table(db, 4, "GH_HT");
            Table(db, 4, "GH_HB");
            Table(db, 4, "GH_BOTH");
            Table(db, 4, "GH_HIDE");
            List<DwgAnswerKey.Record> all = Saved(db);

            // The header's cells take the title's bottom as their top: red; the data's take the header's bottom, blue.
            Assert.Contains(" 172=1 ", Cells(TableOf(all, 0))[3]);
            Assert.Contains(" 279=-2 69=1 ", Cells(TableOf(all, 0))[3]);
            Assert.Contains(" 279=-2 69=1 ", Cells(TableOf(all, 0))[5]);
            Assert.Contains(" 279=-2 69=3 ", Cells(TableOf(all, 2))[6]);
            Assert.Contains(" 172=0 ", Cells(TableOf(all, 1))[3]);
            Assert.Contains(" 172=0 ", Cells(TableOf(all, 3))[3]);
            Assert.Contains(" 279=-2 289=0 69=0 ", Cells(TableOf(all, 4))[3]);

            // The content lists no line of them: a style's lines are in its map.
            Assert.Equal("94=0", Lines(Cell(ContentCells(ContentOf(all, TableOf(all, 0))), 1, 0)));
        }

        #endregion
    }
}
