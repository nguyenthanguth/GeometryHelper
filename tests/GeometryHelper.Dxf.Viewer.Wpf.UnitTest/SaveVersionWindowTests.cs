using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Save Drawing As of the window with the version chosen, as AutoCAD 2021 lists it: a type for each DXF version, the one of
    /// the drawing's own version chosen first, and the version of the type chosen what is written, and written again by Save.
    /// </summary>
    public class SaveVersionWindowTests
    {
        private static readonly string[] Types =
        {
            "AutoCAD 2018 Drawing (*.dwg)",
            "AutoCAD 2018 DXF (*.dxf)",
            "AutoCAD 2013/LT2013 DXF (*.dxf)",
            "AutoCAD 2010/LT2010 DXF (*.dxf)",
            "AutoCAD 2007/LT2007 DXF (*.dxf)",
            "AutoCAD 2004/LT2004 DXF (*.dxf)",
            "AutoCAD 2000/LT2000 DXF (*.dxf)",
            "AutoCAD R12/LT2 DXF (*.dxf)",
        };

        private static readonly string[] Versions =
        {
            "AC1032",
            "AC1027",
            "AC1024",
            "AC1021",
            "AC1018",
            "AC1015",
            "AC1009",
        };

        /// <summary>A drawing of the version with one line, in a folder, opened in a window's view model.</summary>
        private static MainViewModel Opened(TestFolder folder, string version, string name = "plan.dxf")
        {
            var database = new DxfDatabase(version);
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write(name, database)));
            return model;
        }

        [Theory]
        [InlineData("AC1032", 1)]
        [InlineData("AC1027", 2)]
        [InlineData("AC1024", 3)]
        [InlineData("AC1021", 4)]
        [InlineData("AC1018", 5)]
        [InlineData("AC1015", 6)]
        public void SaveAs_OffersTheDwgAndEveryDxfVersion_TheTypeOfTheDrawingsOwnVersionChosen(
            string version,
            int chosen
        )
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, version);

                CadExportJob job = model.CreateSaveAsJob();

                Assert.Equal(Types, job.FileTypes);
                Assert.Equal(Types[chosen], job.FileType);
                Assert.Equal(Path.Combine(folder.FullPath, "plan.dxf"), job.FileName);
            }
        }

        [Fact]
        public void SaveAs_AfterTheTypeWasChosen_OffersThatTypeFirstAgain()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, "AC1032");

                Assert.True(model.SaveAs(Path.Combine(folder.FullPath, "old.dxf"), Types[4]));

                Assert.Equal(Types[4], model.CreateSaveAsJob().FileType);
                Assert.Equal(Types, model.CreateSaveAsJob().FileTypes);
            }
        }

        [Fact]
        public void ADrawingOfR14_IsOfferedItsOwnTypeAsWell_AsTheDxfItIs()
        {
            using (var folder = new TestFolder())
            {
                string text = new DxfDatabase("AC1015").ToText().Replace("AC1015", "AC1014");
                var model = new MainViewModel();
                Assert.True(model.Open(folder.WriteText("old.dxf", text)));

                CadExportJob job = model.CreateSaveAsJob();

                Assert.Equal(Types.Concat(new[] { "DXF (*.dxf)" }).ToArray(), job.FileTypes);
                Assert.Equal("DXF (*.dxf)", job.FileType);
            }
        }

        [Fact]
        public void ADrawingSavedAsThroughTheDialog_AsEachVersion_IsWrittenInIt()
        {
            using (var folder = new TestFolder())
            {
                for (int i = 0; i < Versions.Length; i++)
                {
                    MainViewModel model = Opened(folder, "AC1032", "from-" + i + ".dxf");
                    string path = Path.Combine(folder.FullPath, "as-" + Versions[i] + ".dxf");
                    model.SaveAsRequested += (s, e) =>
                    {
                        CadExportJob job = model.CreateSaveAsJob();
                        job.FileName = path;
                        job.FileType = Types[1 + Array.IndexOf(Versions, Versions[i])];
                        model.SaveAs(job.FileName, job.FileType);
                    };

                    model.Execute("SAVEAS");

                    Assert.Equal(Versions[i], DxfDatabase.Open(path).Version);
                    Assert.Single(DxfDatabase.Open(path).ModelSpace.OfType<DxfLine>());
                    Assert.Equal(path, model.Document.FileName);
                    Assert.Contains("Saved " + path + ".", model.History);
                }
            }
        }

        [Fact]
        public void QsaveAfterSaveAsAVersion_WritesThatVersionAgain()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, "AC1032");
                string path = Path.Combine(folder.FullPath, "plan2010.dxf");

                Assert.True(model.SaveAs(path, Types[3]));
                model.Document.ModelSpace.Block.AppendEntity(
                    new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(10, 50, 0))
                );
                model.Execute("QSAVE");

                Assert.Equal("AC1024", DxfDatabase.Open(path).Version);
                Assert.Equal(2, DxfDatabase.Open(path).ModelSpace.OfType<DxfLine>().Count());
                Assert.Equal("AC1032", model.Document.Database.Version);
            }
        }

        [Fact]
        public void SaveAsTheDwgType_WritesAnAutoCad2018Dwg_WhateverVersionWasChosenBefore()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, "AC1032");
                Assert.True(model.SaveAs(Path.Combine(folder.FullPath, "a.dxf"), Types[4]));
                string dwg = Path.Combine(folder.FullPath, "a.dwg");

                Assert.True(model.SaveAs(dwg, Types[0]));

                Assert.Equal("AC1032", CadDrawingFile.Open(dwg).Version);
                Assert.Null(model.Document.SaveVersion);
            }
        }

        [Fact]
        public void ADxfTypeForADwgName_IsRefused_ThatNothingIsWrittenAndItIsSaid()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, "AC1032");
                string dwg = Path.Combine(folder.FullPath, "a.dwg");

                Assert.False(model.SaveAs(dwg, Types[3]));

                Assert.False(File.Exists(dwg));
                Assert.Contains(model.History, l => l.StartsWith("Cannot save " + dwg + ":", StringComparison.Ordinal));
            }
        }

        [Fact]
        public void WhatTheVersionCannotHold_IsSaidAfterSaved()
        {
            using (var folder = new TestFolder())
            {
                string text = new DxfDatabase("AC1032").ToText();
                int end = text.LastIndexOf("ENDSEC", StringComparison.Ordinal);
                int line = text.LastIndexOf("  0", end, StringComparison.Ordinal);
                string eol = text.Contains("\r\n") ? "\r\n" : "\n";
                string style = string.Join(
                    eol,
                    "  0",
                    "VISUALSTYLE",
                    "  5",
                    "FFF1",
                    "330",
                    "0",
                    "100",
                    "AcDbVisualStyle",
                    "  2",
                    "custom",
                    string.Empty
                );
                var model = new MainViewModel();
                Assert.True(model.Open(folder.WriteText("styled.dxf", text.Insert(line, style))));
                string path = Path.Combine(folder.FullPath, "styled2000.dxf");

                Assert.True(model.SaveAs(path, Types[6]));

                Assert.Contains(model.History, l => l.Contains("VISUALSTYLE 1") && l.Contains("AC1015"));
                Assert.DoesNotContain("VISUALSTYLE", File.ReadAllText(path));
            }
        }
    }
}
