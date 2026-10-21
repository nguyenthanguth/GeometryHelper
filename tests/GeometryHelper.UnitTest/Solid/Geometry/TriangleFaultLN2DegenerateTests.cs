using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A triangle has no area when its height over its longest side is within the point tolerance, or its corners lie on
    /// one line to rounding; any other has its normal and its plane.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoTriangle3.IsDegenerate(Tolerance)"/> read twice the area, a length squared, against the vector
    /// tolerance, and <see cref="GeoTriangle3.Normal"/> refused any triangle with twice its area under the default vector
    /// tolerance whatever tolerance the caller worked to. The surface mesh keeps real triangles 0.002 wide, and a body with an
    /// opening 0.002 from its corner threw from Normal in GetShortestLineTo and GetIntersections at a tolerance of 1E-6, in
    /// 1 036 of 8 000 queries.
    /// </remarks>
    public class TriangleFaultLN2DegenerateTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoTriangle3 Small = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(0.002, 0, 0), new GeoPoint3(0, 0.002, 0));

        [Fact]
        public void ATriangle0Point002Across_IsATriangleWithANormal()
        {
            Assert.False(Small.IsDegenerate(Default));
            Assert.Equal(GeoVector3.ZAxis, Small.Normal);
            Assert.Equal(GeoVector3.ZAxis, Small.GetPlane().Normal);
        }

        [Fact]
        public void ASliverThinnerThanTheTolerance_HasNoArea()
        {
            var sliver = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0), new GeoPoint3(500, 0.0004, 0));

            Assert.True(sliver.IsDegenerate(Default));
            Assert.False(sliver.IsDegenerate(new Tolerance(1E-4, 1E-5)));
            Assert.Equal(GeoVector3.ZAxis, sliver.Normal);
        }

        [Fact]
        public void CornersOnALineToRounding_HaveNoAreaAtAnyTolerance()
        {
            var flat = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1), new GeoPoint3(3, 3, 3));

            Assert.True(flat.IsDegenerate(new Tolerance(0, 0)));
            Assert.Throws<InvalidOperationException>(() => flat.Normal);
        }

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoCoordinateSystem3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), GeoVector3.XAxis, GeoVector3.YAxis), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Fact]
        public void ABodyWithAnOpeningNearItsCorner_AnswersAtAFineTolerance()
        {
            var fine = new Tolerance(1E-6, 1E-6);
            GeoSolid3 body = Box(0, 0, 0, 100, 60, 40).WithOpenings(new[] { Box(0.002, 0.002, 0.002, 30, 20, 10) });
            var random = new Random(5);

            for (int i = 0; i < 300; i++)
            {
                var line = new GeoLine3(
                    new GeoPoint3(random.NextDouble() * 40 - 5, random.NextDouble() * 30 - 5, random.NextDouble() * 20 - 5),
                    new GeoPoint3(random.NextDouble() * 40 - 5, random.NextDouble() * 30 - 5, random.NextDouble() * 20 - 5));

                GeoPoint3[] crossings = body.GetIntersections(line, fine);
                GeoLine3 shortest = line.GetShortestLineTo(body, fine);
                double distance = body.DistanceTo(line, fine);

                Assert.True(shortest.Length >= distance - 1E-9);
                Assert.Equal(distance <= fine.EqualPoint, body.CollidesWith(line, fine));
                Assert.True(crossings.Length == 0 || body.CollidesWith(line, fine));
            }
        }
    }
}
