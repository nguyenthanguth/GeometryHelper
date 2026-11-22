using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using GeometryHelper.Cad;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Cad.Wpf;
using GeometryHelper.Cad.Wpf.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Plugins.Net8Runner
{
    /// <summary>
    /// The plugin checks only .NET 8 can make: the sample plugin rebuilt while loaded and reloaded runs its new code, and
    /// its collectible load context is collected once it is unloaded. Prints PASS or FAIL per check; exits 1 on a failure.
    /// </summary>
    internal static class Program
    {
        private const string Sample = "GeometryHelper.Cad.SamplePlugin";
        private const string SampleUi = "GeometryHelper.Cad.SampleUiPlugin";
        private const string FirstDone = "Numbered {0} circles.";
        private const string SecondDone = "Second build: numbered {0} circles.";

        private static int _failures;

        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(), "gk-plugin-net8", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string beside = Path.Combine(AppContext.BaseDirectory, "SamplePlugin");
            var clock = Stopwatch.StartNew();

            try
            {
                string second = BuildSecondVersion(
                    Path.Combine(beside, "CircleTools.cs.txt"),
                    Path.Combine(root, "second"),
                    Sample,
                    "\"" + FirstDone + "\"",
                    "\"" + SecondDone + "\"",
                    typeof(CadPlugin),
                    typeof(DxfDatabase)
                );
                Console.WriteLine("Built the second version in " + Ms(clock));

                WeakReference[] contexts = ReloadAndUnload(root, Path.Combine(beside, Sample + ".dll"), second);
                Check("every load context of the sample is collected after unloading", Collected(contexts));

                WeakReference[] installed = LoadFromThePluginsFolder(root, Path.Combine(beside, Sample + ".dll"));
                Check(
                    "the load context of the plugin loaded from the Plugins folder is collected after unloading",
                    Collected(installed)
                );

                WeakReference[] info = LoadDrawingInfoFromThePluginsFolder(root);
                Check(
                    "the load context of the sample that ships in Plugins\\DrawingInfo is collected after unloading",
                    Collected(info)
                );

                string uiBuilt = Path.Combine(AppContext.BaseDirectory, "SampleUiPlugin", SampleUi + ".dll");
                Check(
                    "the plugin that adds to the window, its tab, Properties, menus and snap shown, is collected after unloading",
                    UiPluginIsCollected(root, uiBuilt, "plain", finderThrows: false)
                );
                string failing = BuildFailingFinder(
                    Path.Combine(AppContext.BaseDirectory, "SampleUiPlugin", "CircleUi.cs.txt"),
                    Path.Combine(root, "failing")
                );
                Check(
                    "the same, where the finder of its snap threw before the unload, is collected after unloading",
                    UiPluginIsCollected(root, failing, "failing", finderThrows: true)
                );
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

            Console.WriteLine(
                (_failures == 0 ? "ALL PASSED" : _failures + " FAILED")
                    + " in "
                    + Ms(clock)
                    + " on "
                    + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            );
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

            using (
                var manager = new CadPluginManager(
                    new CadPluginAssemblyLoader(),
                    Path.Combine(root, "plugins"),
                    Path.Combine(root, "plugins.json"),
                    Path.Combine(root, "copies")
                )
            )
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
                Expect(
                    "the first build numbers the circles",
                    Run(registry),
                    string.Format(CultureInfo.InvariantCulture, FirstDone, 3)
                );

                // Built again while loaded: the copy is loaded, so the file is not locked.
                File.Copy(second, path, true);
                Check("the .dll loaded is not locked, so a new build overwrites it", true);

                clock.Restart();
                CadPluginResult reloaded = manager.Reload(Sample);
                Console.WriteLine("  reload " + Ms(clock));
                Check(
                    "the sample reloads with no warning: " + reloaded,
                    reloaded.Succeeded && reloaded.Warnings.Count == 0
                );
                contexts.AddRange(Contexts());
                Expect(
                    "the new build runs after PLUGINRELOAD, without a restart",
                    Run(registry),
                    string.Format(CultureInfo.InvariantCulture, SecondDone, 3)
                );
                Check("a failing command changes nothing and names the plugin", Fails(registry));

                clock.Restart();
                CadPluginResult unloaded = manager.Unload(Sample);
                Console.WriteLine("  unload " + Ms(clock));
                Check(
                    "the sample unloads: " + unloaded.Message,
                    unloaded.Succeeded && unloaded.Message == "Unloaded " + Sample + "."
                );
                Check("its commands are gone", registry.Find("NUMCIRCLES") == null && registry.Find("NC") == null);
            }

            return Distinct(contexts);
        }

        /// <summary>Puts the sample in a folder of the Plugins folder with a plugin.json, loads it at start, runs it and unloads it.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] LoadFromThePluginsFolder(string root, string built)
        {
            string plugins = Path.Combine(root, "installed");
            string folder = Path.Combine(plugins, "number-circles");
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, "plugin.json"),
                "{ \"id\": \"number-circles\", \"name\": \"Number circles\", \"version\": \"1.2.0\", \"kind\": \"command\", \"minimumApiVersion\": \"1.0\", \"commands\": [ \"NUMCIRCLES\" ] }"
            );
            File.Copy(built, Path.Combine(folder, Sample + ".dll"));
            var contexts = new List<WeakReference>();
            var registry = CadCommandRegistry.CreateDefault();

            using (
                var manager = new CadPluginManager(
                    new CadPluginAssemblyLoader(),
                    plugins,
                    Path.Combine(root, "installed.json"),
                    Path.Combine(root, "copies2")
                )
            )
            {
                manager.Attach(registry);
                IReadOnlyList<CadPluginResult> started = manager.LoadAtStart();
                Check(
                    "the plugin folder loads at start with no question: " + string.Join(" ", started),
                    started.Count == 1
                        && started[0].Succeeded
                        && started[0].Plugin.Name == "Number circles"
                        && started[0].Plugin.IsTrusted
                );
                contexts.AddRange(Contexts());
                Expect("it runs", Run(registry), string.Format(CultureInfo.InvariantCulture, FirstDone, 3));
                CadPluginResult unloaded = manager.Unload("Number circles");
                Check("it unloads: " + unloaded.Message, unloaded.Succeeded);
            }

            return Distinct(contexts);
        }

        /// <summary>
        /// Loads the sample that ships in Plugins\DrawingInfo from a Plugins folder at start, runs GHDWGINFO on a drawing of
        /// three circles, and unloads it; gives its load contexts.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] LoadDrawingInfoFromThePluginsFolder(string root)
        {
            string plugins = Path.Combine(root, "installed-info");
            string folder = Path.Combine(plugins, "DrawingInfo");
            Directory.CreateDirectory(folder);

            foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "DrawingInfo")))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }

            var contexts = new List<WeakReference>();
            var registry = CadCommandRegistry.CreateDefault();

            using (
                var manager = new CadPluginManager(
                    new CadPluginAssemblyLoader(),
                    plugins,
                    Path.Combine(root, "installed-info.json"),
                    Path.Combine(root, "copies-info")
                )
            )
            {
                manager.Attach(registry);
                IReadOnlyList<CadPluginResult> started = manager.LoadAtStart();
                Check(
                    "the Drawing info plugin loads at start with no question: " + string.Join(" ", started),
                    started.Count == 1
                        && started[0].Succeeded
                        && started[0].Plugin.Name == "DrawingInfo"
                        && started[0].Plugin.IsTrusted
                );
                contexts.AddRange(Contexts());

                var session = new CadCommandSession(Circles(), registry);
                session.Feed("GHDWGINFO");
                Check(
                    "GHDWGINFO writes the objects by type: " + string.Join(" | ", session.History),
                    session.History.Contains("Model space: 3 objects") && session.History.Contains("  Circle   3")
                );
                CadPluginResult unloaded = manager.Unload("DrawingInfo");
                Check("it unloads: " + unloaded.Message, unloaded.Succeeded);
            }

            return Distinct(contexts);
        }

        /// <summary>
        /// Shows what the plugin that adds to the window adds, in a headless window model that stays alive as the window does in
        /// a running presenter, unloads the plugin, and tells whether its load context is collected while the model lives on.
        /// Runs on an STA thread, as the window's own code does.
        /// </summary>
        private static bool UiPluginIsCollected(string root, string built, string label, bool finderThrows)
        {
            bool collected = false;
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    collected = ShowAndUnloadUiPlugin(root, built, label, finderThrows);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                Check(label + ": the run ends without an exception: " + failure, false);
            }

            return collected;
        }

        private static bool ShowAndUnloadUiPlugin(string root, string built, string label, bool finderThrows)
        {
            string dev = Path.Combine(root, "ui-" + label);
            Directory.CreateDirectory(dev);
            string path = Path.Combine(dev, SampleUi + ".dll");
            File.Copy(built, path);
            string drawing = Path.Combine(dev, "circles.dxf");
            Circles().Database.Save(drawing);

            using (
                var manager = new CadPluginManager(
                    new CadPluginAssemblyLoader(),
                    Path.Combine(dev, "plugins"),
                    Path.Combine(dev, "plugins.json"),
                    Path.Combine(dev, "copies")
                )
            )
            {
                manager.ConfirmUntrusted = p => true;
                var model = new MainViewModel();
                model.UsePlugins(manager, loadAtStart: false);
                Check(label + ": the drawing opens", model.Open(drawing));

                WeakReference[] contexts = ShowUiPlugin(model, path, label, finderThrows);
                CadPluginResult unloaded = model.UnloadPlugin(SampleUi);
                Check(label + ": the plugin unloads: " + unloaded.Message, unloaded.Succeeded);
                Check(label + ": nothing of it is left in the window", NothingOfTheUiPluginIsLeft(model));

                // The model, with its snaps, Properties and menus, is alive here, as the window is while the user works on.
                bool collected = Collected(contexts);
                GC.KeepAlive(model);
                return collected;
            }
        }

        /// <summary>
        /// Loads the plugin, selects its tab, selects a circle, reads the Properties, canvas, DRAWINGS and OSNAP menus, turns
        /// its snap on and asks it twice at the top of a circle; gives its load context.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] ShowUiPlugin(MainViewModel model, string path, string label, bool finderThrows)
        {
            // Load contexts of earlier checks, held here so that only the one this load makes is told apart.
            var known = new List<object>();

            foreach (WeakReference context in Contexts())
            {
                known.Add(context.Target);
            }

            CadPluginResult loaded = model.LoadPlugin(path);
            Check(label + ": the plugin loads: " + loaded, loaded.Succeeded);
            WeakReference[] contexts = Contexts().Where(c => !known.Contains(c.Target)).ToArray();
            Check(label + ": it has a collectible load context of its own", contexts.Length == 1);

            Check(
                label + ": its tab Circles is after Plugins",
                model.PluginTabs.Count == 1 && model.PluginTabs[0].TabHeader == "Circles"
            );
            model.SelectedTab = model.PluginTabs[0];
            model.Document.Selection.Set(new[] { "C1" });
            Check(label + ": its tab Circle is shown for a circle", model.IsPluginContextTabSelected);
            Check(
                label + ": Properties shows its group Circle tools",
                model.PropertyGroups.Any(g => g.Title == "Circle tools")
            );
            FileRowViewModel row = model.FileRows.First(r => !r.IsHeader);
            Check(
                label + ": its DRAWINGS button, row items, canvas items and snap are listed",
                model.FileListButtons.Count == 1
                    && model.FileRowMenu(row).Any(i => i.Header == "Circle report")
                    && model.CanvasMenu().Any(i => i.Header == "Number these")
                    && model.OsnapMenu().Any(i => i.Header == "Circle top")
            );

            // The snap on, with no built-in snap, asked 0.5 right of and 0.2 over the top of the circle C1 about (0, 0) of radius 10.
            model.OsnapMenu().First(i => i.Header == "Circle top").Command.Execute(null);
            model.SnapModes = CadSnapModes.None;
            int lines = model.History.Count;
            CadSnapResult first = SnapAt(model, new GeoPoint2(0.5, 10.2), 3.0);
            SnapAt(model, new GeoPoint2(0.5, 10.2), 3.0);
            int said = model.History.Skip(lines).Count(l => l.Contains("failed and is off"));

            if (finderThrows)
            {
                Check(
                    label + ": the finder threw once, the window said so once, and the snap gave no point",
                    said == 1 && first.Kind != CadSnapKind.Custom
                );
            }
            else
            {
                Check(
                    label + ": the snap gives the top of the circle",
                    said == 0 && first.Kind == CadSnapKind.Custom && first.CustomSnap.Name == "Circle top"
                );
            }

            return contexts;
        }

        /// <summary>Whether the window shows nothing of the plugin: no tab, no Properties group, no button, no menu item, no snap.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool NothingOfTheUiPluginIsLeft(MainViewModel model)
        {
            FileRowViewModel row = model.FileRows.First(r => !r.IsHeader);
            return model.PluginTabs.Count == 0
                && model.PluginContextTabs.Count == 0
                && model.IsHomeTabSelected
                && model.FileListButtons.Count == 0
                && !model.PropertyGroups.Any(g => g.Title == "Circle tools")
                && !model.FileRowMenu(row).Any(i => i.Header == "Circle report")
                && !model.CanvasMenu().Any(i => i.Header == "Number these")
                && !model.OsnapMenu().Any(i => i.Header == "Circle top");
        }

        /// <summary>
        /// What the canvas asks when the cursor moves: <c>CadPicking.Snap</c>, which the window keeps to itself, found by
        /// reflection so that the runner needs no access to its internals.
        /// </summary>
        private static CadSnapResult SnapAt(MainViewModel model, GeoPoint2 cursor, double aperture)
        {
            MethodInfo snap = typeof(MainViewModel)
                .Assembly.GetType("GeometryHelper.Cad.Wpf.CadPicking", true)
                .GetMethod("Snap", BindingFlags.Public | BindingFlags.Static);
            return (CadSnapResult)
                snap.Invoke(
                    null,
                    new object[] { model.Document, model.Document.ModelSpace, model.Snap, cursor, aperture, null, null }
                );
        }

        /// <summary>
        /// Builds the sample that adds to the window with the finder of its snap changed to throw, and its icon drawn from a path
        /// where it was an embedded picture the build here does not carry; the path of the .dll.
        /// </summary>
        private static string BuildFailingFinder(string source, string folder)
        {
            const string picture = "CadPluginIcon.FromResource(typeof(CircleUi).Assembly, IconResource)";
            string text = File.ReadAllText(source);

            if (!text.Contains(picture, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The sample no longer reads its icon with " + picture + ".");
            }

            Directory.CreateDirectory(folder);
            string edited = Path.Combine(folder, "CircleUi.cs.txt");
            File.WriteAllText(edited, text.Replace(picture, "CadPluginIcon.FromPathData(\"M3 3 H13 V13 H3 Z\")"));
            return BuildSecondVersion(
                edited,
                Path.Combine(folder, "build"),
                SampleUi,
                "yield return new GeoPoint2(circle.Center.X, circle.Center.Y + circle.Radius);",
                "throw new InvalidOperationException(\"The finder fails on purpose.\"); yield break;",
                typeof(CadPlugin),
                typeof(DxfDatabase)
            );
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

            return texts == 0
                && !document.CanUndo
                && session.History.Contains(
                    "*Error* FAILNOW of the plugin " + Sample + " failed: The sample fails on purpose."
                );
        }

        /// <summary>
        /// Builds a sample's source with one line changed, outside the repository, against the assemblies of the types given;
        /// the path of the .dll.
        /// </summary>
        private static string BuildSecondVersion(
            string source,
            string folder,
            string sample,
            string from,
            string to,
            params Type[] references
        )
        {
            Directory.CreateDirectory(folder);
            string text = File.ReadAllText(source);

            if (!text.Contains(from, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The sample no longer has " + from + "; the runner changes that line to tell the builds apart."
                );
            }

            File.WriteAllText(Path.Combine(folder, Path.GetFileNameWithoutExtension(source)), text.Replace(from, to));
            var project = new StringBuilder("<Project Sdk=\"Microsoft.NET.Sdk\">\n");
            project.Append(
                "  <PropertyGroup><TargetFramework>netstandard2.0</TargetFramework><LangVersion>latest</LangVersion><AssemblyName>"
                    + sample
                    + "</AssemblyName></PropertyGroup>\n"
            );
            project.Append("  <ItemGroup>\n");

            foreach (Type type in references)
            {
                project.Append(
                    "    <Reference Include=\""
                        + type.Assembly.GetName().Name
                        + "\"><HintPath>"
                        + type.Assembly.Location
                        + "</HintPath><Private>false</Private></Reference>\n"
                );
            }

            project.Append("  </ItemGroup>\n</Project>\n");
            File.WriteAllText(Path.Combine(folder, sample + ".csproj"), project.ToString());
            string output = Path.Combine(folder, "out");
            var start = new ProcessStartInfo(
                "dotnet",
                "build \""
                    + Path.Combine(folder, sample + ".csproj")
                    + "\" -c Release -o \""
                    + output
                    + "\" --nologo -v q"
            )
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
                if (
                    context.IsCollectible
                    && context.Name != null
                    && context.Name.StartsWith("GeometryHelper plugin ", StringComparison.Ordinal)
                )
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
                    Console.WriteLine(
                        "  "
                            + contexts.Length
                            + " load context(s) collected after "
                            + (i + 1)
                            + " collection(s), "
                            + Ms(clock)
                    );
                    return contexts.Length > 0;
                }
            }

            Console.WriteLine(
                "  "
                    + Array.FindAll(contexts, c => c.IsAlive).Length
                    + " of "
                    + contexts.Length
                    + " load context(s) still alive"
            );
            return false;
        }

        private static CadDocument Circles()
        {
            var text = new StringBuilder();
            text.Append("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nTABLES\n");
            text.Append(
                "0\nTABLE\n2\nLTYPE\n70\n1\n0\nLTYPE\n2\nCONTINUOUS\n70\n0\n3\nSolid line\n72\n65\n73\n0\n40\n0\n0\nENDTAB\n"
            );
            text.Append(
                "0\nTABLE\n2\nLAYER\n70\n1\n0\nLAYER\n2\n0\n70\n0\n62\n7\n6\nCONTINUOUS\n0\nENDTAB\n0\nENDSEC\n"
            );
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

        private static void Expect(string what, string actual, string expected) =>
            Check(
                what + (actual == expected ? string.Empty : ": \"" + actual + "\", not \"" + expected + "\""),
                actual == expected
            );

        private static string Ms(Stopwatch clock) =>
            clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms";
    }
}
