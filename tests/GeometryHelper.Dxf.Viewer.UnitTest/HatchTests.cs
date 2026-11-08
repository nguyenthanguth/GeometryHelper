using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class HatchTests
    {
        private static readonly GeoPoint2[] Square = { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) };

        /// <summary>A square 30 by 20 hatched with lines of its own at 45 degrees, the spacing given.</summary>
        private static DisplayList2 UserHatch(double spacing, Action<DxfHatch> more = null)
        {
            var database = new DxfDatabase("AC1018");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = Math.PI / 4.0;
            hatch.PatternSpace = spacing;
            hatch.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            database.ModelSpace.AppendEntity(hatch);
            more?.Invoke(hatch);
            CadDocument document = CadDocument.From(database);
            return document.GetDisplay(document.ModelSpace);
        }

        [Fact]
        public void ASquareHatchedWithLinesAt45DegreesDrawsItsElevenLinesInsideItAndNotItsBoundary()
        {
            DisplayList2 list = UserHatch(3.175);

            DisplaySegments2 lines = Assert.IsType<DisplaySegments2>(Assert.Single(list.Items));
            Assert.Equal(11, lines.Count);
            Assert.Null(lines.Style.Dashes);

            for (int i = 0; i < lines.Count; i++)
            {
                double x0 = lines.Xy[4 * i];
                double y0 = lines.Xy[4 * i + 1];
                double x1 = lines.Xy[4 * i + 2];
                double y1 = lines.Xy[4 * i + 3];
                Assert.InRange(x0, -1E-9, 30 + 1E-9);
                Assert.InRange(y1, -1E-9, 20 + 1E-9);
                Assert.Equal(1.0, (y1 - y0) / (x1 - x0), 9);
            }
        }

        [Theory]
        [InlineData(0.001)]
        [InlineData(0.00001)]
        public void AHatchWhoseLinesWouldPassTwentyThousandIsDrawnByItsBoundary(double spacing)
        {
            // 36 000 lines at the first spacing, worked out and refused; 3 600 000 at the second, refused by their count first.
            DisplayList2 list = UserHatch(spacing);

            DisplayPolyline2 boundary = Assert.IsType<DisplayPolyline2>(Assert.Single(list.Items));
            Assert.True(boundary.Closed);
            Assert.Equal(30.0, boundary.Bounds.MaxX, 9);
        }

        [Fact]
        public void APatternTheDrawingHoldsNoLinesOfIsDrawnByItsBoundary()
        {
            var database = new DxfDatabase("AC1018");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            hatch.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            database.ModelSpace.AppendEntity(hatch);
            CadDocument document = CadDocument.From(database);

            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.True(Assert.IsType<DisplayPolyline2>(Assert.Single(list.Items)).Closed);
        }

        [Fact]
        public void AGradientIsFilledWithTheColourHalfwayAlongIt()
        {
            var database = new DxfDatabase("AC1018");
            var hatch = new DxfHatch();
            hatch.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            database.ModelSpace.AppendEntity(hatch);
            hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, "LINEAR");
            CadDocument document = CadDocument.From(database);

            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            DisplayFill2 fill = Assert.IsType<DisplayFill2>(Assert.Single(list.Items));
            Assert.Equal(hatch.EvaluateGradientColorAt(0.5f).ColorValue & 0xFFFFFF, fill.Style.Rgb);
            Assert.Equal((128 << 16) | (128 << 8) | 204, fill.Style.Rgb);
        }

        [Fact]
        public void AHatchSeenFromBelowDrawsItsLinesMirroredIntoTheWorld()
        {
            // The same square in the plane whose normal points down: its own x runs along the world's -x.
            DisplayList2 list = UserHatch(3.175, hatch => hatch.Normal = new GeoVector3(0, 0, -1));

            DisplaySegments2 lines = Assert.IsType<DisplaySegments2>(Assert.Single(list.Items));
            Assert.Equal(11, lines.Count);
            Assert.InRange(lines.Bounds.MaxX, -1E-9, 1E-9);
            Assert.Equal(-30.0, lines.Bounds.MinX, 9);
            Assert.Equal(-1.0, (lines.Xy[3] - lines.Xy[1]) / (lines.Xy[2] - lines.Xy[0]), 9);
        }

        [Fact]
        public void APickOnAPatternLineTakesTheHatchAndOneBetweenTheLinesDoesNot()
        {
            var database = new DxfDatabase("AC1018");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = 0.0;
            hatch.PatternSpace = 5.0;
            hatch.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            database.ModelSpace.AppendEntity(hatch);
            CadDocument document = CadDocument.From(database);
            PickIndex index = document.GetPickIndex(document.ModelSpace);

            Assert.Equal(new[] { hatch.Handle }, index.Pick(12, 10.1, 0.3));
            Assert.Empty(index.Pick(12, 12.5, 0.3));
        }
    }
}
