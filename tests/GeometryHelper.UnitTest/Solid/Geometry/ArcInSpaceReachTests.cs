using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc and a circle in space could be asked about a point and nothing else. These are the first
    /// questions they can answer about another shape: where they cross a plane, a segment or a ray, and
    /// whether they touch one — asked from either side.
    /// </summary>
    public class ArcInSpaceReachTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A quarter turn of a hundred radius sweeping the first quadrant of z = 0, built from three points it
        /// passes through so that nothing here depends on where the frame puts angle nought.
        /// </summary>
        private static GeoArc3 Quarter() => GeoArc3.FromThreePoints(
            new GeoPoint3(100, 0, 0),
            new GeoPoint3(100 * Math.Sqrt(0.5), 100 * Math.Sqrt(0.5), 0),
            new GeoPoint3(0, 100, 0));

        private static GeoCircle3 Ring() => new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 100.0);

        private static double Reach() => Math.Sqrt(100 * 100 - 50 * 50);

        [Fact]
        public void AnArcAnswersAboutAPlaneAndThePlaneAnswersTheSame()
        {
            GeoArc3 arc = Quarter();
            var half = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));
            var away = new GeoPlane3(new GeoPoint3(500, 0, 0), new GeoVector3(1, 0, 0));

            Assert.Single(arc.GetIntersections(half));
            Assert.True(arc.GetIntersections(half)[0].IsEqualTo(new GeoPoint3(50, Reach(), 0), Loose));

            // Whichever of the two is in hand.
            Assert.Equal(arc.GetIntersections(half).Length, half.GetIntersections(arc).Length);
            Assert.Equal(arc.CollidesWith(half), half.CollidesWith(arc));
            Assert.True(arc.CollidesWith(half));

            Assert.Empty(arc.GetIntersections(away));
            Assert.False(arc.CollidesWith(away));
            Assert.False(away.CollidesWith(arc));

            Assert.True(arc.TryIntersectWith(half, out GeoPoint3[] found));
            Assert.True(half.TryIntersectWith(arc, out GeoPoint3[] back));
            Assert.Equal(found.Length, back.Length);
            Assert.True(found[0].IsEqualTo(back[0], Loose));
        }

        [Fact]
        public void AnArcAnswersAboutASegmentAndARayBothWaysRound()
        {
            GeoArc3 arc = Quarter();

            var across = new GeoLine3(new GeoPoint3(50, -200, 0), new GeoPoint3(50, 200, 0));
            var beside = new GeoLine3(new GeoPoint3(0, 0, -50), new GeoPoint3(0, 0, 50));
            var into = new GeoRay3(new GeoPoint3(50, -200, 0), new GeoVector3(0, 1, 0));
            var pointingAway = new GeoRay3(new GeoPoint3(50, 200, 0), new GeoVector3(0, 1, 0));

            Assert.Single(arc.GetIntersections(across));
            Assert.Equal(arc.GetIntersections(across).Length, across.GetIntersections(arc).Length);
            Assert.Equal(arc.CollidesWith(across), across.CollidesWith(arc));

            Assert.Empty(arc.GetIntersections(beside));
            Assert.False(beside.CollidesWith(arc));

            Assert.Single(arc.GetIntersections(into));
            Assert.Equal(arc.GetIntersections(into).Length, into.GetIntersections(arc).Length);
            Assert.Equal(arc.CollidesWith(into), into.CollidesWith(arc));

            // A ray only reaches what lies ahead of it.
            Assert.Empty(arc.GetIntersections(pointingAway));
            Assert.False(pointingAway.CollidesWith(arc));
        }

        [Fact]
        public void ACircleIsTheWholeTurnAndReachesBothSides()
        {
            GeoCircle3 ring = Ring();
            GeoArc3 arc = Quarter();
            var half = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));

            // The quarter reaches one of the two crossings; the whole circle reaches both.
            Assert.Single(arc.GetIntersections(half));
            Assert.Equal(2, ring.GetIntersections(half).Length);

            double[] ys = ring.GetIntersections(half).Select(point => Math.Round(point.Y, 6)).OrderBy(y => y).ToArray();

            Assert.Equal(new[] { -Math.Round(Reach(), 6), Math.Round(Reach(), 6) }, ys);

            Assert.Equal(ring.GetIntersections(half).Length, half.GetIntersections(ring).Length);
            Assert.Equal(ring.CollidesWith(half), half.CollidesWith(ring));

            // The rim is what counts, not the disc: a segment through the middle touches nothing.
            var throughTheMiddle = new GeoLine3(new GeoPoint3(-50, 0, 0), new GeoPoint3(50, 0, 0));

            Assert.Empty(ring.GetIntersections(throughTheMiddle));
            Assert.False(ring.CollidesWith(throughTheMiddle));
            Assert.True(Containment3.Contains(ring, GeoPoint3.Origin));
        }

        [Fact]
        public void ACircleAnswersAboutSegmentsAndRaysBothWaysRound()
        {
            GeoCircle3 ring = Ring();

            var across = new GeoLine3(new GeoPoint3(50, -200, 0), new GeoPoint3(50, 200, 0));
            var into = new GeoRay3(new GeoPoint3(50, -200, 0), new GeoVector3(0, 1, 0));

            Assert.Equal(2, ring.GetIntersections(across).Length);
            Assert.Equal(ring.GetIntersections(across).Length, across.GetIntersections(ring).Length);
            Assert.Equal(ring.CollidesWith(across), across.CollidesWith(ring));

            // The ray starts outside and runs through, so it meets both sides.
            Assert.Equal(2, ring.GetIntersections(into).Length);
            Assert.Equal(ring.GetIntersections(into).Length, into.GetIntersections(ring).Length);
        }

        [Fact]
        public void LyingInAPlaneIsTouchingItWithoutCrossingIt()
        {
            GeoArc3 arc = Quarter();
            GeoCircle3 ring = Ring();
            var own = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);

            foreach (bool touching in new[] { arc.CollidesWith(own), own.CollidesWith(arc), ring.CollidesWith(own), own.CollidesWith(ring) })
            {
                Assert.True(touching);
            }

            Assert.Empty(arc.GetIntersections(own));
            Assert.Empty(ring.GetIntersections(own));
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndAnswersTheSameWithTheDefaultOne()
        {
            Tolerance global = Tolerance.Global;
            GeoArc3 arc = Quarter();
            GeoCircle3 ring = Ring();

            var plane = new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0));
            var line = new GeoLine3(new GeoPoint3(50, -200, 0), new GeoPoint3(50, 200, 0));
            var ray = new GeoRay3(new GeoPoint3(50, -200, 0), new GeoVector3(0, 1, 0));

            Assert.Equal(arc.GetIntersections(plane).Length, arc.GetIntersections(plane, global).Length);
            Assert.Equal(arc.GetIntersections(line).Length, arc.GetIntersections(line, global).Length);
            Assert.Equal(arc.GetIntersections(ray).Length, arc.GetIntersections(ray, global).Length);
            Assert.Equal(ring.GetIntersections(plane).Length, ring.GetIntersections(plane, global).Length);
            Assert.Equal(ring.GetIntersections(line).Length, ring.GetIntersections(line, global).Length);
            Assert.Equal(ring.GetIntersections(ray).Length, ring.GetIntersections(ray, global).Length);

            Assert.Equal(arc.CollidesWith(plane), arc.CollidesWith(plane, global));
            Assert.Equal(arc.CollidesWith(line), arc.CollidesWith(line, global));
            Assert.Equal(arc.CollidesWith(ray), arc.CollidesWith(ray, global));
            Assert.Equal(ring.CollidesWith(plane), ring.CollidesWith(plane, global));

            Assert.Equal(plane.GetIntersections(arc).Length, plane.GetIntersections(arc, global).Length);
            Assert.Equal(line.GetIntersections(arc).Length, line.GetIntersections(arc, global).Length);
            Assert.Equal(ray.GetIntersections(arc).Length, ray.GetIntersections(arc, global).Length);
            Assert.Equal(plane.CollidesWith(ring), plane.CollidesWith(ring, global));

            Assert.Equal(
                arc.TryIntersectWith(plane, out GeoPoint3[] a),
                arc.TryIntersectWith(plane, out GeoPoint3[] b, global));
            Assert.Equal(a.Length, b.Length);

            Assert.Equal(
                plane.TryIntersectWith(arc, out GeoPoint3[] c),
                plane.TryIntersectWith(arc, out GeoPoint3[] d, global));
            Assert.Equal(c.Length, d.Length);
        }
    }
}
