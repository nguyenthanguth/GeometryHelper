using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face that is small beside the tolerance still has its triangles in the surface mesh of its body, and a face
    /// thinner than it, which is left out by design, is said to be.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoTriangle3.IsDegenerate(Tolerance)"/> reads twice a triangle's area against a length, so a triangle
    /// of 0.002 by 0.002 was called nothing, and the mesh of a body that was closed was left open: a box with a void
    /// 0.002 from its corner has a corner of 0.002 on every side left over. A face thinner than the tolerance, as the
    /// side of a slab 0.0008 thick is at 0.001, is a seam at that tolerance and has no triangle, which the ObjWriter
    /// tests pin by design; what was missing is that nothing said so.
    /// </remarks>
    public class SurfaceMeshSmallFacesTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        /// <summary>The tolerance the mesh is turned into faces with, so that no small triangle is refused as collinear.</summary>
        private static readonly Tolerance Fine = new Tolerance(1E-12, 1E-15);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static bool MeshIsClosed(IEnumerable<GeoTriangle3> triangles)
            => new GeoSolid3(triangles.Select(triangle => triangle.ToFace3(Fine))).IsClosed(Tolerance);

        /// <summary>Runs an action and gives back what it wrote about the mesh, as the level and the line.</summary>
        private static List<string> Written(Action action)
        {
            var written = new List<string>();
            bool enabled = GeometryHelperLog.Enable;
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (message.Contains("GeoSolid3.Triangulate"))
                {
                    written.Add(level + ": " + message);
                }
            };

            try
            {
                action();
            }
            finally
            {
                GeometryHelperLog.Writer = null;
                GeometryHelperLog.Enable = enabled;
            }

            return written;
        }

        /// <summary>A slab 100 by 100 by 0.0008, made of six polygons within 1E-5 and no coarser.</summary>
        private static GeoSolid3 Slab(double hair)
        {
            var within = new Tolerance(1E-5, 1E-5);
            const double height = 0.0008;
            var corners = new GeoPoint3[8];

            for (int i = 0; i < 8; i++)
            {
                // Each corner stirred by up to 5E-6 either way, as a modeller leaves them.
                double stir(int axis) => hair * (((i * 7 + axis * 3) % 5) - 2) / 4.0;
                corners[i] = new GeoPoint3((i & 1) * 100.0 + stir(0), ((i >> 1) & 1) * 100.0 + stir(1), ((i >> 2) & 1) * height + stir(2));
            }

            GeoFace3 Face(params int[] at) => new GeoFace3(new GeoPolygon3(at.Select(i => corners[i]).ToArray(), within), null, within);

            return new GeoSolid3(new[] { Face(0, 2, 3, 1), Face(4, 5, 7, 6), Face(0, 1, 5, 4), Face(1, 3, 7, 5), Face(3, 2, 6, 7), Face(2, 0, 4, 6) });
        }

        [Theory]
        [InlineData(1E-5, 20.0)]
        [InlineData(1E-4, 20.0)]
        [InlineData(9.9E-4, 20.0)]
        [InlineData(1.01E-3, 20.0)]
        [InlineData(2E-3, 20.0)]
        [InlineData(3E-3, 20.0)]
        [InlineData(5E-3, 20.0)]
        [InlineData(0.1, 20.0)]
        [InlineData(1.0, 20.0)]
        [InlineData(1E-4, 0.01)]
        [InlineData(1.01E-3, 0.01)]
        [InlineData(2E-3, 0.01)]
        [InlineData(5E-3, 0.01)]
        [InlineData(0.01, 0.1)]
        [InlineData(2E-3, 38.0)]
        [InlineData(3E-3, 38.0)]
        public void AVoidSetBackFromTheCornerOfABox_LeavesItsSurfaceMeshClosed(double setBack, double size)
        {
            GeoSolid3 body = Box(0, 0, 0, 100, 60, 40)
                .WithOpenings(new[] { Box(setBack, setBack, setBack, setBack + size, setBack + size, setBack + size) });

            Assert.True(body.TryCutOpenings(out GeoSolid3 material, Tolerance));
            GeoTriangle3[] mesh = body.TriangulateSurface(Tolerance);

            Assert.True(MeshIsClosed(mesh));
            Assert.Equal(material.Faces.Sum(face => face.Area), mesh.Sum(triangle => triangle.Area), 6);
        }

        [Fact]
        public void ABoxWithAVoidTwoThousandthsFromItsCorner_MeshesToTheAreaOfItsSixSidesAndTheVoidsSix()
        {
            GeoSolid3 body = Box(0, 0, 0, 100, 60, 40).WithOpenings(new[] { Box(2E-3, 2E-3, 2E-3, 20.002, 20.002, 20.002) });

            GeoTriangle3[] mesh = body.TriangulateSurface(Tolerance);

            // The box has 2 * (100 * 60 + 100 * 40 + 60 * 40) = 24 800 square units of side and the void 6 * 20 * 20 = 2 400,
            // with walls 0.002 thick between them: no triangle of that skin is dropped for being small.
            Assert.Equal(27200.0, mesh.Sum(triangle => triangle.Area), 6);
            Assert.True(MeshIsClosed(mesh));
        }

        [Fact]
        public void AFaceTwoThousandthsSquare_IsItsTwoTriangles()
        {
            // Built within 1E-12: a polygon of 4E-6 square units is refused as collinear by a vector tolerance of 1E-5.
            var small = new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(2E-3, 0, 0), new GeoPoint3(2E-3, 2E-3, 0), new GeoPoint3(0, 2E-3, 0) }, Fine), null, Fine);

            GeoTriangle3[] mesh = small.TriangulateSurface(Tolerance);

            Assert.Equal(2, mesh.Length);
            Assert.Equal(4E-6, mesh.Sum(triangle => triangle.Area), 12);
        }

        [Fact]
        public void ATriangleOfASmallFace_HasItsNormalInItsAreaVector()
        {
            var small = new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(2E-3, 0, 0), new GeoPoint3(2E-3, 2E-3, 0), new GeoPoint3(0, 2E-3, 0) }, Fine), null, Fine);

            GeoTriangle3 triangle = small.TriangulateSurface(Tolerance)[0];

            // Its height over its longest side, 0.0014, is more than the tolerance: it is a triangle, with its normal.
            Assert.False(triangle.IsDegenerate(Tolerance));
            Assert.Equal(GeoVector3.ZAxis, triangle.Normal);
            Assert.Equal(4E-6, triangle.GetAreaVector().Z, 12);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E-5)]
        public void ASlabThinnerThanTheTolerance_IsMeshedWholeWithinTheToleranceItWasBuiltWithin(double hair)
        {
            GeoTriangle3[] mesh = Slab(hair).TriangulateSurface(new Tolerance(1E-5, 1E-5));

            // Two triangles for each of the six faces; the four sides, 100 by 0.0008, are 0.32 of the 20 000.32 square units.
            Assert.Equal(12, mesh.Length);
            Assert.Equal(20000.32, mesh.Sum(triangle => triangle.Area), 2);
            Assert.True(MeshIsClosed(mesh));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E-5)]
        public void ASlabThinnerThanTheTolerance_SaysSoOnceWhenItsSidesAreLeftOutOfItsMesh(double hair)
        {
            GeoSolid3 slab = Slab(hair);
            GeoTriangle3[] mesh = null;

            List<string> written = Written(() => mesh = slab.TriangulateSurface(Tolerance));

            // By design the four sides, 0.0008 high, are a seam at a tolerance of 0.001 and the 4 triangles of the two big
            // faces are what is left; the call says once, as a Debug line, that it left 4 of the 6 faces, 0.32 square
            // units, out whole.
            Assert.Equal(4, mesh.Length);
            string line = Assert.Single(written);
            Assert.StartsWith("Debug: ", line);
            Assert.Contains("4 of 6 faces, 0.32", line);
            Assert.Empty(Written(() => slab.TriangulateSurface(new Tolerance(1E-5, 1E-5))));
        }

        [Fact]
        public void TheLineAboutLeftOutFaces_IsWrittenInTheInvariantCulture()
        {
            GeoSolid3 slab = Slab(0.0);
            CultureInfo before = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

                string line = Assert.Single(Written(() => slab.TriangulateSurface(Tolerance)));

                Assert.Contains("faces, 0.32", line);
                Assert.Contains("tolerance of 0.001,", line);
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        [Fact]
        public void TwoBodiesATenthousandthOffTheirPlanes_AreCutPreparedAndMeasuredWithoutAWordAboutTheMesh()
        {
            // The two blocks, turned together 0.7 radians, have a long sliver of a face between them that is thinner than the
            // tolerance: the booleans, the preparing of a body and its volume mesh it themselves, and have nothing to say.
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(10, 30, 20), new GeoVector3(0.3, -0.2, 1), 0.7);
            GeoSolid3 a = Box(0, 0, 0, 20, 60, 40).TransformBy(turn);
            GeoSolid3 b = Box(17.7, -0.0001, 0, 37.7, -0.0001 + 59.9991, 40).TransformBy(turn);
            GeoSolid3 pierced = a.WithOpenings(new[] { b });

            List<string> written = Written(() =>
            {
                a.TryUnion(b, out _, Tolerance);
                a.TrySubtract(b, out _, Tolerance);
                a.TryIntersect(b, out _, Tolerance);
                pierced.GetVolume(Tolerance);
                new GeometryHelper.Spatial.GeoPreparedSolid3(pierced, Tolerance);
                a.CollidesWith(b, Tolerance);
                a.DistanceTo(b, Tolerance);
            });

            Assert.Empty(written);

            // Asked for the mesh itself, the call says what it left out in one line, and says nothing where it left nothing out.
            Assert.Contains("1 of 8 faces", Assert.Single(Written(() => pierced.TriangulateSurface(Tolerance))));
            Assert.Empty(Written(() => a.TriangulateSurface(Tolerance)));
        }

        [Fact]
        public void ASlabThinnerThanTheTolerance_IsAskedCollisionDistanceRaysAndProjectionsWithoutAWordAboutTheMesh()
        {
            // The slab's four sides are left out of its mesh whole at 0.001, which asking the mesh itself says; the questions
            // that mesh the body for themselves have nothing to tell the caller, whichever of them it is.
            GeoSolid3 slab = Slab(0.0);
            GeoSolid3 crossing = slab.Translate(new GeoVector3(10, 10, 0.0004));
            GeoSolid3 above = slab.Translate(new GeoVector3(0, 0, 5));
            GeoPoint3 over = new GeoPoint3(50, 50, 3);

            List<string> written = Written(() =>
            {
                Collision3.CollidesWith(slab, crossing, Tolerance);
                Distance3.DistanceTo(slab, above, Tolerance);
                Intersection3.GetIntersections(new GeoRay3(new GeoPoint3(50, 50, -5), GeoVector3.ZAxis), slab, Tolerance);
                Projection3.GetShortestLineTo(slab, new GeoLine3(over, new GeoPoint3(60, 50, 3)), Tolerance);
                Projection3.GetShortestLineTo(slab, new GeoTriangle3(over, new GeoPoint3(60, 50, 3), new GeoPoint3(50, 60, 3)), Tolerance);
                Projection3.GetShortestLineTo(slab, above, Tolerance);
                Projection3.GetShortestLineTo(slab, new GeoPolyline3(over, new GeoPoint3(60, 50, 3), new GeoPoint3(60, 60, 3)), Tolerance);
                Projection3.GetShortestLineTo(slab, new GeoPolygon3(over, new GeoPoint3(60, 50, 3), new GeoPoint3(60, 60, 3), new GeoPoint3(50, 60, 3)), Tolerance);
                Projection3.GetShortestLineTo(slab, new GeoPlane3(over, GeoVector3.ZAxis), Tolerance);
                Projection3.GetShortestLineTo(slab, new GeoRay3(over, GeoVector3.XAxis), Tolerance);
                Projection3.GetShortestLineTo(slab, new GeoFace3(new GeoPolygon3(over, new GeoPoint3(60, 50, 3), new GeoPoint3(60, 60, 3), new GeoPoint3(50, 60, 3))), Tolerance);
            });

            Assert.Empty(written);
            Assert.Single(Written(() => slab.TriangulateSurface(Tolerance)));
        }

        [Fact]
        public void ABoundaryThatMeetsItselfWithAPieceThreeThousandthsSquare_IsMeshedByStripsWithNoPieceDropped()
        {
            // The hole shares two sides with the boundary, so the face is cut into strips; the L left is 2.7E-5 square units
            // in pieces of 0.003 by 0.003 and 0.003 by 0.006, whose doubled areas, 9E-6 and 1.8E-5, a length tolerance of
            // 1E-5 read as nothing for the first.
            var face = new GeoFace3(
                new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0.006, 0, 0), new GeoPoint3(0.006, 0.006, 0), new GeoPoint3(0, 0.006, 0) }, Fine),
                new[] { new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0.003, 0, 0), new GeoPoint3(0.003, 0.003, 0), new GeoPoint3(0, 0.003, 0) }, Fine) },
                Fine);

            GeoTriangle3[] mesh = face.TriangulateSurface(Tolerance);

            Assert.Equal(2.7E-5, mesh.Sum(triangle => triangle.Area), 12);
        }

        [Theory]
        [InlineData(100.0, 100.0, false)]
        [InlineData(100.0, 0.0008, false)]
        [InlineData(0.002, 0.002, false)]
        [InlineData(100.0, 1E-10, true)]
        [InlineData(100.0, 0.0, true)]
        public void ATriangleIsNothingOnlyWhenItsNarrowestWidthIsAMillionthOfTheTolerance(double length, double width, bool collapsed)
        {
            // A right triangle with its long leg along X, its narrowest width the altitude to the hypotenuse.
            var triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(length, 0, 0), new GeoPoint3(length, width, 0));

            Assert.Equal(collapsed, EarClipping.IsCollapsed(triangle, Tolerance));
        }

        [Fact]
        public void ASliverWithNoWidthAtAll_IsStillLeftOutOfTheMesh()
        {
            var within = new Tolerance(1E-12, 1E-12);
            var needle = new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 1E-10, 0), new GeoPoint3(0, 1E-10, 0) }, within), null, within);

            Assert.Empty(needle.TriangulateSurface(Tolerance));
        }
    }
}
