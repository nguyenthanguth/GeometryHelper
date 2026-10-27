using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A body far from the origin holds a point as one at the origin does. Whether a point is in a body is asked to within
    /// rounding, a billionth, which 5E7 out is less than the rounding of the coordinates: the crossings of the rays counted
    /// from the point stood 1.9E-9 off the planes of the faces they crossed, were refused, and the count came out wrong. A ray
    /// starting 0.0516 outside a U turned about in space collided with it, rays 0.05 off the side of a cylinder collided with
    /// it, and points 10 inside the cylinder were 10 from it. Further out than a million the reading is 1E-15 of the largest
    /// coordinate now, and the list of where a ray meets a body and its collision read the box of the body and the origin as
    /// far: a ray from within that reach over a box, pointing away, collides and is given a point, and one from three times
    /// as far does not. A point at infinity or not a number is read to a billionth, as before.
    /// </summary>
    public class Solid3FaultG1eFarOutTests
    {
        /// <summary>A point tolerance and a planar one of a hundredth.</summary>
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, 1E-5, Math.PI / 180, 1E-2);

        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        [Theory]
        [InlineData(1.0)]
        [InlineData(0.0)]
        public void ARayStartingFiveHundredthsOutsideAUTurnedAboutInSpace_DoesNotCollideWithItFiftyMillionOutAsAtTheOrigin(double far)
        {
            // The origin stands 0.0516 inside the gap of the U, off its inner wall, past the tolerance of five hundredths.
            var tolerance = new Tolerance(0.05, 1E-5, Math.PI / 180, 0.05);
            var c = new GeoPoint3(29891493.32507117 * far, -50881210.22604462 * far, 1098599.1457004962 * far);
            var u = new GeoVector3(-0.4914452572907459, -0.8688336114330585, 0.060080901546344344);
            var n = new GeoVector3(-0.8703774655684305, 0.49238416531412854, 0.0009493042633146856);
            GeoVector3 v = n.CrossProduct(u);
            double[][] profile = { new[] { 0.0, 0 }, new[] { 100.0, 0 }, new[] { 100.0, 100 }, new[] { 80.0, 100 }, new[] { 80.0, 20 }, new[] { 20.0, 20 }, new[] { 20.0, 100 }, new[] { 0.0, 100 } };
            var corners = new GeoPoint3[profile.Length];

            for (int i = 0; i < profile.Length; i++)
            {
                corners[i] = c.Add(u.Multiply(profile[i][0])).Add(n.Multiply(profile[i][1]));
            }

            GeoSolid3 body = GeoSolid3.Extrude(new GeoPolygon3(corners), v.Multiply(100), tolerance);
            GeoPoint3 start = c.Add(u.Multiply(79.94838496076333)).Add(n.Multiply(20.077697498992876)).Add(v.Multiply(11.935216024022186));
            var ray = new GeoRay3(start, new GeoVector3(-0.2741142077055374, 0.9300847396931561, -0.24454811003947585));

            Assert.False(ray.CollidesWith(body, tolerance));
            Assert.InRange(ray.DistanceTo(body, tolerance), 0.0516, 0.0517);
        }

        /// <summary>
        /// A cylinder of radius 30 and height 100 made by turning a rectangle about an axis leaning in space, standing at the
        /// given multiple of (31 234 567.25, -42 345 678.5, 12 345 678.75), in 18 facets round its side.
        /// </summary>
        private static GeoSolid3 Cylinder(double far, out GeoVector3 axis)
        {
            axis = new GeoVector3(1, 2, 3).Multiply(1 / Math.Sqrt(14));
            var side = new GeoVector3(3, 0, -1).Multiply(1 / Math.Sqrt(10));
            var foot = new GeoPoint3(31234567.25 * far, -42345678.5 * far, 12345678.75 * far);
            var rectangle = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 100), new GeoPoint2(0, 100));

            return GeoSolid3.Revolve(rectangle, new GeoCoordinateSystem3(foot, side, axis), 2 * Math.PI, 0.5, Tolerance.Default);
        }

        /// <summary>Gets the middle of a face, by the mean of its corners, and its normal turned away from the axis.</summary>
        private static GeoPoint3 Middle(GeoFace3 face, GeoPoint3 foot, GeoVector3 axis, out GeoVector3 outward)
        {
            var sum = new GeoVector3(0, 0, 0);

            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                sum = sum.Add(foot.GetVectorTo(corner));
            }

            GeoPoint3 middle = foot.Add(sum.Multiply(1.0 / face.Boundary.Vertices.Count));
            GeoVector3 normal = face.GetPlane().Normal;
            GeoVector3 away = foot.GetVectorTo(middle).Subtract(axis.Multiply(foot.GetVectorTo(middle).DotProduct(axis)));

            outward = away.DotProduct(normal) > 0 ? normal : normal.Multiply(-1);
            return middle;
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(0.0)]
        public void ARayRunningAlongACylinderFiveHundredthsOffASideFacet_DoesNotCollideWithItFiftyMillionOutAsAtTheOrigin(double far)
        {
            GeoSolid3 body = Cylinder(far, out GeoVector3 axis);
            var foot = new GeoPoint3(31234567.25 * far, -42345678.5 * far, 12345678.75 * far);
            int facets = 0;

            foreach (GeoFace3 face in body.Faces)
            {
                GeoPoint3 middle = Middle(face, foot, axis, out GeoVector3 outward);

                if (Math.Abs(outward.DotProduct(axis)) > 0.5)
                {
                    continue;
                }

                facets++;

                // Five hundredths off, past the tolerance of a hundredth, it misses; five thousandths off, within it, it touches.
                Assert.False(new GeoRay3(middle.Add(outward.Multiply(0.05)), axis).CollidesWith(body, Hundredth), $"0.05 off the facet {facets}");
                Assert.True(new GeoRay3(middle.Add(outward.Multiply(0.005)), axis).CollidesWith(body, Hundredth), $"0.005 off the facet {facets}");
            }

            Assert.Equal(18, facets);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(0.0)]
        public void APointTenInsideACylinderUnderASideFacet_IsNoughtFromItFiftyMillionOutAsAtTheOrigin(double far)
        {
            GeoSolid3 body = Cylinder(far, out GeoVector3 axis);
            var foot = new GeoPoint3(31234567.25 * far, -42345678.5 * far, 12345678.75 * far);
            int facets = 0;

            foreach (GeoFace3 face in body.Faces)
            {
                GeoPoint3 middle = Middle(face, foot, axis, out GeoVector3 outward);

                if (Math.Abs(outward.DotProduct(axis)) > 0.5)
                {
                    continue;
                }

                facets++;

                GeoPoint3 inside = middle.Subtract(outward.Multiply(10));

                Assert.Equal(0.0, inside.DistanceTo(body, Hundredth));
                Assert.True(body.Contains(inside, Hundredth));
            }

            Assert.Equal(18, facets);
        }

        /// <summary>A box 100 by 80 by 50 square to the axes, its lowest corner at (at, at, at).</summary>
        private static GeoSolid3 BoxAt(double at) => new GeoObb3(new GeoPoint3(at + 50, at + 40, at + 25), 100, 80, 50).ToSolid();

        /// <summary>The middle of the top of <see cref="BoxAt"/>, lifted by a height.</summary>
        private static GeoPoint3 OverTheTop(double at, double height) => new GeoPoint3(at + 50, at + 40, at + 50 + height);

        [Theory]
        [InlineData(5E7, 2.235E-8)]
        [InlineData(5E8, 5E-7)]
        public void ARayFromWithinTheRoundingOverABoxFarOutAndPointingAway_IsGivenAPointAsItCollides(double at, double height)
        {
            // At a tolerance of nought, 2.2E-8 over the top 5E7 out and 4.8E-7 over it 5E8 out are within the 1E-15 of the
            // largest coordinate the body holds a point to: the ray collides, and was given nothing, its box read to a billionth.
            GeoSolid3 box = BoxAt(at);
            var ray = new GeoRay3(OverTheTop(at, height), new GeoVector3(0.3, 0.2, 1));

            Assert.True(ray.CollidesWith(box, Nought));
            Assert.Single(ray.GetIntersections(box, Nought));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E-6)]
        public void ARayFromAMillionthAndAHalfOverABoxTwoThousandMillionOut_CollidesAsItIsNoughtFromIt(double point)
        {
            // 1.431E-6 over the top 2E9 out, within the 2E-6 the body holds a point to there, and past the millionth outside its
            // box that the origin was asked of the body within.
            var tolerance = new Tolerance(point, 1E-9, 0.01, point);
            GeoSolid3 box = BoxAt(2E9);
            GeoPoint3 start = OverTheTop(2E9, 1.5E-6);
            var ray = new GeoRay3(start, new GeoVector3(0.3, 0.2, 1));

            Assert.Equal(0.0, start.DistanceTo(box, tolerance));
            Assert.Equal(0.0, ray.DistanceTo(box, tolerance));
            Assert.True(ray.CollidesWith(box, tolerance));
            Assert.Single(ray.GetIntersections(box, tolerance));
        }

        [Theory]
        [InlineData(5E7, 1.5E-7)]
        [InlineData(4E9, 1.2E-5)]
        public void APointThreeTimesAsFarOverABoxFarOutAsTheBodyHoldsAPoint_IsNotNoughtFromIt(double at, double height)
        {
            // The body holds a point 5E-8 outside it 5E7 out and 4E-6 outside it 4E9 out; three times that is outside it.
            GeoSolid3 box = BoxAt(at);
            GeoPoint3 start = OverTheTop(at, height);

            Assert.InRange(start.DistanceTo(box, Nought), height * 0.9, height * 1.1);
            Assert.False(new GeoRay3(start, new GeoVector3(0.3, 0.2, 1)).CollidesWith(box, Nought));
        }

        [Fact]
        public void APointAtInfinityOrNotANumber_IsAnsweredAsBeforeAndThrowsNothing()
        {
            // A coordinate with no size has no rounding to scale by: the body holds such a point to a billionth, as before.
            GeoSolid3 box = new GeoObb3(new GeoPoint3(50, 40, 25), 100, 80, 50).ToSolid();
            var infinite = new GeoPoint3(double.PositiveInfinity, 0, 0);

            Assert.Equal(double.NaN, infinite.DistanceTo(box, Tolerance.Default));
            Assert.Equal(double.NaN, infinite.DistanceTo(BoxAt(5E7), Tolerance.Default));
            Assert.True(new GeoLine3(infinite, new GeoPoint3(50, 40, 25)).CollidesWith(box, Tolerance.Default));
            Assert.Equal(double.NaN, new GeoSphere3(infinite, 1).DistanceTo(box, Tolerance.Default));
            Assert.False(new GeoRay3(new GeoPoint3(double.NaN, 10, 10), new GeoVector3(-1, 0, 0)).CollidesWith(box, Tolerance.Default));
        }
    }
}
