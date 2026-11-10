using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A table's rows grow to hold their cells' words as AutoCAD 2021 lays a table out: each cell's text wrapped at the cell's
    /// width less its margins, in the presenter's ghsimplex, and its row as high as the tallest cell needs, never lower than it
    /// was. The numbers are those AutoCAD 2021 gave the same tables (GenerateLayout and RecomputeTableBlock, ghsimplex.shx).
    /// </summary>
    public class TableRowFitTests
    {
        // AutoCAD's numbers as its probe printed them, to five places.
        private const int Places = 5;

        /// <summary>A drawing of millimetres whose Standard text style is ghsimplex, with AutoCAD's Standard table style.</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1032");
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));
            db.TextStyleTable["Standard"].FileName = "ghsimplex.shx";

            if (db.TableStyleDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            return db;
        }

        private static DxfTable Table(DxfDatabase db, int rows, double[] heights)
        {
            var table = new DxfTable(new GeoPoint3(0, 0, 0), rows, 3, heights[0], 30.0);
            db.ModelSpace.AppendEntity(table);

            for (int r = 0; r < rows; r++)
            {
                table.SetRowHeight(r, heights[r]);
            }

            return table;
        }

        private static DxfMText Text(DxfTable table, string words) => table.BlockTableRecord.OfType<DxfMText>().Single(m => m.Contents == words);

        [Fact]
        public void WrappedWords_GrowTheirRows_ToTheTallestCell_AsAutoCadLaysThemOut()
        {
            DxfDatabase db = Drawing();
            DxfTable table = Table(db, 6, new[] { 11.0, 9, 9, 9, 9, 9 });
            table.SetTextString(0, 0, "Bill of steel members for the north frame of the hall");
            table.SetTextString(1, 0, "Mark");
            table.SetTextString(1, 1, "Section and grade");
            table.SetTextString(1, 2, "Qty");
            table.SetTextString(2, 0, "B1");
            table.SetTextString(2, 2, "4");
            table.SetTextString(3, 0, "B2 long mark name here");
            table.SetTextString(3, 1, "IPE200");
            table.SetTextString(4, 1, "Line one\\PLine two\\PLine three");
            table.SetTextString(5, 0, "x");
            table.SetAlignment(5, 0, DxfCellAlignment.MiddleLeft);
            table.SetTextString(5, 1, "bottom right words that wrap twice at least here");
            table.SetAlignment(5, 1, DxfCellAlignment.BottomRight);
            table.SetTextString(5, 2, "middle centre wraps");
            table.SetAlignment(5, 2, DxfCellAlignment.MiddleCenter);
            CadDocument document = CadDocument.From(db);
            document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null);

            // The last cell typed, as the editor writes it: the table is laid out anew.
            Assert.True(CadTableEditor.SetText(document, table.Handle, 2, 1, "HEA300 S355 with end plates both sides").Succeeded);

            // Each row the actual height of its tallest text and 1.5 above and below: the title's five lines of 6, the
            // header's three of 4.5 under Section and grade; a row of one-line words keeps its 9.
            table = (DxfTable)document.FindEntity(table.Handle);
            double[] autocad = { 39.28571, 24.21429, 45.42857, 30.0, 38.14286, 60.42857 };

            for (int r = 0; r < autocad.Length; r++)
            {
                Assert.Equal(autocad[r], table.RowHeight(r), Places);
            }

            // The words stand where AutoCAD 2021 put them in the rows grown: centred in the title, 1.5 under the top of a
            // row of data, centred in the last row and 1.5 above its bottom.
            DxfMText title = Text(table, "Bill of steel members for the north frame of the hall");
            Assert.Equal(45.0, title.Location.X, Places);
            Assert.Equal(-19.64286, title.Location.Y, Places);
            Assert.Equal(-65.0, Text(table, "B1").Location.Y, Places);
            Assert.Equal(-110.42857, Text(table, "IPE200").Location.Y, Places);
            Assert.Equal(-207.28571, Text(table, "x").Location.Y, Places);
            Assert.Equal(1.5, Text(table, "x").Location.X, Places);
            DxfMText bottom = Text(table, "bottom right words that wrap twice at least here");
            Assert.Equal(58.5, bottom.Location.X, Places);
            Assert.Equal(-236.0, bottom.Location.Y, Places);
            Assert.Equal(-207.28571, Text(table, "middle centre wraps").Location.Y, Places);
        }

        [Fact]
        public void ARowHigherThanItsWordsNeed_KeepsItsHeight_AndShorterWordsShrinkNoRow()
        {
            DxfDatabase db = Drawing();
            DxfTable table = Table(db, 4, new[] { 20.0, 20, 20, 20 });
            table.SetTextString(0, 0, "Short title");
            table.SetTextString(1, 1, "Section and grade");
            table.SetTextString(2, 1, "HEA300 S355 with end plates both sides");
            CadDocument document = CadDocument.From(db);
            document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null);
            Assert.True(CadTableEditor.SetText(document, table.Handle, 3, 1, "one two three four five six seven eight nine ten eleven twelve").Succeeded);

            table = (DxfTable)document.FindEntity(table.Handle);
            double[] autocad = { 20.0, 24.21429, 45.42857, 68.14286 };

            for (int r = 0; r < autocad.Length; r++)
            {
                Assert.Equal(autocad[r], table.RowHeight(r), Places);
            }

            // The long words made short again: the row keeps its 45.42857, as AutoCAD 2021 kept it.
            Assert.True(CadTableEditor.SetText(document, table.Handle, 2, 1, "a").Succeeded);
            table = (DxfTable)document.FindEntity(table.Handle);
            Assert.Equal(45.42857, table.RowHeight(2), Places);
        }

        [Theory]
        [InlineData(27.0, 1)]
        [InlineData(26.75, 2)]
        public void AWordWhoseInkEndsOnTheWidth_StaysOnItsLine_AsInAutoCad(double width, int lines)
        {
            // AutoCAD 2021 set "with end" in ghsimplex 4.5 high on one line from a width of 27, its ActualWidth 27.
            DxfDatabase db = Drawing();
            db.ModelSpace.AppendEntity(new DxfMText { Contents = "with end", TextHeight = 4.5, Width = width, Location = new GeoPoint3(0, 0, 0) });
            CadDocument document = CadDocument.From(db);
            document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null);

            DisplayText2 shown = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();
            Assert.Equal(lines, shown.Parts.OfType<DisplayText2>().Count());
        }
    }
}
