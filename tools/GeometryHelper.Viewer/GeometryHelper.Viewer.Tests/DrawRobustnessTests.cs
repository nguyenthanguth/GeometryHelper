using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class DrawRobustnessTests
    {
        [Theory]
        [InlineData(1000.0)]
        [InlineData(12000.0)]
        [InlineData(60000.0)]
        public void TheTargetIsNeverClipped_HoweverDeepTheZoomIntoALargeScene(double size)
        {
            var scene = new Bounds3(new Vec3(0, 0, 0), new Vec3(size, 300, 600));
            var camera = new ViewCamera { Target = new Vec3(size, 0, 0), Distance = 0.024 };

            camera.ClipPlanes(scene, out double near, out double far);

            Assert.True(near > 0.0);
            Assert.True(near < camera.Distance, "near " + near + " is past the target at " + camera.Distance);
            Assert.True(far > camera.Distance);
        }

        [Theory]
        [InlineData(1000.0)]
        [InlineData(12000.0)]
        [InlineData(60000.0)]
        public void AnOrthographicEye_StandsJustOutsideTheDepthOfTheScene_NotThreeRadiiAway(double size)
        {
            var scene = new Bounds3(new Vec3(0, 0, 0), new Vec3(size, 300, 600));
            var camera = new ViewCamera { Target = new Vec3(size, 0, 0), Distance = 0.024 };
            camera.SetView(StandardView.Top);
            camera.Target = new Vec3(size, 0, 0);

            camera.ClipPlanes(scene, out double near, out double far);

            // Seen from above the scene is 600 deep; the clip planes hold that and a twentieth of it either side.
            Assert.True(far - near < 600.0 * 1.2 + 1.0, "range " + (far - near));
            Assert.True(near > 0.0);

            Vec3 eye = camera.EyePosition(scene);
            Assert.True(eye.Z > 600.0, "the eye stands at z " + eye.Z + ", inside the scene");
            Assert.True(eye.Z < 600.0 + 0.1 * 600.0 + 1.0, "the eye stands at z " + eye.Z + ", far from the scene");

            foreach (Vec3 corner in new[] { scene.Min, scene.Max })
            {
                double depth = Vec3.Dot(corner - eye, camera.Forward);
                Assert.InRange(depth, near, far);
            }
        }

        [Fact]
        public void AFlatScene_SeenFromAbove_HasADepthRangeOfAFewHundredthsOfItsHeight()
        {
            var plan = new Bounds3(new Vec3(0, 0, 0), new Vec3(60000, 30000, 0));
            var camera = new ViewCamera();
            camera.SetView(StandardView.Top);
            camera.Fit(plan, 1.6);

            camera.ClipPlanes(plan, out double near, out double far);

            // So a step of the depth buffer is (far - near) / 2^24 = 5.6E-8 of the view's own depth, a few thousandths of a millimetre at most.
            Assert.True(far - near < camera.Distance * 0.1, "range " + (far - near));
        }

        [Fact]
        public void ACoordinateThatIsNotANumber_IsLeftOutOfTheDrawingWithAWarning()
        {
            const string json = "{\"items\":[{\"name\":\"p\",\"layer\":\"a\",\"kind\":\"GeoPolyline2\",\"data\":{\"points\":[[0,0],[\"NaN\",1],[2,0],[3,1]]}}]}";
            Scene scene = SceneReader.ReadJson(json);

            Primitives primitives = Primitives.Build(scene);

            // The chain is cut where the point was: the run [0,0] alone has no line, [2,0] to [3,1] is one.
            DrawPolyline line = Assert.Single(primitives.Items[0].Polylines);
            Assert.Equal(2, line.Points.Length);
            Assert.All(line.Points, p => Assert.True(p.IsFinite));
            Assert.Equal(new Vec3(3, 1, 0), primitives.Bounds.Max);
            Assert.Contains("1 coordinate", Assert.Single(primitives.Warnings), StringComparison.Ordinal);
        }

        [Fact]
        public void ATriangleOrAPointOrALabelThatIsNotANumber_GoesWhole()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoPoint2(double.NaN, 1))
                .Add(new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(0, double.PositiveInfinity, 0)))
                .Text(new GeoPoint3(double.NaN, 0, 0), "lost")
                .Add(new GeoPoint2(5, 6))
                .Scene;

            Primitives primitives = Primitives.Build(scene);

            Assert.Empty(primitives.Items[0].Points);
            Assert.Empty(primitives.Items[1].Triangles);
            Assert.Empty(primitives.Items[2].Labels);
            Assert.Equal(3, primitives.Warnings.Count);
            Assert.Equal(new Vec3(5, 6, 0), primitives.Bounds.Max);
            Assert.True(primitives.Bounds.Min.IsFinite);
        }

        [Fact]
        public void ABodyWhoseOpeningsTakeAllOfIt_HasNothingToDrawAndNoWarning()
        {
            GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
                => GeoSolid3.Extrude(new GeoPolygon3(new[] { new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y0, z0), new GeoPoint3(x1, y1, z0), new GeoPoint3(x0, y1, z0) }), new GeoVector3(0, 0, z1 - z0));

            var body = new GeoSolid3(Box(0, 0, 0, 10, 10, 10).Faces, new[] { Box(-1, -1, -1, 11, 11, 11) });

            Primitives primitives = Primitives.Build(new SceneWriter().Add(body).Scene);

            // Nothing is drawn but the outline, and the output says so: the library gives no triangle for a body whose openings take
            // all of it and none for one whose openings it could not cut, and the picture cannot tell which.
            Assert.Empty(primitives.Items[0].Triangles);
            Assert.NotEmpty(primitives.Items[0].Polylines);
            string warning = Assert.Single(primitives.Warnings);
            Assert.Contains("nothing left: the openings take all of it, or the library could not cut them", warning, StringComparison.Ordinal);
        }

        [Fact]
        public void ABodyWithoutOpenings_ThatGivesNoTriangle_IsNotCalledOneWhoseOpeningsTakeAll()
        {
            // Faces with no area the library can cut: the fallback face by face runs, and the warning is the fallback's.
            const string json = "{\"items\":[{\"name\":\"flat\",\"layer\":\"l\",\"kind\":\"GeoSolid3\",\"data\":{\"faces\":[]}}]}";

            Primitives primitives = Primitives.Build(SceneReader.ReadJson(json));

            Assert.DoesNotContain(primitives.Warnings, w => w.Contains("nothing left", StringComparison.Ordinal));
        }

        [Fact]
        public void TheLogOfTheLibrary_AtWarnAndAbove_IsCapturedOnTheThreadThatAskedAndTheRestPassesOn()
        {
            var passedOn = new List<string>();
            Action<GeometryHelper.Enums.GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            GeometryHelperLog.Writer = (level, message, exception) => { lock (passedOn) { passedOn.Add(message); } };

            try
            {
                using (LibraryLog log = LibraryLog.Begin())
                {
                    GeometryHelperLog.Warn("unworkable cut");
                    GeometryHelperLog.Info("quiet");
                    GeometryHelperLog.Err("failed", new InvalidOperationException("why"));

                    // Another thread has no capture open, so its line goes to the writer that was there.
                    var other = new System.Threading.Thread(() => GeometryHelperLog.Warn("elsewhere"));
                    other.Start();
                    other.Join();

                    Assert.Equal(new[] { "WARN unworkable cut", "ERR failed [InvalidOperationException: why]" }, log.Lines);
                    Assert.Equal(new[] { "elsewhere" }, passedOn);
                }

                // Once the last capture is closed the writer that was there is back.
                GeometryHelperLog.Warn("after");
                Assert.Equal(new[] { "elsewhere", "after" }, passedOn);
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }
        }

        [Fact]
        public void CapturesNest_EachLineGoesToTheInnermost_AndTheWriterComesBackAtTheLastEnd()
        {
            Action<GeometryHelper.Enums.GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;

            using (LibraryLog outer = LibraryLog.Begin())
            {
                GeometryHelperLog.Warn("one");

                using (LibraryLog inner = LibraryLog.Begin())
                {
                    GeometryHelperLog.Warn("two");
                    Assert.Equal(new[] { "WARN two" }, inner.Lines);
                }

                GeometryHelperLog.Warn("three");
                Assert.Equal(new[] { "WARN one", "WARN three" }, outer.Lines);
            }

            Assert.Equal(before, GeometryHelperLog.Writer);
        }

        [Fact]
        public void ALoopWithAPointNotANumber_KeepsItsClosingSegmentWhereBothEndsAreFinite()
        {
            const string json = "{\"items\":[{\"name\":\"sq\",\"layer\":\"l\",\"kind\":\"GeoPolygon2\",\"data\":{\"points\":[[0,0],[10,0],[\"NaN\",10],[0,10]]}}]}";

            Primitives primitives = Primitives.Build(SceneReader.ReadJson(json));

            // The loop 0,0 - 10,0 - NaN - 0,10 closes from 0,10 to 0,0: that edge stays, so the run is 0,10 - 0,0 - 10,0.
            DrawPolyline line = Assert.Single(primitives.Items[0].Polylines);
            Assert.False(line.Closed);
            Assert.Equal(new[] { new Vec3(0, 10, 0), new Vec3(0, 0, 0), new Vec3(10, 0, 0) }, line.Points);
        }

        [Fact]
        public void ALoopWhoseFirstPointIsNotANumber_HasNoClosingSegmentToKeep()
        {
            const string json = "{\"items\":[{\"name\":\"sq\",\"layer\":\"l\",\"kind\":\"GeoPolygon2\",\"data\":{\"points\":[[\"NaN\",0],[10,0],[10,10],[0,10]]}}]}";

            Primitives primitives = Primitives.Build(SceneReader.ReadJson(json));

            DrawPolyline line = Assert.Single(primitives.Items[0].Polylines);
            Assert.Equal(new[] { new Vec3(10, 0, 0), new Vec3(10, 10, 0), new Vec3(0, 10, 0) }, line.Points);
        }

        [Fact]
        public void ABodyTheLibraryCannotCutAsAWhole_IsDrawnFaceByFaceAndWarnedOf()
        {
            // A box of which the top is not flat: five faces are fine and the sixth the library refuses at any tolerance.
            const string json = "{\"items\":[{\"name\":\"bent box\",\"layer\":\"l\",\"kind\":\"GeoSolid3\",\"data\":{\"faces\":["
                + "{\"outer\":[[0,0,0],[10,0,0],[10,10,0],[0,10,0]],\"holes\":[]},"
                + "{\"outer\":[[0,0,0],[0,10,0],[0,10,10],[0,0,10]],\"holes\":[]},"
                + "{\"outer\":[[10,0,0],[10,0,10],[10,10,10],[10,10,0]],\"holes\":[]},"
                + "{\"outer\":[[0,0,0],[0,0,10],[10,0,10],[10,0,0]],\"holes\":[]},"
                + "{\"outer\":[[0,10,0],[10,10,0],[10,10,10],[0,10,10]],\"holes\":[]},"
                + "{\"outer\":[[0,0,10],[10,0,10],[10,10,12],[0,10,10]],\"holes\":[]}],\"openings\":[]}}]}";

            Primitives primitives = Primitives.Build(SceneReader.ReadJson(json));

            Assert.Equal(10, primitives.Items[0].Triangles.Count);
            Assert.Equal(6, primitives.Items[0].Polylines.Count);
            string warning = Assert.Single(primitives.Warnings);
            Assert.Contains("drawn one by one", warning, StringComparison.Ordinal);
            Assert.Contains("1 of them could not be drawn", warning, StringComparison.Ordinal);
        }

        [Fact]
        public void TheBoxOfACircle_IsTheExactCurve_NotItsChords()
        {
            // A tilted circle at a coarse point tolerance: the chords stop short of the extremes by up to the chord tolerance.
            var circle = new GeoCircle3(new GeoPoint3(100, 200, 300), new GeoVector3(0.3, -0.5, 0.8), 50.0);
            var writer = new SceneWriter();
            writer.Scene.EqualPoint = 10.0;
            writer.Add(circle);

            Primitives primitives = Primitives.Build(writer.Scene);

            double nx = circle.Normal.X;
            double nz = circle.Normal.Z;
            Assert.Equal(100.0 + 50.0 * Math.Sqrt(1.0 - nx * nx), primitives.Bounds.Max.X, 9);
            Assert.Equal(300.0 - 50.0 * Math.Sqrt(1.0 - nz * nz), primitives.Bounds.Min.Z, 9);
        }

        [Fact]
        public void ACurveBeyondTheCap_IsWarnedOf()
        {
            var writer = new SceneWriter().Add(new GeoCircle2(new GeoPoint2(0, 0), 1E9));

            Primitives primitives = Primitives.Build(writer.Scene);

            Assert.Equal(65536, primitives.Items[0].Polylines[0].Points.Length);
            Assert.Contains("65536 pieces", Assert.Single(primitives.Warnings), StringComparison.Ordinal);
        }

        [Fact]
        public void TheChordToleranceFollowsTheSceneTolerance()
        {
            var fine = new SceneWriter();
            fine.Scene.EqualPoint = 1E-3;
            fine.Add(new GeoCircle2(new GeoPoint2(0, 0), 500));
            var coarse = new SceneWriter();
            coarse.Scene.EqualPoint = 1.0;
            coarse.Add(new GeoCircle2(new GeoPoint2(0, 0), 500));

            int fineCount = Primitives.Build(fine.Scene).Items[0].Polylines[0].Points.Length;
            int coarseCount = Primitives.Build(coarse.Scene).Items[0].Polylines[0].Points.Length;

            Assert.True(fineCount > coarseCount * 3);
        }

        [Fact]
        public void TheProperties_ShowTheCoordinatesAndAreasToTenDigits_AndTheLibrarysOwnBounds()
        {
            var polygon = new GeoPolygon3(new[] { new GeoPoint3(59999.0003, 12345.6789, 5), new GeoPoint3(60000.0008, 12345.6789, 5), new GeoPoint3(60000.0008, 12345.6839, 5), new GeoPoint3(59999.0003, 12345.6839, 5) });
            Scene scene = new SceneWriter().Add(polygon).Scene;
            Primitives primitives = Primitives.Build(scene);

            Dictionary<string, string> numbers = ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]).ToDictionary(p => p.Key, p => p.Value);

            Assert.Equal("(59999.0003, 12345.6789, 5)", numbers["Bounds min (library)"]);
            Assert.Equal("(60000.0008, 12345.6839, 5)", numbers["Bounds max (library)"]);
            Assert.StartsWith("(1.0005, 0.00499", numbers["Bounds size"], StringComparison.Ordinal);
            Assert.Equal((polygon.Area).ToString("G10", System.Globalization.CultureInfo.InvariantCulture), numbers["Area"]);
        }

        [Fact]
        public void NamingTwentyThousandItemsOfOneKind_IsNotAPassOverAllTheItemsEachTime()
        {
            var writer = new SceneWriter();
            var watch = Stopwatch.StartNew();

            for (int i = 0; i < 20000; i++)
            {
                writer.Add(new GeoPoint2(i, 0));
            }

            watch.Stop();

            Assert.Equal("GeoPoint2 20000", writer.Scene.Items[19999].Name);
            Assert.Equal("GeoPoint2 1", writer.Scene.Items[0].Name);

            // A pass over the items for each name would be 200 million comparisons; this is a few hundred milliseconds of encoding.
            Assert.True(watch.ElapsedMilliseconds < 5000, "took " + watch.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void AWriterOverASceneThatExists_CountsOnFromItsItems()
        {
            var first = new SceneWriter().Add(new GeoPoint2(0, 0)).Add(new GeoPoint2(1, 1));

            var second = new SceneWriter(first.Scene).Add(new GeoPoint2(2, 2));

            Assert.Equal("GeoPoint2 3", second.Scene.Items[2].Name);
        }
    }
}
