using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A .py dropped on the window (D1622), with <see cref="FakePythonFiles"/> in place of the engine: it loads as APPLOAD's
    /// Load does, through the Python files of the window, and with no typed echo; it is passed over without a word while a
    /// command waits and with no drawing open, as a .lsp is. Driven at the view-model level, with no real drag, as
    /// <see cref="FileDropTests"/> is.
    /// </summary>
    public sealed class FileDropPythonTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>A window's model on a settings file of its own with a drawing open and the fake as its Python files.</summary>
        private MainViewModel Opened(out FakePythonFiles python)
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            GlobalSettings changed = model.Settings.Clone();
            changed.SecureLoad = 0;
            model.ApplySettings(changed);
            Assert.True(model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            python = new FakePythonFiles();
            model.PythonFiles = python;
            return model;
        }

        private string Py(string name) => _folder.WriteText(name, "pass\n");

        private static string[] Since(MainViewModel model, int count) => model.History.Skip(count).ToArray();

        [Fact]
        public void APyDropped_LoadsThroughThePythonFiles_AndTheCommandLineGetsTheHostsLineAlone()
        {
            MainViewModel model = Opened(out FakePythonFiles python);
            string tools = Py("tools.py");
            python.Defines["tools.py"] = new[] { "PYDROP" };
            int before = model.History.Count;

            model.DropFiles(new[] { tools });

            Assert.Equal(new[] { tools }, python.Loads);
            Assert.Equal(new[] { "tools.py loaded: PYDROP." }, Since(model, before));
            Assert.DoesNotContain(model.History, l => l.StartsWith("Command:", StringComparison.Ordinal));
        }

        [Fact]
        public void APyDropped_RaisesNoDragAndDropBox_AndIsAmongTheLoadedFiles()
        {
            MainViewModel model = Opened(out FakePythonFiles python);
            string tools = Py("tools.py");
            python.Defines["tools.py"] = new[] { "PYDROP" };
            var boxes = new List<string>();
            model.ShowDropMessage = (title, text) => boxes.Add(text);

            model.DropFiles(new[] { tools });

            Assert.Empty(boxes);
            Assert.Equal(new[] { tools }, python.Loaded);

            using (var dialog = new AppLoadViewModel(model))
            {
                Assert.Equal("PYDROP", Assert.Single(dialog.Rows).Commands);
            }
        }

        [Fact]
        public void TwoPyFilesDropped_LoadInTheOrderOfTheDrop()
        {
            MainViewModel model = Opened(out FakePythonFiles python);
            string b = Py("b.py");
            string a = Py("a.py");

            model.DropFiles(new[] { b, a });

            Assert.Equal(new[] { b, a }, python.Loads);
        }

        [Fact]
        public void ALspAndAPyDroppedTogether_EachLoadTheirOwnWay()
        {
            string lisp = _folder.WriteText("tools.lsp", "(defun c:dropped () (princ))\n");
            MainViewModel model = Opened(out FakePythonFiles python);
            string tools = Py("tools.py");

            model.DropFiles(new[] { lisp, tools });

            Assert.Equal(new[] { lisp }, model.LoadedLispFiles);
            Assert.Equal(new[] { tools }, python.Loads);
        }

        [Fact]
        public void APyDroppedWhileACommandWaits_IsPassedOverWithoutAWord_AndTheCommandStillWaits()
        {
            MainViewModel model = Opened(out FakePythonFiles python);
            string tools = Py("tools.py");
            var boxes = new List<string>();
            model.ShowDropMessage = (title, text) => boxes.Add(text);
            model.StartCommand("LINE");
            model.Execute("0,0");
            Assert.True(model.Session.IsRunning);
            string prompt = model.Session.Prompt.Text;
            int before = model.History.Count;

            model.DropFiles(new[] { tools });

            Assert.True(model.Session.IsRunning);
            Assert.Equal(prompt, model.Session.Prompt.Text);
            Assert.Empty(python.Loads);
            Assert.Empty(boxes);
            Assert.Empty(Since(model, before));
        }

        [Fact]
        public void APyDroppedWithNoDrawingOpen_IsPassedOverWithoutAWord()
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            var python = new FakePythonFiles();
            model.PythonFiles = python;
            var boxes = new List<string>();
            model.ShowDropMessage = (title, text) => boxes.Add(text);
            int before = model.History.Count;

            model.DropFiles(new[] { Py("tools.py") });

            Assert.False(model.HasDocument);
            Assert.Empty(python.Loads);
            Assert.Empty(boxes);
            Assert.Empty(Since(model, before));
        }
    }
}
