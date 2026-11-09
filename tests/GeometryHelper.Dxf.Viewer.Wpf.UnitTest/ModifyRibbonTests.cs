using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Home › Modify laid out as AutoCAD 2021's panel, found in the window drawn offscreen: its buttons, Erase, Explode and Offset
    /// by their icons alone, the row of Break, Join, Lengthen and Align under the columns, the menus of the arrows of Trim, Fillet
    /// and Array and of the group's title, each item enabled and starting its command; and a split button that shows the command
    /// last picked from its arrow.
    /// </summary>
    public class ModifyRibbonTests
    {
        [Fact]
        public void EveryButtonAndArrowItemOfModifyStartsItsCommand_InTheOrderOfAutoCadsPanel()
        {
            OnModify((model, group) =>
            {
                List<Button> buttons = Buttons(model, group);
                Assert.Equal(
                    new[] { "MOVE", "COPY", "STRETCH", "ROTATE", "MIRROR", "SCALE", "TRIM", "FILLET", "ARRAYRECT", "ERASE", "EXPLODE", "OFFSET", "BREAK", "JOIN", "LENGTHEN", "ALIGN" },
                    buttons.Select(b => (string)b.CommandParameter));

                foreach (Button button in buttons)
                {
                    Press(model, button.IsEnabled, () => button.Command.Execute(button.CommandParameter), (string)button.CommandParameter);
                }

                // The menus: the title's first, then the arrows of Trim, Fillet and Array.
                var items = new List<string>();

                foreach (Button opener in Descendants(group).OfType<Button>().Where(b => b.ContextMenu != null))
                {
                    Assert.True(opener.IsEnabled);

                    foreach (MenuItem item in MenuOf(opener))
                    {
                        string command = (string)item.CommandParameter;
                        items.Add(command);
                        Press(model, item.Command != null && item.Command.CanExecute(command), () => item.Command.Execute(command), command);
                    }
                }

                Assert.Equal(new[] { "BREAKATPOINT", "BREAK", "JOIN", "LENGTHEN", "ALIGN", "TRIM", "EXTEND", "FILLET", "CHAMFER", "ARRAYRECT", "ARRAYPOLAR" }, items);
            });
        }

        [Fact]
        public void EraseExplodeAndOffsetShowTheirIconsAlone_AndBreakJoinLengthenAndAlignARowOfSmallerIconsUnderALine()
        {
            OnModify((model, group) =>
            {
                List<Button> buttons = Buttons(model, group);

                foreach (string name in new[] { "Erase", "Explode", "Offset" })
                {
                    Button button = buttons.Single(b => (string)b.Content == name);
                    Assert.Equal(name, AutomationProperties.GetName(button));
                    Assert.Equal(name + " (" + name.ToUpperInvariant() + ")", button.ToolTip);
                    Assert.DoesNotContain(Descendants(button).OfType<TextBlock>(), t => t.Text == name);
                    Assert.Equal(16.0, Assert.Single(Descendants(button).OfType<IconView>()).Size);
                }

                Button move = buttons.Single(b => (string)b.Content == "Move");
                Assert.Single(Descendants(move).OfType<TextBlock>(), t => t.Text == "Move");

                Border row = Descendants(group).OfType<Border>().Single(b => Descendants(b).OfType<Button>().Any(x => "BREAK".Equals(x.CommandParameter)) && !Descendants(b).OfType<Button>().Any(x => "MOVE".Equals(x.CommandParameter)) && b.BorderThickness.Top == 1.0);
                List<Button> rowButtons = Descendants(row).OfType<Button>().Where(b => b.Command == model.StartCommandCommand).ToList();
                Assert.Equal(new[] { "Break", "Join", "Lengthen", "Align" }, rowButtons.Select(b => (string)b.Content));

                foreach (Button button in rowButtons)
                {
                    Assert.Equal(14.0, Assert.Single(Descendants(button).OfType<IconView>()).Size);
                    Assert.Single(Descendants(button).OfType<TextBlock>(), t => t.Text == (string)button.Content);
                }

                // The row lies under the columns: its top below the bottom of Stretch, the last of the first column.
                Button stretch = buttons.Single(b => "STRETCH".Equals(b.CommandParameter));
                double stretchBottom = stretch.TranslatePoint(new Point(0, stretch.ActualHeight), group).Y;
                Assert.True(row.TranslatePoint(new Point(0, 0), group).Y >= stretchBottom);
            });
        }

        [Fact]
        public void ExtendPickedFromTrimsArrow_LeavesTheButtonReadingAndRunningExtend_UntilTrimIsPickedAgain()
        {
            OnModify((model, group) =>
            {
                Button trim = Buttons(model, group).Single(b => "TRIM".Equals(b.CommandParameter));
                var split = (Panel)VisualTreeHelper.GetParent(trim);
                var arrow = (Button)split.Children[1];
                List<MenuItem> items = MenuOf(arrow);
                MenuItem extend = items.Single(i => "EXTEND".Equals(i.CommandParameter));
                object trimIcon = trim.Tag;

                Pick(model, extend, "EXTEND");

                Assert.Equal("Extend", trim.Content);
                Assert.Same(extend.Tag, trim.Tag);
                Assert.NotSame(trimIcon, trim.Tag);
                Press(model, trim.IsEnabled, () => trim.Command.Execute(trim.CommandParameter), "EXTEND");

                Pick(model, items.Single(i => "TRIM".Equals(i.CommandParameter)), "TRIM");

                Assert.Equal("Trim", trim.Content);
                Assert.Same(trimIcon, trim.Tag);
                Press(model, trim.IsEnabled, () => trim.Command.Execute(trim.CommandParameter), "TRIM");

                // The array's button reads Polar Array once it is picked, and Array again for the rectangular one.
                Button array = Buttons(model, group).Single(b => "ARRAYRECT".Equals(b.CommandParameter));
                List<MenuItem> arrays = MenuOf((Button)((Panel)VisualTreeHelper.GetParent(array)).Children[1]);
                Pick(model, arrays.Single(i => "ARRAYPOLAR".Equals(i.CommandParameter)), "ARRAYPOLAR");
                Assert.Equal("Polar Array", array.Content);
                Assert.Equal("ARRAYPOLAR", array.CommandParameter);
                Pick(model, arrays.Single(i => "ARRAYRECT".Equals(i.CommandParameter)), "ARRAYRECT");
                Assert.Equal("Array", array.Content);
            });
        }

        /// <summary>Runs a check on Home › Modify of a window drawn offscreen on a thread of its own, a room drawing open.</summary>
        private static void OnModify(Action<MainViewModel, Border> check)
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
                        _ = PackUriHelper.UriSchemePack;
                        var model = new MainViewModel();
                        Assert.True(model.Open(room));
                        var workspace = new CadWorkspace { DataContext = model };

                        try
                        {
                            workspace.Measure(new Size(1600, 960));
                            workspace.Arrange(new Rect(0, 0, 1600, 960));
                            workspace.UpdateLayout();

                            // The group is the innermost border that holds both the title Modify and the Move button.
                            Border group = Descendants(workspace).OfType<Border>()
                                .Last(b => Descendants(b).OfType<TextBlock>().Any(t => t.Text == "Modify") && Descendants(b).OfType<Button>().Any(x => "MOVE".Equals(x.CommandParameter)));
                            check(model, group);
                        }
                        finally
                        {
                            workspace.DrawingCanvas.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                Assert.Null(failure);
            }
        }

        private static List<Button> Buttons(MainViewModel model, Border group)
            => Descendants(group).OfType<Button>().Where(b => b.Command == model.StartCommandCommand).ToList();

        /// <summary>The items of a button's menu, the menu taking the window's model from the button, its items bound at the dispatcher's next turn.</summary>
        private static List<MenuItem> MenuOf(Button opener)
        {
            ContextMenu menu = opener.ContextMenu;
            menu.PlacementTarget = opener;
            opener.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            return menu.Items.OfType<MenuItem>().ToList();
        }

        /// <summary>An item clicked as the mouse clicks it, its Click raised and its command run after the next render; then Esc.</summary>
        private static void Pick(MainViewModel model, MenuItem item, string command)
        {
            MethodInfo click = typeof(MenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            Press(model, item.IsEnabled, () =>
            {
                click.Invoke(item, null);
                item.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            }, command);
        }

        /// <summary>A button or item enabled, pressed, and the command it names started; then Esc.</summary>
        private static void Press(MainViewModel model, bool enabled, Action press, string command)
        {
            Assert.True(enabled, command + " is not enabled.");
            int before = model.History.Count;
            press();
            Assert.Contains("Command: " + command, model.History.Skip(before));
            Assert.DoesNotContain(model.History.Skip(before), l => l.StartsWith("Unknown command", StringComparison.Ordinal));
            model.Escape();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }
    }
}
