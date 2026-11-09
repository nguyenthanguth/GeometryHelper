using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The layers of the viewports of a layout, as AutoCAD 2021's Layer manager, VPLAYER and LAYVPI set them: frozen in one
    /// viewport or in all, frozen in new viewports, and drawn in another colour in one viewport; each change one undo step.
    /// </summary>
    public class ViewportLayerTests
    {
        /// <summary>
        /// A line from (0, 0) to (2 000, 1 000) on the layer DIM and a rectangle around it on 0, in model space; on Layout1 two
        /// viewports 200 by 100 showing them, the first about (110, 60), the second about (110, 200).
        /// </summary>
        private static CadDocument TwoViewports(out CadCommandSession session, out string first, out string second)
        {
            CadDocument document = Blank();
            session = Session(document);
            Assert.True(CadLayerEditor.Add(document, "DIM").Succeeded);
            Assert.True(CadLayerEditor.SetCurrent(document, "DIM").Succeeded);
            Run(session, "LINE", P(0, 0), P(2000, 1000), Enter);
            Assert.True(CadLayerEditor.SetCurrent(document, "0").Succeeded);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            Run(session, "MV", P(10, 150), P(210, 250));
            var viewports = All<DxfViewport>(document.CurrentSpace).Where(v => v.Number != 1).ToList();
            first = viewports[0].Handle;
            second = viewports[1].Handle;
            return document;
        }

        private static DxfLayerTableRecord Dim(CadDocument document) => document.Database.LayerTable["DIM"];

        private static bool FrozenIn(CadDocument document, string viewport) => Entity<DxfViewport>(document, viewport).IsLayerFrozenInViewport(Dim(document));

        /// <summary>The viewport the layout's display list draws for a viewport entity.</summary>
        private static DisplayViewport2 Drawn(CadDocument document, string viewport)
            => document.GetDisplay(document.Spaces[1]).Items.OfType<DisplayViewport2>().Single(v => v.Handle == viewport);

        [Fact]
        public void AFreezeInTheViewportWorkedThrough_HidesTheLayerThereOnly_AndIsOneUndoStep()
        {
            CadDocument document = TwoViewports(out _, out string first, out string second);
            Assert.True(document.EnterViewport(first));
            Assert.Equal(first, CadViewportLayerEditor.TargetViewport(document).Handle);

            CadEditResult result = CadViewportLayerEditor.SetFrozen(document, new[] { CadViewportLayerEditor.TargetViewport(document).Handle }, new[] { "dim" }, true);

            Assert.True(result.Succeeded);
            Assert.True(FrozenIn(document, first));
            Assert.False(FrozenIn(document, second));
            Assert.Contains("DIM", Drawn(document, first).FrozenLayers);
            Assert.DoesNotContain("DIM", Drawn(document, second).FrozenLayers);
            Assert.Equal("LAYER", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.False(FrozenIn(document, first));
            Assert.Empty(Drawn(document, first).FrozenLayers);
        }

        [Fact]
        public void OnThePaper_TheViewportSelectedAlone_IsTheOneTheColumnsActOn()
        {
            CadDocument document = TwoViewports(out _, out string first, out _);
            Assert.Null(CadViewportLayerEditor.TargetViewport(document));

            document.Selection.Set(new[] { first });
            Assert.Equal(first, CadViewportLayerEditor.TargetViewport(document).Handle);

            document.CurrentSpace = document.ModelSpace;
            Assert.Null(CadViewportLayerEditor.TargetViewport(document));
        }

        [Fact]
        public void FreezeAndThawInAllViewports_ActOnEveryViewportOfEveryLayout()
        {
            CadDocument document = TwoViewports(out _, out string first, out string second);

            Assert.True(CadViewportLayerEditor.SetFrozenInAllViewports(document, new[] { "DIM" }, true).Succeeded);
            Assert.True(FrozenIn(document, first) && FrozenIn(document, second));
            Assert.Equal(2, document.NextUndo.Records.Count);

            Assert.True(CadViewportLayerEditor.SetFrozenInAllViewports(document, new[] { "DIM" }, false).Succeeded);
            Assert.False(FrozenIn(document, first) || FrozenIn(document, second));
        }

        [Fact]
        public void ALayerFrozenInNewViewports_IsFrozenInTheNextMview_NotInThoseThere()
        {
            CadDocument document = TwoViewports(out CadCommandSession session, out string first, out _);

            Assert.True(CadViewportLayerEditor.SetFrozenInNewViewports(document, "DIM", true).Succeeded);
            Assert.True(Dim(document).ViewportVisibilityDefault);
            Assert.False(FrozenIn(document, first));

            Run(session, "MV", P(250, 10), P(350, 110));
            string third = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            Assert.True(FrozenIn(document, third));
        }

        [Fact]
        public void ANewLayerVpFrozenInAllViewports_IsAddedFrozenInEach_OneUndoStep()
        {
            CadDocument document = TwoViewports(out _, out string first, out string second);

            Assert.True(CadViewportLayerEditor.AddFrozenInAllViewports(document, "TITLE").Succeeded);

            DxfLayerTableRecord title = document.Database.LayerTable["TITLE"];
            Assert.True(Entity<DxfViewport>(document, first).IsLayerFrozenInViewport(title));
            Assert.True(Entity<DxfViewport>(document, second).IsLayerFrozenInViewport(title));
            Assert.True(document.Undo());
            Assert.False(document.Database.LayerTable.Has("TITLE"));
            Assert.False(CadViewportLayerEditor.AddFrozenInAllViewports(document, "dim").Succeeded);
        }

        [Fact]
        public void AColourOverriddenInOneViewport_DrawsTheLayerInItThere_AndTheLayersOwnElsewhere()
        {
            CadDocument document = TwoViewports(out _, out string first, out string second);
            string line = All<DxfLine>(document.ModelSpace).Single().Handle;

            Assert.True(CadViewportLayerEditor.SetColor(document, first, "DIM", DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1)).Succeeded);
            Assert.True(CadViewportLayerEditor.SetLineWeight(document, first, "DIM", DxfLineWeight.LineWeight050).Succeeded);

            DxfLayerViewportProperties look = Dim(document).GetViewportOverrides(Entity<DxfViewport>(document, first));
            Assert.Equal(1, look.Color.ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight050, look.LineWeight);
            DisplayItem2 red = Drawn(document, first).Model.Items.Single(i => i.Handle == line);
            DisplayItem2 white = Drawn(document, second).Model.Items.Single(i => i.Handle == line);
            Assert.Equal(0xFF0000, red.Style.Rgb);
            Assert.Equal(0.5, red.Style.LineWeight);
            Assert.Equal(0xFFFFFF, white.Style.Rgb);
            Assert.Same(document.GetDisplay(document.ModelSpace), Drawn(document, second).Model);

            // Taken out in the first viewport only, then nothing is left to take out.
            Assert.True(CadViewportLayerEditor.RemoveOverrides(document, new[] { "DIM" }, first).Succeeded);
            Assert.False(Dim(document).HasOverrides);
            Assert.Equal(0xFFFFFF, Drawn(document, first).Model.Items.Single(i => i.Handle == line).Style.Rgb);
            Assert.True(document.Undo());
            Assert.True(Dim(document).HasViewportOverrides(Entity<DxfViewport>(document, first)));
        }

        [Fact]
        public void APlotStyleInADrawingPlottedByColour_AndAColourByLayer_AreRefused()
        {
            CadDocument document = TwoViewports(out _, out string first, out _);

            Assert.False(CadViewportLayerEditor.SetPlotStyle(document, first, "DIM", "Normal").Succeeded);
            Assert.False(CadViewportLayerEditor.SetColor(document, first, "DIM", DxfColor.ByLayer).Succeeded);
            Assert.False(CadViewportLayerEditor.SetLinetype(document, first, "DIM", "NOSUCH").Succeeded);
            Assert.False(Dim(document).HasOverrides);
        }

        [Fact]
        public void Vplayer_FreezesInAll_ThawsInTheCurrent_ResetsAndLists_AsOneUndoStep()
        {
            CadDocument document = TwoViewports(out CadCommandSession session, out string first, out string second);
            Assert.True(document.EnterViewport(first));

            Run(session, "VPLAYER", "F", "D*", "A", "T", "DIM", "C", "?", Enter);

            Assert.False(FrozenIn(document, first));
            Assert.True(FrozenIn(document, second));
            Assert.Contains("Layers currently frozen in viewport " + Entity<DxfViewport>(document, first).Number + ":", session.History);
            Assert.Equal("VPLAYER", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.False(FrozenIn(document, first) || FrozenIn(document, second));

            // Vpvisdflt Frozen, then Reset in all: each viewport takes the layer's default.
            Run(session, "VPLAYER", "V", "DIM", "F", "R", "DIM", "A", Enter);
            Assert.True(FrozenIn(document, first) && FrozenIn(document, second));

            Run(session, "VPLAYER", "N", "TITLE,NOTES", Enter);
            Assert.True(Entity<DxfViewport>(document, second).IsLayerFrozenInViewport(document.Database.LayerTable["NOTES"]));

            Run(session, "VPLAYER", "F", "NOSUCH", Enter);
            Assert.Contains("No matching layer names found for \"NOSUCH\".", session.History);
        }

        [Fact]
        public void Layvpi_FreezesThePickedLayerInEveryViewportButTheOneWorkedThrough()
        {
            CadDocument document = TwoViewports(out CadCommandSession session, out string first, out string second);
            Assert.True(document.EnterViewport(first));

            Run(session, "LAYVPI", P(1000, 500), Enter);

            Assert.False(FrozenIn(document, first));
            Assert.True(FrozenIn(document, second));
            Assert.Contains("Layer \"DIM\" has been frozen in all viewports but the current one.", session.History);

            document.LeaveViewport();
            Run(session, "LAYVPI");
            Assert.Equal("LAYVPI works in a viewport: enter one (MSPACE) first.", session.History.Last());
        }
    }
}
