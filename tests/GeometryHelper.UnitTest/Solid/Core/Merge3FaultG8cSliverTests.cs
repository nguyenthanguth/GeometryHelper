using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Merging the faces of a plane where one of them is a sliver thinner than the tolerance along the rim: its two long sides
    /// a hair apart cancelled each other as if two faces shared them, and the merged face, and a beam less a bent bar whose
    /// side was cut so, came out short.
    /// </summary>
    public class Merge3FaultG8cSliverTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Fact]
        public void ASideCutJustOverItsFloor_MergesBackWhole()
        {
            // A side 2 000 by 300 cut by a line running from its bottom corner at one end to 0.0011 over it at the other, and
            // across by four lines up it: a triangle 2 000 long below the line, of 1.1 square millimetres, and five strips
            // above it. Where the triangle was thinner than the tolerance its two long sides cancelled each other, and the
            // side merged 0.53 square millimetres short.
            const double rise = 0.0011;
            double[] xs = { 1000, 600, 200, -200, -600, -1000 };
            double Z(double x) => rise * (1000 - x) / 2000;
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(new GeoPoint3(1000, 0, 0), new GeoPoint3(-1000, 0, 0), new GeoPoint3(-1000, 0, rise))),
            };

            for (int i = 0; i + 1 < xs.Length; i++)
            {
                faces.Add(new GeoFace3(new GeoPolygon3(
                    new GeoPoint3(xs[i], 0, Z(xs[i])), new GeoPoint3(xs[i + 1], 0, Z(xs[i + 1])),
                    new GeoPoint3(xs[i + 1], 0, 300), new GeoPoint3(xs[i], 0, 300))));
            }

            GeoFace3[] merged = Merge3.CoplanarFaces(faces, Tol);

            Assert.Single(merged);
            Assert.Equal(600000.0, faces.Sum(f => f.Area), 6);
            Assert.Equal(600000.0, merged[0].Area, 6);
        }

        [Theory]
        [InlineData(0.001000000000011006, -0.6680026593003434, 0.9700941052148557, -0.7819907328961374, 0.30668040798356777)]
        [InlineData(0.0009999999999999972, -0.0862188307038596, 0.8120668557528719, -0.6552474171180499, 0.17176066812675478)]
        public void ABeamLessABentBarEndingTheToleranceOffItsFloor_KeepsItsSides(double off, double ax, double ay, double az, double angle)
        {
            // A bar 16 across bent through a right angle in forty steps, its straight leg ending the tolerance over the floor of a
            // beam 2 000 by 200 by 300, both turned. The beam was cut by the plane of the bar's end, which met the floor within
            // the tolerance and left a sliver below it on each side; merged, the sides came out 0.72 and 0.49 square
            // millimetres short, and the difference 32.9 cubic millimetres short, and 54.8 the second way.
            var profile = new GeoPolygon2(Enumerable.Range(0, 16).Select(i => new GeoPoint2(8 * Math.Cos(i * Math.PI / 8), 8 * Math.Sin(i * Math.PI / 8))));
            var points = new List<GeoPoint3> { new GeoPoint3(100, 0, off) };

            for (int i = 0; i <= 40; i++)
            {
                double a = i * Math.PI / 80;
                points.Add(new GeoPoint3(200 - 100 * Math.Cos(a), 0, 600 + 100 * Math.Sin(a)));
            }

            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(ax, ay, az), angle);
            GeoSolid3 beam = Box(-1000, -100, 0, 1000, 100, 300).TransformBy(turn);
            GeoSolid3 bar = GeoSolid3.Sweep(profile, new GeoPolyline3(points), new GeoVector3(0, 1, 0), Tol).TransformBy(turn);

            Assert.True(bar.TryIntersect(beam, out GeoSolid3 common, Tol));
            double expected = 2000.0 * 200 * 300 - common.GetVolume(Tol);

            Assert.True(beam.TrySubtract(bar, out GeoSolid3 rest, Tol));
            Assert.True(rest.Validate(Tol).IsValid, rest.Validate(Tol).ToString());

            // The bar's floor, 0.001 thick over its 196 square millimetres, either way.
            Assert.InRange(rest.GetVolume(Tol), expected - 0.2, expected + 1E-6);
        }
    }
}
