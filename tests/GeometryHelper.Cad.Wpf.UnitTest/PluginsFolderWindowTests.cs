using System;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Plugins folder beside the app, as the window meets it: the folder the app loads from, the plugins found in it at
    /// start, the Plugin manager's trusted folders, and no way left to install a .ghplugin.
    /// </summary>
    public sealed class PluginsFolderWindowTests : IDisposable
    {
        private const string SampleName = "GeometryHelper.Cad.SamplePlugin";
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private CadPluginManager Manager(string plugins) =>
            new CadPluginManager(
                new CadPluginAssemblyLoader(),
                plugins,
                Path.Combine(_folder.FullPath, "plugins.json"),
                Path.Combine(_folder.FullPath, "copies")
            );

        [Fact]
        public void TheAppLoadsItsPluginsFromThePluginsFolderBesideTheDll_NotFromTheUserFolder()
        {
            string beside = Path.Combine(CadSupportFolders.Default.ProgramFolder, "Plugins");

            Assert.Equal(beside, CadAppHost.AppPluginsFolder(), ignoreCase: true);
            Assert.True(Directory.Exists(beside));
            Assert.True(File.Exists(Path.Combine(beside, "README.txt")));
        }

        [Fact]
        public void ADllInThePluginsFolder_IsLoadedWhenTheWindowStarts_AndSaidOnTheCommandLine()
        {
            string plugins = Path.Combine(_folder.FullPath, "Plugins");
            Directory.CreateDirectory(plugins);
            File.Copy(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePlugin", SampleName + ".dll"),
                Path.Combine(plugins, SampleName + ".dll")
            );
            var model = new MainViewModel();

            using (CadPluginManager manager = Manager(plugins))
            {
                CadPluginResult loaded = Assert.Single(model.UsePlugins(manager, loadAtStart: true));

                Assert.True(loaded.Succeeded, loaded.ToString());
                Assert.True(loaded.Plugin.IsTrusted);
                Assert.Contains(model.History, line =>
                    line.StartsWith("Loaded " + SampleName, StringComparison.Ordinal)
                );
            }
        }

        [Fact]
        public void ThePluginManagerShowsThePluginsFolderFirstAmongTheTrustedFolders_AndItCannotBeRemoved()
        {
            string plugins = Path.Combine(_folder.FullPath, "Plugins");

            using (CadPluginManager manager = Manager(plugins))
            using (var model = new PluginManagerViewModel(manager))
            {
                manager.AddTrustedFolder(Path.Combine(_folder.FullPath, "dev"));

                Assert.Equal(plugins, model.TrustedFolders[0]);
                model.SelectedFolder = plugins;
                Assert.False(model.RemoveFolderCommand.CanExecute(null));
            }
        }

        [Theory]
        [InlineData("InstallCommand")]
        [InlineData("RemoveCommand")]
        [InlineData("InstallRequested")]
        [InlineData("Install")]
        public void ThePluginManager_HasNoInstallOrRemoveAnyMore(string member)
        {
            Assert.Null(typeof(PluginManagerViewModel).GetMember(member).FirstOrDefault());
        }
    }
}
