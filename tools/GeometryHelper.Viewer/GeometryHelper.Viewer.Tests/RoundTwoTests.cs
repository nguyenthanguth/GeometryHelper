using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Geometry;
using GeometryHelper.Viewer.Builds;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class MeasureDifferenceTests
    {
        private static GeoPolygon2 Sq(double x) => new GeoPolygon2(new GeoPoint2(x, 0), new GeoPoint2(x + 10, 0), new GeoPoint2(x + 10, 10), new GeoPoint2(x, 10));

        [Fact]
        public void DataTheSame_ButTheAreaTheTwoBuildsGaveNot_IsItsOwnStatus_AndTheDifferenceIsListed()
        {
            var left = new SceneWriter();
            var right = new SceneWriter();
            left.Add(Sq(0), name: "s");
            right.Add(Sq(0), name: "s");
            left.Scene.Items[0].Properties["measure.area"] = "100";
            right.Scene.Items[0].Properties["measure.area"] = "99.5";
            left.Scene.Items[0].Properties["measure.volume"] = "1";
            right.Scene.Items[0].Properties["measure.error"] = "MissingMethodException: x";

            SceneComparison comparison = SceneComparison.Compare(left.Scene, right.Scene);

            ComparedItem item = comparison.Items.Single();
            Assert.Equal(CompareStatus.NumbersDiffer, item.Status);
            Assert.True(item.DataIdentical);
            Assert.Equal("Area -0.5, Volume: left '1', right none, Measure error: left none, right 'MissingMethodException: x'", item.Difference);
            Assert.False(comparison.AreIdentical);
            Assert.EndsWith(", 1 with other numbers", comparison.Summary, StringComparison.Ordinal);
        }

        [Fact]
        public void ANumberThatDiffersInTheLastBit_AndANoteOfTheFallback_AreNumbersDiffering()
        {
            var left = new SceneWriter();
            var right = new SceneWriter();
            left.Add(Sq(0), name: "s");
            right.Add(Sq(0), name: "s");
            double next = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(100.0) + 1);
            left.Scene.Items[0].Properties["measure.length"] = "100";
            right.Scene.Items[0].Properties["measure.length"] = next.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            left.Scene.Items[0].Properties["measure.note"] = "rebuilt and measured at 1E-12";

            ComparedItem item = SceneComparison.Compare(left.Scene, right.Scene).Items.Single();

            Assert.Equal(CompareStatus.NumbersDiffer, item.Status);
            Assert.Contains("Length +", item.Difference, StringComparison.Ordinal);
            Assert.Contains("Measure note: left 'rebuilt and measured at 1E-12', right none", item.Difference, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSameNumbersOnBothSides_AreIdentical()
        {
            var left = new SceneWriter();
            var right = new SceneWriter();
            left.Add(Sq(0), name: "s");
            right.Add(Sq(0), name: "s");
            left.Scene.Items[0].Properties["measure.area"] = "100";
            right.Scene.Items[0].Properties["measure.area"] = "100";

            Assert.True(SceneComparison.Compare(left.Scene, right.Scene).AreIdentical);
        }

        [Fact]
        public void AChangedItem_AlsoSaysWhatItsBuildCouldNotMeasure()
        {
            var left = new SceneWriter();
            var right = new SceneWriter();
            left.Add(Sq(0), name: "s");
            right.Add(Sq(1), name: "s");
            right.Scene.Items[0].Properties["measure.error"] = "MissingMethodException: GetVolume";

            ComparedItem item = SceneComparison.Compare(left.Scene, right.Scene).Items.Single();

            Assert.Equal(CompareStatus.Changed, item.Status);
            Assert.Contains("Measure error: left none, right 'MissingMethodException: GetVolume'", item.Difference, StringComparison.Ordinal);
            Assert.Contains("number(s) differ", item.Difference, StringComparison.Ordinal);
        }

        [Fact]
        public void TheItemWhoseNumbersDiffer_IsDrawnOnceOnALayerOfItsOwn_AndHiddenByNeitherSide()
        {
            var left = new SceneWriter();
            var right = new SceneWriter();
            left.Add(Sq(0), name: "s");
            right.Add(Sq(0), name: "s");
            left.Scene.Items[0].Properties["measure.area"] = "100";
            right.Scene.Items[0].Properties["measure.area"] = "99";

            ComparisonScene layout = ComparisonScene.Build(SceneComparison.Compare(left.Scene, right.Scene), left.Scene, right.Scene);

            Assert.Equal(new[] { ComparisonScene.NumbersDifferLayer }, layout.Scene.Layers.Select(l => l.Name));
            Assert.Equal(ComparisonScene.NumbersDifferColor, layout.Scene.Layers.Single().Color);
            Assert.Single(layout.Scene.Items);
            Assert.Empty(layout.HiddenFor(CompareView.Left));
            Assert.Empty(layout.HiddenFor(CompareView.Right));
            Assert.Empty(layout.DashedItems);
        }

        [Fact]
        public void ARuntimeNoteOfTheFallback_IsWrittenIntoTheItem_AndShownByTheMeasuring()
        {
            // At 1E-3 the three corners are one point and the library refuses the triangle; at 1E-12 they are three.
            var writer = new SceneWriter();
            writer.Scene.SetTolerance(new Tolerance(1E-3, 1E-5));
            var item = new SceneItem { Name = "tiny", Layer = "default", Kind = "GeoPolygon2" };
            item.Data = System.Text.Json.JsonDocument.Parse("{\"points\":[[0,0],[0.0004,0],[0,0.0004]]}").RootElement.Clone();
            writer.Scene.Items.Add(item);

            IReadOnlyList<KeyValuePair<string, string>> numbers = ItemMeasures.Numbers(item, writer.Scene);
            RunnerMeasures.Add(writer.Scene);

            Assert.Equal("Library", numbers.Last().Key);
            Assert.Contains("1E-12", numbers.Last().Value, StringComparison.Ordinal);
            Assert.Equal(numbers.Last().Value, item.Properties["measure.note"]);
            Assert.Equal("3", item.Properties["measure.vertices"]);
        }
    }

    public class AutoNameTests
    {
        private static GeoPolygon2 Sq(double x) => new GeoPolygon2(new GeoPoint2(x, 0), new GeoPoint2(x + 10, 0), new GeoPoint2(x + 10, 10), new GeoPoint2(x, 10));

        [Fact]
        public void AnItemWithNoName_IsMarkedAsAutoNamed_AndOneWithANameIsNot()
        {
            var writer = new SceneWriter();
            writer.Add(Sq(0));
            writer.Add(Sq(20), name: "mine");
            writer.Add(new[] { Sq(40), Sq(60) });
            writer.Add(new[] { Sq(80), Sq(100) }, name: "pair");
            writer.Text(new GeoPoint3(0, 0, 0), "a label");

            Assert.Equal(new[] { true, false, true, true, false, false, true }, writer.Scene.Items.Select(i => i.AutoName));
            Assert.Equal("GeoPolygon2 1", writer.Scene.Items[0].Name);
        }

        [Fact]
        public void TheMark_SurvivesTheFile_AndIsWrittenOnlyWhereItIsSet()
        {
            var writer = new SceneWriter();
            writer.Add(Sq(0));
            writer.Add(Sq(20), name: "mine");

            string json = writer.ToJson();
            Scene read = SceneReader.ReadJson(json);

            Assert.Equal(1, json.Split(new[] { "\"auto\":true" }, StringSplitOptions.None).Length - 1);
            Assert.Equal(new[] { true, false }, read.Items.Select(i => i.AutoName));
            Assert.Equal(json, new SceneWriter(read).ToJson());
        }

        [Fact]
        public void AutoNamesThatShift_DoNotPairTheWrongItems_ThePiecesAreFoundByTheirData()
        {
            // The other build gives one piece fewer on an earlier layer, so every auto name after it is one lower.
            var a = new SceneWriter();
            var b = new SceneWriter();
            a.Add(new[] { Sq(0), Sq(20) }, layer: "offset");
            b.Add(new[] { Sq(0) }, layer: "offset");
            a.Add(Sq(100), layer: "check");
            a.Add(Sq(200), layer: "check");
            b.Add(Sq(100), layer: "check");
            b.Add(Sq(200), layer: "check");

            SceneComparison comparison = SceneComparison.Compare(a.Scene, b.Scene);

            Assert.Equal(new[] { CompareStatus.Identical, CompareStatus.OnlyLeft, CompareStatus.Identical, CompareStatus.Identical }, comparison.Items.Select(i => i.Status));
            Assert.Equal(new[] { CompareMatch.Data, CompareMatch.None, CompareMatch.Data, CompareMatch.Data }, comparison.Items.Select(i => i.Match));
            Assert.Equal(new[] { 0, 1, 2, 3 }, comparison.Items.Select(i => i.LeftIndex));
            Assert.Equal(new[] { 0, -1, 1, 2 }, comparison.Items.Select(i => i.RightIndex));
        }

        [Fact]
        public void AutoNamedItemsThatDiffer_AreStillPairedByOrderOnTheirLayerAndKind()
        {
            var a = new SceneWriter();
            var b = new SceneWriter();
            a.Add(Sq(0));
            a.Add(Sq(20));
            b.Add(Sq(1));
            b.Add(Sq(20));

            SceneComparison comparison = SceneComparison.Compare(a.Scene, b.Scene);

            Assert.Equal(new[] { CompareMatch.Order, CompareMatch.Data }, comparison.Items.Select(i => i.Match));
            Assert.Equal(new[] { CompareStatus.Changed, CompareStatus.Identical }, comparison.Items.Select(i => i.Status));
        }

        [Fact]
        public void ANameTheScriptGave_StillPairsFirst_EvenAgainstItemsWhoseDataIsTheSame()
        {
            var a = new SceneWriter();
            var b = new SceneWriter();
            a.Add(Sq(0), name: "x");
            a.Add(Sq(20), name: "y");
            b.Add(Sq(20), name: "x");
            b.Add(Sq(0), name: "y");

            SceneComparison comparison = SceneComparison.Compare(a.Scene, b.Scene);

            Assert.All(comparison.Items, item => Assert.Equal(CompareMatch.Name, item.Match));
            Assert.All(comparison.Items, item => Assert.Equal(CompareStatus.Changed, item.Status));
        }

        [Fact]
        public void ASceneFileWrittenBeforeTheMark_ReadsAsNamedItems()
        {
            const string old = "{\"format\":\"ghscene\",\"version\":1,\"layers\":[{\"name\":\"L\",\"color\":\"#112233\"}],\"items\":[{\"name\":\"GeoPoint2 1\",\"layer\":\"L\",\"kind\":\"GeoPoint2\",\"data\":[1,2]}]}";

            Assert.False(SceneReader.ReadJson(old).Items.Single().AutoName);
        }
    }

    public class ExportInAComparisonTests
    {
        [Fact]
        public void TheCopySelected_IsTakenFromItsOwnSide_WithItsOwnToleranceAndItsBuildsNumbers()
        {
            var left = new SceneWriter("left");
            var right = new SceneWriter("right");
            left.Scene.SetTolerance(new Tolerance(0.002, 1E-5, 0.01, 0.02));
            right.Scene.SetTolerance(new Tolerance(0.05, 1E-5, 0.01, 0.07));
            left.Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10)), name: "tri");
            right.Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(12, 0), new GeoPoint2(0, 10)), name: "tri");
            left.Scene.Items[0].Properties["measure.area"] = "50";
            right.Scene.Items[0].Properties["measure.area"] = "60.123456789012345";
            ComparisonScene layout = ComparisonScene.Build(SceneComparison.Compare(left.Scene, right.Scene), left.Scene, right.Scene);

            KeyValuePair<Scene, SceneItem> leftCopy = layout.SourceOf(0);
            KeyValuePair<Scene, SceneItem> rightCopy = layout.SourceOf(1);

            Assert.Same(left.Scene, leftCopy.Key);
            Assert.Same(left.Scene.Items[0], leftCopy.Value);
            Assert.Same(right.Scene, rightCopy.Key);
            Assert.Same(right.Scene.Items[0], rightCopy.Value);

            string text = TestExporter.Export(new[] { rightCopy, leftCopy });

            Assert.True(text.Contains("new Tolerance(0.050000000000000003, 1.0000000000000001E-05, 0.01, 0.070000000000000007)", StringComparison.Ordinal), text);
            Assert.Contains("new Tolerance(0.002, 1.0000000000000001E-05, 0.01, 0.02)", text, StringComparison.Ordinal);
            Assert.Contains("the build that made the scene measured 60.123456789012345.", text, StringComparison.Ordinal);
            Assert.Contains("the build that made the scene measured 50.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("the viewer measured", text, StringComparison.Ordinal);
            Assert.Contains("from the scene 'right', 'left'", text.Replace("Written by the GeometryHelper viewer ", string.Empty), StringComparison.Ordinal);
        }

        [Fact]
        public void WhatBothSidesShare_IsTakenFromTheRight()
        {
            var left = new SceneWriter();
            var right = new SceneWriter();
            left.Add(new GeoPoint2(1, 1), name: "p");
            right.Add(new GeoPoint2(1, 1), name: "p");

            ComparisonScene layout = ComparisonScene.Build(SceneComparison.Compare(left.Scene, right.Scene), left.Scene, right.Scene);

            Assert.Same(right.Scene, layout.SourceOf(0).Key);
        }

        [Fact]
        public void AnItemItsBuildCouldNotMeasure_SaysSo_AndHasNoNumberToAssert()
        {
            var writer = new SceneWriter();
            writer.Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10)), name: "tri");
            writer.Scene.Items[0].Properties["measure.error"] = "MissingMethodException: Area";

            string text = TestExporter.Export(writer.Scene, writer.Scene.Items);

            Assert.Contains("// That build could not measure the shape: MissingMethodException: Area", text, StringComparison.Ordinal);
            Assert.Contains("Assert.Fail(", text, StringComparison.Ordinal);
        }

        [Fact]
        public void ANoteOfTheFallback_IsCarriedIntoTheTest()
        {
            var writer = new SceneWriter();
            writer.Add(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10)), name: "tri");
            writer.Scene.Items[0].Properties["measure.area"] = "50";
            writer.Scene.Items[0].Properties["measure.note"] = "rebuilt and measured at 1E-12";

            Assert.Contains("// rebuilt and measured at 1E-12", TestExporter.Export(writer.Scene, writer.Scene.Items), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Builds Release 12.1.0 once for the tests of older commits, or says it cannot: a clone with no history, or a copy of the sources with no repository,
    /// has no such commit, and the tests then do nothing but say so.
    /// </summary>
    public sealed class OlderCommitFixture : IDisposable
    {
        private readonly TempFolder _cache = new TempFolder("ghviewer-old");

        public OlderCommitFixture()
        {
            string repository = FindRepository();

            if (repository == null)
            {
                Why = "there is no Git repository above the tests";
                return;
            }

            Environment = new CompareEnvironment
            {
                CacheRoot = _cache.Path,
                RunnerFolder = Path.Combine(AppContext.BaseDirectory, "runner"),
                OwnLibrary = typeof(Tolerance).Assembly.Location,
                BuildTimeout = TimeSpan.FromMinutes(5),
            };

            try
            {
                GitBuilds.ResolveAsync(Environment, repository, Commit, CancellationToken.None).GetAwaiter().GetResult();
                Source = BuildSource.FromCommit(Commit, repository);
            }
            catch (InvalidOperationException e)
            {
                Why = "the repository has no commit " + Commit + " (" + e.Message + ")";
            }
        }

        public const string Commit = "1761e79";

        public CompareEnvironment Environment { get; }

        public BuildSource Source { get; }

        /// <summary>Gets why the commit is not there, or null.</summary>
        public string Why { get; }

        public string Cache => _cache.Path;

        public void Dispose() => _cache.Dispose();

        private static string FindRepository()
        {
            for (DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }
            }

            return null;
        }
    }

    public class OlderCommitTests : IClassFixture<OlderCommitFixture>
    {
        private readonly OlderCommitFixture _fixture;
        private readonly Xunit.Abstractions.ITestOutputHelper _output;

        public OlderCommitTests(OlderCommitFixture fixture, Xunit.Abstractions.ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private bool Skipped()
        {
            if (_fixture.Why == null)
            {
                return false;
            }

            _output.WriteLine("NOT RUN: " + _fixture.Why + ". The test of a build before GeoRay2 needs the history of this repository.");
            return true;
        }

        [Fact]
        public async Task ReleaseTwelveOne_WhichLacksGeoRay2_BuildsThroughTheCommitPath_AndRunsAScriptThatDoesNotUseIt()
        {
            if (Skipped())
            {
                return;
            }

            using (var folder = new TempFolder("ghviewer-oldscript"))
            {
                string script = folder.Combine("tri.csx");
                File.WriteAllText(script, "Tol = new Tolerance(0.01, 1E-5);\nShow(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10)), name: \"tri\");\nShow(new GeoPoint2(1, 2));");

                SideRunResult result = await SideRunner.RunAsync(_fixture.Environment, script, _fixture.Source, "old", null, CancellationToken.None);

                Assert.True(result.Succeeded, string.Join("\n", result.Errors));
                Assert.EndsWith(" 12.1.0.0", result.LibraryLine, StringComparison.Ordinal);
                Assert.Equal(2, result.Scene.Items.Count);
                Assert.Equal(0.01, result.Scene.EqualPoint);
                ItemNumbers triangle = RunnerMeasures.Read(result.Scene.Items[0]);
                Assert.Equal(50.0, triangle.Area);
                Assert.Equal(3, triangle.Vertices);
                Assert.Null(triangle.Error);
                Assert.True(result.Scene.Items.All(i => !i.Properties.ContainsKey("measure.error")));
            }
        }

        [Fact]
        public async Task AScriptThatUsesATypeTheOlderBuildLacks_FailsToCompileForThatSideAlone()
        {
            if (Skipped())
            {
                return;
            }

            using (var folder = new TempFolder("ghviewer-oldscript"))
            {
                string script = folder.Combine("ray.csx");
                File.WriteAllText(script, "Show(new GeoRay2(new GeoPoint2(0, 0), new GeoVector2(1, 0)), name: \"ray\");");

                SideRunResult old = await SideRunner.RunAsync(_fixture.Environment, script, _fixture.Source, "old", null, CancellationToken.None);
                SideRunResult own = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "own", null, CancellationToken.None);

                Assert.False(old.Succeeded);
                Assert.Contains("GeoRay2", string.Join("\n", old.Errors), StringComparison.Ordinal);
                Assert.True(own.Succeeded, string.Join("\n", own.Errors));
            }
        }
    }

    public class TwoSidesAtOnceTests : IClassFixture<CompareFixture>
    {
        private readonly CompareFixture _fixture;

        public TwoSidesAtOnceTests(CompareFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TwoSidesStartingAtOnce_WithAKeepOfOne_NeverDeleteEachOthersRunner()
        {
            using (var folder = new TempFolder())
            {
                var environment = new CompareEnvironment
                {
                    CacheRoot = _fixture.Environment.CacheRoot,
                    RunnerFolder = _fixture.Environment.RunnerFolder,
                    OwnLibrary = _fixture.Environment.OwnLibrary,
                    CacheKeep = 1,
                };
                string script = folder.Combine("a.csx");
                File.WriteAllText(script, "Show(new GeoPoint2(1, 2));");
                byte[] library = File.ReadAllBytes(typeof(Tolerance).Assembly.Location);

                for (int run = 0; run < 6; run++)
                {
                    // A copy of the library with a byte of padding after it, which the loader ignores: a different DLL, so a different folder of its own.
                    string padded = folder.Combine("padded" + run, "GeometryHelper.dll");
                    Directory.CreateDirectory(Path.GetDirectoryName(padded));
                    File.WriteAllBytes(padded, library.Concat(new[] { (byte)run }).ToArray());

                    Task<SideRunResult> left = SideRunner.RunAsync(environment, script, BuildSource.Own, "left", null, CancellationToken.None);
                    Task<SideRunResult> right = SideRunner.RunAsync(environment, script, BuildSource.FromDll(padded), "right", null, CancellationToken.None);
                    await Task.WhenAll(left, right);

                    Assert.True(left.Result.Succeeded, "run " + run + " left: " + string.Join(" / ", left.Result.Errors));
                    Assert.True(right.Result.Succeeded, "run " + run + " right: " + string.Join(" / ", right.Result.Errors));
                }
            }
        }
    }

    public class ForeignShapeTests
    {
        [Fact]
        public void AClassOfAScriptCalledLikeAShape_IsNotDrawn_ItIsNotOfTheLibrary()
        {
            var writer = new SceneWriter();

            ArgumentException error = Assert.Throws<ArgumentException>(() => writer.Add((object)new Foreign.GeoPoint2()));

            Assert.Contains("Foreign.GeoPoint2", error.Message, StringComparison.Ordinal);
            Assert.Empty(writer.Scene.Items);
            Assert.Null(GeometryEncoder.KindOf(new Foreign.GeoPoint2()));
            Assert.Throws<ArgumentException>(() => GeometryEncoder.Encode(new Foreign.GeoPoint2()));
            Assert.Equal("GeoPoint2", GeometryEncoder.KindOf(new GeometryHelper.Geometry.GeoPoint2(1, 2)));
        }
    }

    public class ChildLifetimeTests : IClassFixture<CompareFixture>
    {
        private readonly CompareFixture _fixture;

        public ChildLifetimeTests(CompareFixture fixture)
        {
            _fixture = fixture;
        }

        private static bool IsAlive(int id)
        {
            try
            {
                using (Process process = Process.GetProcessById(id))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        [Fact]
        public async Task AScriptThatEndsAndLeavesAChildHoldingItsOutput_ReturnsWithinTheGrace_AndTheChildIsKilled()
        {
            using (var folder = new TempFolder())
            {
                string script = folder.Combine("spawn.csx");
                File.WriteAllText(
                    script,
                    "var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(\"ping\", \"-n 40 127.0.0.1 -w 1000\") { UseShellExecute = false, CreateNoWindow = true });\nNote(\"child \" + child.Id);\nShow(new GeoPoint2(1, 2), name: \"p\");");
                var clock = Stopwatch.StartNew();

                SideRunResult result = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "left", null, CancellationToken.None);

                Assert.True(result.Succeeded, string.Join("\n", result.Errors));
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), clock.Elapsed.ToString());
                Assert.False(result.TimedOut);
                int child = int.Parse(result.Notes.Single().Substring("child ".Length));
                await Task.Delay(500);
                Assert.False(IsAlive(child), "the child the script left behind is still running");
            }
        }

        [Fact]
        public async Task TheScriptHash_IsOfTheTextWhenTheRunStarted_AndChangesWithAnEdit()
        {
            using (var folder = new TempFolder())
            {
                string script = folder.Combine("a.csx");
                File.WriteAllText(script, "Show(new GeoPoint2(1, 2));");
                SideRunResult first = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "left", null, CancellationToken.None);
                File.WriteAllText(script, "Show(new GeoPoint2(1, 3));");
                SideRunResult second = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "left", null, CancellationToken.None);

                Assert.Equal(64, first.ScriptHash.Length);
                Assert.NotEqual(first.ScriptHash, second.ScriptHash);
                Assert.Equal(second.ScriptHash, SideRunner.HashOfScript(script));
                Assert.Null(SideRunner.HashOfScript(folder.Combine("nothing.csx")));
            }
        }
    }
}
