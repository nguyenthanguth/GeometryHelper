using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Plot dialog of PLOT (Ctrl+P) and its preview, driven with no window: what each field shows and sets, Window&lt;
    /// handing the settings back for the corners to be picked, Apply to Layout writing the page setup as one undo step, OK
    /// writing the PDF, and the preview's pan and zooms.
    /// </summary>
    public class PlotDialogTests
    {
        private static PlotDialogViewModel Dialog(
            CadDocument document,
            CadPlotSettings settings = null,
            bool isPageSetup = false
        )
        {
            var job = new CadPlotJob(
                document.ModelSpace,
                settings ?? CadPlotSettings.Read(document, document.ModelSpace),
                DisplayBox.Empty,
                null
            );
            var printers = new IPlotPrinter[]
            {
                new FakePlotPrinter(@"\\print\HP LaserJet M507", true),
                new FakePlotPrinter("Microsoft Print to PDF", false),
            };
            return new PlotDialogViewModel(document, job, isPageSetup, printers);
        }

        [Fact]
        public void TheDialogShowsThePdfWriterChosen_AndNoneGreyed()
        {
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty());

            Assert.Equal("Plot - Model", dialog.Title);
            Assert.Equal("plan-level2.dxf · Model", dialog.Footer);
            // The raster devices of design PlotRaster follow the PDF one: GeometryHelper PNG and JPG.
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
            Assert.Equal(
                new[]
                {
                    string.Empty,
                    "built in, vector",
                    "built in, raster",
                    "built in, raster",
                    "Windows",
                    "Windows · network",
                },
                dialog.Devices.Select(d => d.Kind).ToArray()
            );
            Assert.True(dialog.Devices[4].StartsGroup);
            Assert.Same(dialog.Devices[1], dialog.Device);
            dialog.Device = dialog.Devices[0];
            Assert.Same(dialog.Devices[1], dialog.Device);
            Assert.Equal("GeometryHelper PDF · vector, built in", dialog.PlotterText);
            Assert.Equal("<None>", dialog.PageSetup);
            Assert.Equal("Page Setup - Model", Dialog(PlotPdfTests.Plan(), isPageSetup: true).Title);
        }

        [Fact]
        public void TheFieldsShowTheWindowAtOneToTwentyCentredOnA3_AndEachSetsThePlot()
        {
            PlotDialogViewModel dialog = Dialog(PlotPdfTests.Plan(), PlotPdfTests.AtOneToTwenty());

            Assert.Equal("ISO A3 (420.00 x 297.00 MM)", dialog.Paper.Label);
            Assert.Equal(10, dialog.Papers.Count);
            Assert.Equal("420 MM", dialog.SheetWidthText);
            Assert.Equal("297", dialog.SheetHeightText);
            Assert.Equal(92.0, dialog.SheetPictureWidth, 9);
            Assert.Equal(92.0 * 297 / 420, dialog.SheetPictureHeight, 9);
            Assert.Equal(
                new[] { "Display", "Extents", "Limits", "Window" },
                dialog.Areas.Select(a => a.Name).ToArray()
            );
            Assert.Equal("Window", dialog.Area.Name);
            Assert.True(dialog.IsWindow);
            Assert.Equal("From (120, 60) to (7 620, 5 360)", dialog.WindowText);
            Assert.Equal("1:20", dialog.ScaleName);
            Assert.Equal("1", dialog.PaperUnitsText);
            Assert.Equal("20", dialog.DrawingUnitsText);
            Assert.True(dialog.Centered);
            Assert.False(dialog.IsOffsetEnabled);
            Assert.Equal("17.50", dialog.OffsetXText);
            Assert.Equal("11.00", dialog.OffsetYText);
            Assert.Equal(CadPlotStyleTable.Monochrome, dialog.StyleTable);
            Assert.True(dialog.Landscape);
            Assert.Equal(0.0, dialog.OrientationAngle);

            // Fitted: the units of the fitted scale, greyed.
            dialog.FitToPaper = true;
            Assert.False(dialog.IsScaleEnabled);
            Assert.Equal("Custom", dialog.ScaleName);
            Assert.Equal("18.47", dialog.DrawingUnitsText);
            Assert.Equal("1.93", dialog.OffsetXText);
            dialog.FitToPaper = false;

            dialog.ScaleName = "1:50";
            Assert.Equal("50", dialog.DrawingUnitsText);
            dialog.DrawingUnitsText = "23";
            Assert.Equal(23.0, dialog.Settings.DrawingUnits);
            Assert.Equal("Custom", dialog.ScaleName);

            dialog.Centered = false;
            Assert.True(dialog.IsOffsetEnabled);
            dialog.OffsetXText = "3";
            Assert.Equal(3.0, dialog.Settings.OffsetX);

            dialog.Paper = dialog.Papers.Single(p => p.Name == "ISO A4");
            dialog.Portrait = true;
            Assert.Equal("210 MM", dialog.SheetWidthText);
            Assert.Equal("297", dialog.SheetHeightText);
            Assert.Equal(90.0, dialog.OrientationAngle);
            dialog.UpsideDown = true;
            Assert.Equal(270.0, dialog.OrientationAngle);

            dialog.Area = dialog.Areas.Single(a => a.Area == CadPlotArea.Extents);
            Assert.False(dialog.IsWindow);
            Assert.Equal(new DisplayBox(120, 60, 7620, 5360), dialog.Page.Area);
        }

        [Fact]
        public void ApplyToLayout_WritesThePageSetupAsOneUndoStep_AndOkWritesThePdf()
        {
            using (var folder = new TestFolder())
            {
                CadDocument document = PlotPdfTests.Plan();
                PlotDialogViewModel dialog = Dialog(document, PlotPdfTests.AtOneToTwenty());
                dialog.StyleTable = CadPlotStyleTable.Grayscale;

                Assert.True(dialog.ApplyToLayout().Succeeded);
                Assert.Equal("PLOT", document.NextUndo.Name);
                CadPlotSettings read = CadPlotSettings.Read(document, document.ModelSpace);
                Assert.Equal(CadPlotStyleTable.Grayscale, read.StyleTable);
                Assert.Equal(CadPlotArea.Window, read.Area);

                dialog.SaveChangesToLayout = false;
                string path = Path.Combine(folder.FullPath, dialog.SuggestedFileName);
                CadPlotPage page = dialog.Plot(path);

                Assert.Equal("plan-level2-Model.pdf", dialog.SuggestedFileName);
                Assert.True(File.Exists(path));
                Assert.Equal(420.0, page.SheetWidth);
                Assert.Contains("ROOM 1", Pdf.Text(File.ReadAllBytes(path)));
            }
        }

        [Fact]
        public void PlotThroughTheWindow_PicksTheWindowOnTheCanvas_ShowsTheDialogAgain_AndWritesThePdfOnOk()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", PlotPdfTests.Plan().Database)));
                var shown = new List<CadPlotSettings>();
                model.ShowPlot = dialog =>
                {
                    shown.Add(dialog.Settings.Clone());

                    if (shown.Count == 1)
                    {
                        dialog.Area = dialog.Areas.Single(a => a.Area == CadPlotArea.Window);
                        dialog.FitToPaper = false;
                        dialog.ScaleName = "1:20";
                        dialog.PicksWindow = true;
                        return false;
                    }

                    return true;
                };
                string written = null;
                model.AskPlotFile = (where, name) => written = Path.Combine(folder.FullPath, name);

                model.Execute("PLOT");
                Assert.Single(shown);
                Assert.Equal("Specify first corner:", model.Session.PromptText);
                model.Session.Feed(new GeoPoint2(120, 60));
                model.Session.Feed(new GeoPoint2(7620, 5360));

                Assert.Equal(2, shown.Count);
                Assert.Equal(CadPlotArea.Window, shown[1].Area);
                Assert.Equal(new DisplayBox(120, 60, 7620, 5360), shown[1].Window);
                Assert.Equal(20.0, shown[1].DrawingUnits);
                Assert.False(model.Session.IsRunning);
                Assert.Equal(Path.Combine(folder.FullPath, "plan-level2-Model.pdf"), written);
                Assert.True(File.Exists(written));
                Assert.Contains("Plot written to " + written + ".", model.History);
            }
        }

        [Fact]
        public void ThePreviewShowsTheWholeSheet_PansAndZooms_AndSaysWhatIsPlotted()
        {
            CadDocument document = PlotPdfTests.Plan();
            var preview = new PlotPreviewViewModel(
                document,
                document.ModelSpace,
                PlotPdfTests.AtOneToTwenty(),
                DisplayBox.Empty
            );

            Assert.Equal("GeometryHelper PDF · ISO A3 landscape · 1:20 · Monochrome", preview.Summary);
            Assert.Equal("Paper 420 × 297 mm · printable area dashed", preview.PaperText);
            Assert.Equal("Press ESC or ENTER to exit, or right-click to display shortcut menu.", preview.HelpText);
            Assert.True(preview.IsPan);

            preview.ZoomOriginal(1400, 800);
            Assert.Equal(0.88 * 800 / 297, preview.PixelsPerMillimetre, 12);
            Assert.Equal(210.0, preview.CenterX, 12);
            Assert.Equal(148.5, preview.CenterY, 12);

            // The point under the cursor stays put as the wheel zooms.
            Affine2 before = preview.SheetToScreen(1400, 800);
            preview.ZoomAt(300, 200, 2.0, 1400, 800);
            Affine2 after = preview.SheetToScreen(1400, 800);
            double sx = (300 - before.E) / before.A;
            double sy = (200 - before.F) / before.D;
            Assert.Equal(300.0, after.X(sx, sy), 9);
            Assert.Equal(200.0, after.Y(sx, sy), 9);

            preview.PanBy(100, 0);
            Assert.Equal(sx + (700 - 300 - 100) / after.A, preview.CenterX, 9);

            preview.IsZoomWindow = true;
            Assert.False(preview.IsPan);
            Assert.Equal(PlotPreviewMode.ZoomWindow, preview.Mode);
            double zoom = preview.PixelsPerMillimetre;
            preview.ZoomWindow(600, 300, 800, 400, 1400, 800);
            Assert.Equal(zoom * 7.0, preview.PixelsPerMillimetre, 9);
        }

        [Fact]
        public void ThePlotWindowAndThePreviewLayOutOffscreen()
        {
            Exception failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    CadDocument document = PlotPdfTests.Plan();
                    var window = new PlotWindow(Dialog(document, PlotPdfTests.AtOneToTwenty()));
                    window.Body.Measure(new System.Windows.Size(1040, 760));
                    window.Body.Arrange(new System.Windows.Rect(0, 0, 1040, 760));
                    Assert.True(window.Body.ActualWidth > 0);

                    var preview = new PlotPreviewWindow(
                        new PlotPreviewViewModel(
                            document,
                            document.ModelSpace,
                            PlotPdfTests.AtOneToTwenty(),
                            DisplayBox.Empty
                        )
                    );
                    preview.Body.Measure(new System.Windows.Size(1400, 860));
                    preview.Body.Arrange(new System.Windows.Rect(0, 0, 1400, 860));
                    PlotPreviewCanvas sheet = preview.Sheet;
                    sheet.Model.ZoomOriginal(1400, 800);

                    using (SkiaSharp.SKBitmap drawn = sheet.Draw(1400, 800))
                    {
                        // The grey of the window around the sheet, white on the sheet away from the plot, black on the frame.
                        Assert.Equal(new SkiaSharp.SKColor(0x5B, 0x65, 0x73), drawn.GetPixel(10, 10));
                        Assert.Equal(SkiaSharp.SKColors.White, drawn.GetPixel(700, 400 - (int)(0.88 * 800 / 2) + 20));
                    }

                    window.Close();
                    preview.Close();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}
