using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// At a tolerance of nought a crossing of a face is held to the rounding of numbers the size of the coordinates, on its
    /// plane and past its rim. The crossing a ray finds on a plane stands off it by that rounding, and was refused on every
    /// face of a body turned about in space or made by <c>Revolve</c>: a ray from inside a turned cylinder crossed nothing, a
    /// point inside it was read outside, and one outside it, its rays crossing only some faces, inside. A ray through the seam
    /// of two triangles was held by both or by neither.
    /// </summary>
    public class TurnedBodyAtToleranceNoughtFaultG1bTests
    {
        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        /// <summary>The axis of the turned bodies: their Y axis, leaning in space.</summary>
        private static readonly GeoCoordinateSystem3 Turned = new GeoCoordinateSystem3(
            GeoPoint3.Origin, new GeoVector3(0.6, 0.8, 0.0), new GeoVector3(-0.48, 0.36, 0.8));

        /// <summary>A cylinder of radius 20 and height 50, or a cone of radius 20 at its foot and height 50, about the axis.</summary>
        private static GeoSolid3 Body(string shape)
        {
            GeoPolygon2 profile = shape == "cylinder"
                ? new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 50), new GeoPoint2(0, 50))
                : new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(0, 50));

            return GeoSolid3.Revolve(profile, Turned, 2 * Math.PI, 0.01, Tolerance.Default);
        }

        /// <summary>The point at a distance from the axis, an angle round it and a height along it.</summary>
        private static GeoPoint3 At(double radius, double angle, double height)
            => Turned.Origin.Add(Turned.XAxis.Multiply(radius * Math.Cos(angle))).Add(Turned.YAxis.Multiply(height))
                .Add(Turned.ZAxis.Multiply(radius * Math.Sin(angle)));

        /// <summary>A hundred points inside the body, from a tenth to eight tenths of the way out from the axis.</summary>
        private static IEnumerable<GeoPoint3> Inside(string shape)
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    double height = 5 + 4 * i;
                    double reach = shape == "cylinder" ? 20 : 20 * (1 - height / 50);

                    yield return At(0.8 * reach * (j + 1) / 10, 0.7 * j + 0.3 * i, height);
                }
            }
        }

        [Theory]
        [InlineData("cylinder")]
        [InlineData("cone")]
        public void AtToleranceNought_ARayFromInsideATurnedBody_CrossesItsSurfaceOnce(string shape)
        {
            GeoSolid3 body = Body(shape);
            var prepared = new GeoPreparedSolid3(body, Nought);
            var missed = new List<GeoPoint3>();
            int k = 0;

            foreach (GeoPoint3 point in Inside(shape))
            {
                var ray = new GeoRay3(point, new GeoVector3(0.3 + 0.1 * (k / 10), -0.2 + 0.05 * (k % 10), 0.5));
                k++;

                if (prepared.GetIntersections(ray, Nought).Length != 1 || ray.GetIntersections(body, Nought).Length != 1)
                {
                    missed.Add(point);
                }
            }

            Assert.Empty(missed);
        }

        [Theory]
        [InlineData("cylinder")]
        [InlineData("cone")]
        public void AtToleranceNought_APointInsideATurnedBody_IsReadInside(string shape)
        {
            GeoSolid3 body = Body(shape);
            var prepared = new GeoPreparedSolid3(body, Nought);
            var misread = new List<GeoPoint3>();

            foreach (GeoPoint3 point in Inside(shape))
            {
                if (prepared.Locate(point, Nought) != PointLocation.Inside || body.Locate(point, Nought) != PointLocation.Inside)
                {
                    misread.Add(point);
                }
            }

            Assert.Empty(misread);
        }

        [Theory]
        [InlineData(1.6, 4.0)]
        [InlineData(1.5, 3.0)]
        public void AtToleranceNought_APointOneAndAHalfOutsideATurnedCylinder_IsReadOutside(double beyond, double angle)
        {
            // Halfway up the side, 1.6 or 1.5 out from the radius: both were read inside by the prepared body, the first by
            // the body too.
            GeoSolid3 body = Body("cylinder");
            var prepared = new GeoPreparedSolid3(body, Nought);
            GeoPoint3 point = At(20 + beyond, angle, 25);

            Assert.Equal(PointLocation.OutSide, prepared.Locate(point, Nought));
            Assert.Equal(PointLocation.OutSide, body.Locate(point, Nought));
            Assert.False(prepared.Contains(point, Nought));
        }

        [Fact]
        public void AtToleranceNought_APointOnTheAxisOfACylinder_IsReadInsideThoughItsFirstRayRunsThroughASeam()
        {
            // The cylinder of radius 20 standing on the floor along Y, cut into sides with a seam at 45 degrees: the first ray
            // Locate casts from a point on the axis runs through that seam, and was held by neither side.
            double chord = 20 * (1 - Math.Cos(Math.PI / 64)) * 1.0000001;
            GeoSolid3 body = GeoSolid3.Revolve(
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 50), new GeoPoint2(0, 50)),
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), 2 * Math.PI, chord, Tolerance.Default);
            var prepared = new GeoPreparedSolid3(body, Nought);
            var misread = new List<double>();

            for (int height = 1; height < 50; height++)
            {
                var point = new GeoPoint3(0, height, 0);

                if (prepared.Locate(point, Nought) != PointLocation.Inside || body.Locate(point, Nought) != PointLocation.Inside)
                {
                    misread.Add(height);
                }
            }

            Assert.Empty(misread);
        }

        [Fact]
        public void AtToleranceNought_ARayThroughTheSeamsOfACylinder_IsGivenEachCrossingOnce()
        {
            // The ray from 60 out along the seam at 45 degrees, through the axis 20 up, leaves by the seam opposite.
            double chord = 20 * (1 - Math.Cos(Math.PI / 64)) * 1.0000001;
            GeoSolid3 body = GeoSolid3.Revolve(
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 50), new GeoPoint2(0, 50)),
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), 2 * Math.PI, chord, Tolerance.Default);
            var prepared = new GeoPreparedSolid3(body, Nought);
            var ray = new GeoRay3(new GeoPoint3(-60, 20, -60), new GeoVector3(1, 0, 1));

            GeoPoint3[] crossings = prepared.GetIntersections(ray, Nought);

            Assert.Equal(2, crossings.Length);
            Assert.Equal(2, ray.GetIntersections(body, Nought).Length);
            Assert.Equal(0.0, crossings[0].DistanceTo(new GeoPoint3(-20 / Math.Sqrt(2), 20, -20 / Math.Sqrt(2))), 9);
        }

        [Theory]
        [InlineData(1E4)]
        [InlineData(1E6)]
        [InlineData(1E8)]
        public void AtToleranceNought_ARayFromFarOffThroughTheSeamsOfACylinder_IsGivenEachCrossingOnce(double far)
        {
            // The same ray along the seam at 45 degrees, from far out along it: its crossings, worked out from there, were rounded
            // off the planes of both sides at each seam, and were given twice or not at all.
            double chord = 20 * (1 - Math.Cos(Math.PI / 64)) * 1.0000001;
            GeoSolid3 body = GeoSolid3.Revolve(
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 50), new GeoPoint2(0, 50)),
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), 2 * Math.PI, chord, Tolerance.Default);
            var prepared = new GeoPreparedSolid3(body, Nought);
            var ray = new GeoRay3(new GeoPoint3(-far, 20, -far), new GeoVector3(1, 0, 1));
            var entry = new GeoPoint3(-20 / Math.Sqrt(2), 20, -20 / Math.Sqrt(2));

            GeoPoint3[] crossings = prepared.GetIntersections(ray, Nought);
            GeoPoint3[] ofBody = ray.GetIntersections(body, Nought);

            Assert.Equal(2, crossings.Length);
            Assert.Equal(2, ofBody.Length);
            Assert.Equal(0.0, crossings[0].DistanceTo(entry), 9);
            Assert.Equal(0.0, ofBody[0].DistanceTo(entry), 9);
        }

        [Fact]
        public void AtToleranceNought_ARayThroughTheAxisOfACylinderOfAThousandSides_IsGivenTheFarSeamOnce()
        {
            // The cylinder of radius 20 and 1 080 faces along Y, the ray from 60 out towards a point of its axis 43.38 up: it
            // leaves by a seam whose two crossings, each worked out from its own side, lie 5.2E-14 apart along the ray, more
            // than the rounding of 4.9E-14, and it was given that seam twice.
            double chord = 20 * (1 - Math.Cos(Math.PI / 1080)) * 1.0000001;
            GeoSolid3 body = GeoSolid3.Revolve(
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 50), new GeoPoint2(0, 50)),
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), 2 * Math.PI, chord, Tolerance.Default);
            var prepared = new GeoPreparedSolid3(body, Nought);
            const double Angle = -2.193297093339541, Height = 43.38281158515383;
            var from = new GeoPoint3(60 * Math.Cos(Angle), Height, 60 * Math.Sin(Angle));
            var ray = new GeoRay3(from, from.GetVectorTo(new GeoPoint3(0, Height, 0)));

            Assert.Equal(2, ray.GetIntersections(body, Nought).Length);
            Assert.Equal(2, prepared.GetIntersections(ray, Nought).Length);
        }

        [Fact]
        public void AtToleranceNought_APointAHairInsideTheFloorOfABox_IsReadInside()
        {
            // The box 100 by 80 by 50 standing on the floor, the point 3.03E-13 over it: the floor behind it, rounded within a
            // hundred-trillionth, was counted as crossed by the ray going up, and the point read outside.
            GeoSolid3 box = new GeoObb3(new GeoPoint3(50, 40, 25), 100, 80, 50).ToSolid();
            var prepared = new GeoPreparedSolid3(box, Nought);
            var point = new GeoPoint3(50, 40, 3.03E-13);

            Assert.Equal(PointLocation.Inside, box.Locate(point, Nought));
            Assert.Equal(PointLocation.Inside, prepared.Locate(point, Nought));
        }

        [Fact]
        public void APointThreeBillionthsInsideTheFloorOfABoxAMillionOut_IsNoDistanceFromIt()
        {
            // The box 100 by 80 by 50 a million out along each axis, the point 3.03E-9 over its floor: at the default tolerance
            // it is in the box, as the body read it before.
            const double Out = 1E6;
            GeoSolid3 box = new GeoObb3(new GeoPoint3(Out + 50, Out + 40, Out + 25), 100, 80, 50).ToSolid();
            var point = new GeoPoint3(Out + 50, Out + 40, Out + 3.03E-9);

            Assert.Equal(0.0, point.DistanceTo(box, Tolerance.Default));
        }

        [Fact]
        public void AtToleranceNought_ARayFromAThousandMillionOffPassingBesideABox_IsGivenNoPoint()
        {
            // Up along Z from a thousand million under the box 100 by 80 by 50, 5E-6 beyond its side x = 100: it misses the box,
            // as CollidesWith says, and was given two points.
            GeoSolid3 box = new GeoObb3(new GeoPoint3(50, 40, 25), 100, 80, 50).ToSolid();
            var prepared = new GeoPreparedSolid3(box, Nought);
            var ray = new GeoRay3(new GeoPoint3(100 + 5E-6, 40, -1E9), new GeoVector3(0, 0, 1));

            Assert.False(ray.CollidesWith(box, Nought));
            Assert.Empty(ray.GetIntersections(box, Nought));
            Assert.Empty(prepared.GetIntersections(ray, Nought));
        }
    }
}
