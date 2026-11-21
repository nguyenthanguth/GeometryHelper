using System;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Plugins.UnitTest
{
    /// <summary>
    /// The sample that adds to the window, CircleUi, loaded from its .dll as a user loads a plugin: what it registered, read
    /// through the public members of <see cref="CadPluginHost"/> alone, and what unloading, reloading and a second copy do to
    /// it.
    /// </summary>
    public sealed class SampleUiPluginTests : IDisposable
    {
        private const string SampleUiName = "GeometryHelper.Cad.SampleUiPlugin";

        private readonly PluginFiles _files = new PluginFiles();
        private readonly CadPluginManager _manager;

        public SampleUiPluginTests()
        {
            _manager = _files.Manager(true);
        }

        public void Dispose()
        {
            _manager.Dispose();
            _files.Dispose();
        }

        private string Copy(string folder, string name = SampleUiName)
        {
            string built = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleUiPlugin", SampleUiName + ".dll");
            string dir = Path.Combine(_files.Root, folder);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".dll");
            File.Copy(built, path, true);
            return path;
        }

        private CadLoadedPlugin Load(string folder = "ui", string name = SampleUiName)
        {
            CadPluginResult result = _manager.Load(Copy(folder, name));
            Assert.True(result.Succeeded, result.ToString());
            return result.Plugin;
        }

        [Fact]
        public void TheSampleLoadsWithItsTabsPanelProvidersButtonsMenusAndSnap()
        {
            CadLoadedPlugin loaded = Load();
            CadPlugin plugin = loaded.Plugin;
            CadPluginHost host = plugin.Host;

            Assert.Equal(new Version(1, 1), plugin.RequiredApiVersion);
            Assert.Empty(plugin.Problems);
            Assert.Equal(new[] { "NUMCIRCLES2" }, plugin.Commands.Select(c => c.Name));
            Assert.Equal(Path.Combine(_files.Root, "ui"), host.Folder);
            Assert.Equal(SampleUiName, host.PluginName);
            Assert.True(host.ShowsCommandButtons);

            Assert.Equal(new[] { "Circles", "Circle" }, host.RibbonTabs.Select(t => t.Id));
            Assert.Null(host.RibbonTabs[0].ShowWhen);
            Assert.NotNull(host.RibbonTabs[1].ShowWhen);

            CadRibbonPanel number = Assert.Single(host.RibbonTabs[0].Panels);
            Assert.Equal("Number", number.Title);
            var large = Assert.IsType<CadRibbonButton>(number.Items[0]);
            Assert.Equal(CadRibbonItemSize.Large, large.Size);
            Assert.Equal("NUMCIRCLES2", large.Command);
            Assert.NotNull(large.Icon.PathData);
            var count = Assert.IsType<CadRibbonButton>(number.Items[1]);
            Assert.NotNull(count.Run);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, count.Icon.Image.Take(4));
            var sizes = Assert.IsType<CadRibbonSplitButton>(number.Items[3]);
            Assert.Equal(3, sizes.Buttons.Count);

            Assert.Equal("Circle tools", Assert.Single(host.RibbonPanels).Title);
            Assert.Single(host.PropertyProviders);
            Assert.Single(host.DrawingsButtons);
            Assert.Single(host.DrawingsMenus);
            Assert.Single(host.CanvasMenus);

            CadCustomSnap snap = Assert.Single(host.ObjectSnaps);
            Assert.Equal("Circle top", snap.Name);
            Assert.Equal("CTOP", snap.Keyword);
            Assert.Equal(typeof(DxfCircle), Assert.Single(snap.EntityTypes));
            Assert.True(Assert.Single(snap.Glyph).IsFilled);
        }

        [Fact]
        public void AddingToTheHostOfALoadedPlugin_Throws()
        {
            CadPluginHost host = Load().Plugin.Host;

            Assert.Throws<InvalidOperationException>(() => host.AddRibbonTab(new CadRibbonTab("Late", "Late")));
        }

        [Fact]
        public void UnloadingTakesThePluginOutOfTheLoadedOnes_AndReloadingGivesItAFreshHost()
        {
            CadLoadedPlugin loaded = Load();
            CadPluginHost before = loaded.Plugin.Host;
            int changes = 0;
            _manager.PluginsChanged += (s, e) => changes++;

            Assert.True(_manager.Unload(SampleUiName).Succeeded);
            Assert.Null(loaded.Plugin);
            Assert.Equal(1, changes);

            CadPluginResult reloaded = _manager.Reload(SampleUiName);

            Assert.True(reloaded.Succeeded, reloaded.ToString());
            Assert.Equal(2, changes);
            Assert.NotSame(before, reloaded.Plugin.Plugin.Host);
            Assert.Equal(new[] { "Circles", "Circle" }, reloaded.Plugin.Plugin.Host.RibbonTabs.Select(t => t.Id));
        }

        [Fact]
        public void ASecondCopyUnderAnotherName_LosesItsTabsAndSnapToTheFirstLoadedWithProblemLines()
        {
            Load("one", SampleUiName);
            CadPluginResult second = _manager.Load(Copy("two", "SampleUiCopy"));

            Assert.True(second.Succeeded, second.ToString());
            CadPlugin copy = second.Plugin.Plugin;
            Assert.Empty(copy.Host.RibbonTabs);
            Assert.Empty(copy.Host.ObjectSnaps);
            Assert.Contains(
                "The ribbon tab Circles of the plugin SampleUiCopy is left out: the plugin "
                    + SampleUiName
                    + " has a tab of that id.",
                copy.Problems
            );
            Assert.Contains(
                "The snap CTOP of the plugin SampleUiCopy is left out: the plugin "
                    + SampleUiName
                    + " has a snap of that keyword.",
                copy.Problems
            );
            Assert.Contains(second.Warnings, w => w.Contains("The snap CTOP"));
            Assert.Single(copy.Host.RibbonPanels);
        }
    }
}
