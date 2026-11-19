using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// <see cref="CadWorkspaceExtension.DocumentSaved"/>: told after every save that reaches a file, whichever way it was
    /// asked for (Save, Save As, QSAVE, Save All), with the document and the file's full path, and not told of a save that
    /// failed or was refused.
    /// </summary>
    public sealed class ExtensionDocumentSavedTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private sealed class Recorder : CadWorkspaceExtension
        {
            public List<(CadDocument Document, string Path)> Saved { get; } =
                new List<(CadDocument Document, string Path)>();

            public override void DocumentSaved(CadDocument document, string path) => Saved.Add((document, path));
        }

        [Fact]
        public void Save_TellsEveryExtensionTheDocumentAndItsFilesFullPath()
        {
            var model = new MainViewModel();
            var first = new Recorder();
            var second = new Recorder();
            model.AddExtension(first);
            model.AddExtension(second);
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            AddLine(model);

            Assert.True(model.Save());

            Assert.Equal((model.Document, Path.GetFullPath(room)), Assert.Single(first.Saved));
            Assert.Equal((model.Document, Path.GetFullPath(room)), Assert.Single(second.Saved));
        }

        [Fact]
        public void QsavedOnTheCommandLine_IsToldToo_ThoughTheCommandWritesTheFileWithoutTheWindow()
        {
            var model = new MainViewModel();
            var recorder = new Recorder();
            model.AddExtension(recorder);
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            AddLine(model);

            model.Execute("QSAVE");

            Assert.False(model.Document.IsModified);
            Assert.Equal((model.Document, Path.GetFullPath(room)), Assert.Single(recorder.Saved));
        }

        [Fact]
        public void SaveAs_TellsTheNewPath_AndSaveAllTellsEachDrawingItSaved()
        {
            var model = new MainViewModel();
            var recorder = new Recorder();
            model.AddExtension(recorder);
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            string other = _folder.Write("other.dxf", TestFiles.Room(out _, out _));
            string copy = Path.Combine(_folder.FullPath, "copy.dxf");
            Assert.True(model.Open(room));

            Assert.True(model.SaveAs(copy));

            Assert.Equal(Path.GetFullPath(copy), Assert.Single(recorder.Saved).Path);

            recorder.Saved.Clear();
            Assert.True(model.Open(other));
            AddLine(model);
            CadDocument changedOther = model.Document;
            Assert.True(model.Open(copy));
            AddLine(model);

            Assert.True(model.SaveAllDocuments());

            Assert.Equal(
                new[] { Path.GetFullPath(copy), Path.GetFullPath(other) },
                new[] { recorder.Saved[0].Path, recorder.Saved[1].Path },
                StringComparer.OrdinalIgnoreCase
            );
            Assert.Equal(2, recorder.Saved.Count);
            Assert.Contains(recorder.Saved, s => ReferenceEquals(s.Document, changedOther));
        }

        [Fact]
        public void ASaveThatIsRefused_IsNotToldToAnyExtension()
        {
            var model = new MainViewModel();
            var recorder = new Recorder();
            model.AddExtension(recorder);
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            string other = _folder.Write("other.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(other));
            Assert.True(model.Open(room));

            // other.dxf is open as another drawing: room.dxf is not written over it.
            Assert.False(model.SaveAs(other));

            Assert.Empty(recorder.Saved);
        }

        private static void AddLine(MainViewModel model)
        {
            CadDocument document = model.Document;
            Assert.True(
                document
                    .ApplyEdit("LINE", null, scope =>
                        scope.Append(document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0)))
                    )
                    .Succeeded
            );
        }
    }
}
