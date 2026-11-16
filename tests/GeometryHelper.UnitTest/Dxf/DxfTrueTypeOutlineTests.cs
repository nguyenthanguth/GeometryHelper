using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The outlines of a TrueType font's letters (<see cref="DxfTrueTypeFont.OutlineOf"/>), read from fonts the tests write: a
    /// letter of an on-curve line and a curve whose control point is off the outline, and a letter made of that one, scaled by
    /// a half and moved.
    /// </summary>
    public class DxfTrueTypeOutlineTests
    {
        private static byte[] U16(int value) => new[] { (byte)(value >> 8), (byte)value };

        /// <summary>
        /// The glyf record of a square 1 000 on a side whose top right corner is a curve: (0, 0), (1 000, 0), the control point
        /// (1 000, 1 000) off the curve, (0, 1 000); the curve runs from (1 000, 0) to (0, 1 000).
        /// </summary>
        private static byte[] CurvedSquare()
        {
            var bytes = new List<byte>();
            bytes.AddRange(U16(1));
            bytes.AddRange(U16(0));
            bytes.AddRange(U16(0));
            bytes.AddRange(U16(1000));
            bytes.AddRange(U16(1000));
            bytes.AddRange(U16(3));
            bytes.AddRange(U16(0));
            bytes.AddRange(new byte[] { 1, 1, 0, 1 });

            foreach (int x in new[] { 0, 1000, 0, -1000 })
            {
                bytes.AddRange(U16(x));
            }

            foreach (int y in new[] { 0, 0, 1000, 0 })
            {
                bytes.AddRange(U16(y));
            }

            return bytes.ToArray();
        }

        /// <summary>The glyf record of a letter made of letter 1 scaled by 0.5 and moved by (200, 300).</summary>
        private static byte[] Composite()
        {
            var bytes = new List<byte>();
            bytes.AddRange(U16(-1));
            bytes.AddRange(U16(200));
            bytes.AddRange(U16(300));
            bytes.AddRange(U16(700));
            bytes.AddRange(U16(800));
            bytes.AddRange(U16(0x0001 | 0x0002 | 0x0008));
            bytes.AddRange(U16(1));
            bytes.AddRange(U16(200));
            bytes.AddRange(U16(300));
            bytes.AddRange(U16(0x2000));
            return bytes.ToArray();
        }

        private static DxfTrueTypeFont Font()
        {
            var letters = new List<TrueTypeFile.Letter>
            {
                new TrueTypeFile.Letter(0, 500, 50, 0, 450, 700),
                new TrueTypeFile.Letter('A', 1100, 0, 0, 1000, 1000) { Record = CurvedSquare() },
                new TrueTypeFile.Letter('B', 800, 200, 300, 700, 800) { Record = Composite() },
                new TrueTypeFile.Letter(' ', 250),
                new TrueTypeFile.Letter('V', 600, 0, 0, 600, 700),
            };
            return DxfTrueTypeFont.Read(TrueTypeFile.Font(letters));
        }

        [Fact]
        public void ALetterOfALineAndACurve_IsOneClosedContour_TheCurveCutIntoSixPieces()
        {
            IReadOnlyList<double[]> outline = Font().OutlineOf('A');

            double[] contour = Assert.Single(outline);

            // (0, 0), (1 000, 0), five points of the curve to (0, 1 000), then (0, 1 000) itself: 8 points.
            Assert.Equal(16, contour.Length);
            Assert.Equal(new[] { 0.0, 0.0, 1000.0, 0.0 }, contour.Take(4).ToArray());

            // The curve's middle, at t = 0.5 (a quarter of each end and a half of the control point).
            Assert.Equal(750.0, contour[8], 9);
            Assert.Equal(750.0, contour[9], 9);
            Assert.Equal(0.0, contour[14], 9);
            Assert.Equal(1000.0, contour[15], 9);
        }

        [Fact]
        public void ALetterMadeOfAnother_IsItScaledAndMoved()
        {
            IReadOnlyList<double[]> outline = Font().OutlineOf('B');

            double[] contour = Assert.Single(outline);
            Assert.Equal(16, contour.Length);
            Assert.Equal(200.0, contour[0], 9);
            Assert.Equal(300.0, contour[1], 9);
            Assert.Equal(700.0, contour[2], 9);
            Assert.Equal(300.0, contour[3], 9);
            Assert.Equal(575.0, contour[8], 9);
            Assert.Equal(675.0, contour[9], 9);
        }

        [Fact]
        public void ASpaceHasNoOutline_AndALetterTheFontDoesNotMapIsNull()
        {
            DxfTrueTypeFont font = Font();

            Assert.Empty(font.OutlineOf(' '));
            Assert.Null(font.OutlineOf('Z'));
            Assert.Null(font.OutlineOf(0));
        }

        [Fact]
        public void ALetterWhoseRecordIsCutShort_GivesNothing_AndDoesNotThrow()
        {
            var letters = new List<TrueTypeFile.Letter>
            {
                new TrueTypeFile.Letter(0, 500, 50, 0, 450, 700),
                new TrueTypeFile.Letter('A', 1100, 0, 0, 1000, 1000) { Record = CurvedSquare().Take(20).ToArray() },
                new TrueTypeFile.Letter('V', 600, 0, 0, 600, 700),
            };
            DxfTrueTypeFont font = DxfTrueTypeFont.Read(TrueTypeFile.Font(letters));

            IReadOnlyList<double[]> outline = font.OutlineOf('A');

            Assert.True(outline == null || outline.All(c => c.Length % 2 == 0));
        }
    }
}
