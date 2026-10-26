using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A ray starting within the planar tolerance of a flat shape's plane is read as the line without ends along it is, by how
    /// far that line climbs across the shape, and given what the line meets at its origin or ahead of it: one climbing more
    /// than the planar tolerance across the shape is given its crossing, and one climbing less lies level over it and is given
    /// nothing, as the line is. A ray level within the vector tolerance used to be read by the part of it over the shape and
    /// given nothing where its line crosses, and then, read by its own crossing, was given crossings its line is not.
    /// </summary>
    public class RayLevelThroughTheShapeTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly GeoTriangle3 Triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0), new GeoPoint3(0, 200, 0));

        private static readonly GeoPolygon3 Square = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(1E5, 0.0)]
        [InlineData(1E5, 2.1)]
        public void ARayDippingThroughTheShapeLevelWithinTheVectorTolerance_IsGivenTheCrossing_AsALongSegmentIs(double offset, double angle)
        {
            // From 0.0005 over the plane at (10, 50) along X, falling 1E-5 for each unit: it crosses the plane at (60, 50) and
            // leaves the square 0.0004 under it, the triangle 0.0009 under it, both within the planar tolerance.
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(offset, -2.0 * offset, offset)).Multiply(
                GeoTransform3.RotationAxis(new GeoPoint3(0, 0, 0), new GeoVector3(0.3, -0.5, 0.8), angle));
            var triangle = new GeoTriangle3(Triangle.A.TransformBy(move), Triangle.B.TransformBy(move), Triangle.C.TransformBy(move));
            var square = new GeoPolygon3(Square[0].TransformBy(move), Square[1].TransformBy(move), Square[2].TransformBy(move), Square[3].TransformBy(move));
            GeoVector3 along = new GeoVector3(1, 0, -1E-5).Normalize().TransformBy(move);
            var ray = new GeoRay3(new GeoPoint3(10, 50, 0.0005).TransformBy(move), along);
            GeoPoint3 crossing = new GeoPoint3(60, 50, 0).TransformBy(move);
            var segment = new GeoLine3(ray.Origin, ray.Origin.Add(along.Multiply(3000)));

            Assert.InRange(Assert.Single(segment.GetIntersections(square, Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
            Assert.InRange(Assert.Single(ray.GetIntersections(triangle, Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
            Assert.InRange(Assert.Single(ray.GetIntersections(square, Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
            Assert.InRange(Assert.Single(ray.GetIntersections(new GeoFace3(square), Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
        }

        [Fact]
        public void ARayStartingOnTheShapeLevelWithIt_IsStillGivenNothing()
        {
            // From the plane itself, rising 1E-6: its crossing is its origin, and it lies level over the square.
            var ray = new GeoRay3(new GeoPoint3(10, 50, 0), new GeoVector3(1, 0, 1E-6).Normalize());

            Assert.Empty(ray.GetIntersections(Square, Thousandth));
        }

        [Fact]
        public void ARayClimbingLessThanThePlanarToleranceAcrossTheSquare_IsGivenNothing_AsItsLine()
        {
            // From 0.0002 over the square falling 4E-6 for each unit: it would cross the floor at (60, 50, 0), but it climbs
            // 0.0004 across the square and lies within the planar tolerance of it from end to end.
            var origin = new GeoPoint3(10, 50, 0.0002);
            GeoVector3 along = new GeoVector3(1, 0, -4E-6).Normalize();

            Assert.Empty(new GeoXLine3(origin, along).GetIntersections(Square, Thousandth));
            Assert.Empty(new GeoRay3(origin, along).GetIntersections(Square, Thousandth));
            Assert.Empty(new GeoRay3(origin, along).GetIntersections(new GeoFace3(Square), Thousandth));
        }

        [Theory]
        [InlineData(5000.0, 10.0, 1E-7, 1E-10)]
        [InlineData(100.0, 5.0, 1.1E-8, 1.2E-10)]
        [InlineData(100.0, 5.0, 0.9E-8, 1E-10)]
        public void ARayAHairOffThePlaneAndLevelToRounding_IsGivenNothing_HoweverNearItStarts(double length, double x, double height, double fall)
        {
            // A rectangle 5 000 by 100, or the square, and a ray from 1E-7 or about 1E-8 over it falling 1E-10 or so: it was
            // given its crossing, (1010, 50, 0) or (96.67, 50, 0), where it starts more than a hundred-millionth over the plane,
            // and nothing where it starts less.
            var shape = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(length, 0, 0), new GeoPoint3(length, 100, 0), new GeoPoint3(0, 100, 0));
            var origin = new GeoPoint3(x, 50, height);
            GeoVector3 along = new GeoVector3(1, 0, -fall).Normalize();

            Assert.Empty(new GeoXLine3(origin, along).GetIntersections(shape, Thousandth));
            Assert.Empty(new GeoRay3(origin, along).GetIntersections(shape, Thousandth));
        }

        [Fact]
        public void ARayNearTheShapeMeetsIt_OnlyWhereItsLineDoes()
        {
            var random = new Random(9113);
            var face = new GeoFace3(Square, new[] { new GeoPolygon3(new GeoPoint3(30, 30, 0), new GeoPoint3(30, 70, 0), new GeoPoint3(70, 70, 0), new GeoPoint3(70, 30, 0)) });

            for (int i = 0; i < 4000; i++)
            {
                double height = (random.NextDouble() * 2 - 1) * (i % 3 == 0 ? 1E-7 : 0.0015);
                double slope = (random.NextDouble() * 2 - 1) * (i % 2 == 0 ? 1E-5 : 1E-3);
                double angle = random.NextDouble() * 2 * Math.PI;
                var origin = new GeoPoint3(-60 + 220 * random.NextDouble(), -60 + 220 * random.NextDouble(), height);
                GeoVector3 along = new GeoVector3(Math.Cos(angle), Math.Sin(angle), slope).Normalize();
                var ray = new GeoRay3(origin, along);
                var line = new GeoXLine3(origin, along);

                AssertOnTheLine(ray, ray.GetIntersections(Triangle, Thousandth), line.GetIntersections(Triangle, Thousandth), p => Distance3.DistanceTo(Triangle, p));
                AssertOnTheLine(ray, ray.GetIntersections(Square, Thousandth), line.GetIntersections(Square, Thousandth), p => Distance3.DistanceTo(Square, p, Thousandth));
                AssertOnTheLine(ray, ray.GetIntersections(face, Thousandth), line.GetIntersections(face, Thousandth), p => Distance3.DistanceTo(face, p, Thousandth));
            }
        }

        /// <summary>
        /// Each point of the ray is one of its line's, but for its origin where the line meets the shape only behind it, or
        /// lies level over it only behind it, and for a point the ray holds within the tolerance of the shape where the line names
        /// a place too: the line names one place, the nearest, where it touches the shape at two, and the ray touching it at the
        /// other is given that.
        /// </summary>
        private static void AssertOnTheLine(GeoRay3 ray, GeoPoint3[] onRay, GeoPoint3[] onLine, Func<GeoPoint3, double> gapToShape)
        {
            foreach (GeoPoint3 point in onRay)
            {
                bool atOrigin = !(point.DistanceTo(ray.Origin) > Thousandth.EqualPoint);
                bool behind = Array.TrueForAll(onLine, p => ray.Origin.GetVectorTo(p).DotProduct(ray.Direction) < 0.0);
                bool held = onLine.Length > 0 && ray.IsPointOn(point, Thousandth) && !(gapToShape(point) > Thousandth.EqualPoint);

                Assert.True(Array.Exists(onLine, p => !(p.DistanceTo(point) > Thousandth.EqualPoint)) || (atOrigin && behind) || held, $"{ray}: {point}");
            }
        }
    }
}
