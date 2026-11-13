using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The views and viewports of the layout follow-ups, each checked against AutoCAD 2021's console (probes GhVp1 to GhVp4):
    /// a view saved in a layout remembers it and -VIEW Restore shows that layout, a view of model space restored in a layout
    /// goes into a viewport picked ("Select Viewport for view:"), and MVIEW's Polygonal makes a viewport clipped by a polygon.
    /// </summary>
    public class ViewportViewFollowUpsTests
    {
        private static CadCommandSession Start(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return session;
        }

        /// <summary>A drawing with a rectangle 2 000 by 1 000 in model space and on Layout1 a viewport 200 by 100 about (110, 60).</summary>
        private static CadDocument WithViewport(out CadCommandSession session, out string handle)
        {
            CadDocument document = Blank();
            session = Start(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            handle = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            return document;
        }

        /// <summary>A drawing with a second layout, Sheet 2, after Layout1.</summary>
        private static CadDocument WithTwoLayouts(out CadCommandSession session)
        {
            CadDocument document = Blank();
            session = Start(document);
            Run(session, "LAYOUT", "N", "Sheet 2");
            Assert.Equal(3, document.Spaces.Count);
            return document;
        }

        [Fact]
        public void AViewSavedInALayout_KeepsTheLayout_AndOneOfModelSpaceKeepsNone()
        {
            CadDocument document = WithTwoLayouts(out CadCommandSession session);
            document.CurrentSpace = document.Spaces[2];
            Run(session, "-VIEW", "W", "PV", P(10, 20), P(110, 80));
            document.CurrentSpace = document.ModelSpace;
            Run(session, "-VIEW", "W", "MV1", P(0, 0), P(600, 400));

            Assert.Equal(document.Spaces[2].Layout.Handle, document.Database.ViewTable["PV"].Layout.Handle);
            Assert.Null(document.Database.ViewTable["MV1"].Layout);
        }

        [Fact]
        public void ViewRestoreOfAPaperView_ShowsTheLayoutItWasSavedIn_NotTheOneOnScreen()
        {
            CadDocument document = WithTwoLayouts(out CadCommandSession session);
            document.CurrentSpace = document.Spaces[2];
            Run(session, "-VIEW", "W", "PV", P(10, 20), P(110, 80));
            document.CurrentSpace = document.Spaces[1];

            Run(session, "-VIEW", "R", "PV");

            // AutoCAD 2021 (probe GhVp1) showed the layout a view was saved in when it was restored from the Model tab ("Restoring
            // cached viewports - Regenerating layout."); from another layout it is the same switch.
            Assert.Same(document.Spaces[2], document.CurrentSpace);
            Assert.Equal("-VIEW", document.NextUndo.Name);
        }

        [Fact]
        public void ViewRestoreOfAModelView_InALayout_AsksForAViewport_AndShowsTheWholeViewThroughIt()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            document.CurrentSpace = document.ModelSpace;
            Run(session, "-VIEW", "W", "M1", P(0, 0), P(600, 400));
            document.CurrentSpace = document.Spaces[1];

            Run(session, "-VIEW", "R", "M1");

            // AutoCAD 2021 (probe GhVp2): "Restoring Model space view." and "Select Viewport for view:" asked, the pick of a
            // viewport 200 by 100 giving it the view's centre and the height that takes the whole view, here 400 (600 over 2 is less).
            Assert.Contains("Restoring Model space view.", session.History);
            Assert.True(session.IsRunning);
            Assert.Equal("Select Viewport for view:", session.PromptText);
            Run(session, new object[] { new[] { handle } });

            Assert.False(session.IsRunning);
            DxfViewport viewport = All<DxfViewport>(document.CurrentSpace).Last();
            Assert.Equal(300.0, viewport.ViewCenter.X, 3);
            Assert.Equal(200.0, viewport.ViewCenter.Y, 3);
            Assert.Equal(400.0, viewport.ViewHeight, 3);
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Assert.Equal("-VIEW", document.NextUndo.Name);

            // One undo step: the viewport shows what it did and the paper is worked on again.
            Assert.True(document.Undo());
            viewport = All<DxfViewport>(document.CurrentSpace).Last();
            Assert.Equal(1000.0, viewport.ViewHeight, 3);
            Assert.Null(document.ActiveViewport);
        }

        [Fact]
        public void ViewRestoreOfAModelView_FitsItsWidthToAViewportTallerThanTheView()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            document.CurrentSpace = document.ModelSpace;
            Run(session, "-VIEW", "W", "M1", P(0, 0), P(900, 300));
            document.CurrentSpace = document.Spaces[1];

            Run(session, "-VIEW", "R", "M1", new[] { handle });

            // A view 900 by 300 into a viewport twice as wide as high needs 450 of height, not 300.
            Assert.Equal(450.0, All<DxfViewport>(document.CurrentSpace).Last().ViewHeight, 3);
        }

        [Fact]
        public void ViewRestoreOfAModelView_WorkedThroughAViewport_AsksNothing_AndOnAPickOfNoViewport_AsksAgain()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            document.CurrentSpace = document.ModelSpace;
            Run(session, "-VIEW", "W", "M1", P(0, 0), P(600, 400));
            document.CurrentSpace = document.Spaces[1];
            string line = All<DxfViewport>(document.CurrentSpace).First().Handle;

            // The sheet's own viewport, number 1, is not one a view goes into.
            Run(session, "-VIEW", "R", "M1", new[] { line });
            Assert.True(session.IsRunning);
            Run(session, new object[] { new[] { handle } });
            Assert.False(session.IsRunning);

            // Worked through it now, a second restore goes into it with no prompt (probe GhVp2: CVPORT stayed 3).
            Run(session, "-VIEW", "R", "M1");
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void MviewPolygonal_AsksPointsAsPlineDoes_AndMakesAViewportClippedByTheClosedPolygon()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];

            Run(session, "MVIEW", "Polygonal", P(30, 30), P(190, 30), P(30, 90), "C");

            // AutoCAD 2021 (probe GhVp3): the polygon a closed polyline made before the viewport, the viewport's frame its box
            // (160 by 60 about (110, 60)), clipped to it, the view as MVIEW gives a rectangle, one undo step.
            DxfViewport viewport = Assert.Single(All<DxfViewport>(document.CurrentSpace), v => v.NonRectClipOn);
            DxfPolyline border = Assert.IsType<DxfPolyline>(viewport.NonRectClipEntity);
            Assert.True(border.Closed);
            Assert.Equal(3, border.NumberOfVertices);
            Assert.Equal(160.0, viewport.Width, 3);
            Assert.Equal(60.0, viewport.Height, 3);
            Assert.Equal(110.0, viewport.CenterPoint.X, 3);
            Assert.Equal(60.0, viewport.CenterPoint.Y, 3);
            Assert.Equal(1000.0, viewport.ViewHeight, 3);
            DxfEntity[] entities = document.CurrentSpace.Block.ToArray();
            Assert.True(
                System.Array.IndexOf(entities, (DxfEntity)border) < System.Array.IndexOf(entities, (DxfEntity)viewport)
            );
            Assert.Equal("MVIEW", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.DoesNotContain(All<DxfViewport>(document.CurrentSpace), v => v.NonRectClipOn);
            Assert.Empty(All<DxfPolyline>(document.CurrentSpace));
        }

        [Fact]
        public void TheOutlineOfARegionClip_IsKeptBetweenCalls_AndReadAgainOnceTheRegionHasMoved()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "PLINE", P(300, 40), P(400, 40), P(400, 100), P(300, 100), "C");
            Run(session, "REGION", new[] { All<DxfPolyline>(document.CurrentSpace).Single().Handle }, Enter);
            string region = All<DxfUnknownEntity>(document.CurrentSpace).Single(e => e.DxfName == "REGION").Handle;
            Run(session, "MVIEW", "Object", new[] { region });
            DxfViewport viewport = All<DxfViewport>(document.CurrentSpace).Single(v => v.NonRectClipOn);

            DisplayClip2 outline = CadViewportClips.OutlineOf(viewport);
            Assert.Same(outline, CadViewportClips.OutlineOf(viewport));
            double left = Enumerable.Range(0, outline.Count).Min(i => outline.X(i));

            Run(session, "MOVE", new[] { region }, Enter, P(0, 0), P(50, 0));
            viewport = All<DxfViewport>(document.CurrentSpace).Single(v => v.NonRectClipOn);
            DisplayClip2 moved = CadViewportClips.OutlineOf(viewport);

            Assert.NotSame(outline, moved);
            Assert.Equal(left + 50.0, Enumerable.Range(0, moved.Count).Min(i => moved.X(i)), 3);
        }

        [Fact]
        public void MviewPolygonal_EnterClosesIt_AndTwoPointsOnlyMakeNothing_AsAutoCadSaysItNeedsThree()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            document.CurrentSpace = document.Spaces[1];

            Run(session, "MVIEW", "Polygonal", P(50, 10), P(70, 10), P(70, 30), Enter);
            Assert.Single(All<DxfViewport>(document.CurrentSpace), v => v.NonRectClipOn);

            Run(session, "MVIEW", "Polygonal", P(50, 10), P(70, 10), Enter);
            Assert.Contains("Clip polyline must have at least 3 vertices.", session.History);
            Assert.Single(All<DxfViewport>(document.CurrentSpace), v => v.NonRectClipOn);
            Assert.Single(All<DxfPolyline>(document.CurrentSpace));
        }
    }
}
