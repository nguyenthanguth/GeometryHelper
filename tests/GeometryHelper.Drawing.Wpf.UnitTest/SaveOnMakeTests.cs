using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>A model drawing made is saved into the save folder of Global settings, so that autosave keeps copies of it.</summary>
    public class SaveOnMakeTests
    {
        [Fact]
        public async Task ADrawingMadeIsSavedIntoTheSaveFolderUnderANameNotTakenAndAutosaveKeepsCopiesOfIt()
        {
            string root = Bench.NewFolder();
            string folder = Path.Combine(root, "drawings");

            try
            {
                var bench = new Bench();
                bench.Model.ApplySettings(
                    new GlobalSettings
                    {
                        SaveFolder = folder,
                        AutosaveMinutes = 5,
                        BackupsKept = 2,
                    }
                );

                // The folder is made, and the drawing saved there under its number.
                ModelDrawing first = await bench.MakeAsync();
                string path = Path.Combine(folder, first.Number + ".dxf");
                Assert.True(first.Document.HasFile);
                Assert.Equal(path, first.Document.FileName);
                Assert.True(File.Exists(path));
                Assert.False(first.Document.IsModified);
                Assert.Contains("Saved " + first.Number + " as " + path + ".", bench.Model.History);
                byte[] saved = File.ReadAllBytes(path);

                // A second drawing of the same number takes the next name free; the first file is not written over.
                ModelDrawing second = await bench.MakeAsync();
                Assert.Equal(first.Number, second.Number);
                Assert.Equal(Path.Combine(folder, second.Number + " (2).dxf"), second.Document.FileName);
                Assert.Equal(saved, File.ReadAllBytes(path));

                // Changed by hand, the first is autosaved beside its file, which is left as it was saved.
                first.Document.ApplyEdit("LINE", null, s =>
                    s.Append(first.Document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)))
                );
                IReadOnlyList<string> written = bench.Model.Autosave();
                Assert.Equal(new[] { Path.Combine(folder, first.Number + ".autosave1.dxf") }, written);
                Assert.Equal(saved, File.ReadAllBytes(path));
            }
            finally
            {
                Bench.Delete(root);
            }
        }

        [Fact]
        public async Task ADrawingMadeBeforeGlobalSettingsAreAppliedIsKeptInMemoryOnly()
        {
            var bench = new Bench();

            ModelDrawing made = await bench.MakeAsync();

            Assert.False(made.Document.HasFile);
            Assert.DoesNotContain(bench.Model.History, l => l.StartsWith("Saved ", System.StringComparison.Ordinal));
        }
    }
}
