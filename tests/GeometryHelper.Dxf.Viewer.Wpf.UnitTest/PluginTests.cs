using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Command plugins in the window, with no window: the sample plugin loaded through the view model, its command on the
    /// Plugins tab and the command line, the Plugin manager and a plugin's settings.
    /// </summary>
    public sealed class PluginTests : IDisposable
    {
        private const string SampleName = "GeometryHelper.Dxf.Viewer.SamplePlugin";
        private readonly TestFolder _folder = new TestFolder();
        private readonly CadPluginManager _manager;
        private readonly string _sample;

        public PluginTests()
        {
            _manager = new CadPluginManager(
                new CadPluginAssemblyLoader(),
                Path.Combine(_folder.FullPath, "plugins"),
                Path.Combine(_folder.FullPath, "plugins.json"),
                Path.Combine(_folder.FullPath, "copies")
            );
            string dev = Path.Combine(_folder.FullPath, "dev");
            Directory.CreateDirectory(dev);
            _sample = Path.Combine(dev, SampleName + ".dll");
            File.Copy(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePlugin", SampleName + ".dll"),
                _sample,
                true
            );
            _manager.AddTrustedFolder(dev);
        }

        public void Dispose()
        {
            _manager.Dispose();
            _folder.Dispose();
        }

        [Fact]
        public void TheSamplePutsNumCirclesOnThePluginsTabThatRunsOnTheCommandLineUndoesAndGoesWhenUnloaded()
        {
            var model = new MainViewModel();
            model.UsePlugins(_manager, loadAtStart: false);
            model.OpenDocument(Circles());
            Assert.False(model.PluginRibbon.HasCommands);

            CadPluginResult loaded = model.LoadPlugin(_sample);

            Assert.True(loaded.Succeeded, loaded.ToString());
            Assert.Contains(loaded.ToString(), model.History);
            Assert.Equal(new[] { "FAILNOW", "NUMCIRCLES" }, model.PluginRibbon.Commands.Select(b => b.Name));
            PluginCommandButton button = model.PluginRibbon.Commands.Single(b => b.Name == "NUMCIRCLES");
            Assert.Equal("NC", button.Alias);
            Assert.Equal("NUMCIRCLES", button.Label);
            Assert.Contains("Number the selected circles", button.ToolTip);

            model.SelectedTab = model.PluginsTab;
            Assert.True(model.PluginsTab.IsTabSelected);
            Assert.Same(model.PluginRibbon, model.PluginsTab.TabContent);
            model.PluginRibbon.RunCommand.Execute(button);
            Assert.StartsWith("Select objects", model.Prompt);
            model.Session.FeedSelection(
                model.Document.Database.ModelSpace.Cast<DxfEntity>().Select(e => e.Handle).ToList()
            );
            model.Enter();
            Assert.StartsWith("Start at", model.Prompt);
            model.Execute("7");

            Assert.Equal(new[] { "7", "8", "9" }, Texts(model).Select(t => t.TextString));

            Assert.True(model.Undo());
            Assert.Empty(Texts(model));

            model.Execute("NC");
            model.Escape();
            Assert.Contains("NUMCIRCLES", model.History);

            CadPluginResult unloaded = model.UnloadPlugin(loaded.Plugin.Name);

            Assert.True(unloaded.Succeeded, unloaded.ToString());
            Assert.Empty(model.PluginRibbon.Commands);
            Assert.Null(model.Session.Registry.Find("NUMCIRCLES"));
        }

        [Fact]
        public void PluginLoadTypedAsksTheWindowForItsFileDialogAndTheManagersCommandsAreThere()
        {
            var model = new MainViewModel();
            int asked = 0;
            model.PluginLoadRequested += (s, e) => asked++;
            model.UsePlugins(_manager, loadAtStart: false);
            model.OpenDocument(Circles());

            model.Execute("NETLOAD");

            Assert.Equal(1, asked);
            Assert.NotNull(model.Session.Registry.Find(CadPluginManager.UnloadCommand));
            Assert.NotNull(model.Session.Registry.Find(CadPluginManager.ReloadCommand));
        }

        [Fact]
        public void APluginLoadedBeforeADocumentIsOpenedIsOnItsCommandLineAndLetGoWhenItIsReplaced()
        {
            var model = new MainViewModel();
            model.UsePlugins(_manager, loadAtStart: false);
            Assert.True(model.LoadPlugin(_sample).Succeeded);

            model.OpenDocument(Circles(), "drawing");
            CadCommandRegistry first = model.Session.Registry;
            Assert.NotNull(first.Find("NUMCIRCLES"));

            model.OpenDocument(Circles(), "drawing");

            Assert.Null(first.Find("NUMCIRCLES"));
            Assert.NotNull(model.Session.Registry.Find("NUMCIRCLES"));
        }

        [Fact]
        public void TheManagerListsTheSampleUnloadsItFromItsOnBoxAndTrustsAFolder()
        {
            Assert.True(_manager.Load(_sample).Succeeded);

            using (var manager = new PluginManagerViewModel(_manager))
            {
                PluginRowViewModel row = Assert.Single(manager.Rows);
                Assert.Same(row, manager.Selected);
                Assert.Equal("Command", row.Kind);
                Assert.Equal("FAILNOW, NUMCIRCLES", row.Commands);
                Assert.Equal("Yes", row.Trusted);
                Assert.True(row.IsLoaded);
                Assert.Equal("All 1", manager.AllChipText);
                Assert.Equal("Command plugins 1", manager.CommandChipText);
                Assert.Equal("Drawing plugins 0", manager.DrawingChipText);
                Assert.Contains("NUMCIRCLES (NC): Number the selected circles", manager.SelectedCommands);
                Assert.True(manager.HasSettings);
                Assert.NotEmpty(manager.LogLines);

                manager.ToggleLoadedCommand.Execute(row);

                Assert.False(manager.Rows.Single().IsLoaded);
                Assert.False(manager.MessageFailed);
                Assert.Contains(SampleName, manager.Message);
                Assert.True(manager.LogLines.Count >= 2);

                string folder = Path.Combine(_folder.FullPath, "company");
                Directory.CreateDirectory(folder);
                Assert.True(manager.AddFolder(folder));
                Assert.Contains(manager.TrustedFolders, f => f.EndsWith("company", StringComparison.OrdinalIgnoreCase));
                manager.SelectedFolder = manager.TrustedFolders.Last();
                manager.RemoveFolderCommand.Execute(null);
                Assert.DoesNotContain(manager.TrustedFolders, f =>
                    f.EndsWith("company", StringComparison.OrdinalIgnoreCase)
                );
                manager.SelectedFolder = manager.TrustedFolders[0];
                Assert.False(manager.RemoveFolderCommand.CanExecute(null));
            }
        }

        [Fact]
        public void ADrawingPluginIsRefusedWithTheManagersReason()
        {
            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(_sample), "plugin.json"),
                "{ \"id\": \"bolt-dims\", \"name\": \"Bolt dimensions\", \"version\": \"1.2.0\", \"author\": \"GeometryHelper\", \"kind\": \"drawing\", \"minimumApiVersion\": \"1.0\", \"commands\": [ \"BOLTDIM\" ] }"
            );

            using (var manager = new PluginManagerViewModel(_manager))
            {
                CadPluginResult result = manager.Load(_sample);

                Assert.False(result.Succeeded);
                Assert.True(manager.MessageFailed);
                Assert.Contains(
                    "is of the kind \"drawing\", and this presenter loads command plugins only",
                    manager.Message
                );
            }
        }

        [Fact]
        public void APluginsSettingsRefuseATextHeightBelowItsMinimumAndSayWhy()
        {
            Assert.True(_manager.Load(_sample).Succeeded);

            using (var manager = new PluginManagerViewModel(_manager))
            {
                PluginSettingsViewModel settings = manager.CreateSettings();
                PluginSettingRow height = settings.Rows.Single(r => r.Label == "Text height");
                Assert.True(height.IsNumber);
                Assert.Equal("2.5", height.Value);
                Assert.EndsWith("From 0.1 to 1000.", height.Description);

                height.Value = "0.05";

                Assert.True(height.HasError);
                Assert.Equal(
                    2.5,
                    (double)_manager.Plugins.Single().Plugin.Settings.Single(s => s.Label == "Text height").Value
                );

                height.Value = "5";

                Assert.False(height.HasError);
                Assert.Equal(
                    5.0,
                    (double)_manager.Plugins.Single().Plugin.Settings.Single(s => s.Label == "Text height").Value
                );
                Assert.True(settings.Rows.Single(r => r.Label == "Layer").IsText);
            }
        }

        [Fact]
        public void ThePluginManagerAndAPluginsSettingsDrawTheirRowsOffscreen()
        {
            Assert.True(_manager.Load(_sample).Succeeded);
            double settingsHeight = 0.0;
            int managerRows = 0;
            Exception failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    using (var manager = new PluginManagerViewModel(_manager))
                    {
                        var settings = new PluginSettingsWindow(manager.CreateSettings());
                        System.Windows.FrameworkElement body = settings.Body;
                        settings.Content = null;
                        body.DataContext = settings.Model;
                        body.Measure(new System.Windows.Size(460, double.PositiveInfinity));
                        settingsHeight = body.DesiredSize.Height;
                        settings.Close();

                        var window = new PluginManagerWindow(manager);
                        System.Windows.FrameworkElement root = window.Body;
                        window.Content = null;
                        root.DataContext = manager;
                        root.Measure(new System.Windows.Size(900, 700));
                        root.Arrange(new System.Windows.Rect(0, 0, 900, 700));
                        root.UpdateLayout();
                        managerRows = FindAll<System.Windows.Controls.ListBoxItem>(root)
                            .Count(i => i.DataContext is PluginRowViewModel);
                        window.Close();
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(failure);
            Assert.True(settingsHeight > 180.0, settingsHeight + " high");
            Assert.Equal(1, managerRows);
        }

        private static System.Collections.Generic.List<T> FindAll<T>(System.Windows.DependencyObject parent)
            where T : System.Windows.DependencyObject
        {
            var found = new System.Collections.Generic.List<T>();

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                System.Windows.DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

                if (child is T match)
                {
                    found.Add(match);
                }

                found.AddRange(FindAll<T>(child));
            }

            return found;
        }

        private static CadDocument Circles()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 10));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(50, 0, 0), GeoVector3.ZAxis, 10));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(100, 20, 0), GeoVector3.ZAxis, 10));
            return CadDocument.From(database, "circles.dxf");
        }

        private static System.Collections.Generic.List<DxfDBText> Texts(MainViewModel model) =>
            model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfDBText>().Where(t => !t.IsErased).ToList();
    }
}
