using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Viewer.Scripting;

namespace GeometryHelper.Viewer.Runner
{
    /// <summary>
    /// <c>GeometryHelper.Viewer.Runner script.csx --out scene.ghscene.json [--measure]</c>: runs the script the way the viewer does, with the
    /// GeometryHelper.dll that stands beside this program, and writes the scene it builds. With <c>--measure</c> every item also carries the
    /// numbers this build of the library gives for it (<see cref="RunnerMeasures"/>), for the comparison of two builds.
    /// </summary>
    /// <remarks>
    /// Standard output carries <c>library:</c> (which GeometryHelper.dll was loaded), <c>note:</c> (a line the script said with Note) and <c>ok:</c>;
    /// standard error carries the errors, a compile error as <c>file.csx(3,14): error CS0029: ...</c>. The exit code is 0 when the scene
    /// was written, 1 when the script did not compile or ended with an exception (no scene is written), and 2 for a bad command line.
    /// </remarks>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // The hook goes in before any method that names a type of the library is compiled, which is why the work is in another method.
            UseTheLibraryBesideThisProgram();
            return Execute(args).GetAwaiter().GetResult();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<int> Execute(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.Out.NewLine = "\n";
            Console.Error.NewLine = "\n";

            string script = null;
            string output = null;
            bool measure = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    output = args[++i];
                }
                else if (string.Equals(args[i], "--measure", StringComparison.OrdinalIgnoreCase))
                {
                    measure = true;
                }
                else if (script == null && !args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    script = args[i];
                }
                else
                {
                    Console.Error.WriteLine("Unknown argument " + args[i] + ".");
                    return 2;
                }
            }

            if (script == null || output == null)
            {
                Console.Error.WriteLine("Usage: GeometryHelper.Viewer.Runner script.csx --out scene.ghscene.json [--measure]");
                return 2;
            }

            if (!File.Exists(script))
            {
                Console.Error.WriteLine("No such script: " + script);
                return 2;
            }

            Assembly library = typeof(Tolerance).Assembly;
            Console.WriteLine("library: " + library.Location + " " + library.GetName().Version);

            try
            {
                if (File.Exists(output))
                {
                    File.Delete(output);
                }

                ScriptRunResult run = await ScriptHost.RunAsync(
                    File.ReadAllText(script, Encoding.UTF8),
                    script,
                    line => Console.WriteLine("note: " + line.Replace("\r", string.Empty).Replace("\n", "\\n")),
                    CancellationToken.None);

                if (!run.Succeeded)
                {
                    foreach (string error in run.Errors)
                    {
                        Console.Error.WriteLine(error);
                    }

                    return 1;
                }

                if (measure)
                {
                    RunnerMeasures.Add(run.Scene);
                }

                new SceneWriter(run.Scene).Save(output);
                Console.WriteLine("ok: " + run.Scene.Items.Count + " item(s) on " + run.Scene.Layers.Count + " layer(s) in " + run.Elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s");
                return 0;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Console.Error.WriteLine(script + ": " + e.GetType().Name + ": " + e.Message);
                return 1;
            }
        }

        /// <summary>
        /// Makes a request for GeometryHelper.dll end in the file beside this program even where its version is older than the one the viewer
        /// was built against: the runtime refuses a lower version at once, and a build of last year's library is the point of the comparison.
        /// </summary>
        private static void UseTheLibraryBesideThisProgram()
        {
            string beside = Path.Combine(AppContext.BaseDirectory, "GeometryHelper.dll");

            AssemblyLoadContext.Default.Resolving += (context, name) =>
                name.Name == "GeometryHelper" && File.Exists(beside) ? context.LoadFromAssemblyPath(beside) : null;
        }
    }
}
