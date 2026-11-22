using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The load-result line of Load/Unload Applications: red, above the hint, hidden until a load fails, naming the last file
    /// that failed with the error the command history wrote. Driven with no window through the view model; the last tests lay
    /// the window out offscreen.
    /// </summary>
    public sealed class AppLoadErrorLineTests : IDisposable
    {
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

        private MainViewModel Opened()
        {
            string room = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.LoadSecurity.SecureLoad = 0;
            GlobalSettings changed = model.Settings.Clone();
            changed.SecureLoad = 0;
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

        /// <summary>A manager over the loader of the runtime, with a folder it trusts for the files of the test.</summary>
        private CadPluginManager Manager(out string dev)
        {
            var manager = new CadPluginManager(
                new CadPluginAssemblyLoader(),
                Path.Combine(_folder.FullPath, "plugins"),
                Path.Combine(_folder.FullPath, "plugins.json"),
                Path.Combine(_folder.FullPath, "copies")
            );
            _disposables.Add(manager);
            dev = Path.Combine(_folder.FullPath, "dev");
            Directory.CreateDirectory(dev);
            manager.AddTrustedFolder(dev);
            return manager;
        }

        private const string ErrOne = "(defun c:ERRONE () 1)\n(car 5)";

        [Fact]
        public void TheLine_StartsHidden_AndASuccessfulLoadDoesNotShowIt()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            Assert.False(dialog.HasLoadError);
            Assert.Equal(string.Empty, dialog.LoadError);

            dialog.Load(new[] { Lisp("fine.lsp", "(defun c:FINE () 1)") });

            Assert.False(dialog.HasLoadError);
            Assert.Equal(string.Empty, dialog.LoadError);
        }

        [Fact]
        public void ALspThatFailsAfterDefiningACommand_ShowsItsErrorAndTheCommand()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { Lisp("err.lsp", ErrOne) });

            string error = model.History.Last(l => l.StartsWith("; error:", StringComparison.Ordinal));
            Assert.Contains("bad argument type", error);
            Assert.True(dialog.HasLoadError);
            Assert.Equal("err.lsp", dialog.LoadErrorName);
            Assert.Equal(" failed to load: " + error + " (ERRONE was defined before the error)", dialog.LoadErrorText);
        }

        [Fact]
        public void ALspThatFailsBeforeDefiningAnything_NamesNoCommand()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { Lisp("early.lsp", "(car 5)\n(defun c:LATE () 1)") });

            string error = model.History.Last(l => l.StartsWith("; error:", StringComparison.Ordinal));
            Assert.Equal("early.lsp failed to load: " + error, dialog.LoadError);
        }

        [Fact]
        public void ALspThatFailsAfterTwoCommands_NamesBothWithWere()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { Lisp("two.lsp", "(defun c:AA () 1)\n(defun c:BB () 1)\n(car 5)") });

            Assert.EndsWith(" (AA, BB were defined before the error)", dialog.LoadError);
        }

        [Fact]
        public void TheNextGoodLoad_HidesTheLine()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            dialog.Load(new[] { Lisp("err.lsp", ErrOne) });
            Assert.True(dialog.HasLoadError);
            var changed = new List<string>();
            dialog.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            dialog.Load(new[] { Lisp("good.lsp", "(defun c:GOOD () 1)") });

            Assert.False(dialog.HasLoadError);
            Assert.Equal(string.Empty, dialog.LoadError);
            Assert.Contains(nameof(AppLoadViewModel.HasLoadError), changed);
        }

        [Fact]
        public void LoadWithNoDrawingOpen_SaysToOpenADrawing()
        {
            var model = new MainViewModel { SettingsPath = SettingsFile };
            AppLoadViewModel dialog = Created(model);
            string path = Lisp("nodoc.lsp", "(setq a 1)");
            dialog.AskFiles = folder => new[] { path };

            dialog.LoadCommand.Execute(null);

            Assert.True(dialog.HasLoadError);
            Assert.Equal("Open a drawing to load nodoc.lsp.", dialog.LoadError);
            Assert.Contains("Open a drawing to load nodoc.lsp.", model.History);
        }

        [Fact]
        public void TwoFilesThatFail_ShowTheLastWithAndOneMore()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            string first = Lisp("first.lsp", "(car 5)");
            string second = Lisp("second.lsp", "(defun c:SECOND () 1)\n(car 6)");

            dialog.Load(new[] { first, second });

            Assert.Equal("second.lsp", dialog.LoadErrorName);
            Assert.EndsWith(" (SECOND was defined before the error) (and 1 more)", dialog.LoadError);
        }

        [Fact]
        public void AFailureAndAGoodFileInOneLoad_ShowTheFailure()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { Lisp("err.lsp", ErrOne), Lisp("good.lsp", "(defun c:GOOD () 1)") });

            Assert.Equal("err.lsp", dialog.LoadErrorName);
            Assert.DoesNotContain("more", dialog.LoadError);
        }

        [Fact]
        public void AFileTypeRefused_ShowsTheRefusal()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            string path = _folder.WriteText("old.arx", "x");

            dialog.Load(new[] { path });

            Assert.True(dialog.HasLoadError);
            Assert.Equal("old.arx", dialog.LoadErrorName);
            Assert.Equal(
                "old.arx cannot be loaded: .arx, .dbx, .dvb, .fas and .vlx files are not supported.",
                dialog.LoadError
            );
            Assert.Equal(dialog.LoadError, model.History.Last());
        }

        [Fact]
        public void AFileOfNeitherType_ShowsTheRefusalToo()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);

            dialog.Load(new[] { _folder.WriteText("notes.txt", "x") });

            Assert.Equal("notes.txt is not an AutoLISP file or a plugin.", dialog.LoadError);
        }

        [Fact]
        public void ADroppedLspThatFails_ShowsTheLine()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);

            dialog.LoadDropped(new[] { Lisp("err.lsp", ErrOne) });

            Assert.Equal("err.lsp", dialog.LoadErrorName);
            Assert.Contains(" failed to load: ; error:", dialog.LoadErrorText);
        }

        [Fact]
        public void ReloadOfAFileThatNowFails_ShowsTheLine_AndAGoodReloadHidesIt()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            string path = Lisp("edit.lsp", "(defun c:EDIT () 1)");
            dialog.Load(new[] { path });
            Assert.False(dialog.HasLoadError);
            File.WriteAllText(path, "(defun c:EDIT () 2)\n(car 5)");
            dialog.ChooseRows(dialog.Rows);

            dialog.Reload();

            Assert.True(dialog.HasLoadError);
            Assert.Equal("edit.lsp", dialog.LoadErrorName);
            Assert.EndsWith(" (EDIT was defined before the error)", dialog.LoadError);

            File.WriteAllText(path, "(defun c:EDIT () 3)");
            dialog.ChooseRows(dialog.Rows);
            dialog.Reload();

            Assert.False(dialog.HasLoadError);
        }

        [Fact]
        public void TickingOnAFileThatNowFails_ShowsTheLine()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            string path = Lisp("tick.lsp", "(defun c:TICK () 1)");
            dialog.Load(new[] { path });
            AppLoadRow row = Assert.Single(dialog.Rows);
            row.IsOn = false;
            File.WriteAllText(path, "(car 5)");

            dialog.Rows.Single().IsOn = true;

            Assert.Equal("tick.lsp", dialog.LoadErrorName);
            Assert.Contains(" failed to load: ; error:", dialog.LoadErrorText);
        }

        [Fact]
        public void ADllThatFailsToLoad_ShowsWhatThePluginManagerSaid()
        {
            CadPluginManager manager = Manager(out string dev);
            var model = new MainViewModel { SettingsPath = SettingsFile };
            model.UsePlugins(manager, loadAtStart: false);
            model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _)));
            AppLoadViewModel dialog = Created(model);
            string path = Path.Combine(dev, "broken.dll");
            File.WriteAllText(path, "this is not an assembly");

            dialog.Load(new[] { path });

            Assert.True(dialog.HasLoadError);
            Assert.Equal("broken.dll", dialog.LoadErrorName);
            Assert.Equal(" failed to load: " + model.History.Last(), dialog.LoadErrorText);
        }

        [Fact]
        public void TheWindow_ShowsTheLineRedWithTheNameInBold_OnlyAfterAFailure()
        {
            MainViewModel model = Opened();
            AppLoadViewModel dialog = Created(model);
            string path = Lisp("err.lsp", ErrOne);
            Visibility before = default;
            Visibility after = default;
            Visibility hidden = default;
            string line = null;
            string expected = null;
            string boldName = null;
            string hint = null;
            System.Windows.Media.Color fill = default;
            System.Windows.Media.Color border = default;

            OnSta(() =>
            {
                var window = new AppLoadWindow(dialog);
                FrameworkElement root = window.Body;
                window.Content = null;
                root.DataContext = dialog;
                Layout(root);
                var box = Named<Border>(root, "LoadErrorBox");
                before = box.Visibility;

                dialog.Load(new[] { path });
                Layout(root);
                after = box.Visibility;
                expected = dialog.LoadError;
                var text = Named<TextBlock>(root, "LoadErrorLine");
                line = string.Concat(text.Inlines.OfType<Run>().Select(r => r.Text));
                boldName = text.Inlines.OfType<Run>().First().Text;
                hint = Named<TextBlock>(root, "LoadErrorHint").Text;
                fill = ((System.Windows.Media.SolidColorBrush)box.Background).Color;
                border = ((System.Windows.Media.SolidColorBrush)box.BorderBrush).Color;

                dialog.Load(new[] { Lisp("good.lsp", "(defun c:GOOD () 1)") });
                Layout(root);
                hidden = box.Visibility;
                window.Close();
            });

            Assert.Equal(Visibility.Collapsed, before);
            Assert.Equal(Visibility.Visible, after);
            Assert.Equal(Visibility.Collapsed, hidden);
            Assert.Equal(expected, line);
            Assert.StartsWith("err.lsp failed to load: ; error:", line);
            Assert.Equal("err.lsp", boldName);
            Assert.Equal("Shown until the next load", hint);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0x3A, 0x1D, 0x1D), fill);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0xF8, 0x51, 0x49), border);
        }

        private static T Named<T>(DependencyObject root, string name)
            where T : FrameworkElement => Descendants(root).OfType<T>().Single(e => e.Name == name);

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        private static void Layout(FrameworkElement root)
        {
            root.Measure(new Size(900, 700));
            root.Arrange(new Rect(0, 0, 900, 700));
            root.UpdateLayout();
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
