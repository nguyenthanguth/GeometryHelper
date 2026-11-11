using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// DWG in the presenter, as AutoCAD 2021 takes it: a DWG opened as its drawing and saved back as a DWG of AutoCAD 2018, a
    /// drawing saved as .dwg written so, a DWG attached as an external reference or inserted as a block read by the DWG
    /// reader, and FILEDIA 0 asking on the command line the file QSAVE of a new drawing, XATTACH, IMAGEATTACH, WBLOCK's
    /// Browse and INSERT's Browse would ask a file dialog for.
    /// </summary>
    public class DwgFileTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gk", "dwg-" + Guid.NewGuid().ToString("N"));

        public DwgFileTests()
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

        /// <summary>
        /// A metric drawing of a line on layer GRID from (0, 0) to (1 000, 0) and a circle of radius 100 about the origin,
        /// written as a DWG of AutoCAD 2018 in the test's folder.
        /// </summary>
        private string GridDwg(string name = "grid")
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID" });
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" }
            );
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0));
            string path = Path.Combine(_folder, name + ".dwg");
            DwgWriter.Write(database, path);
            return path;
        }

        /// <summary>A metric drawing with a line from (0, 0) to (0, 500), saved in the test's folder as host.dxf and opened.</summary>
        private CadDocument Host()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 500, 0)));
            string path = Path.Combine(_folder, "host.dxf");
            database.Save(path);
            return CadDocument.Open(path);
        }

        /// <summary>A PNG of 40 by 30 red pixels in the test's folder.</summary>
        private string Png(string name)
        {
            string path = Path.Combine(_folder, name);

            using (var bitmap = new SKBitmap(40, 30))
            {
                bitmap.Erase(SKColors.Red);

                using (SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
                {
                    File.WriteAllBytes(path, data.ToArray());
                }
            }

            return path;
        }

        private static string Magic(string path) => Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 6);

        private static void Answer<T>(CadCommandSession session, string name, Action<T> step)
        {
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == name)
                {
                    step((T)e.Payload);
                    e.Handled = true;
                }
            };
        }

        private static CadCommandSession WithFiledia0(CadDocument document)
        {
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");
            Assert.False(session.FileDialogs);
            return session;
        }

        [Fact]
        public void ADwgOpened_IsItsDrawing_AndQsaveWritesItBackAsADwgOfAutoCad2018()
        {
            string path = GridDwg();

            CadDocument document = CadDocument.Open(path);

            Assert.True(document.HasFile);
            Assert.Equal(Path.GetFullPath(path), document.FileName);
            Assert.Equal("grid.dwg", document.Title);
            Assert.Equal("GRID", Assert.Single(All<DxfLine>(document.ModelSpace)).Layer);
            Assert.Equal(100.0, Assert.Single(All<DxfCircle>(document.ModelSpace)).Radius, 9);

            CadCommandSession session = Session(document);
            Run(session, "LINE", new GeoPoint2(0, 50), new GeoPoint2(250, 50), Enter);
            Run(session, "QSAVE");

            Assert.Contains("Saved " + Path.GetFullPath(path) + ".", session.History);
            Assert.False(document.IsModified);
            Assert.Equal("AC1032", Magic(path));
            DxfDatabase again = DwgReader.Open(path);
            Assert.Equal(2, again.ModelSpace.OfType<DxfLine>().Count());
            Assert.Single(again.ModelSpace.OfType<DxfCircle>());
        }

        [Fact]
        public void ADrawingSavedAsDotDwg_IsWrittenAsAutoCad2018_AndAsDotDxfAsTextAgain()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(10, 20, 0), new GeoVector3(0, 0, 1), 25.0));
            CadDocument document = CadDocument.From(database, "Drawing1.dxf");
            string dwg = Path.Combine(_folder, "ring.dwg");
            string dxf = Path.Combine(_folder, "ring.dxf");

            document.SaveAs(dwg);

            Assert.Equal(dwg, document.FileName);
            Assert.Equal("AC1032", Magic(dwg));
            Assert.Equal(25.0, Assert.Single(DwgReader.Open(dwg).ModelSpace.OfType<DxfCircle>()).Radius, 9);
            Assert.Equal(string.Empty, document.LeftOutOfDwg);

            document.SaveAs(dxf);

            Assert.Equal(dxf, document.FileName);
            Assert.False(CadDrawingFile.IsDwg(dxf));
            Assert.Single(DxfDatabase.Open(dxf).ModelSpace.OfType<DxfCircle>());
        }

        [Fact]
        public void ADwgAttachedAsAnExternalReference_IsReadByTheDwgReader_AndShownUnderItsInsert()
        {
            string grid = GridDwg();
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Answer<CadAttachRequest>(session, "XATTACH", r =>
            {
                r.FileName = grid;
                Assert.Null(r.Error);
                Assert.Equal("grid", r.Name);
                Assert.Equal(".\\grid.dwg", r.SavedPath);
                Assert.Equal(DxfUnits.Millimeters, r.Units);
                r.SpecifyPointOnScreen = false;
                r.Point = new GeoPoint3(100, 200, 0);
                r.Action = CadDialogAction.Ok;
            });

            Run(session, "XATTACH");

            Assert.Contains("\"grid\" loaded.", session.History);
            Assert.Equal(".\\grid.dwg", document.Database.BlockTable["grid"].PathName);
            DisplayPolyline2 line = Assert.Single(
                document.GetDisplay(document.ModelSpace).Items.OfType<DisplayPolyline2>(),
                p => p.Layer == "grid|GRID"
            );
            Assert.Equal(new[] { 100.0, 200.0, 1100.0, 200.0 }, line.Xy);
            Assert.Equal(CadReferenceStatus.Loaded, Assert.Single(document.Xrefs.GetReferences()).Status);
        }

        [Fact]
        public void ADwgTypedAtInsert_IsBroughtInAsABlockOfItsName()
        {
            string grid = GridDwg("column");
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Run(session, "-INSERT", grid, new GeoPoint2(500, 500), Enter, Enter, Enter);

            Assert.False(session.IsRunning);
            DxfBlockTableRecord block = document.Database.BlockTable["column"];
            Assert.Single(block.OfType<DxfLine>());
            Assert.Single(block.OfType<DxfCircle>());
            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal("column", insert.Name);
            Near(new GeoPoint3(500, 500, 0), insert.Position);
        }

        [Fact]
        public void TheTypedWblock_WritesADwgOfAutoCad2018_WhereTheFileIsNamedDotDwg()
        {
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            string path = Path.Combine(_folder, "whole.dwg");

            Run(session, "-WBLOCK", path, "*");

            Assert.False(session.IsRunning);
            Assert.Equal("AC1032", Magic(path));
            DxfLine line = Assert.Single(DwgReader.Open(path).ModelSpace.OfType<DxfLine>());
            Near(new GeoPoint3(0, 500, 0), line.EndPoint);
        }

        [Fact]
        public void QsaveOfANewDrawingWithFiledia0_AsksTheFileOnTheCommandLine_AndSavesThere()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "Drawing1.dxf");
            CadCommandSession session = WithFiledia0(document);
            var dialogs = new List<string>();
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "QSAVE" && e.Payload == null)
                {
                    dialogs.Add(e.Name);
                    e.Handled = true;
                }
            };
            string path = Path.Combine(_folder, "typed");

            Run(session, "QSAVE");

            Assert.Equal("Save drawing as <Drawing1.dxf>:", session.PromptText);
            Run(session, path);

            Assert.Empty(dialogs);
            Assert.False(session.IsRunning);
            Assert.Equal(path + ".dxf", document.FileName);
            Assert.Contains("Saved " + path + ".dxf.", session.History);
            Assert.False(CadDrawingFile.IsDwg(path + ".dxf"));
            Assert.NotNull(DxfDatabase.Open(path + ".dxf").ModelSpace);
        }

        [Fact]
        public void QsaveOfANewDrawingWithFiledia1_StillAsksTheWindow_AndWithNoneSaysSo()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "Drawing1.dxf");
            CadCommandSession session = Session(document);

            Run(session, "QSAVE");

            Assert.False(session.IsRunning);
            Assert.Contains("The drawing has no file yet: save it with Save As.", session.History);
            Assert.False(document.HasFile);
        }

        [Fact]
        public void XattachWithFiledia0_AsksTheFileOnTheCommandLine_ThenShowsTheDialogWithIt()
        {
            string grid = GridDwg();
            CadDocument document = Host();
            CadCommandSession session = WithFiledia0(document);
            string seen = null;
            Answer<CadAttachRequest>(session, "XATTACH", r =>
            {
                seen = r.FileName;
                r.SpecifyPointOnScreen = false;
                r.Action = CadDialogAction.Ok;
            });

            Run(session, "XATTACH");

            Assert.Equal("Enter name of file to attach:", session.PromptText);
            Run(session, grid);

            Assert.Equal(grid, seen);
            Assert.False(session.IsRunning);
            Assert.Contains("\"grid\" loaded.", session.History);
        }

        [Fact]
        public void XattachWithFiledia0_OfAFileNotFound_SaysSo_AndShowsNoDialog()
        {
            CadDocument document = Host();
            CadCommandSession session = WithFiledia0(document);
            var dialogs = new List<string>();
            Answer<CadAttachRequest>(session, "XATTACH", r => dialogs.Add(r.FileName));
            string missing = Path.Combine(_folder, "missing.dwg");

            Run(session, "XATTACH", missing);

            Assert.Empty(dialogs);
            Assert.False(session.IsRunning);
            Assert.Contains("\"" + missing + "\" cannot be found.", session.History);
        }

        [Fact]
        public void ImageattachWithFiledia0_AsksTheFileOnTheCommandLine_ThenShowsTheDialogWithIt()
        {
            string png = Png("logo.png");
            CadDocument document = Host();
            CadCommandSession session = WithFiledia0(document);
            string seen = null;
            Answer<CadAttachRequest>(session, "IMAGEATTACH", r =>
            {
                seen = r.FileName;
                Assert.Equal(40, r.PixelWidth);
                r.SpecifyPointOnScreen = false;
                r.Action = CadDialogAction.Ok;
            });

            Run(session, "IMAGEATTACH");

            Assert.Equal("Enter image file name to attach:", session.PromptText);
            Run(session, png);

            Assert.Equal(png, seen);
            Assert.False(session.IsRunning);
            Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayImage2>());
        }

        [Fact]
        public void WblockBrowseWithFiledia0_AsksTheFileOnTheCommandLine_ThenShowsTheDialogAgainWithIt()
        {
            CadDocument document = Host();
            CadCommandSession session = WithFiledia0(document);
            var shown = new List<string>();
            string path = Path.Combine(_folder, "browsed");
            Answer<CadWriteBlockRequest>(session, "WBLOCK", r =>
            {
                shown.Add(r.FileName);
                Assert.False(r.FileDialogs);

                if (shown.Count == 1)
                {
                    r.Action = CadDialogAction.Browse;
                    return;
                }

                r.Source = CadWriteBlockSource.EntireDrawing;
                r.Action = CadDialogAction.Ok;
            });

            Run(session, "WBLOCK");

            Assert.Equal("Enter name of output file:", session.PromptText);
            Run(session, path + ".dwg");

            Assert.False(session.IsRunning);
            Assert.Equal(2, shown.Count);
            Assert.Equal(path + ".dwg", shown[1]);
            Assert.Equal("AC1032", Magic(path + ".dwg"));
        }

        [Fact]
        public void WblockWithFiledia1_TellsTheDialogItsBrowseShowsTheFileDialog()
        {
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            bool? dialogs = null;
            Answer<CadWriteBlockRequest>(session, "WBLOCK", r => dialogs = r.FileDialogs);

            Run(session, "WBLOCK");

            Assert.True(dialogs);
        }

        [Fact]
        public void InsertBrowseWithFiledia0_AsksTheDrawingOnTheCommandLine_ThenShowsThePaletteAgainWithIt()
        {
            string column = GridDwg("column");
            CadDocument document = Host();
            CadCommandSession session = WithFiledia0(document);
            var shown = new List<string>();
            Answer<CadInsertRequest>(session, "INSERT", r =>
            {
                shown.Add(r.FileName);
                Assert.False(r.FileDialogs);

                if (shown.Count == 1)
                {
                    r.Action = CadDialogAction.Browse;
                    return;
                }

                r.Name = string.Empty;
                r.SpecifyPointOnScreen = false;
                r.Point = new GeoPoint3(500, 500, 0);
                r.Action = CadDialogAction.Ok;
            });

            Run(session, "INSERT");

            Assert.Equal("Enter name of drawing file:", session.PromptText);
            Run(session, column);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { null, column }, shown);
            Assert.Equal("column", Assert.Single(All<DxfBlockReference>(document.ModelSpace)).Name);
        }

        [Fact]
        public void InsertBrowseWithFiledia0_OfADrawingNotFound_SaysSo_AndShowsThePaletteAgainAsItWas()
        {
            CadDocument document = Host();
            CadCommandSession session = WithFiledia0(document);
            var shown = new List<string>();
            Answer<CadInsertRequest>(session, "INSERT", r =>
            {
                shown.Add(r.FileName);
                r.Action = shown.Count == 1 ? CadDialogAction.Browse : CadDialogAction.Cancel;
            });
            string missing = Path.Combine(_folder, "missing.dwg");

            Run(session, "INSERT", missing);

            Assert.False(session.IsRunning);
            Assert.Equal(new string[] { null, null }, shown);
            Assert.Contains("\"" + missing + "\" cannot be found.", session.History);
        }

        [Fact]
        public void ADrawingName_IsOneThePresenterOpens_WhereItEndsInDwgOrDxf()
        {
            Assert.True(CadDrawingFile.IsDrawingName(@"D:\Plans\plan.dwg"));
            Assert.True(CadDrawingFile.IsDrawingName(@"D:\Plans\PLAN.DXF"));
            Assert.False(CadDrawingFile.IsDrawingName(@"D:\Plans\plan.dwt"));
            Assert.False(CadDrawingFile.IsDrawingName(@"D:\Plans\plan.ghplugin"));
            Assert.False(CadDrawingFile.IsDrawingName(null));
            Assert.StartsWith(
                "Drawing (*.dwg)|*.dwg|DXF (*.dxf)|*.dxf",
                CadDrawingFile.OpenFilter,
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void AFileIsReadAsADwg_ByItsFirstBytes_NotByItsName()
        {
            string dwg = GridDwg();
            string named = Path.Combine(_folder, "really-a-dwg.dxf");
            File.Copy(dwg, named);
            string dxf = Path.Combine(_folder, "text.dxf");
            CadTemplate.CreateMetric().Save(dxf);

            Assert.True(CadDrawingFile.IsDwg(dwg));
            Assert.True(CadDrawingFile.IsDwg(named));
            Assert.False(CadDrawingFile.IsDwg(dxf));
            Assert.Single(CadDrawingFile.Open(named).ModelSpace.OfType<DxfLine>());
            Assert.Empty(CadDrawingFile.Open(dxf).ModelSpace.OfType<DxfLine>());
        }

        [Fact]
        public void ADrawingWithAPictureSavedAsADwg_LeavesNothingOut_AndSaysNoLeftOutLine()
        {
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            string png = Png("logo.png");
            Answer<CadAttachRequest>(session, "IMAGEATTACH", r =>
            {
                r.FileName = png;
                r.SpecifyPointOnScreen = false;
                r.Action = CadDialogAction.Ok;
            });
            Run(session, "IMAGEATTACH");
            string dwg = Path.Combine(_folder, "host.dwg");

            document.SaveAs(dwg);

            // What is left out depends on what the writer covers: since W5b the picture's RASTERVARIABLES is written too.
            Assert.Equal(string.Empty, document.LeftOutOfDwg);

            Run(session, "QSAVE");

            Assert.Contains("Saved " + dwg + ".", session.History);
            Assert.DoesNotContain(session.History, line =>
                line.StartsWith("Left out of the DWG", StringComparison.Ordinal)
            );

            document.SaveAs(Path.Combine(_folder, "host-again.dxf"));

            Assert.Equal(string.Empty, document.LeftOutOfDwg);
        }
    }
}
