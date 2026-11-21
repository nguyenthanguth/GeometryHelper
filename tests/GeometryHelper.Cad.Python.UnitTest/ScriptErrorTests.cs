using System;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// P6 (D925, D926): every way a script can end badly leaves the app running. For each script of D925 (not
    /// <c>os._exit</c>, which is documented as the one way a script ends the process): the outcome, the line on the command
    /// line, no undo group left open and the drawing as before the run, and the next run completing. Each script first
    /// draws a circle, so that "as before the run" and "no group left open" mean something: a run that fails or is stopped
    /// takes the circle back, a run that ends by <c>exit()</c> keeps it.
    /// </summary>
    public sealed class ScriptErrorTests : PythonHostTestBase
    {
        /// <summary>The first two lines of every script: the module, and a circle of radius 5 at the origin as one undo step.</summary>
        private static readonly string[] Draws = { UsesCad, "cad.command('CIRCLE', (0, 0), 5)" };

        public ScriptErrorTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>The lines of <see cref="Draws"/> followed by <paramref name="rest"/>; the first of <paramref name="rest"/> is line 3.</summary>
        private static string[] AfterDrawing(params string[] rest) => Draws.Concat(rest).ToArray();

        /// <summary>
        /// Runs the script as PYRUN does, checks what every ending has in common, and gives the run: no run going on, no
        /// group open, <paramref name="circles"/> circles left in the drawing, and the next run completing and printing.
        /// </summary>
        private CadPythonRunEventArgs RunAndCheck(CadCommandSession session, string name, string[] lines, int circles)
        {
            CadPythonRunEventArgs run = Run(session, Script(name, lines));
            AssertHostUsable(session, circles);
            return run;
        }

        private void AssertHostUsable(CadCommandSession session, int circles)
        {
            Assert.False(Host.IsRunning);
            Assert.False(session.Document.IsInGroup);
            Assert.Equal(circles, session.Document.EntityCount);

            CadPythonRunEventArgs next = RunLines(session, new[] { "print('the next run')" });

            Assert.Equal(CadPythonOutcome.Completed, next.Outcome);
            Assert.Equal("the next run", Printed.Last());
        }

        [Fact]
        public void ADivisionByZero_FailsWithItsLineOnTheCommandLine_TakesTheCircleBack_AndTheNextRunCompletes()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(session, "zero.py", AfterDrawing("1 / 0"), 0);

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Equal("ZeroDivisionError: division by zero", run.Error);
            Assert.Contains("Python error: ZeroDivisionError: division by zero (zero.py, line 3)", session.History);
        }

        [Fact]
        public void SysExitThree_Fails_NamesTheCodeOnTheCommandLine_AndTheNextRunCompletes()
        {
            // A script that leaves with a code other than 0 or None has failed (D705 step 5); python itself would exit with 3.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(session, "exit3.py", AfterDrawing("import sys", "sys.exit(3)"), 0);

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Contains("Python error: SystemExit: 3 (exit3.py, line 4)", session.History);
        }

        [Fact]
        public void SysExitWithAMessage_Fails_NamesTheMessageOnTheCommandLine_AndTheNextRunCompletes()
        {
            // sys.exit("bye") is a code that is not 0 or None: python prints bye and exits with 1, so the run has failed.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(
                session,
                "exitbye.py",
                AfterDrawing("import sys", "sys.exit('bye')"),
                0
            );

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Contains("Python error: SystemExit: bye (exitbye.py, line 4)", session.History);
        }

        [Theory]
        [InlineData("exit()")]
        [InlineData("quit()")]
        public void ExitOrQuit_CompletesWithNoErrorLine_KeepsTheCircle_AndTheNextRunCompletes(string leave)
        {
            // exit() and quit() raise SystemExit with no code, which is a normal leave: the circle is kept as ending a run
            // does, nothing is written as an error and the run has no error text.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(session, "leave.py", AfterDrawing(leave), 1);

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Null(run.Error);
            Assert.DoesNotContain(session.History, line => line.StartsWith("Python error", StringComparison.Ordinal));
            Assert.Empty(Errors);
        }

        [Fact]
        public void ARaisedKeyboardInterrupt_IsCancelled_WritesCancel_TakesTheCircleBack_AndTheNextRunCompletes()
        {
            // The same ending as Esc: *Cancel* on the command line, KeyboardInterrupt in the palette, no "Python error" line.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(
                session,
                "interrupt.py",
                AfterDrawing("raise KeyboardInterrupt"),
                0
            );

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Contains("*Cancel*", session.History);
            Assert.Contains("KeyboardInterrupt", Errors);
            Assert.DoesNotContain(session.History, line => line.StartsWith("Python error", StringComparison.Ordinal));
        }

        [Fact]
        public void EndlessRecursion_FailsWithARecursionError_TakesTheCircleBack_AndTheNextRunCompletes()
        {
            // def f(): f() is called until Python's own limit (1000 frames) stops it; the message is Python's.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(
                session,
                "recurse.py",
                AfterDrawing("def f():", "    f()", "f()"),
                0
            );

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.StartsWith("RecursionError: maximum recursion depth exceeded", run.Error, StringComparison.Ordinal);
            Assert.Contains(session.History, line =>
                line.StartsWith(
                    "Python error: RecursionError: maximum recursion depth exceeded",
                    StringComparison.Ordinal
                ) && line.EndsWith("(recurse.py, line 4)", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void TwoHundredLevelsOfRecursionThroughADotNetCallback_Complete_AndTheNextRunCompletes()
        {
            // down(n) calls itself through a System.Func that wraps it, so every level crosses from Python to .NET and back:
            // 200 levels is far under the 1000 of Python's own limit and must not end the process. down(200) = 200. The
            // script drops its delegate at the end (holder.clear()); one left behind in a cycle is the case of
            // ScriptDelegateShutdownTests.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(
                session,
                "callback.py",
                AfterDrawing(
                    "from System import Func, Int32",
                    "holder = []",
                    "def down(n):",
                    "    return 0 if n == 0 else holder[0].Invoke(n - 1) + 1",
                    "holder.append(Func[Int32, Int32](down))",
                    "print(down(200))",
                    "holder.clear()"
                ),
                1
            );

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Contains("200", Printed);
        }

        [Fact]
        public void ADotNetExceptionFromTheObjectModel_IsOneLineNamingItsType_TakesTheCircleBack_AndTheNextRunCompletes()
        {
            // CadDocument.BeginGroup(None) throws ArgumentNullException ("Value cannot be null." and the parameter name);
            // pythonnet raises it as a Python exception whose text carries the .NET stack trace after the message. The
            // command line gets one line: the type and the message, then the file and line, never the stack trace or a line
            // end (net48 and net8 word the parameter name differently, so only the start of the message is compared). The
            // group the host opened for the run is still open under the exception and is cancelled.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(
                session,
                "badargument.py",
                AfterDrawing("cad.document().BeginGroup(None)"),
                0
            );

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.StartsWith(
                "System.ArgumentNullException: Value cannot be null.",
                run.Error,
                StringComparison.Ordinal
            );
            Assert.DoesNotContain("\n", run.Error);
            string line = Assert.Single(session.History, entry =>
                entry.StartsWith("Python error: ", StringComparison.Ordinal)
            );
            Assert.StartsWith(
                "Python error: System.ArgumentNullException: Value cannot be null.",
                line,
                StringComparison.Ordinal
            );
            Assert.EndsWith("(badargument.py, line 3)", line, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
        }

        [Fact]
        public void ADotNetExceptionTypedInThePalette_IsOneLineNamingItsType_AndTheNextEntryRuns()
        {
            // The same exception from a palette entry: one line, with no file part and no stack trace.
            CadCommandSession session = NewSession();

            Enter(session, "import geometryhelper.cad as cad; cad.document().BeginGroup(None)");

            Assert.Equal(CadPythonOutcome.Failed, Finished.Last().Outcome);
            string line = Assert.Single(session.History, entry =>
                entry.StartsWith("Python error: ", StringComparison.Ordinal)
            );
            Assert.StartsWith(
                "Python error: System.ArgumentNullException: Value cannot be null.",
                line,
                StringComparison.Ordinal
            );
            Assert.DoesNotContain("\n", line);
            Assert.DoesNotContain("\r", line);
            Assert.False(session.Document.IsInGroup);

            Enter(session, "1 + 1");
            Assert.Equal("2", Printed.Last());
        }

        [Fact]
        public void ACommandOfAPyLoadFileThatRaises_Fails_TakesTheCircleBack_AndTheNextRunCompletes()
        {
            // PYLOAD defines P6BOOM; typing it runs its function as one step named P6BOOM, which draws a circle and raises.
            CadCommandSession session = NewSession();
            string path = Script(
                "p6boom.py",
                UsesCad,
                "@cad.cad_command('P6BOOM')",
                "def boom():",
                "    cad.command('CIRCLE', (0, 0), 5)",
                "    1 / 0"
            );
            Load(session, path);
            Owner.Session = session;

            session.Feed("P6BOOM");
            Owner.Drain();

            CadPythonRunEventArgs run = Finished.Last();
            Assert.Equal("P6BOOM", run.Name);
            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Equal("ZeroDivisionError: division by zero", run.Error);
            Assert.Contains("Python error: ZeroDivisionError: division by zero (p6boom.py, line 5)", session.History);
            AssertHostUsable(session, 0);
        }

        [Fact]
        public void ALoadThatRaises_Fails_TakesTheCircleBack_AndTheNextRunCompletes()
        {
            // The module-level code of a PYLOAD file draws a circle and raises: the run is PYLOAD and the circle is taken back.
            CadCommandSession session = NewSession();
            string path = Script("p6load.py", AfterDrawing("raise RuntimeError('at load')"));

            CadPythonRunEventArgs run = Load(session, path);

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Equal("PYLOAD", run.Name);
            Assert.Contains("Python error: RuntimeError: at load (p6load.py, line 3)", session.History);
            AssertHostUsable(session, 0);
        }

        [Fact]
        public void AHandlerThatRaisesAfterTheRunEnded_ThrowsToTheCallerOfTheEvent_AndTheHostGoesOn()
        {
            // pythonnet builds the delegate of a handler and calls the function on whichever thread raises the event, with no run
            // going on, so the host cannot catch the error there: it reaches the caller of the event as a PythonException, and the
            // window reports it (CadCrashLog, D926). What the host guarantees is that it is left in no state: no run, no group, the
            // next run working. The handler is taken off at the end so that no delegate made from a Python function outlives the test.
            CadCommandSession session = NewSession();
            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "import builtins",
                    "def handler(sender, args):",
                    "    raise RuntimeError('handler boom')",
                    "builtins.p6_handler = handler",
                    "cad.document().Changed += handler",
                }
            );
            Assert.Empty(Errors);

            global::Python.Runtime.PythonException thrown = Assert.Throws<global::Python.Runtime.PythonException>(() =>
                AddLine(session.Document, 10)
            );

            Assert.Contains("handler boom", thrown.Message, StringComparison.Ordinal);
            Assert.Equal("RuntimeError", thrown.Type.Name);
            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "import builtins",
                    "cad.document().Changed -= builtins.p6_handler",
                    "del builtins.p6_handler",
                }
            );
            AssertHostUsable(session, 1);
        }

        [Theory]
        [InlineData("import sys; sys.stdout = None")]
        [InlineData("import sys; sys.stderr = None")]
        [InlineData("import sys; f = open(__file__ + '.log', 'w'); f.close(); sys.stdout = f")]
        [InlineData("import sys; sys.stdout = open(__file__ + '.log', 'w')")]
        public void AScriptThatLeavesItsOutputStreamBroken_DoesNotTakeTheOutputOfTheNextRun(string spoil)
        {
            // None, a closed file and a file are all things a script may leave in sys.stdout or sys.stderr. The host puts its own
            // writers back before every run, so the next run prints to the command line as the first did.
            CadCommandSession session = NewSession();
            RunLines(session, new[] { spoil });

            CadPythonRunEventArgs next = RunLines(session, new[] { "print('next ok')" });

            Assert.Equal(CadPythonOutcome.Completed, next.Outcome);
            Assert.Equal(new[] { "next ok" }, Printed);
            Assert.Contains("next ok", session.History);
        }

        [Fact]
        public void AScriptThatSetsStdoutToNone_IsItselfReportedAsCompleted_NotAsAHostError()
        {
            // The run's own end flushes sys.stdout, which is None by then: that is the script's affair, not a failure of the host.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, AfterDrawing("import sys", "sys.stdout = None"));

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Null(run.Error);
            Assert.Equal(1, session.Document.EntityCount);
            Assert.DoesNotContain(session.History, line =>
                line.StartsWith("Python host error", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void TwoHundredLevelsThroughADotNetCallbackInThePalette_GiveTheirAnswer()
        {
            CadCommandSession session = NewSession();
            Enter(session, "from System import Func, Int32");
            Enter(session, "def f(n):\n    if n == 0: return 0\n    return d.Invoke(n - 1) + 1\n");
            Enter(session, "d = Func[Int32, Int32](f)");

            Enter(session, "d.Invoke(200)");

            Assert.Equal(CadPythonOutcome.Completed, Finished.Last().Outcome);
            Assert.Contains("200", Printed);
            Enter(session, "del d");
            Enter(session, "1 + 1");
            Assert.Equal("2", Printed.Last());
        }

        [Fact]
        public void ASyntaxErrorTypedInThePalette_IsShownOnTheCommandLine_WithoutARun()
        {
            CadCommandSession session = NewSession();

            Assert.Equal(CadPythonEntry.Ran, Enter(session, "x = (1,\ny = 2"));

            Assert.Empty(Finished);
            Assert.Contains(session.History, line =>
                line.StartsWith("Python error: SyntaxError: '(' was never closed", StringComparison.Ordinal)
            );
            Assert.NotEmpty(Errors);
            Enter(session, "1 + 1");
            Assert.Equal("2", Printed.Last());
        }

        [Fact]
        public void ADelegateMadeFromAFunctionOfTheScript_LeftBehindInACycle_StillLetsTheHostShutDown()
        {
            // f reads d and d wraps f, through the globals of the run: a cycle that only Python's collector frees, so the
            // System.Func is still alive when Dispose shuts Python down. The run itself completes; the shutdown is what the
            // collection's cleanup checks: on net48 it fails there, against every test of the run, as "Test Collection Cleanup
            // Failure (Python host)" (a SerializationException from pythonnet's runtime-data stash).
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[] { "from System import Func, Int32", "def f(n):", "    return d", "d = Func[Int32, Int32](f)" }
            );

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
        }

        [Fact]
        public void AnImportThatIsNotThere_FailsWithAModuleNotFoundError_AndTheNextRunCompletes()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(
                session,
                "noimport.py",
                AfterDrawing("import nonexistent_module"),
                0
            );

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Contains(
                "Python error: ModuleNotFoundError: No module named 'nonexistent_module' (noimport.py, line 3)",
                session.History
            );
        }

        [Fact]
        public void AFileWithASyntaxError_FailsBeforeAnyLineRuns_NamesTheLine_AndTheNextRunCompletes()
        {
            // The bracket opened on line 3 is never closed, so Python refuses the whole file and the circle on line 2 is
            // never drawn.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunAndCheck(session, "syntax.py", AfterDrawing("x = (1,", "y = 2"), 0);

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Contains(session.History, line =>
                line.StartsWith("Python error: SyntaxError: ", StringComparison.Ordinal)
                && line.EndsWith("(syntax.py, line 3)", StringComparison.Ordinal)
            );
        }

        [Theory]
        [InlineData("1 / 0", CadPythonOutcome.Failed, "Python error: ZeroDivisionError: division by zero")]
        [InlineData("import sys; sys.exit(3)", CadPythonOutcome.Failed, "Python error: SystemExit: 3")]
        [InlineData("import sys; sys.exit('bye')", CadPythonOutcome.Failed, "Python error: SystemExit: bye")]
        [InlineData(
            "import nonexistent_module",
            CadPythonOutcome.Failed,
            "Python error: ModuleNotFoundError: No module named 'nonexistent_module'"
        )]
        [InlineData("exit()", CadPythonOutcome.Completed, null)]
        [InlineData("quit()", CadPythonOutcome.Completed, null)]
        [InlineData("raise KeyboardInterrupt", CadPythonOutcome.Cancelled, "*Cancel*")]
        public void ThePaletteEntry_EndsAsAScriptDoes_WithItsLineOnTheCommandLine_AndTheNextEntryRuns(
            string code,
            CadPythonOutcome outcome,
            string line
        )
        {
            // One line typed in the palette: the same outcomes and texts as a file, but no file part in the line (D711).
            CadCommandSession session = NewSession();

            Enter(session, code);

            Assert.Equal(outcome, Finished.Last().Outcome);

            if (line != null)
            {
                Assert.Contains(line, session.History);
            }
            else
            {
                Assert.DoesNotContain(session.History, entry =>
                    entry.StartsWith("Python error", StringComparison.Ordinal)
                );
            }

            Assert.False(Host.IsRunning);
            Assert.False(session.Document.IsInGroup);

            Enter(session, "1 + 1");
            Assert.Equal("2", Printed.Last());
        }

        [Fact]
        public void ARecursionErrorTypedInThePalette_IsReported_AndTheNextEntryRuns()
        {
            // A definition of two lines is complete with the empty line a user ends it with.
            CadCommandSession session = NewSession();

            Enter(session, "def f():\n    f()\n");
            Enter(session, "f()");

            Assert.Equal(CadPythonOutcome.Failed, Finished.Last().Outcome);
            Assert.Contains(session.History, line =>
                line.StartsWith(
                    "Python error: RecursionError: maximum recursion depth exceeded",
                    StringComparison.Ordinal
                )
            );

            Enter(session, "1 + 1");
            Assert.Equal("2", Printed.Last());
        }
    }
}
