using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf.Plugins;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The application class of a plugin the window tests write themselves, so that one test can make a button that throws, a
    /// snap that fails or a tab of seven buttons without a .dll of its own: what <see cref="OnInitialize"/> holds is run in
    /// Initialize with the host, and <see cref="OnTerminate"/> in Terminate. It is found in this assembly by the loader
    /// of <see cref="PluginWindowKit"/>, which gives this assembly as the plugin "HookPlugin"; the command HOOKCMD comes with it.
    /// </summary>
    public sealed class HookPluginApplication : ICadPluginApplication
    {
        /// <summary>Gets or sets what Initialize does with the host; null for nothing.</summary>
        public static Action<CadPluginHost> OnInitialize { get; set; }

        /// <summary>Gets or sets what Terminate does; null for nothing.</summary>
        public static Action OnTerminate { get; set; }

        /// <inheritdoc/>
        public void Initialize(CadPluginHost host) => OnInitialize?.Invoke(host);

        /// <inheritdoc/>
        public void Terminate() => OnTerminate?.Invoke();

        /// <summary>The command of the hook plugin: writes "hook ran".</summary>
        /// <param name="c">The command's context.</param>
        /// <returns>No prompt.</returns>
        [CadCommand("HOOKCMD", Description = "The command of the plugin the window tests write themselves")]
        public IEnumerable<CadPrompt> Run(CadCommandContext c)
        {
            c.Write("hook ran");
            yield break;
        }
    }

    /// <summary>
    /// What the tests of what plugins add to the window share: a manager over the loader of the runtime in a folder of its own,
    /// the sample that adds to the window (GeometryHelper.Cad.SampleUiPlugin, loaded from its .dll as a user's plugin is) and
    /// the hook plugin made of <see cref="HookPluginApplication"/>.
    /// </summary>
    internal sealed class PluginWindowKit : IDisposable
    {
        /// <summary>The name of the sample plugin: its file name without the extension.</summary>
        public const string SampleName = "GeometryHelper.Cad.SampleUiPlugin";

        /// <summary>The name of the hook plugin.</summary>
        public const string HookName = "HookPlugin";

        private readonly TestFolder _folder = new TestFolder();
        private readonly CadPluginManager _manager;
        private readonly string _sample;
        private readonly string _hook;

        public PluginWindowKit()
        {
            _manager = new CadPluginManager(
                new KitLoader(),
                Path.Combine(_folder.FullPath, "plugins"),
                Path.Combine(_folder.FullPath, "plugins.json"),
                Path.Combine(_folder.FullPath, "copies")
            );
            string dev = Path.Combine(_folder.FullPath, "dev");
            Directory.CreateDirectory(dev);
            _sample = Path.Combine(dev, SampleName + ".dll");
            File.Copy(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleUiPlugin", SampleName + ".dll"),
                _sample,
                true
            );

            // The manager copies the file and hands its path to the loader, which gives this assembly: any bytes will do.
            _hook = Path.Combine(dev, HookName + ".dll");
            File.WriteAllText(_hook, "not read: the loader of the kit gives the assembly of the tests");
            _manager.AddTrustedFolder(dev);
        }

        /// <summary>Gets the folder of this kit, which holds its manager's files and may hold a test's.</summary>
        public TestFolder Folder => _folder;

        /// <summary>Makes a window's view model that uses the kit's manager, with no plugin loaded and no drawing open.</summary>
        public MainViewModel Model()
        {
            var model = new MainViewModel();
            model.UsePlugins(_manager, loadAtStart: false);
            return model;
        }

        /// <summary>Opens the two columns and the wall of <see cref="TestFiles.Columns"/> in a view model, as a file of the folder.</summary>
        public MainViewModel Opened(out string first, out string second, out string wall)
        {
            MainViewModel model = Model();
            Assert.True(model.Open(_folder.Write("columns.dxf", TestFiles.Columns(out first, out second, out wall))));
            return model;
        }

        /// <summary>Loads the sample that adds to the window.</summary>
        public void LoadSample(MainViewModel model)
        {
            CadPluginResult loaded = model.LoadPlugin(_sample);
            Assert.True(loaded.Succeeded, loaded.ToString());
        }

        /// <summary>Loads the hook plugin, whose Initialize runs what is given and whose Terminate runs what is given.</summary>
        public void LoadHook(MainViewModel model, Action<CadPluginHost> initialize, Action terminate = null)
        {
            HookPluginApplication.OnInitialize = initialize;
            HookPluginApplication.OnTerminate = terminate;
            CadPluginResult loaded = model.LoadPlugin(_hook);
            Assert.True(loaded.Succeeded, loaded.ToString());
        }

        public void Dispose()
        {
            HookPluginApplication.OnInitialize = null;
            HookPluginApplication.OnTerminate = null;
            _manager.Dispose();
            _folder.Dispose();
        }

        /// <summary>The loader of the runtime for every plugin but the hook, whose assemblies are the tests' own.</summary>
        private sealed class KitLoader : CadPluginLoader
        {
            private readonly CadPluginAssemblyLoader _runtime = new CadPluginAssemblyLoader();

            public override bool CanUnload => _runtime.CanUnload;

            public override IReadOnlyList<Assembly> Load(
                string name,
                IReadOnlyList<string> files,
                string probeFolder
            ) =>
                name == HookName
                    ? new[] { typeof(HookPluginApplication).Assembly }
                    : _runtime.Load(name, files, probeFolder);

            public override void Unload(IReadOnlyList<Assembly> assemblies)
            {
                if (!assemblies.Contains(typeof(HookPluginApplication).Assembly))
                {
                    _runtime.Unload(assemblies);
                }
            }
        }
    }
}
