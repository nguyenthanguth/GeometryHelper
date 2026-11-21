using System;
using System.Collections.Generic;
using GeometryHelper.Cad.Dcl;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The vertical rule of the layout (L5 part F, item 1): the room above and below each kind of tile and what a spacer takes
    /// between two of them, fitted to the windows AutoCAD 2021 drew for E1's and FDVAL's dialogs (g1 and g3). The dialogs here
    /// are written for the test in the shape of those two routines; a window's coordinates are the layout's plus 9 across (the
    /// frame and the body's padding) and 30 down (the frame and the title bar), and its size the body's plus 16 and 37. Rows
    /// marked <c>AutoCAD 2021 (GUI g1)</c> or <c>(GUI g3)</c> are measured rectangles; <c>ours: F1</c> is the rule fitted to them.
    /// </summary>
    public class DclLayoutVerticalTests
    {
        private const string FieldsLabel = "Choose the fields to filter ...";

        // The widths the window's font gave in g1 and g3 (the label of FDVAL's text is AutoCAD's own 219 wide).
        private static DclMetrics Window() =>
            new DclMetrics
            {
                ButtonPaddingX = 19,
                LabelGap = 7,
                BoxBorder = 5,
                TextWidth = text =>
                    text == "Text:" ? 24
                    : text == "  OK  " ? 30
                    : text == "Cancel" ? 36
                    : text == FieldsLabel ? 219
                    : 6 * text.Length,
            };

        private static DclTile Dialog(string inner) =>
            DclParser.Parse("d : dialog {\n" + inner + "\n}", @"C:\dcl\vertical.dcl", null).NewDialog("d");

        private static DclTile EditDialog() =>
            Dialog(
                ": edit_box { key = \"t\"; label = \"Text:\"; edit_width = 60; allow_accept = true; }\n"
                    + "spacer_1; ok_cancel;"
            );

        private static DclTile FieldsDialog() =>
            Dialog(
                ": text { label = \""
                    + FieldsLabel
                    + "\"; }\nspacer;\n"
                    + ": list_box { key = \"lst\"; multiple_select = true; }\nspacer; ok_cancel;"
            );

        private static void AssertWindow(double x, double y, double width, double height, DclRect rect)
        {
            // positions are floored to whole pixels, as the window places its controls; sizes are as laid out
            Assert.Equal(x, Math.Floor(rect.X + 9), 6);
            Assert.Equal(y, Math.Floor(rect.Y + 30), 6);
            Assert.Equal(width, rect.Width, 6);
            Assert.Equal(height, rect.Height, 6);
        }

        private static IReadOnlyDictionary<DclTile, DclRect> Arrange(DclTile dialog) =>
            DclLayout.Arrange(dialog, Window());

        [Fact]
        public void TheEditDialog_IsTheWindowOfAutoCad_FourHundredThirtyThreeByOneHundredNine()
        {
            // AutoCAD 2021 (GUI g1, e1.png): the window is 433 x 109, its body 417 x 72
            DclTile dialog = EditDialog();

            DclRect body = Arrange(dialog)[dialog];

            Assert.Equal(417, body.Width, 6);
            Assert.Equal(72, body.Height, 6);
            Assert.Equal(433, body.Width + 16, 6);
            Assert.Equal(109, body.Height + 37, 6);
        }

        [Fact]
        public void TheEditDialogsControls_AreAtTheRectanglesOfAutoCad()
        {
            // AutoCAD 2021 (GUI g1): the box 47,37 372 x 19 after the label "Text:" at 16 (31 wide), OK 151,73 49 x 22 and
            // Cancel 228,73 55 x 22
            DclTile dialog = EditDialog();

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            DclRect tile = placed[dialog.Find("t")];
            double label = DclLayout.LabelWidth(dialog.Find("t"), Window());
            Assert.Equal(31, label, 6);
            AssertWindow(16, 37, 403, 19, tile);
            Assert.Equal(47, Math.Floor(tile.X + 9 + label), 6);
            Assert.Equal(372, tile.Width - label, 6);
            AssertWindow(151, 73, 49, 22, placed[dialog.Find("accept")]);
            AssertWindow(228, 73, 55, 22, placed[dialog.Find("cancel")]);
        }

        [Fact]
        public void TheFieldsDialog_IsTheWindowOfAutoCad_TwoHundredFortyNineByTwoHundredTwentySeven()
        {
            // AutoCAD 2021 (GUI g3, fdval.png): the window is 249 x 227, its body 233 x 190
            DclTile dialog = FieldsDialog();

            DclRect body = Arrange(dialog)[dialog];

            Assert.Equal(233, body.Width, 6);
            Assert.Equal(190, body.Height, 6);
            Assert.Equal(249, body.Width + 16, 6);
            Assert.Equal(227, body.Height + 37, 6);
        }

        [Fact]
        public void TheFieldsDialogsControls_AreAtTheRectanglesOfAutoCad()
        {
            // AutoCAD 2021 (GUI g1 at 14:48:08, g3): the text 16,35 219 x 13, the list 16,59 219 wide (its control 119 high, a
            // line short of the tile's 120), OK 59,191 and Cancel 136,191
            DclTile dialog = FieldsDialog();

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertWindow(16, 35, 219, 13, placed[dialog.Children[0]]);
            AssertWindow(16, 59, 219, 120, placed[dialog.Find("lst")]);
            AssertWindow(59, 191, 49, 22, placed[dialog.Find("accept")]);
            AssertWindow(136, 191, 55, 22, placed[dialog.Find("cancel")]);
        }

        [Fact]
        public void ASpacerOfOneLine_IsNineHighBetweenItsNeighbours_AndABareOneFour()
        {
            // ours: F1 (fitted to E1, where spacer_1 stands 17 between the box and OK with the margins of 4 either side, and to
            // FDVAL, whose two bare spacers stand 11 and 12 between the text, the list and OK)
            DclTile one = Dialog(
                ": spacer { key = \"a\"; height = 1; } : spacer { key = \"b\"; } : spacer_0 { key = \"c\"; }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(one);

            Assert.Equal(9, placed[one.Find("a")].Height, 6);
            Assert.Equal(4, placed[one.Find("b")].Height, 6);
            Assert.Equal(0, placed[one.Find("c")].Height, 6);
            Assert.Equal(3 + 9 + 4 + 0 + 3, placed[one].Height, 6);
        }

        [Fact]
        public void ASpacer_HasNoMarginAboveOrBelow_ButStillOneAtItsSides()
        {
            // ours: F1 (the vertical room of a spacer is its own height; the sideways 4 stay, so OK and Cancel are 28 apart)
            DclTile dialog = Dialog(": spacer { key = \"s\"; width = 2; height = 1; }");

            DclRect spacer = Arrange(dialog)[dialog.Find("s")];

            Assert.Equal(3 + 4, spacer.X, 6);
            Assert.Equal(3, spacer.Y, 6);
            Assert.Equal(12, spacer.Width, 6);
            Assert.Equal(9, spacer.Height, 6);
        }

        [Fact]
        public void ATextStandsTwoFromTheTopOfItsSlot_AndSixFromTheBottom()
        {
            // AutoCAD 2021 (GUI g3): the first text of FDVAL is at 35, 2 above where an edit box starts (37), and two texts one
            // over the other are 21 apart (g1: 296, 317, 338)
            DclTile dialog = Dialog(": text { key = \"a\"; label = \"A\"; } : text { key = \"b\"; label = \"B\"; }");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(3 + 2, placed[dialog.Find("a")].Y, 6);
            Assert.Equal(21, placed[dialog.Find("b")].Y - placed[dialog.Find("a")].Y, 6);
            Assert.Equal(3 + 21 + 21 + 3, placed[dialog].Height, 6);
        }

        [Fact]
        public void TheLeftColumnOfTheEveryKindDialog_StandsWhereAutoCadPutIt()
        {
            // AutoCAD 2021 (GUI g1, all.png): the tops of the controls of its left column, edit 37, edit 64, popup 91, popup 120,
            // toggle 146, toggle 169, radio box 192, slider 245, slider 270, text 296, 317, 338, and the radio box 48 high
            DclTile dialog = Dialog(
                ": edit_box { key = \"a\"; edit_width = 5; } : edit_box { key = \"b\"; edit_width = 5; }"
                    + ": popup_list { key = \"c\"; edit_width = 5; } : popup_list { key = \"d\"; edit_width = 5; }"
                    + ": toggle { key = \"e\"; label = \"E\"; } : toggle { key = \"f\"; label = \"F\"; }"
                    + ": boxed_radio_row { key = \"g\"; : radio_button { key = \"r\"; label = \"R\"; } }"
                    + ": slider { key = \"h\"; } : slider { key = \"i\"; }"
                    + ": text { key = \"j\"; label = \"J\"; } : text { key = \"k\"; label = \"K\"; } : text { key = \"l\"; label = \"L\"; }"
            );
            string[] keys = { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l" };
            int[] tops = { 37, 64, 91, 120, 146, 169, 192, 245, 270, 296, 317, 338 };

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            for (int i = 0; i < keys.Length; i++)
            {
                Assert.Equal(tops[i], Math.Floor(placed[dialog.Find(keys[i])].Y + 30), 6);
            }

            Assert.Equal(48, placed[dialog.Find("g")].Height, 6);
            Assert.Equal(215, Math.Floor(placed[dialog.Find("r")].Y + 30), 6);
        }

        [Fact]
        public void ABoxedClusterAtTheTop_StartsAtThirtyFour_AndItsListFiftyFour()
        {
            // AutoCAD 2021 (GUI g1, all.png): the boxed column starts at 34, three under the dialog's margin, and the label of
            // its list at 55; here the list is at 54 (a pixel high, the list's own top of 1 fitted to FDVAL)
            DclTile dialog = Dialog(
                ": boxed_column { key = \"b\"; label = \"Box\"; : list_box { key = \"l\"; width = 10; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(34, placed[dialog.Find("b")].Y + 30, 6);
            Assert.Equal(54, placed[dialog.Find("l")].Y + 30, 6);
        }

        [Fact]
        public void TheErrorTileOfAnExitRow_StandsTwentyEightBelowOk()
        {
            // AutoCAD 2021 (GUI g1, all.png): OK at 383 and the error tile under the buttons at 411
            DclTile dialog = Dialog("ok_cancel_help_errtile;");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(28, placed[dialog.Find("error")].Y - placed[dialog.Find("accept")].Y, 6);
        }

        [Fact]
        public void TheExitRowAfterAListWithNoSpacer_StandsEightBelowIt()
        {
            // ours: D345 and F1 (the buttons keep their margins of 4 and a list its 4 below: the rule moves only what a spacer or
            // a text adds)
            DclTile dialog = Dialog(": list_box { key = \"l\"; height = 3; } ok_cancel;");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            DclRect list = placed[dialog.Find("l")];
            Assert.Equal(8, placed[dialog.Find("accept")].Y - (list.Y + list.Height), 6);
        }
    }
}
