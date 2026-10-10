using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Where two curves in space cross each other. Coplanar goes down into the shared plane, where
    /// <see cref="Arc2"/> answers; otherwise the two planes meet in a line and at most two points can be
    /// common to both.
    /// </summary>
    /// <remarks>
    /// Arcs are built from three points they pass through rather than from angles, because angle nought sits
    /// wherever <see cref="GeoPlane3.GetAxes"/> puts it.
    /// </remarks>
    public class Arc3CurvesTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A half turn of a hundred radius in z = 0, from (100, 0, 0) over the top to (-100, 0, 0).
        /// </summary>
        private static GeoArc3 UpperHalf() => GeoArc3.FromThreePoints(
            new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0), new GeoPoint3(-100, 0, 0));

        /// <summary>
        /// The other half of the same circle, from (-100, 0, 0) under the bottom back to (100, 0, 0).
        /// </summary>
        private static GeoArc3 LowerHalf() => GeoArc3.FromThreePoints(
            new GeoPoint3(-100, 0, 0), new GeoPoint3(0, -100, 0), new GeoPoint3(100, 0, 0));

        // A plane through the origin tilted 30 degrees about X, so that the coplanar cases below are in space.
        private static readonly GeoVector3 Along = new GeoVector3(1, 0, 0);

        private static readonly GeoVector3 Across = new GeoVector3(0, Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6));

        private static GeoPoint3 InTilted(double x, double y) => new GeoPoint3(0, 0, 0).Add(Along.Multiply(x)).Add(Across.Multiply(y));

        [Fact]
        public void ACoplanarCircleAHairInsideAnotherNearlyTouchingIt_TouchesItOnce_BetweenTheRims()
        {
            // As in the plane (IntersectionTests): a circle of radius 99.94 a centre 0.0593 off the middle of one of
            // 100, both in the tilted plane, comes within 0.0007 of its rim at (100, 0) without crossing it, within the
            // point tolerance of 0.001, so they touch once, at (99.99965, 0) between the rims. Off the radical line the
            // touch fell 1.18 outside both and nothing was found.
            var tolerance = new Tolerance(1E-3, 1E-5);
            GeoVector3 normal = Along.CrossProduct(Across);
            var outer = new GeoCircle3(InTilted(0, 0), normal, 100);
            var inner = new GeoCircle3(InTilted(0.0593, 0), normal, 99.94);

            foreach (GeoPoint3[] found in new[] { outer.GetIntersections(inner, tolerance), inner.GetIntersections(outer, tolerance) })
            {
                GeoPoint3 touch = Assert.Single(found);
                Assert.True(touch.IsEqualTo(InTilted(99.99965, 0), new Tolerance(1E-9, 1E-9)), touch.ToString());
            }

            Assert.True(outer.CollidesWith(inner, tolerance));
        }

        [Fact]
        public void ACoplanarCircleAHairOutsideAnotherNearlyTouchingIt_TouchesItOnce_BetweenTheRims()
        {
            // As in the plane: circles of radius 250 and 100, their centres 350.0004 apart in the tilted plane, come
            // within 0.0004 of each other at (0, 250) without crossing, inside the point tolerance of 0.001, so they
            // touch once, at (0, 250.0002) between the rims, as GeoCircle2 says. The radical line runs 0.0002 outside
            // both there, and nothing was found.
            var tolerance = new Tolerance(1E-3, 1E-5);
            GeoVector3 normal = Along.CrossProduct(Across);
            var big = new GeoCircle3(InTilted(0, 0), normal, 250);
            var small = new GeoCircle3(InTilted(0, 350.0004), normal, 100);

            foreach (GeoPoint3[] found in new[] { big.GetIntersections(small, tolerance), small.GetIntersections(big, tolerance) })
            {
                GeoPoint3 touch = Assert.Single(found);
                Assert.True(touch.IsEqualTo(InTilted(0, 250.0002), new Tolerance(1E-9, 1E-9)), touch.ToString());
            }

            Assert.True(big.CollidesWith(small, tolerance));

            // Two thousandths apart they are clear of the tolerance, and nothing touches.
            var clear = new GeoCircle3(InTilted(0, 350.002), normal, 100);
            Assert.Empty(big.GetIntersections(clear, tolerance));
            Assert.False(big.CollidesWith(clear, tolerance));
        }

        [Fact]
        public void TwoCirclesFarFromTheOriginInPlanesAThousandthOfADegreeApartCrossAtBothPoints()
        {
            // A circle of radius 300 about (30 000, 30 000, 30 000), where real models lie, and one through its rim at
            // angles 0.6 and 2.8, its centre 200 back across that chord, in a plane leaning 0.001 degrees about it. The
            // line the planes share was found from their distances to the world origin, which a thousandth of a degree
            // turns from the rounding of 52 000 into an error of millimetres: no point was found.
            var tolerance = Tolerance.Default;
            var first = new GeoCircle3(new GeoPoint3(30000, 30000, 30000), new GeoVector3(0, -Math.Sin(Math.PI / 6), Math.Cos(Math.PI / 6)), 300);
            GeoPoint3 a = first.GetPointAtAngle(0.6);
            GeoPoint3 b = first.GetPointAtAngle(2.8);
            GeoVector3 chord = a.GetVectorTo(b).Normalize();
            GeoVector3 normal = first.Normal.RotateBy(0.001 * Math.PI / 180.0, chord);
            GeoPoint3 middle = a.Add(a.GetVectorTo(b).Multiply(0.5));
            GeoVector3 back = normal.CrossProduct(chord).Normalize();
            if (back.DotProduct(middle.GetVectorTo(first.Center)) > 0.0)
            {
                back = back.Multiply(-1.0);
            }

            GeoPoint3 centre = middle.Add(back.Multiply(200));
            var second = new GeoCircle3(centre, normal, centre.DistanceTo(a));

            foreach (GeoPoint3[] found in new[] { first.GetIntersections(second, tolerance), second.GetIntersections(first, tolerance) })
            {
                Assert.Equal(2, found.Length);
                Assert.Contains(found, p => p.DistanceTo(a) <= 1E-6);
                Assert.Contains(found, p => p.DistanceTo(b) <= 1E-6);
            }

            Assert.True(first.CollidesWith(second, tolerance));
        }

        [Fact]
        public void ACoplanarArcPassingFourTenThousandthsOutsideACircle_TouchesIt()
        {
            // The far half of a circle of radius 101.5004 about (1.5, 0) passes the circle of radius 100 at (-100, 0)
            // only 0.0004 out, within the tolerance: one touch at (-100.0002, 0), as in the plane (ArcAgainstDiscTests).
            var tolerance = new Tolerance(1E-3, 1E-5);
            GeoVector3 normal = Along.CrossProduct(Across);
            var circle = new GeoCircle3(InTilted(0, 0), normal, 100);
            GeoArc3 half = GeoArc3.FromThreePoints(InTilted(1.5, 101.5004), InTilted(1.5 - 101.5004, 0), InTilted(1.5, -101.5004));

            GeoPoint3 touch = Assert.Single(Arc3.GetIntersections(half, circle, tolerance));
            Assert.True(touch.IsEqualTo(InTilted(-100.0002, 0), new Tolerance(1E-9, 1E-9)), touch.ToString());
            Assert.True(Arc3.CollidesWith(half, circle, tolerance));

            // Two thousandths out it is clear of the tolerance, and nothing touches.
            GeoArc3 clear = GeoArc3.FromThreePoints(InTilted(1.5, 101.502), InTilted(1.5 - 101.502, 0), InTilted(1.5, -101.502));
            Assert.Empty(Arc3.GetIntersections(clear, circle, tolerance));
        }

        [Fact]
        public void TwoCoplanarArcsOfDifferentCirclesCrossWhereTheirCirclesDo()
        {
            GeoArc3 first = UpperHalf();

            // A second circle of the same radius centred at (100, 0, 0): the two circles meet at
            // (50, +-86.602540, 0), and this half sweeps the upper one.
            GeoArc3 second = GeoArc3.FromThreePoints(
                new GeoPoint3(200, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 0, 0));

            GeoPoint3[] crossings = Arc3.GetIntersections(first, second);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(50, Math.Sqrt(100 * 100 - 50 * 50), 0), Loose),
                crossings[0].ToString());

            Assert.True(Arc3.CollidesWith(first, second));
            Assert.Equal(crossings.Length, Arc3.GetIntersections(second, first).Length);

            // Two coplanar circles far apart share nothing.
            GeoArc3 apart = GeoArc3.FromThreePoints(
                new GeoPoint3(1000, 0, 0), new GeoPoint3(900, 100, 0), new GeoPoint3(800, 0, 0));

            Assert.Empty(Arc3.GetIntersections(first, apart));
            Assert.False(Arc3.CollidesWith(first, apart));
        }

        [Fact]
        public void TwoArcsOfOneCircleCrossNowhereAndStillTouchWhereTheyMeet()
        {
            GeoArc3 upper = UpperHalf();
            GeoArc3 lower = LowerHalf();

            // Circles lying on each other meet along their length rather than at points, so there is nothing
            // to name as a crossing -- and the two halves do meet, at both ends.
            Assert.Empty(Arc3.GetIntersections(upper, lower));
            Assert.True(Arc3.CollidesWith(upper, lower));

            // The same arc against itself.
            Assert.True(Arc3.CollidesWith(upper, upper));

            // And two arcs of one circle that do not reach each other touch nowhere.
            GeoArc3 quarter = GeoArc3.FromThreePoints(
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(100 * Math.Sqrt(0.5), 100 * Math.Sqrt(0.5), 0),
                new GeoPoint3(0, 100, 0));
            GeoArc3 farQuarter = GeoArc3.FromThreePoints(
                new GeoPoint3(0, -100, 0),
                new GeoPoint3(100 * Math.Sqrt(0.5), -100 * Math.Sqrt(0.5), 0),
                new GeoPoint3(100, 0, 0));

            // These two share the end (100, 0, 0), so they do touch.
            Assert.True(Arc3.CollidesWith(quarter, farQuarter));
        }

        [Fact]
        public void TwoArcsInDifferentPlanesShareAtMostTheTwoPointsOnTheirPlanesLine()
        {
            GeoArc3 flat = UpperHalf();

            // A half turn standing upright in the plane y = 0, of the same radius about the same centre. The
            // two planes meet along the x axis, and both circles pass through (100, 0, 0) and (-100, 0, 0).
            GeoArc3 upright = GeoArc3.FromThreePoints(
                new GeoPoint3(100, 0, 0), new GeoPoint3(0, 0, 100), new GeoPoint3(-100, 0, 0));

            GeoPoint3[] crossings = Arc3.GetIntersections(flat, upright);

            Assert.Equal(2, crossings.Length);

            double[] xs = crossings.Select(point => Math.Round(point.X, 6)).OrderBy(x => x).ToArray();

            Assert.Equal(new[] { -100.0, 100.0 }, xs);
            Assert.All(crossings, point => Assert.Equal(0.0, point.Y, 6));
            Assert.All(crossings, point => Assert.Equal(0.0, point.Z, 6));

            Assert.True(Arc3.CollidesWith(flat, upright));
            Assert.Equal(crossings.Length, Arc3.GetIntersections(upright, flat).Length);

            // Lift the upright one clear of the flat one and they share nothing.
            GeoArc3 lifted = upright.Translate(new GeoVector3(0, 0, 500));

            Assert.Empty(Arc3.GetIntersections(flat, lifted));
            Assert.False(Arc3.CollidesWith(flat, lifted));
        }

        [Fact]
        public void ACircleIsTheWholeTurnOfAnArcHere()
        {
            GeoArc3 upper = UpperHalf();
            var ring = new GeoCircle3(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 100.0);

            // The circle about (100, 0, 0) meets the circle about the origin at (50, +-86.602540, 0); the
            // upper half reaches only the positive one.
            GeoPoint3[] crossings = Arc3.GetIntersections(upper, ring);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(50, Math.Sqrt(100 * 100 - 50 * 50), 0), Loose));

            // Two whole circles reach both.
            var about = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0);

            Assert.Equal(2, Arc3.GetIntersections(about, ring).Length);
            Assert.True(Arc3.CollidesWith(about, ring));

            // Two circles of one circle -- the same one twice -- meet along their length, not at points.
            Assert.Empty(Arc3.GetIntersections(about, about));
            Assert.True(Arc3.CollidesWith(about, about));

            // And two circles in different planes through the same two points.
            var upright = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 1, 0), 100.0);

            Assert.Equal(2, Arc3.GetIntersections(about, upright).Length);
        }

        [Fact]
        public void TheTypesAnswerTheSameFromEitherSide()
        {
            GeoArc3 upper = UpperHalf();
            GeoArc3 lower = LowerHalf();
            var ring = new GeoCircle3(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 100.0);
            var about = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0);

            Assert.Equal(Arc3.GetIntersections(upper, ring).Length, upper.GetIntersections(ring).Length);
            Assert.Equal(upper.GetIntersections(ring).Length, ring.GetIntersections(upper).Length);
            Assert.Equal(upper.CollidesWith(ring), ring.CollidesWith(upper));
            Assert.Equal(upper.CollidesWith(lower), lower.CollidesWith(upper));
            Assert.Equal(about.GetIntersections(ring).Length, ring.GetIntersections(about).Length);

            Assert.True(upper.TryIntersectWith(ring, out GeoPoint3[] a));
            Assert.True(ring.TryIntersectWith(upper, out GeoPoint3[] b));
            Assert.Equal(a.Length, b.Length);

            // Two halves of one circle: touching, with nothing to name.
            Assert.False(upper.TryIntersectWith(lower, out GeoPoint3[] none));
            Assert.Empty(none);
            Assert.True(upper.CollidesWith(lower));
        }

        [Fact]
        public void AWholeTurnCanBeLaidOutInAPlaneAtAll()
        {
            // Found by these tests rather than by reading: a whole turn has its start and its end in the same
            // place, so the three points that describe every other arc are two points and cannot describe it.
            // GeoArc3.ProjectToArc2 used to hand those three to FromThreePoints and get "three points on one
            // line have no arc through them" -- from a Try method, by way of PlanarMap.TryToArc2.
            var whole = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0, 0.0, 0.0);
            GeoCoordinateSystem3 frame = PlanarMap.FrameOf(whole.GetPlane());

            GeoArc2 flat = whole.ProjectToArc2(frame);

            Assert.Equal(100.0, flat.Radius, 9);
            Assert.True(flat.Center.IsEqualTo(new GeoPoint2(0, 0), Loose));
            Assert.Equal(2.0 * Math.PI, Math.Abs(flat.SweptAngle), 6);

            Assert.True(PlanarMap.TryToArc2(frame, whole, out GeoArc2 viaMap));
            Assert.Equal(flat.Radius, viaMap.Radius, 9);

            // A partial arc is still laid out through its three points, unchanged.
            GeoArc3 half = UpperHalf();

            Assert.True(PlanarMap.TryToArc2(PlanarMap.FrameOf(half.GetPlane()), half, out GeoArc2 flatHalf));
            Assert.Equal(100.0, flatHalf.Radius, 9);
            Assert.Equal(Math.PI, Math.Abs(flatHalf.SweptAngle), 6);
        }

        [Fact]
        public void EveryPairTakesATolerance()
        {
            Tolerance global = Tolerance.Global;
            GeoArc3 upper = UpperHalf();
            GeoArc3 lower = LowerHalf();
            var ring = new GeoCircle3(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 100.0);
            var about = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0);

            Assert.Equal(Arc3.GetIntersections(upper, lower).Length, Arc3.GetIntersections(upper, lower, global).Length);
            Assert.Equal(Arc3.GetIntersections(upper, ring).Length, Arc3.GetIntersections(upper, ring, global).Length);
            Assert.Equal(Arc3.GetIntersections(about, ring).Length, Arc3.GetIntersections(about, ring, global).Length);
            Assert.Equal(Arc3.CollidesWith(upper, lower), Arc3.CollidesWith(upper, lower, global));
            Assert.Equal(Arc3.CollidesWith(upper, ring), Arc3.CollidesWith(upper, ring, global));
            Assert.Equal(Arc3.CollidesWith(about, ring), Arc3.CollidesWith(about, ring, global));

            Assert.Equal(upper.GetIntersections(lower).Length, upper.GetIntersections(lower, global).Length);
            Assert.Equal(upper.CollidesWith(ring), upper.CollidesWith(ring, global));
            Assert.Equal(ring.CollidesWith(about), ring.CollidesWith(about, global));
        }
    }
}
