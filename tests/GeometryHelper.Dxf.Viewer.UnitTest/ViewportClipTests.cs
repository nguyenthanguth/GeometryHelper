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

        /// <summary>
        /// The viewport of <see cref="WithViewport"/> with, on the paper, a region: a ring (a circle of radius 40 about (110, 60)
        /// less one of radius 15), or an L.
        /// </summary>
        private static CadDocument WithARegion(
            out CadCommandSession session,
            out string handle,
            out string region,
            bool ring
        )
        {
            CadDocument document = WithViewport(out session, out handle);

            if (ring)
            {
                Run(session, "CIRCLE", P(110, 60), "40");
                Run(session, "CIRCLE", P(110, 60), "15");
                string[] circles = All<DxfCircle>(document.CurrentSpace).Select(c => c.Handle).ToArray();
                Run(session, "REGION", circles, Enter);
                var made = All<DxfUnknownEntity>(document.CurrentSpace).Where(e => e.DxfName == "REGION").ToList();
                DxfUnknownEntity disc = made.Single(r =>
                    r.GetRegionShape().Loops.Count == 1 && Math.Abs(r.GetRegionShape().Area - Math.PI * 1600.0) < 1.0
                );
                DxfUnknownEntity hole = made.Single(r => r != disc);
                Run(session, "SUBTRACT", new[] { disc.Handle }, Enter, new[] { hole.Handle }, Enter);
                region = disc.Handle;
            }
            else
            {
                Run(session, "PLINE", P(30, 30), P(190, 30), P(190, 60), P(110, 60), P(110, 90), P(30, 90), "C");
                string polyline = All<DxfPolyline>(document.CurrentSpace).Single().Handle;
                Run(session, "REGION", new[] { polyline }, Enter);
                region = All<DxfUnknownEntity>(document.CurrentSpace).Single(e => e.DxfName == "REGION").Handle;
            }

            return document;
        }

        [Fact]
        public void ARegionPicked_ClipsTheViewport_AsAnyClosedBoundaryDoes_AndItsFrameIsTheBoxOfTheRegion()
        {
            CadDocument document = WithARegion(
                out CadCommandSession session,
                out string handle,
                out string region,
                false
            );

            Run(session, "VPCLIP", new[] { handle }, new[] { region });

            // AutoCAD 2021 (VPCLIP, a REGION of the L): the region itself is the boundary, the frame its box (160 by 60 about
            // (110, 60)), the view kept at 1:10.
            DxfViewport viewport = Viewport(document, handle);
            Assert.True(viewport.NonRectClipOn);
            Assert.Equal(region, viewport.NonRectClipEntity.Handle);
            Near(new GeoPoint3(110, 60, 0), viewport.CenterPoint);
            Assert.Equal(160.0, viewport.Width, 9);
            Assert.Equal(60.0, viewport.Height, 9);
            Assert.Equal(0.1, viewport.CustomScale, 9);
            Assert.Equal("VPCLIP", document.NextUndo.Name);
        }

        [Fact]
        public void ARegionWithAHole_ClipsTheViewport_AndItsOutlineHasTheHole()
        {
            CadDocument document = WithARegion(
                out CadCommandSession session,
                out string handle,
                out string region,
                true
            );

            Run(session, "VPCLIP", new[] { handle }, new[] { region });

            // AutoCAD 2021 (VPCLIP, a ring of radius 40 and 15, then plotted): the frame is 80 by 80, and nothing of model space
            // shows in the hole.
            DxfViewport viewport = Viewport(document, handle);
            Assert.Equal(80.0, viewport.Width, 9);
            Assert.Equal(80.0, viewport.Height, 9);
            DisplayViewport2 shown = document
                .GetDisplay(document.CurrentSpace)
                .Items.OfType<DisplayViewport2>()
                .Single(v => v.Handle == handle);
            Assert.NotNull(shown.Clip);
            Assert.True(shown.Clip.Contains(110 + 30, 60));
            Assert.False(shown.Clip.Contains(110, 60));
            Assert.False(shown.Clip.Contains(110 + 41, 60));
            Assert.Equal(5.0, shown.Clip.DistanceToEdge(110 + 20, 60), 3);
            Assert.Equal(2, shown.Clip.FlattenAll(0.01).Count);
        }

        /// <summary>Corners of a closed polyline as x, y pairs, for comparing with AutoCAD's.</summary>
        private static double[] Corners(DxfEntity boundary)
        {
            var points = new List<double>();

            foreach (DxfVertex2d vertex in (DxfPolyline2d)boundary)
            {
                points.Add(vertex.Position.X);
                points.Add(vertex.Position.Y);
            }

            return points.ToArray();
        }

        private static void AssertSize(DxfViewport viewport, double width, double height)
        {
            Assert.Equal(width, viewport.Width, 3);
            Assert.Equal(height, viewport.Height, 3);
        }

        [Fact]
        public void AViewportTurned45AboutTheOrigin_BecomesClipped_ToItsTurnedCorners()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);

            Run(session, "ROTATE", new[] { handle }, Enter, P(0, 0), "45");

            // AutoCAD 2021 (probe GhVr12, ROTATE 0,0 45 of a viewport 200 by 100 about (110, 60), its view 1 000 high about
            // (1 000, 500)): clipped to a new 2D polyline of the corners turned, the frame 212.132 square about (35.355, 120.208),
            // the view twisted 45 degrees, its centre the turn of the old one, 2 121.32 high.
            DxfViewport viewport = Viewport(document, handle);
            Assert.True(viewport.NonRectClipOn);
            AssertView(viewport, 35.35534, 120.20815, 353.55339, 1060.66017, 45.0, 2121.32034);
            AssertSize(viewport, 212.132, 212.132);
            DxfEntity boundary = Assert.IsType<DxfPolyline2d>(viewport.NonRectClipEntity);
            Assert.Equal(new[] { viewport.Handle }, boundary.GetPersistentReactorIds());
            Assert.Equal("0", boundary.Layer);
            Assert.Equal(
                new[] { 0.0, 14.1421, 141.4214, 155.5635, 70.7107, 226.2742, -70.7107, 84.8528 },
                Corners(boundary).Select(v => Math.Round(v, 4)).ToArray()
            );
            Assert.Equal("ROTATE", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.False(Viewport(document, handle).NonRectClipOn);
            AssertView(Viewport(document, handle), 110.0, 60.0, 1000.0, 500.0, 0.0, 1000.0);
        }

        [Fact]
        public void AViewportTurnedAboutItsOwnMiddle_KeepsItsCentre_AndTurnsTheViewByTheAngleBackwardsToo()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "ROTATE", new[] { handle }, Enter, P(110, 60), "30");

            // AutoCAD 2021 (ROTATE 110,60 30): the frame 223.205 by 186.603 about the same centre, twisted 30 degrees.
            DxfViewport viewport = Viewport(document, handle);
            AssertView(viewport, 110.0, 60.0, 616.02540, 933.01270, 30.0, 1866.02540);
            AssertSize(viewport, 223.205, 186.603);
        }

        [Fact]
        public void AViewportTurnedMinus30_IsTwisted330Degrees_AndItsCornersRunAsAutoCadsDo()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);

            Run(session, "ROTATE", new[] { handle }, Enter, P(110, 60), "-30");

            // AutoCAD 2021: the view's centre (1 116.025, -66.987), twisted 330 degrees; the corners start at the lower left one.
            DxfViewport viewport = Viewport(document, handle);
            AssertView(viewport, 110.0, 60.0, 1116.02540, -66.98730, 330.0, 1866.02540);
            Assert.Equal(
                new[] { -1.6025, 66.6987, 171.6025, -33.3013, 221.6025, 53.3013, 48.3975, 153.3013 },
                Corners(viewport.NonRectClipEntity).Select(v => Math.Round(v, 4)).ToArray()
            );
        }

        [Fact]
        public void AViewportTurnedAQuarter_StaysARectangle_WithItsSidesSwapped()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);

            Run(session, "ROTATE", new[] { handle }, Enter, P(0, 0), "90");

            // AutoCAD 2021 (ROTATE 0,0 90): not clipped, 100 by 200 about (-60, 110), twisted 90 degrees, its view 2 000 high about
            // (-500, 1 000).
            DxfViewport viewport = Viewport(document, handle);
            Assert.False(viewport.NonRectClipOn);
            Assert.Null(viewport.NonRectClipEntity);
            AssertView(viewport, -60.0, 110.0, -500.0, 1000.0, 90.0, 2000.0);
            AssertSize(viewport, 100.0, 200.0);
        }

        /// <summary>The viewport of <see cref="WithViewport"/> made 100 by 80 about (100, 100), twisted 0.3, its target (30, 10), its view 400 high about (50, -20).</summary>
        private static CadDocument WithATwistedView(out CadCommandSession session, out string handle)
        {
            CadDocument document = WithViewport(out session, out handle);
            DxfViewport shown = Viewport(document, handle);
            shown.CenterPoint = new GeoPoint3(100, 100, 0);
            shown.Width = 100;
            shown.Height = 80;
            shown.ViewCenter = new GeoPoint2(50, -20);
            shown.ViewHeight = 400;
            shown.TwistAngle = 0.3;
            shown.ViewTarget = new GeoPoint3(30, 10, 0);
            return document;
        }

        [Fact]
        public void AViewportWithATargetAndATwist_TurnedAboutAPoint_WritesTheViewCentreAsTheTwistTurnsItAboutTheTarget()
        {
            CadDocument document = WithATwistedView(out CadCommandSession session, out string handle);

            Run(session, "ROTATE", new[] { handle }, Enter, P(20, 30), "60");

            // AutoCAD 2021 (probe GhVr7, ROTATE 20,30 60): about (-0.622, 134.282), 119.282 by 126.603, twisted 77.19 degrees,
            // the view's centre (33.688, 12.850), 633.013 high; its target kept.
            DxfViewport viewport = Viewport(document, handle);
            AssertView(viewport, -0.62179, 134.28203, 33.68800, 12.84990, 77.1887, 633.01270);
            AssertSize(viewport, 119.282, 126.603);
            Near(new GeoPoint3(30, 10, 0), viewport.ViewTarget);
        }

        [Fact]
        public void AViewportTurnedAQuarter_WithATargetAndATwist_TurnsTheViewCentreAboutTheTarget()
        {
            CadDocument document = WithATwistedView(out CadCommandSession session, out string handle);

            Run(session, "ROTATE", new[] { handle }, Enter, P(20, 30), "90");

            // AutoCAD 2021 (ROTATE 20,30 90): 80 by 100 about (-50, 110), twisted 107.19 degrees, the view's centre (22.750, 27.972).
            DxfViewport viewport = Viewport(document, handle);
            Assert.False(viewport.NonRectClipOn);
            AssertView(viewport, -50.0, 110.0, 22.74970, 27.97230, 107.1887, 500.0);
            AssertSize(viewport, 80.0, 100.0);
        }

        [Fact]
        public void ATurnWithinAMillionthOfARadianOfAQuarter_IsAQuarter_AndTenTimesThat_IsNot()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "ROTATE", new[] { handle }, Enter, P(110, 60), "90.00001");

            // AutoCAD 2021: 90.00001 degrees (1.7E-7 radian) made a rectangle 100 by 200, 90.0001 (1.7E-6) a clipped viewport.
            Assert.False(Viewport(document, handle).NonRectClipOn);
            AssertSize(Viewport(document, handle), 100.0, 200.0);

            CadDocument other = WithViewport(out CadCommandSession second, out string again);
            Run(second, "ROTATE", new[] { again }, Enter, P(110, 60), "90.0001");
            Assert.True(Viewport(other, again).NonRectClipOn);
        }

        [Fact]
        public void AnUnclippedViewportMirrored_MovesItsCentre_AndKeepsItsSizeAndView()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);

            Run(session, "MIRROR", new[] { handle }, Enter, P(0, 0), P(10, 10), "Yes");

            // AutoCAD 2021 (probes GhVr7 and GhVr12, MIRROR in place and as a copy): about the line y = x the centre (110, 60) goes
            // to (60, 110); the frame stays 200 by 100 and the view as it was.
            DxfViewport viewport = Viewport(document, handle);
            AssertView(viewport, 60.0, 110.0, 1000.0, 500.0, 0.0, 1000.0);
            AssertSize(viewport, 200.0, 100.0);
            Assert.False(viewport.NonRectClipOn);

            Run(session, "MIRROR", new[] { handle }, Enter, P(0, 0), P(0, 100), Enter);
            DxfViewport copy = All<DxfViewport>(document.CurrentSpace).Last();
            Assert.NotEqual(handle, copy.Handle);
            AssertView(copy, -60.0, 110.0, 1000.0, 500.0, 0.0, 1000.0);
            AssertView(Viewport(document, handle), 60.0, 110.0, 1000.0, 500.0, 0.0, 1000.0);
        }

        /// <summary>A viewport 80 by 60 about (100, 90) showing 100 high about (20, 10), clipped to the triangle (60, 60), (140, 60), (60, 120).</summary>
        private static CadDocument ClippedToATriangle(out CadCommandSession session, out string handle)
        {
            CadDocument document = WithViewport(out session, out handle);
            DxfViewport shown = Viewport(document, handle);
            shown.CenterPoint = new GeoPoint3(100, 90, 0);
            shown.Width = 80;
            shown.Height = 60;
            shown.ViewCenter = new GeoPoint2(20, 10);
            shown.ViewHeight = 100;
            Run(session, "PLINE", P(60, 60), P(140, 60), P(60, 120), "C");
            string triangle = All<DxfPolyline>(document.CurrentSpace).Last().Handle;
            Run(session, "VPCLIP", new[] { handle }, new[] { triangle });
            return document;
        }

        [Fact]
        public void ACopyMirroredAboutADiagonal_WhoseFrameChangesSize_MovesTheViewByTheStepOfItsCentre()
        {
            CadDocument document = ClippedToATriangle(out CadCommandSession session, out string handle);

            Run(session, "MIRROR", new[] { handle }, Enter, P(0, -50), P(10, -40), Enter);

            // AutoCAD 2021 (probe GhVr8, M1): the original stays; the copy's frame, the box of the mirrored triangle, is 60 by 80
            // about (140, 50) and its view's centre (86.667, -56.667), 133.333 high: the step from (100, 90) to (140, 50) over the
            // scale 0.6 of the view.
            DxfViewport[] pair = Clipped(document);
            Assert.Equal(2, pair.Length);
            AssertView(Viewport(document, handle), 100.0, 90.0, 20.0, 10.0, 0.0, 100.0);
            DxfViewport copy = pair.Single(v => v.Handle != handle);
            AssertView(copy, 140.0, 50.0, 86.66667, -56.66667, 0.0, 133.33333);
            AssertSize(copy, 60.0, 80.0);
        }

        [Fact]
        public void ACopyMirroredAboutAVerticalLine_OrWhoseFrameKeepsItsSize_KeepsTheView()
        {
            CadDocument document = ClippedToATriangle(out CadCommandSession session, out string handle);
            Run(session, "MIRROR", new[] { handle }, Enter, P(150, 0), P(150, 10), Enter);

            // AutoCAD 2021 (M3): about x = 150 the frame is the same size; the copy about (200, 90) shows the view as it was.
            DxfViewport copy = Clipped(document).Single(v => v.Handle != handle);
            AssertView(copy, 200.0, 90.0, 20.0, 10.0, 0.0, 100.0);
        }

        [Theory]
        [InlineData(3E-7, 10.0)]
        [InlineData(1E-6, -390.0)]
        public void ACopyMirroredAboutALineAFewMillionthsOfADegreeOffHorizontal_KeepsTheView_OnlyWhileTheFrameKeepsItsSize(
            double degrees,
            double viewY
        )
        {
            CadDocument document = ClippedToATriangle(out CadCommandSession session, out string handle);
            double a = degrees * Math.PI / 180.0;

            Run(
                session,
                "MIRROR",
                new[] { handle },
                Enter,
                P(0, -30),
                P(10 * Math.Cos(a), -30 + 10 * Math.Sin(a)),
                Enter
            );

            // AutoCAD 2021 (probes GhVr9 and GhVr10): a line 3E-7 degrees from horizontal changes the height of the box by 8E-7 and
            // the view stays; 1E-6 degrees changes it by 2.8E-6 and the view moves by the step of the frame's centre, 240 over 0.6.
            DxfViewport copy = Clipped(document).Single(v => v.Handle != handle);
            AssertView(copy, 100.0, -150.0, 20.0, viewY, 0.0, 100.0);
        }

        [Fact]
        public void AMirrorThatErases_AboutADiagonal_MovesTheViewByTheStepFromTheMirroredCentre()
        {
            CadDocument document = ClippedToATriangle(out CadCommandSession session, out string handle);

            Run(
                session,
                "MIRROR",
                new[] { handle },
                Enter,
                P(50, -50),
                P(50 + Math.Cos(Math.PI / 6), -50 + Math.Sin(Math.PI / 6)),
                "Yes"
            );

            // AutoCAD 2021 (probe GhVr9, E1, the line at 30 degrees through (50, -50)): the frame 51.962 by 99.282 about
            // (176.244, -76.699); its centre went to (196.244, -76.699) with the frame and is 20 left of it, so the view's centre
            // moves by 20 over the scale 0.6: (-13.333, 10).
            DxfViewport viewport = Assert.Single(Clipped(document));
            AssertView(viewport, 176.24356, -76.69873, -13.33333, 10.0, 0.0, 165.47005);
            AssertSize(viewport, 51.962, 99.282);
        }

        [Fact]
        public void MviewObject_MakesAViewportOfAClosedObject_ItsFrameTheBoxOfIt_AndAViewportOfARegionToo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "CIRCLE", P(110, 60), "30");
            string circle = All<DxfCircle>(document.CurrentSpace).Single().Handle;
            Run(session, "PLINE", P(300, 40), P(400, 40), P(400, 100), P(300, 100), "C");
            Run(session, "REGION", new[] { All<DxfPolyline>(document.CurrentSpace).Single().Handle }, Enter);
            string region = All<DxfUnknownEntity>(document.CurrentSpace).Single(e => e.DxfName == "REGION").Handle;

            Run(session, "MVIEW", "Object", new[] { circle });
            Run(session, "MVIEW", "Object", new[] { region });

            // AutoCAD 2021 (probe GhVr11, MVIEW Object): a viewport for each, its frame the box of the object, the object its
            // border with the viewport among its reactors, the whole in one undo step each.
            DxfViewport[] clipped = Clipped(document);
            Assert.Equal(2, clipped.Length);
            Assert.Equal(circle, clipped[0].NonRectClipEntity.Handle);
            Near(new GeoPoint3(110, 60, 0), clipped[0].CenterPoint);
            AssertSize(clipped[0], 60.0, 60.0);
            Assert.Equal(region, clipped[1].NonRectClipEntity.Handle);
            Near(new GeoPoint3(350, 70, 0), clipped[1].CenterPoint);
            AssertSize(clipped[1], 100.0, 60.0);
            Assert.Equal(new[] { clipped[1].Handle }, clipped[1].NonRectClipEntity.GetPersistentReactorIds());
            Assert.Equal("MVIEW", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Single(Clipped(document));
        }

        [Fact]
        public void MviewObject_RefusesAnOpenPolylineAndALine_WithAutoCadsWords_AndAsksAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "PLINE", P(300, 40), P(400, 40), P(400, 100), Enter);
            string open = All<DxfPolyline>(document.CurrentSpace).Single().Handle;
            Run(session, "LINE", P(0, 0), P(50, 50), Enter);
            string line = All<DxfLine>(document.CurrentSpace).Single().Handle;

            Run(session, "MVIEW", "Object", new[] { open });
            Assert.Contains("Clip object must be closed.", session.History);
            Assert.True(session.IsRunning);
            Run(session, new object[] { new[] { line } });

            // AutoCAD 2021 (probe GhVr11): "Clip object must be closed." for the open polyline, "Invalid viewport clip object."
            // for a line, each time "Select object to clip viewport:" again.
            Assert.Contains("Invalid viewport clip object.", session.History);
            Assert.True(session.IsRunning);
            Assert.Empty(Clipped(document));
        }

        [Fact]
        public void AViewportPastedIntoALayoutNeverShown_GetsTheSheetViewportFirst_AsAutoCadsAuditAsks()
        {
            CadDocument document = WithViewport(out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            DrawL(session);
            var clipboard = new CadMemoryClipboard();
            session.Clipboard = clipboard;
            Run(session, "COPYCLIP", new[] { handle }, Enter);

            CadDocument other = Blank();
            other.CurrentSpace = other.Spaces[1];
            CadCommandSession pasting = Session(other);
            pasting.Clipboard = clipboard;
            Assert.Empty(All<DxfViewport>(other.CurrentSpace));
            Run(pasting, "PASTECLIP", P(0, 0));
            Run(pasting, "PASTECLIP", P(250, 0));

            // AutoCAD 2021 (AUDIT of the saved drawing, probe GhVr13): a layout holding viewports with none for its sheet had
            // "Invalid linetype eNullObjectId changed to ByLayer" for the one it makes up; with the sheet's viewport, number 1,
            // first and the pasted ones numbered 2 and 3, it finds nothing.
            DxfViewport[] viewports = All<DxfViewport>(other.CurrentSpace).OrderBy(v => v.Number).ToArray();
            Assert.Equal(new[] { 1, 2, 3 }, viewports.Select(v => v.Number).ToArray());
            Assert.False(viewports[0].NonRectClipOn);
            Assert.True(viewports[1].NonRectClipOn);
            Assert.True(viewports[2].NonRectClipOn);
            Assert.Equal("PASTECLIP", other.NextUndo.Name);
            Assert.True(other.Undo());
            Assert.Equal(
                new[] { 1, 2 },
                All<DxfViewport>(other.CurrentSpace).Select(v => v.Number).OrderBy(n => n).ToArray()
            );
        }

        [Fact]
        public void ARegionBoundaryMoved_TakesItsViewportWithIt_ShowingWhatItShowed()
        {
            CadDocument document = WithARegion(
                out CadCommandSession session,
                out string handle,
                out string region,
                false
            );
            Run(session, "VPCLIP", new[] { handle }, new[] { region });

            Run(session, "MOVE", new[] { region }, Enter, P(0, 0), P(20, 5));

            // As for a polyline boundary: the frame follows the region with the view's centre where it was.
            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(130, 65, 0), viewport.CenterPoint);
            Assert.Equal(1000.0, viewport.ViewCenter.X, 9);
            Assert.Equal(region, viewport.NonRectClipEntity.Handle);
            Assert.True(document.Undo());
            Near(new GeoPoint3(110, 60, 0), Viewport(document, handle).CenterPoint);
        }
    }
}
