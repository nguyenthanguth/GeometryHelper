using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <c>ColorValue</c> of the colours that are not a colour of their own, as AutoCAD 2021 gives it (probe GhD7, ARGB of
    /// <c>Color.ColorValue</c>): BYLAYER white (255, 255, 255), BYBLOCK black, the foreground white, while its <c>Red</c>,
    /// <c>Green</c> and <c>Blue</c> are 0 for BYLAYER.
    /// </summary>
    public class DxfColorValueTests
    {
        [Fact]
        public void ByLayer_HasTheColorValueOfWhite_AsAutoCadGivesIt()
        {
            Assert.Equal(0xFFFFFF, DxfColor.ByLayer.ColorValue);
            Assert.Equal(0xFFFFFF, DxfColor.FromColorIndex(DxfColorMethod.ByLayer, 256).ColorValue);
            Assert.Equal(0xFFFFFF, DxfColor.OfIndex(256).ColorValue);
        }

        [Fact]
        public void ByLayer_KeepsItsRedGreenAndBlueAtNought()
        {
            Assert.Equal(0, DxfColor.ByLayer.Red);
            Assert.Equal(0, DxfColor.ByLayer.Green);
            Assert.Equal(0, DxfColor.ByLayer.Blue);
        }

        [Fact]
        public void AColourOfTheIndex_GivesItsPaletteForRedGreenAndBlue_WhereAutoCadGivesNought()
        {
            // AutoCAD 2021 (probe Gh8): Color.FromColorIndex(ByAci, 1) has Red, Green and Blue 0 and ColorValue 0xFF0000; 30 is
            // 0xFF7F00 and 140 is 0x00BFFF. Here the three read as the palette colour, which the viewer draws with.
            DxfColor red = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);

            Assert.Equal(0xFF0000, red.ColorValue);
            Assert.Equal(255, red.Red);
            Assert.Equal(0, red.Green);
            Assert.Equal(0, red.Blue);
            Assert.Equal(0xFF7F00, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 30).ColorValue);
            Assert.Equal(0x00BFFF, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 140).ColorValue);
        }

        [Fact]
        public void ByBlock_HasTheColorValueOfBlack_AndTheForegroundOfWhite()
        {
            Assert.Equal(0, DxfColor.ByBlock.ColorValue);
            Assert.Equal(0xFFFFFF, DxfColor.FromColorIndex(DxfColorMethod.Foreground, 7).ColorValue);
        }

        [Fact]
        public void ByLayer_IsStillNotEqualToAColourOfAnotherMethod_WhateverItsColorValue()
        {
            Assert.NotEqual(DxfColor.ByLayer, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7));
            Assert.NotEqual(DxfColor.ByLayer, DxfColor.FromRgb(255, 255, 255));
        }
    }
}
