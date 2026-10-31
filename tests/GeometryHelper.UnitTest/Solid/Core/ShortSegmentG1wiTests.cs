using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A polyline cut at a distance starts every piece where the last ended, on the chain; a point beside a short side of a
    /// polyline, a polygon or a face is given its closest point on the side as it is; and a segment or a leg shorter than the
    /// global tolerance is cut where it crosses at the tolerance asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The corner is the polyline from (0, 0, 0) to (10, 0, 0) and on to (10, 10, 0), the hairpin the polyline from (0, 0, 0)
    /// to (10, 0, 0), on to (10, 0.006, 0) and back to (0, 0.006, 0); each is cut at a hundredth.
    /// </para>
    /// <para>
    /// The box is 10 by 10 by 10 standing under the floor, its top at z = 0 from (0, 0) to (10, 10), and the square is that
    /// top. The segment from (5, 5, 0.0004) to (5, 5, -0.0005) is 0.0009 long, shorter than the global thousandth and longer
    /// than the billionth it is asked at, and the polyline from (2, 5, 0.0544) down to (5, 5, 0.0004), through the top to
    /// (5, 5, -0.0005) and on to (8, 5, -0.0545) crosses the top by that leg. A polyline keeps such a leg only where the
    /// library made it, as its own cuts do; the public constructor drops it at the global tolerance, so it is built here as
    /// the library builds one. No test sets the global tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1wiTests
    {
        private static readonly Tolerance Billionth = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static readonly GeoPoint3 Beside = new GeoPoint3(10.03, 0.003, 0);

        private static GeoPolyline3 Corner() => new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0));

        private static GeoPolyline3 Hairpin()
            => new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0.006, 0), new GeoPoint3(0, 0.006, 0));

        private static GeoPolygon3 Polygon()
            => new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0.006, 0), new GeoPoint3(0, 10, 0));

        private static GeoObb3 Box() => new GeoObb3(new GeoPoint3(5, 5, -5), 10, 10, 10);

        private static GeoPolygon3 Top() => new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

        private static GeoLine3 Segment() => new GeoLine3(new GeoPoint3(5, 5, 0.0004), new GeoPoint3(5, 5, -0.0005));

        private static GeoPolyline3 Stepping()
            => new GeoPolyline3(new[] { new GeoPoint3(2, 5, 0.0544), new GeoPoint3(5, 5, 0.0004), new GeoPoint3(5, 5, -0.0005), new GeoPoint3(8, 5, -0.0545) }, true);

        private static void AssertAt(double x, double y, double z, GeoPoint3 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
            Assert.Equal(z, actual.Z, 12);
        }

        [Fact]
        public void ACutWithinTheToleranceBeforeACorner_StartsTheNextPieceAtTheCorner()
        {
            // The next piece started at (10, -0.005, 0), the cut carried back along the next leg, off the polyline.
            GeoPolyline3[] pieces = Corner().SplitAtDistances(new[] { 9.995 }, Hundredth);

            Assert.Equal(2, pieces.Length);
            AssertAt(10, 0, 0, pieces[0].EndPoint);
            AssertAt(10, 0, 0, pieces[1].StartPoint);
        }

        [Fact]
        public void ACutWithinTheToleranceAfterACorner_StartsTheNextPieceAtTheCorner()
        {
            // The first piece ended at the corner and the next started 0.005 past it, at (10, 0.005, 0).
            GeoPolyline3[] pieces = Corner().SplitAtDistances(new[] { 10.005 }, Hundredth);

            Assert.Equal(2, pieces.Length);
            AssertAt(10, 0, 0, pieces[0].EndPoint);
            AssertAt(10, 0, 0, pieces[1].StartPoint);
        }

        [Fact]
        public void ACutOnAShortLeg_StartsTheNextPieceWhereTheLastEnded()
        {
            // The cut 0.003 up the leg 0.006 long started the next piece at (10.003, 0.006, 0), beside the polyline.
            GeoPolyline3[] pieces = Hairpin().SplitAtDistances(new[] { 10.003 }, Hundredth);

            Assert.Equal(2, pieces.Length);
            AssertAt(10, 0, 0, pieces[0].EndPoint);
            AssertAt(10, 0, 0, pieces[1].StartPoint);
            AssertAt(0, 0.006, 0, pieces[1].EndPoint);
        }

        [Fact]
        public void APolylineCutByABodyThroughItsShortLeg_StartsThePieceInsideOnTheChain()
        {
            // At a hundredth the leg from (5, 5, 0.004) to (5, 5, -0.005) crosses the top; the piece inside started at
            // (4.995, 5, -0.0049), carried back along the last leg, 0.005 off the polyline.
            var polyline = new GeoPolyline3(new GeoPoint3(2, 5, 0.054), new GeoPoint3(5, 5, 0.004), new GeoPoint3(5, 5, -0.005), new GeoPoint3(8, 5, -0.055));

            Assert.True(polyline.TrySplitBy(Box().ToSolid(), out GeoPolyline3[] inside, out GeoPolyline3[] outside, Hundredth));
            AssertAt(5, 5, 0.004, Assert.Single(inside).StartPoint);
            AssertAt(5, 5, 0.004, Assert.Single(outside).EndPoint);
        }

        [Fact]
        public void APieceShorterThanTheGlobalTolerance_IsKeptAtTheToleranceAsked()
        {
            // The piece 0.0005 long between the cuts threw: it was rebuilt at the global thousandth, a single point.
            var polyline = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            GeoPolyline3[] pieces = polyline.SplitAtDistances(new[] { 5, 5.0005 }, Billionth);

            Assert.Equal(3, pieces.Length);
            Assert.Equal(0.0005, pieces[1].Length, 12);
        }

        [Fact]
        public void TheClosestPointOfAPolylineOrAPolygon_BesideItsShortSide_IsOnTheSideAsItIs()
        {
            // The foot (10, 0.003, 0), 0.03 away, where it was the side's start, 0.0301496 away.
            AssertAt(10, 0.003, 0, Projection3.ProjectToPolyline(Hairpin(), Beside, Hundredth));
            AssertAt(10, 0.003, 0, Projection3.ProjectToPolygonBoundary(Polygon(), Beside, Hundredth));
            AssertAt(10, 0.003, 0, Projection3.ProjectToPolygon(Polygon(), Beside, Hundredth));
        }

        [Fact]
        public void TheClosestPointOfAPolylineOrAPolygon_BesideASideShorterThanTheGlobalTolerance_IsOnThatSide()
        {
            // These take no tolerance and read the global thousandth: the side 0.0006 long offered its start.
            var polyline = new GeoPolyline3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0.0006, 0), new GeoPoint3(0, 0.0006, 0) }, true);
            var polygon = new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0.0006, 0), new GeoPoint3(0, 10, 0) }, Billionth);
            var point = new GeoPoint3(10.03, 0.0003, 0);

            AssertAt(10, 0.0003, 0, polyline.GetClosestPointOnBoundary(point));
            AssertAt(10, 0.0003, 0, point.GetClosestPointOnBoundary(polyline));
            AssertAt(10, 0.0003, 0, polygon.GetClosestPointOnBoundary(point));
            AssertAt(10, 0.0003, 0, point.GetClosestPointOnBoundary(polygon));
        }

        [Fact]
        public void TheClosestPointOfAFace_BesideAShortSideOfItsOutlineOrOfAHole_IsOnTheSideAsItIs()
        {
            // The hole runs round a needle of material 0.006 wide from the left, its tip the side at x = 10, and the point
            // stands in the hole 0.03 off the tip; the outline is the polygon.
            var needle = new GeoPolygon3(
                new GeoPoint3(-5, -5, 0), new GeoPoint3(20, -5, 0), new GeoPoint3(20, 5, 0), new GeoPoint3(-5, 5, 0),
                new GeoPoint3(-5, 0.006, 0), new GeoPoint3(10, 0.006, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(-5, 0, 0));
            var square = new GeoPolygon3(new GeoPoint3(-20, -20, 0), new GeoPoint3(30, -20, 0), new GeoPoint3(30, 30, 0), new GeoPoint3(-20, 30, 0));
            var holed = new GeoFace3(square, new[] { needle });

            AssertAt(10, 0.003, 0, new GeoFace3(Polygon()).GetClosestPointOnBoundary(Beside, Hundredth));
            AssertAt(10, 0.003, 0, Projection3.ProjectToFace(new GeoFace3(Polygon()), Beside, Hundredth));
            AssertAt(10, 0.003, 0, holed.GetClosestPointOnBoundary(Beside, Hundredth));
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_IsCutAtAPointOnIt_AndByAPlaneAPolygonOrAFace_WhereItPasses()
        {
            // It was not cut: the place was measured along it at the global thousandth, at its start.
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));

            Assert.True(Segment().TrySplitBy(new GeoPoint3(5, 5, 0), out GeoLine3[] pieces, Billionth));
            AssertAt(5, 5, 0, pieces[0].EndPoint);
            Assert.True(Segment().TrySplitBy(plane, out pieces, Billionth));
            AssertAt(5, 5, 0, pieces[0].EndPoint);
            Assert.True(Segment().TrySplitBy(Top(), out pieces, Billionth));
            AssertAt(5, 5, 0, pieces[0].EndPoint);
            Assert.True(Segment().TrySplitBy(new GeoFace3(Top()), out pieces, Billionth));
            AssertAt(5, 5, 0, pieces[0].EndPoint);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_IsCutByAnOrientedBoxWhereItPasses()
        {
            Assert.True(Segment().TrySplitBy(Box(), out GeoLine3[] inside, out GeoLine3[] outside, Billionth));
            AssertAt(5, 5, 0, Assert.Single(inside).StartPoint);
            AssertAt(5, 5, 0, Assert.Single(outside).EndPoint);

            Assert.True(Segment().TrySplitBy(new[] { Box() }, out inside, out outside, Billionth));
            AssertAt(5, 5, 0, Assert.Single(inside).StartPoint);
            AssertAt(5, 5, 0, Assert.Single(outside).EndPoint);
        }

        [Fact]
        public void APolylineWithALegShorterThanTheGlobalTolerance_IsCutWhereTheLegPasses()
        {
            // It was cut at the leg's start, (5, 5, 0.0004), the place measured along the leg at the global thousandth.
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));

            Assert.True(Stepping().TrySplitBy(plane, out GeoPolyline3[] pieces, Billionth));
            AssertAt(5, 5, 0, pieces[1].StartPoint);
            Assert.True(Stepping().TrySplitBy(Top(), out pieces, Billionth));
            AssertAt(5, 5, 0, pieces[1].StartPoint);
            Assert.True(Stepping().TrySplitBy(new GeoFace3(Top()), out pieces, Billionth));
            AssertAt(5, 5, 0, pieces[1].StartPoint);

            Assert.True(Stepping().TrySplitBy(Box(), out GeoPolyline3[] inside, out GeoPolyline3[] outside, Billionth));
            AssertAt(5, 5, 0, Assert.Single(inside).StartPoint);
            AssertAt(5, 5, 0, Assert.Single(outside).EndPoint);
            Assert.True(Stepping().TrySplitBy(new[] { Box() }, out inside, out outside, Billionth));
            AssertAt(5, 5, 0, Assert.Single(inside).StartPoint);
            AssertAt(5, 5, 0, Assert.Single(outside).EndPoint);
        }
    }
}
