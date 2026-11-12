using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// SAVEAS and DXFOUT typed on the command line (FILEDIA 0), in the words of AutoCAD 2021's resources: "Current file format",
    /// "Enter file format [R14(LT98&amp;LT97)/2000(LT2000)/.../Dxf/Template/Standards] &lt;2018&gt;:", for Dxf "Current DXF
    /// settings" and "Enter decimal places of accuracy (0 to 16) or [Version]", "Enter version [R12(LT2)/2000(LT2000)/.../2018(LT2018)]",
    /// and the file; DXFOUT's copy that leaves the drawing as it was.
    /// </summary>
    public class SaveAsPromptTests : IDisposable
    {
        private const string FormatPrompt =
            "Enter file format [R14(LT98&LT97)/2000(LT2000)/2004(LT2004)/2007(LT2007)/2010(LT2010)/2013(LT2013)/2018(LT2018)/Dxf/Template/Standards] <2018>:";

        private const string VersionList =
            "R12(LT2)/2000(LT2000)/2004(LT2004)/2007(LT2007)/2010(LT2010)/2013(LT2013)/2018(LT2018)";

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "saveasprompt-" + Guid.NewGuid().ToString("N")
        );

        public SaveAsPromptTests()
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
                // A file a watcher still holds is left for the system to clear.
            }
        }

        /// <summary>A new metric drawing of a line and a circle, named Drawing1.dwg as AutoCAD names a new one, FILEDIA 0.</summary>
        private static CadCommandSession NewDwg(out CadDocument document)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0));
            document = CadDocument.From(database, "Drawing1.dwg");
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");
            Assert.False(session.FileDialogs);
            return session;
        }

        private static string Magic(string path) => Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 6);

        [Fact]
        public void SaveAsOfANewDwgWithFiledia0_SaysTheCurrentFormat_AndAsksTheFormatInAutoCadsWords()
        {
            CadCommandSession session = NewDwg(out _);

            Run(session, "SAVEAS");

            Assert.Contains("Current file format: AutoCAD 2018 Drawing", session.History);
            Assert.Equal(FormatPrompt, session.PromptText);
        }

        [Fact]
        public void TheFormat2018_ThenAName_SavesADwgOfAutoCad2018_AsAskedAtSaveDrawingAs()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            string path = Path.Combine(_folder, "plan");

            Run(session, "SAVEAS", Enter);

            Assert.Equal("Save drawing as <Drawing1.dwg>:", session.PromptText);
            Run(session, path);

            Assert.False(session.IsRunning);
            Assert.Equal(path + ".dwg", document.FileName);
            Assert.Equal("AC1032", Magic(path + ".dwg"));
            Assert.Contains("Saved " + path + ".dwg.", session.History);
        }

        [Fact]
        public void TheFormatTypedAs2018OrItsLightName_IsTheDwgToo()
        {
            foreach (string word in new[] { "2018", "lt2018", "2018(LT2018)" })
            {
                CadCommandSession session = NewDwg(out CadDocument document);
                string path = Path.Combine(_folder, "plan" + word.Length);

                Run(session, "SAVEAS", word, path);

                Assert.Equal("AC1032", Magic(path + ".dwg"));
                Assert.Equal(path + ".dwg", document.FileName);
            }
        }

        [Theory]
        [InlineData("R14", "AutoCAD R14(LT98/LT97) Drawing")]
        [InlineData("2000", "AutoCAD 2000(LT2000) Drawing")]
        [InlineData("2013(LT2013)", "AutoCAD 2013(LT2013) Drawing")]
        public void AFormatOfTheDwgBefore2018_IsRefused_AndNothingIsWritten(string typed, string name)
        {
            CadCommandSession session = NewDwg(out CadDocument document);

            Run(session, "SAVEAS", typed);

            Assert.False(session.IsRunning);
            Assert.Contains(name + " (*.dwg) is not written: a DWG is saved as AutoCAD 2018 (2018).", session.History);
            Assert.False(document.HasFile);
        }

        [Theory]
        [InlineData("Template", "A drawing template (*.dwt) is not written here.")]
        [InlineData("St", "A drawing standards file (*.dws) is not written here.")]
        public void TemplateAndStandards_AreNotWritten_AndSaidSo(string typed, string said)
        {
            CadCommandSession session = NewDwg(out CadDocument document);

            Run(session, "SAVEAS", typed);

            Assert.False(session.IsRunning);
            Assert.Contains(said, session.History);
            Assert.False(document.HasFile);
        }

        [Fact]
        public void AFormatThatIsNoKeyword_IsRefused_AndAsksAgain()
        {
            CadCommandSession session = NewDwg(out _);

            Run(session, "SAVEAS", "nonsense");

            Assert.True(session.IsRunning);
            Assert.Contains("Invalid option keyword.", session.History);
            Assert.Equal(FormatPrompt, session.PromptText);
        }

        [Fact]
        public void Dxf_AsksTheSettings_ThenTheVersion_ThenTheFile_AndSavesThatVersion()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            string path = Path.Combine(_folder, "plan");

            Run(session, "SAVEAS", "Dxf");

            Assert.Contains(
                "Current DXF settings: Precision= 16 Format= ASCII Preview= No Version= 2018",
                session.History
            );
            Assert.Equal("Enter decimal places of accuracy (0 to 16) or [Version] <16>:", session.PromptText);

            Run(session, "Version");

            Assert.Equal("Enter version [" + VersionList + "] <2018>:", session.PromptText);

            Run(session, "2010");

            Assert.Contains(
                "Current DXF settings: Precision= 16 Format= ASCII Preview= No Version= 2010",
                session.History
            );
            Assert.Equal("Enter decimal places of accuracy (0 to 16) or [Version] <16>:", session.PromptText);

            Run(session, Enter);

            Assert.Equal("Save drawing as <Drawing1.dxf>:", session.PromptText);
            Run(session, path);

            Assert.False(session.IsRunning);
            Assert.Equal(path + ".dxf", document.FileName);
            Assert.Equal("AC1024", document.SaveVersion);
            Assert.Equal("AC1024", DxfDatabase.Open(path + ".dxf").Version);
            Assert.Single(DxfDatabase.Open(path + ".dxf").ModelSpace.OfType<DxfCircle>());
        }

        [Fact]
        public void TheVersionOfADxf_TypedAsR14_IsRefused_AndAsksAgain()
        {
            CadCommandSession session = NewDwg(out _);

            Run(session, "SAVEAS", "D", "V", "R14");

            Assert.True(session.IsRunning);
            Assert.Contains("R14 is not written: a DXF is saved as R12 or R2000 and later.", session.History);
            Assert.Equal("Enter version [" + VersionList + "] <2018>:", session.PromptText);

            Run(session, "bogus");

            Assert.Contains("Invalid option keyword.", session.History);
        }

        [Fact]
        public void APlacesNumberAtTheAccuracyPrompt_IsSaid_AndTheDxfIsWrittenInFull()
        {
            CadCommandSession session = NewDwg(out _);

            Run(session, "SAVEAS", "Dxf", "8");

            Assert.Contains(
                "The coordinates are written in full: the places of accuracy are not set.",
                session.History
            );
            Assert.Equal("Save drawing as <Drawing1.dxf>:", session.PromptText);

            Run(session, Esc);
            Run(session, "SAVEAS", "Dxf", "20");

            Assert.True(session.IsRunning);
            Assert.Contains("Value must be between 0 and 16.", session.History);
        }

        [Fact]
        public void ADxfNamedForADwg_AndADwgNamedForADxf_WhenTheFormatWasTyped_AreRefused()
        {
            CadCommandSession session = NewDwg(out CadDocument document);

            Run(session, "SAVEAS", "Dxf", Enter, Path.Combine(_folder, "wrong.dwg"));

            Assert.False(session.IsRunning);
            Assert.Contains("A DXF is saved to a name ending in .dxf, not .dwg.", session.History);

            Run(session, "SAVEAS", "2018", Path.Combine(_folder, "wrong.dxf"));

            Assert.Contains("A DWG is saved to a name ending in .dwg, not .dxf.", session.History);
            Assert.False(document.HasFile);
            Assert.Empty(Directory.GetFiles(_folder));
        }

        [Fact]
        public void ANameOfTheOtherExtension_AfterTheOfferedFormatWasTaken_SaysWhatTheFormatIs()
        {
            // Enter at the format takes the offered one; a name ending in .dxf then saves a DXF of AutoCAD 2018, as the name alone
            // said before the format was asked, and one ending in .dwg a DWG of a drawing that is a DXF.
            CadCommandSession session = NewDwg(out CadDocument document);
            string dxf = Path.Combine(_folder, "named.dxf");

            Run(session, "SAVEAS", Enter, dxf);

            Assert.False(session.IsRunning);
            Assert.Equal(dxf, document.FileName);
            Assert.Equal("AC1032", DxfDatabase.Open(dxf).Version);

            CadDocument plan = CadDocument.From(CadTemplate.CreateMetric(), "Plan.dxf");
            CadCommandSession other = Session(plan);
            Run(other, "FILEDIA", "0");
            string dwg = Path.Combine(_folder, "named.dwg");

            Run(other, "SAVEAS", Enter, Enter, dwg);

            Assert.Equal(dwg, plan.FileName);
            Assert.Equal("AC1032", Magic(dwg));
        }

        [Fact]
        public void SaveAsOfADxfDrawing_OffersDxfAsItsFormat_AndItsOwnVersion()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            CadDocument document = CadDocument.From(database, "Plan.dxf");
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");

            Run(session, "SAVEAS");

            Assert.Equal(
                "Enter file format ["
                    + "R14(LT98&LT97)/2000(LT2000)/2004(LT2004)/2007(LT2007)/2010(LT2010)/2013(LT2013)/2018(LT2018)/Dxf/Template/Standards] <Dxf>:",
                session.PromptText
            );

            Run(session, Enter);

            // The metric template is AutoCAD 2013's, which is what the drawing is offered in.
            Assert.Equal("AC1027", database.Version);
            Assert.Contains(
                "Current DXF settings: Precision= 16 Format= ASCII Preview= No Version= 2013",
                session.History
            );
        }

        [Fact]
        public void QsaveOfANewDrawingWithFiledia0_AsksTheSameFormat_BeforeTheFile()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            string path = Path.Combine(_folder, "quick");

            Run(session, "QSAVE");

            Assert.Equal(FormatPrompt, session.PromptText);
            Run(session, Enter, path);

            Assert.Equal(path + ".dwg", document.FileName);
            Assert.Equal("AC1032", Magic(path + ".dwg"));
        }

        [Fact]
        public void SaveAsWithAWindow_ShowsItsDialog_WithFiledia1_AndTheTypedFileIsHandedToIt()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "Drawing1.dwg");
            CadCommandSession session = Session(document);
            string name = null;
            string type = null;
            bool dialog = false;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "SAVEAS" && e.Payload == null)
                {
                    dialog = true;
                    e.Handled = true;
                }
                else if (e.Name == "SAVEAS" && e.Payload is CadExportJob job)
                {
                    name = job.FileName;
                    type = job.FileType;
                    e.Handled = true;
                }
            };

            Run(session, "SAVEAS");

            Assert.True(dialog);
            Assert.False(session.IsRunning);

            dialog = false;
            Run(session, "FILEDIA", "0", "SAVEAS", "Dxf", "V", "2004", Enter, Path.Combine(_folder, "win"));

            Assert.False(dialog);
            Assert.Equal(Path.Combine(_folder, "win.dxf"), name);
            Assert.Equal("AutoCAD 2004/LT2004 DXF (*.dxf)", type);
            Assert.False(File.Exists(Path.Combine(_folder, "win.dxf")));
        }

        [Fact]
        public void DxfoutWithFiledia0_AsksTheFileThenTheSettings_AndWritesACopyThatLeavesTheDrawingAsItWas()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            document.ModelSpace.Block.AppendEntity(new DxfLine(new GeoPoint3(0, 5, 0), new GeoPoint3(10, 5, 0)));
            string path = Path.Combine(_folder, "copy");
            bool modifiedBefore = document.IsModified;

            Run(session, "DXFOUT");

            Assert.Equal("Enter DXF file name <Drawing1>:", session.PromptText);
            Run(session, path);

            Assert.Contains(
                "Current DXF settings: Precision= 16 Format= ASCII Preview= No Version= 2018",
                session.History
            );
            Assert.Equal("Enter decimal places of accuracy (0 to 16) or [Version] <16>:", session.PromptText);

            Run(session, "V", "2007", Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("AC1021", DxfDatabase.Open(path + ".dxf").Version);
            Assert.Equal(2, DxfDatabase.Open(path + ".dxf").ModelSpace.OfType<DxfLine>().Count());
            Assert.False(document.HasFile);
            Assert.Equal(modifiedBefore, document.IsModified);
            Assert.Null(document.SaveVersion);
        }

        [Fact]
        public void DxfoutOfADrawingWithAFile_LeavesItsFileAndVersionAlone()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            string own = Path.Combine(_folder, "own.dxf");
            document.SaveAs(own, "AC1018", null);
            string before = File.ReadAllText(own);
            string copy = Path.Combine(_folder, "copy.dxf");

            Run(session, "DXFOUT", copy, Enter);

            Assert.Equal("AC1018", DxfDatabase.Open(copy).Version);
            Assert.Equal(own, document.FileName);
            Assert.Equal("AC1018", document.SaveVersion);
            Assert.False(document.IsModified);
            Assert.Equal(before, File.ReadAllText(own));
        }

        [Fact]
        public void DxfoutWithADialog_AsksTheWindowForACreateDxfFileDialog_OfTheDxfTypesOnly()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "Drawing1.dwg");
            CadCommandSession session = Session(document);
            string path = Path.Combine(_folder, "dialog.dxf");
            CadExportJob seen = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "DXFOUT" && e.Payload is CadExportJob job)
                {
                    seen = job;
                    job.FileName = path;
                    job.FileType = CadExportJob.DxfSaveTypes.Single(t => t.Contains("2013"));
                    e.Handled = true;
                }
            };

            Run(session, "DXFOUT");

            Assert.False(session.IsRunning);
            Assert.Equal("Create DXF File", seen.Title);
            Assert.Equal(CadExportJob.DxfSaveTypes, seen.FileTypes);
            Assert.Equal("AC1027", DxfDatabase.Open(path).Version);
        }

        [Fact]
        public void TheDxfTypesOfSaveAs_AreSevenWithR12LastAndEachNamesItsVersion()
        {
            Assert.Equal(7, CadExportJob.DxfSaveTypes.Count);
            Assert.Equal("AutoCAD 2018 DXF (*.dxf)", CadExportJob.DxfSaveTypes[0]);
            Assert.Equal("AutoCAD R12/LT2 DXF (*.dxf)", CadExportJob.DxfSaveTypes[6]);
            Assert.Equal("AC1009", CadExportJob.DxfVersionOfType("AutoCAD R12/LT2 DXF (*.dxf)"));
            Assert.Equal("AutoCAD 2010/LT2010 DXF (*.dxf)", CadExportJob.DxfTypeOfVersion("ac1024"));
            Assert.Equal("DXF (*.dxf)", CadExportJob.DxfTypeOfVersion("AC1014"));
            Assert.Null(CadExportJob.DxfVersionOfType("DXF (*.dxf)"));
            Assert.Null(CadExportJob.DxfVersionOfType(CadExportJob.Dwg));
        }
    }
}
