using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The names of W2 (D1651 to D1654): <c>@command</c> in place of <c>cad_command</c>, <c>cmd</c> in place of the feeder
    /// <c>command</c>, <c>PAUSE</c>, <c>get_entity</c> and <c>get_selection</c> giving <c>Entity</c> objects, <c>document()</c>
    /// and <c>selection()</c> gone, and <c>@on_unload</c>. The command names are made up for each test (H2ONE and so on) because
    /// the commands live as long as the process.
    /// </summary>
    public sealed class CommandRenameTests : PythonHostTestBase
    {
        public CommandRenameTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>Types a command, plays the answers at its prompts, and checks that each was asked for and none was left.</summary>
        private void Type(CadCommandSession session, string command, params Action<CadCommandSession>[] answers)
        {
            Owner.Session = session;
            Owner.Answer(answers);
            Assert.True(session.Feed(command));
            Owner.Drain();
            Assert.Empty(Owner.Unanswered);
            Assert.Equal(0, Owner.PendingAnswers);
        }

        /// <summary>The lines of the command line and the palette that begin with "Python error:", each once.</summary>
        private string[] ErrorLines(CadCommandSession session) =>
            Errors
                .Concat(session.History)
                .Where(line => line.StartsWith("Python error:", StringComparison.Ordinal))
                .Distinct()
                .ToArray();

        #region @command

        [Fact]
        public void ACommandMadeWithTheCommandDecorator_IsInTheRegistry_AndRunsItsFunction()
        {
            CadCommandSession session = NewSession();
            string path = Script(
                "h2-decorated.py",
                "from geometryhelper.cad import command, write",
                "@command('H2ONE', alias='H2O', description='Says it ran')",
                "def run_it():",
                "    write('H2ONE ran')"
            );

            Load(session, path);
            Type(session, "H2O");

            Assert.Contains("H2ONE", Host.CommandNames);
            Assert.NotNull(session.Registry.Find("H2ONE"));
            Assert.Contains("H2ONE ran", session.History);
        }

        [Fact]
        public void TheOldNames_AreGone_AndTheDecoratorTakesOnlyTheNameAsAPositional()
        {
            // cad_command, document and selection raise AttributeError; command is the decorator now, so the feeder's
            // command('CIRCLE', (0, 0), 5) is a TypeError, not a circle.
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "for name in ('cad_command', 'document', 'selection'):",
                    "    try:",
                    "        getattr(cad, name)",
                    "        print(name, 'is still there')",
                    "    except AttributeError:",
                    "        print(name, 'is gone')",
                    "try:",
                    "    cad.command('CIRCLE', (0, 0), 5)",
                    "except TypeError:",
                    "    print('command is the decorator')",
                }
            );

            Assert.Equal(
                new[] { "cad_command is gone", "document is gone", "selection is gone", "command is the decorator" },
                Printed
            );
            Assert.Equal(0, session.Document.EntityCount);
        }

        #endregion

        #region uses_selection

        private string Selecting(string name, string command, bool usesSelection) =>
            Script(
                name,
                "from geometryhelper.cad import command, get_selection, write",
                "@command('" + command + "'" + (usesSelection ? ", uses_selection=True" : string.Empty) + ")",
                "def work():",
                "    write('took ' + ','.join(sorted(e.handle for e in get_selection())))"
            );

        [Fact]
        public void ACommandWithUsesSelection_TakesWhatWasSelectedBeforeIt_WithoutAsking()
        {
            CadCommandSession session = NewSession();
            string first = AddLine(session.Document, 0).Handle;
            string second = AddLine(session.Document, 50).Handle;
            AddLine(session.Document, 100);
            session.Document.Selection.Set(new[] { first, second });
            Load(session, Selecting("h2-uses.py", "H2USES", true));

            Type(session, "H2USES");

            Assert.Empty(Owner.Asked);
            Assert.Contains(
                "took " + string.Join(",", new[] { first, second }.OrderBy(h => h, StringComparer.Ordinal)),
                session.History
            );
        }

        [Fact]
        public void ACommandWithUsesSelection_AsksWhenNothingWasSelected()
        {
            CadCommandSession session = NewSession();
            string first = AddLine(session.Document, 0).Handle;
            Load(session, Selecting("h2-asks.py", "H2ASKS", true));

            Type(session, "H2ASKS", Objects(first), EnterKey);

            Assert.Single(Owner.Asked);
            Assert.Contains("took " + first, session.History);
        }

        [Fact]
        public void ACommandWithoutUsesSelection_AsksEvenWhenObjectsWereSelected()
        {
            CadCommandSession session = NewSession();
            string first = AddLine(session.Document, 0).Handle;
            string second = AddLine(session.Document, 50).Handle;
            session.Document.Selection.Set(new[] { first });
            Load(session, Selecting("h2-plain.py", "H2PLAIN", false));

            Type(session, "H2PLAIN", Objects(second), EnterKey);

            Assert.Single(Owner.Asked);
            Assert.Contains("took " + second, session.History);
        }

        #endregion

        #region cmd and PAUSE

        [Fact]
        public void Cmd_FeedsACommandAsTheFeederCommandDid()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                new[] { UsesCad, "cad.cmd('CIRCLE', (10, 20), 50)", "cad.cmd('_LINE', (0, 0), (100, 0), '')" }
            );

            Assert.Equal(2, session.Document.EntityCount);
            DxfCircle circle = Assert.Single(All<DxfCircle>(session.Document));
            Assert.Equal(50, circle.Radius, 6);
            Assert.Equal(10, circle.Center.X, 6);
            Assert.Equal(20, circle.Center.Y, 6);
            DxfLine line = Assert.Single(All<DxfLine>(session.Document));
            Assert.Equal(100, line.EndPoint.X, 6);
        }

        [Fact]
        public void PauseIsOneBackslash_LikeAutoLispsPause()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "print(repr(cad.PAUSE))" });

            Assert.Equal(new[] { @"'\\'" }, Printed);
        }

        [Fact]
        public void APauseInAFedCommand_HandsThatOnePromptToTheUser()
        {
            // CIRCLE asks its centre, which the user picks at (10, 20); the radius 5 is fed.
            CadCommandSession session = NewSession();

            RunLines(session, new[] { UsesCad, "cad.cmd('CIRCLE', cad.PAUSE, 5)" }, Picked(10, 20));

            DxfCircle circle = Assert.Single(All<DxfCircle>(session.Document));
            Assert.Equal(5, circle.Radius, 6);
            Assert.Equal(10, circle.Center.X, 6);
            Assert.Equal(20, circle.Center.Y, 6);
            Assert.Single(Owner.Asked);
        }

        [Fact]
        public void EscAtAPause_RaisesCancelled_AndTheRunIsCancelled()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "try:",
                    "    cad.cmd('CIRCLE', cad.PAUSE, 5)",
                    "except cad.Cancelled:",
                    "    print('cancelled')",
                    "    raise",
                },
                EscKey
            );

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Equal(new[] { "cancelled" }, Printed);
            Assert.Equal(0, session.Document.EntityCount);
        }

        #endregion

        #region The prompts give Entity objects

        [Fact]
        public void GetEntity_GivesAnEntityOfTheDrawing_OrNoneOnEnter()
        {
            CadCommandSession session = NewSession();
            string handle = AddLine(session.Document, 0).Handle;

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "e = cad.get_entity('Pick a line')",
                    "print(type(e).__name__, e.handle, e.dxf_type)",
                    "print(cad.get_entity('Pick another'))",
                },
                Objects(handle),
                EnterKey
            );

            Assert.Equal(new[] { "Entity " + handle + " LINE", "None" }, Printed);
        }

        [Fact]
        public void GetSelection_GivesTheEntitiesPickedUntilEnter()
        {
            CadCommandSession session = NewSession();
            string first = AddLine(session.Document, 0).Handle;
            string second = AddLine(session.Document, 50).Handle;

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "picked = cad.get_selection()",
                    "print(sorted(type(e).__name__ for e in picked))",
                    "print(sorted(e.handle for e in picked))",
                },
                Objects(first, second),
                EnterKey
            );

            string[] sorted = new[] { first, second }.OrderBy(h => h, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "['Entity', 'Entity']", "['" + sorted[0] + "', '" + sorted[1] + "']" }, Printed);
        }

        [Fact]
        public void TheDrawingOfARun_IsReachedByActiveDrawing_AndItsSelectionByItsSelectionMethod()
        {
            CadCommandSession session = NewSession();
            string first = AddLine(session.Document, 0).Handle;
            AddLine(session.Document, 50);
            session.Document.Selection.Set(new[] { first });

            RunLines(
                session,
                new[]
                {
                    UsesCad,
                    "d = cad.active_drawing()",
                    "print(d.net is cad.session().Document)",
                    "print([e.handle for e in d.selection()])",
                }
            );

            Assert.Equal(new[] { "True", "['" + first + "']" }, Printed);
        }

        #endregion

        #region @on_unload

        private string Hooked(string name, string marker, params string[] hook) =>
            Script(
                name,
                new[]
                {
                    "from geometryhelper.cad import on_unload",
                    "MARK = " + Raw(marker),
                    "def note(text):",
                    "    with open(MARK, 'a') as f:",
                    "        f.write(text + '\\n')",
                    "note('loaded')",
                    "@on_unload",
                    "def bye():",
                }
                    .Concat(hook)
                    .ToArray()
            );

        private static string[] Marks(string marker) =>
            File.ReadAllLines(marker).OrderBy(line => line, StringComparer.Ordinal).ToArray();

        [Fact]
        public void AnUnloadHook_RunsWhenTheFileIsUnloaded()
        {
            CadCommandSession session = NewSession();
            string marker = Path.Combine(Folder, "unload.txt");
            string path = Hooked("h2-bye.py", marker, "    note('unloaded')");
            Load(session, path);
            Assert.Equal(new[] { "loaded" }, Marks(marker));

            Assert.True(Host.UnloadFile(path));

            Assert.Equal(new[] { "loaded", "unloaded" }, Marks(marker));
            Assert.DoesNotContain(Host.LoadedFiles, f => f.EndsWith("h2-bye.py", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void AnUnloadHook_RunsWhenTheFileIsLoadedAgain_AndAgainWhenTheNewCopyIsUnloaded()
        {
            // Load, load again, unload: the hook ran for the first copy at the reload and for the second at the unload.
            CadCommandSession session = NewSession();
            string marker = Path.Combine(Folder, "reload.txt");
            string path = Hooked("h2-again.py", marker, "    note('unloaded')");
            Load(session, path);

            Load(session, path);
            Assert.Equal(new[] { "loaded", "loaded", "unloaded" }, Marks(marker));

            Assert.True(Host.UnloadFile(path));
            Assert.Equal(new[] { "loaded", "loaded", "unloaded", "unloaded" }, Marks(marker));
        }

        [Fact]
        public void AnErrorInAnUnloadHook_IsOneLine_AndTheUnloadGoesOn()
        {
            CadCommandSession session = NewSession();
            string marker = Path.Combine(Folder, "boom.txt");
            string path = Hooked("h2-boom.py", marker, "    raise RuntimeError('boom')");
            Load(session, path);

            bool unloaded = Host.UnloadFile(path);

            Assert.True(unloaded);
            string line = Assert.Single(ErrorLines(session));
            Assert.Contains("RuntimeError: boom", line);
            Assert.Contains("h2-boom.py", line);
            Assert.Empty(Host.CommandsOf(path));
        }

        #endregion
    }
}
