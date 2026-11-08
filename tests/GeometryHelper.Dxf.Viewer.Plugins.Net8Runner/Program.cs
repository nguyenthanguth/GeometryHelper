using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using GeometryHelper.Drawing;
using GeometryHelper.Drawing.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Plugins;
using GeometryHelper.Dxf.Viewer.Wpf.Plugins;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Plugins.Net8Runner
{
    /// <summary>
    /// The plugin checks only .NET 8 can make: the sample plugin rebuilt while loaded and reloaded runs its new code, and
    /// its collectible load context is collected once it is unloaded. Prints PASS or FAIL per check; exits 1 on a failure.
    /// </summary>
    internal static class Program
    {
        private const string Sample = "GeometryHelper.Dxf.Viewer.SamplePlugin";
        private const string FirstDone = "Numbered {0} circles.";
        private const string SecondDone = "Second build: numbered {0} circles.";

        private const string DrawingSample = "GeometryHelper.Drawing.SamplePlugin";
        private const string FirstRounding = "input.AddSetting(\"Rounding\", 1.0, 0.01, 100.0);";
        private const string SecondRounding = "input.AddSetting(\"Rounding\", 5.0, 0.01, 100.0);";

        private static int _failures;

        /// <summary>The drawing the drawing plugin ran on, kept alive while its load context is to be collected.</summary>
        private static Drawing.Drawing _drawing;

        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(), "gk-plugin-net8", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string beside = Path.Combine(AppContext.BaseDirectory, "SamplePlugin");
            var clock = Stopwatch.StartNew();

            try
            {
                string second = BuildSecondVersion(Path.Combine(beside, "CircleTools.cs.txt"), Path.Combine(root, "second"), Sample, "\"" + FirstDone + "\"", "\"" + SecondDone + "\"", typeof(CadPlugin), typeof(DxfDatabase));
                Console.WriteLine("Built the second version in " + Ms(clock));

                WeakReference[] contexts = ReloadAndUnload(root, Path.Combine(beside, Sample + ".dll"), second);
                Check("every load context of the sample is collected after unloading", Collected(contexts));

                WeakReference[] installed = InstallAndUninstall(root, Path.Combine(beside, Sample + ".dll"));
                Check("the load context of the package installed is collected after uninstalling", Collected(installed));

                WeakReference[] drawing = DrawingPluginRunAndUnload(root, Path.Combine(AppContext.BaseDirectory, "DrawingSamplePlugin", DrawingSample + ".dll"));
                Check("the load context of the drawing plugin is collected after unloading, its drawing kept", Collected(drawing) && _drawing.PluginRuns.Count == 1);

                string drawingBeside = Path.Combine(AppContext.BaseDirectory, "DrawingSamplePlugin");
                string drawingSecond = BuildSecondVersion(Path.Combine(drawingBeside, "BoltDimensions.cs.txt"), Path.Combine(root, "drawing-second"), DrawingSample, FirstRounding, SecondRounding, typeof(DrawingPlugin), typeof(CadPlugin), typeof(DxfDatabase));
                WeakReference[] rebuilt = DrawingPluginRebuiltAndReloaded(root, Path.Combine(drawingBeside, DrawingSample + ".dll"), drawingSecond);
                Check("every load context of the drawing plugin rebuilt and reloaded is collected after unloading", Collected(rebuilt));
            }
            catch (Exception exception)
            {
                Check("the run ends without an exception: " + exception, false);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    Console.WriteLine("The folder " + root + " is left: " + exception.Message);
                }
            }

            Console.WriteLine((_failures == 0 ? "ALL PASSED" : _failures + " FAILED") + " in " + Ms(clock) + " on " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            return _failures == 0 ? 0 : 1;
        }

        /// <summary>Loads the sample, runs it, builds over it, reloads and runs the new build, unloads; gives the load contexts it saw.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] ReloadAndUnload(string root, string built, string second)
        {
            string dev = Path.Combine(root, "dev");
            Directory.CreateDirectory(dev);
            string path = Path.Combine(dev, Sample + ".dll");
            File.Copy(built, path);
            var registry = CadCommandRegistry.CreateDefault();
            var asked = new List<string>();
            var contexts = new List<WeakReference>();

            using (var manager = new CadPluginManager(new CadPluginAssemblyLoader(), Path.Combine(root, "plugins"), Path.Combine(root, "plugins.json"), Path.Combine(root, "copies")))
            {
                manager.ConfirmUntrusted = p =>
                {
                    asked.Add(p);
                    return true;
                };
                manager.Attach(registry);
                Check("unloading frees the code on .NET 8", manager.CanUnload);

                var clock = Stopwatch.StartNew();
                CadPluginResult loaded = manager.Load(path);
                Check("the sample loads: " + loaded, loaded.Succeeded);
                Check("a .dll outside the trusted folders is asked about once", asked.Count == 1 && asked[0] == path);
                Console.WriteLine("  load " + Ms(clock));
                contexts.AddRange(Contexts());
                Check("the sample has a collectible load context of its own", contexts.Count == 1);
                Expect("the first build numbers the circles", Run(registry), string.Format(CultureInfo.InvariantCulture, FirstDone, 3));

                // Built again while loaded: the copy is loaded, so the file is not locked.
                File.Copy(second, path, true);
                Check("the .dll loaded is not locked, so a new build overwrites it", true);

                clock.Restart();
                CadPluginResult reloaded = manager.Reload(Sample);
                Console.WriteLine("  reload " + Ms(clock));
                Check("the sample reloads with no warning: " + reloaded, reloaded.Succeeded && reloaded.Warnings.Count == 0);
                contexts.AddRange(Contexts());
                Expect("the new build runs after PLUGINRELOAD, without a restart", Run(registry), string.Format(CultureInfo.InvariantCulture, SecondDone, 3));
                Check("a failing command changes nothing and names the plugin", Fails(registry));

                clock.Restart();
                CadPluginResult unloaded = manager.Unload(Sample);
                Console.WriteLine("  unload " + Ms(clock));
                Check("the sample unloads: " + unloaded.Message, unloaded.Succeeded && unloaded.Message == "Unloaded " + Sample + ".");
                Check("its commands are gone", registry.Find("NUMCIRCLES") == null && registry.Find("NC") == null);
            }

            return Distinct(contexts);
        }

        /// <summary>Installs a package of the sample, loads it again in a second manager as at a start, runs it and uninstalls it.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] InstallAndUninstall(string root, string built)
        {
            string package = Path.Combine(root, "sample.ghplugin");

            using (ZipArchive zip = ZipFile.Open(package, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("plugin.json").Open(), new UTF8Encoding(false)))
                {
                    writer.Write("{ \"id\": \"number-circles\", \"name\": \"Number circles\", \"version\": \"1.2.0\", \"kind\": \"command\", \"minimumApiVersion\": \"1.0\", \"commands\": [ \"NUMCIRCLES\" ] }");
                }

                zip.CreateEntryFromFile(built, Sample + ".dll");
            }

            var contexts = new List<WeakReference>();
            string plugins = Path.Combine(root, "installed");

            using (var manager = new CadPluginManager(new CadPluginAssemblyLoader(), plugins, Path.Combine(root, "installed.json"), Path.Combine(root, "copies2")))
            {
                manager.ConfirmUntrusted = p => true;
                CadPluginResult installed = manager.Install(package);
                Check("the package installs and loads: " + installed, installed.Succeeded && installed.Plugin.Name == "Number circles");
                contexts.AddRange(Contexts());
            }

            var registry = CadCommandRegistry.CreateDefault();

            using (var manager = new CadPluginManager(new CadPluginAssemblyLoader(), plugins, Path.Combine(root, "installed.json"), Path.Combine(root, "copies3")))
            {
                manager.Attach(registry);
                IReadOnlyList<CadPluginResult> started = manager.LoadAtStart();
                Check("the installed plugin loads at the next start", started.Count == 1 && started[0].Succeeded);
                contexts.AddRange(Contexts());
                Expect("it runs", Run(registry), string.Format(CultureInfo.InvariantCulture, FirstDone, 3));
                CadPluginResult removed = manager.Uninstall("Number circles");
                Check("it uninstalls, its folder deleted: " + removed, removed.Succeeded && !Directory.Exists(Path.Combine(plugins, "number-circles")));
            }

            return Distinct(contexts);
        }

        /// <summary>
        /// Loads the sample drawing plugin through a catalog, runs it on a plate with a bolt, unloads it; gives the load
        /// contexts it saw. The drawing, which keeps the run, is kept in <see cref="_drawing"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] DrawingPluginRunAndUnload(string root, string built)
        {
            var tolerance = Tolerance.Default;
            DrawingSource source = PlateWithBolt(tolerance);
            var contexts = new List<WeakReference>();

            using (var manager = new CadPluginManager(new CadPluginAssemblyLoader(), Path.Combine(root, "drawing"), Path.Combine(root, "drawing.json"), Path.Combine(root, "copies4")))
            {
                manager.ConfirmUntrusted = p => true;
                var catalog = new DrawingPluginCatalog(manager);
                CadPluginResult loaded = manager.Load(built);
                Check("the sample drawing plugin loads: " + loaded, loaded.Succeeded && catalog.Plugins.Count == 1);
                contexts.AddRange(Contexts());
                _drawing = DrawingBuilder.Build(source, DrawingSettings.Assembly, tolerance);
                _drawing.Plugins = catalog;
                UpdateReport report = _drawing.RunPlugin(catalog.Find("Bolt dimensions"), _drawing.Views[0].Id);
                Check("it runs on a drawing: " + report, report.PluginRunsRun.Count == 1);
                CadPluginResult unloaded = manager.Unload(DrawingSample);
                Check("it unloads and leaves the catalog: " + unloaded.Message, unloaded.Succeeded && catalog.Plugins.Count == 0);
            }

            return Distinct(contexts);
        }

        /// <summary>
        /// Loads the sample drawing plugin from a folder of its own, runs it, builds a second version over its .dll while it is
        /// loaded, reloads it and runs the new build, which declares another rounding, then unloads it; gives the load contexts.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] DrawingPluginRebuiltAndReloaded(string root, string built, string second)
        {
            string dev = Path.Combine(root, "drawing-dev");
            Directory.CreateDirectory(dev);
            string path = Path.Combine(dev, DrawingSample + ".dll");
            File.Copy(built, path);
            var tolerance = Tolerance.Default;
            var asked = new List<string>();
            var contexts = new List<WeakReference>();

            using (var manager = new CadPluginManager(new CadPluginAssemblyLoader(), Path.Combine(root, "drawing2"), Path.Combine(root, "drawing2.json"), Path.Combine(root, "copies5")))
            {
                manager.ConfirmUntrusted = p =>
                {
                    asked.Add(p);
                    return true;
                };
                var catalog = new DrawingPluginCatalog(manager);
                CadPluginResult loaded = manager.Load(path);
                Check("the drawing plugin loads from a folder that is not trusted, asked about once: " + loaded, loaded.Succeeded && asked.Count == 1 && asked[0] == path);
                contexts.AddRange(Contexts());
                Drawing.Drawing first = DrawingBuilder.Build(PlateWithBolt(tolerance), DrawingSettings.Assembly, tolerance);
                first.Plugins = catalog;
                first.RunPlugin(catalog.Find("Bolt dimensions"), first.Views[0].Id);
                Expect("the first build declares the rounding 1", Rounding(first), "1");

                // Built again while loaded: the copy is loaded, so the file is not locked.
                File.Copy(second, path, true);
                Check("the drawing plugin's .dll loaded is not locked, so a new build overwrites it", true);
                CadPluginResult reloaded = manager.Reload(DrawingSample);
                Check("the drawing plugin reloads with no warning: " + reloaded, reloaded.Succeeded && reloaded.Warnings.Count == 0 && catalog.Plugins.Count == 1);
                contexts.AddRange(Contexts());
                Drawing.Drawing again = DrawingBuilder.Build(PlateWithBolt(tolerance), DrawingSettings.Assembly, tolerance);
                again.RunPlugin(catalog.Find("Bolt dimensions"), again.Views[0].Id);
                Expect("the new build runs after PLUGINRELOAD, without a restart", Rounding(again), "5");
                UpdateReport update = first.Update(PlateWithBolt(tolerance));
                Check("the drawing it ran on before runs its run again with the new build on update", update.PluginRunsRun.Count == 1 && Rounding(first) == "1");
                CadPluginResult unloaded = manager.Unload(DrawingSample);
                Check("the drawing plugin unloads and leaves the catalog: " + unloaded.Message, unloaded.Succeeded && catalog.Plugins.Count == 0);
            }

            return Distinct(contexts);
        }

        /// <summary>The rounding the drawing's one plugin run keeps.</summary>
        private static string Rounding(Drawing.Drawing drawing) => drawing.PluginRuns.Single().Settings.Single(s => s.Key == "Rounding").Value;

        /// <summary>A plate 400 by 400 by 25 with one bolt through it, 150 off its middle.</summary>
        private static DrawingSource PlateWithBolt(Tolerance tolerance)
        {
            GeoSolid3 plate = GeoSolid3.Extrude(new GeoPolygon2(new GeoPoint2(-200, -200), new GeoPoint2(200, -200), new GeoPoint2(200, 200), new GeoPoint2(-200, 200)), GeoCoordinateSystem3.Global, 25, tolerance);
            GeoSolid3 bolt = GeoSolid3.Cylinder(new GeoPoint3(150, 0, -30), new GeoPoint3(150, 0, 50), 10, 24, tolerance);
            return new DrawingSource(new[]
            {
                new DrawingObject("P-1", DrawingObjectKind.Part, plate, GeoCoordinateSystem3.Global, "PLATE", "P1", "PL25*400", "S355", "A-1", true),
                new DrawingObject("B-1", DrawingObjectKind.Bolt, bolt, new GeoCoordinateSystem3(new GeoPoint3(150, 0, -30), GeoVector3.ZAxis, GeoVector3.XAxis), "BOLT", "M20", "M20*80", "8.8", "A-1"),
            });
        }

        /// <summary>Runs NUMCIRCLES on three circles; the line it writes when it is done.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Run(CadCommandRegistry registry)
        {
            CadDocument document = Circles();
            var session = new CadCommandSession(document, registry);
            session.Feed("NUMCIRCLES");
            session.FeedSelection(new[] { "C1", "C2", "C3" });
            session.Enter();
            session.Enter();
            int texts = 0;

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                if (entity is DxfDBText)
                {
                    texts++;
                }
            }

            string last = session.History.Count > 0 ? session.History[session.History.Count - 1] : string.Empty;
            return texts == 3 && document.Undo() ? last : "texts: " + texts + ", last line: " + last;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Fails(CadCommandRegistry registry)
        {
            CadDocument document = Circles();
            var session = new CadCommandSession(document, registry);
            session.Feed("FAILNOW");
            session.Feed("now");
            int texts = 0;

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                if (entity is DxfDBText)
                {
                    texts++;
                }
            }

            return texts == 0 && !document.CanUndo && session.History.Contains("*Error* FAILNOW of the plugin " + Sample + " failed: The sample fails on purpose.");
        }

        /// <summary>
        /// Builds a sample's source with one line changed, outside the repository, against the assemblies of the types given;
        /// the path of the .dll.
        /// </summary>
        private static string BuildSecondVersion(string source, string folder, string sample, string from, string to, params Type[] references)
        {
            Directory.CreateDirectory(folder);
            string text = File.ReadAllText(source);

            if (!text.Contains(from, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The sample no longer has " + from + "; the runner changes that line to tell the builds apart.");
            }

            File.WriteAllText(Path.Combine(folder, Path.GetFileNameWithoutExtension(source)), text.Replace(from, to));
            var project = new StringBuilder("<Project Sdk=\"Microsoft.NET.Sdk\">\n");
            project.Append("  <PropertyGroup><TargetFramework>netstandard2.0</TargetFramework><LangVersion>latest</LangVersion><AssemblyName>" + sample + "</AssemblyName></PropertyGroup>\n");
            project.Append("  <ItemGroup>\n");

            foreach (Type type in references)
            {
                project.Append("    <Reference Include=\"" + type.Assembly.GetName().Name + "\"><HintPath>" + type.Assembly.Location + "</HintPath><Private>false</Private></Reference>\n");
            }

            project.Append("  </ItemGroup>\n</Project>\n");
            File.WriteAllText(Path.Combine(folder, sample + ".csproj"), project.ToString());
            string output = Path.Combine(folder, "out");
            var start = new ProcessStartInfo("dotnet", "build \"" + Path.Combine(folder, sample + ".csproj") + "\" -c Release -o \"" + output + "\" --nologo -v q")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = folder,
            };

            using (Process build = Process.Start(start))
            {
                string log = build.StandardOutput.ReadToEnd() + build.StandardError.ReadToEnd();

                if (!build.WaitForExit(180000) || build.ExitCode != 0)
                {
                    throw new InvalidOperationException("The second version did not build: " + log);
                }
            }

            return Path.Combine(output, sample + ".dll");
        }

        private static IEnumerable<WeakReference> Contexts()
        {
            foreach (AssemblyLoadContext context in AssemblyLoadContext.All)
            {
                if (context.IsCollectible && context.Name != null && context.Name.StartsWith("GeometryHelper plugin ", StringComparison.Ordinal))
                {
                    yield return new WeakReference(context);
                }
            }
        }

        private static WeakReference[] Distinct(List<WeakReference> contexts)
        {
            var distinct = new List<WeakReference>();

            foreach (WeakReference context in contexts)
            {
                if (!distinct.Exists(d => ReferenceEquals(d.Target, context.Target)))
                {
                    distinct.Add(context);
                }
            }

            return distinct.ToArray();
        }

        private static bool Collected(WeakReference[] contexts)
        {
            var clock = Stopwatch.StartNew();

            for (int i = 0; i < 20; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (Array.TrueForAll(contexts, c => !c.IsAlive))
                {
                    Console.WriteLine("  " + contexts.Length + " load context(s) collected after " + (i + 1) + " collection(s), " + Ms(clock));
                    return contexts.Length > 0;
                }
            }

            Console.WriteLine("  " + Array.FindAll(contexts, c => c.IsAlive).Length + " of " + contexts.Length + " load context(s) still alive");
            return false;
        }

        private static CadDocument Circles()
        {
            var text = new StringBuilder();
            text.Append("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nTABLES\n");
            text.Append("0\nTABLE\n2\nLTYPE\n70\n1\n0\nLTYPE\n2\nCONTINUOUS\n70\n0\n3\nSolid line\n72\n65\n73\n0\n40\n0\n0\nENDTAB\n");
            text.Append("0\nTABLE\n2\nLAYER\n70\n1\n0\nLAYER\n2\n0\n70\n0\n62\n7\n6\nCONTINUOUS\n0\nENDTAB\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nENTITIES\n");
            text.Append("0\nCIRCLE\n5\nC1\n8\n0\n10\n0\n20\n0\n30\n0\n40\n10\n");
            text.Append("0\nCIRCLE\n5\nC2\n8\n0\n10\n50\n20\n0\n30\n0\n40\n10\n");
            text.Append("0\nCIRCLE\n5\nC3\n8\n0\n10\n100\n20\n20\n30\n0\n40\n10\n");
            text.Append("0\nENDSEC\n0\nEOF\n");
            return CadDocument.From(DxfDatabase.Parse(text.ToString()), "circles.dxf");
        }

        private static void Check(string what, bool passed)
        {
            Console.WriteLine((passed ? "PASS " : "FAIL ") + what);

            if (!passed)
            {
                _failures++;
            }
        }

        private static void Expect(string what, string actual, string expected)
            => Check(what + (actual == expected ? string.Empty : ": \"" + actual + "\", not \"" + expected + "\""), actual == expected);

        private static string Ms(Stopwatch clock) => clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms";
    }
}
