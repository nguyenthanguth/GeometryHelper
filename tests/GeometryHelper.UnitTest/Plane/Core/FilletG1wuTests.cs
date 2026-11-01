using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A corner between two arcs, or between a straight piece and an arc, filleted at the radius at which the two moved by it
    /// only touch, is left square at any size.
    /// </summary>
    /// <remarks>
    /// Where the moved circles, or the moved line and circle, only touch, the arc about the point they share sweeps half a
    /// turn, which a fillet never draws. Whether they touched was read to nought, so a shade of overlap the rounding of the
    /// numbers left gave a centre whose arc swept a hair under half a turn, and was drawn at one size and not at another.
    /// No test sets the global tolerance.
    /// </remarks>
    public class FilletG1wuTests
    {
        private static Tolerance Within(double point) => new Tolerance(point, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, point);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        // The arc of the circle of radius 10 about the origin running counter-clockwise for a radian up to where it crosses
        // the circle of radius 7 about (12, 0), at (8.125, 5.83), and on along that circle counter-clockwise for a radian. The
        // lens inside both circles runs from (5, 0) to (10, 0), and the circle filling it, of radius 2.5 about (7.5, 0),
        // touches the first arc at (10, 0) and the second at (5, 0), the ends of one of its diameters.
        private static GeoPolylineArc2 Lens(double scale)
        {
            double x = 195.0 / 24.0;
            double y = Math.Sqrt(100.0 - x * x);
            double first = Math.Atan2(y, x);
            double second = Math.Atan2(y, x - 12.0);

            return new GeoPolylineArc2(
                new[]
                {
                    P(10.0 * Math.Cos(first - 1.0) * scale, 10.0 * Math.Sin(first - 1.0) * scale),
                    P(x * scale, y * scale),
                    P((12.0 + 7.0 * Math.Cos(second + 1.0)) * scale, 7.0 * Math.Sin(second + 1.0) * scale),
                },
                new[] { Math.Tan(0.25), Math.Tan(0.25), 0.0 });
        }

        // The cap of the circle of radius 12 about the origin above the chord y = 4, turned by half a radian about the origin
        // and moved to (30, 40), as ConvertG1wtTests draws it: its largest circle, of radius 4, touches the chord at (0, 4)
        // and the arc at (0, 12).
        private static GeoPolylineArc2 Cap(double scale)
        {
            double x0 = Math.Sqrt(12.0 * 12.0 - 4.0 * 4.0);
            double corner = Math.Atan2(4.0, x0);
            double end = Math.PI / 2 + (Math.PI / 2 - corner) / 2;
            GeoTransform2 place = GeoTransform2.Translation(new GeoVector2(30 * scale, 40 * scale)) * GeoTransform2.Rotation(P(0, 0), 0.5);

            return new GeoPolylineArc2(
                new[] { P(-0.5 * x0 * scale, 4.0 * scale), P(x0 * scale, 4.0 * scale), P(12.0 * Math.Cos(end) * scale, 12.0 * Math.Sin(end) * scale) }.Select(place.Transform),
                new[] { 0.0, Math.Tan((end - corner) / 4), 0.0 });
        }

        [Fact]
        public void TwoArcsFilletedByTheRadiusOfTheCircleFillingTheLensBetweenThem_AreLeftSquareAtEverySize()
        {
            foreach (double scale in new[] { 1E-3, 1.0, 1E3 })
            {
                bool rounded = Lens(scale).TryFilletAt(1, 2.5 * scale, out _, Within(1E-3 * scale));

                Assert.False(rounded, "at a scale of " + scale);
            }
        }

        [Fact]
        public void TwoArcsFilletedByAMillionthOfTheRadiusLessThanTheLensWidth_AreRoundedAlikeAtEverySize()
        {
            double radius = 2.5 * (1.0 - 1E-6);

            bool full = Lens(1.0).TryFilletAt(1, radius, out GeoPolylineArc2 rounded, Within(1E-3));
            bool small = Lens(1E-3).TryFilletAt(1, radius * 1E-3, out GeoPolylineArc2 roundedSmall, Within(1E-6));
            bool large = Lens(1E3).TryFilletAt(1, radius * 1E3, out GeoPolylineArc2 roundedLarge, Within(1.0));

            Assert.True(full);
            Assert.True(small);
            Assert.True(large);
            Assert.Equal(4, rounded.VertexCount);
            Assert.Equal(rounded.Length / 1000, roundedSmall.Length, 12);
            Assert.Equal(rounded.Length * 1000, roundedLarge.Length, 6);
            Assert.True(Math.Abs(rounded.GetEdgeAt(1).ToArc().SweptAngle) < Math.PI);
        }

        [Fact]
        public void TheCapFilletedByTheRadiusOfItsLargestCircle_IsLeftSquareAtEverySize()
        {
            foreach (double scale in new[] { 1E-3, 1.0, 1E3 })
            {
                bool rounded = Cap(scale).TryFilletAt(1, 4.0 * scale, out _, Within(1E-3 * scale));

                Assert.False(rounded, "at a scale of " + scale);
            }
        }
    }
}
