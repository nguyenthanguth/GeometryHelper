using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two rays parallel but for the rounding of the points they were built through do not cross far out where a double
    /// cannot place a crossing.
    /// </summary>
    /// <remarks>
    /// Two rays crossed wherever the sine of the angle between them was above 1E-15, however far along the crossing fell. A
    /// ray rebuilt through two points of another moved 10 to its side, a hundred apart, is a sine of some 1E-14 off it at
    /// coordinates of a thousand and ten thousand, and their carriers cross some 1E15 along, where a double cannot place a
    /// point within a thousandth. Where that lay ahead of both origins the two read nought apart and touching, though they
    /// are 10 apart all along. They follow the rule of the line without ends now: a sine of 1E-9 or less places no crossing
    /// further along than a double can place it within the point tolerance, measured by the larger of the two distances
    /// along, so that both ways round agree; above it, at any real angle, they still cross however far out.
    /// </remarks>
    public class RayFaultLN1NearlyParallelTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        [Theory]
        [InlineData(1E3)]
        [InlineData(1E4)]
        [InlineData(1E5)]
        [InlineData(1E6)]
        public void ARayRebuiltThroughTwoPointsOfAnotherMovedTen_IsTenFromIt(double scale)
        {
            var random = new Random(15);
            int wrong = 0;

            for (int i = 0; i < 100000; i++)
            {
                double angle = random.NextDouble() * 2.0 * Math.PI;
                var direction = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                var origin = new GeoPoint2(scale * random.NextDouble(), scale * random.NextDouble());
                var original = new GeoRay2(origin, direction);
                var side = new GeoVector2(-direction.Y * 10, direction.X * 10);
                double from = 200 * random.NextDouble() - 100;
                var rebuilt = new GeoRay2(
                    new GeoPoint2(origin.X + side.X + direction.X * from, origin.Y + side.Y + direction.Y * from),
                    new GeoPoint2(origin.X + side.X + direction.X * (from + 100), origin.Y + side.Y + direction.Y * (from + 100)));

                if (Math.Abs(original.DistanceTo(rebuilt, Thousandth) - 10.0) > 1E-6
                    || Math.Abs(rebuilt.DistanceTo(original, Thousandth) - 10.0) > 1E-6
                    || original.CollidesWith(rebuilt, Thousandth)
                    || rebuilt.CollidesWith(original, Thousandth)
                    || Math.Abs(original.GetShortestLineTo(rebuilt, Thousandth).Length - 10.0) > 1E-6
                    || Math.Abs(rebuilt.GetShortestLineTo(original, Thousandth).Length - 10.0) > 1E-6)
                {
                    wrong++;
                }
            }

            Assert.Equal(0, wrong);
        }

        [Fact]
        public void TwoRaysAtARealAngle_StillCrossHoweverFarOut()
        {
            // At a sine of 1E-8 the ray from (0, 10) along X crosses the X axis ray a billion along, and the two touch there.
            var axis = new GeoRay2(new GeoPoint2(0, 0), GeoVector2.XAxis);
            var tilted = new GeoRay2(new GeoPoint2(0, 10), new GeoVector2(Math.Sqrt(1 - 1E-16), -1E-8));

            Assert.Equal(0.0, axis.DistanceTo(tilted, Thousandth));
            Assert.Equal(0.0, tilted.DistanceTo(axis, Thousandth));
            Assert.True(axis.CollidesWith(tilted, Thousandth));
            Assert.Equal(0.0, axis.GetShortestLineTo(tilted, Thousandth).Length);
        }
    }
}
