using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A straight shape asked about a curved one. The curved types could always measure themselves against
    /// the straight ones, but not the other way about, so half of every pair would not compile. The answer
    /// has to be the same whichever end the question starts from, and the segment has to turn round with it.
    /// </summary>
    public class MixedFamilyTests
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
        /// A half circle of radius twenty about (300, 50), swept from a quarter turn to three quarters, so
        /// it is the left-hand half and it faces the shapes at the origin. Its nearest point is (280, 50).
        /// </summary>
        private static GeoArc2 Bulge() => new GeoArc2(new GeoPoint2(300, 50), 20.0, Math.PI / 2, 3 * Math.PI / 2);

        private static GeoPolygonArc2 Slot() => new GeoPolygonArc2(
            new[] { new GeoPoint2(300, 0), new GeoPoint2(400, 0), new GeoPoint2(400, 100), new GeoPoint2(300, 100) },
            new[] { 0.0, 1.0, 0.0, 0.0 });

        private static GeoPolylineArc2 OpenSlot() => Slot().ToPolylineArc2();

        [Fact]
        public void APolygonCanNowBeAskedAboutAnArcAndAgreesWithTheArcAboutIt()
        {
            GeoPolygon2 square = Square();
            GeoArc2 bulge = Bulge();

            // The square reaches x = 100 and the half circle bulges back to x = 280.
            Assert.Equal(180.0, square.DistanceTo(bulge), 9);
            Assert.Equal(Arc2.DistanceTo(bulge, Side()), square.DistanceTo(bulge), 9);

            GeoLine2 joining = square.GetShortestLineTo(bulge);

            Assert.Equal(square.DistanceTo(bulge), joining.Length, 9);
            Assert.Equal(100.0, joining.StartPoint.X, 9);
            Assert.True(Arc2.IsPointOn(bulge, joining.EndPoint, Tight));
        }

        [Fact]
        public void EveryStraightShapeCanBeAskedAboutEveryCurvedOne()
        {
            GeoPolygon2 square = Square();
            GeoPolyline2 chain = Chain();
            GeoRectangle2 plate = Plate();
            GeoLine2 side = Side();
            GeoCircle2 hole = Hole();

            GeoArc2 bulge = Bulge();
            GeoPolygonArc2 slot = Slot();
            GeoPolylineArc2 open = OpenSlot();

            // Each reading is the same as the one the curved shape gives of the straight one.
            Assert.Equal(Distance2.DistanceTo(slot, square), square.DistanceTo(slot), 9);
            Assert.Equal(Distance2.DistanceTo(slot, chain), chain.DistanceTo(slot), 9);
            Assert.Equal(Distance2.DistanceTo(slot, side), side.DistanceTo(slot), 9);
            Assert.Equal(Distance2.DistanceTo(slot, hole), hole.DistanceTo(slot), 9);
            Assert.Equal(Distance2.DistanceTo(slot, plate.ToPolygon()), plate.DistanceTo(slot), 9);

            Assert.Equal(Distance2.DistanceTo(open, square), square.DistanceTo(open), 9);
            Assert.Equal(Distance2.DistanceTo(open, chain), chain.DistanceTo(open), 9);
            Assert.Equal(Distance2.DistanceTo(open, side), side.DistanceTo(open), 9);
            Assert.Equal(Distance2.DistanceTo(open, hole), hole.DistanceTo(open), 9);
            Assert.Equal(Distance2.DistanceTo(open, plate.ToPolygon()), plate.DistanceTo(open), 9);

            Assert.Equal(Arc2.DistanceTo(bulge, side), side.DistanceTo(bulge), 9);
            Assert.Equal(Arc2.DistanceTo(bulge, hole), hole.DistanceTo(bulge), 9);
            Assert.Equal(square.DistanceTo(bulge), plate.DistanceTo(bulge), 9);
            Assert.True(chain.DistanceTo(bulge) > 0.0);

            // And the tolerance forms agree with the ones that take none.
            Assert.Equal(square.DistanceTo(slot), square.DistanceTo(slot, Tolerance.Global), 9);
            Assert.Equal(side.DistanceTo(bulge), side.DistanceTo(bulge, Tolerance.Global), 9);
            Assert.Equal(plate.DistanceTo(open), plate.DistanceTo(open, Tolerance.Global), 9);
        }

        [Fact]
        public void TheSegmentTurnsRoundWithTheQuestion()
        {
            GeoPolygon2 square = Square();
            GeoPolygonArc2 slot = Slot();

            GeoLine2 there = square.GetShortestLineTo(slot);
            GeoLine2 back = slot.GetShortestLineTo(square);

            Assert.Equal(there.Length, back.Length, 9);
            Assert.True(there.StartPoint.IsEqualTo(back.EndPoint, Tight));
            Assert.True(there.EndPoint.IsEqualTo(back.StartPoint, Tight));

            // The same for the open chain and for a plain arc.
            GeoPolylineArc2 open = OpenSlot();

            Assert.True(square.GetShortestLineTo(open).StartPoint.IsEqualTo(open.GetShortestLineTo(square).EndPoint, Tight));

            GeoLine2 side = Side();
            GeoArc2 bulge = Bulge();

            Assert.True(side.GetShortestLineTo(bulge).StartPoint.IsEqualTo(Arc2.GetShortestLineTo(bulge, side).EndPoint, Tight));
            Assert.True(side.GetShortestLineTo(bulge).EndPoint.IsEqualTo(Arc2.GetShortestLineTo(bulge, side).StartPoint, Tight));
        }

        [Fact]
        public void EverySegmentIsAsLongAsItsOwnMeasurement()
        {
            GeoPolygon2 square = Square();
            GeoPolyline2 chain = Chain();
            GeoRectangle2 plate = Plate();
            GeoLine2 side = Side();
            GeoCircle2 hole = Hole();

            GeoArc2 bulge = Bulge();
            GeoPolygonArc2 slot = Slot();
            GeoPolylineArc2 open = OpenSlot();

            // None of these shapes overlaps another, so the boundary reading and the region reading agree.
            Assert.Equal(square.DistanceTo(bulge), square.GetShortestLineTo(bulge).Length, 9);
            Assert.Equal(square.DistanceTo(slot), square.GetShortestLineTo(slot).Length, 9);
            Assert.Equal(square.DistanceTo(open), square.GetShortestLineTo(open).Length, 9);

            Assert.Equal(chain.DistanceTo(bulge), chain.GetShortestLineTo(bulge).Length, 9);
            Assert.Equal(chain.DistanceTo(slot), chain.GetShortestLineTo(slot).Length, 9);

            Assert.Equal(plate.DistanceTo(bulge), plate.GetShortestLineTo(bulge).Length, 9);
            Assert.Equal(plate.DistanceTo(slot), plate.GetShortestLineTo(slot).Length, 9);

            Assert.Equal(side.DistanceTo(bulge), side.GetShortestLineTo(bulge).Length, 9);
            Assert.Equal(side.DistanceTo(slot), side.GetShortestLineTo(slot).Length, 9);

            Assert.Equal(hole.DistanceTo(bulge), hole.GetShortestLineTo(bulge).Length, 9);
            Assert.Equal(hole.DistanceTo(slot), hole.GetShortestLineTo(slot).Length, 9);
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNothing()
        {
            GeoPolygon2 square = Square();
            GeoRectangle2 plate = Plate();
            GeoArc2 bulge = Bulge();
            GeoPolygonArc2 slot = Slot();

            Assert.True(Projection2.GetShortestLineTo(square, bulge).IsEqualTo(square.GetShortestLineTo(bulge), Tight));
            Assert.True(Projection2.GetShortestLineTo(square, slot, Tolerance.Global).IsEqualTo(square.GetShortestLineTo(slot, Tolerance.Global), Tight));
            Assert.True(Projection2.GetShortestLineTo(plate, bulge).IsEqualTo(plate.GetShortestLineTo(bulge), Tight));
            Assert.Equal(Distance2.DistanceTo(square, bulge), square.DistanceTo(bulge), 9);
            Assert.Equal(Distance2.DistanceTo(plate, slot), plate.DistanceTo(slot), 9);

            Assert.Throws<ArgumentNullException>(() => Distance2.DistanceTo((GeoPolygon2)null, bulge));
            Assert.Throws<ArgumentNullException>(() => Distance2.DistanceTo((GeoPolyline2)null, bulge));
            Assert.Throws<ArgumentNullException>(() => Projection2.GetShortestLineTo(square, (GeoPolygonArc2)null));
            Assert.Throws<ArgumentNullException>(() => Projection2.GetShortestLineTo((GeoPolygon2)null, bulge));
        }

        [Fact]
        public void AnArcInsideAPolygonIsNoDistanceFromItButStillHasASegmentToItsOutline()
        {
            GeoPolygon2 square = Square();
            var buried = new GeoArc2(new GeoPoint2(50, 50), 10.0, 0.0, Math.PI);

            // Distance2 reads a polygon as a filled region, so an arc lying inside it is nought away.
            Assert.Equal(0.0, square.DistanceTo(buried), 12);

            // The segment runs from boundary to boundary, so it has a length.
            GeoLine2 joining = square.GetShortestLineTo(buried);

            Assert.True(joining.Length > 0.0);
            Assert.Equal(40.0, joining.Length, 9);
            Assert.True(Arc2.IsPointOn(buried, joining.EndPoint, Tight));
        }
    }
}
