using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Page Setup Manager that PAGESETUP opens, driven with no window: the page setups listed with how they plot, Set
    /// Current, New… asking a name and what to start with and then the Page Setup dialog, Modify…, Import… of another
    /// drawing, and Add… of the Plot dialog; each one undo step, nothing added when a dialog is cancelled.
    /// </summary>
    public class PageSetupManagerTests
    {
        private static CadPlotSettings OnA2() =>
            new CadPlotSettings
            {
                Area = CadPlotArea.Window,
                Window = new DisplayBox(120, 60, 7620, 5360),
                FitToPaper = false,
                PaperUnits = 1,
                DrawingUnits = 20,
                Paper = CadPaper.Standard[2],
                StyleTable = CadPlotStyleTable.Grayscale,
            };

        private static PageSetupManagerViewModel Manager(CadDocument document, params IPlotPrinter[] printers) =>
            new PageSetupManagerViewModel(document, document.ModelSpace, printers);

        [Fact]
        public void TheNameOfANewPageSetup_StartsAtSetup1_AndIsRefusedWhenAutoCadRefusesItOrTheDrawingHasIt()
        {
            var asked = new PageSetupNameViewModel("New Page Setup", new[] { "Setup1", "Roof" });

            Assert.Equal("Setup2", asked.Name);
            Assert.True(asked.CanOk);
            Assert.False(asked.HasError);
            Assert.False(asked.HasStartWith);

            asked.Name = "roof";
            Assert.False(asked.CanOk);
            Assert.Equal("The drawing has a page setup named roof already.", asked.Error);

            asked.Name = "A/B";
            Assert.False(asked.CanOk);
            Assert.True(asked.HasError);

            asked.Name = "  Walls ";
            Assert.True(asked.CanOk);
            Assert.Null(asked.Error);

            var starting = new PageSetupNameViewModel("New Page Setup", new string[0], new[] { "<None>", "*Model*" });
            Assert.True(starting.HasStartWith);
            Assert.Equal("<None>", starting.StartWithChoice);
            starting.StartWithChoice = "*Model*";
            starting.StartWithChoice = "*Nowhere*";
            Assert.Equal("*Model*", starting.StartWithChoice);
        }

        [Fact]
        public void TheManagerListsThePageSetups_WithHowTheyPlot_AndSetCurrentCopiesOneIntoTheLayout()
        {
            CadDocument document = PlotPdfTests.Plan();
            Assert.True(OnA2().SaveAsPageSetup(document, "A2 plan").Succeeded);
            Assert.True(
                new CadPlotSettings { Paper = CadPaper.Standard[4] }
                    .SaveAsPageSetup(document, "Small")
                    .Succeeded
            );
            PageSetupManagerViewModel manager = Manager(document);

            Assert.Equal("Page Setup Manager", manager.Title);
            Assert.Equal("Current layout: Model", manager.LayoutText);
            Assert.Equal("Current page setup: <None>", manager.CurrentText);
            Assert.Equal(new[] { "A2 plan", "Small" }, manager.Items.Select(i => i.Name).ToArray());
            Assert.Equal(
                "ISO A2 (594.00 x 420.00 MM) · GeometryHelper PDF · Window 1:20 · grayscale.ctb",
                manager.Items[0].Details
            );
            Assert.False(manager.CanSetCurrent);
            Assert.False(manager.CanModify);
            Assert.Equal(string.Empty, manager.DetailsText);

            manager.Selected = manager.Items[0];
            Assert.True(manager.CanSetCurrent);
            Assert.True(manager.CanModify);
            Assert.Equal(manager.Items[0].Details, manager.DetailsText);

            CadEditResult result = manager.SetCurrent();

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("Current page setup: A2 plan", manager.CurrentText);
            Assert.True(manager.Items[0].IsCurrent);
            Assert.False(manager.Items[1].IsCurrent);
            Assert.Equal("A2 plan", manager.Selected.Name);
            Assert.False(manager.CanSetCurrent);
            Assert.Equal("PAGESETUP", document.NextUndo.Name);
            Assert.True(CadPlotSettings.Read(document, document.ModelSpace).Paper.IsSameAs(CadPaper.Standard[2]));

            Assert.True(document.Undo());
            Assert.Equal(string.Empty, CadPlotSettings.CurrentPageSetupName(document, document.ModelSpace));
        }

        [Fact]
        public void New_AsksANameAndWhatToStartWith_ThenThePageSetupDialog_AndAddsItOnOk()
        {
            CadDocument document = PlotPdfTests.Plan();
            Assert.True(OnA2().ApplyTo(document, document.ModelSpace).Succeeded);
            PageSetupManagerViewModel manager = Manager(document);
            IReadOnlyList<string> offered = null;
            PlotDialogViewModel edited = null;
            manager.ShowName = asked =>
            {
                offered = asked.StartWith;
                asked.Name = "Roof";
                asked.StartWithChoice = "*Model*";
                return true;
            };
            manager.ShowPageSetup = dialog =>
            {
                edited = dialog;
                dialog.Paper = dialog.Papers.Single(p => p.Name == "ISO A4");
                dialog.StyleTable = CadPlotStyleTable.None;
                return true;
            };

            CadEditResult result = manager.New();

            Assert.Equal(new[] { "<None>", "*Model*", "*Layout1*" }, offered);
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("Roof", edited.PageSetupName);
            Assert.True(edited.IsPageSetup);
            Assert.False(edited.CanPickWindow);
            Assert.False(edited.CanAddPageSetup);
            Assert.Equal("Page Setup - Model", edited.Title);
            Assert.Equal(new[] { "Roof" }, CadPlotSettings.PageSetupNames(document));
            CadPlotSettings roof = CadPlotSettings.ReadPageSetup(document, document.ModelSpace, "Roof");
            Assert.True(roof.Paper.IsSameAs(CadPaper.Standard[4]));
            Assert.Equal(CadPlotStyleTable.None, roof.StyleTable);
            // It began as the layout Model was: a window at 1:20.
            Assert.Equal(CadPlotArea.Window, roof.Area);
            Assert.Equal(20.0, roof.DrawingUnits);
            Assert.Equal("PAGESETUP", document.NextUndo.Name);
            Assert.Equal(new[] { "Roof" }, manager.Items.Select(i => i.Name).ToArray());
            // The layout itself was not touched.
            Assert.True(CadPlotSettings.Read(document, document.ModelSpace).Paper.IsSameAs(CadPaper.Standard[2]));
        }

        [Fact]
        public void New_AddsNothing_WhenTheNameOrThePageSetupDialogIsCancelled()
        {
            CadDocument document = PlotPdfTests.Plan();
            PageSetupManagerViewModel manager = Manager(document);
            manager.ShowName = asked => false;
            manager.ShowPageSetup = dialog => throw new Xunit.Sdk.XunitException("No name, no dialog.");
            Assert.Null(manager.New());

            manager.ShowName = asked =>
            {
                asked.Name = "Roof";
                return true;
            };
            manager.ShowPageSetup = dialog => false;
            Assert.Null(manager.New());

            manager.ShowName = asked =>
            {
                asked.Name = "a/b";
                return true;
            };
            Assert.Null(manager.New());
            Assert.Empty(CadPlotSettings.PageSetupNames(document));
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void New_StartingWithNone_GivesADefaultSheetOfModelSpace()
        {
            CadDocument document = PlotPdfTests.Plan();
            Assert.True(OnA2().ApplyTo(document, document.ModelSpace).Succeeded);
            PageSetupManagerViewModel manager = Manager(document);
            CadPlotSettings seen = null;
            manager.ShowName = asked =>
            {
                asked.Name = "Blank";
                return true;
            };
            manager.ShowPageSetup = dialog =>
            {
                seen = dialog.Settings.Clone();
                return true;
            };

            Assert.True(manager.New().Succeeded);

            Assert.True(seen.Paper.IsSameAs(CadPaper.Default));
            Assert.True(seen.FitToPaper);
            Assert.Equal(CadPlotArea.Display, seen.Area);
        }

        [Fact]
        public void Modify_ShowsThePageSetupInTheDialog_AndWritesItBackOnOk_AsOneUndoStep()
        {
            CadDocument document = PlotPdfTests.Plan();
            Assert.True(OnA2().SaveAsPageSetup(document, "A2 plan").Succeeded);
            PageSetupManagerViewModel manager = Manager(document);
            manager.Selected = manager.Items.Single();
            manager.ShowPageSetup = dialog =>
            {
                Assert.Equal("A2 plan", dialog.PageSetupName);
                Assert.Equal("A2 plan", dialog.PageSetup);
                Assert.Equal("ISO A2 (594.00 x 420.00 MM)", dialog.Paper.Label);
                Assert.Equal(CadPlotArea.Window, dialog.Settings.Area);
                dialog.Portrait = true;
                return true;
            };

            CadEditResult result = manager.Modify();

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("PAGESETUP", document.NextUndo.Name);
            Assert.Single(CadPlotSettings.PageSetupNames(document));
            Assert.False(CadPlotSettings.ReadPageSetup(document, document.ModelSpace, "A2 plan").Landscape);

            Assert.True(document.Undo());
            Assert.True(CadPlotSettings.ReadPageSetup(document, document.ModelSpace, "A2 plan").Landscape);
        }

        [Fact]
        public void Modify_AndNew_OfferThePrintersOfWindowsInTheirDialog()
        {
            CadDocument document = PlotPdfTests.Plan();
            Assert.True(OnA2().SaveAsPageSetup(document, "A2 plan").Succeeded);
            PageSetupManagerViewModel manager = Manager(document, new FakePlotPrinter("Office printer"));
            manager.Selected = manager.Items.Single();
            string[] devices = null;
            manager.ShowPageSetup = dialog =>
            {
                devices = dialog.Devices.Select(d => d.Name).ToArray();
                dialog.Device = dialog.Devices.Single(d => d.Name == "Office printer");
                return true;
            };

            Assert.True(manager.Modify().Succeeded);

            Assert.Contains("Office printer", devices);
            CadPlotSettings again = CadPlotSettings.ReadPageSetup(document, document.ModelSpace, "A2 plan");
            Assert.Equal("Office printer", again.SavedPlotter);
            Assert.Equal(
                "Office printer",
                ((DxfPlotSettings)document.Database.PlotSettingsDictionary["A2 plan"]).PlotConfigurationName
            );
        }

        [Fact]
        public void Import_CopiesTheTickedPageSetupsOfAnotherDrawing_AndSaysWhenThereAreNone()
        {
            using (var folder = new TestFolder())
            {
                CadDocument source = PlotPdfTests.Plan();
                Assert.True(OnA2().SaveAsPageSetup(source, "A2 plan").Succeeded);
                Assert.True(
                    new CadPlotSettings { Paper = CadPaper.Standard[4] }
                        .SaveAsPageSetup(source, "Small")
                        .Succeeded
                );
                string withSetups = folder.Write("source.dxf", source.Database);
                string without = folder.Write("plain.dxf", PlotPdfTests.Plan().Database);
                CadDocument document = PlotPdfTests.Plan();
                PageSetupManagerViewModel manager = Manager(document);
                ImportPageSetupsViewModel shown = null;
                manager.AskImportFile = () => withSetups;
                manager.ShowImport = chosen =>
                {
                    shown = chosen;
                    chosen.Items.Single(i => i.Name == "Small").IsChecked = false;
                    return true;
                };

                CadEditResult result = manager.Import();

                Assert.True(result.Succeeded, result.Message);
                Assert.Equal("Import Page Setups", shown.Title);
                Assert.Equal("Page setups in source.dxf:", shown.FromText);
                Assert.Equal(new[] { "A2 plan", "Small" }, shown.Items.Select(i => i.Name).ToArray());
                Assert.Equal(new[] { "A2 plan" }, CadPlotSettings.PageSetupNames(document));
                Assert.Equal(new[] { "A2 plan" }, manager.Items.Select(i => i.Name).ToArray());
                Assert.Null(manager.Message);

                manager.AskImportFile = () => without;
                result = manager.Import();
                Assert.False(result.Succeeded);
                Assert.Equal("The drawing plain.dxf has no page setups.", manager.Message);
                Assert.True(manager.HasMessage);

                manager.AskImportFile = () => Path.Combine(folder.FullPath, "missing.dxf");
                Assert.False(manager.Import().Succeeded);
                Assert.StartsWith(
                    "The drawing missing.dxf could not be opened",
                    manager.Message,
                    System.StringComparison.Ordinal
                );

                manager.AskImportFile = () => null;
                Assert.Null(manager.Import());
            }
        }

        [Fact]
        public void ImportingWithNothingTicked_IsNotOffered()
        {
            var shown = new ImportPageSetupsViewModel("x.dxf", new[] { "A", "B" });
            Assert.True(shown.CanOk);
            Assert.Equal(new[] { "A", "B" }, shown.Chosen);

            shown.Items[0].IsChecked = false;
            Assert.Equal(new[] { "B" }, shown.Chosen);
            shown.Items[1].IsChecked = false;
            Assert.False(shown.CanOk);
            Assert.Empty(shown.Chosen);
        }

        [Fact]
        public void AddOfThePlotDialog_KeepsThePageSetupUnderANewName_AndChoosesIt()
        {
            CadDocument document = PlotPdfTests.Plan();
            var job = new CadPlotJob(document.ModelSpace, PlotPdfTests.AtOneToTwenty(), DisplayBox.Empty, null);
            CadPlotStyleTable layoutTable = CadPlotSettings.Read(document, document.ModelSpace).StyleTable;
            var dialog = new PlotDialogViewModel(document, job);
            Assert.Null(dialog.AskAndAddPageSetup());
            Assert.True(dialog.CanAddPageSetup);
            PageSetupNameViewModel asked = null;
            dialog.ShowPageSetupName = name =>
            {
                asked = name;
                name.Name = "Plan 1-20";
                return true;
            };
            dialog.StyleTable = CadPlotStyleTable.Grayscale;

            CadEditResult result = dialog.AskAndAddPageSetup();

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("Add Page Setup", asked.Title);
            Assert.False(asked.HasStartWith);
            Assert.Equal(new[] { "<None>", "Plan 1-20" }, dialog.PageSetups.ToArray());
            Assert.Equal("Plan 1-20", dialog.PageSetup);
            CadPlotSettings kept = CadPlotSettings.ReadPageSetup(document, document.ModelSpace, "Plan 1-20");
            Assert.Equal(CadPlotStyleTable.Grayscale, kept.StyleTable);
            Assert.Equal(20.0, kept.DrawingUnits);
            Assert.Equal("PAGESETUP", document.NextUndo.Name);
            // The layout is as it was: Add… keeps a page setup apart, Apply to Layout writes the layout.
            Assert.Equal(layoutTable, CadPlotSettings.Read(document, document.ModelSpace).StyleTable);

            // The name taken, the dialog cannot be OK'd; the box says so and nothing is written.
            dialog.ShowPageSetupName = name =>
            {
                name.Name = "plan 1-20";
                return true;
            };
            Assert.Null(dialog.AskAndAddPageSetup());
            Assert.Single(CadPlotSettings.PageSetupNames(document));
        }

        [Fact]
        public void TheManagerTheNameBoxTheImportListAndThePlotDialogOnAPrinter_LayOutOffscreen()
        {
            System.Exception failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    CadDocument document = PlotPdfTests.Plan();
                    Assert.True(OnA2().SaveAsPageSetup(document, "A2 plan").Succeeded);
                    var manager = new PageSetupManagerWindow(Manager(document));
                    manager.Body.Measure(new System.Windows.Size(640, 440));
                    manager.Body.Arrange(new System.Windows.Rect(0, 0, 640, 440));
                    Assert.True(manager.Body.ActualWidth > 0);

                    var name = new PageSetupNameWindow(
                        new PageSetupNameViewModel("New Page Setup", new[] { "A2 plan" }, new[] { "<None>", "*Model*" })
                    );
                    name.Body.Measure(new System.Windows.Size(400, 400));
                    name.Body.Arrange(new System.Windows.Rect(0, 0, 400, 400));
                    Assert.True(name.Body.ActualHeight > 0);

                    var import = new ImportPageSetupsWindow(new ImportPageSetupsViewModel("x.dxf", new[] { "A", "B" }));
                    import.Body.Measure(new System.Windows.Size(420, 380));
                    import.Body.Arrange(new System.Windows.Rect(0, 0, 420, 380));
                    Assert.True(import.Body.ActualWidth > 0);

                    var job = new CadPlotJob(document.ModelSpace, PlotPdfTests.AtOneToTwenty(), DisplayBox.Empty, null);
                    var dialog = new PlotDialogViewModel(
                        document,
                        job,
                        false,
                        new IPlotPrinter[] { new FakePlotPrinter("Office printer") }
                    );
                    dialog.Device = dialog.Devices.Single(d => d.Name == "Office printer");
                    var plot = new PlotWindow(dialog);
                    plot.Body.Measure(new System.Windows.Size(1040, 760));
                    plot.Body.Arrange(new System.Windows.Rect(0, 0, 1040, 760));
                    Assert.True(plot.Body.ActualWidth > 0);
                }
                catch (System.Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new System.InvalidOperationException(failure.Message, failure);
            }
        }

        [Fact]
        public void PagesetupOpensTheManager_OnTheSpace_WithThePrintersOfWindows_AndPlotStillShowsTheDialog()
        {
            using (var folder = new TestFolder())
            {
                var printer = new FakePlotPrinter("Office printer");
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", PlotPdfTests.Plan().Database)));
                model.WindowsPrinters = () => new IPlotPrinter[] { printer };
                PageSetupManagerViewModel shown = null;
                model.ShowPageSetupManager = manager => shown = manager;
                model.ShowPlot = dialog => false;

                model.Execute("PAGESETUP");

                Assert.NotNull(shown);
                Assert.Equal("Model", shown.Space.Name);
                Assert.Equal("Current layout: Model", shown.LayoutText);
                Assert.False(model.Session.IsRunning);

                // Without a manager to show, PAGESETUP shows the Page Setup dialog as before.
                model.ShowPageSetupManager = null;
                PlotDialogViewModel dialog = null;
                model.ShowPlot = d =>
                {
                    dialog = d;
                    return false;
                };
                model.Execute("PAGESETUP");
                Assert.True(dialog.IsPageSetup);
                Assert.Null(dialog.PageSetupName);

                model.ShowPlot = d =>
                {
                    dialog = d;
                    return false;
                };
                model.Execute("PLOT");
                Assert.False(dialog.IsPageSetup);
                Assert.True(dialog.CanAddPageSetup);
            }
        }
    }
}
