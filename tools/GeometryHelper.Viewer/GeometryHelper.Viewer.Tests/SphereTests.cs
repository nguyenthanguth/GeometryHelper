using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The ball in the viewer: its kind in a scene, its drawing as a fine mesh at the chord rule of the scene with three great circles,
    /// its numbers, its centre and circles to snap to, the pick of its surface, the section a clip plane leaves on it, and the files
    /// the writers make of it.
    /// </summary>
    public class SphereTests
    {
        private static readonly GeoPoint3 Centre = new GeoPoint3(100.25, -40.5, 7.125);

        private static Scene Ball(double radius) => new SceneWriter().Add(new GeoSphere3(Centre, radius), name: "ball").Scene;

        private static double Area(IEnumerable<DrawTriangle> triangles) => triangles.Sum(t => Vec3.Cross(t.B - t.A, t.C - t.A).Length * 0.5);

        private static double Away(Vec3 point) => (point - new Vec3(Centre.X, Centre.Y, Centre.Z)).Length;

        private static string G10(double value) => value.ToString("G10", CultureInfo.InvariantCulture);

        [Fact]
        public void TheKindIsKnown_AWriterTakesIt_AndItSurvivesTheJsonOfAScene()
        {
            Assert.True(SceneKinds.IsKnown("GeoSphere3"));

            var ball = new GeoSphere3(new GeoPoint3(10.25, -3.5, 7.125), 12.375);
            Scene read = SceneReader.Read(new SceneWriter().Add(ball).ToJson());

            Assert.Equal("GeoSphere3", Assert.Single(read.Items).Kind);
            Assert.Equal(ball, (GeoSphere3)read.Items[0].ToGeometry());
        }

        [Fact]
        public void ABallOfRadiusTwo_IsMeshedWithinTheChordRuleOfTheScene_ExactlyAsTheLibraryMeshesIt()
        {
            Scene scene = Ball(2.0);
            Primitives primitives = Primitives.Build(scene);
            ItemPrimitives drawn = primitives.Items[0];

            // The chord rule is a tenth of the point tolerance.
            GeoTriangle3[] library = new GeoSphere3(Centre, 2.0).TriangulateSurface(scene.EqualPoint / 10.0);

            Assert.Equal(library.Length, drawn.Triangles.Count);
            Assert.Empty(primitives.Warnings);
            Assert.True(drawn.IsClosedBody);
        }

        [Fact]
        public void ABallOfRadiusFifty_IsDrawnWithAFineMeshOnTheSphere_ThreeGreatCircles_AndADotAtItsCentre()
        {
            Primitives primitives = Primitives.Build(Ball(50.0));
            ItemPrimitives drawn = primitives.Items[0];

            Assert.Empty(primitives.Warnings);
            Assert.True(drawn.IsClosedBody);
            Assert.InRange(drawn.Triangles.Count, 100000, PrimitiveBuilder.MostBallTriangles);
            Assert.All(drawn.Triangles, t => Assert.Equal(50.0, Away(t.A), 9));
            Assert.Equal(new Vec3(Centre.X, Centre.Y, Centre.Z), Assert.Single(drawn.Points));

            // Three circles, closed, each in a plane through the centre square to an axis, every point on the sphere.
            Assert.Equal(3, drawn.Polylines.Count);
            Assert.All(drawn.Polylines, line => Assert.True(line.Closed));
            Assert.All(drawn.Polylines.SelectMany(line => line.Points), p => Assert.Equal(50.0, Away(p), 9));
            Assert.All(drawn.Polylines[0].Points, p => Assert.Equal(Centre.X, p.X, 9));
            Assert.All(drawn.Polylines[1].Points, p => Assert.Equal(Centre.Y, p.Y, 9));
            Assert.All(drawn.Polylines[2].Points, p => Assert.Equal(Centre.Z, p.Z, 9));
        }

        [Fact]
        public void TheBoxOfABall_IsTheBoxOfTheSphereToTheLastDigit_NotOfItsMesh()
        {
            Primitives primitives = Primitives.Build(Ball(50.0));

            Assert.Equal(Centre.X - 50.0, primitives.Bounds.Min.X, 9);
            Assert.Equal(Centre.Y - 50.0, primitives.Bounds.Min.Y, 9);
            Assert.Equal(Centre.Z - 50.0, primitives.Bounds.Min.Z, 9);
            Assert.Equal(Centre.X + 50.0, primitives.Bounds.Max.X, 9);
            Assert.Equal(Centre.Y + 50.0, primitives.Bounds.Max.Y, 9);
            Assert.Equal(Centre.Z + 50.0, primitives.Bounds.Max.Z, 9);
        }

        [Fact]
        public void TheMeshOfABall_HoldsItsAreaToAFewPartsInAHundredThousand_AndNeverOverIt()
        {
            ItemPrimitives drawn = Primitives.Build(Ball(50.0)).Items[0];
            double area = Area(drawn.Triangles);
            var ball = new GeoSphere3(Centre, 50.0);

            // The corners lie on the sphere, so the mesh lies inside it: a little under, never over.
            Assert.InRange(area, ball.SurfaceArea * (1.0 - 1E-4), ball.SurfaceArea);
        }

        [Fact]
        public void ABallSoBigThatItsMeshWouldPassTheCap_IsCutInNoMoreTrianglesThanTheCap_AndSaysSo()
        {
            Primitives primitives = Primitives.Build(Ball(5000.0));
            ItemPrimitives drawn = primitives.Items[0];

            Assert.InRange(drawn.Triangles.Count, 100000, PrimitiveBuilder.MostBallTriangles);
            Assert.Contains("triangles", drawn.Warning);
            Assert.Contains("ball", drawn.Warning);
            Assert.Single(primitives.Warnings);

            // The circles are cut by the rule whatever the mesh is, and the box is the sphere's.
            Assert.All(drawn.Polylines.SelectMany(line => line.Points), p => Assert.Equal(5000.0, Away(p), 7));
            Assert.Equal(Centre.X + 5000.0, primitives.Bounds.Max.X, 7);
        }

        [Fact]
        public void ABallTheLibraryRefuses_StillShowsItsCirclesAndSaysWhy()
        {
            // A scene read from a file can hold a radius the library will not make a ball of.
            Scene scene = Ball(3.0);
            scene.Items[0].Data = JsonDocument.Parse("{\"center\":[0,0,0],\"radius\":-3}").RootElement;

            ItemPrimitives drawn = Primitives.Build(scene).Items[0];

            Assert.Empty(drawn.Triangles);
            Assert.False(drawn.IsClosedBody);
            Assert.NotNull(drawn.Warning);

            // The circles are read from the numbers and are still there: three, closed, round the centre at the distance the radius gives.
            Assert.Equal(3, drawn.Polylines.Count);
            Assert.All(drawn.Polylines, line => Assert.True(line.Closed));
            Assert.All(drawn.Polylines.SelectMany(line => line.Points), p => Assert.Equal(3.0, p.Length, 9));
            Assert.Equal(new Vec3(0, 0, 0), Assert.Single(drawn.Points));
        }

        [Fact]
        public void FortyBallsOfRadiusOneHundred_AreCutAtOneCoarserToleranceToFitTheBudgetOfTheScene_AndTheSceneIsWarnedOnce()
        {
            var writer = new SceneWriter();

            for (int i = 0; i < 40; i++)
            {
                writer.Add(new GeoSphere3(new GeoPoint3(i * 250.0, 0, 0), 100.0), name: "ball " + i);
            }

            Primitives primitives = Primitives.Build(writer.Scene);
            long total = primitives.Items.Sum(item => (long)item.Triangles.Count);

            // At the chord tolerance of the scene each would take 262 144 at most, 10.2 million in all.
            Assert.InRange(total, 3500000, PrimitiveBuilder.MostBallTrianglesInAScene);
            Assert.All(primitives.Items, item => Assert.Equal(primitives.Items[0].Triangles.Count, item.Triangles.Count));
            Assert.All(primitives.Items, item => Assert.Null(item.Warning));

            string said = Assert.Single(primitives.Warnings);
            Assert.Contains("40 ball(s)", said);
            Assert.Contains("budget of 4000000", said);
            Assert.DoesNotContain("still over", said);

            // Every ball is on its sphere whatever its tolerance.
            Assert.All(primitives.Items[7].Triangles, t => Assert.Equal(100.0, (t.A - new Vec3(7 * 250.0, 0, 0)).Length, 9));
        }

        [Fact]
        public void BallsThatFitTheBudgetTogether_AreCutAsTheyWouldBeAlone()
        {
            var writer = new SceneWriter();

            for (int i = 0; i < 5; i++)
            {
                writer.Add(new GeoSphere3(new GeoPoint3(i * 100.0, 0, 0), 20.0));
            }

            Primitives primitives = Primitives.Build(writer.Scene);
            int alone = Primitives.Build(new SceneWriter().Add(new GeoSphere3(GeoPoint3.Origin, 20.0)).Scene).Items[0].Triangles.Count;

            Assert.All(primitives.Items, item => Assert.Equal(alone, item.Triangles.Count));
            Assert.Empty(primitives.Warnings);
        }

        [Fact]
        public void BallsOfDifferentRadii_ShareOneToleranceInASceneOverTheBudget_TheBiggerTheMoreTriangles()
        {
            var writer = new SceneWriter();

            for (int i = 0; i < 30; i++)
            {
                writer.Add(new GeoSphere3(new GeoPoint3(i * 500.0, 0, 0), i % 2 == 0 ? 100.0 : 25.0));
            }

            Primitives primitives = Primitives.Build(writer.Scene);

            Assert.InRange(primitives.Items.Sum(item => (long)item.Triangles.Count), 1, PrimitiveBuilder.MostBallTrianglesInAScene);
            Assert.True(primitives.Items[0].Triangles.Count > primitives.Items[1].Triangles.Count);
            Assert.Single(primitives.Warnings);
        }

        [Fact]
        public void TheNumbersOfABall_AreItsCentreRadiusVolumeAndArea_AsTheLibraryGivesThem()
        {
            Scene scene = Ball(12.5);
            Primitives primitives = Primitives.Build(scene);
            var ball = new GeoSphere3(Centre, 12.5);

            Dictionary<string, string> numbers = ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]).ToDictionary(p => p.Key, p => p.Value);

            Assert.Equal("12.5", numbers["Radius"]);
            Assert.Equal(G10(ball.Volume), numbers["Volume"]);
            Assert.Equal(G10(ball.SurfaceArea), numbers["Surface area"]);
            Assert.Equal("100.25, -40.5, 7.125", numbers["Center"]);
            Assert.Equal("(" + G10(Centre.X - 12.5) + ", " + G10(Centre.Y - 12.5) + ", " + G10(Centre.Z - 12.5) + ")", numbers["Bounds min (library)"]);
        }

        [Fact]
        public void TheCentreOfABall_SnapsAsACentre_ExactlyWhereItIs()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoSphere3(new GeoPoint3(10, 20, 0), 40.0))
                .Add(new GeoLine2(new GeoPoint2(-100, -100), new GeoPoint2(100, -100))));

            (double x, double y) = view.Pixel(new Vec3(10, 20, 0));
            SnapHit hit = SnapEngine.Find(view.Geometry, view.Map, x + 2.0, y - 1.5, SnapKinds.Centre, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null);

            Assert.Equal("centre", hit.Name);
            Assert.Equal(new Vec3(10, 20, 0), hit.Point);
            Assert.Equal(0, hit.ItemIndex);
        }

        [Fact]
        public void TheNearestPointOfABallsGreatCircle_IsOnTheSphereToTheLastDigit()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoSphere3(new GeoPoint3(0, 0, 0), 40.0)));

            // The equator seen from above is the rim of the disc: a point of it at 30 degrees, the cursor two pixels outside it.
            double angle = 30.0 * Math.PI / 180.0;
            (double x, double y) = view.Pixel(new Vec3(40.0 * Math.Cos(angle), 40.0 * Math.Sin(angle), 0));
            SnapHit hit = SnapEngine.Find(view.Geometry, view.Map, x + 2.0 * Math.Cos(angle), y - 2.0 * Math.Sin(angle), SnapKinds.Nearest, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null);

            Assert.Equal("nearest", hit.Name);
            Assert.Equal(40.0, hit.Point.Length, 12);
        }

        [Fact]
        public void TheSurfaceOfABallSeenFromAbove_IsPickedWhereTheRayMeetsItsMesh_WithinTheSagOfTheSphere()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoSphere3(new GeoPoint3(0, 0, 0), 40.0)));

            // Off the two great circles that cross at the top, which a click on them picks instead.
            double height = Math.Sqrt(40.0 * 40.0 - 20.0 * 20.0 - 20.0 * 20.0);
            (double x, double y) = view.Pixel(new Vec3(20, 20, height));
            PickHit hit = PickEngine.Pick(view.Primitives, view.Geometry, view.Map, x, y, null, true);

            Assert.Equal(PickKind.Surface, hit.Kind);
            Assert.Equal(0, hit.ItemIndex);
            Assert.Equal(40.0, hit.Point.Length, 2);
            Assert.Equal(height, hit.Point.Z, 2);
            Assert.True(hit.Normal.Z > 0.7, "the normal of the triangle hit is " + hit.Normal);
        }

        [Fact]
        public void ABallCutByAClipPlaneThroughItsCentre_HasACapOfTheDiscItLeaves()
        {
            Primitives primitives = Primitives.Build(Ball(50.0));
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(Centre.X, Centre.Y, Centre.Z), new Vec3(0, 0, 1)) });

            ClipResult result = ClipResult.Build(primitives, clips, true);

            IReadOnlyList<DrawTriangle> cap = result.Caps[0];
            Assert.NotEmpty(cap);
            Assert.Equal(0, result.CapsRefused);

            // The section of the mesh is a polygon inside the circle of the sphere: its area is the disc's to a few parts in a thousand.
            Assert.InRange(Area(cap), Math.PI * 2500.0 * (1.0 - 1E-3), Math.PI * 2500.0);
            Assert.All(cap, t => Assert.Equal(Centre.Z, t.A.Z, 9));
        }

        [Fact]
        public void ABallCutOffCentre_HasACapOfTheSmallerDisc()
        {
            Primitives primitives = Primitives.Build(Ball(50.0));
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(Centre.X, Centre.Y, Centre.Z + 30.0), new Vec3(0, 0, 1)) });

            ClipResult result = ClipResult.Build(primitives, clips, true);

            // A plane 30 above the centre of a ball of radius 50 cuts a circle of radius 40.
            Assert.InRange(Area(result.Caps[0]), Math.PI * 1600.0 * (1.0 - 1E-3), Math.PI * 1600.0);
        }

        [Fact]
        public void TheGapBetweenABallAndABox_IsTheLibrarysExactDistance()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoSphere3(new GeoPoint3(0, 0, 0), 10.0))
                .Add(new GeoAabb3(new GeoPoint3(30, -5, -5), new GeoPoint3(40, 5, 5)))
                .Scene;

            LibraryDistance gap = LibraryMeasure.Between(scene, 0, 1);

            Assert.Equal(20.0, gap.Distance.Value, 12);
            Assert.Equal(10.0, gap.From.Value.X, 12);
            Assert.Equal(30.0, gap.To.Value.X, 12);
        }

        [Fact]
        public void TheDiagnosticsOfAScene_TakeABallAsAPieceWithItsVolume_AndFindNothingWrongWithIt()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoSphere3(new GeoPoint3(0, 0, 0), 10.0), name: "ball")
                .Add(new GeoAabb3(new GeoPoint3(30, 0, 0), new GeoPoint3(40, 10, 10)), name: "box")
                .Scene;

            DiagnosticReport pieces = Diag.Run(scene, DiagnosticChecks.Pieces);
            DiagnosticFinding piece = Assert.Single(Diag.Of(pieces, DiagnosticKind.Piece));
            Assert.Contains("volume", piece.Message);
            Assert.Equal("GeoSphere3.Volume", piece.Source);
            Assert.Equal(new GeoSphere3(GeoPoint3.Origin, 10.0).Volume, piece.Size, 9);

            // Every other check runs over it with no word of complaint about what it is.
            DiagnosticReport all = Diag.Run(scene, DiagnosticChecks.All & ~DiagnosticChecks.Gap & ~DiagnosticChecks.Pieces & ~DiagnosticChecks.Clash);
            Assert.DoesNotContain(all.Findings, f => f.ItemName == "ball");
        }

        [Fact]
        public void TheGapCheck_MeasuresABallToAnotherItemWithTheLibrarysExactDistance()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoSphere3(new GeoPoint3(0, 0, 0), 10.0), name: "ball")
                .Add(new GeoPoint3(25, 0, 0), name: "dot")
                .Scene;

            DiagnosticReport report = Diag.Run(scene, DiagnosticChecks.Gap, true, 0, 1);

            DiagnosticFinding gap = Assert.Single(Diag.Of(report, DiagnosticKind.Gap));
            Assert.Equal(15.0, gap.Size, 12);
        }

        [Fact]
        public void ABallWrittenAsADxf_ComesBackAsAMeshOfTheTrianglesWritten_OnItsLayer()
        {
            var ball = new GeoSphere3(Centre, 20.0);
            Scene read = SceneImport.ReadText(new DxfWriter().Add(ball, 0.05, "balls").ToString(), "dxf");

            Assert.Equal("GeoMesh3", Assert.Single(read.Items).Kind);
            Assert.Equal(ball.TriangulateSurface(0.05).Length, GeometryDecoder.Faces(read.Items[0].Data).Count());
            Assert.Equal("balls", read.Items[0].Layer);
        }

        [Fact]
        public void ABallWrittenAsAnObjAndAsWkt_ComesBackAsMeshesOfTheTrianglesWritten()
        {
            var ball = new GeoSphere3(Centre, 20.0);
            int triangles = ball.TriangulateSurface(0.05).Length;

            Scene obj = SceneImport.ReadText(new ObjWriter().Add(ball, 0.05, "ball").ToString(), "obj");
            Scene wkt = SceneImport.ReadText(Wkt.Write(ball, 0.05), "wkt");

            Assert.Equal(triangles, GeometryDecoder.Faces(obj.Items.First(i => i.Kind == "GeoMesh3").Data).Count());
            Assert.Equal(triangles, GeometryDecoder.Faces(wkt.Items.First(i => i.Kind == "GeoMesh3").Data).Count());
        }
    }
}
