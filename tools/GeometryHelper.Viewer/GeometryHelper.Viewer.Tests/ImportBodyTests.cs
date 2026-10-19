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
    /// The bodies of a mesh read from a file are told apart by their edges, within the tolerance, and not by the corners their triangles share: a body with a
    /// void in it is one item, two bodies lying against each other are two, a corner a hair off still closes a body, and the clash check gives the numbers the
    /// library gives for the bodies that were written.
    /// </summary>
    public class ImportBodyTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;
        private static readonly ClashOptions Options = new ClashOptions(0.0, true, 1);

        private static GeoSolid3 Plate(double x, double y, double z, double sx, double sy, double sz)
            => GeoSolid3.Extrude(
                new GeoPolygon3(new GeoPoint3(x, y, z), new GeoPoint3(x + sx, y, z), new GeoPoint3(x + sx, y + sy, z), new GeoPoint3(x, y + sy, z)),
                new GeoVector3(0, 0, sz));

        private static Scene Dxf(params (GeoSolid3 Body, string Layer)[] bodies)
        {
            var writer = new DxfWriter();

            foreach ((GeoSolid3 body, string layer) in bodies)
            {
                writer.Add(body, Tol, layer);
            }

            return SceneImport.ReadText(writer.ToString(), "dxf");
        }

        private static Scene Obj(params (GeoSolid3 Body, string Name)[] bodies)
        {
            var writer = new ObjWriter();

            foreach ((GeoSolid3 body, string name) in bodies)
            {
                writer.Add(body, Tol, name);
            }

            return SceneImport.ReadText(writer.ToString(), "obj");
        }

        private static DiagnosticReport Clash(Scene scene, params int[] items) => Diag.Run(scene, DiagnosticChecks.Clash, true, items);

        private static ClashResult[] Library(GeoSolid3 a, GeoSolid3 b) => Clash3.Find(new[] { a }, new[] { b }, Options, Tol);

        private static GeoSolid3 Hollow() => Plate(0, 0, 0, 100, 60, 40).WithOpenings(new[] { Plate(30, 20, 10, 20, 20, 20) });

        #region A body with a void

        [Fact]
        public void ABodyWithAVoidInIt_IsOneItem_InADxfAndInAnObj()
        {
            GeoSolid3 hollow = Hollow();

            foreach (Scene scene in new[] { Dxf((hollow, "steel")), Obj((hollow, "hollow")) })
            {
                SceneItem item = Imp.Only(scene);
                List<ShellPiece> pieces = MeshShells.Split(Imp.MeshPoints(item), GeometryDecoder.Faces(item.Data).ToList(), 1E-3, CancellationToken.None);

                Assert.Equal("GeoMesh3", item.Kind);
                ShellPiece piece = Assert.Single(pieces);
                Assert.True(piece.Closed);
                Assert.Single(piece.Voids);
                Assert.Equal(hollow.TriangulateSurface(Tol).Length, piece.Faces.Count);
            }
        }

        [Fact]
        public void ABodyWithAVoidInIt_IsNotAClashWithItsOwnVoid()
        {
            // The void as a body of its own would clash with the walls round it for its whole volume, 8 000; it is not a body of its own.
            foreach (Scene scene in new[] { Dxf((Hollow(), "steel")), Obj((Hollow(), "hollow")) })
            {
                DiagnosticReport report = Clash(scene);

                Assert.Empty(Diag.Of(report, DiagnosticKind.HardClash));
                Assert.Empty(Diag.Of(report, DiagnosticKind.TouchingBodies));
                Assert.Contains(Diag.Of(report, DiagnosticKind.Note), n => n.Message.Contains("needs two bodies"));
            }
        }

        [Fact]
        public void APartInTheVoid_DoesNotClash_AndAPartThroughTheWallDoes_AsTheLibrarySays()
        {
            GeoSolid3 hollow = Hollow();
            GeoSolid3 inTheVoid = Plate(35, 25, 15, 5, 5, 5);
            GeoSolid3 throughTheWall = Plate(25, 25, 5, 10, 10, 30);

            Assert.Empty(Library(hollow, inTheVoid));
            ClashResult expected = Library(hollow, throughTheWall).Single();
            Assert.Equal(ClashKind.Hard, expected.Kind);

            foreach (string format in new[] { "dxf", "obj" })
            {
                Scene free = format == "dxf" ? Dxf((hollow, "steel"), (inTheVoid, "bolt")) : Obj((hollow, "hollow"), (inTheVoid, "bolt"));
                Scene through = format == "dxf" ? Dxf((hollow, "steel"), (throughTheWall, "bolt")) : Obj((hollow, "hollow"), (throughTheWall, "bolt"));

                Assert.Equal(2, free.Items.Count);
                Assert.Empty(Clash(free).Findings);

                DiagnosticFinding clash = Assert.Single(Diag.Of(Clash(through), DiagnosticKind.HardClash));
                Assert.Equal(expected.Volume, clash.Size, 6);
                Assert.Equal(expected.Location.X, clash.At.X, 6);
                Assert.Equal(expected.Location.Y, clash.At.Y, 6);
                Assert.Equal(expected.Location.Z, clash.At.Z, 6);
                Assert.Contains(expected.Depth.ToString("G10", CultureInfo.InvariantCulture) + " deep", clash.Message);
            }
        }

        [Fact]
        public void AShellThatRunsInsideOutAndHoldsNothing_IsAnItemOfItsOwn()
        {
            GeoTriangle3[] inside = Plate(0, 0, 0, 10, 10, 10).TriangulateSurface(Tol);
            GeoTriangle3[] turned = inside.Select(t => new GeoTriangle3(t.A, t.C, t.B)).ToArray();
            Scene scene = SceneImport.ReadText(new ObjWriter().Add(turned, "inside out").ToString(), "obj");
            SceneItem item = Imp.Only(scene);
            ShellPiece piece = Assert.Single(MeshShells.Split(Imp.MeshPoints(item), GeometryDecoder.Faces(item.Data).ToList(), 1E-3, CancellationToken.None));

            Assert.True(piece.Closed);
            Assert.Empty(piece.Voids);
        }

        [Fact]
        public void AVoidIsGivenToTheSmallestBodyThatHoldsIt()
        {
            // A box 100 across with a box 60 across in it as a void, and a box 20 across in that, as a body: three shells, and the void goes to the 100.
            GeoSolid3 big = Plate(0, 0, 0, 100, 100, 100).WithOpenings(new[] { Plate(20, 20, 20, 60, 60, 60) });
            GeoSolid3 small = Plate(40, 40, 40, 20, 20, 20);
            Scene scene = Dxf((big, "steel"), (small, "steel"));

            Assert.Equal(2, scene.Items.Count);
            Assert.Equal(new[] { big.TriangulateSurface(Tol).Length, 12 }, scene.Items.Select(i => Imp.MeshFaces(i).Count).ToArray());
            Assert.Empty(Clash(scene).Findings);
        }

        #endregion

        #region A corner a hair off

        private static GeoSolid3 HairBox(double e)
        {
            const double h = 20.0;
            GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

            return new GeoSolid3(new[]
            {
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(0, 60, 0), P(100, 60, 0), P(100, 0, 0)), null),
                new GeoFace3(new GeoPolygon3(P(0, 0, h + e), P(100, 0, h + e), P(100, 60, h + e), P(0, 60, h + e)), null),
                new GeoFace3(new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 0, h), P(0, 0, h)), null),
                new GeoFace3(new GeoPolygon3(P(100, 0, 0), P(100, 60, 0), P(100, 60, h), P(100, 0, h)), null),
                new GeoFace3(new GeoPolygon3(P(100, 60, 0), P(0, 60, 0), P(0, 60, h), P(100, 60, h)), null),
                new GeoFace3(new GeoPolygon3(P(0, 60, 0), P(0, 0, 0), P(0, 0, h), P(0, 60, h)), null),
            });
        }

        [Fact]
        public void ABodyWhoseTopIsAHairAboveItsSides_IsOneClosedBody_AndClashesAsTheLibrarySays()
        {
            GeoSolid3 box = HairBox(1E-7);
            GeoSolid3 other = Plate(50, 10, 10, 100, 20, 30);
            ClashResult expected = Library(box, other).Single();

            Assert.True(box.IsClosed(Tol));
            Assert.Equal(10000.0, expected.Volume, 3);

            foreach (Scene scene in new[] { Dxf((box, "steel"), (other, "bolt")), Obj((box, "steel"), (other, "bolt")) })
            {
                Assert.Equal(2, scene.Items.Count);
                SceneItem item = scene.Items[0];
                ShellPiece piece = Assert.Single(MeshShells.Split(Imp.MeshPoints(item), GeometryDecoder.Faces(item.Data).ToList(), 1E-3, CancellationToken.None));
                Assert.True(piece.Closed);

                // The corners are 1E-7 apart, where the library's 1E-3 welds them; the number differs from the library's on the six faces by the 2E-5 of its own healing.
                DiagnosticFinding clash = Assert.Single(Diag.Of(Clash(scene), DiagnosticKind.HardClash));
                Assert.Equal(expected.Volume, clash.Size, 3);
                Assert.InRange(clash.Size, 10000.00005, 10000.00009);
            }
        }

        [Fact]
        public void ACornerAFewThousandthsOff_ClosesAtTheDefaultToleranceAndNotAtAFinerOne()
        {
            GeoSolid3 box = HairBox(5E-4);
            Scene scene = Dxf((box, "steel"));
            SceneItem item = Imp.Only(scene);
            Vec3[] points = Imp.MeshPoints(item);
            List<int[]> faces = GeometryDecoder.Faces(item.Data).ToList();

            Assert.True(Assert.Single(MeshShells.Split(points, faces, 1E-3, CancellationToken.None)).Closed);
            Assert.False(MeshShells.Split(points, faces, 1E-5, CancellationToken.None).All(p => p.Closed));
        }

        #endregion

        #region Bodies that meet

        [Fact]
        public void TwoBodiesSharingAFace_AreTwoItems_AndTheClashIsTheLibrarysTouch()
        {
            GeoSolid3 a = Plate(0, 0, 0, 100, 60, 20);
            GeoSolid3 b = Plate(100, 0, 0, 100, 60, 20);
            ClashResult expected = Library(a, b).Single();

            foreach (Scene scene in new[] { Dxf((a, "steel"), (b, "steel")), Obj((a, "one"), (b, "two")) })
            {
                Assert.Equal(2, scene.Items.Count);
                DiagnosticFinding touch = Assert.Single(Diag.Of(Clash(scene), DiagnosticKind.TouchingBodies));

                Assert.Equal(ClashKind.Touch, expected.Kind);
                Assert.Equal(1200.0, expected.ContactArea, 6);
                Assert.Equal(expected.ContactArea, touch.Size, 6);
                Assert.Empty(Diag.Of(Clash(scene), DiagnosticKind.HardClash));
            }
        }

        [Fact]
        public void ABodyInsideAnotherThatSharesACornerWithIt_IsTwoItems_AndAHardClashOfTheVolumeOfTheSmaller()
        {
            GeoSolid3 big = Plate(0, 0, 0, 100, 60, 20);
            GeoSolid3 small = Plate(0, 0, 0, 50, 30, 10);
            ClashResult expected = Library(big, small).Single();

            foreach (Scene scene in new[] { Dxf((big, "steel"), (small, "steel")), Obj((big, "big"), (small, "small")) })
            {
                Assert.Equal(2, scene.Items.Count);
                DiagnosticFinding clash = Assert.Single(Diag.Of(Clash(scene), DiagnosticKind.HardClash));

                Assert.Equal(15000.0, expected.Volume, 6);
                Assert.Equal(expected.Volume, clash.Size, 6);
            }
        }

        [Fact]
        public void BodiesThatMeetOnlyAlongAnEdge_AreTwoItems_AndTouchWithNoAreaShared()
        {
            GeoSolid3 a = Plate(0, 0, 0, 100, 60, 20);
            GeoSolid3 b = Plate(100, 60, 0, 100, 60, 20);
            Scene scene = Dxf((a, "steel"), (b, "steel"));

            Assert.Equal(2, scene.Items.Count);
            DiagnosticFinding touch = Assert.Single(Diag.Of(Clash(scene), DiagnosticKind.TouchingBodies));
            Assert.Equal(0.0, touch.Size);
        }

        [Fact]
        public void ARowOfBodiesLyingFaceToFace_IsOneItemToABody_InAnyOrderOfTheFile()
        {
            GeoSolid3[] row = { Plate(0, 0, 0, 10, 10, 10), Plate(10, 0, 0, 10, 10, 10), Plate(20, 0, 0, 10, 10, 10), Plate(10, 10, 0, 10, 10, 10) };

            Assert.Equal(4, Dxf(row.Select(b => (b, "steel")).ToArray()).Items.Count);
            Assert.Equal(4, Dxf(row.AsEnumerable().Reverse().Select(b => (b, "steel")).ToArray()).Items.Count);
            Assert.Equal(4, Obj(row.Select((b, i) => (b, "b" + i.ToString(CultureInfo.InvariantCulture))).ToArray()).Items.Count);
        }

        [Fact]
        public void SeveralBodiesInOneObjectOfAnObj_AreSeveralItemsNamedAfterIt()
        {
            GeoTriangle3[] first = Plate(0, 0, 0, 10, 10, 10).TriangulateSurface(Tol);
            GeoTriangle3[] second = Plate(50, 0, 0, 10, 10, 10).TriangulateSurface(Tol);
            Scene scene = SceneImport.ReadText(new ObjWriter().Add(first.Concat(second), "two boxes").ToString(), "obj");

            Assert.Equal(new[] { "two_boxes", "two_boxes 2" }, scene.Items.Select(i => i.Name).ToArray());
            Assert.All(scene.Items, i => Assert.Equal(12, Imp.MeshFaces(i).Count));
        }

        [Fact]
        public void ABodyWithAFaceTurnedOver_IsOneItem_WithTheFaultInIt()
        {
            GeoSolid3 turned = Diag.TurnedBox();
            Scene scene = Dxf((turned, "steel"));
            SceneItem item = Imp.Only(scene);

            Assert.Equal(12, Imp.MeshFaces(item).Count);
            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Edges, true);
            Assert.NotEmpty(Diag.Of(report, DiagnosticKind.InconsistentWinding));
        }

        [Fact]
        public void TheSplittingDoesNotFollowTheOrderOfTheFaces()
        {
            // A box, a box lying against it, and in the first a void: two bodies, however the faces are ordered.
            GeoTriangle3[] triangles = Plate(0, 0, 0, 10, 10, 10).TriangulateSurface(Tol)
                .Concat(Plate(10, 0, 0, 10, 10, 10).TriangulateSurface(Tol))
                .Concat(Plate(2, 2, 2, 3, 3, 3).TriangulateSurface(Tol).Select(t => new GeoTriangle3(t.A, t.C, t.B)))
                .ToArray();
            var points = new List<Vec3>();
            var faces = new List<int[]>();

            foreach (GeoTriangle3 t in triangles)
            {
                faces.Add(new[] { points.Count, points.Count + 1, points.Count + 2 });
                points.Add(new Vec3(t.A.X, t.A.Y, t.A.Z));
                points.Add(new Vec3(t.B.X, t.B.Y, t.B.Z));
                points.Add(new Vec3(t.C.X, t.C.Y, t.C.Z));
            }

            string Summary(List<int[]> order)
            {
                List<ShellPiece> pieces = MeshShells.Split(points, order, 1E-3, CancellationToken.None);
                return string.Join(";", pieces.Select(p => p.Closed + ":" + p.Voids.Count + ":" + p.Faces.Count + ":" + string.Join(",", p.Faces.Select(f => Imp.Key(order[f].Select(i => points[i]))).OrderBy(k => k, StringComparer.Ordinal).Take(1))).OrderBy(x => x, StringComparer.Ordinal));
            }

            List<ShellPiece> forward = MeshShells.Split(points, faces, 1E-3, CancellationToken.None);
            List<int[]> shuffled = faces.OrderBy(f => (f[0] * 7919) % 104729).ToList();

            Assert.Equal(2, forward.Count);
            Assert.All(forward, p => Assert.True(p.Closed));
            Assert.Equal(new[] { 1, 0 }, forward.Select(p => p.Voids.Count).ToArray());
            Assert.Equal(Summary(faces), Summary(shuffled));
        }

        #endregion
    }
}
