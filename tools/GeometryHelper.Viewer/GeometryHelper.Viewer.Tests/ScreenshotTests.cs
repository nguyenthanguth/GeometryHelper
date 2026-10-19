using System;
using System.Linq;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ScreenshotTests
    {
        [Fact]
        public void TheCommandLine_TakesTheViewsTheScaleAndTheBackgroundOfAPicture()
        {
            CommandLine line = CommandLine.Parse(new[] { "box.csx", "--screenshot", "out.png", "--views", "4", "--scale", "2", "--background", "Transparent", "--mode", "wireframe" });

            Assert.Null(line.Problem);
            Assert.Equal("box.csx", line.File);
            Assert.Equal("out.png", line.Screenshot);
            Assert.Equal(4, line.Views);
            Assert.Equal(2, line.Scale);
            Assert.Equal(ScreenshotBackground.Transparent, line.Background);
            Assert.Equal("wireframe", line.Mode);
            Assert.True(line.Headless);
        }

        [Fact]
        public void ACommandLineThatGivesNoneOfThem_TakesTheOldPicture_OneViewAtTheSizeOfTheWindowOverTheColourOfTheWindow()
        {
            CommandLine line = CommandLine.Parse(new[] { "box.csx", "--screenshot", "out.png", "--view", "top" });

            Assert.Null(line.Problem);
            Assert.Null(line.Views);
            Assert.Equal(1, line.Scale);
            Assert.Equal(ScreenshotBackground.Window, line.Background);
        }

        [Theory]
        [InlineData("--views", "3")]
        [InlineData("--views", "0")]
        [InlineData("--views", "four")]
        [InlineData("--scale", "3")]
        [InlineData("--scale", "8")]
        [InlineData("--scale", "x")]
        [InlineData("--background", "black")]
        [InlineData("--background", "7")]
        public void AViewsAScaleOrABackgroundThatIsNotOne_IsRefusedWithAMessageNamingIt(string option, string value)
        {
            CommandLine line = CommandLine.Parse(new[] { "box.csx", option, value });

            Assert.NotNull(line.Problem);
            Assert.Contains(option, line.Problem, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("--views")]
        [InlineData("--scale")]
        [InlineData("--background")]
        public void AnOptionWithNoValueAfterIt_IsRefused(string option)
        {
            Assert.Contains(option, CommandLine.Parse(new[] { "box.csx", option }).Problem, StringComparison.Ordinal);
        }

        [Fact]
        public void TheScaleOfAPicture_IsOneTwoOrFour_AndAnythingElseIsTakenAsOne()
        {
            Assert.All(new[] { 1, 2, 4 }, s => Assert.True(ScreenshotOptions.IsScale(s)));
            Assert.All(new[] { 0, 3, 5, 8, -1 }, s => Assert.False(ScreenshotOptions.IsScale(s)));

            var options = new ScreenshotOptions { Scale = 4 };
            Assert.Equal(4, options.Scale);
            options.Scale = 3;
            Assert.Equal(1, options.Scale);
        }

        [Fact]
        public void APictureOfAllTheViews_IsTheAreaTheyShareTimesTheScale_AndEachViewIsWhereItIsInTheWindowTimesTheScale()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);
            var options = new ScreenshotOptions { AllViews = true, Scale = 2 };

            options.PictureSize(layout, 1002.0, 802.0, 2.0, out int width, out int height);
            var cells = options.Cells(layout, 1002.0, 802.0, 2.0);

            Assert.Equal(2004, width);
            Assert.Equal(1604, height);
            Assert.Equal(new[] { 0, 1, 2, 3 }, cells.Select(c => c.Slot));
            Assert.Equal(new[] { 0.0, 1004.0, 0.0, 1004.0 }, cells.Select(c => c.X));
            Assert.Equal(new[] { 0.0, 0.0, 804.0, 804.0 }, cells.Select(c => c.Y));
            Assert.All(cells, c => Assert.Equal(1000.0, c.Width));
            Assert.All(cells, c => Assert.Equal(800.0, c.Height));
        }

        [Fact]
        public void APictureOfTheActiveView_IsTheSizeOfItsCellTimesTheScale_AtTheCornerOfThePicture()
        {
            var layout = new ViewLayout();
            layout.SetCount(4);
            layout.Activate(2);
            var options = new ScreenshotOptions { AllViews = false, Scale = 4 };

            options.PictureSize(layout, 1002.0, 802.0, 2.0, out int width, out int height);
            ViewCell cell = Assert.Single(options.Cells(layout, 1002.0, 802.0, 2.0));

            Assert.Equal(2, cell.Slot);
            Assert.Equal(0.0, cell.X);
            Assert.Equal(0.0, cell.Y);
            Assert.Equal(2000, width);
            Assert.Equal(1600, height);
        }

        [Fact]
        public void APictureOfOneView_IsTheWholeAreaOnceOrTwiceOrFourTimes_AndNeverSmallerThanAPixel()
        {
            var layout = new ViewLayout();

            foreach (int scale in new[] { 1, 2, 4 })
            {
                var options = new ScreenshotOptions { AllViews = true, Scale = scale };
                options.PictureSize(layout, 693.6, 539.2, 2.0, out int width, out int height);

                Assert.Equal((int)Math.Ceiling(693.6 * scale), width);
                Assert.Equal((int)Math.Ceiling(539.2 * scale), height);
                Assert.Equal(0.0, Assert.Single(options.Cells(layout, 693.6, 539.2, 2.0)).X);
            }

            new ScreenshotOptions().PictureSize(layout, 0.0, 0.0, 2.0, out int w, out int h);
            Assert.True(w >= 1 && h >= 1);
            Assert.Throws<ArgumentNullException>(() => new ScreenshotOptions().Cells(null, 10, 10, 2));
        }

        [Fact]
        public void TheOptionsOfAPicture_StartAsAllTheViewsAtTheSizeOfTheWindowOverWhite()
        {
            Assert.Equal(ScreenshotBackground.White, new ScreenshotOptions().Background);
            Assert.True(new ScreenshotOptions().AllViews);
            Assert.Equal(1, new ScreenshotOptions().Scale);
        }
    }
}
