using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The three types the plane had left behind. An arc could be measured against a point, a segment and
    /// another arc and against nothing else, though Core knew the rest; an edge could be measured against
    /// four shapes and never asked the cheap question of whether it touches them at all; and a face could
    /// answer only about points.
    /// </summary>
    /// <remarks>
    /// The face is the one with a rule of its own, and it is held here: its boundary is the outline
    /// together with the rim of every hole, and a probe reaches its material when it reaches the outline
    /// and no hole has swallowed it whole. A ring drawn around a hole is the case that catches a rule
    /// written carelessly, so it has an assertion to itself.
    /// </remarks>
    public class ArcEdgeAndFaceReachTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A quarter turn of a hundred radius about the origin, from (100, 0) round to (0, 100).
        /// </summary>
        private static GeoArc2 Quarter() => new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, Math.PI / 2);

        /// <summary>
        /// A square two hundred off to the right, well clear of that arc.
        /// </summary>
        private static GeoPolygon2 Clear() => new GeoPolygon2(
            new GeoPoint2(300, -50), new GeoPoint2(400, -50), new GeoPoint2(400, 50), new GeoPoint2(300, 50));

        /// <summary>
        /// Two hundred square with a forty-wide hole in the middle of it.
        /// </summary>
        private static GeoFace2 Pierced() => new GeoFace2(
            new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(0, 200)),
            new[]
            {
                new GeoPolygon2(
                    new GeoPoint2(80, 80), new GeoPoint2(120, 80), new GeoPoint2(120, 120), new GeoPoint2(80, 120))
            });

        [Fact]
        public void AnArcNowMeasuresAgainstEveryShapeItCouldNotReach()
        {
            GeoArc2 arc = Quarter();
            GeoPolygon2 square = Clear();
            var circle = new GeoCircle2(new GeoPoint2(300, 0), 50);
            var rect = new GeoRectangle2(new GeoPoint2(350, 0), 100, 100);
            var chain = new GeoPolyline2(new GeoPoint2(300, -50), new GeoPoint2(300, 50));

            // Everything out there sits two hundred from the nearest point of the arc, which is (100, 0),
            // except the circle, whose rim comes fifty nearer.
            Assert.Equal(200.0, arc.DistanceTo(square), 6);
            Assert.Equal(200.0, arc.DistanceTo(rect), 6);
            Assert.Equal(200.0, arc.DistanceTo(chain), 6);
            Assert.Equal(150.0, arc.DistanceTo(circle), 6);

            // And the joining segment leaves the arc and lands on the other shape, so its length is that
            // same distance and its start is a point of the arc.
            foreach (GeoLine2 join in new[]
                     {
                         arc.GetShortestLineTo(square), arc.GetShortestLineTo(rect),
                         arc.GetShortestLineTo(chain), arc.GetShortestLineTo(circle),
                     })
            {
                Assert.True(join.StartPoint.IsEqualTo(new GeoPoint2(100, 0), Loose), join.StartPoint.ToString());
            }

            Assert.Equal(arc.DistanceTo(square), arc.GetShortestLineTo(square).Length, 6);
            Assert.True(arc.GetShortestLineTo(circle).EndPoint.IsEqualTo(new GeoPoint2(250, 0), Loose));

            // The near shapes are the ones it never touches.
            Assert.False(arc.CollidesWith(square));
            Assert.False(arc.CollidesWith(rect));
            Assert.False(arc.CollidesWith(chain));
            Assert.False(arc.CollidesWith(circle));
            Assert.Empty(arc.GetIntersections(square));
        }

        [Fact]
        public void AnArcKnowsWhatItCrossesAndWhereItCrossesIt()
        {
            GeoArc2 arc = Quarter();

            // A square holding the start of the arc: the arc leaves it through the top edge, and nowhere else.
            var over = new GeoPolygon2(
                new GeoPoint2(50, -50), new GeoPoint2(150, -50), new GeoPoint2(150, 50), new GeoPoint2(50, 50));

            Assert.True(arc.CollidesWith(over));

            GeoPoint2[] crossings = arc.GetIntersections(over);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint2(Math.Sqrt(100 * 100 - 50 * 50), 50), Loose),
                crossings[0].ToString());

            // An arc of another circle that genuinely cuts across it.
            var cutting = new GeoArc2(new GeoPoint2(100, 0), 100, Math.PI / 2, Math.PI);

            Assert.True(arc.CollidesWith(cutting));
            Assert.Single(arc.GetIntersections(cutting));
            Assert.True(arc.GetIntersections(cutting)[0]
                .IsEqualTo(new GeoPoint2(50, Math.Sqrt(100 * 100 - 50 * 50)), Loose));

            // Two arcs of one circle cross nowhere, because circles lying on each other meet along their
            // length and not at points; sharing an end still counts as touching, and a gap still does not.
            var next = new GeoArc2(new GeoPoint2(0, 0), 100, Math.PI / 2, Math.PI);
            var apart = new GeoArc2(new GeoPoint2(0, 0), 100, Math.PI, 3 * Math.PI / 2);

            Assert.True(arc.CollidesWith(next));
            Assert.False(arc.CollidesWith(apart));
            Assert.True(arc.CollidesWith(arc));

            Assert.False(arc.CollidesWith(new GeoArc2(new GeoPoint2(1000, 0), 100, 0.0, Math.PI / 2)));
        }

        [Fact]
        public void AnEdgeCanBeAskedWhetherItTouchesRatherThanHowFarOff()
        {
            var straight = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            var curved = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1.0);

            var across = new GeoLine2(new GeoPoint2(50, -100), new GeoPoint2(50, 100));
            var away = new GeoLine2(new GeoPoint2(300, -100), new GeoPoint2(300, 100));

            Assert.True(straight.CollidesWith(across));
            Assert.True(curved.CollidesWith(across));
            Assert.False(straight.CollidesWith(away));
            Assert.False(curved.CollidesWith(away));

            // The two share both ends, so they touch each other.
            Assert.True(straight.CollidesWith(curved));
            Assert.True(curved.CollidesWith(straight));

            // A closed shape holding the edge whole counts as touching, which is what it counts as for the
            // segment or the arc the edge is read as.
            var around = new GeoPolygon2(
                new GeoPoint2(-500, -500), new GeoPoint2(500, -500), new GeoPoint2(500, 500), new GeoPoint2(-500, 500));

            Assert.True(straight.CollidesWith(around));
            Assert.True(curved.CollidesWith(around));
            Assert.True(curved.CollidesWith(new GeoCircle2(new GeoPoint2(50, 0), 500)));
            Assert.False(curved.CollidesWith(Clear()));

            // And each answers exactly what the shape it is reads as answers.
            Assert.Equal(straight.ToLine().CollidesWith(around), straight.CollidesWith(around));
            Assert.Equal(curved.ToArc().CollidesWith(around), curved.CollidesWith(around));
        }

        [Fact]
        public void AFaceAnswersAboutItsMaterialAndNotJustItsOutline()
        {
            GeoFace2 face = Pierced();

            var inHole = new GeoLine2(new GeoPoint2(90, 90), new GeoPoint2(110, 110));
            var outOfHole = new GeoLine2(new GeoPoint2(90, 90), new GeoPoint2(150, 150));
            var onMaterial = new GeoLine2(new GeoPoint2(10, 10), new GeoPoint2(20, 20));
            var elsewhere = new GeoLine2(new GeoPoint2(300, 300), new GeoPoint2(400, 400));

            // The outline alone says the segment in the hole is there, and it is the hole that says otherwise.
            Assert.True(face.Boundary.CollidesWith(inHole));
            Assert.False(face.CollidesWith(inHole));

            Assert.True(face.CollidesWith(outOfHole));
            Assert.True(face.CollidesWith(onMaterial));
            Assert.False(face.CollidesWith(elsewhere));
        }

        [Fact]
        public void ARingDrawnRoundAHoleStillReachesTheMaterial()
        {
            GeoFace2 face = Pierced();

            // Its centre is inside the hole and it crosses no rim, which is exactly what a segment lying in
            // the hole looks like -- but this one holds the hole rather than being held by it.
            var around = new GeoCircle2(new GeoPoint2(100, 100), 60);
            var within = new GeoCircle2(new GeoPoint2(100, 100), 10);

            Assert.True(face.CollidesWith(around));
            Assert.False(face.CollidesWith(within));

            // The same told apart for a polygon.
            var ring = new GeoPolygon2(
                new GeoPoint2(40, 40), new GeoPoint2(160, 40), new GeoPoint2(160, 160), new GeoPoint2(40, 160));
            var speck = new GeoPolygon2(
                new GeoPoint2(95, 95), new GeoPoint2(105, 95), new GeoPoint2(105, 105), new GeoPoint2(95, 105));

            Assert.True(face.CollidesWith(ring));
            Assert.False(face.CollidesWith(speck));
        }

        [Fact]
        public void AFaceGivesEveryCrossingOfItsOutlineAndOfItsHoles()
        {
            GeoFace2 face = Pierced();
            var right = new GeoLine2(new GeoPoint2(-50, 100), new GeoPoint2(250, 100));

            double[] crossings = face.GetIntersections(right)
                .Select(point => point.X)
                .OrderBy(x => x)
                .ToArray();

            Assert.Equal(4, crossings.Length);
            Assert.Equal(new[] { 0.0, 80.0, 120.0, 200.0 }, crossings.Select(x => Math.Round(x, 6)));

            // Nothing of the outline and nothing of a rim is crossed by a line that stays outside.
            Assert.Empty(face.GetIntersections(new GeoLine2(new GeoPoint2(-50, 300), new GeoPoint2(250, 300))));
        }

        [Fact]
        public void AFaceReachesOutFromWhicheverPartOfItsBoundaryIsNearest()
        {
            GeoFace2 face = Pierced();

            // From the middle of the hole the nearest boundary is that rim, twenty off.
            Assert.Equal(20.0, face.GetShortestLineTo(new GeoPoint2(100, 100)).Length, 6);

            // From the material near the left-hand side it is the outline, ten off, and the segment lands on
            // the point asked about, which is what tells this from GetClosestPointOnBoundary.
            GeoLine2 reach = face.GetShortestLineTo(new GeoPoint2(10, 100));

            Assert.Equal(10.0, reach.Length, 6);
            Assert.True(reach.StartPoint.IsEqualTo(new GeoPoint2(0, 100), Loose));
            Assert.True(reach.EndPoint.IsEqualTo(new GeoPoint2(10, 100), Loose));

            // And to a shape away off the corner it is that corner.
            GeoLine2 far = face.GetShortestLineTo(new GeoLine2(new GeoPoint2(300, 300), new GeoPoint2(400, 400)));

            Assert.True(far.StartPoint.IsEqualTo(new GeoPoint2(200, 200), Loose), far.StartPoint.ToString());
            Assert.Equal(Math.Sqrt(100 * 100 + 100 * 100), far.Length, 6);
        }

        [Fact]
        public void EveryNewMethodTakesAToleranceAndAnswersTheSameWithTheDefaultOne()
        {
            Tolerance global = Tolerance.Global;
            GeoArc2 arc = Quarter();
            GeoPolygon2 square = Clear();
            GeoFace2 face = Pierced();
            var circle = new GeoCircle2(new GeoPoint2(300, 0), 50);
            var rect = new GeoRectangle2(new GeoPoint2(350, 0), 100, 100);
            var chain = new GeoPolyline2(new GeoPoint2(300, -50), new GeoPoint2(300, 50));
            var curved = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1.0);
            var across = new GeoLine2(new GeoPoint2(50, -100), new GeoPoint2(50, 100));

            Assert.Equal(arc.DistanceTo(square), arc.DistanceTo(square, global), 9);
            Assert.Equal(arc.DistanceTo(circle), arc.DistanceTo(circle, global), 9);
            Assert.Equal(arc.DistanceTo(rect), arc.DistanceTo(rect, global), 9);
            Assert.Equal(arc.DistanceTo(chain), arc.DistanceTo(chain, global), 9);
            Assert.Equal(arc.CollidesWith(square), arc.CollidesWith(square, global));
            Assert.Equal(arc.CollidesWith(arc), arc.CollidesWith(arc, global));
            Assert.True(arc.GetShortestLineTo(square).IsEqualTo(arc.GetShortestLineTo(square, global), Loose));
            Assert.True(arc.GetShortestLineTo(circle).IsEqualTo(arc.GetShortestLineTo(circle, global), Loose));
            Assert.Equal(arc.GetIntersections(square).Length, arc.GetIntersections(square, global).Length);
            Assert.Equal(arc.GetIntersections(chain).Length, arc.GetIntersections(chain, global).Length);

            Assert.Equal(curved.CollidesWith(across), curved.CollidesWith(across, global));
            Assert.Equal(curved.CollidesWith(square), curved.CollidesWith(square, global));

            var probe = new GeoLine2(new GeoPoint2(90, 90), new GeoPoint2(150, 150));

            Assert.Equal(face.CollidesWith(probe), face.CollidesWith(probe, global));
            Assert.Equal(face.CollidesWith(circle), face.CollidesWith(circle, global));
            Assert.Equal(face.GetIntersections(probe).Length, face.GetIntersections(probe, global).Length);
            Assert.True(face.GetShortestLineTo(probe).IsEqualTo(face.GetShortestLineTo(probe, global), Loose));
            Assert.True(face.GetShortestLineTo(new GeoPoint2(100, 100))
                .IsEqualTo(face.GetShortestLineTo(new GeoPoint2(100, 100), global), Loose));
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            GeoFace2 face = Pierced();
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 1));

            Assert.Throws<ArgumentNullException>(() => Face2.CollidesWith(null, line));
            Assert.Throws<ArgumentNullException>(() => Face2.GetIntersections(null, line));
            Assert.Throws<ArgumentNullException>(() => Face2.GetShortestLineTo(null, line));
            Assert.Throws<ArgumentNullException>(() => face.CollidesWith((GeoPolygon2)null));
        }
    }
}
