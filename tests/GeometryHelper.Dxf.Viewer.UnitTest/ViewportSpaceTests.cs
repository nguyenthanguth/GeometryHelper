using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>Model space worked in through a viewport (MSPACE, PSPACE), its view zoomed and scaled, with no window.</summary>
    public class ViewportSpaceTests
    {
        /// <summary>
        /// A rectangle from (0, 0) to (2 000, 1 000) in model space, and on Layout1 a viewport 200 by 100 about (110, 60)
        /// showing it all at 1:10, its view about (1 000, 500).
        /// </summary>
        private static CadDocument WithViewport(out CadCommandSession session, out string handle)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            handle = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            return document;
        }

        private static DxfViewport Viewport(CadDocument document, string handle) => Entity<DxfViewport>(document, handle);

        [Fact]
        public void MspaceEntersTheViewport_LinesThenGoIntoModelSpace_AndPspaceGoesBackToThePaper()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            int changes = 0;
            document.ActiveViewportChanged += (s, e) => changes++;

            Run(session, "MS");
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Assert.Same(document.ModelSpace, document.WorkingSpace);
            Assert.Same(document.Spaces[1], document.CurrentSpace);

            Run(session, "LINE", P(100, 100), P(600, 300), Enter);
            Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Empty(All<DxfLine>(document.CurrentSpace));

            Run(session, "PS");
            Assert.Null(document.ActiveViewport);
            Assert.Same(document.CurrentSpace, document.WorkingSpace);
            Run(session, "LINE", P(0, 0), P(50, 50), Enter);
            Assert.Single(All<DxfLine>(document.CurrentSpace));
            Assert.Equal(2, changes);

            // MSPACE enters the viewport last worked through again.
            Run(session, "MSPACE");
            Assert.Equal(handle, document.ActiveViewport.Handle);
        }

        [Fact]
        public void MspaceAndPspaceOnTheModelTab_AreRefused_AsALayoutWithNoViewportToEnter()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            document.CurrentSpace = document.ModelSpace;

            Run(session, "MS");
            Assert.Equal("** Command not allowed in Model Tab **", session.History.Last());
            Run(session, "PS");
            Assert.Equal("** Command not allowed in Model Tab **", session.History.Last());
            Assert.Null(document.ActiveViewport);

            // From the Model tab no viewport of a layout is entered; on the layout with its viewport erased, none is left.
            Assert.False(document.EnterViewport(handle));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "ERASE", new[] { handle }, Enter);
            Run(session, "MS");
            Assert.Equal("There are no active model space viewports.", session.History.Last());
            Assert.Null(document.ActiveViewport);
        }

        [Fact]
        public void APickThroughTheViewport_SelectsInModelSpace()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out _);
            string rectangle = Assert.Single(All<DxfPolyline>(document.ModelSpace)).Handle;
            Assert.True(document.EnterViewport(document.EnterableViewports.Single().Handle));

            Run(session, "ERASE", P(1000, 0), Enter);

            Assert.Null(document.FindEntity(rectangle));
        }

        [Fact]
        public void ZoomThroughTheViewport_ChangesItsViewNotThePaper_EachZoomOneUndoStep()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            var paper = new CadView(800, 600);
            paper.ZoomTo(new DisplayBox(0, 0, 420, 297));
            session.View = paper;
            double zoom = paper.PixelsPerUnit;
            document.EnterViewport(handle);
            Assert.Equal(1000, Viewport(document, handle).ViewHeight, 9);

            Run(session, "ZOOM", "2X");
            Assert.Equal(500, Viewport(document, handle).ViewHeight, 9);
            Near(P(1000, 500), Viewport(document, handle).ViewCenter);
            Assert.Equal("ZOOM", document.NextUndo.Name);
            Assert.Equal(zoom, paper.PixelsPerUnit);

            // 1/50XP: model space at 1:50, a frame 100 high showing 5 000.
            Run(session, "ZOOM", "1/50XP");
            Assert.Equal(0.02, Viewport(document, handle).CustomScale, 12);
            Assert.Equal(DxfStandardScaleType.Scale1To50, Viewport(document, handle).StandardScale);

            Run(session, "ZOOM", P(0, 0), P(400, 100));
            Assert.Equal(200, Viewport(document, handle).ViewHeight, 9);
            Near(P(200, 50), Viewport(document, handle).ViewCenter);

            Run(session, "ZOOM", "E");
            Assert.Equal(1000, Viewport(document, handle).ViewHeight, 9);

            Run(session, "PAN", P(0, 0), P(100, 50));
            Near(P(900, 450), Viewport(document, handle).ViewCenter);

            Assert.True(document.Undo());
            Near(P(1000, 500), Viewport(document, handle).ViewCenter);
            Assert.True(document.Undo());
            Assert.Equal(200, Viewport(document, handle).ViewHeight, 9);
        }

        [Fact]
        public void ALockedViewport_KeepsItsView_TheZoomGoingToThePaper()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Viewport(document, handle).Locked = true;
            var paper = new CadView(800, 600);
            paper.ZoomTo(new DisplayBox(0, 0, 420, 297));
            session.View = paper;
            double zoom = paper.PixelsPerUnit;
            document.EnterViewport(handle);

            Run(session, "ZOOM", "2X");

            Assert.Equal(1000, Viewport(document, handle).ViewHeight, 9);
            Assert.Equal(2 * zoom, paper.PixelsPerUnit, 9);
            Assert.True(document.SetViewportView(handle, P(0, 0), 10, "ZOOM").Refused);
        }

        [Fact]
        public void TheScaleRowsOfProperties_SetTheViewHeightFromTheScale_AndDisplayLockedKeepsIt()
        {
            CadDocument document = WithViewport(out _, out string handle);
            var handles = new[] { handle };

            Assert.True(CadPropertyEditor.Set(document, handles, "Standard scale", "1:50").Succeeded);
            Assert.Equal(5000, Viewport(document, handle).ViewHeight, 9);
            Near(P(1000, 500), Viewport(document, handle).ViewCenter);

            Assert.True(CadPropertyEditor.Set(document, handles, "Custom scale", "0.2").Succeeded);
            Assert.Equal(500, Viewport(document, handle).ViewHeight, 9);
            Assert.True(CadPropertyEditor.Set(document, handles, "Standard scale", "1:7").Refused);

            Assert.True(CadPropertyEditor.Set(document, handles, "Display locked", "Yes").Succeeded);
            Assert.Equal("Yes", CadProperties.Describe(document, null, handles).Groups.Single(g => g.Title == "Misc").Rows.Single(r => r.Label == "Display locked").Value);
            CadEditResult locked = CadPropertyEditor.Set(document, handles, "Standard scale", "1:100");
            Assert.Equal("The viewport's display is locked.", locked.Message);
            Assert.Equal(500, Viewport(document, handle).ViewHeight, 9);
        }

        [Fact]
        public void NextViewport_CyclesTheLayoutsViewports_AsCtrlR()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string first);
            Run(session, "MV", P(220, 10), P(320, 110));
            string second = All<DxfViewport>(document.CurrentSpace).Last().Handle;

            Assert.False(document.NextViewport());
            document.EnterViewport(first);
            Assert.True(document.NextViewport());
            Assert.Equal(second, document.ActiveViewport.Handle);
            Assert.True(document.NextViewport());
            Assert.Equal(first, document.ActiveViewport.Handle);
            Assert.Equal(new[] { first, second }, document.EnterableViewports.Select(v => v.Handle));
        }

        [Fact]
        public void AViewportErasedWhileWorkedThrough_LeavesThePaperWorkedOn()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            document.EnterViewport(handle);
            document.LeaveViewport();
            Run(session, "ERASE", new[] { handle }, Enter);

            Assert.Empty(document.EnterableViewports);
            Assert.False(document.EnterViewport(handle));
            Assert.Null(document.ActiveViewport);
        }

        [Fact]
        public void TheViewThroughAViewport_AndTheViewItGivesBack_AreEachOthersInverse()
        {
            CadDocument document = WithViewport(out _, out string handle);
            DxfViewport viewport = Viewport(document, handle);
            var paper = new CadView(800, 600);
            paper.ZoomTo(new DisplayBox(0, 0, 420, 297));

            CadView model = CadViewports.ModelView(paper, viewport);
            Assert.Equal(paper.PixelsPerUnit * 0.1, model.PixelsPerUnit, 12);
            paper.DrawingToPixel(110, 60, out double px, out double py);
            model.ScreenToDrawing(px, py, out double x, out double y);
            Assert.Equal(1000, x, 6);
            Assert.Equal(500, y, 6);
            Near(P(1000, 500), CadViewports.ToModel(viewport, P(110, 60)));
            Near(P(110, 60), CadViewports.ToPaper(viewport, P(1000, 500)));

            // Zoomed in twice about a pixel, the viewport's view halves and the model point under the pixel stays there.
            model.ZoomAt(300, 200, 2.0);
            model.ScreenToDrawing(300, 200, out double ax, out double ay);
            CadViewports.ViewOf(paper, model, viewport, out GeoPoint2 center, out double height);
            Assert.Equal(500, height, 9);
            viewport.ViewCenter = center;
            viewport.ViewHeight = height;
            paper.ScreenToDrawing(300, 200, out double sx, out double sy);
            Near(P(ax, ay), CadViewports.ToModel(viewport, P(sx, sy)), 1E-6);

            Assert.Equal("1:50", CadViewports.ScaleName(0.02));
            Assert.Equal("1:37.5", CadViewports.ScaleName(1 / 37.5));
            Assert.True(CadViewports.TryReadScale("1:50", out double scale));
            Assert.Equal(0.02, scale, 15);
            Assert.Contains("1:100", CadViewports.StandardScales);
        }
    }
}
