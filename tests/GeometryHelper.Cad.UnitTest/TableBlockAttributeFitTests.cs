using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The two boxes AutoCAD 2021 lays a block cell out by when its block holds an attribute definition that is not constant
    /// (probed in its console, margins 1.5, one pass of its layout): the scale that fits a block to its cell comes from the
    /// ink of the definition's words in the font of its own style, while the least a column or row may be, and the box the
    /// block is aligned in the cell by, come from the same words measured in Arial, whatever the style's font. Here a
    /// style of gh.ttf (an A 6 wide at 7 high, inked from -0.1 to 6.1) and an arial.ttf of its own (an A 8 wide): the block, a
    /// rectangle 20 by 10, with the definition at (22, 0), reaches 28.1 across by its ink and 30 in Arial.
    /// </summary>
    public class TableBlockAttributeFitTests : IDisposable
    {
        private const int Places = 5;
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gh-t8-" + Guid.NewGuid().ToString("N"));

        public TableBlockAttributeFitTests()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(Path.Combine(_folder, "gh.ttf"), TrueTypeFile.Font(TrueTypeFile.Letters()));
        }

        public void Dispose() => Directory.Delete(_folder, true);

        private void WriteArial() =>
            File.WriteAllBytes(
                Path.Combine(_folder, "arial.ttf"),
                TrueTypeFile.Font(
                    new List<TrueTypeFile.Letter>
                    {
                        new TrueTypeFile.Letter(0, 500, 50, 0, 450, 700),
                        new TrueTypeFile.Letter(' ', 250),
                        new TrueTypeFile.Letter('A', 800, 0, 0, 800, 700),
                        new TrueTypeFile.Letter('V', 800, 0, 0, 800, 700),
                    },
                    "Arial"
                )
            );

        /// <summary>
        /// AutoCAD's own table drawing with BOLT a rectangle 20 by 10, its definition at (22, 0) holding an A 7 high, the cell
        /// (2, 1) 40 wide and 60 high at the middle, the document and the drawing finding fonts in the folder.
        /// </summary>
        private DxfTable Table(
            Action<DxfAttributeDefinition> definition,
            Action<DxfTable> change,
            out CadDocument document
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
                new GeoPoint3(20, 0, 0),
                new GeoPoint3(20, 10, 0),
                new GeoPoint3(0, 10, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                bolt.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            DxfAttributeDefinition own = bolt.OfType<DxfAttributeDefinition>().Single();
            own.Position = new GeoPoint3(22, 0, 0);
            own.Height = 7.0;
            own.TextString = "A";
            definition?.Invoke(own);
            db.TextStyleTable["Standard"].FileName = "gh.ttf";
            DxfTable table = db.ModelSpace.OfType<DxfTable>().First();
            table.SetBlockAttributeValue(2, 1, own, "A");
            table.SetAlignment(2, 1, DxfCellAlignment.MiddleCenter);
            table.SetColumnWidth(0, 40.0);
            table.SetColumnWidth(1, 40.0);
            table.SetRowHeight(2, 60.0);
            change?.Invoke(table);
            document = CadDocument.From(db);
            document.Database.FontSearchPath = _folder;
            document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null);
            return (DxfTable)document.FindEntity(table.Handle);
        }

        private static DxfBlockReference Insert(DxfTable table) =>
            table.BlockTableRecord.OfType<DxfBlockReference>().Single();

        [Fact]
        public void TheLeastWidthOfAColumn_CountsTheWordsOfADefinitionInArial_NotInTheFontOfItsStyle()
        {
            WriteArial();
            DxfTable table = Table(null, t => t.SetColumnWidth(1, 10.0), out _);

            // 22 + the A of arial.ttf, 8 wide, and the margins: 33, where gh.ttf's 6.1 would make 31.1.
            Assert.Equal(33.0, CadTableEditor.MinimumColumnWidth(table, 1), Places);
        }

        [Fact]
        public void WhereNoArialIsFound_TheWordsAreMeasuredInTheFontOfTheirStyle()
        {
            DxfTable table = Table(null, t => t.SetColumnWidth(1, 10.0), out _);

            Assert.Equal(31.1, CadTableEditor.MinimumColumnWidth(table, 1), Places);
        }

        [Fact]
        public void AConstantDefinition_IsCountedInTheFontOfItsStyle()
        {
            WriteArial();
            DxfTable table = Table(d => d.Constant = true, t => t.SetColumnWidth(1, 10.0), out _);

            Assert.Equal(31.1, CadTableEditor.MinimumColumnWidth(table, 1), Places);
        }

        [Fact]
        public void ABlockNotFitted_IsAlignedInItsCellByTheBoxOfItsWordsInArial()
        {
            // The column 40 wide and the block 30 across in Arial: centred, its insertion point at 40 + (40 - 30) / 2 = 45
            // (centred by its ink, 28.1, it would stand at 45.95). Its rows and columns as they were.
            WriteArial();
            DxfTable table = Table(null, null, out CadDocument document);

            CadTableEditor.Redraw(table, document);

            Assert.Equal(45.0, Insert(table).Position.X, Places);
            Assert.Equal(40.0, table.ColumnWidth(1), Places);
        }

        [Fact]
        public void ABlockFitted_IsFittedByItsInk_ItsColumnWidenedToHoldItsWordsInArialAtThatScale()
        {
            // One pass of AutoCAD's layout: the scale is 37 over the ink's 28.1, 1.31673, in the column's 40 less margins; the
            // column, which that scale gives 30 in Arial, 39.50178, is then widened to 42.50178, the block filling it from
            // its margin: its insertion point at 40 + 1.5.
            WriteArial();
            DxfTable table = Table(null, t => t.SetAutoScale(2, 1, true), out CadDocument document);

            CadTableEditor.Redraw(table, document);

            Assert.Equal(42.50178, table.ColumnWidth(1), Places);
            Assert.Equal(1.31673, Insert(table).ScaleFactors.X, Places);
            Assert.Equal(41.5, Insert(table).Position.X, Places);
        }

        [Fact]
        public void ATableDrawnAgain_KeepsItsColumn_AndTheScaleItWasFittedAt()
        {
            // AutoCAD stores the scale it fitted the block at (its 144); the table drawn again finds the block fitted to the
            // widened cell and does not widen it again, as a second pass of AutoCAD's own would (1.44061, 41.40525, and on).
            WriteArial();
            DxfTable table = Table(null, t => t.SetAutoScale(2, 1, true), out CadDocument document);
            CadTableEditor.Redraw(table, document);

            Assert.Equal(1.31673, table.BlockScale(2, 1), Places);

            CadTableEditor.Redraw(table, document);
            CadTableEditor.Redraw(table, document);

            Assert.Equal(42.50178, table.ColumnWidth(1), Places);
            Assert.Equal(1.31673, table.BlockScale(2, 1), Places);
            Assert.Equal(1.31673, Insert(table).ScaleFactors.X, Places);
        }

        [Fact]
        public void AColumnSetToAnotherWidth_IsFittedAnewByTheInkAndWidenedForArialAgain()
        {
            // The column made 60: 57 over the ink's 28.1 is 2.02847, and 30 of Arial at it, 60.8541 and the margins, 63.85409.
            WriteArial();
            DxfTable table = Table(null, t => t.SetAutoScale(2, 1, true), out CadDocument document);
            CadTableEditor.Redraw(table, document);

            table.SetColumnWidth(1, 60.0);
            CadTableEditor.Redraw(table, document);

            Assert.Equal(63.85409, table.ColumnWidth(1), Places);
            Assert.Equal(2.02847, Insert(table).ScaleFactors.X, Places);
        }

        [Fact]
        public void ABlockFitted_AsksNothingOfTheLeastItsColumnMayBe()
        {
            // AutoCAD's MinimumColumnWidth of a fitted block cell, a definition or none, is the margins and the words of the
            // other cells alone: 9, as a column of words is.
            WriteArial();
            DxfTable table = Table(null, t => t.SetAutoScale(2, 1, true), out _);

            Assert.Equal(9.0, CadTableEditor.MinimumColumnWidth(table, 1), Places);
        }

        [Fact]
        public void ATableDrawnWithNoLayout_FitsItsBlockByTheBoxOfTheInkAndArialTogether()
        {
            // RecomputeTableBlock alone keeps the column, 40: the box that fits it is the wider of the two, 30, so that the
            // block stands inside the cell, 37 over 30, 1.23333, as the layout leaves a column it has widened.
            WriteArial();
            DxfTable table = Table(null, t => t.SetAutoScale(2, 1, true), out _);

            table.RecomputeTableBlock();

            Assert.Equal(1.23333, Insert(table).ScaleFactors.X, Places);
            Assert.Equal(41.5, Insert(table).Position.X, Places);
            Assert.Equal(40.0, table.ColumnWidth(1), Places);
        }

        [Fact]
        public void TheAttributesOfAFittedBlock_StandAtItsScale()
        {
            WriteArial();
            DxfTable table = Table(null, t => t.SetAutoScale(2, 1, true), out CadDocument document);

            CadTableEditor.Redraw(table, document);

            DxfAttributeReference attribute = Assert.Single(Insert(table).AttributeCollection);
            Assert.Equal(7.0 * 37.0 / 28.1, attribute.Height, Places);
        }
    }
}
