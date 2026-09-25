using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// What a solid can be asked about the things around it. The measurements were all in
    /// <see cref="Distance3"/> and <see cref="Collision3"/> already, but the solid itself could only be
    /// asked about a point, so everything else had to be written the long way round.
    /// </summary>
    public class SolidReachTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A box a hundred on each side, with its near-lower-left corner at the origin.
        /// </summary>
        private static GeoSolid3 Box() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100))
            .ToObb()
            .ToSolid();

        private static GeoSolid3 BoxAt(double x) => new GeoAabb3(new GeoPoint3(x, 0, 0), new GeoPoint3(x + 100, 100, 100))
            .ToObb()
            .ToSolid();

        [Fact]
        public void ASolidMeasuresTheSameWhicheverWayItIsAsked()
        {
            GeoSolid3 box = Box();
            GeoSolid3 away = BoxAt(300);

            var point = new GeoPoint3(300, 50, 50);
            var line = new GeoLine3(new GeoPoint3(300, 50, 0), new GeoPoint3(300, 50, 100));
            var triangle = new GeoTriangle3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(300, 100, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 100, 0), new GeoPoint3(300, 100, 0));
            var polyline = new GeoPolyline3(new GeoPoint3(300, 0, 0), new GeoPoint3(300, 100, 0));
            var plane = new GeoPlane3(new GeoPoint3(300, 0, 0), new GeoVector3(1, 0, 0));
            var obb = new GeoAabb3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 100, 100)).ToObb();
            var aabb = new GeoAabb3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 100, 100));

            Assert.Equal(Distance3.DistanceTo(box, point), box.DistanceTo(point), 9);
            Assert.Equal(box.DistanceTo(point), box.DistanceTo(point, Tolerance.Global), 9);
            Assert.Equal(Distance3.DistanceTo(line, box), box.DistanceTo(line), 9);
            Assert.Equal(Distance3.DistanceTo(box, away), box.DistanceTo(away), 9);
            Assert.Equal(Distance3.DistanceTo(box, triangle), box.DistanceTo(triangle), 9);
            Assert.Equal(Distance3.DistanceTo(box, polygon), box.DistanceTo(polygon), 9);
            Assert.Equal(Distance3.DistanceTo(box, polyline), box.DistanceTo(polyline), 9);
            Assert.Equal(Distance3.DistanceTo(box, plane), box.DistanceTo(plane), 9);
            Assert.Equal(Distance3.DistanceTo(box, obb), box.DistanceTo(obb), 9);
            Assert.Equal(Distance3.DistanceTo(box, aabb), box.DistanceTo(aabb), 9);

            // Two hundred from the far face of the box at x = 100 to everything standing at x = 300.
            Assert.Equal(200.0, box.DistanceTo(point), 9);
            Assert.Equal(200.0, box.DistanceTo(line), 9);
            Assert.Equal(200.0, box.DistanceTo(away), 9);
            Assert.Equal(200.0, box.DistanceTo(triangle), 9);
            Assert.Equal(200.0, box.DistanceTo(polygon), 9);
            Assert.Equal(200.0, box.DistanceTo(polyline), 9);
            Assert.Equal(200.0, box.DistanceTo(plane), 9);
            Assert.Equal(200.0, box.DistanceTo(obb), 9);
            Assert.Equal(200.0, box.DistanceTo(aabb), 9);
        }

        [Fact]
        public void WhatReachesIntoTheBodyIsAtNoDistanceAtAll()
        {
            GeoSolid3 box = Box();

            var inside = new GeoPoint3(50, 50, 50);
            var through = new GeoLine3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50));
            var cuttingPlane = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));
            var buried = new GeoTriangle3(
                new GeoPoint3(20, 20, 50), new GeoPoint3(80, 20, 50), new GeoPoint3(20, 80, 50));

            Assert.Equal(0.0, box.DistanceTo(inside), 12);
            Assert.Equal(0.0, box.DistanceTo(through), 12);
            Assert.Equal(0.0, box.DistanceTo(cuttingPlane), 12);
            Assert.Equal(0.0, box.DistanceTo(buried), 12);

            // A triangle wholly inside touches no face of the body, so it is the corners that give it away.
            Assert.True(box.Contains(buried.A));
        }

        [Fact]
        public void TheSurfaceCanBeAskedForItsNearestPointEvenFromInside()
        {
            GeoSolid3 box = Box();

            // From outside: straight in through the face at x = 100.
            GeoPoint3 fromOutside = box.GetClosestPointOnBoundary(new GeoPoint3(300, 50, 50));

            Assert.True(fromOutside.IsEqualTo(new GeoPoint3(100, 50, 50), Tight));

            // From inside the answer is still on the surface, where the distance is nought.
            var inside = new GeoPoint3(50, 50, 10);
            GeoPoint3 fromInside = box.GetClosestPointOnBoundary(inside);

            Assert.Equal(0.0, box.DistanceTo(inside), 12);
            Assert.True(fromInside.IsEqualTo(new GeoPoint3(50, 50, 0), Tight));

            Assert.True(fromInside.IsEqualTo(box.GetClosestPointOnBoundary(inside, Tolerance.Global), Tight));
            Assert.True(fromOutside.IsEqualTo(Projection3.ProjectToSolid(box, new GeoPoint3(300, 50, 50)), Tight));
        }

        [Fact]
        public void ASolidMeetsThingsTheSameWhicheverWayItIsAsked()
        {
            GeoSolid3 box = Box();
            GeoSolid3 away = BoxAt(300);
            GeoSolid3 overlapping = BoxAt(50);

            var through = new GeoLine3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50));
            var clear = new GeoLine3(new GeoPoint3(300, 50, 0), new GeoPoint3(300, 50, 100));
            var polyline = new GeoPolyline3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50));
            var polygon = new GeoPolygon3(
                new GeoPoint3(50, -50, 50), new GeoPoint3(50, 150, 50), new GeoPoint3(50, 150, 60), new GeoPoint3(50, -50, 60));
            var face = new GeoFace3(polygon);
            var obb = new GeoAabb3(new GeoPoint3(50, 50, 50), new GeoPoint3(150, 150, 150)).ToObb();

            Assert.Equal(Collision3.CollidesWith(through, box), box.CollidesWith(through));
            Assert.Equal(Collision3.CollidesWith(polyline, box), box.CollidesWith(polyline));
            Assert.Equal(Collision3.CollidesWith(polygon, box), box.CollidesWith(polygon));
            Assert.Equal(Collision3.CollidesWith(face, box), box.CollidesWith(face));
            Assert.Equal(Collision3.CollidesWith(obb, box), box.CollidesWith(obb));
            Assert.Equal(Collision3.CollidesWith(box, overlapping), box.CollidesWith(overlapping));

            Assert.True(box.CollidesWith(through));
            Assert.True(box.CollidesWith(through, Tolerance.Global));
            Assert.False(box.CollidesWith(clear));
            Assert.True(box.CollidesWith(overlapping));
            Assert.False(box.CollidesWith(away));
        }

        [Fact]
        public void ASolidReportsWhereThingsPassThroughIt()
        {
            GeoSolid3 box = Box();

            var through = new GeoLine3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50));
            GeoPoint3[] crossings = box.GetIntersections(through);

            Assert.Equal(2, crossings.Length);
            Assert.Contains(crossings, p => p.IsEqualTo(new GeoPoint3(0, 50, 50), Tight));
            Assert.Contains(crossings, p => p.IsEqualTo(new GeoPoint3(100, 50, 50), Tight));
            Assert.Equal(Intersection3.GetIntersections(through, box).Length, crossings.Length);

            var cuttingPlane = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));
            GeoPoint3[] onThePlane = box.GetIntersections(cuttingPlane);

            Assert.NotEmpty(onThePlane);
            Assert.All(onThePlane, p => Assert.Equal(50.0, p.X, 9));
            Assert.Equal(Intersection3.GetIntersections(cuttingPlane, box).Length, onThePlane.Length);
            Assert.Equal(onThePlane.Length, box.GetIntersections(cuttingPlane, Tolerance.Global).Length);
        }

        [Fact]
        public void APlaneIsMeasuredFromWhicheverSideTheBodyIsOn()
        {
            GeoSolid3 box = Box();

            // The box runs from x = 0 to x = 100. A plane at x = 300 facing either way is two hundred off.
            var facingAway = new GeoPlane3(new GeoPoint3(300, 0, 0), new GeoVector3(1, 0, 0));
            var facingBack = new GeoPlane3(new GeoPoint3(300, 0, 0), new GeoVector3(-1, 0, 0));

            Assert.Equal(200.0, box.DistanceTo(facingAway), 9);
            Assert.Equal(200.0, box.DistanceTo(facingBack), 9);

            // And one at x = -40 is forty off, whichever way it faces.
            var behind = new GeoPlane3(new GeoPoint3(-40, 0, 0), new GeoVector3(1, 0, 0));

            Assert.Equal(40.0, box.DistanceTo(behind), 9);

            // Touching counts as meeting.
            var touching = new GeoPlane3(new GeoPoint3(100, 0, 0), new GeoVector3(1, 0, 0));

            Assert.Equal(0.0, box.DistanceTo(touching), 9);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            GeoSolid3 box = Box();
            var polygon = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0));

            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo((GeoSolid3)null, polygon));
            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo(box, (GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo(box, (GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => box.DistanceTo((GeoSolid3)null));
        }
    }
}
