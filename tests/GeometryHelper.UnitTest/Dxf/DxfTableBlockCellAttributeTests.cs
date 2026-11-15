using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The attributes of a block cell's block in the picture of a table (<see cref="DxfTable.RecomputeTableBlock"/>), as AutoCAD
    /// 2021's console drew them in the table's anonymous block: the INSERT of the block is followed by an ATTRIB for each
    /// definition of the block that is not constant (an invisible one too), filled from the definition as the INSERT command
    /// fills one and placed by the INSERT's transformation, its words the value the cell gives the definition, else the
    /// definition's own (a value of no words is no words). A constant definition has no ATTRIB.
    /// </summary>
    public class DxfTableBlockCellAttributeTests
    {
        private const int Places = 5;

        /// <summary>AutoCAD's own table drawing, its block BOLT a rectangle 20 by 4 and its definition SIZE (words M12, 1.5 high) at (22, 0).</summary>
        private static DxfTable Drawing(
            out DxfAttributeDefinition definition,
            Action<DxfAttributeDefinition> set = null
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
                new GeoPoint3(20, 4, 0),
                new GeoPoint3(0, 4, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                bolt.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            definition = bolt.OfType<DxfAttributeDefinition>().Single();
            definition.Position = new GeoPoint3(22, 0, 0);
            definition.Height = 2.5;
            set?.Invoke(definition);
            return db.ModelSpace.OfType<DxfTable>().First();
        }

        private static DxfBlockReference Insert(DxfTable table)
        {
            table.RecomputeTableBlock();
            return table.BlockTableRecord.OfType<DxfBlockReference>().Single();
        }

        [Fact]
        public void ABlockCellWhoseBlockHoldsADefinition_IsDrawnWithAnAttributeOfTheCellsValue()
        {
            DxfTable table = Drawing(out DxfAttributeDefinition definition);
            table.SetBlockAttributeValue(2, 1, definition, "M20");

            DxfBlockReference insert = Insert(table);

            DxfAttributeReference attribute = Assert.Single(insert.AttributeCollection);
            Assert.Equal("SIZE", attribute.Tag);
            Assert.Equal("M20", attribute.TextString);
            Assert.False(attribute.Invisible);
            Assert.Equal(2.5, attribute.Height, Places);

            // The definition's place, 22 along the block's x, taken by the INSERT's transformation.
            GeoPoint3 placed = insert.BlockTransform.Transform(definition.Position);
            Assert.Equal(placed.X, attribute.Position.X, Places);
            Assert.Equal(placed.Y, attribute.Position.Y, Places);
        }

        [Fact]
        public void AnAttributeOfABlockCellTheCellGivesNoValue_HasTheDefinitionsWords()
        {
            DxfTable table = Drawing(out _);

            DxfAttributeReference attribute = Assert.Single(Insert(table).AttributeCollection);

            Assert.Equal("M12", attribute.TextString);
        }

        [Fact]
        public void AValueOfNoWords_IsAnAttributeOfNoWords()
        {
            DxfTable table = Drawing(out DxfAttributeDefinition definition);
            table.SetBlockAttributeValue(2, 1, definition, string.Empty);

            DxfAttributeReference attribute = Assert.Single(Insert(table).AttributeCollection);

            Assert.Equal(string.Empty, attribute.TextString);
        }

        [Fact]
        public void ABlockCellScaledAndTurned_HasAnAttributeScaledAndTurnedWithIt()
        {
            // As AutoCAD drew one at scale 2 and 30 degrees: the definition 2.5 high is 5 high and turned with the block.
            DxfTable table = Drawing(out DxfAttributeDefinition definition);
            table.SetBlockScale(2, 1, 2.0);
            table.SetBlockRotation(2, 1, Math.PI / 6.0);

            DxfBlockReference insert = Insert(table);
            DxfAttributeReference attribute = Assert.Single(insert.AttributeCollection);

            Assert.Equal(5.0, attribute.Height, Places);
            Assert.Equal(Math.PI / 6.0, attribute.Rotation, Places);
            GeoPoint3 placed = insert.BlockTransform.Transform(definition.Position);
            Assert.Equal(placed.X, attribute.Position.X, Places);
            Assert.Equal(placed.Y, attribute.Position.Y, Places);
        }

        [Fact]
        public void AConstantDefinition_HasNoAttribute_AndAnInvisibleOne_HasAnInvisibleAttribute()
        {
            DxfTable constant = Drawing(out _, d => d.Constant = true);
            Assert.Empty(Insert(constant).AttributeCollection);

            DxfTable invisible = Drawing(out _, d => d.Invisible = true);
            DxfAttributeReference attribute = Assert.Single(Insert(invisible).AttributeCollection);
            Assert.True(attribute.Invisible);
        }

        [Fact]
        public void ATableDrawnTwice_HasTheAttributesOnceAndTheDrawingReadsBackTheSame()
        {
            DxfTable table = Drawing(out DxfAttributeDefinition definition);
            table.SetBlockAttributeValue(2, 1, definition, "M20");
            table.RecomputeTableBlock();
            DxfBlockReference insert = Insert(table);

            Assert.Single(insert.AttributeCollection);

            string text = table.Item.File.Database.ToText();
            DxfDatabase read = DxfDatabase.Parse(text);
            DxfTable again = read.ModelSpace.OfType<DxfTable>().First();
            DxfAttributeReference attribute = Assert.Single(
                again.BlockTableRecord.OfType<DxfBlockReference>().Single().AttributeCollection
            );

            Assert.Equal("M20", attribute.TextString);
            Assert.Equal(text, read.ToText());
        }

        [Fact]
        public void ABlockWithTwoDefinitions_HasTheirAttributesInTheOrderOfTheBlock()
        {
            DxfTable table = Drawing(out _);
            DxfBlockTableRecord bolt = table.Item.File.Database.BlockTable["BOLT"];
            bolt.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(22, 5, 0), "second", "LEN", "length", null) { Height = 2.5 }
            );

            DxfAttributeReference[] attributes = Insert(table).AttributeCollection.ToArray();

            Assert.Equal(new[] { "SIZE", "LEN" }, attributes.Select(a => a.Tag).ToArray());
            Assert.Equal("second", attributes[1].TextString);
        }
    }
}
