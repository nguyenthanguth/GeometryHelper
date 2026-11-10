using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// PLOT to an image (designs PlotRaster and PlotRasterCustom): GeometryHelper PNG and JPG in the Plot dialog, sizes in
    /// pixels and Custom…, the image written at its size on white with colour 7 black, and FILEDIA 0 asking the file.
    /// </summary>
    public class PlotRasterTests
    {
        /// <summary>The plan of the PDF tests with a line of colour 7 across its middle, from (120, 2 710) to (7 620, 2 710).</summary>
        private static CadDocument PlanWithWhiteLine()
        {
            DxfDatabase database = PlotPdfTests.Plan().Database;
            var line = new DxfLine(new GeoPoint3(120, 2710, 0), new GeoPoint3(7620, 2710, 0)) { ColorIndex = 7 };
            database.ModelSpace.AppendEntity(line);
            return CadDocument.From(database, "plan-level2.dxf");
        }

        private static PlotDialogViewModel Dialog(CadDocument document)
        {
            var settings = CadPlotSettings.Read(document, document.ModelSpace);
            settings.Area = CadPlotArea.Extents;
            var job = new CadPlotJob(document.ModelSpace, settings, DisplayBox.Empty, null);
            return new PlotDialogViewModel(document, job, false, null, new[] { CadPaper.Pixels(800, 600) });
        }

        [Fact]
        public void ChoosingPng_GivesPixelSizesFullHdFitted_NoStyleTable_AndAPngFileName()
        {
            PlotDialogViewModel dialog = Dialog(PlanWithWhiteLine());
            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PngDevice);

            Assert.True(dialog.IsRaster);
            Assert.Equal("1920 x 1080 Pixels (Full HD)", dialog.Paper.Label);
            Assert.Equal(
                new[] { "1280 x 720 Pixels (HD)", "1600 x 1200 Pixels (UXGA)", "1920 x 1080 Pixels (Full HD)", "2560 x 1440 Pixels (QHD)", "3840 x 2160 Pixels (4K UHD)", "800 x 600 Pixels (Custom)", "Custom…" },
                dialog.Papers.Select(p => p.Label).ToArray());
            Assert.Equal("1920 PIXELS", dialog.SheetWidthText);
            Assert.Equal("1080", dialog.SheetHeightText);
            Assert.True(dialog.FitToPaper);
            Assert.True(dialog.Centered);
            Assert.Equal(CadPlotStyleTable.None, dialog.StyleTable);
            Assert.Equal("pixels", dialog.OffsetUnit);
            Assert.Equal("96", dialog.DpiText);
            Assert.Equal("GeometryHelper PNG · raster, built in", dialog.PlotterText);
            Assert.Equal("plan-level2-Model.png", dialog.SuggestedFileName);

            // JPG keeps the size; the PDF one goes back to its sheet.
            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.JpgDevice);
            Assert.Equal("1920 x 1080 Pixels (Full HD)", dialog.Paper.Label);
            Assert.Equal("plan-level2-Model.jpg", dialog.SuggestedFileName);
            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PdfDevice);
            Assert.False(dialog.IsRaster);
            Assert.False(dialog.Paper.IsPixels);
            Assert.Equal("plan-level2-Model.pdf", dialog.SuggestedFileName);
        }

        [Fact]
        public void APngOfFullHd_IsExactly1920By1080_WhiteBehind_RedKept_AndColourSevenBlack()
        {
            using (var folder = new TestFolder())
            {
                PlotDialogViewModel dialog = Dialog(PlanWithWhiteLine());
                dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PngDevice);
                string path = Path.Combine(folder.FullPath, dialog.SuggestedFileName);
                CadPlotPage page = dialog.Plot(path);

                byte[] bytes = File.ReadAllBytes(path);
                Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, bytes.Take(4).ToArray());

                using (SKBitmap image = SKBitmap.Decode(path))
                {
                    Assert.Equal(1920, image.Width);
                    Assert.Equal(1080, image.Height);

                    // The plan 7 500 by 5 300 fitted to the height: 1 080 / 5 300 pixels a unit, centred across.
                    Assert.Equal(1080.0 / 5300.0, page.Scale, 9);
                    Assert.Equal(SKColors.White, image.GetPixel(10, 540));
                    Assert.Equal(SKColors.White, image.GetPixel(1910, 540));

                    // The white line across the middle, a hairline a pixel wide, plots grey to black, never white; the red side stays red.
                    SKColor[] across = Enumerable.Range(537, 7).Select(y => image.GetPixel(960, y)).ToArray();
                    Assert.Contains(across, p => p.Red < 160 && p.Red == p.Green && p.Green == p.Blue);
                    int left = (int)System.Math.Round((1920 - 7500 * page.Scale) / 2.0);
                    Assert.Contains(Enumerable.Range(left - 3, 7).Select(x => image.GetPixel(x, 300)), p => p.Red > 200 && p.Green < p.Red - 40 && p.Blue < p.Red - 40);
                }

                // JPG: a JPEG of the same size.
                dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.JpgDevice);
                string jpeg = Path.Combine(folder.FullPath, dialog.SuggestedFileName);
                dialog.Plot(jpeg);

                Assert.Equal(new byte[] { 0xFF, 0xD8 }, File.ReadAllBytes(jpeg).Take(2).ToArray());

                using (SKBitmap image = SKBitmap.Decode(jpeg))
                {
                    Assert.Equal(1920, image.Width);
                    Assert.Equal(1080, image.Height);
                }
            }
        }

        [Fact]
        public void CustomMakes2480By1754_AddsItToTheList_AndRefusesASideOutside16To8000()
        {
            PlotDialogViewModel dialog = Dialog(PlanWithWhiteLine());
            dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PngDevice);
            var made = new List<CadPaper>();
            dialog.CustomSizeMade += (s, size) => made.Add(size);
            CustomImageSizeViewModel asked = null;
            dialog.ShowCustomSize = custom =>
            {
                asked = custom;
                Assert.Equal("1920", custom.WidthText);
                custom.WidthText = "9000";
                Assert.False(custom.IsWidthValid);
                Assert.False(custom.CanOk);
                custom.WidthText = "2480";
                custom.HeightText = "1754";
                Assert.True(custom.CanOk);
                return true;
            };

            dialog.Paper = CadPaper.CustomPixelsChoice;

            Assert.NotNull(asked);
            Assert.Equal("2480 x 1754 Pixels (Custom)", dialog.Paper.Label);
            Assert.Equal("2480 x 1754 Pixels (Custom)", Assert.Single(made).Label);
            Assert.Contains(dialog.Papers, p => p.Label == "2480 x 1754 Pixels (Custom)");
            Assert.True(dialog.Papers.Last().IsCustomChoice);
            Assert.Equal("2480 PIXELS", dialog.SheetWidthText);

            // Cancel keeps the size chosen.
            dialog.ShowCustomSize = custom => false;
            dialog.Paper = CadPaper.CustomPixelsChoice;
            Assert.Equal("2480 x 1754 Pixels (Custom)", dialog.Paper.Label);
        }

        [Fact]
        public void TheSizesOfTheUsersOwnAreKeptInTheSettingsFile_NewestFirst()
        {
            using (var folder = new TestFolder())
            {
                var settings = new GlobalSettings();
                settings.NoteCustomPixelSize(CadPaper.Pixels(800, 600));
                settings.NoteCustomPixelSize(CadPaper.Pixels(2480, 1754));
                settings.NoteCustomPixelSize(CadPaper.Pixels(1920, 1080));
                string path = Path.Combine(folder.FullPath, "settings.json");
                settings.Save(path);

                GlobalSettings read = GlobalSettings.Load(path);

                Assert.Equal(new[] { "2480 x 1754 Pixels (Custom)", "800 x 600 Pixels (Custom)" }, read.CustomPixelSizes.Select(p => p.Label).ToArray());
            }
        }

        [Fact]
        public void WithFiledia0_OkAsksTheFileOnTheCommandLine_AndWritesThePng()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", PlanWithWhiteLine().Database)));
                model.ShowPlot = dialog =>
                {
                    dialog.Device = dialog.Devices.Single(d => d.Name == CadPlotSettings.PngDevice);
                    dialog.Paper = dialog.Papers.First();
                    return true;
                };
                model.AskPlotFile = (where, name) => throw new Xunit.Sdk.XunitException("FILEDIA 0 asks no file dialog.");
                model.Execute("FILEDIA");
                model.Execute("0");

                model.Execute("PLOT");

                Assert.Equal("Enter file name <" + Path.Combine(folder.FullPath, "plan-level2-Model.png") + ">:", model.Session.PromptText);

                string path = Path.Combine(folder.FullPath, "out");
                model.Execute(path);

                Assert.False(model.Session.IsRunning);
                Assert.Contains("Plot written to " + path + ".png.", model.History);

                using (SKBitmap image = SKBitmap.Decode(path + ".png"))
                {
                    Assert.Equal(1280, image.Width);
                    Assert.Equal(720, image.Height);
                }
            }
        }
    }
}
