using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// APPLOAD's Load/Unload Applications and its Startup Suite, as the design draws them: the folder browser, Load with its
    /// footer and its refusals, Add to History and the History list, the Loaded Applications list with Unload, and the Startup
    /// Suite. Driven with no window; the last tests lay the two windows out offscreen.
    /// </summary>
    public sealed class AppLoadTests : IDisposable
    {
        private const string SampleName = "GeometryHelper.Cad.SamplePlugin";
        private readonly TestFolder _folder = new TestFolder();
        private readonly string _settingsFile;
        private readonly List<IDisposable> _managers = new List<IDisposable>();

        public AppLoadTests()
        {
            _settingsFile = Path.Combine(_folder.FullPath, "settings.json");
            Directory.CreateDirectory(Browsed);
        }

        public void Dispose()
        {
            foreach (IDisposable manager in _managers)
            {
                manager.Dispose();
            }

            _folder.Dispose();
        }

        /// <summary>The folder the dialog opens on in most of the tests; it holds the files they write.</summary>
        private string Browsed => Path.Combine(_folder.FullPath, "lisp");

        private string Lisp(string name, string text)
        {
            string path = Path.Combine(Browsed, name);
            File.WriteAllText(path, text);
            return path;
        }

        private MainViewModel Opened(Action<GlobalSettings> settings = null)
        {
            string room = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel { SettingsPath = _settingsFile };
            GlobalSettings changed = model.Settings.Clone();
            changed.AppLoadFolder = Browsed;
            settings?.Invoke(changed);
            model.ApplySettings(changed);
            Assert.True(model.Open(room));
            return model;
        }

        /// <summary>Runs APPLOAD with a window that keeps the dialog it is given, which the test then drives.</summary>
        private static AppLoadViewModel Shown(MainViewModel model)
        {
            AppLoadViewModel shown = null;
            model.ShowAppLoad = dialog =>
            {
                shown = dialog;
                return true;
            };
            model.StartCommand("APPLOAD");
            Assert.NotNull(shown);
            return shown;
        }

        private static void Choose(AppLoadViewModel dialog, params string[] names) =>
            dialog.ChooseEntries(dialog.Entries.Where(e => names.Contains(e.Name)));

        private GlobalSettings Saved() => GlobalSettings.Load(_settingsFile);

        // ---- The folder browser

        [Fact]
        public void TheDialogOpensInTheFolderLastUsed()
        {
            AppLoadViewModel dialog = Shown(Opened());

            Assert.Equal(Browsed, dialog.Folder);
            Assert.Equal(Browsed, dialog.LookIn);
            Assert.Equal(Browsed, dialog.FolderChain.Last());
            Assert.Equal(Path.GetPathRoot(Browsed), dialog.FolderChain.First());
        }

        [Fact]
        public void WithNoFolderUsedYet_TheDialogOpensInTheFirstSupportFolderThatIsThere_ElseDocuments()
        {
            string support = Directory.CreateDirectory(Path.Combine(_folder.FullPath, "support")).FullName;

            AppLoadViewModel viaSupport = Shown(
                Opened(s =>
                {
                    s.AppLoadFolder = Path.Combine(_folder.FullPath, "gone");
                    s.SupportSearchFolders = new[] { Path.Combine(_folder.FullPath, "missing"), support };
                })
            );
            AppLoadViewModel viaDocuments = Shown(Opened(s => s.AppLoadFolder = string.Empty));

            Assert.Equal(support, viaSupport.Folder);
            Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), viaDocuments.Folder);
        }

        [Fact]
        public void TheList_HasTheFoldersFirstThenTheFilesOfTheType_EachByNameWithCaseIgnored()
        {
            Directory.CreateDirectory(Path.Combine(Browsed, "beta"));
            Directory.CreateDirectory(Path.Combine(Browsed, "Alpha"));
            Lisp("b.lsp", "");
            Lisp("A.lsp", "");
            Lisp("c.dll", "");
            Lisp("notes.txt", "");
            Lisp("tools.fas", "");
            AppLoadViewModel dialog = Shown(Opened());

            Assert.Equal(new[] { "Alpha", "beta", "A.lsp", "b.lsp" }, dialog.Entries.Select(e => e.Name));
            Assert.Equal(
                new[] { "File folder", "File folder", "AutoLISP file", "AutoLISP file" },
                dialog.Entries.Select(e => e.Type)
            );

            dialog.FileType = dialog.FileTypes[1];
            Assert.Equal(new[] { "Alpha", "beta", "c.dll" }, dialog.Entries.Select(e => e.Name));
            Assert.Equal("Plugin", dialog.Entries.Last().Type);

            dialog.FileType = dialog.FileTypes[2];
            Assert.Equal(
                new[] { "Alpha", "beta", "A.lsp", "b.lsp", "c.dll", "notes.txt", "tools.fas" },
                dialog.Entries.Select(e => e.Name)
            );
            Assert.Equal(
                new[] { "AutoLISP file", "AutoLISP file", "Plugin", "Text document", "Compiled AutoLISP" },
                dialog.Entries.Where(e => !e.IsFolder).Select(e => e.Type)
            );
        }

        [Fact]
        public void TheFileTypes_AreAutoLispPluginsAndAllFiles_AutoLispFirst()
        {
            AppLoadViewModel dialog = Shown(Opened());

            Assert.Equal(
                new[] { "AutoLISP Files (*.lsp)", "Plugins (*.dll)", "All files (*.*)" },
                dialog.FileTypes.Select(t => t.Label)
            );
            Assert.Same(dialog.FileTypes[0], dialog.FileType);
        }

        [Fact]
        public void UpGoesToTheParent_BackToTheFolderShownBefore_AndAFolderThatIsARootHasNoUp()
        {
            string sub = Directory.CreateDirectory(Path.Combine(Browsed, "old")).FullName;
            AppLoadViewModel dialog = Shown(Opened(s => s.AppLoadFolder = sub));
            Assert.False(dialog.CanBack);
            Assert.True(dialog.CanUp);

            dialog.Up();

            Assert.Equal(Browsed, dialog.Folder);
            Assert.True(dialog.CanBack);

            dialog.Back();

            Assert.Equal(sub, dialog.Folder);
            Assert.False(dialog.CanBack);

            Assert.True(dialog.OpenFolder(Path.GetPathRoot(sub)));
            Assert.False(dialog.CanUp);
            dialog.Up();
            Assert.Equal(Path.GetPathRoot(sub), dialog.Folder);
        }

        [Fact]
        public void ChoosingTheFolderInLookIn_OpensIt_AndAFolderThatIsNotThereOrIsShownIsNotOpened()
        {
            AppLoadViewModel dialog = Shown(Opened());
            string root = Path.GetPathRoot(Browsed);

            dialog.LookIn = root;

            Assert.Equal(root, dialog.Folder);
            Assert.False(dialog.OpenFolder(root));
            Assert.False(dialog.OpenFolder(Path.Combine(Browsed, "nothing")));
            Assert.False(dialog.OpenFolder(" "));
            Assert.Equal(root, dialog.Folder);
        }

        [Fact]
        public void ChoosingFiles_PutsTheirNamesInTheFileNameBoxInTheOrderOfTheList_AFolderIsNotNamed()
        {
            Directory.CreateDirectory(Path.Combine(Browsed, "old"));
            Lisp("b.lsp", "");
            Lisp("a.lsp", "");
            AppLoadViewModel dialog = Shown(Opened());

            Choose(dialog, "b.lsp");
            Assert.Equal("b.lsp", dialog.FileName);

            Choose(dialog, "b.lsp", "a.lsp");
            Assert.Equal("\"a.lsp\" \"b.lsp\"", dialog.FileName);

            Choose(dialog, "old");
            Assert.Equal("\"a.lsp\" \"b.lsp\"", dialog.FileName);
        }

        [Fact]
        public void ADoubleClickOnAFolderOpensIt_AndOnAFileLoadsIt()
        {
            string old = Directory.CreateDirectory(Path.Combine(Browsed, "old")).FullName;
            string tools = Lisp("tools.lsp", "(setq ghtools 1)");
            AppLoadViewModel dialog = Shown(Opened());

            dialog.Activate(dialog.Entries.Single(e => e.Name == "old"));
            Assert.Equal(old, dialog.Folder);

            dialog.Back();
            dialog.Activate(dialog.Entries.Single(e => e.Name == "tools.lsp"));

            Assert.Equal("tools.lsp successfully loaded.", dialog.Status);
            Assert.Equal(new[] { tools }, dialog.LoadedRows.Select(r => r.FullPath));
        }

        // ---- Load

        [Fact]
        public void LoadingAChosenLspFile_LoadsItIntoTheDrawing_ShowsItInGreen_AndKeepsTheFolderAndTheHistory()
        {
            string tools = Lisp("tools.lsp", "(defun c:ghtools () (princ \"tools\") (princ))\n(princ \"hello\")");
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            Choose(dialog, "tools.lsp");
            Assert.True(dialog.CanLoad);

            dialog.Load();

            Assert.Equal("tools.lsp successfully loaded.", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Loaded, dialog.StatusKind);
            Assert.Equal("tools.lsp successfully loaded.", model.History.Last());
            Assert.Contains("hello", model.History);
            Assert.Equal(new[] { "GHTOOLS" }, model.Session.LispCommandNames);
            AppLoadRow loaded = Assert.Single(dialog.LoadedRows);
            Assert.Equal(tools, loaded.FullPath);
            Assert.Equal("tools.lsp", loaded.File);
            Assert.Equal("AutoLISP", loaded.Kind);
            Assert.Equal(Browsed, loaded.Folder);
            Assert.Equal(new[] { tools }, dialog.HistoryRows.Select(r => r.FullPath));

            GlobalSettings saved = Saved();
            Assert.Equal(new[] { tools }, saved.AppLoadHistory);
            Assert.Equal(Browsed, saved.AppLoadFolder);
        }

        [Fact]
        public void ALspFileTypedByItsNameInTheFolderShown_OrByItsFullPathElsewhere_IsLoaded()
        {
            string tools = Lisp("tools.lsp", "(setq ghtools 1)");
            string other = _folder.WriteText("other.lsp", "(setq ghother 1)");
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);

            dialog.FileName = " tools.lsp ";
            dialog.Load();
            dialog.FileName = other;
            dialog.Load();

            Assert.Equal(new[] { tools, other }, dialog.LoadedRows.Select(r => r.FullPath));
            Assert.Equal(1, model.Session.Lisp.GetValue("GHTOOLS"));
            Assert.Equal(1, model.Session.Lisp.GetValue("GHOTHER"));
        }

        [Fact]
        public void SeveralFilesInQuotes_AreLoadedInOrder_AndTheFooterShowsTheLastOne()
        {
            string a = Lisp("a.lsp", "(princ \"[a]\")");
            string b = Lisp("b.lsp", "(princ \"[b]\")");
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            Choose(dialog, "a.lsp", "b.lsp");

            dialog.Load();

            Assert.Equal(
                new[] { "[a]", "a.lsp successfully loaded.", "[b]", "b.lsp successfully loaded." },
                model.History.Skip(model.History.Count - 4)
            );
            Assert.Equal("b.lsp successfully loaded.", dialog.Status);
            Assert.Equal(new[] { a, b }, dialog.LoadedRows.Select(r => r.FullPath));
            Assert.Equal(new[] { b, a }, dialog.HistoryRows.Select(r => r.FullPath));
        }

        [Fact]
        public void LoadIsGreyedWithNothingChosenOrTyped()
        {
            AppLoadViewModel dialog = Shown(Opened());
            Assert.False(dialog.CanLoad);

            dialog.FileName = "  ";
            Assert.False(dialog.CanLoad);

            dialog.FileName = "\"\"";
            Assert.True(dialog.CanLoad);
            dialog.Load();

            Assert.Equal(string.Empty, dialog.Status);
            Assert.Empty(dialog.HistoryRows);
            Assert.Empty(Saved().AppLoadHistory);
        }

        [Fact]
        public void ALspFileWithAnError_ShowsTheErrorLineInRed_IsNotLoaded_YetIsInTheHistory()
        {
            string bad = Lisp("bad.lsp", "(setq ghb 1)\n(car 1)");
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            dialog.FileName = "bad.lsp";

            dialog.Load();

            Assert.Equal("bad.lsp was not loaded: ; error: bad argument type: consp 1", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Failed, dialog.StatusKind);
            Assert.Equal("; error: bad argument type: consp 1", model.History.Last());
            Assert.Empty(dialog.LoadedRows);
            Assert.Equal(new[] { bad }, dialog.HistoryRows.Select(r => r.FullPath));
        }

        [Fact]
        public void ALspFileThatIsMissing_IsNotLoadedAndSaysWhy()
        {
            AppLoadViewModel dialog = Shown(Opened());
            dialog.FileName = "missing.lsp";

            dialog.Load();

            Assert.Equal(AppLoadStatusKind.Failed, dialog.StatusKind);
            Assert.StartsWith("missing.lsp was not loaded: ; error: LOAD failed: ", dialog.Status);
            Assert.Empty(dialog.LoadedRows);
        }

        [Fact]
        public void ACompiledOrNativeFile_IsRefusedInAmber_AndTheCommandLineSaysSo()
        {
            string[] refused = { "tools.fas", "tools.vlx", "tools.arx", "tools.dbx", "tools.dvb" };

            foreach (string name in refused)
            {
                Lisp(name, "x");
            }

            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            dialog.FileType = dialog.FileTypes[2];

            foreach (string name in refused)
            {
                dialog.FileName = name;
                dialog.Load();

                string line = name + " cannot be loaded: .arx, .dbx, .dvb, .fas and .vlx files are not supported.";
                Assert.Equal(line, dialog.Status);
                Assert.Equal(AppLoadStatusKind.Refused, dialog.StatusKind);
                Assert.Equal(line, model.History.Last());
            }

            Assert.Empty(dialog.LoadedRows);
            Assert.Equal(refused.Length, dialog.HistoryRows.Count);
        }

        [Fact]
        public void AnyOtherFile_IsSaidToBeNeitherAnAutoLispFileNorAPlugin()
        {
            Lisp("readme.txt", "hello");
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            dialog.FileName = "readme.txt";

            dialog.Load();

            Assert.Equal("readme.txt is not an AutoLISP file or a plugin.", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Refused, dialog.StatusKind);
            Assert.Equal("readme.txt is not an AutoLISP file or a plugin.", model.History.Last());
        }

        [Fact]
        public void ANameNoPathCanHold_IsRefusedAndNotKept()
        {
            AppLoadViewModel dialog = Shown(Opened());
            dialog.FileName = "a|b.lsp";

            dialog.Load();

            Assert.Equal("a|b.lsp is not a valid file name.", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Refused, dialog.StatusKind);
            Assert.Empty(dialog.HistoryRows);
        }

        [Fact]
        public void ADllWithNoPluginManager_SaysSoInRed()
        {
            Lisp("tool.dll", "x");
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            dialog.FileName = "tool.dll";

            dialog.Load();

            Assert.Equal("No plugin manager is in use: applications cannot be loaded.", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Failed, dialog.StatusKind);
            Assert.Equal(dialog.Status, model.History.Last());
        }

        // ---- History

        [Fact]
        public void TheHistory_HasEachFileGivenToLoadFirst_ALoadedAgainMovesUp_AndKeepsFiftyAtMost()
        {
            string a = Lisp("a.lsp", "");
            string b = Lisp("b.lsp", "");
            AppLoadViewModel dialog = Shown(Opened());

            foreach (string name in new[] { "a.lsp", "b.lsp", "a.lsp" })
            {
                dialog.FileName = name;
                dialog.Load();
            }

            Assert.Equal(new[] { a, b }, dialog.HistoryRows.Select(r => r.FullPath));
            Assert.Equal(new[] { a, b }, Saved().AppLoadHistory);

            for (int i = 0; i < 60; i++)
            {
                dialog.FileName = "f" + i + ".lsp";
                dialog.Load();
            }

            Assert.Equal(GlobalSettings.AppLoadHistoryLimit, dialog.HistoryRows.Count);
            Assert.Equal("f59.lsp", dialog.HistoryRows[0].File);
            Assert.Equal("f10.lsp", dialog.HistoryRows[49].File);
        }

        [Fact]
        public void WithAddToHistoryUnticked_NothingIsKept_AndTheChoiceIsKeptInTheSettings()
        {
            Lisp("a.lsp", "");
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);
            Assert.True(dialog.AddToHistory);

            dialog.AddToHistory = false;
            dialog.FileName = "a.lsp";
            dialog.Load();

            Assert.Empty(dialog.HistoryRows);
            Assert.False(Saved().AppLoadAddToHistory);
            Assert.Empty(Saved().AppLoadHistory);
            Assert.False(Shown(model).AddToHistory);
        }

        [Fact]
        public void TheHistoryListTab_ShowsTheHistoryWithItsKinds_AndLoadLoadsTheRowsChosen()
        {
            string a = Lisp("a.lsp", "(setq gha 1)");
            string b = Lisp("b.lsp", "(setq ghb 1)");
            string dll = Lisp("c.dll", "x");
            MainViewModel model = Opened(s => s.AppLoadHistory = new[] { dll, b, a });
            AppLoadViewModel dialog = Shown(model);
            Assert.False(dialog.IsHistoryTab);
            Assert.True(dialog.IsLoadedTab);
            Assert.Empty(dialog.Rows);

            dialog.IsHistoryTab = true;

            Assert.Same(dialog.HistoryRows, dialog.Rows);
            Assert.Equal(new[] { "Plugin", "AutoLISP", "AutoLISP" }, dialog.Rows.Select(r => r.Kind));
            Assert.False(dialog.CanLoad);

            dialog.ChooseRows(new[] { dialog.Rows[2], dialog.Rows[1] });
            Assert.True(dialog.CanLoad);
            dialog.Load();

            Assert.Equal(new[] { b, a }, dialog.LoadedRows.Select(r => r.FullPath));
            Assert.Equal(1, model.Session.Lisp.GetValue("GHA"));
            Assert.Equal(1, model.Session.Lisp.GetValue("GHB"));
            Assert.Equal(AppLoadStatusKind.Loaded, dialog.StatusKind);
        }

        [Fact]
        public void RemoveOnTheHistoryList_TakesTheRowsChosenOutOfTheListOnly_NeverOffTheDisk()
        {
            string a = Lisp("a.lsp", "");
            string b = Lisp("b.lsp", "");
            string c = Lisp("c.lsp", "");
            AppLoadViewModel dialog = Shown(Opened(s => s.AppLoadHistory = new[] { a, b, c }));
            dialog.IsHistoryTab = true;
            Assert.Equal("Remove", dialog.ActionLabel);
            Assert.False(dialog.CanAct);

            dialog.ChooseRows(new[] { dialog.Rows[0], dialog.Rows[2] });
            Assert.True(dialog.CanAct);
            Assert.Equal("Take the file out of the History list; the file stays on the disk", dialog.ActionToolTip);
            dialog.Act();

            Assert.Equal(new[] { b }, dialog.HistoryRows.Select(r => r.FullPath));
            Assert.Equal(new[] { b }, Saved().AppLoadHistory);
            Assert.True(File.Exists(a));
            Assert.True(File.Exists(c));
            Assert.False(dialog.CanAct);
        }

        // ---- Loaded Applications and Unload

        [Fact]
        public void TheLoadedApplications_ListTheFilesLoadedByLoadByTheLoadFunctionAndByTheStartupFiles()
        {
            string support = Directory.CreateDirectory(Path.Combine(_folder.FullPath, "support")).FullName;
            string acaddoc = Path.Combine(support, "acaddoc.lsp");
            File.WriteAllText(acaddoc, "(setq ghd 1)");
            string suite = Lisp("suite.lsp", "(setq ghs 1)");
            string typed = Lisp("typed.lsp", "(setq ght 1)");
            string tools = Lisp("tools.lsp", "(setq ghl 1)");
            MainViewModel model = Opened(s =>
            {
                s.SupportSearchFolders = new[] { support, Browsed };
                s.LispStartupSuite = new[] { suite };
            });
            model.Execute("(load \"typed\")");
            AppLoadViewModel dialog = Shown(model);
            dialog.FileName = "tools.lsp";
            dialog.Load();

            Assert.Equal(new[] { acaddoc, suite, typed, tools }, dialog.LoadedRows.Select(r => r.FullPath));
            Assert.All(dialog.LoadedRows, r => Assert.Equal("AutoLISP", r.Kind));
        }

        [Fact]
        public void TheLoadedApplications_AreThoseOfTheDrawingShown()
        {
            string tools = Lisp("tools.lsp", "(setq ghl 1)");
            MainViewModel model = Opened();
            AppLoadViewModel first = Shown(model);
            first.FileName = "tools.lsp";
            first.Load();
            Assert.Equal(new[] { tools }, first.LoadedRows.Select(r => r.FullPath));

            string room = _folder.Write("second.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));

            Assert.Empty(Shown(model).LoadedRows);
        }

        [Fact]
        public void UnloadIsGreyedOnAnAutoLispRow_WithTheTooltipThatSaysWhy()
        {
            Lisp("tools.lsp", "(setq ghl 1)");
            AppLoadViewModel dialog = Shown(Opened());
            dialog.FileName = "tools.lsp";
            dialog.Load();
            Assert.Equal("Unload", dialog.ActionLabel);
            Assert.False(dialog.CanAct);

            dialog.ChooseRows(dialog.Rows);

            Assert.False(dialog.CanAct);
            Assert.Equal(
                "AutoLISP files cannot be unloaded: what they defined stays in the drawing until it is closed",
                dialog.ActionToolTip
            );

            dialog.Act();

            Assert.Single(dialog.LoadedRows);
            Assert.Equal("tools.lsp successfully loaded.", dialog.Status);
        }

        private MainViewModel OpenedWithPlugins(out string sample)
        {
            var manager = new CadPluginManager(
                new CadPluginAssemblyLoader(),
                Path.Combine(_folder.FullPath, "plugins"),
                Path.Combine(_folder.FullPath, "plugins.json"),
                Path.Combine(_folder.FullPath, "copies")
            );
            _managers.Add(manager);
            string dev = Path.Combine(_folder.FullPath, "dev");
            Directory.CreateDirectory(dev);
            sample = Path.Combine(dev, SampleName + ".dll");
            File.Copy(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePlugin", SampleName + ".dll"),
                sample,
                true
            );
            manager.AddTrustedFolder(dev);
            MainViewModel model = Opened();
            model.UsePlugins(manager, loadAtStart: false);
            return model;
        }

        [Fact]
        public void APluginLoaded_IsAPluginRowInBlue_AndUnloadSwitchesItOffAsThePluginManagerDoes()
        {
            MainViewModel model = OpenedWithPlugins(out string sample);
            AppLoadViewModel dialog = Shown(model);
            dialog.FileType = dialog.FileTypes[1];
            dialog.FileName = sample;

            dialog.Load();

            Assert.Equal(AppLoadStatusKind.Loaded, dialog.StatusKind);
            Assert.Equal(model.History.Last(), dialog.Status);
            AppLoadRow row = Assert.Single(dialog.LoadedRows);
            Assert.Equal("Plugin", row.Kind);
            Assert.True(row.IsPlugin);
            Assert.False(row.IsOff);
            Assert.Equal(SampleName + ".dll", row.File);
            Assert.Equal(Path.GetDirectoryName(sample), row.Folder);

            dialog.ChooseRows(new[] { row });
            Assert.True(dialog.CanAct);
            Assert.Equal("Unload the plugin, as the Plugin manager does", dialog.ActionToolTip);

            dialog.Act();

            Assert.Equal(AppLoadStatusKind.Info, dialog.StatusKind);
            Assert.Matches("^(Unloaded|Switched) ", dialog.Status);
            Assert.Equal(model.History.Last(), dialog.Status);
            AppLoadRow off = Assert.Single(dialog.LoadedRows);
            Assert.True(off.IsOff);
            Assert.False(model.Plugins.Plugins.Single().IsLoaded);

            dialog.ChooseRows(new[] { off });
            Assert.False(dialog.CanAct);
        }

        // ---- The Startup Suite

        [Fact]
        public void AnAutoLispFileDroppedOnTheBriefcase_JoinsTheStartupSuite_Saved_AndLoadsIntoDrawingsOpenedAfterwards()
        {
            string suite = Lisp("suite.lsp", "(princ \"[suite]\")");
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Shown(model);

            dialog.AddToStartupSuite(new[] { suite, Path.Combine(Browsed.ToUpperInvariant(), "SUITE.LSP") });

            Assert.Equal(new[] { suite }, dialog.StartupSuite.Files);
            Assert.Equal(new[] { suite }, Saved().LispStartupSuite);
            Assert.Equal("suite.lsp added to the Startup Suite.", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Info, dialog.StatusKind);
            Assert.DoesNotContain("[suite]", model.History);

            Assert.True(model.Open(_folder.Write("next.dxf", TestFiles.Room(out _, out _))));

            Assert.Contains("[suite]", model.History);
        }

        [Fact]
        public void ADllOrAnyOtherFileDroppedOnTheBriefcase_IsRefusedWithWhy()
        {
            AppLoadViewModel dialog = Shown(Opened());

            dialog.AddToStartupSuite(new[] { Path.Combine(Browsed, "tool.dll") });

            Assert.Equal("Plugins load at start from the Plugin manager (At start).", dialog.Status);
            Assert.Equal(AppLoadStatusKind.Refused, dialog.StatusKind);

            dialog.AddToStartupSuite(new[] { Path.Combine(Browsed, "tools.fas") });

            Assert.Equal("tools.fas is not an AutoLISP file: the Startup Suite holds .lsp files only.", dialog.Status);
            Assert.Empty(dialog.StartupSuite.Files);
            Assert.Empty(Saved().LispStartupSuite);
        }

        [Fact]
        public void TheStartupSuiteWindow_AddsRemovesAndKeepsTheOrder()
        {
            string a = Lisp("a.lsp", "");
            string b = Lisp("b.lsp", "");
            string c = Lisp("c.lsp", "");
            MainViewModel model = Opened(s => s.LispStartupSuite = new[] { a });
            StartupSuiteViewModel suite = Shown(model).StartupSuite;
            Assert.False(suite.IsEmpty);
            Assert.False(suite.CanRemove);

            suite.Add();
            Assert.Equal(new[] { a }, suite.Files);

            suite.AskFiles = () => new[] { c, b, a };
            suite.Add();

            Assert.Equal(new[] { a, c, b }, suite.Files);
            Assert.Equal(new[] { a, c, b }, Saved().LispStartupSuite);

            suite.Selected = c;
            Assert.True(suite.CanRemove);
            suite.Remove();

            Assert.Equal(new[] { a, b }, suite.Files);
            Assert.Equal(new[] { a, b }, Saved().LispStartupSuite);
            Assert.False(suite.CanRemove);

            suite.Selected = a;
            suite.Remove();
            suite.Selected = b;
            suite.Remove();

            Assert.True(suite.IsEmpty);
            Assert.Empty(Saved().LispStartupSuite);
        }

        [Fact]
        public void TheStartupSuiteWindow_AFileThatIsNotAutoLisp_IsRefusedWithAMessageThatTheNextAddClears()
        {
            string a = Lisp("a.lsp", "");
            StartupSuiteViewModel suite = Shown(Opened()).StartupSuite;

            Assert.Equal(1, suite.Add(new[] { a, Path.Combine(Browsed, "x.dll") }));
            Assert.Equal("Plugins load at start from the Plugin manager (At start).", suite.Message);

            Assert.Equal(0, suite.Add(new[] { a }));
            Assert.Equal(string.Empty, suite.Message);
            Assert.Equal(new[] { a }, suite.Files);
        }

        [Fact]
        public void ContentsShowsTheStartupSuiteOverTheDialog()
        {
            AppLoadViewModel dialog = Shown(Opened());
            StartupSuiteViewModel shown = null;
            dialog.ShowStartupSuite = suite => shown = suite;

            dialog.ShowContents();

            Assert.Same(dialog.StartupSuite, shown);
        }

        [Fact]
        public void HelpOpensTheGuidePageOfAutoLisp_AtAppLoadAndAtTheStartupSuite()
        {
            AppLoadViewModel dialog = Shown(Opened());

            Assert.Equal(
                "https://nguyenthanguth.github.io/GeometryHelper/guides/presenter/lisp.html#appload",
                dialog.HelpUrl
            );
            Assert.Equal(
                "https://nguyenthanguth.github.io/GeometryHelper/guides/presenter/lisp.html#the-startup-suite",
                dialog.StartupSuite.HelpUrl
            );
        }

        // ---- Where APPLOAD goes

        [Fact]
        public void AppLoad_ShowsTheDialogOnce_AndCancellingItWritesNothing()
        {
            MainViewModel model = Opened();
            int shown = 0;
            model.ShowAppLoad = dialog =>
            {
                shown++;
                return false;
            };
            int before = model.History.Count;

            model.StartCommand("APPLOAD");

            Assert.Equal(1, shown);
            Assert.Equal(before + 1, model.History.Count);
            Assert.False(model.IsCommandRunning);
        }

        [Fact]
        public void LoadApplication_IsAvailableOnceTheWindowCanShowTheDialog()
        {
            MainViewModel model = Opened();
            var ribbon = (ManageRibbonViewModel)model.ManageTab.TabContent;
            Assert.False(ribbon.CanLoadApplication);

            model.ShowAppLoad = dialog => true;

            Assert.True(ribbon.CanLoadApplication);
        }

        // ---- The windows

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;
                    test();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static IEnumerable<DependencyObject> Logical(DependencyObject parent)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is DependencyObject element)
                {
                    yield return element;

                    foreach (DependencyObject below in Logical(element))
                    {
                        yield return below;
                    }
                }
            }
        }

        private static Dictionary<string, FrameworkElement> Named(FrameworkElement body) =>
            Logical(body)
                .OfType<FrameworkElement>()
                .Where(e => !string.IsNullOrEmpty(AutomationProperties.GetName(e)))
                .ToDictionary(AutomationProperties.GetName);

        private static void Layout(FrameworkElement body, double width, double height)
        {
            body.Measure(new Size(width, height));
            body.Arrange(new Rect(0, 0, width, height));
            body.UpdateLayout();
        }

        [Fact]
        public void TheAppLoadWindow_HasEveryControlOfTheDesign_LoadGreyedUntilAFileIsNamed_AndTheButtonFollowingTheTab()
        {
            Lisp("tools.lsp", "(setq ghl 1)");
            string[] expected =
            {
                "Look in",
                "Up one level",
                "Back",
                "Files",
                "File name",
                "Load",
                "Files of type",
                "Add to History",
                "Loaded Applications",
                "History list",
                "Applications",
                "Unload or Remove",
                "Startup Suite briefcase",
                "Contents",
                "Help",
                "Status",
            };
            MainViewModel model = Opened();

            OnSta(() =>
            {
                var window = new AppLoadWindow(new AppLoadViewModel(model));
                Layout(window.Body, 720, 600);
                Dictionary<string, FrameworkElement> named = Named(window.Body);

                Assert.Equal(
                    expected.OrderBy(n => n, StringComparer.Ordinal),
                    named.Keys.OrderBy(n => n, StringComparer.Ordinal)
                );
                Assert.False(named["Load"].IsEnabled);
                Assert.False(named["Back"].IsEnabled);
                Assert.True(named["Up one level"].IsEnabled);
                Assert.False(named["Unload or Remove"].IsEnabled);
                Assert.Equal("Unload", ((Button)named["Unload or Remove"]).Content);
                Assert.True(((CheckBox)named["Add to History"]).IsChecked);

                window.Model.FileName = "tools.lsp";
                Layout(window.Body, 720, 600);
                Assert.True(named["Load"].IsEnabled);

                window.Model.IsHistoryTab = true;
                Layout(window.Body, 720, 600);
                Assert.Equal("Remove", ((Button)named["Unload or Remove"]).Content);
                Assert.False(named["Load"].IsEnabled);
                Assert.True(((RadioButton)named["History list"]).IsChecked);

                window.Model.IsHistoryTab = false;
                Layout(window.Body, 640, 540);
                Assert.True(((RadioButton)named["Loaded Applications"]).IsChecked);
                Assert.Single(((ListBox)named["Files"]).Items);
            });
        }

        [Fact]
        public void ChoosingAFolderInTheLookInBox_OpensItAndTheBoxFollowsToTheNewFolder()
        {
            Lisp("tools.lsp", "(setq ghl 1)");
            MainViewModel model = Opened();

            OnSta(() =>
            {
                var window = new AppLoadWindow(new AppLoadViewModel(model));
                Layout(window.Body, 720, 600);
                var look = (ComboBox)Named(window.Body)["Look in"];
                var files = (ListBox)Named(window.Body)["Files"];
                Assert.Equal(Browsed, look.SelectedItem);
                Assert.Single(files.Items);
                string parent = Path.GetDirectoryName(Browsed);

                look.SelectedItem = parent;
                Layout(window.Body, 720, 600);

                Assert.Equal(parent, window.Model.Folder);
                Assert.Equal(parent, look.SelectedItem);
                Assert.Equal(window.Model.FolderChain, look.Items.Cast<string>());
                Assert.Contains("lisp", files.Items.Cast<AppLoadEntry>().Select(e => e.Name));
            });
        }

        [Fact]
        public void TheAppLoadWindow_ShowsTheStatusInTheColourOfItsKind()
        {
            Lisp("tools.lsp", "(setq ghl 1)");
            Lisp("tools.fas", "x");
            Lisp("bad.lsp", "(car 1)");
            MainViewModel model = Opened();
            var dialog = new AppLoadViewModel(model);
            dialog.FileType = dialog.FileTypes[2];

            OnSta(() =>
            {
                var window = new AppLoadWindow(dialog);
                Layout(window.Body, 720, 600);
                var status = (TextBlock)Named(window.Body)["Status"];

                dialog.FileName = "tools.lsp";
                dialog.Load();
                Layout(window.Body, 720, 600);
                Assert.Equal("tools.lsp successfully loaded.", status.Text);
                Assert.Equal("#FF3FB950", ((System.Windows.Media.SolidColorBrush)status.Foreground).Color.ToString());

                dialog.FileName = "tools.fas";
                dialog.Load();
                Layout(window.Body, 720, 600);
                Assert.Equal("#FFD29922", ((System.Windows.Media.SolidColorBrush)status.Foreground).Color.ToString());

                dialog.FileName = "bad.lsp";
                dialog.Load();
                Layout(window.Body, 720, 600);
                Assert.Equal("#FFFF8A7A", ((System.Windows.Media.SolidColorBrush)status.Foreground).Color.ToString());
            });
        }

        [Fact]
        public void TheStartupSuiteWindow_HasTheListAddRemoveCloseAndHelp_TheEmptyWordsShownOnlyWithNoFile()
        {
            string a = Lisp("a.lsp", "");
            MainViewModel model = Opened();
            StartupSuiteViewModel suite = Shown(model).StartupSuite;

            OnSta(() =>
            {
                var window = new StartupSuiteWindow(suite);
                Layout(window.Body, 460, 360);
                Dictionary<string, FrameworkElement> named = Named(window.Body);

                Assert.Equal(
                    new[] { "Add", "Help", "List of applications", "Remove" },
                    named.Keys.OrderBy(n => n, StringComparer.Ordinal)
                );
                Assert.False(named["Remove"].IsEnabled);
                TextBlock empty = Logical(window.Body)
                    .OfType<TextBlock>()
                    .Single(t => t.Text.StartsWith("No file yet.", StringComparison.Ordinal));
                Assert.Equal(Visibility.Visible, empty.Visibility);

                suite.Add(new[] { a });
                Layout(window.Body, 460, 360);

                Assert.Equal(Visibility.Collapsed, empty.Visibility);
                Assert.Single(((ListBox)named["List of applications"]).Items);

                suite.Selected = a;
                Layout(window.Body, 460, 360);
                Assert.True(named["Remove"].IsEnabled);
            });
        }
    }
}
