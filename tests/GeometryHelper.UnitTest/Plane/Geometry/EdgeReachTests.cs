using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The last asymmetry in the plane. A <see cref="GeoEdge2"/> could be asked about the other shapes and
    /// none of them could be asked about it, because Core has no pair naming an edge at all: an edge is a
    /// segment or an arc and is always read as whichever it is.
    /// </summary>
    /// <remarks>
    /// That reading is what these hold. Every direction added here is the edge handed on as its own segment
    /// or its own arc, so the test that matters is that each answers exactly what that shape answers — an
    /// edge that disagreed with the shape it stands for would be a second, quieter opinion.
    /// </remarks>
    public class EdgeReachTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A hundred long, lying on the x axis.
        /// </summary>
        private static GeoEdge2 Straight() => new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));

        /// <summary>
        /// The same chord bowed into a half turn, so it reaches fifty off the axis.
        /// </summary>
        private static GeoEdge2 Curved() => new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1.0);

        private static GeoPolygon2 Square(double x, double y, double size) => new GeoPolygon2(
            new GeoPoint2(x, y), new GeoPoint2(x + size, y), new GeoPoint2(x + size, y + size), new GeoPoint2(x, y + size));

        /// <summary>
        /// Two hundred square with a forty-wide hole in the middle of it.
        /// </summary>
        private static GeoFace2 Pierced() => new GeoFace2(
            Square(0, 0, 200),
            new[] { Square(80, 80, 40) });

        [Fact]
        public void AnEdgeAnswersExactlyWhatTheShapeItStandsForAnswers()
        {
            GeoEdge2 straight = Straight();
            GeoEdge2 curved = Curved();

            GeoPolygon2 poly = Square(300, -50, 100);
            var rect = new GeoRectangle2(new GeoPoint2(350, 0), 100, 100);
            var chain = new GeoPolyline2(new GeoPoint2(300, -50), new GeoPoint2(300, 50));
            var loop = new GeoPolygonArc2(poly);
            var open = new GeoPolylineArc2(new[] { new GeoPoint2(300, -50), new GeoPoint2(300, 50) });

            Assert.Equal(straight.ToLine().DistanceTo(poly), straight.DistanceTo(poly), 9);
            Assert.Equal(straight.ToLine().DistanceTo(rect), straight.DistanceTo(rect), 9);
            Assert.Equal(straight.ToLine().DistanceTo(chain), straight.DistanceTo(chain), 9);
            Assert.Equal(straight.ToLine().DistanceTo(loop), straight.DistanceTo(loop), 9);
            Assert.Equal(straight.ToLine().DistanceTo(open), straight.DistanceTo(open), 9);

            Assert.Equal(curved.ToArc().DistanceTo(poly), curved.DistanceTo(poly), 9);
            Assert.Equal(curved.ToArc().DistanceTo(rect), curved.DistanceTo(rect), 9);
            Assert.Equal(curved.ToArc().DistanceTo(chain), curved.DistanceTo(chain), 9);
            Assert.Equal(curved.ToArc().DistanceTo(loop), curved.DistanceTo(loop), 9);
            Assert.Equal(curved.ToArc().DistanceTo(open), curved.DistanceTo(open), 9);

            // Both stop at (100, 0) on the near side, and the square begins at x = 300.
            Assert.Equal(200.0, straight.DistanceTo(poly), 6);
            Assert.Equal(200.0, curved.DistanceTo(poly), 6);

            Assert.True(straight.ToLine().GetShortestLineTo(loop).IsEqualTo(straight.GetShortestLineTo(loop), Loose));
            Assert.True(curved.ToArc().GetShortestLineTo(loop).IsEqualTo(curved.GetShortestLineTo(loop), Loose));
            Assert.True(curved.ToArc().GetClosestEdge(loop).IsEqualTo(curved.GetClosestEdge(loop), Loose));
        }

        [Fact]
        public void EveryShapeCanNowBeAskedAboutAnEdgeAndAgreesWithIt()
        {
            GeoEdge2 straight = Straight();
            GeoEdge2 curved = Curved();

            GeoPolygon2 poly = Square(300, -50, 100);
            var rect = new GeoRectangle2(new GeoPoint2(350, 0), 100, 100);
            var chain = new GeoPolyline2(new GeoPoint2(300, -50), new GeoPoint2(300, 50));
            var loop = new GeoPolygonArc2(poly);
            var open = new GeoPolylineArc2(new[] { new GeoPoint2(300, -50), new GeoPoint2(300, 50) });
            var circle = new GeoCircle2(new GeoPoint2(300, 0), 50);
            var arc = new GeoArc2(new GeoPoint2(300, 0), 50, 0.0, Math.PI / 2);

            foreach (GeoEdge2 edge in new[] { straight, curved })
            {
                Assert.Equal(edge.DistanceTo(poly), poly.DistanceTo(edge), 9);
                Assert.Equal(edge.DistanceTo(rect), rect.DistanceTo(edge), 9);
                Assert.Equal(edge.DistanceTo(chain), chain.DistanceTo(edge), 9);
                Assert.Equal(edge.DistanceTo(loop), loop.DistanceTo(edge), 9);
                Assert.Equal(edge.DistanceTo(open), open.DistanceTo(edge), 9);
                Assert.Equal(edge.DistanceTo(circle), circle.DistanceTo(edge), 9);
                Assert.Equal(edge.DistanceTo(arc), arc.DistanceTo(edge), 9);

                Assert.Equal(edge.CollidesWith(poly), poly.CollidesWith(edge));
                Assert.Equal(edge.CollidesWith(loop), loop.CollidesWith(edge));
                Assert.Equal(edge.CollidesWith(circle), circle.CollidesWith(edge));

                Assert.Equal(edge.GetIntersections(poly).Length, poly.GetIntersections(edge).Length);
                Assert.Equal(edge.GetIntersections(loop).Length, loop.GetIntersections(edge).Length);
                Assert.Equal(edge.GetIntersections(circle).Length, circle.GetIntersections(edge).Length);
            }

            // A point had no way of asking either.
            var probe = new GeoPoint2(300, 0);

            Assert.Equal(200.0, probe.DistanceTo(straight), 6);
            Assert.True(probe.GetClosestPointOnBoundary(straight).IsEqualTo(new GeoPoint2(100, 0), Loose));
            Assert.Equal(straight.DistanceTo(probe), probe.DistanceTo(straight), 9);
            Assert.Equal(curved.DistanceTo(probe), probe.DistanceTo(curved), 9);
        }

        [Fact]
        public void TheReachToAnEdgeRunsFromTheShapeAskedAndNotFromTheEdge()
        {
            GeoEdge2 straight = Straight();
            var loop = new GeoPolygonArc2(Square(300, -50, 100));

            GeoLine2 fromLoop = loop.GetShortestLineTo(straight);
            GeoLine2 fromEdge = straight.GetShortestLineTo(loop);

            Assert.True(fromLoop.StartPoint.IsEqualTo(new GeoPoint2(300, 0), Loose), fromLoop.StartPoint.ToString());
            Assert.True(fromLoop.EndPoint.IsEqualTo(new GeoPoint2(100, 0), Loose), fromLoop.EndPoint.ToString());

            // The two are each other reversed, which is what tells a wired direction from a copied one.
            Assert.True(fromLoop.StartPoint.IsEqualTo(fromEdge.EndPoint, Loose));
            Assert.True(fromLoop.EndPoint.IsEqualTo(fromEdge.StartPoint, Loose));
            Assert.Equal(200.0, fromLoop.Length, 6);
        }

        [Fact]
        public void TwoStraightPiecesMeetAtOnePointAndThatIsStillAListOfThem()
        {
            GeoEdge2 straight = Straight();
            GeoEdge2 curved = Curved();

            var across = new GeoLine2(new GeoPoint2(50, -100), new GeoPoint2(50, 100));

            // This is the one direction that is not simply the edge handed on: a segment against a segment
            // meets at a single point, and it is wrapped so that both readings give a list.
            GeoPoint2[] one = across.GetIntersections(straight);

            Assert.Single(one);
            Assert.True(one[0].IsEqualTo(new GeoPoint2(50, 0), Loose), one[0].ToString());

            // The edge answers the same asked the way round that already worked.
            Assert.Equal(straight.GetIntersections(across).Length, one.Length);
            Assert.True(straight.GetIntersections(across)[0].IsEqualTo(one[0], Loose));

            // A curved edge is a half turn of radius fifty about (50, 0), so the same line meets its top.
            GeoPoint2[] curvedHit = across.GetIntersections(curved);

            Assert.Single(curvedHit);
            Assert.Equal(50.0, curvedHit[0].X, 6);
            Assert.Equal(50.0, Math.Abs(curvedHit[0].Y), 6);
            Assert.Equal(curved.GetIntersections(across).Length, curvedHit.Length);

            // And a line that misses gives an empty list rather than null.
            var away = new GeoLine2(new GeoPoint2(500, -100), new GeoPoint2(500, 100));

            Assert.Empty(away.GetIntersections(straight));
            Assert.Empty(away.GetIntersections(curved));
        }

        [Fact]
        public void AFaceReadsAnEdgeAsMaterialOrAsNothingTheWayItReadsAnythingElse()
        {
            GeoFace2 face = Pierced();

            var onMaterial = new GeoEdge2(new GeoPoint2(10, 10), new GeoPoint2(50, 10));
            var inHole = new GeoEdge2(new GeoPoint2(90, 100), new GeoPoint2(110, 100));
            var elsewhere = new GeoEdge2(new GeoPoint2(500, 500), new GeoPoint2(600, 500));

            Assert.True(face.CollidesWith(onMaterial));
            Assert.True(onMaterial.CollidesWith(face));

            // Lying wholly in the hole reaches no material, exactly as a segment there does not.
            Assert.False(face.CollidesWith(inHole));
            Assert.False(inHole.CollidesWith(face));
            Assert.Equal(face.CollidesWith(inHole.ToLine()), face.CollidesWith(inHole));

            Assert.False(face.CollidesWith(elsewhere));

            // An edge crossing the outline crosses it once, whichever side asks.
            var crossing = new GeoEdge2(new GeoPoint2(-50, 100), new GeoPoint2(50, 100));

            Assert.Single(face.GetIntersections(crossing));
            Assert.Equal(face.GetIntersections(crossing).Length, crossing.GetIntersections(face).Length);

            Assert.True(face.GetShortestLineTo(elsewhere).EndPoint
                .IsEqualTo(elsewhere.GetShortestLineTo(face).StartPoint, Loose));
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceWhereTheShapeUnderneathDoesAndAnswersTheSame()
        {
            Tolerance global = Tolerance.Global;
            GeoEdge2 curved = Curved();
            GeoFace2 face = Pierced();

            GeoPolygon2 poly = Square(300, -50, 100);
            var loop = new GeoPolygonArc2(poly);
            var open = new GeoPolylineArc2(new[] { new GeoPoint2(300, -50), new GeoPoint2(300, 50) });
            var circle = new GeoCircle2(new GeoPoint2(300, 0), 50);
            var across = new GeoLine2(new GeoPoint2(50, -100), new GeoPoint2(50, 100));

            Assert.Equal(curved.DistanceTo(loop), curved.DistanceTo(loop, global), 9);
            Assert.Equal(curved.CollidesWith(face), curved.CollidesWith(face, global));
            Assert.Equal(curved.GetIntersections(poly).Length, curved.GetIntersections(poly, global).Length);
            Assert.True(curved.GetShortestLineTo(poly).IsEqualTo(curved.GetShortestLineTo(poly, global), Loose));
            Assert.True(curved.GetClosestEdge(loop).IsEqualTo(curved.GetClosestEdge(loop, global), Loose));

            Assert.Equal(loop.DistanceTo(curved), loop.DistanceTo(curved, global), 9);
            Assert.Equal(poly.CollidesWith(curved), poly.CollidesWith(curved, global));
            Assert.Equal(face.GetIntersections(curved).Length, face.GetIntersections(curved, global).Length);
            Assert.True(open.GetShortestLineTo(curved).IsEqualTo(open.GetShortestLineTo(curved, global), Loose));
            Assert.Equal(across.GetIntersections(curved).Length, across.GetIntersections(curved, global).Length);

            Assert.Equal(
                circle.TryIntersectWith(curved, out GeoPoint2[] a),
                circle.TryIntersectWith(curved, out GeoPoint2[] b, global));
            Assert.Equal(a.Length, b.Length);

            Assert.Equal(
                curved.TryIntersectWith(circle, out GeoPoint2[] c),
                circle.TryIntersectWith(curved, out GeoPoint2[] d));
            Assert.Equal(c.Length, d.Length);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            GeoEdge2 straight = Straight();

            Assert.Throws<ArgumentNullException>(() => straight.DistanceTo((GeoPolygon2)null));
            Assert.Throws<ArgumentNullException>(() => straight.CollidesWith((GeoFace2)null));
            Assert.Throws<ArgumentNullException>(() => straight.GetIntersections((GeoPolygon2)null));
            Assert.Throws<ArgumentNullException>(() => straight.GetClosestEdge((GeoPolygonArc2)null));
        }
    }
}
