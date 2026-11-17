using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A viewport's frame resized, as AutoCAD 2021 does it (probe GhVps1, STRETCH, SCALE and the properties of a viewport 200 by
    /// 100 at 1:50 showing (1 000, 500), with the view twisted 0 and 30 degrees alike): the model stands still on the sheet,
    /// only the window onto it changes, the scale is kept, and the view's centre moves by the step of the frame's centre over
    /// the scale. A MOVE is the other way about: the frame takes its view along.
    /// </summary>
    public class ViewportStretchTests
    {
        private static readonly GeoPoint2 ModelPoint = new GeoPoint2(1234.0, 567.0);

        /// <summary>
        /// A viewport on Layout1 200 by 100 about (110, 60), at 1:50 showing model space about (1 000, 500), its view twisted by
        /// the angle given.
        /// </summary>
        private static CadDocument WithViewport(double twistDegrees, out CadCommandSession session, out string handle)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            DxfViewport viewport = All<DxfViewport>(document.CurrentSpace).Last();
            viewport.CustomScale = 0.02;
            viewport.TwistAngle = twistDegrees * Math.PI / 180.0;
            handle = viewport.Handle;
            return document;
        }

        private static DxfViewport Viewport(CadDocument document, string handle) =>
            Entity<DxfViewport>(document, handle);

        /// <summary>Where the model point stands on the sheet through the viewport.</summary>
        private static GeoPoint2 Paper(CadDocument document, string handle) =>
            CadViewports.ToPaper(Viewport(document, handle), ModelPoint);

        private static void AssertStill(GeoPoint2 was, GeoPoint2 now)
        {
            Assert.Equal(was.X, now.X, 6);
            Assert.Equal(was.Y, now.Y, 6);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void ACornerGripDragged_LeavesTheModelWhereItWasOnTheSheet(double twist)
        {
            CadDocument document = WithViewport(twist, out _, out string handle);
            GeoPoint2 was = Paper(document, handle);

            // The lower right corner, (210, 10), to (240, -20): AutoCAD's STRETCH gives the frame 230 by 130 about (125, 45), its
            // view's centre moved by (15, -15) over the scale, 0.02, to (1 750, -250), 6 500 high.
            Assert.True(document.GripDrag(handle, 1, P(240, -20)).Succeeded);

            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(125, 45, 0), viewport.CenterPoint);
            Assert.Equal(230.0, viewport.Width, 9);
            Assert.Equal(130.0, viewport.Height, 9);
            Assert.Equal(0.02, viewport.CustomScale, 12);
            Assert.Equal(1750.0, viewport.ViewCenter.X, 6);
            Assert.Equal(-250.0, viewport.ViewCenter.Y, 6);
            Assert.Equal(6500.0, viewport.ViewHeight, 6);
            AssertStill(was, Paper(document, handle));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void ACornerGripDraggedPastTheOppositeOne_StillLeavesTheModelWhereItWasOnTheSheet(double twist)
        {
            CadDocument document = WithViewport(twist, out _, out string handle);
            GeoPoint2 was = Paper(document, handle);

            // The lower right corner to (-20, 200), past the upper left one at (10, 110): the frame 30 by 90 about (-5, 155).
            Assert.True(document.GripDrag(handle, 1, P(-20, 200)).Succeeded);

            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(-5, 155, 0), viewport.CenterPoint);
            Assert.Equal(30.0, viewport.Width, 9);
            Assert.Equal(90.0, viewport.Height, 9);
            Assert.Equal(0.02, viewport.CustomScale, 12);
            AssertStill(was, Paper(document, handle));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void AStretchOfOneCorner_ResizesTheFrame_AndLeavesTheModelWhereItWasOnTheSheet(double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            GeoPoint2 was = Paper(document, handle);

            // AutoCAD 2021 (STRETCH, crossing window about the lower right corner, 0,0 to 30,-30): 230 by 130 about (125, 45).
            Run(session, "STRETCH", "C", P(200, 0), P(220, 20), Enter, P(0, 0), P(30, -30));

            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(125, 45, 0), viewport.CenterPoint);
            Assert.Equal(230.0, viewport.Width, 9);
            Assert.Equal(130.0, viewport.Height, 9);
            Assert.Equal(0.02, viewport.CustomScale, 12);
            Assert.Equal(1750.0, viewport.ViewCenter.X, 6);
            Assert.Equal(-250.0, viewport.ViewCenter.Y, 6);
            AssertStill(was, Paper(document, handle));
            Assert.Equal("STRETCH", document.NextUndo.Name);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void AStretchOfAnEdge_MovesTheEdge_AndLeavesTheModelWhereItWasOnTheSheet(double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            GeoPoint2 was = Paper(document, handle);

            // AutoCAD 2021 (the two right corners, 0,0 to 20,0): 220 wide about x = 120, the view's centre 500 to the right.
            Run(session, "STRETCH", "C", P(200, 0), P(220, 120), Enter, P(0, 0), P(20, 0));

            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(120, 60, 0), viewport.CenterPoint);
            Assert.Equal(220.0, viewport.Width, 9);
            Assert.Equal(100.0, viewport.Height, 9);
            Assert.Equal(1500.0, viewport.ViewCenter.X, 6);
            Assert.Equal(500.0, viewport.ViewCenter.Y, 6);
            Assert.Equal(5000.0, viewport.ViewHeight, 6);
            AssertStill(was, Paper(document, handle));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void AStretchOfAllFourCorners_MovesTheFrameWithItsView(double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            GeoPoint2 was = Paper(document, handle);

            // AutoCAD 2021: a window holding every corner is a move, the view's centre where it was.
            Run(session, "STRETCH", "C", P(0, 0), P(220, 120), Enter, P(0, 0), P(30, -20));

            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(140, 40, 0), viewport.CenterPoint);
            Assert.Equal(1000.0, viewport.ViewCenter.X, 6);
            Assert.Equal(500.0, viewport.ViewCenter.Y, 6);
            GeoPoint2 now = Paper(document, handle);
            Assert.Equal(was.X + 30.0, now.X, 6);
            Assert.Equal(was.Y - 20.0, now.Y, 6);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void AMove_CarriesTheModelWithTheFrame(double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            GeoPoint2 was = Paper(document, handle);

            Run(session, "MOVE", new[] { handle }, Enter, P(0, 0), P(30, -20));

            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(140, 40, 0), viewport.CenterPoint);
            Assert.Equal(1000.0, viewport.ViewCenter.X, 6);
            Assert.Equal(500.0, viewport.ViewCenter.Y, 6);
            GeoPoint2 now = Paper(document, handle);
            Assert.Equal(was.X + 30.0, now.X, 6);
            Assert.Equal(was.Y - 20.0, now.Y, 6);
        }

        [Theory]
        [InlineData(0.0, "Width", "260")]
        [InlineData(30.0, "Width", "260")]
        [InlineData(0.0, "Height", "160")]
        [InlineData(30.0, "Height", "160")]
        public void AWidthOrHeightSetInTheProperties_LeavesTheModelWhereItWasOnTheSheet(
            double twist,
            string label,
            string value
        )
        {
            CadDocument document = WithViewport(twist, out _, out string handle);
            GeoPoint2 was = Paper(document, handle);

            CadEditResult result = CadPropertyEditor.Set(document, new[] { handle }, label, value);

            Assert.True(result.Succeeded, result.Message);
            DxfViewport viewport = Viewport(document, handle);
            Assert.Equal(
                double.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                label == "Width" ? viewport.Width : viewport.Height,
                9
            );
            Assert.Equal(0.02, viewport.CustomScale, 12);
            AssertStill(was, Paper(document, handle));
        }

        [Theory]
        [InlineData(0.0, 10.0, 10.0, 6000.0, 3000.0)]
        [InlineData(30.0, 10.0, 10.0, 6000.0, 3000.0)]
        [InlineData(0.0, 110.0, 60.0, 1000.0, 500.0)]
        [InlineData(30.0, 110.0, 60.0, 1000.0, 500.0)]
        public void ASheetScaleOfTheViewportByTwo_ScalesTheFrameAndTheViewHeight_LeavingTheModelWhereItWasOnTheSheet(
            double twist,
            double baseX,
            double baseY,
            double viewX,
            double viewY
        )
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            GeoPoint2 was = Paper(document, handle);

            // AutoCAD 2021 (SCALE of the viewport by 2): the frame 400 by 200, the scale still 1:50 so its view is 10 000 high, and
            // the view's centre moved by the step of the frame's centre over the scale: about the corner (10, 10) the centre goes
            // from (110, 60) to (210, 110), 100 and 50 over 0.02 further; about its own centre it stays.
            Run(session, "SCALE", new[] { handle }, Enter, P(baseX, baseY), "2");

            DxfViewport viewport = Viewport(document, handle);
            Near(new GeoPoint3(baseX + 2.0 * (110.0 - baseX), baseY + 2.0 * (60.0 - baseY), 0), viewport.CenterPoint);
            Assert.Equal(400.0, viewport.Width, 9);
            Assert.Equal(200.0, viewport.Height, 9);
            Assert.Equal(0.02, viewport.CustomScale, 12);
            Assert.Equal(10000.0, viewport.ViewHeight, 6);
            Assert.Equal(viewX, viewport.ViewCenter.X, 6);
            Assert.Equal(viewY, viewport.ViewCenter.Y, 6);
            AssertStill(was, Paper(document, handle));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void AVertexOfAClipBoundaryDragged_LeavesTheModelWhereItWasOnTheSheet(double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            Run(session, P(30, 30), P(190, 30), P(190, 90), P(30, 90), Enter);
            string border = Viewport(document, handle).NonRectClipEntity.Handle;
            GeoPoint2 was = Paper(document, handle);

            // AutoCAD 2021 (STRETCH of the boundary's corner, 15 right and 10 up): the frame grows, the view's centre follows it.
            Assert.True(document.GripDrag(border, 2, P(205, 100)).Succeeded);

            DxfViewport viewport = Viewport(document, handle);
            Assert.Equal(175.0, viewport.Width, 9);
            Assert.Equal(70.0, viewport.Height, 9);
            Assert.Equal(0.02, viewport.CustomScale, 12);
            AssertStill(was, Paper(document, handle));
        }
    }
}
