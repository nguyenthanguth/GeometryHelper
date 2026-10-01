using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The closed shapes of the plane broken into triangles: every triangle within the material, every hole open,
    /// every triangle counter-clockwise, and the area of the triangles that of the shape.
    /// </summary>
    public class TriangulateSurface2Tests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Square(double x, double y, double side) => new GeoPolygon2(P(x, y), P(x + side, y), P(x + side, y + side), P(x, y + side));

        /// <summary>
        /// Asserts the triangles cover the material: their area is the material's, each runs counter-clockwise, and
        /// none lies outside the boundary or within a hole.
        /// </summary>
        private static void AssertCovers(GeoTriangle2[] triangles, GeoFace2 face, double material, double slack)
        {
            Assert.InRange(triangles.Sum(t => t.Area), material - slack, material + slack);

            foreach (GeoTriangle2 triangle in triangles)
            {
                Assert.False(triangle.IsClockwise);
                Assert.NotEqual(PointLocation.OutSide, face.Boundary.Locate(triangle.Centroid, Tolerance));

                foreach (GeoPolygon2 hole in face.Holes)
                {
                    Assert.NotEqual(PointLocation.Inside, hole.Locate(triangle.Centroid, Tolerance));
                }
            }
        }

        [Fact]
        public void ASquareIsTwoTrianglesOnItsOwnCorners()
        {
            GeoPolygon2 square = Square(0, 0, 100);

            GeoTriangle2[] triangles = square.TriangulateSurface(Tolerance);

            Assert.Equal(2, triangles.Length);
            AssertCovers(triangles, new GeoFace2(square), 10000, 1E-9);
            Assert.All(triangles.SelectMany(t => t.GetVertices()), corner => Assert.Contains(corner, square.Vertices));
        }

        [Fact]
        public void APolygonRunningClockwiseGivesCounterClockwiseTriangles()
        {
            GeoPolygon2 clockwise = Square(0, 0, 100).Reverse();

            GeoTriangle2[] triangles = clockwise.TriangulateSurface(Tolerance);

            Assert.True(clockwise.IsClockwise);
            AssertCovers(triangles, new GeoFace2(clockwise), 10000, 1E-9);
        }

        [Fact]
        public void AnLIsFollowedRoundItsNotch()
        {
            var l = new GeoPolygon2(P(0, 0), P(100, 0), P(100, 40), P(40, 40), P(40, 100), P(0, 100));

            GeoTriangle2[] triangles = l.TriangulateSurface(Tolerance);

            Assert.Equal(4, triangles.Length);
            AssertCovers(triangles, new GeoFace2(l), l.Area, 1E-9);
            Assert.All(triangles, t => Assert.Equal(PointLocation.OutSide, t.Locate(new GeoPoint2(70, 70), Tolerance)));
        }

        [Fact]
        public void AFaceIsMeshedRoundEveryHole()
        {
            var face = new GeoFace2(Square(0, 0, 1000), new[] { Square(100, 100, 200), Square(500, 400, 300), Square(150, 600, 100) });

            GeoTriangle2[] triangles = face.TriangulateSurface(Tolerance);

            AssertCovers(triangles, face, face.Area, 1E-6);
        }

        [Fact]
        public void HolesSharingAnEdgeAreTakenTogether()
        {
            // Two holes side by side, sharing the side x = 300, and a third standing on the boundary's bottom edge.
            var face = new GeoFace2(Square(0, 0, 1000), new[] { Square(100, 100, 200), Square(300, 100, 200), Square(700, 0, 100) });

            GeoTriangle2[] triangles = face.TriangulateSurface(Tolerance);

            AssertCovers(triangles, face, 1000.0 * 1000.0 - 2 * 200 * 200 - 100 * 100, 1E-6);
        }

        [Fact]
        public void APolygonCrossingItselfIsCoveredWhereMakeValidSaysItIs()
        {
            // A bow tie of two triangles meeting at (50, 50).
            var bowTie = new GeoPolygon2(P(0, 0), P(100, 100), P(100, 0), P(0, 100));

            GeoTriangle2[] triangles = bowTie.TriangulateSurface(Tolerance);
            double valid = bowTie.MakeValid(Tolerance).Sum(face => face.Area);

            Assert.Equal(5000.0, valid, 6);
            Assert.InRange(triangles.Sum(t => t.Area), valid - 1E-6, valid + 1E-6);
            Assert.All(triangles, t => Assert.False(t.IsClockwise));
        }

        [Fact]
        public void APolygonOfNoAreaGivesNoTriangles()
        {
            var flat = new GeoPolygon2(P(0, 0), P(50, 0), P(100, 0));

            Assert.Empty(flat.TriangulateSurface(Tolerance));
        }

        [Fact]
        public void AHoleOfNoAreaTakesNothingAway()
        {
            var face = new GeoFace2(Square(0, 0, 100), new[] { new GeoPolygon2(P(10, 10), P(20, 10), P(30, 10)) });

            Assert.InRange(face.TriangulateSurface(Tolerance).Sum(t => t.Area), 10000 - 1E-9, 10000 + 1E-9);
        }

        [Fact]
        public void AFaceFarFromTheOriginKeepsItsArea()
        {
            var offset = new GeoVector2(4.2E5, -3.1E5);
            var face = new GeoFace2(Square(0, 0, 1000).Translate(offset), new[] { Square(250, 250, 500).Translate(offset) });

            GeoTriangle2[] triangles = face.TriangulateSurface(Tolerance);

            AssertCovers(triangles, face, 750000, 1E-6);
        }

        [Fact]
        public void TheTrianglesAreTheOnesTheFaceHasInSpace()
        {
            var face = new GeoFace2(new GeoPolygon2(P(0, 0), P(300, 0), P(300, 100), P(120, 100), P(120, 250), P(0, 250)), new[] { Square(30, 40, 50) });
            GeoTriangle2[] flat = face.TriangulateSurface(Tolerance);

            var frame = new GeoCoordinateSystem3(new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis));
            GeoTriangle3[] space = face.ToFace3(frame).TriangulateSurface(Tolerance);

            Assert.Equal(space.Length, flat.Length);
            Assert.Equal(space.Sum(t => t.Area), flat.Sum(t => t.Area), 9);
        }

        [Fact]
        public void ALoopWithArcsIsCoveredAsItFlattens()
        {
            GeoPolygonArc2 rounded = Square(0, 0, 100).Fillet(20.0);
            const double chord = 0.05;

            GeoTriangle2[] triangles = rounded.TriangulateSurface(chord, Tolerance);
            GeoPolygon2 flattened = rounded.Flatten(chord);

            AssertCovers(triangles, new GeoFace2(flattened), flattened.Area, 1E-6);

            // The chords lie inside the arcs, by no more than the chord tolerance along each.
            double exact = 100.0 * 100.0 - (4.0 - Math.PI) * 20.0 * 20.0;
            Assert.InRange(triangles.Sum(t => t.Area), exact - chord * rounded.Length, exact);
            Assert.All(triangles.SelectMany(t => t.GetVertices()), corner => Assert.True(rounded.DistanceTo(corner) <= 1E-9));
        }

        [Fact]
        public void ADiscIsFannedFromItsCenter()
        {
            var disc = new GeoCircle2(new GeoPoint2(500, -200), 50);

            GeoTriangle2[] triangles = disc.TriangulateSurface(Tolerance);
            GeoPolygon2 rim = disc.ToPolygon();

            Assert.Equal(rim.VertexCount, triangles.Length);
            Assert.Equal(rim.Area, triangles.Sum(t => t.Area), 9);
            Assert.All(triangles, t => Assert.Equal(disc.Center, t.A));
            Assert.All(triangles, t => Assert.False(t.IsClockwise));

            // A finer chord tolerance gives more and smaller triangles, closer to the circle.
            GeoTriangle2[] finer = disc.TriangulateSurface(0.001, Tolerance);
            Assert.True(finer.Length > triangles.Length);
            Assert.InRange(finer.Sum(t => t.Area), triangles.Sum(t => t.Area), Math.PI * 50 * 50);
        }

        [Fact]
        public void ADiscOfNoRadiusGivesNoTriangles()
        {
            Assert.Empty(new GeoCircle2(new GeoPoint2(1, 1), 0).TriangulateSurface(Tolerance));
        }

        [Fact]
        public void ARectangleIsTwoTriangles()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(10, 20), 300, 120, 0.4);

            GeoTriangle2[] triangles = rectangle.TriangulateSurface(Tolerance);

            Assert.Equal(2, triangles.Length);
            AssertCovers(triangles, new GeoFace2(rectangle.ToPolygon()), 300 * 120, 1E-9);
            Assert.Empty(new GeoRectangle2(new GeoPoint2(0, 0), 300, 0).TriangulateSurface(Tolerance));
        }
    }
}
