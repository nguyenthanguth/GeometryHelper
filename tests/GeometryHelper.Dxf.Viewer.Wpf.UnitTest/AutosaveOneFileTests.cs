using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Autosave as the maintainer asked on 2026-11-17: one copy beside each drawing, written over each time (plan.autosave.dxf,
    /// or plan.autosave.dwg with Global settings' Autosave format set to DWG), and the numbered copies older versions left
    /// still found by the Drawing Recovery Manager.
    /// </summary>
    public sealed class AutosaveOneFileTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private static void AddLine(MainViewModel model, double y)
        {
            CadDocument document = model.Document;
            Assert.True(
                document
                    .ApplyEdit("LINE", null, scope =>
                        scope.Append(document.ModelSpace, new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(10, y, 0)))
                    )
                    .Succeeded
            );
        }

        private string LineDwg(string name)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
            string path = Path.Combine(_folder.FullPath, name);
            DwgWriter.Write(database, path);
            return path;
        }

        private static string Magic(string path) => Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 6);

        private string At(string name) => Path.Combine(_folder.FullPath, name);

        [Fact]
        public void EachAutosaveOfADxfWritesTheSameFileOver_NoNumberedCopyIsMade()
        {
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings { AutosaveMinutes = 5 });
            Assert.True(model.Open(room));
            AddLine(model, 1);

            Assert.Equal(new[] { At("room.autosave.dxf") }, model.Autosave());
            int first = CadDocument.Open(At("room.autosave.dxf")).EntityCount;

            AddLine(model, 2);
            Assert.Equal(new[] { At("room.autosave.dxf") }, model.Autosave());

            Assert.Equal(first + 1, CadDocument.Open(At("room.autosave.dxf")).EntityCount);
            Assert.Equal(new[] { "room.autosave.dxf", "room.dxf" }, FilesOf(_folder.FullPath));
        }

        [Fact]
        public void TheFormatDwgWritesPlanAutosaveDwgInAutoCad2018_ForADxfDrawing()
        {
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings { AutosaveMinutes = 5, AutosaveFormat = "DWG" });
            Assert.True(model.Open(room));
            AddLine(model, 1);

            Assert.Equal(new[] { At("room.autosave.dwg") }, model.Autosave());
            model.Autosave();

            Assert.Equal("AC1032", Magic(At("room.autosave.dwg")));
            Assert.Equal(CadDocument.Open(room).EntityCount + 1, CadDocument.Open(At("room.autosave.dwg")).EntityCount);
            Assert.Equal(new[] { "room.autosave.dwg", "room.dxf" }, FilesOf(_folder.FullPath));
            model.Refresh();
            Assert.DoesNotContain(model.FileRows, r => r.Label.Contains("autosave"));
        }

        [Fact]
        public void TheFormatDwgOfADwgDrawingKeepsTheVersionItWasSavedAs_AndTheNameKeepsDwg()
        {
            string plan = LineDwg("plan.dwg");
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings { AutosaveMinutes = 5, AutosaveFormat = "DWG" });
            Assert.True(model.Open(plan));
            Assert.True(model.SaveAs(plan, CadExportJob.DwgTypeOfVersion("AC1027")));
            AddLine(model, 5);

            Assert.Equal(new[] { At("plan.dwg.autosave.dwg") }, model.Autosave());

            Assert.Equal("AC1027", Magic(At("plan.dwg.autosave.dwg")));
            Assert.Equal(2, CadDocument.Open(At("plan.dwg.autosave.dwg")).EntityCount);
        }

        [Fact]
        public void TheDefaultFormatIsDxf_ItIsKeptInTheFile_AndAnotherNameIsRefused()
        {
            Assert.Equal("DXF", new GlobalSettings().AutosaveFormat);
            string path = At("settings.json");
            new GlobalSettings { AutosaveFormat = "dwg" }.Save(path);

            Assert.Equal("DWG", GlobalSettings.Load(path).AutosaveFormat);
            Assert.Equal("DXF", GlobalSettings.Load(At("none.json")).AutosaveFormat);
            Assert.Throws<ArgumentException>(() => new GlobalSettings().AutosaveFormat = "PDF");

            File.WriteAllText(path, "{ \"saveAndExport\": { \"autosaveFormat\": \"PDF\", \"backupsKept\": 7 } }");
            Assert.Equal("DXF", GlobalSettings.Load(path).AutosaveFormat);
        }

        [Fact]
        public void GlobalSettingsSavingHasAutosaveFormat_AndNoBackupsKept()
        {
            var model = new MainViewModel();
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings("Save and export");

            Assert.Equal(
                new[] { "Save folder", "Autosave every", "Autosave format" },
                dialog.Sections.Single(s => s.Title == "Saving").Rows.Select(r => r.Label)
            );
            GlobalSettingRow row = dialog.Row("Save and export", "Autosave format");
            Assert.Equal("DXF", row.Value);
            Assert.Equal(new[] { "DXF", "DWG" }, row.Options);

            row.Value = "DWG";
            Assert.True(dialog.Apply());
            Assert.Equal("DWG", model.Settings.AutosaveFormat);
        }

        [Fact]
        public void TheDrawingRecoveryManagerListsTheOneCopyOfEachFormat_AndTheNumberedOnesOfOlderVersions()
        {
            string a = _folder.Write("a.dxf", TestFiles.Room(out _, out _));
            File.Copy(a, At("a.autosave.dxf"));
            File.Copy(a, At("a.autosave2.dxf"));
            File.Copy(a, At("a.autosave1.dxf"));
            File.Copy(a, At("a.dwg.autosave.dwg"));
            File.Copy(a, At("a.autosavex.dxf"));
            var model = new MainViewModel();
            var shown = new List<BackupFileRow>();
            model.ShowDrawingRecovery = vm => shown.AddRange(vm.Copies);
            Assert.True(model.Open(a));

            model.Execute("DRAWINGRECOVERY");

            Assert.Equal(
                new[] { "a.autosave.dxf", "a.autosave1.dxf", "a.autosave2.dxf", "a.dwg.autosave.dwg" },
                shown.Select(r => r.Name)
            );
            Assert.Equal(new[] { "a.dxf", "a.dxf", "a.dxf", "a.dwg" }, shown.Select(r => r.Original));
        }

        [Fact]
        public void AnOldNumberedCopyIsStillOpenedByRecovery_AndAutosaveNeverWritesOrDeletesIt()
        {
            string a = _folder.Write("a.dxf", TestFiles.Room(out _, out _));
            File.Copy(a, At("a.autosave1.dxf"));
            byte[] old = File.ReadAllBytes(At("a.autosave1.dxf"));
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings { AutosaveMinutes = 5 });
            Assert.True(model.Open(a));
            AddLine(model, 1);
            model.Autosave();

            Assert.Equal(old, File.ReadAllBytes(At("a.autosave1.dxf")));
            Assert.False(File.Exists(At("a.autosave2.dxf")));

            model.Execute("DRAWINGRECOVERY");
            Assert.Contains(model.History, l => l.StartsWith("  1  a.autosave.dxf  ", StringComparison.Ordinal));
            Assert.Contains(model.History, l => l.StartsWith("  2  a.autosave1.dxf  ", StringComparison.Ordinal));
            model.Execute("2");
            Assert.Equal(
                Path.GetFullPath(At("a.autosave1.dxf")),
                Path.GetFullPath(model.Document.FileName),
                StringComparer.OrdinalIgnoreCase
            );
        }

        private static string[] FilesOf(string folder) =>
            Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }
}
