using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class PrimitivesTests
    {
        public static IEnumerable<object[]> Kinds => SampleShapes.KindData;

        private static ItemPrimitives Draw(object shape)
        {
            Primitives primitives = Primitives.Build(new SceneWriter().Add(shape).Scene);
            Assert.Empty(primitives.Warnings);
            return Assert.Single(primitives.Items);
        }

        private static double TriangleArea(IEnumerable<DrawTriangle> triangles)
            => triangles.Sum(t => Vec3.Cross(t.B - t.A, t.C - t.A).Length * 0.5);

        [Theory]
        [MemberData(nameof(Kinds))]
        public void EveryKind_GivesPrimitivesAndBoundsThatHoldThem(string kind)
        {
            ItemPrimitives item = Draw(SampleShapes.Make(kind, 5));

            Assert.True(item.Points.Count + item.Polylines.Count + item.Triangles.Count > 0, kind + " draws nothing");
            Assert.False(item.Bounds.IsEmpty);

            var everything = item.Points
                .Concat(item.Polylines.SelectMany(l => l.Points))
                .Concat(item.Triangles.SelectMany(t => new[] { t.A, t.B, t.C }))
                .ToList();

            Assert.All(everything, p => Assert.True(p.IsFinite));

            // A ray and a plane are drawn out to the edge of the scene, and count for their origin only.
            if (kind != "GeoRay2" && kind != "GeoRay3" && kind != "GeoPlane3")
            {
                Assert.All(everything, p =>
                {
                    Assert.InRange(p.X, item.Bounds.Min.X, item.Bounds.Max.X);
                    Assert.InRange(p.Y, item.Bounds.Min.Y, item.Bounds.Max.Y);
                    Assert.InRange(p.Z, item.Bounds.Min.Z, item.Bounds.Max.Z);
                });
            }
        }

        [Fact]
        public void ALabel_DrawsAtItsPlaceAndCountsInTheBounds()
        {
            Scene scene = new SceneWriter().Text(new GeoPoint3(1, 2, 3), "here", 5.0).Scene;
            Primitives primitives = Primitives.Build(scene);

            DrawLabel label = Assert.Single(primitives.Items[0].Labels);
            Assert.Equal("here", label.Text);
            Assert.Equal(new Vec3(1, 2, 3), label.At);
            Assert.Equal(new Vec3(1, 2, 3), primitives.Bounds.Min);
        }

        [Fact]
        public void ABox30By20By10WithAHole10By10Through_IsThePrismAndDrawsItsTwentyFourHundredSquareUnits()
        {
            ItemPrimitives item = Draw(SampleShapes.Box(30, 20, 10, true));

            // The hole is drawn too, and it is cut from z = -1 to 11, past both faces.
            Assert.Equal(new Vec3(0, 0, -1), item.Bounds.Min);
            Assert.Equal(new Vec3(30, 20, 11), item.Bounds.Max);

            // Outer skin 2 * (600 + 300 + 200), less the two ends of the hole, plus its four walls.
            Assert.Equal(2400.0, TriangleArea(item.Triangles), 6);
        }

        [Fact]
        public void ABoxWithoutAnOpening_DrawsItsSixFaces()
        {
            ItemPrimitives item = Draw(SampleShapes.Box(30, 20, 10, false));

            Assert.Equal(2200.0, TriangleArea(item.Triangles), 6);
            Assert.Equal(6, item.Polylines.Count);
        }

        [Fact]
        public void ATriangleOfASolid_IsLitByTheNormalPointingOutOfTheBody()
        {
            ItemPrimitives item = Draw(SampleShapes.Box(30, 20, 10, false));
            Vec3 middle = new Vec3(15, 10, 5);

            foreach (DrawTriangle t in item.Triangles)
            {
                Vec3 centre = (t.A + t.B + t.C) * (1.0 / 3.0);
                Assert.True(Vec3.Dot(t.Normal, centre - middle) > 0.0, "a normal points into the box");
            }
        }

        [Fact]
        public void ACircleOfRadius50_IsCutByTheChordErrorOfAScenePointToleranceTenthAndAllOnTheCircle()
        {
            ItemPrimitives item = Draw(new GeoCircle2(new GeoPoint2(10, 20), 50));

            DrawPolyline ring = Assert.Single(item.Polylines);
            Assert.True(ring.Closed);
            Assert.InRange(ring.Points.Length, 1570, 1572);
            Assert.All(ring.Points, p => Assert.Equal(50.0, Math.Sqrt((p.X - 10) * (p.X - 10) + (p.Y - 20) * (p.Y - 20)), 9));
        }

        [Fact]
        public void AQuarterArc_HasItsEndsExactlyWhereTheArcEnds()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, Math.PI / 2.0);
            DrawPolyline line = Assert.Single(Draw(arc).Polylines);

            Assert.InRange(line.Points.Length, 557, 559);
            Assert.True(Distance(line.Points[0], arc.StartPoint) < 1E-9);
            Assert.True(Distance(line.Points[line.Points.Length - 1], arc.EndPoint) < 1E-9);
        }

        [Fact]
        public void AnArcInSpace_StartsAndEndsWhereTheLibrarySaysItDoes_ForEitherWayRound()
        {
            foreach (double end in new[] { 2.0, 5.0 })
            {
                foreach (bool clockwise in new[] { false, true })
                {
                    var arc = new GeoArc3(new GeoPoint3(10, -20, 30), new GeoVector3(1, 2, 3).Normalize(), 75.0, 0.7, end, clockwise);
                    DrawPolyline line = Assert.Single(Draw(arc).Polylines);

                    Assert.True(Distance(line.Points[0], arc.StartPoint) < 1E-9, "start, end " + end + " clockwise " + clockwise);
                    Assert.True(Distance(line.Points[line.Points.Length - 1], arc.EndPoint) < 1E-9, "end, end " + end + " clockwise " + clockwise);
                    Assert.True(line.Points.Length >= 3);
                }
            }
        }

        [Fact]
        public void APositiveBulge_BowsToTheRightOfTheEdge_AndANegativeOneToTheLeft()
        {
            DrawPolyline positive = Assert.Single(Draw(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0) }, new[] { 1.0, 0.0 })).Polylines);
            DrawPolyline negative = Assert.Single(Draw(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0) }, new[] { -1.0, 0.0 })).Polylines);

            // A bulge of 1 is a half circle of radius 5 about (5, 0).
            Assert.True(positive.Points.Length >= 91);
            Assert.All(positive.Points, p => Assert.Equal(5.0, Math.Sqrt((p.X - 5) * (p.X - 5) + p.Y * p.Y), 9));
            Assert.All(positive.Points.Skip(1).Take(positive.Points.Length - 2), p => Assert.True(p.Y < 0.0));
            Assert.All(negative.Points.Skip(1).Take(negative.Points.Length - 2), p => Assert.True(p.Y > 0.0));
        }

        [Fact]
        public void ABulgeBeyondAHalfCircle_KeepsTheCentreOnTheFarSideOfTheChord()
        {
            // Three quarters of a circle of radius 5 through (0,0) and (7.07, 0): tan(270 / 4 degrees) = 2.414.
            double chord = 5.0 * Math.Sqrt(2.0);
            DrawPolyline line = Assert.Single(Draw(new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(chord, 0) }, new[] { Math.Tan(270.0 * Math.PI / 180.0 / 4.0), 0.0 })).Polylines);

            Vec3 centre = new Vec3(chord / 2.0, -chord / 2.0, 0.0);
            Assert.All(line.Points, p => Assert.Equal(5.0, Distance(p, centre), 9));
        }

        [Fact]
        public void AnArcInSpaceChainAndALoop_BowByTheirNormals()
        {
            var up = new GeoVector3(0, 0, 1);
            var chain = new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(10, 0, 5) }, new[] { 1.0, 0.0 }, new[] { up, default(GeoVector3) });
            DrawPolyline line = Assert.Single(Draw(chain).Polylines);

            Assert.True(line.Points.Length >= 91);
            Assert.All(line.Points, p => Assert.Equal(5.0, p.Z));
            Assert.All(line.Points, p => Assert.Equal(5.0, Distance(p, new Vec3(5, 0, 5)), 9));
            Assert.All(line.Points.Skip(1).Take(line.Points.Length - 2), p => Assert.True(p.Y < 0.0));
        }

        [Fact]
        public void AnEllipseInSpace_StandsOnItsTwoRadii()
        {
            var normal = new GeoVector3(0, 0, 1);
            var ellipse = new GeoEllipse3(new GeoPoint3(1, 2, 3), normal, new GeoVector3(1, 1, 0).Normalize(), 40, 10);
            DrawPolyline ring = Assert.Single(Draw(ellipse).Polylines);
            Vec3 major = new Vec3(ellipse.MajorAxis.X, ellipse.MajorAxis.Y, ellipse.MajorAxis.Z);
            Vec3 minor = new Vec3(ellipse.MinorAxis.X, ellipse.MinorAxis.Y, ellipse.MinorAxis.Z);

            Assert.True(ring.Closed);
            Assert.All(ring.Points, p =>
            {
                Vec3 d = p - new Vec3(1, 2, 3);
                double u = Vec3.Dot(d, major) / 40.0;
                double v = Vec3.Dot(d, minor) / 10.0;
                Assert.Equal(1.0, u * u + v * v, 9);
            });
        }

        [Fact]
        public void ARotatedRectangle_DrawsItsFourCorners()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(10, 20), 40, 10, Math.PI / 2.0);
            DrawPolyline outline = Assert.Single(Draw(rectangle).Polylines);

            Assert.Equal(4, outline.Points.Length);
            Assert.Equal(5.0, outline.Points.Min(p => p.X), 9);
            Assert.Equal(15.0, outline.Points.Max(p => p.X), 9);
            Assert.Equal(0.0, outline.Points.Min(p => p.Y), 9);
            Assert.Equal(40.0, outline.Points.Max(p => p.Y), 9);
        }

        [Fact]
        public void AFaceWithAHole_DrawsTheAreaItHasLessTheHole()
        {
            var face = (GeoFace2)SampleShapes.Make("GeoFace2", 1);
            ItemPrimitives item = Draw(face);

            Assert.Equal(face.Area, TriangleArea(item.Triangles), 6);
            Assert.Equal(2, item.Polylines.Count);
        }

        [Fact]
        public void AMeshOfAnLShape_DrawsTheTwoThousandSquareUnitsItCovers()
        {
            var mesh = new GeoPolygon3(SampleShapes.LShape().Select(p => new GeoPoint3(p.X, p.Y, 7.5))).ToMesh(Meshing.MeshKind.Convex);
            ItemPrimitives item = Draw(mesh);

            Assert.Equal(2000.0, TriangleArea(item.Triangles), 6);
            Assert.Equal(mesh.FaceCount, item.Polylines.Count);
        }

        [Fact]
        public void ABoxAndAnOrientedBox_DrawTwelveEdgesAndTwelveTriangles()
        {
            ItemPrimitives aabb = Draw(new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 20, 10)));
            ItemPrimitives obb = Draw(new GeoObb3(new GeoPoint3(0, 0, 0), 30, 20, 10, new GeoVector3(1, 1, 0).Normalize(), new GeoVector3(-1, 1, 0).Normalize()));

            Assert.Equal(12, aabb.Triangles.Count);
            Assert.Equal(6, aabb.Polylines.Count);
            Assert.Equal(2200.0, TriangleArea(aabb.Triangles), 6);
            Assert.Equal(2200.0, TriangleArea(obb.Triangles), 6);
            Assert.All(aabb.Triangles, t => Assert.True(Vec3.Dot(t.Normal, (t.A + t.B + t.C) * (1.0 / 3.0) - new Vec3(15, 10, 5)) > 0.0));
        }

        [Fact]
        public void ARayAndAPlane_ReachOutToTwiceTheDiagonalOfTheRestOfTheScene()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 20, 10)))
                .Add(new GeoRay3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0)))
                .Add(new GeoPlane3(new GeoPoint3(15, 10, 5), new GeoVector3(0, 0, 1)))
                .Scene;

            Primitives primitives = Primitives.Build(scene);
            double diagonal = Math.Sqrt(30 * 30 + 20 * 20 + 10 * 10);

            DrawPolyline ray = Assert.Single(primitives.Items[1].Polylines);
            Assert.Equal(2.0 * diagonal, Distance(ray.Points[0], ray.Points[1]), 9);

            // Neither stretches the box round the scene.
            Assert.Equal(new Vec3(30, 20, 10), primitives.Bounds.Max);
            Assert.Equal(new Vec3(0, 0, 0), primitives.Bounds.Min);
            Assert.Equal(2, primitives.Items[2].Triangles.Count);
        }

        [Fact]
        public void AShapeTheLibraryRefuses_StillDrawsItsOutlineAndIsWarnedOf()
        {
            // A polygon of two points is no polygon to the library, but the numbers are there to draw.
            const string json = "{\"items\":[{\"name\":\"thin\",\"layer\":\"l\",\"kind\":\"GeoPolygon2\",\"data\":{\"points\":[[0,0],[10,0]]}}]}";
            Scene scene = SceneReader.Read(json);

            Assert.Throws<ArgumentException>(() => scene.Items[0].ToGeometry());

            Primitives primitives = Primitives.Build(scene);
            Assert.Single(primitives.Items[0].Polylines);
            Assert.Empty(primitives.Warnings);

            IReadOnlyList<KeyValuePair<string, string>> numbers = ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]);
            Assert.Contains(numbers, pair => pair.Key == "Library" && pair.Value.StartsWith("cannot rebuild", StringComparison.Ordinal));
        }

        [Fact]
        public void AFaceOffItsPlane_KeepsItsOutlineAndWarnsWhereTheSurfaceCannotBeCut()
        {
            const string json = "{\"items\":[{\"name\":\"bent\",\"layer\":\"l\",\"kind\":\"GeoFace3\",\"data\":{\"outer\":[[0,0,0],[10,0,0],[10,10,5],[0,10,0]],\"holes\":[]}}]}";
            Primitives primitives = Primitives.Build(SceneReader.Read(json));

            Assert.Single(primitives.Items[0].Polylines);
            Assert.Empty(primitives.Items[0].Triangles);
            Assert.Single(primitives.Warnings);
            Assert.Contains("bent", primitives.Warnings[0], StringComparison.Ordinal);
        }

        [Fact]
        public void TheItemsOfThePrimitives_FollowTheItemsOfTheScene()
        {
            Scene scene = new SceneWriter().Add(new GeoPoint2(0, 0)).Add(new GeoLine2(0, 0, 3, 4)).Add(new GeoCircle2(new GeoPoint2(0, 0), 1)).Scene;
            Primitives primitives = Primitives.Build(scene);

            Assert.Equal(new[] { 0, 1, 2 }, primitives.Items.Select(i => i.ItemIndex));
            Assert.Equal(new Vec3(-1, -1, 0).X, primitives.Bounds.Min.X, 3);
            Assert.Equal(3.0, primitives.Bounds.Max.X, 9);
        }

        [Fact]
        public void APointOutsideTheNumbers_IsLeftOutOfTheBoundsSoOneBadCoordinateDoesNotHideTheScene()
        {
            Scene scene = new SceneWriter().Add(new GeoPoint3(double.NaN, 0, 0)).Add(new GeoPoint3(1, 2, 3)).Scene;
            Primitives primitives = Primitives.Build(scene);

            Assert.Equal(new Vec3(1, 2, 3), primitives.Bounds.Min);
            Assert.Equal(new Vec3(1, 2, 3), primitives.Bounds.Max);
        }

        private static double Distance(Vec3 a, Vec3 b) => (a - b).Length;

        private static double Distance(Vec3 a, GeoPoint2 b) => Distance(a, new Vec3(b.X, b.Y, 0.0));

        private static double Distance(Vec3 a, GeoPoint3 b) => Distance(a, new Vec3(b.X, b.Y, b.Z));
    }
}
