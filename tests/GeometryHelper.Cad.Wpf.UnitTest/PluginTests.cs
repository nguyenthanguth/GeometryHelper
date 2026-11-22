using System;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Command plugins in the window, with no window: the sample plugin loaded through the view model, its command on the
    /// Plugins tab and the command line, and the Load/Unload Applications table (see AppLoadWindowTests).
    /// </summary>
    public sealed class PluginTests : IDisposable
    {
        private const string SampleName = "GeometryHelper.Cad.SamplePlugin";
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
