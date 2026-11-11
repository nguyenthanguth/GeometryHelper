using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// DWG in the window, as AutoCAD 2021 takes it: a DWG opened, listed with the DWGs of its folder and saved back as a DWG;
    /// Save As offering AutoCAD 2018 Drawing (*.dwg) first, then the drawing's own DXF; FILEDIA 0 asking on the command line
    /// the file of QSAVE of a new drawing and of the Blocks palette's Browse…; a DWG that cannot be read said, not recovered.
    /// </summary>
    public class DwgFileWindowTests
    {
        /// <summary>A metric drawing of one line from (0, 0) to (1 000, 0), written as a DWG of AutoCAD 2018 in a folder.</summary>
        private static string LineDwg(TestFolder folder, string name)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
            string path = Path.Combine(folder.FullPath, name);
            DwgWriter.Write(database, path);
            return path;
        }

        private static string Magic(string path) => Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 6);

        [Fact]
        public void ADwgOpened_IsShownWithTheDrawingFilesOfItsFolder_AndSavedBackAsADwg()
        {
            using (var folder = new TestFolder())
            {
                string plan = LineDwg(folder, "plan.dwg");
                LineDwg(folder, "other.dwg");
                folder.Write("notes.dxf", CadTemplate.CreateMetric());
                var model = new MainViewModel();

                Assert.True(model.Open(plan));

                Assert.Equal("plan.dwg", model.Document.Title);
                Assert.Contains(model.History, l =>
                    l.StartsWith("Opened plan.dwg: 1 entities", StringComparison.Ordinal)
                );
                Assert.Contains(model.FileRows, r =>
                    r.Path != null && r.Path.EndsWith("other.dwg", StringComparison.Ordinal)
                );
                Assert.Contains(model.FileRows, r =>
                    r.Path != null && r.Path.EndsWith("notes.dxf", StringComparison.Ordinal)
                );

                model.Document.ModelSpace.Block.AppendEntity(
                    new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(1000, 100, 0))
                );
                Assert.True(model.Save());

                Assert.Contains("Saved " + plan + ".", model.History);
                Assert.Equal("AC1032", Magic(plan));
                Assert.Equal(2, DwgReader.Open(plan).ModelSpace.OfType<DxfLine>().Count());
            }
        }

        [Fact]
        public void SaveAs_OffersTheDwgOfAutoCad2018First_ThenTheDrawingsOwnDxf_TheTypeOfItsFileChosen()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.Null(model.CreateSaveAsJob());

                Assert.True(model.Open(LineDwg(folder, "plan.dwg")));
                CadExportJob job = model.CreateSaveAsJob();

                Assert.Equal("Save Drawing As", job.Title);
                Assert.Equal(new[] { "AutoCAD 2018 Drawing (*.dwg)", "AutoCAD 2018 DXF (*.dxf)" }, job.FileTypes);
                Assert.Equal(CadExportJob.Dwg, job.FileType);
                Assert.Equal(Path.Combine(folder.FullPath, "plan.dwg"), job.FileName);

                model.NewDrawing();
                job = model.CreateSaveAsJob();

                Assert.Equal(
                    new[] { "AutoCAD 2018 Drawing (*.dwg)", "AutoCAD 2013/LT2013 DXF (*.dxf)" },
                    job.FileTypes
                );
                Assert.Equal("AutoCAD 2013/LT2013 DXF (*.dxf)", job.FileType);
                Assert.Equal("Drawing1.dxf", job.FileName);
            }
        }

        [Fact]
        public void ADrawingSavedAsThroughTheDialog_AsADwg_IsWrittenSo_AndQsaveKeepsItADwg()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                string path = Path.Combine(folder.FullPath, "plan.dwg");
                model.SaveAsRequested += (s, e) =>
                {
                    CadExportJob job = model.CreateSaveAsJob();
                    job.FileName = path;
                    job.FileType = CadExportJob.Dwg;
                    model.SaveAs(job.FileName);
                };
                model.NewDrawing();
                model.Document.ModelSpace.Block.AppendEntity(
                    new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 50.0)
                );

                model.Execute("QSAVE");

                Assert.Equal(path, model.Document.FileName);
                Assert.Equal("AC1032", Magic(path));

                model.Document.ModelSpace.Block.AppendEntity(
                    new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 80.0)
                );
                model.Execute("QSAVE");

                Assert.Equal("AC1032", Magic(path));
                Assert.Equal(2, DwgReader.Open(path).ModelSpace.OfType<DxfCircle>().Count());
            }
        }

        [Fact]
        public void QsaveOfANewDrawingWithFiledia0_AsksTheFileOnTheCommandLine_AndListsTheDrawingUnderIt()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.SaveAsRequested += (s, e) =>
                    throw new Xunit.Sdk.XunitException("FILEDIA 0 shows no Save As dialog.");
                model.NewDrawing();
                model.Execute("FILEDIA");
                model.Execute("0");
                string path = Path.Combine(folder.FullPath, "typed.dwg");

                model.Execute("QSAVE");

                Assert.Equal("Save drawing as <Drawing1.dxf>:", model.Session.PromptText);
                model.Execute(path);

                Assert.False(model.Session.IsRunning);
                Assert.Equal(path, model.Document.FileName);
                Assert.Equal("AC1032", Magic(path));
                Assert.Contains("Saved " + path + ".", model.History);
                Assert.Contains(model.FileRows, r => r.Path == path);
            }
        }

        [Fact]
        public void SaveAsWithFiledia0_GivesANameTypedWithNoExtension_TheExtensionOfTheDrawingsFile()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(LineDwg(folder, "plan.dwg")));
                model.Execute("FILEDIA");
                model.Execute("0");
                string path = Path.Combine(folder.FullPath, "copy");

                model.Execute("SAVEAS");
                model.Execute(path);

                Assert.Equal(path + ".dwg", model.Document.FileName);
                Assert.Equal("AC1032", Magic(path + ".dwg"));
            }
        }

        [Fact]
        public void ADwgOfAVersionNotRead_IsSaidOnTheCommandLine_NotOpenedAsARecoveredDxf()
        {
            using (var folder = new TestFolder())
            {
                string path = Path.Combine(folder.FullPath, "old.dwg");
                var bytes = new byte[256];
                Encoding.ASCII.GetBytes("AC1014").CopyTo(bytes, 0);
                File.WriteAllBytes(path, bytes);
                var model = new MainViewModel();

                Assert.False(model.Open(path));

                Assert.Null(model.Document);
                Assert.Contains(model.History, l =>
                    l.StartsWith("Cannot open old.dwg: The DWG version AC1014", StringComparison.Ordinal)
                    && l.Contains("is not read yet")
                );
            }
        }

        [Fact]
        public void ThePalettesBrowseWithFiledia0_AsksTheDrawingOnTheCommandLine_AndShowsItsBlocks()
        {
            using (var folder = new TestFolder())
            {
                string column = LineDwg(folder, "column.dwg");
                var model = new MainViewModel();
                model.NewDrawing();
                model.Execute("FILEDIA");
                model.Execute("0");
                int shown = 0;
                model.ShowBlocksPalette = palette =>
                {
                    shown++;
                    Assert.True(palette.BrowsesOnCommandLine);

                    if (shown == 1)
                    {
                        palette.Request.Action = CadDialogAction.Browse;
                        return true;
                    }

                    Assert.Equal(column, palette.OtherFile);
                    Assert.True(palette.IsOtherDrawing);
                    Assert.Equal("column.dwg · 1 block", palette.SourceText);
                    Assert.True(palette.Choose(palette.Tiles[0]));
                    return true;
                };

                model.Execute("INSERT");

                Assert.Equal("Enter name of drawing file:", model.Session.PromptText);
                model.Execute(column);
                model.Execute("500,500");

                Assert.Equal(2, shown);
                Assert.False(model.Session.IsRunning);
                DxfBlockReference placed = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfBlockReference>());
                Assert.Equal("column", placed.Name);
            }
        }

        [Fact]
        public void TheDialogsWithFiledia1_BrowseWithTheFileDialog_AndOfferDwgFirst()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(LineDwg(folder, "plan.dwg")));
                bool? palette = null;
                bool? writeBlock = null;
                model.ShowBlocksPalette = p =>
                {
                    palette = p.BrowsesOnCommandLine;
                    return true;
                };
                model.ShowWriteBlock = w =>
                {
                    writeBlock = w.BrowsesOnCommandLine;
                    return true;
                };

                model.Execute("INSERT");
                model.Execute("WBLOCK");

                Assert.False(palette);
                Assert.False(writeBlock);
                var attach = new AttachReferenceViewModel(new CadAttachRequest(model.Document, CadAttachKind.Xref));
                Assert.Equal(CadDrawingFile.OpenFilter, attach.BrowseFilter);
                Assert.Contains("Drawing (*.dwg)", attach.FilesNote, StringComparison.Ordinal);
            }
        }
    }
}
