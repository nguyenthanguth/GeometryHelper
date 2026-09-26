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
    /// Where an arc in space crosses a plane, a segment or a ray. None of it is sampled: an arc lies in a
    /// plane, two distinct planes meet in a line, and a line meets a circle in at most two points, so every
    /// answer here comes out of one quadratic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two kinds of check, and the second is the one that earns its keep. Hand-worked values pin the cases
    /// that can be worked by hand; then every answer is held against the same arc sampled to a chord
    /// tolerance of a hundredth and crossed the long way round, by an oracle written here that calls none of
    /// the code under test.
    /// </para>
    /// <para>
    /// Every arc here is built from three points it passes through, never from a start and end angle, because
    /// <see cref="GeoPlane3.GetAxes"/> says outright that a plane has no preferred pair of in-plane axes:
    /// angle nought is wherever that pair happens to point, which for the plane z = 0 is the <b>y</b> axis and
    /// not the x axis. A test that assumes otherwise tests the frame rather than the arithmetic.
    /// </para>
    /// </remarks>
    public class Arc3Tests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A quarter turn of a hundred radius about the origin, sweeping the first quadrant of z = 0 from
        /// (100, 0, 0) round to (0, 100, 0).
        /// </summary>
        private static GeoArc3 Quarter() => GeoArc3.FromThreePoints(
            new GeoPoint3(100, 0, 0),
            new GeoPoint3(100 * Math.Sqrt(0.5), 100 * Math.Sqrt(0.5), 0),
            new GeoPoint3(0, 100, 0));

        /// <summary>
        /// The reach of the circle at x = 50, which is where most of the crossings below land.
        /// </summary>
        private static double Reach() => Math.Sqrt(100 * 100 - 50 * 50);

        /// <summary>
        /// Where a sampled version of the arc crosses a plane, worked out chord by chord.
        /// </summary>
        /// <remarks>
        /// Deliberately the long way: this calls <see cref="Intersection3"/> on each chord of the sampled
        /// chain and nothing in <see cref="Arc3"/>, so agreeing with it is evidence rather than a tautology.
        /// </remarks>
        private static GeoPoint3[] SampledCrossings(GeoArc3 arc, GeoPlane3 plane)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoLine3 chord in arc.ToPolylineByChordTolerance(0.01).GetEdges())
            {
                // A vertex of the chain sitting on the plane belongs to the two chords that meet there, and
                // both report it, so the same place has to be counted once.
                if (Intersection3.TryIntersectWith(chord, plane, out GeoPoint3 hit)
                    && !found.Any(already => already.IsEqualTo(hit, Loose)))
                {
                    found.Add(hit);
                }
            }

            return found.ToArray();
        }

        private static void Agrees(GeoPoint3[] exact, GeoPoint3[] sampled, double slack)
        {
            Assert.Equal(sampled.Length, exact.Length);

            foreach (GeoPoint3 point in exact)
            {
                Assert.True(sampled.Any(near => near.DistanceTo(point) <= slack),
                    $"{point} is nowhere near any sampled crossing");
            }
        }

        [Fact]
        public void AnArcCrossesAPlaneWhereItsCircleWouldAndOnlyWhereItSweeps()
        {
            GeoArc3 arc = Quarter();

            // x = 50 cuts the circle at y = +-86.602540; the first quadrant holds only the positive one.
            var half = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));
            GeoPoint3[] crossings = Arc3.GetIntersections(arc, half);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(50, Reach(), 0), Loose), crossings[0].ToString());

            Agrees(crossings, SampledCrossings(arc, half), 1.0);

            // y = 0 holds one end of the arc, and the other crossing of that line, (-100, 0, 0), is a
            // half turn away and outside the sweep.
            GeoPoint3[] atEnd = Arc3.GetIntersections(arc, new GeoPlane3(GeoPoint3.Origin, new GeoVector3(0, 1, 0)));

            Assert.Single(atEnd);
            Assert.True(atEnd[0].IsEqualTo(new GeoPoint3(100, 0, 0), Loose), atEnd[0].ToString());

            // A plane the circle cannot reach, though it is not parallel to the arc's own.
            Assert.Empty(Arc3.GetIntersections(arc, new GeoPlane3(new GeoPoint3(500, 0, 0), new GeoVector3(1, 0, 0))));
        }

        [Fact]
        public void APlaneGrazingTheCircleGivesTheOnePointItTouches()
        {
            GeoArc3 arc = Quarter();

            // x = 100 touches the circle at exactly one point, an end of the arc. Rounding puts the
            // discriminant either side of nought here, so this is the case the tolerance has to catch.
            GeoPoint3[] touching = Arc3.GetIntersections(arc, new GeoPlane3(new GeoPoint3(100, 0, 0), new GeoVector3(1, 0, 0)));

            Assert.Single(touching);
            Assert.True(touching[0].IsEqualTo(new GeoPoint3(100, 0, 0), Loose), touching[0].ToString());

            // And a hair beyond it touches nothing.
            Assert.Empty(Arc3.GetIntersections(arc, new GeoPlane3(new GeoPoint3(100.01, 0, 0), new GeoVector3(1, 0, 0))));
        }

        [Fact]
        public void AnArcLyingInThePlaneCrossesItNowhereAndTouchesItEverywhere()
        {
            GeoArc3 arc = Quarter();
            var own = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);

            // The two answer different questions and this is the one case where they part.
            Assert.Empty(Arc3.GetIntersections(arc, own));
            Assert.True(Arc3.CollidesWith(arc, own));

            // A plane parallel to the arc's own but above it shares nothing at all.
            var above = new GeoPlane3(new GeoPoint3(0, 0, 50), GeoVector3.ZAxis);

            Assert.Empty(Arc3.GetIntersections(arc, above));
            Assert.False(Arc3.CollidesWith(arc, above));
        }

        [Fact]
        public void ASegmentIsOnlyCreditedWithWhatItActuallyReaches()
        {
            GeoArc3 arc = Quarter();

            // In the arc's own plane, running clean across it.
            var across = new GeoLine3(new GeoPoint3(50, -200, 0), new GeoPoint3(50, 200, 0));
            GeoPoint3[] crossings = Arc3.GetIntersections(arc, across);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(50, Reach(), 0), Loose));

            // The same line cut short so that neither crossing falls on it.
            Assert.Empty(Arc3.GetIntersections(arc, new GeoLine3(new GeoPoint3(50, -200, 0), new GeoPoint3(50, -100, 0))));

            // Out of the arc's plane: it pierces z = 0 at an end of the arc.
            Assert.Single(Arc3.GetIntersections(arc, new GeoLine3(new GeoPoint3(100, 0, -50), new GeoPoint3(100, 0, 50))));

            // Through the centre, which lies on no circle.
            Assert.Empty(Arc3.GetIntersections(arc, new GeoLine3(new GeoPoint3(0, 0, -50), new GeoPoint3(0, 0, 50))));

            Assert.True(Arc3.CollidesWith(arc, across));
            Assert.False(Arc3.CollidesWith(arc, new GeoLine3(new GeoPoint3(0, 0, -50), new GeoPoint3(0, 0, 50))));
        }

        [Fact]
        public void ARayOnlyReachesWhatLiesAheadOfIt()
        {
            GeoArc3 arc = Quarter();

            var into = new GeoRay3(new GeoPoint3(50, -200, 0), new GeoVector3(0, 1, 0));
            var away = new GeoRay3(new GeoPoint3(50, 200, 0), new GeoVector3(0, 1, 0));

            GeoPoint3[] crossings = Arc3.GetIntersections(arc, into);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(50, Reach(), 0), Loose));

            Assert.Empty(Arc3.GetIntersections(arc, away));
            Assert.True(Arc3.CollidesWith(arc, into));
            Assert.False(Arc3.CollidesWith(arc, away));
        }

        [Fact]
        public void AWholeTurnSweepsWhatAQuarterDoesNot()
        {
            // Equal start and end angles mean a whole turn, and for a whole turn it does not matter where the
            // frame puts angle nought, which is why this one is built that way.
            var whole = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0, 0.0, 0.0);
            GeoArc3 quarter = Quarter();

            var half = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));

            Assert.Single(Arc3.GetIntersections(quarter, half));
            Assert.Equal(2, Arc3.GetIntersections(whole, half).Length);

            double[] ys = Arc3.GetIntersections(whole, half).Select(point => Math.Round(point.Y, 6)).OrderBy(y => y).ToArray();

            Assert.Equal(new[] { -Math.Round(Reach(), 6), Math.Round(Reach(), 6) }, ys);

            Agrees(Arc3.GetIntersections(whole, half), SampledCrossings(whole, half), 1.0);
        }

        [Fact]
        public void AnArcTiltedOutOfTheWorldPlanesIsNoDifferent()
        {
            // Nothing above leans on the arc sitting in z = 0. This is the check of that, and it names no
            // world axis at all: a plane through the centre carrying the middle of the arc must cut it there,
            // and the point half a turn away is outside a quarter sweep.
            var tilted = new GeoArc3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 1), 100.0, 0.0, Math.PI / 2);

            GeoVector3 outward = tilted.Center.GetVectorTo(tilted.MidPoint);
            var through = new GeoPlane3(tilted.Center, tilted.Normal.CrossProduct(outward));

            GeoPoint3[] crossings = Arc3.GetIntersections(tilted, through);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(tilted.MidPoint, Loose), crossings[0].ToString());

            Agrees(crossings, SampledCrossings(tilted, through), 1.0);

            // Every crossing really is on the arc and really is on the plane.
            Assert.All(crossings, point => Assert.True(tilted.IsPointOn(point, Loose)));
            Assert.All(crossings, point => Assert.Equal(0.0, through.SignedDistanceTo(point), 6));

            Assert.True(Arc3.CollidesWith(tilted, tilted.GetPlane()));
        }

        [Fact]
        public void AngleNoughtIsWhereTheFrameSaysItIsAndNotOnTheWorldXAxis()
        {
            // Worth pinning, because assuming otherwise is the trap these tests were first written into.
            // GeoPlane3.GetAxes says a plane has no preferred pair of in-plane axes; the arc measures its
            // angles from whichever pair that hands back.
            var arc = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0, 0.0, Math.PI / 2);

            arc.GetPlane().GetAxes(out GeoVector3 uAxis, out _);

            Assert.True(arc.StartPoint.IsEqualTo(GeoPoint3.Origin.Add(uAxis.Multiply(100.0)), Loose),
                $"angle nought is at {arc.StartPoint}, the frame's first axis is {uAxis}");

            // And for z = 0 that axis is not the x axis, which is the whole of the surprise.
            Assert.False(arc.StartPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Loose));
        }

        [Fact]
        public void EveryPairTakesAToleranceAndTheTryFormAgreesWithTheList()
        {
            Tolerance global = Tolerance.Global;
            GeoArc3 arc = Quarter();

            var plane = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));
            var line = new GeoLine3(new GeoPoint3(50, -200, 0), new GeoPoint3(50, 200, 0));
            var ray = new GeoRay3(new GeoPoint3(50, -200, 0), new GeoVector3(0, 1, 0));

            Assert.Equal(Arc3.GetIntersections(arc, plane).Length, Arc3.GetIntersections(arc, plane, global).Length);
            Assert.Equal(Arc3.GetIntersections(arc, line).Length, Arc3.GetIntersections(arc, line, global).Length);
            Assert.Equal(Arc3.GetIntersections(arc, ray).Length, Arc3.GetIntersections(arc, ray, global).Length);

            Assert.Equal(Arc3.CollidesWith(arc, plane), Arc3.CollidesWith(arc, plane, global));
            Assert.Equal(Arc3.CollidesWith(arc, line), Arc3.CollidesWith(arc, line, global));
            Assert.Equal(Arc3.CollidesWith(arc, ray), Arc3.CollidesWith(arc, ray, global));

            Assert.True(Arc3.TryIntersectWith(arc, plane, out GeoPoint3[] onPlane));
            Assert.True(Arc3.TryIntersectWith(arc, line, out GeoPoint3[] onLine));
            Assert.True(Arc3.TryIntersectWith(arc, ray, out GeoPoint3[] onRay));

            Assert.Single(onPlane);
            Assert.Single(onLine);
            Assert.Single(onRay);

            // Nothing crossed leaves an empty array rather than null.
            Assert.False(Arc3.TryIntersectWith(arc, new GeoPlane3(new GeoPoint3(500, 0, 0), new GeoVector3(1, 0, 0)), out GeoPoint3[] none));
            Assert.Empty(none);
        }
    }
}
