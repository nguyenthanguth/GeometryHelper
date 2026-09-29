using System;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// <see cref="Sheet"/>: the size of the paper, its scale and offsets, where it lies, and where each new sheet goes.
    /// </summary>
    public class SheetTests
    {
        [Theory]
        [InlineData(PaperSize.A0, 1189.0, 841.0)]
        [InlineData(PaperSize.A1, 841.0, 594.0)]
        [InlineData(PaperSize.A2, 594.0, 420.0)]
        [InlineData(PaperSize.A3, 420.0, 297.0)]
        [InlineData(PaperSize.A4, 297.0, 210.0)]
        public void TheASeries_IsLandscapeUnlessTurned(PaperSize size, double longSide, double shortSide)
        {
            var landscape = new Sheet(size);
            Assert.Equal(SheetOrientation.Landscape, landscape.Orientation);
            Assert.Equal(longSide, landscape.PaperWidth);
            Assert.Equal(shortSide, landscape.PaperHeight);

            var portrait = new Sheet(size, SheetOrientation.Portrait);
            Assert.Equal(shortSide, portrait.PaperWidth);
            Assert.Equal(longSide, portrait.PaperHeight);
        }

        [Fact]
        public void ASizeOfItsOwn_IsMadeByCustom()
        {
            Sheet wide = Sheet.Custom(1000.0, 700.0);
            Assert.Equal(PaperSize.Custom, wide.Size);
            Assert.Equal(SheetOrientation.Landscape, wide.Orientation);
            Assert.Equal(1000.0, wide.PaperWidth);
            Assert.Equal(700.0, wide.PaperHeight);

            Assert.Equal(SheetOrientation.Portrait, Sheet.Custom(300.0, 500.0).Orientation);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Sheet(PaperSize.Custom));
        }

        [Theory]
        [InlineData(0.0, 700.0)]
        [InlineData(-1.0, 700.0)]
        [InlineData(double.NaN, 700.0)]
        [InlineData(1000.0, double.PositiveInfinity)]
        public void ASizeOfItsOwn_HasToBeAFiniteNumberAboveNought(double width, double height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Sheet.Custom(width, height));
        }

        [Fact]
        public void TheScale_TurnsPaperIntoTheUnitsOfTheBoxes()
        {
            var sheet = new Sheet(PaperSize.A1) { Scale = 50.0 };

            Assert.Equal(42050.0, sheet.Width);
            Assert.Equal(29700.0, sheet.Height);

            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Scale = 0.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Scale = double.NaN);
            Assert.Equal(50.0, sheet.Scale);
        }

        [Fact]
        public void TheValuesOfASheet_HaveToBeFiniteAndNotNegative()
        {
            var sheet = new Sheet(PaperSize.A3);

            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.OffsetLeft = -1.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.OffsetRight = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.OffsetTop = double.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.OffsetBottom = -0.5);
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SheetSpacing = -1.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Origin = new GeoPoint2(double.NaN, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.NewSheet = (SheetDirection)7);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Sheet((PaperSize)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Sheet(PaperSize.A3, (SheetOrientation)5));

            // Each refused value left the sheet as it was.
            Assert.Equal(0.0, sheet.OffsetLeft);
            Assert.Equal(0.0, sheet.SheetSpacing);
            Assert.Equal(new GeoPoint2(0.0, 0.0), sheet.Origin);
            Assert.Equal(SheetDirection.Right, sheet.NewSheet);
        }

        /// <summary>
        /// The first sheet lies at the origin, its usable area inside the offsets, which are on paper and scale with it.
        /// </summary>
        [Fact]
        public void TheFirstFrame_LiesAtTheOrigin_ItsUsableAreaInsideTheOffsets()
        {
            var sheet = new Sheet(PaperSize.A3)
            {
                Scale = 10.0,
                OffsetLeft = 20.0,
                OffsetRight = 10.0,
                OffsetTop = 5.0,
                OffsetBottom = 15.0,
                Origin = new GeoPoint2(1000.0, -500.0),
            };

            SheetFrame frame = sheet.GetFrame(0);

            Assert.Equal(0, frame.Index);
            Assert.True(frame.Bounds.LowerLeft.IsEqualTo(new GeoPoint2(1000.0, -500.0)));
            Assert.True(frame.Bounds.UpperRight.IsEqualTo(new GeoPoint2(1000.0 + 4200.0, -500.0 + 2970.0)));
            Assert.True(frame.UsableArea.LowerLeft.IsEqualTo(new GeoPoint2(1000.0 + 200.0, -500.0 + 150.0)));
            Assert.True(frame.UsableArea.UpperRight.IsEqualTo(new GeoPoint2(1000.0 + 4200.0 - 100.0, -500.0 + 2970.0 - 50.0)));
        }

        /// <summary>
        /// Each new sheet lies beside the one before, on the side asked for, the spacing between them on paper too.
        /// </summary>
        [Theory]
        [InlineData(SheetDirection.Right, 470.0, 0.0)]
        [InlineData(SheetDirection.Left, -470.0, 0.0)]
        [InlineData(SheetDirection.Top, 0.0, 347.0)]
        [InlineData(SheetDirection.Bottom, 0.0, -347.0)]
        public void ANewSheet_LiesBesideTheOneBefore(SheetDirection direction, double stepX, double stepY)
        {
            var sheet = new Sheet(PaperSize.A3) { NewSheet = direction, SheetSpacing = 50.0 };

            for (int k = 0; k < 4; k++)
            {
                SheetFrame frame = sheet.GetFrame(k);
                Assert.Equal(k, frame.Index);
                Assert.True(frame.Bounds.LowerLeft.IsEqualTo(new GeoPoint2(k * stepX, k * stepY)), frame.ToString());
                Assert.Equal(420.0, frame.Bounds.Width, 9);
                Assert.Equal(297.0, frame.Bounds.Height, 9);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetFrame(-1));
        }

        [Fact]
        public void TheSheetsAfterTheFirst_StepByTheirScaledSizeAndSpacing()
        {
            var sheet = new Sheet(PaperSize.A1) { Scale = 50.0, SheetSpacing = 100.0, NewSheet = SheetDirection.Bottom };

            // 594 + 100 mm of paper at 1:50.
            Assert.True(sheet.GetFrame(2).Bounds.LowerLeft.IsEqualTo(new GeoPoint2(0.0, -2 * 34700.0)));
        }

        [Theory]
        [InlineData(SheetCorner.LowerLeft, 100.0, 200.0)]
        [InlineData(SheetCorner.LowerRight, 100.0 - 420.0, 200.0)]
        [InlineData(SheetCorner.UpperLeft, 100.0, 200.0 - 297.0)]
        [InlineData(SheetCorner.UpperRight, 100.0 - 420.0, 200.0 - 297.0)]
        [InlineData(SheetCorner.Center, 100.0 - 210.0, 200.0 - 148.5)]
        public void PlaceCorner_PutsThatPointOfTheSheetThere(SheetCorner corner, double originX, double originY)
        {
            var sheet = new Sheet(PaperSize.A3);

            sheet.PlaceCorner(corner, new GeoPoint2(100.0, 200.0));

            Assert.True(sheet.Origin.IsEqualTo(new GeoPoint2(originX, originY)), sheet.Origin.ToString());
        }

        [Fact]
        public void PlaceCorner_RefusesACornerThatIsNone()
        {
            var sheet = new Sheet(PaperSize.A3);

            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.PlaceCorner((SheetCorner)9, new GeoPoint2(0.0, 0.0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.PlaceCorner(SheetCorner.UpperRight, new GeoPoint2(double.NaN, 0.0)));
        }

        [Fact]
        public void OffsetsThatLeaveNoRoom_AreRefusedWhenTheFrameIsAskedFor()
        {
            var sheet = new Sheet(PaperSize.A4) { OffsetLeft = 150.0, OffsetRight = 147.0 };

            Assert.Throws<InvalidOperationException>(() => sheet.GetFrame(0));

            sheet.OffsetRight = 146.0;
            Assert.Equal(1.0, sheet.GetFrame(0).UsableArea.Width, 9);
        }
    }
}
