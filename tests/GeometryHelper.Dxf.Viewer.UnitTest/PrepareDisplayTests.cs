using System;
using System.Collections.Generic;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The display of a space made ahead, as the window makes it on a worker thread for a drawing it opens: the list the
    /// first <see cref="CadDocument.GetDisplay"/> makes, item for item, the share drawn told as it goes, and a stop that
    /// leaves the document to be drawn again.
    /// </summary>
    public class PrepareDisplayTests
    {
        [Fact]
        public void ADisplayMadeAhead_IsTheListGetDisplayMakes_AndTellsTheShareDrawn()
        {
            string text = BigDrawing.Text(20000);
            CadDocument ahead = CadDocument.From(DxfDatabase.Parse(text), "ahead.dxf");
            CadDocument plain = CadDocument.From(DxfDatabase.Parse(text), "plain.dxf");
            var shares = new List<double>();

            ahead.PrepareDisplay(ahead.ModelSpace, new Told(shares.Add), CancellationToken.None);

            // Every 4 096 of the 20 000 entities, so four times, rising.
            Assert.Equal(4, shares.Count);
            Assert.Equal(4096.0 / 20000.0, shares[0]);
            Assert.Equal(16384.0 / 20000.0, shares[3]);
            Assert.Equal(
                DisplayPatchTests.Describe(plain.GetDisplay(plain.ModelSpace)),
                DisplayPatchTests.Describe(ahead.GetDisplay(ahead.ModelSpace))
            );
            Assert.Same(ahead.GetDisplay(ahead.ModelSpace), ahead.GetPickIndex(ahead.ModelSpace).List);
        }

        [Fact]
        public void ADisplayStoppedWhileMadeAhead_IsNotKept_AndIsMadeWholeWhenNextAskedFor()
        {
            string text = BigDrawing.Text(20000);
            CadDocument document = CadDocument.From(DxfDatabase.Parse(text), "stopped.dxf");
            CadDocument plain = CadDocument.From(DxfDatabase.Parse(text), "plain.dxf");

            using (var stop = new CancellationTokenSource())
            {
                Assert.Throws<OperationCanceledException>(() =>
                    document.PrepareDisplay(document.ModelSpace, new Told(share => stop.Cancel()), stop.Token)
                );
            }

            Assert.Equal(
                DisplayPatchTests.Describe(plain.GetDisplay(plain.ModelSpace)),
                DisplayPatchTests.Describe(document.GetDisplay(document.ModelSpace))
            );
        }

        /// <summary>Tells each share as it is reported, on the thread drawing.</summary>
        private sealed class Told : IProgress<double>
        {
            private readonly Action<double> _told;

            public Told(Action<double> told)
            {
                _told = told;
            }

            public void Report(double value) => _told(value);
        }
    }
}
