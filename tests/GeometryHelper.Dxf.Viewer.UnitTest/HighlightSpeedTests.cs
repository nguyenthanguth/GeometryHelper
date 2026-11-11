using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Wpf;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    [Collection(ViewerSpeedCollection.Name)]
    public class HighlightSpeedTests
    {
        private readonly ITestOutputHelper _output;

        public HighlightSpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// All of a drawing of 100 000 entities selected and drawn highlighted over its picture at its extents, 1 600 by 1 000
        /// pixels, as the canvas draws a selection: by several threads at once, pixel for pixel as one thread draws it, and
        /// where there are 4 processors or more in at most three quarters of one thread's time. Each way is the best of three
        /// runs, each run after a full collection and with none let in while it runs. On the maintainer's 24 processors under
        /// .NET Framework 4.8 one thread takes about 300 ms and the threads 150; the times are written to the test's output.
        /// </summary>
        [Fact]
        public void AllOfAHundredThousandEntitiesSelected_AreHighlightedByThreadsPixelForPixelAsByOne()
        {
            CadDocument document = CadDocument.From(DxfDatabase.Parse(BigDrawing.Text(100000)), "speed-100k.dxf");
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            var handles = new List<string>();

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                handles.Add(entity.Handle);
            }

            var view = new CadView(1600, 1000);
            view.ZoomTo(list.Bounds);
            var options = new CanvasOptions();
            byte[] alone = null,
                together = null;
            double one = double.MaxValue,
                threads = double.MaxValue;

            using (var bitmap = new SKBitmap(1600, 1000))
            using (var canvas = new SKCanvas(bitmap))
            using (SKPixmap pixels = bitmap.PeekPixels())
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.RenderScene(canvas, document, document.ModelSpace, view, options);
                byte[] scene = bitmap.Bytes;

                for (int run = 0; run < Runs; run++)
                {
                    Marshal.Copy(scene, 0, bitmap.GetPixels(), scene.Length);
                    one = Math.Min(
                        one,
                        Time(() =>
                            renderer.RenderHighlight(
                                canvas,
                                document,
                                document.ModelSpace,
                                view,
                                options,
                                handles,
                                false
                            )
                        )
                    );
                    alone = bitmap.Bytes;
                    Marshal.Copy(scene, 0, bitmap.GetPixels(), scene.Length);
                    threads = Math.Min(
                        threads,
                        Time(() =>
                            renderer.RenderHighlight(
                                canvas,
                                document,
                                document.ModelSpace,
                                view,
                                options,
                                handles,
                                false,
                                pixels
                            )
                        )
                    );
                    together = bitmap.Bytes;
                }

                Assert.NotEqual(scene, alone);
            }

            _output.WriteLine(
                "one thread "
                    + one.ToString("0")
                    + " ms, "
                    + Math.Min(16, Environment.ProcessorCount)
                    + " threads "
                    + threads.ToString("0")
                    + " ms"
            );
            Assert.Equal(alone, together);

            if (Environment.ProcessorCount >= 4)
            {
                Assert.True(threads < one * 3 / 4, "threads " + threads + " ms, one thread " + one + " ms");
            }
        }

        private const int Runs = 3;

        /// <summary>The milliseconds a drawing takes, timed after a full collection and with none let in while it runs.</summary>
        private static double Time(Action draw)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.TryStartNoGCRegion(200L * 1024 * 1024);
            var watch = Stopwatch.StartNew();
            draw();
            double elapsed = watch.Elapsed.TotalMilliseconds;

            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
            }

            return elapsed;
        }
    }
}
