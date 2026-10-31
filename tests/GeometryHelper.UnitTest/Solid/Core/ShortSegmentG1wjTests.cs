using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A polyline cut at a distance ends its last piece where the polyline ends, however short its last leg; and the place
    /// along a polyline or round a polygon of a point beside a short side is on the side as it is, not at its start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hook is the polyline from (0, 0, 0) to (10, 0, 0) and on to (10, 0.006, 0), its last leg 0.006 long; the hairpin
    /// goes on from there back to (0, 0.006, 0); the polygon is (0, 0, 0), (10, 0, 0), (10, 0.006, 0), (0, 10, 0). Each is
    /// asked at a hundredth, so the leg or side 0.006 long is no longer than the tolerance, and the point (10.03, 0.003, 0)
    /// lies 0.03 beside its middle.
    /// </para>
    /// <para>
    /// The small hairpin is the hairpin with its short leg 0.0006 long, shorter than the global thousandth, built as the
    /// library builds one, since the public constructor drops such a leg. No test sets the global tolerance.
    /// </para>
    /// </remarks>
    public class ShortSegmentG1wjTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static readonly GeoPoint3 Beside = new GeoPoint3(10.03, 0.003, 0);

        private static GeoPolyline3 Hook() => new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0.006, 0));

        private static GeoPolyline3 Hairpin()
            => new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0.006, 0), new GeoPoint3(0, 0.006, 0));

        private static GeoPolyline3 SmallHairpin()
            => new GeoPolyline3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0.0006, 0), new GeoPoint3(0, 0.0006, 0) }, true);

        private static GeoPolygon3 Polygon()
            => new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0.006, 0), new GeoPoint3(0, 10, 0));

        private static void AssertAt(double x, double y, double z, GeoPoint3 actual)
        {
            Assert.Equal(x, actual.X, 12);
            Assert.Equal(y, actual.Y, 12);
            Assert.Equal(z, actual.Z, 12);
        }

        [Fact]
        public void APolylineWithAShortLastLegCutAtADistance_EndsItsLastPieceWhereThePolylineEnds()
        {
            // The last piece ended at the corner (10, 0, 0) and the end was lost.
            GeoPolyline3[] pieces = Hook().SplitAtDistances(new[] { 5.0 }, Hundredth);

            Assert.Equal(2, pieces.Length);
            AssertAt(5, 0, 0, pieces[0].EndPoint);
            AssertAt(5, 0, 0, pieces[1].StartPoint);
            AssertAt(10, 0.006, 0, pieces[1].EndPoint);
            Assert.Equal(2, pieces[1].VertexCount);
        }

        [Fact]
        public void ACutLeavingOnlyAShortLastLegAfterIt_IsNotMade_AndThePolylineComesBackWhole()
        {
            // The cut lands on the corner and leaves only the leg 0.006 long; it came back as one piece ending at the corner.
            GeoPolyline3 hook = Hook();

            GeoPolyline3[] pieces = hook.SplitAtDistances(new[] { 9.995 }, Hundredth);

            Assert.Single(pieces);
            Assert.Equal(3, pieces[0].VertexCount);
            AssertAt(10, 0.006, 0, pieces[0].EndPoint);
            Assert.False(hook.TrySplitAtDistance(9.995, out _, Hundredth));
        }

        [Fact]
        public void APolylineWithAShortLastLegCutByAPlane_EndsItsLastPieceWhereThePolylineEnds()
        {
            // The cuts by a plane, a body or a box go through the same cutting at a distance.
            Assert.True(Hook().TrySplitBy(new GeoPlane3(new GeoPoint3(5, 0, 0), new GeoVector3(1, 0, 0)), out GeoPolyline3[] pieces, Hundredth));

            Assert.Equal(2, pieces.Length);
            AssertAt(10, 0.006, 0, pieces[1].EndPoint);
        }

        [Fact]
        public void ThePlaceAlongAPolyline_OfAPointBesideAShortLeg_IsOnTheLegAsItIs()
        {
            // 10.003 along, 0.003 along the leg 0.006 long, halfway; it was 10 along, at the start of that leg.
            GeoPolyline3 hairpin = Hairpin();

            Assert.Equal(10.003, Parametrization3.GetDistanceAtPoint(hairpin, Beside, Hundredth), 12);
            Assert.Equal(0.5, Parametrization3.GetParameterAtPoint(hairpin, Beside, Hundredth), 12);
        }

        [Fact]
        public void ThePlaceAlongAPolyline_OfAPointBesideALegShorterThanTheGlobalTolerance_IsOnTheLegAsItIs()
        {
            // 10.0003 along, where the global thousandth read the leg 0.0006 long as its start, 10 along.
            GeoPolyline3 hairpin = SmallHairpin();
            var beside = new GeoPoint3(10.003, 0.0003, 0);

            Assert.Equal(10.0003, hairpin.GetDistanceAtPoint(beside), 12);
            Assert.Equal(0.5, hairpin.GetParameterAtPoint(beside), 12);
        }

        [Fact]
        public void ThePlaceRoundAPolygon_OfAPointBesideAShortSide_IsOnTheSideAsItIs()
        {
            // 10.003 round, 0.003 along the side 0.006 long; it was 10 round, at the start of that side.
            GeoPolygon3 polygon = Polygon();

            Assert.Equal(10.003, polygon.GetDistanceAtPoint(Beside, Hundredth), 12);
            Assert.Equal(10.003 / polygon.Length, polygon.GetParameterAtPoint(Beside, Hundredth), 12);
        }

        [Fact]
        public void APolygonCutByAChainFromBesideAShortSide_MeetsItsOutlineAtTheFootOfTheChain()
        {
            // The chain from (10.001, 0.003, 0) to (0, 5, 0) starts 0.001 beside the side 0.006 long; both pieces met the
            // outline at the side's start (10, 0, 0), 0.003 from the foot.
            var chain = new GeoPolyline3(new GeoPoint3(10.001, 0.003, 0), new GeoPoint3(0, 5, 0));

            Assert.True(Polygon().TrySplitBy(chain, out GeoPolygon3[] pieces, Hundredth));

            Assert.Equal(2, pieces.Length);
            foreach (GeoPolygon3 piece in pieces)
            {
                Assert.Contains(piece.Vertices, corner => corner.DistanceTo(new GeoPoint3(10, 0.003, 0)) <= 1E-12);
                Assert.DoesNotContain(piece.Vertices, corner => corner.DistanceTo(new GeoPoint3(10, 0, 0)) <= 1E-12);
            }
        }
    }
}
