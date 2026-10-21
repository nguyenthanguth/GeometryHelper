using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The queries of a triangle read no area as <see cref="GeoTriangle3.IsDegenerate(Tolerance)"/> does, and a triangle with
    /// none is the segment it has collapsed to, which touches by its exact gap, rather than nothing.
    /// </summary>
    /// <remarks>
    /// The queries read no area by twice the area against the vector tolerance, an area against a number with no size: the
    /// triangle 0.003 by 0.003 had none, so a segment through its middle neither collided with it nor crossed it, its own
    /// centroid was outside it, and 11 137 of 27 000 pairs of a small or thin triangle and a segment, a ray, a polygon, a face, a
    /// triangle, a box, a body or an arc collided otherwise than their distance said. A triangle has no area now when its height
    /// over its longest side is within the point tolerance, and then it is read as its sides: it touches what comes within the
    /// tolerance of it, is crossed where its sides are, and holds the points on them.
    /// </remarks>
    public class TriangleFaultLN4ThinTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        /// <summary>The triangle 0.003 by 0.003 at the origin, in the plane z = 0: twice its area is 9E-6.</summary>
        private static readonly GeoTriangle3 Small = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(0.003, 0, 0), new GeoPoint3(0, 0.003, 0));

        /// <summary>The sliver 100 long and 1E-8 high along X.</summary>
        private static readonly GeoTriangle3 Sliver = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 1E-8, 0));

        [Fact]
        public void TheSmallTriangle_HasAreaAndTheSliverNone()
        {
            Assert.False(Small.IsDegenerate(Default));
            Assert.True(Sliver.IsDegenerate(Default));
        }

        [Fact]
        public void ASegmentThroughTheMiddleOfTheSmallTriangle_CollidesWithItAndCrossesIt()
        {
            var segment = new GeoLine3(new GeoPoint3(0.001, 0.001, -1), new GeoPoint3(0.001, 0.001, 1));

            Assert.True(segment.CollidesWith(Small, Default));
            Assert.True(Small.CollidesWith(segment, Default));
            Assert.True(Intersection3.TryIntersectWith(segment, Small, out GeoPoint3 crossing, Default));
            Assert.Equal(0.0, crossing.DistanceTo(new GeoPoint3(0.001, 0.001, 0)), 12);
            Assert.True(Intersection3.TryIntersectWith(new GeoRay3(segment.StartPoint, segment.Direction), Small, out crossing, Default));
            Assert.Equal(0.0, crossing.DistanceTo(new GeoPoint3(0.001, 0.001, 0)), 12);
        }

        [Fact]
        public void TheSmallTriangle_HoldsItsCentroid()
        {
            // The centroid stands 0.001 from two of its sides, so on them at a thousandth.
            Assert.Equal(PointLocation.OnSide, Small.Locate(Small.Centroid, Default));
            Assert.True(Small.Contains(Small.Centroid, Default));
            Assert.True(Small.TryGetBarycentric(Small.Centroid, out double u, out double v, out double w));
            Assert.Equal(1.0 / 3.0, u, 12);
            Assert.Equal(1.0 / 3.0, v, 12);
            Assert.Equal(1.0 / 3.0, w, 12);
        }

        [Fact]
        public void ASegmentThroughTheSliver_CollidesWithItAndCrossesItsLongestSide()
        {
            var segment = new GeoLine3(new GeoPoint3(50, 0, -1), new GeoPoint3(50, 0, 1));

            Assert.True(segment.CollidesWith(Sliver, Default));
            Assert.True(Sliver.CollidesWith(segment, Default));
            Assert.True(Intersection3.TryIntersectWith(segment, Sliver, out GeoPoint3 crossing, Default));
            Assert.True(crossing.DistanceTo(new GeoPoint3(50, 0, 0)) < 1E-12);
        }

        [Fact]
        public void TheSliver_TouchesAPolygonAndAFaceAcrossIt_WhereItsSidesCross()
        {
            var square = new GeoPolygon3(new[] { new GeoPoint3(40, -10, -1), new GeoPoint3(60, -10, 1), new GeoPoint3(60, 10, 1), new GeoPoint3(40, 10, -1) }, Default);

            Assert.True(Sliver.CollidesWith(square, Default));
            Assert.True(Assert.Single(Intersection3.GetIntersections(Sliver, square, Default)).DistanceTo(new GeoPoint3(50, 0, 0)) < 1E-9);
            Assert.True(Assert.Single(Intersection3.GetIntersections(Sliver, new GeoFace3(square), Default)).DistanceTo(new GeoPoint3(50, 0, 0)) < 1E-9);
        }

        [Fact]
        public void TheSliverAndAnUprightTriangle_CrossWhereItsSidesDo()
        {
            var upright = new GeoTriangle3(new GeoPoint3(50, -10, -10), new GeoPoint3(50, 10, -10), new GeoPoint3(50, 0, 10));

            Assert.True(Sliver.CollidesWith(upright, Default));
            Assert.True(upright.CollidesWith(Sliver, Default));
            Assert.True(Assert.Single(Intersection3.GetIntersections(Sliver, upright, Default)).DistanceTo(new GeoPoint3(50, 0, 0)) < 1E-9);
            Assert.True(Assert.Single(Intersection3.GetIntersections(upright, Sliver, Default)).DistanceTo(new GeoPoint3(50, 0, 0)) < 1E-9);
        }

        [Fact]
        public void TheSliver_HoldsThePointsOnItAndNoMore()
        {
            Assert.Equal(PointLocation.OnSide, Sliver.Locate(new GeoPoint3(50, 0, 0), Default));
            Assert.Equal(PointLocation.OnSide, Sliver.Locate(new GeoPoint3(30, 0.0005, 0), Default));
            Assert.Equal(PointLocation.OutSide, Sliver.Locate(new GeoPoint3(30, 0.002, 0), Default));
            Assert.False(Sliver.TryGetBarycentric(new GeoPoint3(50, 0, 0), out _, out _, out _));
        }

        [Fact]
        public void SmallAndThinTriangles_CollideExactlyAsTheirDistanceSays()
        {
            var random = new Random(41);
            double[] heights = { 1E-9, 1E-5, 9E-4, 0.002 };
            double[] sizes = { 0.001, 0.003, 0.005 };

            for (int i = 0; i < 600; i++)
            {
                var origin = new GeoPoint3(random.NextDouble() * 100, random.NextDouble() * 100, random.NextDouble() * 100);
                GeoTriangle3 triangle = i % 2 == 0
                    ? new GeoTriangle3(origin, origin.Add(new GeoVector3(20, 0, 0)), origin.Add(new GeoVector3(20 * random.NextDouble(), heights[random.Next(4)], 0)))
                    : new GeoTriangle3(origin, origin.Add(new GeoVector3(sizes[random.Next(3)], 0, 0)), origin.Add(new GeoVector3(0, sizes[random.Next(3)], 0)));

                GeoPoint3 aim = triangle.GetPointAtBarycentric(0.3, 0.3, 0.4).Add(new GeoVector3(0, (random.NextDouble() * 2 - 1) * 0.0015, 0));
                var direction = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, 1);
                var segment = new GeoLine3(aim.Add(direction.Multiply(-3)), aim.Add(direction.Multiply(3)));

                double gap = triangle.DistanceTo(segment, Default);
                bool collides = triangle.CollidesWith(segment, Default);

                Assert.Equal(gap <= Default.EqualPoint, collides);
                Assert.Equal(collides, segment.CollidesWith(triangle, Default));

                if (Intersection3.TryIntersectWith(segment, triangle, out GeoPoint3 crossing, Default))
                {
                    Assert.True(collides);
                    Assert.True(triangle.DistanceTo(crossing) <= Default.EqualPoint);
                }
            }
        }
    }
}
