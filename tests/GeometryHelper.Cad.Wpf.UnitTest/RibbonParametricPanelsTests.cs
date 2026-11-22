using System;
using System.Collections.Generic;
using System.Globalization;
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
using static GeometryHelper.Cad.Wpf.UnitTest.RibbonWave4Harness;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Parametric as AutoCAD 2021's (D1341), found in the window drawn offscreen: Geometric (Auto Constrain large, the twelve
    /// constraints as three rows of four tiles, a separator, then Show/Hide, Show All and Hide All with text, the launcher of
    /// Constraint Settings on its Geometric tab), Dimensional (Linear as a large split of Linear, Horizontal and Vertical, Aligned
    /// large, four tiles, Show/Hide, Show All and Hide All, the slide-out of the two constraint modes, the launcher on its
    /// Dimensional tab) and Manage (Delete Constraints and Parameters Manager large, no Constraint Settings button). The tab has no
    /// steps: its panels are full down to AutoCAD's narrowest ribbon, 884 pixels. A strip width is the width the RibbonStrip gets,
    /// which is the window's less the pixels the window gives to what is around it. The tiles are 24 by 22, the rows 24 apart, the
    /// large buttons 72 high.
    /// </summary>
    public sealed class RibbonParametricPanelsTests
    {
        // A strip so wide that nothing could shrink.
        private const double Wide = 3000;

        private const double PanelHeight = 98;
        private const double LargeHeight = 72;
        private const double TileWidth = 24;
        private const double TileHeight = 22;
        private const double Pitch = 24;

        #region What each panel holds (name|command; an empty part is not checked)

        // The body of Geometric read as the harness reads it, columns from left to right, each column from top to bottom: the
        // twelve tiles are three rows of four (Coincident, Collinear, Concentric, Fix / Parallel, Perpendicular, Horizontal,
        // Vertical / Tangent, Smooth, Symmetric, Equal), so the first column is Coincident, Parallel, Tangent.
        private static readonly string[] GeometricBody =
        {
            "Auto Constrain|AUTOCONSTRAIN",
            "Coincident|GCCOINCIDENT",
            "Parallel|GCPARALLEL",
            "Tangent|GCTANGENT",
            "Collinear|GCCOLLINEAR",
            "Perpendicular|GCPERPENDICULAR",
            "Smooth|GCSMOOTH",
            "Concentric|GCCONCENTRIC",
            "Horizontal|GCHORIZONTAL",
            "Symmetric|GCSYMMETRIC",
            "Fix|GCFIX",
            "Vertical|GCVERTICAL",
            "Equal|GCEQUAL",
            "Show/Hide|CONSTRAINTBAR",
            "Show All|",
            "Hide All|",
        };

        // Linear and Aligned large, then two rows of two tiles (Radius, Diameter / Angular, Convert), read by columns, then the
        // three buttons of DCDISPLAY with text.
        private static readonly string[] DimensionalBody =
        {
            "Linear|DCLINEAR",
            "Aligned|DCALIGNED",
            "Radius|DCRADIUS",
            "Angular|DCANGULAR",
            "Diameter|DCDIAMETER",
            "Convert|DCCONVERT",
            "Show/Hide|DCDISPLAY",
            "Show All|DCDISPLAY",
            "Hide All|DCDISPLAY",
        };

        private static readonly string[] ManageBody =
        {
            "Delete Constraints|DELCONSTRAINT",
            "Parameters Manager|PARAMETERS",
        };

        #endregion

        #region The panels and their order

        [Fact]
        public void ParametricShowsGeometricDimensionalAndManage_InAutoCadsOrder_AndNoneShrinksDownTo884()
        {
            // 884 is AutoCAD's narrowest ribbon (a window of 900 less its border): the three panels need about 630.
            OnTab(
                m => m.ParametricTab,
                884,
                (model, workspace, strip) =>
                {
                    List<RibbonPanel> panels = Panels(strip);

                    Assert.Equal(new[] { "Geometric", "Dimensional", "Manage" }, panels.Select(p => p.Title));
                    Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
                    Assert.Equal(0, strip.StepsTaken);
                    Assert.True(string.IsNullOrWhiteSpace(strip.Steps), "Parametric has steps: " + strip.Steps);
                    Assert.Equal(PanelHeight, strip.ActualHeight, 0.5);
                    Assert.All(panels, p => Assert.Equal(PanelHeight, p.ActualHeight, 0.5));
                }
            );
        }

        #endregion

        #region The bodies

        [Fact]
        public void TheBodyOfGeometricIsAutoConstrainLarge_ThenTheTwelveConstraintsAsThreeRowsOfFourTiles_ThenShowHideShowAllAndHideAll()
        {
            OnTab(
                m => m.ParametricTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel geometric = Panel(strip, "Geometric");
                    List<FrameworkElement> controls = BodyControls(geometric);

                    AssertControls(GeometricBody, controls);

                    // Auto Constrain is a large button with its label.
                    Assert.IsNotType<ComboBox>(controls[0]);
                    Assert.Equal(LargeHeight, controls[0].ActualHeight, 0.5);
                    Assert.True(HasText(controls[0]), "Auto Constrain shows no label.");

                    // The twelve are icon-only tiles in four columns of three: 24 by 22, 24 apart both ways, each named for the screen
                    // reader.
                    List<FrameworkElement> tiles = controls.Skip(1).Take(12).ToList();
                    Assert.All(tiles, t => Assert.False(HasText(t), NameOf(t) + " shows text."));
                    Assert.All(tiles, t => Assert.Equal(TileWidth, t.ActualWidth, 0.5));
                    Assert.All(tiles, t => Assert.Equal(TileHeight, t.ActualHeight, 0.5));
                    Assert.All(tiles, t =>
                        Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(t)), "A tile has no name.")
                    );

                    for (int column = 0; column < 4; column++)
                    {
                        for (int row = 0; row < 3; row++)
                        {
                            FrameworkElement tile = tiles[(column * 3) + row];
                            // Columns are a tile and a pixel or two apart.
                            Assert.InRange(
                                Left(tile, geometric.Body) - Left(tiles[0], geometric.Body),
                                (column * TileWidth) - 0.5,
                                (column * (TileWidth + 2)) + 0.5
                            );
                            Assert.Equal(Left(tiles[column * 3], geometric.Body), Left(tile, geometric.Body), 0.5);
                            Assert.Equal(Top(tiles[0], geometric.Body) + (row * Pitch), Top(tile, geometric.Body), 0.5);
                        }
                    }

                    // Show/Hide, Show All and Hide All are one column, three rows 24 apart, each with its text.
                    List<FrameworkElement> bars = controls.Skip(13).ToList();
                    Assert.All(bars, b => Assert.True(HasText(b), NameOf(b) + " shows no text."));
                    Assert.All(bars, b => Assert.Equal(TileHeight, b.ActualHeight, 0.5));

                    for (int row = 1; row < 3; row++)
                    {
                        Assert.Equal(Left(bars[0], geometric.Body), Left(bars[row], geometric.Body), 0.5);
                        Assert.Equal(Top(bars[0], geometric.Body) + (row * Pitch), Top(bars[row], geometric.Body), 0.5);
                    }

                    // The bars are apart from the tiles by more than a gap between two tiles (the separator).
                    Assert.True(
                        Left(bars[0], geometric.Body) - (Left(tiles[9], geometric.Body) + TileWidth) >= 4,
                        "No separator stands between the tiles and Show/Hide."
                    );
                }
            );
        }

        [Fact]
        public void TheBodyOfDimensionalIsTheLinearSplitAndAlignedLarge_ThenFourTiles_ThenShowHideShowAllAndHideAll()
        {
            OnTab(
                m => m.ParametricTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel dimensional = Panel(strip, "Dimensional");
                    List<FrameworkElement> controls = BodyControls(dimensional);

                    AssertControls(DimensionalBody, controls);

                    // Linear and Aligned are large, Linear a split (the icon, then ▾), Aligned not.
                    var linear = (ButtonBase)controls[0];
                    var aligned = (ButtonBase)controls[1];
                    Assert.Equal(LargeHeight, LargeHeightOf(linear), 0.5);
                    Assert.Equal(LargeHeight, LargeHeightOf(aligned), 0.5);
                    Assert.NotNull(SplitArrowOf(linear));
                    Assert.Null(SplitArrowOf(aligned));
                    Assert.True(HasText(aligned), "Aligned shows no label.");

                    // The four tiles: two rows of two, no text, 24 by 22 and 24 apart.
                    List<FrameworkElement> tiles = controls.Skip(2).Take(4).ToList();
                    Assert.All(tiles, t => Assert.False(HasText(t), NameOf(t) + " shows text."));
                    Assert.All(tiles, t => Assert.Equal(TileWidth, t.ActualWidth, 0.5));
                    Assert.Equal(Pitch, Top(tiles[1], dimensional.Body) - Top(tiles[0], dimensional.Body), 0.5);
                    Assert.InRange(
                        Left(tiles[2], dimensional.Body) - Left(tiles[0], dimensional.Body),
                        TileWidth - 0.5,
                        TileWidth + 2.5
                    );

                    // The three of DCDISPLAY have text and stand in a column 24 apart.
                    List<FrameworkElement> bars = controls.Skip(6).ToList();
                    Assert.All(bars, b => Assert.True(HasText(b), NameOf(b) + " shows no text."));
                    Assert.Equal(Pitch, Top(bars[1], dimensional.Body) - Top(bars[0], dimensional.Body), 0.5);
                    Assert.Equal(Pitch, Top(bars[2], dimensional.Body) - Top(bars[1], dimensional.Body), 0.5);

                    // The arrow of Linear drops Linear, Horizontal and Vertical, each starting its own command.
                    AssertMenu(
                        model,
                        SplitArrowOf(linear),
                        new[] { new[] { "Linear", "Horizontal", "Vertical" } },
                        null,
                        new[] { "DCLINEAR", "DCHORIZONTAL", "DCVERTICAL" }
                    );
                }
            );
        }

        [Fact]
        public void ManageHoldsDeleteConstraintsAndParametersManager_AndNoConstraintSettingsButton()
        {
            OnTab(
                m => m.ParametricTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel manage = Panel(strip, "Manage");
                    List<FrameworkElement> controls = BodyControls(manage);

                    AssertControls(ManageBody, controls);
                    Assert.All(controls, c => Assert.Equal(LargeHeight, c.ActualHeight, 0.5));
                    Assert.All(controls, c => Assert.True(HasText(c), NameOf(c) + " shows no label."));

                    // The Constraint Settings button is gone (the two launchers replace it), and the panel has no launcher itself.
                    Assert.DoesNotContain(Descendants(manage).OfType<ButtonBase>(), b =>
                        CommandWord(b) == "CONSTRAINTSETTINGS" || Words(b).Contains("Constraint Settings")
                    );
                    Assert.Null(manage.Launcher);
                }
            );
        }

        #endregion

        #region The slide-out and the launchers

        [Fact]
        public void TheSlideOutOfDimensionalHoldsDynamicAndAnnotationalConstraintMode_TheOneInForcePressed_AndAPickSetsCconstraintform()
        {
            OnTab(
                m => m.ParametricTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel dimensional = Panel(strip, "Dimensional");

                    // The title reads "Dimensional ▾" and opens the slide-out; Geometric and Manage have none.
                    Assert.NotNull(dimensional.SlideOut);
                    Assert.Contains(Descendants(dimensional).OfType<ToggleButton>(), t =>
                        Equals(t.Content, "Dimensional ▾")
                    );
                    Assert.Null(Panel(strip, "Geometric").SlideOut);
                    Assert.Null(Panel(strip, "Manage").SlideOut);

                    OpenSlideOut(dimensional);
                    List<List<FrameworkElement>> rows = Rows(SlideOutControls(dimensional), dimensional.SlideOut);

                    Assert.Equal(2, rows.Count);
                    AssertControls(new[] { "Dynamic Constraint Mode|" }, rows[0]);
                    AssertControls(new[] { "Annotational Constraint Mode|" }, rows[1]);
                    Assert.Equal(
                        Pitch,
                        Top(rows[1][0], dimensional.SlideOut) - Top(rows[0][0], dimensional.SlideOut),
                        0.5
                    );
                    Assert.All(rows.SelectMany(r => r), c => Assert.True(HasText(c), NameOf(c) + " shows no text."));

                    var dynamic = Assert.IsAssignableFrom<ToggleButton>(rows[0][0]);
                    var annotational = Assert.IsAssignableFrom<ToggleButton>(rows[1][0]);

                    // CCONSTRAINTFORM is 0 at the start: the dynamic mode is pressed.
                    Assert.Equal(0, model.Session.Constraints.Form);
                    Assert.True(dynamic.IsChecked);
                    Assert.False(annotational.IsChecked);

                    // A pick of the annotational mode sets CCONSTRAINTFORM 1, and the pressed one follows.
                    Click(annotational);

                    Assert.Equal(1, model.Session.Constraints.Form);
                    Assert.True(annotational.IsChecked);
                    Assert.False(dynamic.IsChecked);

                    // And the other way, and a change made on the command line is followed too.
                    Click(dynamic);

                    Assert.Equal(0, model.Session.Constraints.Form);
                    Assert.True(dynamic.IsChecked);

                    model.Execute("CCONSTRAINTFORM");
                    model.Execute("1");
                    Pump(dimensional);

                    Assert.True(annotational.IsChecked);
                    Assert.False(dynamic.IsChecked);
                }
            );
        }

        [Fact]
        public void TheLaunchersOfGeometricAndDimensionalOpenConstraintSettingsOnTheirTabs()
        {
            OnTab(
                m => m.ParametricTab,
                Wide,
                (model, workspace, strip) =>
                {
                    // The dialog is answered Cancel and its tab noted: 0 Geometric, 1 Dimensional.
                    var tabs = new List<int>();
                    model.ShowConstraintSettings = dialog =>
                    {
                        tabs.Add(dialog.Tab);
                        return false;
                    };

                    RibbonPanel geometric = Panel(strip, "Geometric");
                    RibbonPanel dimensional = Panel(strip, "Dimensional");

                    Assert.NotNull(geometric.Launcher);
                    Assert.NotNull(dimensional.Launcher);
                    Assert.Null(Panel(strip, "Manage").Launcher);
                    Assert.Contains("CONSTRAINTSETTINGS", Assert.IsType<string>(geometric.LauncherToolTip));
                    Assert.Contains("CONSTRAINTSETTINGS", Assert.IsType<string>(dimensional.LauncherToolTip));
                    Assert.Contains("Geometric", (string)geometric.LauncherToolTip);
                    Assert.Contains("Dimensional", (string)dimensional.LauncherToolTip);

                    Assert.True(geometric.Launcher.CanExecute(null));
                    geometric.Launcher.Execute(null);
                    model.Escape();
                    Assert.True(dimensional.Launcher.CanExecute(null));
                    dimensional.Launcher.Execute(null);
                    model.Escape();

                    Assert.Equal(new[] { 0, 1 }, tabs);
                    Assert.Contains("Command: CONSTRAINTSETTINGS", model.History);
                }
            );
        }

        #endregion
    }

    /// <summary>
    /// The window drawn offscreen and the helpers the four test files of ribbon wave 4 (Parametric, View, Manage, Output) share:
    /// the tab laid out at a strip width, the controls of a panel in reading order, "name|command" lines, the menu of a split,
    /// a real click. It follows RibbonHomePanelsTests, which keeps its own copy private.
    /// </summary>
    internal static class RibbonWave4Harness
    {
        #region The window

        /// <summary>
        /// The tab chosen laid out in the window drawn offscreen so that its strip is a width wide, on a thread of its own, a
        /// room drawing open. The four dialog hooks that a stray pick would show in a window are answered Cancel after the
        /// workspace is made (it overwrites them).
        /// </summary>
        /// <param name="tab">The tab to show, of the model.</param>
        /// <param name="stripWidth">The width the strip is given.</param>
        /// <param name="check">What to check, over the model, the workspace and the strip.</param>
        /// <param name="before">What to do to the model after the drawing is open and before the window is made.</param>
        internal static void OnTab(
            Func<MainViewModel, CadWorkspaceExtension> tab,
            double stripWidth,
            Action<MainViewModel, CadWorkspace, RibbonStrip> check,
            Action<MainViewModel> before = null
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
                        before?.Invoke(model);
                        var workspace = new CadWorkspace { DataContext = model };
                        model.ShowBoundaryCreation = dialog => false;
                        model.ShowCheckSpelling = dialog => { };
                        model.ShowViewports = dialog => false;
                        model.ShowMissingShx = dialog => false;

                        try
                        {
                            model.SelectedTab = tab(model);
                            // The window gives its strip a width less what is around it: read at a wide window, then laid out again.
                            Layout(workspace, 3200);
                            RibbonStrip strip = Descendants(workspace)
                                .OfType<RibbonStrip>()
                                .Single(s => ReferenceEquals(s.DataContext, tab(model).TabContent));
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
        /// Takes the window's own answer to a request of Global settings away, so that none is shown, and notes the category
        /// each request names instead.
        /// </summary>
        internal static List<string> NoteSettingsRequests(MainViewModel model)
        {
            typeof(MainViewModel)
                .GetField("_settingsRequested", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(model, null);
            var categories = new List<string>();
            model.SettingsRequested += (sender, category) => categories.Add(category);
            return categories;
        }

        private static void Layout(FrameworkElement element, double width)
        {
            element.Measure(new Size(width, 960));
            element.Arrange(new Rect(0, 0, width, 960));
            element.UpdateLayout();
        }

        internal static void Pump(DispatcherObject element) =>
            element.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));

        internal static string Squeeze(string text) => text.Replace(" ", string.Empty);

        #endregion

        #region Panels and their controls

        internal static List<RibbonPanel> Panels(RibbonStrip strip) => strip.Children.OfType<RibbonPanel>().ToList();

        internal static RibbonPanel Panel(RibbonStrip strip, string title) =>
            Assert.Single(Panels(strip), p => p.Title == title);

        /// <summary>The sizes of the panels in order, as F, M and C.</summary>
        internal static string Sizes(RibbonStrip strip) =>
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
        internal static void OpenSlideOut(RibbonPanel panel)
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
        /// combo box; the part itself where it is one control (a panel whose whole body is a button).
        /// </summary>
        internal static List<FrameworkElement> ControlsOf(DependencyObject root) =>
            new[] { root }
                .Concat(Descendants(root))
                .OfType<FrameworkElement>()
                .Where(e => ((e is ButtonBase b && !IsArrow(b)) || e is ComboBox) && !InsideCompound(e))
                .ToList();

        /// <summary>The body's controls in reading order of its columns: left to right, each column top to bottom.</summary>
        internal static List<FrameworkElement> BodyControls(RibbonPanel panel)
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

        internal static List<FrameworkElement> SlideOutControls(RibbonPanel panel) => ControlsOf(panel.SlideOut);

        /// <summary>Controls in rows, top to bottom, each row left to right.</summary>
        internal static List<List<FrameworkElement>> Rows(List<FrameworkElement> controls, UIElement relativeTo)
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

        /// <summary>Controls against "name|command" lines: where a part is empty the control's own is taken, so that it is not read.</summary>
        internal static void AssertControls(string[] expected, List<FrameworkElement> actual)
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
            ButtonBase button = control as ButtonBase;
            return (parts[0].Length > 0 ? parts[0] : NameOf(control))
                + "|"
                + (parts[1].Length > 0 ? parts[1] : CommandWord(button));
        }

        private static string Describe(FrameworkElement control) =>
            control is ComboBox ? "combo" : NameOf(control) + "|" + CommandWord(control as ButtonBase);

        /// <summary>The first word of the command a button starts: CIRCLE of "CIRCLE 2P"; empty where it starts none by name.</summary>
        internal static string CommandWord(FrameworkElement control) =>
            control is ButtonBase button
                ? ((button.CommandParameter as string) ?? string.Empty).Split(' ')[0]
                : string.Empty;

        /// <summary>The words a control is known by: its name for screen readers, or its text where it has no other.</summary>
        internal static string NameOf(FrameworkElement control)
        {
            string name = AutomationProperties.GetName(control);

            if (string.IsNullOrEmpty(name) && control is ContentControl content)
            {
                name = content.Content as string;
            }

            return name ?? string.Empty;
        }

        /// <summary>Everything a control says of itself, for finding what it is: its name, its tooltip, its content.</summary>
        internal static string Words(FrameworkElement control) =>
            NameOf(control)
            + " "
            + (control.ToolTip as string)
            + " "
            + (AutomationProperties.GetHelpText(control) ?? string.Empty);

        internal static bool HasText(FrameworkElement control) =>
            Descendants(control).OfType<TextBlock>().Any(t => !string.IsNullOrWhiteSpace(t.Text));

        /// <summary>The label a split shows, its texts run together without spaces: "Circle▾", whether the ▾ stands on a line of its own or not.</summary>
        internal static string Label(DependencyObject split) =>
            string.Concat(
                    Descendants(split).OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t))
                )
                .Replace(" ", string.Empty);

        /// <summary>The arrow of a split: the second child of the panel holding the button, as every split of the ribbon is built.</summary>
        internal static ButtonBase SplitArrowOf(ButtonBase main) =>
            VisualTreeHelper.GetParent(main) is Panel panel
            && panel.Children.Count == 2
            && ReferenceEquals(panel.Children[0], main)
            && panel.Children[1] is ButtonBase arrow
            && IsArrow(arrow)
                ? arrow
                : null;

        private static bool IsArrow(ButtonBase button) =>
            button.CommandParameter == null
            && (
                button.ContextMenu != null
                || (AutomationProperties.GetName(button) ?? string.Empty).EndsWith("gallery", StringComparison.Ordinal)
            );

        private static bool InsideCompound(DependencyObject element)
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

        /// <summary>The height of a large button: its own, or for a split the frame round its top and its arrow.</summary>
        internal static double LargeHeightOf(ButtonBase button)
        {
            ButtonBase arrow = SplitArrowOf(button);

            if (arrow == null)
            {
                return button.ActualHeight;
            }

            var panel = (FrameworkElement)VisualTreeHelper.GetParent(button);
            return VisualTreeHelper.GetParent(panel) is Border frame ? frame.ActualHeight : panel.ActualHeight;
        }

        #endregion

        #region Menus and clicks

        /// <summary>The menu of a split's arrow checked: its items in groups parted by separators, and the command each starts.</summary>
        internal static void AssertMenu(
            MainViewModel model,
            ButtonBase arrow,
            string[][] groups,
            string command,
            string[] commands = null
        )
        {
            Assert.NotNull(arrow);
            ContextMenu menu = arrow.ContextMenu;
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

            // Each item, picked, starts its command; Esc ends it.
            foreach (MenuItem item in flat)
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
        internal static string Norm(MenuItem item) =>
            string.Join(
                " ",
                ((string)item.Header ?? string.Empty)
                    .Replace(",", string.Empty)
                    .Replace("…", string.Empty)
                    .Replace("...", string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            );

        /// <summary>The items of a button's menu, the menu taking the window's model from the button, its items bound at the dispatcher's next turn.</summary>
        internal static List<MenuItem> MenuOf(ButtonBase opener)
        {
            ContextMenu menu = opener.ContextMenu;
            menu.PlacementTarget = opener;
            Pump(opener);
            return menu.Items.OfType<MenuItem>().ToList();
        }

        /// <summary>An item clicked as the mouse clicks it, its Click raised and its command run.</summary>
        internal static void Pick(MenuItem item)
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
        /// A button clicked as the mouse clicks it: a toggle switched, its Click raised, its command run. Setting IsChecked from
        /// code would run only what is bound to it, and a button of the ribbon may be bound either way.
        /// </summary>
        internal static void Click(ButtonBase button)
        {
            Assert.True(button.IsEnabled, NameOf(button) + " is not enabled.");
            MethodInfo click = typeof(ButtonBase).GetMethod(
                "OnClick",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );
            click.Invoke(button, null);
            Pump(button);
        }

        #endregion

        #region Where things are

        internal static double Left(FrameworkElement element, UIElement relativeTo) =>
            element.TranslatePoint(new Point(0, 0), relativeTo).X;

        internal static double Top(FrameworkElement element, UIElement relativeTo) =>
            element.TranslatePoint(new Point(0, 0), relativeTo).Y;

        internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
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

        /// <summary>The width a word takes in the font of a text block, in device-independent pixels.</summary>
        internal static double WidthOf(TextBlock block, string word) =>
            new FormattedText(
                word,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch),
                block.FontSize,
                Brushes.Black,
                1.0
            ).WidthIncludingTrailingWhitespace;

        #endregion
    }
}
