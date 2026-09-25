using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The segment joining a solid to something else. What binds it to the measurements already in the
    /// library is that it is exactly as long as the distance they report, and that each of its ends lies
    /// on the surface it came from.
    /// </summary>
    public class SolidShortestLineTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoSolid3 BoxAt(double x) => new GeoAabb3(new GeoPoint3(x, 0, 0), new GeoPoint3(x + 100, 100, 100))
            .ToObb()
            .ToSolid();

        private static GeoSolid3 Box() => BoxAt(0);

        private static bool OnSurface(GeoSolid3 solid, GeoPoint3 point)
            => solid.GetClosestPointOnBoundary(point).DistanceTo(point) < 1E-7;

        [Fact]
        public void TheSegmentRunsFromFaceToFaceAndIsAsLongAsTheGap()
        {
            GeoSolid3 box = Box();
            GeoSolid3 away = BoxAt(300);

            GeoLine3 joining = box.GetShortestLineTo(away);

            Assert.Equal(200.0, joining.Length, 9);
            Assert.Equal(box.DistanceTo(away), joining.Length, 9);
            Assert.Equal(100.0, joining.StartPoint.X, 9);
            Assert.Equal(300.0, joining.EndPoint.X, 9);
            Assert.True(OnSurface(box, joining.StartPoint));
            Assert.True(OnSurface(away, joining.EndPoint));
        }

        [Fact]
        public void EverySortOfThingIsJoinedAndTheLengthIsTheDistanceAlreadyMeasured()
        {
            GeoSolid3 box = Box();
            GeoSolid3 away = BoxAt(300);

            var point = new GeoPoint3(300, 50, 50);
            var line = new GeoLine3(new GeoPoint3(300, 50, 0), new GeoPoint3(300, 50, 100));
            var triangle = new GeoTriangle3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(300, 100, 0));

            Assert.Equal(box.DistanceTo(point), box.GetShortestLineTo(point).Length, 9);
            Assert.Equal(box.DistanceTo(line), box.GetShortestLineTo(line).Length, 9);
            Assert.Equal(box.DistanceTo(triangle), box.GetShortestLineTo(triangle).Length, 9);
            Assert.Equal(box.DistanceTo(away), box.GetShortestLineTo(away).Length, 9);

            foreach (GeoLine3 joining in new[]
            {
                box.GetShortestLineTo(point), box.GetShortestLineTo(line),
                box.GetShortestLineTo(triangle), box.GetShortestLineTo(away)
            })
            {
                Assert.Equal(200.0, joining.Length, 9);
                Assert.True(OnSurface(box, joining.StartPoint), "start off the surface");
            }

            // The end really lands on what was asked about.
            Assert.True(box.GetShortestLineTo(point).EndPoint.IsEqualTo(point, Tight));
            Assert.Equal(0.0, Distance3.DistanceTo(line, box.GetShortestLineTo(line).EndPoint), 7);
            Assert.Equal(0.0, Distance3.DistanceTo(triangle, box.GetShortestLineTo(triangle).EndPoint), 7);
        }

        [Fact]
        public void ThingsThatMeetAreJoinedByNothingAtAll()
        {
            GeoSolid3 box = Box();
            GeoSolid3 overlapping = BoxAt(50);

            var through = new GeoLine3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50));
            var cutting = new GeoTriangle3(
                new GeoPoint3(50, -50, 50), new GeoPoint3(50, 150, 50), new GeoPoint3(50, 50, 200));

            Assert.Equal(0.0, box.GetShortestLineTo(through).Length, 9);
            Assert.Equal(0.0, box.GetShortestLineTo(cutting).Length, 9);
            Assert.Equal(0.0, box.GetShortestLineTo(overlapping).Length, 9);

            // And the point they share really is on both.
            GeoLine3 joining = box.GetShortestLineTo(through);

            Assert.True(joining.StartPoint.IsEqualTo(joining.EndPoint, Tight));
            Assert.True(OnSurface(box, joining.StartPoint));
        }

        [Fact]
        public void APointInsideTheBodyStillGetsASegmentOutToTheSkin()
        {
            // This is where the segment and the distance part company on purpose.
            GeoSolid3 box = Box();
            var inside = new GeoPoint3(50, 50, 10);

            Assert.Equal(0.0, box.DistanceTo(inside), 12);

            GeoLine3 joining = box.GetShortestLineTo(inside);

            Assert.Equal(10.0, joining.Length, 9);
            Assert.True(joining.StartPoint.IsEqualTo(new GeoPoint3(50, 50, 0), Tight));
            Assert.True(joining.EndPoint.IsEqualTo(inside, Tight));
        }

        [Fact]
        public void TwoTrianglesAreJoinedWhereTheyAreNearestAndNowhereElse()
        {
            var flat = new GeoTriangle3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
            var above = new GeoTriangle3(
                new GeoPoint3(0, 0, 40), new GeoPoint3(100, 0, 40), new GeoPoint3(0, 100, 40));

            GeoLine3 joining = Projection3.GetShortestLineTo(flat, above);

            Assert.Equal(40.0, joining.Length, 9);
            Assert.Equal(Distance3.DistanceTo(flat, above), joining.Length, 9);
            Assert.Equal(0.0, Distance3.DistanceTo(flat, joining.StartPoint), 7);
            Assert.Equal(0.0, Distance3.DistanceTo(above, joining.EndPoint), 7);

            // Two that cross share a point, and that is all the segment is.
            var crossing = new GeoTriangle3(
                new GeoPoint3(20, 20, -50), new GeoPoint3(30, 20, 50), new GeoPoint3(20, 30, 50));

            Assert.Equal(0.0, Projection3.GetShortestLineTo(flat, crossing).Length, 9);
            Assert.Equal(0.0, Distance3.DistanceTo(flat, crossing), 9);
        }

        [Fact]
        public void OverManyPositionsTheSegmentIsAsLongAsTheDistance()
        {
            var random = new Random(20260925);
            GeoSolid3 box = Box();

            for (int i = 0; i < 60; i++)
            {
                double x = random.NextDouble() * 600 - 250;
                double y = random.NextDouble() * 600 - 250;
                double z = random.NextDouble() * 600 - 250;

                var point = new GeoPoint3(x, y, z);
                var line = new GeoLine3(point, new GeoPoint3(x + 60, y + 20, z - 30));
                GeoSolid3 other = new GeoAabb3(point, new GeoPoint3(x + 80, y + 80, z + 80)).ToObb().ToSolid();

                GeoLine3 toLine = box.GetShortestLineTo(line);
                GeoLine3 toSolid = box.GetShortestLineTo(other);

                Assert.Equal(box.DistanceTo(line), toLine.Length, 6);
                Assert.True(OnSurface(box, toLine.StartPoint), "start off the surface, run " + i);

                // Two bodies that only touch or overlap are nought apart either way; where they are clear
                // of each other the two readings are the same number.
                if (!box.CollidesWith(other))
                {
                    Assert.Equal(box.DistanceTo(other), toSolid.Length, 6);
                    Assert.True(OnSurface(box, toSolid.StartPoint), "start off the surface, run " + i);
                    Assert.True(OnSurface(other, toSolid.EndPoint), "end off the other surface, run " + i);
                }
            }
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNothing()
        {
            GeoSolid3 box = Box();
            GeoSolid3 away = BoxAt(300);
            var point = new GeoPoint3(300, 50, 50);
            var line = new GeoLine3(new GeoPoint3(300, 50, 0), new GeoPoint3(300, 50, 100));

            Assert.True(Projection3.GetShortestLineTo(box, point).IsEqualTo(box.GetShortestLineTo(point), Tight));
            Assert.True(Projection3.GetShortestLineTo(box, line, Tolerance.Global).IsEqualTo(box.GetShortestLineTo(line, Tolerance.Global), Tight));
            Assert.True(Projection3.GetShortestLineTo(box, away).IsEqualTo(box.GetShortestLineTo(away), Tight));

            Assert.Throws<ArgumentNullException>(() => Projection3.GetShortestLineTo((GeoSolid3)null, point));
            Assert.Throws<ArgumentNullException>(() => Projection3.GetShortestLineTo(box, (GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => box.GetShortestLineTo((GeoSolid3)null));
        }
    }
}
