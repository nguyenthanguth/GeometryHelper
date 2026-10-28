using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// At a tolerance of nought the crossing of a segment, or of a line without ends, with a face is held to the rounding of
    /// numbers the size of the coordinates, on its plane and past its rim, as a ray's is. The crossing a segment finds on a
    /// plane stands off it by that rounding, and was refused by nearly every triangle and face turned about in space: a segment
    /// through the middle of one crossed nothing, and one through a turned cylinder was given one crossing or none.
    /// </summary>
    public class TurnedBodyAtToleranceNoughtFaultG1qTests
    {
        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        /// <summary>The axis of the turned cylinder: its Y axis, leaning in space.</summary>
        private static readonly GeoCoordinateSystem3 Turned = new GeoCoordinateSystem3(
            GeoPoint3.Origin, new GeoVector3(0.6, 0.8, 0.0), new GeoVector3(-0.48, 0.36, 0.8));

        /// <summary>A cylinder of radius 20 and height 50 about the axis.</summary>
        private static GeoSolid3 Cylinder()
            => GeoSolid3.Revolve(
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 50), new GeoPoint2(0, 50)),
                Turned, 2 * Math.PI, 0.01, Tolerance.Default);

        /// <summary>The point at a distance from the axis, an angle round it and a height along it.</summary>
        private static GeoPoint3 At(double radius, double angle, double height)
            => Turned.Origin.Add(Turned.XAxis.Multiply(radius * Math.Cos(angle))).Add(Turned.YAxis.Multiply(height))
                .Add(Turned.ZAxis.Multiply(radius * Math.Sin(angle)));

        /// <summary>A direction leaning every way in space, a different one for each number.</summary>
        private static GeoVector3 Leaning(int i)
            => new GeoVector3(Math.Cos(0.7 * i) * Math.Sin(1.1 + 0.3 * i), Math.Sin(0.7 * i) * Math.Sin(1.1 + 0.3 * i), Math.Cos(1.1 + 0.3 * i));

        /// <summary>
        /// A hundred triangles turned about in space, their sides 60 to 100 long, each with the segment from 30 under its
        /// middle to 30 over it, leaning off its normal.
        /// </summary>
        private static IEnumerable<(GeoTriangle3 Triangle, GeoLine3 Segment)> TurnedTriangles()
        {
            for (int i = 0; i < 100; i++)
            {
                var corner = new GeoPoint3(17.3 + 0.61 * i, 42.9 - 0.37 * i, 8.1 + 0.53 * i);
                GeoPoint3 second = corner.Add(Leaning(i).Multiply(60 + 0.4 * i));
                GeoPoint3 third = corner.Add(Leaning(i + 37).Multiply(100 - 0.3 * i));
                var triangle = new GeoTriangle3(corner, second, third);
                GeoVector3 across = triangle.Normal.Add(Leaning(i + 71).Multiply(0.3));

                yield return (triangle, new GeoLine3(triangle.Centroid.Add(across.Multiply(-30)), triangle.Centroid.Add(across.Multiply(30))));
            }
        }

        [Fact]
        public void AtToleranceNought_ASegmentThroughTheMiddleOfATurnedTriangle_CrossesIt()
        {
            var missed = new List<int>();
            int i = 0;

            foreach ((GeoTriangle3 triangle, GeoLine3 segment) in TurnedTriangles())
            {
                if (!segment.TryIntersectWith(triangle, out GeoPoint3 crossing, Nought) || crossing.DistanceTo(triangle.Centroid) > 1E-9)
                {
                    missed.Add(i);
                }

                i++;
            }

            Assert.Empty(missed);
        }

        [Fact]
        public void AtToleranceNought_ASegmentThroughTheMiddleOfATurnedFace_CrossesIt()
        {
            var missed = new List<int>();
            int i = 0;

            foreach ((GeoTriangle3 triangle, GeoLine3 segment) in TurnedTriangles())
            {
                var face = new GeoFace3(new GeoPolygon3(triangle.A, triangle.B, triangle.C));

                if (!segment.TryIntersectWith(face, out GeoPoint3 crossing, Nought) || crossing.DistanceTo(triangle.Centroid) > 1E-9)
                {
                    missed.Add(i);
                }

                i++;
            }

            Assert.Empty(missed);
        }

        [Fact]
        public void AtToleranceNought_ALineWithoutEndsThroughTheMiddleOfATurnedTriangleOrFace_CrossesIt()
        {
            var missed = new List<int>();
            int i = 0;

            foreach ((GeoTriangle3 triangle, GeoLine3 segment) in TurnedTriangles())
            {
                var line = new GeoXLine3(segment.StartPoint, segment.Direction);
                var face = new GeoFace3(new GeoPolygon3(triangle.A, triangle.B, triangle.C));

                if (!line.TryIntersectWith(triangle, out _, Nought) || !line.TryIntersectWith(face, out _, Nought))
                {
                    missed.Add(i);
                }

                i++;
            }

            Assert.Empty(missed);
        }

        /// <summary>
        /// A hundred lines through points of the axis of the turned cylinder, 5 to 45 up and up to 10 off it, leaning every way.
        /// </summary>
        private static IEnumerable<(GeoPoint3 Through, GeoVector3 Along)> ThroughTheCylinder()
        {
            for (int i = 0; i < 100; i++)
            {
                yield return (At(0.1 * i, 2.3 * i, 5 + 0.4 * i), Leaning(i));
            }
        }

        [Fact]
        public void AtToleranceNought_ASegmentThroughATurnedCylinder_IsGivenItsTwoCrossings()
        {
            GeoSolid3 body = Cylinder();
            var missed = new List<int>();
            int i = 0;

            foreach ((GeoPoint3 through, GeoVector3 along) in ThroughTheCylinder())
            {
                var segment = new GeoLine3(through.Add(along.Multiply(-200)), through.Add(along.Multiply(200)));

                if (segment.GetIntersections(body, Nought).Length != 2)
                {
                    missed.Add(i);
                }

                i++;
            }

            Assert.Empty(missed);
        }

        [Fact]
        public void AtToleranceNought_ALineWithoutEndsThroughATurnedCylinder_IsGivenItsTwoCrossings()
        {
            GeoSolid3 body = Cylinder();
            var prepared = new GeoPreparedSolid3(body, Nought);
            var missed = new List<int>();
            int i = 0;

            foreach ((GeoPoint3 through, GeoVector3 along) in ThroughTheCylinder())
            {
                var line = new GeoXLine3(through, along);

                if (line.GetIntersections(body, Nought).Length != 2 || prepared.GetIntersections(line, Nought).Length != 2)
                {
                    missed.Add(i);
                }

                i++;
            }

            Assert.Empty(missed);
        }

        [Theory]
        [InlineData(0.0, 5E-10)]
        [InlineData(1E6, 5E-10)]
        [InlineData(1E9, 1E-7)]
        public void AtToleranceNought_ASegmentEndingAHairOverABox_TouchesItAsItsDistanceSays(double far, double over)
        {
            // The box 100 by 80 by 50 out along each axis, the segment standing up from a hair over the middle of its top: the
            // body holds its lower end, to a billionth or 1E-15 of the coordinates, and it is nought from it, but the boxes were
            // asked first at the tolerance given and it touched nothing.
            GeoSolid3 box = new GeoObb3(new GeoPoint3(far + 50, far + 40, far + 25), 100, 80, 50).ToSolid();
            var segment = new GeoLine3(new GeoPoint3(far + 50, far + 40, far + 50 + over), new GeoPoint3(far + 50, far + 40, far + 80));

            Assert.Equal(0.0, segment.DistanceTo(box, Nought));
            Assert.True(segment.CollidesWith(box, Nought));
        }

        [Theory]
        [InlineData(1E-12)]
        [InlineData(1E-10)]
        [InlineData(3E-10)]
        public void AtAToleranceBelowABillionth_ASegmentEndingAHairOverABox_TouchesItAsItsDistanceSays(double tolerance)
        {
            // The box is asked first as far apart as the body holds a point, a billionth here, whatever the tolerance below it.
            GeoSolid3 box = new GeoObb3(new GeoPoint3(50, 40, 25), 100, 80, 50).ToSolid();
            var segment = new GeoLine3(new GeoPoint3(50, 40, 50 + 5E-10), new GeoPoint3(50, 40, 80));
            var within = new Tolerance(tolerance, tolerance, Tolerance.DefaultEqualAngleRad, tolerance);

            Assert.Equal(0.0, segment.DistanceTo(box, within));
            Assert.True(segment.CollidesWith(box, within));
        }

        [Fact]
        public void ASegmentWithAnEndThatIsNotANumber_TouchesNoBox()
        {
            // Its box is no box, and the gap to the body's is not a number: it touches nothing, as it never did.
            GeoSolid3 box = new GeoObb3(new GeoPoint3(50, 40, 25), 100, 80, 50).ToSolid();
            var segment = new GeoLine3(new GeoPoint3(50, 40, double.NaN), new GeoPoint3(50, 40, 10));

            Assert.False(segment.CollidesWith(box, Tolerance.Default));
        }

        /// <summary>
        /// The box 100 by 80 by 50 turned about in space, and on each of its sides 20 points with the segment from 10 off the side
        /// to each, leaning off its normal.
        /// </summary>
        private static IEnumerable<(GeoSolid3 Box, GeoFace3 Side, GeoTriangle3 Under, GeoLine3 Segment)> SegmentsEndingOnATurnedBox()
        {
            GeoSolid3 box = new GeoObb3(new GeoPoint3(10, 20, 30), 100, 80, 50, Turned.XAxis, Turned.YAxis).ToSolid();
            var middle = new GeoPoint3(10, 20, 30);

            foreach (GeoFace3 side in box.Faces)
            {
                GeoVector3 normal = side.GetPlane().Normal;

                if (normal.DotProduct(middle.GetVectorTo(side.Boundary.Vertices[0])) < 0)
                {
                    normal = normal.Multiply(-1);
                }

                GeoTriangle3 under = side.TriangulateSurface(Tolerance.Default)[0];

                for (int i = 0; i < 20; i++)
                {
                    double u = 0.1 + 0.017 * i, v = 0.12 + 0.013 * ((7 * i) % 20);
                    GeoPoint3 point = under.A.Add(under.A.GetVectorTo(under.B).Multiply(u)).Add(under.A.GetVectorTo(under.C).Multiply(v));
                    GeoPoint3 start = point.Add(normal.Multiply(10)).Add(Leaning(i).Multiply(3));

                    yield return (box, side, under, new GeoLine3(start, point));
                }
            }
        }

        [Fact]
        public void AtToleranceNought_ASegmentEndingOnATurnedBox_IsGivenItsEnd()
        {
            var missed = new List<GeoPoint3>();

            foreach ((GeoSolid3 box, _, _, GeoLine3 segment) in SegmentsEndingOnATurnedBox())
            {
                GeoPoint3[] crossings = segment.GetIntersections(box, Nought);

                if (crossings.Length != 1 || crossings[0].DistanceTo(segment.EndPoint) > 1E-12)
                {
                    missed.Add(segment.EndPoint);
                }
            }

            Assert.Empty(missed);
        }

        [Fact]
        public void AtToleranceNought_ASegmentEndingOnATurnedSide_CrossesItsTriangleAndFaceWhereItTouchesThem()
        {
            var misread = new List<GeoPoint3>();

            foreach ((_, GeoFace3 side, GeoTriangle3 under, GeoLine3 segment) in SegmentsEndingOnATurnedBox())
            {
                if (segment.TryIntersectWith(under, out _, Nought) != segment.CollidesWith(under, Nought)
                    || segment.TryIntersectWith(side, out _, Nought) != segment.CollidesWith(side, Nought))
                {
                    misread.Add(segment.EndPoint);
                }
            }

            Assert.Empty(misread);
        }

        [Theory]
        [InlineData(1E6)]
        [InlineData(1E8)]
        public void AtToleranceNought_ASegmentFromFarOffThroughTheSeamsOfACylinder_IsGivenEachCrossingOnce(double far)
        {
            // G1b's ray along the seam at 45 degrees, through the axis 20 up, as a segment from as far off to as far beyond: its
            // crossings, worked out from its far ends, were rounded off the planes of both sides at each seam.
            double chord = 20 * (1 - Math.Cos(Math.PI / 64)) * 1.0000001;
            GeoSolid3 body = GeoSolid3.Revolve(
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 50), new GeoPoint2(0, 50)),
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), 2 * Math.PI, chord, Tolerance.Default);
            var segment = new GeoLine3(new GeoPoint3(-far, 20, -far), new GeoPoint3(far, 20, far));

            GeoPoint3[] crossings = segment.GetIntersections(body, Nought);

            Assert.Equal(2, crossings.Length);
            Assert.Equal(0.0, crossings[0].DistanceTo(new GeoPoint3(-20 / Math.Sqrt(2), 20, -20 / Math.Sqrt(2))), 9);
        }
    }
}
