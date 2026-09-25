using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A straight shape asked whether it meets a curved one, and where. The measurements went both ways
    /// already; these are the questions that still ran one way only, and the rectangle, which the curved
    /// types had never been offered at all.
    /// </summary>
    public class MixedMeetingTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoPolygon2 Square() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

        private static GeoPolyline2 Chain() => new GeoPolyline2(
            new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100));

        private static GeoRectangle2 Plate() => new GeoRectangle2(0, 0, 100, 100);

        private static GeoLine2 Side() => new GeoLine2(new GeoPoint2(100, 0), new GeoPoint2(100, 100));

        private static GeoCircle2 Hole() => new GeoCircle2(new GeoPoint2(50, 50), 10.0);

        /// <summary>
        /// A half circle of radius fifty about (100, 50), bulging out to x = 150 and cutting the right-hand
        /// side of every shape above at its two ends.
        /// </summary>
        private static GeoArc2 Bulge() => new GeoArc2(new GeoPoint2(100, 50), 50.0, -Math.PI / 2, Math.PI / 2);

        /// <summary>
        /// A loop sitting astride the right-hand side of the square, so the two really do overlap.
        /// </summary>
        private static GeoPolygonArc2 Straddling() => new GeoPolygonArc2(new GeoPolygon2(
            new GeoPoint2(50, 25), new GeoPoint2(150, 25), new GeoPoint2(150, 75), new GeoPoint2(50, 75)));

        private static GeoPolygonArc2 Away() => new GeoPolygonArc2(new GeoPolygon2(
            new GeoPoint2(300, 0), new GeoPoint2(400, 0), new GeoPoint2(400, 100), new GeoPoint2(300, 100)));

        [Fact]
        public void APolygonCanNowBeAskedWhetherItMeetsAnArc()
        {
            GeoPolygon2 square = Square();
            GeoArc2 bulge = Bulge();

            Assert.True(square.CollidesWith(bulge));
            Assert.True(square.CollidesWith(bulge, Tolerance.Global));

            // The arc leaves and returns at the two ends of the right-hand side.
            GeoPoint2[] meetings = square.GetIntersections(bulge);

            Assert.Equal(2, meetings.Length);
            Assert.Contains(meetings, p => p.IsEqualTo(new GeoPoint2(100, 0), Tight));
            Assert.Contains(meetings, p => p.IsEqualTo(new GeoPoint2(100, 100), Tight));
        }

        [Fact]
        public void AnArcInsideAClosedShapeCountsAsTouchingItEvenWithoutCrossing()
        {
            GeoPolygon2 square = Square();
            GeoRectangle2 plate = Plate();
            GeoCircle2 hole = Hole();

            var buried = new GeoArc2(new GeoPoint2(50, 50), 5.0, 0.0, Math.PI);

            Assert.Empty(square.GetIntersections(buried));
            Assert.True(square.CollidesWith(buried));
            Assert.True(plate.CollidesWith(buried));
            Assert.True(hole.CollidesWith(buried));

            // A polyline has no inside, so an arc that never crosses it does not touch it.
            Assert.False(Chain().CollidesWith(buried));
            Assert.Empty(Chain().GetIntersections(buried));

            // Nor does a segment.
            Assert.False(Side().CollidesWith(buried));
        }

        [Fact]
        public void EveryStraightShapeCanBeAskedAboutEveryCurvedOne()
        {
            GeoPolygon2 square = Square();
            GeoPolyline2 chain = Chain();
            GeoRectangle2 plate = Plate();
            GeoLine2 side = Side();
            GeoCircle2 hole = Hole();

            GeoPolygonArc2 straddling = Straddling();
            GeoPolylineArc2 open = straddling.ToPolylineArc2();
            GeoArc2 bulge = Bulge();

            // Each answer is the one the curved shape gives of the straight one.
            Assert.Equal(Collision2.CollidesWith(straddling, square), square.CollidesWith(straddling));
            Assert.Equal(Collision2.CollidesWith(straddling, chain), chain.CollidesWith(straddling));
            Assert.Equal(Collision2.CollidesWith(straddling, side), side.CollidesWith(straddling));
            Assert.Equal(Collision2.CollidesWith(straddling, hole), hole.CollidesWith(straddling));
            Assert.Equal(Collision2.CollidesWith(straddling, plate.ToPolygon()), plate.CollidesWith(straddling));

            Assert.Equal(Intersection2.GetIntersections(straddling, square).Length, square.GetIntersections(straddling).Length);
            Assert.Equal(Intersection2.GetIntersections(open, chain).Length, chain.GetIntersections(open).Length);
            Assert.Equal(Intersection2.GetIntersections(straddling, side).Length, side.GetIntersections(straddling).Length);
            Assert.Equal(Intersection2.GetIntersections(straddling, hole).Length, hole.GetIntersections(straddling).Length);

            // The square and the straddling loop overlap; the one out at x = 300 does not.
            Assert.True(square.CollidesWith(straddling));
            Assert.False(square.CollidesWith(Away()));
            Assert.True(plate.CollidesWith(straddling));
            Assert.False(plate.CollidesWith(Away()));

            Assert.True(side.CollidesWith(bulge));
            Assert.True(plate.CollidesWith(bulge));
            Assert.True(chain.CollidesWith(bulge));
        }

        [Fact]
        public void ACurvedShapeCanNowBeAskedAboutARectangle()
        {
            GeoRectangle2 plate = Plate();
            GeoPolygonArc2 straddling = Straddling();
            GeoPolygonArc2 away = Away();

            Assert.True(straddling.CollidesWith(plate));
            Assert.False(away.CollidesWith(plate));
            Assert.Equal(plate.CollidesWith(straddling), straddling.CollidesWith(plate));

            Assert.Equal(plate.GetIntersections(straddling).Length, straddling.GetIntersections(plate).Length);
            Assert.Equal(plate.DistanceTo(away), away.DistanceTo(plate), 9);

            // Two hundred from the right-hand side of the plate out to x = 300.
            Assert.Equal(200.0, away.DistanceTo(plate), 9);

            GeoLine2 there = away.GetShortestLineTo(plate);
            GeoLine2 back = plate.GetShortestLineTo(away);

            Assert.Equal(200.0, there.Length, 9);
            Assert.True(there.StartPoint.IsEqualTo(back.EndPoint, Tight));
            Assert.True(there.EndPoint.IsEqualTo(back.StartPoint, Tight));
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNothing()
        {
            GeoPolygon2 square = Square();
            GeoRectangle2 plate = Plate();
            GeoArc2 bulge = Bulge();
            GeoPolygonArc2 straddling = Straddling();

            Assert.Equal(Collision2.CollidesWith(square, bulge), square.CollidesWith(bulge));
            Assert.Equal(Collision2.CollidesWith(plate, straddling), plate.CollidesWith(straddling));
            Assert.Equal(Intersection2.GetIntersections(square, bulge).Length, square.GetIntersections(bulge).Length);
            Assert.Equal(Intersection2.GetIntersections(plate, bulge).Length, plate.GetIntersections(bulge).Length);

            Assert.Throws<ArgumentNullException>(() => Collision2.CollidesWith((GeoPolygon2)null, bulge));
            Assert.Throws<ArgumentNullException>(() => Collision2.CollidesWith((GeoPolyline2)null, bulge));
            Assert.Throws<ArgumentNullException>(() => Collision2.CollidesWith(plate, (GeoPolygonArc2)null));
            Assert.Throws<ArgumentNullException>(() => Intersection2.GetIntersections((GeoPolygon2)null, bulge));
            Assert.Throws<ArgumentNullException>(() => Intersection2.GetIntersections(plate, (GeoPolylineArc2)null));
        }
    }
}
