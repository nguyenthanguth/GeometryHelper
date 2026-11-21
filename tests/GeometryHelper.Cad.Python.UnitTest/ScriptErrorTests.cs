using System;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The ways a script can end badly (P6, D925): by an error, an exit, an interrupt, a recursion that never ends, a .NET
    /// exception, a missing module, a syntax error. Each is run as PYRUN runs a file and as a palette entry, and each must leave
    /// the app running: the error on the command line of the drawing, the palette's lines, no undo group open, nothing of the
    /// run drawn when it failed, and the next run working. Only <c>os._exit</c> is left out, since it is documented as the one
    /// way a script ends the process.
    /// </summary>
    public sealed class ScriptErrorTests : PythonHostTestBase
    {
        public ScriptErrorTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>A line drawn through the object model first, so that a run that fails shows whether its step was cancelled.</summary>
        private static string[] DrawThen(params string[] lines) =>
            new[] { UsesCad, AddLineInPython, "add_line(cad.document(), 0)" }.Concat(lines).ToArray();

        /// <summary>The line number, in the file <see cref="DrawThen"/> wrote, of the <paramref name="index"/>th line after the drawing.</summary>
        private static int Line(int index) => string.Join("\n", DrawThen()).Split('\n').Length + 1 + index;

        /// <summary>The host is not running, the session is at the Command prompt, no group is open, and the next run works.</summary>
        private void AssertTheAppGoesOn(CadCommandSession session)
        {
            Assert.False(Host.IsRunning);
            Assert.Null(Host.RunningDocument);
            Assert.False(session.IsRunning);
            Assert.False(session.Document.IsInGroup);

            CadPythonRunEventArgs next = RunLines(session, new[] { "print('still here')" });

            Assert.Equal(CadPythonOutcome.Completed, next.Outcome);
            Assert.Contains("still here", Printed);
            Assert.Contains("still here", session.History);
        }

        private static bool Wrote(CadCommandSession session, string start) =>
            session.History.Any(line => line.StartsWith(start, StringComparison.Ordinal));

        [Fact]
        public void ADivisionByZero_FailsWithItsLineOnTheCommandLine_AndTheDrawingIsAsBefore()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen("1 / 0"));

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.True(
                Wrote(session, "Python error: ZeroDivisionError: division by zero (script1.py, line " + Line(0) + ")")
            );
            Assert.Contains("ZeroDivisionError: division by zero", Errors);
            Assert.Equal(0, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void AnExitWithACode_FailsAndNamesTheCode_AndTheDrawingIsAsBefore()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen("import sys", "sys.exit(3)"));

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.True(Wrote(session, "Python error: SystemExit: 3 (script1.py, line " + Line(1) + ")"));
            Assert.Equal(0, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void AnExitWithAMessage_FailsAndSaysTheMessage()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen("import sys", "sys.exit('bye')"));

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.True(Wrote(session, "Python error: SystemExit: bye (script1.py, line " + Line(1) + ")"));
            Assert.Equal(0, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
        }

        [Theory]
        [InlineData("exit()")]
        [InlineData("quit()")]
        public void ExitAndQuit_EndTheScriptAsACompletedRun_KeepingWhatItDrew(string leave)
        {
            // Neither ends the app: they raise SystemExit with no code, which is the end of a script and no error.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen(leave, "print('not reached')"));

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Null(run.Error);
            Assert.Empty(Errors);
            Assert.Empty(Printed);
            Assert.Equal(1, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void AKeyboardInterruptRaisedByTheScript_CancelsTheRun_AndTheDrawingIsAsBefore()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen("raise KeyboardInterrupt"));

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Contains("*Cancel*", session.History);
            Assert.Contains("KeyboardInterrupt", Errors);
            Assert.Equal(0, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void ARecursionThatNeverEnds_FailsWithARecursionError_AndTheAppGoesOn()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen("def f(): f()", "f()"));

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.True(Wrote(session, "Python error: RecursionError: maximum recursion depth exceeded"));
            Assert.Equal(0, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void TwoHundredLevelsOfRecursionThroughANetCallback_CompleteWithTheirAnswer()
        {
            // Each level goes from Python into a .NET delegate that calls back into Python: 200 levels use well under the 1 MB
            // of the owner thread's stack (about 650 would not). The delegate is deleted at the end: one left alive in the script's globals
            // would be serialised when the shared host shuts down, which a delegate made from a Python function cannot be on .NET
            // Framework inside the AppDomain xUnit makes.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[]
                {
                    "from System import Func, Int32",
                    "def f(n):",
                    "    if n == 0: return 0",
                    "    return d.Invoke(n - 1) + 1",
                    "d = Func[Int32, Int32](f)",
                    "print(d.Invoke(200))",
                    "del d",
                }
            );

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(new[] { "200" }, Printed);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void ANetExceptionFromTheObjectModel_FailsWithItsTypeAndMessage_AndTheDrawingIsAsBefore()
        {
            // ApplyEdit(None, None, None) throws ArgumentNullException for the name: the type is named, not hidden behind
            // pythonnet's own.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen("cad.document().ApplyEdit(None, None, None)"));

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.True(Wrote(session, "Python error: System.ArgumentNullException: Value cannot be null."));
            Assert.Contains(Errors, line => line.StartsWith("System.ArgumentNullException", StringComparison.Ordinal));
            Assert.Equal(0, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void AnErrorInAPyLoadCommand_FailsOnTheCommandLine_AndTheCommandCanBeRunAgain()
        {
            CadCommandSession session = NewSession();
            string path = Script(
                "p6-defined.py",
                "from geometryhelper.cad import cad_command, write",
                "@cad_command('P6BOOM')",
                "def boom():",
                "    write('before')",
                "    1 / 0"
            );
            Load(session, path);
            Owner.Session = session;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                int before = Finished.Count;
                Assert.True(session.Feed("P6BOOM"));
                Owner.Drain();

                CadPythonRunEventArgs run = Finished[before];
                Assert.Equal("P6BOOM", run.Name);
                Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            }

            Assert.Equal(
                2,
                session.History.Count(line =>
                    line == "Python error: ZeroDivisionError: division by zero (p6-defined.py, line 5)"
                )
            );
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void AHandlerThatRaisesWhenTheEventFiresAfterTheRun_ThrowsToTheCallerOfTheEvent_AndTheHostGoesOn()
        {
            // Python calls the handler on whichever thread raises the event, with no run going on and the host knowing nothing of
            // it. What the host guarantees is that the error is the caller's to meet (the window's handler of unhandled errors,
            // D926) and that the host is not left in a state: no run, no group, the next run working.
            CadCommandSession session = NewSession();
            CadPythonRunEventArgs run = RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "from System import EventHandler",
                    "from GeometryHelper.Cad import CadDocumentChangedEventArgs",
                    "def on_changed(sender, e):",
                    "    raise ValueError('handler boom')",
                    "import builtins",
                    "builtins.p6_handler = EventHandler[CadDocumentChangedEventArgs](on_changed)",
                    "cad.document().Changed += builtins.p6_handler",
                }
            );
            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);

            global::Python.Runtime.PythonException thrown = Assert.Throws<global::Python.Runtime.PythonException>(() =>
                AddLine(session.Document, 0)
            );

            Assert.Contains("handler boom", thrown.Message, StringComparison.Ordinal);

            // The handler is taken off again, so that no delegate made from a Python function is alive when the shared host shuts
            // down (it cannot be serialised on .NET Framework inside the AppDomain xUnit makes).
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
            Assert.False(Host.IsRunning);
            Assert.False(session.Document.IsInGroup);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void AMissingModule_FailsWithModuleNotFoundError_AndTheDrawingIsAsBefore()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen("import nonexistent_module"));

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.True(
                Wrote(
                    session,
                    "Python error: ModuleNotFoundError: No module named 'nonexistent_module' (script1.py, line "
                        + Line(0)
                        + ")"
                )
            );
            Assert.Equal(0, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
        }

        [Fact]
        public void AFileWithASyntaxError_RunsNothingOfIt_AndNamesTheFileAndLine()
        {
            // The line that would draw comes before the broken bracket, but a file that does not compile runs no line at all.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, DrawThen("x = (1,", "y = 2"));

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.True(
                Wrote(session, "Python error: SyntaxError: '(' was never closed (script1.py, line " + Line(0) + ")")
            );
            Assert.Equal(0, session.Document.EntityCount);
            AssertTheAppGoesOn(session);
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

        /// <summary>One entry of the palette: the run it made, the command line's new lines, and the palette's error lines.</summary>
        private CadPythonRunEventArgs Entry(CadCommandSession session, string text)
        {
            int before = Finished.Count;
            Assert.Equal(CadPythonEntry.Ran, Enter(session, text));
            Assert.True(Finished.Count > before, "The entry did not run.");
            return Finished.Last();
        }

        [Theory]
        [InlineData("1 / 0", CadPythonOutcome.Failed, "Python error: ZeroDivisionError: division by zero")]
        [InlineData("import sys; sys.exit(3)", CadPythonOutcome.Failed, "Python error: SystemExit: 3")]
        [InlineData("import sys; sys.exit('bye')", CadPythonOutcome.Failed, "Python error: SystemExit: bye")]
        [InlineData("raise KeyboardInterrupt", CadPythonOutcome.Cancelled, "*Cancel*")]
        [InlineData(
            "import nonexistent_module",
            CadPythonOutcome.Failed,
            "Python error: ModuleNotFoundError: No module named 'nonexistent_module'"
        )]
        [InlineData(
            "import geometryhelper.cad as cad; cad.document().ApplyEdit(None, None, None)",
            CadPythonOutcome.Failed,
            "Python error: System.ArgumentNullException: Value cannot be null."
        )]
        public void APaletteEntryThatEndsBadly_ShowsItOnTheCommandLine_AndThePaletteGoesOn(
            string entry,
            CadPythonOutcome outcome,
            string commandLine
        )
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = Entry(session, entry);

            Assert.Equal(outcome, run.Outcome);
            Assert.Equal("Python", run.Name);
            Assert.True(Wrote(session, commandLine));
            Assert.NotEmpty(Errors);
            AssertThePaletteGoesOn(session);
        }

        [Theory]
        [InlineData("exit()")]
        [InlineData("quit()")]
        public void ExitAndQuitInThePalette_CompleteWithNoError(string entry)
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = Entry(session, entry);

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Empty(Errors);
            AssertThePaletteGoesOn(session);
        }

        [Fact]
        public void ARecursionThatNeverEndsInThePalette_FailsWithARecursionError()
        {
            CadCommandSession session = NewSession();
            Assert.Equal(CadPythonEntry.Ran, Enter(session, "def f():\n    f()\n"));

            CadPythonRunEventArgs run = Entry(session, "f()");

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.True(Wrote(session, "Python error: RecursionError: maximum recursion depth exceeded"));
            AssertThePaletteGoesOn(session);
        }

        [Fact]
        public void TwoHundredLevelsThroughANetCallbackInThePalette_GiveTheirAnswer()
        {
            CadCommandSession session = NewSession();
            Enter(session, "from System import Func, Int32");
            Enter(session, "def f(n):\n    if n == 0: return 0\n    return d.Invoke(n - 1) + 1\n");
            Enter(session, "d = Func[Int32, Int32](f)");

            CadPythonRunEventArgs run = Entry(session, "d.Invoke(200)");

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Contains("200", Printed);
            Enter(session, "del d");
            AssertThePaletteGoesOn(session);
        }

        [Fact]
        public void ASyntaxErrorInThePalette_IsShownOnTheCommandLine_WithoutARun()
        {
            CadCommandSession session = NewSession();

            Assert.Equal(CadPythonEntry.Ran, Enter(session, "x = (1,\ny = 2"));

            Assert.Empty(Finished);
            Assert.True(Wrote(session, "Python error: SyntaxError: '(' was never closed"));
            Assert.NotEmpty(Errors);
            AssertThePaletteGoesOn(session);
        }

        /// <summary>The host is idle, no group is open, and the next entry runs.</summary>
        private void AssertThePaletteGoesOn(CadCommandSession session)
        {
            Assert.False(Host.IsRunning);
            Assert.False(session.IsRunning);
            Assert.False(session.Document.IsInGroup);

            int before = Finished.Count;
            Assert.Equal(CadPythonEntry.Ran, Enter(session, "print('still here')"));

            Assert.Equal(CadPythonOutcome.Completed, Finished[before].Outcome);
            Assert.Contains("still here", session.History);
        }
    }
}
