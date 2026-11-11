using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// EXPORT and -EXPORT, as AutoCAD 2021's: EXPORT's Export Data types, Bitmap written through BMPOUT's image and the rest
    /// refused; -EXPORT's prompts as AutoCAD 2021's console writes them, its Pdf handed to -PLOT's writer, Dwf and dwfX
    /// refused. The window's dialogs are stubbed through the requests EXPORT and -EXPORT.
    /// </summary>
    public class ExportCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "export-" + Guid.NewGuid().ToString("N")
        );

        public ExportCommandTests()
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

        /// <summary>A session whose window answers EXPORT's or -EXPORT's dialog with a file and a type, and keeps the images and plots it is handed.</summary>
        private static CadCommandSession Stubbed(
            CadDocument document,
            Func<CadExportJob, string> file,
            string type,
            List<object> written,
            List<CadExportJob> asked
        )
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                switch (e.Name)
                {
                    case "EXPORT":
                    case "-EXPORT":
                        asked.Add(
                            new CadExportJob(e.Export.Title, e.Export.FileTypes, e.Export.FileType, e.Export.FileName)
                        );
                        e.Export.FileName = file(e.Export);
                        e.Export.FileType = type ?? e.Export.FileType;
                        e.Handled = true;
                        break;
                    case "IMAGEOUT":
                        written.Add(e.Image);
                        e.Handled = true;
                        break;
                    case "-PLOT":
                        written.Add(e.Plot);
                        e.Handled = true;
                        break;
                }
            };
            return session;
        }

        [Fact]
        public void Export_OffersAutoCadsTypes_AndABitmapChosenGoesOnAsBmpout()
        {
            CadDocument document = TwoLines();
            var written = new List<object>();
            var asked = new List<CadExportJob>();
            string file = Path.Combine(_folder, "plan.bmp");
            CadCommandSession session = Stubbed(document, j => file, CadExportJob.Bitmap, written, asked);

            Run(session, "EXPORT");

            CadExportJob job = Assert.Single(asked);
            Assert.Equal("Export Data", job.Title);
            Assert.Equal(
                new[]
                {
                    "3D DWF (*.dwf)",
                    "3D DWFx (*.dwfx)",
                    "ACIS (*.sat)",
                    "Bitmap (*.bmp)",
                    "Block (*.dwg)",
                    "DXX Extract (*.dxx)",
                    "Encapsulated PS (*.eps)",
                    "IGES (*.iges;*.igs)",
                    "Lithography (*.stl)",
                    "Metafile (*.wmf)",
                    "V7 DGN (*.dgn)",
                    "V8 DGN (*.dgn)",
                },
                job.FileTypes
            );
            Assert.Equal(new[] { ".iges", ".igs" }, CadExportJob.ExtensionsOf(job.FileTypes[7]));
            Assert.Equal("Select objects or <all objects and viewports>:", session.PromptText);
            Run(session, Enter);

            CadImageJob image = Assert.IsType<CadImageJob>(Assert.Single(written));
            Assert.Equal(CadImageFormat.Bmp, image.Format);
            Assert.Equal(file, image.FileName);
            Assert.False(session.IsRunning);
            Assert.False(document.IsModified);
        }

        [Fact]
        public void Export_RefusesATypeWithNoWriter_WritingNothing_AndKeepsTheTypeForTheNextExport()
        {
            CadDocument document = TwoLines();
            var written = new List<object>();
            var asked = new List<CadExportJob>();
            CadCommandSession session = Stubbed(
                document,
                j => Path.Combine(_folder, "plan.wmf"),
                "Metafile (*.wmf)",
                written,
                asked
            );

            Run(session, "EXPORT");

            Assert.False(session.IsRunning);
            Assert.Empty(written);
            Assert.Contains(session.History, l =>
                l.StartsWith(
                    "Metafile (*.wmf) has no writer in the presenter: nothing was exported.",
                    StringComparison.Ordinal
                )
            );

            // Bitmap is offered first; the next EXPORT offers the type last chosen.
            Assert.Equal(CadExportJob.Bitmap, asked[0].FileType);
            Run(session, "EXPORT");
            Assert.Equal("Metafile (*.wmf)", asked[1].FileType);
            Assert.EndsWith("two-lines.wmf", asked[1].FileName, StringComparison.Ordinal);
        }

        [Fact]
        public void ExportWithFiledia0_AsksTheFile_ItsExtensionChoosingTheType()
        {
            CadDocument document = TwoLines();
            var written = new List<object>();
            var asked = new List<CadExportJob>();
            CadCommandSession session = Stubbed(document, j => null, null, written, asked);
            Run(session, "FILEDIA");
            Run(session, "0");

            Run(session, "EXPORT");
            Assert.Equal("Enter file name <" + Path.Combine(_folder, "two-lines.bmp") + ">:", session.PromptText);
            Run(session, "plan.dgn");
            Assert.False(session.IsRunning);
            Assert.Contains(session.History, l =>
                l.StartsWith("V7 DGN (*.dgn) has no writer", StringComparison.Ordinal)
            );

            // A name of no listed extension has the type's added.
            Run(session, "EXPORT");
            Assert.Equal("Enter file name <" + Path.Combine(_folder, "two-lines.dgn") + ">:", session.PromptText);
            Run(session, Enter);
            Assert.Empty(asked);
            Assert.Empty(written);
        }

        [Fact]
        public void DashExport_AsksAutoCadsPromptsWordForWord_AndWritesItsPdfThroughPlot()
        {
            CadDocument document = TwoLines();
            var written = new List<object>();
            var asked = new List<CadExportJob>();
            string file = Path.Combine(_folder, "plan.pdf");
            CadCommandSession session = Stubbed(document, j => file, null, written, asked);

            Run(session, "-EXPORT");
            Assert.Equal("Enter file format [Dwf/dwfX/Pdf] <dwfX>", session.PromptText);
            Run(session, "P");
            Assert.Equal("Enter plot area [Display/Extents/Window] <Display>:", session.PromptText);
            Run(session, "E");
            Assert.Equal("Detailed plot configuration? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter);

            CadExportJob job = Assert.Single(asked);
            Assert.Equal("Save As PDF", job.Title);
            Assert.Equal(new[] { CadExportJob.Pdf }, job.FileTypes);
            CadPlotJob plot = Assert.IsType<CadPlotJob>(Assert.Single(written));
            Assert.Equal(file, plot.FileName);
            Assert.Equal(CadPlotArea.Extents, plot.Settings.Area);
            Assert.Equal(CadPlotSettings.PdfDevice, plot.Settings.DeviceName);
            Assert.Equal("Plot written to " + file + ".", session.History.Last());

            // The format answered is the next default, as AutoCAD keeps EXPORTEPLOTFORMAT.
            Run(session, "-EXPORT");
            Assert.Equal("Enter file format [Dwf/dwfX/Pdf] <Pdf>", session.PromptText);
        }

        [Fact]
        public void DashExport_Detailed_AsksThePaperUnitsOrientationScaleAndStyles_ThenWithFiledia0TheFile()
        {
            CadDocument document = TwoLines();
            var written = new List<object>();
            var asked = new List<CadExportJob>();
            CadCommandSession session = Stubbed(document, j => null, null, written, asked);
            Run(session, "FILEDIA");
            Run(session, "0");

            Run(session, "-EXPORT");
            Run(session, "P");
            Run(session, "W");
            Assert.Equal("Specify first corner:", session.PromptText);
            Run(session, "0,0");
            Assert.Equal("Specify opposite corner:", session.PromptText);
            Run(session, "200,100");
            Run(session, "Y");
            Assert.StartsWith("Enter paper size or [?] <", session.PromptText, StringComparison.Ordinal);
            Run(session, Enter);
            Assert.Equal("Enter paper units [Inches/Millimeters] <Millimeters>:", session.PromptText);
            Run(session, Enter);
            Assert.StartsWith(
                "Enter drawing orientation [Portrait/Landscape]",
                session.PromptText,
                StringComparison.Ordinal
            );
            Run(session, "L");
            Assert.StartsWith(
                "Enter plot scale (Plotted Millimeters=Drawing Units) or [Fit] <",
                session.PromptText,
                StringComparison.Ordinal
            );
            Run(session, "F");
            Assert.StartsWith("Plot with plot styles? [Yes/No]", session.PromptText, StringComparison.Ordinal);
            Run(session, Enter);
            Assert.StartsWith(
                "Enter plot style table name (enter . for none) or [?]",
                session.PromptText,
                StringComparison.Ordinal
            );
            Run(session, Enter);
            Assert.StartsWith("Plot with lineweights? [Yes/No]", session.PromptText, StringComparison.Ordinal);
            Run(session, Enter);
            Assert.StartsWith("Enter file name <", session.PromptText, StringComparison.Ordinal);
            Run(session, "boxed");

            Assert.Empty(asked);
            CadPlotJob plot = Assert.IsType<CadPlotJob>(Assert.Single(written));
            Assert.Equal(Path.Combine(_folder, "boxed.pdf"), plot.FileName);
            Assert.Equal(CadPlotArea.Window, plot.Settings.Area);
            Assert.Equal(new DisplayBox(0, 0, 200, 100), plot.Settings.Window);
            Assert.True(plot.Settings.Landscape);
            Assert.True(plot.Settings.FitToPaper);
        }

        [Fact]
        public void DashExport_RefusesDwfAndDwfx_AndAllLayouts()
        {
            CadDocument document = TwoLines();
            var written = new List<object>();
            var asked = new List<CadExportJob>();
            CadCommandSession session = Stubbed(document, j => null, null, written, asked);

            Run(session, "-EXPORT");
            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(
                "DWFx files have no writer in the presenter: nothing was exported. Answer Pdf for a PDF.",
                session.History.Last()
            );

            Run(session, "-EXPORT");
            Run(session, "D");
            Assert.StartsWith("DWF files have no writer", session.History.Last(), StringComparison.Ordinal);

            // On a layout of a drawing that has one.
            document = CadDocument.From(CadTemplate.CreateMetric());
            session = Stubbed(document, j => null, null, written, asked);
            document.CurrentSpace = document.Spaces.First(s => !s.IsModel);
            Run(session, "-EXPORT");
            Run(session, "P");
            Assert.Equal("Enter plot area [Current layout/All layouts] <Current Layout>:", session.PromptText);
            Run(session, "A");
            Assert.False(session.IsRunning);
            Assert.StartsWith("A PDF of all layouts has no writer", session.History.Last(), StringComparison.Ordinal);
            Assert.Empty(written);
            Assert.Empty(asked);
        }
    }
}
