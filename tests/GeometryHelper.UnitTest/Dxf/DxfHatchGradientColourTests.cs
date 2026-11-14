using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The colour of a gradient at a value along it, as AutoCAD 2021's <c>EvaluateGradientColorAt</c> gives it: each channel cut
    /// down to a whole number, and a gradient of one colour running to the colour's tint in hue, saturation and lightness. The
    /// numbers are those AutoCAD returned in accoreconsole probes (twelve gradients, 1 101 channels of two colours, 25 pairs of
    /// colour and tint at nine values).
    /// </summary>
    public class DxfHatchGradientColourTests
    {
        private static DxfHatch Gradient(params DxfGradientColor[] colours)
        {
            DxfHatch hatch = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AC");
            hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, "LINEAR");
            hatch.SetGradientColors(colours);
            return hatch;
        }

        private static DxfGradientColor At(int red, int green, int blue, float value) =>
            new DxfGradientColor(DxfColor.FromRgb((byte)red, (byte)green, (byte)blue), value);

        private static DxfHatch OneColour(int red, int green, int blue, float tint)
        {
            DxfHatch hatch = Gradient(At(red, green, blue, 0f));
            hatch.GradientOneColorMode = true;
            hatch.ShadeTintValue = tint;
            return hatch;
        }

        private static int Rgb(int red, int green, int blue) => (red << 16) | (green << 8) | blue;

        [Theory]
        [InlineData(0.5f, 127, 127, 204)]
        [InlineData(1f / 32f, 7, 7, 251)]
        [InlineData(5f / 32f, 39, 39, 239)]
        [InlineData(31f / 32f, 247, 247, 156)]
        public void ABlueToPaleYellowGradient_CutsEachChannelDown_WhereItWouldHaveRoundedUp(
            float value,
            int red,
            int green,
            int blue
        )
        {
            DxfHatch hatch = Gradient(At(0, 0, 255, 0f), At(255, 255, 153, 1f));

            Assert.Equal(Rgb(red, green, blue), hatch.EvaluateGradientColorAt(value).ColorValue);
        }

        [Theory]
        [InlineData(0.01f, 2, 19, 252)]
        [InlineData(0.02f, 5, 21, 249)]
        [InlineData(0.03f, 7, 23, 247)]
        public void AGradientBetweenTwoColoursOfDifferentChannels_IsTheSameAsAutoCadAtOneHundredthSteps(
            float value,
            int red,
            int green,
            int blue
        )
        {
            DxfHatch hatch = Gradient(At(0, 17, 255, 0f), At(255, 230, 3, 1f));

            Assert.Equal(Rgb(red, green, blue), hatch.EvaluateGradientColorAt(value).ColorValue);
        }

        [Theory]
        [InlineData(1, 17, 26, 36)]
        [InlineData(3, 32, 40, 48)]
        [InlineData(9, 77, 81, 86)]
        [InlineData(16, 130, 130, 130)]
        [InlineData(17, 137, 136, 136)]
        public void AGradientOfDarkToLightColours_IsAutoCadsAtThirtySecondSteps(int step, int red, int green, int blue)
        {
            DxfHatch hatch = Gradient(At(10, 20, 30, 0f), At(250, 240, 230, 1f));

            Assert.Equal(Rgb(red, green, blue), hatch.EvaluateGradientColorAt(step / 32f).ColorValue);
        }

        [Theory]
        [InlineData(100, 150, 50, 0.25f, 82, 123, 41, 64, 96, 32)]
        [InlineData(100, 150, 50, 0.5f, 114, 170, 57, 128, 191, 64)]
        [InlineData(100, 150, 50, 0.75f, 145, 186, 104, 191, 223, 159)]
        [InlineData(100, 150, 50, 1f, 177, 202, 152, 255, 255, 255)]
        [InlineData(100, 150, 50, 0f, 50, 75, 25, 0, 0, 0)]
        [InlineData(200, 100, 50, 0.25f, 151, 75, 37, 102, 51, 25)]
        [InlineData(200, 100, 50, 0.5f, 202, 101, 50, 204, 102, 51)]
        [InlineData(128, 128, 128, 0.75f, 159, 159, 159, 191, 191, 191)]
        [InlineData(255, 0, 0, 1f, 255, 127, 127, 255, 255, 255)]
        public void AGradientOfOneColour_RunsToItsTintInHueSaturationAndLightness_AsAutoCadEvaluatesIt(
            int red,
            int green,
            int blue,
            float tint,
            int middleRed,
            int middleGreen,
            int middleBlue,
            int endRed,
            int endGreen,
            int endBlue
        )
        {
            DxfHatch hatch = OneColour(red, green, blue, tint);

            Assert.Equal(Rgb(red, green, blue), hatch.EvaluateGradientColorAt(0f).ColorValue);
            Assert.Equal(Rgb(middleRed, middleGreen, middleBlue), hatch.EvaluateGradientColorAt(0.5f).ColorValue);
            Assert.Equal(Rgb(endRed, endGreen, endBlue), hatch.EvaluateGradientColorAt(1f).ColorValue);
        }
    }
}
