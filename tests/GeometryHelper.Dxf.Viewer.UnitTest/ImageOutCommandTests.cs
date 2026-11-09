using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf.Viewer.Wpf;
using SkiaSharp;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// PNGOUT, JPGOUT and BMPOUT with no window: AutoCAD 2021's prompts, "Enter file name &lt;file&gt;:" and "Select objects
    /// or &lt;all objects and viewports&gt;:", the image handed to whoever draws it, here <see cref="CadRasterWriter"/>, at the
    /// session's view or 1 920 by 1 080 where it has none; all objects, or those selected alone; the headers of each kind.
    /// </summary>
    public class ImageOutCommandTests : IDisposable
    {
        private const string Selecting = "Select objects or <all objects and viewports>:";

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gk", "imageout-" + Guid.NewGuid().ToString("N"));

        public ImageOutCommandTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        /// <summary>
        /// Two lines across a canvas of 200 by 100: A1 red along y = 20.5, the pixel row 79, and A2 green along y = 80.5, the
        /// pixel row 19, both from x = 10 to 190; saved as two-lines.dxf in the test's folder.
        /// </summary>
        private CadDocument TwoLines()
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(
                null,
                null,
                TestDrawings.Line(10, 20.5, 190, 20.5, color: 1, handle: "A1"),
                TestDrawings.Line(10, 80.5, 190, 80.5, color: 3, handle: "A2")));
            document.SaveAs(Path.Combine(_folder, "two-lines.dxf"));
            return document;
        }

        /// <summary>A view of 200 by 100 pixels at one pixel to the unit, the drawing's (0, 0) at its lower left corner.</summary>
        private static CadView PixelView()
        {
            var view = new CadView(200, 100);
            view.ZoomTo(new DisplayBox(0, 0, 200, 100), 0.0);
            return view;
        }

        /// <summary>A session whose IMAGEOUT is written by <see cref="CadRasterWriter"/>, as a host with no window writes it; each job kept.</summary>
        private static CadCommandSession Windowless(CadDocument document, List<CadImageJob> jobs)
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "IMAGEOUT")
                {
                    CadRasterWriter.Write(document, e.Image);
                    jobs.Add(e.Image);
                    e.Handled = true;
                }
            };
            return session;
        }

        private static int Lit(SKBitmap picture, int row) => Enumerable.Range(0, picture.Width).Count(x => picture.GetPixel(x, row).Red > 100 || picture.GetPixel(x, row).Green > 100);

        private static int Int32(byte[] bytes, int at) => bytes[at] | (bytes[at + 1] << 8) | (bytes[at + 2] << 16) | (bytes[at + 3] << 24);

        private static int BigInt32(byte[] bytes, int at) => (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];

        [Fact]
        public void PngOut_AsksAutoCadsPrompts_AndWithNoViewWritesTheExtentsAt1920By1080_PrintingNothing()
        {
            CadDocument document = TwoLines();
            var jobs = new List<CadImageJob>();
            CadCommandSession session = Windowless(document, jobs);
            string file = Path.Combine(_folder, "plan");

            Run(session, "PNGOUT");
            Assert.Equal("Enter file name <" + Path.Combine(_folder, "two-lines.png") + ">:", session.PromptText);
            Run(session, file);
            Assert.Equal(Selecting, session.PromptText);
            int lines = session.History.Count;
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            Assert.Equal(lines + 1, session.History.Count);
            Assert.False(document.IsModified);
            Assert.Null(document.NextUndo);
            CadImageJob job = Assert.Single(jobs);
            Assert.Equal(file + ".png", job.FileName);
            Assert.Null(job.Handles);
            Assert.Equal(1920, job.PixelWidth);
            Assert.Equal(1080, job.PixelHeight);

            byte[] bytes = File.ReadAllBytes(file + ".png");
            Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }, bytes.Take(8).ToArray());
            Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(bytes, 12, 4));
            Assert.Equal(1920, BigInt32(bytes, 16));
            Assert.Equal(1080, BigInt32(bytes, 20));
            Assert.Equal(8, bytes[24]);
            Assert.Equal(2, bytes[25]);

            using (SKBitmap picture = SKBitmap.Decode(bytes))
            {
                Assert.Equal(1920, picture.Width);
                Assert.True(Enumerable.Range(0, 1080).Sum(y => Lit(picture, y)) > 2 * 1500);
            }
        }

        [Fact]
        public void TheSessionsView_GivesTheSizeAndThePartShown_AndEnterWritesBothLines()
        {
            CadDocument document = TwoLines();
            var jobs = new List<CadImageJob>();
            CadCommandSession session = Windowless(document, jobs);
            session.View = PixelView();
            string file = Path.Combine(_folder, "all.png");

            Run(session, "PNGOUT", file, Enter);

            using (SKBitmap picture = SKBitmap.Decode(file))
            {
                Assert.Equal(200, picture.Width);
                Assert.Equal(100, picture.Height);
                Assert.InRange(Lit(picture, 79), 175, 185);
                Assert.InRange(Lit(picture, 19), 175, 185);
                Assert.Equal(SKColors.Black, picture.GetPixel(100, 50));
                Assert.True(picture.GetPixel(100, 79).Red > 200);
            }
        }

        [Fact]
        public void ObjectsSelected_AreWrittenAlone_TheRestOfTheViewTheBackground()
        {
            CadDocument document = TwoLines();
            var jobs = new List<CadImageJob>();
            CadCommandSession session = Windowless(document, jobs);
            session.View = PixelView();
            string file = Path.Combine(_folder, "red.png");

            Run(session, "PNGOUT", file, new[] { "A1" });
            Assert.Equal(Selecting, session.PromptText);
            Run(session, Enter);

            Assert.Equal(new[] { "A1" }, Assert.Single(jobs).Handles);

            using (SKBitmap picture = SKBitmap.Decode(file))
            {
                Assert.Equal(200, picture.Width);
                Assert.InRange(Lit(picture, 79), 175, 185);
                Assert.Equal(0, Lit(picture, 19));
            }
        }

        [Fact]
        public void TheSameViewTwice_GivesTheSameBytes()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Windowless(document, new List<CadImageJob>());
            session.View = PixelView();
            string first = Path.Combine(_folder, "first.png");
            string second = Path.Combine(_folder, "second.png");

            Run(session, "PNGOUT", first, Enter, "PNGOUT", second, Enter);

            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        }

        [Fact]
        public void BmpOut_WritesTwentyFourBitsAPixel_TheRowsFromTheBottomUp()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Windowless(document, new List<CadImageJob>());
            session.View = PixelView();
            string file = Path.Combine(_folder, "plan.bmp");

            Run(session, "BMPOUT");
            Assert.EndsWith("two-lines.bmp>:", session.PromptText);
            Run(session, file, Enter);

            byte[] bytes = File.ReadAllBytes(file);
            Assert.Equal((byte)'B', bytes[0]);
            Assert.Equal((byte)'M', bytes[1]);
            Assert.Equal(54 + 600 * 100, Int32(bytes, 2));
            Assert.Equal(54, Int32(bytes, 10));
            Assert.Equal(40, Int32(bytes, 14));
            Assert.Equal(200, Int32(bytes, 18));
            Assert.Equal(100, Int32(bytes, 22));
            Assert.Equal(1, bytes[26]);
            Assert.Equal(24, bytes[28]);
            Assert.Equal(0, Int32(bytes, 30));

            // The red line on the pixel row 79 from the top is the file's row 20 from the bottom: blue, green, red.
            int middle = 54 + 20 * 600 + 100 * 3;
            Assert.True(bytes[middle + 2] > 200);
            Assert.True(bytes[middle] < 60);
            Assert.Equal(0, bytes[54 + 50 * 600 + 300]);
        }

        [Fact]
        public void JpgOut_WritesAJpegOfTheViewsSize()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Windowless(document, new List<CadImageJob>());
            session.View = PixelView();
            string file = Path.Combine(_folder, "plan.jpg");

            Run(session, "JPGOUT");
            Assert.EndsWith("two-lines.jpg>:", session.PromptText);
            Run(session, file, Enter);

            byte[] bytes = File.ReadAllBytes(file);
            Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, bytes.Take(3).ToArray());

            using (SKBitmap picture = SKBitmap.Decode(bytes))
            {
                Assert.Equal(200, picture.Width);
                Assert.Equal(100, picture.Height);
                // A line one pixel wide loses some of its red to JPEG's halved colour, but stands out of the black.
                int red = Enumerable.Range(20, 160).Sum(x => picture.GetPixel(x, 79).Red);
                int dark = Enumerable.Range(20, 160).Sum(x => picture.GetPixel(x, 50).Red);
                Assert.True(red > 160 * 60 && dark < 160 * 5, red + " " + dark);
            }
        }

        [Fact]
        public void ANameOfAnotherExtension_HasTheRightOneAdded_AndANameWithNoFolderGoesBesideTheDrawing()
        {
            CadDocument document = TwoLines();
            var jobs = new List<CadImageJob>();
            CadCommandSession session = Windowless(document, jobs);
            session.View = PixelView();

            Run(session, "JPGOUT", Path.Combine(_folder, "plan.jpeg"), Enter, "PNGOUT", "beside", Enter, "PNGOUT", Enter, Enter, "BMPOUT", "UPPER.BMP", Enter);

            Assert.Equal(
                new[] { Path.Combine(_folder, "plan.jpeg.jpg"), Path.Combine(_folder, "beside.png"), Path.Combine(_folder, "two-lines.png"), Path.Combine(_folder, "UPPER.BMP") },
                jobs.Select(j => j.FileName).ToArray());
            Assert.All(jobs, j => Assert.True(File.Exists(j.FileName)));
        }

        [Fact]
        public void WithNoOneToDrawIt_TheCommandSaysSo_AndWritesNothing()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);
            string file = Path.Combine(_folder, "none.png");

            Run(session, "PNGOUT", file, Enter);

            Assert.Equal("PNGOUT needs the window to draw the image: nothing was written.", session.History.Last());
            Assert.False(File.Exists(file));
        }

        [Fact]
        public void TheWindowsDialog_TakesThePlaceOfThePrompt_AndCancelledEndsTheCommandQuietly()
        {
            CadDocument document = TwoLines();
            var jobs = new List<CadImageJob>();
            CadCommandSession session = Windowless(document, jobs);
            session.View = PixelView();
            string chosen = Path.Combine(_folder, "chosen.png");
            string offered = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "PNGOUT")
                {
                    offered = e.Image.FileName;
                    e.Image.FileName = jobs.Count == 0 ? chosen : null;
                    e.Handled = true;
                }
            };

            Run(session, "PNGOUT");
            Assert.Equal(Selecting, session.PromptText);
            Assert.Equal(Path.Combine(_folder, "two-lines.png"), offered);
            Run(session, Enter);
            Assert.True(File.Exists(chosen));

            int lines = session.History.Count;
            Run(session, "PNGOUT");
            Assert.False(session.IsRunning);
            Assert.Equal(lines + 1, session.History.Count);
            Assert.Single(jobs);
        }
    }
}
