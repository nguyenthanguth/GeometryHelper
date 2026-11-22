using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.RibbonWave4Harness;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// View as AutoCAD 2021's (D1342, D1343), found in the window drawn offscreen: Viewport Tools (the UCS Icon toggle), Named Views
    /// (the combo, New View, View Manager), Model Viewports (the Viewport Configuration split, Named, Join, Restore), Palettes
    /// (Tool Palettes, the Properties toggle, the tiles), Interface (the Drawings toggle and the launcher of Global settings), then
    /// the panels the presenter adds, Navigate, Views and Drafting. It shrinks as AutoCAD's does below 1 100: ours first, then
    /// AutoCAD's panels. A strip width is the width the RibbonStrip gets, which is the window's less the pixels the window gives to
    /// what is around it.
    /// </summary>
    public sealed class RibbonViewPanelsTests
    {
        private const double Wide = 3000;

        private const double LargeHeight = 72;
        private const double TileWidth = 24;
        private const double TileHeight = 22;
        private const double Pitch = 24;

        // D1343, one panel and one size per step; the strip reads it parted by semicolons.
        private const string ViewSteps =
            "Drafting:Medium; Navigate:Medium; Views:Collapsed; Drafting:Collapsed; Navigate:Collapsed; Named Views:Medium; "
            + "Model Viewports:Medium; Interface:Collapsed";

        private static readonly string[] ViewOrder =
        {
            "Viewport Tools",
            "Named Views",
            "Model Viewports",
            "Palettes",
            "Interface",
            "Navigate",
            "Views",
            "Drafting",
        };

        // Tool Palettes and Properties large, then the tiles in columns: Command Line, Layer Properties, Design Center in the first
        // (row 1 holds only Command Line), Quick Calculator and External References in the second.
        private static readonly string[] PalettesBody =
        {
            "Tool Palettes|TOOLPALETTES",
            "Properties|",
            "Command Line|",
            "Layer Properties|LAYER",
            "Design Center|ADCENTER",
            "Quick Calculator|QUICKCALC",
            "External References|EXTERNALREFERENCES",
        };

        // What -VPORTS makes (SIngle, 2, 3 and 4 with their arrangements), in the order of the Viewports dialog's list.
        private static readonly string[] Layouts =
        {
            "Single",
            "Two: Vertical",
            "Two: Horizontal",
            "Three: Right",
            "Three: Left",
            "Three: Above",
            "Three: Below",
            "Three: Vertical",
            "Three: Horizontal",
            "Four: Equal",
        };

        #region The panels, their order and their small parts

        [Fact]
        public void ViewShowsViewportToolsNamedViewsModelViewportsPalettesAndInterface_ThenOurNavigateViewsAndDrafting()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    List<RibbonPanel> panels = Panels(strip);

                    Assert.Equal(ViewOrder, panels.Select(p => p.Title));
                    Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
                    Assert.Equal(0, strip.StepsTaken);

                    // No panel has a slide-out; only Interface has a launcher.
                    Assert.All(panels, p => Assert.Null(p.SlideOut));
                    Assert.All(panels.Where(p => p.Title != "Interface"), p => Assert.Null(p.Launcher));
                    Assert.NotNull(Panel(strip, "Interface").Launcher);

                    // The panels of AutoCAD stand before the presenter's own.
                    Assert.Equal(new[] { "Navigate", "Views", "Drafting" }, panels.Skip(5).Select(p => p.Title));
                }
            );
        }

        [Fact]
        public void TheUcsIconToggleIsPressedWhileUcsiconIsOn_AndPressingItTurnsTheIconOff()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel tools = Panel(strip, "Viewport Tools");
                    List<FrameworkElement> controls = BodyControls(tools);

                    AssertControls(new[] { "UCS Icon|" }, controls);
                    var toggle = Assert.IsAssignableFrom<ToggleButton>(controls[0]);
                    Assert.Equal(LargeHeight, toggle.ActualHeight, 0.5);
                    Assert.True(HasText(toggle), "UCS Icon shows no label.");

                    // The icon is on in a new drawing, and the toggle pressed.
                    Assert.True(model.Document.Ucs.IconOn);
                    Assert.True(toggle.IsChecked);

                    // A press turns the icon off, a second turns it on; each leaves no command waiting for an answer.
                    Click(toggle);

                    Assert.False(model.Document.Ucs.IconOn);
                    Assert.False(toggle.IsChecked);
                    Assert.False(model.IsCommandRunning);

                    Click(toggle);

                    Assert.True(model.Document.Ucs.IconOn);
                    Assert.True(toggle.IsChecked);
                    Assert.False(model.IsCommandRunning);

                    // UCSICON OFF typed on the command line is followed by the toggle.
                    model.Execute("UCSICON");
                    model.Execute("OFF");
                    Pump(toggle);

                    Assert.False(model.Document.Ucs.IconOn);
                    Assert.False(toggle.IsChecked);
                }
            );
        }

        [Fact]
        public void TheNamedViewsComboListsUnsavedViewThenTheDrawingsViews_AndPickingOneRestoresIt()
        {
            // Two views saved by window before the ribbon is drawn: Plan A over (0,0)-(100,80), centred at (50,40), and Plan B over
            // (200,200)-(400,360), centred at (300,280).
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel views = Panel(strip, "Named Views");
                    ComboBox combo = Assert.IsType<ComboBox>(BodyControls(views)[0]);

                    Assert.Equal(200, combo.ActualWidth, 1.0);
                    Assert.Equal(TileHeight, combo.ActualHeight, 0.5);
                    Assert.Equal(
                        new[] { "Unsaved View", "Plan A", "Plan B" },
                        combo.Items.Cast<object>().Select(i => i.ToString())
                    );
                    Assert.Equal("Unsaved View", combo.SelectedItem?.ToString());
                    Assert.NotNull(model.Session.View);

                    // A pick restores the view as -VIEW Restore does: the window now shows the view's centre.
                    combo.SelectedItem = combo.Items.Cast<object>().Single(i => i.ToString() == "Plan B");
                    Pump(combo);

                    Assert.Contains("Command: -VIEW", model.History);
                    Assert.False(model.IsCommandRunning);
                    Assert.Equal(300, model.Session.View.CenterX, 3);
                    Assert.Equal(280, model.Session.View.CenterY, 3);

                    combo.SelectedItem = combo.Items.Cast<object>().Single(i => i.ToString() == "Plan A");
                    Pump(combo);

                    Assert.False(model.IsCommandRunning);
                    Assert.Equal(50, model.Session.View.CenterX, 3);
                    Assert.Equal(40, model.Session.View.CenterY, 3);
                },
                SaveTwoViews
            );
        }

        [Fact]
        public void NewViewOpensTheNewViewDialog_AndViewManagerStartsView()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel views = Panel(strip, "Named Views");
                    List<FrameworkElement> controls = BodyControls(views);

                    AssertControls(new[] { "combo", "New View|", "View Manager|VIEW" }, controls);
                    Assert.Equal(Pitch, Top(controls[1], views.Body) - Top(controls[0], views.Body), 0.5);
                    Assert.Equal(Pitch, Top(controls[2], views.Body) - Top(controls[1], views.Body), 0.5);
                    Assert.All(controls.Skip(1), c => Assert.True(HasText(c), NameOf(c) + " shows no text."));

                    // The Views window is answered Cancel, each press counted. View Manager shows the manager. New View shows the same
                    // window, opened on its New View dialog, which a test with no window cannot see: it is read here as VIEW started
                    // and the window asked for once.
                    int managers = 0;
                    model.ShowViewManager = dialog =>
                    {
                        managers++;
                        return false;
                    };

                    Click((ButtonBase)controls[2]);

                    Assert.Contains("Command: VIEW", model.History);
                    Assert.Equal(1, managers);
                    model.Escape();

                    managers = 0;
                    int before = model.History.Count;
                    Click((ButtonBase)controls[1]);

                    Assert.Contains("Command: VIEW", model.History.Skip(before));
                    Assert.Equal(1, managers);
                    model.Escape();
                },
                SaveTwoViews
            );
        }

        #endregion

        #region Model Viewports

        [Fact]
        public void TheViewportConfigurationSplitHoldsTheLayoutsVportsMakes_EachRunningMinusVports_AndNamedJoinAndRestoreStartTheirOptions()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel viewports = Panel(strip, "Model Viewports");
                    List<FrameworkElement> controls = BodyControls(viewports);

                    AssertControls(
                        new[] { "Viewport Configuration|VPORTS", "Named|VPORTS", "Join|-VPORTS", "Restore|-VPORTS" },
                        controls
                    );

                    var configuration = (ButtonBase)controls[0];
                    Assert.Equal(LargeHeight, LargeHeightOf(configuration), 0.5);
                    Assert.NotNull(SplitArrowOf(configuration));
                    Assert.All(controls.Skip(1), c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                    Assert.Equal(Pitch, Top(controls[2], viewports.Body) - Top(controls[1], viewports.Body), 0.5);
                    Assert.Equal(Pitch, Top(controls[3], viewports.Body) - Top(controls[2], viewports.Body), 0.5);

                    // The layouts: one group, each item running -VPORTS, ending with no prompt left over.
                    AssertMenu(model, SplitArrowOf(configuration), new[] { Layouts }, "-VPORTS");

                    // A pick does what it says: Two: Vertical splits the model area, Single joins it again.
                    List<MenuItem> items = MenuOf(SplitArrowOf(configuration));
                    Pick(items.Single(i => Norm(i) == "Two: Vertical"));
                    model.Escape();
                    Assert.True(model.Document.TiledViewports.IsTiled);
                    Pick(items.Single(i => Norm(i) == "Single"));
                    model.Escape();
                    Assert.False(model.Document.TiledViewports.IsTiled);

                    // The top opens the Viewports dialog on its New Viewports tab, Named on its Named Viewports tab.
                    var tabs = new List<bool>();
                    model.ShowViewports = dialog =>
                    {
                        tabs.Add(dialog.IsNamedTab);
                        return false;
                    };

                    Click(configuration);
                    model.Escape();
                    Click((ButtonBase)controls[1]);
                    model.Escape();

                    Assert.Equal(new[] { false, true }, tabs);

                    // Join asks for the dominant viewport of a split model area, Restore for the name of a configuration.
                    Pick(items.Single(i => Norm(i) == "Two: Vertical"));
                    model.Escape();
                    Click((ButtonBase)controls[2]);

                    Assert.True(model.IsCommandRunning);
                    Assert.Contains("Select dominant viewport", model.Prompt);
                    model.Escape();

                    Click((ButtonBase)controls[3]);

                    Assert.True(model.IsCommandRunning);
                    Assert.Contains("Enter name of viewport configuration to restore", model.Prompt);
                    model.Escape();
                }
            );
        }

        [Fact]
        public void TheLabelViewportConfigurationIsNeverBrokenInsideAWord()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    var configuration = (ButtonBase)BodyControls(Panel(strip, "Model Viewports")).First();
                    // The label of a split is in its top and its arrow, which stand in one panel; of a plain button, in the button.
                    DependencyObject host =
                        SplitArrowOf(configuration) != null ? VisualTreeHelper.GetParent(configuration) : configuration;
                    List<TextBlock> labels = Descendants(host)
                        .OfType<TextBlock>()
                        .Where(t => !string.IsNullOrWhiteSpace(t.Text) && t.Text.Trim() != "▾")
                        .ToList();

                    Assert.Equal(
                        "ViewportConfiguration",
                        string.Concat(labels.Select(t => t.Text)).Replace(" ", string.Empty)
                    );

                    // Each word fits the width its text block is given; a word wider than that is broken where it stands
                    // ("Configuratio" and "n"). The label has at most two lines.
                    foreach (TextBlock label in labels)
                    {
                        foreach (string word in label.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            Assert.True(
                                WidthOf(label, word) <= label.ActualWidth + 0.5,
                                "The word "
                                    + word
                                    + " is "
                                    + WidthOf(label, word)
                                    + " wide in a text block "
                                    + label.ActualWidth
                                    + " wide."
                            );
                        }

                        double line = label.FontFamily.LineSpacing * label.FontSize;
                        Assert.True(label.ActualHeight <= (2 * line) + 1, "The label has more than two lines.");
                    }
                }
            );
        }

        #endregion

        #region Palettes and Interface

        [Fact]
        public void ThePropertiesToggleOfPalettesIsPressedWhileThePanelIsShown_AndTheCommandLineTileWhileTheCommandLineIs()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel palettes = Panel(strip, "Palettes");
                    List<FrameworkElement> controls = BodyControls(palettes);
                    var properties = Assert.IsAssignableFrom<ToggleButton>(
                        Assert.Single(controls, c => NameOf(c) == "Properties")
                    );
                    var commandLine = Assert.IsAssignableFrom<ToggleButton>(
                        Assert.Single(controls, c => NameOf(c) == "Command Line")
                    );

                    // Properties is a large toggle, Command Line a tile with no text.
                    Assert.Equal(LargeHeight, properties.ActualHeight, 0.5);
                    Assert.False(HasText(commandLine));
                    Assert.Equal(TileWidth, commandLine.ActualWidth, 0.5);

                    // Pressed while the panel is shown, and a click shows or hides it.
                    model.IsPropertiesPanelShown = true;
                    Pump(properties);
                    Assert.True(properties.IsChecked);

                    Click(properties);

                    Assert.False(model.IsPropertiesPanelShown);
                    Assert.False(properties.IsChecked);

                    Click(properties);

                    Assert.True(model.IsPropertiesPanelShown);
                    Assert.True(properties.IsChecked);

                    // The same for the command line and its tile.
                    model.IsCommandLineShown = true;
                    Pump(commandLine);
                    Assert.True(commandLine.IsChecked);

                    Click(commandLine);

                    Assert.False(model.IsCommandLineShown);
                    Assert.False(commandLine.IsChecked);

                    Click(commandLine);

                    Assert.True(model.IsCommandLineShown);
                    Assert.True(commandLine.IsChecked);

                    // Properties has left Interface, which holds Drawings alone.
                    Assert.DoesNotContain(BodyControls(Panel(strip, "Interface")), c => NameOf(c) == "Properties");
                }
            );
        }

        [Fact]
        public void TheTilesOfPalettesStartLayerQuickcalcAdcenterAndExternalreferences()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel palettes = Panel(strip, "Palettes");
                    List<FrameworkElement> controls = BodyControls(palettes);

                    AssertControls(PalettesBody, controls);

                    // Tool Palettes and Properties are large; the five others are tiles: 24 by 22, no text, 24 apart, Command Line
                    // alone in the first row, two in each of the others.
                    Assert.All(controls.Take(2), c => Assert.Equal(LargeHeight, c.ActualHeight, 0.5));
                    Assert.All(controls.Take(2), c => Assert.True(HasText(c), NameOf(c) + " shows no label."));
                    List<FrameworkElement> tiles = controls.Skip(2).ToList();
                    Assert.All(tiles, t => Assert.False(HasText(t), NameOf(t) + " shows text."));
                    Assert.All(tiles, t => Assert.Equal(TileWidth, t.ActualWidth, 0.5));
                    Assert.All(tiles, t => Assert.Equal(TileHeight, t.ActualHeight, 0.5));

                    double top = Top(tiles[0], palettes.Body);
                    double left = Left(tiles[0], palettes.Body);
                    Assert.Equal(top + Pitch, Top(tiles[1], palettes.Body), 0.5);
                    Assert.Equal(top + (2 * Pitch), Top(tiles[2], palettes.Body), 0.5);
                    Assert.Equal(left + TileWidth, Left(tiles[3], palettes.Body), 1.5);
                    Assert.Equal(top + Pitch, Top(tiles[3], palettes.Body), 0.5);
                    Assert.Equal(top + (2 * Pitch), Top(tiles[4], palettes.Body), 0.5);

                    // Every one with a command starts it through the ribbon's commands, enabled with a drawing open.
                    foreach (ButtonBase button in controls.OfType<ButtonBase>().Where(b => CommandWord(b).Length > 0))
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
                        Assert.NotNull(model.Session.Registry.Find(CommandWord(button)));
                    }
                }
            );
        }

        [Fact]
        public void InterfaceHoldsTheDrawingsToggle_AndItsLauncherOpensGlobalSettingsOnAppearance()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel inter = Panel(strip, "Interface");
                    List<FrameworkElement> controls = BodyControls(inter);

                    AssertControls(new[] { "Drawings|" }, controls);
                    var drawings = Assert.IsAssignableFrom<ToggleButton>(controls[0]);
                    Assert.Equal(LargeHeight, drawings.ActualHeight, 0.5);

                    // Pressed while the list of drawings is shown; a click hides it, a second shows it.
                    model.IsFileListShown = true;
                    Pump(drawings);
                    Assert.True(drawings.IsChecked);

                    Click(drawings);

                    Assert.False(model.IsFileListShown);
                    Assert.False(drawings.IsChecked);

                    Click(drawings);

                    Assert.True(model.IsFileListShown);

                    // The launcher: "Global settings: Appearance", opening the page of that name; no window is shown here.
                    List<string> requests = NoteSettingsRequests(model);

                    Assert.NotNull(inter.Launcher);
                    Assert.Equal("Global settings: Appearance", Assert.IsType<string>(inter.LauncherToolTip));
                    Assert.True(inter.Launcher.CanExecute(null));
                    inter.Launcher.Execute(null);

                    Assert.Equal(new[] { "Appearance" }, requests);
                }
            );
        }

        #endregion

        #region Shrinking

        [Fact]
        public void AtRibbonsDownTo1264_ViewTakesNoStep_AndAt884OurDraftingNavigateAndViewsHaveShrunkBeforeAutoCadsPanels()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    Assert.Equal(Squeeze(ViewSteps), Squeeze(strip.Steps));

                    // Ours, about 1 133 wide, fits every width down to 1 264 with every panel full, as AutoCAD's does.
                    foreach (double width in new[] { 1904, 1584, 1424, 1264 })
                    {
                        strip.Measure(new Size(width, 960));

                        Assert.Equal(0, strip.StepsTaken);
                        Assert.Equal("FFFFFFFF", Sizes(strip));
                    }

                    // At 884 the three panels of ours that lead the steps have shrunk, and the two AutoCAD keeps whole (its Viewport
                    // Tools and Palettes) are full.
                    strip.Measure(new Size(884, 960));

                    Assert.NotEqual(RibbonPanelSize.Full, Panel(strip, "Drafting").Size);
                    Assert.NotEqual(RibbonPanelSize.Full, Panel(strip, "Navigate").Size);
                    Assert.NotEqual(RibbonPanelSize.Full, Panel(strip, "Views").Size);
                    Assert.Equal(RibbonPanelSize.Full, Panel(strip, "Viewport Tools").Size);
                    Assert.Equal(RibbonPanelSize.Full, Panel(strip, "Palettes").Size);
                }
            );
        }

        [Fact]
        public void NarrowingTheStripPixelByPixel_TakesTheStepsOfViewInOrder_ViewportToolsAndPalettesNeverShrinking()
        {
            OnTab(
                m => m.ViewTab,
                Wide,
                (model, workspace, strip) =>
                {
                    List<RibbonPanel> panels = Panels(strip);
                    var steps = Squeeze(ViewSteps)
                        .Split(';')
                        .Select(s =>
                            (
                                Title: s.Split(':')[0],
                                Size: (RibbonPanelSize)Enum.Parse(typeof(RibbonPanelSize), s.Split(':')[1])
                            )
                        )
                        .ToList();

                    // The widest strip at which each step has been taken, found by narrowing one pixel at a time from 1 920.
                    var taken = new Dictionary<int, double>();
                    var last = panels.ToDictionary(p => p.Title, p => RibbonPanelSize.Full);

                    for (double width = 1920; width >= 500; width--)
                    {
                        strip.Measure(new Size(width, 960));

                        foreach (RibbonPanel panel in panels)
                        {
                            Assert.True(panel.Size >= last[panel.Title], panel.Title + " grew back at " + width + ".");

                            for (int k = 0; k < steps.Count; k++)
                            {
                                if (
                                    steps[k].Title == panel.Title
                                    && panel.Size >= steps[k].Size
                                    && !taken.ContainsKey(k)
                                )
                                {
                                    taken[k] = width;
                                }
                            }

                            last[panel.Title] = panel.Size;
                        }

                        Assert.Equal(RibbonPanelSize.Full, Panel(strip, "Viewport Tools").Size);
                        Assert.Equal(RibbonPanelSize.Full, Panel(strip, "Palettes").Size);
                    }

                    // Every step is taken above 500, and one never before the step ahead of it.
                    Assert.Equal(steps.Count, taken.Count);

                    for (int k = 1; k < steps.Count; k++)
                    {
                        Assert.True(
                            taken[k] <= taken[k - 1],
                            steps[k].Title
                                + ":"
                                + steps[k].Size
                                + " ("
                                + taken[k]
                                + ") came before "
                                + steps[k - 1].Title
                                + ":"
                                + steps[k - 1].Size
                                + " ("
                                + taken[k - 1]
                                + ")."
                        );
                    }
                }
            );
        }

        #endregion

        #region What the tests draw

        /// <summary>Saves Plan A over (0,0)-(100,80) and Plan B over (200,200)-(400,360), by -VIEW Window.</summary>
        private static void SaveTwoViews(MainViewModel model)
        {
            foreach (
                string line in new[]
                {
                    "-VIEW",
                    "W",
                    "Plan A",
                    "0,0",
                    "100,80",
                    "-VIEW",
                    "W",
                    "Plan B",
                    "200,200",
                    "400,360",
                }
            )
            {
                model.Execute(line);
            }

            Assert.False(model.IsCommandRunning);
        }

        #endregion
    }
}
