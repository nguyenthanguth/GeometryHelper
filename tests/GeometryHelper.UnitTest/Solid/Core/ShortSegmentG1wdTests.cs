using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// In space, a segment you give no longer than the point tolerance is joined to a shape as its start where it does not
    /// truly meet it, and where it meets it however short, as its distance already reads it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every test passes a tolerance of a thousandth. The stub pointing at a shape is 0.0009 long, its start 0.0015 off and
    /// its end 0.0006 off: it is joined from its start by a line 0.0015 long, where it was joined from its end by one 0.0006
    /// long. The stub passing through a surface or across a segment has its start 0.0004 off on one side and its end 0.0005
    /// off on the other: it is joined by a line of no length where it passes, where it was joined from its start by one
    /// 0.0004 long.
    /// </para>
    /// <para>
    /// The square is 10 by 10 in the floor with a corner at the origin, the box 10 by 10 by 10 below it, and the ball of
    /// radius 5 below it touches it at (5, 5, 0). No test sets the global tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1wdTests
    {
        private static readonly Tolerance Thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);

        private static GeoPolygon3 Square()
            => new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

        private static GeoTriangle3 Triangle() => new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0));

        private static GeoSolid3 Box() => new GeoObb3(new GeoPoint3(5, 5, -5), 10, 10, 10).ToSolid();

        private static GeoLine3 AlongX() => new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

        /// <summary>The stub pointing down at (x, y, 0), stopping 0.0006 short of it.</summary>
        private static GeoLine3 Down(double x, double y) => new GeoLine3(new GeoPoint3(x, y, 0.0015), new GeoPoint3(x, y, 0.0006));

        /// <summary>The stub passing down through (x, y, 0).</summary>
        private static GeoLine3 Through(double x, double y) => new GeoLine3(new GeoPoint3(x, y, 0.0004), new GeoPoint3(x, y, -0.0005));

        private static void AssertAt(double x, double y, double z, GeoPoint3 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
            Assert.Equal(z, actual.Z, 12);
        }

        [Fact]
        public void AStubPointingDownAtAFlatShape_IsJoinedFromItsStart()
        {
            GeoLine3 stub = Down(2, 2);
            GeoLine3 fromSquare = Square().GetShortestLineTo(stub, Thousandth);

            Assert.Equal(0.0015, fromSquare.Length, 12);
            AssertAt(2, 2, 0.0015, fromSquare.EndPoint);
            Assert.Equal(0.0015, stub.GetShortestLineTo(Square(), Thousandth).Length, 12);
            Assert.Equal(Square().DistanceTo(stub, Thousandth), fromSquare.Length, 12);
            Assert.Equal(0.0015, Triangle().GetShortestLineTo(stub, Thousandth).Length, 12);
            Assert.Equal(0.0015, new GeoFace3(Square()).GetShortestLineTo(stub, Thousandth).Length, 12);
            Assert.Equal(0.0015, Box().GetShortestLineTo(stub, Thousandth).Length, 12);
        }

        [Fact]
        public void AStubPassingThroughAFlatShape_IsJoinedWhereItPassesThrough()
        {
            GeoLine3 stub = Through(2, 2);
            GeoLine3 fromSquare = Square().GetShortestLineTo(stub, Thousandth);

            Assert.Equal(0.0, fromSquare.Length, 12);
            AssertAt(2, 2, 0, fromSquare.StartPoint);
            Assert.Equal(0.0, stub.GetShortestLineTo(Square(), Thousandth).Length, 12);
            Assert.Equal(0.0, Triangle().GetShortestLineTo(stub, Thousandth).Length, 12);
            Assert.Equal(0.0, new GeoFace3(Square()).GetShortestLineTo(stub, Thousandth).Length, 12);
            Assert.Equal(0.0, Box().GetShortestLineTo(stub, Thousandth).Length, 12);
        }

        [Fact]
        public void AStubPointingAtASideOfASquareInItsPlane_IsJoinedFromItsStart()
        {
            // It used to be read as touching the side, by a line of no length.
            var stub = new GeoLine3(new GeoPoint3(10.0015, 5, 0), new GeoPoint3(10.0006, 5, 0));
            GeoLine3 fromSquare = Square().GetShortestLineTo(stub, Thousandth);

            Assert.Equal(0.0015, fromSquare.Length, 12);
            AssertAt(10, 5, 0, fromSquare.StartPoint);
        }

        [Fact]
        public void AStubCrossingASegment_IsJoinedWhereItCrosses()
        {
            GeoLine3 stub = Through(5, 0);
            GeoLine3 fromSegment = AlongX().GetShortestLineTo(stub, Thousandth);

            Assert.Equal(0.0, fromSegment.Length, 12);
            AssertAt(5, 0, 0, fromSegment.StartPoint);
            Assert.Equal(0.0, stub.GetShortestLineTo(AlongX(), Thousandth).Length, 12);
            Assert.Equal(0.0, new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0)).GetShortestLineTo(stub, Thousandth).Length, 12);
            Assert.Equal(0.0, new GeoXLine3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0)).GetShortestLineTo(stub, Thousandth).Length, 12);
        }

        [Fact]
        public void AStubPointingAtARay_IsJoinedFromItsStart()
        {
            var ray = new GeoRay3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0));
            GeoLine3 fromRay = ray.GetShortestLineTo(Down(5, 0), Thousandth);

            Assert.Equal(0.0015, fromRay.Length, 12);
            AssertAt(5, 0, 0.0015, fromRay.EndPoint);
            Assert.Equal(0.0, ray.GetShortestLineTo(Through(5, 0), Thousandth).Length, 12);
        }

        [Fact]
        public void AStubThroughTheSkinOfABall_IsNoughtFromIt()
        {
            var ball = new GeoSphere3(new GeoPoint3(5, 5, -5), 5);
            GeoLine3 stub = Through(5, 5);
            GeoLine3 fromBall = ball.GetShortestLineTo(stub, Thousandth);

            Assert.Equal(0.0, ball.DistanceTo(stub, Thousandth));
            Assert.Equal(0.0, fromBall.Length, 12);
            Assert.True(fromBall.StartPoint.Z <= 0.0);
            Assert.Equal(0.0, ball.DistanceTo(new GeoEdge3(stub.StartPoint, stub.EndPoint), Thousandth));
            Assert.Equal(0.0015, ball.DistanceTo(Down(5, 5), Thousandth), 12);
        }

        [Fact]
        public void AStubPointingAtASpline_IsReadAsItsStart()
        {
            GeoSpline3 spline = GeoSpline3.FromControlPoints(1, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) });
            GeoLine3 stub = Down(5, 0);

            Assert.Equal(0.0015, spline.DistanceTo(stub, Thousandth), 9);
            Assert.False(spline.CollidesWith(stub, Thousandth));
            Assert.Equal(0.0015, spline.GetShortestLineTo(stub, Thousandth).Length, 9);
            Assert.Equal(0.0015, spline.DistanceTo(new GeoEdge3(stub.StartPoint, stub.EndPoint), Thousandth), 9);
        }
    }
}
