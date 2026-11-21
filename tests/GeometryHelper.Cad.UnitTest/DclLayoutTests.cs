using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Dcl;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The layout of a dialog's tiles (L5 part P, D310-D313) with the headless numbers of D337 and D345: a character 6 by 13,
    /// a button 22 high with 8 of padding each side of its label (at least its <c>width</c> times 6 plus 1), an edit box 19,
    /// a popup list 21, a toggle 15, a radio button 19, a slider 17, a list line of 13, a label gap of 0, a tile's margin of 4
    /// on every side (a spacer's too), a dialog margin of 3, a boxed cluster's border of 8. Rows marked <c>AutoCAD 2021 (GUI
    /// g1)</c> are measured in the full AutoCAD (sizes and the gaps between tiles); the margins and the boxed cluster are
    /// fitted to the differences of the window coordinates and the rest is the spec's choice (<c>ours: D311</c>,
    /// <c>ours: D312</c>).
    /// </summary>
    public class DclLayoutTests
    {
        private static DclTile Dialog(string inner, string head = "") =>
            DclParser.Parse(head + "\nd : dialog {\n" + inner + "\n}", @"C:\dcl\layout.dcl", null).NewDialog("d");

        private static IReadOnlyDictionary<DclTile, DclRect> Arrange(DclTile dialog, DclMetrics metrics = null) =>
            DclLayout.Arrange(dialog, metrics ?? DclMetrics.Default);

        private static void AssertRect(double x, double y, double width, double height, DclRect actual)
        {
            Assert.Equal(x, actual.X, 6);
            Assert.Equal(y, actual.Y, 6);
            Assert.Equal(width, actual.Width, 6);
            Assert.Equal(height, actual.Height, 6);
        }

        private static void AssertSize(double width, double height, DclRect actual)
        {
            Assert.Equal(width, actual.Width, 6);
            Assert.Equal(height, actual.Height, 6);
        }

        private static DclRect Of(IReadOnlyDictionary<DclTile, DclRect> placed, DclTile dialog, string key) =>
            placed[dialog.Find(key)];

        /// <summary>The size of the one tile with the key "t" in a dialog of just that tile, margins aside.</summary>
        private static DclRect Alone(string tile)
        {
            DclTile dialog = Dialog(tile);
            return Of(Arrange(dialog), dialog, "t");
        }

        // ---- Natural sizes (D311) ----

        [Fact]
        public void ADialogOfOneText_IsTheTextPlusItsMarginsAndTheDialogsMargin()
        {
            // ours: D311 (a text is its label's width by one line), D312 (a margin of 4 on each side, the dialog's 3)
            DclTile dialog = Dialog(": text { key = \"t\"; label = \"Hello\"; }");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(0, 0, 44, 27, placed[dialog]);
            AssertRect(7, 7, 30, 13, placed[dialog.Find("t")]);
        }

        [Fact]
        public void ATextShowsItsValueRatherThanItsLabel_WhenItHasOne()
        {
            // ours: D318 (a text shows its value once set, else its label)
            Assert.Equal(18, Alone(": text { key = \"t\"; value = \"abc\"; label = \"a longer label\"; }").Width);
            Assert.Equal(12, Alone(": text { key = \"t\"; value = \"\"; label = \"xy\"; }").Width);
            Assert.Equal(0, Alone(": text { key = \"t\"; }").Width);
        }

        [Fact]
        public void WidthAndHeight_AreMinimums()
        {
            // ours: D311
            DclRect text = Alone(": text { key = \"t\"; label = \"Hello\"; width = 20; height = 3; }");
            DclRect smaller = Alone(": text { key = \"t\"; label = \"Hello\"; width = 2; height = 1; }");

            Assert.Equal(120, text.Width);
            Assert.Equal(39, text.Height);
            Assert.Equal(30, smaller.Width);
            Assert.Equal(13, smaller.Height);
        }

        [Fact]
        public void AButton_IsItsLabelPlusPaddingAndAtLeastItsWidthTimesSixPlusOne()
        {
            // AutoCAD 2021 (GUI g1): a button is 22 high, its label plus 16 wide and at least width x 6 + 1 (D345, D346)
            Assert.Equal(46, Alone(": button { key = \"t\"; label = \"Apply\"; }").Width);
            Assert.Equal(22, Alone(": button { key = \"t\"; label = \"Apply\"; }").Height);
            Assert.Equal(49, Alone(": button { key = \"t\"; label = \"Apply\"; width = 8; }").Width);
            Assert.Equal(121, Alone(": button { key = \"t\"; label = \"Apply\"; width = 20; }").Width);
            Assert.Equal(26, Alone(": button { key = \"t\"; label = \"Apply\"; height = 2; }").Height);
        }

        [Fact]
        public void TheExitButtons_WithTheMeasuredTextWidths_AreFortyNineAndFiftyFiveWide_AndTwentyEightApart()
        {
            // AutoCAD 2021 (GUI g1): OK 49 x 22 and Cancel 55 x 22, Cancel's left 28 after OK's right (E1: 200 and 228; ok_cancel
            // alone: 65 and 93); the text widths are the ones the layout is given: "  OK  " 30 (under the 33 that would
            // pass 49) and "Cancel" 39 (39 + 16 = 55)
            var metrics = new DclMetrics
            {
                TextWidth = text =>
                    text == "Cancel" ? 39
                    : text == "  OK  " ? 30
                    : 6 * text.Length,
            };
            DclTile dialog = Dialog("ok_cancel;");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog, metrics);

            DclRect ok = Of(placed, dialog, "accept");
            DclRect cancel = Of(placed, dialog, "cancel");
            Assert.Equal(49, ok.Width, 6);
            Assert.Equal(22, ok.Height, 6);
            Assert.Equal(55, cancel.Width, 6);
            Assert.Equal(22, cancel.Height, 6);
            Assert.Equal(28, cancel.X - (ok.X + ok.Width), 6);
            Assert.Equal(ok.Y, cancel.Y, 6);
        }

        [Fact]
        public void TheAccessMarkOfALabel_TakesNoRoom()
        {
            // ours: D311 and D334 (& marks the access key and is not shown)
            Assert.Equal(40, Alone(": button { key = \"t\"; label = \"&Help\"; }").Width);
            Assert.Equal(24, Alone(": text { key = \"t\"; label = \"A&&B&C\"; }").Width);
        }

        [Fact]
        public void AnEditBox_IsItsLabelNoGapAndEditWidthCharactersPlusTwelve()
        {
            // AutoCAD 2021 (GUI g1): the box is edit_width x 6 + 12 wide (20: 132, 60: 372) and 19 high, and starts where its
            // label ends; with the label "&Text:" 31 wide the tile of edit_width 20 is 163 by 19 (D337)
            DclRect labelled = Alone(": edit_box { key = \"t\"; label = \"Name:\"; edit_width = 20; }");
            DclRect bare = Alone(": edit_box { key = \"t\"; edit_width = 20; }");
            DclRect sixty = Alone(": edit_box { key = \"t\"; edit_width = 60; }");
            var measured = new DclMetrics { TextWidth = text => text == "Text:" ? 31 : 6 * text.Length };
            DclTile dialog = Dialog(": edit_box { key = \"t\"; label = \"&Text:\"; edit_width = 20; }");
            DclRect text = Of(Arrange(dialog, measured), dialog, "t");

            Assert.Equal(162, labelled.Width);
            Assert.Equal(19, labelled.Height);
            Assert.Equal(132, bare.Width);
            Assert.Equal(372, sixty.Width);
            Assert.Equal(163, text.Width, 6);
            Assert.Equal(19, text.Height, 6);
            Assert.Equal(31, DclLayout.LabelWidth(dialog.Find("t"), measured), 6);
        }

        [Fact]
        public void AnEditBoxWithNoEditWidth_NeedsAtLeastTenCharacters()
        {
            // ours: D311 (none: the box takes what is left, at least 10 characters and the 12 of the box's frame)
            Assert.Equal(102, Alone(": edit_box { key = \"t\"; label = \"Name:\"; }").Width);
            Assert.Equal(102, Alone(": edit_box { key = \"t\"; label = \"Name:\"; edit_width = 0; }").Width);
            Assert.Equal(72, Alone(": edit_box { key = \"t\"; }").Width);
        }

        [Fact]
        public void APopupList_IsSizedLikeAnEditBox_ButTwentyOneHigh()
        {
            // AutoCAD 2021 (GUI g1): a popup list is 21 high (D345); the width as an edit box's is ours: D311
            DclRect popup = Alone(": popup_list { key = \"t\"; label = \"Style:\"; edit_width = 15; }");

            Assert.Equal(36 + 0 + 102, popup.Width);
            Assert.Equal(21, popup.Height);
        }

        [Fact]
        public void AListBox_IsWidthByHeightLines_TenByTenWhereAbsent()
        {
            // AutoCAD 2021 (GUI g1): a list box width = 20 is 120 wide with no border (D337) and the tile height h is h x 13 - 10
            // (D345: h = 6 gives 68, h = 4 gives 41 where this gives 42); width and height default 10 is ours: D311
            DclRect standard = Alone(": list_box { key = \"t\"; }");
            DclRect sized = Alone(": list_box { key = \"t\"; width = 20; height = 5; }");

            Assert.Equal(60, standard.Width);
            Assert.Equal(120, standard.Height);
            Assert.Equal(120, sized.Width);
            Assert.Equal(55, sized.Height);
        }

        [Fact]
        public void TheLabelOfAListBox_CountsInItsHeight()
        {
            // AutoCAD 2021 (GUI g1): a list box width = 20, height = 6 with the label "List:" is a tile of 68 (the label's line
            // of 13 above a list of 54), no taller than the same list without a label (D345; D337 gives 120 x 68)
            DclRect labelled = Alone(": list_box { key = \"t\"; label = \"List:\"; width = 20; height = 6; }");
            DclRect bare = Alone(": list_box { key = \"t\"; width = 20; height = 6; }");
            DclRect four = Alone(": list_box { key = \"t\"; width = 20; height = 4; }");

            Assert.Equal(120, labelled.Width);
            Assert.Equal(68, labelled.Height);
            Assert.Equal(68, bare.Height);
            Assert.Equal(42, four.Height);
        }

        [Theory]
        [InlineData("toggle", 15)]
        [InlineData("radio_button", 19)]
        public void AToggleAndARadioButton_AreABoxAndTheLabel(string kind, int height)
        {
            // AutoCAD 2021 (GUI g1): a toggle is 15 high and a radio button 19 (D345); the box and the label ours: D311
            DclRect rect = Alone(": " + kind + " { key = \"t\"; label = \"Snap\"; }");

            Assert.Equal(44, rect.Width);
            Assert.Equal(height, rect.Height);
        }

        [Fact]
        public void ASlider_IsWidthLongAlongItsDirection_TenWhereAbsent()
        {
            // AutoCAD 2021 (GUI g1): a slider is 17 thick (D345); ours: D311 (a vertical one is height long)
            DclRect horizontal = Alone(": slider { key = \"t\"; }");
            DclRect wide = Alone(": slider { key = \"t\"; width = 30; }");
            DclRect vertical = Alone(": slider { key = \"t\"; layout = vertical; height = 8; }");

            Assert.Equal(60, horizontal.Width);
            Assert.Equal(17, horizontal.Height);
            Assert.Equal(180, wide.Width);
            Assert.Equal(17, vertical.Width);
            Assert.Equal(104, vertical.Height);
        }

        [Fact]
        public void ATextIsThirteenHigh_AndTheTilesOfAColumnAreEightApart()
        {
            // AutoCAD 2021 (GUI g1): in one column an edit box (19), a popup list (21), a toggle (15), a slider (17) and a text
            // (13) each follow the one before with 8 between (the pitches 27, 29, 23, 25 and 21 of the all-kinds dialog)
            DclTile dialog = Dialog(
                ": edit_box { key = \"a\"; edit_width = 5; } : popup_list { key = \"b\"; edit_width = 5; }"
                    + ": toggle { key = \"c\"; label = \"c\"; } : slider { key = \"d\"; } : text { key = \"e\"; label = \"e\"; }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(27, Of(placed, dialog, "b").Y - Of(placed, dialog, "a").Y, 6);
            Assert.Equal(29, Of(placed, dialog, "c").Y - Of(placed, dialog, "b").Y, 6);
            Assert.Equal(23, Of(placed, dialog, "d").Y - Of(placed, dialog, "c").Y, 6);
            Assert.Equal(25, Of(placed, dialog, "e").Y - Of(placed, dialog, "d").Y, 6);
            Assert.Equal(13, Of(placed, dialog, "e").Height, 6);
        }

        [Fact]
        public void TheButtonsOfARowOfRadioButtons_AreEightApart()
        {
            // AutoCAD 2021 (GUI g1): the radio buttons One and Two of the all-kinds dialog are 8 apart (93 to 101)
            DclTile dialog = Dialog(
                ": radio_row { : radio_button { key = \"a\"; label = \"One\"; } : radio_button { key = \"b\"; label = \"Two\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(8, Of(placed, dialog, "b").X - (Of(placed, dialog, "a").X + Of(placed, dialog, "a").Width), 6);
        }

        [Theory]
        [InlineData("image")]
        [InlineData("image_button")]
        public void AnImage_IsWidthByHeight_OneFromTheOtherThroughTheAspectRatio(string kind)
        {
            // AutoCAD 2021 (GUI g1): width x 6 by height x 13, and aspect_ratio r gives height = width x r (width 10, r 0.5: 60 x 30,
            // D337); the height from the width through r and one from the other are ours: D311; D313 (the width 10 gives dimx 60)
            DclRect both = Alone(": " + kind + " { key = \"t\"; width = 10; height = 5; }");
            DclRect fromWidth = Alone(": " + kind + " { key = \"t\"; width = 10; aspect_ratio = 0.5; }");
            DclRect fromHeight = Alone(": " + kind + " { key = \"t\"; height = 5; aspect_ratio = 0.5; }");
            DclRect widthOnly = Alone(": " + kind + " { key = \"t\"; width = 10; }");
            DclRect neither = Alone(": " + kind + " { key = \"t\"; }");
            DclRect bothAndAspect = Alone(": " + kind + " { key = \"t\"; width = 10; height = 5; aspect_ratio = 3; }");

            AssertRect(7, 7, 60, 65, both);
            Assert.Equal(60, fromWidth.Width);
            Assert.Equal(30, fromWidth.Height);
            Assert.Equal(130, fromHeight.Width);
            Assert.Equal(65, fromHeight.Height);
            Assert.Equal(60, widthOnly.Width);
            Assert.Equal(13, widthOnly.Height);
            Assert.Equal(6, neither.Width);
            Assert.Equal(13, neither.Height);
            Assert.Equal(60, bothAndAspect.Width);
            Assert.Equal(65, bothAndAspect.Height);
            Assert.Equal(60, (int)Math.Floor(both.Width));
        }

        [Fact]
        public void ASpacer_IsItsWidthAndHeightWithTheMargin_OneByOneWhereAbsent()
        {
            // ours: D311 and D306; AutoCAD 2021 (GUI g1): the spacer of width 2 between OK and Cancel leaves 28 between them,
            // 12 and the margin of 4 on each side of the spacer and of each button (D345)
            DclTile one = Dialog("spacer;");
            DclTile zero = Dialog("spacer_0;");
            DclTile two = Dialog(": spacer_0 { width = 2; }");
            DclTile lone = Dialog("spacer_1;");

            AssertRect(0, 0, 6 + 14, 13 + 14, Arrange(one)[one]);
            AssertRect(0, 0, 14, 14, Arrange(zero)[zero]);
            AssertRect(0, 0, 12 + 14, 14, Arrange(two)[two]);
            AssertRect(0, 0, 20, 27, Arrange(lone)[lone]);
        }

        [Fact]
        public void ASpacerThatSaysItsMarginIsNone_HasNone()
        {
            // ours: D312
            DclTile dialog = Dialog(": spacer { key = \"t\"; horizontal_margin = none; vertical_margin = none; }");

            AssertRect(3, 3, 6, 13, Of(Arrange(dialog), dialog, "t"));
        }

        [Fact]
        public void AnEmptyDialog_IsItsMarginsAlone()
        {
            DclTile dialog = Dialog("");

            AssertRect(0, 0, 6, 6, Arrange(dialog)[dialog]);
        }

        [Fact]
        public void AnEmptyRow_TakesNoRoom()
        {
            // ours: D312 (a row has no margin of its own)
            DclTile dialog = Dialog("row;");

            AssertRect(0, 0, 6, 6, Arrange(dialog)[dialog]);
        }

        [Fact]
        public void ATileOfAKindThatIsNotOneOfTheFourteen_IsOneByOneCharacters()
        {
            // ours: D311 (a hand-built dialog; the layout never fails for an odd kind)
            var odd = new DclTile("made_up", "made_up", null, null, "f", 1);
            var dialog = new DclTile("dialog", "d", null, new[] { odd }, "f", 1);

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(7, 7, 6, 13, placed[odd]);
        }

        // ---- Rows and columns (D312) ----

        [Fact]
        public void ARow_LaysItsChildrenLeftToRight()
        {
            // ours: D312 (two buttons of 28 and 52 with a margin of 4 on each side: slots of 36 and 60)
            DclTile dialog = Dialog(
                ": row { : button { key = \"a\"; label = \"Ok\"; } : button { key = \"b\"; label = \"Cancel\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(0, 0, 102, 36, placed[dialog]);
            AssertRect(3, 3, 96, 30, placed[dialog.Children[0]]);
            AssertRect(7, 7, 28, 22, Of(placed, dialog, "a"));
            AssertRect(43, 7, 52, 22, Of(placed, dialog, "b"));
        }

        [Fact]
        public void AColumn_LaysItsChildrenTopToBottom_AndStretchesThemAcross()
        {
            // ours: D312 (a child that is not fixed fills the column across it)
            DclTile dialog = Dialog(
                ": column { : text { key = \"a\"; label = \"a\"; } : text { key = \"b\"; label = \"bbb\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(0, 0, 26 + 6, 42 + 6, placed[dialog]);
            AssertRect(7, 7, 18, 13, Of(placed, dialog, "a"));
            AssertRect(7, 28, 18, 13, Of(placed, dialog, "b"));
        }

        [Fact]
        public void TheDialogItself_IsAColumn()
        {
            // ours: D312
            DclTile dialog = Dialog(": text { key = \"a\"; label = \"a\"; } : text { key = \"b\"; label = \"bbb\"; }");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(7, 7, 18, 13, Of(placed, dialog, "a"));
            AssertRect(7, 28, 18, 13, Of(placed, dialog, "b"));
        }

        [Fact]
        public void RoomToSpare_IsSharedEquallyByTheChildrenThatAreNotFixed()
        {
            // ours: D312 (a list of 240 makes the row 248 wide; the buttons' slots of 36 and 60 leave 152, 76 each)
            DclTile dialog = Dialog(
                ": list_box { key = \"l\"; width = 40; height = 2; } : row { : button { key = \"a\"; label = \"Ok\"; } : button { key = \"b\"; label = \"Cancel\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(3, 27, 248, 30, placed[dialog.Children[1]]);
            AssertRect(7, 31, 104, 22, Of(placed, dialog, "a"));
            AssertRect(119, 31, 128, 22, Of(placed, dialog, "b"));
        }

        [Fact]
        public void AFixedChild_KeepsItsNaturalSizeAndTheOthersShareTheRoom()
        {
            // ours: D312 (a row of a fixed button, a spacer and a text in a row 252 wide: the spacer and the text share)
            DclTile dialog = Dialog(
                ": list_box { key = \"l\"; width = 40; height = 1; }"
                    + ": row { : button { key = \"a\"; label = \"Ok\"; fixed_width = true; } : spacer_0 { key = \"s\"; width = 1; } : text { key = \"x\"; label = \"x\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            // the button's slot is 36 and fixed, the spacer's 14 and the text's 14: used 64 of 248, spare 184, 92 each
            AssertRect(7, 18, 28, 22, Of(placed, dialog, "a"));
            AssertRect(43, 18, 98, 22, Of(placed, dialog, "s"));
            AssertRect(149, 18, 98, 22, Of(placed, dialog, "x"));
        }

        [Fact]
        public void AFixedWidthChildOfAColumn_IsPlacedByItsAlignment()
        {
            // ours: D312 (children of a column are at the left unless they say otherwise; the room is 248 - 36 = 212)
            string wide = ": list_box { key = \"l\"; width = 40; height = 1; }";
            DclTile left = Dialog(wide + ": button { key = \"t\"; label = \"Ok\"; fixed_width = true; }");
            DclTile centered = Dialog(
                wide + ": button { key = \"t\"; label = \"Ok\"; fixed_width = true; alignment = centered; }"
            );
            DclTile right = Dialog(
                wide + ": button { key = \"t\"; label = \"Ok\"; fixed_width = true; alignment = right; }"
            );
            DclTile stretched = Dialog(wide + ": button { key = \"t\"; label = \"Ok\"; }");

            Assert.Equal(7, Of(Arrange(left), left, "t").X);
            Assert.Equal(28, Of(Arrange(left), left, "t").Width);
            Assert.Equal(113, Of(Arrange(centered), centered, "t").X);
            Assert.Equal(219, Of(Arrange(right), right, "t").X);
            Assert.Equal(28, Of(Arrange(right), right, "t").Width);
            Assert.Equal(7, Of(Arrange(stretched), stretched, "t").X);
            Assert.Equal(240, Of(Arrange(stretched), stretched, "t").Width);
        }

        [Fact]
        public void ChildrenFixedWidthAndChildrenAlignment_SetTheDefaultForTheChildren()
        {
            // ours: D312 (a child's own fixed_width wins over the cluster's children_fixed_width)
            DclTile dialog = Dialog(
                ": list_box { key = \"l\"; width = 40; height = 1; }"
                    + ": column { children_fixed_width = true; children_alignment = right;"
                    + " : button { key = \"a\"; label = \"Ok\"; } : button { key = \"b\"; label = \"Cancel\"; }"
                    + " : button { key = \"c\"; label = \"Wide\"; fixed_width = false; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(219, Of(placed, dialog, "a").X);
            Assert.Equal(28, Of(placed, dialog, "a").Width);
            Assert.Equal(195, Of(placed, dialog, "b").X);
            Assert.Equal(52, Of(placed, dialog, "b").Width);
            Assert.Equal(7, Of(placed, dialog, "c").X);
            Assert.Equal(240, Of(placed, dialog, "c").Width);
        }

        [Fact]
        public void AFixedHeightChildOfARow_IsCentredUnlessItSaysTopOrBottom()
        {
            // ours: D312 (the row is 50 high, the button's slot 30: room 20)
            string tall = ": list_box { key = \"l\"; height = 4; width = 5; }";
            DclTile centered = Dialog(
                ": row { " + tall + ": button { key = \"t\"; label = \"Ok\"; fixed_height = true; } }"
            );
            DclTile top = Dialog(
                ": row { " + tall + ": button { key = \"t\"; label = \"Ok\"; fixed_height = true; alignment = top; } }"
            );
            DclTile bottom = Dialog(
                ": row { "
                    + tall
                    + ": button { key = \"t\"; label = \"Ok\"; fixed_height = true; alignment = bottom; } }"
            );
            DclTile stretched = Dialog(": row { " + tall + ": button { key = \"t\"; label = \"Ok\"; } }");

            Assert.Equal(3 + 10 + 4, Of(Arrange(centered), centered, "t").Y, 6);
            Assert.Equal(22, Of(Arrange(centered), centered, "t").Height, 6);
            Assert.Equal(3 + 0 + 4, Of(Arrange(top), top, "t").Y, 6);
            Assert.Equal(3 + 20 + 4, Of(Arrange(bottom), bottom, "t").Y, 6);
            Assert.Equal(7, Of(Arrange(stretched), stretched, "t").Y, 6);
            Assert.Equal(42, Of(Arrange(stretched), stretched, "t").Height, 6);
        }

        [Fact]
        public void ARowsChildrenKeepTheirOrderAndNeverOverlap()
        {
            // ours: D312
            DclTile dialog = Dialog(
                ": row { : toggle { key = \"a\"; label = \"One\"; } : toggle { key = \"b\"; label = \"Two\"; } : toggle { key = \"c\"; label = \"Three\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            DclRect a = Of(placed, dialog, "a");
            DclRect b = Of(placed, dialog, "b");
            DclRect c = Of(placed, dialog, "c");
            Assert.True(a.X + a.Width < b.X);
            Assert.True(b.X + b.Width < c.X);
        }

        [Fact]
        public void ARadioColumn_StacksItsButtons()
        {
            // ours: D312 (a radio cluster lays out as a cluster)
            DclTile dialog = Dialog(
                ": radio_column { key = \"r\"; : radio_button { key = \"a\"; label = \"a\"; } : radio_button { key = \"b\"; label = \"bb\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(7, Of(placed, dialog, "a").Y, 6);
            Assert.Equal(7 + 19 + 8, Of(placed, dialog, "b").Y, 6);
            Assert.Equal(3, Of(placed, dialog, "r").X, 6);
        }

        [Fact]
        public void AClusterOrDialogWidth_IsAMinimum()
        {
            // ours: D311
            DclTile dialog = Dialog(": row { key = \"r\"; width = 30; : text { label = \"x\"; } }");
            DclTile wide = Dialog("width = 40; : text { label = \"x\"; }");

            Assert.Equal(180, Of(Arrange(dialog), dialog, "r").Width, 6);
            AssertRect(0, 0, 240, 6 + 21, Arrange(wide)[wide]);
        }

        // ---- Margins (D312) ----

        [Fact]
        public void AMarginOfNone_IsZero_OnOneSideOrBoth()
        {
            // ours: D312
            DclTile both = Dialog(
                ": button { key = \"t\"; label = \"Apply\"; horizontal_margin = none; vertical_margin = none; }"
            );
            DclTile horizontal = Dialog(": button { key = \"t\"; label = \"Apply\"; horizontal_margin = none; }");

            AssertRect(3, 3, 46, 22, Of(Arrange(both), both, "t"));
            AssertRect(0, 0, 52, 28, Arrange(both)[both]);
            AssertRect(3, 7, 46, 22, Of(Arrange(horizontal), horizontal, "t"));
        }

        [Fact]
        public void RowAndColumn_HaveNoMarginsOfTheirOwn_AndABoxedClusterHasTheDefault()
        {
            // ours: D306 (row and column none) and D312
            DclTile dialog = Dialog(
                ": row { key = \"r\"; : text { label = \"x\"; } } : column { key = \"c\"; : text { label = \"x\"; } } : boxed_row { key = \"b\"; : text { label = \"x\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(3, Of(placed, dialog, "r").X, 6);
            Assert.Equal(3, Of(placed, dialog, "r").Y, 6);
            Assert.Equal(3, Of(placed, dialog, "c").X, 6);
            Assert.Equal(7, Of(placed, dialog, "b").X, 6);
        }

        // ---- Boxed clusters (D312) ----

        [Fact]
        public void ABoxedCluster_AddsABorderAndItsLabelAbove()
        {
            // ours: D312 (border 8 at the sides and below, the label's line of 13 and 4 above; not fitted to the insets of 5,
            // 19 and 2 that the all-kinds dialog's radio box shows)
            DclTile dialog = Dialog(
                ": boxed_column { key = \"b\"; label = \"Group\"; : text { key = \"t\"; label = \"Hello\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(7, 7, 54, 46, Of(placed, dialog, "b"));
            AssertRect(19, 28, 30, 13, Of(placed, dialog, "t"));
            AssertRect(0, 0, 68, 60, placed[dialog]);
        }

        [Fact]
        public void ABoxedCluster_IsAsWideAsItsLabel()
        {
            // ours: D312 (the label is 17 characters, 102, with the border of 8 each side: 118)
            DclRect box = Alone(
                ": boxed_row { key = \"t\"; label = \"A long label here\"; : text { label = \"x\"; } }"
            );

            Assert.Equal(118, box.Width, 6);
        }

        [Fact]
        public void ABoxedRadioColumn_IsBoxedToo()
        {
            // ours: D306 and D312
            DclTile dialog = Dialog(
                ": boxed_radio_column { key = \"t\"; label = \"Pick\"; : radio_button { key = \"a\"; label = \"a\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.True(Of(placed, dialog, "a").X > Of(placed, dialog, "t").X + 8);
            Assert.True(Of(placed, dialog, "a").Y > Of(placed, dialog, "t").Y + 17);
        }

        // ---- The exit rows (D306) ----

        [Fact]
        public void AnOkCancelRow_IsCentredInAWiderDialog()
        {
            // ours: D306 and D312 (the row is 140 wide: two buttons of 52, the spacer of two characters and the margins)
            DclTile dialog = Dialog(": list_box { key = \"l\"; width = 40; height = 1; } ok_cancel;");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            DclRect row = placed[dialog.Children[1]];
            DclRect ok = Of(placed, dialog, "accept");
            DclRect cancel = Of(placed, dialog, "cancel");
            AssertRect(57, 14, 140, 30, row);
            AssertRect(61, 18, 52, 22, ok);
            AssertRect(141, 18, 52, 22, cancel);
            Assert.Equal(row.X - 3, 3 + 248 - (row.X + row.Width), 6);
        }

        [Theory]
        [InlineData("ok_only", 1)]
        [InlineData("ok_cancel", 2)]
        [InlineData("ok_cancel_help", 3)]
        [InlineData("ok_cancel_help_info", 4)]
        public void TheExitRows_AreCentredWhateverTheDialogsWidth(string row, int buttons)
        {
            // ours: D306
            DclTile dialog = Dialog(": list_box { key = \"l\"; width = 60; height = 1; } " + row + ";");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            DclTile[] found = dialog.Children[1].Descendants().Where(tile => tile.Kind == "button").ToArray();
            Assert.Equal(buttons, found.Length);
            double left = placed[found[0]].X - placed[dialog].X;
            double right = placed[dialog].Width - (placed[found[buttons - 1]].X + placed[found[buttons - 1]].Width);
            Assert.Equal(left, right, 6);
        }

        [Fact]
        public void AnErrorTile_IsThirtyFiveCharactersWide_AndTheButtonsBelowItAreCentred()
        {
            // ours: D306 (35 characters is 210, the column is 218 wide, the row of 140 has room 78: 39 each side)
            DclTile dialog = Dialog("ok_cancel_err;");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(210, Of(placed, dialog, "error").Width, 6);
            Assert.Equal(3 + 39 + 4, Of(placed, dialog, "accept").X, 6);
            Assert.Equal(218 + 6, placed[dialog].Width, 6);
        }

        // ---- The whole result (D310, D313) ----

        [Fact]
        public void EveryTileOfTheDialog_IsPlaced_AndTheDialogStartsAtTheOrigin()
        {
            // ours: the contract of DclLayout.Arrange
            DclTile dialog = Dialog(
                ": row { : text { label = \"a\"; } : toggle { label = \"b\"; } } ok_cancel; : image { key = \"i\"; width = 4; }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(1 + dialog.Descendants().Count(), placed.Count);
            Assert.All(dialog.Descendants(), tile => Assert.True(placed.ContainsKey(tile)));
            Assert.Equal(0, placed[dialog].X);
            Assert.Equal(0, placed[dialog].Y);
        }

        [Fact]
        public void DimxAndDimy_OfAnImageOfTenByFiveCharacters_AreSixtyAndSixtyFive()
        {
            // ours: D313 (the rectangle's width and height rounded down; provisional metrics, 6 by 13)
            DclTile dialog = Dialog(": image { key = \"i\"; width = 10; height = 5; }");

            DclRect image = Of(Arrange(dialog), dialog, "i");

            Assert.Equal(60, (int)Math.Floor(image.Width));
            Assert.Equal(65, (int)Math.Floor(image.Height));
        }

        [Fact]
        public void ArrangingTwice_GivesTheSameRectangles()
        {
            DclTile dialog = Dialog(
                ": row { : text { label = \"a\"; } : toggle { label = \"b\"; } } ok_cancel_help; : list_box { width = 20; }"
            );

            IReadOnlyDictionary<DclTile, DclRect> one = Arrange(dialog);
            IReadOnlyDictionary<DclTile, DclRect> two = Arrange(dialog);

            foreach (DclTile tile in dialog.Descendants())
            {
                AssertRect(one[tile].X, one[tile].Y, one[tile].Width, one[tile].Height, two[tile]);
            }
        }

        [Fact]
        public void TheMetrics_ScaleTheResult()
        {
            // ours: D310 (every pixel size comes from the metrics)
            var metrics = new DclMetrics { CharWidth = 7, CharHeight = 14 };
            DclTile dialog = Dialog(": text { key = \"t\"; label = \"Hello\"; }");

            AssertRect(7, 7, 35, 14, Of(Arrange(dialog, metrics), dialog, "t"));
        }

        [Fact]
        public void ATextWidthOfTheMetrics_IsUsedForEveryLabel()
        {
            // ours: D310 (the window gives the measured width)
            var metrics = new DclMetrics { TextWidth = text => text.Length * 10 };
            DclTile dialog = Dialog(
                ": text { key = \"t\"; label = \"Hello\"; fixed_width = true; } : button { key = \"b\"; label = \"&Go\"; fixed_width = true; } : edit_box { key = \"e\"; label = \"Ab\"; edit_width = 5; fixed_width = true; }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog, metrics);

            // the text 5 characters of 10; the button 2 of 10 and 16 of padding; the edit box 2 of 10, no gap, 5 of 6 and 12
            Assert.Equal(50, Of(placed, dialog, "t").Width, 6);
            Assert.Equal(36, Of(placed, dialog, "b").Width, 6);
            Assert.Equal(62, Of(placed, dialog, "e").Width, 6);
        }

        [Fact]
        public void TheDefaultMetrics_AreSixByThirteen()
        {
            // AutoCAD 2021 (GUI g1): a list box width = 20 is 120 px and an image height = 4 is 52 px (D337)
            DclMetrics metrics = DclMetrics.Default;

            Assert.Equal(6, metrics.CharWidth);
            Assert.Equal(13, metrics.CharHeight);
            Assert.Equal(30, metrics.TextWidth("Hello"));
            Assert.Equal(0, metrics.TextWidth(null));
        }

        [Fact]
        public void TheDefaultMetrics_HoldTheMeasuredSizesOfTheControls()
        {
            // AutoCAD 2021 (GUI g1, D345): button 22, edit box 19, popup list 21, toggle 15, radio button 19, slider 17, list
            // line 13, label gap 0, the room a button adds to its label 16 and an edit box's frame 12
            DclMetrics metrics = DclMetrics.Default;

            Assert.Equal(22, metrics.ButtonHeight);
            Assert.Equal(19, metrics.EditHeight);
            Assert.Equal(21, metrics.PopupHeight);
            Assert.Equal(15, metrics.ToggleHeight);
            Assert.Equal(19, metrics.RadioHeight);
            Assert.Equal(17, metrics.SliderThickness);
            Assert.Equal(13, metrics.ListLineHeight);
            Assert.Equal(0, metrics.LabelGap);
            Assert.Equal(16, metrics.ButtonPaddingX);
            Assert.Equal(12, metrics.EditBoxPadding);
        }

        [Fact]
        public void TheTilesThatAreNotFixed_StretchAcrossTheirColumn()
        {
            // AutoCAD 2021 (GUI g1): in the column of a list box of width 20 (120) the image 10 by 4 is 120 x 52, the image with
            // aspect_ratio 0.5 on width 10 is 120 x 30, the image button 6 by 2 is 120 x 26 and a button is 120 x 22 (D337, D345)
            DclTile dialog = Dialog(
                ": list_box { key = \"l\"; width = 20; height = 4; } : image { key = \"i\"; width = 10; height = 4; }"
                    + ": image { key = \"a\"; width = 10; aspect_ratio = 0.5; } : image_button { key = \"b\"; width = 6; height = 2; }"
                    + ": button { key = \"c\"; label = \"&Button\"; }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertSize(120, 52, Of(placed, dialog, "i"));
            AssertSize(120, 30, Of(placed, dialog, "a"));
            AssertSize(120, 26, Of(placed, dialog, "b"));
            AssertSize(120, 22, Of(placed, dialog, "c"));
            AssertSize(120, 42, Of(placed, dialog, "l"));
        }

        // ---- Helpers of the layout ----

        [Theory]
        [InlineData("&Help", "Help")]
        [InlineData("&&", "&")]
        [InlineData("A&&B", "A&B")]
        [InlineData("Save &as...", "Save as...")]
        [InlineData("trailing&", "trailing")]
        [InlineData("plain", "plain")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void StripAccess_TakesTheAccessMarkOut(string label, string shown)
        {
            // ours: D334 (& marks the access key)
            Assert.Equal(shown, DclLayout.StripAccess(label));
        }

        [Fact]
        public void LabelWidth_IsTheLabelAndTheGap_OrNothingWithoutALabel()
        {
            // ours: D310
            DclTile labelled = Dialog(
                ": edit_box { key = \"a\"; label = \"Name:\"; } : edit_box { key = \"b\"; label = \"&Name\"; } : edit_box { key = \"c\"; }"
            );

            Assert.Equal(30, DclLayout.LabelWidth(labelled.Find("a"), DclMetrics.Default), 6);
            Assert.Equal(24, DclLayout.LabelWidth(labelled.Find("b"), DclMetrics.Default), 6);
            Assert.Equal(0, DclLayout.LabelWidth(labelled.Find("c"), DclMetrics.Default), 6);
        }

        [Fact]
        public void ANullDialogOrMetrics_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => DclLayout.Arrange(null, DclMetrics.Default));
            Assert.Throws<ArgumentNullException>(() => DclLayout.Arrange(Dialog(""), null));
        }

        // ---- Dialogs shaped like the acceptance routines' (written for the test) ----

        private const string EditDialog =
            "EDIT : dialog { label = \"Edit text\"; initial_focus = \"txt\";\n"
            + ": edit_box { key = \"txt\"; label = \"Text:\"; edit_width = 60; allow_accept = true; }\n"
            + "spacer_1; ok_cancel; }";

        private const string ListDialog =
            "ListBox : dialog { label = \"Tags\"; : text { label = \"Pick the tags:\"; } spacer;\n"
            + ": list_box { key = \"lst\"; multiple_select = true; width = 30; height = 8; } spacer; ok_cancel; }";

        private const string EveryKind =
            "All : dialog { label = \"All\";\n"
            + ": row { : boxed_column { label = \"Left\"; : text { label = \"Some text\"; is_bold = true; }\n"
            + ": edit_box { label = \"Name:\"; edit_width = 12; } : popup_list { label = \"Style:\"; edit_width = 12; }\n"
            + ": toggle { label = \"A toggle\"; } }\n"
            + ": boxed_radio_column { label = \"Radio\"; : radio_button { label = \"One\"; } : radio_button { label = \"Two\"; } }\n"
            + ": column { : list_box { width = 12; height = 5; } : slider { layout = horizontal; width = 12; } } }\n"
            + ": row { : image { width = 10; height = 4; } : image_button { width = 4; aspect_ratio = 1; } : slider { layout = vertical; height = 4; } spacer; }\n"
            + ": concatenation { : text_part { label = \"One \"; } : text_part { label = \"line\"; } }\n"
            + ": paragraph { : text_part { label = \"First\"; } : text_part { label = \"Second\"; } }\n"
            + "ok_cancel_help_errtile; }";

        [Theory]
        [InlineData(EditDialog, "EDIT")]
        [InlineData(ListDialog, "ListBox")]
        [InlineData(EveryKind, "All")]
        public void EveryTile_LiesInsideItsParent_AndNoTwoSiblingsOverlap(string text, string name)
        {
            // ours: the layout contract (a rectangle inside its cluster, siblings apart)
            DclTile dialog = DclParser.Parse(text, @"C:\dcl\x.dcl", null).NewDialog(name);

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            foreach (DclTile tile in dialog.Descendants())
            {
                DclRect inner = placed[tile];
                DclRect outer = placed[tile.Parent];
                Assert.True(inner.X >= outer.X - 1e-9, tile.Name + " left");
                Assert.True(inner.Y >= outer.Y - 1e-9, tile.Name + " top");
                Assert.True(inner.X + inner.Width <= outer.X + outer.Width + 1e-9, tile.Name + " right");
                Assert.True(inner.Y + inner.Height <= outer.Y + outer.Height + 1e-9, tile.Name + " bottom");
            }

            foreach (
                DclTile cluster in new[] { dialog }.Concat(dialog.Descendants()).Where(tile => tile.Children.Count > 1)
            )
            {
                for (int i = 0; i < cluster.Children.Count; i++)
                {
                    for (int j = i + 1; j < cluster.Children.Count; j++)
                    {
                        DclRect a = placed[cluster.Children[i]];
                        DclRect b = placed[cluster.Children[j]];
                        bool apart =
                            a.X + a.Width <= b.X + 1e-9
                            || b.X + b.Width <= a.X + 1e-9
                            || a.Y + a.Height <= b.Y + 1e-9
                            || b.Y + b.Height <= a.Y + 1e-9;
                        Assert.True(apart, cluster.Name + " children " + i + " and " + j);
                    }
                }
            }
        }

        [Fact]
        public void TheEditDialog_HasTheEditBoxAtItsWidth_AndTheButtonsCentred()
        {
            // AutoCAD 2021 (GUI g1): the box of 60 characters is 372 and starts at its label (D337); label 5 characters 30: 402 wide
            DclTile dialog = DclParser.Parse(EditDialog, @"C:\dcl\x.dcl", null).NewDialog("EDIT");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            DclRect box = Of(placed, dialog, "txt");
            Assert.Equal(402, box.Width, 6);
            Assert.Equal(402 + 8 + 6, placed[dialog].Width, 6);
            Assert.Equal(
                Of(placed, dialog, "accept").X - 3,
                placed[dialog].Width - 3 - (Of(placed, dialog, "cancel").X + Of(placed, dialog, "cancel").Width),
                6
            );
        }
    }
}
