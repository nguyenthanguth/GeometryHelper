using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GeometryHelper.Cad.Dcl;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A multiple-select list, a single list and a slider as the full AutoCAD 2021 reports them (I1 to I3 of L9): the rows of
    /// <c>scratch/l9/m/dcl.md</c>, measured with a probe dialog of our own and real mouse clicks (the keys of a posted click are
    /// not read by a list box), marked <c>AutoCAD 2021 (l9 M2 Rn)</c>. The dialog is driven on an STA thread with no one at the
    /// keyboard, as <see cref="DclDialogWindowTests"/> does.
    /// </summary>
    public class DclL9ListSliderTests
    {
        private const string Kit = """
            l9 : dialog {
                label = "L9 DCL probe";
                : row {
                    : list_box { key = "multi"; label = "Multi"; list = "i0\ni1\ni2\ni3\ni4"; multiple_select = true; width = 16; height = 7; }
                    : list_box { key = "single"; label = "Single"; list = "i0\ni1\ni2\ni3\ni4"; width = 16; height = 7; }
                }
                : slider { key = "sl"; min_value = 0; max_value = 100; small_increment = 1; big_increment = 10; width = 36; }
                ok_cancel;
            }
            """;

        private static DclDialogWindow Open()
        {
            DclFile file = DclParser.Parse(Kit, @"C:\dcl\l9.dcl", null);
            DclDialog dialog = new DclDialog(file.NewDialog("l9"), 1, "l9", null);
            dialog.Initialise();
            dialog.ScreenX = 30000;
            dialog.ScreenY = 30000;
            var window = new DclDialogWindow(dialog);
            window.Refresh();
            window.Chrome.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            window.Chrome.Arrange(new Rect(window.Chrome.DesiredSize));
            window.Chrome.UpdateLayout();
            return window;
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

        private static T Find<T>(DclDialogWindow window, string key)
            where T : DependencyObject
        {
            DclTileView view = window.ViewOf(key);
            Assert.NotNull(view);
            var found = view.FocusTarget as T ?? view.Element as T;
            Assert.NotNull(found);
            return found;
        }

        // The whole choice set at once, as the range of a Shift click leaves it: ListBox.SetSelectedItems is protected, and WPF
        // reports the change as one SelectionChanged.
        private static void Choose(ListBox list, params object[] items) =>
            typeof(ListBox)
                .GetMethod("SetSelectedItems", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(list, new object[] { items });

        // What a tile reported, as "reason value" per event.
        private static string Said(IEnumerable<DclEvent> events) =>
            string.Join("; ", events.Select(e => e.Key + " " + e.Reason + " \"" + e.Value + "\""));

        // A click as the list sees it: the button goes down on the row and comes up on it.
        private static void Click(ListBox list, int index)
        {
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);
            list.RaiseEvent(
                new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = row,
                }
            );
            list.RaiseEvent(
                new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonUpEvent,
                    Source = row,
                }
            );
        }

        private static void DoubleClick(ListBox list, int index)
        {
            Click(list, index);
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);
            list.RaiseEvent(
                new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = Control.MouseDoubleClickEvent,
                    Source = row,
                }
            );
        }

        [Fact]
        public void AListWithMultipleSelect_IsAnExtendedList_AndOneWithoutIsASingleList()
        {
            OnSta(() =>
            {
                DclDialogWindow window = Open();

                // AutoCAD 2021 (l9 M2): the multiple_select list's window style is 0x50830801, LBS_EXTENDEDSEL (0x800) set and
                // LBS_MULTIPLESEL (0x8) not, so a plain click replaces the choice, Ctrl toggles and Shift takes a range; the
                // other list's is 0x50830001, neither. Green on the pin.
                Assert.Equal(SelectionMode.Extended, Find<ListBox>(window, "multi").SelectionMode);
                Assert.Equal(SelectionMode.Single, Find<ListBox>(window, "single").SelectionMode);
            });
        }

        [Fact]
        public void EachChangeOfAnExtendedListsChoice_IsOneEventOfReasonOne_WithTheIndicesAscending()
        {
            OnSta(() =>
            {
                DclDialogWindow window = Open();
                var events = new List<DclEvent>();
                window.Raised += events.Add;
                var list = Find<ListBox>(window, "multi");
                object Row(int i) => list.Items[i];

                // AutoCAD 2021 (l9 M2 R1 to R11, real clicks): plain 0 gave "0"; plain 2 "2"; Ctrl 4 "2 4"; Ctrl 2 (chosen) "4"; Ctrl 0
                // "0 4"; plain 1 "1"; Shift 3 "1 2 3"; Shift 0 (anchor 1) "0 1"; Ctrl 3 "0 1 3"; Shift 4 (anchor 3) "3 4"; plain 4
                // "4": one action each, $reason 1. The clicks are made here as the selection each one leaves, since WPF reads Ctrl
                // and Shift from the keyboard. Green on the pin.
                list.SelectedItem = Row(0);
                list.SelectedItem = Row(2);
                list.SelectedItems.Add(Row(4));
                list.SelectedItems.Remove(Row(2));
                list.SelectedItems.Add(Row(0));
                list.SelectedItem = Row(1);
                Choose(list, Row(1), Row(2), Row(3));
                Choose(list, Row(0), Row(1));
                list.SelectedItems.Add(Row(3));
                Choose(list, Row(3), Row(4));
                list.SelectedItem = Row(4);

                Assert.Equal(
                    "multi 1 \"0\"; multi 1 \"2\"; multi 1 \"2 4\"; multi 1 \"4\"; multi 1 \"0 4\"; multi 1 \"1\"; multi 1 \"1 2 3\"; "
                        + "multi 1 \"0 1\"; multi 1 \"0 1 3\"; multi 1 \"3 4\"; multi 1 \"4\"",
                    Said(events)
                );
            });
        }

        [Fact]
        public void ADoubleClickOnAnItemOfAList_ReportsTheChoiceWithReasonOneAndThenReasonFour()
        {
            OnSta(() =>
            {
                DclDialogWindow window = Open();
                var events = new List<DclEvent>();
                window.Raised += events.Add;
                var list = Find<ListBox>(window, "single");

                // AutoCAD 2021 (l9 M2 RS1, RS2, real clicks): a click on item 1 gave reason 1 value "1"; a double click on item 3
                // gave reason 1 value "3" and then reason 4 value "3". Green on the pin.
                list.SelectedItem = list.Items[1];
                list.SelectedItem = list.Items[3];
                list.RaiseEvent(
                    new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = Control.MouseDoubleClickEvent,
                        Source = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(3),
                    }
                );

                Assert.Equal("single 1 \"1\"; single 1 \"3\"; single 4 \"3\"", Said(events));
            });
        }

        [Fact]
        public void AClickOnTheItemAlreadyChosenInAList_ReportsTheChoiceAgainWithReasonOne()
        {
            OnSta(() =>
            {
                DclDialogWindow window = Open();
                var list = Find<ListBox>(window, "single");
                list.SelectedItem = list.Items[3];
                var events = new List<DclEvent>();
                window.Raised += events.Add;

                Click(list, 3);

                // AutoCAD 2021 (l9 M2 RS3, real click): item 3 chosen and clicked again gives reason 1 value "3" (the list box sends
                // its notification for every click); ours (pin) gave nothing, since WPF reports a change only.
                Assert.Equal("single 1 \"3\"", Said(events));
            });
        }

        [Fact]
        public void AClickOnTheOnlyChosenItemOfAnExtendedList_ReportsTheChoiceAgainWithReasonOne()
        {
            OnSta(() =>
            {
                DclDialogWindow window = Open();
                var list = Find<ListBox>(window, "multi");
                list.SelectedItem = list.Items[1];
                var events = new List<DclEvent>();
                window.Raised += events.Add;

                Click(list, 1);

                // AutoCAD 2021 (l9 M2 R14, real click): item 1 chosen alone and clicked again gives reason 1 value "1"; ours (pin)
                // gave nothing.
                Assert.Equal("multi 1 \"1\"", Said(events));
            });
        }

        [Fact]
        public void ADoubleClickOnTheItemAlreadyChosenInAList_ReportsReasonOneAndThenReasonFour()
        {
            OnSta(() =>
            {
                DclDialogWindow window = Open();
                var list = Find<ListBox>(window, "single");
                list.SelectedItem = list.Items[3];
                var events = new List<DclEvent>();
                window.Raised += events.Add;

                DoubleClick(list, 3);

                // AutoCAD 2021 (l9 M2 RS4, real double click): the chosen item 3 double-clicked gives reason 1 value "3" and then
                // reason 4 value "3"; ours (pin) gave reason 4 only.
                Assert.Equal("single 1 \"3\"; single 4 \"3\"", Said(events));
            });
        }

        [Fact]
        public void ASliderThumbReleased_ReportsTheFinalValueWithReasonOne()
        {
            OnSta(() =>
            {
                DclDialogWindow window = Open();
                var slider = Find<Slider>(window, "sl");
                slider.Value = 37;
                var events = new List<DclEvent>();
                window.Raised += events.Add;

                slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false));

                // AutoCAD 2021 (l9 M2 RL1, real drag of the thumb 60 pixels): six moves gave $reason 3 with values 7 13 19 25 31 37,
                // and the release gave $reason 1 with 37 (the last value). The release is the row pinned here; the reason 3 of a
                // move needs a real drag of the thumb and is not made here. Green on the pin.
                Assert.Equal("sl 1 \"37\"", Said(events));
            });
        }
    }
}
