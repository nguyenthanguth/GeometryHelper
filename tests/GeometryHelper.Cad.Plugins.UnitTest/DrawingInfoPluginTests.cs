using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Plugins.UnitTest
{
    /// <summary>
    /// The sample plugin that ships in the Plugins folder, Drawing info: it loads at start from <c>Plugins\DrawingInfo</c> as the
    /// app's own folder does, and its command GHDWGINFO writes what the drawing holds and changes nothing.
    /// </summary>
    public sealed class DrawingInfoPluginTests : IDisposable
    {
        private readonly PluginFiles _files = new PluginFiles();

        public void Dispose() => _files.Dispose();

        /// <summary>Copies the plugin's .dll and plugin.json, as the build of the app does, into Plugins\DrawingInfo of the test folder.</summary>
        private void Install()
        {
            string built = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DrawingInfo");
            string folder = Path.Combine(_files.PluginFolder, "DrawingInfo");
            Directory.CreateDirectory(folder);

            foreach (string file in Directory.GetFiles(built))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }
        }

        [Fact]
        public void ThePluginInThePluginsFolder_LoadsAtStartWithNoQuestion_AsTheDrawingInfoPlugin()
        {
            Install();
            CadPluginManager manager = _files.Manager();
            manager.ConfirmUntrusted = path => throw new InvalidOperationException("Not to be asked: " + path);
            var registry = CadCommandRegistry.CreateDefault();
            manager.Attach(registry);

            using (manager)
            {
                CadPluginResult started = Assert.Single(manager.LoadAtStart());

                Assert.True(started.Succeeded, started.ToString());
                Assert.Equal("Drawing info", started.Plugin.Name);
                Assert.Equal("1.0.0", started.Plugin.Plugin.Version);
                Assert.True(started.Plugin.IsTrusted);
                Assert.Equal(new[] { "GHDWGINFO" }, started.Plugin.Plugin.Commands.Select(c => c.Name));
                Assert.NotNull(registry.Find("GHDWGINFO"));
            }
        }

        [Fact]
        public void TheFolderHoldsThePluginAndItsManifestOnly_NotAnotherCopyOfThePresenter()
        {
            Install();

            Assert.Equal(
                new[] { "GeometryHelper.Cad.Plugins.DrawingInfo.dll", "plugin.json" },
                Directory
                    .GetFiles(Path.Combine(_files.PluginFolder, "DrawingInfo"))
                    .Select(Path.GetFileName)
                    .OrderBy(n => n, StringComparer.Ordinal)
            );
        }

        [Fact]
        public void Ghdwginfo_OnThreeCirclesAndALine_WritesTheNameTheObjectsByTypeTheLayersTheBlocksAndTheExtents()
        {
            Install();
            CadDocument document = PluginFiles.Circles();
            document.Database.Extmin = new GeoPoint3(-10, -40, 0);
            document.Database.Extmax = new GeoPoint3(110, 30, 0);
            var registry = CadCommandRegistry.CreateDefault();

            using (CadPluginManager manager = _files.Manager())
            {
                manager.Attach(registry);
                manager.LoadAtStart();
                CadCommandSession session = PluginFiles.Session(document, registry);

                session.Feed("GHDWGINFO");

                Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
                Assert.Equal(
                    new[]
                    {
                        "Drawing: circles.dxf",
                        "Model space: 4 objects",
                        "  Circle   3",
                        "  Line     1",
                        "Layers: 1, blocks: 0",
                        "Extents: (-10.00,-40.00) to (110.00,30.00)",
                    },
                    session.History.Skip(1).Select(l => l.TrimEnd()).ToArray()
                );
                Assert.False(document.CanUndo);
            }
        }
    }
}
