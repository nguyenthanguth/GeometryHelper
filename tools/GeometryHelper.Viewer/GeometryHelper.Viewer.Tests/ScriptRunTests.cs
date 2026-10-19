using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Viewer.Scripting;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ScriptRunTests
    {
        [Fact]
        public async Task AfterAnAwait_TheScriptIsBackOnItsThreadAtItsTolerance()
        {
            ScriptRunResult result = await ScriptHost.RunAsync(
                "Tol = new Tolerance(0.05, 1E-5);\nNote(\"before \" + Tolerance.Global.EqualPoint + \" \" + System.Threading.Thread.CurrentThread.Name);\nawait System.Threading.Tasks.Task.Delay(20);\nNote(\"after \" + Tolerance.Global.EqualPoint + \" \" + System.Threading.Thread.CurrentThread.Name);\n",
                "C:/x/aw.csx",
                null,
                CancellationToken.None);

            Assert.True(result.Succeeded, string.Join("\n", result.Errors));
            Assert.Equal(new[] { "before 0.05 GeometryHelper.Viewer script", "after 0.05 GeometryHelper.Viewer script" }, result.Notes);
        }

        [Fact]
        public async Task ATolSetAfterAnAwait_DoesNotCloseItsScopeOnAnotherThread()
        {
            ScriptRunResult result = await ScriptHost.RunAsync("await System.Threading.Tasks.Task.Delay(20);\nTol = new Tolerance(0.05, 1E-5);\nShow(new GeoPoint2(1, 2));\n", "C:/x/aw2.csx", null, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join("\n", result.Errors));
            Assert.Equal(0.05, result.Scene.EqualPoint);
        }

        [Fact]
        public async Task ALoopThatShows_IsStoppedByTheTokenAndTheRunReturns()
        {
            using (var cts = new CancellationTokenSource())
            {
                Task<ScriptRunResult> run = ScriptHost.RunAsync("for (long i = 0; ; i++) { Show(new GeoPoint2(i, 0)); }", null, null, cts.Token);
                await Task.Delay(400);
                cts.Cancel();

                Task finished = await Task.WhenAny(run, Task.Delay(5000));

                Assert.Same(run, finished);
                ScriptRunResult result = await run;
                Assert.False(result.Succeeded);
                Assert.Contains("stopped", Assert.Single(result.Errors), StringComparison.Ordinal);
                Assert.NotEmpty(result.Scene.Items);
            }
        }

        [Fact]
        public async Task ALoopThatOnlyAddsToTheScene_IsStoppedToo()
        {
            using (var cts = new CancellationTokenSource())
            {
                Task<ScriptRunResult> run = ScriptHost.RunAsync("var pt = new GeoPoint2(1, 1); for (;;) { Scene.Add(pt); }", null, null, cts.Token);
                await Task.Delay(400);
                cts.Cancel();

                Assert.Same(run, await Task.WhenAny(run, Task.Delay(5000)));
                Assert.Contains("stopped", Assert.Single((await run).Errors), StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task ALongLoopThatShowsNothing_CanTestTheCancelGlobal()
        {
            using (var cts = new CancellationTokenSource())
            {
                Task<ScriptRunResult> run = ScriptHost.RunAsync("long n = 0; while (!Cancel.IsCancellationRequested) { n++; } Note(\"stopped at my own test\"); Cancel.ThrowIfCancellationRequested();", null, null, cts.Token);
                await Task.Delay(300);
                cts.Cancel();

                Assert.Same(run, await Task.WhenAny(run, Task.Delay(5000)));
                ScriptRunResult result = await run;
                Assert.False(result.Succeeded);
            }
        }

        [Fact]
        public async Task TheErrorOfAFileLoadedWithLoad_NamesThatFileAndItsLine()
        {
            string folder = Path.Combine(Path.GetTempPath(), "csx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllText(Path.Combine(folder, "lib.csx"), "void Boom()\n{\n  throw new InvalidOperationException(\"boom\");\n}\n");
                string main = Path.Combine(folder, "main.csx");
                File.WriteAllText(main, "#load \"lib.csx\"\nNote(\"x\");\nBoom();\n");

                ScriptRunResult result = await ScriptHost.RunAsync(File.ReadAllText(main), main, null, CancellationToken.None);

                string error = Assert.Single(result.Errors);
                Assert.Contains("lib.csx(3)", error, StringComparison.Ordinal);
                Assert.Contains("InvalidOperationException: boom", error, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public async Task ARunNeverThrowsToItsCaller_WhateverTheScriptDoes()
        {
            ScriptRunResult result = await ScriptHost.RunAsync("throw new System.Exception(\"plain\"); ", null, null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains("plain", Assert.Single(result.Errors), StringComparison.Ordinal);
        }
    }
}
