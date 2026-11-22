using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Faults of Load/Unload Applications found by driving the real app (a real mouse, the Delete key, an Open dialog, a drop):
    /// the table is rebuilt on every toggle, so a second click lands on the neighbour and the rows chosen are lost; a file
    /// under a subfolder of Lisps shows an At start box that can be ticked though it loads at every start; and a file that
    /// fails half way, or loses a command when reloaded, leaves a <c>c:</c> command bound that no row lists or can unload.
    /// Driven with no window, as AppLoadWindowTests is.
    /// </summary>
    public sealed class AppLoadWindowAttackTests : IDisposable
    {
        private const string SampleName = "GeometryHelper.Cad.SamplePlugin";
        private readonly TestFolder _folder = new TestFolder();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        public void Dispose()
        {
            foreach (IDisposable item in _disposables)
            {
                item.Dispose();
            }

            _folder.Dispose();
        }

        private string Lisp(string name, string text)
        {
            Directory.CreateDirectory(Path.Combine(_folder.FullPath, "lisp"));
            string path = Path.Combine(_folder.FullPath, "lisp", name);
            File.WriteAllText(path, text);
            return path;
        }

        private MainViewModel Opened(string suite = null)
        {
            string room = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            GlobalSettings changed = model.Settings.Clone();
            changed.SecureLoad = 0;

            if (suite != null)
            {
                changed.LispStartupSuite = new[] { suite };
            }

            model.LoadSecurity.SecureLoad = 0;
            model.ApplySettings(changed);
            Assert.True(model.Open(room));
            return model;
        }

        private AppLoadViewModel Created(MainViewModel model)
        {
            var dialog = new AppLoadViewModel(model);
            _disposables.Add(dialog);
            return dialog;
        }

        private CadPluginManager Manager(out string sample)
        {
            var manager = new CadPluginManager(
                new CadPluginAssemblyLoader(),
                Path.Combine(_folder.FullPath, "plugins"),
                Path.Combine(_folder.FullPath, "plugins.json"),
                Path.Combine(_folder.FullPath, "copies")
            );
            _disposables.Add(manager);
            string dev = Path.Combine(_folder.FullPath, "dev");
            Directory.CreateDirectory(dev);
            sample = Path.Combine(dev, SampleName + ".dll");
            File.Copy(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePlugin", SampleName + ".dll"),
                sample,
                true
            );
            manager.AddTrustedFolder(dev);
            return manager;
        }

        private static AppLoadRow Row(AppLoadViewModel dialog, string name) =>
            Assert.Single(dialog.Rows, r => r.Name == name);

        /// <summary>The commands the rows list, each name once.</summary>
        private static IEnumerable<string> Listed(AppLoadViewModel dialog) =>
            dialog
                .Rows.SelectMany(r => r.Commands.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                .Distinct();

        // ---- A click that rebuilds the table

        [Fact]
        public void UntickingTheOnBoxOfARow_LeavesTheRowWhereItIs_SoASecondClickTicksItAgain()
        {
            // a.lsp, b.lsp and c.lsp lie in the table at 0, 1 and 2. A double click on the On box of b.lsp is two clicks at
            // the same place: the first unticks b.lsp, and the second hits whatever is at 1 by then. With b.lsp gone from the
            // list that is c.lsp, which the second click unloads too (seen in the real app: one double click, two files
            // unloaded). b.lsp must stay at 1, off, so that the second click ticks it back and nothing else is touched.
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            dialog.Load(
                new[]
                {
                    Lisp("a.lsp", "(defun c:AA1 () 1)"),
                    Lisp("b.lsp", "(defun c:BB1 () 1)"),
                    Lisp("c.lsp", "(defun c:CC1 () 1)"),
                }
            );
            Assert.Equal(new[] { "a.lsp", "b.lsp", "c.lsp" }, dialog.Rows.Select(r => r.Name));

            dialog.Rows[1].IsOn = false;

            AppLoadRow underTheCursor = dialog.Rows[1];
            Assert.Equal("b.lsp", underTheCursor.Name);
            Assert.False(underTheCursor.IsOn);

            underTheCursor.IsOn = !underTheCursor.IsOn;

            Assert.Equal(3, dialog.Rows.Count);
            Assert.All(dialog.Rows, r => Assert.True(r.IsOn));
            Assert.Equal(new[] { "AA1", "BB1", "CC1" }, model.Session.LispCommandNames);
        }

        [Fact]
        public void TickingOnOfOneRow_KeepsTheOtherRowsChosen()
        {
            // b.lsp and c.lsp are chosen, then the On box of a.lsp (not chosen) is unticked. The rebuild of the table drops
            // the choice (the real app showed no row selected after the click), so Unload has nothing to act on.
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            dialog.Load(
                new[] { Lisp("a.lsp", "(setq a 1)"), Lisp("b.lsp", "(setq b 1)"), Lisp("c.lsp", "(setq c 1)") }
            );
            dialog.ChooseRows(new[] { Row(dialog, "b.lsp"), Row(dialog, "c.lsp") });

            Row(dialog, "a.lsp").IsOn = false;

            Assert.Equal(new[] { "b.lsp", "c.lsp" }, dialog.Chosen.Select(r => r.Name));
            Assert.True(dialog.UnloadCommand.CanExecute(null));
        }

        [Fact]
        public void AfterReload_TheRowsStayChosen()
        {
            // Reload on two rows chosen, then Unload on the same two: the choice must survive the first, as it does in
            // any list that is refreshed in place. Today Reload rebuilds the table and the choice is empty, so Unload is
            // greyed until the rows are picked again.
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            dialog.Load(new[] { Lisp("a.lsp", "(setq a 1)"), Lisp("b.lsp", "(setq b 1)") });
            dialog.ChooseRows(dialog.Rows);

            dialog.ReloadCommand.Execute(null);

            Assert.Equal(new[] { "a.lsp", "b.lsp" }, dialog.Chosen.Select(r => r.Name));
            Assert.True(dialog.UnloadCommand.CanExecute(null));
        }

        [Fact]
        public void TickingAPluginsAtStart_KeepsTheRowsChosen()
        {
            // The plugin manager raises PluginsChanged for At start, and the table rebuilds from it: the two rows chosen
            // (a plugin and an AutoLISP file) are lost, though the box ticked belongs to one of them.
            CadPluginManager manager = Manager(out string sample);
            MainViewModel model = Opened();
            model.UsePlugins(manager, loadAtStart: false);
            AppLoadViewModel dialog = Created(model);
            dialog.Load(new[] { sample, Lisp("a.lsp", "(setq a 1)") });
            dialog.ChooseRows(dialog.Rows);

            Row(dialog, SampleName).AtStart = true;

            Assert.Equal(2, dialog.Chosen.Count);
        }

        // ---- At start of a file that always loads

        [Fact]
        public void AFileInASubfolderOfTheLispsFolder_ShowsATickedBoxThatCannotBeChanged()
        {
            // Every .lsp in Lisps loads when a drawing opens, "at any depth" (LispStartup.LoadLisps walks the subfolders), so
            // Lisps\Deep\deep.lsp always loads like a file directly in Lisps. The window greys only a file whose own folder
            // is Lisps, so this box stays free: ticking it puts the file in the Startup Suite, and it loads twice at start.
            MainViewModel model = Opened();
            string program = Path.Combine(_folder.FullPath, "program");
            string deep = Directory.CreateDirectory(Path.Combine(program, "Lisps", "Deep")).FullName;
            model.Document.SupportRoots = new CadSupportFolders(program);
            string inside = Path.Combine(deep, "deep.lsp");
            File.WriteAllText(inside, "(defun c:DEEP1 () 1)");
            AppLoadViewModel dialog = Created(model);
            dialog.Load(new[] { inside });

            AppLoadRow row = Row(dialog, "deep.lsp");

            Assert.True(row.IsAtStartFixed);
            Assert.False(row.CanChangeAtStart);
            Assert.True(row.AtStart);
        }

        // ---- A c: command no row lists

        [Fact]
        public void ALspThatFailsHalfWay_LeavesNoCommandThatNoRowLists()
        {
            // err.lsp defines c:ERRONE, then (car 5) fails: "; error: bad argument type: consp 5". The file is not loaded, so
            // it has no row, yet ERRONE stays bound and runs (seen in the real app) and nothing in the window can unload it.
            // Either the commands of a failed load are taken back, or the file is listed: no command may be left that the
            // table cannot reach.
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { Lisp("err.lsp", "(defun c:ERRONE () 1)\n(setq x (car 5))\n(defun c:ERRTWO () 2)") });

            Assert.Empty(model.Session.LispCommandNames.Except(Listed(dialog)));
        }

        [Fact]
        public void AFileThatLosesACommandAndIsReloaded_LeavesNoCommandThatNoRowLists()
        {
            // pair.lsp defines c:KEEP1 and c:GONE1, is edited to define c:KEEP1 only, and is reloaded. Its row now says
            // "KEEP1" while c:GONE1 is still bound, and Unload of the row does not remove it (the interpreter forgot that
            // pair.lsp ever defined it).
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            string path = Lisp("pair.lsp", "(defun c:KEEP1 () 1)\n(defun c:GONE1 () 2)");
            dialog.Load(new[] { path });
            File.WriteAllText(path, "(defun c:KEEP1 () 1)");
            dialog.ChooseRows(dialog.Rows);

            dialog.ReloadCommand.Execute(null);

            Assert.Empty(model.Session.LispCommandNames.Except(Listed(dialog)));
        }

        // ---- The Startup Suite

        [Fact]
        public void ASuiteEntryWrittenWithForwardSlashes_IsOneRowThatIsOnAndAtStart()
        {
            // The suite is a list in settings.json that a person can edit: "C:/dir/lisp/suite.lsp" and the loaded file
            // "C:\dir\lisp\suite.lsp" are one file. The table lists them as two: one On with At start unticked, and one off
            // with At start ticked.
            string path = Lisp("suite.lsp", "(defun c:SUITE1 () 1)");
            string slashes = path.Replace('\\', '/');
            MainViewModel model = Opened(slashes);

            AppLoadViewModel dialog = Created(model);

            AppLoadRow row = Assert.Single(dialog.Rows);
            Assert.True(row.IsOn);
            Assert.True(row.AtStart);
        }
    }
}
