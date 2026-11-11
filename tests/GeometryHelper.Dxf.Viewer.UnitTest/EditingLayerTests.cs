using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingLayerTests
    {
        private static CadDocument WithLayers()
        {
            DxfDatabase database = TestDrawings.DrawingWithLinetypes(
                TestDrawings.Linetype("DASHED", 10, -5),
                TestDrawings.Layer("Steel", 1)
                    + TestDrawings.Layer("Empty", 2)
                    + TestDrawings.Layer("Defpoints", 7)
                    + TestDrawings.Layer("Base|Grid", 3),
                null,
                TestDrawings.Line(0, 0, 100, 0, "Steel", handle: "A1")
            );
            return CadDocument.From(database, "layers.dxf");
        }

        [Fact]
        public void ALayerNothingUsesIsDeletedInOneStep_AndUndoBringsItBack()
        {
            CadDocument document = WithLayers();
            string start = document.Database.ToText();

            CadEditResult result = CadLayerEditor.Delete(document, "empty");

            Assert.True(result.Succeeded);
            Assert.False(document.Database.LayerTable.Has("Empty"));
            Assert.Equal(CadEditRecordKind.TableChanged, Assert.Single(result.Records).Kind);
            AssertReopens(document);
            document.Undo();
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void Layer0DefpointsTheCurrentLayerAnXrefsLayerAndALayerWithObjectsAreNotDeleted_EachForItsReason()
        {
            CadDocument document = WithLayers();
            CadLayerEditor.SetCurrent(document, "Empty");
            string before = document.Database.ToText();

            Assert.Equal(
                "Layer \"0\" cannot be deleted: layer 0 and Defpoints are kept.",
                CadLayerEditor.Delete(document, "0").Message
            );
            Assert.Equal(
                "Layer \"Defpoints\" cannot be deleted: layer 0 and Defpoints are kept.",
                CadLayerEditor.Delete(document, "Defpoints").Message
            );
            Assert.Equal(
                "Layer \"Empty\" cannot be deleted: it is the current layer.",
                CadLayerEditor.Delete(document, "Empty").Message
            );
            Assert.Equal(
                "Layer \"Base|Grid\" cannot be deleted: it is part of an external reference.",
                CadLayerEditor.Delete(document, "Base|Grid").Message
            );
            Assert.Equal(
                "Layer \"Steel\" cannot be deleted: objects are on it.",
                CadLayerEditor.Delete(document, "Steel").Message
            );
            Assert.True(CadLayerEditor.Delete(document, "Nope").Refused);
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void ALayerTakesALinetypeTheDrawingHas_AndOneItHasNotIsRefused()
        {
            CadDocument document = WithLayers();

            Assert.True(CadLayerEditor.SetLinetype(document, "Steel", "dashed").Succeeded);
            Assert.Equal("DASHED", document.Database.LayerTable["Steel"].LinetypeName);
            Assert.Equal("LAYER", document.NextUndo.Name);
            Assert.Equal(
                "The drawing has no linetype \"HIDDEN\": load it into the drawing first.",
                CadLayerEditor.SetLinetype(document, "Steel", "HIDDEN").Message
            );
            Assert.True(CadLayerEditor.SetLinetype(document, "Steel", "ByLayer").Refused);
        }

        [Fact]
        public void ALayerTakesALineweight_ButNotByLayerOrByBlock()
        {
            CadDocument document = WithLayers();

            Assert.True(CadLayerEditor.SetLineWeight(document, "Steel", DxfLineWeight.LineWeight050).Succeeded);
            Assert.Equal(DxfLineWeight.LineWeight050, document.Database.LayerTable["Steel"].LineWeight);
            Assert.Equal(0.5, Assert.Single(document.Layers, l => l.Name == "Steel").LineWeight, 9);
            Assert.True(CadLayerEditor.SetLineWeight(document, "Steel", DxfLineWeight.ByLayer).Refused);
            Assert.True(CadLayerEditor.SetLineWeight(document, "Steel", DxfLineWeight.ByBlock).Refused);
        }
    }
}
