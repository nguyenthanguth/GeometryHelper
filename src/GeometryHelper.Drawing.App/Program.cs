using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.App
{
    /// <summary>
    /// Starts GeometryHelper Drawing. <c>GeometryHelper.Drawing.App.exe plan.dxf</c> opens the window on a drawing;
    /// <c>--screenshot out.png plan.dxf [--size 1600 960] [--space "Layout1"] [--select H1,H2] [--run "MOVE;0,0"] [--cursor x,y]
    /// [--layers]</c> draws the window with the drawing open into a PNG, with no window shown, and exits: 0 when written, 2
    /// for arguments or a file it cannot read.
    /// <c>--run</c> types lines on the command line, parted by semicolons; <c>--cursor</c> puts the cursor on a point of the
    /// drawing, showing the snap and the rubber band of the prompt waiting; <c>--toggle</c> switches status toggles, as GRID;
    /// <c>--layers</c> draws the Layer manager instead.
    /// </summary>
    public static class Program
    {
        /// <summary>The entry point.</summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The exit code.</returns>
        [STAThread]
        public static int Main(string[] args)
        {
            args = args ?? Array.Empty<string>();

            if (args.Length > 0 && string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase))
            {
                return Screenshot(args);
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            var model = new MainViewModel();
            var window = new MainWindow(model);
            string file = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

            if (file != null)
            {
                model.Open(file);
            }

            return app.Run(window);
        }

        /// <summary>Draws the window with a drawing open into a PNG, with no window shown.</summary>
        private static int Screenshot(string[] args)
        {
            string output = null;
            string file = null;
            string spaceName = null;
            string run = null;
            double[] cursor = null;
            bool layers = false;
            var toggles = new List<string>();
            var select = new List<string>();
            int width = 1600;
            int height = 960;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];

                if (string.Equals(arg, "--size", StringComparison.OrdinalIgnoreCase) && i + 2 < args.Length)
                {
                    width = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    height = int.Parse(args[++i], CultureInfo.InvariantCulture);
                }
                else if (string.Equals(arg, "--space", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    spaceName = args[++i];
                }
                else if (string.Equals(arg, "--select", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    select.AddRange(args[++i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(h => h.Trim()));
                }
                else if (string.Equals(arg, "--run", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    run = args[++i];
                }
                else if (string.Equals(arg, "--cursor", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cursor = args[++i].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                }
                else if (string.Equals(arg, "--toggle", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    toggles.AddRange(args[++i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpperInvariant()));
                }
                else if (string.Equals(arg, "--layers", StringComparison.OrdinalIgnoreCase))
                {
                    layers = true;
                }
                else if (output == null)
                {
                    output = arg;
                }
                else if (file == null)
                {
                    file = arg;
                }
            }

            if (output == null || width < 100 || height < 100 || (cursor != null && cursor.Length != 2))
            {
                Console.Error.WriteLine("Usage: GeometryHelper.Drawing.App --screenshot out.png [file.dxf] [--size 1600 960] [--space \"Layout1\"] [--select H1,H2] [--run \"MOVE;0,0\"] [--cursor x,y] [--toggle GRID,SNAP] [--layers]");
                return 2;
            }

            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var model = new MainViewModel();

            foreach (StatusToggleViewModel toggle in model.Toggles.Where(t => toggles.Contains(t.Name)))
            {
                toggle.IsOn = !toggle.IsOn;
            }

            if (file != null && !model.Open(file))
            {
                Console.Error.WriteLine(model.History.Last());
                return 2;
            }

            if (spaceName != null)
            {
                CadSpace space = model.Document?.Spaces.FirstOrDefault(s => string.Equals(s.Name, spaceName, StringComparison.OrdinalIgnoreCase));

                if (space == null)
                {
                    Console.Error.WriteLine("The drawing has no space named " + spaceName + ".");
                    return 2;
                }

                model.SelectSpace(space);
            }

            FrameworkElement root;
            Window window;

            if (layers)
            {
                LayerManagerViewModel manager = model.CreateLayerManager();

                if (manager == null)
                {
                    Console.Error.WriteLine("--layers needs a drawing.");
                    return 2;
                }

                var dialog = new LayerManagerWindow(manager);
                manager.SelectedRow = manager.Rows.FirstOrDefault(r => r.IsCurrent);
                root = dialog.Body;
                window = dialog;
                width = (int)dialog.Width;
                height = (int)dialog.Height;
            }
            else
            {
                window = new MainWindow(model);
                root = (FrameworkElement)window.Content;
            }

            window.Content = null;
            root.DataContext = layers ? (object)((LayerManagerWindow)window).Model : model;
            Layout(root, width, height);

            if (select.Count > 0 && model.Document != null)
            {
                model.Document.Selection.Set(select);
                Layout(root, width, height);
            }

            if (run != null)
            {
                // Each part is one line of the command line; an empty part is Enter.
                foreach (string line in run.Split(';'))
                {
                    model.Execute(line);
                }

                Layout(root, width, height);
            }

            if (cursor != null && root is CadWorkspace workspace)
            {
                workspace.DrawingCanvas.MoveCursorTo(cursor[0], cursor[1]);
                Layout(root, width, height);
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string folder = Path.GetDirectoryName(Path.GetFullPath(output));
            Directory.CreateDirectory(folder);

            using (FileStream stream = File.Create(output))
            {
                encoder.Save(stream);
            }

            window.Close();
            Console.WriteLine("Wrote " + Path.GetFullPath(output) + " (" + width + " by " + height + ").");
            return 0;
        }

        /// <summary>Lays the content out at a size and draws the canvas at once, as no frame will come to draw it.</summary>
        private static void Layout(FrameworkElement root, int width, int height)
        {
            root.Width = width;
            root.Height = height;
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            if (root is CadWorkspace workspace)
            {
                workspace.DrawingCanvas.RenderNow();
                root.UpdateLayout();
            }
        }
    }
}
