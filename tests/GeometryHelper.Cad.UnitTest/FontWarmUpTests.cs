using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Reading the machine's fonts ahead, after the window has drawn: the work never runs on the caller's thread and never
    /// holds it, a failure is logged and not thrown, and the lists a warmed process gives are those a fresh scan gives, in
    /// the same order.
    /// </summary>
    public sealed class FontWarmUpTests
    {
        private static readonly TimeSpan Generous = TimeSpan.FromSeconds(60);

        /// <summary>Waits for a task, failing the test where it is not done in a minute, and rethrows what it threw.</summary>
        private static async Task Done(Task task)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(Generous));
            Assert.Same(task, finished);
            await task;
        }

        [Fact]
        public async Task TheWork_RunsOnAThreadOfItsOwn_BelowNormalPriority_WhileTheCallerIsGivenBackAtOnce()
        {
            using (var started = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                Thread worker = null;
                ThreadPriority priority = ThreadPriority.Normal;

                // The work waits for the test, so that a Run that ran it on the caller's thread would never return.
                Task task = CadWarmUp.Run("a test list", () =>
                {
                    worker = Thread.CurrentThread;
                    priority = worker.Priority;
                    started.Set();
                    release.Wait(Generous);
                });

                Assert.True(started.Wait(Generous), "The work was never started.");
                Assert.False(task.IsCompleted, "Run gave back only when the work was done.");
                Assert.NotSame(Thread.CurrentThread, worker);
                Assert.Equal(ThreadPriority.BelowNormal, priority);
                Assert.True(worker.IsBackground, "The work's thread would keep the app from closing.");

                release.Set();
                await Done(task);
                Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            }
        }

        [Fact]
        public async Task AFailureOfTheWork_IsLoggedAndNotThrown_TheTaskStillCompletes()
        {
            Task task = CadWarmUp.Run("a failing list", () => throw new InvalidOperationException("no disk"));

            await Done(task);
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        }

        [Fact]
        public async Task ReadingTheTrueTypeFontsAhead_GivesTheSameListsAsAFreshScan_InTheSameOrder()
        {
            Task warm = CadTrueTypeFonts.WarmUp();
            await Done(warm);
            Assert.Same(warm, CadTrueTypeFonts.WarmUp());

            // The cached lists first, then a scan made again from the folders: the same families, files, other names.
            IReadOnlyList<string> families = CadTrueTypeFonts.Families;
            // FileOf trims the name it is asked, so a family a font file spells with a space at its end is left to the others.
            var plain = families.Where(f => f == f.Trim()).ToList();
            var files = plain.Select(CadTrueTypeFonts.FileOf).ToList();
            var others = families.Select(f => string.Join("|", CadTrueTypeFonts.LocalNamesOf(f))).ToList();
            SortedDictionary<string, string> fresh = CadTrueTypeFonts.Scan();

            Assert.Equal(fresh.Keys, families);
            Assert.Equal(plain.Select(f => fresh[f]), files);
            Assert.Equal(families.Select(f => string.Join("|", CadTrueTypeFonts.LocalNamesOf(f))), others);
        }

        [Fact]
        public async Task AListAskedWhileTheReadingRuns_WaitsForIt_AndGetsTheSameListAsOneAskedAfter()
        {
            // Several threads ask at once, as a window opened before the warm-up is done does: none reads twice or sees a part.
            Task warm = CadTrueTypeFonts.WarmUp();
            var askers = Enumerable
                .Range(0, 4)
                .Select(i => Task.Run(() => CadTrueTypeFonts.Families.ToList()))
                .ToArray();

            await Done(Task.WhenAll(askers));
            await Done(warm);

            foreach (Task<List<string>> asker in askers)
            {
                Assert.Equal(CadTrueTypeFonts.Families, await asker);
            }
        }
    }
}
