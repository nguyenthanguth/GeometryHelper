using System;
using System.IO;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A font file that could not be read once (held open by another program, a disk that was slow) is read again at the next
    /// measure, by the metrics of a file (<see cref="CadFontMetrics.Of"/>) and by the ink of a face (<see cref="CadTextInk"/>), where
    /// each kept the failure for as long as the process lived and every text after it was measured by the estimate. A file that
    /// is no font at all is still kept as none. The file is held open by the test, which is what makes the read fail.
    /// </summary>
    public class DxfSpeed17FontReadTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gh-t7-" + Guid.NewGuid().ToString("N"));

        public DxfSpeed17FontReadTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose() => Directory.Delete(_folder, true);

        private string Font(string name)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, TrueTypeFile.Font(TrueTypeFile.Letters()));
            return path;
        }

        private static FileStream Held(string path) =>
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        [Fact]
        public void AFontFileHeldOpenWhenItIsMeasured_IsMeasuredOnceItIsFree()
        {
            string path = Font("gh-held.ttf");

            using (Held(path))
            {
                Assert.Null(CadFontMetrics.Of(path));
            }

            CadFontMetrics metrics = CadFontMetrics.Of(path);
            Assert.NotNull(metrics);
            Assert.True(metrics.Ascent > 0.0);
            Assert.Same(metrics, CadFontMetrics.Of(path));
        }

        [Fact]
        public void AFontFileHeldOpenWhenAFaceIsMeasured_IsReadAgainByTheNextMeasureOfThatFace()
        {
            string path = Font("gh-held-face.ttf");
            var face = new CadFontFace(path, null, null, null);

            using (Held(path))
            {
                Assert.False(CadTextInk.Of(face, false, false).IsRead);
            }

            CadTextInk ink = CadTextInk.Of(face, false, false);
            Assert.True(ink.IsRead);
            Assert.Same(ink, CadTextInk.Of(face, false, false));
        }

        [Fact]
        public void AFileThatIsNoFont_IsStillKeptAsNone_ByTheMetrics()
        {
            string path = Path.Combine(_folder, "gh-none.ttf");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
            Assert.Null(CadFontMetrics.Of(path));

            // Written as a font after it was read as none: the answer is the one kept.
            File.WriteAllBytes(path, TrueTypeFile.Font(TrueTypeFile.Letters()));
            Assert.Null(CadFontMetrics.Of(path));
        }
    }
}
