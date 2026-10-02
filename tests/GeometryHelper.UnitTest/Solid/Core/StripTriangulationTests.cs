using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Cutting a face into strips at its corners covers its material, and only that, whatever its rings do to each other.
    /// </summary>
    public class StripTriangulationTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static readonly GeoCoordinateSystem3 Frame =
            new GeoCoordinateSystem3(new GeoPlane3(new GeoPoint3(-25300.75, 61200.5, 3100.0), new GeoVector3(-0.4, 0.1, 0.9).Normalize()));

        private static GeoPoint3 P(double u, double v) => Frame.ToGlobal(new GeoPoint3(u, v, 0));

        private static GeoPolygon3 Ring(params (double U, double V)[] corners) => new GeoPolygon3(corners.Select(c => P(c.U, c.V)), Tolerance);

        private static GeoPolygon3 Square(double u, double v, double side) => Ring((u, v), (u + side, v), (u + side, v + side), (u, v + side));

        [Fact]
        public void APlateWithAHoleIsCoveredRoundTheHole()
        {
            var face = new GeoFace3(Square(0, 0, 100), new[] { Square(30, 40, 20) }, Tolerance);

            GeoTriangle3[] triangles = StripTriangulation.Triangulate(face, Tolerance);

            AssertCovers(face, triangles, 100 * 100 - 20 * 20, 1E-9);
        }

        [Fact]
        public void AFaceThinnerThanTheGlobalToleranceIsCoveredWithinAFinerOne()
        {
            // A strip 300 long and 0.006 wide, a face at a tolerance of a ten-thousandth: laid out, it was checked against the
            // global tolerance, which takes its two long sides for one, and refused.
            var fine = new Tolerance(1E-4, 1E-4);
            var face = new GeoFace3(new GeoPolygon3(new[] { P(0, 0), P(300, 0), P(300, 0.006), P(0, 0.006) }, fine));

            GeoTriangle3[] triangles = StripTriangulation.Triangulate(face, fine);

            Assert.Equal(300 * 0.006, triangles.Sum(t => t.Area), 9);
        }

        [Fact]
        public void TheTrianglesKeepTheCornersOfTheFace()
        {
            var face = new GeoFace3(
                Ring((0, 0), (300, 0), (300, 100), (120, 100), (120, 250), (0, 250)),
                new[] { Square(30, 40, 20), Ring((200, 20), (260, 30), (230, 80)) },
                Tolerance);

            GeoTriangle3[] triangles = StripTriangulation.Triangulate(face, Tolerance);
            var used = new HashSet<GeoPoint3>(triangles.SelectMany(t => new[] { t.A, t.B, t.C }));

            AssertCovers(face, triangles, face.Area, 1E-9);

            foreach (GeoPoint3 corner in face.Boundary.Vertices.Concat(face.Holes.SelectMany(h => h.Vertices)))
            {
                Assert.Contains(corner, used);
            }
        }

        [Fact]
        public void ThePointsTheStripsMakeLieOnTheEdgesAsTheyRunInSpace()
        {
            // The corners stand a thousandth either side of the plane in turn, as a face flat only within the tolerance has
            // them. A point a strip line makes on an edge taken on the plane stood off the edge by as much, and a sliver
            // between it and the corner beside it stood up across the strip.
            GeoPoint3 Q(double u, double v, double w) => Frame.ToGlobal(new GeoPoint3(u, v, w));
            var boundary = new GeoPolygon3(new[] { Q(0, 0, 1E-3), Q(300, 0, -1E-3), Q(300, 100, 1E-3), Q(120, 100, -1E-3), Q(120, 250, 1E-3), Q(0, 250, -1E-3) }, Tolerance);
            var hole = new GeoPolygon3(new[] { Q(30, 40, -1E-3), Q(50, 40, 1E-3), Q(50, 60, -1E-3), Q(30, 60, 1E-3) }, Tolerance);
            var face = new GeoFace3(boundary, new[] { hole }, Tolerance);

            GeoTriangle3[] triangles = StripTriangulation.Triangulate(face, Tolerance);
            var corners = new HashSet<GeoPoint3>(boundary.Vertices.Concat(hole.Vertices));
            var edges = new[] { boundary, hole }
                .SelectMany(ring => ring.Vertices.Select((corner, i) => new GeoLine3(corner, ring.Vertices[(i + 1) % ring.VertexCount])))
                .ToArray();

            AssertCovers(face, triangles, face.Area, 1E-9);

            foreach (GeoPoint3 point in triangles.SelectMany(t => new[] { t.A, t.B, t.C }).Where(p => !corners.Contains(p)))
            {
                Assert.True(edges.Min(edge => edge.DistanceTo(point)) < 1E-9, $"{point} lies off every edge of the face");
            }
        }

        [Fact]
        public void HolesThatOverlapTakeAwayWhatTheyCoverTogether()
        {
            // Two squares 40 a side overlapping in a square 20 a side: together they cover 2 * 1600 - 400.
            var face = new GeoFace3(Square(0, 0, 100), new[] { Square(10, 10, 40), Square(30, 30, 40) }, Tolerance);

            GeoTriangle3[] triangles = StripTriangulation.Triangulate(face, Tolerance);

            AssertCovers(face, triangles, 100 * 100 - (2 * 1600 - 400), 1E-9);
        }

        [Fact]
        public void AHoleReachingPastTheBoundaryTakesAwayOnlyWhatItCovers()
        {
            // Half of the hole lies beyond the right side of the plate.
            var face = new GeoFace3(Square(0, 0, 100), new[] { Square(80, 40, 40) }, Tolerance);

            GeoTriangle3[] triangles = StripTriangulation.Triangulate(face, Tolerance);

            AssertCovers(face, triangles, 100 * 100 - 20 * 40, 1E-9);
        }

        [Fact]
        public void HolesTouchingTheBoundaryAndEachOtherLeaveTheRestCovered()
        {
            // A notch-like hole standing on the bottom side, a second sharing its right side, and a third meeting the
            // second at a corner only.
            var face = new GeoFace3(
                Ring((0, 0), (200, 0), (200, 100), (0, 100)),
                new[] { Square(20, 0, 30), Square(50, 0, 30), Square(80, 30, 20) },
                Tolerance);

            GeoTriangle3[] triangles = StripTriangulation.Triangulate(face, Tolerance);

            AssertCovers(face, triangles, 200 * 100 - 900 - 900 - 400, 1E-9);
        }

        [Fact]
        public void AFaceItsHolesCoverWhollyGivesNoTriangles()
        {
            var face = new GeoFace3(Square(0, 0, 100), new[] { Square(-10, -10, 120) }, Tolerance);

            Assert.Empty(StripTriangulation.Triangulate(face, Tolerance));
        }

        private static void AssertCovers(GeoFace3 face, GeoTriangle3[] triangles, double material, double relative)
        {
            Assert.InRange(triangles.Sum(t => t.Area), material * (1 - relative), material * (1 + relative));

            foreach (GeoTriangle3 triangle in triangles)
            {
                Assert.True(triangle.GetAreaVector().DotProduct(face.Normal) > 0.0);
                Assert.NotEqual(PointLocation.OutSide, Containment3.Locate(face.Boundary, triangle.Centroid, Tolerance));

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    Assert.NotEqual(PointLocation.Inside, Containment3.Locate(hole, triangle.Centroid, Tolerance));
                }
            }
        }
    }
}
