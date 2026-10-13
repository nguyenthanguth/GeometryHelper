using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A fillet is the circle on the inside of the corner, running the way the two pieces run, and it touches neither
    /// piece past the corner.
    /// </summary>
    /// <remarks>
    /// The fillet was the nearest of the four circles touching both pieces, each moved by the radius either way, and a
    /// touching point could lie up to the point tolerance past the corner. Where the radius times the cotangent of half
    /// the turn is within the tolerance, a circle outside the corner touched the first piece past the corner and won:
    /// the chain (0, 0), (10, 0), (3, 5) filleted at 0.002 ran on to (10.00064, 0) and then turned back along an arc of
    /// 35.5° for the corner's 144.5°, a cusp. A curved loop filleted at 50 where no circle of that radius fits inside
    /// the corner was given an arc outside it, and its area grew from 167.2 to 185.5.
    /// </remarks>
    public class FilletFaultFR1InsideTheCornerTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>
        /// Checks that a filleted corner between two segments is the arc of the radius on the inside of the turn, worked
        /// out here from the turn alone: the touching points stand r tan(turn / 2) back from the corner along each piece.
        /// </summary>
        private static void AssertRoundedInside(GeoPolylineArc2 filleted, GeoPoint2 start, GeoPoint2 corner, GeoPoint2 end, double radius)
        {
            GeoVector2 back = corner.GetVectorTo(start) * (1.0 / corner.DistanceTo(start));
            GeoVector2 on = corner.GetVectorTo(end) * (1.0 / corner.DistanceTo(end));

            double turn = Math.Atan2(-back.CrossProduct(on), -back.DotProduct(on));
            double reach = radius * Math.Tan(Math.Abs(turn) / 2.0);

            GeoPoint2 first = corner.Add(back * reach);
            GeoPoint2 second = corner.Add(on * reach);

            Assert.Equal(3, filleted.EdgeCount);

            GeoEdge2 arc = filleted.GetEdgeAt(1);

            Assert.True(arc.IsArc);
            Assert.True(filleted.GetEdgeAt(0).EndPoint.IsEqualTo(first, Tight));
            Assert.True(filleted.GetEdgeAt(2).StartPoint.IsEqualTo(second, Tight));

            // The arc turns the way the corner turns, and by as much.
            Assert.Equal(turn, arc.ToArc().SweptAngle, 9);
            Assert.Equal(radius, arc.ToArc().Radius, 9);

            // A fillet cuts the corner off, so the chain comes back shorter than it went in.
            Assert.True(filleted.Length < start.DistanceTo(corner) + corner.DistanceTo(end));
        }

        [Fact]
        public void ACornerTurning144DegreesFilletedByTwoThousandths_IsRoundedInsideTheCorner()
        {
            var chain = new GeoPolyline2(new[] { P(0, 0), P(10, 0), P(3, 5) });

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, 0.002, Thousandth);

            AssertRoundedInside(filleted, P(0, 0), P(10, 0), P(3, 5), 0.002);

            // The first piece stops short of the corner rather than running on 0.00064 past it.
            Assert.True(filleted.GetEdgeAt(0).EndPoint.X < 10.0 - 0.006);
        }

        [Fact]
        public void TheSameCornerAskedForOnItsOwn_IsRoundedInsideTheCorner()
        {
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(10, 0), P(3, 5) }, null);

            Assert.True(Corner2.TryFilletAt(chain, 1, 0.002, out GeoPolylineArc2 filleted, Thousandth));

            AssertRoundedInside(filleted, P(0, 0), P(10, 0), P(3, 5), 0.002);
        }

        [Theory]
        [InlineData(120.0, 0.0015, 1.0)]
        [InlineData(150.0, 0.0035, 1.0)]
        [InlineData(175.0, 0.02, 1.0)]
        [InlineData(120.0, 0.0015, -1.0)]
        [InlineData(150.0, 0.0035, -1.0)]
        [InlineData(175.0, 0.02, -1.0)]
        public void ASharpCornerWithASmallRadius_IsRoundedInsideTheCornerWhicheverWayItTurns(double turnDegrees, double radius, double way)
        {
            // Each radius is the one at which r cot(turn / 2), how far past the corner the circle outside it touches,
            // is just within the tolerance of a thousandth.
            double turn = turnDegrees * Math.PI / 180.0;

            GeoPoint2 start = P(-10, 0);
            GeoPoint2 corner = P(0, 0);
            GeoPoint2 end = P(10.0 * Math.Cos(turn), way * 10.0 * Math.Sin(turn));

            GeoPolylineArc2 filleted = Corner2.Fillet(new GeoPolyline2(new[] { start, corner, end }), radius, Thousandth);

            AssertRoundedInside(filleted, start, corner, end, radius);
        }

        [Fact]
        public void AnArcRunningIntoACornerOfTwentyDegrees_IsRoundedInsideTheCorner()
        {
            // A sixth of a turn of radius 10 about (0, 10), arriving at the origin heading along X, then a straight
            // leaving at 20°. The fillet's centre stands r to the left of the straight and 10 - r from the
            // arc's centre; of the two such points, the one near the corner is the fillet.
            const double radius = 0.005;
            double heading = 20.0 * Math.PI / 180.0;

            var chain = new GeoPolylineArc2(
                new[] { P(-10.0 * Math.Sin(Math.PI / 3.0), 10.0 - 10.0 * Math.Cos(Math.PI / 3.0)), P(0, 0), P(10.0 * Math.Cos(heading), 10.0 * Math.Sin(heading)) },
                new[] { Math.Tan(Math.PI / 12.0), 0.0, 0.0 });

            GeoVector2 along = new GeoVector2(Math.Cos(heading), Math.Sin(heading));
            GeoVector2 left = new GeoVector2(-Math.Sin(heading), Math.Cos(heading));

            // C = s along + r left, with |C - (0, 10)| = 10 - r: a quadratic in s, the smaller root.
            double bx = radius * left.X, by = radius * left.Y - 10.0;
            double b = 2.0 * (bx * along.X + by * along.Y);
            double c = bx * bx + by * by - (10.0 - radius) * (10.0 - radius);
            double s = (-b - Math.Sqrt(b * b - 4.0 * c)) / 2.0;

            GeoPoint2 centre = P(s * along.X + radius * left.X, s * along.Y + radius * left.Y);
            GeoPoint2 first = P(centre.X * 10.0 / (10.0 - radius), 10.0 + (centre.Y - 10.0) * 10.0 / (10.0 - radius));
            GeoPoint2 second = P(s * along.X, s * along.Y);

            GeoPolylineArc2 filleted = Corner2.Fillet(chain, radius, Thousandth);

            Assert.Equal(3, filleted.EdgeCount);
            Assert.True(filleted.GetEdgeAt(0).EndPoint.IsEqualTo(first, Tight));
            Assert.True(filleted.GetEdgeAt(2).StartPoint.IsEqualTo(second, Tight));

            // The arc stops short of the corner, and the fillet turns left with the corner.
            Assert.True(first.X < 0.0);
            Assert.True(filleted.GetEdgeAt(1).Bulge > 0.0);
            Assert.True(filleted.GetEdgeAt(1).ToArc().Center.IsEqualTo(centre, Tight));
        }

        [Fact]
        public void ARectangleFilletedAtTen_StillGetsAQuarterTurnInsideEveryCorner()
        {
            var rectangle = new GeoPolygon2(new[] { P(0, 0), P(100, 0), P(100, 50), P(0, 50) });

            GeoPolygonArc2 filleted = Corner2.Fillet(rectangle, 10.0, Thousandth);

            GeoPoint2[] expected = { P(10, 0), P(90, 0), P(100, 10), P(100, 40), P(90, 50), P(10, 50), P(0, 40), P(0, 10) };

            Assert.Equal(8, filleted.EdgeCount);

            int start = 0;

            while (start < filleted.VertexCount && !filleted[start].IsEqualTo(expected[0], Tight))
            {
                start++;
            }

            Assert.True(start < filleted.VertexCount);

            for (int i = 0; i < expected.Length; i++)
            {
                int at = (start + i) % filleted.VertexCount;

                Assert.True(filleted[at].IsEqualTo(expected[i], Tight));
                Assert.Equal(i % 2 == 1 ? Math.Tan(Math.PI / 8.0) : 0.0, filleted.GetBulgeAt(at), 12);
            }

            Assert.Equal(5000.0 - (4.0 - Math.PI) * 100.0, filleted.Area, 9);
        }

        [Fact]
        public void ACurvedCornerWhereNoCircleOfTheRadiusFitsInside_IsLeftAsItIs()
        {
            // A loop of one straight side and two arcs bowing in, of radii 25.2 and 75.5, which meet at a tip turning
            // 157.8°. A circle of radius 50 inside the tip would have to lie within the arc of radius 25.2 or hold it,
            // and neither circle reaches the other arc.
            var loop = new GeoPolygonArc2(
                new[] { P(9.2911688258509972, 13.885425084948732), P(26.559591085572215, 35.32124339032994), P(-12.041087552911952, 42.571371335686365) },
                new[] { 0.0, -0.48, -0.12 });

            Assert.False(Corner2.TryFilletAt(loop, 2, 50.0, out GeoPolygonArc2 one, Thousandth));
            Assert.Same(loop, one);

            GeoPolygonArc2 filleted = Corner2.Fillet(loop, 50.0, Thousandth);

            Assert.Equal(3, filleted.VertexCount);
            Assert.Equal(loop.Area, filleted.Area, 9);
        }
    }
}
