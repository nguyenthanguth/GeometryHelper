using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The items of dialogs9 driven with no window shown: EXPORTPDF's Save As PDF, Page Setup Override and Export to PDF
    /// Options, the PDF they write, its hyperlinks, and the PDF opened in the viewer when done.
    /// </summary>
    public class DialogsNineWindowTests
    {
        private static MainViewModel Opened(TestFolder folder, DxfDatabase database = null)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("site.dxf", database ?? CadTemplate.CreateMetric())));
            return model;
        }

        /// <summary>A metric drawing of one line 10 to 190 along, with a hyperlink to a web address where one is given.</summary>
        private static DxfDatabase Linked(string address)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var line = new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(190, 80, 0));
            database.ModelSpace.AppendEntity(line);

            if (address != null)
            {
                line.Hyperlinks.Add(new DxfHyperLink { Name = address, Description = "Plate B-12" });
            }

            return database;
        }

        private static string Latin(string file) => Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(file));

        [Fact]
        public void ExportPdf_ShowsSaveAsPdf_AndSaveWritesTheFileNamed_ThenOpensItInTheViewer()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                model.Document.CurrentSpace = model.Document.Spaces.First(s => !s.IsModel);
                string pdf = Path.Combine(folder.FullPath, "sheet.pdf");
                SaveAsPdfViewModel shown = null;
                var opened = new List<string>();
                model.OpenInViewer = opened.Add;
                model.ShowSaveAsPdf = dialog =>
                {
                    shown = dialog;
                    dialog.FileName = pdf;
                    return true;
                };

                model.Execute("EXPORTPDF");

                Assert.NotNull(shown);
                Assert.Equal(new[] { "Current layout", "All layouts" }, shown.Exports);
                Assert.Equal("Current layout", shown.Export);
                Assert.Equal("Current", shown.PageSetup);
                Assert.True(shown.OpenInViewer);
                Assert.Equal("Hyperlinks included", shown.OptionsSummary);
                Assert.False(model.IsCommandRunning);
                Assert.True(File.Exists(pdf));
                Assert.Contains("Plot written to " + pdf + ".", model.History);
                Assert.Equal(new[] { pdf }, opened);
            }
        }

        [Fact]
        public void Cancel_WritesNothing_AndOpenInViewerOffIsKeptForTheNextExport()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, Linked(null));
                string pdf = Path.Combine(folder.FullPath, "plan.pdf");
                var opened = new List<string>();
                model.OpenInViewer = opened.Add;
                model.ShowSaveAsPdf = dialog => false;

                model.Execute("EXPORTPDF");
                Assert.False(model.IsCommandRunning);
                Assert.False(File.Exists(pdf));

                bool? seen = null;
                model.ShowSaveAsPdf = dialog =>
                {
                    dialog.FileName = pdf;
                    dialog.OpenInViewer = false;
                    return true;
                };
                model.Execute("EXPORTPDF");
                Assert.True(File.Exists(pdf));
                Assert.Empty(opened);

                model.ShowSaveAsPdf = dialog =>
                {
                    seen = dialog.OpenInViewer;
                    return false;
                };
                model.Execute("EXPORTPDF");
                Assert.False(seen);
            }
        }

        [Fact]
        public void Browse_ShowsTheSaveDialogOfPdf_AndAnEmptyNameIsRefused()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, Linked(null));
                string chosen = Path.Combine(folder.FullPath, "chosen.pdf");
                CadExportJob asked = null;
                model.AskExportFile = job =>
                {
                    asked = job;
                    job.FileName = chosen;
                };
                string message = null;
                model.ShowSaveAsPdf = dialog =>
                {
                    dialog.Browse();
                    Assert.Equal(chosen, dialog.FileName);
                    dialog.FileName = "  ";
                    Assert.False(dialog.Accept());
                    message = dialog.Message;
                    dialog.FileName = Path.Combine(folder.FullPath, "named");
                    return true;
                };

                model.Execute("EXPORTPDF");

                Assert.Equal("Save As PDF", asked.Title);
                Assert.Equal(new[] { CadExportJob.Pdf }, asked.FileTypes);
                Assert.Equal("Enter a file name.", message);
                Assert.True(File.Exists(Path.Combine(folder.FullPath, "named.pdf")));
            }
        }

        [Fact]
        public void AllLayoutsWithPageSetupOverride_WritesAPageForEachLayout_OnTheSheetOverridden()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                model.Document.CurrentSpace = model.Document.Spaces.First(s => !s.IsModel);
                int layouts = model.Document.Spaces.Count(s => !s.IsModel);
                string pdf = Path.Combine(folder.FullPath, "all.pdf");
                model.ShowPageSetupOverride = dialog =>
                {
                    dialog.Paper = dialog.Papers.First(p => p.StartsWith("ISO A1", StringComparison.Ordinal));
                    dialog.Portrait = true;
                    return true;
                };
                model.ShowSaveAsPdf = dialog =>
                {
                    dialog.FileName = pdf;
                    dialog.Export = "All layouts";
                    Assert.True(dialog.OverridePageSetup());
                    Assert.Equal("Override", dialog.PageSetup);
                    return true;
                };
                model.OpenInViewer = file => { };

                model.Execute("EXPORTPDF");

                string text = Latin(pdf);
                Assert.Equal(layouts, Regex.Matches(text, @"/Type /Page\b").Count);
                string box =
                    "/MediaBox [0 0 "
                    + (594 * 72 / 25.4).ToString("0.####", CultureInfo.InvariantCulture)
                    + " "
                    + (841 * 72 / 25.4).ToString("0.####", CultureInfo.InvariantCulture)
                    + "]";
                Assert.Equal(layouts, Regex.Matches(text, Regex.Escape(box)).Count);

                // The override is kept for the next EXPORTPDF, Page Setup on Override.
                string kept = null;
                model.ShowSaveAsPdf = dialog =>
                {
                    kept = dialog.PageSetup;
                    return false;
                };
                model.Execute("EXPORTPDF");
                Assert.Equal("Override", kept);
            }
        }

        [Fact]
        public void PageSetupOverride_RefusesAScaleNotAboveNought_AndChangesNothing()
        {
            var settings = new CadPlotSettings();
            var dialog = new PageSetupOverrideViewModel(settings)
            {
                FitToPaper = false,
                PaperUnits = "1",
                DrawingUnits = "0",
                StyleTable = "grayscale.ctb",
            };

            Assert.False(dialog.TryAccept());
            Assert.Equal("Enter a scale above nought.", dialog.Message);
            Assert.True(settings.FitToPaper);
            Assert.Equal(CadPlotStyleTable.Monochrome, settings.StyleTable);

            dialog.DrawingUnits = "50";
            Assert.True(dialog.TryAccept());
            Assert.False(settings.FitToPaper);
            Assert.Equal(50.0, settings.DrawingUnits);
            Assert.Equal(CadPlotStyleTable.Grayscale, settings.StyleTable);
        }

        [Fact]
        public void IncludeHyperlinks_WritesTheLinkOfAnObject_AndOffWritesNone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, Linked("https://example.com/B-12.pdf"));
                string linked = Path.Combine(folder.FullPath, "linked.pdf");
                string plain = Path.Combine(folder.FullPath, "plain.pdf");
                string summary = null;
                model.OpenInViewer = file => { };
                model.ShowSaveAsPdf = dialog =>
                {
                    dialog.FileName = linked;
                    dialog.Export = "Extents";
                    return true;
                };

                model.Execute("EXPORTPDF");
                Assert.Contains("/URI (https://example.com/B-12.pdf)", Latin(linked));

                model.ShowExportPdfOptions = options =>
                {
                    options.IncludeHyperlinks = false;
                    return true;
                };
                model.ShowSaveAsPdf = dialog =>
                {
                    dialog.FileName = plain;
                    dialog.Export = "Extents";
                    Assert.True(dialog.EditOptions());
                    summary = dialog.OptionsSummary;
                    return true;
                };
                model.Execute("EXPORTPDF");

                Assert.Equal("Hyperlinks left out", summary);
                Assert.DoesNotContain("/URI", Latin(plain));
            }
        }

        [Fact]
        public void WithNoSaveAsPdf_ExportPdfShowsTheSaveDialogAlone_AsBefore()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, Linked(null));
                string pdf = Path.Combine(folder.FullPath, "alone.pdf");
                CadExportJob asked = null;
                model.AskExportFile = job =>
                {
                    asked = job;
                    job.FileName = pdf;
                };

                model.Execute("EXPORTPDF");

                Assert.Equal("Save As PDF", asked.Title);
                Assert.True(File.Exists(pdf));
            }
        }
    }
}
