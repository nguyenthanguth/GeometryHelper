using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GeometryHelper.Clash;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The splitting of a mesh into bodies for the clash check: a body the library closes and the edges leave open, bodies that run slightly into each
    /// other along an edge, a shell inside out with nothing round it, loose faces in an OBJ, and the clusters of corners.
    /// </summary>
    public class ImportRoundThreeTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;
        private static readonly ClashOptions Options = new ClashOptions(0.0, true, 1);

        private static GeoPoint3 Q(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoFace3 F(params GeoPoint3[] corners) => new GeoFace3(new GeoPolygon3(corners), null);

        private static GeoSolid3 Plate(double x, double y, double z, double sx, double sy, double sz)
            => GeoSolid3.Extrude(new GeoPolygon3(Q(x, y, z), Q(x + sx, y, z), Q(x + sx, y + sy, z), Q(x, y + sy, z)), new GeoVector3(0, 0, sz));

        private static Scene Dxf(GeoSolid3 a, GeoSolid3 b)
            => SceneImport.ReadText(new DxfWriter().Add(a, Tol, "steel").Add(b, Tol, "steel").ToString(), "dxf");

        private static Scene Obj(GeoSolid3 a, GeoSolid3 b)
            => SceneImport.ReadText(new ObjWriter().Add(a, Tol, "a").Add(b, Tol, "b").ToString(), "obj");

        private static DiagnosticReport Clash(Scene scene) => Diag.Run(scene, DiagnosticChecks.Clash, true);

        #region A body the library closes and the edges leave open

        [Fact]
        public void ABodyWithATJunction_IsABodyToTheLibrary_AndClashesAsItDoes()
        {
            // 20 by 10 by 10; the top is two faces meeting at x = 10 while the front and the back are single quads, so two edges are whole on one side and cut at
            // the middle on the other.
            var tj = new GeoSolid3(new[]
            {
                F(Q(0, 0, 0), Q(0, 10, 0), Q(20, 10, 0), Q(20, 0, 0)),
                F(Q(0, 0, 10), Q(10, 0, 10), Q(10, 10, 10), Q(0, 10, 10)),
                F(Q(10, 0, 10), Q(20, 0, 10), Q(20, 10, 10), Q(10, 10, 10)),
                F(Q(0, 0, 0), Q(20, 0, 0), Q(20, 0, 10), Q(0, 0, 10)),
                F(Q(20, 10, 0), Q(0, 10, 0), Q(0, 10, 10), Q(20, 10, 10)),
                F(Q(20, 0, 0), Q(20, 10, 0), Q(20, 10, 10), Q(20, 0, 10)),
                F(Q(0, 10, 0), Q(0, 0, 0), Q(0, 0, 10), Q(0, 10, 10)),
            });
            GeoSolid3 other = Plate(15, 2, 2, 10, 5, 5);
            ClashResult expected = Clash3.Find(new[] { tj }, new[] { other }, Options, Tol).Single();

            Assert.True(tj.IsClosed(Tol));
            Assert.Equal(125.0, expected.Volume, 6);

            foreach (Scene scene in new[] { Dxf(tj, other), Obj(tj, other) })
            {
                Assert.Equal(2, scene.Items.Count);
                DiagnosticFinding clash = Assert.Single(Diag.Of(Clash(scene), DiagnosticKind.HardClash));
                Assert.Equal(expected.Volume, clash.Size, 6);
                Assert.Empty(Diag.Of(Clash(scene), DiagnosticKind.Note));
            }
        }

        #endregion

        #region Bodies that run into each other along an edge

        private static GeoSolid3 LeaningBody(double x)
            => new GeoSolid3(new[]
            {
                F(Q(10, 0, 0), Q(10, 10, 0), Q(20, 10, 0), Q(20, 0, 0)),
                F(Q(10, 0, 10), Q(20, 0, 10), Q(20, 10, 10), Q(x, 10, 10)),
                F(Q(10, 0, 0), Q(20, 0, 0), Q(20, 0, 10), Q(10, 0, 10)),
                F(Q(20, 10, 0), Q(10, 10, 0), Q(x, 10, 10), Q(20, 10, 10)),
                F(Q(20, 0, 0), Q(20, 10, 0), Q(20, 10, 10), Q(20, 0, 10)),
                F(Q(10, 0, 0), Q(10, 0, 10), Q(x, 10, 10)),
                F(Q(10, 0, 0), Q(x, 10, 10), Q(10, 10, 0)),
            });

        [Fact]
        public void TwoBodiesThatRunIntoEachOtherAlongAnEdge_AreTwoItems_AndAHardClashAsTheLibrarySays()
        {
            // B lies against A at x = 10, but one corner of its touching side is 0.01 into A.
            GeoSolid3 a = Plate(0, 0, 0, 10, 10, 10);
            GeoSolid3 b = LeaningBody(9.99);
            ClashResult expected = Clash3.Find(new[] { a }, new[] { b }, Options, Tol).Single();

            Assert.Equal(ClashKind.Hard, expected.Kind);
            Assert.Equal(1.0 / 3.0, expected.Volume, 6);

            foreach (Scene scene in new[] { Dxf(a, b), Obj(a, b) })
            {
                Assert.Equal(2, scene.Items.Count);
                DiagnosticFinding clash = Assert.Single(Diag.Of(Clash(scene), DiagnosticKind.HardClash));
                Assert.Equal(expected.Volume, clash.Size, 6);
                Assert.Contains("0.009999995 deep", clash.Message);
            }
        }

        [Fact]
        public void TwoBodiesThatLeanAwayFromEachOtherAlongAnEdge_AreTwoItems_AndTouch()
        {
            GeoSolid3 a = Plate(0, 0, 0, 10, 10, 10);
            GeoSolid3 b = LeaningBody(10.01);

            foreach (Scene scene in new[] { Dxf(a, b), Obj(a, b) })
            {
                Assert.Equal(2, scene.Items.Count);
                DiagnosticFinding touch = Assert.Single(Diag.Of(Clash(scene), DiagnosticKind.TouchingBodies));
                Assert.Equal(0.0, touch.Size);
                Assert.Empty(Diag.Of(Clash(scene), DiagnosticKind.HardClash));
            }
        }

        #endregion

        #region An inside-out shell with nothing round it

        [Fact]
        public void AnInsideOutShellWithNoBodyRoundIt_IsNeverABody_AndNoClashIsMadeUp()
        {
            // 100 by 60 by 40 with a void 20 across 2E-3 from its corner, and a block 5 across in the void. The library's mesh of
            // this body closes since its surface mesh keeps the 0.002 corner faces, so the void is read as the void of the body.
            GeoSolid3 hollow = Plate(0, 0, 0, 100, 60, 40).WithOpenings(new[] { Plate(2E-3, 2E-3, 2E-3, 20, 20, 20) });
            GeoSolid3 block = Plate(5, 5, 5, 5, 5, 5);

            Assert.Empty(Clash3.Find(new[] { hollow }, new[] { block }, Options, Tol));

            foreach (Scene scene in new[] { Dxf(hollow, block), Obj(hollow, block) })
            {
                DiagnosticReport report = Clash(scene);

                Assert.Empty(Diag.Of(report, DiagnosticKind.HardClash));
                Assert.Empty(Diag.Of(report, DiagnosticKind.TouchingBodies));
            }
        }

        [Fact]
        public void AnInsideOutBodyInAnotherOnOneLayer_IsTakenAsItsVoid_AndOnItsOwnLayerIsLeftOut()
        {
            GeoSolid3 outer = Plate(0, 0, 0, 100, 60, 40);
            var shell = outer.TriangulateSurface(Tol).ToList();
            GeoSolid3 inner = Plate(40, 20, 10, 10, 10, 10);
            var inside = inner.TriangulateSurface(Tol).Select(t => new GeoTriangle3(t.A, t.C, t.B)).ToList();

            Scene one = SceneImport.ReadText(new DxfWriter().Add(shell.Concat(inside).ToList(), "steel").ToString(), "dxf");
            Scene two = SceneImport.ReadText(new DxfWriter().Add(shell, "steel").Add(inside, "bolt").ToString(), "dxf");

            Assert.Equal(24, Imp.MeshFaces(Assert.Single(one.Items)).Count);
            Assert.Equal(2, two.Items.Count);
            Assert.Contains(Diag.Of(Clash(two), DiagnosticKind.Note), n => n.Message.Contains("an inside-out shell with no body round it"));
        }

        #endregion

        #region Loose faces in an OBJ

        [Fact]
        public void LooseTrianglesInOneObject_AreOneItem_AndAClosedBodyAmongThemIsAnotherItem()
        {
            var grid = new List<GeoTriangle3>();

            for (int i = 0; i < 50; i++)
            {
                grid.Add(new GeoTriangle3(Q(i * 2, 0, 0), Q(i * 2 + 1, 0, 0), Q(i * 2, 1, 0)));
            }

            Scene loose = SceneImport.ReadText(new ObjWriter().Add(grid, "dots").ToString(), "obj");
            Assert.Equal(50, Imp.MeshFaces(Assert.Single(loose.Items)).Count);

            List<GeoTriangle3> mixed = Plate(0, 0, 100, 10, 10, 10).TriangulateSurface(Tol).Concat(grid).ToList();
            Scene both = SceneImport.ReadText(new ObjWriter().Add(mixed, "soup").ToString(), "obj");

            Assert.Equal(new[] { "soup", "soup 2" }, both.Items.Select(i => i.Name).ToArray());
            Assert.Equal(new[] { 12, 50 }, both.Items.Select(i => Imp.MeshFaces(i).Count).ToArray());
        }

        [Fact]
        public void AnOpenBodyInAnObjectWithAClosedOne_IsOneItemOfItsOwn_NotFiveItems()
        {
            List<GeoTriangle3> open = Plate(0, 0, 0, 10, 10, 10).TriangulateSurface(Tol).Take(5).ToList();
            List<GeoTriangle3> closed = Plate(50, 0, 0, 10, 10, 10).TriangulateSurface(Tol).ToList();
            Scene scene = SceneImport.ReadText(new ObjWriter().Add(open.Concat(closed), "soup").ToString(), "obj");

            Assert.Equal(new[] { 5, 12 }, scene.Items.Select(i => Imp.MeshFaces(i).Count).ToArray());
        }

        #endregion

        #region Clusters

        [Fact]
        public void CornersSpreadOverAboutTheTolerance_ClusterTheSameWhateverOrderTheFileIsIn()
        {
            // The triangles of two boxes lying face to face, each corner moved by up to 0.0004 in each way: copies of one corner are not all within 0.001 of each
            // other, and what joins them must not depend on which comes first.
            var random = new Random(11);
            var triangles = new List<GeoTriangle3>();

            foreach (GeoSolid3 box in new[] { Plate(0, 0, 0, 10, 10, 10), Plate(10, 0, 0, 10, 10, 10), Plate(0, 0, 10, 20, 10, 5) })
            {
                foreach (GeoTriangle3 t in box.TriangulateSurface(Tol))
                {
                    GeoPoint3 Move(GeoPoint3 p) => Q(p.X + ((random.NextDouble() - 0.5) * 8E-4), p.Y + ((random.NextDouble() - 0.5) * 8E-4), p.Z + ((random.NextDouble() - 0.5) * 8E-4));
                    triangles.Add(new GeoTriangle3(Move(t.A), Move(t.B), Move(t.C)));
                }
            }

            string Summary(List<GeoTriangle3> order)
            {
                var points = new List<Vec3>();
                var faces = new List<int[]>();

                foreach (GeoTriangle3 t in order)
                {
                    faces.Add(new[] { points.Count, points.Count + 1, points.Count + 2 });
                    points.Add(new Vec3(t.A.X, t.A.Y, t.A.Z));
                    points.Add(new Vec3(t.B.X, t.B.Y, t.B.Z));
                    points.Add(new Vec3(t.C.X, t.C.Y, t.C.Z));
                }

                return string.Join(";", MeshShells.Split(points, faces, 1E-3, CancellationToken.None)
                    .Select(p => p.Closed.ToString() + ":" + p.Faces.Count + ":" + p.Voids.Count)
                    .OrderBy(s => s, StringComparer.Ordinal));
            }

            string expected = Summary(triangles);
            var shuffler = new Random(5);

            for (int k = 0; k < 20; k++)
            {
                Assert.Equal(expected, Summary(triangles.OrderBy(_ => shuffler.Next()).ToList()));
            }

        }

        #endregion
    }
}
