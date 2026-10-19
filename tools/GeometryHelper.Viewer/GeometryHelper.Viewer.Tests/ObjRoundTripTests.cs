using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What ObjWriter writes, read back by the viewer: the numbers to the bit, and every kind the writer takes.
    /// </summary>
    public class ObjRoundTripTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static Scene Read(ObjWriter writer) => SceneImport.ReadText(writer.ToString(), "obj");

        [Fact]
        public void AChainOfAwkwardNumbers_ComesBackToTheBit()
        {
            // The numbers a text format loses first: a tenth, a number with seventeen digits, the smallest and a subnormal, and minus nought.
            var chain = new GeoPolyline3(new[]
            {
                new GeoPoint3(0.1, 1E-300, 123456789.12345678),
                new GeoPoint3(-0.0, 5E-324, 4.8721540113315704),
                new GeoPoint3(1.7976931348623157E300, -2.2250738585072014E-308, 0.30000000000000004),
                new GeoPoint3(1.0 / 3.0, 2.0 / 3.0, Math.PI),
            });

            SceneItem item = Imp.Only(Read(new ObjWriter().Add(chain, "chain")));

            Assert.Equal("GeoPolyline3", item.Kind);
            Assert.Equal("chain", item.Name);
            Imp.SameBits(Imp.Of(chain), Imp.Of(item));
        }

        [Fact]
        public void ARandomChain_ComesBackToTheBit()
        {
            GeoPolyline3 chain = (GeoPolyline3)SampleShapes.Make("GeoPolyline3", 11);
            Imp.SameBits(Imp.Of(chain), Imp.Of(Imp.Only(Read(new ObjWriter().Add(chain)))));
        }

        [Fact]
        public void ASegment_IsALine()
        {
            var line = (GeoLine3)SampleShapes.Make("GeoLine3", 3);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(line, "edge")));

            Assert.Equal("GeoLine3", item.Kind);
            Imp.SameBits(Imp.Of(line), Imp.Of(item));
        }

        [Fact]
        public void APoint_IsAPoint()
        {
            var point = (GeoPoint3)SampleShapes.Make("GeoPoint3", 3);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(point, "dot")));

            Assert.Equal("GeoPoint3", item.Kind);
            Imp.SameBits(Imp.Of(point), Imp.Of(item));
        }

        [Fact]
        public void ARay_IsTheLineItWasWrittenAs()
        {
            var ray = (GeoRay3)SampleShapes.Make("GeoRay3", 5);
            GeoLine3 line = ray.ToLine(250.0);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(ray, 250.0, "ray")));

            Assert.Equal("GeoLine3", item.Kind);
            Imp.SameBits(Imp.Of(line), Imp.Of(item));
        }

        [Fact]
        public void ATriangle_IsAMeshOfOneFace()
        {
            var triangle = (GeoTriangle3)SampleShapes.Make("GeoTriangle3", 8);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(triangle, "t")));

            Assert.Equal("GeoMesh3", item.Kind);
            Assert.Equal(new[] { Imp.Key(triangle.A, triangle.B, triangle.C) }, Imp.Keys(item));
        }

        [Fact]
        public void ABodyWithAnOpening_IsTheMeshOfWhereItsMaterialEnds_TriangleForTriangle()
        {
            GeoSolid3 solid = SampleShapes.Box(30.0, 20.0, 10.0, true);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(solid, Tol, "box")));
            GeoTriangle3[] expected = solid.TriangulateSurface(Tol);

            Assert.Equal("GeoMesh3", item.Kind);
            Assert.Equal(Imp.Keys(expected), Imp.Keys(item));
            Assert.True(expected.Length > 12, "the opening adds triangles to the box's 12");
        }

        [Fact]
        public void AFace_IsItsTriangles()
        {
            var face = (GeoFace3)SampleShapes.Make("GeoFace3", 4);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(face, Tol, "face")));

            Assert.Equal(Imp.Keys(face.TriangulateSurface(Tol)), Imp.Keys(item));
        }

        [Fact]
        public void APolygonThatTurnsOneWay_IsOneFaceOfItsCorners()
        {
            var polygon = (GeoPolygon3)SampleShapes.Make("GeoPolygon3", 6);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(polygon, Tol, "hexagon")));
            List<Vec3[]> faces = Imp.MeshFaces(item);

            Assert.Single(faces);
            Assert.Equal(Imp.Key(polygon.Vertices), Imp.Key(faces[0]));
        }

        [Fact]
        public void AnLShapedPolygon_IsItsTriangles()
        {
            var polygon = new GeoPolygon3(SampleShapes.LShape().Select(p => new GeoPoint3(p.X, p.Y, 7.5)));
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(polygon, Tol, "L")));
            GeoTriangle3[] expected = new GeoFace3(polygon, null, Tol).TriangulateSurface(Tol);

            Assert.Equal(Imp.Keys(expected), Imp.Keys(item));
            Assert.Equal(4, expected.Length);
        }

        [Fact]
        public void ABox_IsItsTwelveTriangles()
        {
            var box = new GeoAabb3(new GeoPoint3(0.1, 0.2, 0.3), new GeoPoint3(30.1, 20.2, 10.3));
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(box, "aabb")));

            Assert.Equal(Imp.Keys(box.ToObb().ToSolid().TriangulateSurface(Tol)), Imp.Keys(item));

            // Eight corners, written 36 times, one triangle at a time.
            Vec3[] points = Imp.MeshPoints(item);
            Assert.Equal(36, points.Length);
            Assert.Equal(8, points.Select(p => Imp.Key(new[] { p })).Distinct().Count());
        }

        [Fact]
        public void AnOrientedBox_IsItsTwelveTriangles()
        {
            var box = (GeoObb3)SampleShapes.Make("GeoObb3", 2);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(box, "obb")));

            Assert.Equal(Imp.Keys(box.ToSolid().TriangulateSurface(Tol)), Imp.Keys(item));
        }

        [Fact]
        public void Triangles_AreOneMeshOfThem()
        {
            GeoTriangle3[] triangles = SampleShapes.Box(10.0, 10.0, 10.0, false).TriangulateSurface(Tol);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(triangles, "tris")));

            Assert.Equal(Imp.Keys(triangles), Imp.Keys(item));
        }

        [Fact]
        public void AMeshOfFlatFaces_KeepsItsFacesAndSharesItsCorners()
        {
            var mesh = (GeoMesh3)SampleShapes.Make("GeoMesh3", 1);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(mesh, "plate")));

            Assert.Equal(mesh.FaceCount, Imp.MeshFaces(item).Count);
            Imp.SameBits(mesh.Vertices.SelectMany(Imp.P3).ToList(), Imp.MeshPoints(item).SelectMany(p => new[] { p.X, p.Y, p.Z }).ToList());

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                Assert.Equal(Imp.Key(mesh.GetFaceIndices(f).Select(i => mesh.Vertices[i])), Imp.Key(Imp.MeshFaces(item)[f]));
            }
        }

        [Fact]
        public void ABodyWrittenOneTriangleAtATime_HasThreeVerticesOfItsOwnToEachTriangle()
        {
            // A box has 8 corners; written triangle by triangle it has 36 vertices in the file, and the reader keeps them as written: the mesh holds 36 points and
            // no triangle names a vertex of another. What makes the 12 one body is their edges, which the reader and the clash check match within the tolerance.
            GeoSolid3 solid = SampleShapes.Box(10.0, 10.0, 10.0, false);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(solid, Tol, "box")));

            Assert.Equal(36, Imp.MeshPoints(item).Length);
            Assert.Equal(12, Imp.MeshFaces(item).Count);
        }

        [Fact]
        public void AnArc_IsAChainOfChordsOnTheArc()
        {
            var arc = (GeoArc3)SampleShapes.Make("GeoArc3", 5);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(arc, 0.1, "arc")));
            Vec3[] points = DataJson.Points(item.Data, "points", 3);

            Assert.Equal("GeoPolyline3", item.Kind);
            Assert.True(points.Length > 8);
            Imp.AssertOnCircle(points, arc.Center, arc.Normal, arc.Radius);
            Assert.True(Imp.Distance(points[0], arc.StartPoint) < 1E-9 * arc.Radius);
            Assert.True(Imp.Distance(points[points.Length - 1], arc.EndPoint) < 1E-9 * arc.Radius);
        }

        [Fact]
        public void ACircle_IsAClosedChainOfChordsOnTheCircle()
        {
            var circle = (GeoCircle3)SampleShapes.Make("GeoCircle3", 5);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(circle, 0.1, "ring")));
            Vec3[] points = DataJson.Points(item.Data, "points", 3);

            Imp.AssertOnCircle(points, circle.Center, circle.Normal, circle.Radius);
            Assert.True(new BitKey(points[0]).Equals(new BitKey(points[points.Length - 1])), "OBJ has no closed line: the last vertex is the first");
        }

        [Fact]
        public void AnEllipse_IsAClosedChainOnTheEllipse()
        {
            var ellipse = (GeoEllipse3)SampleShapes.Make("GeoEllipse3", 7);
            SceneItem item = Imp.Only(Read(new ObjWriter().Add(ellipse, 0.1, "oval")));
            Vec3[] points = DataJson.Points(item.Data, "points", 3);
            GeoVector3 minor = ellipse.MinorAxis;

            foreach (Vec3 p in points)
            {
                GeoVector3 d = ellipse.Center.GetVectorTo(new GeoPoint3(p.X, p.Y, p.Z));
                double u = d.DotProduct(ellipse.MajorAxis) / ellipse.MajorRadius;
                double v = d.DotProduct(minor) / ellipse.MinorRadius;
                Assert.InRange(u * u + v * v, 1.0 - 1E-9, 1.0 + 1E-9);
            }
        }

        [Fact]
        public void AChainWithArcs_AndALoopWithArcs_AreChainsOfChords()
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

            Scene scene = Read(new ObjWriter().Add(chain, 0.05, "chain").Add(loop, 0.05, "loop"));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(new[] { "chain", "loop" }, scene.Items.Select(i => i.Name).ToArray());
            Assert.All(scene.Items, i => Assert.Equal("GeoPolyline3", i.Kind));

            Vec3[] chainPoints = DataJson.Points(scene.Items[0].Data, "points", 3);
            Vec3[] loopPoints = DataJson.Points(scene.Items[1].Data, "points", 3);

            // The straight edges are kept as they are: the chain still passes through its corners to the bit.
            Assert.Contains(chainPoints, p => p.Equals(new Vec3(100, 0, 5)));
            Assert.Contains(chainPoints, p => p.Equals(new Vec3(200, 40, 5)));
            Assert.True(new BitKey(loopPoints[0]).Equals(new BitKey(loopPoints[loopPoints.Length - 1])));
        }

        [Fact]
        public void SeveralShapes_AreSeveralItemsNamedAsGiven()
        {
            Scene scene = Read(new ObjWriter()
                .Add(SampleShapes.Box(10.0, 10.0, 10.0, false), Tol, "plate one")
                .Add(new GeoPoint3(1.0, 2.0, 3.0), "dot")
                .Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 5, 5))));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(new[] { "plate_one", "dot", "shape3" }, scene.Items.Select(i => i.Name).ToArray());
            Assert.Equal(new[] { "GeoMesh3", "GeoPoint3", "GeoLine3" }, scene.Items.Select(i => i.Kind).ToArray());
            Imp.IsAValidScene(scene);
        }

        [Fact]
        public void ABodyCutIntoCells_IsOneItemToACell_NamedByItsIndexes()
        {
            GeoSolid3 slab = SampleShapes.Box(30.0, 20.0, 10.0, false);
            GeoCellGrid3 grid = Mesh3.ToCells(slab, CellOptions3.Divide(3, 1, 1), Tol);
            Scene scene = Read(new ObjWriter().Add(grid, "slab"));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(new[] { "slab_0_0_0", "slab_1_0_0", "slab_2_0_0" }, scene.Items.Select(i => i.Name).ToArray());

            for (int c = 0; c < grid.Cells.Count; c++)
            {
                Assert.Equal(Imp.Keys(grid.Cells[c].Solid.Triangulate(Tol)), Imp.Keys(scene.Items[c]));
            }

            // One cell on its own is written as the mesh of its body, at the global tolerance.
            SceneItem one = Imp.Only(Read(new ObjWriter().Add(grid.Cells[1], "cell")));
            Assert.Equal("cell_1_0_0", one.Name);
        }

        [Fact]
        public void ABodyReadBack_ClosesAsAMeshAndAsABody()
        {
            Scene scene = Read(new ObjWriter().Add(SampleShapes.Box(10.0, 10.0, 10.0, false), Tol, "box"));
            object mesh = scene.Items[0].ToGeometry();

            // A mesh comes back from the scene as the polygons of its faces, which close a body again.
            var faces = Assert.IsType<GeoPolygon3[]>(mesh);
            var solid = new GeoSolid3(faces.Select(f => new GeoFace3(f, null)));
            Assert.True(solid.IsClosed(Tol));
            Assert.Equal(1000.0, solid.GetVolume(), 6);
        }
    }
}
