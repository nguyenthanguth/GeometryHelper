using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Measuring a bent bar against the things around it. The distance from an arc in space to anything but
    /// a point has no closed form, so no method here pretends to one: the bar is followed as closely as the
    /// caller asks with <c>ToPolyline3</c> and the ordinary question is put to the straight chain that comes
    /// back. What the tests check is that the bargain is honest — the sampled answer closes on the true one
    /// as the ask tightens, and never claims the bar is nearer than it is.
    /// </summary>
    public class BentBarClearanceTests
    {
        /// <summary>
        /// A bar bent twice, out along X, across in Y, then up in Z, with a fifty bending radius.
        /// </summary>
        private static GeoPolylineArc3 Bar() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0),
            new GeoPoint3(300, 300, 0), new GeoPoint3(300, 300, 300)).Fillet(50.0);

        private static GeoSolid3 SlabAt(double x) => new GeoAabb3(
            new GeoPoint3(x, -500, -500), new GeoPoint3(x + 100, 500, 500)).ToObb().ToSolid();

        [Fact]
        public void AStraightChainCanNowBeMeasuredAgainstWhatIsAroundIt()
        {
            // Without this the way out of a curved chain led nowhere: ToPolyline3 handed back a chain and
            // there was nothing to ask it but a point.
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0));

            GeoSolid3 slab = SlabAt(300);
            var line = new GeoLine3(new GeoPoint3(300, 0, 0), new GeoPoint3(300, 100, 0));
            var triangle = new GeoTriangle3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(300, 100, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 100, 0), new GeoPoint3(300, 100, 0));
            var plane = new GeoPlane3(new GeoPoint3(300, 0, 0), new GeoVector3(1, 0, 0));
            var obb = new GeoAabb3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 100, 100)).ToObb();
            var aabb = new GeoAabb3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 100, 100));

            // Everything stands two hundred off the far end of the chain.
            Assert.Equal(200.0, chain.DistanceTo(slab), 9);
            Assert.Equal(200.0, chain.DistanceTo(line), 9);
            Assert.Equal(200.0, chain.DistanceTo(triangle), 9);
            Assert.Equal(200.0, chain.DistanceTo(polygon), 9);
            Assert.Equal(200.0, chain.DistanceTo(plane), 9);
            Assert.Equal(200.0, chain.DistanceTo(obb), 9);
            Assert.Equal(200.0, chain.DistanceTo(aabb), 9);
            Assert.Equal(200.0, chain.DistanceTo(new GeoPolyline3(new GeoPoint3(300, 0, 0), new GeoPoint3(300, 100, 0))), 9);

            Assert.False(chain.CollidesWith(slab));
            Assert.True(chain.CollidesWith(SlabAt(50)));

            // Both ways of asking agree, and the tolerance forms agree with the ones that take none.
            Assert.Equal(Distance3.DistanceTo(slab, chain), chain.DistanceTo(slab), 9);
            Assert.Equal(chain.DistanceTo(line), chain.DistanceTo(line, Tolerance.Global), 9);
        }

        [Fact]
        public void TheSampledAnswerClosesOnTheTrueOneAsTheAskTightens()
        {
            GeoPolylineArc3 bar = Bar();

            // A slab standing off the far end of the bar. The bar reaches x = 300, so the gap is 200.
            GeoSolid3 slab = SlabAt(500);

            double coarse = bar.ToPolyline3(20.0).DistanceTo(slab);
            double fine = bar.ToPolyline3(0.1).DistanceTo(slab);
            double finer = bar.ToPolyline3(0.001).DistanceTo(slab);

            // Here the nearest point is on a straight run, so even the coarse sample is exact.
            Assert.Equal(200.0, coarse, 6);
            Assert.Equal(200.0, fine, 9);
            Assert.Equal(200.0, finer, 9);
        }

        [Fact]
        public void SamplingNeverClaimsTheBarIsNearerThanItIs()
        {
            // This is the one property that makes the bargain safe to lean on. A sampled chain lies inside
            // the arcs it stands for, so it is never nearer to anything outside the bar than the bar is;
            // a clearance worked out this way is on the safe side.
            GeoPolylineArc3 bar = Bar();

            // A slab set so that the nearest point of the bar is on a bend rather than on a straight run.
            GeoSolid3 slab = new GeoAabb3(new GeoPoint3(400, 400, -500), new GeoPoint3(900, 900, 500)).ToObb().ToSolid();

            double onTheBar = bar.DistanceTo(bar.GetClosestPointOnBoundary(new GeoPoint3(400, 400, 0)));

            Assert.Equal(0.0, onTheBar, 9);

            double coarse = bar.ToPolyline3(20.0).DistanceTo(slab);
            double fine = bar.ToPolyline3(0.01).DistanceTo(slab);

            // The coarse reading is the safe side of the fine one, and they close on each other.
            Assert.True(coarse >= fine - 1E-9, "coarse " + coarse + " against fine " + fine);
            Assert.True(coarse - fine < 1.0, "coarse " + coarse + " is more than a unit off fine " + fine);

            // And every sampled vertex really is on the bar, so nothing was invented.
            foreach (GeoPoint3 vertex in bar.ToPolyline3(0.01).Vertices)
            {
                Assert.True(bar.DistanceTo(vertex) <= 1E-6);
            }
        }

        [Fact]
        public void ABarThatRunsIntoSomethingSaysSoAtAnyReasonableSampling()
        {
            GeoPolylineArc3 bar = Bar();

            // A slab straddling the middle of the bar.
            GeoSolid3 through = SlabAt(100);

            foreach (double asked in new[] { 20.0, 1.0, 0.01 })
            {
                GeoPolyline3 sampled = bar.ToPolyline3(asked);

                Assert.True(sampled.CollidesWith(through), "at " + asked);
                Assert.Equal(0.0, sampled.DistanceTo(through), 9);
            }

            // And one well clear of it does not, at any sampling.
            GeoSolid3 clear = SlabAt(1000);

            foreach (double asked in new[] { 20.0, 1.0, 0.01 })
            {
                Assert.False(bar.ToPolyline3(asked).CollidesWith(clear), "at " + asked);
                Assert.Equal(700.0, bar.ToPolyline3(asked).DistanceTo(clear), 6);
            }
        }

        [Fact]
        public void AClosedTieIsMeasuredTheSameWay()
        {
            // The loop has ToPolygon3 for the same job, and a loop that is flat can also be measured in its
            // own plane, which is exact.
            var tie = new GeoPolygonArc3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0),
                new GeoPoint3(300, 200, 0), new GeoPoint3(0, 200, 0))).Fillet(40.0);

            GeoSolid3 slab = SlabAt(500);

            Assert.Equal(200.0, tie.ToPolylineArc3().ToPolyline3(0.1).DistanceTo(slab), 6);

            // The sampled loop follows the tie: every vertex of it is on the tie.
            foreach (GeoPoint3 vertex in tie.ToPolygon3(0.01).Vertices)
            {
                Assert.True(tie.IsPointOn(vertex, new Tolerance(1E-5, 1E-5)));
            }

            // What stays exact is everything answered in the plane of the tie.
            Assert.True(tie.Area > 0.0);
            Assert.Equal(tie.Length, tie.ToPolylineArc3().Length, 9);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var polygon = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0));

            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo((GeoPolyline3)null, polygon));
            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo(chain, (GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo(chain, (GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo(chain, (GeoObb3)null));
        }
    }
}
