using System;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Python in the window (D720): with the app's Python off nothing of it is in the commands or the window; with it on, every
    /// drawing has PYRUN, PYLOAD, PYTHON and PYTHONCLOSE and the palette follows the drawing shown. The Python these tests name
    /// is a folder that does not exist, so the finder's problem is the one it words for a setting that does not work and no
    /// engine can start (Python starts once per process: the one test class that runs it is
    /// <see cref="CadPythonWindowOwnerRunTests"/>).
    /// </summary>
    public sealed class MainViewModelPythonTests : IDisposable
    {
        private static readonly string[] Names = { "PYTHON", "PYTHONCLOSE", "PYRUN", "PYLOAD" };
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>The path of a python.exe in a folder that is not there: the finder says "The Python set in Global settings (...) was not found."</summary>
        private string Missing(string name = "nowhere") => Path.Combine(_folder.FullPath, name, "python.exe");

        private static string NotFound(string path) =>
            "The Python set in Global settings (" + path + ") was not found.";

        private MainViewModel Using(string pythonPath)
        {
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings { PythonPath = pythonPath });
            return model;
        }

        private string Room(string name) => _folder.Write(name, TestFiles.Room(out _, out _));

        [Fact]
        public void WithPythonNotUsed_NoPythonCommandIsInTheRegistry_AndThereIsNoPalette()
        {
            var model = new MainViewModel();
            Assert.True(model.Open(Room("room.dxf")));

            Assert.Null(model.PythonConsole);

            foreach (string name in Names)
            {
                Assert.Null(model.Session.Registry.Find(name));
            }
        }

        [Fact]
        public void WithPythonNotUsed_TypingPython_IsNotACommand()
        {
            var model = new MainViewModel();
            Assert.True(model.Open(Room("room.dxf")));

            model.Execute("PYTHON");

            Assert.False(model.Session.IsRunning);
            Assert.Null(model.PythonConsole);
        }

        [Fact]
        public void UsePython_GivesTheDrawingsOpenAndTheOnesMadeLater_TheFourCommands()
        {
            MainViewModel model = Using(Missing());
            Assert.True(model.Open(Room("before.dxf")));
            CadCommandSession before = model.Session;

            model.UsePython(Dispatcher.CurrentDispatcher);
            Assert.True(model.Open(Room("after.dxf")));

            Assert.NotSame(before, model.Session);

            foreach (CadCommandSession session in new[] { before, model.Session })
            {
                foreach (string name in Names)
                {
                    Assert.NotNull(session.Registry.Find(name));
                }
            }
        }

        [Fact]
        public void UsePythonTwice_LeavesTheOnePalette_AndTheOneHost()
        {
            MainViewModel model = Using(Missing());
            model.UsePython(Dispatcher.CurrentDispatcher);
            PythonConsoleViewModel first = model.PythonConsole;

            model.UsePython(Dispatcher.CurrentDispatcher);

            Assert.NotNull(first);
            Assert.Same(first, model.PythonConsole);
        }

        [Fact]
        public void PythonShowsThePalette_AndPythonCloseHidesIt()
        {
            MainViewModel model = Using(Missing());
            Assert.True(model.Open(Room("room.dxf")));
            model.UsePython(Dispatcher.CurrentDispatcher);
            Assert.False(model.PythonConsole.IsShown);

            model.Execute("PYTHON");
            Assert.True(model.PythonConsole.IsShown);
            Assert.False(model.Session.IsRunning);

            model.Execute("PYTHONCLOSE");
            Assert.False(model.PythonConsole.IsShown);
            Assert.False(model.Session.IsRunning);
        }

        [Fact]
        public void ThePaletteFollowsTheDrawingShown_AndAClosedDrawingLeavesIt()
        {
            MainViewModel model = Using(Missing());
            Assert.True(model.Open(Room("a.dxf")));
            model.UsePython(Dispatcher.CurrentDispatcher);
            Assert.Equal(model.Document.Title, model.PythonConsole.DocumentName);

            Assert.True(model.Open(Room("b.dxf")));
            Assert.Equal(model.Document.Title, model.PythonConsole.DocumentName);
            Assert.EndsWith("b.dxf", model.PythonConsole.DocumentName, StringComparison.Ordinal);

            model.AskSaveChanges = document => CadSaveChoice.DontSave;
            Assert.True(model.CloseDocument());
            Assert.True(model.CloseDocument());

            Assert.Equal("", model.PythonConsole.DocumentName);
            Assert.False(model.PythonConsole.IsInputEnabled);
        }

        [Fact]
        public void WithAPythonThatIsNotThere_TheBannerTextIsTheFindersProblem_WordForWord()
        {
            string path = Missing();
            MainViewModel model = Using(path);
            Assert.True(model.Open(Room("room.dxf")));

            model.UsePython(Dispatcher.CurrentDispatcher);

            PythonConsoleViewModel console = model.PythonConsole;
            Assert.False(console.IsUsable);
            Assert.Equal(NotFound(path), console.Problem);
            Assert.Equal(PythonStatusKind.Problem, console.StatusKind);
            Assert.False(console.IsInputEnabled);
        }

        [Fact]
        public void AnEntryWithNoPython_IsNotRun_AndTheTranscriptStaysAsItWas()
        {
            MainViewModel model = Using(Missing());
            Assert.True(model.Open(Room("room.dxf")));
            model.UsePython(Dispatcher.CurrentDispatcher);

            model.PythonConsole.InputText = "x = 1";
            model.PythonConsole.Submit();

            // The input is greyed with no Python: nothing was sent, nothing echoed, the text is still in the box.
            Assert.Empty(model.PythonConsole.Lines);
            Assert.Equal("x = 1", model.PythonConsole.InputText);
            Assert.False(model.Document.IsModified);
        }

        [Fact]
        public void ASettingAppliedBeforePythonHasStarted_TakesEffectAtOnce_InThePalette()
        {
            string first = Missing("first");
            string second = Missing("second");
            MainViewModel model = Using(first);
            model.UsePython(Dispatcher.CurrentDispatcher);
            Assert.Equal(NotFound(first), model.PythonConsole.Problem);

            model.ApplySettings(new GlobalSettings { PythonPath = second });

            Assert.Equal(NotFound(second), model.PythonConsole.Problem);
        }

        [Fact]
        public void PyrunWithNoPython_WritesTheProblemOnTheCommandLine_AndTheRunEnds()
        {
            string path = Missing();
            string script = _folder.WriteText("hello.py", "print('hello')\n");

            OnWindowThread(() =>
            {
                MainViewModel model = Using(path);
                Assert.True(model.Open(Room("room.dxf")));
                model.UsePython(Dispatcher.CurrentDispatcher);
                model.Execute("FILEDIA");
                model.Execute("0");

                model.Execute("PYRUN");
                model.Execute(script);

                // The run is posted behind the command that asked for it, so it comes once the dispatcher works.
                Pump(() => model.History.Contains(NotFound(path)), "the problem on the command line");

                Assert.False(model.Session.IsRunning);
                Assert.False(model.PythonConsole.IsRunning);
                Assert.False(model.Document.IsModified);
            });
        }
    }
}
