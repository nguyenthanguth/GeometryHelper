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
    /// Output as AutoCAD 2021's (D1346, D1340), found in the window drawn offscreen: Plot (Plot, Batch Plot and Preview large, Page
    /// Setup Manager and Plot Stamp small, no launcher) and Export to DWF/PDF (the Export split of Export PDF, Export DWF and
    /// Export). The Import panel has gone: Import is on Insert. The tab has no steps. A strip width is the width the RibbonStrip
    /// gets, which is the window's less the pixels the window gives to what is around it.
    /// </summary>
    public sealed class RibbonOutputPanelsTests
    {
        private const double Wide = 3000;

        private const double PanelHeight = 98;
        private const double LargeHeight = 72;
        private const double TileHeight = 22;
        private const double Pitch = 24;

        [Fact]
        public void OutputShowsPlotAndExportToDwfPdf_AndNoImportPanel()
        {
            OnTab(
                m => m.OutputTab,
                884,
                (model, workspace, strip) =>
                {
                    List<RibbonPanel> panels = Panels(strip);

                    Assert.Equal(new[] { "Plot", "Export to DWF/PDF" }, panels.Select(p => p.Title));
                    Assert.All(panels, p => Assert.Equal(RibbonPanelSize.Full, p.Size));
                    Assert.Equal(0, strip.StepsTaken);
                    Assert.True(string.IsNullOrWhiteSpace(strip.Steps), "Output has steps: " + strip.Steps);
                    Assert.Equal(PanelHeight, strip.ActualHeight, 0.5);
                    Assert.All(panels, p => Assert.Equal(PanelHeight, p.ActualHeight, 0.5));

                    // Nothing on the tab imports: no IMPORT button, and no panel named Import.
                    Assert.DoesNotContain(Descendants(strip).OfType<ButtonBase>(), b => CommandWord(b) == "IMPORT");
                    Assert.DoesNotContain(Descendants(strip).OfType<TextBlock>(), t => t.Text == "Import");
                }
            );
        }

        [Fact]
        public void PlotHoldsPlotBatchPlotAndPreviewLarge_ThenPageSetupManagerAndPlotStamp_AndHasNoLauncher()
        {
            OnTab(
                m => m.OutputTab,
                Wide,
                (model, workspace, strip) =>
                {
                    RibbonPanel plot = Panel(strip, "Plot");
                    List<FrameworkElement> controls = BodyControls(plot);

                    AssertControls(
                        new[]
                        {
                            "Plot|PLOT",
                            "Batch Plot|PUBLISH",
                            "Preview|PREVIEW",
                            "Page Setup Manager|PAGESETUP",
                            "Plot Stamp|PLOTSTAMP",
                        },
                        controls
                    );

                    // The first three are large with their labels, side by side; the last two are small with text, 24 apart.
                    Assert.All(controls.Take(3), c => Assert.Equal(LargeHeight, c.ActualHeight, 0.5));
                    Assert.All(controls, c => Assert.True(HasText(c), NameOf(c) + " shows no text."));
                    Assert.All(controls.Skip(3), c => Assert.Equal(TileHeight, c.ActualHeight, 0.5));
                    Assert.Equal(Pitch, Top(controls[4], plot.Body) - Top(controls[3], plot.Body), 0.5);
                    Assert.Equal(Left(controls[3], plot.Body), Left(controls[4], plot.Body), 0.5);
                    Assert.True(Left(controls[1], plot.Body) > Left(controls[0], plot.Body));
                    Assert.True(Left(controls[2], plot.Body) > Left(controls[1], plot.Body));
                    Assert.True(Left(controls[3], plot.Body) > Left(controls[2], plot.Body));

                    // No Plot launcher: there is no Plot and Publish page to open (D1344); no slide-out either.
                    Assert.Null(plot.Launcher);
                    Assert.Null(plot.SlideOut);
                    Assert.Null(Panel(strip, "Export to DWF/PDF").Launcher);
                }
            );
        }

        [Fact]
        public void TheExportSplitHoldsExportPdfExportDwfAndExport_EachStartingItsCommand()
        {
            OnTab(
                m => m.OutputTab,
                Wide,
                (model, workspace, strip) =>
                {
                    // The file dialogs the three commands would show are left out, so that each runs on the command line, where Esc
                    // ends it.
                    model.AskExportFile = null;
                    model.ShowSaveAsPdf = null;

                    RibbonPanel export = Panel(strip, "Export to DWF/PDF");
                    var top = (ButtonBase)Assert.Single(BodyControls(export));

                    // One large split: the icon over the label "Export", then ▾; the top starts Export PDF.
                    Assert.Equal(LargeHeight, LargeHeightOf(top), 0.5);
                    Assert.NotNull(SplitArrowOf(top));
                    Assert.Equal("EXPORTPDF", CommandWord(top));
                    Assert.Equal("Export▾", Label(top.Parent));

                    AssertMenu(
                        model,
                        SplitArrowOf(top),
                        new[] { new[] { "Export PDF", "Export DWF", "Export" } },
                        null,
                        new[] { "EXPORTPDF", "EXPORTDWF", "EXPORT" }
                    );

                    // The split follows its pick: the top now runs Export, and the label stays Export.
                    Assert.Equal("EXPORT", CommandWord(top));
                    Assert.Equal("Export▾", Label(top.Parent));

                    Pick(MenuOf(SplitArrowOf(top)).Single(i => Norm(i) == "Export DWF"));
                    model.Escape();

                    Assert.Equal("EXPORTDWF", CommandWord(top));
                    Assert.Equal("Export▾", Label(top.Parent));
                }
            );
        }

        [Fact]
        public void EveryButtonOfOutputStartsItsCommand_ThroughStartCommandOrStartCommandWithOption()
        {
            OnTab(
                m => m.OutputTab,
                Wide,
                (model, workspace, strip) =>
                {
                    // The commands the buttons of the tab start, those of the Export split's menu with them: nothing pressed here
                    // opens a window (PLOT, PUBLISH, PREVIEW and PAGESETUP would), so each is read, not run.
                    var words = new SortedSet<string>(System.StringComparer.Ordinal);

                    foreach (RibbonPanel panel in Panels(strip))
                    {
                        foreach (ButtonBase button in ControlsOf(panel.Body).OfType<ButtonBase>())
                        {
                            Assert.True(button.IsEnabled, NameOf(button) + " is not enabled.");
                            Assert.Contains(
                                button.Command,
                                new[] { model.StartCommandCommand, model.StartCommandWithOptionCommand }
                            );
                            Assert.True(
                                button.Command.CanExecute(button.CommandParameter),
                                NameOf(button) + " cannot run."
                            );
                            words.Add(CommandWord(button));

                            ButtonBase arrow = SplitArrowOf(button);

                            if (arrow != null)
                            {
                                foreach (MenuItem item in MenuOf(arrow))
                                {
                                    words.Add(((string)item.CommandParameter).Split(' ')[0]);
                                }
                            }
                        }
                    }

                    Assert.Equal(
                        new[]
                        {
                            "EXPORT",
                            "EXPORTDWF",
                            "EXPORTPDF",
                            "PAGESETUP",
                            "PLOT",
                            "PLOTSTAMP",
                            "PREVIEW",
                            "PUBLISH",
                        },
                        words
                    );
                    Assert.All(words, w => Assert.NotNull(model.Session.Registry.Find(w)));

                    // Plot Stamp is the one that can be pressed here: its window is answered Cancel.
                    model.ShowPlotStamp = dialog => false;
                    ButtonBase stamp = Descendants(strip)
                        .OfType<ButtonBase>()
                        .Single(b => CommandWord(b) == "PLOTSTAMP");
                    int before = model.History.Count;

                    Click(stamp);

                    Assert.Contains("Command: PLOTSTAMP", model.History.Skip(before));
                    model.Escape();
                }
            );
        }
    }
}
