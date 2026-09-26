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
    /// What a curved loop in space can answer by working in its own plane. A <see cref="GeoPolygonArc3"/>
    /// enforces coplanarity, so laying it out as a <see cref="GeoPolygonArc2"/> and lifting the answer back is
    /// exact rather than an approximation — which is already how its area, its centroid and its offsets are
    /// worked out.
    /// </summary>
    /// <remarks>
    /// The decision this rests on, and the tests that hold it: a second shape has to lie in the loop's plane,
    /// and is <b>refused</b> where it does not. Projecting it in would report two stirrups a hundred apart as
    /// overlapping. The library already refuses in three comparable places — a non-planar loop at construction,
    /// <c>PlanarMap.TryToArc2</c> for an arc outside the frame, and an arc in space asked for a distance with no
    /// closed form.
    /// </remarks>
    public class PolygonArc3LiftTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A stirrup: a two hundred square loop in z = 0 with its corners rounded by thirty.
        /// </summary>
        private static GeoPolygonArc3 Stirrup() => new GeoPolygonArc3(new GeoPolygon3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0),
            new GeoPoint3(200, 200, 0), new GeoPoint3(0, 200, 0))).Fillet(30.0);

        /// <summary>
        /// A plain square loop in z = 0, so that a second shape is easy to place in the same plane.
        /// </summary>
        private static GeoPolygonArc3 Square(double x, double y, double size) => new GeoPolygonArc3(new GeoPolygon3(
            new GeoPoint3(x, y, 0), new GeoPoint3(x + size, y, 0),
            new GeoPoint3(x + size, y + size, 0), new GeoPoint3(x, y + size, 0)));

        [Fact]
        public void TwoLoopsInOnePlaneCanBeJoinedAndCutFromEachOther()
        {
            GeoPolygonArc3 first = Square(0, 0, 100);
            GeoPolygonArc3 overlapping = Square(50, 50, 100);

            GeoFace3[] joined = first.Union(overlapping);

            Assert.Single(joined);
            Assert.Equal(100 * 100 + 100 * 100 - 50 * 50, joined[0].Area, 6);

            GeoFace3[] shared = first.Intersect(overlapping);

            Assert.Single(shared);
            Assert.Equal(50 * 50, shared[0].Area, 6);

            GeoFace3[] left = first.Subtract(overlapping);

            Assert.Single(left);
            Assert.Equal(100 * 100 - 50 * 50, left[0].Area, 6);

            GeoFace3[] either = first.Xor(overlapping);

            Assert.Equal(2 * (100 * 100 - 50 * 50), either.Sum(face => face.Area), 6);

            // Every face comes back in the loop's own plane.
            foreach (GeoFace3 face in joined.Concat(shared).Concat(left).Concat(either))
            {
                Assert.All(face.Boundary.Vertices, vertex => Assert.Equal(0.0, vertex.Z, 6));
            }
        }

        [Fact]
        public void ALoopInAnotherPlaneIsRefusedAndNotQuietlyProjected()
        {
            GeoPolygonArc3 flat = Square(0, 0, 100);

            // The same square lifted a hundred above: it shares nothing with the first, and projecting it in
            // would report the two as overlapping entirely.
            GeoPolygonArc3 above = flat.Translate(new GeoVector3(0, 0, 100));

            Assert.False(flat.SharesPlaneWith(above.GetPlane(), Loose));

            Assert.Throws<ArgumentException>(() => flat.Union(above));
            Assert.Throws<ArgumentException>(() => flat.Intersect(above));
            Assert.Throws<ArgumentException>(() => flat.Subtract(above));
            Assert.Throws<ArgumentException>(() => flat.Xor(above));

            // Standing upright is refused the same way, and so is a polygon out of the plane.
            GeoPolygonArc3 upright = new GeoPolygonArc3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 0, 100), new GeoPoint3(0, 0, 100)));

            Assert.Throws<ArgumentException>(() => flat.Union(upright));

            var raised = new GeoPolygon3(
                new GeoPoint3(0, 0, 50), new GeoPoint3(100, 0, 50),
                new GeoPoint3(100, 100, 50), new GeoPoint3(0, 100, 50));

            Assert.Throws<ArgumentException>(() => flat.Union(raised));
            Assert.Throws<ArgumentException>(() => flat.Intersect(raised));
            Assert.Throws<ArgumentException>(() => flat.Subtract(raised));
            Assert.Throws<ArgumentException>(() => flat.Xor(raised));

            // And a null operand is a different complaint.
            Assert.Throws<ArgumentNullException>(() => flat.Union((GeoPolygonArc3)null));
            Assert.Throws<ArgumentNullException>(() => flat.Union((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => flat.Xor((GeoPolygon3)null));
        }

        [Fact]
        public void ALoopCanBeJoinedToAPolygonInTheSamePlane()
        {
            GeoPolygonArc3 loop = Square(0, 0, 100);
            var polygon = new GeoPolygon3(
                new GeoPoint3(50, 50, 0), new GeoPoint3(150, 50, 0),
                new GeoPoint3(150, 150, 0), new GeoPoint3(50, 150, 0));

            Assert.Single(loop.Union(polygon));
            Assert.Equal(50 * 50, loop.Intersect(polygon)[0].Area, 6);
            Assert.Equal(100 * 100 - 50 * 50, loop.Subtract(polygon)[0].Area, 6);
            Assert.Equal(2 * (100 * 100 - 50 * 50), loop.Xor(polygon).Sum(face => face.Area), 6);

            // All four are offered against a polygon, as they are in the plane underneath, and every face
            // comes back in the loop's own plane.
            foreach (GeoFace3 face in loop.Union(polygon).Concat(loop.Xor(polygon)))
            {
                Assert.All(face.Boundary.Vertices, vertex => Assert.Equal(0.0, vertex.Z, 6));
            }
        }

        [Fact]
        public void AStirrupCanBeChamferedAndRoundedOneCornerAtATime()
        {
            GeoPolygonArc3 plain = Square(0, 0, 200);

            GeoPolygonArc3 mitred = plain.Chamfer(20.0);

            // Cutting every corner back takes area off and leaves the loop in its own plane.
            Assert.True(mitred.Area < plain.Area);
            Assert.All(mitred.Vertices, vertex => Assert.Equal(0.0, vertex.Z, 6));

            Assert.True(plain.TryFilletAt(0, 30.0, out GeoPolygonArc3 rounded));
            Assert.True(rounded.Area < plain.Area);
            Assert.Contains(true, rounded.GetEdges().Select(edge => edge.IsArc));

            Assert.True(plain.TryChamferAt(1, 20.0, 20.0, out GeoPolygonArc3 oneCorner));
            Assert.True(oneCorner.Area < plain.Area);

            // A radius the corner has no room for is refused and the loop comes back unchanged.
            Assert.False(plain.TryFilletAt(0, 5000.0, out GeoPolygonArc3 same));
            Assert.Equal(plain.Area, same.Area, 6);
        }

        [Fact]
        public void AStirrupSaysHowFarInsideItselfAPointSits()
        {
            GeoPolygonArc3 plain = Square(0, 0, 200);

            // Dead centre is a hundred from the nearest side.
            Assert.Equal(-100.0, plain.SignedDistanceTo(new GeoPoint3(100, 100, 0)), 6);
            Assert.Equal(-10.0, plain.SignedDistanceTo(new GeoPoint3(10, 100, 0)), 6);
            Assert.Equal(20.0, plain.SignedDistanceTo(new GeoPoint3(-20, 100, 0)), 6);
            Assert.Equal(0.0, plain.SignedDistanceTo(new GeoPoint3(0, 100, 0)), 6);

            // The sign follows Locate, and Locate calls a point off the plane OutSide, so a point above the
            // loop is positive at the real distance rather than negative at its shadow.
            var above = new GeoPoint3(100, 100, 50);

            Assert.Equal(PointLocation.OutSide, plain.Locate(above));
            Assert.True(plain.SignedDistanceTo(above) > 0.0);
            Assert.Equal(plain.DistanceTo(above), plain.SignedDistanceTo(above), 6);
        }

        [Fact]
        public void AStirrupCanBeCutAndTheCutGivesOpenChains()
        {
            GeoPolygonArc3 stirrup = Stirrup();

            // A plane through the middle cuts the loop twice, so two chains come back.
            var across = new GeoPlane3(new GeoPoint3(100, 0, 0), new GeoVector3(1, 0, 0));

            Assert.True(stirrup.TrySplitBy(across, out GeoPolylineArc3[] halves));
            Assert.Equal(2, halves.Length);
            Assert.Equal(stirrup.Length, halves.Sum(piece => piece.Length), 6);

            // The bends survive the cut.
            Assert.Contains(true, halves[0].GetEdges().Select(edge => edge.IsArc));

            // One cut leaves one chain: a ring cut once is a strip, and it is as long as the ring.
            Assert.True(stirrup.TrySplitBy(stirrup.GetPointAtDistance(50.0), out GeoPolylineArc3[] opened));
            Assert.Single(opened);
            Assert.Equal(stirrup.Length, opened[0].Length, 6);

            // A plane the stirrup never reaches leaves it whole, handed back as one open chain.
            Assert.False(stirrup.TrySplitBy(new GeoPlane3(new GeoPoint3(0, 0, 500), GeoVector3.ZAxis), out GeoPolylineArc3[] whole));
            Assert.Single(whole);
            Assert.Equal(stirrup.Length, whole[0].Length, 6);
        }

        [Fact]
        public void AStirrupTrimmedToAMemberKnowsWhatIsInTheConcrete()
        {
            GeoPolygonArc3 stirrup = Stirrup();

            // A slab covering the left half of the stirrup.
            GeoSolid3 slab = new GeoAabb3(new GeoPoint3(-50, -50, -50), new GeoPoint3(100, 250, 50)).ToObb().ToSolid();

            Assert.True(stirrup.TrySplitBy(slab, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside));
            Assert.NotEmpty(inside);
            Assert.NotEmpty(outside);
            Assert.Equal(stirrup.Length, inside.Concat(outside).Sum(piece => piece.Length), 6);

            // Cut at several distances at once.
            Assert.True(stirrup.SplitAtDistances(new[] { 50.0, 200.0, 400.0 }, out GeoPolylineArc3[] pieces));
            Assert.Equal(3, pieces.Length);
            Assert.Equal(stirrup.Length, pieces.Sum(piece => piece.Length), 6);
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoPolygonArc3 loop = Square(0, 0, 100);
            GeoPolygonArc3 other = Square(50, 50, 100);
            GeoPolygonArc3 stirrup = Stirrup();
            var plane = new GeoPlane3(new GeoPoint3(100, 0, 0), new GeoVector3(1, 0, 0));

            var polygon = new GeoPolygon3(
                new GeoPoint3(50, 50, 0), new GeoPoint3(150, 50, 0),
                new GeoPoint3(150, 150, 0), new GeoPoint3(50, 150, 0));

            Assert.Equal(loop.Union(other).Length, loop.Union(other, global).Length);
            Assert.Equal(loop.Xor(polygon).Length, loop.Xor(polygon, global).Length);
            Assert.Equal(loop.Intersect(other)[0].Area, loop.Intersect(other, global)[0].Area, 6);
            Assert.Equal(loop.Chamfer(10.0).Area, loop.Chamfer(10.0, global).Area, 6);
            Assert.Equal(loop.SignedDistanceTo(new GeoPoint3(10, 50, 0)), loop.SignedDistanceTo(new GeoPoint3(10, 50, 0), global), 9);

            Assert.Equal(
                stirrup.TrySplitBy(plane, out GeoPolylineArc3[] a),
                stirrup.TrySplitBy(plane, out GeoPolylineArc3[] b, global));
            Assert.Equal(a.Length, b.Length);

            Assert.Throws<ArgumentNullException>(() => stirrup.SplitAtDistances(null, out _));
            Assert.Throws<ArgumentNullException>(() => stirrup.TrySplitBy((GeoSolid3)null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => ArcChain3.TrySplitBy((GeoPolygonArc3)null, plane, out _));
        }
    }
}
