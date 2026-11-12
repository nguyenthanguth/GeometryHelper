using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A triangle nearly on one line is measured to its sides where the rounding of its normal could lean the foot further
    /// than the triangle is high, and its closest point is checked against that gap: two corners 1E-13 apart put a point
    /// 5E-14 from one of them 2.1 from the triangle, and the closest point of a sliver landed up to 26 away from it.
    /// </summary>
    public class SliverTriangleQueriesTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        /// <summary>The corners (35, 54, -67), (88, 96, -11) and the second moved 1E-13 along Y.</summary>
        private static GeoTriangle3 Needle() =>
            new GeoTriangle3(
                new GeoPoint3(35, 54, -67),
                new GeoPoint3(88, 96, -11),
                new GeoPoint3(88, 96 + 1E-13, -11)
            );

        private static GeoVector3 Direction(Random random)
        {
            while (true)
            {
                var v = new GeoVector3(
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1
                );

                if (v.Length > 0.1 && v.Length < 1)
                {
                    return v.Normalize();
                }
            }
        }

        private static double ToSegment(GeoPoint3 point, GeoPoint3 from, GeoPoint3 to) =>
            new GeoLine3(from, to).DistanceTo(point);

        [Fact]
        public void APointBesideTwoCornersATenTrillionthApart_IsMeasuredToThem_NotTwoAway()
        {
            // It was measured 2.1213 from the triangle, by the foot along a normal that is the rounding of its cross product.
            var point = new GeoPoint3(88, 96 + 5E-14, -11);

            Assert.True(Needle().DistanceTo(point) < 1E-13, $"{Needle().DistanceTo(point)}");
            Assert.True(Needle().GetClosestPointOnBoundary(point).DistanceTo(point) < 1E-13);
            Assert.True(Needle().GetShortestLineTo(point, Tol).Length < 1E-13);
        }

        [Fact]
        public void TheClosestPointOfALongThinTriangle_IsAsFarAsTheTriangle()
        {
            // A triangle 9 000 long and 0.0024 high, which has an area at a thousandth: from a point 0.00004 from it the
            // closest point found by the regions was 2.08 away.
            var triangle = new GeoTriangle3(
                new GeoPoint3(-2385.64, -1816.731, -2875.223),
                new GeoPoint3(1513.515, -1634.873, 3521.065),
                new GeoPoint3(3037.692, -1563.784, 6021.375)
            );
            var point = new GeoPoint3(103.124, -1700.654, 1207.419);

            Assert.False(triangle.IsDegenerate(Tol));
            Assert.Equal(triangle.DistanceTo(point), triangle.GetClosestPointOnBoundary(point).DistanceTo(point), 9);
            Assert.Equal(triangle.DistanceTo(point), triangle.GetShortestLineTo(point, Tol).Length, 9);
        }

        [Fact]
        public void SliversNearlyOnOneLine_AreMeasuredWithinTheirHeight_AndTheirClosestPointLiesOnThem()
        {
            // Two corners a sliver apart, or one a sliver off the line of the other two, 1E-15 to 1E-9 of the long side,
            // with points from 1E-14 to 10 off a corner or a side: the gap is that to the sides within a few times the sliver's height.
            var random = new Random(11);
            int far = 0;

            for (int i = 0; i < 20000; i++)
            {
                double scale = Math.Pow(10, random.Next(0, 5));
                GeoPoint3 Corner() =>
                    new GeoPoint3(
                        (random.NextDouble() - 0.5) * scale,
                        (random.NextDouble() - 0.5) * scale,
                        (random.NextDouble() - 0.5) * scale
                    );

                GeoPoint3 a = Corner();
                GeoPoint3 b = Corner();
                double length = a.DistanceTo(b);
                double height = Math.Pow(10, -9 - random.NextDouble() * 6) * length;
                GeoPoint3 c = (random.Next(2) == 0 ? b : a.Add(a.GetVectorTo(b).Multiply(random.NextDouble()))).Add(
                    Direction(random).Multiply(height)
                );
                var triangle = new GeoTriangle3(a, b, c);
                GeoPoint3 near = random.Next(3) switch
                {
                    0 => b,
                    1 => c,
                    _ => a.Add(a.GetVectorTo(b).Multiply(random.NextDouble())),
                };
                GeoPoint3 point = near.Add(Direction(random).Multiply(Math.Pow(10, -14 + random.NextDouble() * 15)));

                double sides = Math.Min(
                    ToSegment(point, a, b),
                    Math.Min(ToSegment(point, b, c), ToSegment(point, c, a))
                );
                double allowed = 3.0 * height + 1E-12 * Math.Max(1.0, scale);
                GeoPoint3 closest = triangle.GetClosestPointOnBoundary(point);
                double onIt = Math.Min(
                    ToSegment(closest, a, b),
                    Math.Min(ToSegment(closest, b, c), ToSegment(closest, c, a))
                );

                if (
                    !(Math.Abs(triangle.DistanceTo(point) - sides) <= allowed)
                    || !(Math.Abs(closest.DistanceTo(point) - sides) <= allowed)
                    || !(onIt <= allowed)
                )
                {
                    far++;
                }
            }

            Assert.Equal(0, far);
        }

        [Fact]
        public void ATriangleWellClearOfALine_GivesTheSameClosestPointAsBefore()
        {
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

            Assert.True(
                triangle.GetClosestPointOnBoundary(new GeoPoint3(20, 20, 7)).IsEqualTo(new GeoPoint3(20, 20, 0), Tol)
            );
            Assert.True(
                triangle.GetClosestPointOnBoundary(new GeoPoint3(60, 60, 0)).IsEqualTo(new GeoPoint3(50, 50, 0), Tol)
            );
            Assert.Equal(7.0, triangle.DistanceTo(new GeoPoint3(20, 20, 7)), 12);
        }
    }
}
