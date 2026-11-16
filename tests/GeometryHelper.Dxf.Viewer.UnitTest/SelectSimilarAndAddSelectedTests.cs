using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// SELECTSIMILAR and ADDSELECTED as AutoCAD 2021's console runs them (probed with lines and circles): "Select objects or
    /// [SEttings]:", SELECTSIMILARMODE 130 by default (layer and name), 0 the kind alone, and a colour compared as the object
    /// holds it, BYLAYER apart from the layer's own colour; ADDSELECTED draws a new object on the layer of the one picked.
    /// </summary>
    public class SelectSimilarAndAddSelectedTests
    {
        private static string[] Selected(CadDocument document) => document.Selection.Handles.OrderBy(h => h).ToArray();

        /// <summary>A blank drawing with lines: L1 on layer B (BYLAYER), L2 on B in red, L3 on C; a circle K1 on B and K2 on C.</summary>
        private static CadDocument Lines(
            out DxfLine l1,
            out DxfLine l2,
            out DxfLine l3,
            out DxfCircle k1,
            out DxfCircle k2
        )
        {
            CadDocument document = Blank();
            DxfDatabase database = document.Database;
            database.LayerTable.Add(
                new DxfLayerTableRecord { Name = "B", Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1) }
            );
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "C" });
            l1 = Line(database, "B", 0);
            l2 = Line(database, "B", 5);
            l2.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            l3 = Line(database, "C", 10);
            k1 = new DxfCircle(new GeoPoint3(30, 0, 0), new GeoVector3(0, 0, 1), 2) { Layer = "B" };
            database.ModelSpace.AppendEntity(k1);
            k2 = new DxfCircle(new GeoPoint3(40, 0, 0), new GeoVector3(0, 0, 1), 2) { Layer = "C" };
            database.ModelSpace.AppendEntity(k2);
            return document;
        }

        private static DxfLine Line(DxfDatabase database, string layer, double y)
        {
            var line = new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(10, y, 0)) { Layer = layer };
            database.ModelSpace.AppendEntity(line);
            return line;
        }

        [Fact]
        public void SelectSimilar_AsksInAutoCadsWords_AndTakesTheObjectsOfTheSameKindAndLayer()
        {
            CadDocument document = Lines(out DxfLine l1, out DxfLine l2, out _, out _, out _);
            CadCommandSession session = Session(document);

            Run(session, "SELECTSIMILAR");
            Assert.Equal("Select objects or [SEttings]:", session.PromptText);
            Run(session, new[] { l1.Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { l1.Handle, l2.Handle }.OrderBy(h => h).ToArray(), Selected(document));
        }

        [Fact]
        public void ModeZero_TakesEveryObjectOfTheKind_AndTheKindAloneDecidesWhatIsLikeACircle()
        {
            CadDocument document = Lines(out DxfLine l1, out DxfLine l2, out DxfLine l3, out _, out _);
            CadCommandSession session = Session(document);
            Run(session, "SELECTSIMILARMODE", "0", "SELECTSIMILAR", new[] { l1.Handle }, Enter);

            Assert.Equal(new[] { l1.Handle, l2.Handle, l3.Handle }.OrderBy(h => h).ToArray(), Selected(document));
        }

        [Fact]
        public void ModeColour_ComparesTheColourAsHeld_SoByLayerIsNotTheLayersOwnRed()
        {
            CadDocument document = Lines(out DxfLine l1, out DxfLine l2, out DxfLine l3, out _, out _);
            CadCommandSession session = Session(document);
            Run(session, "SELECTSIMILARMODE", "1", "SELECTSIMILAR", new[] { l1.Handle }, Enter);

            // L1 and L3 are both BYLAYER (one on a red layer, one on a white one); L2 is red by its own.
            Assert.Equal(new[] { l1.Handle, l3.Handle }.OrderBy(h => h).ToArray(), Selected(document));

            document.Selection.Clear();
            Run(session, "SELECTSIMILAR", new[] { l2.Handle }, Enter);
            Assert.Equal(new[] { l2.Handle }, Selected(document));
        }

        [Fact]
        public void ModeLayer_AloneSeparatesTheLayers_AndSeveralObjectsPickedTakeTheirKindsTogether()
        {
            CadDocument document = Lines(out DxfLine l1, out DxfLine l2, out _, out DxfCircle k1, out _);
            CadCommandSession session = Session(document);
            Run(session, "SELECTSIMILARMODE", "2", "SELECTSIMILAR", new[] { l1.Handle }, new[] { k1.Handle }, Enter);

            Assert.Equal(new[] { l1.Handle, l2.Handle, k1.Handle }.OrderBy(h => h).ToArray(), Selected(document));
        }

        [Fact]
        public void ObjectsOnAFrozenLayerOrHidden_AreNotTaken()
        {
            CadDocument document = Lines(out DxfLine l1, out DxfLine l2, out DxfLine l3, out _, out _);
            l2.Visible = false;
            document.Database.LayerTable["C"].IsFrozen = true;
            CadCommandSession session = Session(document);
            Run(session, "SELECTSIMILARMODE", "0", "SELECTSIMILAR", new[] { l1.Handle }, Enter);

            Assert.Equal(new[] { l1.Handle }, Selected(document));
            Assert.DoesNotContain(l3.Handle, Selected(document));
        }

        [Fact]
        public void TheObjectsSelectedBefore_AreTakenWithNoPrompt()
        {
            CadDocument document = Lines(out DxfLine l1, out DxfLine l2, out _, out _, out _);
            document.Selection.Set(new[] { l1.Handle });
            CadCommandSession session = Session(document);

            Run(session, "SELECTSIMILAR");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { l1.Handle, l2.Handle }.OrderBy(h => h).ToArray(), Selected(document));
        }

        [Fact]
        public void TheModeVariable_StartsAt130_AndInsertsAreToldApartByTheirBlock()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "SELECTSIMILARMODE");
            Assert.Equal("Enter new value for SELECTSIMILARMODE <130>:", session.PromptText);
            Run(session, Esc);

            // The plan's insert A7 is the only one of BOX: layer and name alike, it is taken alone.
            Run(session, "SELECTSIMILAR", new[] { "A7" }, Enter);
            Assert.Equal(new[] { "A7" }, Selected(document));
        }

        [Fact]
        public void Settings_WithNoWindow_SaysSoAndAsksAgain_AndWithOneTakesTheModeItSets()
        {
            CadDocument document = Lines(out DxfLine l1, out _, out DxfLine l3, out _, out _);
            CadCommandSession session = Session(document);
            Run(session, "SELECTSIMILAR", "SE");
            Assert.Equal("Select objects or [SEttings]:", session.PromptText);
            Assert.Contains("set SELECTSIMILARMODE", session.History.Last());

            session.UiRequested += (s, e) =>
            {
                var request = (CadSelectSimilarRequest)e.Payload;
                Assert.Equal(130, request.Mode);
                request.Mode = 0;
                e.Handled = true;
            };
            Run(session, "SE", new[] { l1.Handle }, Enter);

            Assert.Contains(l3.Handle, Selected(document));
            Assert.Equal(3, Selected(document).Length);
            Run(session, "SELECTSIMILARMODE");
            Assert.Equal("Enter new value for SELECTSIMILARMODE <0>:", session.PromptText);
        }

        [Fact]
        public void AddSelected_DrawsAnObjectOfTheKindPicked_OnItsLayerAndColour_AndGivesTheDrawingsBack()
        {
            CadDocument document = Lines(out _, out DxfLine l2, out _, out DxfCircle k1, out _);
            l2.Linetype = "Continuous";
            CadCommandSession session = Session(document);
            string layer = document.Database.Clayer;

            Run(session, "ADDSELECTED");
            Assert.Equal("Select object:", session.PromptText);
            Run(session, (object)new[] { k1.Handle });
            Assert.Equal("_.circle", session.History.Last());
            Assert.Equal("Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:", session.PromptText);
            Assert.Equal("B", document.Database.Clayer);
            Run(session, P(100, 0), P(105, 0));

            Assert.False(session.IsRunning);
            DxfCircle made = All<DxfCircle>(document.ModelSpace).Last();
            Assert.Equal("B", made.Layer);
            Near(new GeoPoint3(100, 0, 0), made.Center);
            Assert.Equal(layer, document.Database.Clayer);
            Assert.Equal(DxfColor.ByLayer, document.Database.Cecolor);
        }

        [Fact]
        public void AddSelected_TakesAColourOfTheObject_AndPutsTheDrawingsOwnBackWhenEscEndsIt()
        {
            CadDocument document = Lines(out _, out DxfLine l2, out _, out _, out _);
            CadCommandSession session = Session(document);

            Run(session, "ADDSELECTED", new[] { l2.Handle }, P(0, 50), P(10, 50), Esc);

            DxfLine made = All<DxfLine>(document.ModelSpace).Last();
            Assert.Equal(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), made.Color);
            Assert.Equal("B", made.Layer);
            Assert.Equal("0", document.Database.Clayer);
            Assert.Equal(DxfColor.ByLayer, document.Database.Cecolor);
        }

        [Fact]
        public void AddSelected_WithOneObjectSelectedBefore_TakesItWithNoPrompt_AndRefusesAKindWithNoCommand()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A3" });
            CadCommandSession session = Session(document);

            Run(session, "ADDSELECTED");
            Assert.Equal("Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:", session.PromptText);
            Run(session, Esc);

            document.Selection.Set(new[] { "A7" });
            Run(session, "ADDSELECTED");
            Assert.False(session.IsRunning);
            Assert.Equal("This kind of object cannot be added.", session.History.Last());
        }
    }
}
