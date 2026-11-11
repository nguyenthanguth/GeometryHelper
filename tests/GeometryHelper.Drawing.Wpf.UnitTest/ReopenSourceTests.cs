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
    /// A model drawing opened again from its file keeps the source it was made from, its preset and the assemblies it draws
    /// whole, and its settings are changed from that source; a file open already is shown, not opened twice.
    /// </summary>
    public class ReopenSourceTests
    {
        [Fact]
        public async Task ADrawingOpenedAgainKeepsItsPresetAndWholeAssembliesAndAddsAViewFromTheSourceItsFileNames()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);
                Assert.True(bench.Model.Open(path));
                ModelDrawing opened = bench.Workspace.Current;

                Assert.Null(opened.Drawing.Source);
                Assert.Equal("Assembly", opened.PresetName);
                Assert.Equal(new[] { "B1" }, opened.WholeAssemblies);

                Assert.True(await bench.Workspace.AddViewAsync(ViewKind.Isometric));

                Assert.Empty(bench.Messages);
                Assert.Equal(4, opened.Drawing.Views.Count);
                Assert.Contains(opened.Drawing.Views, v => v.Kind == ViewKind.Isometric);
                Assert.Same(bench.Original, opened.Provider);
                Assert.Equal("Changed", opened.PresetName);
                Assert.Equal("UPDATE", opened.Document.NextUndo.Name);

                // Saved and opened again, the file says so.
                Assert.True(bench.Model.Save());
                Assert.True(bench.Model.CloseDocument(opened.Document));
                Assert.True(bench.Model.Open(path));
                Assert.Equal("Changed", bench.Workspace.Current.PresetName);
                Assert.Equal(new[] { "B1" }, bench.Workspace.Current.WholeAssemblies);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task AutoDimensionOfAViewOfADrawingOpenedAgainDimensionsItInOneUndoStep()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);
                Assert.True(bench.Model.Open(path));
                ModelDrawing opened = bench.Workspace.Current;
                opened.Document.Selection.Set(new[] { Tagged(opened, "V-RIGHT") });

                UpdateReport report = bench.Workspace.ReDimensionSelectedView();

                Assert.NotNull(report);
                Assert.Contains(opened.Drawing.Elements, e => e.Kind == "Dimension" && e.ViewId == "V-RIGHT");
                Assert.Contains("V-RIGHT", opened.Drawing.Settings.Views.DimensionedViews);
                Assert.Equal("UPDATE", opened.Document.NextUndo.Name);
                Assert.Contains(bench.Model.History, l =>
                    l.StartsWith("Dimensions of V-RIGHT: ", StringComparison.Ordinal)
                );
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task AnOlderFileThatNamesNoSourceIsRefusedInPlainWordsUntilUpdateFromModelFindsItsSource()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);
                WithoutValues(path);
                Assert.True(bench.Model.Open(path));
                ModelDrawing opened = bench.Workspace.Current;
                Assert.Equal("Custom", opened.PresetName);
                Assert.Empty(opened.WholeAssemblies);
                int entities = DrawingEntityTags.Find(opened.Document.Database).Count;

                Assert.False(await bench.Workspace.AddViewAsync(ViewKind.Isometric));
                MessageViewModel message = Assert.Single(bench.Messages);
                Assert.Contains(
                    "B1 was opened from its file, and the source it was made from is not at hand: pick its source in Update from model first.",
                    message.Text
                );

                opened.Document.Selection.Set(new[] { Tagged(opened, "V-RIGHT") });
                Assert.Null(bench.Workspace.ReDimensionSelectedView());
                Assert.Contains(
                    "Dimensions of V-RIGHT not made: B1 was opened from its file, and the source it was made from is not at hand: pick its source in Update from model first.",
                    bench.Model.History
                );
                Assert.Equal(entities, DrawingEntityTags.Find(opened.Document.Database).Count);

                // Update from model finds the drawing's objects in the test bodies; Add view is made from them then.
                Assert.NotNull(await bench.Workspace.ShowUpdate().PreviewAsync());
                Assert.True(await bench.Workspace.AddViewAsync(ViewKind.Isometric));
                Assert.Same(bench.Original, opened.Provider);
                Assert.Equal(4, opened.Drawing.Views.Count);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task ASourceOfTheNameWithNoneOfTheObjectsIsRefusedAndTheDrawingIsNotWiped()
        {
            string folder = Bench.NewFolder();

            try
            {
                string path = await SavedAndClosedAsync(new Bench(), folder);
                var model = new MainViewModel();
                var workspace = new DrawingWorkspace(
                    model,
                    new IDrawingSourceProvider[] { new EmptySource("Test bodies") },
                    Bench.Tol
                );
                var messages = new List<MessageViewModel>();
                workspace.DialogRequested += (s, e) => messages.Add(e.Model as MessageViewModel);
                Assert.True(model.Open(path));
                ModelDrawing opened = workspace.Current;
                int entities = DrawingEntityTags.Find(opened.Document.Database).Count;

                Assert.False(await workspace.AddViewAsync(ViewKind.Isometric));

                Assert.Contains(
                    "None of the objects B1 draws is in Test bodies: pick the source it was made from in Update from model first.",
                    Assert.Single(messages).Text
                );
                Assert.Empty(opened.Drawing.Views);
                Assert.Equal(entities, DrawingEntityTags.Find(opened.Document.Database).Count);
                Assert.False(opened.Document.CanUndo);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task UpdateFromModelOfADrawingOpenedAgainChoosesTheSourceItsFileNamesAndDrawsTheNewFittingsOfItsWholeAssemblies()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);
                var model = new MainViewModel();
                var workspace = new DrawingWorkspace(
                    model,
                    new IDrawingSourceProvider[] { bench.Changed, bench.Original },
                    Bench.Tol
                );
                Assert.True(model.Open(path));

                var dialog = new UpdateFromModelViewModel(workspace, workspace.Current);
                Assert.Same(bench.Original, dialog.Source);

                dialog.Source = bench.Changed;
                UpdateReport preview = await dialog.PreviewAsync();

                Assert.Contains("B1-stiffener-1", preview.AddedObjects);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task OpeningTheFileADrawingJustMadeWasSavedAsShowsThatDrawingAndOpensNoSecond()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                bench.Model.ApplySettings(new GlobalSettings { SaveFolder = folder });
                ModelDrawing first = await bench.MakeAsync();
                ModelDrawing second = await bench.MakeAsync("Part", d => d.Number = "B1-P");
                Assert.Same(second, bench.Workspace.Current);
                int rows = bench.Model.FileRows.Count;

                Assert.True(bench.Model.Open(first.Document.FileName));

                Assert.Equal(2, bench.Workspace.Drawings.Count);
                Assert.Same(first, bench.Workspace.Current);
                Assert.Same(first.Document, bench.Model.Document);
                Assert.Equal(rows, bench.Model.FileRows.Count);
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
            Assert.Empty(bench.Workspace.Drawings);
            return path;
        }

        /// <summary>Takes out of a drawing's file what it keeps of its source, as a file written before that was kept.</summary>
        private static void WithoutValues(string path)
        {
            DxfDatabase database = DxfDatabase.Open(path);
            DrawingDocumentStore.WriteValues(database, new KeyValuePair<string, string>[0]);
            database.Save(path);
        }

        private static string Tagged(ModelDrawing drawing, string id) =>
            DrawingEntityTags
                .Find(drawing.Document.Database)
                .First(e => !e.IsErased && DrawingEntityTags.TryRead(e, out string tag, out _) && tag == id)
                .Handle;

        /// <summary>A source that is connected and gives no objects.</summary>
        private sealed class EmptySource : IDrawingSourceProvider
        {
            public EmptySource(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public string Description => "A source for the tests";

            public bool IsAvailable => true;

            public string AvailabilityText => "Built in";

            public IReadOnlyList<DrawingObject> CreateObjects() => Array.Empty<DrawingObject>();
        }
    }
}
