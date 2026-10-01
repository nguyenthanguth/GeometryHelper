using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Strips, convex pieces and triangles: each kind's own promise, and the promises every mesh keeps.
    /// </summary>
    public class StripConvexTriangleMeshTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Box(double x0, double y0, double x1, double y1) => new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        private static readonly GeoPolygon2 L = new GeoPolygon2(P(0, 0), P(2000, 0), P(2000, 1000), P(1000, 1000), P(1000, 2000), P(0, 2000));

        /// <summary>A slab with three openings, one of them a triangle, and a hole touching the boundary.</summary>
        private static readonly GeoFace2 Slab = new GeoFace2(
            new GeoPolygon2(P(0, 0), P(9000, 0), P(9000, 4000), P(4500, 4000), P(4500, 8000), P(0, 8000)),
            new[] { Box(1500, 1500, 2700, 2400), new GeoPolygon2(P(6000, 1200), P(7600, 1600), P(7000, 2900)), Box(1800, 5600, 3000, 6600) });

        /// <summary>Holes that touch each other and the boundary, which ear clipping cannot reduce and strips cut instead.</summary>
        private static readonly GeoFace2 Touching = new GeoFace2(
            Box(0, 0, 1000, 600),
            new[] { Box(100, 100, 300, 300), Box(300, 300, 500, 500), Box(700, 0, 900, 200) });

        private static IEnumerable<object[]> Shapes()
        {
            yield return new object[] { "L", new GeoFace2(L) };
            yield return new object[] { "slab", Slab };
            yield return new object[] { "touching", Touching };
            yield return new object[] { "far", new GeoFace2(new GeoPolygon2(L.Vertices.Select(p => P(p.X + 512345.5, p.Y + 7012345.25)))) };
        }

        public static IEnumerable<object[]> ShapesAndKinds()
        {
            foreach (object[] shape in Shapes())
            {
                foreach (MeshKind kind in new[] { MeshKind.Triangles, MeshKind.Convex, MeshKind.Strips })
                {
                    yield return new[] { shape[0], shape[1], kind };
                }
            }
        }

        [Theory]
        [MemberData(nameof(ShapesAndKinds))]
        public void EveryKindCoversTheMaterialEdgeToEdge(string name, GeoFace2 face, MeshKind kind)
        {
            GeoMesh2 mesh = face.ToMesh(kind);

            Assert.True(mesh.Kind == kind, name);
            MeshAssert.IsSound(mesh, p => face.Locate(p, Tolerance), face.Area, Tolerance);
            Assert.All(Enumerable.Range(0, mesh.FaceCount), f => Assert.False(mesh.IsWhole(f)));

            foreach (GeoPoint2 corner in face.Boundary.Vertices.Concat(face.Holes.SelectMany(h => h.Vertices)))
            {
                Assert.Contains(corner, mesh.Vertices);
            }
        }

        [Theory]
        [MemberData(nameof(ShapesAndKinds))]
        public void EachKindHasItsOwnShapeOfFace(string name, GeoFace2 face, MeshKind kind)
        {
            GeoMesh2 mesh = face.ToMesh(kind);

            foreach (GeoPolygon2 piece in mesh.GetFaces())
            {
                List<GeoPoint2> turns = MeshAssert.Turns(piece, 1E-6);

                switch (kind)
                {
                    case MeshKind.Triangles:
                        Assert.True(piece.VertexCount == 3, $"{name}: a face of {piece.VertexCount} corners");
                        break;

                    case MeshKind.Convex:
                        Assert.True(IsConvex(piece), $"{name}: {MeshAssert.Describe(piece)} is not convex");
                        break;

                    case MeshKind.Strips:
                        // A trapezoid whose parallel sides run along the X axis, or a triangle where it comes to a point.
                        Assert.True(turns.Count == 3 || turns.Count == 4, $"{name}: a piece of {turns.Count} corners");
                        Assert.True(turns.Count(t => turns.Count(o => Math.Abs(o.Y - t.Y) <= 1E-6) >= 2) >= 2, $"{name}: {MeshAssert.Describe(piece)} has no side along the strips");
                        break;
                }
            }
        }

        private static bool IsConvex(GeoPolygon2 polygon)
        {
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                GeoPoint2 previous = polygon[(i + polygon.VertexCount - 1) % polygon.VertexCount];
                GeoPoint2 corner = polygon[i];
                GeoPoint2 next = polygon[(i + 1) % polygon.VertexCount];

                if (new GeoLine2(previous, next).DistanceTo(corner) > Tolerance.EqualPoint && previous.GetVectorTo(corner).CrossProduct(corner.GetVectorTo(next)) < 0.0)
                {
                    return false;
                }
            }

            return true;
        }

        [Fact]
        public void TrianglesAreTheSurfacesTrianglesWhileTheRingsStandApart()
        {
            GeoMesh2 mesh = Slab.ToMesh(MeshKind.Triangles);
            GeoTriangle2[] surface = Slab.TriangulateSurface(Tolerance.Global);

            Assert.Equal(surface.Length, mesh.FaceCount);
            Assert.Equal(Slab.Boundary.VertexCount + Slab.Holes.Sum(h => h.VertexCount), mesh.VertexCount);

            foreach (GeoTriangle2 triangle in mesh.ToTriangles())
            {
                Assert.Contains(surface, t => new HashSet<GeoPoint2> { t.A, t.B, t.C }.SetEquals(new[] { triangle.A, triangle.B, triangle.C }));
            }
        }

        [Fact]
        public void TrianglesGivenACornerOnASideAreSplitThereAndStayTriangles()
        {
            // Touching holes are cut into strips, whose triangles meet the edges at points of their own: those points are
            // corners of the triangles on both sides now.
            GeoMesh2 mesh = Touching.ToMesh(MeshKind.Triangles);

            Assert.True(mesh.FaceCount > Touching.TriangulateSurface(Tolerance.Global).Length);
            Assert.All(mesh.GetFaces(), f => Assert.Equal(3, f.VertexCount));
            MeshAssert.EdgeToEdge(mesh, Tolerance);
        }

        [Fact]
        public void AConvexShapeIsOnePiece()
        {
            var hexagon = new GeoPolygon2(Enumerable.Range(0, 6).Select(i => P(1000 * Math.Cos(i * Math.PI / 3), 1000 * Math.Sin(i * Math.PI / 3))));
            GeoMesh2 mesh = hexagon.ToMesh(MeshKind.Convex);

            Assert.Equal(1, mesh.FaceCount);
            Assert.Equal(6, mesh.VertexCount);
            Assert.Equal(hexagon.Area, mesh.Area, 6);

            var disc = new GeoCircle2(P(10, 20), 500);
            Assert.Equal(1, disc.ToMesh(MeshKind.Convex).FaceCount);
        }

        [Fact]
        public void AnLIsTwoConvexPieces()
        {
            GeoMesh2 mesh = L.ToMesh(MeshKind.Convex);

            Assert.Equal(2, mesh.FaceCount);
            MeshAssert.IsSound(mesh, p => L.Locate(p, Tolerance), L.Area, Tolerance);
        }

        [Fact]
        public void HertelAndMehlhornLeaveFewPiecesAroundHoles()
        {
            // A square with a square hole needs four convex pieces at the least; the merge leaves no more than four times
            // that, and here the triangles of ear clipping merge into very few.
            var frame = new GeoFace2(Box(0, 0, 1000, 1000), new[] { Box(300, 300, 700, 700) });
            GeoMesh2 mesh = frame.ToMesh(MeshKind.Convex);

            MeshAssert.IsSound(mesh, p => frame.Locate(p, Tolerance), frame.Area, Tolerance);
            Assert.True(mesh.FaceCount >= 4 && mesh.FaceCount <= 8, $"{mesh.FaceCount} pieces");
        }

        [Fact]
        public void StripsTurnedByAnAngleRunThatWay()
        {
            double angle = Math.PI / 4;
            GeoMesh2 mesh = Slab.ToMesh(MeshOptions.Strips(angle), Tolerance);

            MeshAssert.IsSound(mesh, p => Slab.Locate(p, Tolerance), Slab.Area, Tolerance);

            // Measured across the strips, every corner of a piece stands on one of the lines through the slab's corners.
            var across = new GeoVector2(-Math.Sin(angle), Math.Cos(angle));
            double[] lines = Slab.Boundary.Vertices.Concat(Slab.Holes.SelectMany(h => h.Vertices)).Select(p => p.X * across.X + p.Y * across.Y).ToArray();

            foreach (GeoPoint2 vertex in mesh.Vertices)
            {
                double at = vertex.X * across.X + vertex.Y * across.Y;
                Assert.Contains(lines, line => Math.Abs(line - at) <= 1E-6);
            }
        }

        [Fact]
        public void AnLInStripsIsTwoBandsMeetingEdgeToEdge()
        {
            GeoMesh2 mesh = L.ToMesh(MeshKind.Strips);

            Assert.Equal(2, mesh.FaceCount);
            MeshAssert.IsSound(mesh, p => L.Locate(p, Tolerance), L.Area, Tolerance);

            // The lower band's top runs past the corner of the L the upper band stands on, which is one of its corners now.
            Assert.Contains(mesh.GetFaces(), f => f.VertexCount == 5 && f.Vertices.Contains(P(1000, 1000)));
        }

        [Fact]
        public void APolygonCrossingItselfIsMeshedAsMakeValidReadsIt()
        {
            var bowTie = new GeoPolygon2(P(0, 0), P(1000, 1000), P(1000, 0), P(0, 1000));
            double area = bowTie.MakeValid().Sum(p => p.Area);

            foreach (MeshKind kind in new[] { MeshKind.Triangles, MeshKind.Convex, MeshKind.Strips })
            {
                GeoMesh2 mesh = bowTie.ToMesh(kind);
                Assert.Equal(area, mesh.Area, 6);
                MeshAssert.EdgeToEdge(mesh, Tolerance);
            }

            Assert.Equal(area, bowTie.ToMesh(MeshOptions.Grid(300, 300), Tolerance).Area, 6);
        }

        [Fact]
        public void ADiscInTrianglesIsFannedFromItsCenter()
        {
            var disc = new GeoCircle2(P(100, -50), 800);
            GeoMesh2 mesh = disc.ToMesh(new MeshOptions(MeshKind.Triangles, chordTolerance: 0.5), Tolerance);
            GeoTriangle2[] fan = disc.TriangulateSurface(0.5, Tolerance);

            Assert.Equal(fan.Length, mesh.FaceCount);
            Assert.Equal(fan.Length + 1, mesh.VertexCount);
            Assert.Contains(P(100, -50), mesh.Vertices);
            Assert.Equal(fan.Sum(t => t.Area), mesh.Area, 6);
            MeshAssert.EdgeToEdge(mesh, Tolerance);
        }

        [Fact]
        public void NoAreaIsNoFacesOfAnyKind()
        {
            var flat = new GeoPolygon2(P(0, 0), P(100, 0), P(200, 0));

            foreach (MeshKind kind in new[] { MeshKind.Triangles, MeshKind.Convex, MeshKind.Strips })
            {
                Assert.Equal(0, flat.ToMesh(kind).FaceCount);
                Assert.Equal(0, new GeoRectangle2(P(0, 0), 0, 10).ToMesh(kind).FaceCount);
                Assert.Equal(0, new GeoCircle2(P(0, 0), 0).ToMesh(kind).FaceCount);
            }
        }
    }
}
