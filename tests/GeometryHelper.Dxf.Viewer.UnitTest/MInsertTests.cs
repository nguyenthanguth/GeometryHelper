using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MINSERT: the questions of AutoCAD 2021's console, the entity it makes (3 rows and 4 columns of the block BOX at (100, 100),
    /// 20 and 30 apart), its undo, its round trip through DXF and DWG, and EXPLODE's "1 was minserted.".
    /// </summary>
    public class MInsertTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>MINSERT of BOX at (100, 100), scale 1, no turn: answers up to the question of the rows.</summary>
        private static CadCommandSession ToTheRows(CadDocument document)
        {
            CadCommandSession session = Start(document, "MINSERT", "BOX", "100,100");

            if (session.PromptText.StartsWith("Enter X scale factor", StringComparison.Ordinal))
            {
                Run(session, "1", Enter);
            }
            else
            {
                Run(session, "1");
            }

            Assert.Equal("Specify rotation angle <0>:", session.PromptText);
            return Run(session, "0");
        }

        [Fact]
        public void MinsertAsksTheBlockThePointTheScalesTheRotationTheRowsAndTheColumns_AsAutoCadAsksThem()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "MINSERT");
            Assert.Equal("Enter block name or [?]:", session.PromptText);

            Run(session, "BOX");
            Assert.StartsWith(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate]",
                session.PromptText,
                StringComparison.Ordinal
            );
            session = ToTheRows(Plan());
            Assert.Equal("Enter number of rows (---) <1>:", session.PromptText);
            Run(session, "3");
            Assert.Equal("Enter number of columns (|||) <1>:", session.PromptText);
            Run(session, "4");
            Assert.Equal("Enter distance between rows or specify unit cell (---):", session.PromptText);
            Run(session, "20");
            Assert.Equal("Specify distance between columns (|||):", session.PromptText);
        }

        [Fact]
        public void ThreeRowsOfFourColumns_AreOneMinsertEntity_WithTheDistancesGiven()
        {
            CadDocument document = Plan();
            int before = All<DxfBlockReference>(document.ModelSpace).Count;
            CadCommandSession session = ToTheRows(document);
            Run(session, "3", "4", "20", "30");

            Assert.False(session.IsRunning);
            Assert.Equal(before + 1, All<DxfBlockReference>(document.ModelSpace).Count);
            DxfMInsertBlock array = All<DxfBlockReference>(document.ModelSpace).OfType<DxfMInsertBlock>().Single();
            Assert.Equal("BOX", array.BlockTableRecord.Name);
            Assert.Equal(3, array.Rows);
            Assert.Equal(4, array.Columns);
            Assert.Equal(20.0, array.RowSpacing);
            Assert.Equal(30.0, array.ColumnSpacing);
            Near(new GeoPoint3(100, 100, 0), array.Position);
            Assert.Equal(0.0, array.Rotation, 12);
            AssertReopens(document);

            // The array reads back from its text as an array, and one undo takes it away.
            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            DxfMInsertBlock read = again.ModelSpace.OfType<DxfMInsertBlock>().Single();
            Assert.Equal(12, read.Rows * read.Columns);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace).OfType<DxfMInsertBlock>());
        }

        [Fact]
        public void AUnitCellGivenByTwoCorners_GivesBothDistancesAtOnce_AndANegativeOneLaysTheArrayTheOtherWay()
        {
            CadDocument document = Plan();
            CadCommandSession session = ToTheRows(document);
            Run(session, "2", "3");
            Assert.Equal("Enter distance between rows or specify unit cell (---):", session.PromptText);
            Run(session, "10,10");
            Assert.StartsWith("Specify other corner", session.PromptText, StringComparison.Ordinal);
            Run(session, "-40,25");

            DxfMInsertBlock array = All<DxfBlockReference>(document.ModelSpace).OfType<DxfMInsertBlock>().Single();
            Assert.Equal(15.0, array.RowSpacing);
            Assert.Equal(-50.0, array.ColumnSpacing);
        }

        [Fact]
        public void WithOneRowOrOneColumn_ItsDistanceIsNotAsked()
        {
            CadDocument document = Plan();
            CadCommandSession session = ToTheRows(document);
            Run(session, "1", "5");
            Assert.Equal("Specify distance between columns (|||):", session.PromptText);
            Run(session, "12");

            DxfMInsertBlock row = All<DxfBlockReference>(document.ModelSpace).OfType<DxfMInsertBlock>().Single();
            Assert.Equal(1, row.Rows);
            Assert.Equal(5, row.Columns);
            Assert.Equal(12.0, row.ColumnSpacing);
            Assert.Equal(0.0, row.RowSpacing);
        }

        [Fact]
        public void ACountBelowOneOrPastTheShortRange_IsRefused_AndAskedAgain()
        {
            CadDocument document = Plan();
            CadCommandSession session = ToTheRows(document);
            Run(session, "0");
            Assert.Equal("Requires an integer between 1 and 32767.", session.History.Last());
            Assert.Equal("Enter number of rows (---) <1>:", session.PromptText);
            Run(session, "40000");
            Assert.Equal("Requires an integer between 1 and 32767.", session.History.Last());
            Run(session, Enter);
            Assert.Equal("Enter number of columns (|||) <1>:", session.PromptText);
        }

        [Fact]
        public void ABlockOfNoSuchName_IsNotFound_AndABlockWithAttributesIsNotPlaced()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "MINSERT", "NOPE");
            Assert.Equal("Block \"NOPE\" not found.", session.History.Last());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ExplodeOfAMinsert_SaysItWasMinserted_AsAutoCadSaysIt_AndLeavesItBe()
        {
            CadDocument document = Plan();
            CadCommandSession session = ToTheRows(document);
            Run(session, "2", "2", "20", "30");
            DxfMInsertBlock array = All<DxfBlockReference>(document.ModelSpace).OfType<DxfMInsertBlock>().Single();
            int lines = All<DxfLine>(document.ModelSpace).Count;

            Run(session, "EXPLODE", new[] { array.Handle }, Enter);
            Assert.Equal("1 was minserted.", session.History.Last());
            Assert.Single(All<DxfBlockReference>(document.ModelSpace).OfType<DxfMInsertBlock>());
            Assert.Equal(lines, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void AMinsertIsDrawnAsEveryCellOfTheArray_AndPickedByAPointInAnyCell()
        {
            CadDocument document = Plan();
            CadCommandSession session = ToTheRows(document);
            Run(session, "2", "3", "40", "50");
            DxfMInsertBlock array = All<DxfBlockReference>(document.ModelSpace).OfType<DxfMInsertBlock>().Single();

            // The block BOX is a square of 10; cells stand 50 apart in X and 40 in Y from (100, 100).
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            Assert.True(list.Bounds.MaxX >= 100 + 2 * 50 + 10 - 1E-9);
            Assert.True(list.Bounds.MaxY >= 100 + 40 + 10 - 1E-9);
            Assert.Contains(array.Handle, document.GetPickIndex(document.ModelSpace).Pick(205, 140, 1.0));
        }

        [Fact]
        public void AMinsertRoundTripsThroughDwg_WithItsRowsColumnsAndDistances()
        {
            var database = new DxfDatabase("AC1027");
            var box = new DxfBlockTableRecord { Name = "BOX" };
            database.BlockTable.Add(box);
            box.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            box.AppendEntity(new DxfLine(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0)));
            CadDocument document = CadDocument.From(database, "box.dxf");
            CadCommandSession session = ToTheRows(document);
            Run(session, "3", "4", "20", "30");
            string path = Path.Combine(Path.GetTempPath(), "small3-" + Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                DwgWriter.Write(document.Database, path);
                DxfDatabase back = DwgReader.Open(path);
                DxfMInsertBlock array = back.ModelSpace.OfType<DxfMInsertBlock>().Single();
                Assert.Equal(3, array.Rows);
                Assert.Equal(4, array.Columns);
                Assert.Equal(20.0, array.RowSpacing, 9);
                Assert.Equal(30.0, array.ColumnSpacing, 9);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
