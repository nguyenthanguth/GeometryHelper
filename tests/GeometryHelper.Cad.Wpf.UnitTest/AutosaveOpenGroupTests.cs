using System;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Autosave and a drawing in the middle of an undo group (D720): a script waiting at a prompt holds its run's group open,
    /// the drawing then holds half a command's edits, and a copy written then would keep them. The copy is left for the next
    /// autosave, as a drawing with a command running is.
    /// </summary>
    public sealed class AutosaveOpenGroupTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private string At(string name) => Path.Combine(_folder.FullPath, name);

        private MainViewModel Opened(string name)
        {
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings { AutosaveMinutes = 5 });
            Assert.True(model.Open(_folder.Write(name, TestFiles.Room(out _, out _))));
            return model;
        }

        private static void AddLine(CadDocument document, double y)
        {
            Assert.True(
                document
                    .ApplyEdit("draw", null, scope =>
                        scope.Append(document.ModelSpace, new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(10, y, 0)))
                    )
                    .Succeeded
            );
        }

        [Fact]
        public void ADrawingInTheGroupOfARunWaitingAtAPrompt_IsLeftOutOfTheAutosave_AndSavedOnceTheGroupEnds()
        {
            MainViewModel model = Opened("room.dxf");
            CadDocument document = model.Document;

            // The run's group is open with one line drawn in it: the room has 3 entities, the drawing 4 so far.
            document.BeginGroup("PYRUN");
            AddLine(document, 1);
            Assert.True(document.IsInGroup);
            Assert.True(document.IsModified);

            Assert.Empty(model.Autosave());
            Assert.False(File.Exists(At("room.autosave.dxf")));

            document.EndGroup();

            Assert.Equal(new[] { At("room.autosave.dxf") }, model.Autosave());
            Assert.Equal(4, CadDocument.Open(At("room.autosave.dxf")).EntityCount);
        }

        [Fact]
        public void ARunsGroupCancelled_LeavesTheDrawingUnchanged_AndNothingToAutosave()
        {
            MainViewModel model = Opened("room.dxf");
            CadDocument document = model.Document;

            // A failed run: its line is drawn, then the group is cancelled (D705): the drawing is the room's 3 entities again.
            document.BeginGroup("PYRUN");
            AddLine(document, 1);
            document.CancelGroup();

            Assert.False(document.IsInGroup);
            Assert.Empty(model.Autosave());
            Assert.False(File.Exists(At("room.autosave.dxf")));
        }

        [Fact]
        public void OnlyTheDrawingInTheGroupIsSkipped_AnotherWithChangesIsSaved()
        {
            MainViewModel model = Opened("a.dxf");
            CadDocument inGroup = model.Document;
            inGroup.BeginGroup("PYRUN");
            AddLine(inGroup, 1);

            Assert.True(model.Open(_folder.Write("b.dxf", TestFiles.Room(out _, out _))));
            AddLine(model.Document, 2);

            Assert.Equal(new[] { At("b.autosave.dxf") }, model.Autosave());
            Assert.False(File.Exists(At("a.autosave.dxf")));
            Assert.True(inGroup.IsInGroup);
        }
    }
}
