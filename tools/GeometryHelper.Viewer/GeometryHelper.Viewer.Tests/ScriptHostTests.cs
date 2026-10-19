using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Viewer.Scripting;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ScriptHostTests
    {
        private static ScriptRunResult Run(string code, string path = null, CancellationToken cancel = default(CancellationToken))
            => ScriptHost.RunAsync(code, path, null, cancel).GetAwaiter().GetResult();

        [Fact]
        public void AScript_BuildsASceneWithShowAndSaysLinesWithNote()
        {
            ScriptRunResult result = Run(@"
                var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 60), new GeoPoint2(0, 60) }, new[] { 0.0, 0.5, 0.0, 0.0 });
                Show(loop, layer: ""source"", name: ""loop"", props: new { width = 100 });
                Show(new[] { new GeoPoint2(1, 1), new GeoPoint2(2, 2) }, layer: ""points"");
                Scene.Layer(""source"", ""#202020"");
                Note(""area "" + loop.Area.ToString(""0.0""));
            ");

            Assert.True(result.Succeeded, string.Join("\n", result.Errors));
            Assert.Empty(result.Errors);
            Assert.Equal(3, result.Scene.Items.Count);
            Assert.Equal("GeoPolygonArc2", result.Scene.Items[0].Kind);
            Assert.Equal("#202020", result.Scene.FindLayer("source").Color);
            Assert.Equal("100", result.Scene.Items[0].Properties["width"]);
            Assert.Single(result.Notes);
            Assert.StartsWith("area ", result.Notes[0], StringComparison.Ordinal);
        }

        [Fact]
        public void ACompileError_IsReportedWithItsFileAndLineNumber()
        {
            string path = Path.Combine(Path.GetTempPath(), "broken.csx");
            ScriptRunResult result = Run("var a = 1;\nvar b = 2;\nint c = \"text\";\nShow(new GeoPoint2(a, b));\n", path);

            Assert.False(result.Succeeded);
            string error = Assert.Single(result.Errors);
            Assert.True(error.Contains("broken.csx(3,", StringComparison.Ordinal), error);
            Assert.Contains("CS0029", error, StringComparison.Ordinal);
            Assert.Empty(result.Scene.Items);
        }

        [Fact]
        public void AnExceptionOfTheScript_IsReportedWithTheLineItWasThrownFrom_AndWhatWasShownBeforeStays()
        {
            string path = Path.Combine(Path.GetTempPath(), "throws.csx");
            ScriptRunResult result = Run("Show(new GeoPoint2(1, 2));\nvar tooFew = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1, 0));\nShow(tooFew);\n", path);

            Assert.False(result.Succeeded);
            string error = Assert.Single(result.Errors);
            Assert.True(error.Contains("throws.csx(2)", StringComparison.Ordinal), error);
            Assert.Contains("ArgumentException", error, StringComparison.Ordinal);
            Assert.Single(result.Scene.Items);
        }

        [Fact]
        public void TheToleranceOfTheScript_IsTheOneTheLibraryReadsOnItsThread_AndTheSceneIsOpenedAtIt()
        {
            double before = Tolerance.Global.EqualPoint;

            ScriptRunResult result = Run(@"
                Note(Tolerance.Global.EqualPoint.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Tol = new Tolerance(0.25, 1E-5);
                Note(Tolerance.Global.EqualPoint.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Show(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(0.1, 0), new GeoPoint2(5, 0)));
            ");

            Assert.True(result.Succeeded, string.Join("\n", result.Errors));
            Assert.Equal(new[] { "0.001", "0.25" }, result.Notes);
            Assert.Equal(0.25, result.Scene.EqualPoint);

            // At 0.25 the point 0.1 from the first is the same point, so the chain has two vertices.
            Assert.Equal(2, result.Scene.Items[0].Data.GetProperty("points").GetArrayLength());

            // The scope was on the script's thread and is closed: this thread never saw it.
            Assert.Equal(before, Tolerance.Global.EqualPoint);
        }

        [Fact]
        public void AScript_RunsOnAThreadOfItsOwn()
        {
            ScriptRunResult result = Run("Note(System.Threading.Thread.CurrentThread.Name);");

            Assert.Equal("GeometryHelper.Viewer script", Assert.Single(result.Notes));
        }

        [Fact]
        public void AScriptThatIsCancelledBeforeItStarts_IsStoppedWithoutRunning()
        {
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();

                ScriptRunResult result = Run("Show(new GeoPoint2(1, 2));", null, source.Token);

                Assert.False(result.Succeeded);
                Assert.Contains("stopped", Assert.Single(result.Errors), StringComparison.Ordinal);
                Assert.Empty(result.Scene.Items);
            }
        }

        [Fact]
        public void AShapeTheViewerCannotDraw_StopsTheScriptWithAMessageNamingTheType()
        {
            ScriptRunResult result = Run("Show(new GeoCoordinateSystem3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), new GeoVector3(0, 1, 0)));");

            Assert.False(result.Succeeded);
            Assert.Contains("GeoCoordinateSystem3", Assert.Single(result.Errors), StringComparison.Ordinal);
        }

        [Fact]
        public void TheLibraryAndTheViewerAreImported_SoAScriptNamesNoNamespaces()
        {
            ScriptRunResult result = Run(@"
                var square = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) });
                GeoPolygonArc2[] grown = square.Offset(10.0, new OffsetOptions(OffsetJoin.Round));
                Show(grown, layer: ""round"");
                Note(grown.Length + "" loop"");
            ");

            Assert.True(result.Succeeded, string.Join("\n", result.Errors));
            Assert.Equal("1 loop", Assert.Single(result.Notes));
            Assert.Equal("round", Assert.Single(result.Scene.Items).Layer);
        }

        [Fact]
        public void ALoadedFile_IsFoundBesideTheScript()
        {
            string folder = Path.Combine(Path.GetTempPath(), "csx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllText(Path.Combine(folder, "part.csx"), "GeoPoint2 Corner() => new GeoPoint2(3, 4);\n");
                string main = Path.Combine(folder, "main.csx");
                File.WriteAllText(main, "#load \"part.csx\"\nShow(Corner());\n");

                ScriptRunResult result = Run(File.ReadAllText(main), main);

                Assert.True(result.Succeeded, string.Join("\n", result.Errors));
                Assert.Single(result.Scene.Items);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public async Task TwoScriptsAtOnce_EachKeepToTheirOwnSceneAndTolerance()
        {
            Task<ScriptRunResult> first = ScriptHost.RunAsync("Tol = new Tolerance(0.5, 1E-5); Thread.Sleep(100); Show(new GeoPoint2(1, 1));".Replace("Thread.Sleep", "System.Threading.Thread.Sleep"), null, null, CancellationToken.None);
            Task<ScriptRunResult> second = ScriptHost.RunAsync("System.Threading.Thread.Sleep(50); Note(Tolerance.Global.EqualPoint.ToString(System.Globalization.CultureInfo.InvariantCulture)); Show(new GeoPoint2(2, 2));", null, null, CancellationToken.None);

            ScriptRunResult one = await first;
            ScriptRunResult two = await second;

            Assert.Equal(0.5, one.Scene.EqualPoint);
            Assert.Equal("0.001", Assert.Single(two.Notes));
            Assert.Single(one.Scene.Items);
            Assert.Single(two.Scene.Items);
        }
    }
}
