using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>A printer of Windows that says where it prints, as the real one does through its queue's port.</summary>
    internal sealed class PortPlotPrinter : IPlotPrinter, IPlotPrinterPort
    {
        private readonly FakePlotPrinter _inner;

        internal PortPlotPrinter(string name, string port, bool isNetwork = false)
        {
            _inner = new FakePlotPrinter(name, isNetwork);
            Port = port;
        }

        public string Name => _inner.Name;

        public bool IsNetwork => _inner.IsNetwork;

        public string Port { get; }

        public IReadOnlyList<CadPaper> GetPapers() => _inner.GetPapers();

        public void Print(PlotPrintJob job) => _inner.Print(job);
    }

    /// <summary>
    /// The Page Setup Manager, its dialogs and the Plot dialog's printer mode as the Design artifact draws them (board
    /// PageSetup, v130): the layout's own setup as the first row, the details of the page setup chosen, Rename and Delete on
    /// the list's menu, the three starting points of New…, the Source drawing and the Model or Layout of each page setup of
    /// Import, Import… of Add Page Setup, Plot to file kept for a printer, and Help in each. Driven with no window.
    /// </summary>
    public class PageSetupDesignTests
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

        private static CadDocument TwoSetups()
        {
            CadDocument document = PlotPdfTests.Plan();
            Assert.True(OnA2().SaveAsPageSetup(document, "A2 plan").Succeeded);
            Assert.True(
                new CadPlotSettings { Paper = CadPaper.Standard[4] }
                    .SaveAsPageSetup(document, "Small")
                    .Succeeded
            );
            return document;
        }

        private static PageSetupManagerViewModel Manager(
            CadDocument document,
            CadSpace space = null,
            params IPlotPrinter[] printers
        ) => new PageSetupManagerViewModel(document, space ?? document.ModelSpace, printers);

        private static CadSpace Layout(CadDocument document) => document.Spaces.First(s => !s.IsModel);

        [Fact]
        public void InALayout_TheLayoutsOwnSetupIsTheFirstRow_NamedWithThePageSetupItIsSetTo()
        {
            CadDocument document = TwoSetups();
            CadSpace layout = Layout(document);
            PageSetupManagerViewModel manager = Manager(document, layout);

            Assert.Equal("Current layout: Layout1", manager.LayoutText);
            Assert.Equal(new[] { "*Layout1*", "A2 plan", "Small" }, manager.Items.Select(i => i.Name).ToArray());
            Assert.True(manager.Items[0].IsLayout);
            Assert.False(manager.Items[1].IsLayout);

            manager.Selected = manager.Items[1];
            Assert.True(manager.SetCurrent().Succeeded);

            Assert.Equal("Current page setup: A2 plan", manager.CurrentText);
            Assert.Equal(
                new[] { "*Layout1 (A2 plan)*", "A2 plan", "Small" },
                manager.Items.Select(i => i.Name).ToArray()
            );
            Assert.Equal("A2 plan", manager.Selected.Name);
        }

        [Fact]
        public void TheDetailsOfThePageSetupChosen_AreDeviceNamePlotterPlotSizeWhereAndDescription()
        {
            CadDocument document = TwoSetups();
            PageSetupManagerViewModel manager = Manager(document);

            manager.Selected = manager.Items.Single(i => i.Name == "A2 plan");

            PageSetupInfo info = manager.SelectedInfo;
            Assert.Equal("GeometryHelper PDF", info.DeviceName);
            Assert.Equal("GeometryHelper PDF · vector, built in", info.Plotter);
            Assert.Equal("594.00 x 420.00 mm (Landscape)", info.PlotSize);
            Assert.Equal("File", info.Where);
            Assert.Equal("Writes a PDF, one page per plot; text stays searchable.", info.Description);

            // A turned sheet is told with its orientation, the shorter side first.
            Assert.True(
                new CadPlotSettings { Paper = CadPaper.Standard[2], Landscape = false }
                    .SaveAsPageSetup(document, "Upright")
                    .Succeeded
            );
            manager = Manager(document);
            manager.Selected = manager.Items.Single(i => i.Name == "Upright");
            Assert.Equal("420.00 x 594.00 mm (Portrait)", manager.SelectedInfo.PlotSize);

            // An image device is sized in pixels.
            var image = new CadPlotSettings();
            image.UseDevice(CadPlotSettings.PngDevice);
            Assert.True(image.SaveAsPageSetup(document, "Picture").Succeeded);
            manager = Manager(document);
            manager.Selected = manager.Items.Single(i => i.Name == "Picture");
            Assert.Equal("GeometryHelper PNG", manager.SelectedInfo.DeviceName);
            Assert.Equal("1920 x 1080 pixels (Landscape)", manager.SelectedInfo.PlotSize);
            Assert.Equal("GeometryHelper PNG · raster, built in", manager.SelectedInfo.Plotter);
        }

        [Fact]
        public void ThePageSetupOfAPrinter_SaysWhereItPrints_AndOneOfAPrinterThisComputerLacksSaysSo()
        {
            CadDocument document = TwoSetups();
            var office = new PortPlotPrinter("Office printer", "USB001");
            PageSetupManagerViewModel manager = Manager(document, null, office);
            manager.Selected = manager.Items.Single(i => i.Name == "A2 plan");
            manager.ShowPageSetup = dialog =>
            {
                dialog.Device = dialog.Devices.Single(d => d.Name == "Office printer");
                return true;
            };
            Assert.True(manager.Modify().Succeeded);

            PageSetupInfo info = manager.SelectedInfo;
            Assert.Equal("Office printer", info.DeviceName);
            Assert.Equal("Office printer · printer of Windows", info.Plotter);
            Assert.Equal("USB001", info.Where);
            Assert.Equal("Prints on paper through Windows, drawn at up to 300 dots to the inch.", info.Description);

            ((DxfPlotSettings)document.Database.PlotSettingsDictionary["A2 plan"]).PlotConfigurationName = "HP Gone";
            manager.Selected = null;
            manager.Selected = manager.Items.Single(i => i.Name == "A2 plan");
            // The list is read again when something is done, not on every look.
            manager.Selected = manager.Items.Single(i => i.Name == "Small");
            Assert.True(manager.SetCurrent().Succeeded);
            manager.Selected = manager.Items.Single(i => i.Name == "A2 plan");
            Assert.Equal("HP Gone · not on this computer", manager.SelectedInfo.Plotter);
            Assert.Equal("—", manager.SelectedInfo.Where);
            Assert.Equal(
                "This computer has no such printer: the page setup plots with GeometryHelper PDF.",
                manager.SelectedInfo.Description
            );
        }

        [Fact]
        public void RenameAndDelete_OfTheListsMenu_AreOneUndoStepEach_AndNotOfferedForTheLayoutsRow()
        {
            CadDocument document = TwoSetups();
            PageSetupManagerViewModel manager = Manager(document);

            Assert.False(manager.CanRename);
            Assert.False(manager.CanDelete);
            Assert.False(manager.BeginRename());
            Assert.Null(manager.Delete());

            manager.Selected = manager.Items.Single(i => i.Name == "Small");
            Assert.True(manager.CanRename);
            Assert.True(manager.CanDelete);
            Assert.True(manager.BeginRename());
            Assert.True(manager.Selected.IsEditing);
            Assert.Equal("Small", manager.Selected.EditText);
            manager.Selected.EditText = "  Sheet A4 ";

            CadEditResult renamed = manager.CommitRename();

            Assert.True(renamed.Succeeded, renamed.Message);
            Assert.Equal(new[] { "*Model*", "A2 plan", "Sheet A4" }, manager.Items.Select(i => i.Name).ToArray());
            Assert.Equal("Sheet A4", manager.Selected.Name);
            Assert.False(manager.Selected.IsEditing);
            Assert.Equal("PAGESETUP", document.NextUndo.Name);

            CadEditResult deleted = manager.Delete();

            Assert.True(deleted.Succeeded, deleted.Message);
            Assert.Equal(new[] { "*Model*", "A2 plan" }, manager.Items.Select(i => i.Name).ToArray());
            Assert.Same(manager.Items[0], manager.Selected);
            Assert.Equal("PAGESETUP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(new[] { "A2 plan", "Sheet A4" }, CadPlotSettings.PageSetupNames(document));
            Assert.True(document.Undo());
            Assert.Equal(new[] { "A2 plan", "Small" }, CadPlotSettings.PageSetupNames(document));
        }

        [Fact]
        public void ARenameToAFaultyOrTakenNameOrLeftAsItWas_ChangesNothing_AndEscapeCancelsIt()
        {
            CadDocument document = TwoSetups();
            CadUndoStep last = document.NextUndo;
            PageSetupManagerViewModel manager = Manager(document);
            manager.Selected = manager.Items.Single(i => i.Name == "Small");

            Assert.True(manager.BeginRename());
            manager.Selected.EditText = "a2 plan";
            CadEditResult taken = manager.CommitRename();
            Assert.False(taken.Succeeded);
            Assert.True(manager.HasMessage);
            Assert.DoesNotContain(manager.Items, i => i.IsEditing);

            Assert.True(manager.BeginRename());
            manager.Selected.EditText = "a/b";
            Assert.False(manager.CommitRename().Succeeded);

            Assert.True(manager.BeginRename());
            manager.Selected.EditText = "Small";
            Assert.Null(manager.CommitRename());

            Assert.True(manager.BeginRename());
            manager.Selected.EditText = "Other";
            manager.CancelRename();
            Assert.False(manager.Selected.IsEditing);
            Assert.Null(manager.CommitRename());

            Assert.Equal(new[] { "A2 plan", "Small" }, CadPlotSettings.PageSetupNames(document));
            Assert.Same(last, document.NextUndo);
        }

        [Fact]
        public void ModifyOfTheLayoutsRow_ShowsThePageSetupDialogOnTheLayout_AndWritesItToTheLayoutOnOk()
        {
            CadDocument document = TwoSetups();
            PageSetupManagerViewModel manager = Manager(document);
            PlotDialogViewModel shown = null;
            manager.ShowPageSetup = dialog =>
            {
                shown = dialog;
                dialog.Paper = dialog.Papers.Single(p => p.Name == "ISO A4");
                return true;
            };

            CadEditResult result = manager.Modify();

            Assert.True(result.Succeeded, result.Message);
            Assert.Null(shown.PageSetupName);
            Assert.True(shown.IsPageSetup);
            Assert.False(shown.CanPickWindow);
            Assert.False(shown.CanAddPageSetup);
            Assert.True(CadPlotSettings.Read(document, document.ModelSpace).Paper.IsSameAs(CadPaper.Standard[4]));
            // The layout is written as Apply to Layout writes it: one undo step named PLOT.
            Assert.Equal("PLOT", document.NextUndo.Name);
            Assert.Equal(new[] { "A2 plan", "Small" }, CadPlotSettings.PageSetupNames(document));

            manager.ShowPageSetup = dialog => false;
            Assert.Null(manager.Modify());
        }

        [Fact]
        public void TheManagerKeepsWhetherToDisplayWhenCreatingANewLayout_UnticketedToBeginWith()
        {
            CadDocument document = PlotPdfTests.Plan();
            PageSetupManagerViewModel manager = Manager(document);
            Assert.False(manager.DisplayWhenCreatingLayout);
            manager.DisplayWhenCreatingLayout = true;
            Assert.True(manager.DisplayWhenCreatingLayout);

            var session = new CadCommandSession(document);
            manager.Session = session;
            Assert.False(manager.DisplayWhenCreatingLayout);
            manager.DisplayWhenCreatingLayout = true;
            Assert.Equal("1", session.Registry.FindVariable("SHOWPAGESETUPFORNEWLAYOUTS").GetValue(session));
            manager.DisplayWhenCreatingLayout = false;
            Assert.Equal("0", session.Registry.FindVariable("SHOWPAGESETUPFORNEWLAYOUTS").GetValue(session));
        }

        [Fact]
        public void New_OffersNoneTheDefaultDeviceThePreviousPlotTheLayoutAndTheNamedSetups_ToStartWith()
        {
            CadDocument document = TwoSetups();
            Assert.True(OnA2().ApplyTo(document, document.ModelSpace).Succeeded);
            PageSetupManagerViewModel manager = Manager(document);
            var previous = new CadPlotSettings { Paper = CadPaper.Standard[3], StyleTable = CadPlotStyleTable.None };
            manager.PreviousPlot = () => previous;
            var offered = new List<IReadOnlyList<string>>();
            var seen = new List<CadPlotSettings>();
            string next = null;
            manager.ShowName = asked =>
            {
                offered.Add(asked.StartWith);
                asked.Name = "New" + offered.Count;
                asked.StartWithChoice = next;
                return true;
            };
            manager.ShowPageSetup = dialog =>
            {
                seen.Add(dialog.Settings.Clone());
                return true;
            };

            foreach (string choice in new[] { "<Default output device>", "<Previous plot>", "*Model*", "Small" })
            {
                next = choice;
                Assert.True(manager.New().Succeeded);
            }

            Assert.Equal(
                new[] { "<None>", "<Default output device>", "<Previous plot>", "*Model*", "A2 plan", "Small" },
                offered[0]
            );
            Assert.True(seen[0].Paper.IsSameAs(CadPaper.Default));
            Assert.Equal(CadPlotArea.Display, seen[0].Area);
            Assert.True(seen[1].Paper.IsSameAs(CadPaper.Standard[3]));
            Assert.Equal(CadPlotStyleTable.None, seen[1].StyleTable);
            Assert.Equal(CadPlotArea.Window, seen[2].Area);
            Assert.Equal(20.0, seen[2].DrawingUnits);
            Assert.True(seen[3].Paper.IsSameAs(CadPaper.Standard[4]));
        }

        [Fact]
        public void New_StartingWithThePreviousPlot_WhereNoneWasMade_GivesTheDefaults()
        {
            CadDocument document = PlotPdfTests.Plan();
            Assert.True(OnA2().ApplyTo(document, document.ModelSpace).Succeeded);
            PageSetupManagerViewModel manager = Manager(document);
            CadPlotSettings seen = null;
            manager.ShowName = asked =>
            {
                asked.StartWithChoice = "<Previous plot>";
                return true;
            };
            manager.ShowPageSetup = dialog =>
            {
                seen = dialog.Settings.Clone();
                return true;
            };

            Assert.True(manager.New().Succeeded);

            Assert.True(seen.Paper.IsSameAs(CadPaper.Default));
            Assert.Equal(CadPlotArea.Display, seen.Area);
        }

        [Fact]
        public void ImportPageSetups_ShowTheSourceDrawingTheLocationOfEachAndTheDetailsOfTheOneChosen()
        {
            using (var folder = new TestFolder())
            {
                CadDocument source = TwoSetups();
                string path = folder.Write("source.dxf", source.Database);
                CadDocument document = PlotPdfTests.Plan();
                PageSetupManagerViewModel manager = Manager(document);
                ImportPageSetupsViewModel shown = null;
                manager.AskImportFile = () => path;
                manager.ShowImport = chosen =>
                {
                    shown = chosen;
                    return false;
                };

                Assert.Null(manager.Import());

                Assert.Equal("Source drawing name:", shown.FromText);
                Assert.Equal(path, shown.FileName);
                Assert.Equal(new[] { "Layout", "Layout" }, shown.Items.Select(i => i.Location).ToArray());
                Assert.Null(shown.Selected);
                Assert.Equal(string.Empty, shown.SelectedInfo.PlotSize);

                shown.Selected = shown.Items.Single(i => i.Name == "A2 plan");
                Assert.Equal("594.00 x 420.00 mm (Landscape)", shown.SelectedInfo.PlotSize);
                Assert.Equal("GeometryHelper PDF", shown.SelectedInfo.DeviceName);
            }
        }

        [Fact]
        public void AddPageSetup_OffersImport_ThatCopiesPageSetupsOfAnotherDrawing_AndTheirNamesAreTakenAfter()
        {
            using (var folder = new TestFolder())
            {
                string path = folder.Write("source.dxf", TwoSetups().Database);
                CadDocument document = PlotPdfTests.Plan();
                var job = new CadPlotJob(document.ModelSpace, PlotPdfTests.AtOneToTwenty(), DisplayBox.Empty, null);
                var dialog = new PlotDialogViewModel(document, job);
                bool importOffered = true;
                dialog.ShowPageSetupName = name =>
                {
                    importOffered = name.CanImport;
                    return false;
                };

                // With no file dialog to ask the drawing in, Import… is not offered.
                dialog.AskAndAddPageSetup();
                Assert.False(importOffered);

                dialog.AskImportFile = () => path;
                dialog.ShowImport = chosen => true;
                dialog.ShowPageSetupName = name =>
                {
                    Assert.True(name.CanImport);
                    Assert.Null(name.Error);
                    name.Name = "Small";
                    Assert.Null(name.Error);
                    name.Import();
                    Assert.Equal("The drawing has a page setup named Small already.", name.Error);
                    name.Name = "Roof";
                    return true;
                };

                CadEditResult result = dialog.AskAndAddPageSetup();

                Assert.True(result.Succeeded, result.Message);
                Assert.Equal(new[] { "A2 plan", "Roof", "Small" }, CadPlotSettings.PageSetupNames(document));
                Assert.Equal(new[] { "<None>", "A2 plan", "Roof", "Small" }, dialog.PageSetups.ToArray());
            }
        }

        [Fact]
        public void APrinterIsListedWithWhereItIs_AndPlotToFileIsOfferedForItButNotSupported()
        {
            var network = new PortPlotPrinter(@"\\office-srv\Office printer", @"\\office-srv\Office printer", true);
            var usb = new PortPlotPrinter("Label printer", "USB001");
            var local = new FakePlotPrinter("Microsoft Print to PDF");
            CadDocument document = PlotPdfTests.Plan();
            var job = new CadPlotJob(document.ModelSpace, PlotPdfTests.AtOneToTwenty(), DisplayBox.Empty, null);
            var dialog = new PlotDialogViewModel(document, job, false, new IPlotPrinter[] { network, usb, local });

            Assert.Equal(
                new[] { string.Empty, "built in, vector", "built in, raster", "built in, raster" },
                dialog.Devices.Take(4).Select(d => d.Kind).ToArray()
            );
            Assert.Equal(
                new[] { "Windows · USB", "Windows", "Windows · network" },
                dialog.Devices.Skip(4).Select(d => d.Kind).ToArray()
            );
            Assert.Equal(
                new[] { "Label printer", "Microsoft Print to PDF", @"\\office-srv\Office printer" },
                dialog.Devices.Skip(4).Select(d => d.Name).ToArray()
            );

            Assert.False(dialog.IsPlotToFileEnabled);
            dialog.PlotsToFile = true;
            Assert.True(dialog.PlotsToFile);

            dialog.Device = dialog.Devices.Single(d => d.Name == "Label printer");
            Assert.Equal("USB001", dialog.WhereText);
            Assert.True(dialog.IsPlotToFileEnabled);
            Assert.False(dialog.PlotsToFile);
            Assert.Equal("Plot to file", dialog.PlotToFileText);
            Assert.True(dialog.TryAccept());
            Assert.False(dialog.HasNotice);

            dialog.PlotsToFile = true;
            Assert.True(dialog.PlotsToFile);
            Assert.False(dialog.TryAccept());
            Assert.True(dialog.HasNotice);
            Assert.Contains("not supported for this printer", dialog.Notice, StringComparison.Ordinal);
            Assert.Equal(dialog.Notice, dialog.FooterText);
            Assert.Throws<InvalidOperationException>(() => dialog.Print());

            dialog.PlotsToFile = false;
            Assert.True(dialog.TryAccept());
            Assert.Equal(dialog.Footer, dialog.FooterText);

            dialog.Device = dialog.Devices.Single(d => d.Name == @"\\office-srv\Office printer");
            Assert.Equal(@"\\office-srv\Office printer", dialog.WhereText);
            dialog.Device = dialog.Devices.Single(d => d.Name == "Microsoft Print to PDF");
            Assert.Equal("Printer", dialog.WhereText);

            // Going back to a file ticks it again and greys it, as for the PDF writer.
            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PdfDevice);
            Assert.True(dialog.PlotsToFile);
            Assert.False(dialog.IsPlotToFileEnabled);
        }

        [Fact]
        public void CenterThePlot_IsGreyedWhenThePlotAreaIsTheLayout_AndLiveForAWindow()
        {
            CadDocument document = PlotPdfTests.Plan();
            CadSpace layout = Layout(document);
            CadPlotSettings sheet = CadPlotSettings.Read(document, layout);
            sheet.Area = CadPlotArea.Layout;
            var job = new CadPlotJob(layout, sheet, DisplayBox.Empty, null);
            var dialog = new PlotDialogViewModel(document, job);

            Assert.Equal(CadPlotArea.Layout, dialog.Area.Area);
            Assert.False(dialog.IsCenterEnabled);

            dialog.Area = dialog.Areas.Single(a => a.Area == CadPlotArea.Extents);
            Assert.True(dialog.IsCenterEnabled);
        }

        [Fact]
        public void EachDialogHasAHelpPageOfTheGuide()
        {
            CadDocument document = PlotPdfTests.Plan();
            var job = new CadPlotJob(document.ModelSpace, PlotPdfTests.AtOneToTwenty(), DisplayBox.Empty, null);

            string[] urls =
            {
                Manager(document).HelpUrl,
                new PageSetupNameViewModel("Add Page Setup", new string[0]).HelpUrl,
                new ImportPageSetupsViewModel("x.dxf", new[] { "A" }).HelpUrl,
                new PlotDialogViewModel(document, job).HelpUrl,
            };

            Assert.All(urls, url =>
                Assert.StartsWith(
                    "https://nguyenthanguth.github.io/GeometryHelper/guides/presenter/plot.html",
                    url,
                    StringComparison.Ordinal
                )
            );
        }

        [Fact]
        public void TheDesignsDialogs_LayOutOffscreen_WithTheirNewRowsGroupsAndFooters()
        {
            Exception failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    CadDocument document = TwoSetups();
                    var printer = new PortPlotPrinter("Office printer", "USB001");
                    PageSetupManagerViewModel manager = Manager(document, null, printer);
                    manager.Selected = manager.Items.Single(i => i.Name == "A2 plan");
                    Assert.True(manager.BeginRename());
                    var managerWindow = new PageSetupManagerWindow(manager);
                    managerWindow.Body.Measure(new System.Windows.Size(640, 520));
                    managerWindow.Body.Arrange(new System.Windows.Rect(0, 0, 640, 520));
                    Assert.True(managerWindow.Body.ActualHeight > 0);
                    manager.CancelRename();

                    var name = new PageSetupNameWindow(
                        new PageSetupNameViewModel(
                            "New Page Setup",
                            new[] { "A2 plan" },
                            new[] { "<None>", "<Default output device>", "<Previous plot>", "*Model*", "A2 plan" }
                        )
                        {
                            ImportPageSetups = () => new[] { "A2 plan" },
                            Name = "a/b",
                        }
                    );
                    name.Body.Measure(new System.Windows.Size(420, 520));
                    name.Body.Arrange(new System.Windows.Rect(0, 0, 420, 520));
                    Assert.True(name.Body.ActualHeight > 0);

                    var import = new ImportPageSetupsWindow(
                        new ImportPageSetupsViewModel("C:\\x\\x.dxf", new[] { "A" })
                    );
                    import.Body.Measure(new System.Windows.Size(480, 480));
                    import.Body.Arrange(new System.Windows.Rect(0, 0, 480, 480));
                    Assert.True(import.Body.ActualWidth > 0);

                    var job = new CadPlotJob(document.ModelSpace, PlotPdfTests.AtOneToTwenty(), DisplayBox.Empty, null);
                    var dialog = new PlotDialogViewModel(document, job, false, new IPlotPrinter[] { printer });
                    dialog.Device = dialog.Devices.Single(d => d.Name == "Office printer");
                    var plot = new PlotWindow(dialog);
                    plot.Body.Measure(new System.Windows.Size(1040, 760));
                    plot.Body.Arrange(new System.Windows.Rect(0, 0, 1040, 760));
                    Assert.True(plot.Body.ActualWidth > 0);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new InvalidOperationException(failure.Message, failure);
            }
        }
    }
}
