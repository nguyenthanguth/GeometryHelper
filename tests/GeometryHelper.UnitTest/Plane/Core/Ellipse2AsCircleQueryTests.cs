using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An ellipse whose radii are equal is a circle, and every query the circle has gives the circle's answer to 1E-12 of
    /// the radius, whichever way its major axis points (decision 5). The circle here has radius 100 about (40, -25); the
    /// ellipses are <see cref="GeoEllipse2.FromCircle"/> of it, its axis along X, and the same circle with its axis turned
    /// 30°. The shapes asked about are placed in the drawing, not in either frame, and none of them sits within the point
    /// tolerance of touching, where the circle's tangent band and the ellipse's touching would be asked different
    /// questions. Against a straight shape or a chain the crossings come in the circle's order (amendment A8), and so does
    /// the first crossing a shortest line stands on; against a circle or an arc the ellipse sorts by t where the circle
    /// does not, so there the shortest lines are held to their length. Every shape apart is
    /// placed so that one edge is plainly nearest, its nearest point inside the edge rather than at a corner two edges
    /// share, where which of them is named would be decided by rounding.
    /// </summary>
    public class Ellipse2AsCircleQueryTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private const double Within = 1E-10;

        private static readonly GeoCircle2 Circle = new GeoCircle2(new GeoPoint2(40, -25), 100);

        private static GeoEllipse2[] Ellipses() => new[]
        {
            GeoEllipse2.FromCircle(Circle),
            new GeoEllipse2(Circle.Center, new GeoVector2(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)), 100, 100),
        };

        // A point at an offset from the centre of the circle.
        private static GeoPoint2 At(double x, double y) => new GeoPoint2(Circle.Center.X + x, Circle.Center.Y + y);

        private static GeoPoint2[] AtAll(params double[] xy) => Enumerable.Range(0, xy.Length / 2).Select(i => At(xy[2 * i], xy[2 * i + 1])).ToArray();

        private static GeoLine2 Line(double x0, double y0, double x1, double y1) => new GeoLine2(At(x0, y0), At(x1, y1));

        private static GeoLine2[] Lines() => new[]
        {
            Line(-200, 150, 220, 130), Line(-200, 20, 200, 60), Line(-30, -20, 50, 10),
        };

        private static GeoPolyline2 Polyline() => new GeoPolyline2(AtAll(-250, 160, -50, 120, 150, 200, 260, 60));

        private static GeoPolygon2[] Polygons() => new[]
        {
            new GeoPolygon2(AtAll(150, -20, 260, 40, 140, 120)),
            new GeoPolygon2(AtAll(-30, -20, 40, -20, 0, 50)),
            new GeoPolygon2(AtAll(-200, -210, 220, -200, 200, 230, -190, 200)),
            new GeoPolygon2(AtAll(50, -50, 250, -50, 250, 50, 50, 50)),
        };

        private static GeoRectangle2 Rectangle() => new GeoRectangle2(At(0, 180), 120, 60, 0.2);

        private static GeoTriangle2 Triangle()
        {
            GeoPoint2[] c = AtAll(-180, 40, -140, -90, -260, -20);
            return new GeoTriangle2(c[0], c[1], c[2]);
        }

        private static GeoFace2[] Faces()
        {
            var outline = new GeoPolygon2(AtAll(-2000, -2000, 2000, -2000, 2000, 2000, -2000, 2000));
            return new[]
            {
                new GeoFace2(outline, new[] { new GeoPolygon2(AtAll(-150, -150, 150, -150, 150, 150, -150, 150)) }),
                new GeoFace2(outline, new[] { new GeoPolygon2(AtAll(60, -20, 160, -20, 160, 20, 60, 20)) }),
            };
        }

        private static GeoCircle2[] Circles() => new[]
        {
            new GeoCircle2(At(0, 250), 50), new GeoCircle2(At(90, 0), 50), new GeoCircle2(At(20, 10), 30), new GeoCircle2(At(10, 0), 300),
        };

        private static GeoArc2[] Arcs() => new[]
        {
            new GeoArc2(At(0, 250), 50, Math.PI, 2 * Math.PI), new GeoArc2(At(90, 0), 50, 0.5, 3.5), new GeoArc2(At(10, 0), 40, 0.3, 2.0),
        };

        private static GeoPolylineArc2 Chain() => new GeoPolylineArc2(AtAll(-250, 160, -60, 180, 60, 180, 200, 220), new[] { 0.0, 1.0, 0.4, 0.0 });

        private static GeoPolygonArc2[] Loops() => new[]
        {
            new GeoPolygonArc2(AtAll(-200, -200, 220, -200, 220, 200, -190, 200), new[] { 0.2, 0.0, 0.2, 0.0 }),
            new GeoPolygonArc2(AtAll(150, -40, 250, -40, 250, 40, 150, 40), new[] { 0.0, 0.5, 0.0, 0.3 }),
        };

        private static GeoEdge2[] Edges()
        {
            GeoLine2 line = Lines()[0];
            return new[] { new GeoEdge2(line.StartPoint, line.EndPoint), new GeoEdge2(Arcs()[0]), new GeoEdge2(Arcs()[1]) };
        }

        private static void AssertSamePoints(GeoPoint2[] expected, GeoPoint2[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            foreach (GeoPoint2 point in expected)
            {
                Assert.Contains(actual, p => p.DistanceTo(point) <= Within);
            }
        }

        private static void AssertSameLine(GeoLine2 expected, GeoLine2 actual, bool ends)
        {
            Assert.InRange(Math.Abs(expected.Length - actual.Length), 0.0, Within);
            if (ends)
            {
                Assert.InRange(expected.StartPoint.DistanceTo(actual.StartPoint), 0.0, Within);
                Assert.InRange(expected.EndPoint.DistanceTo(actual.EndPoint), 0.0, Within);
            }
        }

        private static void AssertClose(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0.0, Within);

        [Fact]
        public void PointsAreMeasuredAsTheCircleMeasuresThem()
        {
            foreach (GeoEllipse2 ellipse in Ellipses())
            {
                foreach (GeoPoint2 point in new[] { At(180, 90), At(30, -40), Circle.Center })
                {
                    AssertClose(Circle.DistanceTo(point), ellipse.DistanceTo(point, Strict));
                    AssertClose(Circle.SignedDistanceTo(point, Strict), ellipse.SignedDistanceTo(point, Strict));
                }
            }
        }

        [Fact]
        public void DistancesAreTheCircles()
        {
            foreach (GeoEllipse2 e in Ellipses())
            {
                foreach (GeoLine2 line in Lines()) AssertClose(Circle.DistanceTo(line), e.DistanceTo(line, Strict));
                AssertClose(Circle.DistanceTo(Polyline()), e.DistanceTo(Polyline(), Strict));
                foreach (GeoPolygon2 polygon in Polygons()) AssertClose(Circle.DistanceTo(polygon), e.DistanceTo(polygon, Strict));
                AssertClose(Circle.DistanceTo(Rectangle()), e.DistanceTo(Rectangle(), Strict));
                AssertClose(Circle.DistanceTo(Triangle()), e.DistanceTo(Triangle(), Strict));
                foreach (GeoEdge2 edge in Edges()) AssertClose(Circle.DistanceTo(edge), e.DistanceTo(edge, Strict));
                foreach (GeoCircle2 circle in Circles()) AssertClose(Circle.DistanceTo(circle), e.DistanceTo(circle, Strict));
                foreach (GeoArc2 arc in Arcs()) AssertClose(Circle.DistanceTo(arc, Strict), e.DistanceTo(arc, Strict));
                AssertClose(Circle.DistanceTo(Chain(), Strict), e.DistanceTo(Chain(), Strict));
                foreach (GeoPolygonArc2 loop in Loops()) AssertClose(Circle.DistanceTo(loop, Strict), e.DistanceTo(loop, Strict));
            }
        }

        [Fact]
        public void TouchingIsTheCircles()
        {
            foreach (GeoEllipse2 e in Ellipses())
            {
                foreach (GeoLine2 line in Lines()) Assert.Equal(Circle.CollidesWith(line, Strict), e.CollidesWith(line, Strict));
                Assert.Equal(Circle.CollidesWith(Polyline(), Strict), e.CollidesWith(Polyline(), Strict));
                foreach (GeoPolygon2 polygon in Polygons()) Assert.Equal(Circle.CollidesWith(polygon, Strict), e.CollidesWith(polygon, Strict));
                Assert.Equal(Circle.CollidesWith(Rectangle(), Strict), e.CollidesWith(Rectangle(), Strict));
                Assert.Equal(Circle.CollidesWith(Triangle(), Strict), e.CollidesWith(Triangle(), Strict));
                foreach (GeoFace2 face in Faces()) Assert.Equal(Circle.CollidesWith(face, Strict), e.CollidesWith(face, Strict));
                foreach (GeoEdge2 edge in Edges()) Assert.Equal(Circle.CollidesWith(edge, Strict), e.CollidesWith(edge, Strict));
                foreach (GeoCircle2 circle in Circles()) Assert.Equal(Circle.CollidesWith(circle, Strict), e.CollidesWith(circle, Strict));
                foreach (GeoArc2 arc in Arcs()) Assert.Equal(Circle.CollidesWith(arc, Strict), e.CollidesWith(arc, Strict));
                Assert.Equal(Circle.CollidesWith(Chain(), Strict), e.CollidesWith(Chain(), Strict));
                foreach (GeoPolygonArc2 loop in Loops()) Assert.Equal(Circle.CollidesWith(loop, Strict), e.CollidesWith(loop, Strict));
            }
        }

        [Fact]
        public void CrossingsAreTheCircles()
        {
            foreach (GeoEllipse2 e in Ellipses())
            {
                foreach (GeoLine2 line in Lines()) AssertSamePoints(Circle.GetIntersections(line, Strict), e.GetIntersections(line, Strict));
                AssertSamePoints(Circle.GetIntersections(Polyline(), Strict), e.GetIntersections(Polyline(), Strict));
                foreach (GeoPolygon2 polygon in Polygons()) AssertSamePoints(Circle.GetIntersections(polygon, Strict), e.GetIntersections(polygon, Strict));
                AssertSamePoints(Circle.GetIntersections(Rectangle(), Strict), e.GetIntersections(Rectangle(), Strict));
                AssertSamePoints(Circle.GetIntersections(Triangle(), Strict), e.GetIntersections(Triangle(), Strict));
                foreach (GeoFace2 face in Faces()) AssertSamePoints(Circle.GetIntersections(face, Strict), e.GetIntersections(face, Strict));
                foreach (GeoEdge2 edge in Edges()) AssertSamePoints(Circle.GetIntersections(edge, Strict), e.GetIntersections(edge, Strict));
                foreach (GeoCircle2 circle in Circles()) AssertSamePoints(Circle.GetIntersections(circle, Strict), e.GetIntersections(circle, Strict));
                foreach (GeoArc2 arc in Arcs()) AssertSamePoints(Circle.GetIntersections(arc, Strict), e.GetIntersections(arc, Strict));
                AssertSamePoints(Circle.GetIntersections(Chain(), Strict), e.GetIntersections(Chain(), Strict));
                foreach (GeoPolygonArc2 loop in Loops()) AssertSamePoints(Circle.GetIntersections(loop, Strict), e.GetIntersections(loop, Strict));
            }
        }

        [Fact]
        public void CrossingsOfStraightShapesComeInTheCirclesOrder()
        {
            // Along a segment, edge by edge, the outline then the holes (A8): the same list, not just the same points.
            foreach (GeoEllipse2 e in Ellipses())
            {
                foreach (GeoLine2 line in Lines()) AssertSameOrder(Circle.GetIntersections(line, Strict), e.GetIntersections(line, Strict));
                AssertSameOrder(Circle.GetIntersections(Polyline(), Strict), e.GetIntersections(Polyline(), Strict));
                foreach (GeoPolygon2 polygon in Polygons()) AssertSameOrder(Circle.GetIntersections(polygon, Strict), e.GetIntersections(polygon, Strict));
                AssertSameOrder(Circle.GetIntersections(Rectangle(), Strict), e.GetIntersections(Rectangle(), Strict));
                AssertSameOrder(Circle.GetIntersections(Triangle(), Strict), e.GetIntersections(Triangle(), Strict));
                foreach (GeoFace2 face in Faces()) AssertSameOrder(Circle.GetIntersections(face, Strict), e.GetIntersections(face, Strict));
                AssertSameOrder(Circle.GetIntersections(Edges()[0], Strict), e.GetIntersections(Edges()[0], Strict));
            }
        }

        private static void AssertSameOrder(GeoPoint2[] expected, GeoPoint2[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.InRange(expected[i].DistanceTo(actual[i]), 0.0, Within);
            }
        }

        [Fact]
        public void TouchesAreWhereTheCircleHasThem()
        {
            // 0.0004 off, within the tolerance: a segment above the top touches at its foot, the circle's own point
            // (Intersection2.cs:257-262); a circle of radius 50 above it at the midpoint of the two rims, within 1E-4 of the
            // circle's point, which weights the two by their radii (Intersection2.cs:318-330; A10).
            foreach (GeoEllipse2 e in Ellipses())
            {
                GeoLine2 above = Line(-200, 100.0004, 200, 100.0004);
                AssertSameOrder(Circle.GetIntersections(above, Strict), e.GetIntersections(above, Strict));

                var resting = new GeoCircle2(At(0, 150.0004), 50);
                GeoPoint2[] fromCircle = Circle.GetIntersections(resting, Strict);
                GeoPoint2[] fromEllipse = e.GetIntersections(resting, Strict);
                Assert.Single(fromCircle);
                Assert.Single(fromEllipse);
                Assert.InRange(fromEllipse[0].DistanceTo(fromCircle[0]), 0.0, 1E-4);
                Assert.InRange(Math.Abs(fromEllipse[0].DistanceTo(Circle.Center) - 100), 0.0, 5E-4);
                Assert.InRange(Math.Abs(fromEllipse[0].DistanceTo(resting.Center) - 50), 0.0, 5E-4);
            }
        }

        [Fact]
        public void TryIntersectWithAnswersAsTheCircleDoes()
        {
            foreach (GeoEllipse2 e in Ellipses())
            {
                foreach (GeoLine2 line in Lines())
                {
                    Assert.Equal(Circle.TryIntersectWith(line, out GeoPoint2[] c, Strict), e.TryIntersectWith(line, out GeoPoint2[] x, Strict));
                    AssertSamePoints(c, x);
                }

                foreach (GeoEdge2 edge in Edges())
                {
                    Assert.Equal(Circle.TryIntersectWith(edge, out GeoPoint2[] c, Strict), e.TryIntersectWith(edge, out GeoPoint2[] x, Strict));
                    AssertSamePoints(c, x);
                }

                foreach (GeoCircle2 circle in Circles())
                {
                    Assert.Equal(Circle.TryIntersectWith(circle, out GeoPoint2[] c, Strict), e.TryIntersectWith(circle, out GeoPoint2[] x, Strict));
                    AssertSamePoints(c, x);
                }

                foreach (GeoArc2 arc in Arcs())
                {
                    Assert.Equal(Circle.TryIntersectWith(arc, out GeoPoint2[] c, Strict), e.TryIntersectWith(arc, out GeoPoint2[] x, Strict));
                    AssertSamePoints(c, x);
                }

                Assert.Equal(Circle.TryIntersectWith(Chain(), out GeoPoint2[] fromCircle, Strict), e.TryIntersectWith(Chain(), out GeoPoint2[] fromEllipse, Strict));
                AssertSamePoints(fromCircle, fromEllipse);

                foreach (GeoPolygonArc2 loop in Loops())
                {
                    Assert.Equal(Circle.TryIntersectWith(loop, out GeoPoint2[] c, Strict), e.TryIntersectWith(loop, out GeoPoint2[] x, Strict));
                    AssertSamePoints(c, x);
                }
            }
        }

        [Fact]
        public void ShortestLinesAreTheCircles()
        {
            foreach (GeoEllipse2 e in Ellipses())
            {
                GeoLine2[] lines = Lines();
                AssertSameLine(Circle.GetShortestLineTo(lines[0], Strict), e.GetShortestLineTo(lines[0], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(lines[1], Strict), e.GetShortestLineTo(lines[1], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(lines[2], Strict), e.GetShortestLineTo(lines[2], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(Polyline(), Strict), e.GetShortestLineTo(Polyline(), Strict), true);

                GeoPolygon2[] polygons = Polygons();
                AssertSameLine(Circle.GetShortestLineTo(polygons[0], Strict), e.GetShortestLineTo(polygons[0], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(polygons[1], Strict), e.GetShortestLineTo(polygons[1], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(polygons[2], Strict), e.GetShortestLineTo(polygons[2], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(polygons[3], Strict), e.GetShortestLineTo(polygons[3], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(Rectangle(), Strict), e.GetShortestLineTo(Rectangle(), Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(Triangle(), Strict), e.GetShortestLineTo(Triangle(), Strict), true);
                foreach (GeoFace2 face in Faces()) AssertSameLine(Circle.GetShortestLineTo(face, Strict), e.GetShortestLineTo(face, Strict), false);
                foreach (GeoEdge2 edge in Edges()) AssertSameLine(Circle.GetShortestLineTo(edge, Strict), e.GetShortestLineTo(edge, Strict), false);

                GeoCircle2[] circles = Circles();
                AssertSameLine(Circle.GetShortestLineTo(circles[0], Strict), e.GetShortestLineTo(circles[0], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(circles[1], Strict), e.GetShortestLineTo(circles[1], Strict), false);
                AssertSameLine(Circle.GetShortestLineTo(circles[2], Strict), e.GetShortestLineTo(circles[2], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(circles[3], Strict), e.GetShortestLineTo(circles[3], Strict), true);

                GeoArc2[] arcs = Arcs();
                AssertSameLine(Circle.GetShortestLineTo(arcs[0], Strict), e.GetShortestLineTo(arcs[0], Strict), true);
                AssertSameLine(Circle.GetShortestLineTo(arcs[1], Strict), e.GetShortestLineTo(arcs[1], Strict), false);
                AssertSameLine(Circle.GetShortestLineTo(arcs[2], Strict), e.GetShortestLineTo(arcs[2], Strict), false);
                AssertSameLine(Circle.GetShortestLineTo(Chain(), Strict), e.GetShortestLineTo(Chain(), Strict), false);
                foreach (GeoPolygonArc2 loop in Loops()) AssertSameLine(Circle.GetShortestLineTo(loop, Strict), e.GetShortestLineTo(loop, Strict), false);
            }
        }

        [Fact]
        public void TheClosestEdgesAreTheCircles()
        {
            foreach (GeoEllipse2 e in Ellipses())
            {
                foreach (GeoPolygon2 polygon in Polygons()) Assert.Equal(Circle.GetClosestEdge(polygon), e.GetClosestEdge(polygon, Strict));
                Assert.Equal(Circle.GetClosestEdge(Polyline()), e.GetClosestEdge(Polyline(), Strict));
                Assert.Equal(Circle.GetClosestEdge(Rectangle()), e.GetClosestEdge(Rectangle(), Strict));
                Assert.Equal(Circle.GetClosestEdge(Triangle()), e.GetClosestEdge(Triangle(), Strict));
                Assert.Equal(Circle.GetClosestEdge(Chain(), Strict), e.GetClosestEdge(Chain(), Strict));
                foreach (GeoPolygonArc2 loop in Loops()) Assert.Equal(Circle.GetClosestEdge(loop, Strict), e.GetClosestEdge(loop, Strict));
            }
        }

        [Fact]
        public void HoldingIsTheCircles()
        {
            foreach (GeoEllipse2 e in Ellipses())
            {
                foreach (GeoLine2 line in Lines()) Assert.Equal(Circle.Contains(line, Strict), e.Contains(line, Strict));
                foreach (GeoCircle2 circle in Circles())
                {
                    Assert.Equal(Circle.Contains(circle, Strict), e.Contains(circle, Strict));
                    Assert.Equal(Circle.Contains(circle, Strict), e.Contains(GeoEllipse2.FromCircle(circle), Strict));
                }
            }
        }
    }
}
