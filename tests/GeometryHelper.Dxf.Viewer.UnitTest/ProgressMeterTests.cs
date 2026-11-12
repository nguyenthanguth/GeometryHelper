using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The progress meter of the status bar, as AutoCAD's ProgressMeter: started, given a limit, moved and stopped; meters
    /// started inside others; and the reading and writing of DXF and DWG files telling how far they have got.
    /// </summary>
    public class ProgressMeterTests
    {
        [Fact]
        public void AMeterStarted_IsShownIndeterminate_UntilGivenALimit()
        {
            var progress = new CadProgress();
            int changes = 0;
            progress.Changed += (s, e) => changes++;

            CadProgressMeter meter = progress.Start("Checking blocks");

            Assert.True(progress.IsBusy);
            Assert.Same(meter, progress.Shown);
            Assert.Equal("Checking blocks", meter.Label);
            Assert.True(meter.IsIndeterminate);
            Assert.Equal(-1, meter.Percent);
            Assert.Equal(1, changes);

            meter.SetLimit(8);
            Assert.False(meter.IsIndeterminate);
            Assert.Equal(0, meter.Percent);

            for (int i = 0; i < 3; i++)
            {
                meter.MeterProgress();
            }

            // 3 of 8 is 37.5 %, shown as 37.
            Assert.Equal(3, meter.Position);
            Assert.Equal(37, meter.Percent);

            meter.Stop();
            Assert.False(progress.IsBusy);
            Assert.Null(progress.Shown);
            Assert.False(meter.IsRunning);
        }

        [Fact]
        public void AMeterMovedAThousandTimes_TellsTheStatusBarOncePerWholePercent()
        {
            var progress = new CadProgress();
            CadProgressMeter meter = progress.Start("Saving plan.dwg");
            meter.SetLimit(100_000);
            int changes = 0;
            progress.Changed += (s, e) => changes++;

            for (int i = 0; i < 100_000; i++)
            {
                meter.MeterProgress();
            }

            Assert.Equal(100, changes);
            Assert.Equal(100, meter.Percent);

            // A share that leaves the percent as it was tells nothing.
            meter.Report(0.999);
            meter.Report(0.9991);
            Assert.Equal(101, changes);
            Assert.Equal(99, meter.Percent);
        }

        [Fact]
        public void AShareReported_IsShownAsAWholePercentRoundedDown_AndClamped()
        {
            var progress = new CadProgress();
            CadProgressMeter meter = progress.Start(null);

            Assert.Equal(string.Empty, meter.Label);
            meter.Report(0.405);
            Assert.Equal(40, meter.Percent);
            meter.Report(-3.0);
            Assert.Equal(0, meter.Percent);
            meter.Report(7.0);
            Assert.Equal(100, meter.Percent);
            meter.Report(double.NaN);
            Assert.Equal(100, meter.Percent);
            Assert.Throws<ArgumentOutOfRangeException>(() => meter.SetLimit(-1));
        }

        [Fact]
        public void AMeterStartedInsideAnother_IsShown_AndTheOuterComesBackWhenItStops()
        {
            var progress = new CadProgress();
            CadProgressMeter outer = progress.Start("Saving plan.dwg");
            outer.Report(0.5);
            CadProgressMeter inner = progress.Start("Plugin: checking");
            int changes = 0;
            progress.Changed += (s, e) => changes++;

            Assert.Same(inner, progress.Shown);
            Assert.Equal(2, progress.Depth);

            // The outer moved while hidden tells nothing; the inner moved does.
            outer.Report(0.7);
            Assert.Equal(0, changes);
            inner.Report(0.2);
            Assert.Equal(1, changes);

            inner.Dispose();
            Assert.Same(outer, progress.Shown);
            Assert.Equal(70, progress.Shown.Percent);
            Assert.Equal(2, changes);

            // Stopped twice, or moved once stopped, nothing more happens.
            inner.Stop();
            inner.Report(0.9);
            Assert.Equal(2, changes);
            Assert.Equal(20, inner.Percent);

            // An outer stopped before the inner leaves the inner shown.
            CadProgressMeter third = progress.Start("Third");
            outer.Stop();
            Assert.Same(third, progress.Shown);
            Assert.Equal(1, progress.Depth);
            third.Stop();
            Assert.False(progress.IsBusy);
        }

        [Fact]
        public void AMeterInAUsingBlock_IsStoppedWhenTheWorkThrows()
        {
            var progress = new CadProgress();
            CadProgressMeter kept = null;

            Action work = () =>
            {
                using (CadProgressMeter meter = progress.Start("Failing"))
                {
                    kept = meter;
                    meter.SetLimit(10);
                    meter.MeterProgress();
                    throw new InvalidOperationException("The work failed.");
                }
            };

            Assert.Throws<InvalidOperationException>(work);

            Assert.False(kept.IsRunning);
            Assert.False(progress.IsBusy);
        }

        [Fact]
        public void ACommand_FindsTheMeterOfItsSession()
        {
            var document = CadDocument.From(DxfDatabase.Parse(BigDrawing.Text(10)));
            var shared = new CadProgress();
            var session = new CadCommandSession(document) { Progress = shared };
            CadProgress seen = null;
            session.Registry.Add(new CadCommand("METERTEST", c =>
                {
                    seen = c.Progress;
                    return Array.Empty<CadPrompt>();
                }));

            session.Feed("METERTEST");

            Assert.Same(shared, seen);
            Assert.NotNull(new CadCommandSession(document).Progress);
        }

        [Fact]
        public void SavingAndOpeningADxfAndADwg_TellsTheShareFromNoughtToOne_NeverGoingBack()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-progress-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                string dxf = Path.Combine(folder, "plan.dxf");
                var writer = new DxfWriter();

                for (int i = 0; i < 3000; i++)
                {
                    writer.Add(new GeoLine2(new GeoPoint2(i % 60, i / 60), new GeoPoint2(i % 60 + 0.7, i / 60 + 0.4)));
                }

                writer.Save(dxf);
                var opening = new Recorder();
                CadDocument document = CadDocument.Open(dxf, opening);
                AssertRuns(opening, 2);

                var savingDwg = new Recorder();
                string dwg = Path.Combine(folder, "plan.dwg");
                document.SaveAs(dwg, savingDwg);
                AssertRuns(savingDwg, 50);

                var savingDxf = new Recorder();
                document.SaveAs(Path.Combine(folder, "copy.dxf"), savingDxf);
                AssertRuns(savingDxf, 2);

                var openingDwg = new Recorder();
                CadDocument fromDwg = CadDocument.Open(dwg, openingDwg);
                AssertRuns(openingDwg, 50);

                // Told or not, the same file is read and written.
                byte[] told = File.ReadAllBytes(dwg);
                CadDocument.Open(dxf).SaveAs(dwg);
                Assert.Equal(told, File.ReadAllBytes(dwg));
                Assert.Equal(CadDocument.Open(dwg).Database.ToText(), fromDwg.Database.ToText());
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>The shares told start at 0, never go back, end at 1, are at least <paramref name="fewest"/> and at most 1 001.</summary>
        private static void AssertRuns(Recorder recorder, int fewest)
        {
            List<double> told = recorder.Told;
            Assert.Equal(0.0, told.First());
            Assert.Equal(1.0, told.Last());
            Assert.InRange(told.Count, fewest, 1001);

            for (int i = 1; i < told.Count; i++)
            {
                Assert.True(told[i] >= told[i - 1], "The share went back from " + told[i - 1] + " to " + told[i] + ".");
            }
        }

        private sealed class Recorder : IProgress<double>
        {
            public List<double> Told { get; } = new List<double>();

            public void Report(double value) => Told.Add(value);
        }
    }
}
