using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Python files' own plugin hosts (<see cref="CadPluginHost"/>, one for each loaded <c>.py</c>, D1661) as the window
    /// draws them: a file's tab and panel come after those of the <c>.dll</c> plugins, whenever each was loaded, and go when
    /// the file is unloaded. <see cref="FakePythonPluginHosts"/> stands for the engine, which starts once per process and has
    /// its own suite; it holds the hosts a test gives it and says when the list changed.
    /// </summary>
    public sealed class PythonRibbonTests : IDisposable
    {
        private readonly PluginWindowKit _kit = new PluginWindowKit();

        public void Dispose() => _kit.Dispose();

        /// <summary>The Python files of a window with no engine: the hosts it was given, in the order it was given them.</summary>
        private sealed class FakePythonPluginHosts : IPythonFiles
        {
            private readonly List<CadPluginHost> _hosts = new List<CadPluginHost>();

            public IReadOnlyList<CadPluginHost> PluginHosts => _hosts.ToArray();

            public event EventHandler PluginHostsChanged;

            public IReadOnlyList<string> Loaded => Array.Empty<string>();

            public bool Load(CadCommandSession session, string path) => false;

            public IReadOnlyList<string> CommandsOf(string path) => Array.Empty<string>();

            public bool Unload(string path) => false;

            public void Add(CadPluginHost host)
            {
                _hosts.Add(host);
                PluginHostsChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Remove(CadPluginHost host)
            {
                _hosts.Remove(host);
                PluginHostsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>The host of <c>tools.py</c>: the tab "Py Tools" with the panel "Draw", and the panel "Extras" of the Plugins tab.</summary>
        private static CadPluginHost ToolsHost(Func<CadPluginContext, bool> showWhen = null)
        {
            var host = new CadPluginHost("tools.py", null);
            var tab = new CadRibbonTab("pytools", "Py Tools") { ShowWhen = showWhen };
            var draw = new CadRibbonPanel("Draw");
            draw.Items.Add(new CadRibbonButton("Circle", "CIRCLE"));
            tab.Panels.Add(draw);
            host.AddRibbonTab(tab);
            var extras = new CadRibbonPanel("Extras");
            extras.Items.Add(new CadRibbonButton("Line", "LINE"));
            host.AddRibbonPanel(extras);
            host.Close();
            return host;
        }

        private static string[] Headers(IEnumerable<CadWorkspaceExtension> tabs) =>
            tabs.Select(t => t.TabHeader).ToArray();

        private static string[] PanelsOf(CadWorkspaceExtension tab) =>
            ((PluginTabViewModel)tab.TabContent).Panels.Select(p => p.Title).ToArray();

        [Fact]
        public void APythonFilesTabAndPanels_AreShownAfterTheTabOfAPluginLoadedBefore()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadHook(model, host => host.AddRibbonTab(new CadRibbonTab("Hook", "Hook")));
            var python = new FakePythonPluginHosts();
            model.PythonFiles = python;

            python.Add(ToolsHost());

            Assert.Equal(new[] { "Hook", "Py Tools" }, Headers(model.PluginTabs));
            Assert.Equal(new[] { "Draw" }, PanelsOf(model.PluginTabs[1]));
            Assert.Equal(new[] { "Extras" }, model.PluginRibbon.Panels.Select(p => p.Title));
            Assert.True(model.PluginsTab.IsTabVisible);
        }

        [Fact]
        public void APythonFilesTab_StaysAfterThoseOfThePlugins_EvenWhenAPluginIsLoadedLater()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            var python = new FakePythonPluginHosts();
            model.PythonFiles = python;
            python.Add(ToolsHost());

            _kit.LoadHook(model, host => host.AddRibbonTab(new CadRibbonTab("Hook", "Hook")));

            Assert.Equal(new[] { "Hook", "Py Tools" }, Headers(model.PluginTabs));
        }

        [Fact]
        public void WhenTheFileIsUnloaded_ItsTabAndPanelsAreDropped_AndTheOthersStay()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            _kit.LoadHook(model, host => host.AddRibbonTab(new CadRibbonTab("Hook", "Hook")));
            var python = new FakePythonPluginHosts();
            model.PythonFiles = python;
            CadPluginHost tools = ToolsHost();
            python.Add(tools);
            Assert.Equal(new[] { "Hook", "Py Tools" }, Headers(model.PluginTabs));
            Assert.True(model.PluginsTab.IsTabVisible);

            python.Remove(tools);

            Assert.Equal(new[] { "Hook" }, Headers(model.PluginTabs));
            Assert.Empty(model.PluginRibbon.Panels);
            Assert.False(model.PluginsTab.IsTabVisible);
        }

        [Fact]
        public void ATabWithAShowWhen_IsAContextualTab_AsADllPluginsIs()
        {
            MainViewModel model = _kit.Opened(out _, out _, out _);
            var python = new FakePythonPluginHosts();
            model.PythonFiles = python;

            python.Add(ToolsHost(context => true));

            Assert.Empty(model.PluginTabs);
            Assert.Equal(new[] { "Py Tools" }, Headers(model.PluginContextTabs));
        }
    }
}
