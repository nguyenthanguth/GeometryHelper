using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Dcl;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The layout of a dialog's tiles (L5 part P, D310-D313) with the provisional headless numbers: a character 6 by 13, a
    /// button's padding 12 and height 23, a tile's margin 4 and 3, a dialog margin of 12, a list line of 15 with a border of 2,
    /// a label gap of 6, a boxed cluster's border of 8. Every row is the spec's choice (<c>ours: D311</c>, <c>ours: D312</c>);
    /// the reference gives the units (widths in character widths, heights in character heights) and not the pixels, and the
    /// console cannot show a dialog, so no row is measured.
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
            // ours: D311 (a text is its label's width by one line), D312 (margins 4 and 3, the dialog's 12)
            DclTile dialog = Dialog(": text { key = \"t\"; label = \"Hello\"; }");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(0, 0, 62, 43, placed[dialog]);
            AssertRect(16, 15, 30, 13, placed[dialog.Find("t")]);
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
        public void AButton_IsItsLabelPlusPaddingAndAtLeastItsWidth()
        {
            // ours: D311
            Assert.Equal(42, Alone(": button { key = \"t\"; label = \"Apply\"; }").Width);
            Assert.Equal(23, Alone(": button { key = \"t\"; label = \"Apply\"; }").Height);
            Assert.Equal(48, Alone(": button { key = \"t\"; label = \"Apply\"; width = 8; }").Width);
            Assert.Equal(26, Alone(": button { key = \"t\"; label = \"Apply\"; height = 2; }").Height);
        }

        [Fact]
        public void TheAccessMarkOfALabel_TakesNoRoom()
        {
            // ours: D311 and D334 (& marks the access key and is not shown)
            Assert.Equal(36, Alone(": button { key = \"t\"; label = \"&Help\"; }").Width);
            Assert.Equal(24, Alone(": text { key = \"t\"; label = \"A&&B&C\"; }").Width);
        }

        [Fact]
        public void AnEditBox_IsItsLabelAGapAndEditWidthCharacters()
        {
            // ours: D311
            DclRect labelled = Alone(": edit_box { key = \"t\"; label = \"Name:\"; edit_width = 20; }");
            DclRect bare = Alone(": edit_box { key = \"t\"; edit_width = 20; }");

            Assert.Equal(156, labelled.Width);
            Assert.Equal(23, labelled.Height);
            Assert.Equal(120, bare.Width);
        }

        [Fact]
        public void AnEditBoxWithNoEditWidth_NeedsAtLeastTenCharacters()
        {
            // ours: D311 (none: the box takes what is left, at least 10)
            Assert.Equal(96, Alone(": edit_box { key = \"t\"; label = \"Name:\"; }").Width);
            Assert.Equal(96, Alone(": edit_box { key = \"t\"; label = \"Name:\"; edit_width = 0; }").Width);
            Assert.Equal(60, Alone(": edit_box { key = \"t\"; }").Width);
        }

        [Fact]
        public void APopupList_IsSizedLikeAnEditBox()
        {
            // ours: D311
            DclRect popup = Alone(": popup_list { key = \"t\"; label = \"Style:\"; edit_width = 15; }");

            Assert.Equal(36 + 6 + 90, popup.Width);
            Assert.Equal(23, popup.Height);
        }

        [Fact]
        public void AListBox_IsWidthByHeightLines_TenByTenWhereAbsent()
        {
            // ours: D311 (width default 10, height default 10 lines; a line is 15 and the border 2 on each side)
            DclRect standard = Alone(": list_box { key = \"t\"; }");
            DclRect sized = Alone(": list_box { key = \"t\"; width = 20; height = 5; }");

            Assert.Equal(64, standard.Width);
            Assert.Equal(154, standard.Height);
            Assert.Equal(124, sized.Width);
            Assert.Equal(79, sized.Height);
        }

        [Theory]
        [InlineData("toggle")]
        [InlineData("radio_button")]
        public void AToggleAndARadioButton_AreABoxAndTheLabel(string kind)
        {
            // ours: D311
            DclRect rect = Alone(": " + kind + " { key = \"t\"; label = \"Snap\"; }");

            Assert.Equal(44, rect.Width);
            Assert.Equal(18, rect.Height);
        }

        [Fact]
        public void ASlider_IsWidthLongAlongItsDirection_TenWhereAbsent()
        {
            // ours: D311 (a vertical one is height long)
            DclRect horizontal = Alone(": slider { key = \"t\"; }");
            DclRect wide = Alone(": slider { key = \"t\"; width = 30; }");
            DclRect vertical = Alone(": slider { key = \"t\"; layout = vertical; height = 8; }");

            Assert.Equal(60, horizontal.Width);
            Assert.Equal(21, horizontal.Height);
            Assert.Equal(180, wide.Width);
            Assert.Equal(21, vertical.Width);
            Assert.Equal(104, vertical.Height);
        }

        [Theory]
        [InlineData("image")]
        [InlineData("image_button")]
        public void AnImage_IsWidthByHeight_OneFromTheOtherThroughTheAspectRatio(string kind)
        {
            // ours: D311 (aspect_ratio is width over height, in pixels); D313 (the width 10 gives dimx 60)
            DclRect both = Alone(": " + kind + " { key = \"t\"; width = 10; height = 5; }");
            DclRect fromWidth = Alone(": " + kind + " { key = \"t\"; width = 10; aspect_ratio = 2; }");
            DclRect fromHeight = Alone(": " + kind + " { key = \"t\"; height = 5; aspect_ratio = 2; }");
            DclRect widthOnly = Alone(": " + kind + " { key = \"t\"; width = 10; }");
            DclRect neither = Alone(": " + kind + " { key = \"t\"; }");
            DclRect bothAndAspect = Alone(": " + kind + " { key = \"t\"; width = 10; height = 5; aspect_ratio = 3; }");

            AssertRect(16, 15, 60, 65, both);
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
        public void ASpacer_IsItsWidthAndHeightWithNoMargin_OneByOneWhereAbsent()
        {
            // ours: D311 and D306
            DclTile one = Dialog("spacer;");
            DclTile zero = Dialog("spacer_0;");
            DclTile two = Dialog(": spacer_0 { width = 2; }");
            DclTile lone = Dialog("spacer_1;");

            AssertRect(0, 0, 6 + 24, 13 + 24, Arrange(one)[one]);
            AssertRect(0, 0, 24, 24, Arrange(zero)[zero]);
            AssertRect(0, 0, 12 + 24, 24, Arrange(two)[two]);
            AssertRect(0, 0, 30, 37, Arrange(lone)[lone]);
        }

        [Fact]
        public void ASpacerThatSaysItsMargin_HasIt()
        {
            // ours: D312
            DclTile dialog = Dialog(
                ": spacer { key = \"t\"; horizontal_margin = default; vertical_margin = default; }"
            );

            AssertRect(16, 15, 6, 13, Of(Arrange(dialog), dialog, "t"));
        }

        [Fact]
        public void AnEmptyDialog_IsItsMarginsAlone()
        {
            DclTile dialog = Dialog("");

            AssertRect(0, 0, 24, 24, Arrange(dialog)[dialog]);
        }

        [Fact]
        public void AnEmptyRow_TakesNoRoom()
        {
            // ours: D312 (a row has no margin of its own)
            DclTile dialog = Dialog("row;");

            AssertRect(0, 0, 24, 24, Arrange(dialog)[dialog]);
        }

        [Fact]
        public void ATileOfAKindThatIsNotOneOfTheFourteen_IsOneByOneCharacters()
        {
            // ours: D311 (a hand-built dialog; the layout never fails for an odd kind)
            var odd = new DclTile("made_up", "made_up", null, null, "f", 1);
            var dialog = new DclTile("dialog", "d", null, new[] { odd }, "f", 1);

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(16, 15, 6, 13, placed[odd]);
        }

        // ---- Rows and columns (D312) ----

        [Fact]
        public void ARow_LaysItsChildrenLeftToRight()
        {
            // ours: D312 (two buttons of 24 and 48 with margins of 4 and 3: slots of 32 and 56)
            DclTile dialog = Dialog(
                ": row { : button { key = \"a\"; label = \"Ok\"; } : button { key = \"b\"; label = \"Cancel\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(0, 0, 112, 53, placed[dialog]);
            AssertRect(12, 12, 88, 29, placed[dialog.Children[0]]);
            AssertRect(16, 15, 24, 23, Of(placed, dialog, "a"));
            AssertRect(48, 15, 48, 23, Of(placed, dialog, "b"));
        }

        [Fact]
        public void AColumn_LaysItsChildrenTopToBottom_AndStretchesThemAcross()
        {
            // ours: D312 (a child that is not fixed fills the column across it)
            DclTile dialog = Dialog(
                ": column { : text { key = \"a\"; label = \"a\"; } : text { key = \"b\"; label = \"bbb\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(0, 0, 26 + 24, 38 + 24, placed[dialog]);
            AssertRect(16, 15, 18, 13, Of(placed, dialog, "a"));
            AssertRect(16, 34, 18, 13, Of(placed, dialog, "b"));
        }

        [Fact]
        public void TheDialogItself_IsAColumn()
        {
            // ours: D312
            DclTile dialog = Dialog(": text { key = \"a\"; label = \"a\"; } : text { key = \"b\"; label = \"bbb\"; }");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(16, 15, 18, 13, Of(placed, dialog, "a"));
            AssertRect(16, 34, 18, 13, Of(placed, dialog, "b"));
        }

        [Fact]
        public void RoomToSpare_IsSharedEquallyByTheChildrenThatAreNotFixed()
        {
            // ours: D312 (a list of 244 makes the row 252 wide; the buttons' slots of 32 and 56 leave 164, 82 each)
            DclTile dialog = Dialog(
                ": list_box { key = \"l\"; width = 40; height = 2; } : row { : button { key = \"a\"; label = \"Ok\"; } : button { key = \"b\"; label = \"Cancel\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(12, 52, 252, 29, placed[dialog.Children[1]]);
            AssertRect(16, 55, 106, 23, Of(placed, dialog, "a"));
            AssertRect(130, 55, 130, 23, Of(placed, dialog, "b"));
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

            // the button's slot is 32 and fixed, the spacer's 6 and the text's 14: used 52 of 252, spare 200, 100 each
            AssertRect(16, 40, 24, 23, Of(placed, dialog, "a"));
            AssertRect(44, 37, 106, 29, Of(placed, dialog, "s"));
            AssertRect(154, 40, 106, 23, Of(placed, dialog, "x"));
        }

        [Fact]
        public void AFixedWidthChildOfAColumn_IsPlacedByItsAlignment()
        {
            // ours: D312 (children of a column are at the left unless they say otherwise; the room is 252 - 32 = 220)
            string wide = ": list_box { key = \"l\"; width = 40; height = 1; }";
            DclTile left = Dialog(wide + ": button { key = \"t\"; label = \"Ok\"; fixed_width = true; }");
            DclTile centered = Dialog(
                wide + ": button { key = \"t\"; label = \"Ok\"; fixed_width = true; alignment = centered; }"
            );
            DclTile right = Dialog(
                wide + ": button { key = \"t\"; label = \"Ok\"; fixed_width = true; alignment = right; }"
            );
            DclTile stretched = Dialog(wide + ": button { key = \"t\"; label = \"Ok\"; }");

            Assert.Equal(16, Of(Arrange(left), left, "t").X);
            Assert.Equal(24, Of(Arrange(left), left, "t").Width);
            Assert.Equal(126, Of(Arrange(centered), centered, "t").X);
            Assert.Equal(236, Of(Arrange(right), right, "t").X);
            Assert.Equal(24, Of(Arrange(right), right, "t").Width);
            Assert.Equal(16, Of(Arrange(stretched), stretched, "t").X);
            Assert.Equal(244, Of(Arrange(stretched), stretched, "t").Width);
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

            Assert.Equal(236, Of(placed, dialog, "a").X);
            Assert.Equal(24, Of(placed, dialog, "a").Width);
            Assert.Equal(212, Of(placed, dialog, "b").X);
            Assert.Equal(48, Of(placed, dialog, "b").Width);
            Assert.Equal(16, Of(placed, dialog, "c").X);
            Assert.Equal(244, Of(placed, dialog, "c").Width);
        }

        [Fact]
        public void AFixedHeightChildOfARow_IsCentredUnlessItSaysTopOrBottom()
        {
            // ours: D312 (the row is 70 high, the button's slot 29: room 41)
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

            Assert.Equal(12 + 20.5 + 3, Of(Arrange(centered), centered, "t").Y, 6);
            Assert.Equal(23, Of(Arrange(centered), centered, "t").Height, 6);
            Assert.Equal(12 + 0 + 3, Of(Arrange(top), top, "t").Y, 6);
            Assert.Equal(12 + 41 + 3, Of(Arrange(bottom), bottom, "t").Y, 6);
            Assert.Equal(15, Of(Arrange(stretched), stretched, "t").Y, 6);
            Assert.Equal(64, Of(Arrange(stretched), stretched, "t").Height, 6);
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

            Assert.Equal(15, Of(placed, dialog, "a").Y, 6);
            Assert.Equal(15 + 18 + 6, Of(placed, dialog, "b").Y, 6);
            Assert.Equal(12, Of(placed, dialog, "r").X, 6);
        }

        [Fact]
        public void AClusterOrDialogWidth_IsAMinimum()
        {
            // ours: D311
            DclTile dialog = Dialog(": row { key = \"r\"; width = 30; : text { label = \"x\"; } }");
            DclTile wide = Dialog("width = 40; : text { label = \"x\"; }");

            Assert.Equal(180, Of(Arrange(dialog), dialog, "r").Width, 6);
            AssertRect(0, 0, 240, 24 + 19, Arrange(wide)[wide]);
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

            AssertRect(12, 12, 42, 23, Of(Arrange(both), both, "t"));
            AssertRect(0, 0, 66, 47, Arrange(both)[both]);
            AssertRect(12, 15, 42, 23, Of(Arrange(horizontal), horizontal, "t"));
        }

        [Fact]
        public void RowAndColumn_HaveNoMarginsOfTheirOwn_AndABoxedClusterHasTheDefault()
        {
            // ours: D306 (row and column none) and D312
            DclTile dialog = Dialog(
                ": row { key = \"r\"; : text { label = \"x\"; } } : column { key = \"c\"; : text { label = \"x\"; } } : boxed_row { key = \"b\"; : text { label = \"x\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(12, Of(placed, dialog, "r").X, 6);
            Assert.Equal(12, Of(placed, dialog, "r").Y, 6);
            Assert.Equal(12, Of(placed, dialog, "c").X, 6);
            Assert.Equal(16, Of(placed, dialog, "b").X, 6);
        }

        // ---- Boxed clusters (D312) ----

        [Fact]
        public void ABoxedCluster_AddsABorderAndItsLabelAbove()
        {
            // ours: D312 (border 8 at the sides and below, the label's line of 13 and 4 above)
            DclTile dialog = Dialog(
                ": boxed_column { key = \"b\"; label = \"Group\"; : text { key = \"t\"; label = \"Hello\"; } }"
            );

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            AssertRect(16, 15, 54, 44, Of(placed, dialog, "b"));
            AssertRect(28, 35, 30, 13, Of(placed, dialog, "t"));
            AssertRect(0, 0, 86, 74, placed[dialog]);
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
            // ours: D306 and D312 (the row is 124 wide: two buttons of 48 with margins and a gap of two characters)
            DclTile dialog = Dialog(": list_box { key = \"l\"; width = 40; height = 1; } ok_cancel;");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            DclRect row = placed[dialog.Children[1]];
            DclRect ok = Of(placed, dialog, "accept");
            DclRect cancel = Of(placed, dialog, "cancel");
            AssertRect(76, 37, 124, 29, row);
            AssertRect(80, 40, 48, 23, ok);
            AssertRect(148, 40, 48, 23, cancel);
            Assert.Equal(row.X - 12, 12 + 252 - (row.X + row.Width), 6);
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
            // ours: D306 (35 characters is 210, the column is 218 wide, the row of 124 has room 94: 47 each side)
            DclTile dialog = Dialog("ok_cancel_err;");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            Assert.Equal(210, Of(placed, dialog, "error").Width, 6);
            Assert.Equal(12 + 47 + 4, Of(placed, dialog, "accept").X, 6);
            Assert.Equal(218 + 24, placed[dialog].Width, 6);
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

            AssertRect(16, 15, 35, 14, Of(Arrange(dialog, metrics), dialog, "t"));
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

            // the text 5 characters of 10; the button 2 of 10 and 12 of padding; the edit box 2 of 10, a gap of 6, 5 of 6
            Assert.Equal(50, Of(placed, dialog, "t").Width, 6);
            Assert.Equal(32, Of(placed, dialog, "b").Width, 6);
            Assert.Equal(56, Of(placed, dialog, "e").Width, 6);
        }

        [Fact]
        public void TheDefaultMetrics_AreSixByThirteen()
        {
            DclMetrics metrics = DclMetrics.Default;

            Assert.Equal(6, metrics.CharWidth);
            Assert.Equal(13, metrics.CharHeight);
            Assert.Equal(30, metrics.TextWidth("Hello"));
            Assert.Equal(0, metrics.TextWidth(null));
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

            Assert.Equal(36, DclLayout.LabelWidth(labelled.Find("a"), DclMetrics.Default), 6);
            Assert.Equal(30, DclLayout.LabelWidth(labelled.Find("b"), DclMetrics.Default), 6);
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
            // ours: D311 and D312 (label 5 characters, a gap of 6, 60 characters: 396 wide)
            DclTile dialog = DclParser.Parse(EditDialog, @"C:\dcl\x.dcl", null).NewDialog("EDIT");

            IReadOnlyDictionary<DclTile, DclRect> placed = Arrange(dialog);

            DclRect box = Of(placed, dialog, "txt");
            Assert.Equal(396, box.Width, 6);
            Assert.Equal(396 + 8 + 24, placed[dialog].Width, 6);
            Assert.Equal(
                Of(placed, dialog, "accept").X - 12,
                placed[dialog].Width - 12 - (Of(placed, dialog, "cancel").X + Of(placed, dialog, "cancel").Width),
                6
            );
        }
    }
}
