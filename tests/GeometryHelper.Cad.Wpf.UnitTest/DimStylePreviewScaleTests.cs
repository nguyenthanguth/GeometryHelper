using System.Collections.Generic;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The sample dimensions the Dimension Style Manager and the style dialogs draw of a style whose overall scale is 25, with
    /// words and arrowheads 2.5 (as the scale makes them 62.5): they look as the sample of the same style at scale 1 does,
    /// not as huge words and arrows over a small part.
    /// </summary>
    public class DimStylePreviewScaleTests
    {
        private const int Width = 256;
        private const int Height = 164;

        /// <summary>The pixels of the picture of the style's sample at an overall scale that are not the black ground, and how many fill the middle third of the picture.</summary>
        private static int Ink(double dimscale, double dimtxt)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            CadDocument document = CadDocument.From(database, "preview.dxf");
            SortedDictionary<string, object> values = CadDimStyleEditor.Read(
                database.DimStyleTable[CadDimStyleEditor.Current(database)]
            );
            values["Dimscale"] = dimscale;
            values["Dimtxt"] = dimtxt;
            values["Dimasz"] = dimtxt;
            BitmapSource picture = CadStylePreview.Dimensions(database, values, document.Fonts, Width, Height);
            var pixels = new int[Width * Height];
            picture.CopyPixels(pixels, Width * 4, 0);
            int ink = 0;

            foreach (int pixel in pixels)
            {
                if ((pixel & 0xFFFFFF) != 0)
                {
                    ink++;
                }
            }

            return ink;
        }

        [Fact]
        public void AStyleOfScale25AndWords2Point5_IsPreviewedAsLargeAsTheSameStyleAtScale1()
        {
            int one = Ink(1.0, 2.5);
            int scaled = Ink(25.0, 2.5);

            Assert.True(one > 500, "ink at scale 1: " + one);
            Assert.InRange(scaled, one * 8 / 10, one * 12 / 10);
        }

        [Fact]
        public void ThePreviewOfAStyleNeedsNoScale_ScaleNoughtLooksAsScale1Does()
        {
            Assert.Equal(Ink(1.0, 2.5), Ink(0.0, 2.5));
        }
    }
}
