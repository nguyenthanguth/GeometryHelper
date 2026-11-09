using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Home › Modify laid out as AutoCAD 2021's panel, found in the window drawn offscreen: its buttons, the menus of the arrows
    /// of Trim, Fillet and Array and of the group's title, each item enabled and starting its command.
    /// </summary>
    public class ModifyRibbonTests
    {
        [Fact]
        public void EveryButtonAndArrowItemOfModifyStartsItsCommand_InTheOrderOfAutoCadsPanel()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        PressModify(room);
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

        private static void PressModify(string room)
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
                List<Button> buttons = Descendants(group).OfType<Button>().Where(b => b.Command == model.StartCommandCommand).ToList();
                Assert.Equal(new[] { "MOVE", "COPY", "STRETCH", "ROTATE", "MIRROR", "SCALE", "TRIM", "FILLET", "ARRAYRECT", "ERASE", "OFFSET" }, buttons.Select(b => (string)b.CommandParameter));

                foreach (Button button in buttons)
                {
                    Press(model, button.IsEnabled, () => button.Command.Execute(button.CommandParameter), (string)button.CommandParameter);
                }

                // The menus: the title's first, then the arrows of Trim, Fillet and Array.
                List<Button> menus = Descendants(group).OfType<Button>().Where(b => b.ContextMenu != null).ToList();
                var items = new List<string>();

                foreach (Button opener in menus)
                {
                    Assert.True(opener.IsEnabled);
                    ContextMenu menu = opener.ContextMenu;

                    // The menu takes the window's model from the button it drops from, its items bound at the dispatcher's next turn.
                    menu.PlacementTarget = opener;
                    opener.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

                    foreach (MenuItem item in menu.Items.OfType<MenuItem>())
                    {
                        string command = (string)item.CommandParameter;
                        items.Add(command);
                        Press(model, item.Command != null && item.Command.CanExecute(command), () => item.Command.Execute(command), command);
                    }
                }

                Assert.Equal(new[] { "BREAKATPOINT", "BREAK", "JOIN", "LENGTHEN", "ALIGN", "TRIM", "EXTEND", "FILLET", "CHAMFER", "ARRAYRECT", "ARRAYPOLAR" }, items);
            }
            finally
            {
                workspace.DrawingCanvas.Dispose();
            }
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
