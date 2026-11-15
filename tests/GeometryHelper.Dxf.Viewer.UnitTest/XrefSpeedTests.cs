using System;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The time an external reference of a big drawing takes to show, against the time of the drawing opened on its own. It is
    /// timed with no other test beside it, like the other speed tests, as a ratio of two times taken one after the other.
    /// </summary>
    [Collection(ViewerSpeedCollection.Name)]
    public class XrefSpeedTests : IDisposable
    {
        private const int Runs = 3;
        private const int MostRuns = 8;

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xrefspeed-" + Guid.NewGuid().ToString("N")
        );

        public XrefSpeedTests()
        {
            Directory.CreateDirectory(Path.Combine(_folder, "xref"));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file a watcher still holds is left for the system to clear.
            }
        }

        /// <summary>
        /// A drawing of 100 000 entities shown as an external reference of a drawing of one line: read and drawn within twice
        /// the time of opening and drawing it on its own, each timed at its best of three runs, each run after a full
        /// collection. On the maintainer's machine both take about a second under .NET Framework 4.8. Where the three runs do
        /// not meet the limit, as on a machine loaded by several jobs at once, the runs go on, up to eight, the best of all
        /// being judged: a reference that reads the drawing twice takes twice as long in every run.
        /// </summary>
        [Fact]
        public void AnXrefOfAHundredThousandEntities_IsReadAndShownWithinTwiceTheTimeOfOpeningItsDrawing()
        {
            string big = Path.Combine(_folder, "xref", "big.dxf");
            File.WriteAllText(big, BigDrawing.Text(100000));
            DxfDatabase host = CadTemplate.CreateMetric();
            host.ModelSpace.AppendEntity(
                new DxfBlockReference(GeoPoint3.Origin, host.AttachXref("..\\xref\\big.dxf", "big"))
            );
            string hostPath = Path.Combine(_folder, "plans", "host.dxf");
            Directory.CreateDirectory(Path.GetDirectoryName(hostPath));
            host.Save(hostPath);

            double direct = double.MaxValue;
            double referenced = double.MaxValue;
            int directCount = 0;
            int referencedCount = 0;

            for (int i = 0; i < MostRuns && (i < Runs || referenced > 2.0 * direct); i++)
            {
                direct = Math.Min(direct, Time(() => directCount = Shown(big)));
                referenced = Math.Min(referenced, Time(() => referencedCount = Shown(hostPath)));
            }

            Assert.True(
                directCount == referencedCount,
                directCount + " items alone, " + referencedCount + " referenced."
            );
            Assert.True(
                referenced <= 2.0 * direct,
                "Shown as an external reference in "
                    + referenced.ToString("0", CultureInfo.InvariantCulture)
                    + " ms, opened alone in "
                    + direct.ToString("0", CultureInfo.InvariantCulture)
                    + " ms."
            );
        }

        /// <summary>Opens a drawing and draws its model space, and gives the number of items drawn.</summary>
        private static int Shown(string path)
        {
            CadDocument document = CadDocument.Open(path);
            return document.GetDisplay(document.ModelSpace).Count;
        }

        /// <summary>The milliseconds the work takes, timed after a full collection so that one owed by what ran before is not counted against it.</summary>
        private static double Time(Action work)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            work();
            return watch.Elapsed.TotalMilliseconds;
        }
    }
}
