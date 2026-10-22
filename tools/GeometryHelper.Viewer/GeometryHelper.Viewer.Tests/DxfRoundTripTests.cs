using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What DxfWriter writes, read back by the viewer through the library's DxfReader, a shape lying on the floor as the shape of the plane it is.
    /// Points, segments, chains, polygons, circles and rays come back to the bit; an ARC and an ELLIPSE
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
        public void APointInThePlane_IsAPointInThePlane_ToTheBit()
        {
            var point = (GeoPoint2)SampleShapes.Make("GeoPoint2", 1);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(point)));

            Assert.Equal("GeoPoint2", item.Kind);
            Imp.SameBits(Imp.P2(point), Imp.Of(item));
        }

        [Fact]
        public void APointInSpace_ComesBackToTheBit()
        {
            var point = (GeoPoint3)SampleShapes.Make("GeoPoint3", 1);
            Imp.SameBits(Imp.P3(point), Imp.Of(Imp.Only(Read(new DxfWriter().Add(point)))));
        }

        [Fact]
        public void ASegment_IsALineOfItsOwnKind_ToTheBit()
        {
            var flat = (GeoLine2)SampleShapes.Make("GeoLine2", 2);
            var solid = (GeoLine3)SampleShapes.Make("GeoLine3", 2);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(flat)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(solid)));

            Assert.Equal("GeoLine2", a.Kind);
            Assert.Equal("GeoLine3", b.Kind);
            Imp.SameBits(Imp.Of(flat), Imp.Of(a));
            Imp.SameBits(Imp.Of(solid), Imp.Of(b));
        }

        [Fact]
        public void ARay_IsARayOfItsOwnKind_ToTheBit()
        {
            var flat = (GeoRay2)SampleShapes.Make("GeoRay2", 3);
            var solid = (GeoRay3)SampleShapes.Make("GeoRay3", 3);

            SceneItem a = Imp.Only(Read(new DxfWriter().Add(flat)));
            SceneItem b = Imp.Only(Read(new DxfWriter().Add(solid)));

            Assert.Equal("GeoRay2", a.Kind);
            Assert.Equal("GeoRay3", b.Kind);
            Imp.SameBits(Imp.Of(flat), Imp.Of(a));
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

        private static void AssertSamePoint(GeoPoint3 expected, GeoPoint3 actual, double radius, string what)
            => Assert.True(expected.DistanceTo(actual) <= 1E-9 * radius, what + ": " + expected + " against " + actual + ", " + expected.DistanceTo(actual) + " apart");

        [Fact]
        public void AnArcInSpaceWrittenExactly_ComesBackTheSameArc_EitherWayRound()
        {
            // An arc 120 degrees round a circle of radius 40 standing on the normal (1, 2, 3), centred at (10, -20, 30): the viewer takes the
            // ARC and its extrusion back to world points and names the arc by three of them.
            var counter = new GeoArc3(new GeoPoint3(10, -20, 30), new GeoVector3(1, 2, 3), 40, 0.4, 0.4 + 2.0 * Math.PI / 3.0);
            GeoArc3 clockwise = counter.Reverse();

            foreach (GeoArc3 arc in new[] { counter, clockwise })
            {
                SceneItem item = Imp.Only(Read(new DxfWriter().Add(arc, "bars")));
                Assert.Equal("GeoArc3", item.Kind);

                var back = (GeoArc3)GeometryDecoder.Decode(item);

                Assert.Equal(arc.Radius, back.Radius, 9);
                Assert.Equal(arc.Length, back.Length, 8);
                AssertSamePoint(arc.Center, back.Center, arc.Radius, "the centre");
                AssertSamePoint(arc.MidPoint, back.MidPoint, arc.Radius, "the middle");

                // The viewer reads the ARC counter-clockwise about the extrusion, so a clockwise arc comes back from its end to its start.
                AssertSamePoint(arc.IsClockwise ? arc.EndPoint : arc.StartPoint, back.StartPoint, arc.Radius, "the first end");
                AssertSamePoint(arc.IsClockwise ? arc.StartPoint : arc.EndPoint, back.EndPoint, arc.Radius, "the last end");
            }
        }

        [Fact]
        public void ACircleInSpaceWrittenExactly_ComesBackTheSameCircle()
        {
            var circle = new GeoCircle3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 0), 25);
            SceneItem item = Imp.Only(Read(new DxfWriter().Add(circle)));

            Assert.Equal("GeoCircle3", item.Kind);

            var back = (GeoCircle3)GeometryDecoder.Decode(item);

            Assert.Equal(circle.Radius, back.Radius);
            AssertSamePoint(circle.Center, back.Center, circle.Radius, "the centre");
            Assert.Equal(1.0, Math.Abs(back.Normal.DotProduct(circle.Normal)), 12);
        }

        [Fact]
        public void ASlotInATiltedPlaneWrittenExactly_ComesBackAsFourEdgesWithTheirHalfCircles()
        {
            double sin = Math.Sin(Math.PI / 6.0);
            double cos = Math.Cos(Math.PI / 6.0);
            var normal = new GeoVector3(0, -sin, cos);
            GeoPoint3 At(double x, double y) => new GeoPoint3(5 + x, y * cos, 7 + y * sin);

            var loop = new GeoPolygonArc3(
                new[] { At(0, 0), At(60, 0), At(60, 20), At(0, 20) },
                new[] { 0.0, 1.0, 0.0, 1.0 },
                new[] { default(GeoVector3), normal, default(GeoVector3), normal },
                Tol);

            SceneItem item = Imp.Only(Read(new DxfWriter().Add(loop)));
            Assert.Equal("GeoPolygonArc3", item.Kind);

            var back = (GeoPolygonArc3)GeometryDecoder.Decode(item);

            Assert.Equal(4, back.VertexCount);

            for (int i = 0; i < 4; i++)
            {
                GeoEdge3 was = loop.GetEdgeAt(i);
                GeoEdge3 now = back.GetEdgeAt(i);

                Assert.Equal(was.IsArc, now.IsArc);
                AssertSamePoint(was.StartPoint, now.StartPoint, 60.0, "vertex " + i);
                AssertSamePoint(was.MidPoint, now.MidPoint, 60.0, "the middle of edge " + i);
            }

            Assert.Equal(loop.Area, back.Area, 6);
        }

        [Fact]
        public void ABarBentInTwoPlanesWrittenExactly_ComesBackAsItsLinesAndArcs()
        {
            GeoArc3 first = GeoArc3.FromThreePoints(new GeoPoint3(100, 0, 0), new GeoPoint3(100 + 20 * Math.Sin(Math.PI / 4), 0, 20 - 20 * Math.Cos(Math.PI / 4)), new GeoPoint3(120, 0, 20), Tol);
            GeoArc3 second = GeoArc3.FromThreePoints(new GeoPoint3(120, 0, 100), new GeoPoint3(120 + 20 * (1 - Math.Cos(Math.PI / 4)), 20 * Math.Sin(Math.PI / 4), 100), new GeoPoint3(140, 20, 100), Tol);

            var bar = new GeoPolylineArc3(new[]
            {
                new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new GeoEdge3(first),
                new GeoEdge3(first.EndPoint, new GeoPoint3(120, 0, 100)),
                new GeoEdge3(second),
                new GeoEdge3(second.EndPoint, new GeoPoint3(140, 80, 100)),
            });

            Scene scene = Read(new DxfWriter().Add(bar, Tol, "bar"));

            Assert.Empty(scene.LoadWarnings);
            // The first edge lies on the floor, and is read as the segment of the plane it is.
            Assert.Equal(new[] { "GeoLine2", "GeoArc3", "GeoLine3", "GeoArc3", "GeoLine3" }, scene.Items.Select(i => i.Kind).ToArray());

            List<GeoEdge3> edges = bar.GetEdges().ToList();

            for (int i = 0; i < edges.Count; i++)
            {
                object shape = GeometryDecoder.Decode(scene.Items[i]);

                if (shape is GeoLine2 floor)
                {
                    Assert.Equal(edges[i].StartPoint, new GeoPoint3(floor.StartPoint.X, floor.StartPoint.Y, 0.0));
                    Assert.Equal(edges[i].EndPoint, new GeoPoint3(floor.EndPoint.X, floor.EndPoint.Y, 0.0));
                }
                else if (shape is GeoLine3 line)
                {
                    Assert.Equal(edges[i].StartPoint, line.StartPoint);
                    Assert.Equal(edges[i].EndPoint, line.EndPoint);
                }
                else
                {
                    var arc = (GeoArc3)shape;
                    // An arc whose normal points down is written about up, and so runs the other way: the same ends, either way round.
                    bool turned = edges[i].StartPoint.DistanceTo(arc.StartPoint) > 1E-6;
                    AssertSamePoint(turned ? edges[i].EndPoint : edges[i].StartPoint, arc.StartPoint, 20.0, "the start of edge " + i);
                    AssertSamePoint(edges[i].MidPoint, arc.MidPoint, 20.0, "the middle of edge " + i);
                    AssertSamePoint(turned ? edges[i].StartPoint : edges[i].EndPoint, arc.EndPoint, 20.0, "the end of edge " + i);
                }
            }
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
