using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// EXPORTPDF's Save As PDF dialog as AutoCAD 2021's: the options it hangs on its file request (Output Controls, Export,
    /// Page Setup, Options) and what the command does with each, the window stubbed.
    /// </summary>
    public class ExportPdfDialogTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "exportpdf-" + Guid.NewGuid().ToString("N")
        );

        public ExportPdfDialogTests()
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

        private sealed class Window
        {
            public CadExportPdfOptions Options;
            public readonly List<object> Written = new List<object>();
            public readonly List<string> Opened = new List<string>();
        }

        /// <summary>A session whose window sets the dialog's options, answers the file and keeps what it is handed.</summary>
        private static CadCommandSession Stubbed(
            CadDocument document,
            string file,
            Action<CadExportPdfOptions> set,
            Window window
        )
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                switch (e.Name)
                {
                    case "EXPORTPDF":
                        window.Options = e.Export.PdfOptions;
                        set?.Invoke(e.Export.PdfOptions);
                        e.Export.FileName = file;
                        e.Handled = true;
                        break;
                    case "-PLOT":
                        window.Written.Add(e.Payload);
                        e.Handled = true;
                        break;
                    case "OPENINVIEWER":
                        window.Opened.Add((string)e.Payload);
                        e.Handled = true;
                        break;
                }
            };
            return session;
        }

        private CadDocument TwoLines()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Line(10, 20, 190, 20, handle: "A1"),
                    TestDrawings.Line(10, 80, 190, 80, handle: "A2")
                )
            );
            document.SaveAs(Path.Combine(_folder, "two-lines.dxf"));
            return document;
        }

        private CadDocument Sheets()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric());
            document.SaveAs(Path.Combine(_folder, "sheets.dxf"));
            document.CurrentSpace = document.Spaces.First(s => !s.IsModel);
            return document;
        }

        [Fact]
        public void TheFileRequest_CarriesTheOutputControls_AsAutoCadOffersThem()
        {
            var window = new Window();
            Run(Stubbed(Sheets(), Path.Combine(_folder, "a.pdf"), null, window), "EXPORTPDF");

            CadExportPdfOptions layout = window.Options;
            Assert.False(layout.IsModel);
            Assert.Equal(new[] { "Current layout", "All layouts" }, layout.Exports);
            Assert.Equal(CadExportPdfOptions.CurrentLayout, layout.Export);
            Assert.False(layout.OverridePageSetup);
            Assert.False(layout.IncludePlotStamp);
            Assert.False(layout.OpenInViewer);
            Assert.True(layout.IncludeHyperlinks);

            window = new Window();
            Run(Stubbed(TwoLines(), Path.Combine(_folder, "b.pdf"), null, window), "EXPORTPDF");
            Assert.True(window.Options.IsModel);
            Assert.Equal(new[] { "Display", "Extents", "Window" }, window.Options.Exports);
            Assert.Equal(CadExportPdfOptions.Display, window.Options.Export);
            Assert.Throws<ArgumentException>(() => window.Options.Export = CadExportPdfOptions.AllLayouts);
        }

        [Fact]
        public void AllLayouts_WritesOnePdfOfAPageForEachLayout()
        {
            CadDocument document = Sheets();
            var window = new Window();
            string file = Path.Combine(_folder, "all.pdf");
            CadCommandSession session = Stubbed(document, file, o => o.Export = CadExportPdfOptions.AllLayouts, window);

            Run(session, "EXPORTPDF");

            var book = Assert.IsType<CadPlotBook>(Assert.Single(window.Written));
            Assert.Equal(file, book.FileName);
            Assert.Equal(document.Spaces.Count(s => !s.IsModel), book.Pages.Count);
            Assert.Equal("Plot written to " + file + ".", session.History.Last());
        }

        [Fact]
        public void PageSetupOverride_ReplacesThePageSetupInUse_AndCurrentKeepsIt()
        {
            CadDocument document = Sheets();
            var window = new Window();
            CadPaper a1 = CadPaper.Standard.First(p => p.Name == "ISO A1");
            Run(
                Stubbed(
                    document,
                    Path.Combine(_folder, "o.pdf"),
                    o =>
                    {
                        o.OverridePageSetup = true;
                        o.PageSetupOverride.Paper = a1;
                        o.PageSetupOverride.Landscape = false;
                        o.PageSetupOverride.StyleTable = CadPlotStyleTable.Grayscale;
                    },
                    window
                ),
                "EXPORTPDF"
            );

            var job = Assert.IsType<CadPlotJob>(Assert.Single(window.Written));
            Assert.Same(a1, job.Settings.Paper);
            Assert.False(job.Settings.Landscape);
            Assert.Equal(CadPlotStyleTable.Grayscale, job.Settings.StyleTable);
            Assert.Equal(CadPlotArea.Layout, job.Settings.Area);

            // Current: the layout's own sheet, the override set in the dialog left unused.
            window = new Window();
            Run(
                Stubbed(document, Path.Combine(_folder, "c.pdf"), o => o.PageSetupOverride.Paper = a1, window),
                "EXPORTPDF"
            );
            job = Assert.IsType<CadPlotJob>(Assert.Single(window.Written));
            Assert.NotSame(a1, job.Settings.Paper);
        }

        [Fact]
        public void Window_WithNoBoxAsksItsCornersAfterTheDialog_AndExtentsPlotsTheExtents()
        {
            var window = new Window();
            CadCommandSession session = Stubbed(
                TwoLines(),
                Path.Combine(_folder, "w.pdf"),
                o => o.Export = CadExportPdfOptions.Window,
                window
            );

            Run(session, "EXPORTPDF");
            Assert.Equal("Specify first corner:", session.PromptText);
            Run(session, "0,0");
            Assert.StartsWith("Specify opposite corner", session.PromptText, StringComparison.Ordinal);
            Run(session, "100,50");

            var job = Assert.IsType<CadPlotJob>(Assert.Single(window.Written));
            Assert.Equal(CadPlotArea.Window, job.Settings.Area);
            Assert.Equal(new DisplayBox(0, 0, 100, 50), job.Settings.Window);

            window = new Window();
            Run(
                Stubbed(
                    TwoLines(),
                    Path.Combine(_folder, "e.pdf"),
                    o => o.Export = CadExportPdfOptions.Extents,
                    window
                ),
                "EXPORTPDF"
            );
            Assert.Equal(CadPlotArea.Extents, Assert.IsType<CadPlotJob>(Assert.Single(window.Written)).Settings.Area);
        }

        [Fact]
        public void OpenInViewer_AsksTheWindowToOpenThePdfWritten_AndOffAsksNothing()
        {
            var window = new Window();
            string file = Path.Combine(_folder, "open.pdf");
            Run(Stubbed(TwoLines(), file, o => o.OpenInViewer = true, window), "EXPORTPDF");
            Assert.Equal(new[] { file }, window.Opened);

            window = new Window();
            Run(Stubbed(TwoLines(), file, null, window), "EXPORTPDF");
            Assert.Empty(window.Opened);
        }

        [Fact]
        public void IncludePlotStamp_StampsThePdf_AndTheStampSettingsBecomeTheSessions()
        {
            var window = new Window();
            CadDocument document = TwoLines();
            CadCommandSession session = Stubbed(
                document,
                Path.Combine(_folder, "s.pdf"),
                o =>
                {
                    o.IncludePlotStamp = true;
                    o.PlotStamp.DateAndTime = false;
                },
                window
            );

            Run(session, "EXPORTPDF");

            var job = Assert.IsType<CadPlotJob>(Assert.Single(window.Written));
            Assert.NotNull(job.Settings.PlotStamp);
            Assert.False(job.Settings.PlotStamp.DateAndTime);
            Assert.True(session.PlotStamp.On);
            Assert.False(session.PlotStamp.DateAndTime);
        }

        [Fact]
        public void IncludeHyperlinks_ReachesThePdfWriter_OnAsMade()
        {
            var window = new Window();
            Run(
                Stubbed(TwoLines(), Path.Combine(_folder, "q.pdf"), o => o.IncludeHyperlinks = false, window),
                "EXPORTPDF"
            );
            Assert.False(Assert.IsType<CadPlotJob>(Assert.Single(window.Written)).Settings.PdfHyperlinks);

            window = new Window();
            Run(Stubbed(TwoLines(), Path.Combine(_folder, "r.pdf"), null, window), "EXPORTPDF");
            Assert.True(Assert.IsType<CadPlotJob>(Assert.Single(window.Written)).Settings.PdfHyperlinks);
        }
    }
}
