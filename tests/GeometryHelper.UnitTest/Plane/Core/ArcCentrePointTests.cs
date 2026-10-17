using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The distance from an arc, an edge, a chain or a loop to a point is the exact one, also for a point near the centre of
    /// the arc. It used to carry a point within the point tolerance of the centre to the arc's start, which is up to twice the
    /// tolerance off, and made a point on a small arc a point off it.
    /// </summary>
    public class ArcCentrePointTests
    {
        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        /// <summary>The lower half of the circle of radius 50 about the origin, from (-50, 0) to (50, 0) through (0, -50).</summary>
        private static GeoArc2 Lower() => new GeoArc2(new GeoPoint2(0, 0), 50, Math.PI, 2 * Math.PI);

        [Fact]
        public void APointNearTheCentreOfAnArc_IsTheExactDistanceFromItEitherWayRound()
        {
            // The point (0, -0.0009) stands 49.9991 from the arc, straight down; it read 50.00000001, the distance to the
            // start, at a tolerance of a thousandth.
            Tolerance t = At(1E-3);
            var point = new GeoPoint2(0, -0.0009);
            GeoArc2 arc = Lower();
            var edge = new GeoEdge2(arc);
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(-60, 0), new GeoPoint2(-50, 0), new GeoPoint2(50, 0) }, new[] { 0.0, 1.0, 0.0 });

            Assert.InRange(arc.DistanceTo(point, t), 49.9991 - 1E-9, 49.9991 + 1E-9);
            Assert.InRange(edge.DistanceTo(point, t), 49.9991 - 1E-9, 49.9991 + 1E-9);
            Assert.InRange(chain.DistanceTo(point, t), 49.9991 - 1E-9, 49.9991 + 1E-9);

            using (Tolerance.Use(t))
            {
                Assert.InRange(point.DistanceTo(arc), 49.9991 - 1E-9, 49.9991 + 1E-9);
                Assert.InRange(point.DistanceTo(chain), 49.9991 - 1E-9, 49.9991 + 1E-9);
            }
        }

        [Fact]
        public void APointNearTheCentreOfACurvedLoopsArc_IsTheExactDistanceFromTheLoopEitherWayRound()
        {
            // The loop is the strip from y = 0 up, its bottom side the upper half of the circle of radius 50, which cuts into
            // the strip: the point (0, 0.0009) is in the cut, 49.9991 from the loop, outside it, and its signed distance is the same.
            Tolerance t = At(1E-3);
            var point = new GeoPoint2(0, 0.0009);
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(50, 0), new GeoPoint2(-50, 0), new GeoPoint2(-50, 1000), new GeoPoint2(50, 1000) },
                new[] { 1.0, 0.0, 0.0, 0.0 });

            Assert.InRange(loop.DistanceTo(point, t), 49.9991 - 1E-9, 49.9991 + 1E-9);
            Assert.InRange(Math.Abs(loop.SignedDistanceTo(point, t)), 49.9991 - 1E-9, 49.9991 + 1E-9);

            using (Tolerance.Use(t))
            {
                Assert.InRange(point.DistanceTo(loop), 49.9991 - 1E-9, 49.9991 + 1E-9);
            }
        }

        [Fact]
        public void APointExactlyAtTheCentreOfAnArc_IsItsRadiusFromIt()
        {
            Tolerance t = At(1E-3);
            var centre = new GeoPoint2(0, 0);
            GeoArc2 arc = Lower();
            var edge = new GeoEdge2(arc);
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(-60, 0), new GeoPoint2(-50, 0), new GeoPoint2(50, 0) }, new[] { 0.0, 1.0, 0.0 });

            Assert.Equal(50.0, arc.DistanceTo(centre, t), 9);
            Assert.Equal(50.0, edge.DistanceTo(centre, t), 9);
            Assert.Equal(50.0, chain.DistanceTo(centre, t), 9);
        }

        [Fact]
        public void APointOnAnArcOfARadiusNearTheTolerance_IsOnItWhereverItStandsNearTheCentre()
        {
            // The arc of radius 0.0012 about the origin, and the point 0.0003 under the centre: 0.0009 from the arc, within
            // a tolerance of 0.001. Carried to the arc's start it stood 0.00124 from it, and was off the arc.
            Tolerance t = At(1E-3);
            var point = new GeoPoint2(0, -0.0003);
            var arc = new GeoArc2(new GeoPoint2(0, 0), 0.0012, Math.PI, 2 * Math.PI);
            var edge = new GeoEdge2(arc);
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(-0.0012, 0), new GeoPoint2(0.0012, 0) }, new[] { 1.0, 0.0 });

            Assert.InRange(arc.DistanceTo(point, t), 0.0009 - 1E-12, 0.0009 + 1E-12);
            Assert.InRange(edge.DistanceTo(point, t), 0.0009 - 1E-12, 0.0009 + 1E-12);
            Assert.InRange(chain.DistanceTo(point, t), 0.0009 - 1E-12, 0.0009 + 1E-12);
            Assert.True(arc.IsPointOn(point, t));
            Assert.True(edge.IsPointOn(point, t));
            Assert.True(chain.IsPointOn(point, t));
        }
    }
}
