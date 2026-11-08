using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using GeometryHelper.Drawing.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Plugins
{
    /// <summary>
    /// The drawing plugins (P1): the sample Bolt dimensions loaded from its .dll as a user's plugin, run on TC5, kept in the
    /// drawing and run again on update; and its loading, unloading and reloading through the presenter's plugin manager.
    /// </summary>
    public sealed class DrawingPluginTests : IDisposable
    {
        private const string SampleName = "GeometryHelper.Drawing.SamplePlugin";
        private const string PluginName = "Bolt dimensions";
        private readonly ITestOutputHelper _output;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "gk-drawing-plugin-tests", Guid.NewGuid().ToString("N"));

        public DrawingPluginTests(ITestOutputHelper output)
        {
            _output = output;
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // .NET Framework holds the copies it loaded until the process ends.
            }
        }

        private static string BuiltSample => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePlugin", SampleName + ".dll");

        /// <summary>TC5, its bolts moved apart along the world's X by a distance each side.</summary>
        private static DrawingSource TC5(double apart = 0.0)
        {
            TestBody body = TestBodies.TC5();
            IEnumerable<TestPart> parts = body.Parts.Select(p => !p.Object.Id.StartsWith("TC5-bolt-", StringComparison.Ordinal) ? p
                : p.TransformBy(GeoTransform3.Translation(new GeoVector3(p.Object.Frame.Origin.X < 0 ? -apart : apart, 0, 0))));
            return new DrawingSource(parts.Select(p => p.Object));
        }

        private CadPluginManager Manager() => new CadPluginManager(new CadPluginAssemblyLoader(), Path.Combine(_root, "plugins"), Path.Combine(_root, "plugins.json"), Path.Combine(_root, "copies"))
        {
            ConfirmUntrusted = path => true,
        };

        private string Package(string kind = "drawing")
        {
            string path = Path.Combine(_root, "bolts.ghplugin");

            using (FileStream stream = File.Create(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry(CadPluginManifest.FileName).Open(), new UTF8Encoding(false)))
                {
                    writer.Write("{ \"id\": \"bolt-dimensions\", \"name\": \"Bolt tools\", \"version\": \"1.0.0\", \"kind\": \"" + kind + "\", \"minimumApiVersion\": \"1.0\" }");
                }

                zip.CreateEntryFromFile(BuiltSample, SampleName + ".dll");
            }

            return path;
        }

        private static List<DrawingElement> PluginDimensions(Drawing drawing, string runId = "R-1")
            => drawing.Elements.Where(e => e.Kind == "Dimension" && e.Id.StartsWith("V-FRONT/" + runId + "/", StringComparison.Ordinal)).ToList();

        private static string Value(DrawingElement element, string name) => element.Values.Single(v => v.Key == name).Value;

        private void Write(Drawing drawing)
        {
            foreach (DrawingElement d in PluginDimensions(drawing))
            {
                _output.WriteLine(d.Id + " " + Value(d, "Text") + " " + string.Join(",", d.Links.ObjectIds));
            }
        }

        [Fact]
        public void TheSampleLoadedFromItsPackageDimensionsTheBoltsOfTC5AndTheDrawingKeepsTheRun()
        {
            using (CadPluginManager manager = Manager())
            {
                var catalog = new DrawingPluginCatalog(manager);
                CadPluginResult loaded = manager.Load(Package());
                Assert.True(loaded.Succeeded, loaded.Message);
                Assert.Equal(new[] { PluginName }, loaded.Plugin.DrawingPlugins);
                DrawingPlugin plugin = catalog.Find(PluginName);
                Assert.NotNull(plugin);

                Drawing drawing = DrawingBuilder.Build(TC5(), AssemblyOnA3(), Tol);
                int before = drawing.Elements.Count;
                UpdateReport report = drawing.RunPlugin(plugin, "V-FRONT");
                Write(drawing);

                DrawingPluginRun run = Assert.Single(drawing.PluginRuns);
                Assert.Equal(new[] { run }, report.PluginRunsRun);
                Assert.Equal(("R-1", PluginName, "V-FRONT"), (run.Id, run.Plugin, run.ViewId));
                Assert.Equal(new[] { new KeyValuePair<string, string>("Rounding", "1") }, run.Settings);
                Assert.Empty(run.Inputs.ObjectIds);

                // Along the base plate the pitch of the bolts and their distances from its ends, across it the bolts' middles
                // from its faces; each a DIMENSION of the drawing's own.
                List<DrawingElement> dimensions = PluginDimensions(drawing);
                Assert.Equal(new[] { "10", "15", "300", "50", "50" }, dimensions.Select(d => Value(d, "Text")));
                Assert.Equal("V-FRONT/R-1/D-HoleGroup-Y-1", dimensions[2].Id);
                Assert.Equal(before + dimensions.Count, drawing.Elements.Count);
                Assert.All(dimensions, d => Assert.Equal(double.Parse(Value(d, "Value"), System.Globalization.CultureInfo.InvariantCulture), Dimension(drawing.Database, d.Id).Measurement, 6));
                Assert.Equal(new[] { run }, DrawingDocumentStore.Read(drawing.Database).PluginRuns);
            }
        }

        [Fact]
        public void TC5WithItsBoltsMovedApartIsUpdatedAndThePluginsDimensionsFollowAHandEditKept()
        {
            using (CadPluginManager manager = Manager())
            {
                var catalog = new DrawingPluginCatalog(manager);
                Assert.True(manager.Load(Package()).Succeeded);

                Drawing drawing = DrawingBuilder.Build(TC5(), AssemblyOnA3(), Tol);
                drawing.Plugins = catalog;
                drawing.RunPlugin(catalog.Find(PluginName), "V-FRONT");
                CadDocument document = drawing.OpenInPresenter();
                string pitch = PluginDimensions(drawing).Single(d => Value(d, "Text") == "300").Id;
                Assert.True(document.Owners.TryApply(document, new CadEdit(Dimension(document.Database, pitch).Handle, CadEditKind.Move, newValue: "0,10")));

                UpdateReport report = drawing.Update(TC5(20));
                Write(drawing);

                Assert.Equal(drawing.PluginRuns, report.PluginRunsRun);
                Assert.Empty(report.PluginRunsToRunAgain);
                Assert.Empty(report.PluginRunsDropped);
                Assert.Equal(pitch, Assert.Single(report.EditsKept).ObjectId);
                DrawingElement moved = drawing.Elements.Single(e => e.Id == pitch);
                Assert.Equal("340", Value(moved, "Text"));
                Assert.Contains(pitch, report.ChangedElements);
                Assert.Equal(340.0, Dimension(document.Database, pitch).Measurement, 6);
                Assert.Contains(PluginDimensions(drawing), d => Value(d, "Text") == "30");

                // Saved, opened again with the plugins at hand and updated, the run is run again from the file's record.
                string path = Path.Combine(_root, "tc5.dxf");
                drawing.Save(path);
                Drawing opened = Drawing.Open(path, TC5(20), Tol);
                opened.Plugins = catalog;
                Assert.Single(opened.Update(TC5()).PluginRunsRun);
                Assert.Equal("300", Value(opened.Elements.Single(e => e.Id == pitch), "Text"));
                Assert.Equal(pitch, Assert.Single(opened.Edits).ObjectId);
            }
        }

        [Fact]
        public void ARunWhoseBoltIsTakenOutOfTheModelIsDroppedAndSaid()
        {
            var catalog = new DrawingPluginCatalog();
            catalog.Add(new Recording());
            Drawing drawing = DrawingBuilder.Build(TC5(), AssemblyOnA3(), Tol);
            drawing.Plugins = catalog;
            drawing.RunPlugin(catalog.Find("Recording"), "V-FRONT", new[] { "TC5-bolt-1" }, new[] { new KeyValuePair<string, double>("Gap", 8) });
            Assert.Equal(new[] { new KeyValuePair<string, string>("Gap", "8") }, Assert.Single(drawing.PluginRuns).Settings);

            UpdateReport report = drawing.Update(new DrawingSource(TC5().Objects.Where(o => o.Id != "TC5-bolt-1")));

            DroppedPluginRun dropped = Assert.Single(report.PluginRunsDropped);
            Assert.Equal("the object TC5-bolt-1 is no longer in the model", dropped.Reason);
            Assert.Empty(drawing.PluginRuns);
            Assert.Empty(PluginDimensions(drawing));
        }

        [Fact]
        public void APluginThatThrowsLeavesTheDrawingAsItWas()
        {
            Drawing drawing = DrawingBuilder.Build(TC5(), AssemblyOnA3(), Tol);
            IReadOnlyList<DrawingElement> before = drawing.Elements;

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => drawing.RunPlugin(new Recording { Fail = true }, "V-FRONT"));

            Assert.Contains("Recording failed: on purpose", thrown.Message);
            Assert.Equal(before, drawing.Elements);
            Assert.Empty(drawing.PluginRuns);
            Assert.Throws<ArgumentOutOfRangeException>(() => drawing.RunPlugin(new Recording(), "V-FRONT", settings: new[] { new KeyValuePair<string, double>("Gap", 500) }));
        }

        [Fact]
        public void AnUpdateWithTheRunsPluginNotAtHandKeepsTheRunToRunAgain()
        {
            Drawing drawing = DrawingBuilder.Build(TC5(), AssemblyOnA3(), Tol);
            drawing.RunPlugin(new Recording(), "V-FRONT");
            Assert.NotEmpty(PluginDimensions(drawing));

            UpdateReport report = drawing.Update(TC5(20));

            Assert.Equal(drawing.PluginRuns, report.PluginRunsToRunAgain);
            Assert.Empty(report.PluginRunsRun);
            Assert.Empty(PluginDimensions(drawing));
        }

        [Fact]
        public void ADrawingPluginPackageIsRefusedWithoutACatalogAndLoadedUnloadedAndReloadedWithOne()
        {
            using (CadPluginManager alone = Manager())
            {
                CadPluginResult refused = alone.Load(Package());
                Assert.False(refused.Succeeded);
                Assert.Contains("loads command plugins only", refused.Message);
            }

            using (CadPluginManager manager = Manager())
            {
                var catalog = new DrawingPluginCatalog(manager);
                Assert.Throws<InvalidOperationException>(() => new DrawingPluginCatalog(manager));
                Assert.True(manager.Install(Package()).Succeeded);
                Assert.Equal(PluginName, Assert.Single(catalog.Plugins).Name);

                Assert.True(manager.Unload("Bolt tools").Succeeded);
                Assert.Empty(catalog.Plugins);
                Assert.Empty(manager.Find("Bolt tools").DrawingPlugins);

                Assert.True(manager.Reload("Bolt tools").Succeeded);
                Assert.Equal(PluginName, Assert.Single(catalog.Plugins).Name);

                Assert.True(manager.Uninstall("Bolt tools").Succeeded);
                Assert.Empty(catalog.Plugins);

                // A command plugin's package, the sample of the drawing plugins in it, is refused: it has no command.
                CadPluginResult command = manager.Load(Package("command"));
                Assert.False(command.Succeeded);
                Assert.Contains("has no command", command.Message);
            }
        }

        [Fact]
        public void ABareDllOfDrawingPluginsIsListedOnThePluginManagersDrawingTab()
        {
            string dll = Path.Combine(_root, "dev", SampleName + ".dll");
            Directory.CreateDirectory(Path.GetDirectoryName(dll));
            File.Copy(BuiltSample, dll);

            using (CadPluginManager manager = Manager())
            using (var dialog = new GeometryHelper.Dxf.Viewer.Wpf.PluginManagerViewModel(manager))
            {
                var catalog = new DrawingPluginCatalog(manager);
                Assert.True(dialog.Load(dll).Succeeded);
                dialog.Filter = "Drawing";

                var row = Assert.Single(dialog.Rows);
                Assert.Equal(("Drawing", PluginName), (row.Kind, row.Commands));
                Assert.Equal("Drawing plugins 1", dialog.DrawingChipText);
                Assert.Same(catalog.Find(PluginName), Assert.Single(catalog.Plugins));
            }
        }

        /// <summary>A plugin of the tests: a dimension of the bolts it is given, or of the view's, and a failure on purpose.</summary>
        private sealed class Recording : DrawingPlugin
        {
            public bool Fail { get; set; }

            public override string Name => "Recording";

            public override void DefineInput(DrawingPluginInput input)
            {
                input.AddObjects("Pick bolts", DrawingObjectKind.Bolt);
                input.AddSetting("Gap", 10, 0, 100);
            }

            public override void Run(DrawingPluginContext context)
            {
                if (Fail)
                {
                    throw new InvalidOperationException("on purpose");
                }

                var settings = context.Settings.Dimensions;
                settings.Scale = context.View.Scale;
                settings.Overall = false;
                context.AddDimensions(GeometryHelper.Drawing.Annotation.Annotator.Dimension(context.View.Lines, context.Objects, context.View.Frame.CoordinateSystem, settings, context.Tolerance));
            }
        }
    }
}
