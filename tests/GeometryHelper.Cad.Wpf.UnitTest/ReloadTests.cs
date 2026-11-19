using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// <see cref="MainViewModel.Reload"/>: a document opened again from its file in the same row of the list after the file
    /// was written outside, its view and space kept, its extensions told, a document with unsaved changes refused unless the
    /// changes are dropped, and a file that cannot be read leaving the document as it was.
    /// </summary>
    public sealed class ReloadTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private sealed class Recorder : CadWorkspaceExtension
        {
            public List<string> Told { get; } = new List<string>();

            public override void DocumentOpened(CadDocument document) => Told.Add("opened " + document.EntityCount);

            public override void DocumentClosed(CadDocument document) => Told.Add("closed " + document.EntityCount);
        }

        [Fact]
        public void AFileRewrittenOutside_IsReadAgainInTheSameRow_WithTheViewTheUserLeftItAt()
        {
            var model = new MainViewModel();
            model.SetCanvasSize(800, 600);
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            CadDocument old = model.Document;
            model.View.Pan(120, 40);
            model.View.ZoomToHeight(1500, 700, 900);
            model.NotifyViewChangedByUser();
            double x = model.View.CenterX;
            double y = model.View.CenterY;
            double scale = model.View.PixelsPerUnit;
            int rows = model.FileRows.Count;
            Rewrite(room, 5);

            Assert.True(model.Reload(room));

            Assert.NotSame(old, model.Document);
            Assert.Equal(5, model.Document.EntityCount);
            Assert.Equal("room.dxf", model.Document.Title);
            Assert.Equal(rows, model.FileRows.Count);
            Assert.True(model.FileRows.Single(r => r.Label == "room.dxf").IsOpen);
            Assert.Equal(x, model.View.CenterX, 6);
            Assert.Equal(y, model.View.CenterY, 6);
            Assert.Equal(scale, model.View.PixelsPerUnit, 9);
            Assert.False(model.Document.IsModified);
        }

        [Fact]
        public void AViewStillFittedToTheDrawing_IsFittedToTheNewOne()
        {
            var model = new MainViewModel();
            model.SetCanvasSize(800, 600);
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            double scale = model.View.PixelsPerUnit;

            // Two lines a hundred times longer than the room: the new extents are far larger, so the fit zooms out.
            var database = TestFiles.Room(out _, out _);
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(400000, 300000, 0)) { Layer = "WALLS" }
            );
            database.Save(room);

            Assert.True(model.Reload(room));

            Assert.True(model.View.PixelsPerUnit < scale / 10.0);
        }

        [Fact]
        public void TheSpaceThatWasCurrentStaysCurrent()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            CadSpace layout = model.Document.Spaces.Single(s => !s.IsModel);
            model.Document.CurrentSpace = layout;

            Assert.True(model.Reload(room));

            Assert.Equal(layout.Name, model.Document.CurrentSpace.Name);
            Assert.False(model.Document.CurrentSpace.IsModel);
        }

        [Fact]
        public void TheExtensions_AreToldTheOldDocumentIsClosedThenTheNewOneOpened()
        {
            var model = new MainViewModel();
            var recorder = new Recorder();
            model.AddExtension(recorder);
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            recorder.Told.Clear();
            Rewrite(room, 6);

            Assert.True(model.Reload(room));

            Assert.Equal(new[] { "closed 3", "opened 6" }, recorder.Told);
        }

        [Fact]
        public void ADocumentWithChangesNotSaved_IsRefusedAndKept_UnlessTheChangesAreDropped()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            CadDocument old = model.Document;
            AddLine(model);
            Rewrite(room, 5);

            Assert.False(model.Reload(room));

            Assert.Same(old, model.Document);
            Assert.True(old.IsModified);
            Assert.Contains(model.History, line => line.Contains("room.dxf has changes not saved"));

            Assert.True(model.Reload(room, discardChanges: true));

            Assert.NotSame(old, model.Document);
            Assert.Equal(5, model.Document.EntityCount);
            Assert.False(model.Document.IsModified);
        }

        [Fact]
        public void AFileThatCannotBeRead_LeavesTheDocumentAsItWas()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            CadDocument old = model.Document;
            File.Delete(room);

            Assert.False(model.Reload(room));

            Assert.Same(old, model.Document);
            Assert.True(model.HasDocument);
            Assert.Contains(model.History, line => line.StartsWith("Cannot reload room.dxf"));
        }

        [Fact]
        public void AFileThatIsNotOpen_IsNotReloaded_AndNothingIsOpened()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            string other = _folder.Write("other.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            CadDocument shown = model.Document;

            Assert.False(model.Reload(other));
            Assert.False(model.Reload(Path.Combine(_folder.FullPath, "nothing.dxf")));
            Assert.False(model.Reload(null));

            Assert.Same(shown, model.Document);
            Assert.False(model.FileRows.Single(r => r.Label == "other.dxf").IsOpen);
        }

        [Fact]
        public void ADocumentOpenButNotShown_IsReplacedInItsRow_AndTheOneShownStays()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            string other = _folder.Write("other.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            Assert.True(model.Open(other));
            CadDocument shown = model.Document;
            Rewrite(room, 7);

            Assert.True(model.Reload(room));

            Assert.Same(shown, model.Document);
            Assert.Equal("other.dxf", model.Document.Title);

            model.Activate(model.FileRows.Single(r => r.Label == "room.dxf"));

            Assert.Equal(7, model.Document.EntityCount);
            Assert.Equal("room.dxf", model.Document.Title);
        }

        [Fact]
        public void AReloadedDocument_SavesToItsFileAsAnyOther()
        {
            var model = new MainViewModel();
            string room = _folder.Write("room.dxf", TestFiles.Room(out _, out _));
            Assert.True(model.Open(room));
            Rewrite(room, 4);
            Assert.True(model.Reload(room));
            AddLine(model);

            Assert.True(model.Save());

            Assert.Equal(5, CadDocument.Open(room).EntityCount);
        }

        /// <summary>Writes the room again with more lines, so that it has <paramref name="entities"/> entities.</summary>
        private void Rewrite(string path, int entities)
        {
            DxfDatabase database = TestFiles.Room(out _, out _);

            for (int i = 3; i < entities; i++)
            {
                database.ModelSpace.AppendEntity(
                    new DxfLine(new GeoPoint3(0, 100 * i, 0), new GeoPoint3(4000, 100 * i, 0)) { Layer = "WALLS" }
                );
            }

            database.Save(path);
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
