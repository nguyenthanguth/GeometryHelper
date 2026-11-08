using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>
    /// A model drawing opened again from its file takes a section and a detail picked on the canvas, says the source its file
    /// names, is looked at by CheckSources, and a drawing saved into a folder is listed once.
    /// </summary>
    public class ReopenFollowUpTests
    {
        [Fact]
        public async Task ASectionOfADrawingOpenedAgainIsPickedOnceItIsMadeAgainFromTheSourceItsFileNames()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);
                Assert.True(bench.Model.Open(path));
                ModelDrawing opened = bench.Workspace.Current;
                var later = new List<Action>();
                bench.Workspace.Deferrer = later.Add;
                Assert.Empty(opened.Drawing.Views);

                bench.Model.Execute("VS");

                Assert.Contains("B1 was opened from its file: it is made again from Test bodies first, then the points are picked.", bench.Model.History);
                Assert.False(bench.Model.IsCommandRunning);
                Run(later);
                Assert.Equal(3, opened.Drawing.Views.Count);
                Assert.Equal("UPDATE", opened.Document.NextUndo.Name);
                Assert.True(bench.Model.IsCommandRunning);
                Assert.StartsWith("Specify first point of the cut line in a view", bench.Model.Prompt);

                GeoRectangle2 front = opened.Drawing.Views.Single(v => v.Kind == ViewKind.Front).SheetBox;
                string x = front.Center.X.ToString("R", CultureInfo.InvariantCulture);
                bench.Model.Execute(x + "," + (front.Center.Y + 0.4 * front.Height).ToString("R", CultureInfo.InvariantCulture));
                bench.Model.Execute(x + "," + (front.Center.Y - 0.4 * front.Height).ToString("R", CultureInfo.InvariantCulture));
                bench.Model.Execute("100");
                Run(later);

                Assert.Contains(opened.Drawing.Settings.Views.Sections, s => s.Name == "A");
                Assert.Contains(opened.Drawing.Views, v => v.Kind == ViewKind.Section);
                Assert.Same(bench.Original, opened.Provider);

                // The section is a step of its own: undone, the views made again stay.
                Assert.True(bench.Model.Undo());
                Assert.Equal(3, opened.Drawing.Views.Count);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task ADetailOfADrawingOpenedAgainIsPickedOnceItIsMadeAgain()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);
                Assert.True(bench.Model.Open(path));
                ModelDrawing opened = bench.Workspace.Current;
                var later = new List<Action>();
                bench.Workspace.Deferrer = later.Add;

                bench.Model.Execute("VD");
                Run(later);
                Assert.StartsWith("Specify first corner of the detail in a view", bench.Model.Prompt);

                DrawingView view = opened.Drawing.Views.Single(v => v.Kind == ViewKind.Front);
                GeoRectangle2 front = view.SheetBox;
                bench.Model.Execute(Point(front.Center.X + 0.3 * front.Width, front.Center.Y - 0.4 * front.Height));
                bench.Model.Execute(Point(front.Center.X + 0.48 * front.Width, front.Center.Y + 0.4 * front.Height));
                bench.Model.Execute((view.Scale / 2.0).ToString("R", CultureInfo.InvariantCulture));
                Run(later);

                DrawingView detail = Assert.Single(opened.Drawing.Views, v => v.Kind == ViewKind.Detail);
                Assert.Equal(view.Scale / 2.0, detail.Scale, 6);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task WithNoSourceAtHandASectionOfADrawingOpenedAgainIsRefusedInPlainWords()
        {
            string folder = Bench.NewFolder();

            try
            {
                string path = await SavedAndClosedAsync(new Bench(), folder);
                var model = new MainViewModel();
                var workspace = new DrawingWorkspace(model, new IDrawingSourceProvider[] { UnconnectedSourceProvider.TeklaStructures }, Bench.Tol);
                var later = new List<Action>();
                workspace.Deferrer = later.Add;
                Assert.True(model.Open(path));
                ModelDrawing opened = workspace.Current;
                int entities = DrawingEntityTags.Find(opened.Document.Database).Count;

                model.Execute("VS");
                model.Execute("VD");

                Assert.Equal(2, model.History.Count(l => l == "B1 was opened from its file, and the source it was made from is not at hand: pick its source in Update from model first."));
                Assert.False(model.IsCommandRunning);
                Assert.Empty(later);
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
        public async Task PropertiesOfADrawingOpenedAgainNameTheSourceItsFileKeepsUntilItIsMadeAgain()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);
                Assert.True(bench.Model.Open(path));

                Assert.Equal("Test bodies (from file)", Row(bench.Model, "Source"));

                Assert.True(await bench.Workspace.AddViewAsync(ViewKind.Isometric));

                Assert.Equal("Test bodies", Row(bench.Model, "Source"));
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task CheckSourcesMarksADrawingOpenedAgainOutOfDateWhenTheSourceItsFileNamesHasNewObjectsAndChangesNothing()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                string path = await SavedAndClosedAsync(bench, folder);

                // The source of the name the file keeps gives the beam with a stiffener added to the assembly drawn whole.
                var model = new MainViewModel();
                var workspace = new DrawingWorkspace(model, new IDrawingSourceProvider[] { new Named("Test bodies", bench.Changed) }, Bench.Tol);
                Assert.True(model.Open(path));
                ModelDrawing opened = workspace.Current;
                int entities = DrawingEntityTags.Find(opened.Document.Database).Count;

                workspace.CheckSources();

                Assert.True(opened.IsOutOfDate);
                Assert.Equal(CadFileState.OutOfDate, Assert.Single(model.FileRows, r => !r.IsHeader).State);
                Assert.Empty(opened.Drawing.Views);
                Assert.Null(opened.Drawing.Source);
                Assert.Equal(entities, DrawingEntityTags.Find(opened.Document.Database).Count);
                Assert.False(opened.Document.CanUndo);

                // The source as it was when the drawing was made: up to date.
                var same = new MainViewModel();
                var sameWorkspace = new DrawingWorkspace(same, new IDrawingSourceProvider[] { bench.Original }, Bench.Tol);
                Assert.True(same.Open(path));
                sameWorkspace.CheckSources();
                Assert.False(sameWorkspace.Current.IsOutOfDate);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task CheckSourcesLeavesADrawingOpenedAgainWithNoSourceAtHand()
        {
            string folder = Bench.NewFolder();

            try
            {
                string path = await SavedAndClosedAsync(new Bench(), folder);
                var model = new MainViewModel();
                var workspace = new DrawingWorkspace(model, new IDrawingSourceProvider[] { UnconnectedSourceProvider.TeklaStructures, new Named("Other model", new TestBodiesSourceProvider(Bench.Tol, changed: true)) }, Bench.Tol);
                Assert.True(model.Open(path));

                workspace.CheckSources();

                Assert.False(workspace.Current.IsOutOfDate);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task ADrawingJustMadeAndSavedIntoAFolderIsListedOnceWhenAnotherFileOfTheFolderIsOpened()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                bench.Model.ApplySettings(new GlobalSettings { SaveFolder = folder });
                ModelDrawing made = await bench.MakeAsync();
                string plain = Path.Combine(folder, "plain.dxf");
                var database = new DxfDatabase("AC1027");
                database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
                database.Save(plain);

                Assert.True(bench.Model.Open(plain));

                Assert.Equal(new[] { "plain.dxf", "B1" }, bench.Model.FileRows.Where(r => !r.IsHeader).Select(r => r.Label));
                Assert.DoesNotContain(bench.Model.FileRows, r => string.Equals(r.Path, made.Document.FileName, StringComparison.OrdinalIgnoreCase));
                Assert.Equal("All 2", bench.Model.AllChipText);
                Assert.Equal("DXF 1", bench.Model.DxfChipText);

                // Closed, the drawing's file is listed as a file of the folder again.
                Assert.True(bench.Model.CloseDocument(made.Document));
                Assert.Contains(bench.Model.FileRows, r => string.Equals(r.Path, made.Document.FileName, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task ADrawingSavedWithSaveAndShownAgainByTheWorkspaceIsListedOnce()
        {
            string folder = Bench.NewFolder();

            try
            {
                var bench = new Bench();
                bench.Model.ApplySettings(new GlobalSettings { SaveFolder = folder });
                ModelDrawing first = await bench.MakeAsync();
                Assert.True(bench.Model.Save());
                await bench.MakeAsync("Part", d => d.Number = "B1-P");

                bench.Workspace.Show(first);

                Assert.Equal(new[] { "B1", "B1-P" }, bench.Model.FileRows.Where(r => !r.IsHeader).Select(r => r.Label));
                Assert.Same(first.Document, bench.Model.Document);
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

        /// <summary>Runs the work put off until the command ended, and what that puts off in turn.</summary>
        private static void Run(List<Action> later)
        {
            while (later.Count > 0)
            {
                Action work = later[0];
                later.RemoveAt(0);
                work();
            }
        }

        private static string Point(double x, double y)
            => x.ToString("R", CultureInfo.InvariantCulture) + "," + y.ToString("R", CultureInfo.InvariantCulture);

        private static string Row(MainViewModel model, string label)
            => model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == label).Value;

        /// <summary>A source of another name over the objects of one.</summary>
        private sealed class Named : IDrawingSourceProvider
        {
            private readonly IDrawingSourceProvider _inner;

            public Named(string name, IDrawingSourceProvider inner)
            {
                Name = name;
                _inner = inner;
            }

            public string Name { get; }

            public string Description => "A source for the tests";

            public bool IsAvailable => true;

            public string AvailabilityText => "Built in";

            public IReadOnlyList<DrawingObject> CreateObjects() => _inner.CreateObjects();
        }
    }
}
