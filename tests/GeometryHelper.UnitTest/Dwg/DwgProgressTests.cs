using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// How far the DWG reader and writer have got, told to an <see cref="IProgress{T}"/> for the presenter's progress meter:
    /// from 0 to 1 in thousandths, never going back, and the same bytes as without it.
    /// </summary>
    public class DwgProgressTests
    {
        [Fact]
        public void ADrawingOfTwoThousandLines_WrittenAndRead_TellsNoughtToOneNeverGoingBack_AndTheSameBytes()
        {
            var writer = new DxfWriter();

            for (int i = 0; i < 2000; i++)
            {
                writer.Add(new GeoLine2(new GeoPoint2(i % 50, i / 50), new GeoPoint2(i % 50 + 0.5, i / 50 + 0.25)));
            }

            string text = DxfDatabase.Parse(writer.ToString()).ToText();
            var writing = new List<double>();
            byte[] told = DwgWriter.ToDwg(text, DwgWriter.DefaultVersion, out _, new Recorder(writing));
            Assert.Equal(DwgWriter.ToDwg(text, DwgWriter.DefaultVersion), told);
            AssertRuns(writing);

            string path = Path.Combine(Path.GetTempPath(), "gh-dwg-progress-" + Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                File.WriteAllBytes(path, told);
                var reading = new List<double>();
                DxfDatabase read = DwgReader.Open(path, new Recorder(reading));
                Assert.Equal(DwgReader.Open(path).ToText(), read.ToText());
                AssertRuns(reading);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void AssertRuns(List<double> told)
        {
            Assert.Equal(0.0, told[0]);
            Assert.Equal(1.0, told[told.Count - 1]);
            Assert.InRange(told.Count, 100, 1001);

            for (int i = 1; i < told.Count; i++)
            {
                Assert.True(
                    told[i] > told[i - 1],
                    "The share did not grow from " + told[i - 1] + " to " + told[i] + "."
                );
                Assert.Equal(Math.Round(told[i] * 1000.0), told[i] * 1000.0, 9);
            }
        }

        private sealed class Recorder : IProgress<double>
        {
            private readonly List<double> _told;

            public Recorder(List<double> told) => _told = told;

            public void Report(double value) => _told.Add(value);
        }
    }
}
