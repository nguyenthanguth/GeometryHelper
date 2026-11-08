using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GeometryHelper.Dxf.Viewer.Wpf;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>
    /// The workspace with a model drawing open and each dialog of the model drawing, drawn offscreen on an STA thread as the
    /// app's screenshot mode draws them, and their pixels read back; the PNGs are kept in %TEMP%\gk\drawing\wpf-b-tests.
    /// </summary>
    public class RenderTests
    {
        private static readonly string Folder = Path.Combine(Path.GetTempPath(), "gk", "drawing", "wpf-b-tests");

        [Fact]
        public void TheWorkspaceWithAModelDrawingShowsTheDrawingTabAndTheSheetOnTheCanvas()
        {
            Dictionary<string, byte[]> shots = OnSta(() =>
            {
                var model = new MainViewModel();
                var workspace = new CadWorkspace { DataContext = model };

                // The dialogs not hooked, as the app's screenshot mode: a dialog shown would wait for a click.
                var drawings = new DrawingWorkspace(model, DrawingWorkspaceExtensions.DefaultSources(Bench.Tol), Bench.Tol);
                drawings.TabContent = new DrawingRibbonTab { DataContext = drawings };
                NewDrawingViewModel dialog = drawings.ShowNewDrawing();
                dialog.NextAsync().Pump();
                dialog.NextAsync().Pump();
                Assert.NotNull(dialog.NextAsync().Pump());
                model.SelectedTab = drawings;
                return new Dictionary<string, byte[]> { ["workspace"] = Render(workspace, 1600, 960, "workspace-b1.png") };
            });

            byte[] pixels = shots["workspace"];

            // The views of the sheet are drawn on the canvas between the panels 288 and 312 wide: many pixels not black.
            int drawn = 0;

            for (int y = 160; y < 860; y++)
            {
                for (int x = 290; x < 1286; x++)
                {
                    drawn += Rgb(pixels, 1600, x, y) != 0x000000 ? 1 : 0;
                }
            }

            Assert.True(drawn > 3000, drawn + " pixels drawn");

            // The Drawing tab is shown, selected, after Home: the selected colour behind its name; the file row of B1 selected too.
            Assert.Equal(0x1F4E79, Rgb(pixels, 1600, 74, 42));
            Assert.Equal(0x1F4E79, Rgb(pixels, 1600, 150, 335));
        }

        [Fact]
        public void EveryDialogDrawsOnThePanelColour()
        {
            Dictionary<string, byte[]> shots = OnSta(() =>
            {
                var bench = new Bench();
                ModelDrawing made = bench.MakeAsync().Pump();
                NewDrawingViewModel creating = bench.Workspace.ShowNewDrawing();
                creating.NextAsync().Pump();
                UpdateFromModelViewModel update = bench.Workspace.ShowUpdate();
                update.Source = bench.Changed;
                update.PreviewAsync().Pump();
                var dialogs = new Dictionary<string, DialogViewModel>
                {
                    ["new"] = creating,
                    ["update"] = update,
                    ["settings"] = bench.Workspace.ShowSettings(),
                    ["export"] = bench.Workspace.ShowExport(),
                    ["message"] = MessageViewModel.Failure("Drawing not made", made.Number + " could not be made.", new InvalidOperationException("The settings allow no scale.")),
                };
                var rendered = new Dictionary<string, byte[]>();

                foreach (KeyValuePair<string, DialogViewModel> pair in dialogs)
                {
                    DrawingDialogWindow window = DrawingWorkspaceExtensions.CreateDialog(pair.Value);
                    FrameworkElement body = window.Body;
                    int width = (int)window.Width;
                    int height = double.IsNaN(window.Height) ? 200 : (int)window.Height;
                    window.Content = null;
                    body.DataContext = pair.Value;
                    rendered[pair.Key] = Render(body, width, height, "dialog-" + pair.Key + ".png");
                    window.Close();
                }

                return rendered;
            });

            Assert.Equal(5, shots.Count);

            foreach (KeyValuePair<string, byte[]> shot in shots)
            {
                var colours = new HashSet<int>();

                for (int i = 0; i < shot.Value.Length; i += 4)
                {
                    colours.Add(BitConverter.ToInt32(shot.Value, i));
                }

                Assert.True(colours.Count > 20, shot.Key + ": " + colours.Count + " colours");
                Assert.Contains(0x262B33, colours.Select(c => c & 0xFFFFFF));
            }
        }

        private static T OnSta<T>(Func<T> work)
        {
            T result = default;
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    result = work();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromMinutes(2)), "the render did not end in 2 minutes");
            Assert.Null(failure);
            return result;
        }

        private static byte[] Render(FrameworkElement root, int width, int height, string name)
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

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            Directory.CreateDirectory(Folder);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (FileStream stream = File.Create(Path.Combine(Folder, name)))
            {
                encoder.Save(stream);
            }

            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            return pixels;
        }

        private static int Rgb(byte[] pixels, int width, int x, int y)
        {
            int i = (y * width + x) * 4;
            return (pixels[i + 2] << 16) | (pixels[i + 1] << 8) | pixels[i];
        }
    }
}
