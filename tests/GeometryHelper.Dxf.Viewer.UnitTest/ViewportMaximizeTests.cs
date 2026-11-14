using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// VPMAX and VPMIN as AutoCAD 2021 runs them (probed in its console): "Select a viewport to maximize:", the viewport given
    /// the canvas at the scale it had with the others of the layout off and model space worked through it; VPMIN putting the
    /// frame and the view back as they were and the paper worked on; the messages of a second VPMAX and of a VPMIN with none.
    /// </summary>
    public sealed class ViewportMaximizeTests
    {
        /// <summary>
        /// A rectangle from (0, 0) to (2 000, 1 000) in model space, and on Layout1 two viewports, 200 by 100 about (110, 60) and
        /// 60 by 60 about (300, 60), each showing it at 1:10 about its middle; a canvas of 1 000 by 600 pixels over the sheet.
        /// </summary>
        private static CadDocument WithViewports(out CadCommandSession session, out string first, out string second)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            Run(session, "MV", P(270, 30), P(330, 90));
            first = All<DxfViewport>(document.CurrentSpace)[All<DxfViewport>(document.CurrentSpace).Count - 2].Handle;
            second = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            var view = new CadView(1000, 600);
            view.ZoomTo(new DisplayBox(0, 0, 400, 240), 0.0);
            session.View = view;
            return document;
        }

        private static DxfViewport Viewport(CadDocument document, string handle) =>
            Entity<DxfViewport>(document, handle);

        [Fact]
        public void TheViewportPicked_TakesTheCanvasAtItsScale_TheOthersGoOff_AndModelSpaceIsWorkedThroughIt()
        {
            CadDocument document = WithViewports(out CadCommandSession session, out string first, out string second);
            DxfViewport before = Viewport(document, first);
            double scale = before.Height / before.ViewHeight;
            GeoPoint2 modelAtOldMiddle = CadViewports.ToModel(before, new GeoPoint2(110, 60));
            DisplayBox shown = session.View.VisibleBox;
            double margin = 10.0 / session.View.PixelsPerUnit;

            Run(session, "VPMAX");
            Assert.Equal("Select a viewport to maximize:", session.PromptText);
            Run(session, (object)new[] { first });

            Assert.False(session.IsRunning);
            DxfViewport max = Viewport(document, first);
            Assert.Equal(shown.Width - 2 * margin, max.Width, 6);
            Assert.Equal(shown.Height - 2 * margin, max.Height, 6);
            Assert.Equal((shown.MinX + shown.MaxX) / 2.0, max.CenterPoint.X, 6);
            Assert.Equal((shown.MinY + shown.MaxY) / 2.0, max.CenterPoint.Y, 6);
            Assert.Equal(scale, max.Height / max.ViewHeight, 9);

            // The part of model space under any point of the sheet is the one the viewport showed there.
            GeoPoint2 modelNow = CadViewports.ToModel(max, new GeoPoint2(110, 60));
            Assert.Equal(modelAtOldMiddle.X, modelNow.X, 6);
            Assert.Equal(modelAtOldMiddle.Y, modelNow.Y, 6);
            Assert.False(Viewport(document, second).On);
            Assert.Equal(first, document.ActiveViewport?.Handle);
            Assert.Equal(first, document.MaximizedViewportHandle);
            Assert.True(document.IsViewportMaximized);
            Assert.Equal("VPMAX", document.NextUndo.Name);
        }

        [Fact]
        public void Vpmin_PutsTheFrameTheViewAndTheOthersBack_AndWorksOnThePaper()
        {
            CadDocument document = WithViewports(out CadCommandSession session, out string first, out string second);
            DxfViewport before = Viewport(document, first);
            GeoPoint3 center = before.CenterPoint;
            GeoPoint2 viewCenter = before.ViewCenter;
            Run(session, "VPMAX", (object)new[] { first });

            // A zoom made while it is maximized is given up with it.
            Assert.True(document.SetViewportView(first, new GeoPoint2(50, 50), 80.0, "ZOOM").Succeeded);
            Run(session, "VPMIN");

            Assert.False(session.IsRunning);
            DxfViewport back = Viewport(document, first);
            Assert.Equal(200.0, back.Width, 9);
            Assert.Equal(100.0, back.Height, 9);
            Assert.Equal(center.X, back.CenterPoint.X, 9);
            Assert.Equal(center.Y, back.CenterPoint.Y, 9);
            Assert.Equal(viewCenter.X, back.ViewCenter.X, 9);
            Assert.Equal(viewCenter.Y, back.ViewCenter.Y, 9);
            Assert.Equal(1000.0, back.ViewHeight, 9);
            Assert.True(Viewport(document, second).On);
            Assert.Null(document.ActiveViewport);
            Assert.False(document.IsViewportMaximized);
            Assert.Equal("VPMIN", document.NextUndo.Name);
        }

        [Fact]
        public void AViewportWorkedThroughAlready_IsTheOneMaximized_WithNoViewportAsked()
        {
            CadDocument document = WithViewports(out CadCommandSession session, out string first, out string second);
            Assert.True(document.EnterViewport(second));

            Run(session, "VPMAX");

            Assert.False(session.IsRunning);
            Assert.Equal(second, document.MaximizedViewportHandle);
            Assert.False(Viewport(document, first).On);
        }

        [Fact]
        public void TheMessages_AreAutoCads_AndAnythingButAViewportIsAskedAgain()
        {
            CadDocument document = WithViewports(out CadCommandSession session, out string first, out _);
            Run(session, "VPMIN");
            Assert.Contains("No viewport is currently maximized.", session.History);
            Assert.False(session.IsRunning);

            Run(session, "VPMAX", (object)new[] { document.ModelSpace.Block.First().Handle });
            Assert.True(session.IsRunning);
            Assert.Contains("Invalid selection, please select a single viewport.", session.History);
            Assert.Equal("Select a viewport to maximize:", session.PromptText);

            Run(session, Enter);
            Assert.Equal(2, session.History.Count(l => l == "Invalid selection, please select a single viewport."));
            Assert.True(session.IsRunning);

            Run(session, (object)new[] { first });
            Assert.False(session.IsRunning);
            Run(session, "VPMAX");
            Assert.Contains("A viewport is currently maximized.", session.History);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AnUndoOfVpmax_PutsTheViewportBack_AndARedoMaximizesItAgain()
        {
            CadDocument document = WithViewports(out CadCommandSession session, out string first, out string second);
            Run(session, "VPMAX", (object)new[] { first });
            Assert.True(document.IsViewportMaximized);

            Assert.True(document.Undo());
            Assert.False(document.IsViewportMaximized);
            Assert.Equal(200.0, Viewport(document, first).Width, 9);
            Assert.True(Viewport(document, second).On);

            Assert.True(document.Redo());
            Assert.True(document.IsViewportMaximized);
            Assert.False(Viewport(document, second).On);
        }

        [Fact]
        public void OnTheModelTab_VpmaxAndVpmin_AreVportsToggle()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Assert.Null(Record.Exception(() => document.TiledViewports.Split(2, "Vertical", false)));
            Assert.True(document.TiledViewports.IsTiled);

            Run(session, "VPMAX");
            Assert.False(document.TiledViewports.IsTiled);

            Run(session, "VPMIN");
            Assert.True(document.TiledViewports.IsTiled);
        }

        private static string Tiles(CadDocument document) =>
            string.Join(" ", document.TiledViewports.Tiles.Select(t => t.Id + ":" + Corners(t)));

        private static string Corners(CadTiledViewport t) =>
            Number(t.Left) + "," + Number(t.Bottom) + "-" + Number(t.Right) + "," + Number(t.Top);

        private static string Number(double value) =>
            value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        [Fact]
        public void OnTheModelTabWithOneViewportAndNoneKept_VpmaxMakesFourTiles_AsAutoCADsToggleDoes()
        {
            // Probed in AutoCAD 2021's console, fed by standard input: a drawing with one viewport (id 2), VPMAX gives these four,
            // the current one (id 2) the first, and keeps them as *Multiple.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Assert.False(document.TiledViewports.IsTiled);

            Run(session, "VPMAX");

            Assert.Equal("2:0.5,0-1,0.5 3:0.5,0.5-1,1 4:0,0.5-0.5,1 5:0,0-0.5,0.5", Tiles(document));
            Assert.Equal(4, document.TiledViewports.Saved("*Multiple").Count);
            Assert.Equal(2, document.TiledViewports.Current.Id);
        }

        [Fact]
        public void TheFirstToggleFromOneViewport_SaysTheRegenerationOncePerTile()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "VPMAX");
            Assert.Equal(4, session.History.Count(line => line == "Regenerating model."));

            Run(session, "VPMAX");
            Assert.Equal(5, session.History.Count(line => line == "Regenerating model."));
        }

        [Fact]
        public void TheViewportLeftAlone_TakesTheOtherOfNumbers2And3_AndIsCurrentWithThatNumber()
        {
            // AutoCAD 2021: from four tiles with 2 current, VPMAX and -VPORTS SIngle leave the one with number 3; with 3 current, number 2.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "VPMAX");
            Assert.Equal(2, document.TiledViewports.Current.Id);

            Run(session, "VPMAX");
            Assert.Equal("3:0,0-1,1", Tiles(document));
            Assert.Equal(3, document.TiledViewports.Current.Id);

            // Brought back, the tile that was current takes the number of the one alone and the others the free numbers in order.
            Run(session, "VPMIN");
            Assert.Equal("3:0.5,0-1,0.5 2:0.5,0.5-1,1 4:0,0.5-0.5,1 5:0,0-0.5,0.5", Tiles(document));
            Assert.True(document.TiledViewports.MakeCurrent(3));

            Run(session, "-VPORTS", "SI");
            Assert.Equal("2:0,0-1,1", Tiles(document));
        }
    }
}
