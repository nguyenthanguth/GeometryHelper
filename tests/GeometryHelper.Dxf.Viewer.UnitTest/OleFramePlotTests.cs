using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// OLE frames plotted: the picture of a bitmap frame in a PNG and a PDF, red and blue at high graphics and black at
    /// monochrome, the frame drawn on the sheet only at OLEFRAME 1, and a frame left out by OLEHIDE left off the plot.
    /// </summary>
    public class OleFramePlotTests
    {
        private static CadPlotSettings OneToOne() =>
            new CadPlotSettings
            {
                Area = CadPlotArea.Window,
                Window = new DisplayBox(0, 0, 100, 60),
                FitToPaper = false,
                PaperUnits = 1,
                DrawingUnits = 1,
            };

        private static CadDocument Drawing(byte quality, int oleFrame, int oleHide = 0)
        {
            CadDocument document = OleFrameDisplayTests.Drawing(out DxfOle2Frame frame);
            frame.OutputQuality = quality;
            document.OleFrame = oleFrame;
            document.OleHide = oleHide;
            document.Regen();
            return document;
        }

        /// <summary>The plot of the drawing at 1:1 in pixels, 4 to the millimetre.</summary>
        private static SKBitmap Plotted(CadDocument document)
        {
            CadPlotSettings settings = OneToOne();
            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
            const int perMillimetre = 4;
            int width = (int)Math.Ceiling(page.SheetWidth * perMillimetre);
            int height = (int)Math.Ceiling(page.SheetHeight * perMillimetre);
            var bitmap = new SKBitmap(width, height);

            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                canvas.Clear(SKColors.White);
                renderer.RenderPlot(
                    canvas,
                    document,
                    document.ModelSpace,
                    page,
                    settings,
                    new Affine2(perMillimetre, 0.0, 0.0, -perMillimetre, 0.0, height)
                );
            }

            return bitmap;
        }

        /// <summary>How many pixels of the plot are red, blue and dark.</summary>
        private static (int red, int blue, int dark) Counted(SKBitmap bitmap)
        {
            int red = 0;
            int blue = 0;
            int dark = 0;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    SKColor c = bitmap.GetPixel(x, y);
                    red += c.Red > 200 && c.Green < 60 && c.Blue < 60 ? 1 : 0;
                    blue += c.Blue > 200 && c.Red < 60 && c.Green < 60 ? 1 : 0;
                    dark += c.Red < 60 && c.Green < 60 && c.Blue < 60 ? 1 : 0;
                }
            }

            return (red, blue, dark);
        }

        /// <summary>How many pixels two plots of one size differ in.</summary>
        private static int Differences(SKBitmap one, SKBitmap other)
        {
            int count = 0;

            for (int y = 0; y < one.Height; y++)
            {
                for (int x = 0; x < one.Width; x++)
                {
                    count += one.GetPixel(x, y) == other.GetPixel(x, y) ? 0 : 1;
                }
            }

            return count;
        }

        [Fact]
        public void AtHighGraphics_ThePicturePlotsInItsColours()
        {
            using (SKBitmap bitmap = Plotted(Drawing(2, 2)))
            {
                (int red, int blue, int _) = Counted(bitmap);

                Assert.True(red > 1000, "red " + red);
                Assert.True(blue > 1000, "blue " + blue);
            }
        }

        [Fact]
        public void AtMonochrome_ThePicturePlotsInBlackAndWhite()
        {
            using (SKBitmap bitmap = Plotted(Drawing(0, 2)))
            {
                (int red, int blue, int dark) = Counted(bitmap);

                Assert.Equal(0, red);
                Assert.Equal(0, blue);
                Assert.True(dark > 2000, "dark " + dark);
            }
        }

        [Fact]
        public void TheFrame_PlotsOnlyAtOleframe1()
        {
            using (SKBitmap notPlotted = Plotted(Drawing(2, 2)))
            using (SKBitmap off = Plotted(Drawing(2, 0)))
            using (SKBitmap plotted = Plotted(Drawing(2, 1)))
            {
                Assert.Equal(0, Differences(notPlotted, off));
                int drawn = Differences(plotted, notPlotted);
                Assert.True(drawn > 50, "The frame changed " + drawn + " pixels.");
            }
        }

        [Fact]
        public void APdf_HoldsThePictureAsAnImage_UnlessOlehideLeavesItOut()
        {
            string Pdf(CadDocument document)
            {
                using (var memory = new MemoryStream())
                {
                    CadPdfWriter.Write(memory, document, document.ModelSpace, OneToOne(), DisplayBox.Empty);
                    return Encoding.GetEncoding("iso-8859-1").GetString(memory.ToArray());
                }
            }

            Assert.Contains("/Subtype /Image", Pdf(Drawing(2, 2)));
            Assert.DoesNotContain("/Subtype /Image", Pdf(Drawing(2, 2, 3)));
        }
    }
}
