using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// Esc while Python computes (D707): <c>Stop()</c> from another thread interrupts the script on the owner thread. The
    /// script runs on the thread of the test inside <c>Run</c>; a timer thread presses Esc once the script says it is
    /// looping, by creating a file the thread waits for, so that the press never lands before the script has started.
    /// </summary>
    public sealed class StopTests : PythonHostTestBase
    {
        /// <summary>The bound of the tests: 25 times the 19-20 ms the interrupt of a pure Python loop took on net8 (research U1).</summary>
        private const int BoundMilliseconds = 500;

        public StopTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>
        /// Runs <paramref name="lines"/>, which must create the file <paramref name="marker"/> and then loop for ever, and
        /// presses Esc 50 ms after the file is there.
        /// </summary>
        /// <returns>The end of the run, and the milliseconds from the press to the end of the run.</returns>
        private (CadPythonRunEventArgs Run, long Milliseconds) RunAndStop(
            CadCommandSession session,
            string marker,
            params string[] lines
        )
        {
            var stopwatch = new Stopwatch();
            long pressed = -1;
            long ended = -1;
            EventHandler<CadPythonRunEventArgs> finished = (sender, e) => ended = stopwatch.ElapsedMilliseconds;
            Host.RunFinished += finished;
            var presser = new Thread(() =>
            {
                var wait = Stopwatch.StartNew();

                while (!File.Exists(marker) && wait.ElapsedMilliseconds < 60000)
                {
                    Thread.Sleep(5);
                }

                Thread.Sleep(50);
                stopwatch.Start();
                pressed = 0;
                Host.Stop();
            });
            presser.Start();

            try
            {
                CadPythonRunEventArgs run = RunLines(session, lines);
                presser.Join();
                Assert.Equal(0, pressed);
                return (run, ended);
            }
            finally
            {
                Host.RunFinished -= finished;
            }
        }

        private string Marker() => Path.Combine(Folder, "looping-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void AnEndlessLoop_IsStoppedByEsc_WithinTheBound_AndTheDrawingIsAsBefore()
        {
            // The loop runs after a circle was drawn by command() inside the run's group: the stop takes the circle back.
            CadCommandSession session = NewSession();
            string marker = Marker();

            var (run, milliseconds) = RunAndStop(
                session,
                marker,
                UsesCad,
                "cad.command('CIRCLE', (0, 0), 5)",
                "open(" + Raw(marker) + ", 'w').close()",
                "while True:",
                "    pass"
            );

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.InRange(milliseconds, 0, BoundMilliseconds);
            Assert.Equal(0, session.Document.EntityCount);
            Assert.False(session.Document.IsInGroup);
            Assert.False(session.Document.CanUndo);
            Assert.Contains(session.History, line => line.EndsWith("*Cancel*", StringComparison.Ordinal));
        }

        [Fact]
        public void TheRunAfterAStop_IsNotHitByTheInterrupt()
        {
            // An Esc that lands as a script ends waits in Python for the next bytecode (research M8); the host drains it
            // (D705 step 6), so the next script runs through to its end.
            CadCommandSession session = NewSession();
            string marker = Marker();
            RunAndStop(session, marker, "open(" + Raw(marker) + ", 'w').close()", "while True:", "    pass");

            CadPythonRunEventArgs next = RunLines(
                session,
                new[] { "total = 0", "for i in range(100000):", "    total += i", "print(total)" }
            );

            Assert.Equal(CadPythonOutcome.Completed, next.Outcome);
            Assert.Equal(new[] { "4999950000" }, Printed);
        }

        [Fact]
        public void StoppedThreeTimesInARow_EachRunEndsWithinTheBound()
        {
            CadCommandSession session = NewSession();

            for (int i = 0; i < 3; i++)
            {
                string marker = Marker();
                var (run, milliseconds) = RunAndStop(
                    session,
                    marker,
                    "open(" + Raw(marker) + ", 'w').close()",
                    "while True:",
                    "    pass"
                );

                Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
                Assert.InRange(milliseconds, 0, BoundMilliseconds);
            }
        }

        [Fact]
        public void AScriptThatCatchesException_IsStoppedAllTheSame()
        {
            // KeyboardInterrupt is not an Exception: the script's handler does not see it.
            CadCommandSession session = NewSession();
            string marker = Marker();

            var (run, milliseconds) = RunAndStop(
                session,
                marker,
                "try:",
                "    open(" + Raw(marker) + ", 'w').close()",
                "    while True:",
                "        pass",
                "except Exception:",
                "    print('swallowed')"
            );

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.InRange(milliseconds, 0, BoundMilliseconds);
            Assert.Empty(Printed);
        }

        [Fact]
        public void StopWithNoRunGoingOn_DoesNothing_AndTheNextRunIsNotInterrupted()
        {
            // The press check and the interrupt happen under the GIL and only while a run computes (D707), so a stray Esc
            // between runs is not kept for the next one.
            CadCommandSession session = NewSession();

            Host.Stop();
            CadPythonRunEventArgs run = RunLines(
                session,
                new[] { "total = 0", "for i in range(100000):", "    total += i", "print(total)" }
            );

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(new[] { "4999950000" }, Printed);
        }
    }
}
