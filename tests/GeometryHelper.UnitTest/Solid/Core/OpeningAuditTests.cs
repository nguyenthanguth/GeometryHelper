using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The audit that started this work, kept as a test: every question a body is asked, by a probe lying
    /// wholly inside a bolt hole and five clear of every wall of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A body keeps an opening as a whole body subtracted from it, and fifteen queries read its faces — which
    /// run straight across the hole — as where the material ends. A pin through a bolt hole was a clash, nought
    /// away, and crossed four times. For a clash check between Tekla parts that is every bolted connection.
    /// </para>
    /// <para>
    /// Every expected value is worked out by hand from the drawing: the plate is 100 x 100 x 20, the hole is
    /// 20 x 20 at 40..60 and overshoots both faces by one, as a through-hole usually is drawn.
    /// </para>
    /// </remarks>
    public class OpeningAuditTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Plate() => Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });

        // Probes lying wholly in the hole: up the middle of it, or within 5 of its middle.
        private static readonly GeoLine3 Line = new GeoLine3(new GeoPoint3(50, 50, -50), new GeoPoint3(50, 50, 50));
        private static readonly GeoRay3 Ray = new GeoRay3(new GeoPoint3(50, 50, -50), GeoVector3.ZAxis);
        private static readonly GeoPolyline3 Polyline = new GeoPolyline3(new GeoPoint3(50, 50, -50), new GeoPoint3(50, 50, 0), new GeoPoint3(50, 50, 50));
        private static readonly GeoTriangle3 Triangle = new GeoTriangle3(new GeoPoint3(46, 46, 10), new GeoPoint3(54, 46, 10), new GeoPoint3(50, 54, 10));
        private static readonly GeoPolygon3 Polygon = new GeoPolygon3(new GeoPoint3(46, 46, 10), new GeoPoint3(54, 46, 10), new GeoPoint3(54, 54, 10), new GeoPoint3(46, 54, 10));
        private static readonly GeoObb3 Obb = new GeoObb3(new GeoPoint3(50, 50, 0), 10, 10, 100);
        private static readonly GeoAabb3 Aabb = new GeoAabb3(new GeoPoint3(45, 45, -50), new GeoPoint3(55, 55, 50));
        private static GeoSolid3 Pin() => Box(45, 45, -50, 55, 55, 50);

        [Fact]
        public void NothingInTheHoleTouchesThePlate()
        {
            GeoSolid3 plate = Plate();

            Assert.False(plate.CollidesWith(Line));
            Assert.False(plate.CollidesWith(Ray));
            Assert.False(plate.CollidesWith(Polyline));
            Assert.False(plate.CollidesWith(Polygon));
            Assert.False(plate.CollidesWith(new GeoFace3(Polygon)));
            Assert.False(plate.CollidesWith(Obb));
            Assert.False(plate.CollidesWith(Aabb));
            Assert.False(plate.CollidesWith(Pin()));

            // And asked the other way round, the same.
            Assert.False(Line.CollidesWith(plate));
            Assert.False(Ray.CollidesWith(plate));
            Assert.False(Polyline.CollidesWith(plate));
            Assert.False(Obb.CollidesWith(plate));
            Assert.False(Aabb.CollidesWith(plate));
            Assert.False(Pin().CollidesWith(plate));
        }

        [Fact]
        public void EveryDistanceIsMeasuredToTheWallsOfTheHole()
        {
            GeoSolid3 plate = Plate();

            // Up the middle: ten from every wall.
            Assert.Equal(10.0, plate.DistanceTo(new GeoPoint3(50, 50, 10)), 6);
            Assert.Equal(10.0, plate.DistanceTo(Line), 6);
            Assert.Equal(10.0, plate.DistanceTo(Ray), 6);
            Assert.Equal(10.0, plate.DistanceTo(Polyline), 6);

            // Corners at 46 and 54: six from the walls at 40 and 60.
            Assert.Equal(6.0, plate.DistanceTo(Triangle), 6);
            Assert.Equal(6.0, plate.DistanceTo(Polygon), 6);

            // Sides at 45 and 55: five.
            Assert.Equal(5.0, plate.DistanceTo(Obb), 6);
            Assert.Equal(5.0, plate.DistanceTo(Aabb), 6);
            Assert.Equal(5.0, plate.DistanceTo(Pin()), 6);

            // The other way round, the same.
            Assert.Equal(10.0, Line.DistanceTo(plate), 6);
            Assert.Equal(10.0, Ray.DistanceTo(plate), 6);
            Assert.Equal(5.0, Pin().DistanceTo(plate), 6);
        }

        [Fact]
        public void NothingUpTheMiddleOfTheHoleCrossesThePlate()
        {
            GeoSolid3 plate = Plate();

            Assert.Empty(plate.GetIntersections(Line));
            Assert.Empty(plate.GetIntersections(Ray));
            Assert.Empty(Polyline.GetIntersections(plate));

            // A line through the material, beside the hole, still crosses top and bottom.
            var beside = new GeoLine3(new GeoPoint3(20, 20, -50), new GeoPoint3(20, 20, 50));

            Assert.Equal(2, plate.GetIntersections(beside).Length);

            // And a line crossing the hole sideways, at mid-depth, meets the walls where they are.
            var across = new GeoLine3(new GeoPoint3(-50, 50, 10), new GeoPoint3(150, 50, 10));
            GeoPoint3[] hits = plate.GetIntersections(across);

            Assert.Equal(4, hits.Length);
            Assert.Contains(hits, hit => hit.IsEqualTo(new GeoPoint3(40, 50, 10), Loose));
            Assert.Contains(hits, hit => hit.IsEqualTo(new GeoPoint3(60, 50, 10), Loose));
        }

        [Fact]
        public void TheShortestLinesReachTheWallsNotTheFacesAcrossTheHole()
        {
            GeoSolid3 plate = Plate();

            Assert.Equal(10.0, plate.GetShortestLineTo(Line).Length, 6);
            Assert.Equal(10.0, plate.GetShortestLineTo(Ray).Length, 6);
            Assert.Equal(6.0, plate.GetShortestLineTo(Triangle).Length, 6);
            Assert.Equal(5.0, plate.GetShortestLineTo(Pin()).Length, 6);

            // The line leaves the plate on a wall of the hole.
            GeoLine3 toPin = plate.GetShortestLineTo(Pin());

            Assert.Equal(PointLocation.OnSide, plate.Locate(toPin.StartPoint));
        }

        [Fact]
        public void APinThatReallyBitesThePlateIsStillAClash()
        {
            GeoSolid3 plate = Plate();

            // Wider than the hole: it bites five into the plate all the way round.
            GeoSolid3 fat = Box(35, 35, -50, 65, 65, 50);

            Assert.True(plate.CollidesWith(fat));
            Assert.Equal(0.0, plate.DistanceTo(fat), 9);
            Assert.True(plate.TryIntersect(fat, out GeoSolid3 bite));
            Assert.Equal(30.0 * 30 * 20 - 20.0 * 20 * 20, bite.Volume, 3);

            // A pin grazing one wall exactly touches, and that is contact.
            GeoSolid3 grazing = Box(40, 45, -50, 50, 55, 50);

            Assert.True(plate.CollidesWith(grazing));
            Assert.Equal(0.0, plate.DistanceTo(grazing), 9);
        }

        [Fact]
        public void AnIndexOverThePlateLetsARayThroughTheHole()
        {
            GeoSolid3 plate = Plate();
            GeoBvh3 index = plate.BuildIndex();

            // The claim: the index follows the material, so a ray down the hole meets nothing.
            Assert.Empty(index.GetIntersections(Ray));
            Assert.Empty(GeoBvh3.FromSolid(plate).GetIntersections(Ray));

            // Through the material, the index answers exactly what scanning the surface mesh answers, one hit
            // per triangle it meets — its contract, which is why a ray landing on a diagonal of the mesh is
            // named by both triangles sharing it. Checked against the scan rather than against a count
            // guessed from where the diagonals might run.
            var through = new GeoRay3(new GeoPoint3(13, 27, -50), GeoVector3.ZAxis);
            int scanned = plate.TriangulateSurface().Count(triangle => GeometryHelper.Core.Intersection3.TryIntersectWith(through, triangle, out _));

            Assert.True(scanned >= 2);
            Assert.Equal(scanned, index.GetIntersections(through).Length);

            // The body itself names each place once: top and bottom.
            Assert.Equal(2, plate.GetIntersections(through).Length);
        }

        [Fact]
        public void AFarAwayOpeningIsNotCutForAQuestionAskedFarFromIt()
        {
            // Twenty holes along a plate; the pin sits in the tenth. Only that hole can change the answer, and
            // the answer is the same as for a plate with that hole alone.
            var holes = Enumerable.Range(0, 20).Select(i => Box(20 + i * 50, 40, -1, 40 + i * 50, 60, 21)).ToArray();
            GeoSolid3 plate = Box(0, 0, 0, 1020, 100, 20).WithOpenings(holes);
            GeoSolid3 pin = Box(475, 45, -50, 485, 55, 50);

            Assert.False(plate.CollidesWith(pin));
            Assert.Equal(5.0, plate.DistanceTo(pin), 6);

            GeoSolid3 oneHole = Box(0, 0, 0, 1020, 100, 20).WithOpenings(new[] { holes[9] });

            Assert.Equal(oneHole.DistanceTo(pin), plate.DistanceTo(pin), 6);
        }

        [Fact]
        public void CuttingOnceAndAskingManyTimesGivesTheSameAnswers()
        {
            GeoSolid3 plate = Plate();

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material));
            Assert.Empty(material.Openings);

            Assert.Equal(plate.CollidesWith(Pin()), material.CollidesWith(Pin()));
            Assert.Equal(plate.DistanceTo(Pin()), material.DistanceTo(Pin()), 9);
            Assert.Equal(plate.DistanceTo(Triangle), material.DistanceTo(Triangle), 9);
            Assert.Equal(plate.GetIntersections(Line).Length, material.GetIntersections(Line).Length);
        }
    }
}
