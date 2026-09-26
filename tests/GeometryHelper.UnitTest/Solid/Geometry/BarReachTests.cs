using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A <see cref="GeoPolylineArc3"/> is what a reinforcing bar is: straight runs with a tangent arc at every
    /// bend. Until now it could be measured against a point and nothing else. These are the questions a bar is
    /// actually asked on site — where it crosses a pour break, whether it hits an embed, where it leaves the
    /// concrete — and the point of them is that they are answered <b>without straightening the bends</b>.
    /// </summary>
    public class BarReachTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A bar in the plane z = 0: along the x axis from the origin to (400, 0, 0), then up to (400, 200, 0),
        /// with a fifty radius at the one corner.
        /// </summary>
        private static GeoPolylineArc3 Bar() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0)).Fillet(50.0);

        /// <summary>
        /// A slab of concrete reaching out to x = 300, with the start of the bar well inside it, so the bar
        /// leaves it at exactly one place.
        /// </summary>
        /// <remarks>
        /// It starts at x = -100 rather than at the origin on purpose: a slab face through the very start of
        /// the bar would be a second crossing, which is right but makes a poor example.
        /// </remarks>
        private static GeoSolid3 Slab() =>
            new GeoAabb3(new GeoPoint3(-100, -100, -50), new GeoPoint3(300, 300, 50)).ToObb().ToSolid();

        [Fact]
        public void ABarKnowsWhereItCrossesAPourBreak()
        {
            GeoPolylineArc3 bar = Bar();

            // A vertical break at x = 200, square to the straight run.
            var pourBreak = new GeoPlane3(new GeoPoint3(200, 0, 0), new GeoVector3(1, 0, 0));

            GeoPoint3[] crossings = bar.GetIntersections(pourBreak);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(200, 0, 0), Loose), crossings[0].ToString());
            Assert.True(bar.CollidesWith(pourBreak));

            // A break beyond the end of the bar crosses nothing.
            Assert.Empty(bar.GetIntersections(new GeoPlane3(new GeoPoint3(1000, 0, 0), new GeoVector3(1, 0, 0))));

            // And one through the bend crosses it on the arc, not on the corner the bar never reaches.
            var throughBend = new GeoPlane3(new GeoPoint3(380, 0, 0), new GeoVector3(1, 0, 0));
            GeoPoint3[] onBend = bar.GetIntersections(throughBend);

            Assert.Single(onBend);

            // The bar turns before the corner, so the crossing is short of y = 0 by nothing and short of the
            // corner in x: it is on the fillet, whose centre is at (350, 50, 0) with radius 50.
            Assert.Equal(50.0, new GeoPoint3(350, 50, 0).DistanceTo(onBend[0]), 6);
        }

        [Fact]
        public void ABarKnowsWhereItLeavesTheConcrete()
        {
            GeoPolylineArc3 bar = Bar();
            GeoSolid3 slab = Slab();

            GeoPoint3[] crossings = bar.GetIntersections(slab);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(300, 0, 0), Loose), crossings[0].ToString());
            Assert.True(bar.CollidesWith(slab));

            // The face of the slab it leaves through answers the same.
            Assert.Equal(bar.GetIntersections(slab).Length, slab.GetIntersections(bar).Length);
            Assert.Equal(bar.CollidesWith(slab), slab.CollidesWith(bar));

            // A bar well clear of the slab touches none of it.
            GeoPolylineArc3 elsewhere = bar.Translate(new GeoVector3(0, 0, 5000));

            Assert.Empty(elsewhere.GetIntersections(slab));
            Assert.False(elsewhere.CollidesWith(slab));
        }

        [Fact]
        public void ABarKnowsWhetherItHitsAnEmbed()
        {
            GeoPolylineArc3 bar = Bar();

            // A plate set across the straight run.
            GeoSolid3 embed = new GeoAabb3(new GeoPoint3(150, -20, -20), new GeoPoint3(170, 20, 20)).ToObb().ToSolid();

            Assert.True(bar.CollidesWith(embed));
            Assert.NotEmpty(bar.GetIntersections(embed));

            // One set beside the bar, clear of it.
            GeoSolid3 clear = new GeoAabb3(new GeoPoint3(150, 500, -20), new GeoPoint3(170, 540, 20)).ToObb().ToSolid();

            Assert.False(bar.CollidesWith(clear));
            Assert.Empty(bar.GetIntersections(clear));

            // And one sitting in the bend, which the chord of the bend would miss and the arc does not.
            GeoSolid3 atBend = new GeoAabb3(new GeoPoint3(385, 10, -20), new GeoPoint3(405, 30, 20)).ToObb().ToSolid();

            Assert.True(bar.CollidesWith(atBend));
        }

        [Fact]
        public void TheBendIsHonouredRatherThanCutOff()
        {
            GeoPolylineArc3 bar = Bar();

            // The bar has an arc in it, and it is shorter than the polyline it was filleted from.
            Assert.Contains(true, bar.GetEdges().Select(edge => edge.IsArc));

            GeoPolyline3 setOut = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0));

            Assert.True(bar.Length < setOut.Length);

            // The corner of the set-out is a place the bar never reaches, because it turns before it gets
            // there. That is the whole reason a bar must not be read as its set-out.
            var corner = new GeoPoint3(400, 0, 0);

            Assert.True(Containment3.IsPointOn(setOut, corner, Loose));
            Assert.False(bar.IsPointOn(corner, Loose));
            Assert.True(bar.DistanceTo(corner) > 1.0);
        }

        [Fact]
        public void ACornerOfTheBarIsNamedOnceThoughTwoEdgesMeetThere()
        {
            GeoPolylineArc3 bar = Bar();

            // A plane through the point where the straight run meets the fillet: both edges hold it.
            GeoPoint3 seam = bar.GetEdgeAt(0).EndPoint;
            var through = new GeoPlane3(seam, new GeoVector3(1, 0, 0));

            Assert.Single(bar.GetIntersections(through), point => point.IsEqualTo(seam, Loose));
        }

        [Fact]
        public void AClosedLoopAnswersTheSameWay()
        {
            // A stirrup: a square loop in z = 0 with its corners rounded.
            GeoPolygonArc3 stirrup = new GeoPolygonArc3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0),
                new GeoPoint3(200, 200, 0), new GeoPoint3(0, 200, 0))).Fillet(30.0);

            var across = new GeoPlane3(new GeoPoint3(100, 0, 0), new GeoVector3(1, 0, 0));

            // A plane through the middle cuts the loop twice, once on each side.
            Assert.Equal(2, stirrup.GetIntersections(across).Length);
            Assert.True(stirrup.CollidesWith(across));
            Assert.Equal(stirrup.GetIntersections(across).Length, across.GetIntersections(stirrup).Length);

            GeoSolid3 slab = new GeoAabb3(new GeoPoint3(-50, -50, -50), new GeoPoint3(250, 250, 50)).ToObb().ToSolid();

            Assert.True(stirrup.CollidesWith(slab));
            Assert.Empty(stirrup.GetIntersections(slab));
            Assert.Equal(stirrup.CollidesWith(slab), slab.CollidesWith(stirrup));
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoPolylineArc3 bar = Bar();
            GeoSolid3 slab = Slab();
            var plane = new GeoPlane3(new GeoPoint3(200, 0, 0), new GeoVector3(1, 0, 0));
            GeoAabb3 crate = new GeoAabb3(new GeoPoint3(150, -20, -20), new GeoPoint3(170, 20, 20));

            Assert.Equal(bar.GetIntersections(plane).Length, bar.GetIntersections(plane, global).Length);
            Assert.Equal(bar.GetIntersections(slab).Length, bar.GetIntersections(slab, global).Length);
            Assert.Equal(bar.GetIntersections(crate).Length, bar.GetIntersections(crate, global).Length);
            Assert.Equal(bar.CollidesWith(slab), bar.CollidesWith(slab, global));
            Assert.Equal(slab.CollidesWith(bar), slab.CollidesWith(bar, global));
            Assert.Equal(plane.GetIntersections(bar).Length, plane.GetIntersections(bar, global).Length);

            Assert.Throws<ArgumentNullException>(() => ArcChain3.GetIntersections((GeoPolylineArc3)null, plane));
            Assert.Throws<ArgumentNullException>(() => ArcChain3.CollidesWith((GeoPolylineArc3)null, plane));
            Assert.Throws<ArgumentNullException>(() => bar.GetIntersections((GeoSolid3)null));
        }
    }
}
