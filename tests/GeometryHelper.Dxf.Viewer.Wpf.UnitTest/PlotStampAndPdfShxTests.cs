using System;
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
    /// The plot stamp and PDFSHX in GeometryHelper PDF: the stamp's lines written as text at the corner the stamp names and
    /// added to its log, and the words of an SHX font written again over their strokes, found by a reader and not seen.
    /// </summary>
    public class PlotStampAndPdfShxTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "stamp-" + Guid.NewGuid().ToString("N")
        );

        public PlotStampAndPdfShxTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        private static byte[] Written(CadDocument document, CadPlotSettings settings)
        {
            using (var memory = new MemoryStream())
            {
                CadPdfWriter.Write(memory, document, document.ModelSpace, settings, DisplayBox.Empty);
                return memory.ToArray();
            }
        }

        /// <summary>A plan of ROOM 1 in the SHX font the presenter has of its own, ghsimplex, 250 high, drawn as its strokes.</summary>
        private static CadDocument ShxPlan()
        {
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable["Standard"].FileName = CadFonts.BuiltInName + ".shx";
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(120, 60, 0), new GeoPoint3(7620, 5360, 0)));
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = "ROOM 1",
                    Position = new GeoPoint3(1500, 2500, 0),
                    Height = 250,
                }
            );
            CadDocument document = CadDocument.From(database, "shx-plan.dxf");
            document.Fonts = new GlobalSettings().ToFonts();
            return document;
        }

        [Fact]
        public void AStampedPlot_HasTheStampsLinesAsText_AndAPlotWithNoStampHasNone()
        {
            CadDocument document = PlotPdfTests.Plan();
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();

            Assert.DoesNotContain("Checked by AB", Pdf.Text(Written(document, settings)));

            settings.PlotStamp = new CadPlotStamp
            {
                LayoutName = true,
                PaperSize = true,
                PlotScale = true,
                UserField1 = "Checked by AB",
            };
            byte[] pdf = Written(document, settings);
            string text = Pdf.Text(pdf);

            Assert.Contains("plan-level2.dxf", text);
            Assert.Contains("Model", text);
            Assert.Contains(CadPlotSettings.PdfDevice, text);
            Assert.Contains(settings.Paper.Label, text);
            Assert.Contains("1:20", text);
            Assert.Contains("Checked by AB", text);
            Assert.Contains("ROOM 1", text);
            Pdf.AssertCrossReferencesPointAtTheirObjects(pdf);
        }

        /// <summary>The box of dark pixels a stamp drew on a sheet of white, in the millimetres of the sheet, Y upward.</summary>
        private static DisplayBox Inked(CadPlotStamp stamp, out CadPlotPage page)
        {
            CadDocument document = PlotPdfTests.Plan();
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
            page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
            IReadOnlyList<string> lines = stamp.Lines(
                document,
                document.ModelSpace,
                settings,
                page,
                DateTime.Now,
                "ab"
            );
            const int perMillimetre = 4;
            int width = (int)Math.Ceiling(page.SheetWidth * perMillimetre);
            int height = (int)Math.Ceiling(page.SheetHeight * perMillimetre);
            DisplayBox ink = DisplayBox.Empty;

            using (var bitmap = new SKBitmap(width, height))
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                CadPlotStampPainter.Draw(
                    canvas,
                    stamp,
                    lines,
                    page,
                    new Affine2(perMillimetre, 0.0, 0.0, -perMillimetre, 0.0, height)
                );

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (bitmap.GetPixel(x, y).Red < 128)
                        {
                            ink = ink.Add((double)x / perMillimetre, (double)(height - y) / perMillimetre);
                        }
                    }
                }
            }

            return ink;
        }

        [Fact]
        public void TheStampIsDrawnInItsCorner_InByTheOffset()
        {
            // Three lines of 5.08 mm letters: a block about 17.8 mm high, its lower left at (7.54, 7.54), the descenders under it.
            DisplayBox ink = Inked(new CadPlotStamp(), out CadPlotPage page);
            Assert.False(ink.IsEmpty);
            Assert.InRange(ink.MinX, 7.0, 9.0);
            Assert.InRange(ink.MinY, 5.0, 8.0);
            Assert.InRange(ink.MaxY, 24.0, 27.0);
            Assert.InRange(ink.MaxX, 20.0, page.SheetWidth / 2.0);

            ink = Inked(new CadPlotStamp { Location = CadPlotStampCorner.TopRight }, out page);
            Assert.InRange(ink.MaxX, 410.0, 413.5);
            Assert.InRange(ink.MaxY, 287.0, 290.5);
            Assert.True(ink.MinX > page.SheetWidth / 2.0);
        }

        [Fact]
        public void AVerticalStampReadsUpTheSheet_TallerThanItIsWide()
        {
            DisplayBox ink = Inked(new CadPlotStamp { Vertical = true }, out _);

            Assert.True(
                ink.Height > ink.Width,
                "A vertical stamp is taller than wide: " + ink.Width + " by " + ink.Height
            );
            DisplayBox flat = Inked(new CadPlotStamp(), out _);
            Assert.True(flat.Width > flat.Height);
        }

        [Fact]
        public void AStampOnAFile_AddsItsLineToTheLog_BesideThePlot_ThatManyTimesAsPlotsAreMade()
        {
            CadDocument document = PlotPdfTests.Plan();
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
            settings.PlotStamp = new CadPlotStamp { LoginName = true };
            string plot = Path.Combine(_folder, "plan.pdf");

            CadPdfWriter.Write(plot, document, document.ModelSpace, settings, DisplayBox.Empty);
            CadPdfWriter.Write(plot, document, document.ModelSpace, settings, DisplayBox.Empty);

            string[] log = File.ReadAllLines(Path.Combine(_folder, "plot.log"));
            Assert.Equal(2, log.Length);
            Assert.Contains("plan-level2.dxf", log[0]);
            Assert.Contains(Environment.UserName, log[0]);
            Assert.EndsWith(CadPlotSettings.PdfDevice, log[0], StringComparison.Ordinal);

            settings.PlotStamp.WriteLog = false;
            settings.PlotStamp.LogFile = "off.log";
            CadPdfWriter.Write(plot, document, document.ModelSpace, settings, DisplayBox.Empty);
            Assert.False(File.Exists(Path.Combine(_folder, "off.log")));

            // A log that cannot be written does not fail the plot.
            settings.PlotStamp.WriteLog = true;
            settings.PlotStamp.LogFile = Path.Combine(_folder, "missing folder", "x.log");
            CadPdfWriter.Write(plot, document, document.ModelSpace, settings, DisplayBox.Empty);
        }

        [Fact]
        public void PdfShxWritesTheWordsOfAnShxFontAgain_ForAReaderToFind_AndNoEyeSeesThem()
        {
            CadDocument document = ShxPlan();
            Assert.Contains(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>(), t =>
                t.Strokes != null && t.Text == "ROOM 1"
            );
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();

            // As it was: the strokes alone, no text a reader finds.
            Assert.DoesNotContain("ROOM 1", Pdf.Text(Written(document, settings)));

            settings.PdfShx = true;
            Assert.Contains("ROOM 1", Pdf.Text(Written(document, settings)));

            // On a sheet the two plots differ by no more than one level of one colour in a pixel.
            using (SKBitmap plain = Rendered(document, false))
            using (SKBitmap hidden = Rendered(document, true))
            {
                int most = 0;
                int dark = 0;

                for (int y = 0; y < plain.Height; y++)
                {
                    for (int x = 0; x < plain.Width; x++)
                    {
                        SKColor a = plain.GetPixel(x, y);
                        SKColor b = hidden.GetPixel(x, y);
                        most = Math.Max(most, Math.Abs(a.Red - b.Red));
                        dark += a.Red < 128 ? 1 : 0;
                    }
                }

                Assert.True(dark > 0, "The plot drew something.");
                Assert.True(most <= 1, "The largest difference of a pixel was " + most + ".");
            }
        }

        private static void Type(MainViewModel model, string text)
        {
            model.CommandText = text;
            model.SubmitCommand();
        }

        [Fact]
        public void TheWindow_WritesTheStampedPdfOfExportPdf_ForTheFileItsDialogChose_AndShowsImportsOpenDialog()
        {
            string source = Path.Combine(_folder, "plan.dxf");
            PlotPdfTests.Plan().Database.Save(source);
            var model = new MainViewModel();
            Assert.True(model.Open(source));
            var asked = new List<CadExportJob>();
            string pdf = Path.Combine(_folder, "out.pdf");
            model.AskExportFile = job =>
            {
                asked.Add(job);
                job.FileName = job.Opens ? Path.Combine(_folder, "bolt.dxf") : pdf;
            };
            var bolt = new DxfDatabase("AC1027");
            bolt.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)));
            bolt.Save(Path.Combine(_folder, "bolt.dxf"));

            model.StartCommand("-PLOTSTAMP");
            Type(model, "On");
            Type(model, "User");
            Type(model, "Checked by AB");
            Type(model, string.Empty);
            Type(model, string.Empty);
            Assert.True(model.Session.PlotStamp.On);

            model.StartCommand("EXPORTPDF");
            Assert.Equal("Save As PDF", Assert.Single(asked).Title);
            Assert.True(File.Exists(pdf));
            Assert.Contains("Checked by AB", Pdf.Text(File.ReadAllBytes(pdf)));
            Assert.Contains("Plot written to " + pdf + ".", model.History);
            Assert.True(File.Exists(Path.Combine(_folder, "plot.log")));

            // The stamp is the window's, not one drawing's: a second drawing has it too.
            string other = Path.Combine(_folder, "other.dxf");
            PlotPdfTests.Plan().Database.Save(other);
            Assert.True(model.Open(other));
            Assert.True(model.Session.PlotStamp.On);

            model.StartCommand("IMPORT");
            Assert.True(asked[1].Opens);
            Assert.Equal("Import File", asked[1].Title);
            Type(model, "0,0");
            Type(model, string.Empty);
            Type(model, string.Empty);
            Type(model, string.Empty);
            Assert.True(model.Document.Database.BlockTable.Has("bolt"));
        }

        private static SKBitmap Rendered(CadDocument document, bool shxText)
        {
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
            const int perMillimetre = 4;
            int width = (int)Math.Ceiling(page.SheetWidth * perMillimetre);
            int height = (int)Math.Ceiling(page.SheetHeight * perMillimetre);
            var bitmap = new SKBitmap(width, height);

            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                canvas.Clear(SKColors.White);
                renderer.RenderPlot(
                    canvas,
                    document,
                    document.ModelSpace,
                    page,
                    settings,
                    new Affine2(perMillimetre, 0.0, 0.0, -perMillimetre, 0.0, height),
                    shxText: shxText
                );
            }

            return bitmap;
        }
    }
}
