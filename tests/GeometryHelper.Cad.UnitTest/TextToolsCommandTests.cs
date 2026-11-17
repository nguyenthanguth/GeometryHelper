using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// SCALETEXT, JUSTIFYTEXT and TXT2MTXT. The first two ask AutoCAD 2021's prompts (probed in its console: "Select objects:",
    /// "Enter a base point option for scaling [Existing/Left/Center/Middle/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR] &lt;Existing&gt;:",
    /// "Specify new model height or [Paper height/Match object/Scale factor] &lt;2.5000&gt;:", "Enter a justification option
    /// [Left/Align/Fit/Center/Middle/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR] &lt;Left&gt;:") and give AutoCAD's results: a "HELLO" 5 high
    /// scaled to 10 about its centre keeps its centre, one justified Right is held by the end of its baseline and drawn where
    /// it was. The boxes are those the texts are drawn in. TXT2MTXT is asked in its own words (the console has no such
    /// command).
    /// </summary>
    public class TextToolsCommandTests
    {
        /// <summary>A drawing of a TEXT 5 high at (10, 10) turned by <paramref name="turn"/>, held as <paramref name="justify"/>, and the handle of it.</summary>
        private static CadDocument OneText(DxfAttachmentPoint justify, double turn, out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var text = new DxfDBText
            {
                TextString = "HELLO",
                Position = new GeoPoint3(10, 10, 0),
                Height = 5,
                Rotation = turn,
            };
            database.ModelSpace.AppendEntity(text);

            if (justify != DxfAttachmentPoint.BaseLeft)
            {
                text.Justify = justify;
                text.AlignmentPoint = new GeoPoint3(10, 10, 0);
            }

            handle = text.Handle;
            return CadDocument.From(database, "text.dxf");
        }

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

        private static void SameBox(DisplayBox expected, DisplayBox actual, string what)
        {
            Assert.True(
                Math.Abs(expected.MinX - actual.MinX) < 1E-6
                    && Math.Abs(expected.MinY - actual.MinY) < 1E-6
                    && Math.Abs(expected.MaxX - actual.MaxX) < 1E-6
                    && Math.Abs(expected.MaxY - actual.MaxY) < 1E-6,
                what
                    + ": expected ("
                    + expected.MinX
                    + ", "
                    + expected.MinY
                    + ")-("
                    + expected.MaxX
                    + ", "
                    + expected.MaxY
                    + ") but was ("
                    + actual.MinX
                    + ", "
                    + actual.MinY
                    + ")-("
                    + actual.MaxX
                    + ", "
                    + actual.MaxY
                    + ")."
            );
        }

        [Fact]
        public void ScaleText_AsksInAutoCadsWords_AndKeepsTheBasePointAndTheHeightForTheNextRun()
        {
            CadDocument document = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string handle);
            CadCommandSession session = Run(Session(document), "SCALETEXT");
            Assert.Equal("Select objects:", session.PromptText);

            Run(session, new[] { handle }, Enter);
            Assert.Equal(
                "Enter a base point option for scaling [Existing/Left/Center/Middle/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR] <Existing>:",
                session.PromptText
            );

            Run(session, "C");
            Assert.StartsWith(
                "Specify new model height or [Paper height/Match object/Scale factor] <",
                session.PromptText
            );
            Run(session, "10");
            Assert.False(session.IsRunning);
            Assert.Contains("1 objects changed", session.History);

            Run(session, "SCALETEXT", new[] { handle }, Enter);
            Assert.Equal(
                "Enter a base point option for scaling [Existing/Left/Center/Middle/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR] <Center>:",
                session.PromptText
            );
            Run(session, Enter);
            Assert.StartsWith(
                "Specify new model height or [Paper height/Match object/Scale factor] <10",
                session.PromptText
            );
        }

        [Fact]
        public void ScaleText_Existing_KeepsTheFirstPoint_ScalesTheHeight_AndIsOneUndoStep()
        {
            CadDocument document = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string handle);
            string start = document.Database.ToText();

            Run(Session(document), "SCALETEXT", new[] { handle }, Enter, "E", "10");

            DxfDBText text = Entity<DxfDBText>(document, handle);
            Assert.Equal(10.0, text.Height, 9);
            Near(new GeoPoint3(10, 10, 0), text.Position);
            Assert.Equal("SCALETEXT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ScaleText_AboutTheCentre_KeepsTheCentreOfTheBox_AndAboutTheMiddleTheMiddleOfIt()
        {
            CadDocument document = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string handle);
            DisplayBox before = Box(document, handle);
            double centre = (before.MinX + before.MaxX) / 2.0;

            Run(Session(document), "SCALETEXT", new[] { handle }, Enter, "C", "10");

            DxfDBText text = Entity<DxfDBText>(document, handle);
            Assert.Equal(centre - (centre - 10.0) * 2.0, text.Position.X, 6);
            Assert.Equal(10.0, text.Position.Y, 9);

            // About the middle the baseline moves down by half the height the text gained: 12.5 stays the middle.
            CadDocument other = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string second);
            Run(Session(other), "SCALETEXT", new[] { second }, Enter, "M", "10");
            Assert.Equal(7.5, Entity<DxfDBText>(other, second).Position.Y, 6);
        }

        [Fact]
        public void ScaleText_AboutACorner_KeepsThatCorner()
        {
            CadDocument document = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string handle);
            DisplayBox before = Box(document, handle);

            Run(Session(document), "SCALETEXT", new[] { handle }, Enter, "TL", "10");

            // The top left of the box stays: x at the left edge, y at the top.
            DisplayBox after = Box(document, handle);
            Assert.Equal(before.MinX, after.MinX, 6);
            Assert.Equal(before.MaxY, after.MaxY, 6);
            Assert.Equal(before.MaxX - before.MinX, (after.MaxX - after.MinX) / 2.0, 6);
        }

        [Fact]
        public void ScaleText_ByAScaleFactor_AndByReference_AndByMatchingATextObject()
        {
            CadDocument document = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string handle);
            CadCommandSession session = Run(Session(document), "SCALETEXT", new[] { handle }, Enter, "E", "S", "3");
            Assert.Equal(15.0, Entity<DxfDBText>(document, handle).Height, 9);

            // The factor is the default of the next run in the session.
            Run(session, "SCALETEXT", new[] { handle }, Enter, "E", "S");
            Assert.StartsWith("Specify scale factor or [Reference] <3", session.PromptText);
            Run(session, "R", "2", "1");
            Assert.Equal(7.5, Entity<DxfDBText>(document, handle).Height, 9);

            CadDocument two = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(0, 0, 5, "ALPHA", handle: "B1"),
                    TestDrawings.Text(0, 20, 8, "BETA", handle: "B2")
                ),
                "two.dxf"
            );
            session = Run(Session(two), "SCALETEXT", new[] { "B1" }, Enter, "E", "M");
            Assert.Equal("Select a text object with the desired height:", session.PromptText);
            Run(session, (object)new[] { "B2" });
            Assert.Equal(8.0, Entity<DxfDBText>(two, "B1").Height, 9);
        }

        [Fact]
        public void ScaleText_GivesEachTextTheSameHeight_EachAboutItsOwnBasePoint_AndIgnoresWhatIsNotText()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(0, 0, 5, "ALPHA", handle: "B1"),
                    TestDrawings.Text(30, 20, 2, "BETA", handle: "B2"),
                    TestDrawings.Line(0, 80, 10, 80, handle: "B3")
                ),
                "mixed.dxf"
            );

            CadCommandSession session = Run(
                Session(document),
                "SCALETEXT",
                new[] { "B1", "B2", "B3" },
                Enter,
                "E",
                "4"
            );

            Assert.Contains("1 was filtered out.", session.History);
            Assert.Contains("2 objects changed", session.History);
            Assert.Equal(4.0, Entity<DxfDBText>(document, "B1").Height, 9);
            Assert.Equal(4.0, Entity<DxfDBText>(document, "B2").Height, 9);
            Near(new GeoPoint3(30, 20, 0), Entity<DxfDBText>(document, "B2").Position);
        }

        [Fact]
        public void ScaleText_PaperHeight_LeavesTextThatIsNotAnnotativeAlone_AndSaysSo()
        {
            CadDocument document = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string handle);
            string start = document.Database.ToText();
            CadCommandSession session = Run(Session(document), "SCALETEXT", new[] { handle }, Enter, "E", "P");
            Assert.StartsWith("Specify new paper height <", session.PromptText);
            Run(session, "6");

            Assert.Contains("1 non-annotative objects ignored", session.History);
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ScaleText_OfAnMText_ScalesItsHeightAndItsWidthAboutItsAttachmentPoint()
        {
            var database = new DxfDatabase("AC1027");
            var words = new DxfMText
            {
                Location = new GeoPoint3(100, 100, 0),
                TextHeight = 5,
                Width = 50,
                Contents = "Line one",
            };
            database.ModelSpace.AppendEntity(words);
            CadDocument document = CadDocument.From(database, "mtext.dxf");

            Run(Session(document), "SCALETEXT", new[] { words.Handle }, Enter, "E", "10");

            var scaled = Entity<DxfMText>(document, words.Handle);
            Assert.Equal(10.0, scaled.TextHeight, 9);
            Assert.Equal(100.0, scaled.Width, 9);
            Near(new GeoPoint3(100, 100, 0), scaled.Location);
        }

        [Theory]
        [InlineData("Left")]
        [InlineData("Center")]
        [InlineData("Middle")]
        [InlineData("Right")]
        [InlineData("TL")]
        [InlineData("TC")]
        [InlineData("TR")]
        [InlineData("ML")]
        [InlineData("MC")]
        [InlineData("MR")]
        [InlineData("BL")]
        [InlineData("BC")]
        [InlineData("BR")]
        public void JustifyText_KeepsATextWhereItIsDrawn_ForEveryJustification_ThoughTurned(string option)
        {
            foreach (
                DxfAttachmentPoint from in new[]
                {
                    DxfAttachmentPoint.BaseLeft,
                    DxfAttachmentPoint.MiddleCenter,
                    DxfAttachmentPoint.BottomRight,
                }
            )
            {
                CadDocument document = OneText(from, 0.5, out string handle);
                DisplayBox before = Box(document, handle);

                Run(Session(document), "JUSTIFYTEXT", new[] { handle }, Enter, option);

                SameBox(before, Box(document, handle), option + " from " + from);
            }
        }

        [Fact]
        public void JustifyText_Right_HoldsTheTextByTheEndOfItsBaseline_AsAutoCadDoes()
        {
            CadDocument document = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string handle);
            DisplayBox before = Box(document, handle);

            CadCommandSession session = Run(Session(document), "JUSTIFYTEXT", new[] { handle }, Enter);
            Assert.Equal(
                "Enter a justification option [Left/Align/Fit/Center/Middle/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR] <Left>:",
                session.PromptText
            );
            Run(session, "R");

            DxfDBText text = Entity<DxfDBText>(document, handle);
            Assert.Equal(DxfAttachmentPoint.BaseRight, text.Justify);
            Assert.Equal(10.0, text.AlignmentPoint.Y, 9);
            Assert.Equal(before.MaxX, text.AlignmentPoint.X, 6);
            Near(new GeoPoint3(10, 10, 0), text.Position);
            Assert.Equal("JUSTIFYTEXT", document.NextUndo.Name);
        }

        [Fact]
        public void JustifyText_Left_PutsTheFirstPointWhereTheWordsStart()
        {
            CadDocument document = OneText(DxfAttachmentPoint.MiddleCenter, 0.0, out string handle);
            DisplayBox before = Box(document, handle);

            Run(Session(document), "JUSTIFYTEXT", new[] { handle }, Enter, "L");

            DxfDBText text = Entity<DxfDBText>(document, handle);
            Assert.Equal(DxfAttachmentPoint.BaseLeft, text.Justify);
            Assert.Equal(before.MinX, text.Position.X, 6);
            SameBox(before, Box(document, handle), "Left");
        }

        [Fact]
        public void JustifyText_AlignAndFit_HoldTheTextByBothEndsOfItsBaseline()
        {
            CadDocument document = OneText(DxfAttachmentPoint.BaseLeft, 0.0, out string handle);
            DisplayBox before = Box(document, handle);

            Run(Session(document), "JUSTIFYTEXT", new[] { handle }, Enter, "A");

            DxfDBText text = Entity<DxfDBText>(document, handle);
            Assert.Equal(DxfAttachmentPoint.BaseAlign, text.Justify);
            Near(new GeoPoint3(10, 10, 0), text.Position);
            Assert.Equal(before.MaxX, text.AlignmentPoint.X, 6);

            Run(Session(document), "JUSTIFYTEXT", new[] { handle }, Enter, "F");
            Assert.Equal(DxfAttachmentPoint.BaseFit, Entity<DxfDBText>(document, handle).Justify);
        }

        [Theory]
        [InlineData("TL", DxfAttachmentPoint.TopLeft)]
        [InlineData("BR", DxfAttachmentPoint.BottomRight)]
        [InlineData("MC", DxfAttachmentPoint.MiddleCenter)]
        [InlineData("Left", DxfAttachmentPoint.BottomLeft)]
        [InlineData("Center", DxfAttachmentPoint.BottomCenter)]
        [InlineData("Middle", DxfAttachmentPoint.MiddleCenter)]
        [InlineData("Right", DxfAttachmentPoint.BottomRight)]
        public void JustifyText_AttachesAnMTextByTheMatchingCorner_AndKeepsItDrawnWhereItIs(
            string option,
            DxfAttachmentPoint expected
        )
        {
            var database = new DxfDatabase("AC1027");
            var words = new DxfMText
            {
                Location = new GeoPoint3(100, 100, 0),
                TextHeight = 5,
                Contents = "Line one",
                Rotation = 0.4,
            };
            database.ModelSpace.AppendEntity(words);
            CadDocument document = CadDocument.From(database, "mtext.dxf");
            DisplayBox before = Box(document, words.Handle);

            Run(Session(document), "JUSTIFYTEXT", new[] { words.Handle }, Enter, option);

            Assert.Equal(expected, Entity<DxfMText>(document, words.Handle).Attachment);
            SameBox(before, Box(document, words.Handle), option);
        }

        [Fact]
        public void TextToMText_CombinesTheTextsFromTheTopDown_AsParagraphsOfOneMText_AndErasesThem()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(10, 10, 2.5, "last", handle: "B1"),
                    TestDrawings.Text(10, 30, 2.5, "first", handle: "B2"),
                    TestDrawings.Text(10, 20, 2.5, "mid {x}", handle: "B3")
                ),
                "texts.dxf"
            );

            CadCommandSession session = Run(Session(document), "TXT2MTXT");
            Assert.Equal("Select objects or [Settings]:", session.PromptText);
            Run(session, new[] { "B1", "B2", "B3" }, Enter);

            Assert.False(session.IsRunning);
            Assert.Contains("3 text objects combined into 1 MTEXT.", session.History);
            Assert.DoesNotContain(All<DxfDBText>(document.ModelSpace), t => !t.IsErased);
            DxfMText combined = Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.Equal("first\\Pmid \\{x\\}\\Plast", combined.Contents);
            Assert.Equal(2.5, combined.TextHeight, 9);
            Assert.Equal(DxfAttachmentPoint.TopLeft, combined.Attachment);
            Assert.Equal(10.0, combined.Location.X, 6);
            Assert.True(
                combined.Location.Y > 30.0 && combined.Location.Y < 34.0,
                "The top of the top text, " + combined.Location.Y
            );
            Assert.Equal("TXT2MTXT", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.DoesNotContain(All<DxfMText>(document.ModelSpace), t => !t.IsErased);
            Assert.Equal(3, All<DxfDBText>(document.ModelSpace).Count(t => !t.IsErased));
        }

        [Fact]
        public void TextToMText_Settings_RunTheTextsTogetherKeepTheOriginalsAndOrderAsSelected_AndAreKept()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(10, 10, 2.5, "one", handle: "B1"),
                    TestDrawings.Text(10, 30, 2.5, "two", handle: "B2")
                ),
                "texts.dxf"
            );

            CadCommandSession session = Run(Session(document), "TXT2MTXT", "S");
            Assert.Equal("Specify the width of the MTEXT, 0 for no limit <0.0000>:", session.PromptText);
            Run(session, "40");
            Assert.Equal("Keep each text as a paragraph of its own? [Yes/No] <Yes>:", session.PromptText);
            Run(session, "N");
            Assert.Equal("Erase the original texts? [Yes/No] <Yes>:", session.PromptText);
            Run(session, "N");
            Assert.Equal(
                "Order the texts by position or as selected? [Position/Selection] <Position>:",
                session.PromptText
            );
            Run(session, "S");
            Assert.Equal("Select objects or [Settings]:", session.PromptText);
            Run(session, new[] { "B1", "B2" }, Enter);

            Assert.Equal(2, All<DxfDBText>(document.ModelSpace).Count(t => !t.IsErased));
            DxfMText combined = Assert.Single(All<DxfMText>(document.ModelSpace));
            Assert.Equal("one two", combined.Contents);
            Assert.Equal(40.0, combined.Width, 9);
        }
    }
}
