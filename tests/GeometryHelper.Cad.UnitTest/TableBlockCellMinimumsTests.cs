using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The least width of a column of block cells, the 4/3 of a text height a row of a block cell in the first column stands
    /// at, and an attribute definition counted in a block's extents, as AutoCAD 2021's console gave them (a table of a side
    /// database, margins 1.5, text of 6 for a title and 4.5): <c>MinimumColumnWidth</c> of a column with a block 30 by 10
    /// is 33, at scale 2 63, turned 45 degrees 31.28427, and 9 with the block fitted to its cell, as it is for a column of
    /// words; a block merged across two columns asks the column it starts in alone.
    /// </summary>
    public class TableBlockCellMinimumsTests : IDisposable
    {
        private const int Places = 5;
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>How the drawing finds the font its attribute definitions are measured in.</summary>
        private enum Fonts
        {
            None,
            OfTheDrawing,
            OfTheDocument,
        }

        private DxfDatabase Drawing(
            double width,
            double height,
            Action<DxfDatabase, DxfBlockTableRecord> block,
            Fonts fonts
        )
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];

            foreach (DxfCircle circle in bolt.OfType<DxfCircle>().ToList())
            {
                circle.Erase();
            }

            var corners = new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(width, 0, 0),
                new GeoPoint3(width, height, 0),
                new GeoPoint3(0, height, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                bolt.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            block(db, bolt);

            if (fonts != Fonts.None)
            {
                _folder.Write("test.shx", ShxFiles.Unifont());
                db.TextStyleTable["Standard"].FileName = "test.shx";
            }

            return db;
        }

        private CadDocument Document(DxfDatabase db, Fonts fonts)
        {
            CadDocument document = CadDocument.From(db);

            if (fonts == Fonts.OfTheDrawing)
            {
                document.Database.FontSearchPath = _folder.FullPath;
            }

            document.Fonts =
                fonts == Fonts.OfTheDocument
                    ? new CadFonts(new[] { _folder.FullPath }, null, null)
                    : new CadFonts(null, CadFonts.BuiltInName, null);
            return document;
        }

        /// <summary>AutoCAD's own table drawing with its block BOLT a rectangle, a table of the document that is laid out as the editor lays a table out.</summary>
        private DxfTable Table(
            double width,
            double height,
            Action<DxfTable> change,
            Action<DxfDatabase, DxfBlockTableRecord> block = null,
            Fonts fonts = Fonts.None,
            bool redraw = false
        )
        {
            DxfDatabase db = Drawing(width, height, block ?? ((d, b) => { }), fonts);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().First();
            change(table);
            CadDocument document = Document(db, fonts);
            table = (DxfTable)document.FindEntity(table.Handle);

            if (redraw)
            {
                CadTableEditor.Redraw(table, document);
            }

            return table;
        }

        [Theory]
        [InlineData(30.0, 10.0, 1.0, 33.0)]
        [InlineData(30.0, 10.0, 2.0, 63.0)]
        [InlineData(100.0, 5.0, 1.0, 103.0)]
        [InlineData(2.0, 2.0, 1.0, 9.0)]
        [InlineData(6.0, 40.0, 1.0, 9.0)]
        public void TheLeastWidthOfAColumn_HoldsTheBlockOfABlockCell_AndTheMargins(
            double width,
            double height,
            double scale,
            double least
        )
        {
            // The block asks its width at its scale and two margins of 1.5; a narrower one leaves the 9 of two margins and the
            // title's text of 6, where the column's words are the tallest.
            DxfTable table = Table(width, height, t => t.SetBlockScale(2, 1, scale));

            Assert.Equal(least, CadTableEditor.MinimumColumnWidth(table, 1), Places);
            Assert.Equal(9.0, CadTableEditor.MinimumColumnWidth(table, 0), Places);
        }

        [Fact]
        public void ATurnedBlock_AsksTheBoxItsTurnedExtentsFill()
        {
            DxfTable table = Table(30, 10, t => t.SetBlockRotation(2, 1, Math.PI / 4.0));

            // (30 + 10) times the root of a half, 28.28427, and the margins.
            Assert.Equal(31.28427, CadTableEditor.MinimumColumnWidth(table, 1), Places);

            table.SetBlockRotation(2, 1, Math.PI / 2.0);

            Assert.Equal(13.0, CadTableEditor.MinimumColumnWidth(table, 1), Places);
        }

        [Fact]
        public void ABlockFittedToItsCell_AsksNothing_AndABlockAcrossTwoColumns_AsksTheColumnItStartsInAlone()
        {
            DxfTable fitted = Table(30, 10, t => t.SetAutoScale(2, 1, true));
            Assert.Equal(9.0, CadTableEditor.MinimumColumnWidth(fitted, 1), Places);

            DxfTable merged = Table(30, 10, t =>
            {
                t.InsertColumns(2, 30.0, 1);
                t.MergeCells(new DxfCellRange(2, 1, 2, 2));
            });
            Assert.Equal(33.0, CadTableEditor.MinimumColumnWidth(merged, 1), Places);
            Assert.Equal(9.0, CadTableEditor.MinimumColumnWidth(merged, 2), Places);
        }

        [Fact]
        public void AWidthSetBelowTheBlocksLeast_IsHeldAtIt()
        {
            DxfDatabase db = Drawing(30, 10, (d, b) => { }, Fonts.None);
            CadDocument document = Document(db, Fonts.None);
            string handle = db.ModelSpace.OfType<DxfTable>().First().Handle;

            Assert.True(CadTableEditor.SetColumnWidths(document, handle, 1, new[] { 2.0 }).Succeeded);
            Assert.Equal(33.0, ((DxfTable)document.FindEntity(handle)).ColumnWidth(1), Places);
        }

        [Theory]
        [InlineData(false, 9.0)]
        [InlineData(true, 9.0)]
        public void ABlockCellInTheFirstColumn_StillStandsItsRowAtOneLineOfTheTextHeight(bool fitted, double row)
        {
            // AutoCAD 2021, rows of 3 and a block 2 by 2 in the first column, fitted or not: the row is laid out by the one
            // line, 4.5 times 4/3 and two margins, 9, where the block and its margins ask 5 (or nothing).
            DxfTable table = Table(
                2,
                2,
                t =>
                {
                    t.DeleteColumns(0, 1);
                    t.SetRowHeight(2, 3.0);
                    t.SetAutoScale(2, 0, fitted);
                },
                redraw: true
            );

            Assert.Equal(DxfTableCellType.BlockCell, table.CellType(2, 0));
            Assert.Equal(row, table.RowHeight(2), Places);
        }

        [Fact]
        public void ABlockCellInTheFirstColumnTallerThanOneLine_AsksItsHeightAndTheMargins()
        {
            DxfTable table = Table(
                2,
                10,
                t =>
                {
                    t.DeleteColumns(0, 1);
                    t.SetRowHeight(2, 3.0);
                },
                redraw: true
            );

            Assert.Equal(13.0, table.RowHeight(2), Places);
        }

        /// <summary>
        /// BOLT's own attribute definition (the cell gives it a value), made to stand at (22, 0), 20 high, with the default
        /// words given, beside a rectangle 20 by 4: of the unifont, an O is 12 wide and 12 high and moves the pen 18, so that
        /// "OO" is 22 + 18 + 12 = 52 wide to the right of the block's base point and 12 high.
        /// </summary>
        private static DxfAttributeDefinition Own(
            DxfBlockTableRecord block,
            string words,
            Action<DxfAttributeDefinition> set = null
        )
        {
            DxfAttributeDefinition definition = block.OfType<DxfAttributeDefinition>().Single();
            definition.Position = new GeoPoint3(22, 0, 0);
            definition.Height = 20.0;
            definition.TextString = words;
            set?.Invoke(definition);
            return definition;
        }

        private static void GiveValue(DxfTable table, string value) =>
            table.SetBlockAttributeValue(
                2,
                1,
                table.GetBlockTableRecord(2, 1).OfType<DxfAttributeDefinition>().Single(),
                value
            );

        [Theory]
        [InlineData("OO", 55.0, 15.0)]
        [InlineData("O", 37.0, 15.0)]
        [InlineData("", 23.0, 9.0)]
        public void AnAttributeDefinition_IsCountedInTheBlocksExtents_ByTheWordsTheCellGivesIt(
            string value,
            double least,
            double row
        )
        {
            // AutoCAD 2021 (a line from (0, 0) to (20, 0) and an attribute definition at x 22): the box of the words the cell
            // gives the attribute counts, to the right and in height, and words that are empty count nothing (23, the line and
            // the margins; the row at the 9 of its one line).
            DxfTable table = Table(
                20,
                4,
                t => GiveValue(t, value),
                (db, block) => Own(block, "OO"),
                Fonts.OfTheDrawing,
                true
            );

            Assert.Equal(least, CadTableEditor.MinimumColumnWidth(table, 1), Places);
            Assert.Equal(row, table.RowHeight(2), Places);
            Assert.Equal(Math.Max(30.0, least), table.ColumnWidth(1), Places);
        }

        [Fact]
        public void AnAttributeDefinitionTheCellGivesNoValue_IsCountedByItsOwnWords()
        {
            DxfTable table = Table(
                20,
                4,
                t => { },
                (db, block) =>
                {
                    Own(block, "OO", a => a.Invisible = true);
                    block.AppendEntity(
                        new DxfAttributeDefinition(new GeoPoint3(22, 0, 0), "OO", "TAG2", "prompt", null)
                        {
                            Height = 20.0,
                        }
                    );
                },
                Fonts.OfTheDrawing
            );

            Assert.Equal(55.0, CadTableEditor.MinimumColumnWidth(table, 1), Places);
        }

        [Fact]
        public void AnInvisibleAttributeDefinition_CountsNothing_AndAConstantOneCountsItsOwnWords()
        {
            DxfTable invisible = Table(
                20,
                4,
                t => GiveValue(t, "OO"),
                (db, block) => Own(block, "OO", a => a.Invisible = true),
                Fonts.OfTheDrawing
            );
            Assert.Equal(23.0, CadTableEditor.MinimumColumnWidth(invisible, 1), Places);

            // A constant one shows its own words whatever the cell gives.
            DxfTable constant = Table(
                20,
                4,
                t => GiveValue(t, "O"),
                (db, block) => Own(block, "OO", a => a.Constant = true),
                Fonts.OfTheDrawing
            );
            Assert.Equal(55.0, CadTableEditor.MinimumColumnWidth(constant, 1), Places);
        }

        [Fact]
        public void AnAttributeDefinitionInsideTheRectangle_ChangesNothing_AndOneWithNoFontIsLeftOut()
        {
            DxfTable inside = Table(60, 20, t => GiveValue(t, "OO"), (db, block) => Own(block, "OO", a =>
                    {
                        a.Position = new GeoPoint3(3, 3, 0);
                        a.Height = 5.0;
                    }), Fonts.OfTheDrawing);
            Assert.Equal(63.0, CadTableEditor.MinimumColumnWidth(inside, 1), Places);

            // The fonts are nowhere found: the words cannot be measured, and the definition is left out, as it was.
            DxfTable unfound = Table(20, 4, t => GiveValue(t, "OO"), (db, block) => Own(block, "OO"), Fonts.None);
            Assert.Equal(23.0, CadTableEditor.MinimumColumnWidth(unfound, 1), Places);
        }

        [Fact]
        public void TheFontsOfTheDocument_MeasureTheAttributeDefinitions_WhereTheDrawingNamesNone()
        {
            DxfDatabase db = Drawing(20, 4, (d, b) => Own(b, "OO"), Fonts.OfTheDocument);

            // The fonts of the document come before those of Windows, so that an arial.ttf of its own (an O 15 wide and 12 high
            // at 20 high) is the Arial the words are measured in, and no font of the machine changes the numbers: "OO" is 30
            // wide in Arial where the unifont's ink makes 30 as well (18 + 12).
            _folder.Write(
                "arial.ttf",
                TrueTypeFile.Font(
                    new List<TrueTypeFile.Letter>
                    {
                        new TrueTypeFile.Letter(0, 500, 50, 0, 450, 700),
                        new TrueTypeFile.Letter('A', 600, 0, 0, 600, 700),
                        new TrueTypeFile.Letter('O', 525, 0, 0, 525, 420),
                        new TrueTypeFile.Letter('V', 600, 0, 0, 600, 700),
                    },
                    "Arial"
                )
            );
            GiveValue(db.ModelSpace.OfType<DxfTable>().First(), "OO");
            CadDocument document = Document(db, Fonts.OfTheDocument);
            string handle = db.ModelSpace.OfType<DxfTable>().First().Handle;
            Assert.Equal(string.Empty, document.Database.FontSearchPath);

            Assert.True(CadTableEditor.SetColumnWidths(document, handle, 1, new[] { 2.0 }).Succeeded);
            Assert.Equal(55.0, ((DxfTable)document.FindEntity(handle)).ColumnWidth(1), Places);
            Assert.Equal(string.Empty, document.Database.FontSearchPath);

            Assert.True(CadTableEditor.SetRowHeights(document, handle, 2, new[] { 3.0 }).Succeeded);
            Assert.Equal(15.0, ((DxfTable)document.FindEntity(handle)).RowHeight(2), Places);
            Assert.Equal(string.Empty, document.Database.FontSearchPath);
        }
    }
}
