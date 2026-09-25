using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The same holding-to-account in space as in the plane: each operation offered twice, and each of
    /// those with and without a tolerance, all called and compared. A segment and a ray answer most of
    /// the same questions, so both are put through them.
    /// </summary>
    public class SolidMirrorTests
    {
        private static readonly Tolerance Global = Tolerance.Global;

        private static GeoLine3 Line() => new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

        private static GeoLine3 Crossing() => new GeoLine3(new GeoPoint3(50, -50, 0), new GeoPoint3(50, 50, 0));

        private static GeoLine3 Skew() => new GeoLine3(new GeoPoint3(50, -50, 30), new GeoPoint3(50, 50, 30));

        private static GeoLine3 Alongside() => new GeoLine3(new GeoPoint3(0, 40, 0), new GeoPoint3(100, 40, 0));

        private static GeoRay3 Ray() => new GeoRay3(new GeoPoint3(50, -80, 0), GeoVector3.YAxis);

        private static GeoPlane3 Plane() => new GeoPlane3(new GeoPoint3(70, 0, 0), GeoVector3.XAxis);

        private static GeoTriangle3 Triangle() => new GeoTriangle3(
            new GeoPoint3(60, -20, -20), new GeoPoint3(60, 40, -20), new GeoPoint3(60, 0, 40));

        [Fact]
        public void ASegmentInSpaceMeasuresTheSameWhicheverWayItIsAsked()
        {
            GeoLine3 line = Line();

            Assert.Equal(Distance3.DistanceTo(Ray(), line), line.DistanceTo(Ray()), 12);
            Assert.Equal(Distance3.DistanceTo(Plane(), line), line.DistanceTo(Plane()), 12);
            Assert.Equal(line.DistanceTo(Skew()), Distance3.DistanceTo(line, Skew()), 12);

            // Two segments crossing at a point are nought apart; the skew one stands off by its height.
            Assert.Equal(0.0, line.DistanceTo(Crossing()), 9);
            Assert.Equal(30.0, line.DistanceTo(Skew()), 9);
            Assert.Equal(40.0, line.DistanceTo(Alongside()), 9);

            // The plane cuts it, so there is no distance to it.
            Assert.Equal(0.0, line.DistanceTo(Plane()), 9);

            Assert.True(line.GetShortestLineTo(Skew(), Global).Length >= 0.0);
            Assert.True(line.GetShortestLineTo(Skew(), LineExtension.Both, Global).Length >= 0.0);
        }

        [Fact]
        public void ASegmentInSpaceAnswersItsQuestionsTheSameWhicheverWayItIsAsked()
        {
            GeoLine3 line = Line();
            var on = new GeoPoint3(50, 0, 0);

            Assert.Equal(line.IsPointOn(on), line.IsPointOn(on, Global));
            Assert.True(line.IsPointOn(on));
            Assert.False(line.IsPointOn(new GeoPoint3(50, 1, 0)));

            Assert.Equal(line.IsParallelTo(Alongside()), line.IsParallelTo(Alongside(), Global));
            Assert.True(line.IsParallelTo(Alongside()));
            Assert.False(line.IsParallelTo(Crossing()));

            Assert.Equal(line.IsPerpendicularTo(Crossing()), line.IsPerpendicularTo(Crossing(), Global));
            Assert.True(line.IsPerpendicularTo(Crossing()));

            // A plane whose normal runs along the segment is anything but parallel to it.
            Assert.Equal(line.IsParallelTo(Plane()), line.IsParallelTo(Plane(), Global));
            Assert.False(line.IsParallelTo(Plane()));

            // Two segments in the same plane are coplanar; one lifted out of it is not.
            Assert.Equal(line.IsCoplanarWith(Crossing()), line.IsCoplanarWith(Crossing(), Global));
            Assert.True(line.IsCoplanarWith(Crossing()));
            Assert.False(line.IsCoplanarWith(Skew()));
        }

        [Fact]
        public void ASegmentInSpaceMeetsThingsTheSameWhicheverWayItIsAsked()
        {
            GeoLine3 line = Line();

            Assert.Equal(
                line.TryIntersectWith(Crossing(), out GeoPoint3 first),
                line.TryIntersectWith(Crossing(), out _, Global));
            Assert.True(first.IsEqualTo(new GeoPoint3(50, 0, 0)));

            Assert.Equal(
                line.TryIntersectWith(Crossing(), LineExtension.Both, out _),
                line.TryIntersectWith(Crossing(), LineExtension.Both, out _, Global));

            Assert.Equal(
                line.TryIntersectWith(Plane(), out GeoPoint3 onPlane),
                line.TryIntersectWith(Plane(), out _, Global));
            Assert.True(onPlane.IsEqualTo(new GeoPoint3(70, 0, 0)));

            Assert.Equal(
                line.TryIntersectWith(Triangle(), out GeoPoint3 onTriangle),
                line.TryIntersectWith(Triangle(), out _, Global));
            Assert.True(onTriangle.IsEqualTo(new GeoPoint3(60, 0, 0)));

            // A segment that stops short of the plane does not reach it. There is no form that runs it on
            // to the plane, because a plane is endless and the answer would always be yes.
            var stops = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0));
            Assert.False(stops.TryIntersectWith(Plane(), out _));
            Assert.False(stops.TryIntersectWith(Plane(), out _, Global));
            Assert.Equal(40.0, stops.DistanceTo(Plane()), 9);
        }

        [Fact]
        public void ARayAnswersTheSameQuestionsAsASegment()
        {
            GeoRay3 ray = Ray();

            Assert.Equal(Distance3.DistanceTo(ray, Line()), ray.DistanceTo(Line()), 12);
            Assert.Equal(0.0, ray.DistanceTo(Line()), 9);

            var on = new GeoPoint3(50, 0, 0);
            Assert.True(ray.GetClosestPointOnBoundary(on).IsEqualTo(on));
            Assert.Equal(ray.IsPointOn(on), ray.IsPointOn(on, Global));
            Assert.True(ray.IsPointOn(on));

            // Behind where it starts is not on it, however well it lines up.
            Assert.False(ray.IsPointOn(new GeoPoint3(50, -100, 0)));

            Assert.Equal(ray.IsParallelTo(Crossing()), ray.IsParallelTo(Crossing(), Global));
            Assert.True(ray.IsParallelTo(Crossing()));
            Assert.Equal(ray.IsParallelTo(Plane()), ray.IsParallelTo(Plane(), Global));
            Assert.True(ray.IsParallelTo(Plane()));

            Assert.Equal(
                ray.TryIntersectWith(Plane(), out _),
                ray.TryIntersectWith(Plane(), out _, Global));

            Assert.Equal(
                ray.TryIntersectWith(Triangle(), out _),
                ray.TryIntersectWith(Triangle(), out _, Global));

            // A ray running up the Y axis at x = 50 never reaches the triangle standing at x = 60.
            Assert.False(ray.TryIntersectWith(Triangle(), out _));

            var toward = new GeoRay3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis);
            Assert.True(toward.TryIntersectWith(Triangle(), out GeoPoint3 hit));
            Assert.True(hit.IsEqualTo(new GeoPoint3(60, 0, 0)));
        }

        [Fact]
        public void ARayComparesAndReadsBackLikeEveryOtherShape()
        {
            GeoRay3 ray = Ray();
            GeoRay3 same = Ray();

            Assert.True(ray == same);
            Assert.False(ray != same);
            Assert.True(ray.IsEqualTo(same));
            Assert.True(ray.IsEqualTo(same, Global));
            Assert.False(ray.IsEqualTo(new GeoRay3(ray.Origin, GeoVector3.XAxis)));
            Assert.Contains("Ray", ray.ToString());
        }
    }
}
