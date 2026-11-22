using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// What the tests of the Insert and Annotate tabs share: the window drawn offscreen with its dialogs let go, one tab's strip
    /// laid out at a width, the panels, their controls in reading order, the menus of the splits, and the checks of a part
    /// against "name|command" lines. It is RibbonHomePanelsTests' set of helpers, one copy of it for the two new files; a name
    /// is compared by its letters and digits alone ("Combine / Text" is "Combine Text"), as a label broken over two lines is.
    /// </summary>
    internal static class RibbonTabHarness
    {
        // A strip so wide that no panel of either tab shrinks (they need 1 050 and 1 560 at the most).
        public const double Full = 3000;

        // The metrics of D1305 and D1313, as RibbonHomePanelsTests reads them.
        public const double PanelHeight = 98;
        public const double LargeHeight = 72;
        public const double TileWidth = 24;
        public const double TileHeight = 22;
        public const double Pitch = 24;

        #region The window

        /// <summary>
        /// The drawing open in the window drawn offscreen with the tab chosen shown, laid out so that its strip is a width wide,
        /// on a thread of its own. Every dialog the window would show is let go first (see <see cref="LetGoOfTheWindow"/>), so a
        /// pick of a button or a menu item opens no window.
        /// </summary>
        public static void OnTab(
            Func<MainViewModel, CadWorkspaceExtension> tab,
            double stripWidth,
            Action<MainViewModel, CadWorkspace, RibbonStrip> check
        )
        {
            using (var folder = new TestFolder())
            {
                string file = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
                        _ = PackUriHelper.UriSchemePack;
                        var model = new MainViewModel();
                        Assert.True(model.Open(file));
                        var workspace = new CadWorkspace { DataContext = model };
                        LetGoOfTheWindow(model);
                        CadWorkspaceExtension chosen = tab(model);
                        model.SelectedTab = chosen;

                        try
                        {
                            // The window gives its strip a width less what is around it: read at a wide window, then laid out again.
                            Layout(workspace, 3200);
                            RibbonStrip strip = StripOf(workspace, chosen.TabContent);
                            double around = 3200 - strip.ActualWidth;
                            Layout(workspace, stripWidth + around);
                            Pump(workspace);
                            check(model, workspace, strip);
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

        /// <summary>
        /// Takes away what would show a window: every hook the workspace gave the model (the Show and Ask delegates) and the
        /// request of the style dialogs, then the four dialogs RibbonHomePanelsTests answers Cancel (BOUNDARY, SPELL, the viewports,
        /// the missing SHX) are answered so. A test that wants to see a dialog sets its own hook afterwards.
        /// </summary>
        public static void LetGoOfTheWindow(MainViewModel model)
        {
            foreach (
                PropertyInfo hook in typeof(MainViewModel)
                    .GetProperties()
                    .Where(p => p.CanWrite && typeof(Delegate).IsAssignableFrom(p.PropertyType))
            )
            {
                hook.SetValue(model, null);
            }

            typeof(MainViewModel)
                .GetField("StyleDialogRequested", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(model, null);
            model.ShowBoundaryCreation = dialog => false;
            model.ShowCheckSpelling = dialog => { };
            model.ShowViewports = dialog => false;
            model.ShowMissingShx = dialog => false;
        }

        public static RibbonStrip StripOf(CadWorkspace workspace, object content) =>
            Descendants(workspace).OfType<RibbonStrip>().Single(s => ReferenceEquals(s.DataContext, content));

        public static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 960));
            element.Arrange(new Rect(0, 0, width, 960));
            element.UpdateLayout();
        }

        public static void Pump(DispatcherObject element) =>
            element.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

        #endregion

        #region Panels, bodies and slide-outs

        public static string Squeeze(string text) => text.Replace(" ", string.Empty);

        public static List<RibbonPanel> Panels(RibbonStrip strip) => strip.Children.OfType<RibbonPanel>().ToList();

        public static RibbonPanel Panel(RibbonStrip strip, string title) =>
            Assert.Single(Panels(strip), p => p.Title == title);

        /// <summary>One letter per panel in the order of the strip: F full, M medium, C collapsed.</summary>
        public static string Sizes(RibbonStrip strip) =>
            new string(
                Panels(strip)
                    .Select(p =>
                        p.Size == RibbonPanelSize.Full ? 'F'
                        : p.Size == RibbonPanelSize.Medium ? 'M'
                        : 'C'
                    )
                    .ToArray()
            );

        /// <summary>The slide-out dropped under the panel by its title, laid out, so that the positions of its controls can be read.</summary>
        public static void OpenSlideOut(RibbonPanel panel)
        {
            Assert.NotNull(panel.SlideOut);

            if (panel.IsDropDownOpen)
            {
                return;
            }

            var popup = (Popup)panel.Template.FindName("PART_Popup", panel);
            panel.IsDropDownOpen = true;
            Pump(popup);
            Assert.True(panel.IsDropDownOpen);
            popup.Child.Measure(new Size(2000, 960));
            popup.Child.Arrange(new Rect(0, 0, popup.Child.DesiredSize.Width, popup.Child.DesiredSize.Height));
            ((FrameworkElement)popup.Child).UpdateLayout();
        }

        /// <summary>
        /// The controls of a part of a panel: its buttons, toggles and combo boxes, not the arrow of a split nor what is inside a
        /// combo box.
        /// </summary>
        public static List<FrameworkElement> ControlsOf(DependencyObject root) =>
            Descendants(root)
                .OfType<FrameworkElement>()
                .Where(e => ((e is ButtonBase b && !IsArrow(b)) || e is ComboBox) && !InsideCompound(e))
                .ToList();

        /// <summary>The body's controls in reading order of its columns: left to right, each column top to bottom.</summary>
        public static List<FrameworkElement> BodyControls(RibbonPanel panel)
        {
            var columns = new List<List<FrameworkElement>>();

            foreach (
                FrameworkElement control in ControlsOf(panel.Body)
                    .OrderBy(c => Left(c, panel.Body))
                    .ThenBy(c => Top(c, panel.Body))
            )
            {
                List<FrameworkElement> column = columns.LastOrDefault();

                if (column == null || Left(control, panel.Body) - Left(column[0], panel.Body) > 8)
                {
                    columns.Add(column = new List<FrameworkElement>());
                }

                column.Add(control);
            }

            return columns.SelectMany(c => c.OrderBy(e => Top(e, panel.Body))).ToList();
        }

        /// <summary>
        /// The small controls of a body that stand beside or under a large button, in rows, top to bottom, each row left to
        /// right: the controls 30 high or more (the large buttons) are not in them.
        /// </summary>
        public static List<List<FrameworkElement>> SmallRows(RibbonPanel panel) =>
            Rows(ControlsOf(panel.Body).Where(c => c.ActualHeight < 30).ToList(), panel.Body);

        public static List<FrameworkElement> SlideOutControls(RibbonPanel panel) => ControlsOf(panel.SlideOut);

        /// <summary>Controls in rows, top to bottom, each row left to right.</summary>
        public static List<List<FrameworkElement>> Rows(List<FrameworkElement> controls, UIElement relativeTo)
        {
            var rows = new List<List<FrameworkElement>>();

            foreach (
                FrameworkElement control in controls.OrderBy(c => Top(c, relativeTo)).ThenBy(c => Left(c, relativeTo))
            )
            {
                List<FrameworkElement> row = rows.LastOrDefault();

                if (row == null || Top(control, relativeTo) - Top(row[0], relativeTo) > 6)
                {
                    rows.Add(row = new List<FrameworkElement>());
                }

                row.Add(control);
            }

            return rows.Select(r => r.OrderBy(c => Left(c, relativeTo)).ToList()).ToList();
        }

        public static void AssertSlideOut(RibbonPanel panel, string[][] expected)
        {
            OpenSlideOut(panel);
            List<List<FrameworkElement>> rows = Rows(SlideOutControls(panel), panel.SlideOut);

            Assert.Equal(expected.Length, rows.Count);

            for (int r = 0; r < expected.Length; r++)
            {
                AssertControls(expected[r], rows[r]);
            }
        }

        #endregion

        #region Reading a control

        /// <summary>Controls against "name|command" lines: where a part is empty the control's own is taken, so that it is not read.</summary>
        public static void AssertControls(string[] expected, List<FrameworkElement> actual)
        {
            Assert.Equal(expected.Length, actual.Count);
            Assert.Equal(expected.Select((line, i) => Wanted(line, actual[i])), actual.Select(Describe));
        }

        private static string Wanted(string line, FrameworkElement control)
        {
            if (line == "combo")
            {
                return line;
            }

            string[] parts = line.Split('|');
            return (parts[0].Length > 0 ? Compact(parts[0]) : Compact(NameOf(control)))
                + "|"
                + (parts[1].Length > 0 ? parts[1] : CommandWord(control));
        }

        private static string Describe(FrameworkElement control) =>
            control is ComboBox ? "combo" : Compact(NameOf(control)) + "|" + CommandWord(control);

        /// <summary>A name by its letters and digits, in lower case: "Add/Delete Scales" is "adddeletescales".</summary>
        public static string Compact(string name) =>
            new string((name ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        /// <summary>The first word of the command a button starts: CIRCLE of "CIRCLE 2P"; empty where it starts none by name.</summary>
        public static string CommandWord(FrameworkElement control) =>
            control is ButtonBase button
                ? ((button.CommandParameter as string) ?? string.Empty).Split(' ')[0]
                : string.Empty;

        /// <summary>The words a control is known by: its name for screen readers, or its text where it has no other.</summary>
        public static string NameOf(FrameworkElement control)
        {
            string name = AutomationProperties.GetName(control);

            if (string.IsNullOrEmpty(name) && control is ContentControl content)
            {
                name = content.Content as string;
            }

            return name ?? string.Empty;
        }

        /// <summary>Everything a control says of itself, for finding what it is: its name, its tooltip, its help text.</summary>
        public static string Words(FrameworkElement control) =>
            NameOf(control)
            + " "
            + (control.ToolTip as string)
            + " "
            + (AutomationProperties.GetHelpText(control) ?? string.Empty);

        public static bool HasText(FrameworkElement control) =>
            Descendants(control).OfType<TextBlock>().Any(t => !string.IsNullOrWhiteSpace(t.Text));

        /// <summary>The label a split shows, its texts run together without spaces: "Circle▾", whether the ▾ stands on a line of its own or not.</summary>
        public static string Label(DependencyObject split) =>
            string.Concat(
                    Descendants(split).OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t))
                )
                .Replace(" ", string.Empty);

        /// <summary>What a combo's item says: the content of a ComboBoxItem or the text of a string, else the Name, Label or Text it has, else its ToString().</summary>
        public static string ItemText(object item)
        {
            if (item is ContentControl content && content.Content is string shown)
            {
                return shown.Trim();
            }

            if (item is string text)
            {
                return text.Trim();
            }

            foreach (string property in new[] { "Name", "Label", "Text", "Header" })
            {
                if (item.GetType().GetProperty(property)?.GetValue(item) is string value && value.Trim().Length > 0)
                {
                    return value.Trim();
                }
            }

            return item.ToString().Trim();
        }

        public static List<string> ItemTexts(ComboBox combo) => combo.Items.Cast<object>().Select(ItemText).ToList();

        /// <summary>The arrow of a split: the second child of the panel holding the button, as every split of the ribbon is built.</summary>
        public static ButtonBase SplitArrowOf(ButtonBase main) =>
            VisualTreeHelper.GetParent(main) is Panel panel
            && panel.Children.Count == 2
            && ReferenceEquals(panel.Children[0], main)
            && panel.Children[1] is ButtonBase arrow
            && IsArrow(arrow)
                ? arrow
                : null;

        public static bool IsArrow(ButtonBase button) =>
            button.CommandParameter == null
            && (
                button.ContextMenu != null
                || (AutomationProperties.GetName(button) ?? string.Empty).EndsWith("gallery", StringComparison.Ordinal)
            );

        public static bool InsideCompound(DependencyObject element)
        {
            for (
                DependencyObject at = VisualTreeHelper.GetParent(element);
                at != null;
                at = VisualTreeHelper.GetParent(at)
            )
            {
                if (at is ComboBox || at is Slider || at is TextBoxBase || at is ScrollBar)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The height of a control: its own, or for a split button the frame round its top and its arrow.</summary>
        public static double LargeHeightOf(FrameworkElement control)
        {
            ButtonBase arrow = control is ButtonBase button ? SplitArrowOf(button) : null;

            if (arrow == null)
            {
                return control.ActualHeight;
            }

            var panel = (FrameworkElement)VisualTreeHelper.GetParent(control);
            return VisualTreeHelper.GetParent(panel) is Border frame ? frame.ActualHeight : panel.ActualHeight;
        }

        public static Rect Union(FrameworkElement first, FrameworkElement second, UIElement relativeTo)
        {
            var a = new Rect(
                first.TranslatePoint(new Point(0, 0), relativeTo),
                new Size(first.ActualWidth, first.ActualHeight)
            );
            var b = new Rect(
                second.TranslatePoint(new Point(0, 0), relativeTo),
                new Size(second.ActualWidth, second.ActualHeight)
            );
            return Rect.Union(a, b);
        }

        public static double Left(FrameworkElement element, UIElement relativeTo) =>
            element.TranslatePoint(new Point(0, 0), relativeTo).X;

        public static double Top(FrameworkElement element, UIElement relativeTo) =>
            element.TranslatePoint(new Point(0, 0), relativeTo).Y;

        public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
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

        #endregion

        #region Menus and picks

        /// <summary>The menu of a split's arrow checked: its items in groups parted by separators, and the command each starts.</summary>
        public static void AssertMenu(
            MainViewModel model,
            ButtonBase arrow,
            string[][] groups,
            string command,
            string[] commands = null
        )
        {
            Assert.NotNull(arrow);
            ContextMenu menu = arrow.ContextMenu;
            Assert.NotNull(menu);
            menu.PlacementTarget = arrow;
            Pump(arrow);

            var actual = new List<List<MenuItem>> { new List<MenuItem>() };

            foreach (object item in menu.Items)
            {
                if (item is Separator)
                {
                    actual.Add(new List<MenuItem>());
                }
                else
                {
                    actual.Last().Add((MenuItem)item);
                }
            }

            Assert.Equal(groups, actual.Select(g => g.Select(Norm).ToArray()));

            List<MenuItem> flat = actual.SelectMany(g => g).ToList();
            string[] words = flat.Select(i => ((string)i.CommandParameter ?? string.Empty).Split(' ')[0]).ToArray();
            Assert.Equal(commands ?? flat.Select(i => command).ToArray(), words);
            PickEach(model, flat);
        }

        /// <summary>Each item, picked, starts its command; Esc ends it.</summary>
        public static void PickEach(MainViewModel model, IEnumerable<MenuItem> items)
        {
            foreach (MenuItem item in items)
            {
                int before = model.History.Count;

                Pick(item);

                Assert.Contains(
                    "Command: " + ((string)item.CommandParameter).Split(' ')[0],
                    model.History.Skip(before)
                );
                Assert.DoesNotContain(model.History.Skip(before), l =>
                    l.StartsWith("Unknown command", StringComparison.Ordinal)
                );
                model.Escape();
            }
        }

        /// <summary>The name of a menu item, as the spec writes it: no commas, no ellipsis, one space between words.</summary>
        public static string Norm(MenuItem item) =>
            string.Join(
                " ",
                ((string)item.Header ?? string.Empty)
                    .Replace(",", string.Empty)
                    .Replace("…", string.Empty)
                    .Replace("...", string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            );

        /// <summary>The items of a button's menu, the menu taking the window's model from the button, its items bound at the dispatcher's next turn.</summary>
        public static List<MenuItem> MenuOf(ButtonBase opener)
        {
            ContextMenu menu = opener.ContextMenu;
            menu.PlacementTarget = opener;
            Pump(opener);
            return menu.Items.OfType<MenuItem>().ToList();
        }

        /// <summary>An item clicked as the mouse clicks it, its Click raised and its command run.</summary>
        public static void Pick(MenuItem item)
        {
            Assert.True(item.IsEnabled, item.Header + " is not enabled.");
            MethodInfo click = typeof(MenuItem).GetMethod(
                "OnClick",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );
            click.Invoke(item, null);
            Pump(item);
        }

        /// <summary>
        /// Every button of a strip's bodies and slide-outs that names a command runs it through StartCommand or
        /// StartCommandWithOption, enabled; and every command word of the buttons and their menus is one the presenter has.
        /// </summary>
        /// <returns>The words, for a check that a panel is not empty.</returns>
        public static SortedSet<string> AssertEveryButtonStartsItsCommand(MainViewModel model, RibbonStrip strip)
        {
            var words = new SortedSet<string>(StringComparer.Ordinal);

            foreach (RibbonPanel panel in Panels(strip))
            {
                if (panel.SlideOut != null)
                {
                    OpenSlideOut(panel);
                }

                foreach (DependencyObject root in new DependencyObject[] { panel.Body, panel.SlideOut })
                {
                    if (root == null)
                    {
                        continue;
                    }

                    foreach (ButtonBase button in Descendants(root).OfType<ButtonBase>())
                    {
                        if (CommandWord(button).Length > 0)
                        {
                            Assert.Contains(
                                button.Command,
                                new[] { model.StartCommandCommand, model.StartCommandWithOptionCommand }
                            );
                            Assert.True(button.IsEnabled, CommandWord(button) + " is not enabled.");
                            Assert.True(
                                button.Command.CanExecute(button.CommandParameter),
                                CommandWord(button) + " cannot run."
                            );
                        }

                        words.Add(CommandWord(button));

                        if (button.ContextMenu != null)
                        {
                            foreach (MenuItem item in MenuOf(button))
                            {
                                words.Add(((item.CommandParameter as string) ?? string.Empty).Split(' ')[0]);
                            }
                        }
                    }
                }
            }

            words.Remove(string.Empty);
            string[] unknown = words
                .Where(w => model.Session.Registry.Find(w) == null && model.Session.Registry.FindVariable(w) == null)
                .ToArray();
            Assert.True(unknown.Length == 0, "No such command: " + string.Join(", ", unknown));
            return words;
        }

        /// <summary>A button pressed as its command runs, giving the command its parameter.</summary>
        public static void Press(ButtonBase button)
        {
            Assert.True(button.IsEnabled, NameOf(button) + " is not enabled.");
            Assert.True(button.Command.CanExecute(button.CommandParameter), NameOf(button) + " cannot run.");
            button.Command.Execute(button.CommandParameter);
        }

        #endregion

        #region Shrinking

        /// <summary>The sizes the steps of a strip leave its panels at after a number of them has been taken.</summary>
        public static string After(string steps, int taken, IEnumerable<string> titles)
        {
            var sizes = titles.ToDictionary(t => t, t => RibbonPanelSize.Full);

            foreach (string step in steps.Split(';').Take(taken))
            {
                string[] parts = step.Split(':');
                sizes[parts[0].Trim()] = (RibbonPanelSize)Enum.Parse(typeof(RibbonPanelSize), parts[1].Trim());
            }

            return string.Concat(
                titles.Select(t =>
                    sizes[t] == RibbonPanelSize.Full ? 'F'
                    : sizes[t] == RibbonPanelSize.Medium ? 'M'
                    : 'C'
                )
            );
        }

        /// <summary>
        /// The strip narrowed one pixel at a time from 1 920 to 500: no panel grows back, every step of the string is taken (the
        /// steps in the order the string lists them, one never before the step ahead of it), and the panels named never leave Full.
        /// </summary>
        public static void AssertStepsInOrder(RibbonStrip strip, string steps, params string[] neverShrink)
        {
            List<RibbonPanel> panels = Panels(strip);
            var steplist = steps
                .Split(';')
                .Select(s =>
                    (
                        Title: s.Split(':')[0].Trim(),
                        Size: (RibbonPanelSize)Enum.Parse(typeof(RibbonPanelSize), s.Split(':')[1].Trim())
                    )
                )
                .ToList();
            var taken = new Dictionary<int, double>();
            var last = panels.ToDictionary(p => p.Title, p => RibbonPanelSize.Full);

            for (double width = 1920; width >= 500; width--)
            {
                strip.Measure(new Size(width, 960));

                foreach (RibbonPanel panel in panels)
                {
                    Assert.True(panel.Size >= last[panel.Title], panel.Title + " grew back at " + width + ".");

                    for (int k = 0; k < steplist.Count; k++)
                    {
                        if (steplist[k].Title == panel.Title && panel.Size >= steplist[k].Size && !taken.ContainsKey(k))
                        {
                            taken[k] = width;
                        }
                    }

                    last[panel.Title] = panel.Size;
                }

                Assert.All(panels.Where(p => neverShrink.Contains(p.Title)), p =>
                    Assert.Equal(RibbonPanelSize.Full, p.Size)
                );
            }

            Assert.Equal(steplist.Count, taken.Count);

            for (int k = 1; k < steplist.Count; k++)
            {
                Assert.True(
                    taken[k] <= taken[k - 1],
                    steplist[k].Title
                        + ":"
                        + steplist[k].Size
                        + " ("
                        + taken[k]
                        + ") came before "
                        + steplist[k - 1].Title
                        + ":"
                        + steplist[k - 1].Size
                        + " ("
                        + taken[k - 1]
                        + ")."
                );
            }
        }

        #endregion
    }
}
