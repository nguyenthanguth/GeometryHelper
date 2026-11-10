using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The whole window content drawn offscreen on an STA thread, as the app's screenshot mode draws it, and its pixels read
    /// back; the PNG is kept in %TEMP%\gk\drawing\wpf-tests to be looked at.
    /// </summary>
    public class WorkspaceRenderTests
    {
        private const int Width = 1600;
        private const int Height = 960;

        [Fact]
        public void TheWorkspaceDrawnOffscreenShowsTheDrawingOnABlackCanvas()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                string png = Path.Combine(folder.FullPath, "workspace.png");
                byte[] pixels = null;
                Rect canvas = Rect.Empty;
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        pixels = Render(room, png, out canvas);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                Assert.Null(failure);
                Assert.True(new FileInfo(png).Length > 10000);

                // Not blank: the panels, the ribbon and the drawing give many colours.
                var colours = new HashSet<int>();

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    colours.Add(BitConverter.ToInt32(pixels, i));
                }

                Assert.True(colours.Count > 50, colours.Count + " colours");

                // The canvas sits between the two panels, 288 and 312 wide, and is black away from the drawing.
                Assert.Equal(288.0, canvas.X, 0);
                Assert.Equal(Width - 288.0 - 312.0, canvas.Width, 0);
                Assert.Equal(0x000000, Rgb(pixels, (int)canvas.Right - 4, (int)canvas.Top + 4));
                Assert.Equal(0x000000, Rgb(pixels, (int)canvas.Right - 4, (int)canvas.Bottom - 4));

                // The walls are drawn white, or grey where a line falls between pixels, across the canvas: the bottom wall of
                // 4 000 at 0.18 pixels to the unit is a line of some 720 pixels.
                int white = 0;

                for (int y = (int)canvas.Top; y < (int)canvas.Bottom; y++)
                {
                    for (int x = (int)canvas.Left; x < (int)canvas.Right; x++)
                    {
                        white += Grey(Rgb(pixels, x, y)) ? 1 : 0;
                    }
                }

                Assert.True(white > 1000, white + " white pixels");

                // The panels are the panel colour of the design, #262B33, below their headers.
                Assert.Equal(0x262B33, Rgb(pixels, 140, Height - 60));
                Assert.Equal(0x262B33, Rgb(pixels, Width - 6, Height - 60));
            }
        }

        private static bool Grey(int rgb) => ((rgb >> 16) & 0xFF) == (rgb & 0xFF) && ((rgb >> 8) & 0xFF) == (rgb & 0xFF) && (rgb & 0xFF) > 100;

        private static int Rgb(byte[] pixels, int x, int y)
        {
            int i = (y * Width + x) * 4;
            return (pixels[i + 2] << 16) | (pixels[i + 1] << 8) | pixels[i];
        }

        private static byte[] Render(string file, string png, out Rect canvas)
        {
            // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
            _ = PackUriHelper.UriSchemePack;
            var model = new MainViewModel();
            Assert.True(model.Open(file));
            var workspace = new CadWorkspace { DataContext = model };

            for (int pass = 0; pass < 2; pass++)
            {
                workspace.Measure(new Size(Width, Height));
                workspace.Arrange(new Rect(0, 0, Width, Height));
                workspace.UpdateLayout();
                workspace.DrawingCanvas.RenderNow();
            }

            CadCanvas drawing = workspace.DrawingCanvas;
            Point corner = drawing.TranslatePoint(new Point(0, 0), workspace);
            canvas = new Rect(corner, new Size(drawing.ActualWidth, drawing.ActualHeight));
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(workspace);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(png));

            using (FileStream stream = File.Create(png))
            {
                encoder.Save(stream);
            }

            var pixels = new byte[Width * Height * 4];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            drawing.Dispose();
            return pixels;
        }
    }
}
