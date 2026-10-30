using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment you give, no longer than the point tolerance, is read as its start where it does not truly meet the other
    /// shape, and is in contact where it does, crossing a side or running into an arc, however short: as space reads it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every test passes a tolerance of a thousandth. The stub pointing at a shape is 0.0009 long, its start 0.0015 off the
    /// shape and its end 0.0006 off: read as its start it is 0.0015 away and touches nothing, where measured by its end it
    /// was 0.0006 away and touching. The stub crossing a side has its start 0.0004 off outside and its end 0.0005 inside:
    /// it is nought away and meets the side where it crosses, where read as its start it was 0.0004 away and met nothing.
    /// </para>
    /// <para>
    /// The segment along the x axis runs from the origin to (10, 0); the square is 10 by 10 on the origin; the arc is the
    /// upper half of the circle of radius 50 about the origin; the ellipse is 60 by 40 about the origin, its major axis
    /// along x. No test sets the global tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1wcTests
    {
        private static readonly Tolerance Thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);

        private static GeoLine2 AlongX() => new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

        private static GeoPolygon2 Square()
            => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

        private static GeoArc2 UpperHalf() => new GeoArc2(new GeoPoint2(0, 0), 50, 0, Math.PI);

        /// <summary>The stub pointing down at (5, 0), stopping 0.0006 short of it.</summary>
        private static GeoLine2 Towards() => new GeoLine2(new GeoPoint2(5, 0.0015), new GeoPoint2(5, 0.0006));

        /// <summary>The stub crossing the x axis at (5, 0).</summary>
        private static GeoLine2 Across() => new GeoLine2(new GeoPoint2(5, 0.0004), new GeoPoint2(5, -0.0005));

        private static void AssertAt(double x, double y, GeoPoint2 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
        }

        [Fact]
        public void AStubPointingAtASegmentAndStoppingShortOfIt_IsReadAsItsStart()
        {
            GeoLine2 stub = Towards();
            GeoLine2 fromSegment = AlongX().GetShortestLineTo(stub, Thousandth);

            Assert.Equal(0.0015, AlongX().DistanceTo(stub, Thousandth), 12);
            Assert.Equal(0.0015, stub.DistanceTo(AlongX(), Thousandth), 12);
            Assert.False(AlongX().CollidesWith(stub, Thousandth));
            Assert.False(stub.CollidesWith(AlongX(), Thousandth));
            AssertAt(5, 0, fromSegment.StartPoint);
            AssertAt(5, 0.0015, fromSegment.EndPoint);
            Assert.Equal(0.0015, stub.GetShortestLineTo(AlongX(), Thousandth).Length, 12);
        }

        [Fact]
        public void AStubCrossingASegment_IsNoughtFromIt_AndMeetsItWhereItCrosses()
        {
            GeoLine2 stub = Across();
            GeoLine2 fromSegment = AlongX().GetShortestLineTo(stub, Thousandth);

            Assert.Equal(0.0, AlongX().DistanceTo(stub, Thousandth));
            Assert.Equal(0.0, stub.DistanceTo(AlongX(), Thousandth));
            Assert.Equal(0.0, fromSegment.Length);
            AssertAt(5, 0, fromSegment.StartPoint);
            Assert.True(AlongX().TryIntersectWith(stub, out GeoPoint2 crossing, Thousandth));
            AssertAt(5, 0, crossing);
            Assert.True(stub.TryIntersectWith(AlongX(), out crossing, Thousandth));
            AssertAt(5, 0, crossing);
        }

        [Fact]
        public void AStubNeitherCrossingNorReachingASegment_MeetsItNowhere()
        {
            // Its start is 0.0008 off and its end 0.0002: it collides, as its start does, and crosses nothing.
            var stub = new GeoLine2(new GeoPoint2(5, 0.0008), new GeoPoint2(5, 0.0002));

            Assert.Equal(0.0008, AlongX().DistanceTo(stub, Thousandth), 12);
            Assert.True(AlongX().CollidesWith(stub, Thousandth));
            Assert.False(AlongX().TryIntersectWith(stub, out _, Thousandth));
        }

        [Fact]
        public void AStubPointingAtASideOfASquare_IsReadAsItsStart()
        {
            var stub = new GeoLine2(new GeoPoint2(5, -0.0015), new GeoPoint2(5, -0.0006));
            GeoLine2 fromSquare = Square().GetShortestLineTo(stub, Thousandth);

            Assert.False(Square().CollidesWith(stub, Thousandth));
            Assert.False(stub.CollidesWith(Square(), Thousandth));
            Assert.Equal(0.0015, fromSquare.Length, 12);
            AssertAt(5, -0.0015, fromSquare.EndPoint);
            Assert.Equal(0.0015, stub.GetShortestLineTo(Square(), Thousandth).Length, 12);
            Assert.Equal(0.0015, new GeoFace2(Square()).DistanceTo(stub, Thousandth), 12);
            Assert.Equal(0.0015, new GeoPolygonArc2(Square()).DistanceTo(stub, Thousandth), 12);
        }

        [Fact]
        public void AStubCrossingASideOfASquare_MeetsItWhereItCrosses()
        {
            var stub = new GeoLine2(new GeoPoint2(5, -0.0004), new GeoPoint2(5, 0.0005));
            GeoLine2 fromSquare = Square().GetShortestLineTo(stub, Thousandth);

            GeoPoint2[] points = Square().GetIntersections(stub, Thousandth);
            Assert.Single(points);
            AssertAt(5, 0, points[0]);
            Assert.Single(new GeoPolyline2(Square().Vertices).GetIntersections(stub, Thousandth));
            Assert.Single(new GeoRectangle2(0, 0, 10, 10).GetIntersections(stub, Thousandth));
            Assert.Single(new GeoPolygonArc2(Square()).GetIntersections(stub, Thousandth));
            Assert.Single(new GeoFace2(Square()).GetIntersections(stub, Thousandth));
            Assert.Equal(0.0, fromSquare.Length);
            AssertAt(5, 0, fromSquare.StartPoint);
        }

        [Fact]
        public void AStubPointingAtAnArcAndStoppingShortOfIt_IsReadAsItsStart()
        {
            var stub = new GeoLine2(new GeoPoint2(0, 50.0015), new GeoPoint2(0, 50.0006));

            Assert.Equal(0.0015, UpperHalf().DistanceTo(stub, Thousandth), 12);
            Assert.False(UpperHalf().CollidesWith(stub, Thousandth));
            Assert.Equal(0.0015, UpperHalf().GetShortestLineTo(stub, Thousandth).Length, 12);
            Assert.Equal(0.0015, stub.GetShortestLineTo(UpperHalf(), Thousandth).Length, 12);
        }

        [Fact]
        public void AStubRunningIntoAnArc_IsJoinedToItWhereItCrossesTheRim()
        {
            // It used to be joined at its start, 0.0004 outside the rim.
            var stub = new GeoLine2(new GeoPoint2(0, 50.0004), new GeoPoint2(0, 49.9995));
            GeoLine2 fromArc = UpperHalf().GetShortestLineTo(stub, Thousandth);

            Assert.Equal(0.0, UpperHalf().DistanceTo(stub, Thousandth));
            Assert.Equal(0.0, fromArc.Length, 12);
            AssertAt(0, 50, fromArc.StartPoint);
            AssertAt(0, 50, stub.GetShortestLineTo(new GeoCircle2(new GeoPoint2(0, 0), 50), Thousandth).EndPoint);
        }

        [Fact]
        public void AStubPointingAtAnEllipseOrASpline_IsReadAsItsStart()
        {
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 0), 30, 20);
            var atTheTop = new GeoLine2(new GeoPoint2(0, 20.0015), new GeoPoint2(0, 20.0006));
            GeoSpline2 spline = GeoSpline2.FromPolyline(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)));

            Assert.Equal(0.0015, ellipse.DistanceTo(atTheTop, Thousandth), 9);
            Assert.False(ellipse.CollidesWith(atTheTop, Thousandth));
            Assert.Equal(0.0015, ellipse.GetShortestLineTo(atTheTop, Thousandth).Length, 9);
            Assert.Equal(0.0015, spline.DistanceTo(Towards(), Thousandth), 12);
            Assert.False(spline.CollidesWith(Towards(), Thousandth));
            Assert.Equal(0.0015, spline.GetShortestLineTo(Towards(), Thousandth).Length, 12);
        }

        [Fact]
        public void AStraightEdgeAsShortAsAStub_IsReadAsOneIs()
        {
            var edge = new GeoEdge2(new GeoPoint2(5, 0.0015), new GeoPoint2(5, 0.0006));

            Assert.Equal(0.0015, AlongX().DistanceTo(edge, Thousandth), 12);
            Assert.False(AlongX().CollidesWith(edge, Thousandth));
            Assert.Equal(0.0015, new GeoFace2(Square()).DistanceTo(new GeoEdge2(new GeoPoint2(5, -0.0015), new GeoPoint2(5, -0.0006)), Thousandth), 12);
            Assert.Single(AlongX().GetIntersections(new GeoEdge2(Across().StartPoint, Across().EndPoint), Thousandth));
        }
    }
}
