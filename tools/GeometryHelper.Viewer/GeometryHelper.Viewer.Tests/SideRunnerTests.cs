using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Viewer.Builds;
using GeometryHelper.Viewer.Scripting;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class SideRunnerTests : IClassFixture<CompareFixture>
    {
        private const string Script = @"
var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100.1234567890123, 60), new GeoPoint2(0, 60) }, new[] { 0.0, 0.5, 0.0, 0.0 });
Show(loop, layer: ""source"", name: ""loop"", props: new { width = 100.5, join = ""Round"" });
Show(loop.Offset(5, new OffsetOptions(OffsetJoin.Miter)), layer: ""out"", name: ""miter"");
Show(new GeoPoint3(1, 2, 3), name: ""p"");
Scene.Text(new GeoPoint3(0, 0, 0), ""a label"");
Note(""two lines"" + ""\nof one note"");
";

        private readonly CompareFixture _fixture;

        public SideRunnerTests(CompareFixture fixture)
        {
            _fixture = fixture;
        }

        private static string WriteScript(TempFolder folder, string text, string name = "run.csx")
        {
            string path = folder.Combine(name);
            File.WriteAllText(path, text);
            return path;
        }

        private static async Task<(int Exit, string Out, string Err)> RunRunnerAsync(string runnerFolder, string script, string output, bool measure)
        {
            var outLines = new List<string>();
            var errLines = new List<string>();
            var arguments = new List<string> { script, "--out", output };

            if (measure)
            {
                arguments.Add("--measure");
            }

            ChildResult result = await ChildProcess.RunAsync(
                Path.Combine(runnerFolder, "GeometryHelper.Viewer.Runner.exe"),
                arguments,
                runnerFolder,
                TimeSpan.FromSeconds(120),
                outLines.Add,
                errLines.Add,
                CancellationToken.None);

            return (result.ExitCode, string.Join("\n", outLines), string.Join("\n", errLines));
        }

        [Fact]
        public async Task TheRunnerProgram_WritesTheSameJsonAsTheScriptHostInTheProcess_ToTheBit()
        {
            using (var folder = new TempFolder())
            {
                string script = WriteScript(folder, Script);
                ScriptRunResult inProcess = await ScriptHost.RunAsync(File.ReadAllText(script), script, null, CancellationToken.None);
                Assert.True(inProcess.Succeeded, string.Join("\n", inProcess.Errors));
                string expected = new SceneWriter(inProcess.Scene).ToJson();

                var run = await RunRunnerAsync(_fixture.Environment.RunnerFolder, script, folder.Combine("out.ghscene.json"), false);

                Assert.Equal(0, run.Exit);
                Assert.Equal(expected, File.ReadAllText(folder.Combine("out.ghscene.json")));
                Assert.Contains("library: ", run.Out, StringComparison.Ordinal);
                Assert.Contains("note: two lines\\nof one note", run.Out, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task WithMeasure_TheRunnerAddsTheNumbersOfItsLibrary_AndNothingElseChanges()
        {
            using (var folder = new TempFolder())
            {
                string script = WriteScript(folder, Script);
                ScriptRunResult inProcess = await ScriptHost.RunAsync(File.ReadAllText(script), script, null, CancellationToken.None);
                string expected = new SceneWriter(inProcess.Scene).ToJson();

                var run = await RunRunnerAsync(_fixture.Environment.RunnerFolder, script, folder.Combine("out.ghscene.json"), true);

                Assert.Equal(0, run.Exit);
                Scene scene = SceneReader.ReadFile(folder.Combine("out.ghscene.json"));
                ItemNumbers loop = RunnerMeasures.Read(scene.Items[0]);
                Assert.Equal(4, loop.Vertices);
                Assert.True(loop.Length > 300.0);
                Assert.True(loop.Area > 6000.0);
                Assert.False(loop.Volume.HasValue);

                // The library of the viewer's own process gives the numbers the runner's does, to the bit.
                RunnerMeasures.Add(inProcess.Scene);
                Assert.Equal(new SceneWriter(inProcess.Scene).ToJson(), new SceneWriter(scene).ToJson());

                foreach (SceneItem item in scene.Items)
                {
                    foreach (string key in item.Properties.Keys.Where(k => k.StartsWith(RunnerMeasures.Prefix, StringComparison.Ordinal)).ToList())
                    {
                        item.Properties.Remove(key);
                    }
                }

                Assert.Equal(expected, new SceneWriter(scene).ToJson());
            }
        }

        [Fact]
        public async Task AScriptRunThroughASide_GivesTheSceneItBuilt_TheNotes_AndTheLibraryThatWasLoaded()
        {
            using (var folder = new TempFolder())
            {
                string script = WriteScript(folder, Script);
                var log = new List<string>();

                SideRunResult result = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "left", line => { lock (log) { log.Add(line); } }, CancellationToken.None);

                Assert.True(result.Succeeded, string.Join("\n", result.Errors));
                Assert.Equal(4, result.Scene.Items.Count);
                Assert.Equal("two lines\nof one note".Replace("\n", "\\n"), Assert.Single(result.Notes));
                Assert.Equal(64, result.LibraryKey.Length);
                Assert.Equal(RunnerCache.KeyOf(typeof(Tolerance).Assembly.Location), result.LibraryKey);

                // The DLL that ran was the copy in the runner cache, not the one the viewer holds.
                Assert.StartsWith(Path.Combine(_fixture.Environment.RunnersFolder, result.LibraryKey), result.LibraryLine, StringComparison.OrdinalIgnoreCase);
                Assert.All(log, line => Assert.StartsWith("[left] ", line, StringComparison.Ordinal));
                Assert.Empty(Directory.GetFiles(_fixture.Environment.ScenesFolder));
                Assert.True(result.Elapsed > TimeSpan.Zero);
            }
        }

        [Fact]
        public async Task ADllAtAnotherPath_WithTheSameBytes_SharesTheCopyOfTheRunner()
        {
            using (var folder = new TempFolder())
            {
                string copy = folder.Combine("elsewhere", "GeometryHelper.dll");
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(typeof(Tolerance).Assembly.Location, copy);
                string script = WriteScript(folder, "Show(new GeoPoint2(1, 2));");
                var log = new List<string>();

                SideRunResult own = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "left", null, CancellationToken.None);
                SideRunResult other = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.FromDll(copy), "right", log.Add, CancellationToken.None);

                Assert.True(other.Succeeded, string.Join("\n", other.Errors));
                Assert.Equal(own.LibraryKey, other.LibraryKey);
                Assert.Contains(log, line => line.Contains("runner cache hit", StringComparison.Ordinal));
                Assert.Single(Directory.GetDirectories(_fixture.Environment.RunnersFolder), d => !d.Contains(".tmp-", StringComparison.Ordinal));
            }
        }

        [Fact]
        public async Task AScriptThatDoesNotCompile_ComesBackWithTheCompilerErrorsAndItsLine_AndNoScene()
        {
            using (var folder = new TempFolder())
            {
                string script = WriteScript(folder, "var a = 1;\nint b = \"text\";\nShow(new GeoPoint2(a, 1));\n", "broken.csx");

                SideRunResult result = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "right", null, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Null(result.Scene);
                string error = Assert.Single(result.Errors);
                Assert.Contains("broken.csx(2,", error, StringComparison.Ordinal);
                Assert.Contains("CS0029", error, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task AScriptThatThrows_ComesBackWithTheExceptionAndTheLine()
        {
            using (var folder = new TempFolder())
            {
                string script = WriteScript(folder, "Show(new GeoPoint2(1, 2));\nvar tooFew = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1, 0));\n", "throws.csx");

                SideRunResult result = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "left", null, CancellationToken.None);

                Assert.False(result.Succeeded);
                string error = Assert.Single(result.Errors);
                Assert.Contains("throws.csx(2)", error, StringComparison.Ordinal);
                Assert.Contains("ArgumentException", error, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task TheToleranceTheScriptSets_IsTheToleranceOfTheSceneTheRunnerWrites()
        {
            using (var folder = new TempFolder())
            {
                string script = WriteScript(folder, "Tol = new Tolerance(0.05, 1E-4, 0.02, 0.07);\nShow(new GeoPoint2(1, 2));");

                SideRunResult result = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.Own, "left", null, CancellationToken.None);

                Assert.True(result.Succeeded, string.Join("\n", result.Errors));
                Assert.Equal(0.05, result.Scene.EqualPoint);
                Assert.Equal(1E-4, result.Scene.EqualVector);
                Assert.Equal(0.02, result.Scene.EqualAngleRad);
                Assert.Equal(0.07, result.Scene.EqualPlanar);
            }
        }

        [Fact]
        public async Task ADllThatIsNotThere_IsAnErrorOfTheSide_NotAnException()
        {
            using (var folder = new TempFolder())
            {
                string script = WriteScript(folder, "Show(new GeoPoint2(1, 2));");

                SideRunResult result = await SideRunner.RunAsync(_fixture.Environment, script, BuildSource.FromDll(folder.Combine("nothing.dll")), "right", null, CancellationToken.None);

                Assert.False(result.Succeeded);
                Assert.Contains("There is no GeometryHelper.dll", Assert.Single(result.Errors), StringComparison.Ordinal);
            }
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

        private static Process StartInnocentBystander()
            => Process.Start(new ProcessStartInfo("ping", "-n 60 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });

        // The script says its own process id and starts a grandchild that sleeps, saying that one's id too.
        private const string Sleeper = @"
Note(""runner "" + Environment.ProcessId);
var grandchild = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(""ping"", ""-n 120 127.0.0.1"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
Note(""grandchild "" + grandchild.Id);
System.Threading.Thread.Sleep(120000);
Show(new GeoPoint2(1, 2));
";

        private static int IdFrom(SideRunResult result, string word) => int.Parse(result.Notes.Single(n => n.StartsWith(word + " ", StringComparison.Ordinal)).Substring(word.Length + 1));

        [Fact]
        public async Task ARunThatOutlastsItsTime_IsKilledWithItsGrandchild_AndOnlyThose()
        {
            using (var folder = new TempFolder())
            using (Process bystander = StartInnocentBystander())
            {
                try
                {
                    CompareEnvironment environment = new CompareEnvironment
                    {
                        CacheRoot = _fixture.Environment.CacheRoot,
                        RunnerFolder = _fixture.Environment.RunnerFolder,
                        OwnLibrary = _fixture.Environment.OwnLibrary,
                        RunTimeout = TimeSpan.FromSeconds(6),
                    };
                    string script = WriteScript(folder, Sleeper);
                    var clock = Stopwatch.StartNew();

                    SideRunResult result = await SideRunner.RunAsync(environment, script, BuildSource.Own, "left", null, CancellationToken.None);

                    Assert.True(result.TimedOut);
                    Assert.False(result.Succeeded);
                    Assert.Contains("was killed", Assert.Single(result.Errors), StringComparison.Ordinal);
                    Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), clock.Elapsed.ToString());

                    int runner = IdFrom(result, "runner");
                    int grandchild = IdFrom(result, "grandchild");
                    await Task.Delay(500);
                    Assert.False(IsAlive(runner), "the runner is still alive");
                    Assert.False(IsAlive(grandchild), "the grandchild is still alive");
                    Assert.True(IsAlive(bystander.Id), "a process that was not started by the run was killed");
                }
                finally
                {
                    if (!bystander.HasExited)
                    {
                        bystander.Kill(true);
                    }
                }
            }
        }

        [Fact]
        public async Task ACancelledRun_KillsTheProcessTreeAtOnce()
        {
            using (var folder = new TempFolder())
            using (var cancel = new CancellationTokenSource())
            {
                string script = WriteScript(folder, Sleeper);
                var seen = new TaskCompletionSource<bool>();

                // Cancel once the script has said its grandchild's id, which is the moment both are alive.
                Task<SideRunResult> run = SideRunner.RunAsync(
                    _fixture.Environment,
                    script,
                    BuildSource.Own,
                    "right",
                    line =>
                    {
                        if (line.Contains("note: grandchild", StringComparison.Ordinal))
                        {
                            seen.TrySetResult(true);
                        }
                    },
                    cancel.Token);

                Assert.Same(seen.Task, await Task.WhenAny(seen.Task, Task.Delay(TimeSpan.FromSeconds(90))));
                var clock = Stopwatch.StartNew();
                cancel.Cancel();
                SideRunResult result = await run;

                Assert.True(result.Cancelled);
                Assert.False(result.Succeeded);
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), clock.Elapsed.ToString());
                await Task.Delay(500);
                Assert.False(IsAlive(IdFrom(result, "runner")));
                Assert.False(IsAlive(IdFrom(result, "grandchild")));
            }
        }
    }
}
