using System;
using System.Diagnostics;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Wpf;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    [Collection(ViewerSpeedCollection.Name)]
    public class SpeedTests
    {
        /// <summary>
        /// A drawing of 100 000 entities (lines, arcs, circles, polylines, texts and 2 000 INSERTs of a block of 20) opened from
        /// its file, shown, indexed, picked in, selected by a window of about 1 000 and drawn zoomed in. The limits are three
        /// times the targets (display 3 s, pick 5 ms, window 50 ms, a frame zoomed in 60 ms), so the test guards against a fall
        /// that is not noise; on the maintainer's machine it takes 1 s, 0.01 ms, 0.4 ms and 7 ms under .NET Framework 4.8.
        /// </summary>
        [Fact]
        public void ADrawingOfAHundredThousandEntitiesIsShownPickedAndDrawnWithinThreeTimesItsTargets()
        {
            string path = Path.Combine(TestRun.Folder("speed"), "speed-100k.dxf");
            File.WriteAllText(path, BigDrawing.Text(100000));

            var watch = Stopwatch.StartNew();
            CadDocument document = CadDocument.Open(path);
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            double display = watch.Elapsed.TotalMilliseconds;
            PickIndex index = document.GetPickIndex(document.ModelSpace);

            var random = new Random(7);
            watch.Restart();

            for (int i = 0; i < 200; i++)
            {
                index.Pick(random.NextDouble() * list.Bounds.Width, random.NextDouble() * list.Bounds.Height, 1.0);
            }

            double pick = watch.Elapsed.TotalMilliseconds / 200.0;
            double x0 = 50 * BigDrawing.Cell - 1;
            var box = new DisplayBox(x0, x0, x0 + 32 * BigDrawing.Cell, x0 + 32 * BigDrawing.Cell);
            index.Window(box);
            watch.Restart();
            int windowed = index.Window(box).Count;
            double window = watch.Elapsed.TotalMilliseconds;

            double frame;

            using (var bitmap = new SKBitmap(1600, 1000))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                var view = new CadView(1600, 1000);
                view.ZoomTo(list.Bounds);
                view.ZoomAt(800, 500, 20.0);
                renderer.Render(canvas, document, document.ModelSpace, view, new CanvasOptions());
                watch.Restart();

                for (int i = 0; i < 5; i++)
                {
                    renderer.Render(canvas, document, document.ModelSpace, view, new CanvasOptions());
                }

                frame = watch.Elapsed.TotalMilliseconds / 5.0;
            }

            Assert.Equal(178000, list.Count);
            Assert.InRange(windowed, 1000, 1050);
            Assert.True(display < 9000, "display " + display + " ms");
            Assert.True(pick < 15, "pick " + pick + " ms");
            Assert.True(window < 150, "window " + window + " ms");
            Assert.True(frame < 180, "frame " + frame + " ms");
        }
    }

    /// <summary>
    /// The tests that time a drawing of 100 000 entities, run with no other test beside them: run side by side, each one's
    /// collections stop the others' timers, and a layer switched on in 110 ms alone took from 230 to 540 ms.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ViewerSpeedCollection
    {
        /// <summary>The name of the collection.</summary>
        public const string Name = "Viewer speed";
    }
}
