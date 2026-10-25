using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The ACAD_TABLE of <see cref="DxfDimensionDrawings"/> copied into another drawing with the values its block cell gives
    /// the attributes of the block that comes with it, and scaled alike in every direction or mirrored, its sizes with it, as
    /// AutoCAD's SCALE and MIRROR carry a table; an INSERT of a block holding it exploded under such a placing keeps it.
    /// </summary>
    public class DxfTableCopyAndScaleTests
    {
        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ACopyForAnotherDrawing_KeepsTheValuesItsBlockCellGives_ThePointersTakenToTheCopiedAttributeDefinitions(string version)
        {
            DxfDatabase source = DxfDatabase.Parse(version == "R2000" ? DxfDimensionDrawings.R2000 : DxfDimensionDrawings.R2018);
            DxfDatabase other = DxfDatabase.Parse(version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018);
            Assert.False(other.BlockTable.Has("BOLT"));
            DxfEntity copy = Table(source).Clone();

            other.ModelSpace.AppendEntity(copy);

            // The block BOLT came with the table's picture, its ATTDEF SIZE a copy of its own: the cell's 331 names that copy.
            DxfAttributeDefinition size = other.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            Assert.NotEqual("4C", size.Handle);
            string record = Records(other.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.Contains("179\n1\n331\n" + size.Handle + "\n300\nM12\n", record, StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(other.ToText());
            DxfTable read = again.ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal("M12", read.GetBlockAttributeValue(2, 1, again.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));
        }

        [Fact]
        public void ACopyForADrawingWithABlockOfTheName_LosesTheValues_AndItsCountAgrees()
        {
            // The target has a BOLT of its own, which the cell then shows.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDatabase other = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var bolt = new DxfBlockTableRecord { Name = "BOLT" };
            other.BlockTable.Add(bolt);
            bolt.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 2.0));
            DxfEntity copy = Table(source).Clone();

            other.ModelSpace.AppendEntity(copy);

            string record = Records(other.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.Contains("\n179\n0\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n331\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n300\nM12\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void ATableScaledAlike_ScalesItsRowsColumnsTextHeightsAndBlockScales_AndItsPicture()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            DxfLine line = db.BlockTable["*T12"].OfType<DxfLine>().Single(l => l.Handle == "EA");

            table.TransformBy(GeoTransform3.Scaling(2.0));

            Assert.Equal(Bits(new GeoPoint3(600.0, -200.0, 0.0)), Bits(table.Position));
            Assert.Equal(Bits(new GeoVector3(1.0, 0.0, 0.0)), Bits(table.Direction));
            Assert.Equal(20.0, table.RowHeight(0));
            Assert.Equal(16.0, table.RowHeight(2));
            Assert.Equal(60.0, table.ColumnWidth(1));
            Assert.Equal(52.0, table.Height);
            Assert.Equal(7.0, table.TextHeight(1, 0));
            Assert.Equal(2.0, table.BlockScale(2, 1));
            Assert.Equal(Bits(new GeoPoint3(140.0, -20.0, 0.0)), Bits(line.EndPoint));
            DxfTable read = Table(DxfDatabase.Parse(db.ToText()));
            Assert.Equal(20.0, read.RowHeight(0));
            Assert.Equal("Parts", read.TextString(0, 0));
        }

        [Fact]
        public void ATableMirrored_StandsWhereTheMirrorPutsItsCells_ItsNormalTurnedRound()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            var mirror = GeoTransform3.Scaling(-1.0, 1.0, 1.0);
            var before = new List<GeoPoint3>();

            for (int row = 0; row < table.NumRows; row++)
            {
                for (int col = 0; col < table.NumColumns; col++)
                {
                    before.AddRange(table.GetCellExtents(row, col, false).Select(p => mirror.Transform(p)));
                }
            }

            table.TransformBy(mirror);

            Assert.Equal(Bits(new GeoVector3(0.0, 0.0, -1.0)), Bits(table.Normal));
            Assert.Equal(-1.0, table.Direction.X, 15);
            Assert.Equal(10.0, table.RowHeight(0));
            var after = new List<GeoPoint3>();

            for (int row = 0; row < table.NumRows; row++)
            {
                for (int col = 0; col < table.NumColumns; col++)
                {
                    after.AddRange(table.GetCellExtents(row, col, false));
                }
            }

            for (int i = 0; i < before.Count; i++)
            {
                Assert.True(before[i].DistanceTo(after[i]) <= 1E-9, "corner " + i + ": " + before[i] + " against " + after[i]);
            }
        }

        [Fact]
        public void ATableSheared_OrScaledUnlike_IsRefused_AndLeftAsItWas()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            Refused<ArgumentException>(() => table.TransformBy(GeoTransform3.Scaling(2.0, 1.0, 1.0)), "scaled alike in every direction");
            Refused<ArgumentException>(() => table.TransformBy(new GeoTransform3(new double[,] { { 1, 0.5, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } })), "scaled alike in every direction");
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void AnInsertOfABlockHoldingATable_ExplodedScaledAlikeOrMirrored_KeepsTheTable_AndScaledUnlike_LeavesItOut()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "TB" };
            db.BlockTable.Add(block);
            block.AppendEntity(Table(db).Clone());
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block) { ScaleFactors = new GeoVector3(2.0, 2.0, 2.0) };
            db.ModelSpace.AppendEntity(insert);

            DxfTable part = insert.Explode().OfType<DxfTable>().Single();

            Assert.Equal(20.0, part.RowHeight(0));
            Assert.Equal(Bits(new GeoPoint3(600.0, -200.0, 0.0)), Bits(part.Position));
            insert.ScaleFactors = new GeoVector3(-1.0, 1.0, 1.0);
            DxfTable mirrored = insert.Explode().OfType<DxfTable>().Single();
            Assert.Equal(-300.0, mirrored.Position.X, 12);
            Assert.Equal(-1.0, mirrored.Normal.Z, 15);
            insert.ScaleFactors = new GeoVector3(2.0, 1.0, 1.0);
            Assert.Empty(insert.Explode().OfType<DxfTable>());
        }

        private static void Refused<T>(Action action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }
    }
}
