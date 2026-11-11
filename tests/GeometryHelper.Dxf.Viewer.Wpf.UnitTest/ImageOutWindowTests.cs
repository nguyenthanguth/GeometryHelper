using System;
using System.IO;
using System.Threading;
using System.Windows;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// PNGOUT, JPGOUT and BMPOUT in the window, offscreen on an STA thread: the file asked through the Create Raster File
    /// function (stubbed, no dialog shown), then the canvas's scene written at the canvas's size and colours, as the renderer
    /// draws it, with no selection highlight or grips; its grid with all objects, the objects selected alone without it.
    /// </summary>
    public class ImageOutWindowTests
    {
        private const int Width = 800;
        private const int Height = 600;

        /// <summary>Runs a test on an STA thread with the room open, a canvas of 800 by 600 over it, and the folder of the room.</summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas, string, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out _)));
                        var canvas = new CadCanvas
                        {
                            Document = model.Document,
                            Space = model.Space,
                            View = model.View,
                            Session = model.Session,
                            Snap = model.Snap,
                        };
                        canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Options.Background = 0x203040;
                        canvas.Measure(new Size(Width, Height));
                        canvas.Arrange(new Rect(0, 0, Width, Height));
                        canvas.RenderNow();
                        test(model, canvas, folder.FullPath, bottom);
                        canvas.Dispose();
                    }
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
        }

        /// <summary>The renderer's own frame of the canvas's view: its scene, or the entities given alone.</summary>
        private static SKBitmap Frame(CadCanvas canvas, string only = null)
        {
            var frame = new SKBitmap(
                new SKImageInfo(canvas.PixelWidth, canvas.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul)
            );

            using (var surface = new SKCanvas(frame))
            using (var renderer = new SkiaCanvasRenderer())
            {
                if (only == null)
                {
                    renderer.RenderScene(surface, canvas.Document, canvas.Space, canvas.View, canvas.Options);
                }
                else
                {
                    renderer.RenderOnly(
                        surface,
                        canvas.Document,
                        canvas.Space,
                        canvas.View,
                        canvas.Options,
                        new[] { only }
                    );
                }
            }

            return frame;
        }

        /// <summary>The pixels of the image and the frame that differ, red, green and blue compared.</summary>
        private static int Differ(SKBitmap image, SKBitmap frame)
        {
            Assert.Equal(frame.Width, image.Width);
            Assert.Equal(frame.Height, image.Height);
            int count = 0;

            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    SKColor a = image.GetPixel(x, y);
                    SKColor b = frame.GetPixel(x, y);
                    count += a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue ? 0 : 1;
                }
            }

            return count;
        }

        [Fact]
        public void PngOut_AsksTheFileOfTheDialog_AndWritesTheCanvasScene_WithoutTheSelectionOrItsGrips()
        {
            OnCanvas(
                (model, canvas, folder, bottom) =>
                {
                    model.Document.Selection.Set(new[] { bottom });
                    canvas.RenderNow();
                    Assert.True(canvas.GripCount > 0);
                    string offeredFolder = null;
                    string offeredName = null;
                    string path = Path.Combine(folder, "room-shown.png");
                    model.AskImageFile = (where, name) =>
                    {
                        offeredFolder = where;
                        offeredName = name;
                        return path;
                    };

                    model.Execute("PNGOUT");
                    Assert.Equal("Select objects or <all objects and viewports>:", model.Session.PromptText);
                    model.Session.Enter();

                    Assert.False(model.Session.IsRunning);
                    Assert.Equal(folder, offeredFolder);
                    Assert.Equal("room.png", offeredName);
                    Assert.Equal(Width, canvas.PixelWidth);

                    using (SKBitmap image = SKBitmap.Decode(path))
                    using (SKBitmap frame = Frame(canvas))
                    {
                        Assert.Equal(Width, image.Width);
                        Assert.Equal(Height, image.Height);
                        Assert.Equal(new SKColor(0x20, 0x30, 0x40), image.GetPixel(0, 0));
                        Assert.Equal(0, Differ(image, frame));
                    }
                }
            );
        }

        [Fact]
        public void TheGridShown_IsWrittenWithAllObjects_AndNotWithTheObjectsSelected()
        {
            OnCanvas(
                (model, canvas, folder, bottom) =>
                {
                    canvas.ShowGrid = true;
                    canvas.RenderNow();
                    string all = Path.Combine(folder, "all.bmp");
                    string selected = Path.Combine(folder, "selected.bmp");
                    string next = all;
                    model.AskImageFile = (where, name) => next;

                    model.Execute("BMPOUT");
                    model.Session.Enter();
                    next = selected;
                    model.Execute("BMPOUT");
                    model.Session.FeedSelection(new[] { bottom });
                    model.Session.Enter();

                    using (SKBitmap image = SKBitmap.Decode(all))
                    using (SKBitmap frame = Frame(canvas))
                    {
                        Assert.True(Differ(image, frame) > 1000);
                    }

                    using (SKBitmap image = SKBitmap.Decode(selected))
                    using (SKBitmap frame = Frame(canvas, bottom))
                    using (SKBitmap scene = Frame(canvas))
                    {
                        Assert.Equal(0, Differ(image, frame));
                        Assert.True(Differ(image, scene) > 0);
                    }
                }
            );
        }

        [Fact]
        public void TheDialogCancelled_WritesNothing_AndNoDialogLeavesTheCommandLinesPrompt()
        {
            OnCanvas(
                (model, canvas, folder, bottom) =>
                {
                    model.AskImageFile = (where, name) => null;
                    model.Execute("JPGOUT");
                    Assert.False(model.Session.IsRunning);
                    Assert.Empty(Directory.GetFiles(folder, "*.jpg"));

                    model.AskImageFile = null;
                    model.Execute("JPGOUT");
                    Assert.Equal(
                        "Enter file name <" + Path.Combine(folder, "room.jpg") + ">:",
                        model.Session.PromptText
                    );
                    model.Session.Feed("typed");
                    model.Session.Enter();
                    Assert.True(File.Exists(Path.Combine(folder, "typed.jpg")));
                }
            );
        }
    }
}
