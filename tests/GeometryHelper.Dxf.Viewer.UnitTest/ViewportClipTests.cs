using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// VPCLIP and the viewport it clips, as AutoCAD 2021's (probe GhVc1): a polygon of points or an object picked clips the
    /// viewport and fits its frame to the box of the boundary, Delete takes the clip off, the boundary and the viewport are one
    /// object to a pick, a move of either takes both, and each command is one undo step.
    /// </summary>
    public class ViewportClipTests
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

        private static DxfViewport Viewport(CadDocument document, string handle) =>
            Entity<DxfViewport>(document, handle);

        /// <summary>The six corners of an L inside the viewport: 160 by 60 about (110, 60), the upper right left out.</summary>
        private static void DrawL(CadCommandSession session)
        {
            Run(session, P(30, 30), P(190, 30), P(190, 60), P(110, 60), P(110, 90), P(30, 90), Enter);
        }

        [Fact]
        public void APolygonOfPoints_ClipsTheViewport_AndFitsItsFrame_InOneUndoStep()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            string before = document.Database.ToText();

            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);

            DxfViewport viewport = Viewport(document, handle);
            DxfPolyline border = Assert.IsType<DxfPolyline>(viewport.NonRectClipEntity);
            Assert.Equal(6, border.NumberOfVertices);
            Assert.True(border.Closed);
            Assert.True(viewport.NonRectClipOn);
            Near(new GeoPoint3(110, 60, 0), viewport.CenterPoint);
            Assert.Equal(160.0, viewport.Width, 9);
            Assert.Equal(60.0, viewport.Height, 9);
            Assert.Equal(0.1, viewport.CustomScale, 9);
            Assert.Equal(600.0, viewport.ViewHeight, 9);
            Assert.Contains(handle, border.GetPersistentReactorIds());
            Assert.Equal("VPCLIP", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
            Assert.True(document.Redo());
            Assert.True(Viewport(document, handle).NonRectClipOn);
        }

        [Fact]
        public void APolygonWithAnArc_KeepsTheBulge_AndIsClosedByEnter()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);

            // AutoCAD 2021: the side (140, 70) to (140, 130) drawn in Arc mode from an edge running east is a half circle.
            Run(
                session,
                "VPCLIP",
                new[] { handle },
                Enter,
                P(30, 30),
                P(110, 30),
                "Arc",
                P(110, 90),
                "Line",
                P(30, 90),
                Enter
            );

            DxfPolyline border = Assert.IsType<DxfPolyline>(Viewport(document, handle).NonRectClipEntity);
            Assert.Equal(4, border.NumberOfVertices);
            Assert.Equal(0.0, border.GetBulgeAt(0), 9);
            Assert.Equal(1.0, border.GetBulgeAt(1), 9);
            Assert.Equal(0.0, border.GetBulgeAt(2), 9);
            Assert.Equal(0.0, border.GetBulgeAt(3), 9);

            // The half circle bulges 30 to the right of the edge: the box is 110 wide from x = 30 to 140.
            DxfViewport viewport = Viewport(document, handle);
            Assert.Equal(110.0, viewport.Width, 9);
            Assert.Equal(85.0, viewport.CenterPoint.X, 9);
        }

        [Fact]
        public void TwoPointsAndEnter_GiveNoClip_AsAutoCadSays()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            string before = document.Database.ToText();

            Run(session, "VPCLIP", new[] { handle }, Enter, P(30, 30), P(190, 30), Enter);

            Assert.Equal("Clip polyline must have at least 3 vertices.", session.History.Last());
            Assert.Equal(before, document.Database.ToText());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ACirclePicked_ClipsTheViewport_AndStaysTheSameEntity()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "CIRCLE", P(110, 60), "30");
            string circle = Assert.Single(All<DxfCircle>(document.CurrentSpace)).Handle;

            Run(session, "VPCLIP", new[] { handle }, new[] { circle });

            DxfViewport viewport = Viewport(document, handle);
            Assert.Equal(circle, viewport.NonRectClipEntity.Handle);
            Assert.Equal(60.0, viewport.Width, 9);
            Assert.Equal(60.0, viewport.Height, 9);
            Near(new GeoPoint3(110, 60, 0), viewport.CenterPoint);
            Assert.Equal(600.0, viewport.ViewHeight, 9);
        }

        [Fact]
        public void AnOpenPolyline_IsRefused_WithAutoCadsWords_AndTheCommandAsksAgain()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "PLINE", P(30, 30), P(190, 30), P(190, 90), Enter);
            string open = Assert.Single(All<DxfPolyline>(document.CurrentSpace)).Handle;

            Run(session, "VPCLIP", new[] { handle }, new[] { open });

            Assert.Equal("Clip object must be closed.", session.History.Last());
            Assert.True(session.IsRunning);
            Assert.False(Viewport(document, handle).NonRectClipOn);
            Run(session, Esc);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ANonViewportPicked_IsRefused_AndTheViewportAskedAgain()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "LINE", P(1, 1), P(5, 5), Enter);
            string line = Assert.Single(All<DxfLine>(document.CurrentSpace)).Handle;

            Run(session, "VPCLIP", new[] { line });

            Assert.Equal("Object selected was not a viewport.", session.History.Last());
            Assert.True(session.IsRunning);
            Run(session, Esc);
            Assert.False(Viewport(document, handle).NonRectClipOn);
        }

        [Fact]
        public void Delete_ErasesTheBoundary_AndKeepsTheFrame_AndOnAnUnclippedViewportSaysSo()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);

            Run(session, "VPCLIP", new[] { handle }, "Delete");
            Assert.Equal("Viewport does not have clip boundary associated.", session.History.Last());

            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            string border = Viewport(document, handle).NonRectClipEntity.Handle;
            Assert.NotNull(document.FindEntity(border));

            Run(session, "VPCLIP", new[] { handle }, "Delete");

            DxfViewport viewport = Viewport(document, handle);
            Assert.False(viewport.NonRectClipOn);
            Assert.Null(viewport.NonRectClipEntity);
            Assert.Null(document.FindEntity(border));
            Assert.Equal(160.0, viewport.Width, 9);
            Assert.Equal(60.0, viewport.Height, 9);
            Assert.Equal("VPCLIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.NotNull(document.FindEntity(border));
            Assert.True(Viewport(document, handle).NonRectClipOn);
        }

        [Fact]
        public void APickOfTheBoundary_TakesTheViewportToo_AndTheViewportTakesItsBoundary()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            string border = Viewport(document, handle).NonRectClipEntity.Handle;

            IReadOnlyList<string> byBorder = document.WithGroups(new[] { border });
            IReadOnlyList<string> byViewport = document.WithGroups(new[] { handle });

            Assert.Equal(new[] { border, handle }, byBorder);
            Assert.Equal(new[] { handle, border }, byViewport);
        }

        [Fact]
        public void MovingTheBoundary_MovesTheViewportWithIt_ShowingWhatItShowed()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            string border = Viewport(document, handle).NonRectClipEntity.Handle;
            DxfViewport viewport = Viewport(document, handle);
            GeoPoint2 centreBefore = viewport.ViewCenter;

            Run(session, "MOVE", new[] { border }, Enter, P(0, 0), P(20, 5));

            viewport = Viewport(document, handle);
            Near(new GeoPoint3(130, 65, 0), viewport.CenterPoint);
            Assert.Equal(160.0, viewport.Width, 9);
            Assert.Equal(60.0, viewport.Height, 9);

            // The frame moved 20 right and 5 up with its boundary and the view's centre stayed, as AutoCAD 2021 leaves it (probe GhVq,
            // MOVE 0,0 20,5): what the viewport showed goes with it.
            Assert.Equal(centreBefore.X, viewport.ViewCenter.X, 9);
            Assert.Equal(centreBefore.Y, viewport.ViewCenter.Y, 9);
            Assert.Equal(0.1, viewport.CustomScale, 9);
            DxfPolyline moved = Assert.IsType<DxfPolyline>(viewport.NonRectClipEntity);
            Near(new GeoPoint2(50, 35), moved.GetPoint2dAt(0));
            Assert.True(document.Undo());
            Near(new GeoPoint3(110, 60, 0), Viewport(document, handle).CenterPoint);
        }

        [Fact]
        public void AMoveOfTheViewportAlone_TakesTheBoundaryToo()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            string border = Viewport(document, handle).NonRectClipEntity.Handle;

            Run(session, "MOVE", new[] { handle }, Enter, P(0, 0), P(20, 5));

            Near(new GeoPoint3(130, 65, 0), Viewport(document, handle).CenterPoint);
            DxfPolyline moved = Assert.IsType<DxfPolyline>(Entity<DxfEntity>(document, border));
            Near(new GeoPoint2(50, 35), moved.GetPoint2dAt(0));
        }

        [Fact]
        public void ErasingTheBoundary_ErasesTheViewport_AndUndoBringsBothBack()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            string border = Viewport(document, handle).NonRectClipEntity.Handle;

            Run(session, "ERASE", new[] { border }, Enter);

            Assert.Null(document.FindEntity(border));
            Assert.Null(document.FindEntity(handle));
            Assert.True(document.Undo());
            Assert.NotNull(document.FindEntity(border));
            Assert.True(Viewport(document, handle).NonRectClipOn);
        }

        [Fact]
        public void ACopyOfAClippedViewport_IsTiedToTheCopyOfItsBoundary()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            string border = Viewport(document, handle).NonRectClipEntity.Handle;

            Run(session, "COPY", new[] { handle }, Enter, P(0, 0), P(0, 120), Enter);

            DxfViewport[] viewports = All<DxfViewport>(document.CurrentSpace).Where(v => v.NonRectClipOn).ToArray();
            Assert.Equal(2, viewports.Length);
            DxfViewport copy = viewports.Single(v => v.Handle != handle);
            Assert.NotEqual(border, copy.NonRectClipEntity.Handle);
            Near(new GeoPoint3(110, 180, 0), copy.CenterPoint);
            Assert.Equal(border, Viewport(document, handle).NonRectClipEntity.Handle);
        }

        [Fact]
        public void AClippedViewportHasItsMiddleAsItsOnlyGrip_AndDraggingIt_MovesTheBoundaryToo()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            DxfPolyline border = (DxfPolyline)Viewport(document, handle).NonRectClipEntity;

            // AutoCAD 2021: the grips of a clipped viewport are its middle alone; the corners of its frame are the boundary's.
            CadGrip only = Assert.Single(CadGrips.Of(Viewport(document, handle)));
            Assert.Equal(CadGripKind.Center, only.Kind);
            Assert.True(document.GripDrag(handle, 0, P(130, 65)).Succeeded);

            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(130, 65, 0), viewport.CenterPoint);
            Near(new GeoPoint2(50, 35), ((DxfPolyline)viewport.NonRectClipEntity).GetPoint2dAt(0));
            Assert.Equal(border.Handle, viewport.NonRectClipEntity.Handle);
        }

        [Fact]
        public void AVertexOfTheBoundaryDragged_FitsTheViewportsFrameToIt()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            string border = Viewport(document, handle).NonRectClipEntity.Handle;

            // The second corner, (190, 30), dragged to (210, 30): the L is 180 wide from x = 30, its frame about x = 120.
            Assert.True(document.GripDrag(border, 1, P(210, 30)).Succeeded);

            DxfViewport viewport = Viewport(document, handle);
            Assert.Equal(180.0, viewport.Width, 9);
            Near(new GeoPoint3(120, 60, 0), viewport.CenterPoint);
            Assert.Equal(0.1, viewport.CustomScale, 9);
        }

        [Fact]
        public void VpclipWorkedThroughAViewport_SwitchesToThePaper_AndBackIntoTheViewportAfterwards()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "MS");
            Assert.Equal(handle, document.ActiveViewport.Handle);

            Run(session, "VPCLIP");
            Assert.Equal("Switching to Paper space.", session.History.Last());
            Assert.Null(document.ActiveViewport);
            Run(session, new[] { handle }, Enter);
            DrawL(session);

            // AutoCAD 2021 writes both lines and leaves the viewport worked through again, clipped.
            Assert.Equal("Switching to Model space.", session.History.Last());
            Assert.Equal(handle, document.ActiveViewport.Handle);
            Assert.True(Viewport(document, handle).NonRectClipOn);

            // Cancelled, it goes back as well.
            Run(session, "VPCLIP", Esc);
            Assert.Equal("Switching to Model space.", session.History.Last());
            Assert.Equal(handle, document.ActiveViewport.Handle);
        }

        [Fact]
        public void ADisplayedViewport_CarriesItsOutline_AnLShapeAndAnExactCircle()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);

            DisplayViewport2 shown = document
                .GetDisplay(document.CurrentSpace)
                .Items.OfType<DisplayViewport2>()
                .Single(v => v.Handle == handle);

            Assert.NotNull(shown.Clip);
            Assert.Equal(6, shown.Clip.Count);
            Assert.True(shown.Clip.Contains(50, 40));
            Assert.False(shown.Clip.Contains(150, 80));
            Assert.Equal(0.0, shown.Clip.DistanceToEdge(110, 75), 6);

            var round = new DisplayClip2(new[] { 140.0, 60.0, 80.0, 60.0 }, new[] { 1.0, 1.0 });
            Assert.True(round.Contains(110, 60));
            Assert.False(round.Contains(110, 91));
            Assert.Equal(0.0, round.DistanceToEdge(110, 90), 4);
            double[] ring = round.Flatten(0.001);
            for (int i = 0; i < ring.Length; i += 2)
            {
                double distance = Math.Sqrt(Math.Pow(ring[i] - 110, 2) + Math.Pow(ring[i + 1] - 60, 2));
                Assert.Equal(30.0, distance, 6);
            }
        }

        /// <summary>
        /// The viewport of <see cref="WithViewport"/> clipped to a circle of radius 30 about (110, 60): 60 by 60 there, its
        /// view about (1 000, 500) at 1:10, 600 high.
        /// </summary>
        private static CadDocument ClippedToACircle(out CadCommandSession session, out string handle, out string circle)
        {
            CadDocument document = WithViewport(out session, out handle);
            Run(session, "CIRCLE", P(110, 60), "30");
            circle = Assert.Single(All<DxfCircle>(document.CurrentSpace)).Handle;
            Run(session, "VPCLIP", new[] { handle }, new[] { circle });
            return document;
        }

        /// <summary>The viewports of the layout that are clipped, the first made first.</summary>
        private static DxfViewport[] Clipped(CadDocument document) =>
            All<DxfViewport>(document.CurrentSpace).Where(v => v.NonRectClipOn).ToArray();

        private static void AssertView(
            DxfViewport viewport,
            double x,
            double y,
            double viewX,
            double viewY,
            double twistDegrees,
            double viewHeight = 600.0
        )
        {
            Assert.Equal(x, viewport.CenterPoint.X, 3);
            Assert.Equal(y, viewport.CenterPoint.Y, 3);
            Assert.Equal(viewX, viewport.ViewCenter.X, 3);
            Assert.Equal(viewY, viewport.ViewCenter.Y, 3);
            Assert.Equal(twistDegrees, viewport.TwistAngle * 180.0 / Math.PI, 3);
            Assert.Equal(viewHeight, viewport.ViewHeight, 3);
        }

        [Fact]
        public void ARotatedPair_TwistsTheView_AndWritesAutoCadsViewCentre()
        {
            CadDocument document = ClippedToACircle(
                out CadCommandSession session,
                out string handle,
                out string circle
            );

            Run(session, "ROTATE", new[] { handle }, Enter, P(0, 0), "45");

            // AutoCAD 2021 (probe GhVq, ROTATE 0,0 45): the frame about (35.3553, 120.2082), the view twisted 45 degrees and its
            // centre at (767.1068, 2011.3203), which is the turn of 2 V - C' (V the view's centre, C' the frame's new centre).
            DxfViewport viewport = Viewport(document, handle);
            AssertView(viewport, 35.35534, 120.20815, 767.10678, 2011.32034, 45.0);
            Assert.Equal(60.0, viewport.Width, 9);
            Assert.Equal(60.0, viewport.Height, 9);
            Assert.Equal(circle, viewport.NonRectClipEntity.Handle);
            Near(new GeoPoint3(35.35534, 120.20815, 0), ((DxfCircle)viewport.NonRectClipEntity).Center, 1E-3);
            Assert.Equal("ROTATE", document.NextUndo.Name);
            Assert.True(document.Undo());
            AssertView(Viewport(document, handle), 110.0, 60.0, 1000.0, 500.0, 0.0);
        }

        [Fact]
        public void APairRotatedAboutItsOwnMiddle_KeepsItsFrame_AndAutoCadsViewCentre()
        {
            CadDocument document = ClippedToACircle(out CadCommandSession session, out string handle, out _);

            Run(session, "ROTATE", new[] { handle }, Enter, P(110, 60), "90");

            // AutoCAD 2021 (ROTATE 110,60 90): the frame where it was, twisted 90 degrees, the view's centre at (-940, 1890).
            AssertView(Viewport(document, handle), 110.0, 60.0, -940.0, 1890.0, 90.0);
        }

        [Fact]
        public void ARotationBackwards_WritesTheTwistAsAutoCadDoes_BetweenNoughtAndThreeHundredSixty()
        {
            CadDocument document = ClippedToACircle(out CadCommandSession session, out string handle, out _);

            Run(session, "ROTATE", new[] { handle }, Enter, P(110, 60), "-30");

            Assert.Equal(330.0, Viewport(document, handle).TwistAngle * 180.0 / Math.PI, 3);
        }

        [Fact]
        public void ARotatedCopyOfAPair_IsTwistedAndTiedToItsOwnBoundary_WhileTheOriginalStays()
        {
            CadDocument document = ClippedToACircle(
                out CadCommandSession session,
                out string handle,
                out string circle
            );

            Run(session, "ROTATE", new[] { handle }, Enter, P(0, 0), "C", "45");

            DxfViewport[] pair = Clipped(document);
            Assert.Equal(2, pair.Length);
            AssertView(Viewport(document, handle), 110.0, 60.0, 1000.0, 500.0, 0.0);
            Assert.Equal(circle, Viewport(document, handle).NonRectClipEntity.Handle);
            DxfViewport copy = pair.Single(v => v.Handle != handle);
            AssertView(copy, 35.35534, 120.20815, 767.10678, 2011.32034, 45.0);
            Assert.NotEqual(circle, copy.NonRectClipEntity.Handle);
            Assert.Equal(new[] { copy.Handle }, copy.NonRectClipEntity.GetPersistentReactorIds());
        }

        [Fact]
        public void AMirroredCopyOfAPair_KeepsTheView_AndFollowsTheMirroredBoundary()
        {
            CadDocument document = ClippedToACircle(
                out CadCommandSession session,
                out string handle,
                out string circle
            );

            Run(session, "MIRROR", new[] { handle }, Enter, P(0, 0), P(0, 100), Enter);

            // AutoCAD 2021 (MIRROR 0,0 0,1 N): the copy's frame about (-110, 60) with the view's centre and twist as they were.
            DxfViewport[] pair = Clipped(document);
            Assert.Equal(2, pair.Length);
            AssertView(Viewport(document, handle), 110.0, 60.0, 1000.0, 500.0, 0.0);
            DxfViewport copy = pair.Single(v => v.Handle != handle);
            AssertView(copy, -110.0, 60.0, 1000.0, 500.0, 0.0);
            Assert.Equal(60.0, copy.Width, 9);
            Assert.NotEqual(circle, copy.NonRectClipEntity.Handle);
            Near(new GeoPoint3(-110, 60, 0), ((DxfCircle)copy.NonRectClipEntity).Center);
            Assert.Equal(new[] { copy.Handle }, copy.NonRectClipEntity.GetPersistentReactorIds());
        }

        [Fact]
        public void AMirrorThatErasesTheSource_LeavesOnePairAtTheMirroredPlace_WithTheViewKept()
        {
            CadDocument document = ClippedToACircle(
                out CadCommandSession session,
                out string handle,
                out string circle
            );

            Run(session, "MIRROR", new[] { handle }, Enter, P(0, 0), P(0, 100), "Yes");

            DxfViewport viewport = Assert.Single(Clipped(document));
            Assert.Equal(handle, viewport.Handle);
            AssertView(viewport, -110.0, 60.0, 1000.0, 500.0, 0.0);
            Assert.Equal(circle, viewport.NonRectClipEntity.Handle);
        }

        [Fact]
        public void APairMoved_KeepsItsView_AsAutoCadsMoveDoes()
        {
            CadDocument document = ClippedToACircle(out CadCommandSession session, out string handle, out _);

            Run(session, "MOVE", new[] { handle }, Enter, P(0, 0), P(20, 5));

            // AutoCAD 2021 (MOVE 0,0 20,5): the frame moved with its boundary, the view's centre where it was.
            AssertView(Viewport(document, handle), 130.0, 65.0, 1000.0, 500.0, 0.0);
        }

        [Fact]
        public void APairScaledByTwo_ScalesTheFrameAndTheViewHeight_AndMovesTheViewCentreByTheStep()
        {
            CadDocument document = ClippedToACircle(out CadCommandSession session, out string handle, out _);

            Run(session, "SCALE", new[] { handle }, Enter, P(0, 0), "2");

            // AutoCAD 2021 (SCALE 0,0 2): the frame 120 by 120 about (220, 120), its view 1 200 high about (2 100, 1 100).
            DxfViewport viewport = Viewport(document, handle);
            AssertView(viewport, 220.0, 120.0, 2100.0, 1100.0, 0.0, 1200.0);
            Assert.Equal(120.0, viewport.Width, 9);
        }

        [Fact]
        public void AClippedViewportCopiedAndPastedIntoAnotherDrawing_BringsItsBoundary_AndKeepsThePair()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            var clipboard = new CadMemoryClipboard();
            session.Clipboard = clipboard;
            Run(session, "COPYCLIP", new[] { handle }, Enter);
            Assert.Equal(2, clipboard.GetData().Count);

            CadDocument other = Blank();
            other.CurrentSpace = other.Spaces[1];
            CadCommandSession pasting = Session(other);
            pasting.Clipboard = clipboard;
            Run(pasting, "PASTECLIP", P(0, 0));

            DxfViewport pasted = Assert.Single(All<DxfViewport>(other.CurrentSpace), v => v.NonRectClipOn);
            DxfPolyline border = Assert.IsType<DxfPolyline>(pasted.NonRectClipEntity);
            Assert.Equal(6, border.NumberOfVertices);
            Assert.Equal(new[] { pasted.Handle }, border.GetPersistentReactorIds());

            // The lower left corner of the pair, (30, 30), came to the point given: the frame 160 by 60 about (80, 30).
            Near(new GeoPoint3(80, 30, 0), pasted.CenterPoint);
            Assert.Equal(160.0, pasted.Width, 9);
            Assert.Equal(1000.0, pasted.ViewCenter.X, 9);
            Assert.Equal("PASTECLIP", other.NextUndo.Name);
            AssertReopens(other);
            Assert.Equal(handle, Viewport(document, handle).Handle);
        }
    }
}
