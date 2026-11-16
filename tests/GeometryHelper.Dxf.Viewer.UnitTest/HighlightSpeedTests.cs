using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.UnitTest;
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
        /// where there are 4 processors or more and three quarters of the threads run at once (a spin of the same threads shows it), in at most
        /// three quarters of one thread's time; where the machine is too busy for that, only the pixels are compared. Each way is the best of three
        /// runs, each run after a full collection and with none let in while it runs. On the maintainer's 24 processors under
        /// .NET Framework 4.8 one thread takes about 300 ms and the threads 150; the times are written to the test's output.
        /// </summary>
        [Fact]
        public void AllOfAHundredThousandEntitiesSelected_AreHighlightedByThreadsPixelForPixelAsByOne()
        {
            double runningBefore = ThreadsRunningAtOnce();
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
            var ones = new List<double>();
            var threadRuns = new List<double>();

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
                    ones.Add(
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
                    one = Math.Min(one, ones[ones.Count - 1]);
                    alone = bitmap.Bytes;
                    Marshal.Copy(scene, 0, bitmap.GetPixels(), scene.Length);
                    threadRuns.Add(
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
                    threads = Math.Min(threads, threadRuns[threadRuns.Count - 1]);
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

            // Threads can only beat one where the processors are free: with other builds on the machine the pool gets one or two of
            // them and runs no faster than one thread. So the speed-up is asked of the threads where a spin of the same threads shows
            // at least three quarters of them running at once before the runs and after them, and where it does not (the pool is then slower than one thread, 2 974 ms to 560 seen) only the pixels are compared.
            double running = Math.Min(runningBefore, ThreadsRunningAtOnce());
            _output.WriteLine("threads running at once " + running.ToString("0.0"));

            if (Environment.ProcessorCount >= 4 && running >= Math.Min(16, Environment.ProcessorCount) * 3 / 4.0)
            {
                Assert.True(
                    threads < one * 3 / 4,
                    "threads "
                        + threads
                        + " ms, one thread "
                        + one
                        + " ms (every run: one thread "
                        + SpeedGuard.Describe(ones)
                        + "; threads "
                        + SpeedGuard.Describe(threadRuns)
                        + ")"
                );
            }
        }

        private const int Runs = 3;

        /// <summary>
        /// How many of the threads the highlight uses really run at once now: the time one thread takes to spin a fixed number of
        /// turns, over the time the same spin takes on all of them at once, times their number; the best of three. 16 on 16 free
        /// processors, 2 where two are free.
        /// </summary>
        private static double ThreadsRunningAtOnce()
        {
            int count = Math.Min(16, Environment.ProcessorCount);
            double best = 0.0;

            for (int run = 0; run < Runs; run++)
            {
                double alone = Spin(1);
                double together = Spin(count);
                best = Math.Max(best, count * alone / together);
            }

            return best;
        }

        /// <summary>The milliseconds that <paramref name="threads"/> threads take, each spinning the same number of turns.</summary>
        private static double Spin(int threads)
        {
            var workers = new System.Threading.Thread[threads];
            double sink = 0.0;

            for (int i = 0; i < threads; i++)
            {
                workers[i] = new System.Threading.Thread(() =>
                {
                    double x = 1.0;

                    for (int k = 0; k < 20000000; k++)
                    {
                        x = x * 1.0000001 + 1e-9;
                    }

                    sink += x;
                });
            }

            var watch = Stopwatch.StartNew();

            foreach (System.Threading.Thread worker in workers)
            {
                worker.Start();
            }

            foreach (System.Threading.Thread worker in workers)
            {
                worker.Join();
            }

            return watch.Elapsed.TotalMilliseconds + (sink < 0 ? 1 : 0);
        }

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

            // A collection the drawing forced may leave the region open although the latency mode no longer says so
            // (seen under .NET Framework 4.8 with other tests running beside this one), so it is ended in any case.
            try
            {
                GC.EndNoGCRegion();
            }
            catch (InvalidOperationException) { }

            return elapsed;
        }
    }
}
