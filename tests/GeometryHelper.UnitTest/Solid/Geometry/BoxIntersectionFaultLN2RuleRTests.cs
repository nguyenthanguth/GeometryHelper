using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A box gives crossings only where the shape comes within the point tolerance of it, where it collides.
    /// </summary>
    /// <remarks>
    /// A segment that barely moves across a pair of faces was held within them when it ran no further outside than the
    /// tolerance, on each pair on its own, so that one running past an edge 0.000714 off both faces, 0.00101 from the box,
    /// crossed it at two points and did not collide with it; a ray, a line without ends and a chain of segments went the
    /// same way. An arc ending within the tolerance of each face's plane and of its outline, but further from the corner
    /// they share, was given a point too. Crossings are kept now only where the shape touches the box.
    /// </remarks>
    public class BoxIntersectionFaultLN2RuleRTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoAabb3 Box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10));

        private static GeoLine3 PastTheEdge(double gap)
        {
            double off = gap / Math.Sqrt(2.0);

            return new GeoLine3(new GeoPoint3(-5, 10 + off, 10 + off), new GeoPoint3(15, 10 + off, 10 + off));
        }

        [Fact]
        public void ASegmentPastAnEdgeFurtherThanTheTolerance_CrossesNothing()
        {
            GeoLine3 segment = PastTheEdge(0.00101);

            Assert.False(Box.CollidesWith(segment, Default));
            Assert.Empty(Box.GetIntersections(segment, Default));
            Assert.Empty(Box.ToObb().GetIntersections(segment, Default));
            Assert.Empty(Box.GetIntersections(new GeoRay3(segment.StartPoint, segment.Direction), Default));
            Assert.Empty(new GeoXLine3(segment.StartPoint, segment.Direction).GetIntersections(Box, Default));
            Assert.Empty(Box.GetIntersections(new GeoPolyline3(new[] { segment.StartPoint, segment.EndPoint }), Default));
        }

        [Fact]
        public void ASegmentPastAnEdgeWithinTheTolerance_StillCrossesItTwice()
        {
            GeoLine3 segment = PastTheEdge(0.0009);

            Assert.True(Box.CollidesWith(segment, Default));
            Assert.Equal(2, Box.GetIntersections(segment, Default).Length);
        }

        [Fact]
        public void ASegmentLeavingAHeldSlab_CrossesNothingFromEitherEnd()
        {
            // It barely moves along Y and Z, starting inside both slabs and ending 0.0009 outside them: 0.00119 from the edge
            // where it is within the slab of X.
            var segment = new GeoLine3(new GeoPoint3(-15, 9.99995, 9.99995), new GeoPoint3(1, 10.0009, 10.0009));
            var back = new GeoLine3(segment.EndPoint, segment.StartPoint);

            Assert.False(Box.CollidesWith(segment, Default));
            Assert.Empty(Box.GetIntersections(segment, Default));
            Assert.Empty(Box.GetIntersections(back, Default));
            Assert.Empty(Box.ToObb().GetIntersections(segment, Default));
            Assert.Empty(Box.GetIntersections(new GeoRay3(segment.StartPoint, segment.Direction), Default));
            Assert.Empty(Box.GetIntersections(new GeoPolyline3(new[] { segment.StartPoint, segment.EndPoint }), Default));
            Assert.Empty(Box.GetIntersections(new GeoPolylineArc3(new[] { new GeoEdge3(segment.StartPoint, segment.EndPoint) }), Default));
        }

        [Fact]
        public void SegmentsNearAnEdge_CrossOnlyWhereTheyCollide()
        {
            var random = new Random(7);
            GeoObb3 box = Box.ToObb();

            for (int i = 0; i < 5000; i++)
            {
                double y0 = 10 + (random.NextDouble() * 2 - 1) * 0.0015, z0 = 10 + (random.NextDouble() * 2 - 1) * 0.0015;
                double y1 = y0 + (random.NextDouble() * 2 - 1) * 0.0015, z1 = z0 + (random.NextDouble() * 2 - 1) * 0.0015;
                double x0 = -20 + random.NextDouble() * 15, x1 = x0 + random.NextDouble() * 30;
                var segment = new GeoLine3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1));

                Assert.True(box.GetIntersections(segment, Default).Length == 0 || box.CollidesWith(segment, Default));
                Assert.True(box.GetIntersections(new GeoLine3(segment.EndPoint, segment.StartPoint), Default).Length == 0 || box.CollidesWith(segment, Default));
            }
        }

        private static GeoArc3 FromTheCorner(double off)
        {
            var box = new GeoPoint3(30 + off, 20 + off, 10 + off);

            return GeoArc3.FromThreePoints(box, new GeoPoint3(33, 23, 12), new GeoPoint3(35, 27, 15), Default);
        }

        private static readonly GeoAabb3 Slab = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 20, 10));

        [Fact]
        public void AnArcEndingFurtherThanTheToleranceOffACorner_CrossesNothing()
        {
            // 0.00064 off in each of the three directions, 0.0011 from the corner.
            GeoArc3 arc = FromTheCorner(0.00064);

            Assert.False(Slab.CollidesWith(arc, Default));
            Assert.Empty(Slab.GetIntersections(arc, Default));
            Assert.Empty(Slab.ToObb().GetIntersections(arc, Default));
            Assert.Empty(Slab.GetIntersections(new GeoPolylineArc3(new[] { new GeoEdge3(arc) }), Default));
        }

        [Fact]
        public void AnArcEndingWithinTheToleranceOfACorner_StillMeetsIt()
        {
            // 0.0005 off in each direction, 0.00087 from the corner.
            GeoArc3 arc = FromTheCorner(0.0005);

            Assert.True(Slab.CollidesWith(arc, Default));
            Assert.Single(Slab.GetIntersections(arc, Default));
        }
    }
}
