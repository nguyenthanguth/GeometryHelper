using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Setting <c>ShadeTintValue</c> of a gradient of one colour makes its second colour the tint of the first, as AutoCAD
    /// 2021's does (probed in accoreconsole: red at 0 and green at 1 in one-colour mode, the tint set to 0.3, and the colours
    /// read back are red and (153, 0, 0); the same value set again gives them again).
    /// </summary>
    public class DxfHatchShadeTintTests
    {
        private static DxfHatch Gradient(params DxfGradientColor[] colours)
        {
            DxfHatch hatch = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AC");
            hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, "LINEAR");
            hatch.SetGradientColors(colours);
            return hatch;
        }

        private static DxfGradientColor At(DxfColor colour, float value) => new DxfGradientColor(colour, value);

        private static DxfColor Rgb(int red, int green, int blue) =>
            DxfColor.FromRgb((byte)red, (byte)green, (byte)blue);

        private static (int, int, int) Channels(DxfColor colour) => (colour.Red, colour.Green, colour.Blue);

        [Theory]
        [InlineData(255, 0, 0, 0.3f, 153, 0, 0)]
        [InlineData(255, 0, 0, 0.6f, 255, 51, 51)]
        [InlineData(255, 0, 0, 0.9f, 255, 204, 204)]
        [InlineData(255, 0, 0, 0.25f, 128, 0, 0)]
        [InlineData(100, 150, 50, 0.75f, 191, 223, 159)]
        [InlineData(100, 150, 50, 0.333333f, 84, 126, 42)]
        [InlineData(100, 150, 50, 0.0001f, 0, 0, 0)]
        [InlineData(100, 150, 50, 0.336f, 84, 126, 42)]
        [InlineData(100, 150, 50, 0.345f, 87, 130, 43)]
        [InlineData(100, 150, 50, 0.285f, 71, 107, 36)]
        [InlineData(100, 150, 50, 0.994f, 252, 254, 251)]
        [InlineData(100, 150, 50, 0.57f, 145, 200, 91)]
        [InlineData(100, 150, 50, 0.015f, 3, 4, 1)]
        [InlineData(100, 150, 50, 0.125f, 31, 46, 15)]
        [InlineData(100, 150, 50, 0.875f, 222, 238, 205)]
        [InlineData(200, 100, 50, 0.345f, 139, 69, 35)]
        [InlineData(0, 128, 255, 0.994f, 250, 252, 255)]
        public void TheTintSetInOneColourMode_MakesTheSecondColourTheTintOfTheFirst(
            int red,
            int green,
            int blue,
            float tint,
            int tintRed,
            int tintGreen,
            int tintBlue
        )
        {
            DxfHatch hatch = Gradient(At(Rgb(red, green, blue), 0f), At(Rgb(0, 0, 255), 1f));
            hatch.GradientOneColorMode = true;

            hatch.ShadeTintValue = tint;

            DxfGradientColor[] colours = hatch.GetGradientColors();
            Assert.Equal(2, colours.Length);
            Assert.Equal((red, green, blue), Channels(colours[0].Color));
            Assert.Equal(0f, colours[0].Value);
            Assert.Equal((tintRed, tintGreen, tintBlue), Channels(colours[1].Color));
            Assert.True(colours[1].Color.IsByColor);
            Assert.Equal(1f, colours[1].Value);
        }

        [Fact]
        public void TheSameTintSetAgain_MakesTheSecondColourTheTintAgain()
        {
            DxfHatch hatch = Gradient(At(Rgb(255, 0, 0), 0f), At(Rgb(0, 255, 0), 1f));
            hatch.GradientOneColorMode = true;
            hatch.ShadeTintValue = 0.3f;
            hatch.SetGradientColors(new[] { At(Rgb(255, 0, 0), 0f), At(Rgb(0, 255, 0), 1f) });
            Assert.Equal((0, 255, 0), Channels(hatch.GetGradientColors()[1].Color));

            hatch.ShadeTintValue = 0.3f;

            Assert.Equal((153, 0, 0), Channels(hatch.GetGradientColors()[1].Color));
        }

        [Fact]
        public void AFirstColourOfTheIndex_StaysOfTheIndex_AndItsPaletteColourIsTinted()
        {
            // Index 7 is white in the palette, so its tint at 0.3 is a grey of 77, as AutoCAD gave for it.
            DxfHatch hatch = Gradient(
                At(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7), 0f),
                At(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2), 1f)
            );
            hatch.GradientOneColorMode = true;

            hatch.ShadeTintValue = 0.3f;

            DxfGradientColor[] colours = hatch.GetGradientColors();
            Assert.True(colours[0].Color.IsByAci);
            Assert.Equal(7, colours[0].Color.ColorIndex);
            Assert.True(colours[1].Color.IsByColor);
            Assert.Equal((77, 77, 77), Channels(colours[1].Color));
        }

        [Fact]
        public void TheSecondColoursValue_StaysWhereItWas()
        {
            DxfHatch hatch = Gradient(At(Rgb(255, 0, 0), 0f), At(Rgb(0, 255, 0), 0.5f));
            hatch.GradientOneColorMode = true;

            hatch.ShadeTintValue = 0.3f;

            DxfGradientColor[] colours = hatch.GetGradientColors();
            Assert.Equal(0.5f, colours[1].Value);
            Assert.Equal((153, 0, 0), Channels(colours[1].Color));
        }

        [Fact]
        public void TheTintSetInTwoColourMode_ChangesNoColour_AndNeitherDoesTurningOneColourModeOn()
        {
            DxfHatch hatch = Gradient(At(Rgb(255, 0, 0), 0f), At(Rgb(0, 255, 0), 1f));

            hatch.ShadeTintValue = 0.9f;
            Assert.Equal((0, 255, 0), Channels(hatch.GetGradientColors()[1].Color));

            hatch.GradientOneColorMode = true;
            Assert.Equal(0.9f, hatch.ShadeTintValue);
            Assert.Equal((0, 255, 0), Channels(hatch.GetGradientColors()[1].Color));
        }

        [Fact]
        public void SettingTheColoursInOneColourMode_KeepsThemAsTheyAre_UntilTheTintIsSet()
        {
            DxfHatch hatch = Gradient(At(Rgb(255, 0, 0), 0f), At(Rgb(0, 255, 0), 1f));
            hatch.GradientOneColorMode = true;
            hatch.ShadeTintValue = 0.6f;

            hatch.SetGradientColors(new[] { At(Rgb(100, 150, 50), 0f), At(Rgb(0, 0, 0), 1f) });

            Assert.Equal((0, 0, 0), Channels(hatch.GetGradientColors()[1].Color));

            hatch.ShadeTintValue = 0.75f;

            Assert.Equal((191, 223, 159), Channels(hatch.GetGradientColors()[1].Color));
        }

        [Fact]
        public void AGradientOfOneColourAlone_RunsToTheTintInWholeHundredths()
        {
            // (100, 150, 50) at 0.336 ends in (84, 126, 42), halfway (92, 138, 46), as AutoCAD evaluated it.
            DxfHatch hatch = Gradient(At(Rgb(100, 150, 50), 0f));
            hatch.GradientOneColorMode = true;
            hatch.ShadeTintValue = 0.336f;

            Assert.Equal((84, 126, 42), Channels(hatch.EvaluateGradientColorAt(1f)));
            Assert.Equal((92, 138, 46), Channels(hatch.EvaluateGradientColorAt(0.5f)));
        }
    }
}
