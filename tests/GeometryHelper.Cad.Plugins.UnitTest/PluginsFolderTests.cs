using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.Cad.Plugins.UnitTest
{
    /// <summary>
    /// The Plugins folder beside the app: every .dll directly in it, then every folder of .dlls at any depth, is loaded at start, in the order of
    /// their names; the folder is trusted and asks nothing; a file that is not a plugin, or fails to load, is one line of the
    /// log and stops no other; and a .ghplugin is no longer taken.
    /// </summary>
    public sealed class PluginsFolderTests : IDisposable
    {
        private readonly PluginFiles _files = new PluginFiles();

        public void Dispose() => _files.Dispose();

        private string Log => Path.Combine(_files.Root, "log.txt");

        /// <summary>A plugin of one command in a .dll of its own, saved in the folder given; the file's path.</summary>
        private string Emitted(string folder, string stem, string command) =>
            EmittedPlugin.ToFile(
                folder,
                EmittedPlugin.UniqueName(stem),
                "1.1",
                Log,
                new EmittedClass(stem) { Command = command }
            );

        private CadPluginManager Manager()
        {
            CadPluginManager manager = _files.Manager();
            manager.ConfirmUntrusted = path => throw new InvalidOperationException("Not to be asked: " + path);
            return manager;
        }

        [Fact]
        public void TwoDllsInThePluginsFolder_LoadAtStart_InTheOrderOfTheirNames()
        {
            string zeta = Emitted(_files.PluginFolder, "zeta", "ZETACMD");
            string alpha = Emitted(_files.PluginFolder, "alpha", "ALPHACMD");
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = Manager())
            {
                manager.Attach(registry);

                IReadOnlyList<CadPluginResult> started = manager.LoadAtStart();

                Assert.Equal(2, started.Count);
                Assert.All(started, r => Assert.True(r.Succeeded, r.ToString()));
                Assert.Equal(
                    new[] { Path.GetFileNameWithoutExtension(alpha), Path.GetFileNameWithoutExtension(zeta) },
                    started.Select(r => r.Plugin.Name)
                );
                Assert.Equal(new[] { alpha, zeta }, started.Select(r => r.Plugin.Path));
                Assert.Equal(new[] { alpha, zeta }, manager.Log.Select(l => l.Path));
                Assert.NotNull(registry.Find("ALPHACMD"));
                Assert.NotNull(registry.Find("ZETACMD"));
            }
        }

        [Fact]
        public void TheDllsLoadBeforeTheSubfolders_EachGroupInTheOrderOfItsNames()
        {
            string zeta = Emitted(_files.PluginFolder, "zeta", "ZETACMD");
            string alpha = Emitted(_files.PluginFolder, "alpha", "ALPHACMD");
            string beta = Emitted(Path.Combine(_files.PluginFolder, "beta-tools"), "beta", "BETACMD");
            string aaa = Emitted(Path.Combine(_files.PluginFolder, "aaa-tools"), "aaa", "AAACMD");

            using (CadPluginManager manager = Manager())
            {
                IReadOnlyList<CadPluginResult> started = manager.LoadAtStart();

                Assert.All(started, r => Assert.True(r.Succeeded, r.ToString()));
                Assert.Equal(
                    new[] { alpha, zeta, Path.GetDirectoryName(aaa), Path.GetDirectoryName(beta) },
                    started.Select(r => r.Plugin.Path)
                );
            }
        }

        [Fact]
        public void ASubfolderWithAPluginJson_LoadsAsOnePlugin_UnderTheNameOfItsManifest()
        {
            string folder = Path.Combine(_files.PluginFolder, "number-circles");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, CadPluginManifest.FileName), PluginFiles.Manifest());
            File.Copy(PluginFiles.BuiltSample, Path.Combine(folder, PluginFiles.SampleName + ".dll"));

            using (CadPluginManager manager = Manager())
            {
                CadPluginResult loaded = Assert.Single(manager.LoadAtStart());

                Assert.True(loaded.Succeeded, loaded.ToString());
                Assert.Equal("Loaded Number circles 1.2.0: FAILNOW, NUMCIRCLES.", loaded.Message);
                Assert.Equal(folder, loaded.Plugin.Path);
                Assert.True(loaded.Plugin.IsTrusted);
                Assert.True(loaded.Plugin.IsInstalled);
            }
        }

        [Fact]
        public void ASubfolderWithNoPluginJson_LoadsItsDllsAsOnePlugin_UnderTheNameOfTheFolder()
        {
            string folder = Path.Combine(_files.PluginFolder, "circle.tools");
            Directory.CreateDirectory(folder);
            File.Copy(PluginFiles.BuiltSample, Path.Combine(folder, PluginFiles.SampleName + ".dll"));
            Emitted(folder, "second", "SECONDCMD");

            using (CadPluginManager manager = Manager())
            {
                CadPluginResult loaded = Assert.Single(manager.LoadAtStart());

                Assert.True(loaded.Succeeded, loaded.ToString());
                Assert.Equal("circle.tools", loaded.Plugin.Name);
                Assert.Null(loaded.Plugin.Manifest);
                Assert.Equal(
                    new[] { "FAILNOW", "NUMCIRCLES", "SECONDCMD" },
                    loaded.Plugin.Plugin.Commands.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal)
                );
            }
        }

        [Fact]
        public void ASubfolderWithNoDllAndNoPluginJson_IsPassedOverWithNoLine()
        {
            string docs = Path.Combine(_files.PluginFolder, "docs");
            Directory.CreateDirectory(docs);
            File.WriteAllText(Path.Combine(docs, "readme.txt"), "notes");
            File.WriteAllText(Path.Combine(_files.PluginFolder, "README.txt"), "notes");

            using (CadPluginManager manager = Manager())
            {
                Assert.Empty(manager.LoadAtStart());
                Assert.Empty(manager.Log);
                Assert.Empty(manager.Plugins);
            }
        }

        [Fact]
        public void ADllThatIsNoPlugin_AndOneThatCannotBeLoaded_AreOneLineEach_AndTheOthersStillLoad()
        {
            string alpha = Emitted(_files.PluginFolder, "alpha", "ALPHACMD");
            string plain = EmittedPlugin.WithResource(
                _files.PluginFolder,
                EmittedPlugin.UniqueName("beta"),
                "notes.txt",
                new byte[] { 1, 2, 3 }
            );
            string broken = Path.Combine(_files.PluginFolder, "GhEmittedgamma-broken.dll");
            File.WriteAllBytes(broken, new byte[] { 0x4d, 0x5a, 1, 2, 3 });
            string zeta = Emitted(_files.PluginFolder, "zeta", "ZETACMD");
            var lines = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            IReadOnlyList<CadPluginResult> started;

            using (CadPluginManager manager = Manager())
            {
                try
                {
                    GeometryHelperLog.Writer = (level, text, exception) =>
                        lines.Add(GeometryHelperLog.Format(level, text, exception));
                    started = manager.LoadAtStart();
                }
                finally
                {
                    GeometryHelperLog.Writer = before;
                }

                Assert.Equal(new[] { true, false, false, true }, started.Select(r => r.Succeeded));
                Assert.Equal(new[] { alpha, plain, broken, zeta }, manager.Log.Select(l => l.Path));
                Assert.Equal(new[] { true, false, false, true }, manager.Log.Select(l => l.Succeeded));
                Assert.Equal(new[] { alpha, zeta }, manager.Plugins.Select(p => p.Path).OrderBy(p => p));

                foreach (string refused in new[] { plain, broken })
                {
                    Assert.Single(lines, l => l.Contains(refused));
                }

                Assert.Contains("has no command", started[1].Message, StringComparison.Ordinal);
                Assert.Contains(broken, started[2].Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ThePluginsFolderIsTrusted_SoItsPluginsLoadWithNoQuestion()
        {
            string path = Emitted(_files.PluginFolder, "alpha", "ALPHACMD");

            using (CadPluginManager manager = Manager())
            {
                Assert.Equal(_files.PluginFolder, manager.PluginFolder);
                Assert.Equal(_files.PluginFolder, manager.TrustedFolders[0]);
                Assert.True(manager.IsTrusted(path));
                Assert.True(manager.IsTrusted(Path.Combine(_files.PluginFolder, "sub", "x.dll")));

                CadPluginResult loaded = Assert.Single(manager.LoadAtStart());

                Assert.True(loaded.Succeeded, loaded.ToString());
                Assert.True(loaded.Plugin.IsTrusted);
                Assert.True(Assert.Single(manager.Log).Trusted);
            }
        }

        [Fact]
        public void APluginLoadedAlready_IsNotLoadedASecondTime_AtStart()
        {
            string alpha = Emitted(_files.PluginFolder, "alpha", "ALPHACMD");
            string zeta = Emitted(_files.PluginFolder, "zeta", "ZETACMD");

            using (CadPluginManager manager = Manager())
            {
                Assert.True(manager.Load(alpha).Succeeded);

                CadPluginResult started = Assert.Single(manager.LoadAtStart());

                Assert.Equal(zeta, started.Plugin.Path);
                Assert.Empty(manager.LoadAtStart());
                Assert.Equal(2, manager.Plugins.Count);
                Assert.Equal(2, manager.Log.Count(l => l.Succeeded));
            }
        }

        [Fact]
        public void APluginOfTheFolder_IsLoadedAtEveryStartAnyway_SoItIsNotPutOnTheStartupList()
        {
            Emitted(_files.PluginFolder, "alpha", "ALPHACMD");

            using (CadPluginManager manager = Manager())
            {
                CadPluginResult started = Assert.Single(manager.LoadAtStart());

                Assert.True(started.Plugin.IsInstalled);
                Assert.True(started.Plugin.LoadAtStartup);
                Assert.False(manager.SetLoadAtStartup(started.Plugin.Name, true));
                Assert.Empty(manager.StartupList);
            }
        }

        [Fact]
        public void ADllTwoLevelsDown_IsAPluginOfItsFolder_NamedAfterThatFolder()
        {
            string alpha = Emitted(_files.PluginFolder, "alpha", "ALPHACMD");
            string deeper = Emitted(Path.Combine(_files.PluginFolder, "Company", "ToolA"), "deeper", "DEEPCMD");
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = Manager())
            {
                manager.Attach(registry);

                IReadOnlyList<CadPluginResult> started = manager.LoadAtStart();

                Assert.All(started, r => Assert.True(r.Succeeded, r.ToString()));
                Assert.Equal(new[] { alpha, Path.GetDirectoryName(deeper) }, started.Select(r => r.Plugin.Path));
                Assert.Equal("ToolA", started[1].Plugin.Name);
                Assert.True(started[1].Plugin.IsInstalled);
                Assert.True(started[1].Plugin.IsTrusted);
                Assert.NotNull(registry.Find("DEEPCMD"));
            }
        }

        [Fact]
        public void TheFoldersOfAPluginsTree_LoadDepthFirst_EachLevelInTheOrderOfItsNames()
        {
            string zeta = Emitted(_files.PluginFolder, "zeta", "ZETACMD");
            string deepA = Emitted(Path.Combine(_files.PluginFolder, "Acme", "Tools", "ToolB"), "toolb", "TOOLBCMD");
            string flat = Emitted(Path.Combine(_files.PluginFolder, "Acme", "ToolA"), "toola", "TOOLACMD");
            string beta = Emitted(Path.Combine(_files.PluginFolder, "beta"), "beta", "BETACMD");

            using (CadPluginManager manager = Manager())
            {
                IReadOnlyList<CadPluginResult> started = manager.LoadAtStart();

                Assert.All(started, r => Assert.True(r.Succeeded, r.ToString()));
                Assert.Equal(
                    new[]
                    {
                        zeta,
                        Path.GetDirectoryName(flat),
                        Path.GetDirectoryName(deepA),
                        Path.GetDirectoryName(beta),
                    },
                    started.Select(r => r.Plugin.Path)
                );
            }
        }

        [Fact]
        public void AFolderWithDlls_IsOnePlugin_AndItsOwnSubfolderIsNotWalked()
        {
            string tool = Emitted(Path.Combine(_files.PluginFolder, "Company", "ToolA"), "toola", "TOOLACMD");
            string inner = Emitted(Path.Combine(_files.PluginFolder, "Company", "ToolA", "lib"), "inner", "INNERCMD");

            using (CadPluginManager manager = Manager())
            {
                CadPluginResult loaded = Assert.Single(manager.LoadAtStart());

                Assert.True(loaded.Succeeded, loaded.ToString());
                Assert.Equal(Path.GetDirectoryName(tool), loaded.Plugin.Path);
                Assert.Null(manager.Find(Path.GetFileNameWithoutExtension(inner)));
            }
        }

        [Fact]
        public void AHiddenFolderOfThePluginsTree_IsNotWalked()
        {
            Emitted(Path.Combine(_files.PluginFolder, "Company", "ToolA"), "toola", "TOOLACMD");
            string company = Path.Combine(_files.PluginFolder, "Company");
            new DirectoryInfo(company).Attributes |= FileAttributes.Hidden;

            using (CadPluginManager manager = Manager())
            {
                Assert.Empty(manager.LoadAtStart());
            }
        }

        [Fact]
        public void ADllOutsideThePluginsFolder_StillLoadsForTheSession_AndFromTheStartupList()
        {
            string dev = _files.Sample();

            using (CadPluginManager manager = _files.Manager(true))
            {
                Assert.True(manager.Load(dev).Succeeded);
                Assert.False(manager.Find(PluginFiles.SampleName).IsInstalled);
                Assert.True(manager.SetLoadAtStartup(PluginFiles.SampleName, true));
            }

            // The first load locks its copies on .NET Framework, so the next start copies to a folder of its own.
            using (
                var again = new CadPluginManager(
                    new Wpf.Plugins.CadPluginAssemblyLoader(),
                    _files.PluginFolder,
                    _files.SettingsPath,
                    Path.Combine(_files.ShadowFolder, "again")
                )
                {
                    ConfirmUntrusted = path => true,
                }
            )
            {
                CadPluginResult started = Assert.Single(again.LoadAtStart());

                Assert.True(started.Succeeded, started.ToString());
                Assert.Equal(dev, started.Plugin.Path);
            }
        }

        [Fact]
        public void AGhplugin_IsNoLongerAPlugin_NotLoadedByPath_NorAtStart()
        {
            string package = Path.Combine(_files.Root, "sample.ghplugin");
            File.WriteAllText(package, "PK");
            string inFolder = Path.Combine(_files.PluginFolder, "inside.ghplugin");
            Directory.CreateDirectory(_files.PluginFolder);
            File.WriteAllText(inFolder, "PK");

            using (CadPluginManager manager = _files.Manager(true))
            {
                CadPluginResult refused = manager.Load(package);

                Assert.False(refused.Succeeded);
                Assert.Equal(
                    "There is no plugin " + package + ": a plugin is a .dll, or the folder of one.",
                    refused.Message
                );
                Assert.Empty(manager.LoadAtStart());
                Assert.Single(manager.Log);
                Assert.Empty(manager.Plugins);
            }
        }

        [Fact]
        public void PluginLoadOnTheCommandLine_AsksForADllOrItsFolder()
        {
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager(true))
            {
                manager.Attach(registry);
                CadCommandSession session = PluginFiles.Session(PluginFiles.Circles(), registry);

                session.Feed("NETLOAD");

                Assert.Equal("Enter the path of a plugin (.dll or its folder):", session.PromptText);
            }
        }
    }
}
