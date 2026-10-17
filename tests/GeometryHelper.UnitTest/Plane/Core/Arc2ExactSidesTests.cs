using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A shape's own sides are measured as the sides they are against arcs, however short, and a vertex a shape shares
    /// with a curved edge reads nought whichever shape asks. A segment given by the caller keeps its reading as a point
    /// when it is shorter than the tolerance; the sides of a triangle, polygon, rectangle, chain or loop do not.
    /// </summary>
    public class Arc2ExactSidesTests
    {
        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        #region An edge and the shapes that share its vertex

        public static System.Collections.Generic.IEnumerable<object[]> SharedVertexCases()
        {
            foreach (string kind in new[] { "polygon", "polyline", "rectangle", "triangle", "chain", "loop" })
            {
                yield return new object[] { kind, 100.0, 0.0 };
                yield return new object[] { kind, 10000.0, 1E5 };
            }
        }

        [Theory]
        [MemberData(nameof(SharedVertexCases))]
        public void AShapeSharingTheEndOfAnEdgeOfBulge1E7_IsNoughtFromItEitherWayRound(string kind, double chord, double offset)
        {
            Tolerance t = At(1E-3);
            double x0 = offset, y0 = offset * 0.5;
            var edge = new GeoEdge2(new GeoPoint2(x0, y0), new GeoPoint2(x0 + chord, y0), 1E-7);
            GeoPoint2 v = edge.EndPoint;
            GeoPoint2 right = new GeoPoint2(v.X + 50, v.Y), corner = new GeoPoint2(v.X + 50, v.Y + 50), up = new GeoPoint2(v.X + 50, v.Y + 10);

            double there, back;

            switch (kind)
            {
                case "polygon":
                    var polygon = new GeoPolygon2(v, right, corner);
                    there = polygon.DistanceTo(edge); back = edge.DistanceTo(polygon);
                    break;
                case "polyline":
                    var polyline = new GeoPolyline2(v, up);
                    there = polyline.DistanceTo(edge); back = edge.DistanceTo(polyline);
                    break;
                case "rectangle":
                    var rect = new GeoRectangle2(new GeoPoint2(v.X + 25, v.Y + 25), 50, 50, 0);
                    there = rect.DistanceTo(edge); back = edge.DistanceTo(rect);
                    break;
                case "triangle":
                    var triangle = new GeoTriangle2(v, right, corner);
                    there = triangle.DistanceTo(edge); back = edge.DistanceTo(triangle);
                    break;
                case "chain":
                    var chain = new GeoPolylineArc2(new[] { v, up }, new[] { 0.0, 0.0 });
                    there = chain.DistanceTo(edge, t); back = edge.DistanceTo(chain, t);
                    break;
                default:
                    var loop = new GeoPolygonArc2(new[] { v, right, corner }, new[] { 0.0, 0.0, 0.0 });
                    there = loop.DistanceTo(edge, t); back = edge.DistanceTo(loop, t);
                    break;
            }

            Assert.Equal(0.0, there);
            Assert.Equal(0.0, back);
        }

        #endregion

        #region A shape's own short side

        // The arc about the origin of radius 0.665358 from 0.111401 to 2.78899 radians.
        private static GeoArc2 Arc() => new GeoArc2(new GeoPoint2(0, 0), 0.665358, 0.111401, 2.78899);

        private static void AssertMeasured(double truth, double there, double back, bool collides, bool collidesBack, double equalPoint)
        {
            Assert.InRange(there, truth - 1E-9, truth + 1E-9);
            Assert.InRange(back, truth - 1E-9, truth + 1E-9);
            Assert.Equal(truth <= equalPoint, collides);
            Assert.Equal(truth <= equalPoint, collidesBack);
        }

        [Fact]
        public void ATriangleWithASideShorterThanTheTolerance_IsMeasuredAsTheTriangleItIs()
        {
            // Its short side, 0.0845, was a reason to read it as its longest side at a tolerance of 0.1, 0.1301 off the
            // arc, where it stands 0.0523 off it and collides.
            Tolerance t = At(0.1);
            var triangle = new GeoTriangle2(new GeoPoint2(0.678308, -0.078659), new GeoPoint2(0.627916, 0.0335963), new GeoPoint2(0.567449, -0.0252647));
            GeoArc2 arc = Arc();

            AssertMeasured(0.05234469823034279, triangle.DistanceTo(arc, t), arc.DistanceTo(triangle, t), triangle.CollidesWith(arc, t), arc.CollidesWith(triangle, t), 0.1);
        }

        [Fact]
        public void APolygonWithASideShorterThanTheTolerance_IsMeasuredAsThePolygonItIs()
        {
            // The side from the second corner to the third is 0.06; the polygon is 0.13724 off the arc, which at a tolerance
            // of 0.138 is a collision.
            Tolerance t = At(0.138);
            var polygon = new GeoPolygon2(new GeoPoint2(0.6794780791991906, -0.06378992785720422), new GeoPoint2(0.619497986343159, -0.06224446320803608), new GeoPoint2(0.5313297234266787, -0.3489958492801707));
            GeoArc2 arc = Arc();

            AssertMeasured(0.13724261790651732, polygon.DistanceTo(arc, t), arc.DistanceTo(polygon, t), polygon.CollidesWith(arc, t), arc.CollidesWith(polygon, t), 0.138);
        }

        [Fact]
        public void ARectangleWithASideShorterThanTheTolerance_IsMeasuredAsTheRectangleItIs()
        {
            Tolerance t = At(0.139);
            var rect = new GeoRectangle2(new GeoPoint2(0.8336599981014124, -0.11101827610764912), 0.06, 0.2271937720401338, 0.6813389954720512);
            GeoArc2 arc = Arc();

            AssertMeasured(0.138687680030960, rect.DistanceTo(arc, t), arc.DistanceTo(rect, t), rect.CollidesWith(arc, t), arc.CollidesWith(rect, t), 0.139);
        }

        [Fact]
        public void ACurvedChainWithAnEdgeShorterThanTheTolerance_IsMeasuredAsTheChainItIs()
        {
            Tolerance t = At(0.138);
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0.6794780791991906, -0.06378992785720422), new GeoPoint2(0.619497986343159, -0.06224446320803608), new GeoPoint2(0.5313297234266787, -0.3489958492801707) },
                new[] { 0.0, 0.0, 0.0 });
            GeoArc2 arc = Arc();

            AssertMeasured(0.13724261790651732, chain.DistanceTo(arc, t), arc.DistanceTo(chain, t), chain.CollidesWith(arc, t), arc.CollidesWith(chain, t), 0.138);
        }

        [Fact]
        public void ACurvedLoopWithASideShorterThanTheTolerance_IsMeasuredAsTheLoopItIs()
        {
            Tolerance t = At(0.138);
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0.6794780791991906, -0.06378992785720422), new GeoPoint2(0.619497986343159, -0.06224446320803608), new GeoPoint2(0.5313297234266787, -0.3489958492801707) },
                new[] { 0.0, 0.0, 0.0 });
            GeoArc2 arc = Arc();

            AssertMeasured(0.13724261790651732, loop.DistanceTo(arc, t), arc.DistanceTo(loop, t), loop.CollidesWith(arc, t), arc.CollidesWith(loop, t), 0.138);
        }

        #endregion

        #region A shape's own short side against a point or a circle

        private static void UnderBothScopes(Action<Tolerance> check)
        {
            foreach (double equalPoint in new[] { 1E-5, 1E-2 })
            {
                var scope = new Tolerance(equalPoint, Tolerance.DefaultEqualVector);

                using (Tolerance.Use(scope))
                {
                    check(scope);
                }
            }
        }

        [Fact]
        public void ATriangleWithASideOfFourThousandths_IsMeasuredToAPointAndACircleAsTheTriangleItIs()
        {
            // The top side is 0.004 long, shorter than a global tolerance of 0.01 and longer than 0.00001: it is the side it is
            // under both. The point stands 0.006 over its middle, the circle of radius 0.002 about (0.002, 0.008) 0.006.
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(0.004, 0), new GeoPoint2(0.002, -5));
            var point = new GeoPoint2(0.002, 0.006);
            var circle = new GeoCircle2(new GeoPoint2(0.002, 0.008), 0.002);

            UnderBothScopes(t =>
            {
                Assert.InRange(triangle.DistanceTo(point), 0.006 - 1E-12, 0.006 + 1E-12);
                Assert.InRange(point.DistanceTo(triangle), 0.006 - 1E-12, 0.006 + 1E-12);
                Assert.InRange(triangle.DistanceTo(circle), 0.006 - 1E-12, 0.006 + 1E-12);
                Assert.InRange(circle.DistanceTo(triangle), 0.006 - 1E-12, 0.006 + 1E-12);
            });
        }

        [Fact]
        public void ATriangleWithASideUnderAThousandth_IsMeasuredToAPointAndACircleAtTheDefaultTolerance()
        {
            // The circle of radius 0.00015 about (0.00045, 0.0011) stands 0.00095 from the top side, which is 0.0009 long;
            // that is within the point tolerance of a thousandth, so they collide.
            Tolerance t = Tolerance.Default;
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(0.0009, 0), new GeoPoint2(0.00045, -5));
            var circle = new GeoCircle2(new GeoPoint2(0.00045, 0.0011), 0.00015);
            var point = new GeoPoint2(0.00045, 0.00095);

            Assert.InRange(triangle.DistanceTo(circle), 0.00095 - 1E-12, 0.00095 + 1E-12);
            Assert.InRange(circle.DistanceTo(triangle), 0.00095 - 1E-12, 0.00095 + 1E-12);
            Assert.True(triangle.CollidesWith(circle, t));
            Assert.True(circle.CollidesWith(triangle, t));
            Assert.InRange(triangle.DistanceTo(point), 0.00095 - 1E-12, 0.00095 + 1E-12);
        }

        [Fact]
        public void APolygonWithASideOfFiveThousandths_IsMeasuredToAPointAndACircleAsThePolygonItIs()
        {
            var polygon = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(0.005, 0), new GeoPoint2(0.0025, -5));
            var point = new GeoPoint2(0.0025, 0.003);
            var circle = new GeoCircle2(new GeoPoint2(0.0025, 0.004), 0.001);

            UnderBothScopes(t =>
            {
                Assert.InRange(polygon.DistanceTo(point), 0.003 - 1E-12, 0.003 + 1E-12);
                Assert.InRange(point.DistanceTo(polygon), 0.003 - 1E-12, 0.003 + 1E-12);
                Assert.InRange(polygon.DistanceTo(circle), 0.003 - 1E-12, 0.003 + 1E-12);
                Assert.InRange(circle.DistanceTo(polygon), 0.003 - 1E-12, 0.003 + 1E-12);
            });
        }

        [Fact]
        public void ACurvedLoopAndAChainWithASideOfFiveThousandths_AreMeasuredToAPointAsTheyAre()
        {
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(0.005, 0), new GeoPoint2(0.0025, -5) }, new[] { 0.0, 0.0, 0.0 });
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(0.005, 0) }, new[] { 0.0, 0.0 });
            var point = new GeoPoint2(0.0025, 0.003);
            var circle = new GeoCircle2(new GeoPoint2(0.0025, 0.004), 0.001);

            foreach (double equalPoint in new[] { 1E-5, 1E-2 })
            {
                Tolerance t = At(equalPoint);

                Assert.InRange(loop.DistanceTo(point, t), 0.003 - 1E-12, 0.003 + 1E-12);
                Assert.InRange(point.DistanceTo(loop, t), 0.003 - 1E-12, 0.003 + 1E-12);
                Assert.InRange(chain.DistanceTo(point, t), 0.003 - 1E-12, 0.003 + 1E-12);
                Assert.InRange(point.DistanceTo(chain, t), 0.003 - 1E-12, 0.003 + 1E-12);
                Assert.InRange(loop.DistanceTo(circle, t), 0.003 - 1E-12, 0.003 + 1E-12);
                Assert.InRange(chain.DistanceTo(circle, t), 0.003 - 1E-12, 0.003 + 1E-12);
            }
        }

        #endregion
    }
}
