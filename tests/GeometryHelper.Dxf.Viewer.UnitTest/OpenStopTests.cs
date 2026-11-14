using System;
using System.IO;
using System.Text;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A drawing opened from a path that a token stops, as the window opens one on a worker thread that Esc and Stop stop:
    /// the passes over the file's records look at the token as they go, and a drawing opened with a token not stopped is the
    /// one opened without.
    /// </summary>
    public class OpenStopTests
    {
        private static string Written(string text)
        {
            string path = Path.Combine(Path.GetTempPath(), "openstop-" + Guid.NewGuid().ToString("N") + ".dxf");
            File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
            return path;
        }

        [Fact]
        public void ADrawingOpenedWithAStoppedToken_IsGivenUpWhileItIsRead()
        {
            string path = Written(BigDrawing.Text(20000));

            try
            {
                using (var stopped = new CancellationTokenSource())
                {
                    stopped.Cancel();

                    Assert.Throws<OperationCanceledException>(() => CadDocument.Open(path, null, stopped.Token));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ADrawingOpenedWithATokenNotStopped_IsTheDocumentOpenedWithoutOne()
        {
            string path = Written(BigDrawing.Text(20000));

            try
            {
                using (var running = new CancellationTokenSource())
                {
                    CadDocument with = CadDocument.Open(path, null, running.Token);
                    CadDocument without = CadDocument.Open(path);

                    Assert.Equal(without.Database.ToText(), with.Database.ToText());
                    Assert.Equal(
                        DisplayPatchTests.Describe(without.GetDisplay(without.ModelSpace)),
                        DisplayPatchTests.Describe(with.GetDisplay(with.ModelSpace))
                    );
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
