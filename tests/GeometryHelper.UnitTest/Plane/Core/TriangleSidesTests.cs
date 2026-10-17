using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A triangle's own sides are measured as the sides they are, however short beside the tolerance. A triangle whose
    /// corners stand within the caller's tolerance of each other was read as its longest side alone, so a thin one was
    /// further from a shape than it is.
    /// </summary>
    public class TriangleSidesTests
    {
        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        /// <summary>The triangle (0, 0), (0, 1), (0.08, 1): its top is 0.08 wide, less than a tolerance of 0.1, and its left side is the line x = 0.</summary>
        private static GeoTriangle2 Thin() => new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(0, 1), new GeoPoint2(0.08, 1));

        /// <summary>Read as its longest side, from (0, 0) to (0.08, 1), it stands 0.1099 from x = -0.07 at y = 0.5; it stands 0.07 from it.</summary>
        private const double Truth = 0.07;

        public static System.Collections.Generic.IEnumerable<object[]> Shapes()
        {
            foreach (string kind in new[] { "segment", "polygon", "rectangle", "polyline", "ray", "triangle", "edge", "face" })
            {
                yield return new object[] { kind };
            }
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void AThinTriangle_IsThatFarFromAShapeAndCollidesWithItAtTheToleranceEitherWayRound(string kind)
        {
            Tolerance t = At(0.1);
            GeoTriangle2 triangle = Thin();
            double there, back;
            bool collides, collidesBack;

            switch (kind)
            {
                case "segment":
                    var line = new GeoLine2(new GeoPoint2(-1, 0.5), new GeoPoint2(-0.07, 0.5));
                    there = triangle.DistanceTo(line); back = line.DistanceTo(triangle);
                    collides = triangle.CollidesWith(line, t); collidesBack = line.CollidesWith(triangle, t);
                    break;
                case "polygon":
                    var polygon = new GeoPolygon2(new GeoPoint2(-1, 0.4), new GeoPoint2(-0.07, 0.4), new GeoPoint2(-0.07, 0.6), new GeoPoint2(-1, 0.6));
                    there = triangle.DistanceTo(polygon); back = polygon.DistanceTo(triangle);
                    collides = triangle.CollidesWith(polygon, t); collidesBack = polygon.CollidesWith(triangle, t);
                    break;
                case "rectangle":
                    var rect = new GeoRectangle2(new GeoPoint2(-0.57, 0.5), 1, 0.2, 0);
                    there = triangle.DistanceTo(rect); back = rect.DistanceTo(triangle);
                    collides = triangle.CollidesWith(rect, t); collidesBack = rect.CollidesWith(triangle, t);
                    break;
                case "polyline":
                    var polyline = new GeoPolyline2(new GeoPoint2(-1, 0.5), new GeoPoint2(-0.07, 0.5));
                    there = triangle.DistanceTo(polyline); back = polyline.DistanceTo(triangle);
                    collides = triangle.CollidesWith(polyline, t); collidesBack = polyline.CollidesWith(triangle, t);
                    break;
                case "ray":
                    var ray = new GeoRay2(new GeoPoint2(-0.07, 0.5), new GeoVector2(-1, 0));
                    there = triangle.DistanceTo(ray, t); back = ray.DistanceTo(triangle, t);
                    collides = triangle.CollidesWith(ray, t); collidesBack = ray.CollidesWith(triangle, t);
                    break;
                case "triangle":
                    var other = new GeoTriangle2(new GeoPoint2(-1, 0.4), new GeoPoint2(-0.07, 0.5), new GeoPoint2(-1, 0.6));
                    there = triangle.DistanceTo(other); back = other.DistanceTo(triangle);
                    collides = triangle.CollidesWith(other, t); collidesBack = other.CollidesWith(triangle, t);
                    break;
                case "edge":
                    var edge = new GeoEdge2(new GeoPoint2(-1, 0.5), new GeoPoint2(-0.07, 0.5));
                    there = triangle.DistanceTo(edge); back = edge.DistanceTo(triangle);
                    collides = triangle.CollidesWith(edge, t); collidesBack = edge.CollidesWith(triangle, t);
                    break;
                default:
                    var face = new GeoFace2(new GeoPolygon2(new GeoPoint2(-1, 0.3), new GeoPoint2(-0.07, 0.3), new GeoPoint2(-0.07, 0.7), new GeoPoint2(-1, 0.7)));
                    there = triangle.DistanceTo(face, t); back = face.DistanceTo(triangle, t);
                    collides = triangle.CollidesWith(face, t); collidesBack = face.CollidesWith(triangle, t);
                    break;
            }

            Assert.InRange(there, Truth - 1E-12, Truth + 1E-12);
            Assert.InRange(back, Truth - 1E-12, Truth + 1E-12);
            Assert.True(collides);
            Assert.True(collidesBack);
        }

        [Fact]
        public void ASliverAndAPointInsideIt_AgreeOnEveryMember()
        {
            // The triangle (0, 0), (10, 0), (0, 0.0005) is thinner than a tolerance of a thousandth. The point (1, 0.0001) lies
            // inside it, 0.0001 over its long side, and 0.00035 from its slanted one. Read as the long side alone, the
            // slanted side was the one it was measured to.
            Tolerance t = At(1E-3);
            var sliver = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 0.0005));
            var point = new GeoPoint2(1, 0.0001);
            var exact = new Tolerance(1E-12, 1E-12);

            Assert.Equal(0.0, sliver.DistanceTo(point));
            Assert.True(sliver.Contains(point, t));
            Assert.Equal(0.0, sliver.SignedDistanceTo(point, t));

            GeoLine2 shortest = sliver.GetShortestLineTo(point, t);
            Assert.InRange(shortest.Length, 0.0001 - 1E-12, 0.0001 + 1E-12);
            Assert.True(shortest.EndPoint.IsEqualTo(point, exact));
            Assert.True(sliver.GetClosestPointOnBoundary(point).IsEqualTo(new GeoPoint2(1, 0), exact));

            GeoLine2 edge = sliver.GetClosestEdge(point);
            Assert.True(edge.StartPoint.IsEqualTo(new GeoPoint2(0, 0), exact));
            Assert.True(edge.EndPoint.IsEqualTo(new GeoPoint2(10, 0), exact));

            // A segment 0.0009 over the long side stands 0.00045 from the slanted one at its start and 0.0005 at its end,
            // both within the 0.0006 of the tolerance; read as the long side alone it stands 0.0009 from it.
            Tolerance close = At(6E-4);
            Assert.True(sliver.Contains(new GeoLine2(new GeoPoint2(1, 0.0009), new GeoPoint2(2, 0.0009)), close));
            Assert.False(sliver.Contains(new GeoLine2(new GeoPoint2(1, 0.003), new GeoPoint2(2, 0.003)), close));
        }

        [Fact]
        public void ASliverIsAskedForTheShortestLineToAPointAtAnyToleranceAsTheTriangleItIs()
        {
            // The sliver 100 long and 0.5 high, the point 1 past its tip: its short side is nearest at (100, 0.25) whatever
            // the tolerance, 1 included, which used to read the sliver as the segment of its hull and give (100, 0.5).
            var sliver = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 0.5));
            var point = new GeoPoint2(101, 0.25);
            var exact = new Tolerance(1E-9, 1E-9);

            foreach (double equalPoint in new[] { 1E-3, 1.0 })
            {
                Assert.True(sliver.GetShortestLineTo(point, At(equalPoint)).StartPoint.IsEqualTo(new GeoPoint2(100, 0.25), exact));
            }
        }

        [Fact]
        public void ATriangleWithCornersFourThousandthsApart_IsMeasuredTheSameWhateverTheGlobalTolerance()
        {
            // The corners (0, 1) and (0.004, 1) stand within a global tolerance of 0.01 and not within 0.00001. The triangle is
            // 0.0123 from the left of x = -0.0123 either way.
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(0, 1), new GeoPoint2(0.004, 1));
            var polygon = new GeoPolygon2(new GeoPoint2(-1, 0.4), new GeoPoint2(-0.0123, 0.4), new GeoPoint2(-0.0123, 0.6), new GeoPoint2(-1, 0.6));

            double fine, coarse;

            using (Tolerance.Use(new Tolerance(1E-5, Tolerance.DefaultEqualVector)))
            {
                fine = triangle.DistanceTo(polygon);
            }

            using (Tolerance.Use(new Tolerance(1E-2, Tolerance.DefaultEqualVector)))
            {
                coarse = triangle.DistanceTo(polygon);
            }

            Assert.InRange(fine, 0.0123 - 1E-12, 0.0123 + 1E-12);
            Assert.Equal(fine, coarse);
        }
    }
}
