using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Results the gluing of a boolean left open where it takes from two faces lying back to back the area they share.
    /// </summary>
    /// <remarks>
    /// Each pair came out open in a scan of random blocks, prisms, stars, Ls, Is and bent bars, and is rebuilt here from the
    /// numbers the scan drew. What two bodies share is measured at a tolerance of 1e-10.
    /// </remarks>
    public class BackToBackGapTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-10, 1E-10, Math.PI / 180, 1E-10);

        private static GeoSolid3 Turned(GeoSolid3 part, GeoVector3 axis, double angle)
            => part.TransformBy(GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, angle));

        /// <summary>
        /// An I 11 long, its flange tip and inner side met at a corner by a prism of twelve sides passing it.
        /// </summary>
        private static (GeoSolid3 A, GeoSolid3 B) IAndPrism() => (
            Turned(GeoSolid3.Extrude(
                    new GeoPolygon2(
                        new GeoPoint2(-12.5, 0), new GeoPoint2(12.5, 0), new GeoPoint2(12.5, 4), new GeoPoint2(3, 4), new GeoPoint2(3, 72),
                        new GeoPoint2(12.5, 72), new GeoPoint2(12.5, 76), new GeoPoint2(-12.5, 76), new GeoPoint2(-12.5, 72), new GeoPoint2(-3, 72),
                        new GeoPoint2(-3, 4), new GeoPoint2(-12.5, 4)),
                    new GeoCoordinateSystem3(new GeoPoint3(-25, -25, 23), GeoVector3.XAxis, GeoVector3.YAxis), 11),
                new GeoVector3(-0.2993645834733567, 0.44856412473533491, -0.31418844909136578), 0.24493570357790945),
            Turned(GeoSolid3.Cylinder(new GeoPoint3(-22, -27, -3), new GeoPoint3(-83, 2, 71), 17, 12),
                new GeoVector3(0.28093027592679964, 0.22354216767639956, -0.19311523888870852), 2.2736830349423376));

        /// <summary>
        /// Two bars bent twice, crossing.
        /// </summary>
        private static (GeoSolid3 A, GeoSolid3 B) CrossingBars() => (
            Turned(GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(-6, -2, -36), new GeoPoint3(-59, 2, -7), new GeoPoint3(-61, 12, 30), new GeoPoint3(-29, -11, 26)).Fillet(17), 6, 0.3),
                new GeoVector3(-0.44080992086828219, 0.079094676104883965, 0.49383613699760109), 2.1122341910899776),
            Turned(GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(-2, -37, 17), new GeoPoint3(-17, -24, 28), new GeoPoint3(-11, -77, 55), new GeoPoint3(-38, -91, 9)).Fillet(15), 10, 0.3),
                new GeoVector3(-0.14885957010502909, -0.38470693625728924, 0.3328549544479954), 1.72172328071749));

        private static double Shared(GeoSolid3 a, GeoSolid3 b) => Boolean3.Intersect(a, b, Tight).Sum(piece => piece.GetVolume());

        private static void AssertNear(double expected, double actual, double scale, string what)
            => Assert.True(Math.Abs(actual - expected) <= 5E-6 * scale, $"{what}: {actual:R}, expected {expected:R}");

        [Fact]
        public void APrismPassingTheCornerOfAnI_UnitesWithIt_Closed()
        {
            // Taken from the end of the I, the prism's face there left a corner of it 0.0006 square millimetres across,
            // every side of it longer than the point tolerance, and the clipping took it for a seam and dropped it, while
            // the faces beside it kept their edges round it; and a corner of the prism 0.002 off the flange's edge left a
            // needle 5.9 long on the flange's side. The union was open by both.
            (GeoSolid3 a, GeoSolid3 b) = IAndPrism();

            Assert.True(a.TryUnion(b, out GeoSolid3 union));
            Assert.True(union.IsClosed());
            AssertNear(a.GetVolume() + b.GetVolume() - Shared(a, b), union.GetVolume(), a.GetVolume() + b.GetVolume(), "the union");
        }

        [Fact]
        public void TwoBentBarsCrossing_ShareAClosedBody()
        {
            (GeoSolid3 a, GeoSolid3 b) = CrossingBars();
            double shared = Shared(a, b);

            Assert.True(a.TryIntersect(b, out GeoSolid3 common));
            Assert.True(common.IsClosed());
            AssertNear(shared, common.GetVolume(), a.GetVolume() + b.GetVolume(), "the common part");
        }

        [Theory]
        [InlineData(1E-2)]
        [InlineData(1E-3)]
        [InlineData(1E-4)]
        public void TwoBentBarsCrossing_ShareWhatTheyShareWithinATolerance(double within)
        {
            // Within a thousandth the planes of the first bar crossed 33 cells of the second they could not cut, and the
            // cells either side of some of them were judged apart; cut by the planes of the second, every cell left so was
            // judged alike, and that is the way taken now. Cut the first way, the common part lost an eighth.
            var tolerance = new Tolerance(within, 1E-2 * within, Tolerance.DefaultEqualAngleRad, within);
            (GeoSolid3 a, GeoSolid3 b) = CrossingBars();

            Assert.True(a.TryIntersect(b, out GeoSolid3 common, tolerance));
            Assert.True(common.IsClosed(tolerance));
            AssertNear(Shared(a, b), common.GetVolume(tolerance), a.GetVolume() + b.GetVolume(), "the common part");
        }
    }
}
