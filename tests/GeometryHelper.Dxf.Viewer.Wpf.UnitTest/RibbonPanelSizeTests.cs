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

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The ribbon laid out as AutoCAD 2021's, found in the window drawn offscreen: Home's panels in AutoCAD's order, shrunk in
    /// the steps of the design as the window narrows, a collapsed panel's drop-down with its full buttons, the split buttons of
    /// Draw Order, Paste, Linear and Continue showing the choice picked last, Group Selection switching PICKSTYLE, Annotate's
    /// Dimensions slide-out, and the Text Editor tab fitted to 1 600 pixels.
    /// </summary>
    public class RibbonPanelSizeTests
    {
        private static readonly string[] HomeOrder =
        {
            "Draw",
            "Modify",
            "Annotation",
            "Layers",
            "Properties",
            "Groups",
            "Utilities",
            "Clipboard",
            "View",
        };

        [Fact]
        public void HomeShowsItsPanelsInAutoCadsOrder_AndAt2300PixelsNoneShrinks()
        {
            OnWorkspace(2300, (model, workspace) =>
            {
                List<RibbonPanel> panels = HomePanels(model, workspace);

                Assert.Equal(HomeOrder, panels.Select(p => p.Title));
                Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
                Assert.Equal(0, HomeStrip(model, workspace).StepsTaken);
            });
        }

        [Fact]
        public void At1600Pixels_ViewClipboardUtilitiesAndGroupsCollapse_AndPropertiesLayersAndDrawGoMedium()
        {
            OnWorkspace(1600, (model, workspace) =>
            {
                List<RibbonPanel> panels = HomePanels(model, workspace);
                var sizes = panels.ToDictionary(p => p.Title, p => p.Size);

                Assert.Equal(7, HomeStrip(model, workspace).StepsTaken);
                Assert.Equal(RibbonPanelSize.Medium, sizes["Draw"]);
                Assert.Equal(RibbonPanelSize.Full, sizes["Modify"]);
                Assert.Equal(RibbonPanelSize.Full, sizes["Annotation"]);
                Assert.Equal(RibbonPanelSize.Medium, sizes["Layers"]);
                Assert.Equal(RibbonPanelSize.Medium, sizes["Properties"]);
                Assert.Equal(RibbonPanelSize.Collapsed, sizes["Groups"]);
                Assert.Equal(RibbonPanelSize.Collapsed, sizes["Utilities"]);
                Assert.Equal(RibbonPanelSize.Collapsed, sizes["Clipboard"]);
                Assert.Equal(RibbonPanelSize.Collapsed, sizes["View"]);
                Assert.True(HomeStrip(model, workspace).DesiredSize.Width <= 1600 - 24);

                // Medium Draw keeps Rectangle, drawn by its icon alone and still named for the tooltip and screen readers.
                Button rectangle = Descendants(panels[0])
                    .OfType<Button>()
                    .Single(b => "RECTANG".Equals(b.CommandParameter));
                Assert.Equal("Rectangle", AutomationProperties.GetName(rectangle));
                Assert.DoesNotContain(Descendants(rectangle).OfType<TextBlock>(), t => t.Text == "Rectangle");
                Assert.Single(Descendants(rectangle).OfType<IconView>());
            });
        }

        [Fact]
        public void ACollapsedPanelDropsItsFullButtons_UnderATitleBarWithAPin_AndTakesThemBackWhenShut()
        {
            OnWorkspace(1600, (model, workspace) =>
            {
                RibbonPanel clipboard = HomePanels(model, workspace).Single(p => p.Title == "Clipboard");
                var popup = (Popup)clipboard.Template.FindName("PART_Popup", clipboard);
                string[] commands = { "PASTECLIP", "CUTCLIP", "COPYCLIP", "COPYBASE" };

                Assert.Equal(RibbonPanelSize.Collapsed, clipboard.Size);
                Assert.False(popup.IsOpen);

                clipboard.IsDropDownOpen = true;

                List<Button> dropped = Descendants(popup.Child)
                    .OfType<Button>()
                    .Where(b => b.CommandParameter is string)
                    .ToList();
                Assert.Equal(commands, dropped.Select(b => (string)b.CommandParameter));
                Assert.Contains(Descendants(popup.Child).OfType<TextBlock>(), t => t.Text == "Clipboard");
                ToggleButton pin = Descendants(popup.Child)
                    .OfType<ToggleButton>()
                    .Single(t => Equals(t.ToolTip, "Pin the panel open"));

                foreach (Button button in dropped)
                {
                    Press(
                        model,
                        button.IsEnabled,
                        () => button.Command.Execute(button.CommandParameter),
                        (string)button.CommandParameter
                    );
                }

                pin.IsChecked = true;
                Assert.True(clipboard.IsPinned);

                clipboard.IsDropDownOpen = false;

                Assert.DoesNotContain(Descendants(popup.Child).OfType<Button>(), b => b.CommandParameter is string);
                Assert.Equal(
                    commands,
                    Descendants(clipboard)
                        .OfType<Button>()
                        .Where(b => b.CommandParameter is string)
                        .Select(b => (string)b.CommandParameter)
                );
            });
        }

        [Fact]
        public void ADropDownNearTheRightEdgeShiftsLeftToStayInTheWindow()
        {
            Assert.Equal(-90.0, RibbonPanel.ShiftToFit(1400.0, 290.0, 1600.0), 9);
            Assert.Equal(0.0, RibbonPanel.ShiftToFit(100.0, 290.0, 1600.0), 9);
            Assert.Equal(-10.0, RibbonPanel.ShiftToFit(10.0, 2000.0, 1600.0), 9);
        }

        [Fact]
        public void SendToBackPickedFromDrawOrder_LeavesTheButtonRunningIt_AndBackIsGivenOnceTheObjectsArePicked()
        {
            OnWorkspace(1600, (model, workspace) =>
            {
                RibbonPanel modify = HomePanels(model, workspace).Single(p => p.Title == "Modify");
                Button order = Descendants(modify)
                    .OfType<Button>()
                    .Single(b => "DRAWORDER F".Equals(b.CommandParameter));
                Button arrow = SplitArrowOf(order);
                List<MenuItem> items = MenuOf(arrow);

                Assert.Equal(
                    new[] { "DRAWORDER F", "DRAWORDER B", "DRAWORDER A", "DRAWORDER U", "TEXTTOFRONT T" },
                    items.Select(i => (string)i.CommandParameter)
                );
                Assert.Same(model.StartCommandWithOptionCommand, order.Command);

                Pick(items.Single(i => "DRAWORDER B".Equals(i.CommandParameter)));
                model.Escape();

                Assert.Equal("Send to Back", order.Content);
                Assert.Equal("Send to Back", AutomationProperties.GetName(order));
                Assert.Equal("DRAWORDER B", order.CommandParameter);
                Assert.Same(items[1].Tag, order.Tag);

                int before = model.History.Count;
                order.Command.Execute(order.CommandParameter);
                Assert.Contains("Command: DRAWORDER", model.History.Skip(before));
                Assert.True(model.IsCommandRunning);
                model.Execute("ALL");
                model.Enter();

                Assert.False(model.IsCommandRunning);
                Assert.Contains(model.History.Skip(before), l =>
                    l.StartsWith("Enter object ordering option", StringComparison.Ordinal)
                    && l.EndsWith(" B", StringComparison.Ordinal)
                );

                // Bring Text to Front asks nothing: TEXTTOFRONT is given Text at once.
                Pick(items.Single(i => "TEXTTOFRONT T".Equals(i.CommandParameter)));
                Assert.Equal("TEXTTOFRONT T", order.CommandParameter);
                Assert.False(model.IsCommandRunning);
                Assert.Contains(model.History, l =>
                    l.StartsWith("Bring to front", StringComparison.Ordinal)
                    && l.EndsWith(" T", StringComparison.Ordinal)
                );
            });
        }

        [Fact]
        public void PasteAsBlockPickedFromPastesArrow_LeavesTheTopRunningIt_TheLabelStillPaste()
        {
            OnWorkspace(2300, (model, workspace) =>
            {
                RibbonPanel clipboard = HomePanels(model, workspace).Single(p => p.Title == "Clipboard");
                Button paste = Descendants(clipboard)
                    .OfType<Button>()
                    .Single(b => "PASTECLIP".Equals(b.CommandParameter));
                Button arrow = SplitArrowOf(paste);
                List<MenuItem> items = MenuOf(arrow);

                Assert.Equal(
                    new[] { "PASTECLIP", "PASTEBLOCK", "PASTEORIG", "PASTESPEC" },
                    items.Select(i => (string)i.CommandParameter)
                );

                Pick(items[1]);
                model.Escape();

                Assert.Equal("Paste as Block", paste.Content);
                Assert.Equal("PASTEBLOCK", paste.CommandParameter);
                Assert.Equal("Paste ▾", arrow.Content);
                Press(model, paste.IsEnabled, () => paste.Command.Execute(paste.CommandParameter), "PASTEBLOCK");
            });
        }

        [Fact]
        public void RadiusAndBaselinePickedOnAnnotate_LeaveTheirButtonsReadingAndRunningThem()
        {
            OnWorkspace(1600, (model, workspace) =>
            {
                model.SelectedTab = model.AnnotateTab;
                Layout(workspace, 1600);
                RibbonPanel dimensions = Descendants(workspace)
                    .OfType<RibbonPanel>()
                    .Single(p => p.Title == "Dimensions");
                Assert.Equal(RibbonPanelSize.Full, dimensions.Size);

                Button linear = Descendants(dimensions)
                    .OfType<Button>()
                    .Single(b => "DIMLINEAR".Equals(b.CommandParameter));
                Button linearArrow = SplitArrowOf(linear);
                Assert.Equal("Linear ▾", Label(linearArrow));
                List<MenuItem> types = MenuOf(linearArrow);
                Assert.Equal(
                    new[] { "DIMLINEAR", "DIMALIGNED", "DIMANGULAR", "DIMRADIUS", "DIMDIAMETER" },
                    types.Select(i => (string)i.CommandParameter)
                );

                Pick(types[3]);
                model.Escape();
                Layout(workspace, 1600);

                Assert.Equal("Radius", linear.Content);
                Assert.Equal("Radius ▾", Label(linearArrow));
                Press(model, linear.IsEnabled, () => linear.Command.Execute(linear.CommandParameter), "DIMRADIUS");

                Button continueButton = Descendants(dimensions)
                    .OfType<Button>()
                    .Single(b => "DIMCONTINUE".Equals(b.CommandParameter));
                List<MenuItem> chains = MenuOf(SplitArrowOf(continueButton));
                Assert.Equal(new[] { "DIMCONTINUE", "DIMBASELINE" }, chains.Select(i => (string)i.CommandParameter));

                Pick(chains[1]);
                model.Escape();

                Assert.Equal("Baseline", continueButton.Content);
                Press(
                    model,
                    continueButton.IsEnabled,
                    () => continueButton.Command.Execute(continueButton.CommandParameter),
                    "DIMBASELINE"
                );

                // Quick, Adjust Space and Break start theirs.
                foreach (string command in new[] { "QDIM", "DIMSPACE", "DIMBREAK" })
                {
                    Button button = Descendants(dimensions)
                        .OfType<Button>()
                        .Single(b => command.Equals(b.CommandParameter));
                    Press(model, button.IsEnabled, () => button.Command.Execute(button.CommandParameter), command);
                }
            });
        }

        [Fact]
        public void TheDimensionsSlideOutGivesObliqueAndTextAngle_AsDimEditOAndR()
        {
            OnWorkspace(1600, (model, workspace) =>
            {
                model.SelectedTab = model.AnnotateTab;
                Layout(workspace, 1600);
                RibbonPanel dimensions = Descendants(workspace)
                    .OfType<RibbonPanel>()
                    .Single(p => p.Title == "Dimensions");
                ToggleButton title = Descendants(dimensions)
                    .OfType<ToggleButton>()
                    .Single(t => Equals(t.Content, "Dimensions ▾"));
                var popup = (Popup)dimensions.Template.FindName("PART_Popup", dimensions);

                title.IsChecked = true;
                popup.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

                Assert.True(dimensions.IsDropDownOpen);
                List<Button> items = Descendants(popup.Child)
                    .OfType<Button>()
                    .Where(b => b.CommandParameter is string)
                    .ToList();
                Assert.Equal(new[] { "DIMEDIT O", "DIMEDIT R" }, items.Select(b => (string)b.CommandParameter));

                foreach (Button item in items)
                {
                    string option = ((string)item.CommandParameter).Substring(8);
                    int before = model.History.Count;
                    Assert.True(item.IsEnabled);
                    item.Command.Execute(item.CommandParameter);
                    Assert.Contains("Command: DIMEDIT", model.History.Skip(before));
                    Assert.Contains(model.History.Skip(before), l =>
                        l.StartsWith("Enter type of dimension editing", StringComparison.Ordinal)
                        && l.EndsWith(" " + option, StringComparison.Ordinal)
                    );
                    model.Escape();
                }
            });
        }

        [Fact]
        public void GroupSelectionIsPressedWhilePickStyleIsOne_AndPressingItSwitchesPickStyle()
        {
            OnWorkspace(1600, (model, workspace) =>
            {
                RibbonPanel groups = HomePanels(model, workspace).Single(p => p.Title == "Groups");
                ToggleButton toggle = Descendants(groups)
                    .OfType<ToggleButton>()
                    .Single(t => Equals(t.Content, "Group Selection On/Off"));
                Assert.Equal(1, model.Document.PickStyle);
                Assert.True(toggle.IsEnabled);
                Assert.True(toggle.IsChecked);

                toggle.IsChecked = false;

                Assert.Equal(0, model.Document.PickStyle);
                Assert.Equal("<Group off>", model.History.Last());

                model.Execute("PICKSTYLE");
                model.Execute("1");

                Assert.True(toggle.IsChecked);

                // Group and Ungroup start their commands; Quick Select is there to start QSELECT, whose dialog is not shown here;
                // Group Edit and Table are not built yet.
                foreach (string command in new[] { "GROUP", "UNGROUP" })
                {
                    Button button = Descendants(workspace)
                        .OfType<Button>()
                        .Single(b => command.Equals(b.CommandParameter) && b.Command == model.StartCommandCommand);
                    Press(model, button.IsEnabled, () => button.Command.Execute(button.CommandParameter), command);
                }

                Button quickSelect = Descendants(workspace)
                    .OfType<Button>()
                    .Single(b => "QSELECT".Equals(b.CommandParameter));
                Assert.True(quickSelect.IsEnabled);
                Assert.Same(model.StartCommandCommand, quickSelect.Command);

                Assert.False(
                    Descendants(groups).OfType<Button>().Single(b => Equals(b.Content, "Group Edit")).IsEnabled
                );
                Assert.False(Descendants(workspace).OfType<Button>().Single(b => Equals(b.Content, "Table")).IsEnabled);
            });
        }

        [Fact]
        public void TheTextEditorTabFits1600Pixels_CloseTheLastPanel_AndAt2300NoneShrinks()
        {
            OnWorkspace(1600, (model, workspace) =>
            {
                model.Execute("MTEXT");
                model.Execute("0,100");
                model.Execute("200,40");
                Assert.Same(model.TextEditorTab, model.SelectedTab);
                Layout(workspace, 1600);

                RibbonStrip strip = Descendants(workspace)
                    .OfType<RibbonStrip>()
                    .Single(s => s.DataContext is TextEditorViewModel);
                List<RibbonPanel> panels = strip.Children.OfType<RibbonPanel>().ToList();
                var sizes = panels.ToDictionary(p => p.Title, p => p.Size);

                Assert.Equal(
                    new[] { "Style", "Formatting", "Paragraph", "Insert", "Spell Check", "Tools", "Options", "Close" },
                    panels.Select(p => p.Title)
                );
                Assert.True(strip.DesiredSize.Width <= 1600 - 24, "The tab is " + strip.DesiredSize.Width + " wide.");
                Assert.InRange(strip.StepsTaken, 1, 5);
                Assert.Equal(RibbonPanelSize.Collapsed, sizes["Spell Check"]);
                Assert.Equal(RibbonPanelSize.Full, sizes["Style"]);
                Assert.Equal(RibbonPanelSize.Full, sizes["Formatting"]);
                Assert.Equal(RibbonPanelSize.Full, sizes["Close"]);
                Assert.Same(panels.Last(), strip.Children[strip.Children.Count - 1]);

                // Close Text Editor is drawn inside the window.
                RibbonPanel close = panels.Last();
                Assert.True(close.TranslatePoint(new Point(close.ActualWidth, 0), workspace).X <= 1600);

                Layout(workspace, 2300);
                Assert.Equal(0, strip.StepsTaken);
                Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
            });
        }

        /// <summary>Runs a check on the window drawn offscreen at a width on a thread of its own, a room drawing open.</summary>
        private static void OnWorkspace(double width, Action<MainViewModel, CadWorkspace> check)
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
                            Layout(workspace, width);
                            check(model, workspace);
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

        private static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 960));
            element.Arrange(new Rect(0, 0, width, 960));
            element.UpdateLayout();
        }

        private static RibbonStrip HomeStrip(MainViewModel model, CadWorkspace workspace) =>
            Descendants(workspace).OfType<RibbonStrip>().Single(s => ReferenceEquals(s.DataContext, model));

        private static List<RibbonPanel> HomePanels(MainViewModel model, CadWorkspace workspace) =>
            HomeStrip(model, workspace).Children.OfType<RibbonPanel>().ToList();

        /// <summary>The arrow beside the button of a split, which drops its choices.</summary>
        private static Button SplitArrowOf(Button main) =>
            (Button)((Panel)VisualTreeHelper.GetParent(main)).Children[1];

        /// <summary>The words a button shows, its content formatted as its presenter formats it.</summary>
        private static string Label(Button button) =>
            string.IsNullOrEmpty(button.ContentStringFormat)
                ? (string)button.Content
                : string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    button.ContentStringFormat,
                    button.Content
                );

        /// <summary>The items of a button's menu, the menu taking the window's model from the button, its items bound at the dispatcher's next turn.</summary>
        private static List<MenuItem> MenuOf(Button opener)
        {
            ContextMenu menu = opener.ContextMenu;
            menu.PlacementTarget = opener;
            opener.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            return menu.Items.OfType<MenuItem>().ToList();
        }

        /// <summary>An item clicked as the mouse clicks it, its Click raised and its command run.</summary>
        private static void Pick(MenuItem item)
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
            item.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        /// <summary>A button enabled, pressed, and the command it names started; then Esc.</summary>
        private static void Press(MainViewModel model, bool enabled, Action press, string command)
        {
            Assert.True(enabled, command + " is not enabled.");
            int before = model.History.Count;
            press();
            Assert.Contains("Command: " + command, model.History.Skip(before));
            Assert.DoesNotContain(model.History.Skip(before), l =>
                l.StartsWith("Unknown command", StringComparison.Ordinal)
            );
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
