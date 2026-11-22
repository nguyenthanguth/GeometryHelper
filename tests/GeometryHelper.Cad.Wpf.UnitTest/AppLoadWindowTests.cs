using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Load/Unload Applications, the one window of APPLOAD and the Plugin manager button: its table of AutoLISP files and
    /// plugins (On, Name, Kind, Commands, File, At start), the chips, Load, Reload and Unload, and At start kept in the
    /// Startup Suite. Driven with no window; the last tests lay the window out offscreen.
    /// </summary>
    public sealed class AppLoadWindowTests : IDisposable
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

        private string SettingsFile => Path.Combine(_folder.FullPath, "settings.json");

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
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.LoadSecurity.SecureLoad = 0;
            GlobalSettings changed = model.Settings.Clone();
            changed.SecureLoad = 0;

            if (suite != null)
            {
                changed.LispStartupSuite = new[] { suite };
            }

            model.ApplySettings(changed);
            Assert.True(model.Open(room));
            return model;
        }

        /// <summary>A manager over the loader of the runtime, with the sample plugin copied to a folder it trusts.</summary>
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

        /// <summary>Runs APPLOAD with a window that keeps the dialog it is given, which the test then drives.</summary>
        private AppLoadViewModel Shown(MainViewModel model)
        {
            AppLoadViewModel shown = null;
            model.ShowAppLoad = dialog =>
            {
                shown = dialog;
                _disposables.Add(dialog);
                return true;
            };
            model.StartCommand("APPLOAD");
            Assert.NotNull(shown);
            return shown;
        }

        /// <summary>A dialog over a view model, as a window with no drawing open makes it; its Open dialog is never asked.</summary>
        private AppLoadViewModel Created(MainViewModel model)
        {
            var dialog = new AppLoadViewModel(model);
            _disposables.Add(dialog);
            return dialog;
        }

        private static AppLoadRow Row(AppLoadViewModel dialog, string name) =>
            Assert.Single(dialog.Rows, r => r.Name == name);

        // ---- The AutoLISP rows

        [Fact]
        public void ALspThatDefinesAaAndBb_ShowsARowWithItsCommands()
        {
            MainViewModel model = Opened();
            string path = Lisp("pair.lsp", "(defun c:AA () 1)\n(defun helper () 2)\n(defun c:BB () 3)");
            AppLoadViewModel dialog = Shown(model);

            dialog.Load(new[] { path });

            AppLoadRow row = Assert.Single(dialog.Rows);
            Assert.Equal("pair.lsp", row.Name);
            Assert.Equal("Lisp", row.Kind);
            Assert.False(row.IsPlugin);
            Assert.Equal("AA, BB", row.Commands);
            Assert.Equal(path, row.File);
            Assert.True(row.IsOn);
            Assert.False(row.AtStart);
            Assert.True(row.CanChangeAtStart);
            Assert.Equal("pair.lsp successfully loaded.", model.History.Last());
        }

        [Fact]
        public void ALspThatDefinesNoCommand_HasAnEmptyCommandsCell()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);

            dialog.Load(new[] { Lisp("plain.lsp", "(defun helper () 2)") });

            Assert.Equal(string.Empty, Assert.Single(dialog.Rows).Commands);
        }

        [Fact]
        public void UnloadOfThatRow_MakesItsCommandsUnknown_TheRowLeaves_AndOtherFunctionsStay()
        {
            MainViewModel model = Opened();
            string path = Lisp("pair.lsp", "(defun c:ZA () 1)\n(defun helper () 2)\n(defun c:ZB () 3)");
            AppLoadViewModel dialog = Shown(model);
            dialog.Load(new[] { path });
            dialog.ChooseRows(dialog.Rows);

            dialog.UnloadCommand.Execute(null);

            Assert.Empty(dialog.Rows);
            Assert.Empty(model.Session.LispCommandNames);
            Assert.Equal("pair.lsp unloaded.", model.History.Last());
            model.Execute("ZA");
            Assert.Contains("Unknown command", model.History.Last());
            model.Execute("(helper)");
            Assert.Equal("2", model.History.Last());
        }

        [Fact]
        public void UnloadWithNothingChosen_CannotBePressed()
        {
            AppLoadViewModel dialog = Shown(Opened());

            Assert.False(dialog.UnloadCommand.CanExecute(null));
            Assert.False(dialog.ReloadCommand.CanExecute(null));
        }

        [Fact]
        public void UntickingOn_IsUnload_AndTickingItAgainLoadsTheFile_OfAFileInTheStartupSuite()
        {
            string path = Lisp("suite.lsp", "(defun c:SU () 1)");
            MainViewModel model = Opened(path);
            AppLoadViewModel dialog = Shown(model);
            AppLoadRow row = Row(dialog, "suite.lsp");
            Assert.True(row.IsOn);
            Assert.True(row.AtStart);

            row.IsOn = false;

            // It stays in the list, off, because the suite still holds it.
            row = Row(dialog, "suite.lsp");
            Assert.False(row.IsOn);
            Assert.Equal(string.Empty, row.Commands);
            Assert.Empty(model.Session.LispCommandNames);

            row.IsOn = true;

            row = Row(dialog, "suite.lsp");
            Assert.True(row.IsOn);
            Assert.Equal("SU", row.Commands);
            Assert.Equal(new[] { "SU" }, model.Session.LispCommandNames);
        }

        [Fact]
        public void ReloadLoadsTheChosenRowsAgain_AndTheCommandsFollowTheFile()
        {
            MainViewModel model = Opened();
            string path = Lisp("edit.lsp", "(defun c:ONE () 1)");
            AppLoadViewModel dialog = Shown(model);
            dialog.Load(new[] { path });
            File.WriteAllText(path, "(defun c:ONE () 1)\n(defun c:TWO () 2)");
            dialog.ChooseRows(dialog.Rows);

            dialog.ReloadCommand.Execute(null);

            Assert.Equal("ONE, TWO", Assert.Single(dialog.Rows).Commands);
            Assert.Equal("edit.lsp successfully loaded.", model.History.Last());
        }

        // ---- At start

        [Fact]
        public void AtStart_RoundTripsThroughTheStartupSuiteStorage()
        {
            MainViewModel model = Opened();
            string path = Lisp("start.lsp", "(defun c:ST () 1)");
            AppLoadViewModel dialog = Shown(model);
            dialog.Load(new[] { path });

            Row(dialog, "start.lsp").AtStart = true;

            Assert.Equal(new[] { path }, model.Settings.LispStartupSuite);
            Assert.Equal(new[] { path }, GlobalSettings.Load(SettingsFile).LispStartupSuite);
            Assert.True(Row(dialog, "start.lsp").AtStart);

            // A window opened afterwards reads the suite back, so that current suites carry over.
            Assert.True(Row(Shown(model), "start.lsp").AtStart);

            Row(dialog, "start.lsp").AtStart = false;

            Assert.Empty(model.Settings.LispStartupSuite);
            Assert.Empty(GlobalSettings.Load(SettingsFile).LispStartupSuite);
            Assert.False(Row(dialog, "start.lsp").AtStart);
        }

        [Fact]
        public void AFileInTheLispsFolder_ShowsATickedBoxThatCannotBeChanged()
        {
            MainViewModel model = Opened();
            string program = Path.Combine(_folder.FullPath, "program");
            string lisps = Directory.CreateDirectory(Path.Combine(program, "Lisps")).FullName;
            model.Document.SupportRoots = new CadSupportFolders(program);
            string inside = Path.Combine(lisps, "GH-Info.lsp");
            File.WriteAllText(inside, "(defun c:GHI () 1)");
            string outside = Lisp("mine.lsp", "(defun c:MINE () 1)");
            AppLoadViewModel dialog = Shown(model);
            dialog.Load(new[] { inside, outside });

            AppLoadRow fixedRow = Row(dialog, "GH-Info.lsp");
            Assert.True(fixedRow.AtStart);
            Assert.True(fixedRow.IsAtStartFixed);
            Assert.False(fixedRow.CanChangeAtStart);
            Assert.True(Row(dialog, "mine.lsp").CanChangeAtStart);

            fixedRow.AtStart = false;

            Assert.True(Row(dialog, "GH-Info.lsp").AtStart);
            Assert.Empty(model.Settings.LispStartupSuite);
        }

        [Fact]
        public void APluginsAtStart_IsTheStartupList()
        {
            CadPluginManager manager = Manager(out string sample);
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            AppLoadViewModel dialog = Created(model);
            dialog.Load(new[] { sample });

            AppLoadRow row = Row(dialog, SampleName);
            Assert.False(row.AtStart);
            Assert.True(row.CanChangeAtStart);

            row.AtStart = true;

            Assert.Contains(sample, manager.StartupList, StringComparer.OrdinalIgnoreCase);
            Assert.True(Row(dialog, SampleName).AtStart);

            row = Row(dialog, SampleName);
            row.AtStart = false;

            Assert.DoesNotContain(sample, manager.StartupList, StringComparer.OrdinalIgnoreCase);
        }

        // ---- Plugins

        [Fact]
        public void LoadWithADll_GoesTheNetloadWay_AndThePluginRowShowsItsCommands()
        {
            CadPluginManager manager = Manager(out string sample);
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { sample });

            AppLoadRow row = Assert.Single(dialog.Rows);
            Assert.Equal(SampleName, row.Name);
            Assert.Equal("Plugin", row.Kind);
            Assert.True(row.IsPlugin);
            Assert.Equal("FAILNOW, NUMCIRCLES", row.Commands);
            Assert.Equal(sample, row.File);
            Assert.True(row.IsOn);
            Assert.Contains(model.History, line => line.StartsWith("Loaded " + SampleName, StringComparison.Ordinal));
            Assert.Equal(Path.GetDirectoryName(sample), model.Settings.AppLoadFolder);
        }

        [Fact]
        public void ADllOutsideTheTrustedFolders_AsksOnceAsNetloadDoes_AndIsNotLoadedWhenRefused()
        {
            CadPluginManager manager = Manager(out string sample);
            string elsewhere = Path.Combine(_folder.FullPath, "elsewhere");
            Directory.CreateDirectory(elsewhere);
            string other = Path.Combine(elsewhere, SampleName + ".dll");
            File.Copy(sample, other);
            var asked = new List<string>();
            manager.ConfirmUntrusted = path =>
            {
                asked.Add(path);
                return false;
            };
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { other });

            Assert.Equal(new[] { other }, asked);
            Assert.DoesNotContain(dialog.Rows, r => r.IsOn);
        }

        [Fact]
        public void TickingAPluginsOnBox_LoadsAndUnloadsIt()
        {
            CadPluginManager manager = Manager(out string sample);
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            AppLoadViewModel dialog = Created(model);
            dialog.Load(new[] { sample });

            Row(dialog, SampleName).IsOn = false;

            Assert.False(Row(dialog, SampleName).IsOn);
            Assert.False(manager.Plugins.Single().IsLoaded);

            Row(dialog, SampleName).IsOn = true;

            Assert.True(Row(dialog, SampleName).IsOn);
            Assert.True(manager.Plugins.Single().IsLoaded);
        }

        [Fact]
        public void UnloadAndReloadOnAPluginRow_ActAsThePluginManagerDid()
        {
            CadPluginManager manager = Manager(out string sample);
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            AppLoadViewModel dialog = Created(model);
            dialog.Load(new[] { sample });
            dialog.ChooseRows(dialog.Rows);

            dialog.UnloadCommand.Execute(null);

            Assert.False(manager.Plugins.Single().IsLoaded);
            Assert.Single(dialog.Rows);
            Assert.False(dialog.Rows[0].IsOn);

            dialog.ChooseRows(dialog.Rows);
            dialog.ReloadCommand.Execute(null);

            Assert.True(manager.Plugins.Single().IsLoaded);
            Assert.True(dialog.Rows[0].IsOn);
        }

        // ---- The table and the chips

        [Fact]
        public void TheChips_CountTheRowsByKind_AndFilterTheTable()
        {
            CadPluginManager manager = Manager(out string sample);
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _)));
            model.LoadSecurity.SecureLoad = 0;
            AppLoadViewModel dialog = Shown(model);
            dialog.Load(new[] { Lisp("b.lsp", "(setq b 1)"), Lisp("a.lsp", "(setq a 1)"), sample });

            Assert.Equal("All 3", dialog.AllChipText);
            Assert.Equal("Lisp 2", dialog.LispChipText);
            Assert.Equal("Plugins 1", dialog.PluginChipText);
            Assert.Equal(3, dialog.Rows.Count);

            dialog.FilterCommand.Execute("Lisp");
            Assert.Equal(new[] { "a.lsp", "b.lsp" }, dialog.Rows.Select(r => r.Name));

            dialog.FilterCommand.Execute("Plugin");
            Assert.Equal(new[] { SampleName }, dialog.Rows.Select(r => r.Name));

            dialog.FilterCommand.Execute("All");
            Assert.Equal(3, dialog.Rows.Count);
            Assert.Equal("All 3", dialog.AllChipText);
        }

        [Fact]
        public void TheRowsSortByNameThenFile_EitherCaseTheSame()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            string one = Lisp("Tools.lsp", "(setq x 1)");
            Directory.CreateDirectory(Path.Combine(_folder.FullPath, "other"));
            string two = Path.Combine(_folder.FullPath, "other", "tools.lsp");
            File.WriteAllText(two, "(setq y 1)");
            string three = Lisp("alpha.lsp", "(setq z 1)");

            dialog.Load(new[] { one, two, three });

            Assert.Equal(new[] { three, one, two }, dialog.Rows.Select(r => r.File));
        }

        [Fact]
        public void TheDialogOfALoadWithNoDrawing_SaysSoAndLoadsNoLispFile()
        {
            var model = new MainViewModel { SettingsPath = SettingsFile };
            string path = Lisp("nodoc.lsp", "(setq a 1)");
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { path });

            Assert.Empty(dialog.Rows);
            Assert.Contains("Open a drawing to load nodoc.lsp.", model.History);
        }

        // ---- Load…

        [Fact]
        public void LoadAsksForFilesOnTheFolderOfTheLastLoad_AndLoadsThemInOrder()
        {
            MainViewModel model = Opened();
            string first = Lisp("one.lsp", "(defun c:ONE () 1)");
            string second = Lisp("two.lsp", "(defun c:TWO () 1)");
            AppLoadViewModel dialog = Shown(model);
            var folders = new List<string>();
            dialog.AskFiles = folder =>
            {
                folders.Add(folder);
                return new[] { second, first };
            };

            dialog.LoadCommand.Execute(null);
            dialog.LoadCommand.Execute(null);

            Assert.Equal(new[] { string.Empty, Path.GetDirectoryName(first) }, folders);
            Assert.Equal(
                new[] { "two.lsp successfully loaded.", "one.lsp successfully loaded." },
                model.History.Where(l => l.EndsWith("successfully loaded.", StringComparison.Ordinal)).Take(2)
            );
            Assert.Equal(Path.GetDirectoryName(first), GlobalSettings.Load(SettingsFile).AppLoadFolder);
        }

        [Fact]
        public void LoadAskedAndCancelled_LoadsNothing()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            dialog.AskFiles = folder => null;
            int before = model.History.Count;

            dialog.LoadCommand.Execute(null);

            Assert.Equal(before, model.History.Count);
        }

        [Fact]
        public void TheOpenDialogOffersApplicationsFirst_ThenLispPluginsAndAllFiles()
        {
            Assert.Equal(
                "Applications (*.lsp;*.dll)|*.lsp;*.dll|Lisp files (*.lsp)|*.lsp|Plugins (*.dll)|*.dll|All files (*.*)|*.*",
                AppLoadViewModel.OpenFilter
            );
        }

        [Fact]
        public void AnotherExtension_GetsTheExistingRefusalLine_AndNoRow()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            string notes = _folder.WriteText("notes.txt", "hello");
            string tools = _folder.WriteText("tools.arx", "x");

            dialog.Load(new[] { notes, tools });

            Assert.Contains("notes.txt is not an AutoLISP file or a plugin.", model.History);
            Assert.Contains(
                "tools.arx cannot be loaded: .arx, .dbx, .dvb, .fas and .vlx files are not supported.",
                model.History
            );
            Assert.Empty(dialog.Rows);
        }

        [Fact]
        public void FilesDroppedOnTheTable_AreLoadedInTheOrderOfTheDrop_AndAFolderIsPassedOver()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            string one = Lisp("d1.lsp", "(setq d1 1)");
            string two = Lisp("d2.lsp", "(setq d2 1)");

            dialog.LoadDropped(new[] { two, one, Path.GetDirectoryName(one) });

            Assert.Equal(new[] { "d1.lsp", "d2.lsp" }, dialog.Rows.Select(r => r.Name));
            Assert.Equal(string.Empty, model.Settings.AppLoadFolder);
        }

        // ---- The commands that open it

        [Fact]
        public void AppLoadAndThePluginManagerButton_OpenTheSameWindow()
        {
            CadPluginManager manager = Manager(out _);
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            Assert.True(model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            int shown = 0;
            model.ShowAppLoad = dialog =>
            {
                shown++;
                return true;
            };

            model.StartCommand("APPLOAD");
            model.PluginRibbon.ManagerCommand.Execute(null);

            Assert.Equal(2, shown);
        }

        // ---- The window

        [Fact]
        public void TheWindow_HasTheTitleTheToolbarTheChipsTheSixColumnsAndTheHint()
        {
            CadPluginManager manager = Manager(out string sample);
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _)));
            model.LoadSecurity.SecureLoad = 0;
            AppLoadViewModel dialog = new AppLoadViewModel(model);
            string lsp = Lisp("layers.lsp", "(defun c:C1 () 1)\n(defun c:C2 () 1)");
            dialog.Load(new[] { lsp, sample });
            string[] texts = null;
            string title = null;
            int rows = 0;
            Color lispKind = default;
            Color pluginKind = default;
            int boxes = 0;

            OnSta(() =>
            {
                var window = new AppLoadWindow(dialog);
                title = window.Title;
                FrameworkElement root = window.Body;
                window.Content = null;
                root.DataContext = dialog;
                root.Measure(new Size(900, 700));
                root.Arrange(new Rect(0, 0, 900, 700));
                root.UpdateLayout();
                List<DependencyObject> all = Descendants(root).ToList();
                texts = all.OfType<TextBlock>()
                    .Select(t => t.Text)
                    .Concat(all.OfType<Button>().Select(b => b.Content as string))
                    .Concat(all.OfType<RadioButton>().Select(b => b.Content as string))
                    .ToArray();
                rows = all.OfType<ListBoxItem>().Count(i => i.DataContext is AppLoadRow);
                List<TextBlock> kinds = all.OfType<TextBlock>().Where(t => t.Name == "Kind").ToList();
                lispKind = ((SolidColorBrush)kinds.Single(t => t.Text == "Lisp").Foreground).Color;
                pluginKind = ((SolidColorBrush)kinds.Single(t => t.Text == "Plugin").Foreground).Color;
                boxes = all.OfType<CheckBox>().Count();
                window.Close();
            });

            Assert.Equal("Load/Unload Applications", title);
            Assert.Contains("Load… (.lsp, .dll)", texts);
            Assert.Contains("Reload", texts);
            Assert.Contains("Unload", texts);
            Assert.Contains("All 2", texts);
            Assert.Contains("Lisp 1", texts);
            Assert.Contains("Plugins 1", texts);

            foreach (string head in new[] { "On", "Name", "Kind", "Commands", "File", "At start" })
            {
                Assert.Contains(head, texts);
            }

            Assert.Contains(
                "Files in the Lisps and Plugins folders beside the app load at every start; their At start box is greyed. Hover a Commands cell for the full list.",
                texts
            );
            Assert.Contains("C1, C2", texts);
            Assert.Equal(2, rows);
            Assert.Equal(4, boxes);
            Assert.Equal(Color.FromRgb(0x3F, 0xB9, 0x50), lispKind);
            Assert.Equal(Color.FromRgb(0x6C, 0xB6, 0xFF), pluginKind);
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
