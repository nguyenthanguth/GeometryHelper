using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The words of an MTEXT laid out as AutoCAD 2021 lays them: wrapped at its width, its lines 5/3 of its height apart, its
    /// stacks, tabs, paragraphs, columns, mask and frame. Every text is 10 high in a font 10 above its baseline, so a unit of
    /// the font is a unit of the drawing: an I is a line up at 1 moving the pen 4, and a space moves it 5.
    /// </summary>
    public sealed class MTextLayoutTests : IDisposable
    {
        private const double Close = 1E-9;
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        private DisplayText2 Shown(string contents, double width = 0.0, Action<DxfMText> more = null)
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            var mtext = new DxfMText
            {
                Contents = contents,
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 10.0,
                Width = width,
            };
            database.ModelSpace.AppendEntity(mtext);
            mtext.TextStyleName = "GH_SHX";
            more?.Invoke(mtext);
            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null, f => f == "Arial");
            return document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();
        }

        private static DisplayText2[] Lines(DisplayText2 shown) => shown.Parts.OfType<DisplayText2>().ToArray();

        /// <summary>Where the foot of the first letter of a part stands.</summary>
        private static void Foot(double x, double y, DisplayText2 part)
        {
            Assert.Equal(x, part.Strokes[0][0], 9);
            Assert.Equal(y, part.Strokes[0][1], 9);
        }

        [Fact]
        public void WordsPastTheWidth_GoToTheNextLine_FiveThirdsOfTheHeightBelow()
        {
            DisplayText2 shown = Shown("II II II", 20.0);

            // "II II" is inked 17 wide and fits 20; a third II would end at 30. The ink of a line starts on the edge of
            // the box, its first baseline the height below the top, the next 10 x 5 / 3 below it.
            DisplayText2[] lines = Lines(shown);
            Assert.Equal(new[] { "II II", "II" }, lines.Select(t => t.Text).ToArray());
            Foot(0.0, -10.0, lines[0]);
            Foot(0.0, -10.0 - (50.0 / 3.0), lines[1]);
        }

        [Fact]
        public void AWidthOfZero_WrapsNothing_AndAWordWiderThanTheWidthStaysWhole()
        {
            Assert.Equal(new[] { "II II II" }, Lines(Shown("II II II")).Select(t => t.Text).ToArray());

            DisplayText2 wide = Shown("I IIIIII I", 10.0);
            Assert.Equal(new[] { "I", "IIIIII", "I" }, Lines(wide).Select(t => t.Text).ToArray());

            // Six letters inked from the first to the last, 20 apart: the MTEXT is as wide as its widest ink.
            Assert.Equal(20.0, wide.EstimatedWidth, 9);
        }

        [Theory]
        [InlineData(DxfAttachmentPoint.TopCenter, -8.5, -10.0)]
        [InlineData(DxfAttachmentPoint.TopRight, -17.0, -10.0)]
        [InlineData(DxfAttachmentPoint.MiddleLeft, 0.0, 3.0 + (1.0 / 3.0))]
        [InlineData(DxfAttachmentPoint.BottomRight, -17.0, 50.0 / 3.0)]
        public void TheBlockOfLinesHangsByItsAttachmentPoint_TheInkOfEachLineSetInTheWidth(
            DxfAttachmentPoint attachment,
            double x,
            double y
        )
        {
            DisplayText2 shown = Shown("II II II", 20.0, t => t.Attachment = attachment);

            // The block is 20 wide and 10 + 50 / 3 high, no ink below the last baseline. Centred, the 17 of ink of the first
            // line stand from -8.5; on the right they end on the location.
            Foot(x, y, Lines(shown)[0]);
        }

        [Fact]
        public void ExactSpacingKeepsItsPitchUnderATallerRun_AtLeastSpacingMakesRoomForIt()
        {
            Action<DxfMText> exactly = t => t.LineSpacingStyle = DxfLineSpacingStyle.Exactly;

            DisplayText2[] atLeast = Lines(Shown(@"I\P{\H2x;I}\PI"));
            DisplayText2[] exact = Lines(Shown(@"I\P{\H2x;I}\PI", 0.0, exactly));
            DisplayText2[] twice = Lines(Shown(@"I\PI", 0.0, t => t.LineSpacingFactor = 2.0));

            // At least: the line of the letter 20 high stands 10 lower than a line of 10 would, and the line after it
            // 20 + 20 / 3 + 10 / 3 less the 10 it is already down.
            Assert.Equal(-10.0, atLeast[0].Strokes[0][1], 9);
            Assert.Equal(-10.0 - (50.0 / 3.0) - 10.0, atLeast[1].Strokes[0][1], 9);
            Assert.Equal(atLeast[1].Strokes[0][1] - (20.0 / 3.0) - (10.0 / 3.0) - 10.0, atLeast[2].Strokes[0][1], 9);

            // Exactly: 50 / 3 from baseline to baseline whatever stands on the lines.
            Assert.Equal(-10.0 - (50.0 / 3.0), exact[1].Strokes[0][1], 9);
            Assert.Equal(-10.0 - (100.0 / 3.0), exact[2].Strokes[0][1], 9);

            // A factor of 2 doubles the pitch.
            Assert.Equal(-10.0 - (100.0 / 3.0), twice[1].Strokes[0][1], 9);
        }

        [Fact]
        public void AStackedFraction_StandsItsLowerPartOnTheBaseline_TheUpperAThirdOfTheHeightAboveIt_ABarBetween()
        {
            DisplayText2 shown = Shown(@"I\SI/II;I");

            // The pen stands at 3 after the first letter. The lower part "II" is inked 4 wide from it, the upper "I" centred
            // over it; the bar a sixth of the height over the lower part, as long as the wider part; the pen moves on by the
            // wider ink and a seventh of the height, and the last letter is inked 1 after it.
            DisplayText2[] parts = Lines(shown);
            Assert.Equal(new[] { "I", "I", "II", "I" }, parts.Select(t => t.Text).ToArray());
            double baseline = parts[0].Strokes[0][1];
            double bar = Math.Round(baseline + 10.0 + (10.0 / 6.0), 9);
            Foot(0.0, baseline, parts[0]);
            Foot(5.0, baseline + 10.0 + (10.0 / 3.0), parts[1]);
            Foot(3.0, baseline, parts[2]);
            Foot(8.0 + (10.0 / 7.0), baseline, parts[3]);
            Assert.Equal(
                new[] { 3.0, bar, 7.0, bar },
                Assert.Single(shown.Parts.OfType<DisplayPolyline2>()).Xy.Select(v => Math.Round(v, 9)).ToArray()
            );
            Assert.Equal("II/III", shown.Text);
        }

        [Fact]
        public void ATabPutsTheInkOfTheNextWordOnTheNextStop_FourHeightsApartUnlessTheParagraphSetsItsOwn()
        {
            Foot(40.0, -10.0, Lines(Shown("I\tI"))[1]);
            Foot(25.0, -10.0, Lines(Shown("\\pxt2.5;I\tI"))[1]);
        }

        [Theory]
        [InlineData("l", 0.0)]
        [InlineData("c", 10.0)]
        [InlineData("r", 20.0)]
        public void AParagraphSetLeftCentredOrRight_PutsTheInkOfItsLineSoInTheWidth(string code, double first)
        {
            DisplayText2[] lines = Lines(Shown(@"\pxq" + code + ";II II II", 50.0));

            // One line inked 30 wide in a width of 50.
            Foot(first, -10.0, Assert.Single(lines));
        }

        [Fact]
        public void AJustifiedParagraph_SpreadsItsLinesOverTheWidthButNotItsLast_ADistributedOneEveryLine()
        {
            DisplayText2[] justified = Lines(Shown(@"\pxqj;II II II", 20.0));
            DisplayText2[] distributed = Lines(Shown(@"\pxqd;II II II", 20.0));

            // "II II" is inked 17 wide: justified, the 3 left go into its space and its last letter stands on 20; the last
            // line "II" stays as it is, inked to 4. Distributed, that line too ends on 20.
            Assert.Equal(20.0, justified.Where(t => t.Strokes[0][1] > -20.0).Max(t => t.Strokes.Max(s => s[0])), 9);
            Assert.Equal(4.0, justified.Where(t => t.Strokes[0][1] < -20.0).Max(t => t.Strokes.Max(s => s[0])), 9);
            Assert.Equal(20.0, distributed.Where(t => t.Strokes[0][1] < -20.0).Max(t => t.Strokes.Max(s => s[0])), 9);
        }

        [Fact]
        public void AParagraphsIndentAndSpacing_MoveItsFirstLineItsOtherLinesAndThePitchBetweenParagraphs()
        {
            DisplayText2[] lines = Lines(Shown(@"\pxi2,l1,a1;II II II\PI", 30.0));

            // The first line starts 3 heights in, where no word fits and the first stays whole; the others 1 height in; a
            // height more between the paragraph and the next, whose margins are the same.
            Assert.Equal(new[] { "II", "II II", "I" }, lines.Select(t => t.Text).ToArray());
            Foot(30.0, -10.0, lines[0]);
            Foot(10.0, -10.0 - (50.0 / 3.0), lines[1]);
            Foot(30.0, -10.0 - (100.0 / 3.0) - 10.0, lines[2]);
        }

        [Fact]
        public void StaticColumns_TakeTheLinesThatFitTheirHeight_EachAColumnAndAGutterOn()
        {
            DisplayText2 shown = Shown(@"I\PI\PI\PI\PI", 0.0, t =>
            {
                t.SetStaticColumns(20.0, 5.0, 2);
                t.Height = 30.0;
            });

            // A column 30 high holds the baselines at 10 and 26.67; the third line starts the second column, 25 on.
            DisplayText2[] lines = Lines(shown);
            Assert.Equal(5, lines.Length);
            Foot(0.0, -10.0, lines[0]);
            Foot(0.0, -10.0 - (50.0 / 3.0), lines[1]);
            Foot(25.0, -10.0, lines[2]);
            Foot(25.0, -10.0 - (50.0 / 3.0), lines[3]);
            Foot(25.0, -10.0 - (100.0 / 3.0), lines[4]);
        }

        [Fact]
        public void AFrame_IsDrawnHalfAHeightAroundTheWidthAndTheLines_AndAMaskUnderTheWordsByItsOwnFactor()
        {
            DisplayText2 framed = Shown("II II II", 20.0, t => t.ShowBorders = true);
            DisplayText2 masked = Shown("II II II", 20.0, t =>
            {
                t.BackgroundFill = true;
                t.BackgroundScaleFactor = 1.2;
                t.BackgroundFillColor = DxfColor.FromRgb(255, 0, 0);
            });

            // The lines are 20 wide and 10 + 50 / 3 high down to the last baseline: the frame 5 around them, the mask 2.
            DisplayPolyline2 frame = Assert.IsType<DisplayPolyline2>(framed.Parts[0]);
            Assert.True(frame.Closed);
            Assert.Equal(
                new[] { -5.0, -31.666666667, 25.0, -31.666666667, 25.0, 5.0, -5.0, 5.0 },
                frame.Xy.Select(v => Math.Round(v, 9)).ToArray()
            );

            DisplayFill2 mask = Assert.IsType<DisplayFill2>(masked.Parts[0]);
            Assert.Equal(
                new[] { -2.0, -28.666666667, 22.0, -28.666666667, 22.0, 2.0, -2.0, 2.0 },
                Assert.Single(mask.Rings).Select(v => Math.Round(v, 9)).ToArray()
            );
            Assert.Equal(0xFF0000, mask.Style.Rgb);
            Assert.True(Math.Abs(masked.Parts.OfType<DisplayText2>().First().Strokes[0][0]) < Close);
        }
    }
}
