using System;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A curved loop in space answers what lies in its plane on the sides it has: Locate, SignedDistanceTo, GetClosestEdge
    /// and the shortest line to a shape in its plane laid the loop out in its plane through a cleaning at the global point
    /// tolerance, so a side shorter than that went, whatever tolerance the loop was built with and the caller passed, and the
    /// answers disagreed with IsPointOn and DistanceTo, which measure every side.
    /// </summary>
    /// <remarks>
    /// Every loop here lies in a plane tilted 30 degrees about X through (1000, -2000, 500), and is drawn in that plane's own
    /// x and y: (0, 0), (10, 0), (10, 0.0006) and (0, 10), built within a billionth, so it keeps its right-hand side 0.0006
    /// long, shorter than the global tolerance of a thousandth. Dropped, that side left the triangle (0, 0), (10, 0), (0, 10).
    /// No test sets the global tolerance; each passes its own.
    /// </remarks>
    public class GeoPolygonArc3G1tTests
    {
        private static readonly GeoPoint3 Origin = new GeoPoint3(1000, -2000, 500);
        private static readonly GeoVector3 AlongX = new GeoVector3(1, 0, 0);
        private static readonly GeoVector3 AlongY = new GeoVector3(0, Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));

        /// <summary>A tolerance small enough to keep a side 0.0006 long when the loop is built.</summary>
        private static readonly Tolerance Build = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        /// <summary>The point at x, y in the tilted plane.</summary>
        private static GeoPoint3 At(double x, double y) => Origin.Add(AlongX.Multiply(x)).Add(AlongY.Multiply(y));

        /// <summary>The loop (0, 0), (10, 0), (10, 0.0006), (0, 10), every side straight.</summary>
        private static GeoPolygonArc3 Loop()
            => new GeoPolygonArc3(new[] { At(0, 0), At(10, 0), At(10, 0.0006), At(0, 10) }, null, null, Build);

        /// <summary>The same loop with its short side a half circle bulging out to (10.0003, 0.0003).</summary>
        private static GeoPolygonArc3 LoopWithAShortBend()
        {
            var bend = new GeoEdge3(GeoArc3.FromThreePoints(At(10, 0), At(10.0003, 0.0003), At(10, 0.0006), Build));

            return new GeoPolygonArc3(
                new[] { At(0, 0), At(10, 0), At(10, 0.0006), At(0, 10) },
                new[] { 0.0, bend.Bulge, 0.0, 0.0 },
                new[] { default(GeoVector3), bend.Normal, default(GeoVector3), default(GeoVector3) },
                Build);
        }

        [Fact]
        public void APointJustOutsideASideShorterThanTheGlobalTolerance_IsOnTheLoopWhereIsPointOnSaysSo()
        {
            // At a tolerance of 0.0001, the point (10.00005, 0.0003) is 0.00005 out from the middle of the side from (10, 0) to
            // (10, 0.0006): on the loop. With the side dropped it stood 0.00025 from the triangle's long side and was outside.
            Tolerance t = At(1E-4);
            GeoPolygonArc3 loop = Loop();
            GeoPoint3 beside = At(10.00005, 0.0003);

            Assert.Equal(4, loop.EdgeCount);
            Assert.InRange(loop.DistanceTo(beside, t), 0.00005 - 1E-11, 0.00005 + 1E-11);
            Assert.True(loop.IsPointOn(beside, t));
            Assert.Equal(PointLocation.OnSide, loop.Locate(beside, t));
            Assert.True(loop.Contains(beside, t));
            Assert.Equal(0.0, loop.SignedDistanceTo(beside, t));
        }

        [Fact]
        public void APointJustOutsideAShortBend_IsOnTheLoopWhereIsPointOnSaysSo()
        {
            // The short side bent into a half circle of radius 0.0003: the point 0.00005 beyond its outermost point, at
            // (10.00035, 0.0003), is on the loop at a tolerance of 0.0001. Dropped, the bend left the point 0.00046 outside.
            Tolerance t = At(1E-4);
            GeoPolygonArc3 loop = LoopWithAShortBend();
            GeoPoint3 beside = At(10.00035, 0.0003);

            Assert.True(loop.GetEdgeAt(1).IsArc);
            Assert.InRange(loop.DistanceTo(beside, t), 0.00005 - 1E-11, 0.00005 + 1E-11);
            Assert.True(loop.IsPointOn(beside, t));
            Assert.Equal(PointLocation.OnSide, loop.Locate(beside, t));
            Assert.Equal(0.0, loop.SignedDistanceTo(beside, t));
        }

        [Fact]
        public void APointInTheSliverASideShorterThanTheGlobalToleranceAdds_IsInsideTheLoop()
        {
            // The point (9.9998, 0.0003) is 0.0002 in from the short side and further from the others: inside, 0.0002 deep, at a
            // tolerance of 0.0001. The triangle left without the side has it 0.00007 outside its long side, so it was read on
            // the side, and nought from it.
            Tolerance t = At(1E-4);
            GeoPolygonArc3 loop = Loop();
            GeoPoint3 inside = At(9.9998, 0.0003);

            Assert.InRange(loop.DistanceTo(inside, t), 0.0002 - 1E-11, 0.0002 + 1E-11);
            Assert.False(loop.IsPointOn(inside, t));
            Assert.Equal(PointLocation.Inside, loop.Locate(inside, t));
            Assert.InRange(loop.SignedDistanceTo(inside, t), -0.0002 - 1E-11, -0.0002 + 1E-11);
        }

        [Fact]
        public void APointOutBesideTheEndOfASideShorterThanTheGlobalTolerance_IsItsOwnDistanceOut()
        {
            // At a tolerance of 0.002, more than the global one, the point (10.0025, 0.0006) is 0.0025 out from the corner at
            // the top of the short side. Without the side it stood 0.00257 out, from the corner (10, 0).
            Tolerance t = At(2E-3);
            GeoPolygonArc3 loop = Loop();
            GeoPoint3 outside = At(10.0025, 0.0006);

            Assert.Equal(PointLocation.OutSide, loop.Locate(outside, t));
            Assert.InRange(loop.SignedDistanceTo(outside, t), 0.0025 - 1E-11, 0.0025 + 1E-11);
            Assert.InRange(loop.SignedDistanceTo(outside, t), loop.DistanceTo(outside, t) - 1E-11, loop.DistanceTo(outside, t) + 1E-11);
        }

        [Fact]
        public void TheEdgeNearestAPointBesideASideShorterThanTheGlobalTolerance_IsThatSide()
        {
            // At a tolerance of 0.0001, the side from (10, 0) to (10, 0.0006) is the edge nearest (10.00005, 0.0003). Without it
            // the answer was the triangle's long side from (10, 0) to (0, 10), which the loop does not have.
            Tolerance t = At(1E-4);
            GeoEdge3 nearest = Loop().GetClosestEdge(At(10.00005, 0.0003), t);

            Assert.False(nearest.IsArc);
            Assert.InRange(nearest.StartPoint.DistanceTo(At(10, 0)), 0.0, 1E-9);
            Assert.InRange(nearest.EndPoint.DistanceTo(At(10, 0.0006)), 0.0, 1E-9);
        }

        [Fact]
        public void TheShortestLineToASegmentBesideASideShorterThanTheGlobalTolerance_LandsOnThatSide()
        {
            // The segment from (10.0002, 0.0001) to (10.0002, 0.0005) runs 0.0002 beside the short side: the line between them
            // is 0.0002 long and leaves the side. Without the side it left the corner (10, 0), 0.000224 long.
            Tolerance t = At(1E-4);
            var segment = new GeoLine3(At(10.0002, 0.0001), At(10.0002, 0.0005));
            GeoLine3 line = Loop().GetShortestLineTo(segment, t);

            Assert.InRange(line.Length, 0.0002 - 1E-11, 0.0002 + 1E-11);
            Assert.InRange(line.StartPoint.DistanceTo(At(10, 0.0001)), 0.0, 0.0004 + 1E-11);
            Assert.InRange(Loop().DistanceTo(line.StartPoint, t), 0.0, 1E-11);
        }
    }
}
