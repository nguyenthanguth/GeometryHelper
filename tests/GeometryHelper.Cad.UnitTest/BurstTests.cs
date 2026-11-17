using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// BURST, the Express Tools command, as AutoCAD 2021's own BURST.LSP does it: the block reference is exploded, its
    /// attributes become TEXTs of the values they show, what comes out on layer 0 goes to the reference's layer and what is
    /// ByBlock takes its colour and linetype.
    /// </summary>
    public class BurstTests
    {
        /// <summary>
        /// The block GB of a line on layer 0, a circle ByBlock, the constant attribute NOTE of value CONST and the variable
        /// attribute TAG, inserted at (100, 100) on the layer WALLS in red, TAG given the value B-12 on the layer TEXTS.
        /// </summary>
        private static CadDocument Inserted(out string insert)
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "blocks.dxf");
            DxfDatabase db = document.Database;
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "TEXTS" });
            var block = new DxfBlockTableRecord { Name = "GB", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            block.AppendEntity(
                new DxfCircle(new GeoPoint3(5, 5, 0), new GeoVector3(0, 0, 1), 2.0) { Color = DxfColor.ByBlock }
            );
            block.AppendEntity(
                new DxfAttributeDefinition(new GeoPoint3(0, 10, 0), "CONST", "NOTE", "Note", null) { Constant = true }
            );
            var tagDefinition = new DxfAttributeDefinition(new GeoPoint3(0, 20, 0), "X", "TAG", "Tag", null);
            block.AppendEntity(tagDefinition);
            var reference = new DxfBlockReference(new GeoPoint3(100, 100, 0), block)
            {
                Layer = "WALLS",
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1),
            };
            db.ModelSpace.AppendEntity(reference);
            var tag = new DxfAttributeReference();
            tag.SetAttributeFromBlock(tagDefinition, reference.BlockTransform);
            tag.TextString = "B-12";
            tag.Layer = "TEXTS";
            reference.AttributeCollection.AppendAttribute(tag);
            document = CadDocument.From(DxfDatabase.Parse(db.ToText()), "blocks.dxf");
            insert = All<DxfBlockReference>(document.ModelSpace).Single().Handle;
            return document;
        }

        [Fact]
        public void ABlockReference_BurstsIntoItsPiecesAndTheTextsOfItsAttributes()
        {
            CadDocument document = Inserted(out string insert);
            CadCommandSession session = Session(document);

            Run(session, "BURST");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { insert }, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            Assert.Empty(All<DxfAttributeDefinition>(document.ModelSpace));

            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Near(new GeoPoint3(100, 100, 0), line.StartPoint, 1E-9);
            Near(new GeoPoint3(110, 100, 0), line.EndPoint, 1E-9);

            // What came out on layer 0 goes to the reference's layer; what was ByBlock takes its colour.
            Assert.Equal("WALLS", line.Layer);
            DxfCircle circle = Assert.Single(All<DxfCircle>(document.ModelSpace));
            Assert.Equal("WALLS", circle.Layer);
            Assert.Equal(1, circle.Color.ColorIndex);

            // The constant attribute and the variable one's value, each a TEXT; the attribute keeps the layer it is on.
            var texts = All<DxfDBText>(document.ModelSpace).Where(t => t.GetType() == typeof(DxfDBText)).ToList();
            Assert.Equal(2, texts.Count);
            DxfDBText note = texts.Single(t => t.TextString == "CONST");
            Assert.Equal("WALLS", note.Layer);
            Near(new GeoPoint3(100, 110, 0), note.Position, 1E-9);
            DxfDBText tag = texts.Single(t => t.TextString == "B-12");
            Assert.Equal("TEXTS", tag.Layer);
            Near(new GeoPoint3(100, 120, 0), tag.Position, 1E-9);
            AssertReopens(document);
        }

        [Fact]
        public void TheRunIsOneUndoStep()
        {
            CadDocument document = Inserted(out string insert);
            CadCommandSession session = Session(document);
            Run(session, "BURST", new[] { insert }, Enter);

            Assert.Equal("BURST", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Single(All<DxfBlockReference>(document.ModelSpace));
        }

        [Fact]
        public void ABlockThatMayNotBeExploded_IsPassedOver_AsExplodePassesItOver()
        {
            CadDocument document = Inserted(out string insert);
            document.Database.BlockTable["GB"].Explodable = false;
            CadCommandSession session = Session(document);

            Run(session, "BURST", new[] { insert }, Enter);

            Assert.Contains("1 was not able to be exploded.", session.History);
            Assert.Single(All<DxfBlockReference>(document.ModelSpace));
        }

        [Fact]
        public void OnlyBlockReferencesAreTaken_AndAnAttributeIsTextWhateverItsVisibility()
        {
            CadDocument document = Inserted(out string insert);
            DxfBlockReference reference = Entity<DxfBlockReference>(document, insert);
            reference.AttributeCollection[0].Invisible = true;
            document.ApplyEdit("draw", null, s =>
                s.Append(document.ModelSpace, new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0))
            );
            CadCommandSession session = Session(document);

            Run(session, "BURST", "ALL", Enter);

            Assert.Equal(2, All<DxfCircle>(document.ModelSpace).Count);
            Assert.Contains(All<DxfDBText>(document.ModelSpace), t => t.TextString == "B-12");
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
        }
    }
}
