using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The Drawing API inside the app (D1655 to D1659, D1662, D1642): the scenarios of <c>tests/python/drawing_scenarios.py</c>,
    /// the list the headless and the remote runners take too, run on <c>active_drawing()</c> of a run; what a commit and the
    /// end of a run make of the undo steps of the drawing; and the drawings of the window's host (<see cref="ICadRemoteHost"/>),
    /// which the test gives a fake of. The steps are read from <see cref="CadDocument.NextUndo"/> and taken back with
    /// <see cref="CadDocument.Undo"/>, which the scripts cannot do from inside a run.
    /// </summary>
    public sealed class DrawingApiTests : PythonHostTestBase
    {
        public DrawingApiTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>The folder of <c>drawing_scenarios.py</c>, found above the test assembly.</summary>
        private static string ScenariosFolder()
        {
            string folder = AppContext.BaseDirectory;

            while (folder != null && !File.Exists(Path.Combine(folder, "tests", "python", "drawing_scenarios.py")))
            {
                folder = Path.GetDirectoryName(folder);
            }

            Assert.NotNull(folder);
            return Path.Combine(folder, "tests", "python");
        }

        /// <summary>The lines that make the scenarios importable in the app.</summary>
        private static string[] Importing(string folder) =>
            new[] { "import sys", "sys.path.insert(0, " + Raw(folder) + ")", "import drawing_scenarios" };

        /// <summary>A session over the file at <paramref name="path"/>, as NewSession makes one over a blank drawing.</summary>
        private CadCommandSession OpenedSession(string path)
        {
            var session = new CadCommandSession(CadDocument.Open(path));
            session.Remembered["filedia"] = 0.0;
            Host.AddCommandsTo(session.Registry);
            return session;
        }

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

        private static string[] OnTheActiveDrawing(params string[] lines) =>
            new[] { UsesCad, "d = cad.active_drawing()" }.Concat(lines).ToArray();

        #region The scenarios

        [Fact]
        public void EveryScenarioOfTheSharedList_GivesInTheAppTheResultItGivesOnTheOtherBackends()
        {
            // The scenarios that need a drawing of a file get the file the list writes. The ones that undo are left out here and
            // taken by the tests below, since a script cannot take a step back from inside its own run.
            string folder = ScenariosFolder();
            RunLines(
                NewSession(),
                Importing(folder)
                    .Concat(
                        new[]
                        {
                            "for s in drawing_scenarios.SCENARIOS:",
                            "    print(s.name + '|' + s.drawing + '|' + str(s.needs_undo))",
                        }
                    )
                    .ToArray()
            );
            List<string[]> listed = Printed.Select(line => line.Split('|')).ToList();
            Assert.NotEmpty(listed);
            var failures = new List<string>();

            foreach (string[] item in listed.Where(item => item[2] == "False"))
            {
                CadCommandSession session;

                if (item[1] == "file")
                {
                    string path = Path.Combine(Folder, item[0] + ".dxf");
                    RunLines(
                        NewSession(),
                        Importing(folder).Concat(new[] { "drawing_scenarios.write_file(" + Raw(path) + ")" }).ToArray()
                    );
                    session = OpenedSession(path);
                }
                else
                {
                    session = NewSession(item[0] + ".dxf");
                }

                CadPythonRunEventArgs run = RunLines(
                    session,
                    Importing(folder)
                        .Concat(
                            new[]
                            {
                                "from geometryhelper.cad import active_drawing",
                                "drawing_scenarios.check(drawing_scenarios.named('" + item[0] + "'), active_drawing())",
                            }
                        )
                        .ToArray()
                );

                if (run.Outcome != CadPythonOutcome.Completed)
                {
                    failures.Add(item[0] + ": " + run.Error);
                }
            }

            Assert.Empty(failures);
        }

        #endregion

        #region Undo steps

        [Fact]
        public void AnAddedLayerALineAndACircleCommittedOnce_AreOneStepOfTheRun_ThatOneUndoTakesBack()
        {
            // The layer and the two entities are one commit: the run's own step holds all three, so one Undo leaves no step.
            CadCommandSession session = NewSession();

            RunLines(
                session,
                OnTheActiveDrawing(
                    "d.layers.add('Steel')",
                    "d.add_line((0, 0), (10, 0))",
                    "d.add_circle((0, 0), 5)",
                    "d.commit()"
                )
            );

            CadDocument document = session.Document;
            Assert.Equal(2, document.EntityCount);
            Assert.Equal("PYRUN", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TwoCommitsOfOneRun_AreStillOneStep_BecauseTheRunIsOne()
        {
            CadCommandSession session = NewSession();

            RunLines(
                session,
                OnTheActiveDrawing("d.add_line((0, 0), (10, 0))", "d.commit()", "d.add_circle((0, 0), 5)", "d.commit()")
            );

            CadDocument document = session.Document;
            Assert.Equal(2, document.EntityCount);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ChangesQueuedAndNeverCommitted_AreCommittedWhenTheRunEnds_InsideItsStep()
        {
            CadCommandSession session = NewSession();

            RunLines(session, OnTheActiveDrawing("d.add_line((0, 0), (10, 0))", "d.add_circle((0, 0), 5)"));

            CadDocument document = session.Document;
            Assert.Equal(2, document.EntityCount);
            Assert.Equal("PYRUN", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ChangesQueuedByACommand_AreCommittedWhenItEnds_InAStepNamedAfterIt()
        {
            CadCommandSession session = NewSession();
            string path = Script(
                "h2-draws.py",
                "import geometryhelper.cad as cad",
                "@cad.command('H2DRAW')",
                "def draw():",
                "    d = cad.active_drawing()",
                "    d.add_line((0, 0), (10, 0))",
                "    d.add_circle((0, 0), 5)"
            );
            Load(session, path);

            Type(session, "H2DRAW");

            CadDocument document = session.Document;
            Assert.Equal(2, document.EntityCount);
            Assert.Equal("H2DRAW", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        #endregion

        #region The drawings of the window

        [Fact]
        public void WithNoDocumentHost_DocumentsListsOnlyTheDrawingOfTheRun()
        {
            CadCommandSession session = NewSession("only.dxf");

            RunLines(
                session,
                new[] { UsesCad, "print([d.name for d in cad.documents()])", "print(len(cad.documents()))" }
            );

            Assert.Null(Host.DocumentHost);
            Assert.Equal(new[] { "['only.dxf']", "1" }, Printed);
        }

        [Fact]
        public void WithADocumentHost_DocumentsListsItsDrawingsInItsOrder_AndFindsOneByName()
        {
            CadCommandSession session = NewSession("a.dxf");
            var fake = new FakeDocumentHost(session.Document, Blank("b.dxf"));

            WithHost(fake, () =>
                RunLines(
                    session,
                    new[]
                    {
                        UsesCad,
                        "print([d.name for d in cad.documents()])",
                        "print([d.is_active for d in cad.documents()])",
                        "print(cad.documents()['b.dxf'].name)",
                        "try:",
                        "    cad.documents()['nope.dxf']",
                        "except KeyError as e:",
                        "    print('nope.dxf' in str(e))",
                    }
                )
            );

            Assert.Equal(new[] { "['a.dxf', 'b.dxf']", "[True, False]", "b.dxf", "True" }, Printed);
        }

        [Fact]
        public void ACommitToASecondDrawing_MakesOneStepThere_NamedAfterTheRun_AndLeavesTheRunsDrawingAlone()
        {
            CadCommandSession session = NewSession("a.dxf");
            CadDocument second = Blank("b.dxf");
            var fake = new FakeDocumentHost(session.Document, second);

            WithHost(fake, () =>
                RunLines(
                    session,
                    new[]
                    {
                        UsesCad,
                        "other = cad.documents()['b.dxf']",
                        "other.add_line((0, 0), (10, 0))",
                        "other.add_circle((0, 0), 5)",
                        "other.commit()",
                    }
                )
            );

            Assert.Equal(2, second.EntityCount);
            Assert.Equal("PYRUN", second.NextUndo.Name);
            Assert.Equal(0, session.Document.EntityCount);
            Assert.False(session.Document.CanUndo);
            Assert.True(second.Undo());
            Assert.Equal(0, second.EntityCount);
            Assert.False(second.CanUndo);
        }

        [Fact]
        public void OpenDrawingAndNewDrawing_GoThroughTheDocumentHost_AndGiveTheDrawingsItMade()
        {
            CadCommandSession session = NewSession("a.dxf");
            var fake = new FakeDocumentHost(session.Document);
            string plan = Path.Combine(Folder, "plan.dxf");

            WithHost(fake, () =>
                RunLines(
                    session,
                    new[]
                    {
                        UsesCad,
                        "print(cad.open_drawing(" + Raw(plan) + ").name)",
                        "print(cad.new_drawing().name)",
                        "print([d.name for d in cad.documents()])",
                    }
                )
            );

            Assert.Equal(new[] { "plan.dxf", "Drawing1.dxf", "['a.dxf', 'plan.dxf', 'Drawing1.dxf']" }, Printed);
            Assert.Equal(new[] { plan }, fake.Opened);
            Assert.Equal(1, fake.NewCount);
        }

        /// <summary>Gives the host the fake for the length of <paramref name="body"/>: the host is shared by every test of the process.</summary>
        private void WithHost(FakeDocumentHost fake, Action body)
        {
            Host.DocumentHost = fake;

            try
            {
                body();
            }
            finally
            {
                Host.DocumentHost = null;
            }
        }

        /// <summary>
        /// The window's host with no window: the drawings it was given in the order it was given them, the first one active,
        /// each with a session of its own; <see cref="Open"/> makes a blank drawing of the file's name and notes the path,
        /// <see cref="New"/> makes <c>Drawing&lt;n&gt;.dxf</c>.
        /// </summary>
        private sealed class FakeDocumentHost : ICadRemoteHost
        {
            private readonly List<CadDocument> _documents;
            private readonly Dictionary<CadDocument, CadCommandSession> _sessions =
                new Dictionary<CadDocument, CadCommandSession>();

            public FakeDocumentHost(params CadDocument[] documents)
            {
                _documents = new List<CadDocument>(documents);
                ActiveDocument = documents.FirstOrDefault();

                foreach (CadDocument document in documents)
                {
                    _sessions[document] = new CadCommandSession(document);
                }
            }

            public List<string> Opened { get; } = new List<string>();

            public int NewCount { get; private set; }

            public string Title => "fake";

            public IReadOnlyList<CadDocument> Documents => _documents;

            public CadDocument ActiveDocument { get; private set; }

            public CadDocument Open(string path)
            {
                Opened.Add(path);
                return Add(Path.GetFileName(path));
            }

            public CadDocument New(string template) => Add("Drawing" + (++NewCount) + ".dxf");

            public void Activate(CadDocument document) => ActiveDocument = document;

            public CadCommandSession SessionOf(CadDocument document) => _sessions[document];

            public void Type(CadDocument document, string line) => _sessions[document].Feed(line);

            public void Save(CadDocument document) => document.Save();

            public void SaveAs(CadDocument document, string path, string version) =>
                document.SaveAs(path, version, null);

            public void Close(CadDocument document)
            {
                _documents.Remove(document);
                _sessions.Remove(document);
            }

            private CadDocument Add(string name)
            {
                CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), name);
                _documents.Add(document);
                _sessions[document] = new CadCommandSession(document);
                ActiveDocument = document;
                return document;
            }
        }

        #endregion
    }
}
