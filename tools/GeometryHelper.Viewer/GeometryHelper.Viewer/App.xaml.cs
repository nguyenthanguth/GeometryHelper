using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The application: it reads the command line and either opens the window or, for <c>--check</c>, only loads the file
    /// and says whether it loads.
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        internal CommandLine CommandLine { get; private set; }

        /// <summary>
        /// Says what went wrong in the output pane and in the error log, and carries on: a fault in drawing one frame is not worth losing a scene for.
        /// </summary>
        private void Report(Exception exception)
        {
            string text = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture) + " " + exception + Environment.NewLine;
            Console.Error.WriteLine(text);

            try
            {
                Directory.CreateDirectory(Settings.Folder);
                File.AppendAllText(Path.Combine(Settings.Folder, "error.log"), text);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }

            (MainWindow as MainWindow)?.Report("Error: " + exception.Message + " (details in " + Path.Combine(Settings.Folder, "error.log") + ")");
        }

        /// <summary>
        /// Starts the viewer.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // A window application has no console of its own; this one writes to the console it was started from, if any.
            AttachConsole(-1);

            DispatcherUnhandledException += (s, args) =>
            {
                Report(args.Exception);
                args.Handled = true;
            };

            CommandLine = CommandLine.Parse(e.Args);

            if (CommandLine.Problem != null)
            {
                Console.Error.WriteLine(CommandLine.Problem);
                Shutdown(2);
                return;
            }

            if (CommandLine.Check)
            {
                Shutdown(HeadlessCheck.Run(CommandLine.File));
                return;
            }

            MainWindow = new MainWindow(CommandLine);
            MainWindow.Show();
        }
    }

    /// <summary>
    /// <c>--check</c>: loads a script or a scene file and builds what it would draw, with no window, so that a build or a
    /// person can tell whether a file opens.
    /// </summary>
    internal static class HeadlessCheck
    {
        internal static int Run(string path)
        {
            try
            {
                return Check(path);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Console.Error.WriteLine("FAILED " + path + ": " + e.GetType().Name + ": " + e.Message);
                return 1;
            }
        }

        private static int Check(string path)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("FAILED " + path + ": no such file.");
                return 1;
            }

            LoadResult result = SceneLoader.LoadAsync(path, line => Console.WriteLine("note: " + line), CancellationToken.None).GetAwaiter().GetResult();

            foreach (string error in result.Errors)
            {
                Console.Error.WriteLine(error);
            }

            if (!result.Succeeded)
            {
                Console.Error.WriteLine("FAILED " + path + ": " + result.Errors.Count + " error(s).");
                return 1;
            }

            Primitives primitives = Primitives.Build(result.Scene);

            foreach (string warning in primitives.Warnings)
            {
                Console.WriteLine("warning: " + warning);
            }

            foreach (string warning in result.Scene.LoadWarnings)
            {
                Console.WriteLine("warning: " + warning);
            }

            foreach (string kind in result.Scene.UnknownKinds)
            {
                Console.WriteLine("warning: the kind '" + kind + "' is kept but not drawn.");
            }

            Console.WriteLine(
                "OK " + path + ": " + result.Scene.Items.Count + " item(s) on " + result.Scene.Layers.Count + " layer(s), bounds " + primitives.Bounds + ", "
                + primitives.Items.Sum(i => i.Triangles.Count) + " triangle(s), " + primitives.Items.Sum(i => i.Polylines.Count) + " line(s), "
                + primitives.Warnings.Count + " warning(s).");
            return 0;
        }
    }
}
