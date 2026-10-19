using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class SceneToleranceTests
    {
        private static readonly Tolerance Fine = new Tolerance(1E-3, 1E-5, Math.PI / 180.0, 1E-2);

        private static GeoFace3 FaceFiveThousandthsOffFlat()
            => new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0.005), new GeoPoint3(0, 100, 0) }));

        [Fact]
        public void AWriterRecordsAllFourThresholdsOfTheToleranceInForceWhereItIsMade()
        {
            using (Tolerance.Use(Fine))
            {
                var writer = new SceneWriter();

                Assert.Equal(1E-3, writer.Scene.EqualPoint);
                Assert.Equal(1E-5, writer.Scene.EqualVector);
                Assert.Equal(Math.PI / 180.0, writer.Scene.EqualAngleRad);
                Assert.Equal(1E-2, writer.Scene.EqualPlanar);
                Assert.Equal(Fine, writer.Scene.Tolerance);
            }

            Assert.Equal(Tolerance.Default, new SceneWriter().Scene.Tolerance);
        }

        [Fact]
        public void TheFourThresholds_SurviveTheFile()
        {
            var writer = new SceneWriter();
            writer.Scene.SetTolerance(new Tolerance(0.25, 1E-4, 0.02, 0.5));

            Scene read = SceneReader.ReadJson(writer.ToJson());

            Assert.Equal(new Tolerance(0.25, 1E-4, 0.02, 0.5), read.Tolerance);
        }

        [Fact]
        public void ASceneThatSaysOnlyItsPointTolerance_HasThePlanarToleranceFollowIt()
        {
            Scene read = SceneReader.ReadJson("{\"equalPoint\":0.05,\"items\":[]}");

            Assert.Equal(0.05, read.EqualPlanar);
            Assert.Equal(Tolerance.DefaultEqualVector, read.EqualVector);
        }

        [Fact]
        public void AFaceBuiltUnderAPlanarToleranceOfOneHundredth_IsDrawnAndMeasuredUnderIt()
        {
            SceneWriter writer;

            using (Tolerance.Use(Fine))
            {
                writer = new SceneWriter().Add(FaceFiveThousandthsOffFlat()) ;
            }

            Primitives primitives = Primitives.Build(writer.Scene);

            Assert.Empty(primitives.Warnings);
            Assert.True(primitives.Items[0].Triangles.Count > 0);

            // At the default planar tolerance of 1E-3 the library would not take it: that is what happened without the scene's own.
            Assert.Throws<ArgumentException>(() => { using (Tolerance.Use(Tolerance.Default)) { writer.Scene.Items[0].ToGeometry(); } });

            var numbers = ItemMeasures.Describe(writer.Scene.Items[0], writer.Scene, primitives.Items[0]).ToDictionary(p => p.Key, p => p.Value);
            Assert.DoesNotContain("Library", numbers.Keys);
            Assert.Contains("Area", numbers.Keys);
        }

        [Fact]
        public async System.Threading.Tasks.Task ATolSetInAScript_WritesAllFourThresholds()
        {
            var result = await Scripting.ScriptHost.RunAsync("Tol = new Tolerance(1E-3, 1E-5, Math.PI / 180, 1E-2);\nShow(new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0.005), new GeoPoint3(0, 100, 0) })));", null, null, System.Threading.CancellationToken.None);

            Assert.True(result.Succeeded, string.Join("\n", result.Errors));
            Assert.Equal(1E-2, result.Scene.EqualPlanar);

            Primitives primitives = Primitives.Build(result.Scene);
            Assert.Empty(primitives.Warnings);
            Assert.True(primitives.Items[0].Triangles.Count > 0);
        }

        [Fact]
        public void AShapeRefusedAtTheSceneToleranceButNotAt1E12_IsRebuiltThereAndSaysSo()
        {
            // A triangle 0.0004 across is no polygon at 1E-3, and an exact one at 1E-12.
            const string json = "{\"items\":[{\"name\":\"tiny\",\"layer\":\"l\",\"kind\":\"GeoPolygon3\",\"data\":{\"points\":[[0,0,0],[0.0004,0,0],[0,0.0004,0]]}}]}";
            Scene scene = SceneReader.ReadJson(json);

            Assert.Throws<ArgumentException>(() => { using (Tolerance.Use(scene.Tolerance)) { scene.Items[0].ToGeometry(); } });

            object shape = scene.Rebuild(scene.Items[0], out string note);

            Assert.IsType<GeoPolygon3>(shape);
            Assert.StartsWith("rebuilt and measured at 1E-12", note, StringComparison.Ordinal);

            Primitives primitives = Primitives.Build(scene);
            var numbers = ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]);
            Assert.Contains(numbers, p => p.Key == "Library" && p.Value.StartsWith("rebuilt and measured at 1E-12", StringComparison.Ordinal));
            Assert.Contains(numbers, p => p.Key == "Area");
        }

        [Fact]
        public void AShapeRefusedAtBothTolerances_StillShowsItsVertexCountAndItsDrawnBounds()
        {
            const string json = "{\"items\":[{\"name\":\"two\",\"layer\":\"l\",\"kind\":\"GeoPolygon2\",\"data\":{\"points\":[[1,2],[11,2]]}}]}";
            Scene scene = SceneReader.ReadJson(json);
            Primitives primitives = Primitives.Build(scene);

            var numbers = ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]).ToDictionary(p => p.Key, p => p.Value);

            Assert.StartsWith("cannot rebuild the shape, even at 1E-12", numbers["Library"], StringComparison.Ordinal);
            Assert.Equal("2", numbers["Vertices (raw)"]);
            Assert.Equal("(1, 2, 0)", numbers["Bounds min"]);
            Assert.Equal("(11, 2, 0)", numbers["Bounds max"]);
        }

        [Fact]
        public void TheMinimalTolerance_IsNotAllZeros()
        {
            Tolerance minimal = Scene.MinimalTolerance;

            Assert.Equal(1E-12, minimal.EqualPoint);
            Assert.Equal(1E-12, minimal.EqualVector);
            Assert.Equal(1E-12, minimal.EqualPlanar);
            Assert.NotEqual(new Tolerance(0, 0, 0, 0), minimal);
        }

        [Fact]
        public void APolygonFinerThanTheScene_IsWarnedOfWhereTheLibraryMergesItsVertices()
        {
            GeoPolygon2 square;

            using (Tolerance.Use(new Tolerance(1E-5, 1E-7)))
            {
                square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100.0004, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));
            }

            Assert.Equal(5, square.VertexCount);

            // The writer was made outside the scope, so the scene says 1E-3, at which the library keeps four of the five.
            var writer = new SceneWriter().Add(square);
            Primitives primitives = Primitives.Build(writer.Scene);

            Assert.Equal(5, primitives.Items[0].Polylines[0].Points.Length);

            var numbers = ItemMeasures.Describe(writer.Scene.Items[0], writer.Scene, primitives.Items[0]).ToList();
            KeyValuePair<string, string> warning = Assert.Single(numbers, p => p.Key == "Warning");
            Assert.Contains("keeps 4 of the 5 vertices", warning.Value, StringComparison.Ordinal);
        }

        [Fact]
        public void AShapeOfAFineSceneIsRebuiltAtTheFineTolerance_AndNeedsNoWarning()
        {
            SceneWriter writer;

            using (Tolerance.Use(new Tolerance(1E-5, 1E-7)))
            {
                writer = new SceneWriter().Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100.0004, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100)));
            }

            Primitives primitives = Primitives.Build(writer.Scene);
            var numbers = ItemMeasures.Describe(writer.Scene.Items[0], writer.Scene, primitives.Items[0]).ToDictionary(p => p.Key, p => p.Value);

            Assert.Equal("5", numbers["Vertices"]);
            Assert.DoesNotContain("Warning", numbers.Keys);
            Assert.DoesNotContain("Library", numbers.Keys);
        }
        [Theory]
        [InlineData("-1")]
        [InlineData("\"NaN\"")]
        [InlineData("\"Infinity\"")]
        public void AnEqualPointThatIsNegativeOrNotANumber_IsRefusedWithAMessageNamingIt(string value)
        {
            string json = "{\"equalPoint\":" + value + ",\"items\":[{\"kind\":\"GeoPoint2\",\"data\":[1,2]}]}";

            FormatException error = Assert.Throws<FormatException>(() => SceneReader.ReadJson(json));

            Assert.Contains("equalPoint", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async System.Threading.Tasks.Task ASceneAtAToleranceOfNought_SavesAndReopens()
        {
            var result = await Scripting.ScriptHost.RunAsync("Tol = new Tolerance(0, 0);\nShow(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10)));", null, null, System.Threading.CancellationToken.None);
            Assert.True(result.Succeeded, string.Join("\n", result.Errors));
            Assert.Equal(0.0, result.Scene.EqualPoint);

            string json = new SceneWriter(result.Scene).ToJson();
            Scene read = SceneReader.ReadJson(json);

            Assert.Equal(0.0, read.EqualPoint);
            Assert.Equal(json, new SceneWriter(read).ToJson());

            // Drawn at a chord tolerance that is not nought, and rebuilt at the nought the scene asks.
            Primitives primitives = Primitives.Build(read);
            Assert.Empty(primitives.Warnings);
            Assert.IsType<GeoPolygon2>(read.Rebuild(read.Items[0], out string note));
            Assert.Null(note);
        }

        [Theory]
        [InlineData("{\"layers\":[1]}")]
        [InlineData("{\"items\":[1]}")]
        [InlineData("{\"items\":[{\"kind\":\"GeoPoint2\"}],\"version\":1.5}")]
        [InlineData("{\"equalVector\":-5,\"items\":[]}")]
        [InlineData("{\"items\":[{\"kind\":7}]}")]
        public void AMalformedScene_IsRefusedAsAFormatErrorAndNothingElse(string json)
        {
            Assert.Throws<FormatException>(() => SceneReader.ReadJson(json));
        }

        [Theory]
        [InlineData("{\"items\":[{\"kind\":\"GeoMesh2\",\"data\":{\"points\":[[0,0],[1,0],[0,1]],\"faces\":[[0,1,2.5]]}}]}")]
        [InlineData("{\"items\":[{\"kind\":\"GeoPolyline2\",\"data\":{\"points\":[[0,0],[1e400,0]]}}]}")]
        [InlineData("{\"items\":[{\"kind\":\"GeoPolyline2\",\"data\":7}]}")]
        [InlineData("{\"items\":[{\"kind\":\"GeoFace3\",\"data\":{\"outer\":5,\"holes\":[]}}]}")]
        [InlineData("{\"items\":[{\"kind\":\"GeoSolid3\",\"data\":null}]}")]
        public void BadNumbersInOneItem_NeverMakeTheBuildOfTheSceneThrow(string json)
        {
            Scene scene = SceneReader.ReadJson(json);

            Primitives primitives = Primitives.Build(scene);

            Assert.Single(primitives.Items);
            Assert.NotEmpty(ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]));
        }
    }
}
