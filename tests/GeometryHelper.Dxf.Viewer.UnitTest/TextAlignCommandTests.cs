using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// TEXTALIGN (TA): texts aligned to another text or to a line, as AutoCAD 2021's (probed in its console: "Current
    /// settings: Align = Left, Spacing Mode = Current Vertical", "Select text objects to align [alIgnment/Options]:", "Select
    /// alignment direction [Left/Center/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR] &lt;Left&gt;:", "Enter an option [Distribute/Set
    /// spacing/current Vertical/current Horizontal] &lt;current Vertical&gt;:", "Set spacing &lt;0.000000&gt;:", "Select text
    /// object to align to [Point]:", "Pick first point", "Pick second point or [Options]:"). The boxes are those the texts are
    /// drawn in.
    /// </summary>
    public class TextAlignCommandTests
    {
        /// <summary>Three texts 5 high: ALPHA at (0, 0), BETA at (10, 20) and GAMMA at (30, 50), and a line B4.</summary>
        private static CadDocument Texts() =>
            CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(0, 0, 5, "ALPHA", handle: "B1"),
                    TestDrawings.Text(10, 20, 5, "BETA", handle: "B2"),
                    TestDrawings.Text(30, 50, 5, "GAMMA", handle: "B3"),
                    TestDrawings.Line(0, 80, 10, 80, handle: "B4")
                ),
                "texts.dxf"
            );

        private static readonly string[] All3 = { "B1", "B2", "B3" };

        /// <summary>The box a text is drawn in.</summary>
        private static DisplayBox Box(CadDocument document, string handle)
        {
            DisplayBox box = DisplayBox.Empty;

            foreach (DisplayItem2 item in document.GetDisplay(document.ModelSpace).Items)
            {
                if (item.Handle == handle)
                {
                    box = box.Union(item.Bounds);
                }
            }

            return box;
        }

        [Fact]
        public void TheCommandAsksInAutoCadsWords_AndKeepsItsSettings()
        {
            CadDocument document = Texts();
            CadCommandSession session = Run(Session(document), "TEXTALIGN");

            Assert.Contains("Current settings: Align = Left, Spacing Mode = Current Vertical", session.History);
            Assert.Equal("Select text objects to align [alIgnment/Options]:", session.PromptText);

            Run(session, "I");
            Assert.Equal(
                "Select alignment direction [Left/Center/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR] <Left>:",
                session.PromptText
            );
            Run(session, "TR");
            Assert.Equal("Select text objects to align [alIgnment/Options]:", session.PromptText);
            Assert.Contains("Current settings: Align = TR, Spacing Mode = Current Vertical", session.History);

            Run(session, "O");
            Assert.Equal(
                "Enter an option [Distribute/Set spacing/current Vertical/current Horizontal] <current Vertical>:",
                session.PromptText
            );
            Run(session, "S");
            Assert.Equal("Set spacing <0.000000>:", session.PromptText);
            Run(session, "5");
            Assert.Contains("Current settings: Align = TR, Spacing Mode = Set spacing (5.000000)", session.History);

            Run(session, "O", "D");
            Assert.Contains("Current settings: Align = TR, Spacing Mode = Distribute evenly", session.History);
            Run(session, Enter);
            Assert.False(session.IsRunning);

            // The settings are kept for the next run, as AutoCAD keeps them in its variables.
            Run(session, "TA");
            Assert.Equal(
                "Current settings: Align = TR, Spacing Mode = Distribute evenly",
                session.History.Last(h => h.StartsWith("Current settings"))
            );
            Assert.Equal(
                2,
                session.History.Count(h => h == "Current settings: Align = TR, Spacing Mode = Distribute evenly")
            );
        }

        [Fact]
        public void AfterTheObjects_TheCommandAsksForTheTextToAlignTo_AndPointIsAKeyword()
        {
            CadCommandSession session = Run(Session(Texts()), "TEXTALIGN", new[] { "B1", "B2" }, Enter);

            Assert.Equal("Select text object to align to [Point]:", session.PromptText);

            Run(session, "P");
            Assert.Equal("Pick first point:", session.PromptText);
            Run(session, P(0, 100));
            Assert.Contains("Spacing mode: Current Vertical", session.History);
            Assert.Equal("Pick second point or [Options]:", session.PromptText);
        }

        [Fact]
        public void AlignLeft_PutsTheLeftEdgesOnTheLeftEdgeOfTheTextPicked_AndKeepsTheirHeights_InOneStep()
        {
            CadDocument document = Texts();
            DisplayBox first = Box(document, "B1");
            double height2 = Box(document, "B2").MinY;
            double height3 = Box(document, "B3").MinY;

            Run(Session(document), "TA", All3, Enter, new[] { "B1" });

            Assert.Equal(first.MinX, Box(document, "B2").MinX, 6);
            Assert.Equal(first.MinX, Box(document, "B3").MinX, 6);
            Assert.Equal(first.MinX, Box(document, "B1").MinX, 6);
            Assert.Equal(height2, Box(document, "B2").MinY, 6);
            Assert.Equal(height3, Box(document, "B3").MinY, 6);
            Assert.Equal("TEXTALIGN", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(10.0, Entity<DxfDBText>(document, "B2").Position.X, 9);
            Assert.Equal(30.0, Entity<DxfDBText>(document, "B3").Position.X, 9);
        }

        [Fact]
        public void AlignRightAndCenter_PutTheirEdgesAndMiddlesOnTheTextPickeds()
        {
            CadDocument document = Texts();
            CadCommandSession session = Run(Session(document), "TA", "I", "Right", All3, Enter, new[] { "B3" });
            Assert.False(session.IsRunning);

            double right = Box(document, "B3").MaxX;
            Assert.Equal(right, Box(document, "B1").MaxX, 6);
            Assert.Equal(right, Box(document, "B2").MaxX, 6);

            Run(Session(document), "TA", "I", "Center", All3, Enter, new[] { "B2" });

            double middle = (Box(document, "B2").MinX + Box(document, "B2").MaxX) / 2.0;
            Assert.Equal(middle, (Box(document, "B1").MinX + Box(document, "B1").MaxX) / 2.0, 6);
            Assert.Equal(middle, (Box(document, "B3").MinX + Box(document, "B3").MaxX) / 2.0, 6);
        }

        [Fact]
        public void CurrentHorizontal_KeepsTheirPlacesAlongX_AndPutsTheirMiddlesOnTheMiddleOfTheTextPicked()
        {
            CadDocument document = Texts();
            double x1 = Box(document, "B1").MinX;
            double x3 = Box(document, "B3").MinX;

            Run(Session(document), "TA", "O", "H", "I", "MC", All3, Enter, new[] { "B2" });

            double middle = (Box(document, "B2").MinY + Box(document, "B2").MaxY) / 2.0;
            Assert.Equal(middle, (Box(document, "B1").MinY + Box(document, "B1").MaxY) / 2.0, 6);
            Assert.Equal(middle, (Box(document, "B3").MinY + Box(document, "B3").MaxY) / 2.0, 6);
            Assert.Equal(x1, Box(document, "B1").MinX, 6);
            Assert.Equal(x3, Box(document, "B3").MinX, 6);
        }

        [Fact]
        public void SetSpacing_StacksTheTextsAboveAndBelowTheTextPicked_ThatStaysWhereItIs()
        {
            CadDocument document = Texts();
            DisplayBox middle = Box(document, "B2");

            Run(Session(document), "TA", "O", "S", "3", All3, Enter, new[] { "B2" });

            Assert.Equal(middle.MinY, Box(document, "B2").MinY, 6);
            Assert.Equal(middle.MaxY + 3.0, Box(document, "B3").MinY, 6);
            Assert.Equal(middle.MinY - 3.0, Box(document, "B1").MaxY, 6);
            Assert.Equal(middle.MinX, Box(document, "B3").MinX, 6);
            Assert.Equal(middle.MinX, Box(document, "B1").MinX, 6);
        }

        [Fact]
        public void Distribute_SpacesTheTextsBetweenTheTopAndTheBottomOnesEvenly_AndAlignsThem()
        {
            CadDocument document = Texts();
            DisplayBox top = Box(document, "B3");
            DisplayBox bottom = Box(document, "B1");

            Run(Session(document), "TA", "O", "D", All3, Enter, new[] { "B1" });

            Assert.Equal(top.MinY, Box(document, "B3").MinY, 6);
            Assert.Equal(bottom.MinY, Box(document, "B1").MinY, 6);
            double upper = Box(document, "B3").MinY - Box(document, "B2").MaxY;
            double lower = Box(document, "B2").MinY - Box(document, "B1").MaxY;
            Assert.Equal(upper, lower, 6);
            Assert.Equal(bottom.MinX, Box(document, "B2").MinX, 6);
            Assert.Equal(bottom.MinX, Box(document, "B3").MinX, 6);
        }

        [Fact]
        public void APointLine_TakesTheTopLeftCornersOfTheTextsOntoIt_KeepingTheirHeights()
        {
            // AutoCAD 2021 (probed in its console, three TEXT 2.5 high at (10, 10), (60, 25) and (30, 50), the line through
            // (0, 0) and (100, 50) given as the points of Point): under Align = Left the top left corner of each box is on
            // the line, as under TL (the left edge of AAA at -45 where the line is at 12.5), not its middle.
            CadDocument document = Texts();
            double y3 = Box(document, "B3").MinY;

            // The line y = x + 100, through (0, 100) and (50, 150).
            Run(Session(document), "TA", All3, Enter, "P", P(0, 100), P(50, 150));

            foreach (string handle in All3)
            {
                DisplayBox box = Box(document, handle);
                Assert.Equal(box.MaxY - 100.0, box.MinX, 6);
            }

            Assert.Equal(y3, Box(document, "B3").MinY, 6);
            Assert.Equal("TEXTALIGN", document.NextUndo.Name);
        }

        [Fact]
        public void APointLine_UnderRight_TakesTheTopRightCorners_AndUnderCenter_TheMiddles()
        {
            // AutoCAD 2021, the same texts and a line of slope 1/2 through (30, 50): Right puts the top right corner of AAA at
            // (-45, 12.5), on the line, the middles of Center are on it, as MC.
            CadDocument document = Texts();
            Run(Session(document), "TA", "I", "Right", All3, Enter, "P", P(0, 100), P(50, 150));

            foreach (string handle in All3)
            {
                DisplayBox box = Box(document, handle);
                Assert.Equal(box.MaxY - 100.0, box.MaxX, 6);
            }

            Assert.True(document.Undo());
            Run(Session(document), "TA", "I", "Center", All3, Enter, "P", P(0, 100), P(50, 150));

            foreach (string handle in All3)
            {
                DisplayBox box = Box(document, handle);
                Assert.Equal((box.MinY + box.MaxY) / 2.0 - 100.0, (box.MinX + box.MaxX) / 2.0, 6);
            }
        }

        [Fact]
        public void ALineNearHorizontal_UnderCurrentVertical_IsDiscarded_WithAutoCadsWords()
        {
            CadDocument document = Texts();

            CadCommandSession session = Run(Session(document), "TA", All3, Enter, "P", P(0, 100), P(50, 100));

            Assert.Contains(
                "The base line is near horizontal or vertical, aligned texts may be out of available area, alignment is discarded.",
                session.History
            );
            Assert.False(document.CanUndo);
            Assert.Equal(10.0, Entity<DxfDBText>(document, "B2").Position.X, 9);
        }

        [Fact]
        public void ObjectsThatAreNotTexts_AreFilteredOut_AndTheCommandEndsWhereNoneIsLeft()
        {
            CadDocument document = Texts();

            CadCommandSession session = Run(Session(document), "TA", new[] { "B1", "B4" }, Enter);
            Assert.Contains("1 was filtered out.", session.History);
            Assert.Equal("Select text object to align to [Point]:", session.PromptText);

            CadCommandSession none = Run(Session(document), "TA", new[] { "B4" }, Enter);
            Assert.Contains("1 was filtered out.", none.History);
            Assert.False(none.IsRunning);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ATextToAlignToThatIsNotAText_IsRefused_AndAskedAgain()
        {
            CadDocument document = Texts();

            CadCommandSession session = Run(Session(document), "TA", All3, Enter, new[] { "B4" });

            Assert.Contains("Selected object is not a text object.", session.History);
            Assert.Equal("Select text object to align to [Point]:", session.PromptText);
            Assert.False(document.CanUndo);
        }
    }
}
