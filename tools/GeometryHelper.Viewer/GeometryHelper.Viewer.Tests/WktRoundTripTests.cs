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
    /// What Wkt writes, read back by the viewer: the numbers to the bit, and every kind the writer takes. The format writes a polygon counter-clockwise and
    /// a hole clockwise, and a ring closed, so a polygon is compared as the format writes it.
    /// </summary>
    public class WktRoundTripTests
    {
        private static Scene Read(string wkt) => SceneImport.ReadText(wkt, "wkt");

        private static double[] Flatten(IEnumerable<GeoPoint2> points) => points.SelectMany(Imp.P2).ToArray();

        private static double[] Flatten(IEnumerable<GeoPoint3> points) => points.SelectMany(Imp.P3).ToArray();

        /// <summary>
        /// The corners of a polygon as the format writes them: counter-clockwise, or clockwise for a hole.
        /// </summary>
        private static List<GeoPoint2> Wound(IEnumerable<GeoPoint2> corners, bool counterClockwise)
        {
            var list = corners.ToList();
            double twice = 0.0;

            for (int i = 0; i < list.Count; i++)
            {
                GeoPoint2 next = list[(i + 1) % list.Count];
                twice += (list[i].X * next.Y) - (next.X * list[i].Y);
            }

            if ((twice > 0.0) != counterClockwise)
            {
                list.Reverse();
            }

            return list;
        }

        [Fact]
        public void APointInThePlane_ComesBackToTheBit()
        {
            var point = (GeoPoint2)SampleShapes.Make("GeoPoint2", 1);
            SceneItem item = Imp.Only(Read(Wkt.Write(point)));

            Assert.Equal("GeoPoint2", item.Kind);
            Imp.SameBits(Imp.P2(point), Imp.Of(item));
        }

        [Fact]
        public void APointInSpace_ComesBackToTheBit_MinusNoughtToo()
        {
            var point = new GeoPoint3(-0.0, 1E-300, 123456789.12345678);
            SceneItem item = Imp.Only(Read(Wkt.Write(point)));

            Assert.Equal("GeoPoint3", item.Kind);
            Imp.SameBits(Imp.P3(point), Imp.Of(item));
        }

        [Fact]
        public void ASegment_IsALine_InThePlaneAndInSpace()
        {
            var flat = (GeoLine2)SampleShapes.Make("GeoLine2", 2);
            var solid = (GeoLine3)SampleShapes.Make("GeoLine3", 2);

            SceneItem a = Imp.Only(Read(Wkt.Write(flat)));
            SceneItem b = Imp.Only(Read(Wkt.Write(solid)));

            Assert.Equal("GeoLine2", a.Kind);
            Assert.Equal("GeoLine3", b.Kind);
            Imp.SameBits(Imp.Of(flat), Imp.Of(a));
            Imp.SameBits(Imp.Of(solid), Imp.Of(b));
        }

        [Fact]
        public void AChain_IsAChain_InThePlaneAndInSpace()
        {
            var flat = (GeoPolyline2)SampleShapes.Make("GeoPolyline2", 4);
            var solid = (GeoPolyline3)SampleShapes.Make("GeoPolyline3", 4);

            SceneItem a = Imp.Only(Read(Wkt.Write(flat)));
            SceneItem b = Imp.Only(Read(Wkt.Write(solid)));

            Assert.Equal("GeoPolyline2", a.Kind);
            Assert.Equal("GeoPolyline3", b.Kind);
            Imp.SameBits(Imp.Of(flat), Imp.Of(a));
            Imp.SameBits(Imp.Of(solid), Imp.Of(b));
        }

        [Fact]
        public void APolygon_LosesTheCornerThatClosesItsRing_AndKeepsTheRest()
        {
            var flat = (GeoPolygon2)SampleShapes.Make("GeoPolygon2", 5);
            var solid = (GeoPolygon3)SampleShapes.Make("GeoPolygon3", 5);

            SceneItem a = Imp.Only(Read(Wkt.Write(flat)));
            SceneItem b = Imp.Only(Read(Wkt.Write(solid)));

            Assert.Equal("GeoPolygon2", a.Kind);
            Assert.Equal("GeoPolygon3", b.Kind);
            Imp.SameBits(Flatten(Wound(flat.Vertices, true)), Imp.Of(a));
            Imp.SameBits(Flatten(solid.Vertices), Imp.Of(b));
        }

        [Fact]
        public void ARectangleAndATriangle_ArePolygonsWoundCounterClockwise()
        {
            var rectangle = (GeoRectangle2)SampleShapes.Make("GeoRectangle2", 6);
            var triangle = (GeoTriangle2)SampleShapes.Make("GeoTriangle2", 6);

            SceneItem a = Imp.Only(Read(Wkt.Write(rectangle)));
            SceneItem b = Imp.Only(Read(Wkt.Write(triangle)));

            Imp.SameBits(Flatten(Wound(rectangle.GetVertices(), true)), Imp.Of(a));
            Imp.SameBits(Flatten(Wound(new[] { triangle.A, triangle.B, triangle.C }, true)), Imp.Of(b));
        }

        [Fact]
        public void ATriangleInSpace_KeepsItsCornersInTheOrderGiven()
        {
            var triangle = (GeoTriangle3)SampleShapes.Make("GeoTriangle3", 6);
            SceneItem item = Imp.Only(Read(Wkt.Write(triangle)));

            Assert.Equal("GeoPolygon3", item.Kind);
            Imp.SameBits(Flatten(new[] { triangle.A, triangle.B, triangle.C }), Imp.Of(item));
        }

        [Fact]
        public void AFaceWithAHole_IsAFace_TheHoleWoundClockwise()
        {
            var face = (GeoFace2)SampleShapes.Make("GeoFace2", 7);
            SceneItem item = Imp.Only(Read(Wkt.Write(face)));

            Assert.Equal("GeoFace2", item.Kind);

            var expected = new List<double>();
            expected.AddRange(Flatten(Wound(face.Boundary.Vertices, true)));
            expected.AddRange(Flatten(Wound(face.Holes[0].Vertices, false)));
            Imp.SameBits(expected, Imp.Of(item));
        }

        [Fact]
        public void AFaceInSpaceWithAHole_IsAFace_TheHoleRunningTheOtherWayRoundFromHowItIsHeld()
        {
            var outer = new GeoPolygon3(new[] { new GeoPoint3(0.1, 0.2, 5), new GeoPoint3(100.1, 0.2, 5), new GeoPoint3(100.1, 100.2, 5), new GeoPoint3(0.1, 100.2, 5) });
            var hole = new GeoPolygon3(new[] { new GeoPoint3(20.1, 20.2, 5), new GeoPoint3(40.1, 20.2, 5), new GeoPoint3(40.1, 40.2, 5), new GeoPoint3(20.1, 40.2, 5) });
            var face = new GeoFace3(outer, new[] { hole });

            SceneItem item = Imp.Only(Read(Wkt.Write(face)));

            Assert.Equal("GeoFace3", item.Kind);

            var expected = new List<double>();
            expected.AddRange(Flatten(face.Boundary.Vertices));
            expected.AddRange(Flatten(face.Holes[0].Vertices.Reverse()));
            Imp.SameBits(expected, Imp.Of(item));
        }

        [Fact]
        public void SeveralFaces_AreSeveralItems()
        {
            GeoFace2[] faces =
            {
                new GeoFace2(new GeoPolygon2(new GeoPoint2(0.1, 0.2), new GeoPoint2(10.1, 0.2), new GeoPoint2(10.1, 10.2)), null),
                new GeoFace2(new GeoPolygon2(new GeoPoint2(20.1, 0.2), new GeoPoint2(30.1, 0.2), new GeoPoint2(30.1, 10.2), new GeoPoint2(20.1, 10.2)), null),
            };

            Scene scene = Read(Wkt.Write(faces));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(2, scene.Items.Count);

            for (int i = 0; i < 2; i++)
            {
                Assert.Equal("GeoPolygon2", scene.Items[i].Kind);
                Imp.SameBits(Flatten(Wound(faces[i].Boundary.Vertices, true)), Imp.Of(scene.Items[i]));
            }
        }

        [Fact]
        public void AMeshOfTheFlatShapes_IsOnePolygonToAFace()
        {
            var mesh = (GeoMesh3)SampleShapes.Make("GeoMesh3", 1);
            Scene scene = Read(Wkt.Write(mesh));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(mesh.FaceCount, scene.Items.Count);

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                Imp.SameBits(Flatten(mesh.GetFaceIndices(f).Select(i => mesh.Vertices[i])), Imp.Of(scene.Items[f]));
            }
        }

        [Fact]
        public void AMeshInThePlane_IsOnePolygonToAFace()
        {
            var mesh = (Meshing.GeoMesh2)SampleShapes.Make("GeoMesh2", 1);
            Scene scene = Read(Wkt.Write(mesh));

            Assert.Equal(mesh.FaceCount, scene.Items.Count);
            Assert.All(scene.Items, i => Assert.Equal("GeoPolygon2", i.Kind));
        }

        [Fact]
        public void ABody_IsTheMeshOfItsTriangles_TriangleForTriangle()
        {
            GeoSolid3 solid = SampleShapes.Box(30.0, 20.0, 10.0, true);
            SceneItem item = Imp.Only(Read(Wkt.Write(solid)));

            Assert.Equal("GeoMesh3", item.Kind);
            Assert.Equal(Imp.Keys(solid.TriangulateSurface()), Imp.Keys(item));
        }

        [Fact]
        public void ABodyWithNothingLeft_IsNothing()
        {
            Scene scene = Read("POLYHEDRALSURFACE Z EMPTY");
            Assert.Empty(scene.Items);
            Assert.Empty(scene.LoadWarnings);
        }

        [Fact]
        public void ABoxOfTwelveTriangles_ShareTheirCornersWhereTheyAreOnePoint()
        {
            SceneItem item = Imp.Only(Read(Wkt.Write(SampleShapes.Box(30.0, 20.0, 10.0, false))));

            Assert.Equal(8, Imp.MeshPoints(item).Length);
            Assert.Equal(12, Imp.MeshFaces(item).Count);
        }

        [Fact]
        public void SeveralPoints_AreSeveralPoints()
        {
            GeoPoint2[] flat = { new GeoPoint2(0.1, 0.2), new GeoPoint2(1.5, -2.5) };
            GeoPoint3[] solid = { new GeoPoint3(0.1, 0.2, 0.3), new GeoPoint3(1.5, -2.5, 4.5) };

            Scene a = Read(Wkt.Write(flat));
            Scene b = Read(Wkt.Write(solid));

            Assert.Equal(new[] { "GeoPoint2", "GeoPoint2" }, a.Items.Select(i => i.Kind).ToArray());
            Assert.Equal(new[] { "GeoPoint3", "GeoPoint3" }, b.Items.Select(i => i.Kind).ToArray());
            Imp.SameBits(Flatten(flat), a.Items.SelectMany(i => Imp.Of(i)).ToList());
            Imp.SameBits(Flatten(solid), b.Items.SelectMany(i => Imp.Of(i)).ToList());
        }

        [Fact]
        public void SeveralSegmentsAndChains_AreSeveralItems()
        {
            var lines = new[] { new GeoLine2(new GeoPoint2(0.1, 0.2), new GeoPoint2(3.1, 4.2)), new GeoLine2(new GeoPoint2(5.1, 6.2), new GeoPoint2(7.1, 8.2)) };
            var chains = new[] { (GeoPolyline3)SampleShapes.Make("GeoPolyline3", 1), (GeoPolyline3)SampleShapes.Make("GeoPolyline3", 2) };

            Scene a = Read(Wkt.Write(lines));
            Scene b = Read(Wkt.Write(chains));

            Assert.All(a.Items, i => Assert.Equal("GeoLine2", i.Kind));
            Assert.All(b.Items, i => Assert.Equal("GeoPolyline3", i.Kind));
            Imp.SameBits(Imp.Of(chains[1]), Imp.Of(b.Items[1]));
        }

        [Fact]
        public void ACircleCutIntoChords_IsAPolygonOfThem()
        {
            var circle = (GeoCircle2)SampleShapes.Make("GeoCircle2", 3);
            SceneItem item = Imp.Only(Read(Wkt.Write(circle, 0.1)));
            Vec3[] points = DataJson.Points(item.Data, "points", 2);

            Assert.Equal("GeoPolygon2", item.Kind);
            Assert.True(points.Length > 20);
            Assert.All(points, p => Assert.InRange(Math.Sqrt(((p.X - circle.Center.X) * (p.X - circle.Center.X)) + ((p.Y - circle.Center.Y) * (p.Y - circle.Center.Y))) / circle.Radius, 1.0 - 1E-9, 1.0 + 1E-9));
        }

        [Fact]
        public void AnArcCutIntoChords_IsAChainFromItsStartToItsEnd()
        {
            var arc = (GeoArc2)SampleShapes.Make("GeoArc2", 3);
            SceneItem item = Imp.Only(Read(Wkt.Write(arc, 0.1)));
            Vec3[] points = DataJson.Points(item.Data, "points", 2);

            Assert.Equal("GeoPolyline2", item.Kind);
            Assert.True(Math.Abs(points[0].X - arc.StartPoint.X) < 1E-9 * arc.Radius && Math.Abs(points[0].Y - arc.StartPoint.Y) < 1E-9 * arc.Radius);
            Assert.True(Math.Abs(points[points.Length - 1].X - arc.EndPoint.X) < 1E-9 * arc.Radius && Math.Abs(points[points.Length - 1].Y - arc.EndPoint.Y) < 1E-9 * arc.Radius);
        }

        [Fact]
        public void ACircleAndAnEllipseInSpace_ArePolygonsOfChords()
        {
            var circle = (GeoCircle3)SampleShapes.Make("GeoCircle3", 3);
            var ellipse = (GeoEllipse3)SampleShapes.Make("GeoEllipse3", 3);

            SceneItem a = Imp.Only(Read(Wkt.Write(circle, 0.1)));
            SceneItem b = Imp.Only(Read(Wkt.Write(ellipse, 0.1)));

            Assert.Equal("GeoPolygon3", a.Kind);
            Assert.Equal("GeoPolygon3", b.Kind);
            Imp.AssertOnCircle(DataJson.Points(a.Data, "points", 3), circle.Center, circle.Normal, circle.Radius);
        }

        [Fact]
        public void ChainsWithArcs_AreChainsAndPolygonsOfChords()
        {
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(200, 40) }, new[] { 0.4, 0.0, 0.0 });
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) }, new[] { 0.0, 0.5, 0.0, 0.0 });

            SceneItem a = Imp.Only(Read(Wkt.Write(chain, 0.05)));
            SceneItem b = Imp.Only(Read(Wkt.Write(loop, 0.05)));

            Assert.Equal("GeoPolyline2", a.Kind);
            Assert.Equal("GeoPolygon2", b.Kind);
            Assert.Equal(0.0, Imp.Of(a)[0]);
            Assert.True(Imp.Of(b).Count > 8);
        }

        [Fact]
        public void ARay_IsTheSegmentItWasWrittenAs()
        {
            var ray = (GeoRay2)SampleShapes.Make("GeoRay2", 3);
            SceneItem item = Imp.Only(Read(Wkt.Write(ray, 300.0)));

            Assert.Equal("GeoLine2", item.Kind);
            Imp.SameBits(Imp.Of(ray.ToLine(300.0)), Imp.Of(item));
        }

        [Fact]
        public void ARayInSpace_AndArcsAndLoopsInSpace_AreSegmentsAndChainsOfChords()
        {
            var ray = (GeoRay3)SampleShapes.Make("GeoRay3", 3);
            var arc = (GeoArc3)SampleShapes.Make("GeoArc3", 3);
            var up = new GeoVector3(0.0, 0.0, 1.0);
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(100, 0, 5), new GeoPoint3(200, 40, 5) }, new[] { 0.4, 0.0, 0.0 }, new[] { up, up, up });
            var loop = new GeoPolygonArc3(new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(100, 0, 5), new GeoPoint3(100, 100, 5), new GeoPoint3(0, 100, 5) }, new[] { 0.0, 0.5, 0.0, 0.0 }, new[] { up, up, up, up });

            SceneItem a = Imp.Only(Read(Wkt.Write(ray, 300.0)));
            SceneItem b = Imp.Only(Read(Wkt.Write(arc, 0.1)));
            SceneItem c = Imp.Only(Read(Wkt.Write(chain, 0.05)));
            SceneItem d = Imp.Only(Read(Wkt.Write(loop, 0.05)));

            Assert.Equal("GeoLine3", a.Kind);
            Imp.SameBits(Imp.Of(ray.ToLine(300.0)), Imp.Of(a));
            Assert.Equal("GeoPolyline3", b.Kind);
            Imp.AssertOnCircle(DataJson.Points(b.Data, "points", 3), arc.Center, arc.Normal, arc.Radius);
            Assert.Equal(new[] { "GeoPolyline3", "GeoPolygon3" }, new[] { c.Kind, d.Kind });
            Assert.Contains(DataJson.Points(c.Data, "points", 3), p => p.Equals(new Vec3(200, 40, 5)));
        }

        [Fact]
        public void ManyOfEachKind_AreManyItems_InThePlaneAndInSpace()
        {
            var lines3 = new[] { (GeoLine3)SampleShapes.Make("GeoLine3", 1), (GeoLine3)SampleShapes.Make("GeoLine3", 2) };
            var chains2 = new[] { (GeoPolyline2)SampleShapes.Make("GeoPolyline2", 1), (GeoPolyline2)SampleShapes.Make("GeoPolyline2", 2) };
            GeoFace3[] faces3 =
            {
                new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0.1, 0.2, 5), new GeoPoint3(10.1, 0.2, 5), new GeoPoint3(10.1, 10.2, 5) }), null),
                new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(20.1, 0.2, 5), new GeoPoint3(30.1, 0.2, 5), new GeoPoint3(30.1, 10.2, 5) }), null),
            };

            Scene a = Read(Wkt.Write(lines3));
            Scene b = Read(Wkt.Write(chains2));
            Scene c = Read(Wkt.Write(faces3));

            Assert.All(a.Items, i => Assert.Equal("GeoLine3", i.Kind));
            Assert.All(b.Items, i => Assert.Equal("GeoPolyline2", i.Kind));
            Assert.All(c.Items, i => Assert.Equal("GeoPolygon3", i.Kind));
            Imp.SameBits(Imp.Of(lines3[1]), Imp.Of(a.Items[1]));
            Imp.SameBits(Imp.Of(chains2[0]), Imp.Of(b.Items[0]));
            Imp.SameBits(Flatten(faces3[1].Boundary.Vertices), Imp.Of(c.Items[1]));
        }

        [Fact]
        public void AWrittenScene_IsAValidScene()
        {
            var text = string.Join("\n", Wkt.Write((GeoPolyline3)SampleShapes.Make("GeoPolyline3", 9)), Wkt.Write(SampleShapes.Box(10.0, 10.0, 10.0, false)), Wkt.Write(new GeoPoint2(1.0, 2.0)));
            Scene scene = Read(text);

            Assert.Equal(3, scene.Items.Count);
            Imp.IsAValidScene(scene);
        }
    }
}
