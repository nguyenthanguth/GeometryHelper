using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The rest of the asymmetries in the plane. A face could be asked about every shape and no shape could
    /// be asked about a face; a point lagged behind its counterpart in space by four shapes; the edge of a
    /// many-edged shape nearest something could only be asked of the many-edged shape; and a point could not
    /// be asked how deep inside anything it sat.
    /// </summary>
    public class ReachBothWaysTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

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

        private static GeoPolygon2 Square(double x, double y, double size) => new GeoPolygon2(
            new GeoPoint2(x, y), new GeoPoint2(x + size, y), new GeoPoint2(x + size, y + size), new GeoPoint2(x, y + size));

        [Fact]
        public void EveryShapeCanNowBeAskedAboutAFaceAndAgreesWithTheFace()
        {
            GeoFace2 face = Pierced();

            var line = new GeoLine2(new GeoPoint2(-50, 100), new GeoPoint2(250, 100));
            var arc = new GeoArc2(new GeoPoint2(100, 100), 60, 0.0, Math.PI / 2);
            var circle = new GeoCircle2(new GeoPoint2(100, 100), 60);
            var rect = new GeoRectangle2(new GeoPoint2(100, 100), 120, 120);
            var chain = new GeoPolyline2(new GeoPoint2(-50, 100), new GeoPoint2(250, 100));
            var poly = Square(40, 40, 120);
            var loop = new GeoPolygonArc2(Square(40, 40, 120));
            var open = new GeoPolylineArc2(new[] { new GeoPoint2(-50, 100), new GeoPoint2(250, 100) });

            // Whichever of the two is in hand, the answer is the same.
            Assert.Equal(face.CollidesWith(line), line.CollidesWith(face));
            Assert.Equal(face.CollidesWith(arc), arc.CollidesWith(face));
            Assert.Equal(face.CollidesWith(circle), circle.CollidesWith(face));
            Assert.Equal(face.CollidesWith(rect), rect.CollidesWith(face));
            Assert.Equal(face.CollidesWith(chain), chain.CollidesWith(face));
            Assert.Equal(face.CollidesWith(poly), poly.CollidesWith(face));
            Assert.Equal(face.CollidesWith(loop), loop.CollidesWith(face));
            Assert.Equal(face.CollidesWith(open), open.CollidesWith(face));

            // And every one of these does reach the material, so the agreement is not agreement on false.
            Assert.True(line.CollidesWith(face));
            Assert.True(circle.CollidesWith(face));
            Assert.True(poly.CollidesWith(face));

            Assert.Equal(face.GetIntersections(line).Length, line.GetIntersections(face).Length);
            Assert.Equal(face.GetIntersections(circle).Length, circle.GetIntersections(face).Length);
            Assert.Equal(face.GetIntersections(poly).Length, poly.GetIntersections(face).Length);
            Assert.Equal(4, line.GetIntersections(face).Length);
        }

        [Fact]
        public void TheReachToAFaceRunsFromTheShapeAskedAndNotFromTheFace()
        {
            GeoFace2 face = Pierced();
            var away = new GeoLine2(new GeoPoint2(300, 300), new GeoPoint2(400, 400));

            GeoLine2 fromShape = away.GetShortestLineTo(face);
            GeoLine2 fromFace = face.GetShortestLineTo(away);

            // It leaves the segment and lands on the boundary of the face, which is the face's answer reversed.
            Assert.True(fromShape.StartPoint.IsEqualTo(new GeoPoint2(300, 300), Loose), fromShape.StartPoint.ToString());
            Assert.True(fromShape.EndPoint.IsEqualTo(new GeoPoint2(200, 200), Loose), fromShape.EndPoint.ToString());
            Assert.True(fromShape.StartPoint.IsEqualTo(fromFace.EndPoint, Loose));
            Assert.True(fromShape.EndPoint.IsEqualTo(fromFace.StartPoint, Loose));
            Assert.Equal(Math.Sqrt(100 * 100 + 100 * 100), fromShape.Length, 6);

            // The same holds from a point, which had no way of asking at all.
            var corner = new GeoPoint2(300, 300);

            Assert.True(corner.GetShortestLineTo(face).StartPoint.IsEqualTo(corner, Loose));
            Assert.True(corner.GetShortestLineTo(face).EndPoint.IsEqualTo(new GeoPoint2(200, 200), Loose));
        }

        [Fact]
        public void APointInThePlaneNowReachesTheCurvedShapesAndTheFace()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, Math.PI / 2);
            GeoFace2 face = Pierced();
            var loop = new GeoPolygonArc2(Square(300, 0, 100));
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(300, 0), new GeoPoint2(300, 100) });

            var probe = new GeoPoint2(300, 0);

            // Each agrees with the shape asked the other way, which is where the answer came from all along.
            Assert.Equal(arc.DistanceTo(probe), probe.DistanceTo(arc), 9);
            Assert.Equal(face.DistanceTo(probe), probe.DistanceTo(face), 9);
            Assert.Equal(Distance2.DistanceTo(loop, probe), probe.DistanceTo(loop), 9);
            Assert.Equal(Distance2.DistanceTo(chain, probe), probe.DistanceTo(chain), 9);

            // (300, 0) stands two hundred off the near end of the arc, at (100, 0).
            Assert.Equal(200.0, probe.DistanceTo(arc), 6);
            Assert.True(probe.GetClosestPointOnBoundary(arc).IsEqualTo(new GeoPoint2(100, 0), Loose));
            Assert.True(probe.GetClosestPointOnBoundary(face).IsEqualTo(new GeoPoint2(200, 0), Loose));

            // The joining segment leaves the point, not the arc. Handing back the arc's own answer would give
            // the right length pointing the wrong way, which is why both ends are checked.
            GeoLine2 reach = probe.GetShortestLineTo(arc);

            Assert.True(reach.StartPoint.IsEqualTo(probe, Loose), reach.StartPoint.ToString());
            Assert.True(reach.EndPoint.IsEqualTo(new GeoPoint2(100, 0), Loose), reach.EndPoint.ToString());
            Assert.Equal(200.0, reach.Length, 6);
        }

        [Fact]
        public void WhichEdgeOfAManyEdgedShapeIsNearestCanBeAskedFromEitherSide()
        {
            GeoPolygon2 poly = Square(0, 0, 100);
            var rect = new GeoRectangle2(new GeoPoint2(50, 50), 100, 100);
            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100));
            var loop = new GeoPolygonArc2(poly);
            var open = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) });

            var below = new GeoPoint2(50, -50);
            var line = new GeoLine2(new GeoPoint2(0, -50), new GeoPoint2(100, -50));
            var circle = new GeoCircle2(new GeoPoint2(50, -50), 10);
            var arc = new GeoArc2(new GeoPoint2(50, -50), 10, 0.0, Math.PI / 2);

            // The nearest edge of the square to something below it is its bottom edge, whichever side asks.
            Assert.True(below.GetClosestEdge(poly).IsEqualTo(poly.GetClosestEdge(below), Loose));
            Assert.True(line.GetClosestEdge(poly).IsEqualTo(poly.GetClosestEdge(line), Loose));
            Assert.True(circle.GetClosestEdge(poly).IsEqualTo(poly.GetClosestEdge(circle), Loose));
            Assert.True(below.GetClosestEdge(rect).IsEqualTo(rect.GetClosestEdge(below), Loose));
            Assert.True(below.GetClosestEdge(chain).IsEqualTo(chain.GetClosestEdge(below), Loose));

            Assert.True(below.GetClosestEdge(poly).StartPoint.Y < 1E-9);
            Assert.True(below.GetClosestEdge(poly).EndPoint.Y < 1E-9);

            // The curved shapes give back an edge rather than a segment, and answer both ways as well.
            Assert.True(below.GetClosestEdge(loop).IsEqualTo(loop.GetClosestEdge(below), Loose));
            Assert.True(below.GetClosestEdge(open).IsEqualTo(open.GetClosestEdge(below), Loose));
            Assert.True(arc.GetClosestEdge(loop).IsEqualTo(loop.GetClosestEdge(arc), Loose));
            Assert.True(arc.GetClosestEdge(open).IsEqualTo(open.GetClosestEdge(arc), Loose));
        }

        [Fact]
        public void APointKnowsHowDeepInsideAClosedShapeItSits()
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 100);
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 200, 200);
            GeoPolygon2 poly = Square(-100, -100, 200);
            var loop = new GeoPolygonArc2(poly);
            GeoFace2 face = Pierced();

            var middle = new GeoPoint2(0, 0);
            var outside = new GeoPoint2(300, 0);

            Assert.Equal(-100.0, middle.SignedDistanceTo(circle), 6);
            Assert.Equal(200.0, outside.SignedDistanceTo(circle), 6);

            // Each is the shape's own answer, asked from the other side.
            Assert.Equal(circle.SignedDistanceTo(middle), middle.SignedDistanceTo(circle), 9);
            Assert.Equal(rect.SignedDistanceTo(middle), middle.SignedDistanceTo(rect), 9);
            Assert.Equal(poly.SignedDistanceTo(middle), middle.SignedDistanceTo(poly), 9);
            Assert.Equal(loop.SignedDistanceTo(middle), middle.SignedDistanceTo(loop), 9);
            Assert.Equal(face.SignedDistanceTo(middle), middle.SignedDistanceTo(face), 9);
            Assert.Equal(circle.SignedDistanceTo(outside), outside.SignedDistanceTo(circle), 9);
        }

        [Fact]
        public void TryIntersectWithNowRunsBothWaysAndAgreesWithGetIntersections()
        {
            var line = new GeoLine2(new GeoPoint2(-200, 0), new GeoPoint2(200, 0));
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 100);
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, Math.PI);
            var chain = new GeoPolyline2(new GeoPoint2(-200, 0), new GeoPoint2(200, 0));
            var loop = new GeoPolygonArc2(Square(-50, -50, 100));
            var open = new GeoPolylineArc2(new[] { new GeoPoint2(-200, 0), new GeoPoint2(200, 0) });

            // The segment crosses the circle at both ends of a diameter.
            Assert.True(line.TryIntersectWith(circle, out GeoPoint2[] fromLine));
            Assert.True(circle.TryIntersectWith(line, out GeoPoint2[] fromCircle));
            Assert.Equal(2, fromLine.Length);
            Assert.Equal(fromLine.Length, fromCircle.Length);
            Assert.Equal(line.GetIntersections(circle).Length, fromLine.Length);

            foreach (double x in fromLine.Select(point => Math.Abs(point.X)).OrderBy(x => x))
            {
                Assert.Equal(100.0, x, 6);
            }

            // Every other pair agrees with itself reversed, and with the crossings already offered.
            Assert.Equal(line.TryIntersectWith(arc, out GeoPoint2[] a), arc.TryIntersectWith(line, out GeoPoint2[] b));
            Assert.Equal(a.Length, b.Length);
            Assert.Equal(line.GetIntersections(arc).Length, a.Length);

            Assert.Equal(line.TryIntersectWith(chain, out GeoPoint2[] c), chain.TryIntersectWith(line, out GeoPoint2[] d));
            Assert.Equal(c.Length, d.Length);

            Assert.Equal(line.TryIntersectWith(loop, out GeoPoint2[] e), loop.TryIntersectWith(line, out GeoPoint2[] f));
            Assert.Equal(e.Length, f.Length);
            Assert.Equal(line.GetIntersections(loop).Length, e.Length);

            Assert.Equal(line.TryIntersectWith(open, out GeoPoint2[] g), open.TryIntersectWith(line, out GeoPoint2[] h));
            Assert.Equal(g.Length, h.Length);

            // A shape can be asked about its own sort, which it could not be either.
            Assert.True(circle.TryIntersectWith(new GeoCircle2(new GeoPoint2(150, 0), 100), out GeoPoint2[] two));
            Assert.Equal(2, two.Length);

            Assert.True(loop.TryIntersectWith(new GeoPolygonArc2(Square(0, -50, 100)), out GeoPoint2[] crossed));
            Assert.NotEmpty(crossed);

            // Nothing crossed leaves an empty array rather than null, which is what the summaries promise.
            Assert.False(line.TryIntersectWith(new GeoCircle2(new GeoPoint2(0, 5000), 10), out GeoPoint2[] none));
            Assert.Empty(none);
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndAnswersTheSameWithTheDefaultOne()
        {
            Tolerance global = Tolerance.Global;
            GeoFace2 face = Pierced();

            var line = new GeoLine2(new GeoPoint2(-50, 100), new GeoPoint2(250, 100));
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, Math.PI / 2);
            var circle = new GeoCircle2(new GeoPoint2(100, 100), 60);
            var rect = new GeoRectangle2(new GeoPoint2(100, 100), 120, 120);
            var chain = new GeoPolyline2(new GeoPoint2(-50, 100), new GeoPoint2(250, 100));
            GeoPolygon2 poly = Square(40, 40, 120);
            var loop = new GeoPolygonArc2(poly);
            var open = new GeoPolylineArc2(new[] { new GeoPoint2(-50, 100), new GeoPoint2(250, 100) });
            var probe = new GeoPoint2(300, 0);

            Assert.Equal(line.CollidesWith(face), line.CollidesWith(face, global));
            Assert.Equal(arc.CollidesWith(face), arc.CollidesWith(face, global));
            Assert.Equal(circle.CollidesWith(face), circle.CollidesWith(face, global));
            Assert.Equal(rect.CollidesWith(face), rect.CollidesWith(face, global));
            Assert.Equal(chain.CollidesWith(face), chain.CollidesWith(face, global));
            Assert.Equal(poly.CollidesWith(face), poly.CollidesWith(face, global));
            Assert.Equal(loop.CollidesWith(face), loop.CollidesWith(face, global));
            Assert.Equal(open.CollidesWith(face), open.CollidesWith(face, global));

            Assert.Equal(line.GetIntersections(face).Length, line.GetIntersections(face, global).Length);
            Assert.True(line.GetShortestLineTo(face).IsEqualTo(line.GetShortestLineTo(face, global), Loose));
            Assert.True(probe.GetShortestLineTo(face).IsEqualTo(probe.GetShortestLineTo(face, global), Loose));
            Assert.True(probe.GetShortestLineTo(arc).IsEqualTo(probe.GetShortestLineTo(arc, global), Loose));

            Assert.Equal(probe.DistanceTo(arc), probe.DistanceTo(arc, global), 9);
            Assert.Equal(probe.DistanceTo(face), probe.DistanceTo(face, global), 9);
            Assert.Equal(probe.DistanceTo(loop), probe.DistanceTo(loop, global), 9);
            Assert.Equal(probe.SignedDistanceTo(face), probe.SignedDistanceTo(face, global), 9);
            Assert.True(probe.GetClosestPointOnBoundary(arc).IsEqualTo(probe.GetClosestPointOnBoundary(arc, global), Loose));
            Assert.True(probe.GetClosestEdge(loop).IsEqualTo(probe.GetClosestEdge(loop, global), Loose));

            Assert.Equal(
                line.TryIntersectWith(circle, out GeoPoint2[] p),
                line.TryIntersectWith(circle, out GeoPoint2[] q, global));
            Assert.Equal(p.Length, q.Length);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            var probe = new GeoPoint2(300, 0);

            Assert.Throws<ArgumentNullException>(() => line.CollidesWith((GeoFace2)null));
            Assert.Throws<ArgumentNullException>(() => line.GetIntersections((GeoFace2)null));
            Assert.Throws<ArgumentNullException>(() => line.GetShortestLineTo((GeoFace2)null));
            Assert.Throws<ArgumentNullException>(() => probe.DistanceTo((GeoFace2)null));
            Assert.Throws<ArgumentNullException>(() => probe.GetClosestEdge((GeoPolygon2)null));
        }
    }
}
