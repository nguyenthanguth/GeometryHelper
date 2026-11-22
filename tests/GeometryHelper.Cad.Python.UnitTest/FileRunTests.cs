using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// A file run as PYRUN runs it (D705, D711): its globals, its folder on <c>sys.path</c>, what it prints, how it ends,
    /// and the refusals before and around it (D718, D705 step 1), and the package it imports (D702, D712).
    /// </summary>
    public sealed class FileRunTests : PythonHostTestBase
    {
        public FileRunTests(PythonHostFixture fixture)
            : base(fixture) { }

        [Fact]
        public void TwoPrintedLines_AreTwoLinesOnTheCommandLineAndTwoInTheOutput_NotOneForEachWrite()
        {
            // print("hello", "world") makes five writes to sys.stdout (research M6 saw 11 for two prints); the host gathers them
            // into the two whole lines the script printed.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, new[] { "print('hello', 'world')", "print('second')" });

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(new[] { "hello world", "second" }, Printed);
            Assert.Empty(Errors);
            Assert.Contains("hello world", session.History);
            Assert.Contains("second", session.History);
        }

        [Fact]
        public void APartialLineAtTheEndOfTheRun_IsFlushedAsALine()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { "import sys", "sys.stdout.write('no line end')" });

            Assert.Equal(new[] { "no line end" }, Printed);
            Assert.Contains("no line end", session.History);
        }

        [Fact]
        public void APartialLineBeforeAPrompt_IsShownBeforeTheQuestionIsAsked()
        {
            // input("Name? ") writes the question to stdout and then waits: the question has to be out before the wait.
            CadCommandSession session = NewSession();
            string[] seenAtThePrompt = null;

            RunLines(
                session,
                new[] { UsesCad, "import sys", "sys.stdout.write('Name? ')", "cad.get_string('Name')" },
                s =>
                {
                    seenAtThePrompt = Printed.ToArray();
                    s.Feed("Thang");
                }
            );

            Assert.Equal(new[] { "Name? " }, seenAtThePrompt);
        }

        [Fact]
        public void TextWrittenToStderr_IsAnErrorLine()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { "import sys", "print('oops', file=sys.stderr)" });

            Assert.Equal(new[] { "oops" }, Errors);
            Assert.Empty(Printed);
        }

        [Fact]
        public void TheRunIsAnnounced_WithTheStepNameAndTheDrawing()
        {
            CadCommandSession session = NewSession("first.dxf");

            CadPythonRunEventArgs run = RunLines(session, new[] { "pass" });

            CadPythonRunEventArgs started = Assert.Single(Started);
            Assert.Equal("PYRUN", started.Name);
            Assert.Same(session.Document, started.Document);
            Assert.Null(started.Outcome);
            Assert.Equal("PYRUN", run.Name);
            Assert.Same(session.Document, run.Document);
            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Null(run.Error);
        }

        [Fact]
        public void AFileRun_CarriesTheFullPathOfItsFile_AndAPaletteEntryNone()
        {
            // So that a window can write "PYRUN circles.py" for a PYRUN typed on the command line (D737).
            CadCommandSession session = NewSession();
            string path = Script("named.py", "pass");

            CadPythonRunEventArgs run = Run(session, path);
            Enter(session, "1 + 1");

            Assert.Equal(Path.GetFullPath(path), Started[0].Path);
            Assert.Equal(Path.GetFullPath(path), run.Path);
            Assert.Null(Started[1].Path);
        }

        [Fact]
        public void WhileAScriptAsks_TheHostIsRunningInThatDrawing()
        {
            CadCommandSession session = NewSession("first.dxf");
            bool running = false;
            CadDocument where = null;

            RunLines(session, new[] { UsesCad, "cad.get_point('Where')" }, s =>
            {
                running = Host.IsRunning;
                where = Host.RunningDocument;
                s.Feed("1,1");
            });

            Assert.True(running);
            Assert.Same(session.Document, where);
            Assert.False(Host.IsRunning);
            Assert.Null(Host.RunningDocument);
        }

        [Fact]
        public void EachRunStartsWithFreshGlobals()
        {
            // The first run sets counter; a run that shared globals would find it and print 2.
            CadCommandSession session = NewSession();
            string path = Script(
                "counter.py",
                "try:",
                "    counter",
                "except NameError:",
                "    counter = 0",
                "counter += 1",
                "print(counter)"
            );

            Run(session, path);
            Run(session, path);

            Assert.Equal(new[] { "1", "1" }, Printed);
        }

        [Fact]
        public void TheScript_IsMain_AndKnowsItsFullPath()
        {
            CadCommandSession session = NewSession();
            string path = Script("who.py", "print(__name__)", "print(__file__)");

            Run(session, path);

            Assert.Equal(2, Printed.Count);
            Assert.Equal("__main__", Printed[0]);
            Assert.Equal(Path.GetFullPath(path), Printed[1], ignoreCase: true);
        }

        [Fact]
        public void ItsFolderIsFirstOnSysPathDuringTheRun_AndOffItAfter()
        {
            // As `python file.py` does: the folder of the script is on the path for the run only, so one script's neighbours
            // are not importable from the next script that is run from another folder.
            CadCommandSession session = NewSession();
            string first = Script("first-folder.py", "import sys", "print(sys.path[0])");
            string otherFolder = Path.Combine(Folder, "other");
            Directory.CreateDirectory(otherFolder);
            string second = WriteFile(
                otherFolder,
                "second-folder.py",
                Lines("import sys", "print(sys.path[0])", "print(" + Raw(Folder) + " in sys.path)")
            );

            Run(session, first);
            Run(session, second);

            Assert.Equal(3, Printed.Count);
            Assert.Equal(Folder, Printed[0], ignoreCase: true);
            Assert.Equal(otherFolder, Printed[1], ignoreCase: true);
            Assert.Equal("False", Printed[2]);
        }

        [Fact]
        public void ASiblingModule_IsImportedFromTheScriptsFolder()
        {
            CadCommandSession session = NewSession();
            WriteFile(Folder, "sibling_module_h2.py", Lines("VALUE = 7"));
            string path = Script("uses-sibling.py", "import sibling_module_h2", "print(sibling_module_h2.VALUE)");

            CadPythonRunEventArgs run = Run(session, path);

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(new[] { "7" }, Printed);
        }

        [Theory]
        [InlineData("raise SystemExit(0)")]
        [InlineData("import sys; sys.exit()")]
        public void ALeaveWithoutAnError_Completes(string code)
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, new[] { code });

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Null(run.Error);
        }

        [Fact]
        public void ALeaveWithAnotherCode_Fails()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(session, new[] { "raise SystemExit(3)" });

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
        }

        [Fact]
        public void ADivisionByZero_FailsWithTheLineOnTheCommandLine_AndATracebackWithoutTheHostsFrames()
        {
            // boom.py: x = 1, y = 0, z = x / y on line 3. The traceback is the file's own: the wrapper that executes it
            // (`<string>`) and the frames of _app.py are dropped (D711, research M12).
            CadCommandSession session = NewSession();
            string path = Script("boom.py", "x = 1", "y = 0", "z = x / y");

            CadPythonRunEventArgs run = Run(session, path);

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Equal("ZeroDivisionError: division by zero", run.Error);
            Assert.Contains("Python error: ZeroDivisionError: division by zero (boom.py, line 3)", session.History);
            Assert.Contains(Errors, line => line.Contains("boom.py") && line.Contains("line 3"));
            Assert.Contains("ZeroDivisionError: division by zero", Errors);
            Assert.DoesNotContain(Errors, line => line.Contains("_app.py") || line.Contains("<string>"));
        }

        [Fact]
        public void AnErrorInOneRun_DoesNotStopTheNext()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { "1 / 0" });
            CadPythonRunEventArgs next = RunLines(session, new[] { "print('still here')" });

            Assert.Equal(CadPythonOutcome.Completed, next.Outcome);
            Assert.Contains("still here", Printed);
        }

        [Fact]
        public void ASyntaxError_FailsWithTheFileAndTheLine()
        {
            // The bracket opened on line 1 is never closed.
            CadCommandSession session = NewSession();
            string path = Script("bad.py", "x = (1,", "y = 2");

            CadPythonRunEventArgs run = Run(session, path);

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Contains(session.History, line =>
                line.StartsWith("Python error: SyntaxError: ", StringComparison.Ordinal)
            );
            Assert.Contains(session.History, line => line.EndsWith("(bad.py, line 1)", StringComparison.Ordinal));
        }

        [Fact]
        public void APathThatLoadCheckRefuses_RunsNothing_AndSaysWhy()
        {
            // LoadCheck gets the full path (D718); its answer is written as it is, and nothing of the file runs.
            CadCommandSession session = NewSession();
            string blocked = Script("blocked.py", "print('ran')");
            string allowed = Script("allowed.py", "print('allowed ran')");
            string asked = null;
            Host.LoadCheck = path =>
            {
                asked = path;
                return path.EndsWith("blocked.py", StringComparison.OrdinalIgnoreCase)
                    ? "blocked by the test policy"
                    : null;
            };

            CadPythonRunEventArgs refused = Run(session, blocked);
            CadPythonRunEventArgs ran = Run(session, allowed);

            Assert.Equal(CadPythonOutcome.Refused, refused.Outcome);
            Assert.Contains("blocked by the test policy", session.History);
            Assert.Equal(CadPythonOutcome.Completed, ran.Outcome);
            Assert.Equal(new[] { "allowed ran" }, Printed);
            Assert.Equal(Path.GetFullPath(allowed), asked, ignoreCase: true);
        }

        [Fact]
        public void ARunPostedWhileAnotherAsks_IsRefused_AndTheFirstGoesOn()
        {
            // One evaluation per process (D705 step 1). The window's owner runs posted work inside a wait, as a dispatcher
            // frame does; the action plays that. The refusal names the drawing the running script is in.
            CadCommandSession first = NewSession("first.dxf");
            CadCommandSession second = NewSession("second.dxf");
            string asking = Script("asking.py", UsesCad, "cad.get_point('Where')", "print('first done')");
            string other = Script("other.py", "print('second ran')");

            CadPythonRunEventArgs run = Run(first, asking, s =>
            {
                Host.RunFile(second, other);
                Owner.Drain();
                s.Feed("1,1");
            });

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Contains("A Python script is running in first.dxf: wait for it or press Esc.", second.History);
            Assert.Equal(new[] { "first done" }, Printed);
            Assert.Equal(CadPythonOutcome.Refused, Finished.First().Outcome);
        }

        [Fact]
        public void TheDocumentAndSession_AreThoseTheRunStartedIn()
        {
            CadCommandSession first = NewSession("first.dxf");
            CadCommandSession second = NewSession("second.dxf");

            RunLines(
                second,
                new[] { UsesCad, "print(cad.session().Document.Title)", "print(cad.session().Document.Title)" }
            );

            Assert.Equal(new[] { "second.dxf", "second.dxf" }, Printed);
            Assert.False(first.IsRunning);
        }

        [Fact]
        public void WriteWithTwoLines_PutsTwoLinesOnTheCommandLineAndInTheOutput()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "cad.write('one\\ntwo')" });

            Assert.Equal(new[] { "one", "two" }, Printed);
            Assert.Contains("one", session.History);
            Assert.Contains("two", session.History);
        }

        [Fact]
        public void TheSelectionOfTheDrawing_IsListedByItsHandles()
        {
            CadCommandSession session = NewSession();
            DxfLine line = AddLine(session.Document, 0);
            session.Document.Selection.Set(new[] { line.Handle });

            RunLines(
                session,
                new[] { UsesCad, "print(sorted(str(h) for h in cad.session().Document.Selection.Handles))" }
            );

            Assert.Equal(new[] { "['" + line.Handle + "']" }, Printed);
        }

        [Fact]
        public void TheAppsCopyOfGeometryhelper_IsTheOneImported()
        {
            // D702: <app folder>/python is first on sys.path, so the package beside the DLLs wins over a pip install. The app
            // folder is the output folder, not where xUnit shadow-copies the assemblies on .NET Framework (D734).
            CadCommandSession session = NewSession();
            string expected = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "python",
                "geometryhelper",
                "__init__.py"
            );

            RunLines(session, new[] { "import geometryhelper", "print(geometryhelper.__file__)" });

            Assert.Equal(new[] { expected }, Printed, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void TheVersionOfThePackage_IsTheVersionOfTheLibraryWithoutItsFourthPart()
        {
            // The app's copy has no dist-info to read the version from (D712): it takes the assembly's, 13.12.0 for 13.12.0.0.
            CadCommandSession session = NewSession();
            Version assembly = typeof(GeoPoint3).Assembly.GetName().Version;
            string expected = assembly.Major + "." + assembly.Minor + "." + assembly.Build;

            RunLines(session, new[] { "import geometryhelper", "print(geometryhelper.__version__)" });

            Assert.Equal(new[] { expected }, Printed);
        }

        [Fact]
        public void TheThreeAssemblies_AreInTheAppDomainOnceEach_AfterTheScriptImportedThem()
        {
            // The package adds only the assemblies the AppDomain lacks (P2 D604); inside the app none is lacking, so no second
            // copy of GeometryHelper.dll makes a second GeoPoint3.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[]
                {
                    "import geometryhelper",
                    "from GeometryHelper.Cad import CadDocument",
                    "from GeometryHelper.Geometry import GeoPoint3",
                }
            );

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            string[] names = { "GeometryHelper", "GeometryHelper.Cad.Remote", "GeometryHelper.Cad" };
            foreach (string name in names)
            {
                Assert.Single(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == name);
            }
        }
    }
}
