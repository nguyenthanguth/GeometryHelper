using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A ray or a line without ends lying level over a triangle, a polygon or a face, the part of it over the shape within the planar
    /// tolerance of the shape's plane at both its ends, meets it nowhere, as a segment lying there does; against a box a ray and a line
    /// are read as the segment from the origin past the box, as they were.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ray from 0.0004 over a triangle rising 1E-6 was given its origin, which its crossing of the plane read as a touch, where a
    /// segment along it was given nothing; and a line without ends was read level by where it was cut, a millimetre past the shape,
    /// so that 0.0009 over a polygon and rising 1E-6 it was given the point over the polygon's rim.
    /// </para>
    /// <para>
    /// The ray is judged so wherever it starts within the planar tolerance of the plane, not only where its crossing is its origin to
    /// the bit: from the plane itself it was given nothing, and from 1E-12 under it the crossing a hair ahead, (10.000001, 20, 0).
    /// </para>
    /// </remarks>
    public class RayLevelFaultLN8Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly GeoTriangle3 Triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

        private static readonly GeoPolygon3 Square = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));

        private static readonly GeoFace3 Plate = new GeoFace3(Square);

        /// <summary>The box 10 by 10 by 10 from the origin.</summary>
        private static readonly GeoObb3 Box = new GeoObb3(new GeoPoint3(5, 5, 5), 10, 10, 10);

        private static GeoVector3 Rising(double slope) => new GeoVector3(1, 0.5, slope).Normalize();

        [Theory]
        [InlineData(0.0, 1E-6)]
        [InlineData(1E-12, 1E-6)]
        [InlineData(-1E-12, 1E-6)]
        [InlineData(0.0004, 1E-6)]
        [InlineData(0.0009, 1E-6)]
        [InlineData(-0.0004, 4E-6)]
        public void ARayLyingLevelOverTheShape_IsGivenNothingAsTheSegmentAlongItIs(double height, double slope)
        {
            var origin = new GeoPoint3(10, 20, height);
            var ray = new GeoRay3(origin, Rising(slope));
            var segment = new GeoLine3(origin, origin.Add(Rising(slope).Multiply(40)));

            Assert.True(ray.CollidesWith(Triangle, Thousandth));
            Assert.Empty(segment.GetIntersections(Triangle, Thousandth));
            Assert.Empty(ray.GetIntersections(Triangle, Thousandth));
            Assert.Empty(ray.GetIntersections(Square, Thousandth));
            Assert.Empty(ray.GetIntersections(Plate, Thousandth));
            Assert.Empty(Triangle.GetIntersections(ray, Thousandth));
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(1E5, 0.0)]
        [InlineData(0.0, 0.7)]
        [InlineData(1E5, 2.1)]
        public void ARayLyingLevelOverATurnedTriangle_IsGivenNothing_FarOutOrNot(double offset, double angle)
        {
            // The triangle and the rays a hair either side of its plane, turned about an axis askew and moved out.
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(offset, -2.0 * offset, offset)).Multiply(
                GeoTransform3.RotationAxis(new GeoPoint3(0, 0, 0), new GeoVector3(0.3, -0.5, 0.8), angle));
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0).TransformBy(move), new GeoPoint3(100, 0, 0).TransformBy(move), new GeoPoint3(0, 100, 0).TransformBy(move));

            foreach (double height in new[] { 0.0, 1E-9, -1E-9, 3E-4, -3E-4 })
            {
                var ray = new GeoRay3(new GeoPoint3(10, 20, height).TransformBy(move), Rising(1E-6).TransformBy(move));

                Assert.Empty(ray.GetIntersections(triangle, Thousandth));
            }
        }

        [Theory]
        [InlineData(0.0004, 1E-6)]
        [InlineData(0.0009, 1E-6)]
        public void ALineLyingLevelOverTheShape_IsGivenNothing(double height, double slope)
        {
            var line = new GeoXLine3(new GeoPoint3(10, 20, height), Rising(slope));

            Assert.True(line.CollidesWith(Square, Thousandth));
            Assert.Empty(line.GetIntersections(Triangle, Thousandth));
            Assert.Empty(line.GetIntersections(Square, Thousandth));
            Assert.Empty(line.GetIntersections(Plate, Thousandth));
        }

        [Fact]
        public void ARayRisingOffTheShapeOverIt_IsStillGivenItsOrigin()
        {
            // 0.0009 over the triangle and rising 4E-6, it stands 0.0011 over the hypotenuse where it leaves the triangle: it touches
            // the triangle at its origin, as the segment along it does.
            var origin = new GeoPoint3(10, 20, 0.0009);
            var ray = new GeoRay3(origin, Rising(4E-6));
            var segment = new GeoLine3(origin, origin.Add(Rising(4E-6).Multiply(60)));

            Assert.Equal(0.0, Assert.Single(segment.GetIntersections(Triangle, Thousandth)).DistanceTo(origin), 9);
            Assert.Equal(0.0, Assert.Single(ray.GetIntersections(Triangle, Thousandth)).DistanceTo(origin), 9);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(1E5, 0.0)]
        [InlineData(0.0, 0.7)]
        [InlineData(1E5, 2.1)]
        public void ARayFromJustUnderTheShapeThatCrossesItAtASlant_IsGivenTheCrossing(double offset, double angle)
        {
            // From 0.0009 under the plane and rising 1.5E-5, the ray crosses it 60 along, and runs from -9E-4 to +3E-4 over the
            // triangle: both ends of the part over it lie within the planar tolerance, but it crosses the plane there, as the segment
            // from its origin and the line along it are seen to.
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(offset, -2.0 * offset, offset)).Multiply(
                GeoTransform3.RotationAxis(new GeoPoint3(0, 0, 0), new GeoVector3(0.3, -0.5, 0.8), angle));
            GeoPoint3 F(double x, double y, double z) => new GeoPoint3(x, y, z).TransformBy(move);
            var triangle = new GeoTriangle3(F(0, 0, 0), F(100, 0, 0), F(0, 100, 0));
            var square = new GeoPolygon3(F(0, 0, 0), F(100, 0, 0), F(100, 100, 0), F(0, 100, 0));
            GeoVector3 along = new GeoVector3(1, 0, 1.5E-5).Normalize().TransformBy(move);

            foreach (double x in new[] { -20.0, -5.0, -0.5, 5.0 })
            {
                var ray = new GeoRay3(F(x, 20, -0.0009), along);
                GeoPoint3 crossing = F(x + 60, 20, 0);
                var segment = new GeoLine3(ray.Origin, F(x + 120, 20, 0.0009));

                Assert.InRange(Assert.Single(segment.GetIntersections(triangle, Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
                Assert.InRange(Assert.Single(new GeoXLine3(ray.Origin, along).GetIntersections(triangle, Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
                Assert.InRange(Assert.Single(ray.GetIntersections(triangle, Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
                Assert.InRange(Assert.Single(ray.GetIntersections(square, Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
                Assert.InRange(Assert.Single(ray.GetIntersections(new GeoFace3(square), Thousandth)).DistanceTo(crossing), 0.0, 1E-5);
            }
        }

        [Fact]
        public void ARayCrossingTheShape_IsGivenTheCrossing()
        {
            var ray = new GeoRay3(new GeoPoint3(10, 20, 5), new GeoVector3(0.1, 0.2, -1).Normalize());
            GeoPoint3 crossing = Assert.Single(ray.GetIntersections(Triangle, Thousandth));

            Assert.Equal(0.0, crossing.Z, 9);
        }

        private static void AssertPoints(GeoPoint3[] points, params GeoPoint3[] expected)
        {
            Assert.Equal(expected.Length, points.Length);

            foreach (GeoPoint3 point in expected)
            {
                Assert.Contains(points, p => p.DistanceTo(point) < 1E-9);
            }
        }

        [Fact]
        public void RaysAndLinesAlongTheFacesOfABox_AreReadAsTheSegmentFromTheOriginPastIt()
        {
            // Within the tolerance of the top face, from inside, from beside the box and dipping across it, and from an edge: the crossings
            // of the neighbouring faces, as the segment from the origin past the box is given them.
            AssertPoints(new GeoRay3(new GeoPoint3(5, 5, 9.9995), GeoVector3.XAxis).GetIntersections(Box, Thousandth), new GeoPoint3(10, 5, 9.9995));
            AssertPoints(new GeoXLine3(new GeoPoint3(5, 5, 9.9995), GeoVector3.XAxis).GetIntersections(Box, Thousandth), new GeoPoint3(0, 5, 9.9995), new GeoPoint3(10, 5, 9.9995));
            Assert.Equal(2, new GeoRay3(new GeoPoint3(-5, 5, 10.0005), GeoVector3.XAxis).GetIntersections(Box, Thousandth).Length);
            Assert.Equal(2, new GeoRay3(new GeoPoint3(5, 5, 10.0005), new GeoVector3(1, 0, -2E-4)).GetIntersections(Box, Thousandth).Length);
            Assert.Single(new GeoRay3(new GeoPoint3(9.9995, 5, 9.9995), GeoVector3.YAxis).GetIntersections(Box, Thousandth));
        }

        [Fact]
        public void ARayAndTheSegmentFromItsOriginPastABox_MeetItAlike()
        {
            var random = new Random(8121);

            for (int i = 0; i < 2000; i++)
            {
                double[] at = { random.NextDouble() * 10, random.NextDouble() * 10, random.NextDouble() * 10 };
                int axis = random.Next(3);
                at[axis] = (random.Next(2) == 0 ? 0 : 10) + (random.NextDouble() * 2 - 1) * 0.0015;
                var direction = new GeoVector3(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, (random.NextDouble() * 2 - 1) * (i % 2 == 0 ? 1E-4 : 1.0));

                if (direction.Length < 0.1)
                {
                    continue;
                }

                var ray = new GeoRay3(new GeoPoint3(at[0], at[1], at[2]), direction);
                double reach = 0.0;

                foreach (GeoPoint3 corner in Box.GetCorners())
                {
                    reach = Math.Max(reach, ray.GetDistanceAtPoint(corner));
                }

                GeoPoint3[] fromRay = ray.GetIntersections(Box, Thousandth);
                GeoPoint3[] fromSegment = Box.GetIntersections(ray.ToLine(Math.Max(reach, 0.0) + Thousandth.EqualPoint), Thousandth);

                Assert.Equal(fromSegment, fromRay);
            }
        }
    }
}
