using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// In space, a segment you give no longer than the point tolerance meets a shape where it truly meets it, and otherwise at
    /// its start where that touches the shape, as its distance reads it; and a side of a shape no longer than the tolerance is
    /// joined to as the side it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stubs are asked at a tolerance of a thousandth. The stub pointing at a shape is 0.0009 long, its start 0.0015 off and
    /// its end 0.0006 off: it meets nothing, where a crossing was given at its end. The stub passing through a surface or
    /// across a segment has its start 0.0004 off on one side and its end 0.0005 off on the other: it meets the shape where it
    /// passes, where it was given nothing or a point off it. The stub standing over a shape, its start 0.0008 off and its end
    /// 0.0002, meets it at its start.
    /// </para>
    /// <para>
    /// The shapes with a short side are asked at a hundredth, that side longer than the default tolerance the shapes are
    /// built with. The chain runs from (-7, -7, 0) to the origin, on to (0.008, 0, 0) and down to (7, -7, 0); the point
    /// (0.006, 0.02, 0) stands 0.02 off its short side, and 0.0201 off the corner (0.008, 0, 0), which is what it was
    /// joined to while the short side was read as its start. No test sets the global tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1weTests
    {
        private static readonly Tolerance Thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);

        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static GeoPolygon3 Square()
            => new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

        private static GeoPlane3 Floor() => new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));

        private static GeoSolid3 Box() => new GeoObb3(new GeoPoint3(5, 5, -5), 10, 10, 10).ToSolid();

        private static GeoLine3 AlongX() => new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

        /// <summary>The stub pointing down at (x, y, 0), stopping 0.0006 short of it.</summary>
        private static GeoLine3 Down(double x, double y) => new GeoLine3(new GeoPoint3(x, y, 0.0015), new GeoPoint3(x, y, 0.0006));

        /// <summary>The stub passing down through (x, y, 0).</summary>
        private static GeoLine3 Through(double x, double y) => new GeoLine3(new GeoPoint3(x, y, 0.0004), new GeoPoint3(x, y, -0.0005));

        /// <summary>The stub standing over (x, y, 0), from 0.0008 above to 0.0002 above.</summary>
        private static GeoLine3 Over(double x, double y) => new GeoLine3(new GeoPoint3(x, y, 0.0008), new GeoPoint3(x, y, 0.0002));

        private static GeoPoint3[] ChainCorners()
            => new[] { new GeoPoint3(-7, -7, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0.008, 0, 0), new GeoPoint3(7, -7, 0) };

        private static void AssertAt(double x, double y, double z, GeoPoint3 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
            Assert.Equal(z, actual.Z, 12);
        }

        [Fact]
        public void AStubPassingThroughASquare_MeetsItWhereItPasses()
        {
            GeoLine3 stub = Through(5, 5);

            GeoPoint3[] points = Square().GetIntersections(stub, Thousandth);

            Assert.Single(points);
            AssertAt(5, 5, 0, points[0]);
            Assert.True(stub.TryIntersectWith(Square(), out GeoPoint3 crossing, Thousandth));
            AssertAt(5, 5, 0, crossing);
            Assert.True(stub.TryIntersectWith(Floor(), out crossing, Thousandth));
            AssertAt(5, 5, 0, crossing);
            Assert.True(new GeoFace3(Square()).TryIntersectWith(stub, out crossing, Thousandth));
            AssertAt(5, 5, 0, crossing);
        }

        [Fact]
        public void AStubPassingThroughTheTopOfABox_MeetsTheBodyWhereItPasses()
        {
            GeoPoint3[] points = Box().GetIntersections(Through(5, 5), Thousandth);

            Assert.Single(points);
            AssertAt(5, 5, 0, points[0]);
        }

        [Fact]
        public void AStubPointingAtAShape_StoppingShortOfIt_MeetsItNowhere()
        {
            GeoLine3 stub = Down(5, 5);

            Assert.Empty(stub.GetIntersections(Floor(), Thousandth));
            Assert.Empty(Square().GetIntersections(stub, Thousandth));
            Assert.Empty(Box().GetIntersections(stub, Thousandth));
            Assert.Empty(new GeoRay3(new GeoPoint3(0, 5, 0), new GeoVector3(1, 0, 0)).GetIntersections(stub, Thousandth));
        }

        [Fact]
        public void AStubStandingOverAShapeWithinTheTolerance_MeetsItAtItsStart()
        {
            GeoLine3 stub = Over(5, 5);

            GeoPoint3[] onSquare = Square().GetIntersections(stub, Thousandth);

            Assert.Single(onSquare);
            AssertAt(5, 5, 0.0008, onSquare[0]);
            Assert.True(stub.TryIntersectWith(Floor(), out GeoPoint3 onFloor, Thousandth));
            AssertAt(5, 5, 0.0008, onFloor);
            AssertAt(5, 5, 0.0008, Assert.Single(stub.GetIntersections(Floor(), Thousandth)));
        }

        [Fact]
        public void AStubCrossingASegment_MeetsItWhereItCrosses_AndOneBesideIt_AtItsStart()
        {
            Assert.True(Through(5, 0).TryIntersectWith(AlongX(), out GeoPoint3 crossing, Thousandth));
            AssertAt(5, 0, 0, crossing);

            Assert.True(Over(5, 0).TryIntersectWith(AlongX(), out GeoPoint3 touch, Thousandth));
            AssertAt(5, 0, 0.0008, touch);

            Assert.False(Down(5, 0).TryIntersectWith(AlongX(), out _, Thousandth));
        }

        [Fact]
        public void AStubPointingAtAnArc_StoppingShortOfIt_MeetsItNowhere_AndOneStandingOverIt_AtItsStart()
        {
            // The arc through (-3, 0, -1), the origin and (3, 0, -1), in the plane y = 0, the origin at its top.
            GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(-3, 0, -1), new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, -1), Thousandth);

            Assert.Empty(arc.GetIntersections(Down(0, 0), Thousandth));
            AssertAt(0, 0, 0.0008, Assert.Single(arc.GetIntersections(Over(0, 0), Thousandth)));
        }

        [Fact]
        public void APolylineWithASideShorterThanTheTolerance_IsJoinedToTheSideAsItIs()
        {
            var chain = new GeoPolyline3(ChainCorners());
            var point = new GeoPoint3(0.006, 0.02, 0);
            var segment = new GeoLine3(new GeoPoint3(0.006, 0.02, -5), new GeoPoint3(0.006, 0.02, 5));

            Assert.Equal(0.02, chain.GetShortestLineTo(point, Hundredth).Length, 12);
            Assert.Equal(0.02, Distance3.DistanceTo(chain, point, Hundredth), 12);
            Assert.Equal(0.02, chain.GetShortestLineTo(segment, Hundredth).Length, 12);
            Assert.Equal(chain.DistanceTo(segment, Hundredth), chain.GetShortestLineTo(segment, Hundredth).Length, 12);
        }

        [Fact]
        public void ARegionWithASideShorterThanTheTolerance_IsJoinedToTheSideAsItIs()
        {
            var polygon = new GeoPolygon3(ChainCorners());
            var sliver = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(0.008, 0, 0), new GeoPoint3(0.004, -10, 0));
            var point = new GeoPoint3(0.006, 0.02, 0);
            var segment = new GeoLine3(new GeoPoint3(0.006, 0.02, -5), new GeoPoint3(0.006, 0.02, 5));

            Assert.Equal(0.02, polygon.GetShortestLineTo(point, Hundredth).Length, 12);
            Assert.Equal(0.02, polygon.GetShortestLineTo(segment, Hundredth).Length, 12);
            Assert.Equal(0.02, new GeoFace3(polygon).GetShortestLineTo(segment, Hundredth).Length, 12);
            Assert.Equal(0.02, sliver.GetShortestLineTo(segment, Hundredth).Length, 12);
        }
    }
}
