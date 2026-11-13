using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The items of dialogs8 driven with no window shown: Insert Layout(s) and Template Options of LAYOUT, the plot stamp on
    /// a PNG plot and on a plot upside down, and a PDF of all layouts of a page each.
    /// </summary>
    public class DialogsEightWindowTests
    {
        /// <summary>A template of the sheets Plan, Sect and Cover, a line on each, written as DXF.</summary>
        private static string TemplateFile(TestFolder folder)
        {
            DxfDatabase database = CadTemplate.CreateMetric();

            foreach (string sheet in new[] { "Plan", "Sect", "Cover" })
            {
                var layout = new DxfLayout { LayoutName = sheet };
                layout.AddToLayoutDictionary(database, null);
                layout.Initialize();
                layout.BlockTableRecord.AppendEntity(new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(100, 50, 0)));
            }

            return folder.Write("sheets.dxf", database);
        }

        private static MainViewModel Opened(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("site.dxf", CadTemplate.CreateMetric())));
            return model;
        }

        private static string[] Names(MainViewModel model) => model.Document.Spaces.Select(s => s.Name).ToArray();

        [Fact]
        public void LayoutTemplate_ShowsSelectTemplateFromFile_ThenInsertLayouts_AndBringsInThePicked_AsOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                string template = TemplateFile(folder);
                MainViewModel model = Opened(folder);
                string[] before = Names(model);
                CadExportJob asked = null;
                model.AskExportFile = job =>
                {
                    asked = job;
                    job.FileName = template;
                };
                InsertLayoutViewModel shown = null;
                string[] listed = null;
                string[] picked = null;
                model.ShowInsertLayout = dialog =>
                {
                    shown = dialog;
                    listed = dialog.Layouts.Select(l => l.Name).ToArray();
                    picked = dialog.PickedNames.ToArray();
                    dialog.Layouts[0].IsPicked = false;
                    dialog.Layouts[1].IsPicked = true;
                    dialog.Layouts[3].IsPicked = true;
                    return true;
                };

                model.Execute("LAYOUT");
                model.Execute("T");

                // The template's tabs in their order, its own Layout1 first and picked to start with.
                Assert.Equal(template, shown.FileName);
                Assert.Equal(new[] { "Layout1", "Plan", "Sect", "Cover" }, listed);
                Assert.Equal(new[] { "Layout1" }, picked);

                Assert.Equal("Select Template From File", asked.Title);
                Assert.True(asked.Opens);
                Assert.Equal(new[] { "Drawing Template (*.dwt)", "Drawing (*.dwg)", "DXF (*.dxf)" }, asked.FileTypes);
                Assert.NotNull(shown);
                Assert.False(model.IsCommandRunning);
                Assert.Equal(before.Concat(new[] { "Plan", "Cover" }), Names(model));

                Assert.True(model.Document.Undo());
                Assert.Equal(before, Names(model));
            }
        }

        [Fact]
        public void LayoutTemplate_CancelInEitherDialog_BringsNothingIn_AndWithNoDialogTheNamesAreTyped()
        {
            using (var folder = new TestFolder())
            {
                string template = TemplateFile(folder);
                MainViewModel model = Opened(folder);
                string[] before = Names(model);
                model.AskExportFile = job => job.FileName = template;
                model.ShowInsertLayout = dialog => false;

                model.Execute("-LAYOUT");
                model.Execute("T");
                Assert.False(model.IsCommandRunning);
                Assert.Equal(before, Names(model));
                Assert.Null(model.Document.NextUndo);

                model.AskExportFile = job => job.FileName = null;
                model.ShowInsertLayout = dialog => throw new InvalidOperationException("No layouts are asked.");
                model.Execute("LAYOUT");
                model.Execute("T");
                Assert.False(model.IsCommandRunning);
                Assert.Equal(before, Names(model));

                // The file chosen in the dialog, the names typed where no Insert Layout(s) is set.
                model.AskExportFile = job => job.FileName = template;
                model.ShowInsertLayout = null;
                model.Execute("LAYOUT");
                model.Execute("T");
                Assert.Equal("Enter layout name(s) or [?]:", model.Session.PromptText);
                model.Execute("Sect");
                Assert.Equal(before.Concat(new[] { "Sect" }), Names(model));
            }
        }

        [Fact]
        public void LayoutSaveAs_ShowsCreateDrawingFile_ThenTemplateOptions_AndWritesTheMeasurementChosen()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                string file = Path.Combine(folder.FullPath, "sheet.dwt");
                CadExportJob asked = null;
                string offered = null;
                model.AskExportFile = job =>
                {
                    asked = job;
                    offered = job.FileName;
                    job.FileName = file;
                };
                TemplateOptionsViewModel shown = null;
                string[] before = null;
                model.ShowTemplateOptions = dialog =>
                {
                    shown = dialog;
                    before = new[] { dialog.FileName, dialog.Measurement, dialog.Description };
                    dialog.Description = "Workshop sheet A1";
                    dialog.Measurement = "English";
                    return true;
                };
                string layout = Names(model)[1];

                model.Execute("LAYOUT");
                model.Execute("SA");
                model.Execute(layout);

                Assert.False(model.IsCommandRunning);
                Assert.Equal("Create Drawing File", asked.Title);
                Assert.Equal("AutoCAD Drawing Template (*.dwt)", asked.FileTypes[0]);
                Assert.Equal(CadExportJob.Dwg, asked.FileTypes[1]);
                Assert.Contains("AutoCAD 2010/LT2010 DXF (*.dxf)", asked.FileTypes);
                Assert.Equal(layout + ".dwt", Path.GetFileName(offered));
                Assert.Equal(new[] { "English", "Metric" }, shown.Measurements);
                Assert.Equal(new[] { file, "Metric", string.Empty }, before);
                Assert.True(CadDrawingFile.IsDwg(file));
                DxfDatabase written = CadDrawingFile.Open(file);
                Assert.Equal(DxfMeasurementValue.English, written.Measurement);
                Assert.Contains(written.Layouts, l => l.LayoutName == layout);
            }
        }

        [Fact]
        public void LayoutSaveAs_CancelInTemplateOptionsWritesNothing_AndADxfTypeWritesItsVersionWithNoOptions()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                string layout = Names(model)[1];
                string dwt = Path.Combine(folder.FullPath, "none.dwt");
                model.AskExportFile = job => job.FileName = dwt;
                model.ShowTemplateOptions = dialog => false;

                model.Execute("LAYOUT");
                model.Execute("SA");
                model.Execute(layout);
                Assert.False(model.IsCommandRunning);
                Assert.False(File.Exists(dwt));

                string dxf = Path.Combine(folder.FullPath, "sheet.dxf");
                model.AskExportFile = job =>
                {
                    job.FileName = dxf;
                    job.FileType = "AutoCAD 2010/LT2010 DXF (*.dxf)";
                };
                model.ShowTemplateOptions = dialog => throw new InvalidOperationException("No options for a DXF.");
                model.Execute("LAYOUT");
                model.Execute("SA");
                model.Execute(layout);

                Assert.False(model.IsCommandRunning);
                Assert.Equal("AC1024", DxfDatabase.Open(dxf).Version);
            }
        }

        // ---- The plot stamp on images and on a plot upside down

        /// <summary>A PNG plot of the plan on 1920 by 1080 pixels, its pixels.</summary>
        private static SKBitmap Png(TestFolder folder, CadPlotStamp stamp, bool upsideDown)
        {
            CadDocument document = PlotPdfTests.Plan();
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
            settings.UseDevice(CadPlotSettings.PngDevice);
            settings.Area = CadPlotArea.Extents;
            settings.UpsideDown = upsideDown;
            settings.PlotStamp = stamp;
            string path = Path.Combine(folder.FullPath, Guid.NewGuid().ToString("N") + ".png");
            CadRasterWriter.WritePlot(path, document, document.ModelSpace, settings, DisplayBox.Empty);
            return SKBitmap.Decode(path);
        }

        /// <summary>The box of the pixels two images differ in, in rows from the top; empty where they are the same.</summary>
        private static DisplayBox Differ(SKBitmap one, SKBitmap other)
        {
            DisplayBox box = DisplayBox.Empty;
            SKColor[] a = one.Pixels;
            SKColor[] b = other.Pixels;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    box = box.Add(i % one.Width, i / one.Width);
                }
            }

            return box;
        }

        [Fact]
        public void APngPlot_IsStampedInItsCorner_AtNinetySixPixelsToTheInch_AndTheLogGetsItsLine()
        {
            using (var folder = new TestFolder())
            using (SKBitmap plain = Png(folder, null, false))
            using (
                SKBitmap stamped = Png(
                    folder,
                    new CadPlotStamp
                    {
                        UserField1 = "Checked by AB",
                        LogFile = Path.Combine(folder.FullPath, "plot.log"),
                    },
                    false
                )
            )
            {
                DisplayBox ink = Differ(plain, stamped);

                // 0.1 inch in from the lower left corner: 9.6 pixels; three or more lines of 19.2-pixel letters above.
                Assert.False(ink.IsEmpty);
                Assert.InRange(ink.MinX, 8.0, 14.0);
                Assert.InRange(1080 - ink.MaxY, 4.0, 11.0);
                Assert.True(1080 - ink.MinY > 55.0, "The stamp is several lines of 19.2-pixel letters high.");
                Assert.True(ink.MaxX < 960.0, "The stamp stays in the left half.");
                Assert.Contains("Checked by AB", File.ReadAllText(Path.Combine(folder.FullPath, "plot.log")));
            }
        }

        [Fact]
        public void APngPlotUpsideDown_HasItsStampTurnedWithIt_InTheOppositeCorner()
        {
            using (var folder = new TestFolder())
            using (SKBitmap plain = Png(folder, null, true))
            using (SKBitmap stamped = Png(folder, new CadPlotStamp { WriteLog = false }, true))
            {
                DisplayBox ink = Differ(plain, stamped);

                Assert.False(ink.IsEmpty);
                Assert.InRange(1920 - ink.MaxX, 4.0, 14.0);
                Assert.InRange(ink.MinY, 4.0, 14.0);
                Assert.True(ink.MinX > 960.0, "The stamp stands in the right half.");
            }
        }

        [Fact]
        public void TheFootprintOfAStampOnAPlotUpsideDown_IsTheBoxTurnedHalfRound()
        {
            CadDocument document = PlotPdfTests.Plan();
            CadPlotSettings settings = PlotPdfTests.AtOneToTwenty();
            CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
            var stamp = new CadPlotStamp();

            DisplayBox upright = stamp.Footprint(page, 60.0, 15.0);
            DisplayBox turned = stamp.Footprint(page, 60.0, 15.0, true, 1.0);

            Assert.Equal(page.SheetWidth - upright.MaxX, turned.MinX, 9);
            Assert.Equal(page.SheetHeight - upright.MaxY, turned.MinY, 9);
            Assert.Equal(60.0, turned.Width, 9);
            Assert.Equal(15.0, turned.Height, 9);

            // On a sheet of pixels a millimetre is 96 / 25.4 of them.
            DisplayBox pixels = stamp.Footprint(page, 60.0, 15.0, false, 96.0 / 25.4);
            Assert.Equal(60.0 * 96.0 / 25.4, pixels.Width, 9);
            Assert.Throws<ArgumentOutOfRangeException>(() => stamp.Footprint(page, 1.0, 1.0, false, 0.0));
        }

        // ---- A PDF of all layouts

        [Fact]
        public void APdfOfTwoLayouts_IsTwoPages_EachTheSizeOfItsOwnSheet()
        {
            using (var folder = new TestFolder())
            {
                DxfDatabase database = CadTemplate.CreateMetric();
                var second = new DxfLayout { LayoutName = "Second" };
                second.AddToLayoutDictionary(database, null);
                second.Initialize();
                CadDocument document = CadDocument.From(database, "site.dxf");
                CadSpace[] layouts = document.Spaces.Where(s => !s.IsModel).ToArray();
                CadSpace[] plotted = { layouts[0], layouts.Single(s => s.Name == "Second") };
                int[] sheets = { 3, 1 };
                var jobs = new List<CadPlotJob>();

                // The first on ISO A3, the second on ISO A1, each on its side.
                for (int i = 0; i < 2; i++)
                {
                    CadPlotSettings settings = CadPlotSettings.Read(document, plotted[i]);
                    settings.UseDevice(CadPlotSettings.PdfDevice);
                    settings.Area = CadPlotArea.Layout;
                    settings.Paper = CadPaper.Standard[sheets[i]];
                    settings.Landscape = true;
                    settings.PlotStamp = new CadPlotStamp
                    {
                        WriteLog = false,
                        UserField1 = "Sheet of " + plotted[i].Name,
                    };
                    jobs.Add(new CadPlotJob(plotted[i], settings, DisplayBox.Empty, null));
                }

                string path = Path.Combine(folder.FullPath, "site.pdf");
                IReadOnlyList<CadPlotPage> pages = CadPdfWriter.WritePages(path, document, jobs);

                byte[] pdf = File.ReadAllBytes(path);
                string text = Encoding.GetEncoding(28591).GetString(pdf);
                Assert.Equal(2, pages.Count);
                Assert.Equal(2, Regex.Matches(text, @"/Type /Page\b").Count);
                string[] boxes = Regex
                    .Matches(text, @"/MediaBox \[0 0 ([0-9.]+) ([0-9.]+)\]")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value + " " + m.Groups[2].Value)
                    .ToArray();
                Assert.Contains("1190.5512 841.8898", boxes);
                Assert.Contains("2383.937 1683.7795", boxes);
                Pdf.AssertCrossReferencesPointAtTheirObjects(pdf);
                string words = Pdf.Text(pdf);
                Assert.Contains("Sheet of " + plotted[0].Name, words);
                Assert.Contains("Sheet of Second", words);
            }
        }

        [Fact]
        public void DashExportAllLayouts_InTheWindow_WritesOnePdfOfAPageForEachLayout()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                string pdf = Path.Combine(folder.FullPath, "all.pdf");
                model.AskExportFile = job => job.FileName = pdf;
                model.Document.CurrentSpace = model.Document.Spaces.First(s => !s.IsModel);
                int layouts = model.Document.Spaces.Count(s => !s.IsModel);

                model.Execute("-EXPORT");
                model.Execute("P");
                model.Execute("A");
                model.Execute(string.Empty);

                Assert.False(model.IsCommandRunning);
                Assert.Contains("Plot written to " + pdf + ".", model.History);
                string text = Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(pdf));
                Assert.Equal(layouts, Regex.Matches(text, @"/Type /Page\b").Count);
            }
        }
    }
}
