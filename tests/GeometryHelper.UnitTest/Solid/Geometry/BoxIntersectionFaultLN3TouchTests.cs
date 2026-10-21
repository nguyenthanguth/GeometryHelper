using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment, a ray, a line without ends or a chain that touches a box within the point tolerance without crossing a
    /// face is given the point of it nearest the box.
    /// </summary>
    /// <remarks>
    /// The slabs of a box are read exactly on the axes a segment moves along, so one passing within the tolerance of an
    /// edge, or stopping within it short of a face, collided with the box and crossed nothing: 19 256 of 200 000 segments
    /// near an edge, and the rays, lines and chains through them alike. They are given the point of them nearest the box
    /// now, as the other shapes give a touch, and cutting a chain by a box still cuts only where it crosses a face.
    /// </remarks>
    public class BoxIntersectionFaultLN3TouchTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoAabb3 Box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10));

        // Across the edge where the faces y = 10 and z = 10 meet, 0.0005 off it at its nearest.
        private static readonly GeoLine3 PastTheEdge = new GeoLine3(new GeoPoint3(5, 10.1, 9.9007), new GeoPoint3(5, 9.9007, 10.1));

        [Fact]
        public void ASegmentPassingWithinTheToleranceOfAnEdge_TouchesItWhereItIsNearest()
        {
            Assert.True(Box.CollidesWith(PastTheEdge, Default));

            GeoPoint3 touch = Assert.Single(Box.GetIntersections(PastTheEdge, Default));

            Assert.Equal(0.0, touch.DistanceTo(new GeoPoint3(5, 10.00035, 10.00035)), 12);
            Assert.Single(Box.ToObb().GetIntersections(PastTheEdge, Default));
            Assert.Single(Box.GetIntersections(new GeoLine3(PastTheEdge.EndPoint, PastTheEdge.StartPoint), Default));
            Assert.Single(Box.GetIntersections(new GeoRay3(PastTheEdge.StartPoint, PastTheEdge.Direction), Default));
            Assert.Single(new GeoXLine3(PastTheEdge.StartPoint, PastTheEdge.Direction).GetIntersections(Box, Default));
            Assert.Single(Box.GetIntersections(new GeoPolyline3(new[] { PastTheEdge.StartPoint, PastTheEdge.EndPoint }), Default));
        }

        [Fact]
        public void ASegmentStoppingWithinTheToleranceOfAFace_TouchesItAtItsEnd()
        {
            var segment = new GeoLine3(new GeoPoint3(-5, 5, 5), new GeoPoint3(-0.0005, 5, 5));

            Assert.True(Box.CollidesWith(segment, Default));
            Assert.Equal(segment.EndPoint, Assert.Single(Box.GetIntersections(segment, Default)));
        }

        [Fact]
        public void ASegmentPassingFurtherThanTheToleranceOffAnEdge_StillMissesIt()
        {
            // 0.00106 off the edge.
            var segment = new GeoLine3(new GeoPoint3(5, 10.1, 9.9015), new GeoPoint3(5, 9.9015, 10.1));

            Assert.False(Box.CollidesWith(segment, Default));
            Assert.Empty(Box.GetIntersections(segment, Default));
        }

        [Fact]
        public void ATouch_IsTheSamePointEitherWayRound()
        {
            var back = new GeoLine3(PastTheEdge.EndPoint, PastTheEdge.StartPoint);

            Assert.Equal(Assert.Single(Box.GetIntersections(PastTheEdge, Default)), Assert.Single(Box.GetIntersections(back, Default)));
        }

        [Fact]
        public void AChainLevelOverAFace_LiesAlongItAndCrossesNothing()
        {
            // 0.0005 over the top face, as the body of the box and an arc in the same place read it.
            var chain = new GeoPolyline3(new[] { new GeoPoint3(2, 2, 10.0005), new GeoPoint3(5, 3, 10.0005), new GeoPoint3(8, 2, 10.0005), new GeoPoint3(8, 8, 10.0005) });
            var segment = new GeoLine3(new GeoPoint3(2, 2, 10.0005), new GeoPoint3(5, 3, 10.0005));

            Assert.True(Box.CollidesWith(chain, Default));
            Assert.Empty(Box.GetIntersections(chain, Default));
            Assert.Empty(Box.GetIntersections(segment, Default));
            Assert.Empty(Box.GetIntersections(new GeoLine3(segment.EndPoint, segment.StartPoint), Default));
            Assert.Empty(Box.ToObb().ToSolid().GetIntersections(segment, Default));
        }

        [Fact]
        public void ARayStartingJustOffAFaceAndRunningAway_TouchesItAtItsOrigin()
        {
            var ray = new GeoRay3(new GeoPoint3(5, 5, 10.0005), GeoVector3.ZAxis);

            Assert.True(Box.ToObb().CollidesWith(ray, Default));
            Assert.Equal(ray.Origin, Assert.Single(Box.ToObb().GetIntersections(ray, Default)));
        }

        [Fact]
        public void RaysNearTheBoxInEveryDirection_CrossWhereAndOnlyWhereTheyCollide()
        {
            var random = new Random(43);
            GeoObb3 box = Box.ToObb();

            for (int i = 0; i < 5000; i++)
            {
                double[] at = { random.NextDouble() * 10, random.NextDouble() * 10, random.NextDouble() * 10 };
                int axis = random.Next(3);
                at[axis] = (random.Next(2) == 0 ? 0 : 10) + (random.NextDouble() * 2 - 1) * 0.003;
                var direction = new GeoVector3(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1);

                if (direction.Length < 0.1)
                {
                    continue;
                }

                var ray = new GeoRay3(new GeoPoint3(at[0], at[1], at[2]), direction);

                Assert.Equal(box.CollidesWith(ray, Default), box.GetIntersections(ray, Default).Length > 0);
            }
        }

        [Fact]
        public void ASegmentWhollyInsideTheBox_StillCrossesNothing()
        {
            var segment = new GeoLine3(new GeoPoint3(2, 3, 4), new GeoPoint3(7, 6, 5));

            Assert.True(Box.CollidesWith(segment, Default));
            Assert.Empty(Box.GetIntersections(segment, Default));
        }

        [Fact]
        public void AChainTouchingTheBox_IsNotCutWhereItTouches()
        {
            var chain = new GeoPolyline3(new[] { new GeoPoint3(5, 10.1, 9.9007), new GeoPoint3(5, 9.9007, 10.1), new GeoPoint3(5, 9.5, 12) });

            Assert.False(chain.TrySplitBy(Box, out GeoPolyline3[] inside, out GeoPolyline3[] outside, Default));
            Assert.Empty(inside);
            Assert.Equal(3, Assert.Single(outside).VertexCount);
        }

        [Fact]
        public void SegmentsNearAnEdge_CrossWhereAndOnlyWhereTheyCollide()
        {
            var random = new Random(7);
            GeoObb3 box = Box.ToObb();

            for (int i = 0; i < 5000; i++)
            {
                double y0 = 10 + (random.NextDouble() * 2 - 1) * 0.0015, z0 = 10 + (random.NextDouble() * 2 - 1) * 0.0015;
                double y1 = y0 + (random.NextDouble() * 2 - 1) * 0.0015, z1 = z0 + (random.NextDouble() * 2 - 1) * 0.0015;
                double x0 = -20 + random.NextDouble() * 15, x1 = x0 + random.NextDouble() * 30;
                var segment = new GeoLine3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1));
                bool collides = box.CollidesWith(segment, Default);

                // One lying along the box, level with the top or the side to the vector tolerance, collides and crosses
                // nothing; any other crosses exactly where it collides.
                double length = segment.Length;
                bool along = Math.Min(Math.Abs(y1 - y0), Math.Abs(z1 - z0)) <= 2E-5 * length;
                bool expected = collides && !along;
                int forward = box.GetIntersections(segment, Default).Length;

                Assert.True(forward == 0 || collides);
                Assert.True(!expected || forward > 0);
                Assert.Equal(forward > 0, Box.GetIntersections(segment, Default).Length > 0);
                Assert.Equal(forward > 0, box.GetIntersections(new GeoLine3(segment.EndPoint, segment.StartPoint), Default).Length > 0);
                Assert.Equal(forward > 0, Box.GetIntersections(new GeoPolyline3(new[] { segment.StartPoint, segment.EndPoint }), Default).Length > 0);

                var ray = new GeoRay3(segment.StartPoint, segment.Direction);
                var line = new GeoXLine3(segment.StartPoint, segment.Direction);

                Assert.Equal(box.CollidesWith(ray, Default), box.GetIntersections(ray, Default).Length > 0);
                Assert.Equal(line.CollidesWith(Box, Default), line.GetIntersections(Box, Default).Length > 0);
            }
        }
    }
}
