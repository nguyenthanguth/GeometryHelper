using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Plugins.UnitTest
{
    /// <summary>Loading, unloading, reloading, installing and the startup list, with the loader of .NET Framework, as hosted in Tekla.</summary>
    public sealed class PluginManagerTests : IDisposable
    {
        private readonly PluginFiles _files = new PluginFiles();

        public void Dispose() => _files.Dispose();

        [Fact]
        public void ADllOutsideTheTrustedFoldersIsNotLoadedUntilConfirmed()
        {
            string path = _files.Sample();
            var asked = new List<string>();

            using (CadPluginManager manager = _files.Manager())
            {
                CadPluginResult refused = manager.Load(path);
                manager.ConfirmUntrusted = p =>
                {
                    asked.Add(p);
                    return asked.Count > 1;
                };
                CadPluginResult declined = manager.Load(path);
                CadPluginResult loaded = manager.Load(path);

                Assert.False(refused.Succeeded);
                Assert.Equal(path + " is outside the trusted folders, and loading it was not confirmed.", refused.Message);
                Assert.False(declined.Succeeded);
                Assert.True(loaded.Succeeded, loaded.ToString());
                Assert.Equal(new[] { path, path }, asked);
                Assert.False(loaded.Plugin.IsTrusted);
                Assert.Equal(new[] { false, false, true }, manager.Log.Select(l => l.Succeeded));
                Assert.All(manager.Log, l => Assert.False(l.Trusted));
                Assert.All(manager.Log, l => Assert.Equal(path, l.Path));
            }
        }

        [Fact]
        public void ADllInATrustedFolderLoadsWithoutAskingAndEveryLoadIsLoggedWithItsFullPath()
        {
            string path = _files.Sample();
            var lines = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            int changes = 0;

            using (CadPluginManager manager = _files.Manager())
            {
                manager.ConfirmUntrusted = p => throw new InvalidOperationException("Not to be asked.");
                Assert.True(manager.AddTrustedFolder(Path.GetDirectoryName(path)));
                manager.LogChanged += (s, e) => changes++;
                CadPluginResult result;

                try
                {
                    GeometryHelperLog.Writer = (level, text, exception) => lines.Add(GeometryHelperLog.Format(level, text, exception));
                    result = manager.Load(path);
                }
                finally
                {
                    GeometryHelperLog.Writer = before;
                }

                Assert.True(result.Succeeded, result.ToString());
                Assert.Equal("Loaded " + PluginFiles.SampleName + " " + result.Plugin.Plugin.Version + ": FAILNOW, NUMCIRCLES.", result.Message);
                Assert.True(result.Plugin.IsTrusted);
                CadPluginLogEntry entry = Assert.Single(manager.Log);
                Assert.Equal(CadPluginAction.Load, entry.Action);
                Assert.Equal(PluginFiles.SampleName, entry.Plugin);
                Assert.Equal(path, entry.Path);
                Assert.True(entry.Trusted);
                Assert.Equal(1, changes);
                Assert.Contains(lines, l => l.StartsWith("INFO CadPluginManager: Load " + PluginFiles.SampleName + " from " + path + " (trusted): Loaded", StringComparison.Ordinal));
            }
        }

        [Fact]
        public void TheDllLoadedIsACopySoTheOriginalCanBeBuiltAgainWhileItIsLoaded()
        {
            string path = _files.Sample();
            var loader = new RecordingLoader();

            using (var manager = new CadPluginManager(loader, _files.PluginFolder, _files.SettingsPath, _files.ShadowFolder) { ConfirmUntrusted = p => true })
            {
                Assert.True(manager.Load(path).Succeeded);

                File.Copy(PluginFiles.BuiltSample, path, true);
                File.Delete(path);

                string copy = Assert.Single(loader.Files);
                Assert.StartsWith(manager.ShadowFolder + Path.DirectorySeparatorChar, copy, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(Path.GetFileName(path), Path.GetFileName(copy));
                Assert.Equal(Path.GetDirectoryName(path), loader.ProbeFolder);
                Assert.False(File.Exists(path));
            }
        }

        [Fact]
        public void TheCommandsGoToEveryRegistryAttachedAndUnloadingTakesThemAway()
        {
            var early = CadCommandRegistry.CreateDefault();
            var late = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Attach(early);
                CadPluginResult loaded = manager.Load(_files.Sample());
                manager.Attach(late);
                Assert.NotNull(early.Find("NUMCIRCLES"));
                Assert.NotNull(late.Find("NC"));

                CadPluginResult unloaded = manager.Unload(PluginFiles.SampleName.ToLowerInvariant());

                Assert.True(unloaded.Succeeded);
                Assert.False(manager.CanUnload);
                Assert.Equal("Switched " + PluginFiles.SampleName + " off: its commands are gone, and its code stays loaded until the presenter restarts.", unloaded.Message);
                Assert.Null(early.Find("NUMCIRCLES"));
                Assert.Null(late.Find("NC"));
                Assert.False(loaded.Plugin.IsLoaded);
                Assert.Same(loaded.Plugin, Assert.Single(manager.Plugins));
                Assert.False(manager.Unload(PluginFiles.SampleName).Succeeded);
            }
        }

        [Fact]
        public void ARunGoingOnWhenItsPluginIsUnloadedFailsAndChangesNothing()
        {
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Attach(registry);
                manager.Load(_files.Sample());
                CadDocument document = PluginFiles.Circles();
                CadCommandSession session = PluginFiles.Session(document, registry);
                session.Feed("FAILNOW");
                Assert.Single(PluginFiles.Texts(document));

                manager.Unload(PluginFiles.SampleName);
                session.Feed("anything");

                Assert.Equal(CadCommandOutcome.Failed, session.LastOutcome);
                Assert.Contains("*Error* The plugin " + PluginFiles.SampleName + " was unloaded while FAILNOW ran.", session.History);
                Assert.Empty(PluginFiles.Texts(document));
            }
        }

        [Fact]
        public void ReloadingOnNetFrameworkLoadsTheCommandsAgainAndSaysTheFirstBuildStays()
        {
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Attach(registry);
                manager.Load(_files.Sample());

                CadPluginResult reloaded = manager.Reload(PluginFiles.SampleName);

                Assert.True(reloaded.Succeeded, reloaded.ToString());
                Assert.StartsWith("Reloaded " + PluginFiles.SampleName, reloaded.Message, StringComparison.Ordinal);
                Assert.Contains(reloaded.Warnings, w => w.Contains(".NET Framework takes a new build when the presenter restarts"));
                Assert.Equal(new[] { CadPluginAction.Load, CadPluginAction.Reload }, manager.Log.Select(l => l.Action));
                CadDocument document = PluginFiles.Circles();
                CadCommandSession session = PluginFiles.Session(document, registry);
                session.Feed("NC");
                session.FeedSelection(new[] { "C1" });
                session.Enter();
                session.Enter();
                Assert.Single(PluginFiles.Texts(document));
            }
        }

        [Fact]
        public void APluginWhoseFileHasGoneIsNotReloadedAndStaysLoaded()
        {
            string path = _files.Sample();
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Attach(registry);
                manager.Load(path);
                File.Delete(path);

                CadPluginResult refused = manager.Reload(PluginFiles.SampleName);

                Assert.False(refused.Succeeded);
                Assert.Equal("There is no file " + path + " to reload " + PluginFiles.SampleName + " from.", refused.Message);
                Assert.True(manager.Find(PluginFiles.SampleName).IsLoaded);
                Assert.NotNull(registry.Find("NUMCIRCLES"));
            }
        }

        [Fact]
        public void LoadingAPluginLoadedAlreadyIsRefused()
        {
            string path = _files.Sample();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Load(path);

                CadPluginResult again = manager.Load("\"" + path + "\"");

                Assert.False(again.Succeeded);
                Assert.Equal("The plugin " + PluginFiles.SampleName + " is loaded already, from " + path + "; PLUGINRELOAD loads it again.", again.Message);
            }
        }

        [Fact]
        public void PluginLoadUnloadAndReloadAreAskedOnTheCommandLine()
        {
            string path = _files.Sample();
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Attach(registry);
                CadCommandSession session = PluginFiles.Session(PluginFiles.Circles(), registry);

                session.Feed("NETLOAD");
                Assert.Equal("Enter the path of a plugin (.dll or .ghplugin):", session.PromptText);
                session.Feed("\"" + path + "\"");
                Assert.NotNull(registry.Find("NUMCIRCLES"));

                session.Feed("PLUGINUNLOAD");
                Assert.Equal("Enter the name of the plugin to unload <" + PluginFiles.SampleName + ">:", session.PromptText);
                session.Enter();
                Assert.Null(registry.Find("NUMCIRCLES"));

                session.Feed("PLUGINRELOAD");
                session.Feed(PluginFiles.SampleName);
                Assert.NotNull(registry.Find("NUMCIRCLES"));
                Assert.Contains(session.History, h => h.StartsWith("Reloaded " + PluginFiles.SampleName, StringComparison.Ordinal));

                Assert.True(manager.Detach(registry));
                Assert.Null(registry.Find("NUMCIRCLES"));
                Assert.Null(registry.Find("PLUGINLOAD"));
                Assert.Null(registry.Find("NETLOAD"));
            }
        }

        [Fact]
        public void TheStartupListAndTrustedFoldersAreWrittenAndLoadedAtTheNextStart()
        {
            string path = _files.Sample();

            using (CadPluginManager manager = _files.Manager())
            {
                manager.AddTrustedFolder(Path.GetDirectoryName(path));
                manager.Load(path);
                Assert.True(manager.SetLoadAtStartup(PluginFiles.SampleName, true));
                Assert.True(manager.Find(PluginFiles.SampleName).LoadAtStartup);
            }

            string json = File.ReadAllText(_files.SettingsPath);
            Assert.Contains(path.Replace("\\", "\\\\"), json, StringComparison.Ordinal);

            using (CadPluginManager manager = _files.Manager())
            {
                Assert.Equal(new[] { path }, manager.StartupList);
                Assert.Equal(new[] { _files.PluginFolder, Path.GetDirectoryName(path) }, manager.TrustedFolders);

                CadPluginResult started = Assert.Single(manager.LoadAtStart());

                Assert.True(started.Succeeded, started.ToString());
                Assert.True(started.Plugin.LoadAtStartup);
                Assert.True(manager.SetLoadAtStartup(PluginFiles.SampleName, false));
                Assert.Empty(manager.StartupList);
            }
        }

        [Fact]
        public void APackageIsInstalledIntoThePluginFolderAndLoadedAtEveryStartUntilUninstalled()
        {
            string package = _files.Package(PluginFiles.Manifest());
            string folder = Path.Combine(_files.PluginFolder, "number-circles");

            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult installed = manager.Install(package);

                Assert.True(installed.Succeeded, installed.ToString());
                Assert.Equal("Loaded Number circles 1.2.0: FAILNOW, NUMCIRCLES.", installed.Message);
                Assert.True(installed.Plugin.IsInstalled);
                Assert.True(installed.Plugin.IsTrusted);
                Assert.Equal(folder, installed.Plugin.Path);
                Assert.Equal(Path.Combine(folder, "icon.png"), installed.Plugin.IconPath);
                Assert.True(File.Exists(Path.Combine(folder, PluginFiles.SampleName + ".dll")));
                Assert.Equal(new[] { CadPluginAction.Install, CadPluginAction.Load }, manager.Log.Select(l => l.Action));
            }

            using (CadPluginManager manager = _files.Manager())
            {
                CadPluginResult started = Assert.Single(manager.LoadAtStart());
                Assert.True(started.Succeeded, started.ToString());
                Assert.Equal("Number circles", started.Plugin.Name);

                CadPluginResult removed = manager.Uninstall("number circles");

                Assert.True(removed.Succeeded, removed.ToString());
                Assert.False(Directory.Exists(folder));
                Assert.Empty(manager.Plugins);
            }
        }

        [Fact]
        public void APackageOutsideTheTrustedFoldersIsInstalledOnlyWhenConfirmed()
        {
            string package = _files.Package(PluginFiles.Manifest());

            using (CadPluginManager manager = _files.Manager())
            {
                CadPluginResult refused = manager.Install(package);

                Assert.False(refused.Succeeded);
                Assert.False(Directory.Exists(_files.PluginFolder));
            }
        }

        [Fact]
        public void APackageThatNeedsANewerApiIsRefusedBeforeAnyOfItsCodeIsCopiedOrLoaded()
        {
            string package = _files.Package(PluginFiles.Manifest(api: "2.0"));

            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult refused = manager.Load(package);

                Assert.False(refused.Succeeded);
                Assert.Equal("The plugin Number circles needs version 2.0 of the plugin API, and this presenter has 1.0: it needs a newer presenter.", refused.Message);
                Assert.False(Directory.Exists(manager.ShadowFolder));
                Assert.Empty(manager.Plugins);
                Assert.False(Assert.Single(manager.Log).Succeeded);
            }
        }

        [Fact]
        public void APackageLoadedForTheSessionRunsFromItsCopiesUnderTheNameOfItsManifest()
        {
            string package = _files.Package(PluginFiles.Manifest());
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Attach(registry);

                CadPluginResult loaded = manager.Load(package);

                Assert.True(loaded.Succeeded, loaded.ToString());
                Assert.Equal("Number circles", loaded.Plugin.Name);
                Assert.Equal("1.2.0", loaded.Plugin.Plugin.Version);
                Assert.False(loaded.Plugin.IsInstalled);
                Assert.NotNull(registry.Find("NUMCIRCLES"));
            }
        }

        [Fact]
        public void ThePresentersOwnAssemblyInAPluginsFolderIsNotLoadedASecondTime()
        {
            string folder = Path.Combine(_files.PluginFolder, "with-copies");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, CadPluginManifest.FileName), PluginFiles.Manifest("with-copies", "With copies"));
            File.Copy(PluginFiles.BuiltSample, Path.Combine(folder, PluginFiles.SampleName + ".dll"));
            File.Copy(typeof(CadPlugin).Assembly.Location, Path.Combine(folder, Path.GetFileName(typeof(CadPlugin).Assembly.Location)));
            var registry = CadCommandRegistry.CreateDefault();
            var loader = new RecordingLoader();

            using (var manager = new CadPluginManager(loader, _files.PluginFolder, _files.SettingsPath, _files.ShadowFolder))
            {
                manager.Attach(registry);

                CadPluginResult loaded = Assert.Single(manager.LoadAtStart());

                Assert.True(loaded.Succeeded, loaded.ToString());
                Assert.Equal(2, loader.Files.Count);
                Assert.Equal(PluginFiles.SampleName, Assert.Single(loader.Loaded).GetName().Name);
                CadDocument document = PluginFiles.Circles();
                CadCommandSession session = PluginFiles.Session(document, registry);
                session.Feed("NUMCIRCLES");
                session.FeedSelection(new[] { "C1", "C2" });
                session.Enter();
                session.Enter();
                Assert.Equal(2, PluginFiles.Texts(document).Count);
            }
        }

        [Theory]
        [InlineData("missing.dll")]
        [InlineData("notes.txt")]
        public void APathThatIsNoPluginIsRefusedSayingWhatAPluginIs(string file)
        {
            string path = Path.Combine(_files.Root, file);
            File.WriteAllText(Path.Combine(_files.Root, "notes.txt"), "not a plugin");

            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult refused = manager.Load(path);

                Assert.False(refused.Succeeded);
                Assert.Equal("There is no plugin " + path + ": a plugin is a .dll, a .ghplugin, or the folder of one installed.", refused.Message);
            }
        }

        /// <summary>The loader of .NET Framework, keeping what it was given and what it gave.</summary>
        private sealed class RecordingLoader : CadPluginLoader
        {
            private readonly CadPluginLoader _inner = new Wpf.Plugins.CadPluginAssemblyLoader();

            public List<string> Files { get; } = new List<string>();

            public List<Assembly> Loaded { get; } = new List<Assembly>();

            public string ProbeFolder { get; private set; }

            public override bool CanUnload => _inner.CanUnload;

            public override IReadOnlyList<Assembly> Load(string name, IReadOnlyList<string> files, string probeFolder)
            {
                Files.AddRange(files);
                ProbeFolder = probeFolder;
                IReadOnlyList<Assembly> loaded = _inner.Load(name, files, probeFolder);
                Loaded.AddRange(loaded);
                return loaded;
            }

            public override void Unload(IReadOnlyList<Assembly> assemblies) => _inner.Unload(assemblies);
        }
    }
}
