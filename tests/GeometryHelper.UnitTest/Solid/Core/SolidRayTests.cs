using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A body asked about a ray. A ray runs on for ever in one direction, so it cannot be cut into a
    /// segment and measured that way without first choosing how far to cut; every answer here is held
    /// against a long segment along the same line, which is the reading that choice would have given.
    /// </summary>
    public class SolidRayTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A box a hundred on each side with its near-lower-left corner at the origin.
        /// </summary>
        private static GeoSolid3 Box() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100))
            .ToObb()
            .ToSolid();

        private static bool OnSurface(GeoSolid3 solid, GeoPoint3 point)
            => solid.GetClosestPointOnBoundary(point).DistanceTo(point) < 1E-7;

        [Fact]
        public void ARayRunningAtTheBodyGoesInOneSideAndOutTheOther()
        {
            GeoSolid3 box = Box();
            var incoming = new GeoRay3(new GeoPoint3(-200, 50, 50), new GeoVector3(1, 0, 0));

            Assert.True(box.CollidesWith(incoming));
            Assert.Equal(0.0, box.DistanceTo(incoming), 12);

            GeoPoint3[] crossings = box.GetIntersections(incoming);

            Assert.Equal(2, crossings.Length);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(0, 50, 50), Tight));
            Assert.True(crossings[1].IsEqualTo(new GeoPoint3(100, 50, 50), Tight));
        }

        [Fact]
        public void ARayPointedAwayNeverReachesTheBodyAtAll()
        {
            GeoSolid3 box = Box();

            // Starting two hundred out to the left and going further left.
            var leaving = new GeoRay3(new GeoPoint3(-200, 50, 50), new GeoVector3(-1, 0, 0));

            Assert.False(box.CollidesWith(leaving));
            Assert.Empty(box.GetIntersections(leaving));

            // Its nearest point is its own origin, so it stands two hundred off.
            Assert.Equal(200.0, box.DistanceTo(leaving), 9);

            GeoLine3 joining = box.GetShortestLineTo(leaving);

            Assert.Equal(200.0, joining.Length, 9);
            Assert.True(joining.StartPoint.IsEqualTo(new GeoPoint3(0, 50, 50), Tight));
            Assert.True(joining.EndPoint.IsEqualTo(leaving.Origin, Tight));
        }

        [Fact]
        public void ARayRunningPastTheBodyIsMeasuredWhereItRunsPast()
        {
            GeoSolid3 box = Box();

            // Parallel to x, level with the middle in z, but two hundred away in y.
            var passing = new GeoRay3(new GeoPoint3(-500, 300, 50), new GeoVector3(1, 0, 0));

            Assert.False(box.CollidesWith(passing));
            Assert.Equal(200.0, box.DistanceTo(passing), 9);

            GeoLine3 joining = box.GetShortestLineTo(passing);

            Assert.Equal(200.0, joining.Length, 9);
            Assert.True(OnSurface(box, joining.StartPoint));
            Assert.Equal(100.0, joining.StartPoint.Y, 9);
            Assert.Equal(300.0, joining.EndPoint.Y, 9);
        }

        [Fact]
        public void ARayStartingInsideIsAtNoDistanceAndComesOutOnce()
        {
            GeoSolid3 box = Box();
            var inside = new GeoRay3(new GeoPoint3(50, 50, 50), new GeoVector3(1, 0, 0));

            Assert.True(box.CollidesWith(inside));
            Assert.Equal(0.0, box.DistanceTo(inside), 12);
            Assert.Equal(0.0, box.GetShortestLineTo(inside).Length, 9);

            GeoPoint3[] crossings = box.GetIntersections(inside);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(100, 50, 50), Tight));
        }

        [Fact]
        public void ARayAgreesWithALongSegmentAlongTheSameLine()
        {
            var random = new Random(20260925);
            GeoSolid3 box = Box();

            for (int i = 0; i < 40; i++)
            {
                var origin = new GeoPoint3(
                    random.NextDouble() * 800 - 400,
                    random.NextDouble() * 800 - 400,
                    random.NextDouble() * 800 - 400);

                var direction = new GeoVector3(
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1);

                if (direction.Length < 1E-6)
                {
                    continue;
                }

                var ray = new GeoRay3(origin, direction);

                // Three thousand is far enough to leave the box behind whatever the direction.
                GeoLine3 asSegment = ray.ToLine(3000.0);

                Assert.Equal(box.DistanceTo(asSegment), box.DistanceTo(ray), 6);
                Assert.Equal(box.CollidesWith(asSegment), box.CollidesWith(ray));
                Assert.Equal(box.GetIntersections(asSegment).Length, box.GetIntersections(ray).Length);

                GeoLine3 joining = box.GetShortestLineTo(ray);

                Assert.Equal(box.DistanceTo(ray), joining.Length, 6);
                Assert.True(OnSurface(box, joining.StartPoint), "start off the surface, run " + i);

                // The far end lies on the ray, which means ahead of the origin and square to nothing else.
                Assert.True(ray.GetDistanceAtPoint(joining.EndPoint) >= -1E-7, "end behind the origin, run " + i);
                Assert.Equal(0.0, Distance3.DistanceTo(ray, joining.EndPoint), 6);
            }
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNothing()
        {
            GeoSolid3 box = Box();
            var ray = new GeoRay3(new GeoPoint3(-200, 50, 50), new GeoVector3(1, 0, 0));
            var triangle = new GeoTriangle3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(300, 100, 0));

            Assert.Equal(Distance3.DistanceTo(ray, box), box.DistanceTo(ray), 9);
            Assert.Equal(Collision3.CollidesWith(ray, box), box.CollidesWith(ray));
            Assert.Equal(Intersection3.GetIntersections(ray, box).Length, box.GetIntersections(ray).Length);
            Assert.True(Projection3.GetShortestLineTo(box, ray).IsEqualTo(box.GetShortestLineTo(ray), Tight));
            Assert.Equal(box.DistanceTo(ray), box.DistanceTo(ray, Tolerance.Global), 9);

            // A ray against a single face, and the segment joining them, agree with each other too.
            Assert.Equal(
                Distance3.DistanceTo(ray, triangle),
                Projection3.GetShortestLineTo(ray, triangle).Length,
                9);

            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo(ray, (GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => Collision3.CollidesWith(ray, (GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => Intersection3.GetIntersections(ray, (GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => Projection3.GetShortestLineTo((GeoSolid3)null, ray));
        }
    }
}
