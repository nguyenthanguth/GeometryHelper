using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>
    /// A drawing changed while it is being made again, closed meanwhile, made again from a source that fails, and undone
    /// back past the change of its preset.
    /// </summary>
    public class WorkspaceSafetyTests
    {
        [Fact]
        public async Task AViewAskedForWhileTheDrawingIsMadeAgainIsRefusedInWordsAndNoViewIsLost()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            var gate = new TaskCompletionSource<bool>();
            DrawingSettings settings = made.Drawing.Settings;
            settings.Views.Kinds.Add(ViewKind.Isometric);
            Task<UpdateReport> first = bench.Workspace.ApplySettingsAsync(
                made,
                settings,
                "Changed",
                async (text, work) =>
                {
                    await gate.Task;
                    return work();
                }
            );

            Assert.False(await bench.Workspace.AddViewAsync(ViewKind.Back));
            made.Document.Selection.Set(new[] { Bench.FirstOf(made, "View") });
            Assert.Null(bench.Workspace.ReDimensionSelectedView());

            Assert.Equal(2, bench.Model.History.Count(l => l == "B1 is being made again; wait for it."));
            gate.SetResult(true);
            Assert.NotNull(await first);
            Assert.True(await bench.Workspace.AddViewAsync(ViewKind.Back));

            Assert.Contains(made.Drawing.Views, v => v.Kind == ViewKind.Isometric);
            Assert.Contains(made.Drawing.Views, v => v.Kind == ViewKind.Back);
            Assert.Empty(bench.Messages);
        }

        [Fact]
        public async Task ADrawingClosedWhileItIsMadeAgainIsNotWrittenInto()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            var gate = new TaskCompletionSource<bool>();
            DrawingSettings settings = made.Drawing.Settings;
            settings.Views.Kinds.Add(ViewKind.Isometric);
            Task<UpdateReport> late = bench.Workspace.ApplySettingsAsync(
                made,
                settings,
                "Changed",
                async (text, work) =>
                {
                    await gate.Task;
                    return work();
                }
            );
            bench.Model.AskSaveChanges = d => CadSaveChoice.DontSave;
            Assert.True(bench.Model.CloseDocument(made.Document));

            gate.SetResult(true);
            await Assert.ThrowsAsync<OperationCanceledException>(() => late);

            Assert.DoesNotContain(made.Drawing.Views, v => v.Kind == ViewKind.Isometric);
            Assert.False(made.Document.CanUndo);
            Assert.Equal("Assembly", made.PresetName);
            Assert.Contains("B1 was closed while it was made again; nothing was written.", bench.Model.History);
        }

        [Fact]
        public async Task AnErrorOfAnyKindFromTheSourceIsSaidOnTheCommandLineAndDoesNotEscape()
        {
            string folder = Bench.NewFolder();

            try
            {
                string path = await SavedAndClosedAsync(new Bench(), folder);
                var model = new MainViewModel();
                var workspace = new DrawingWorkspace(
                    model,
                    new IDrawingSourceProvider[] { new StoppedSource("Test bodies") },
                    Bench.Tol
                );
                Assert.True(model.Open(path));
                ModelDrawing opened = workspace.Current;
                opened.Document.Selection.Set(new[] { Tagged(opened, "V-RIGHT") });
                int entities = DrawingEntityTags.Find(opened.Document.Database).Count;

                Assert.Null(workspace.ReDimensionSelectedView());

                Assert.Contains("Dimensions of V-RIGHT not made: The model server stopped answering.", model.History);
                Assert.Equal(entities, DrawingEntityTags.Find(opened.Document.Database).Count);
                Assert.False(opened.Document.CanUndo);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task UndoingTheFirstChangeOfADrawingOpenedAgainTakesItsPresetBackInTheFileAndInProperties()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);
                Assert.True(bench.Model.Open(path));
                ModelDrawing opened = bench.Workspace.Current;
                string Row(string label) =>
                    bench.Model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == label).Value;
                Assert.Equal("Assembly", Row("Preset"));

                Assert.True(await bench.Workspace.AddViewAsync(ViewKind.Isometric));
                Assert.Equal("Changed", Preset(opened));
                Assert.Equal("Changed", opened.PresetName);

                Assert.True(opened.Document.Undo());
                Assert.Equal("Assembly", Preset(opened));
                Assert.Equal("Assembly", opened.PresetName);
                Assert.Equal("Assembly", Row("Preset"));

                Assert.True(opened.Document.Redo());
                Assert.Equal("Changed", Preset(opened));
                Assert.Equal("Changed", opened.PresetName);
                Assert.Equal("Changed", Row("Preset"));
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task ExportWritesTheBytesTheDrawingSavesAndLeavesTheDatabaseShownWithItsOwnFile()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            string folder = Bench.NewFolder();

            try
            {
                string own = made.Drawing.Database.Filename;
                ExportViewModel dialog = bench.Workspace.ShowExport();
                dialog.Folder = Path.Combine(folder, "export");

                string path = Assert.Single(await dialog.ExportAsync());

                Assert.Equal(own, made.Drawing.Database.Filename);
                string saved = Path.Combine(folder, "saved.dxf");
                made.Drawing.Save(saved);
                Assert.Equal(File.ReadAllBytes(saved), File.ReadAllBytes(path));
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        /// <summary>Makes the drawing of the beam, saved into the folder as Global settings ask, and closes it; gives its path.</summary>
        private static async Task<string> SavedAndClosedAsync(Bench bench, string folder)
        {
            bench.Model.ApplySettings(new GlobalSettings { SaveFolder = folder });
            ModelDrawing made = await bench.MakeAsync();
            string path = made.Document.FileName;
            Assert.True(File.Exists(path));
            Assert.True(bench.Model.CloseDocument(made.Document));
            return path;
        }

        private static string Preset(ModelDrawing drawing) =>
            DrawingDocumentStore.ReadValues(drawing.Document.Database).Single(v => v.Key == "Preset").Value;

        private static string Tagged(ModelDrawing drawing, string id) =>
            DrawingEntityTags
                .Find(drawing.Document.Database)
                .First(e => !e.IsErased && DrawingEntityTags.TryRead(e, out string tag, out _) && tag == id)
                .Handle;

        /// <summary>The error a source of a model server gives when the server stops answering: of no kind the workspace expects.</summary>
        private sealed class ServerStoppedException : Exception
        {
            public ServerStoppedException()
                : base("The model server stopped answering.") { }
        }

        /// <summary>A source that is connected, and whose server stops answering when asked for its objects.</summary>
        private sealed class StoppedSource : IDrawingSourceProvider
        {
            public StoppedSource(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public string Description => "A source for the tests";

            public bool IsAvailable => true;

            public string AvailabilityText => "Built in";

            public IReadOnlyList<DrawingObject> CreateObjects() => throw new ServerStoppedException();
        }
    }
}
