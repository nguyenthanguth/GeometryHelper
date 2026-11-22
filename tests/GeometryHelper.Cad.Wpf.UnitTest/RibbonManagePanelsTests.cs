using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.RibbonWave4Harness;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Manage as AutoCAD 2021's (D1345), found in the window drawn offscreen: Customization (User Interface and Tool Palettes large,
    /// Edit Aliases), Applications (Load Application and Run Script large), CAD Standards (Layer Translator, Check, Configure, the
    /// last two greyed as not built yet) and Cleanup (Purge and Find Non-Purgeable Items large, then Overkill, Audit and Recover).
    /// The tab has no steps: its panels are full down to 884 pixels. A strip width is the width the RibbonStrip gets, which is the
    /// window's less the pixels the window gives to what is around it.
    /// </summary>
    public sealed class RibbonManagePanelsTests
    {
        private const double Wide = 3000;

        private const double PanelHeight = 98;
        private const double LargeHeight = 72;
        private const double TileHeight = 22;
        private const double Pitch = 24;

        #region The panels and their order

        [Fact]
        public void ManageShowsCustomizationApplicationsCadStandardsAndCleanup_InAutoCadsOrder()
        {
            // 884 is AutoCAD's narrowest ribbon (a window of 900 less its border): the four panels need about 660.
            OnTab(
                m => m.ManageTab,
                884,
                (model, workspace, strip) =>
                {
                    List<RibbonPanel> panels = Panels(strip);

                    Assert.Equal(
                        new[] { "Customization", "Applications", "CAD Standards", "Cleanup" },
                        panels.Select(p => p.Title)
                    );
                    Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
                    Assert.Equal(0, strip.StepsTaken);
                    Assert.True(string.IsNullOrWhiteSpace(strip.Steps), "Manage has steps: " + strip.Steps);
                    Assert.Equal(PanelHeight, strip.ActualHeight, 0.5);
                    Assert.All(panels, p => Assert.Equal(PanelHeight, p.ActualHeight, 0.5));

                    // None has a slide-out or a launcher: AutoCAD's four have neither on this tab.
                    Assert.All(panels, p => Assert.Null(p.SlideOut));
                    Assert.All(panels, p => Assert.Null(p.Launcher));
                }
            );
        }

        #endregion

        #region The bodies

        [Fact]
        public void CustomizationHoldsUserInterfaceAndToolPalettesLarge_ThenEditAliases()
        {
            OnTab(
                m => m.ManageTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel customization = Panel(strip, "Customization");
                    List<FrameworkElement> controls = BodyControls(customization);

                    AssertControls(
                        new[] { "User Interface|CUI", "Tool Palettes|TOOLPALETTES", "Edit Aliases|ALIASEDIT" },
                        controls
                    );
                    Assert.All(controls.Take(2), c => Assert.Equal(LargeHeight, c.ActualHeight, 0.5));
                    Assert.All(controls, c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                    Assert.Equal(TileHeight, controls[2].ActualHeight, 0.5);

                    // Edit Aliases stands in the first row of its column, level with the top of the large buttons.
                    Assert.Equal(Top(controls[0], customization.Body), Top(controls[2], customization.Body), 0.5);

                    // User Interface and Edit Aliases show Global settings on Shortcuts, here noted instead of shown.
                    List<string> requests = NoteSettingsRequests(model);

                    Click((ButtonBase)controls[0]);
                    Click((ButtonBase)controls[2]);

                    Assert.Equal(new[] { "Shortcuts", "Shortcuts" }, requests);
                }
            );
        }

        [Fact]
        public void ApplicationsHoldsLoadApplicationAndRunScript_LoadApplicationOpeningTheApplicationsWindow()
        {
            OnTab(
                m => m.ManageTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel applications = Panel(strip, "Applications");
                    List<FrameworkElement> controls = BodyControls(applications);

                    AssertControls(new[] { "Load Application|APPLOAD", "Run Script|SCRIPT" }, controls);
                    Assert.All(controls, c => Assert.Equal(LargeHeight, c.ActualHeight, 0.5));
                    Assert.All(controls, c => Assert.True(HasText(c), NameOf(c) + " shows no label."));
                    Assert.Same(model.StartCommandCommand, ((ButtonBase)controls[1]).Command);

                    // Load Application shows Load/Unload Applications; the window is answered Cancel here.
                    int shown = 0;
                    model.ShowAppLoad = dialog =>
                    {
                        shown++;
                        return false;
                    };

                    Click((ButtonBase)controls[0]);

                    Assert.Equal(1, shown);
                    Assert.Contains("Command: APPLOAD", model.History);
                }
            );
        }

        [Fact]
        public void CadStandardsHoldsLayerTranslatorCheckAndConfigure_InThatOrder_TheLastTwoGreyedAsNotBuiltYet()
        {
            OnTab(
                m => m.ManageTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel standards = Panel(strip, "CAD Standards");
                    List<FrameworkElement> controls = BodyControls(standards);

                    AssertControls(new[] { "Layer Translator|LAYTRANS", "Check|", "Configure|" }, controls);
                    Assert.All(controls, c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                    Assert.All(controls, c => Assert.Equal(TileHeight, c.ActualHeight, 0.5));
                    Assert.Equal(Pitch, Top(controls[1], standards.Body) - Top(controls[0], standards.Body), 0.5);
                    Assert.Equal(Pitch, Top(controls[2], standards.Body) - Top(controls[1], standards.Body), 0.5);

                    // Layer Translator runs; Check and Configure are greyed, and their tips say they are not built yet.
                    Assert.True(controls[0].IsEnabled);
                    Assert.Same(model.StartCommandCommand, ((ButtonBase)controls[0]).Command);
                    Assert.All(controls.Skip(1), c => Assert.False(c.IsEnabled, NameOf(c) + " is enabled."));
                    Assert.All(controls.Skip(1), c => Assert.Contains("not built yet", Words(c)));
                    Assert.Contains("CHECKSTANDARDS", Words(controls[1]));
                    Assert.Contains("STANDARDS", Words(controls[2]));
                }
            );
        }

        [Fact]
        public void CleanupHoldsPurgeAndFindNonPurgeableItemsLarge_ThenOverkillAuditAndRecover()
        {
            OnTab(
                m => m.ManageTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel cleanup = Panel(strip, "Cleanup");
                    List<FrameworkElement> controls = BodyControls(cleanup);

                    AssertControls(
                        new[]
                        {
                            "Purge|PURGE",
                            "Find Non-Purgeable Items|",
                            "Overkill|OVERKILL",
                            "Audit|AUDIT",
                            "Recover|RECOVER",
                        },
                        controls
                    );

                    // Purge and Find Non-Purgeable Items are large, with their labels; the other three small, with text, 24 apart.
                    Assert.All(controls.Take(2), c => Assert.Equal(LargeHeight, c.ActualHeight, 0.5));
                    Assert.All(controls, c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                    Assert.All(controls.Skip(2), c => Assert.Equal(TileHeight, c.ActualHeight, 0.5));
                    Assert.Equal(Pitch, Top(controls[3], cleanup.Body) - Top(controls[2], cleanup.Body), 0.5);
                    Assert.Equal(Pitch, Top(controls[4], cleanup.Body) - Top(controls[3], cleanup.Body), 0.5);

                    // Every one with a command starts it through the ribbon's commands, enabled with a drawing open.
                    foreach (ButtonBase button in controls.OfType<ButtonBase>().Where(b => CommandWord(b).Length > 0))
                    {
                        Assert.Contains(
                            button.Command,
                            new[] { model.StartCommandCommand, model.StartCommandWithOptionCommand }
                        );
                        Assert.True(button.IsEnabled, CommandWord(button) + " is not enabled.");
                        Assert.NotNull(model.Session.Registry.Find(CommandWord(button)));
                    }
                }
            );
        }

        [Fact]
        public void FindNonPurgeableItemsOpensThePurgeWindowOnItsNonPurgeableView()
        {
            OnTab(
                m => m.ManageTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel cleanup = Panel(strip, "Cleanup");
                    List<FrameworkElement> controls = BodyControls(cleanup);

                    // The Purge window is answered Cancel; whether it was shown on Find Non-Purgeable Items is noted.
                    var views = new List<bool>();
                    model.ShowPurge = dialog =>
                    {
                        views.Add(dialog.IsNonPurgeable);
                        return false;
                    };

                    // Purge shows Purgeable Items, Find Non-Purgeable Items the other view of the same window.
                    Click((ButtonBase)Assert.Single(controls, c => NameOf(c) == "Purge"));
                    model.Escape();
                    Click((ButtonBase)Assert.Single(controls, c => NameOf(c).Contains("Non-Purgeable")));
                    model.Escape();

                    Assert.Equal(new[] { false, true }, views);
                }
            );
        }

        #endregion
    }
}
