using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using GeometryHelper.Cad.Dcl;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The WPF dialogs that show DCL dialogs (L5 part W, D334): every tile kind drawn as a control at the rectangle the layout
    /// gives it, the values shown after <c>set_tile</c>, and what a click, typing, Enter, Esc and a double click report to
    /// <c>start_dialog</c>, driven on an STA thread with no one at the keyboard. Rows marked <c>AutoCAD 2021 (GUI g1)</c> are
    /// measured in the full AutoCAD (the sizes, D345), <c>reference</c> are the DCL help's, <c>ours: D3nn</c> the spec's choice.
    /// </summary>
    public class DclDialogWindowTests
    {
        // The dialogs of the tests, in words of their own.
        private const string Kit = """
            ghedit : dialog {
                label = "Edit text"; initial_focus = "text";
                : edit_box { label = "Text:"; key = "text"; edit_width = 60; allow_accept = true; }
                spacer_1;
                ok_cancel;
            }
            ghfields : dialog {
                label = "Fields to filter ";
                : text { label = "Choose the fields to filter ..."; }
                spacer;
                : list_box { key = "lst"; multiple_select = true; }
                spacer;
                ok_cancel;
            }
            ghall : dialog {
                label = "Every kind";
                : row {
                    : column {
                        : edit_box { key = "txt"; label = "&Text:"; edit_width = 20; value = "zz"; }
                        : edit_box { key = "dis"; label = "Disabled:"; edit_width = 8; is_enabled = false; value = "off"; }
                        : popup_list { key = "popn"; label = "Popup:"; edit_width = 12; }
                        : popup_list { key = "pop"; label = "Popup v:"; edit_width = 12; list = "one\ntwo\nthree"; value = "1"; }
                        : toggle { key = "on"; label = "Toggle on"; value = "1"; }
                        : toggle { key = "off"; label = "Toggle off"; }
                        : boxed_radio_row {
                            key = "rr"; label = "Radio";
                            : radio_button { key = "one"; label = "One"; }
                            : radio_button { key = "two"; label = "Two"; value = "1"; }
                        }
                        : slider { key = "sl"; min_value = 0; max_value = 100; value = 40; width = 25; }
                        : slider { key = "sl2"; min_value = 0; max_value = 100; width = 25; }
                        : slider { key = "slv"; layout = vertical; min_value = 0; max_value = 10; value = 3; height = 4; fixed_width = true; }
                        : text { key = "tx"; label = "Changed"; }
                        : text { key = "tx2"; label = "Value"; }
                        : text { key = "bold"; label = "Bold text"; is_bold = true; }
                    }
                    : boxed_column {
                        label = "Lists and images";
                        : list_box { key = "lst"; label = "List:"; list = "a\nb\nc\nd\ne"; width = 20; height = 6; multiple_select = true; }
                        : list_box { key = "tab"; list = "x\ty\nz\tw"; tabs = "0 10"; width = 20; height = 4; }
                        : image { key = "img"; width = 10; height = 4; color = 2; }
                        : image { key = "img2"; width = 10; aspect_ratio = 0.5; color = 3; }
                        : image_button { key = "ib"; width = 6; height = 2; color = 2; }
                        : button { key = "btn"; label = "&Button"; }
                        : button { key = "bdis"; label = "Disabled"; is_enabled = false; }
                    }
                }
                ok_cancel_help_errtile;
            }
            ghlist : dialog {
                label = "List";
                : list_box { key = "lst"; label = "List:"; list = "a\nb\nc"; width = 20; height = 6; }
                ok_only;
            }
            ghsizes : dialog {
                label = "Sizes";
                : button { key = "btn"; label = "&Button"; }
                : edit_box { key = "txt"; label = "Text:"; edit_width = 20; }
                : popup_list { key = "pop"; label = "Popup:"; edit_width = 12; list = "one\ntwo"; }
                : toggle { key = "on"; label = "Toggle"; }
                : radio_button { key = "one"; label = "One"; }
                : text { key = "tx"; label = "Text"; }
                : slider { key = "sl"; width = 25; }
                : slider { key = "slv"; layout = vertical; height = 4; fixed_width = true; }
                : list_box { key = "lst"; label = "List:"; list = "a\nb\nc"; width = 20; height = 6; }
                ok_cancel;
            }
            ghradios : dialog {
                label = "Radios";
                : radio_column { key = "ra"; : radio_button { key = "a1"; label = "A1"; } : radio_button { key = "a2"; label = "A2"; } }
                : radio_column { key = "rb"; : radio_button { key = "b1"; label = "B1"; } : radio_button { key = "b2"; label = "B2"; } }
                ok_cancel;
            }
            ghplain : dialog {
                label = "Plain";
                : edit_box { key = "eb"; label = "Name:"; edit_width = 10; }
                : edit_box { key = "pw"; label = "Pass:"; edit_width = 10; password_char = "*"; }
                : button { key = "b1"; label = "One"; }
                ok_cancel;
            }
            """;

        private static DclDialog Make(string name, string path = null)
        {
            DclFile file = DclParser.Parse(Kit, path ?? @"C:\dcl\kit.dcl", null);
            DclDialog dialog = new DclDialog(file.NewDialog(name), 1, name, null);
            dialog.Initialise();
            dialog.ScreenX = 30000;
            dialog.ScreenY = 30000;
            return dialog;
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;
                    test();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static void Lay(DclDialogWindow window)
        {
            window.Chrome.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            window.Chrome.Arrange(new Rect(window.Chrome.DesiredSize));
            window.Chrome.UpdateLayout();
        }

        private static T Find<T>(DclDialogWindow window, string key)
            where T : DependencyObject
        {
            DclTileView view = window.ViewOf(key);
            Assert.NotNull(view);
            var found = view.FocusTarget as T ?? view.Element as T;
            Assert.NotNull(found);
            return found;
        }

        // The events a window reports while a test acts on it.
        private static List<DclEvent> Listen(DclDialogWindow window)
        {
            var events = new List<DclEvent>();
            window.Raised += events.Add;
            return events;
        }

        private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        // ---- sizes and places ----

        [Fact]
        public void TheEditDialog_HasOkFortyNineAndCancelFiftyFive_TwentyEightApart()
        {
            // AutoCAD 2021 (GUI g1): OK 49 by 22, Cancel 55 by 22, 28 between them (D345), measured with the true text widths
            DclDialog dialog = Make("ghedit");
            DclMetrics metrics = DclDialogWindow.CreateMetrics();

            IReadOnlyDictionary<DclTile, DclRect> placed = DclLayout.Arrange(dialog.Root, metrics);

            DclRect ok = placed[dialog.Find("accept")];
            DclRect cancel = placed[dialog.Find("cancel")];
            Assert.Equal(49, ok.Width, 6);
            Assert.Equal(22, ok.Height, 6);
            Assert.Equal(55, cancel.Width, 6);
            Assert.Equal(22, cancel.Height, 6);
            Assert.Equal(28, cancel.X - (ok.X + ok.Width), 6);
        }

        [Fact]
        public void TheEditDialog_HasTheBoxThreeHundredSeventyTwoWide_StartingWhereItsLabelEnds()
        {
            // AutoCAD 2021 (GUI g1): the label "Text:" is 31 wide and the box of 60 characters 372, from where the label ends
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghedit"));
                Lay(window);

                var box = (Border)Find<TextBox>(window, "text").Parent;
                var tile = (Grid)window.ViewOf("text").Element;
                Assert.Equal(31, tile.ColumnDefinitions[0].Width.Value, 6);
                Assert.Equal(372, box.ActualWidth, 6);
                Assert.Equal(19, box.ActualHeight, 6);
                Assert.Equal(31 + 372, tile.ActualWidth, 6);
            });
        }

        [Fact]
        public void TheEditDialog_PutsTheWindowAtTheWidthOfAutoCads_AndTheButtonsOnTheBoardsSizes()
        {
            // AutoCAD 2021 (GUI g1): the window is 433 wide, the edit tile at 16 from its left, OK 49 and Cancel 55 by 22
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghedit"));
                Lay(window);

                Assert.Equal(433, window.Chrome.ActualWidth, 6);
                var ok = Find<Button>(window, "accept");
                var cancel = Find<Button>(window, "cancel");
                Assert.Equal(49, ok.Width, 6);
                Assert.Equal(22, ok.Height, 6);
                Assert.Equal(55, cancel.Width, 6);
                Point tile = window.ViewOf("text").Element.TranslatePoint(new Point(0, 0), window.Chrome);
                Assert.Equal(16, tile.X, 6);
            });
        }

        [Fact]
        public void EveryTileKind_HasAControlAtItsLayoutRectangle()
        {
            // ours: D334 (a control per kind, each at the rectangle of DclLayout with the corner on whole pixels)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                IReadOnlyDictionary<DclTile, DclRect> placed = DclLayout.Arrange(dialog.Root, window.Metrics);
                Lay(window);

                var kinds = new Dictionary<string, Type>
                {
                    ["txt"] = typeof(Grid),
                    ["dis"] = typeof(Grid),
                    ["pop"] = typeof(Grid),
                    ["on"] = typeof(CheckBox),
                    ["off"] = typeof(CheckBox),
                    ["one"] = typeof(RadioButton),
                    ["two"] = typeof(RadioButton),
                    ["sl"] = typeof(Slider),
                    ["slv"] = typeof(Slider),
                    ["tx"] = typeof(TextBlock),
                    ["bold"] = typeof(TextBlock),
                    ["lst"] = typeof(Grid),
                    ["tab"] = typeof(Grid),
                    ["img"] = typeof(DclPictureView),
                    ["ib"] = typeof(Button),
                    ["btn"] = typeof(Button),
                    ["bdis"] = typeof(Button),
                    ["accept"] = typeof(Button),
                    ["cancel"] = typeof(Button),
                    ["help"] = typeof(Button),
                    ["error"] = typeof(TextBlock),
                    ["rr"] = typeof(Canvas),
                };

                foreach (KeyValuePair<string, Type> kind in kinds)
                {
                    DclTileView view = window.ViewOf(kind.Key);
                    Assert.True(view != null, kind.Key + " has a control");
                    Assert.IsType(kind.Value, view.Element);
                    DclRect rect = placed[dialog.Find(kind.Key)];
                    Assert.Equal(Math.Floor(rect.X), Canvas.GetLeft(view.Element), 6);
                    Assert.Equal(Math.Floor(rect.Y), Canvas.GetTop(view.Element), 6);
                }

                // the clusters that are not boxed and the spacers draw nothing: only the tiles above have views
                int visible = dialog
                    .Root.Descendants()
                    .Count(tile =>
                        tile.Kind != "spacer"
                        && !((tile.Kind == "cluster" || tile.Kind == "radio_cluster") && !tile.IsTrue("boxed"))
                    );
                Assert.Equal(visible, window.Views.Count);
            });
        }

        [Fact]
        public void TheHeightsOfTheControls_AreAutoCadsMeasuredOnes()
        {
            // AutoCAD 2021 (GUI g1): button 22, edit 19, popup 21, toggle 15, radio 19 (a boxed radio row 48), text 13, slider 17
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghsizes"));
                Lay(window);

                Assert.Equal(22, Find<Button>(window, "btn").ActualHeight, 6);
                Assert.Equal(19, ((Border)Find<TextBox>(window, "txt").Parent).ActualHeight, 6);
                Assert.Equal(21, Find<ComboBox>(window, "pop").ActualHeight, 6);
                Assert.Equal(15, Find<CheckBox>(window, "on").ActualHeight, 6);
                Assert.Equal(19, Find<RadioButton>(window, "one").ActualHeight, 6);
                Assert.Equal(13, window.ViewOf("tx").Element.ActualHeight, 6);
                Assert.Equal(17, Find<Slider>(window, "sl").ActualHeight, 6);
                Assert.Equal(17, Find<Slider>(window, "slv").ActualWidth, 6);
                Assert.Equal(13, window.Metrics.CharHeight, 6);
                Assert.Equal(6, window.Metrics.CharWidth, 6);
            });
        }

        [Fact]
        public void AListBoxOfWidthTwenty_IsOneHundredTwentyWide_WithItsLabelInsideItsHeight()
        {
            // AutoCAD 2021 (GUI g1): a list box width = 20 is 120 wide; height 6 with a label is a tile of 68, the list 54
            OnSta(() =>
            {
                DclDialog dialog = Make("ghlist");
                var window = new DclDialogWindow(dialog);
                Lay(window);

                var tile = (Grid)window.ViewOf("lst").Element;
                var list = Find<ListBox>(window, "lst");
                Assert.Equal(120, list.ActualWidth, 6);
                Assert.Equal(68, tile.ActualHeight, 6);
                Assert.Equal(54, list.ActualHeight, 6);
            });
        }

        // ---- the values shown ----

        [Fact]
        public void TheValuesOfTheTiles_AreShownAtOnce_AndAfterSetTile()
        {
            // AutoCAD 2021 (GUI g1): the values the DCL gave (zz, off, popup "1", toggles, radio value) are what the tiles show
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                window.Refresh();

                Assert.Equal("zz", Find<TextBox>(window, "txt").Text);
                Assert.Equal("off", Find<TextBox>(window, "dis").Text);
                Assert.False(Find<TextBox>(window, "dis").IsEnabled);
                Assert.Equal(1, Find<ComboBox>(window, "pop").SelectedIndex);
                Assert.Equal(
                    new object[] { "one", "two", "three" },
                    Find<ComboBox>(window, "pop").Items.Cast<object>()
                );
                Assert.True(Find<CheckBox>(window, "on").IsChecked);
                Assert.False(Find<CheckBox>(window, "off").IsChecked);
                Assert.False(Find<RadioButton>(window, "one").IsChecked);
                Assert.True(Find<RadioButton>(window, "two").IsChecked);
                Assert.Equal(40, Find<Slider>(window, "sl").Value, 6);
                Assert.Equal(3, Find<Slider>(window, "slv").Value, 6);
                Assert.Equal("Changed", ((TextBlock)window.ViewOf("tx").Element).Text);
                Assert.Equal(5, Find<ListBox>(window, "lst").Items.Count);
                Assert.False(Find<Button>(window, "bdis").IsEnabled);

                // what a routine's set_tile, mode_tile and start_list do
                dialog.SetValue(dialog.Find("txt"), "typed");
                dialog.SetValue(dialog.Find("pop"), "2");
                dialog.SetValue(dialog.Find("off"), "1");
                dialog.SetValue(dialog.Find("one"), "1");
                dialog.SetValue(dialog.Find("sl"), "75");
                dialog.SetValue(dialog.Find("tx"), "Now this");
                dialog.Find("lst").Value = "1 3";
                dialog.Find("btn").IsEnabled = false;
                dialog.Find("dis").IsEnabled = true;
                dialog.Find("tab").Items.AddRange(new[] { "p\tq" });
                window.Refresh();

                Assert.Equal("typed", Find<TextBox>(window, "txt").Text);
                Assert.Equal(2, Find<ComboBox>(window, "pop").SelectedIndex);
                Assert.True(Find<CheckBox>(window, "off").IsChecked);
                Assert.True(Find<RadioButton>(window, "one").IsChecked);
                Assert.False(Find<RadioButton>(window, "two").IsChecked);
                Assert.Equal(75, Find<Slider>(window, "sl").Value, 6);
                Assert.Equal("Now this", ((TextBlock)window.ViewOf("tx").Element).Text);
                Assert.Equal(
                    new[] { "b", "d" },
                    Find<ListBox>(window, "lst").SelectedItems.Cast<FrameworkElement>().Select(row => (string)row.Tag)
                );
                Assert.False(Find<Button>(window, "btn").IsEnabled);
                Assert.True(Find<TextBox>(window, "dis").IsEnabled);
                Assert.Equal(3, Find<ListBox>(window, "tab").Items.Count);
            });
        }

        [Fact]
        public void RefreshingTheValues_ReportsNothing()
        {
            // ours: D334 (a value set by the routine is no act of the user)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                List<DclEvent> events = Listen(window);
                window.Refresh();
                dialog.SetValue(dialog.Find("pop"), "0");
                dialog.SetValue(dialog.Find("on"), "0");
                dialog.SetValue(dialog.Find("sl"), "10");
                dialog.Find("lst").Value = "2";
                window.Refresh();

                Assert.Empty(events);
                Assert.Null(dialog.PendingEditKey);
            });
        }

        [Fact]
        public void ABoxedListWithTabs_PutsItsCellsAtTheStops()
        {
            // ours: D334 (tabs: the cell after a tab starts at the next stop in character widths; a first stop of 0 is the start)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                Lay(window);

                var list = Find<ListBox>(window, "tab");
                var row = (Canvas)list.Items[0];
                var cells = row.Children.Cast<TextBlock>().ToArray();
                Assert.Equal(2, cells.Length);
                Assert.Equal(0, Canvas.GetLeft(cells[0]), 6);
                Assert.Equal(60, Canvas.GetLeft(cells[1]), 6);
                Assert.Equal("x", cells[0].Text);
                Assert.Equal("y", cells[1].Text);
            });
        }

        [Fact]
        public void APasswordEdit_IsAPasswordBox_AndAnEditBoxATextBox()
        {
            // reference: password_char; ours: D336 (WPF's bullet, not the glyph asked for)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghplain");
                dialog.Find("pw").Value = "secret";
                var window = new DclDialogWindow(dialog);
                window.Refresh();

                Assert.IsType<PasswordBox>(window.ViewOf("pw").FocusTarget);
                Assert.Equal("secret", ((PasswordBox)window.ViewOf("pw").FocusTarget).Password);
                Assert.IsType<TextBox>(window.ViewOf("eb").FocusTarget);
                Assert.Equal(132, ((TextBox)window.ViewOf("eb").FocusTarget).MaxLength);
            });
        }

        [Fact]
        public void TheEditLimit_IsTheMostThatCanBeTyped()
        {
            // reference: edit_limit (default 132)
            OnSta(() =>
            {
                DclFile file = DclParser.Parse(
                    "lim : dialog { label = \"L\"; : edit_box { key = \"e\"; label = \"E:\"; edit_limit = 5; } ok_only; }",
                    @"C:\dcl\lim.dcl",
                    null
                );
                var dialog = new DclDialog(file.NewDialog("lim"), 1, "lim", null);
                dialog.Initialise();
                var window = new DclDialogWindow(dialog);

                Assert.Equal(5, Find<TextBox>(window, "e").MaxLength);
            });
        }

        // ---- radio groups and access keys ----

        [Fact]
        public void TheRadioButtonsOfACluster_ShareAGroup_AndTwoClustersDoNot()
        {
            // reference: a radio cluster chooses one button; ours: D334 (one GroupName per radio cluster)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghradios");
                var window = new DclDialogWindow(dialog);
                var a1 = Find<RadioButton>(window, "a1");
                var a2 = Find<RadioButton>(window, "a2");
                var b1 = Find<RadioButton>(window, "b1");
                var b2 = Find<RadioButton>(window, "b2");

                Assert.Equal(a1.GroupName, a2.GroupName);
                Assert.Equal(b1.GroupName, b2.GroupName);
                Assert.NotEqual(a1.GroupName, b1.GroupName);

                dialog.SetValue(dialog.Find("a2"), "1");
                dialog.SetValue(dialog.Find("b1"), "1");
                window.Refresh();

                Assert.True(a2.IsChecked);
                Assert.False(a1.IsChecked);
                Assert.True(b1.IsChecked);
                Assert.False(b2.IsChecked);

                dialog.SetValue(dialog.Find("ra"), "a1");
                window.Refresh();

                Assert.True(a1.IsChecked);
                Assert.False(a2.IsChecked);
                Assert.True(b1.IsChecked);
            });
        }

        [Fact]
        public void AnAmpersandInALabel_IsAnAccessKey_ShownAsWpfsUnderscore()
        {
            // reference: "&" in a label marks the access key (D334); the label of an edit box gives its box the focus
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);

                Assert.Equal("_Button", Find<Button>(window, "btn").Content);
                Assert.Equal("_Help", Find<Button>(window, "help").Content);
                var label = ((Grid)window.ViewOf("txt").Element).Children.OfType<Label>().Single();
                Assert.Equal("_Text:", label.Content);
                Assert.Same(Find<TextBox>(window, "txt"), label.Target);
                Assert.Null(((Grid)window.ViewOf("dis").Element).Children.OfType<Label>().Single().Target);
            });
        }

        [Theory]
        [InlineData("&Text:", "_Text:")]
        [InlineData("Save && quit", "Save & quit")]
        [InlineData("a_b", "a__b")]
        [InlineData("&a_b", "_a__b")]
        [InlineData("Plain", "Plain")]
        [InlineData("Dangling &", "Dangling ")]
        public void ALabelIsReadAsWpfReadsAnAccessKey(string label, string shown)
        {
            // reference: "&" marks the access key and "&&" is one ampersand; ours: D334 (a literal "_" doubled for WPF)
            DclFile file = DclParser.Parse(
                "t : dialog { label = \"T\"; : button { key = \"b\"; label = \"" + label + "\"; } ok_only; }",
                @"C:\dcl\t.dcl",
                null
            );
            DclTile tile = file.NewDialog("t").Find("b");

            Assert.Equal(shown, DclTileView.AccessLabel(tile));
            Assert.Equal(
                label.Contains("&") && !label.Contains("&&") && !label.EndsWith("&"),
                DclTileView.HasAccessKey(tile)
            );
        }

        [Fact]
        public void AMnemonic_UnderlinesTheLetterOfALabelThatHasNoAmpersand()
        {
            // reference: mnemonic names the access key's letter
            DclFile file = DclParser.Parse(
                "t : dialog { label = \"T\"; : button { key = \"b\"; label = \"Apply\"; mnemonic = \"p\"; } ok_only; }",
                @"C:\dcl\t.dcl",
                null
            );

            Assert.Equal("A_pply", DclTileView.AccessLabel(file.NewDialog("t").Find("b")));
        }

        // ---- the acts of the user ----

        [Fact]
        public void AClickOnAButton_ReportsItsKeyAndThePoint()
        {
            // AutoCAD 2021 (GUI g1): a button's action sees $value "1", $reason 1 and the click point (centre where the key was used)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghplain");
                var window = new DclDialogWindow(dialog);
                Lay(window);
                List<DclEvent> events = Listen(window);

                Click(Find<Button>(window, "b1"));

                DclEvent clicked = Assert.Single(events);
                Assert.Equal(DclEventKind.Tile, clicked.Kind);
                Assert.Equal("b1", clicked.Key);
                Assert.Equal("1", clicked.Value);
                Assert.Equal(1, clicked.Reason);
                var button = Find<Button>(window, "b1");
                Assert.Equal((int)Math.Floor(button.ActualWidth / 2), clicked.X);
                Assert.Equal((int)Math.Floor(button.ActualHeight / 2), clicked.Y);
            });
        }

        [Fact]
        public void AToggleClicked_ReportsItsNewValue_AndPutsItIntoTheDialog()
        {
            // AutoCAD 2021 (GUI g3): a toggle's action sees ("tg" "1" 1)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);
                var off = Find<CheckBox>(window, "off");

                off.IsChecked = true;
                Click(off);

                DclEvent clicked = Assert.Single(events);
                Assert.Equal("off", clicked.Key);
                Assert.Equal("1", clicked.Value);
                Assert.Equal(1, clicked.Reason);
                Assert.Equal("1", dialog.Find("off").Value);

                off.IsChecked = false;
                Click(off);
                Assert.Equal("0", events[1].Value);
                Assert.Equal("0", dialog.Find("off").Value);
            });
        }

        [Fact]
        public void ARadioButtonClicked_ReportsItself_AndClearsTheOthersOfItsClusterInTheDialog()
        {
            // AutoCAD 2021 (GUI g3): a radio button with its own action reports itself; reference: one of a cluster is chosen
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);

                Click(Find<RadioButton>(window, "one"));

                DclEvent clicked = Assert.Single(events);
                Assert.Equal("one", clicked.Key);
                Assert.Equal("1", clicked.Value);
                Assert.Equal("1", dialog.Find("one").Value);
                Assert.Equal("0", dialog.Find("two").Value);
            });
        }

        [Fact]
        public void TypingInAnEditBox_PutsTheTextIntoTheDialogAndMarksItPending_WithNoEventYet()
        {
            // AutoCAD 2021 (GUI g1, G8b): the action of an edit box that was typed in waits for the focus to leave (D340)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghplain");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);

                Find<TextBox>(window, "eb").Text = "hey";

                Assert.Empty(events);
                Assert.Equal("hey", dialog.Find("eb").Value);
                Assert.Equal("eb", dialog.PendingEditKey);
            });
        }

        [Fact]
        public void TheFocusLeavingAnEditBoxThatWasTypedIn_ReportsReasonTwo()
        {
            // AutoCAD 2021 (GUI g3): an edit box left by Tab gives ("eb" "hey" 2)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghplain");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);
                var edit = Find<TextBox>(window, "eb");
                var other = Find<Button>(window, "b1");
                edit.Text = "hey";

                edit.RaiseEvent(
                    new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, edit, other)
                    {
                        RoutedEvent = Keyboard.LostKeyboardFocusEvent,
                    }
                );

                DclEvent left = Assert.Single(events);
                Assert.Equal(DclEventKind.Tile, left.Kind);
                Assert.Equal("eb", left.Key);
                Assert.Equal("hey", left.Value);
                Assert.Equal(2, left.Reason);
                Assert.Null(dialog.PendingEditKey);
            });
        }

        [Fact]
        public void TheFocusLeavingAnEditBoxThatWasNotTypedIn_ReportsNothing()
        {
            // ours: D340 (no change, no action)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghplain");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);
                var edit = Find<TextBox>(window, "eb");

                edit.RaiseEvent(
                    new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, edit, Find<Button>(window, "b1"))
                    {
                        RoutedEvent = Keyboard.LostKeyboardFocusEvent,
                    }
                );

                Assert.Empty(events);
            });
        }

        [Fact]
        public void AWindowThatLosesTheKeyboardAltogether_IsNotAnEditBoxLeft()
        {
            // ours: D340 (focus moving to nothing, as when the window is deactivated, owes no action; the close reports it)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghplain");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);
                var edit = Find<TextBox>(window, "eb");
                edit.Text = "hey";

                edit.RaiseEvent(
                    new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, edit, null)
                    {
                        RoutedEvent = Keyboard.LostKeyboardFocusEvent,
                    }
                );

                Assert.Empty(events);
                Assert.Equal("eb", dialog.PendingEditKey);
            });
        }

        private static void Press(DclDialogWindow window, Key key)
        {
            using (var source = new HwndSource(new HwndSourceParameters("DclTest")))
            {
                window.RaiseEvent(
                    new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent,
                    }
                );
            }
        }

        [Fact]
        public void EnterInAnEditBox_ReportsEnterWithItsKey()
        {
            // AutoCAD 2021 (GUI g1, G8): Enter in an edit box is an act of that box (its action with $reason 1, or the default tile)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghedit");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);
                window.NoteFocus(window.ViewOf("text"));

                Press(window, Key.Enter);

                DclEvent enter = Assert.Single(events);
                Assert.Equal(DclEventKind.Enter, enter.Kind);
                Assert.Equal("text", enter.Key);
            });
        }

        [Fact]
        public void EnterWithNothingFocused_ReportsEnterWithNoKey()
        {
            // reference: Enter presses the default tile
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghedit"));
                List<DclEvent> events = Listen(window);

                Press(window, Key.Enter);

                DclEvent enter = Assert.Single(events);
                Assert.Equal(DclEventKind.Enter, enter.Kind);
                Assert.Null(enter.Key);
            });
        }

        [Fact]
        public void EscapeAndTheCloseBox_ReportEscape()
        {
            // AutoCAD 2021 (GUI g1): Esc and the close box press the cancel tile (D324)
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghedit"));
                List<DclEvent> events = Listen(window);

                Press(window, Key.Escape);
                window.Close();

                Assert.Equal(2, events.Count);
                Assert.All(events, e => Assert.Equal(DclEventKind.Escape, e.Kind));
                Assert.False(window.IsReleased);
            });
        }

        [Fact]
        public void AWindowTheHostReleases_ReportsNothingWhenItCloses()
        {
            // ours: D334 (only the user's closing is an act)
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghedit"));
                List<DclEvent> events = Listen(window);

                window.Release();

                Assert.Empty(events);
                Assert.True(window.IsReleased);
            });
        }

        [Fact]
        public void AListChosenIn_ReportsTheIndices_AndADoubleClickReasonFour()
        {
            // reference: $reason 1 a pick and 4 a double click in a list box (D328); the value is the indices, ascending
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                Lay(window);
                List<DclEvent> events = Listen(window);
                var list = Find<ListBox>(window, "lst");

                list.SelectedItems.Add(list.Items[3]);
                list.SelectedItems.Add(list.Items[1]);

                Assert.Equal(2, events.Count);
                Assert.Equal("lst", events[1].Key);
                Assert.Equal("1 3", events[1].Value);
                Assert.Equal(1, events[1].Reason);
                Assert.Equal("1 3", dialog.Find("lst").Value);

                var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
                list.RaiseEvent(
                    new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = Control.MouseDoubleClickEvent,
                        Source = row,
                    }
                );

                DclEvent doubled = events[events.Count - 1];
                Assert.Equal(4, doubled.Reason);
                Assert.Equal("1 3", doubled.Value);
            });
        }

        [Fact]
        public void APopupChosenIn_ReportsItsIndex()
        {
            // reference: a popup list's value is the index chosen
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);

                Find<ComboBox>(window, "pop").SelectedIndex = 2;

                DclEvent chosen = Assert.Single(events);
                Assert.Equal("pop", chosen.Key);
                Assert.Equal("2", chosen.Value);
                Assert.Equal(1, chosen.Reason);
                Assert.Equal("2", dialog.Find("pop").Value);
            });
        }

        [Fact]
        public void ASliderMoved_ReportsItsIntegerValue()
        {
            // reference: $reason 1 a slider picked; the value is an integer within min_value and max_value
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                List<DclEvent> events = Listen(window);

                Find<Slider>(window, "sl").Value = 62;

                DclEvent moved = Assert.Single(events);
                Assert.Equal("sl", moved.Key);
                Assert.Equal("62", moved.Value);
                Assert.Equal(1, moved.Reason);
                Assert.Equal("62", dialog.Find("sl").Value);
            });
        }

        [Fact]
        public void AnImageButtonClicked_ReportsItsPixelsAndADoubleClickReasonFour()
        {
            // reference: $x $y the point of a click in an image button, in its pixels; $reason 4 a double click
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghall"));
                window.Refresh();
                Lay(window);
                List<DclEvent> events = Listen(window);
                var button = Find<Button>(window, "ib");

                Click(button);
                button.RaiseEvent(
                    new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = Control.MouseDoubleClickEvent,
                    }
                );

                Assert.Equal(2, events.Count);
                Assert.Equal("ib", events[0].Key);
                Assert.Equal(1, events[0].Reason);
                Assert.Equal(4, events[1].Reason);
            });
        }

        // ---- images ----

        [Fact]
        public void AnImage_DrawsTheOperationsOfItsPicture_AndTheColoursOfDcl()
        {
            // reference: fill_image and vector_image in ACI colours and the DCL colours -2, -15, -16, -18 (D320)
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                dialog.Find("img").Picture.Add(new DclImageOp(DclImageOpKind.Fill, 0, 0, 60, 52, 2));
                dialog.Find("img").Picture.Add(new DclImageOp(DclImageOpKind.Vector, 0, 0, 10, 10, 1));
                var window = new DclDialogWindow(dialog);
                window.Refresh();

                var picture = (DclPictureView)window.ViewOf("img").Element;
                Assert.Equal(2, picture.Operations.Count);
                Assert.Equal(DclImageOpKind.Vector, picture.Operations[1].Kind);
                Assert.Equal(0xFFFF00, DclPictureView.ColorOf(2));
                Assert.Equal(0xFF0000, DclPictureView.ColorOf(1));
                Assert.Equal(0x000000, DclPictureView.ColorOf(-2));
                Assert.Equal(0x262B33, DclPictureView.ColorOf(-15));
                Assert.Equal(0xE6E9ED, DclPictureView.ColorOf(-16));
                Assert.Equal(0x3A414C, DclPictureView.ColorOf(-18));
                Assert.Equal(0xFFFFFF, DclPictureView.ColorOf("white", 0));
                Assert.Equal(0x262B33, DclPictureView.ColorOf("dialog_background", 0));
                Assert.Equal(0x14181D, DclPictureView.ColorOf("nonsense", 0x14181D));
            });
        }

        [Fact]
        public void AFlippedImageButton_IsDrawnFlipped()
        {
            // reference: mode_tile 4 flips the highlight of an image button
            OnSta(() =>
            {
                DclDialog dialog = Make("ghall");
                var window = new DclDialogWindow(dialog);
                window.Refresh();
                var picture = (DclPictureView)((Button)window.ViewOf("ib").Element).Content;
                Assert.False(picture.IsFlipped);

                dialog.Find("ib").IsFlipped = true;
                window.Refresh();

                Assert.True(picture.IsFlipped);
            });
        }

        // ---- focus, the host and nested dialogs ----

        [Fact]
        public void InitialFocus_NamesTheTileThatTakesTheFocusWhenTheWindowIsShown_AndModeTileTwoMovesIt()
        {
            // reference: initial_focus and mode_tile 2 give a tile the keyboard focus (D334)
            OnSta(() =>
            {
                var host = new DclWindowHost(() => null);
                DclDialog edit = Make("ghedit");
                DclDialog fields = Make("ghfields");
                host.Open(edit);
                host.Open(fields);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                DclDialogWindow editWindow = host.WindowOf(edit);
                DclDialogWindow fieldsWindow = host.WindowOf(fields);

                // initial_focus names the edit box, its text selected; with none the first tile that takes the Tab key is it
                Assert.Same(Find<TextBox>(editWindow, "text"), FocusManager.GetFocusedElement(editWindow));
                Assert.Equal("text", edit.FocusKey);
                Assert.Same(Find<ListBox>(fieldsWindow, "lst"), FocusManager.GetFocusedElement(fieldsWindow));

                // mode_tile 2 moves it
                edit.FocusKey = "cancel";
                editWindow.Refresh();

                Assert.Same(Find<Button>(editWindow, "cancel"), FocusManager.GetFocusedElement(editWindow));
                host.Close(fields);
                host.Close(edit);
            });
        }

        [Fact]
        public void TheHostOpensTheWindowOverTheMainOne_SwitchesItOff_AndGivesItBackOnClose()
        {
            // reference: a dialog takes the input while it is shown (D334): the main window is disabled, then enabled again
            OnSta(() =>
            {
                var main = new Window
                {
                    Width = 400,
                    Height = 300,
                    Left = 30100,
                    Top = 30100,
                    ShowInTaskbar = false,
                };
                main.Show();
                var host = new DclWindowHost(() => main);
                DclDialog dialog = Make("ghedit");

                host.Open(dialog);
                DclDialogWindow window = host.WindowOf(dialog);

                Assert.NotNull(window);
                Assert.True(window.IsVisible);
                Assert.Same(main, window.Owner);
                Assert.False(main.IsEnabled);
                Assert.False(window.ShowInTaskbar);
                Assert.Equal("Edit text", window.Title);

                host.Close(dialog);

                Assert.True(main.IsEnabled);
                Assert.False(window.IsVisible);
                Assert.Null(host.WindowOf(dialog));
                main.Close();
            });
        }

        [Fact]
        public void TheHostCentresTheWindowOverTheMainOne_UnlessTheDialogWasGivenAPoint()
        {
            // reference: screen-pt '(-1 -1) centres the dialog over the window; a point places it (D316)
            OnSta(() =>
            {
                var main = new Window
                {
                    Width = 800,
                    Height = 600,
                    Left = 30050,
                    Top = 30040,
                    ShowInTaskbar = false,
                };
                main.Show();
                var host = new DclWindowHost(() => main);
                DclDialog centred = Make("ghedit");
                centred.ScreenX = null;
                centred.ScreenY = null;
                DclDialog placed = Make("ghedit");
                placed.ScreenX = 30120;
                placed.ScreenY = 30090;

                host.Open(centred);
                DclDialogWindow first = host.WindowOf(centred);
                double middle = first.Left + first.ActualWidth / 2;
                Assert.Equal(main.Left + main.ActualWidth / 2, middle, 0);
                host.Close(centred);

                host.Open(placed);
                DclDialogWindow second = host.WindowOf(placed);
                Assert.Equal(30120, second.PointToScreen(new Point(0, 0)).X, 0);
                Assert.Equal(30090, second.PointToScreen(new Point(0, 0)).Y, 0);
                Assert.Equal(30120, placed.PositionX);
                Assert.Equal(30090, placed.PositionY);
                host.Close(placed);
                main.Close();
            });
        }

        [Fact]
        public void ANestedDialog_IsOverItsParent_WhichTakesNoInputUntilTheChildEnds()
        {
            // reference: a child dialog opens over its parent, which stays shown and takes no input until the child ends (D326)
            OnSta(() =>
            {
                var main = new Window
                {
                    Width = 400,
                    Height = 300,
                    Left = 30100,
                    Top = 30100,
                    ShowInTaskbar = false,
                };
                main.Show();
                var host = new DclWindowHost(() => main);
                DclDialog parent = Make("ghedit");
                host.Open(parent);
                var child = new DclDialog(
                    DclParser.Parse(Kit, @"C:\dcl\kit.dcl", null).NewDialog("ghplain"),
                    1,
                    "ghplain",
                    parent
                );
                child.Initialise();

                host.Open(child);

                DclDialogWindow parentWindow = host.WindowOf(parent);
                DclDialogWindow childWindow = host.WindowOf(child);
                Assert.Same(parentWindow, childWindow.Owner);
                Assert.False(parentWindow.IsEnabled);
                Assert.True(childWindow.IsEnabled);
                Assert.Equal(new[] { parentWindow, childWindow }, host.Windows);

                host.Close(child);

                Assert.True(parentWindow.IsEnabled);
                Assert.False(main.IsEnabled);

                host.Close(parent);

                Assert.True(main.IsEnabled);
                main.Close();
            });
        }

        [Fact]
        public void NextWaitsForTheUser_AndGivesTheirActAfterShowingTheValues()
        {
            // ours: D330 (Next refreshes every tile from the dialog, then pushes a dispatcher frame until the user acts)
            OnSta(() =>
            {
                var host = new DclWindowHost(() => null);
                DclDialog dialog = Make("ghedit");
                host.Open(dialog);
                DclDialogWindow window = host.WindowOf(dialog);
                dialog.SetValue(dialog.Find("text"), "OLD");
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        // by then the value the routine set is on the screen
                        Assert.Equal("OLD", Find<TextBox>(window, "text").Text);
                        Click(Find<Button>(window, "cancel"));
                    }),
                    DispatcherPriority.Background
                );

                DclEvent next = host.Next(dialog);

                Assert.Equal(DclEventKind.Tile, next.Kind);
                Assert.Equal("cancel", next.Key);
                host.Close(dialog);
            });
        }

        [Fact]
        public void TwoActsBeforeNext_AreGivenOneAtATime_InTheirOrder()
        {
            // ours: D330 (an act while the routine's action runs is kept for the next Next)
            OnSta(() =>
            {
                var host = new DclWindowHost(() => null);
                DclDialog dialog = Make("ghplain");
                host.Open(dialog);
                DclDialogWindow window = host.WindowOf(dialog);
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        Click(Find<Button>(window, "b1"));
                        Click(Find<Button>(window, "accept"));
                    }),
                    DispatcherPriority.Background
                );

                DclEvent first = host.Next(dialog);
                DclEvent second = host.Next(dialog);

                Assert.Equal("b1", first.Key);
                Assert.Equal("accept", second.Key);
                host.Close(dialog);
            });
        }

        [Fact]
        public void AWindowClosedByTheSystem_GivesEscapeFromNext()
        {
            // ours: D334 (the close of the window is Esc, and Next never waits on a window that is gone)
            OnSta(() =>
            {
                var host = new DclWindowHost(() => null);
                DclDialog dialog = Make("ghplain");
                host.Open(dialog);
                DclDialogWindow window = host.WindowOf(dialog);
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() => window.Close()),
                    DispatcherPriority.Background
                );

                DclEvent next = host.Next(dialog);

                Assert.Equal(DclEventKind.Escape, next.Kind);
                host.Close(dialog);
            });
        }

        // ---- start_dialog end to end ----

        private static string WriteKit(string folder)
        {
            string path = System.IO.Path.Combine(folder, "kit.dcl");
            File.WriteAllText(path, Kit, new UTF8Encoding(false));
            return path.Replace('\\', '/');
        }

        private static CadCommandSession SessionWith(DclWindowHost host)
        {
            var session = new CadCommandSession(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"));
            session.DclHost = host;
            return session;
        }

        [Fact]
        public void ARoutineShowsTheEditDialog_TheUserTypesAndPressesOk_AndItsActionSeesTheText()
        {
            // reference: the value the user gave is put into the tile before its action runs (D323); OK ends the dialog with 1
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gh-dcl-win-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);

            try
            {
                string kit = WriteKit(folder);
                OnSta(() =>
                {
                    var host = new DclWindowHost(() => null);
                    CadCommandSession session = SessionWith(host);
                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            DclDialogWindow window = host.Windows.Single();
                            Assert.Equal("OLD", Find<TextBox>(window, "text").Text);
                            Find<TextBox>(window, "text").Text = "NEW";
                            Click(Find<Button>(window, "accept"));
                        }),
                        DispatcherPriority.Background
                    );

                    LispResult result = session.Lisp.Run(
                        "(setq id (load_dialog \""
                            + kit
                            + "\")) (new_dialog \"ghedit\" id \"\" (quote (30000 30000))) (set_tile \"text\" \"OLD\") "
                            + "(action_tile \"accept\" \"(setq seen (get_tile \\\"text\\\")) (done_dialog 1)\") (list (start_dialog) seen)"
                    );

                    Assert.False(result.Failed, result.Error);
                    Assert.Equal("(1 \"NEW\")", LispInterpreter.Print(result.Value, true));
                    Assert.Empty(host.Windows);
                });
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void TheEditBoxLeft_RunsItsActionWithReasonTwo_BeforeTheButtonThatTookTheFocus()
        {
            // AutoCAD 2021 (GUI g3): an edit box left by Tab gives ("eb" "hey" 2); here the button's click follows
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gh-dcl-win-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);

            try
            {
                string kit = WriteKit(folder);
                OnSta(() =>
                {
                    var host = new DclWindowHost(() => null);
                    CadCommandSession session = SessionWith(host);
                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            DclDialogWindow window = host.Windows.Single();
                            var edit = Find<TextBox>(window, "eb");
                            edit.Text = "hey";
                            edit.RaiseEvent(
                                new KeyboardFocusChangedEventArgs(
                                    Keyboard.PrimaryDevice,
                                    0,
                                    edit,
                                    Find<Button>(window, "b1")
                                )
                                {
                                    RoutedEvent = Keyboard.LostKeyboardFocusEvent,
                                }
                            );
                            Click(Find<Button>(window, "b1"));
                            Click(Find<Button>(window, "cancel"));
                        }),
                        DispatcherPriority.Background
                    );

                    LispResult result = session.Lisp.Run(
                        "(setq id (load_dialog \""
                            + kit
                            + "\")) (new_dialog \"ghplain\" id \"\" (quote (30000 30000))) (setq log nil) "
                            + "(action_tile \"eb\" \"(setq log (cons (list $key $value $reason) log))\") "
                            + "(action_tile \"b1\" \"(setq log (cons (list $key (get_tile \\\"eb\\\")) log))\") "
                            + "(list (start_dialog) (reverse log))"
                    );

                    Assert.False(result.Failed, result.Error);
                    Assert.Equal(
                        "(0 ((\"eb\" \"hey\" 2) (\"b1\" \"hey\")))",
                        LispInterpreter.Print(result.Value, true)
                    );
                });
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void TheDialogOfAFileThatHasNoHost_IsNotShown_AsTheConsoleDoesNot()
        {
            // AutoCAD 2021 (d1): with no window new_dialog gives nil; a session given the window's host gives T
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gh-dcl-win-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);

            try
            {
                string kit = WriteKit(folder);
                OnSta(() =>
                {
                    var session = new CadCommandSession(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"));
                    string code =
                        "(setq id (load_dialog \"" + kit + "\")) (new_dialog \"ghedit\" id \"\" (quote (30000 30000)))";

                    Assert.Equal("nil", LispInterpreter.Print(session.Lisp.Run(code).Value, true));

                    session.DclHost = new DclWindowHost(() => null);

                    Assert.Equal("T", LispInterpreter.Print(session.Lisp.Run(code).Value, true));
                });
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void TheModelGivesItsHostToTheSessionsOfItsDrawings_ThoseOpenAndThoseOpenedLater()
        {
            // ours: D329 (the session holds the host the window sets)
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "gh-dcl-win-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(folder);

            try
            {
                var model = new MainViewModel();
                string first = System.IO.Path.Combine(folder, "first.dxf");
                string second = System.IO.Path.Combine(folder, "second.dxf");
                File.WriteAllText(first, new DxfDatabase("AC1027").ToText());
                File.WriteAllText(second, new DxfDatabase("AC1027").ToText());
                Assert.True(model.Open(first));
                Assert.Null(model.Session.DclHost);
                var host = new DclWindowHost(() => null);

                model.DclHost = host;

                Assert.Same(host, model.Session.DclHost);
                Assert.True(model.Open(second));
                Assert.Same(host, model.Session.DclHost);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        // ---- pictures for the eye ----

        [Fact]
        public void TheEditDialog_TheFieldsDialog_AndEveryKind_AreDrawnToPngFiles()
        {
            // ours: D333 (4): the pictures to lay beside the design boards and AutoCAD's own screenshots
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gh-dcl-shots");
            Directory.CreateDirectory(folder);

            OnSta(() =>
            {
                DclDialog edit = Make("ghedit");
                edit.Find("text").Value = "OLD";
                DclDialog fields = Make("ghfields");
                fields.Find("lst").Items.AddRange(new[] { "DESC", "LENGTH", "MARK", "MATERIAL", "QTY" });
                fields.Find("lst").Value = "2 4";
                DclDialog every = Make("ghall");
                every.Find("img").Picture.Add(new DclImageOp(DclImageOpKind.Fill, 0, 0, 120, 52, 2));
                every.Find("img").Picture.Add(new DclImageOp(DclImageOpKind.Vector, 0, 0, 12, 12, 1));
                every.Find("error").Value = "An error tile message";
                every.Find("lst").Value = "1";
                every.Find("ib").IsFlipped = false;

                foreach (
                    KeyValuePair<string, DclDialog> shot in new[]
                    {
                        new KeyValuePair<string, DclDialog>("e1.png", edit),
                        new KeyValuePair<string, DclDialog>("fdval.png", fields),
                        new KeyValuePair<string, DclDialog>("all.png", every),
                    }
                )
                {
                    var host = new DclWindowHost(() => null);
                    host.Open(shot.Value);
                    DclDialogWindow window = host.WindowOf(shot.Value);
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    string path = System.IO.Path.Combine(folder, shot.Key);
                    window.RenderPng(path);
                    host.Close(shot.Value);

                    Assert.True(new FileInfo(path).Length > 1000, shot.Key);
                }
            });
        }
    }
}
