using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The True Color tab of Select Color as the design draws it, with no window: the field of hue and saturation and the
    /// luminance bar picked by a click, the bars of red, green and blue, the colour model switched, and HSL and RGB kept
    /// together both ways.
    /// </summary>
    public sealed class SelectColorTrueColorTests
    {
        [Fact]
        public void AClickOnTheField_PicksTheHueAcrossAndTheSaturationDown_TheLuminanceKept()
        {
            var dialog = new SelectColorViewModel(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 30), true);
            Assert.Equal(new[] { 30, 100, 50 }, new[] { dialog.Hue, dialog.Saturation, dialog.Luminance });

            // A field 220 wide and 200 high clicked a quarter across and a quarter down: hue 90, saturation 75.
            dialog.PickField(55, 50, 220, 200);

            Assert.Equal(new[] { 90, 75, 50 }, new[] { dialog.Hue, dialog.Saturation, dialog.Luminance });
            Assert.Equal(new[] { 128, 223, 32 }, new[] { dialog.Red, dialog.Green, dialog.Blue });
            Assert.True(dialog.Color.IsByColor);
            Assert.Equal(0.25, dialog.HueAcross, 12);
            Assert.Equal(0.25, dialog.SaturationDown, 12);

            // Dragged out past the field's left foot, it is taken to the corner: hue 0, saturation 0, a grey of luminance 50.
            dialog.PickField(-10, 300, 220, 200);

            Assert.Equal(new[] { 0, 0, 50 }, new[] { dialog.Hue, dialog.Saturation, dialog.Luminance });
            Assert.Equal("128,128,128", dialog.TrueColorText);
        }

        [Fact]
        public void AClickOnTheLuminanceBar_PicksTheLuminanceDown_TheHueAndSaturationKept()
        {
            var dialog = new SelectColorViewModel(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 30), true);
            dialog.PickField(55, 50, 220, 200);

            dialog.PickLuminance(50, 200);

            Assert.Equal(new[] { 90, 75, 75 }, new[] { dialog.Hue, dialog.Saturation, dialog.Luminance });
            Assert.Equal(0.25, dialog.LuminanceDown, 12);

            dialog.PickLuminance(200, 200);

            Assert.Equal("0,0,0", dialog.TrueColorText);
            Assert.Equal(new[] { 90, 75, 0 }, new[] { dialog.Hue, dialog.Saturation, dialog.Luminance });
        }

        [Fact]
        public void HslAndRgb_GoRoundTheOneIntoTheOther()
        {
            // Hue, saturation and luminance typed give red, green and blue, which read back give them again, a degree or a
            // per cent off at most where the colour is too pale or dark to hold a whole one.
            for (int hue = 0; hue < 360; hue += 15)
            {
                foreach (int saturation in new[] { 40, 70, 100 })
                {
                    foreach (int luminance in new[] { 25, 50, 75 })
                    {
                        var typed = new SelectColorViewModel(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7), true);
                        typed.Hue = hue;
                        typed.Saturation = saturation;
                        typed.Luminance = luminance;
                        var read = new SelectColorViewModel(
                            DxfColor.FromRgb((byte)typed.Red, (byte)typed.Green, (byte)typed.Blue),
                            true
                        );

                        int hueOff = Math.Abs(read.Hue - hue) % 360;
                        Assert.True(
                            Math.Min(hueOff, 360 - hueOff) <= 1,
                            "hue " + hue + "," + saturation + "," + luminance + " read " + read.Hue
                        );
                        Assert.InRange(read.Saturation, saturation - 1, saturation + 1);
                        Assert.InRange(read.Luminance, luminance - 1, luminance + 1);
                    }
                }
            }

            // Red, green and blue read as HSL and typed back give the same colour within a step of the 0 to 255 scale.
            for (int rgb = 0; rgb < 0x1000000; rgb += 0x0F3B29)
            {
                var read = new SelectColorViewModel(
                    DxfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb),
                    true
                );
                int back = SelectColorViewModel.HslToRgb(read.Hue, read.Saturation, read.Luminance);

                Assert.InRange((back >> 16) - (rgb >> 16), -3, 3);
                Assert.InRange(((back >> 8) & 0xFF) - ((rgb >> 8) & 0xFF), -3, 3);
                Assert.InRange((back & 0xFF) - (rgb & 0xFF), -3, 3);
            }
        }

        [Fact]
        public void TheColorModelSwitches_BetweenHslAndRgb_TheColourKept_AndABarPicksItsChannel()
        {
            var dialog = new SelectColorViewModel(DxfColor.FromRgb(255, 127, 0), true);
            Assert.True(dialog.IsTrueColorTab);
            Assert.Equal(new[] { "HSL", "RGB" }, dialog.ColorModels);
            Assert.True(dialog.IsHslModel);

            dialog.ColorModel = "RGB";

            Assert.True(dialog.IsRgbModel);
            Assert.False(dialog.IsHslModel);
            Assert.Equal("255,127,0", dialog.TrueColorText);

            dialog.ColorModel = "CMYK";

            Assert.Equal("RGB", dialog.ColorModel);

            // The green bar 436 wide clicked at its middle: green 128; the bar of green, drawn from red and blue, is kept.
            object greenBar = dialog.GreenBar;
            object redBar = dialog.RedBar;
            dialog.PickChannel(1, 218, 436);

            Assert.Equal("255,128,0", dialog.TrueColorText);
            Assert.Same(greenBar, dialog.GreenBar);
            Assert.NotSame(redBar, dialog.RedBar);
            Assert.Equal(128 / 255.0, dialog.GreenAlong, 12);

            dialog.ColorModel = "HSL";

            Assert.Equal("255,128,0", dialog.TrueColorText);
            Assert.Equal(new[] { 30, 100, 50 }, new[] { dialog.Hue, dialog.Saturation, dialog.Luminance });
        }

        [Fact]
        public void TheColorBox_TakesRedGreenAndBlue_AndTheFieldIsDrawnOnceForAllTheDialogs()
        {
            var dialog = new SelectColorViewModel(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5), true);
            dialog.TrueColorText = "10, 20, 30";

            Assert.Equal("Red, Green, Blue: 10,20,30", dialog.ColorText);

            dialog.TrueColorText = "10,20";

            Assert.Equal("10,20,30", dialog.TrueColorText);

            var other = new SelectColorViewModel(DxfColor.ByLayer, true);
            Assert.Same(dialog.HueSaturationField, other.HueSaturationField);
            Assert.Equal(
                new[] { 218, 198 },
                new[] { dialog.HueSaturationField.Width, dialog.HueSaturationField.Height }.Select(v => (int)v)
            );
        }
    }
}
