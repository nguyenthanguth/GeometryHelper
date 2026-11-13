using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Batch 5 of acad.pgp, output: EXPORTPDF and its aliases, EXPORTDWF and EXPORTDWFX, IMPORT, PLOTSTAMP with -PLOTSTAMP as
    /// AutoCAD 2021's console asks it, and the PDFSHX variable; each handed to the window through the requests the
    /// window answers, here stubbed. The PDF itself is written by the window and is checked in the Wpf tests.
    /// </summary>
    public class OutputBatchTests : IDisposable
    {
        private const string StampOptions =
            "Enter an option [On/OFF/Fields/User Fields/Log file/LOCation/Text Properties/UNits]:";

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "output-" + Guid.NewGuid().ToString("N")
        );

        public OutputBatchTests()
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

        /// <summary>A session whose window answers EXPORTPDF's and IMPORT's file dialogs and keeps the plots it is handed.</summary>
        private static CadCommandSession Stubbed(
            CadDocument document,
            Func<CadExportJob, string> file,
            List<CadPlotJob> written,
            List<CadExportJob> asked
        )
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                switch (e.Name)
                {
                    case "EXPORTPDF":
                    case "IMPORT":
                        asked.Add(
                            new CadExportJob(e.Export.Title, e.Export.FileTypes, e.Export.FileType, e.Export.FileName)
                            {
                                Opens = e.Export.Opens,
                            }
                        );
                        e.Export.FileName = file(e.Export);
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
        public void TheAliasesOfAcadPgp_ReachTheOutputCommandsAndPdfshx()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            foreach (
                string[] pair in new[]
                {
                    new[] { "EPDF", "EXPORTPDF" },
                    new[] { "EDWF", "EXPORTDWF" },
                    new[] { "EDWFX", "EXPORTDWFX" },
                    new[] { "IMP", "IMPORT" },
                    new[] { "DDPLOTSTAMP", "PLOTSTAMP" },
                    new[] { "EPDFSHX", "PDFSHX" },
                }
            )
            {
                Assert.Equal(pair[1], CadShortcuts.Default.Aliases[pair[0]]);
            }

            foreach (
                string name in new[] { "EXPORTPDF", "EXPORTDWF", "EXPORTDWFX", "IMPORT", "PLOTSTAMP", "-PLOTSTAMP" }
            )
            {
                Assert.NotNull(registry.Find(name));
            }

            Assert.NotNull(registry.FindVariable("PDFSHX"));
        }

        [Fact]
        public void Pdfshx_IsZeroToStart_AndSetByTheVariableAndItsAlias()
        {
            CadCommandSession session = Session(TwoLines());

            Run(session, "PDFSHX");
            Assert.Equal("Enter new value for PDFSHX <0>:", session.PromptText);
            Run(session, "1");
            Run(session, "PDFSHX");
            Assert.Equal("Enter new value for PDFSHX <1>:", session.PromptText);
            Run(session, Esc);

            // Its alias sets it again, and a value other than 0 or 1 is refused.
            Run(session, "EPDFSHX", "0");
            Run(session, "PDFSHX");
            Assert.Equal("Enter new value for PDFSHX <0>:", session.PromptText);
            Run(session, Esc);
            Run(session, "PDFSHX", "2");
            Assert.False(session.Remembered.TryGetValue("pdfshx", out double kept) && kept == 2.0);
        }

        [Fact]
        public void PlotstampFields_AreAskedInAutoCadsWords_EachDefaultingToWhatTheStampHas()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "-PLOTSTAMP");
            Assert.Equal(StampOptions, session.PromptText);
            Run(session, "Fields");
            Assert.Equal("Stamp drawing name? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Stamp layout name? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            Assert.Equal("Stamp date and time? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Stamp login name? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Stamp plot device name? [Yes/No] <Yes>:", session.PromptText);
            Run(session, "N");
            Assert.Equal("Stamp paper size? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Stamp plot scale? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");

            // The main question comes back after an option, and Enter leaves.
            Assert.True(session.IsRunning);
            Assert.Equal(StampOptions, session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);

            CadPlotStamp stamp = session.PlotStamp;
            Assert.True(stamp.DrawingName);
            Assert.True(stamp.LayoutName);
            Assert.True(stamp.DateAndTime);
            Assert.False(stamp.LoginName);
            Assert.False(stamp.DeviceName);
            Assert.False(stamp.PaperSize);
            Assert.True(stamp.PlotScale);
            Assert.False(stamp.On);
        }

        [Fact]
        public void PlotstampUserFieldsAndLog_AskTheirTexts_AFullStopClearingAField()
        {
            CadCommandSession session = Session(TwoLines());

            Run(session, "-PLOTSTAMP", "User");
            Assert.Equal("Enter user field 1:", session.PromptText);
            Run(session, "Checked by AB");
            Assert.Equal("Enter user field 2:", session.PromptText);
            Run(session, "Rev C");
            Run(session, "User");
            Assert.Equal("Enter user field 1 <Checked by AB>:", session.PromptText);
            Run(session, Enter);
            Run(session, ".");
            Assert.Equal("Checked by AB", session.PlotStamp.UserField1);
            Assert.Equal(string.Empty, session.PlotStamp.UserField2);

            Run(session, "Log");
            Assert.Equal("Write plot stamp to log file? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter log file path <plot.log>:", session.PromptText);
            Run(session, "stamps.log");
            Assert.Equal("stamps.log", session.PlotStamp.LogFile);
            Run(session, "Log");
            Run(session, "N");

            // No log, no path asked: the main question comes straight back.
            Assert.Equal(StampOptions, session.PromptText);
            Assert.False(session.PlotStamp.WriteLog);
        }

        [Fact]
        public void PlotstampLocationTextAndUnits_AreAskedInAutoCadsWords()
        {
            CadCommandSession session = Session(TwoLines());

            Run(session, "-PLOTSTAMP", "LOC");
            Assert.Equal("Enter location [TL/TR/BL/BR] <BL>:", session.PromptText);
            Run(session, "TL");
            Assert.Equal("Enter stamp orientation [Horizontal/Vertical] <Horizontal>:", session.PromptText);
            Run(session, "V");
            Assert.Equal("Stamp upside-down? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            Assert.Equal("Specify plot stamp offset <0.1000,0.1000>:", session.PromptText);
            Run(session, "5,6");
            Assert.Equal(
                "Specify offset relative to [paper Border/printable Area] <printable Area>:",
                session.PromptText
            );
            Run(session, "B");

            CadPlotStamp stamp = session.PlotStamp;
            Assert.Equal(CadPlotStampCorner.TopLeft, stamp.Location);
            Assert.True(stamp.Vertical);
            Assert.True(stamp.UpsideDown);
            Assert.Equal(5.0, stamp.OffsetX);
            Assert.Equal(6.0, stamp.OffsetY);
            Assert.True(stamp.OffsetFromPaperBorder);

            Run(session, "Text");
            Assert.Equal("Enter font name <Segoe UI>:", session.PromptText);
            Run(session, "Arial");
            Assert.Equal("Enter text height <0.2000>:", session.PromptText);
            Run(session, "0.15");
            Assert.Equal("Place plot stamp on single line? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            Assert.Equal("Arial", stamp.FontName);
            Assert.Equal(0.15, stamp.TextHeight);
            Assert.True(stamp.SingleLine);

            Run(session, "UN");
            Assert.Equal("Enter measurement units [Inches/Millimeters/Pixels] <Inches>:", session.PromptText);
            Run(session, "M");
            Assert.Equal(CadPlotStampUnits.Millimeters, stamp.Units);
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void PlotstampOnAndOff_SwitchTheStamp_AndAnOffsetNotAPairIsAskedAgain()
        {
            CadCommandSession session = Session(TwoLines());

            Run(session, "-PLOTSTAMP", "On", Enter);
            Assert.True(session.PlotStamp.On);
            Run(session, "PLOTSTAMP", "OFF", Enter);
            Assert.False(session.PlotStamp.On);

            Run(session, "DDPLOTSTAMP", "LOC", Enter, Enter, Enter, "wide");
            Assert.Equal("Requires an offset as x,y.", session.History.Last());
            Assert.StartsWith("Specify plot stamp offset <", session.PromptText, StringComparison.Ordinal);
            Run(session, Enter, Enter);
            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(0.1, session.PlotStamp.OffsetX);
        }

        [Fact]
        public void Plotstamp_TheDialogEditsACopy_WhichTheSessionsStampTakesOnOkOnly()
        {
            CadCommandSession session = Session(TwoLines());
            CadPlotStamp shared = session.PlotStamp;
            bool accept = false;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "PLOTSTAMP")
                {
                    var request = (CadPlotStampRequest)e.Payload;
                    Assert.NotSame(shared, request.Stamp);
                    request.Stamp.LayoutName = true;
                    request.Stamp.UserField1 = "Level 2";
                    request.Stamp.Location = CadPlotStampCorner.TopRight;
                    request.Stamp.Units = CadPlotStampUnits.Millimeters;
                    request.IsAccepted = accept;
                    e.Handled = true;
                }
            };

            Run(session, "PLOTSTAMP");
            Assert.False(session.IsRunning);
            Assert.False(shared.LayoutName);

            accept = true;
            Run(session, "DDPLOTSTAMP");

            Assert.Same(shared, session.PlotStamp);
            Assert.True(shared.LayoutName);
            Assert.Equal("Level 2", shared.UserField1);
            Assert.Equal(CadPlotStampCorner.TopRight, shared.Location);
            Assert.Equal(CadPlotStampUnits.Millimeters, shared.Units);
        }

        [Fact]
        public void APlotWhileTheStampIsOn_CarriesACopyOfIt_AndPdfshxRidesAlong()
        {
            CadDocument document = TwoLines();
            var written = new List<CadPlotJob>();
            var asked = new List<CadExportJob>();
            string file = Path.Combine(_folder, "plan.pdf");
            CadCommandSession session = Stubbed(document, j => file, written, asked);

            Run(session, "EXPORTPDF");
            Assert.Null(Assert.Single(written).Settings.PlotStamp);
            Assert.False(written[0].Settings.PdfShx);

            Run(session, "-PLOTSTAMP", "On", Enter);
            Run(session, "PDFSHX", "1");
            Run(session, "EXPORTPDF");

            CadPlotStamp carried = written[1].Settings.PlotStamp;
            Assert.NotNull(carried);
            Assert.NotSame(session.PlotStamp, carried);
            Assert.True(carried.On);
            Assert.True(written[1].Settings.PdfShx);

            // The settings copy a plot makes of itself keep the stamp apart.
            CadPlotSettings copy = written[1].Settings.Clone();
            Assert.NotSame(carried, copy.PlotStamp);
            copy.PlotStamp.UserField1 = "changed";
            Assert.Equal(string.Empty, carried.UserField1);

            // -PLOT's detailed prompts hand it on too.
            Run(session, "-PLOT", "N", Enter, Enter, Enter, Enter, Enter, Enter);
            Assert.Equal(3, written.Count);
            Assert.NotNull(written[2].Settings.PlotStamp);
            Assert.True(written[2].Settings.PdfShx);
        }

        [Fact]
        public void ExportPdf_AsksTheWindowForTheFile_ThenWritesTheDisplayOfModelSpaceAsAPdf()
        {
            CadDocument document = TwoLines();
            var written = new List<CadPlotJob>();
            var asked = new List<CadExportJob>();
            string file = Path.Combine(_folder, "plan.pdf");
            CadCommandSession session = Stubbed(document, j => file, written, asked);

            Run(session, "EXPORTPDF");

            CadExportJob job = Assert.Single(asked);
            Assert.Equal("Save As PDF", job.Title);
            Assert.Equal(new[] { CadExportJob.Pdf }, job.FileTypes);
            Assert.False(job.Opens);
            Assert.Equal(Path.Combine(_folder, "two-lines-Model.pdf"), job.FileName);
            CadPlotJob plot = Assert.Single(written);
            Assert.Equal(file, plot.FileName);
            Assert.Equal(CadPlotArea.Display, plot.Settings.Area);
            Assert.Equal(CadPlotSettings.PdfDevice, plot.Settings.DeviceName);
            Assert.False(session.IsRunning);
            Assert.Equal("Plot written to " + file + ".", session.History.Last());
        }

        [Fact]
        public void ExportPdf_OnALayoutPlotsTheLayout_AndACancelledDialogWritesNothing()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric());
            document.CurrentSpace = document.Spaces.First(s => !s.IsModel);
            var written = new List<CadPlotJob>();
            var asked = new List<CadExportJob>();
            string file = Path.Combine(_folder, "sheet.pdf");
            CadCommandSession session = Stubbed(document, j => file, written, asked);

            Run(session, "EXPORTPDF");
            Assert.Equal(CadPlotArea.Layout, Assert.Single(written).Settings.Area);

            written.Clear();
            session = Stubbed(document, j => null, written, asked);
            Run(session, "EXPORTPDF");
            Assert.Empty(written);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ExportPdfWithFiledia0_AsksDashExportsQuestions_AndTheFileOnTheCommandLine()
        {
            CadDocument document = TwoLines();
            var written = new List<CadPlotJob>();
            var asked = new List<CadExportJob>();
            CadCommandSession session = Stubbed(document, j => null, written, asked);
            Run(session, "FILEDIA", "0");

            Run(session, "EPDF");
            Assert.Equal("Enter plot area [Display/Extents/Window] <Display>:", session.PromptText);
            Run(session, "E");
            Assert.Equal("Detailed plot configuration? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter);
            Assert.StartsWith("Enter file name <", session.PromptText, StringComparison.Ordinal);
            Run(session, "boxed");

            Assert.Empty(asked);
            CadPlotJob plot = Assert.Single(written);
            Assert.Equal(Path.Combine(_folder, "boxed.pdf"), plot.FileName);
            Assert.Equal(CadPlotArea.Extents, plot.Settings.Area);
        }

        [Fact]
        public void ExportPdfWithNoWindow_AsksTheSameQuestions_AndSaysItNeedsTheWindowsWriter()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "EXPORTPDF", Enter, Enter, "boxed");

            Assert.False(session.IsRunning);
            Assert.Equal("Exporting a PDF needs the window's PDF writer: nothing was written.", session.History.Last());
        }

        [Fact]
        public void ExportDwfAndDwfx_SayThePresenterHasNoWriter_AndWriteNothing()
        {
            CadCommandSession session = Session(TwoLines());

            Run(session, "EDWF");
            Assert.False(session.IsRunning);
            Assert.Equal(
                "DWF files have no writer in the presenter: nothing was exported. EXPORTPDF writes a PDF.",
                session.History.Last()
            );
            Run(session, "EXPORTDWFX");
            Assert.Equal(
                "DWFx files have no writer in the presenter: nothing was exported. EXPORTPDF writes a PDF.",
                session.History.Last()
            );
        }

        private string DrawingFile(string name)
        {
            var source = new DxfDatabase("AC1027");
            source.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)));
            string file = Path.Combine(_folder, name);
            source.Save(file);
            return file;
        }

        [Fact]
        public void Import_AsksTheWindowForADrawing_BringsItInAsABlockOfItsName_AndPlacesItAsInsertDoes()
        {
            CadDocument document = TwoLines();
            string file = DrawingFile("bolt.dxf");
            var written = new List<CadPlotJob>();
            var asked = new List<CadExportJob>();
            CadCommandSession session = Stubbed(document, j => file, written, asked);

            Run(session, "IMP");
            CadExportJob job = Assert.Single(asked);
            Assert.Equal("Import File", job.Title);
            Assert.Equal(new[] { CadExportJob.Drawing }, job.FileTypes);
            Assert.True(job.Opens);
            Assert.Equal("Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate]:", session.PromptText);
            Run(session, P(100, 50), Enter, Enter, Enter);

            Assert.False(session.IsRunning);
            Assert.True(document.Database.BlockTable.Has("bolt"));
            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal("bolt", insert.Name);
            Near(new GeoPoint3(100, 50, 0), insert.Position);
            Assert.Single(document.Database.BlockTable["bolt"].OfType<DxfLine>());
        }

        [Fact]
        public void Import_ACancelledDialogDoesNothing()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Stubbed(document, j => null, new List<CadPlotJob>(), new List<CadExportJob>());

            Run(session, "IMPORT");

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            Assert.False(document.Database.BlockTable.Has("bolt"));
        }

        [Fact]
        public void ImportWithFiledia0_AsksForTheFileOnTheCommandLine_AsAutoCadsConsoleDoes()
        {
            CadDocument document = TwoLines();
            string file = DrawingFile("bolt.dxf");
            var asked = new List<CadExportJob>();
            CadCommandSession session = Stubbed(document, j => null, new List<CadPlotJob>(), asked);
            Run(session, "FILEDIA", "0");

            Run(session, "IMPORT");
            Assert.Equal("Enter import file name:", session.PromptText);
            Run(session, file);
            Assert.Equal("Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate]:", session.PromptText);
            Run(session, P(0, 0), Enter, Enter, Enter);

            Assert.Empty(asked);
            Assert.True(document.Database.BlockTable.Has("bolt"));
            Assert.Single(All<DxfBlockReference>(document.ModelSpace));
        }

        [Fact]
        public void Import_AFileThatIsNotThere_IsCantFindFile_AndAWmfHasNoReader()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "IMPORT", Path.Combine(_folder, "missing.wmf"));
            Assert.False(session.IsRunning);
            Assert.Equal("Can't find file.", session.History.Last());

            string wmf = Path.Combine(_folder, "logo.wmf");
            File.WriteAllBytes(wmf, new byte[] { 1, 2, 3, 4 });
            Run(session, "IMPORT", wmf);
            Assert.False(session.IsRunning);
            Assert.Equal(
                ".WMF files cannot be imported by the presenter, which reads a DWG or a DXF drawing: nothing was imported.",
                session.History.Last()
            );

            // Enter at the name leaves.
            Run(session, "IMPORT", Enter);
            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
        }

        private static CadPlotPage PageAtOneToTwenty(CadDocument document, out CadPlotSettings settings)
        {
            settings = new CadPlotSettings
            {
                Area = CadPlotArea.Window,
                Window = new DisplayBox(0, 0, 8000, 5000),
                FitToPaper = false,
                PaperUnits = 1,
                DrawingUnits = 20,
            };
            return CadPlotPage.Compute(document, document.ModelSpace, settings, DisplayBox.Empty);
        }

        [Fact]
        public void TheStampsLines_FollowAutoCadsOrder_OneLineEachOrOneLineInAll()
        {
            CadDocument document = TwoLines();
            CadPlotPage page = PageAtOneToTwenty(document, out CadPlotSettings settings);
            var when = new DateTime(2026, 6, 9, 14, 5, 9);
            var stamp = new CadPlotStamp
            {
                LayoutName = true,
                LoginName = true,
                PaperSize = true,
                PlotScale = true,
                UserField1 = "Checked by AB",
                UserField2 = "Rev C",
            };

            IReadOnlyList<string> lines = stamp.Lines(document, document.ModelSpace, settings, page, when, "ab");

            Assert.Equal(
                new[]
                {
                    document.FileName,
                    "Model",
                    "2026-06-09 14:05:09",
                    "ab",
                    CadPlotSettings.PdfDevice,
                    settings.Paper.Label,
                    "1:20",
                    "Checked by AB",
                    "Rev C",
                },
                lines
            );

            stamp.SingleLine = true;
            Assert.Equal(
                string.Join("  ", lines),
                Assert.Single(stamp.Lines(document, document.ModelSpace, settings, page, when, "ab"))
            );

            // With AutoCAD's start-up fields: the drawing, the date and the plotter.
            Assert.Equal(
                new[] { document.FileName, "2026-06-09 14:05:09", CadPlotSettings.PdfDevice },
                new CadPlotStamp().Lines(document, document.ModelSpace, settings, page, when, "ab")
            );
            Assert.Empty(
                new CadPlotStamp
                {
                    DrawingName = false,
                    DateAndTime = false,
                    DeviceName = false,
                }.Lines(document, document.ModelSpace, settings, page, when, "ab")
            );
        }

        [Fact]
        public void AnUnsavedDrawingIsStampedByItsTitle()
        {
            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), "Untitled.dxf");
            document.ModelSpace.Block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            CadPlotPage page = PageAtOneToTwenty(document, out CadPlotSettings settings);

            Assert.Equal(
                document.Title,
                new CadPlotStamp().Lines(document, document.ModelSpace, settings, page, DateTime.Now, "ab")[0]
            );
        }

        [Fact]
        public void ScalesAreWrittenAsRatios_AndLengthsInTheUnitsOfTheStamp()
        {
            Assert.Equal("1:20", CadPlotStamp.ScaleText(0.05));
            Assert.Equal("2:1", CadPlotStamp.ScaleText(2.0));
            Assert.Equal("1:1", CadPlotStamp.ScaleText(1.0));
            Assert.Equal("1:18.4669", CadPlotStamp.ScaleText(1.0 / 18.4669));

            var stamp = new CadPlotStamp();
            Assert.Equal(5.08, stamp.ToMillimetres(0.2), 9);
            stamp.Units = CadPlotStampUnits.Millimeters;
            Assert.Equal(5.0, stamp.ToMillimetres(5.0));
            stamp.Units = CadPlotStampUnits.Pixels;
            Assert.Equal(25.4 / 96.0 * 10.0, stamp.ToMillimetres(10.0), 9);
        }

        [Fact]
        public void TheStampStandsInItsCorner_InByTheOffset_ThePrintableAreaOrTheSheet()
        {
            CadDocument document = TwoLines();
            CadPlotPage page = PageAtOneToTwenty(document, out _);

            // ISO A3 on its side: 420 by 297, printable from (5, 5) to (415, 292). A block 40 by 15 mm, 0.1 inch (2.54 mm) in.
            var stamp = new CadPlotStamp();
            DisplayBox box = stamp.Footprint(page, 40.0, 15.0);
            Assert.Equal(new[] { 7.54, 7.54, 47.54, 22.54 }, new[] { box.MinX, box.MinY, box.MaxX, box.MaxY });

            stamp.Location = CadPlotStampCorner.TopRight;
            box = stamp.Footprint(page, 40.0, 15.0);
            Assert.Equal(new[] { 372.46, 274.46, 412.46, 289.46 }, new[] { box.MinX, box.MinY, box.MaxX, box.MaxY });

            // A stamp that reads up the sheet has its width and height changed over.
            stamp.Vertical = true;
            box = stamp.Footprint(page, 40.0, 15.0);
            Assert.Equal(new[] { 15.0, 40.0 }, new[] { box.Width, box.Height });

            // From the edge of the sheet: 420 - 2.54 - 15 along X, 297 - 2.54 - 40 along Y.
            stamp.OffsetFromPaperBorder = true;
            box = stamp.Footprint(page, 40.0, 15.0);
            Assert.Equal(402.46, box.MinX, 9);
            Assert.Equal(254.46, box.MinY, 9);
        }

        [Fact]
        public void TheLogIsBesideThePlotUnlessItsPathIsFull_AndTakesTheStampsFieldsOnALine()
        {
            var stamp = new CadPlotStamp();
            string plot = Path.Combine(_folder, "plan.pdf");

            Assert.Equal(Path.Combine(_folder, "plot.log"), stamp.LogPath(plot));
            stamp.LogFile = Path.Combine(_folder, "logs", "all.log");
            Assert.Equal(Path.Combine(_folder, "logs", "all.log"), stamp.LogPath(plot));
            Assert.Equal("a  b", CadPlotStamp.LogLine(new[] { "a", "b" }));
        }
    }
}
