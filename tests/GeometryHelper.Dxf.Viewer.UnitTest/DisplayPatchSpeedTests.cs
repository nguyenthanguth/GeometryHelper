using System;
using System.Diagnostics;
using System.Runtime;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    [Collection(ViewerSpeedCollection.Name)]
    public class DisplayPatchSpeedTests
    {
        private readonly ITestOutputHelper _output;

        public DisplayPatchSpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// A drawing of 100 000 entities drawn whole, then drawn again after one line moved and after the layer of a third of
        /// them switched off and on. Only what changed is drawn again, so each of those costs a fraction of the whole: the limits
        /// are a third of the whole drawing for the move and the layer switched off, and two thirds for the layer switched on,
        /// which draws its third again. Each is the best of three runs, each run timed with no garbage collection inside it: a
        /// collection owed by the runs before landed in the layer switched on, and with other builds on the machine timed it at
        /// up to 0.8 of the whole, 5 runs in 20 failing. Timed so, the whole takes about 190 ms under .NET Framework 4.8, the
        /// move 20 ms, the layer off 18 ms and on 83 ms; the times are written to the test's output.
        /// </summary>
        [Fact]
        public void AMoveAndALayerSwitchedInADrawingOfAHundredThousandEntities_DrawOnlyWhatChanged()
        {
            CadDocument document = CadDocument.From(DxfDatabase.Parse(BigDrawing.Text(100000)), "speed-100k.dxf");
            document.GetDisplay(document.ModelSpace);
            DisplayList2 whole = null,
                moved = null,
                off = null,
                on = null;
            double first = double.MaxValue,
                move = double.MaxValue,
                layerOff = double.MaxValue,
                layerOn = double.MaxValue;
            string handle = null;

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                if (entity is DxfLine)
                {
                    handle = entity.Handle;
                    break;
                }
            }

            // Each phase is timed three times and the best kept, the drawing put back between runs, so that another process
            // taking the processor in one run does not decide the test.
            for (int run = 0; run < Runs; run++)
            {
                document.Regen();
                first = Math.Min(first, Time(() => whole = document.GetDisplay(document.ModelSpace)));
            }

            for (int run = 0; run < Runs; run++)
            {
                var by = new GeoVector3(run % 2 == 0 ? 5 : -5, 0, 0);
                Assert.True(
                    document
                        .ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle) }, s =>
                            s.Document.FindEntity(handle).TransformBy(GeoTransform3.Translation(by))
                        )
                        .Succeeded
                );
                move = Math.Min(move, Time(() => moved = document.GetDisplay(document.ModelSpace)));
            }

            for (int run = 0; run < Runs; run++)
            {
                Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
                layerOff = Math.Min(layerOff, Time(() => off = document.GetDisplay(document.ModelSpace)));
                Assert.True(CadLayerEditor.SetOn(document, "Steel", true).Succeeded);
                layerOn = Math.Min(layerOn, Time(() => on = document.GetDisplay(document.ModelSpace)));
            }

            _output.WriteLine(
                "whole "
                    + first.ToString("0")
                    + " ms, move "
                    + move.ToString("0")
                    + " ms, layer off "
                    + layerOff.ToString("0")
                    + " ms, layer on "
                    + layerOn.ToString("0")
                    + " ms"
            );

            Assert.Equal(whole.Count, moved.Count);
            Assert.InRange(off.Count, 1, whole.Count * 3 / 4);
            Assert.Equal(whole.Count, on.Count);
            Assert.True(move < first / 3, "move " + move + " ms of " + first);
            Assert.True(layerOff < first / 3, "layer off " + layerOff + " ms of " + first);
            Assert.True(layerOn < first * 2 / 3, "layer on " + layerOn + " ms of " + first);
        }

        private const int Runs = 3;

        /// <summary>
        /// The milliseconds a drawing takes, timed after a full collection and with none let in while it runs (up to 200 MB
        /// made), so that a collection owed by what ran before is not counted against it.
        /// </summary>
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
