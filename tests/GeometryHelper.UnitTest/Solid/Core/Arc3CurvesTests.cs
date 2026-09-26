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
