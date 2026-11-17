using System;
using System.Collections.Generic;
using System.Diagnostics;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Cad.UnitTest
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
        /// move 20 ms, the layer off 18 ms and on 83 ms; the times are written to the test's output. Where the three runs do
        /// not meet the limits, as on a machine loaded by several jobs at once, whose whole took 400 ms and whose move 60,
        /// the runs go on, up to eight, the best of all being judged: a drawing that redraws what it should not is over the
        /// limit in every run.
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
            var wholes = new List<double>();
            var moves = new List<double>();
            var layersOff = new List<double>();
            var layersOn = new List<double>();

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                if (entity is DxfLine)
                {
                    handle = entity.Handle;
                    break;
                }
            }

            // Each phase is timed three times and the best kept, the drawing put back between runs, so that another process
            // taking the processor in one run does not decide the test; the runs go on past three while the limits are not met.
            for (int run = 0; run < MostRuns && (run < Runs || !WithinLimits(first, move, layerOff, layerOn)); run++)
            {
                document.Regen();
                first = Math.Min(first, Kept(wholes, Time(() => whole = document.GetDisplay(document.ModelSpace))));

                var by = new GeoVector3(run % 2 == 0 ? 5 : -5, 0, 0);
                Assert.True(
                    document
                        .ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle) }, s =>
                            s.Document.FindEntity(handle).TransformBy(GeoTransform3.Translation(by))
                        )
                        .Succeeded
                );
                move = Math.Min(move, Kept(moves, Time(() => moved = document.GetDisplay(document.ModelSpace))));

                Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
                layerOff = Math.Min(
                    layerOff,
                    Kept(layersOff, Time(() => off = document.GetDisplay(document.ModelSpace)))
                );
                Assert.True(CadLayerEditor.SetOn(document, "Steel", true).Succeeded);
                layerOn = Math.Min(layerOn, Kept(layersOn, Time(() => on = document.GetDisplay(document.ModelSpace))));
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
            string every =
                " (every run: whole "
                + SpeedGuard.Describe(wholes)
                + "; move "
                + SpeedGuard.Describe(moves)
                + "; layer off "
                + SpeedGuard.Describe(layersOff)
                + "; layer on "
                + SpeedGuard.Describe(layersOn)
                + ")";
            Assert.True(move < first / 3, "move " + move + " ms of " + first + every);
            Assert.True(layerOff < first / 3, "layer off " + layerOff + " ms of " + first + every);
            Assert.True(layerOn < first * 2 / 3, "layer on " + layerOn + " ms of " + first + every);
        }

        private const int Runs = 3;
        private const int MostRuns = 8;

        /// <summary>Keeps the time of a run for the message of a failure, and gives it back.</summary>
        private static double Kept(List<double> every, double milliseconds)
        {
            every.Add(milliseconds);
            return milliseconds;
        }

        /// <summary>Whether the best times so far meet the three limits of the test.</summary>
        private static bool WithinLimits(double first, double move, double layerOff, double layerOn) =>
            move < first / 3 && layerOff < first / 3 && layerOn < first * 2 / 3;

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
