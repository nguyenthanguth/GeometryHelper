using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// PYRUN and PYLOAD typed at the command line, and the commands a loaded file defines with <c>@command</c> (D716,
    /// D718). The command names are made up for each test (H11ONE and so on) because the commands live as long as the
    /// process.
    /// </summary>
    public sealed class LoadCommandTests : PythonHostTestBase
    {
        public LoadCommandTests(PythonHostFixture fixture)
            : base(fixture) { }

        private string Tools(string name, params string[] commands)
        {
            // One function for each command: it writes "<name> ran".
            var lines = new List<string> { "from geometryhelper.cad import command, write" };

            foreach (string command in commands)
            {
                lines.Add("@command('" + command + "')");
                lines.Add("def run_" + command.ToLowerInvariant() + "():");
                lines.Add("    write('" + command + " ran')");
            }

            return Script(name, lines.ToArray());
        }

        [Fact]
        public void PyRunTyped_AsksForTheFileOnTheCommandLine_ThenRunsItAfterTheCommandHasEnded()
        {
            // FILEDIA is 0 in NewSession, so no dialog. The run is posted: when the script starts, PYRUN is over and the
            // session is at the Command prompt, which the script reads from its own session.
            CadCommandSession session = NewSession();
            string path = Script("typed.py", "import geometryhelper.cad as cad", "print(cad.session().IsRunning)");

            Assert.True(session.Feed("PYRUN"));
            Assert.StartsWith("Enter Python file name", session.PromptText, StringComparison.Ordinal);
            Assert.True(session.Feed(path));
            Assert.False(session.IsRunning);
            Assert.Empty(Printed);
            Owner.Session = session;
            Owner.Drain();

            Assert.Equal(new[] { "False" }, Printed);
            CadPythonRunEventArgs run = Assert.Single(Finished);
            Assert.Equal("PYRUN", run.Name);
            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
        }

        [Fact]
        public void PyLoadOfAFile_DefinesItsCommands_AndSaysSo()
        {
            CadCommandSession session = NewSession();
            string path = Tools("h11-tools.py", "H11ONE", "H11TWO");

            CadPythonRunEventArgs run = Load(session, path);

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal("PYLOAD", run.Name);
            Assert.Contains("h11-tools.py loaded: H11ONE, H11TWO.", session.History);
            Assert.Contains("H11ONE", Host.CommandNames);
            Assert.Contains("H11TWO", Host.CommandNames);
            Assert.Equal(Host.CommandNames.OrderBy(n => n, StringComparer.Ordinal), Host.CommandNames);
        }

        [Fact]
        public void ADefinedCommand_RunsItsFunction_AndEnterRepeatsIt()
        {
            CadCommandSession session = NewSession();
            Load(session, Tools("h11-repeat.py", "H11REPEAT"));
            Owner.Session = session;

            Assert.True(session.Feed("H11REPEAT"));
            Owner.Drain();
            session.Enter();
            Owner.Drain();

            Assert.Equal(2, session.History.Count(line => line == "H11REPEAT ran"));
            Assert.Equal("H11REPEAT", session.LastCommandName);
            Assert.Equal(new[] { "H11REPEAT", "H11REPEAT" }, Started.Skip(1).Select(run => run.Name));
        }

        [Fact]
        public void ADefinedCommand_IsInTheRegistryOfAnotherDrawingMadeAfterwards()
        {
            CadCommandSession first = NewSession("first.dxf");
            Load(first, Tools("h11-everywhere.py", "H11EVERY"));

            CadCommandSession second = NewSession("second.dxf");
            Owner.Session = second;
            second.Feed("H11EVERY");
            Owner.Drain();

            Assert.Contains("H11EVERY ran", second.History);
        }

        [Fact]
        public void LoadingTheFileAgain_ReplacesItsCommands_TheOldOnesAreGone()
        {
            CadCommandSession session = NewSession();
            string path = Tools("h11-reload.py", "H11A", "H11B");
            Load(session, path);

            Tools("h11-reload.py", "H11A", "H11C");
            Load(session, path);

            Assert.Contains("H11A", Host.CommandNames);
            Assert.Contains("H11C", Host.CommandNames);
            Assert.DoesNotContain("H11B", Host.CommandNames);
            Assert.Null(session.Registry.Find("H11B"));
            Assert.NotNull(session.Registry.Find("H11C"));
        }

        [Fact]
        public void ANameThatIsACommandAlready_IsRefused_AndTheRestOfTheFileIsKept()
        {
            CadCommandSession session = NewSession();
            string path = Script(
                "h11-clash.py",
                "from geometryhelper.cad import command, write",
                "@command('LINE')",
                "def not_line():",
                "    write('hijacked')",
                "@command('H11OK')",
                "def fine():",
                "    write('H11OK ran')"
            );

            CadPythonRunEventArgs run = Load(session, path);

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Contains("PYLOAD: LINE is a command already; h11-clash.py did not define it.", session.History);
            Assert.Contains("h11-clash.py loaded: H11OK.", session.History);
            Assert.DoesNotContain("LINE", Host.CommandNames);
            Assert.Contains("H11OK", Host.CommandNames);
            Assert.Equal("LINE", session.Registry.Find("LINE").Name);
        }

        [Fact]
        public void AFileThatFailsPartWay_KeepsNoCommandOfItsOwn_AndShowsTheError()
        {
            CadCommandSession session = NewSession();
            string path = Script(
                "h11-fails.py",
                "from geometryhelper.cad import command, write",
                "@command('H11GOOD')",
                "def good():",
                "    write('good ran')",
                "raise RuntimeError('boom')"
            );

            CadPythonRunEventArgs run = Load(session, path);

            Assert.Equal(CadPythonOutcome.Failed, run.Outcome);
            Assert.Contains("Python error: RuntimeError: boom (h11-fails.py, line 5)", session.History);
            Assert.DoesNotContain("H11GOOD", Host.CommandNames);
            Assert.Null(session.Registry.Find("H11GOOD"));
        }

        [Fact]
        public void ModuleLevelDrawing_IsOneStepNamedPyLoad()
        {
            CadCommandSession session = NewSession();
            string path = Script(
                "h11-draws.py",
                "import geometryhelper.cad as cad",
                "cad.cmd('CIRCLE', (0, 0), 5)",
                "cad.cmd('CIRCLE', (50, 0), 5)"
            );

            Load(session, path);

            CadDocument document = session.Document;
            Assert.Equal(2, document.EntityCount);
            Assert.Equal("PYLOAD", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ADefinedCommandThatDraws_IsOneStepNamedAfterTheCommand()
        {
            CadCommandSession session = NewSession();
            string path = Script(
                "h11-draw-command.py",
                "import geometryhelper.cad as cad",
                "@cad.command('H11DRAW')",
                "def draw():",
                "    cad.cmd('CIRCLE', (0, 0), 5)"
            );
            Load(session, path);
            Owner.Session = session;

            session.Feed("H11DRAW");
            Owner.Drain();

            CadDocument document = session.Document;
            Assert.Equal(1, document.EntityCount);
            Assert.Equal("H11DRAW", document.NextUndo.Name);
        }

        [Fact]
        public void ALoadCheckThatRefuses_LoadsNothing_AndSaysWhy()
        {
            CadCommandSession session = NewSession();
            string path = Tools("h11-refused.py", "H11NOPE");
            Host.LoadCheck = p => "loading is off in this test";

            CadPythonRunEventArgs run = Load(session, path);

            Assert.Equal(CadPythonOutcome.Refused, run.Outcome);
            Assert.Contains("loading is off in this test", session.History);
            Assert.DoesNotContain("H11NOPE", Host.CommandNames);
        }

        [Fact]
        public void APyLoadTypedAtTheCommandLine_AsksForTheFile_AndLoadsIt()
        {
            CadCommandSession session = NewSession();
            string path = Tools("h11-typed.py", "H11TYPED");

            Assert.True(session.Feed("PYLOAD"));
            Assert.StartsWith("Enter Python file name", session.PromptText, StringComparison.Ordinal);
            Assert.True(session.Feed(path));
            Owner.Session = session;
            Owner.Drain();

            Assert.Contains("H11TYPED", Host.CommandNames);
            Assert.Contains("h11-typed.py loaded: H11TYPED.", session.History);
        }
    }
}
