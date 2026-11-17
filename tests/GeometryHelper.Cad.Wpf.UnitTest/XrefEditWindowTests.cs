using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The edit of an external reference in place in the window: Edit Xref In-place of the object menu of an external
    /// reference starts REFEDIT on it, and the Reference Edit dialog the window shows for REFEDIT lists the references with the
    /// path of their files and gives the settings back to the command.
    /// </summary>
    public sealed class XrefEditWindowTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xrefeditwindow-" + Guid.NewGuid().ToString("N")
        );

        public XrefEditWindowTests()
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

        /// <summary>site.dxf holding a line from (0, 0) to (100, 0), and host.dxf with it attached as SITE at (1 000, 0), opened in a window; the handle of the INSERT.</summary>
        private MainViewModel Open(out string insert)
        {
            DxfDatabase site = CadTemplate.CreateMetric();
            site.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            site.Save(Path.Combine(_folder, "site.dxf"));
            DxfDatabase database = CadTemplate.CreateMetric();
            var reference = new DxfBlockReference(
                new GeoPoint3(1000, 0, 0),
                database.AttachXref(".\\site.dxf", "SITE")
            );
            database.ModelSpace.AppendEntity(reference);
            string path = Path.Combine(_folder, "host.dxf");
            database.Save(path);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.Open(path));
            insert = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle;
            return model;
        }

        [Fact]
        public void EditXrefInPlace_OfTheObjectMenuOfAnExternalReference_StartsRefeditOnIt()
        {
            MainViewModel model = Open(out string insert);
            model.Document.Selection.Set(new[] { insert });

            CadMenuItemViewModel item = model.CanvasMenu().Single(i => i.Header == "Edit Xref In-place");

            Assert.True(item.IsEnabled);
            Assert.Equal('d', item.AccessKey);
            item.Command.Execute(null);
            Assert.Equal("Enter object selection method [All/Nested] <All>:", model.Prompt);

            model.Execute(string.Empty);

            Assert.NotNull(model.Document.RefEdit);
            Assert.Equal("SITE", model.Document.RefEdit.Name);
            Assert.Equal(Path.Combine(_folder, "site.dxf"), model.Document.RefEdit.XrefFile, ignoreCase: true);
        }

        [Fact]
        public void TheReferenceEditDialog_ListsTheReferencesWithTheirPaths_AndGivesItsChoicesToTheCommand()
        {
            MainViewModel model = Open(out string insert);
            model.Document.Selection.Set(new[] { insert });
            ReferenceEditViewModel shown = null;
            model.ShowReferenceEdit = dialog =>
            {
                shown = dialog;
                dialog.UniqueNames = false;
                dialog.LockObjects = false;
                return true;
            };

            model.CanvasMenu().Single(i => i.Header == "Edit Xref In-place").Command.Execute(null);

            // The reference of the file, its path and the settings: unique names are for an external reference, attribute definitions are not (it has none).
            ReferenceEditRowViewModel row = Assert.Single(shown.References);
            Assert.Equal("SITE", row.Name);
            Assert.Equal("Xref", row.Kind);
            Assert.True(shown.IsExternal);
            Assert.False(shown.HasAttributeDefinitions);
            Assert.Equal(Path.Combine(_folder, "site.dxf"), shown.PathText, ignoreCase: true);
            Assert.True(shown.IsAutomatic);

            // OK took the choices: the edit is open with them, no question asked.
            Assert.NotNull(model.Document.RefEdit);
            Assert.False(model.Document.RefEditUniqueNames);
            Assert.False(model.Document.RefEdit.LocksObjects);
            Assert.Single(model.Document.RefEdit.WorkingSet);
        }

        [Fact]
        public void TheReferenceEditDialog_Cancelled_OpensNoEdit()
        {
            MainViewModel model = Open(out string insert);
            model.Document.Selection.Set(new[] { insert });
            model.ShowReferenceEdit = dialog => false;

            model.CanvasMenu().Single(i => i.Header == "Edit Xref In-place").Command.Execute(null);

            Assert.Null(model.Document.RefEdit);
            Assert.True(model.Document.RefEditUniqueNames);
        }
    }
}
