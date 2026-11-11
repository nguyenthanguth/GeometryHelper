using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Plugins;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Plugins
{
    /// <summary>
    /// The dimensions a drawing plugin adds to a view stand beyond the drawing's own, so that the two do not overlap; and the
    /// sample loaded from a folder that is not trusted is asked about first, through the catalog.
    /// </summary>
    public sealed class DrawingPluginTierTests : IDisposable
    {
        private const string SampleName = "GeometryHelper.Drawing.SamplePlugin";
        private readonly ITestOutputHelper _output;
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "gk-drawing-plugin-tests",
            Guid.NewGuid().ToString("N")
        );

        public DrawingPluginTierTests(ITestOutputHelper output)
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

        private static string BuiltSample =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePlugin", SampleName + ".dll");

        private CadPluginManager Manager(Func<string, bool> confirm) =>
            new CadPluginManager(
                new CadPluginAssemblyLoader(),
                Path.Combine(_root, "plugins"),
                Path.Combine(_root, "plugins.json"),
                Path.Combine(_root, "copies")
            )
            {
                ConfirmUntrusted = confirm,
            };

        [Fact]
        public void TheSamplesDimensionsOnTC5StandBeyondTheDrawingsOwnAndOverlapNoneOfThem()
        {
            using (CadPluginManager manager = Manager(path => true))
            {
                var catalog = new DrawingPluginCatalog(manager);
                Assert.True(manager.Load(BuiltSample).Succeeded);
                Drawing drawing = DrawingBuilder.Build(
                    new DrawingSource(TestBodies.TC5().Parts.Select(p => p.Object)),
                    AssemblyOnA3(),
                    Tol
                );
                DrawingView before = drawing.Views.Single(v => v.Id == "V-FRONT");
                List<DrawingDimension> own = before.Dimensions.ToList();
                Assert.NotEmpty(own);
                Assert.Empty(Annotator.FindOverlaps(own, Tol));

                drawing.RunPlugin(catalog.Find("Bolt dimensions"), "V-FRONT");

                DrawingView view = drawing.Views.Single(v => v.Id == "V-FRONT");
                List<DrawingDimension> added = view
                    .Dimensions.Where(d => d.Id.StartsWith("R-1/", StringComparison.Ordinal))
                    .ToList();
                Assert.Equal(new[] { "10", "15", "300", "50", "50" }, added.Select(d => d.Text));

                foreach (DrawingDimension d in view.Dimensions)
                {
                    _output.WriteLine(
                        d.Id + " " + d.Text + " tier " + d.Tier + " offset " + d.Offset + " line " + d.DimensionLine
                    );
                }

                IReadOnlyList<string> overlaps = Annotator.FindOverlaps(view.Dimensions, Tol);

                foreach (string overlap in overlaps)
                {
                    _output.WriteLine(overlap);
                }

                Assert.Empty(overlaps);

                // Each of the plugin's stands on a tier beyond every one of the drawing's own along its axis, on its side.
                foreach (DrawingDimension d in added)
                {
                    Assert.All(own.Where(o => o.Direction.Equals(d.Direction) && Side(o, view) == Side(d, view)), o =>
                        Assert.True(d.Tier > o.Tier, d.Id + " is on tier " + d.Tier + ", " + o.Id + " on " + o.Tier)
                    );
                }
            }
        }

        [Fact]
        public void TheSampleInAFolderThatIsNotTrustedIsAskedAboutFirstAndNotLoadedWhenRefused()
        {
            string dev = Path.Combine(_root, "dev");
            Directory.CreateDirectory(dev);
            string dll = Path.Combine(dev, SampleName + ".dll");
            File.Copy(BuiltSample, dll);
            var asked = new List<string>();

            using (
                CadPluginManager manager = Manager(path =>
                {
                    asked.Add(path);
                    return false;
                })
            )
            {
                var catalog = new DrawingPluginCatalog(manager);

                CadPluginResult refused = manager.Load(dll);

                Assert.False(refused.Succeeded);
                Assert.Equal(new[] { dll }, asked);
                Assert.Empty(catalog.Plugins);

                manager.ConfirmUntrusted = path =>
                {
                    asked.Add(path);
                    return true;
                };
                Assert.True(manager.Load(dll).Succeeded);
                Assert.Equal(new[] { dll, dll }, asked);
                Assert.Equal("Bolt dimensions", Assert.Single(catalog.Plugins).Name);
                Assert.Contains(manager.Log, e => e.Succeeded && !e.Trusted && e.Path == dll);
            }
        }

        /// <summary>1 where a dimension stands above or on the right of its view's box, -1 below or on the left.</summary>
        private static int Side(DrawingDimension d, DrawingView view)
        {
            GeoLine2 line = d.DimensionLine ?? d.ExtensionLines[0];
            return d.Direction.X > 0.5
                ? (line.StartPoint.Y > view.Box.Center.Y ? 1 : -1)
                : (line.StartPoint.X > view.Box.Center.X ? 1 : -1);
        }
    }
}
