using System;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Python in the window (D720, D1670): with the app's Python off nothing of it is in the commands; with it on, every drawing
    /// has PYRUN and PYLOAD, and PYTHON and PYTHONCLOSE (the palette's commands, removed) are not commands. The Python these
    /// tests name is a folder that does not exist, so the finder's problem is the one it words for a setting that does not work
    /// and no engine can start (Python starts once per process: the one test class that runs it is
    /// <see cref="CadPythonWindowOwnerRunTests"/>).
    /// </summary>
    public sealed class MainViewModelPythonTests : IDisposable
    {
        private static readonly string[] Names = { "PYRUN", "PYLOAD" };
        private static readonly string[] Removed = { "PYTHON", "PYTHONCLOSE" };
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
        public void WithPythonNotUsed_NoPythonCommandIsInTheRegistry()
        {
            var model = new MainViewModel();
            Assert.True(model.Open(Room("room.dxf")));

            Assert.False(model.UsesPython);

            foreach (string name in Names.Concat(Removed))
            {
                Assert.Null(model.Session.Registry.Find(name));
            }
        }

        [Fact]
        public void WithPythonNotUsed_TypingPyrun_IsNotACommand()
        {
            var model = new MainViewModel();
            Assert.True(model.Open(Room("room.dxf")));

            model.Execute("PYRUN");

            Assert.False(model.Session.IsRunning);
            Assert.False(model.UsesPython);
        }

        [Fact]
        public void UsePython_GivesTheDrawingsOpenAndTheOnesMadeLater_PyrunAndPyload_AndNotThePalettesCommands()
        {
            MainViewModel model = Using(Missing());
            Assert.True(model.Open(Room("before.dxf")));
            CadCommandSession before = model.Session;

            model.UsePython(Dispatcher.CurrentDispatcher);
            Assert.True(model.Open(Room("after.dxf")));

            Assert.NotSame(before, model.Session);
            Assert.True(model.UsesPython);

            foreach (CadCommandSession session in new[] { before, model.Session })
            {
                foreach (string name in Names)
                {
                    Assert.NotNull(session.Registry.Find(name));
                }

                foreach (string name in Removed)
                {
                    Assert.Null(session.Registry.Find(name));
                }
            }
        }

        [Fact]
        public void TypingPythonAfterUsePython_IsNotACommand_AsThePaletteIsGone()
        {
            MainViewModel model = Using(Missing());
            Assert.True(model.Open(Room("room.dxf")));
            model.UsePython(Dispatcher.CurrentDispatcher);

            model.Execute("PYTHON");
            model.Execute("PYTHONCLOSE");

            Assert.False(model.Session.IsRunning);
            Assert.Contains(model.Session.History, line =>
                line.Contains("Unknown command \"PYTHON\"", StringComparison.Ordinal)
            );
            Assert.Contains(model.Session.History, line =>
                line.Contains("Unknown command \"PYTHONCLOSE\"", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void ASettingAppliedBeforePythonHasStarted_TakesEffectAtOnce_ForThePyrunThatFollows()
        {
            string first = Missing("first");
            string second = Missing("second");
            string script = _folder.WriteText("hello.py", "print('hello')\n");

            OnWindowThread(() =>
            {
                MainViewModel model = Using(first);
                Assert.True(model.Open(Room("room.dxf")));
                model.UsePython(Dispatcher.CurrentDispatcher);
                model.Execute("FILEDIA");
                model.Execute("0");
                model.Execute("PYRUN");
                model.Execute(script);
                Pump(() => model.History.Contains(NotFound(first)), "the first problem on the command line");

                model.ApplySettings(new GlobalSettings { PythonPath = second });
                model.Execute("PYRUN");
                model.Execute(script);

                Pump(() => model.History.Contains(NotFound(second)), "the second problem on the command line");
            });
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
                Assert.False(model.Document.IsModified);
            });
        }
    }
}
