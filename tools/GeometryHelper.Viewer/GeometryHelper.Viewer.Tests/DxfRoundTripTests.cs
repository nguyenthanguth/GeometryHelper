using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What DxfWriter writes, read back by the viewer. Points, segments, chains, polygons, circles and rays come back to the bit; an ARC and an ELLIPSE
    /// are kept in the file as degrees and as an end point and a ratio, and come back to a few parts in 1E-14. A shape in space is a mesh of 3DFACEs where
    /// it has a surface, and its triangles come back to the bit.
    /// </summary>
    public class DxfRoundTripTests
    {
        private const double Close = 1E-13;
        private static readonly Tolerance Tol = Tolerance.Default;

        private static Scene Read(DxfWriter writer) => SceneImport.ReadText(writer.ToString(), "dxf");

        private static double[] Flatten(IEnumerable<GeoPoint2> points) => points.SelectMany(Imp.P2).ToArray();

        private static double[] Flatten(IEnumerable<GeoPoint3> points) => points.SelectMany(Imp.P3).ToArray();

        [Fact]
        public void APointInThePlane_IsAPointAtZeroHeight_ToTheBit()
        {
            var point = (GeoPoint2)SampleShapes.Make("GeoPoint2", 1);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(point)));

            Assert.Equal("GeoPoint3", item.Kind);
            Imp.SameBits(Imp.Flat(point), Imp.Of(item));
        }

        [Fact]
        public void APointInSpace_ComesBackToTheBit()
        {
            var point = (GeoPoint3)SampleShapes.Make("GeoPoint3", 1);
            Imp.SameBits(Imp.P3(point), Imp.Of(Imp.Only(Read(new DxfWriter().Add(point)))));
        }

        [Fact]
        public void ASegment_IsALine_ToTheBit()
        {
            var flat = (GeoLine2)SampleShapes.Make("GeoLine2", 2);
            var solid = (GeoLine3)SampleShapes.Make("GeoLine3", 2);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(flat)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(solid)));

            Assert.Equal("GeoLine3", a.Kind);
            Assert.Equal("GeoLine3", b.Kind);
            Imp.SameBits(Imp.Flat(flat.StartPoint).Concat(Imp.Flat(flat.EndPoint)).ToList(), Imp.Of(a));
            Imp.SameBits(Imp.Of(solid), Imp.Of(b));
        }

        [Fact]
        public void ARay_IsARay_ToTheBit()
        {
            var flat = (GeoRay2)SampleShapes.Make("GeoRay2", 3);
            var solid = (GeoRay3)SampleShapes.Make("GeoRay3", 3);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(flat)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(solid)));

            Assert.Equal("GeoRay3", a.Kind);
            Assert.Equal("GeoRay3", b.Kind);
            Imp.SameBits(new[] { flat.Origin.X, flat.Origin.Y, 0.0, flat.Direction.X, flat.Direction.Y, 0.0 }, Imp.Of(a));
            Imp.SameBits(Imp.Of(solid), Imp.Of(b));
        }

        [Fact]
        public void AChainAndAPolygonInThePlane_ComeBackToTheBit()
        {
            var chain = (GeoPolyline2)SampleShapes.Make("GeoPolyline2", 4);
            var polygon = (GeoPolygon2)SampleShapes.Make("GeoPolygon2", 4);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(chain)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(polygon)));

            Assert.Equal("GeoPolyline2", a.Kind);
            Assert.Equal("GeoPolygon2", b.Kind);
            Imp.SameBits(Imp.Of(chain), Imp.Of(a));
            Imp.SameBits(Imp.Of(polygon), Imp.Of(b));
        }

        [Fact]
        public void ARectangleAndATriangle_ArePolygonsOfTheirCorners()
        {
            var rectangle = (GeoRectangle2)SampleShapes.Make("GeoRectangle2", 6);
            var triangle = (GeoTriangle2)SampleShapes.Make("GeoTriangle2", 6);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(rectangle)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(triangle)));

            Assert.Equal("GeoPolygon2", a.Kind);
            Assert.Equal("GeoPolygon2", b.Kind);
            Imp.SameBits(Flatten(rectangle.GetVertices()), Imp.Of(a));
            Imp.SameBits(Flatten(new[] { triangle.A, triangle.B, triangle.C }), Imp.Of(b));
        }

        [Fact]
        public void AChainWithArcs_KeepsItsBulges_ToTheBit_ExceptTheOneOnItsLastVertex()
        {
            var chain = (GeoPolylineArc2)SampleShapes.Make("GeoPolylineArc2", 7);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(chain)));

            Assert.Equal("GeoPolylineArc2", item.Kind);

            // The last vertex of an open chain leaves no edge, so the file carries no bulge for it and the chain comes back with nought.
            var expected = Flatten(chain.Vertices).ToList();
            expected.AddRange(chain.Bulges.Take(chain.Bulges.Count - 1));
            expected.Add(0.0);
            Imp.SameBits(expected, Imp.Of(item));
        }

        [Fact]
        public void ALoopWithArcs_KeepsAllItsBulges_ToTheBit()
        {
            var loop = (GeoPolygonArc2)SampleShapes.Make("GeoPolygonArc2", 7);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(loop)));

            Assert.Equal("GeoPolygonArc2", item.Kind);
            Imp.SameBits(Imp.Of(loop), Imp.Of(item));
        }

        [Fact]
        public void AFace_IsItsLoops_TheOutlineAndEachHole()
        {
            var face = (GeoFace2)SampleShapes.Make("GeoFace2", 8);
            Scene scene = Read(new DxfWriter().Add(face, "plate"));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(2, scene.Items.Count);
            Assert.All(scene.Items, i => Assert.Equal("GeoPolygon2", i.Kind));
            Imp.SameBits(Flatten(face.Boundary.Vertices), Imp.Of(scene.Items[0]));
            Imp.SameBits(Flatten(face.Holes[0].Vertices), Imp.Of(scene.Items[1]));
        }

        [Fact]
        public void ACircle_ComesBackToTheBit()
        {
            var circle = (GeoCircle2)SampleShapes.Make("GeoCircle2", 9);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(circle)));

            Assert.Equal("GeoCircle2", item.Kind);
            Imp.SameBits(Imp.Of(circle), Imp.Of(item));
        }

        [Fact]
        public void AnArc_ComesBackToAFewPartsIn1E14_ItsAnglesHavingBeenDegrees()
        {
            var arc = new GeoArc2(new GeoPoint2(123.456789, -98.7654321), 77.123456789, 0.7, 2.9);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(arc)));

            Assert.Equal("GeoArc2", item.Kind);
            Imp.Near(Imp.Of(arc), Imp.Of(item), Close);
        }

        [Fact]
        public void AClockwiseArc_ComesBackCounterClockwiseFromItsEndToItsStart()
        {
            var arc = new GeoArc2(new GeoPoint2(10.5, 20.25), 33.3, 2.9, 0.7, true);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(arc)));

            Assert.True(arc.SweptAngle < 0.0);
            Imp.Near(new[] { arc.Center.X, arc.Center.Y, arc.Radius, arc.EndAngle, -arc.SweptAngle }, Imp.Of(item), Close);
        }

        [Fact]
        public void AnArcOfAWholeTurn_IsACircle()
        {
            var arc = new GeoArc2(new GeoPoint2(1.5, 2.5), 10.0, 0.5, 0.5);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(arc)));

            Assert.Equal("GeoCircle2", item.Kind);
            Imp.SameBits(new[] { 1.5, 2.5, 10.0 }, Imp.Of(item));
        }

        [Fact]
        public void AnEllipse_ComesBackToAFewPartsIn1E14_ItsAxisHavingBeenAnEndPointAndARatio()
        {
            var flat = (GeoEllipse2)SampleShapes.Make("GeoEllipse2", 3);
            var solid = (GeoEllipse3)SampleShapes.Make("GeoEllipse3", 3);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(flat)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(solid)));

            Assert.Equal("GeoEllipse2", a.Kind);
            Assert.Equal("GeoEllipse3", b.Kind);
            Imp.Near(Imp.Of(flat), Imp.Of(a), Close);
            Imp.Near(Imp.Of(solid), Imp.Of(b), Close);
        }

        [Fact]
        public void AChainAndAPolygonInSpace_ComeBackToTheBit()
        {
            var chain = (GeoPolyline3)SampleShapes.Make("GeoPolyline3", 4);
            var polygon = (GeoPolygon3)SampleShapes.Make("GeoPolygon3", 4);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(chain)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(polygon)));

            Assert.Equal("GeoPolyline3", a.Kind);
            Assert.Equal("GeoPolygon3", b.Kind);
            Imp.SameBits(Imp.Of(chain), Imp.Of(a));
            Imp.SameBits(Imp.Of(polygon), Imp.Of(b));
        }

        [Fact]
        public void ATriangleInSpace_IsOne3DFace()
        {
            var triangle = (GeoTriangle3)SampleShapes.Make("GeoTriangle3", 5);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(triangle, "tri")));

            Assert.Equal("GeoMesh3", item.Kind);
            Assert.Equal(new[] { Imp.Key(triangle.A, triangle.B, triangle.C) }, Imp.Keys(item));
        }

        [Fact]
        public void AFaceInSpace_IsTheMeshOfItsTriangles()
        {
            var face = (GeoFace3)SampleShapes.Make("GeoFace3", 5);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(face, Tol)));

            Assert.Equal(Imp.Keys(face.TriangulateSurface(Tol)).OrderBy(k => k, StringComparer.Ordinal).ToList(), Imp.Keys(item).OrderBy(k => k, StringComparer.Ordinal).ToList());
        }

        [Fact]
        public void ABodyWithAnOpening_IsOneMeshOfTheTrianglesWhereItsMaterialEnds()
        {
            GeoSolid3 solid = SampleShapes.Box(30.0, 20.0, 10.0, true);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(solid, Tol, "box")));
            List<string> expected = Imp.Keys(solid.TriangulateSurface(Tol)).OrderBy(k => k, StringComparer.Ordinal).ToList();

            Assert.Equal("GeoMesh3", item.Kind);
            Assert.Equal(expected, Imp.Keys(item).OrderBy(k => k, StringComparer.Ordinal).ToList());

            // The triangles share their corners to the bit, so the mesh has the distinct corners of the triangles and no more.
            int distinct = solid.TriangulateSurface(Tol).SelectMany(t => new[] { t.A, t.B, t.C }).Select(p => Imp.Key(p)).Distinct().Count();
            Assert.Equal(distinct, Imp.MeshPoints(item).Length);
            Assert.True(distinct < 3 * expected.Count);
        }

        [Fact]
        public void TwoBodiesOnOneLayer_AreTwoItems_WhetherApartOrLyingAgainstEachOther()
        {
            GeoSolid3 first = SampleShapes.Box(10.0, 10.0, 10.0, false);
            GeoSolid3 apart = Translate(first, 50.0, 0.0, 0.0);
            GeoSolid3 touching = Translate(first, 10.0, 0.0, 0.0);

            Scene separate = Read(new DxfWriter().Add(first, Tol, "steel").Add(apart, Tol, "steel"));
            Scene joined = Read(new DxfWriter().Add(first, Tol, "steel").Add(touching, Tol, "steel"));

            // Bodies are told apart by their edges, and not by corners they share: two that lie face to face share four corners and four edges, and stay two.
            Assert.Equal(new[] { "steel mesh 1", "steel mesh 2" }, separate.Items.Select(i => i.Name).ToArray());
            Assert.Equal(new[] { "steel mesh 1", "steel mesh 2" }, joined.Items.Select(i => i.Name).ToArray());
            Assert.All(separate.Items.Concat(joined.Items), item => Assert.Equal(12, Imp.MeshFaces(item).Count));
        }

        [Fact]
        public void Triangles_AreOneMesh()
        {
            GeoTriangle3[] triangles = SampleShapes.Box(10.0, 10.0, 10.0, false).TriangulateSurface(Tol);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(triangles, "tris")));

            Assert.Equal(Imp.Keys(triangles), Imp.Keys(item));
            Assert.Equal(8, Imp.MeshPoints(item).Length);
        }

        [Fact]
        public void AMeshOfFlatFaces_IsA3DFaceToAFaceOfThreeOrFourCorners_ToTheBit()
        {
            var mesh = (Meshing.GeoMesh3)SampleShapes.Make("GeoMesh3", 1);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(mesh)));
            List<string> read = Imp.Keys(item);

            Assert.True(read.Count >= mesh.FaceCount);

            // A face of three or four corners that turns one way is one 3DFACE of its corners, in their order; the library cuts any other into triangles.
            for (int f = 0; f < mesh.FaceCount; f++)
            {
                int[] corners = mesh.GetFaceIndices(f);

                if (corners.Length <= 4)
                {
                    Assert.Contains(Imp.Key(corners.Select(i => mesh.Vertices[i])), read);
                }
            }

            Assert.All(Imp.MeshPoints(item), p => Assert.Contains(mesh.Vertices, v => Imp.Key(new[] { p }) == Imp.Key(v)));
        }

        [Fact]
        public void ABox_IsSixQuads_OfItsCorners_ToTheBit()
        {
            var aabb = new GeoAabb3(new GeoPoint3(0.1, 0.2, 0.3), new GeoPoint3(30.1, 20.2, 10.3));
            var obb = (GeoObb3)SampleShapes.Make("GeoObb3", 2);

            foreach ((object box, GeoPoint3[] corners) in new[] { ((object)aabb, aabb.GetCorners()), (obb, obb.GetCorners()) })
            {
                SceneItem item = Imp.Only(box is GeoAabb3 a ? Read(new DxfWriter().Add(a)) : Read(new DxfWriter().Add((GeoObb3)box)));
                List<Vec3[]> faces = Imp.MeshFaces(item);

                Assert.Equal(6, faces.Count);
                Assert.All(faces, f => Assert.Equal(4, f.Length));
                Assert.Equal(8, Imp.MeshPoints(item).Length);
                Assert.All(Imp.MeshPoints(item), p => Assert.Contains(corners, c => Imp.Key(new[] { p }) == Imp.Key(c)));
            }
        }

        [Fact]
        public void CurvesInSpace_AreChainsOfChordsOnThem()
        {
            var arc = (GeoArc3)SampleShapes.Make("GeoArc3", 5);
            var circle = (GeoCircle3)SampleShapes.Make("GeoCircle3", 5);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(arc, 0.1)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(circle, 0.1)));

            Assert.Equal("GeoPolyline3", a.Kind);
            Assert.Equal("GeoPolygon3", b.Kind);
            Imp.AssertOnCircle(DataJson.Points(a.Data, "points", 3), arc.Center, arc.Normal, arc.Radius);
            Imp.AssertOnCircle(DataJson.Points(b.Data, "points", 3), circle.Center, circle.Normal, circle.Radius);
        }

        [Fact]
        public void ChainsAndLoopsWithArcsInSpace_AreChainsOfChordsThatKeepTheirCorners()
        {
            var up = new GeoVector3(0.0, 0.0, 1.0);
            var chain = new GeoPolylineArc3(
                new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(100, 0, 5), new GeoPoint3(200, 40, 5) },
                new[] { 0.4, 0.0, 0.0 },
                new[] { up, up, up });
            var loop = new GeoPolygonArc3(
                new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(100, 0, 5), new GeoPoint3(100, 100, 5), new GeoPoint3(0, 100, 5) },
                new[] { 0.0, 0.5, 0.0, 0.0 },
                new[] { up, up, up, up });

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(chain, 0.05)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(loop, 0.05)));

            Assert.Equal("GeoPolyline3", a.Kind);
            Assert.Equal("GeoPolygon3", b.Kind);
            Assert.Contains(DataJson.Points(a.Data, "points", 3), p => p.Equals(new Vec3(100, 0, 5)));
            Assert.Contains(DataJson.Points(b.Data, "points", 3), p => p.Equals(new Vec3(100, 100, 5)));
        }

        [Fact]
        public void TheLayersAreTheLayers_WithTheirNames()
        {
            Scene scene = Read(new DxfWriter()
                .Add(new GeoPoint2(1.0, 2.0))
                .Add(new GeoPoint2(3.0, 4.0), "walls")
                .Add(new GeoPoint2(5.0, 6.0), "Façade")
                .Add(new GeoPoint2(7.0, 8.0), "WALLS"));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(new[] { "0", "walls", "Façade" }, scene.Layers.Select(l => l.Name).ToArray());
            Assert.Equal(new[] { "0", "walls", "Façade", "walls" }, scene.Items.Select(i => i.Layer).ToArray());
            Assert.Equal(3, scene.Layers.Select(l => l.Color).Distinct().Count());
        }

        [Fact]
        public void TheSameFileWithWindowsLineEnds_ReadsTheSame()
        {
            var writer = new DxfWriter().Add((GeoPolyline3)SampleShapes.Make("GeoPolyline3", 4)).Add(SampleShapes.Box(10.0, 10.0, 10.0, false), Tol, "box");
            Scene unix = SceneImport.ReadText(writer.ToString(), "dxf");
            Scene windows = SceneImport.ReadText(writer.ToString().Replace("\n", "\r\n"), "dxf");

            Assert.Equal(unix.Items.Count, windows.Items.Count);

            for (int i = 0; i < unix.Items.Count; i++)
            {
                Assert.Equal(unix.Items[i].Layer, windows.Items[i].Layer);
                Imp.SameBits(Imp.Of(unix.Items[i]), Imp.Of(windows.Items[i]));
            }
        }

        [Fact]
        public void AWrittenFile_IsAValidScene()
        {
            Scene scene = Read(new DxfWriter()
                .Add((GeoPolygon3)SampleShapes.Make("GeoPolygon3", 1), "a")
                .Add((GeoFace2)SampleShapes.Make("GeoFace2", 2), "b")
                .Add((GeoCircle2)SampleShapes.Make("GeoCircle2", 3), "c")
                .Add(SampleShapes.Box(30.0, 20.0, 10.0, true), Tol, "d"));

            Assert.Empty(scene.LoadWarnings);
            Imp.IsAValidScene(scene);
        }

        private static GeoSolid3 Translate(GeoSolid3 solid, double dx, double dy, double dz)
            => new GeoSolid3(solid.Faces.Select(face => new GeoFace3(
                new GeoPolygon3(face.Boundary.Vertices.Select(p => new GeoPoint3(p.X + dx, p.Y + dy, p.Z + dz))),
                face.Holes.Select(h => new GeoPolygon3(h.Vertices.Select(p => new GeoPoint3(p.X + dx, p.Y + dy, p.Z + dz)))))));
    }
}
