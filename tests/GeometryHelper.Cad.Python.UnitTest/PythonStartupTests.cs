using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// Which .py files load at start (D1608, D1612): the top-level files of each Pythons folder, then the Python Startup
    /// Suite, each once. Touches no Python, so it runs with no engine.
    /// </summary>
    public sealed class PythonStartupFilesTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "gh-pystart-" + Guid.NewGuid().ToString("N"));

        public PythonStartupFilesTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
        }

        /// <summary>A folder of this test, made, under the test's root.</summary>
        private string Folder(params string[] parts)
        {
            string path = Path.Combine(new[] { _root }.Concat(parts).ToArray());
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>A file of one line in a folder; its full path.</summary>
        private static string File(string folder, string name) => WriteFile(folder, name, "pass\n");

        [Fact]
        public void OnlyThePyFilesDirectlyInAFolder_Load_AndThoseOfItsSubfoldersAreLeftOut()
        {
            // A helper module of a package below Pythons must not run as a startup file (D1608).
            string pythons = Folder("Pythons");
            string top = File(pythons, "tools.py");
            File(Folder("Pythons", "helpers"), "inner.py");

            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(new[] { pythons }, Array.Empty<string>());

            Assert.Equal(new[] { top }, files, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void TheFilesOfAFolder_AreInTheOrderOfTheirNames()
        {
            // Written b, A, a2; the order is A.py, a2.py, b.py ("a.py" before "a2.py" because '.' sorts before '2').
            string pythons = Folder("Pythons");
            string b = File(pythons, "b.py");
            string a = File(pythons, "A.py");
            string a2 = File(pythons, "a2.py");

            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(new[] { pythons }, Array.Empty<string>());

            Assert.Equal(new[] { a, a2, b }, files, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void AnUpperCaseInitialThatOrdinalSortsFirst_DoesNotComeBeforeAnEarlierLetter()
        {
            // By ordinal "Zed.py" ('Z' is 0x5A) comes before "alpha.py" ('a' is 0x61); ignoring case alpha comes first.
            string pythons = Folder("Pythons");
            string zed = File(pythons, "Zed.py");
            string alpha = File(pythons, "alpha.py");

            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(new[] { pythons }, Array.Empty<string>());

            Assert.Equal(new[] { alpha, zed }, files, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void LspPycAndTextFiles_AreLeftOut_NoMatterTheirNames()
        {
            string pythons = Folder("Pythons");
            string py = File(pythons, "tools.py");
            File(pythons, "tools.lsp");
            File(pythons, "tools.pyc");
            File(pythons, "tools.py.txt");
            File(pythons, "README.txt");

            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(new[] { pythons }, Array.Empty<string>());

            Assert.Equal(new[] { py }, files, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void TheFolderFiles_ComeBeforeTheSuite_AndTheSuiteKeepsItsOwnOrder()
        {
            // The suite names z.py then a.py, the reverse of their names: its order is the user's, not sorted.
            string pythons = Folder("Pythons");
            string m = File(pythons, "m.py");
            string elsewhere = Folder("elsewhere");
            string z = File(elsewhere, "z.py");
            string a = File(elsewhere, "a.py");

            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(new[] { pythons }, new[] { z, a });

            Assert.Equal(new[] { m, z, a }, files, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void AFileInTheFolderAndInTheSuite_LoadsOnce_WhateverWayItsPathIsWritten()
        {
            // The same file as the suite spells it: upper-case, and through "sub\.." (D1612: paths compared full, ignoring case).
            string pythons = Folder("Pythons");
            string tools = File(pythons, "tools.py");
            string spelled = Path.Combine(pythons, "sub", "..", "TOOLS.PY");
            string other = File(Folder("elsewhere"), "other.py");

            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(new[] { pythons }, new[] { spelled, other });

            Assert.Equal(new[] { tools, other }, files, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void ASuiteThatNamesAFileTwice_LoadsItOnce_AtItsFirstPlace()
        {
            string elsewhere = Folder("elsewhere");
            string one = File(elsewhere, "one.py");
            string two = File(elsewhere, "two.py");

            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(
                Array.Empty<string>(),
                new[] { one, two, one.ToUpperInvariant() }
            );

            Assert.Equal(new[] { one, two }, files, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void AFolderThatDoesNotExist_IsPassedOver_AndTheOthersStillCount()
        {
            string pythons = Folder("Pythons");
            string tools = File(pythons, "tools.py");
            string missing = Path.Combine(_root, "nowhere");

            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(
                new[] { missing, pythons },
                Array.Empty<string>()
            );

            Assert.Equal(new[] { tools }, files, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void NoFolderAndNoSuite_LoadNothing()
        {
            Assert.Empty(CadPythonStartup.FilesToLoad(Array.Empty<string>(), Array.Empty<string>()));
        }
    }

    /// <summary>
    /// What happens when the files are loaded at start (D1612 to D1614, D1623): the commands of the files are in the
    /// registry, a second call loads nothing, a failing first file does not stop the second, module-level drawing is one
    /// undo step named PYLOAD, and the host's own API for loading and unloading a file. Command names are made up for each
    /// test (PYW1...) because commands live as long as the process; the files are written in each test's own folder.
    /// </summary>
    public sealed class PythonStartupTests : PythonHostTestBase
    {
        public PythonStartupTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>A file of one function for each command, which writes "name ran".</summary>
        private string Tools(string file, params string[] commands)
        {
            var lines = new List<string> { "from geometryhelper.cad import cad_command, write" };

            foreach (string command in commands)
            {
                lines.Add("@cad_command('" + command + "')");
                lines.Add("def run_" + command.ToLowerInvariant() + "():");
                lines.Add("    write('" + command + " ran')");
            }

            return Script(file, lines.ToArray());
        }

        /// <summary>Calls the host's own LoadFile as the window does, plays what it posted, and gives what it returned.</summary>
        private bool LoadFile(CadCommandSession session, string path)
        {
            Owner.Session = session;
            bool loaded = Host.LoadFile(session, path);
            Owner.Drain();
            Assert.Empty(Owner.Unanswered);
            return loaded;
        }

        /// <summary>Runs the startup over the files, plays what it posted, and gives what Run returned.</summary>
        private bool Startup(CadPythonStartup startup, CadCommandSession session, params string[] files)
        {
            Owner.Session = session;
            bool started = startup.Run(session, files);
            Owner.Drain();
            Assert.Empty(Owner.Unanswered);
            return started;
        }

        [Fact]
        public void TwoStartupFiles_AreLoaded_AndTheirCommandsAreInTheSessionsRegistry()
        {
            CadCommandSession session = NewSession();
            string one = Tools("w1-one.py", "PYW1ONE");
            string two = Tools("w1-two.py", "PYW1TWO");

            bool started = Startup(new CadPythonStartup(Host), session, one, two);

            Assert.True(started);
            Assert.NotNull(session.Registry.Find("PYW1ONE"));
            Assert.NotNull(session.Registry.Find("PYW1TWO"));
            Assert.Contains("w1-one.py loaded: PYW1ONE.", session.History);
            Assert.Contains("w1-two.py loaded: PYW1TWO.", session.History);
            Assert.Equal(new[] { "PYLOAD", "PYLOAD" }, Finished.Select(run => run.Name));
            Assert.Equal(
                new[] { CadPythonOutcome.Completed, CadPythonOutcome.Completed },
                Finished.Select(run => run.Outcome)
            );
        }

        [Fact]
        public void TheFilesLoadInTheOrderGiven()
        {
            // w1-z.py is given first and w1-a.py second: they load in that order, not by name.
            CadCommandSession session = NewSession();
            string z = Tools("w1-z.py", "PYW1ZORDER");
            string a = Tools("w1-a.py", "PYW1AORDER");

            Startup(new CadPythonStartup(Host), session, z, a);

            List<string> loaded = session.History.Where(l => l.Contains(" loaded: PYW1")).ToList();
            Assert.Equal(new[] { "w1-z.py loaded: PYW1ZORDER.", "w1-a.py loaded: PYW1AORDER." }, loaded);
        }

        [Fact]
        public void ASecondRunOfTheSameStartup_LoadsNothingAgain_EvenOfAFileChangedMeanwhile()
        {
            // The first run loads PYW1FIRST. The file then defines PYW1LATER instead; a second Run over the same files must
            // not load it (D1612: once), so PYW1LATER stays unknown and no run was made.
            CadCommandSession session = NewSession();
            string path = Tools("w1-once.py", "PYW1FIRST");
            var startup = new CadPythonStartup(Host);
            Startup(startup, session, path);
            int runs = Finished.Count;
            Tools("w1-once.py", "PYW1LATER");

            Startup(startup, session, path);

            Assert.Equal(runs, Finished.Count);
            Assert.NotNull(session.Registry.Find("PYW1FIRST"));
            Assert.Null(session.Registry.Find("PYW1LATER"));
        }

        [Fact]
        public void AFailingFirstFile_WritesOneErrorLine_AndTheSecondFileStillLoads()
        {
            CadCommandSession session = NewSession();
            string bad = Script(
                "w1-bad.py",
                "from geometryhelper.cad import cad_command",
                "@cad_command('PYW1BAD')",
                "def bad():",
                "    pass",
                "raise RuntimeError('w1 startup boom')"
            );
            string good = Tools("w1-good.py", "PYW1GOOD");

            Startup(new CadPythonStartup(Host), session, bad, good);

            // One line for the failure, as PYLOAD writes it: the exception and where it was raised.
            string error = Assert.Single(session.History, l => l.StartsWith("Python error:", StringComparison.Ordinal));
            Assert.Contains("RuntimeError: w1 startup boom", error, StringComparison.Ordinal);
            Assert.Contains("w1-bad.py", error, StringComparison.Ordinal);
            Assert.Null(session.Registry.Find("PYW1BAD"));
            Assert.NotNull(session.Registry.Find("PYW1GOOD"));
            Assert.Contains("w1-good.py loaded: PYW1GOOD.", session.History);
            Assert.Equal(
                new[] { CadPythonOutcome.Failed, CadPythonOutcome.Completed },
                Finished.Select(run => run.Outcome)
            );
        }

        [Fact]
        public void ModuleLevelDrawingOfAStartupFile_IsOneUndoStepNamedPyLoad_InThatDrawing()
        {
            // Two circles drawn at module level: one U takes both away, and the step is named PYLOAD.
            CadCommandSession session = NewSession();
            string path = Script(
                "w1-draws.py",
                "import geometryhelper.cad as cad",
                "cad.command('CIRCLE', (0, 0), 5)",
                "cad.command('CIRCLE', (50, 0), 5)"
            );

            Startup(new CadPythonStartup(Host), session, path);

            CadDocument document = session.Document;
            Assert.Equal(2, document.EntityCount);
            Assert.Equal("PYLOAD", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void WithNoUsablePython_RunReturnsFalse_AndSaysWhy_AndNothingStarts()
        {
            // A host on a python.exe that is not there never starts the engine (D1614): the line carries the finder's problem
            // word for word. It may reach the command line or the host's Output; either shows it to the user.
            string missing = Path.Combine(Folder, "nowhere", "python.exe");
            CadPythonEnvironment environment = CadPythonEnvironment.Find(missing);
            var owner = new TestPythonOwner();
            CadCommandSession session = NewSession();
            owner.Session = session;
            var told = new List<string>();

            using (var host = new CadPythonHost(environment, owner))
            {
                host.Output += (sender, e) => told.Add(e.Text);
                string file = Tools("w1-nopython.py", "PYW1NOPY");

                bool started = new CadPythonStartup(host).Run(session, new[] { file });
                owner.Drain();

                string line = "Python files were not loaded: " + environment.Problem;
                Assert.False(started);
                Assert.False(host.IsStarted);
                Assert.Contains(line, session.History.Concat(told));
                Assert.InRange(session.History.Count(l => l == line), 0, 1);
            }
        }

        [Fact]
        public void LoadFile_OfAFileThatLoads_ReturnsTrue_AndCommandsOfListsItsCommandsInTheOrderItDefinedThem()
        {
            // Defined Z before A: the list is in definition order, not sorted (the "loaded:" line sorts, the API does not).
            CadCommandSession session = NewSession();
            string path = Tools("w1-api.py", "PYW1ZED", "PYW1ALPHA");

            bool loaded = LoadFile(session, path);

            Assert.True(loaded);
            Assert.Equal(new[] { "PYW1ZED", "PYW1ALPHA" }, Host.CommandsOf(path));
        }

        [Fact]
        public void LoadFile_OfAFileThatFails_ReturnsFalse_AndItIsNotAmongTheLoadedFiles()
        {
            CadCommandSession session = NewSession();
            string path = Script(
                "w1-api-bad.py",
                "from geometryhelper.cad import cad_command",
                "@cad_command('PYW1APIBAD')",
                "def bad():",
                "    pass",
                "raise RuntimeError('w1 api boom')"
            );

            bool loaded = LoadFile(session, path);

            Assert.False(loaded);
            Assert.Empty(Host.CommandsOf(path));
            Assert.DoesNotContain(Path.GetFullPath(path), Host.LoadedFiles, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void CommandsOf_AFileNeverLoaded_IsEmpty()
        {
            Assert.Empty(Host.CommandsOf(Path.Combine(Folder, "never.py")));
        }

        [Fact]
        public void TheLoadedFiles_AreInTheOrderTheyFirstLoaded_AndLoadingOneAgainKeepsItsPlace()
        {
            // b, a, then b again: the list is b, a, not a, b. The host is shared by the whole process, so only the files of
            // this test's folder are looked at.
            CadCommandSession session = NewSession();
            string b = Tools("w1-order-b.py", "PYW1ORDB");
            string a = Tools("w1-order-a.py", "PYW1ORDA");
            LoadFile(session, b);
            LoadFile(session, a);

            LoadFile(session, b);

            string[] mine = Host
                .LoadedFiles.Where(f => f.StartsWith(Path.GetFullPath(Folder), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.Equal(new[] { Path.GetFullPath(b), Path.GetFullPath(a) }, mine, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void UnloadFile_TakesTheCommandsAndTheAliases_OutOfEveryRegistry_AndAnotherFileKeepsItsOwn()
        {
            // Two drawings' registries both hold the commands; the alias PW1U names the first. A second file's command stays.
            CadCommandSession first = NewSession("first.dxf");
            CadCommandSession second = NewSession("second.dxf");
            string path = Script(
                "w1-unload.py",
                "from geometryhelper.cad import cad_command",
                "@cad_command('PYW1UNLOAD', alias='PW1U')",
                "def one():",
                "    pass",
                "@cad_command('PYW1UNLOADB')",
                "def two():",
                "    pass"
            );
            string other = Tools("w1-keep.py", "PYW1KEEP");
            LoadFile(first, path);
            LoadFile(first, other);
            Assert.NotNull(second.Registry.Find("PYW1UNLOAD"));
            Assert.True(second.Registry.Shortcuts.Aliases.ContainsKey("PW1U"));

            bool unloaded = Host.UnloadFile(path);

            Assert.True(unloaded);

            foreach (CadCommandSession session in new[] { first, second })
            {
                Assert.Null(session.Registry.Find("PYW1UNLOAD"));
                Assert.Null(session.Registry.Find("PYW1UNLOADB"));
                Assert.False(session.Registry.Shortcuts.Aliases.ContainsKey("PW1U"));
                Assert.NotNull(session.Registry.Find("PYW1KEEP"));
            }

            Assert.Empty(Host.CommandsOf(path));
            Assert.DoesNotContain(Path.GetFullPath(path), Host.LoadedFiles, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(new[] { "PYW1KEEP" }, Host.CommandsOf(other));
        }

        [Fact]
        public void UnloadFile_ASecondTime_ReturnsFalse()
        {
            CadCommandSession session = NewSession();
            string path = Tools("w1-twice.py", "PYW1TWICE");
            LoadFile(session, path);
            Assert.True(Host.UnloadFile(path));

            Assert.False(Host.UnloadFile(path));
        }

        [Fact]
        public void UnloadFile_OfAFileNeverLoaded_ReturnsFalse()
        {
            Assert.False(Host.UnloadFile(Path.Combine(Folder, "never.py")));
        }

        [Fact]
        public void ReloadingAFile_ReplacesItsCommands_TheOldOnesAreGone_AndItKeepsOneEntryInTheLoadedFiles()
        {
            CadCommandSession session = NewSession();
            string path = Tools("w1-reload.py", "PYW1RA", "PYW1RB");
            LoadFile(session, path);
            Tools("w1-reload.py", "PYW1RA", "PYW1RC");

            bool loaded = LoadFile(session, path);

            Assert.True(loaded);
            Assert.Equal(new[] { "PYW1RA", "PYW1RC" }, Host.CommandsOf(path));
            Assert.Null(session.Registry.Find("PYW1RB"));
            Assert.NotNull(session.Registry.Find("PYW1RC"));
            Assert.Single(Host.LoadedFiles, f =>
                string.Equals(f, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
