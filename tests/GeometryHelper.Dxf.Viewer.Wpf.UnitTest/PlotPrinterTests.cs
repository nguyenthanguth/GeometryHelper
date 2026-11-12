using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>A printer queue that prints nothing: it keeps the jobs it is given, and lists the sheets it is built with.</summary>
    internal sealed class FakePlotPrinter : IPlotPrinter
    {
        private readonly IReadOnlyList<CadPaper> _papers;

        internal FakePlotPrinter(string name, bool isNetwork = false, IReadOnlyList<CadPaper> papers = null)
        {
            Name = name;
            IsNetwork = isNetwork;
            _papers = papers ?? Sheets();
        }

        public string Name { get; }

        public bool IsNetwork { get; }

        internal int PaperRequests { get; private set; }

        internal List<PlotPrintJob> Jobs { get; } = new List<PlotPrintJob>();

        internal Exception FailWith { get; set; }

        /// <summary>A4 and A3 upright and Letter, as a printer lists them, each with the margins its driver gives.</summary>
        internal static IReadOnlyList<CadPaper> Sheets() =>
            new[]
            {
                new CadPaper("A4", 210, 297, 4, 4, 4, 4),
                new CadPaper("A3", 297, 420, 5, 5, 5, 5),
                new CadPaper("Letter", 215.9, 279.4, 6.35, 6.35, 6.35, 6.35),
            };

        public IReadOnlyList<CadPaper> GetPapers()
        {
            PaperRequests++;
            return _papers;
        }

        public void Print(PlotPrintJob job)
        {
            if (FailWith != null)
            {
                throw FailWith;
            }

            Jobs.Add(job);
        }
    }

    /// <summary>
    /// Printing to the printers of Windows (a seam with a fake queue, never a real printer): the Printer/plotter list taking
    /// them, the sheets they list, OK printing the plot on the printer's paper, copies, and the plot kept in the layout.
    /// </summary>
    public class PlotPrinterTests
    {
        private readonly FakePlotPrinter Network = new FakePlotPrinter(@"\\print\HP LaserJet M507", true);

        private static PlotDialogViewModel Dialog(
            CadDocument document,
            CadPlotSettings settings,
            params FakePlotPrinter[] printers
        ) =>
            new PlotDialogViewModel(
                document,
                new CadPlotJob(document.ModelSpace, settings, DisplayBox.Empty, null),
                false,
                printers
            );

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new InvalidOperationException(failure.Message, failure);
            }
        }

        [Fact]
        public void ThePrintersOfWindows_AreListedAfterTheBuiltInDevices_AndCanBeChosen()
        {
            var local = new FakePlotPrinter("Microsoft Print to PDF");
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty(), Network, local);

            Assert.Equal(
                new[]
                {
                    "None",
                    "GeometryHelper PDF",
                    "GeometryHelper PNG",
                    "GeometryHelper JPG",
                    "Microsoft Print to PDF",
                    @"\\print\HP LaserJet M507",
                },
                dialog.Devices.Select(d => d.Name).ToArray()
            );
            Assert.Equal(
                new[] { false, true, true, true, true, true },
                dialog.Devices.Select(d => d.IsEnabled).ToArray()
            );
            Assert.Equal(0, local.PaperRequests);

            dialog.Device = dialog.Devices[4];

            Assert.Same(dialog.Devices[4], dialog.Device);
            Assert.True(dialog.IsPrinter);
            Assert.False(dialog.IsRaster);
            Assert.True(dialog.Settings.IsPrinter);
            Assert.Equal("Microsoft Print to PDF", dialog.Settings.DeviceName);
            Assert.Equal("Microsoft Print to PDF · printer of Windows", dialog.PlotterText);
            Assert.Equal("Printer", dialog.WhereText);
            Assert.Equal(1, local.PaperRequests);

            dialog.Device = dialog.Devices[5];
            Assert.Equal("Network printer", dialog.WhereText);

            dialog.Device = dialog.Devices[0];
            Assert.Same(dialog.Devices[5], dialog.Device);
        }

        [Fact]
        public void ThePaperSizeListIsThePrintersOwn_TheSizeOfTheSheetKept_AndThePdfSheetsComeBack()
        {
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty(), Network);
            Assert.Equal("ISO A3 (420.00 x 297.00 MM)", dialog.Paper.Label);

            dialog.Device = dialog.Devices.Single(d => d.Name == Network.Name);

            Assert.Equal(
                new[] { "A4 (210.00 x 297.00 MM)", "A3 (297.00 x 420.00 MM)", "Letter (215.90 x 279.40 MM)" },
                dialog.Papers.Select(p => p.Label).ToArray()
            );
            // The plot was on a sheet 420 by 297 and the printer has one of that size, upright as printers name it.
            Assert.Equal("A3 (297.00 x 420.00 MM)", dialog.Paper.Label);
            Assert.True(dialog.Landscape);
            Assert.Equal("420 MM", dialog.SheetWidthText);
            Assert.Equal("297", dialog.SheetHeightText);
            Assert.Equal(new DisplayBox(5, 5, 415, 292), dialog.Page.Printable);

            dialog.Paper = dialog.Papers.Single(p => p.Name == "Letter");
            Assert.Equal("279.4 MM", dialog.SheetWidthText);
            Assert.Equal("Letter", dialog.Settings.Paper.Name);

            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PdfDevice);
            Assert.Equal(10, dialog.Papers.Count);
            Assert.Equal("ISO A3 (420.00 x 297.00 MM)", dialog.Paper.Label);
        }

        [Fact]
        public void APrinterThatListsNoSheet_OffersThePdfSheetsInstead()
        {
            var bare = new FakePlotPrinter("Broken", false, new CadPaper[0]);
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty(), bare);

            dialog.Device = dialog.Devices.Single(d => d.Name == "Broken");

            Assert.True(dialog.IsPrinter);
            Assert.Equal(10, dialog.Papers.Count);
        }

        [Fact]
        public void AnImageDevicePickedBackToAPrinter_GivesTheSheetBackInMillimetres()
        {
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty(), Network);
            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PngDevice);
            Assert.True(dialog.Paper.IsPixels);

            dialog.Device = dialog.Devices.Single(d => d.Name == Network.Name);

            Assert.False(dialog.Paper.IsPixels);
            Assert.Equal("A3 (297.00 x 420.00 MM)", dialog.Paper.Label);
            Assert.Equal("mm", dialog.OffsetUnit);
        }

        [Fact]
        public void ThePlotterALayoutNamed_IsChosenWhenTheDialogOpens_ifThatPrinterIsInstalled()
        {
            CadDocument document = PlotPdfTests.Plan();
            document.Database.Layouts.Single(l => l.ModelType).PlotConfigurationName = Network.Name;
            var settings = CadPlotSettings.Read(document, document.ModelSpace);

            Assert.Equal(Network.Name, Dialog(document, settings, Network).Device.Name);
            Assert.True(Dialog(document, settings, Network).IsPrinter);
            Assert.Equal(CadPlotSettings.PdfDevice, Dialog(document, settings).Device.Name);
            Assert.Equal(
                CadPlotSettings.PdfDevice,
                Dialog(document, settings, new FakePlotPrinter("Other")).Device.Name
            );
        }

        [Fact]
        public void APlotterTheUserLeftForThePdfWriter_IsNotChosenAgainWhenTheDialogComesBackFromWindow()
        {
            CadDocument document = PlotPdfTests.Plan();
            document.Database.Layouts.Single(l => l.ModelType).PlotConfigurationName = Network.Name;
            var job = new CadPlotJob(
                document.ModelSpace,
                CadPlotSettings.Read(document, document.ModelSpace),
                DisplayBox.Empty,
                null
            );
            var dialog = new PlotDialogViewModel(document, job, false, new[] { Network });
            Assert.True(dialog.IsPrinter);

            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PdfDevice);
            dialog.PicksWindow = true;
            dialog.HandBack();

            var again = new PlotDialogViewModel(document, job, false, new[] { Network });
            Assert.False(again.IsPrinter);
            Assert.Equal(CadPlotSettings.PdfDevice, again.Device.Name);
        }

        [Fact]
        public void OkOnAPrinter_PrintsThePlotOnThePrintersPaper_AtTheSheetsSize_AndWritesNoFile()
        {
            // The visual belongs to the thread that made it, so the plot, the print and the look at its pixels share one.
            OnSta(() =>
            {
                CadDocument document = PlotPdfTests.Plan();
                PlotDialogViewModel dialog = Dialog(document, PlotPdfTests.AtOneToTwenty(), Network);
                dialog.Device = dialog.Devices.Single(d => d.Name == Network.Name);

                CadPlotPage page = dialog.Print();

                Assert.Equal(420.0, page.SheetWidth);
                PlotPrintJob job = Assert.Single(Network.Jobs);
                Assert.Equal("plan-level2.dxf - Model", job.Title);
                Assert.Equal("A3", job.Paper.Name);
                Assert.Equal(420.0, job.SheetWidth);
                Assert.Equal(297.0, job.SheetHeight);
                Assert.Equal(1, job.Copies);

                // The sheet at 96 dots to the inch: the frame's left side, 22.5 mm in, dark; the margin and the paper, white.
                double width = 420.0 / 25.4 * 96.0;
                double height = 297.0 / 25.4 * 96.0;
                var picture = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                picture.Render(job.Page);
                var pixels = new byte[(int)width * (int)height * 4];
                picture.CopyPixels(pixels, (int)width * 4, 0);

                int Red(int x, int y) => pixels[(y * (int)width + x) * 4 + 2];

                int frameX = (int)(22.5 / 25.4 * 96.0);
                Assert.Contains(Enumerable.Range(frameX - 3, 7), x => Red(x, 300) < 120);
                Assert.Equal(255, Red(3, 3));
                Assert.Equal(255, Red((int)(width / 2), (int)(height / 2) + 40));
            });
        }

        [Fact]
        public void Copies_AreTypedFromOneTo999_AndOnlyAPrinterTakesThem()
        {
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty(), Network);
            Assert.False(dialog.IsCopiesEnabled);
            Assert.Equal("1", dialog.CopiesText);

            dialog.Device = dialog.Devices.Single(d => d.Name == Network.Name);
            Assert.True(dialog.IsCopiesEnabled);
            dialog.CopiesText = "3";
            dialog.Print();
            dialog.CopiesText = "0";
            Assert.Equal(3, dialog.Copies);
            dialog.CopiesText = "1000";
            Assert.Equal(3, dialog.Copies);
            dialog.CopiesText = "two";
            Assert.Equal(3, dialog.Copies);

            Assert.Equal(3, Network.Jobs.Single().Copies);

            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PdfDevice);
            Assert.False(dialog.IsCopiesEnabled);
            Assert.Equal("1", dialog.CopiesText);
        }

        [Fact]
        public void PlotToFile_IsTickedForAFile_AndOffForAPrinter()
        {
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty(), Network);
            Assert.True(dialog.PlotsToFile);
            Assert.Equal("Plot to file (always, for PDF)", dialog.PlotToFileText);

            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PngDevice);
            Assert.Equal("Plot to file (always, for a raster device)", dialog.PlotToFileText);

            dialog.Device = dialog.Devices.Single(d => d.Name == Network.Name);
            Assert.False(dialog.PlotsToFile);
            Assert.Equal("Plot to file (a printer prints on paper)", dialog.PlotToFileText);
        }

        [Fact]
        public void SaveChangesToLayout_WritesThePrintersNameIntoTheLayout_WhenItPrints()
        {
            CadDocument document = PlotPdfTests.Plan();
            PlotDialogViewModel dialog = Dialog(document, PlotPdfTests.AtOneToTwenty(), Network);
            dialog.Device = dialog.Devices.Single(d => d.Name == Network.Name);
            dialog.SaveChangesToLayout = true;

            dialog.Print();

            DxfLayout model = document.Database.Layouts.Single(l => l.ModelType);
            Assert.Equal(Network.Name, model.PlotConfigurationName);
            Assert.Equal(Network.Name, CadPlotSettings.Read(document, document.ModelSpace).SavedPlotter);
            Assert.Equal(
                "A3_(297.00_x_420.00_MM)",
                model.CanonicalMediaName.Substring(0, "A3_(297.00_x_420.00_MM)".Length)
            );
        }

        [Fact]
        public void PrintingWithNoPrinterChosen_IsRefused()
        {
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty(), Network);

            Assert.Throws<InvalidOperationException>(() => dialog.Print());
            Assert.Empty(Network.Jobs);
        }

        [Fact]
        public void PlotThroughTheWindow_PrintsOnOk_AsksNoFile_AndSaysWhereItWent()
        {
            using (var folder = new TestFolder())
            {
                var printer = new FakePlotPrinter("Office printer");
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", PlotPdfTests.Plan().Database)));
                model.WindowsPrinters = () => new IPlotPrinter[] { printer };
                model.ShowPlot = dialog =>
                {
                    dialog.Device = dialog.Devices.Single(d => d.Name == "Office printer");
                    return true;
                };
                bool asked = false;
                model.AskPlotFile = (where, name) =>
                {
                    asked = true;
                    return null;
                };

                model.Execute("PLOT");

                Assert.False(asked);
                Assert.Single(printer.Jobs);
                Assert.Contains("Plot sent to Office printer.", model.History);
            }
        }

        [Fact]
        public void APrinterThatFails_IsSaidOnTheCommandLine_AndNothingIsThrown()
        {
            using (var folder = new TestFolder())
            {
                var printer = new FakePlotPrinter("Office printer")
                {
                    FailWith = new InvalidOperationException("The queue is paused."),
                };
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", PlotPdfTests.Plan().Database)));
                model.WindowsPrinters = () => new IPlotPrinter[] { printer };
                model.ShowPlot = dialog =>
                {
                    dialog.Device = dialog.Devices.Single(d => d.Name == "Office printer");
                    return true;
                };

                model.Execute("PLOT");

                Assert.Contains("The plot was not printed: The queue is paused.", model.History);
            }
        }

        [Fact]
        public void PlotWithFileDialogsOff_PrintsAtOnce_NotAskingTheFileOnTheCommandLine()
        {
            using (var folder = new TestFolder())
            {
                var printer = new FakePlotPrinter("Office printer");
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", PlotPdfTests.Plan().Database)));
                model.WindowsPrinters = () => new IPlotPrinter[] { printer };
                model.ShowPlot = dialog =>
                {
                    dialog.Device = dialog.Devices.Single(d => d.Name == "Office printer");
                    return true;
                };
                model.Execute("FILEDIA");
                model.Execute("0");

                model.Execute("PLOT");

                Assert.False(model.Session.IsRunning);
                Assert.Single(printer.Jobs);
            }
        }

        [Fact]
        public void ThePreviewsPlot_PrintsWhenThePageSetupNamesAnInstalledPrinter()
        {
            using (var folder = new TestFolder())
            {
                var printer = new FakePlotPrinter("Office printer");
                CadDocument plan = PlotPdfTests.Plan();
                plan.Database.Layouts.Single(l => l.ModelType).PlotConfigurationName = "Office printer";
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", plan.Database)));
                model.WindowsPrinters = () => new IPlotPrinter[] { printer };
                model.ShowPlotPreview = preview => true;
                model.AskPlotFile = (where, name) =>
                    throw new InvalidOperationException("No file is asked for a printer.");

                model.Execute("PREVIEW");

                Assert.Single(printer.Jobs);
                Assert.Contains("Plot sent to Office printer.", model.History);
            }
        }

        [Fact]
        public void APageOfA0_IsPrintedAtAResolutionThatKeepsItsPixelsUnderTheBudget_AndA4At300()
        {
            Assert.Equal(300.0, CadPrintWriter.DotsPerInch(297, 210));
            Assert.Equal(300.0, CadPrintWriter.DotsPerInch(420, 297));

            double a0 = CadPrintWriter.DotsPerInch(1189, 841);
            Assert.InRange(a0, 100.0, 299.0);
            double pixels = 1189 / 25.4 * a0 * (841 / 25.4 * a0);
            Assert.InRange(pixels, 1.0, CadPrintWriter.MaxPixels * 1.001);
            Assert.Equal(a0, CadPrintWriter.DotsPerInch(841, 1189));
        }
    }
}
