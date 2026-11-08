using System;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf.Viewer.Wpf;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>The sources of the New drawing dialog, and its three steps run with no window.</summary>
    public class SourceAndNewDrawingTests
    {
        [Fact]
        public void TheTestBodiesAreThreeAssembliesOfClosedBodiesInTheOrderOfTheirIds()
        {
            var objects = new TestBodiesSourceProvider(Bench.Tol).CreateObjects();

            Assert.Equal(16, objects.Count);
            Assert.Equal(objects.Select(o => o.Id).OrderBy(i => i, StringComparer.Ordinal), objects.Select(o => o.Id));
            Assert.Equal(new[] { "B1-beam", "C1-column", "F1-beam" }, objects.Where(o => o.IsMain).Select(o => o.Id));
            Assert.Equal(new[] { "B1", "C1", "F1" }, objects.Select(o => o.ParentId).Distinct());
            Assert.All(objects, o => Assert.True(o.Solid.IsClosed(Bench.Tol), o.Id + " is not closed"));
            Assert.Equal(4, objects.Count(o => o.Kind == DrawingObjectKind.Bolt));
        }

        [Fact]
        public void TheChangedTestBodiesHaveTheBeamMade3500LongAndAStiffenerAdded()
        {
            var before = new TestBodiesSourceProvider(Bench.Tol).CreateObjects();
            var after = new TestBodiesSourceProvider(Bench.Tol, changed: true).CreateObjects();

            Assert.Equal(3000.0, SourceComparer.LengthAlongX(before.Single(o => o.Id == "B1-beam")), 6);
            Assert.Equal(3500.0, SourceComparer.LengthAlongX(after.Single(o => o.Id == "B1-beam")), 6);
            Assert.Equal(new[] { "B1-stiffener-1" }, after.Select(o => o.Id).Except(before.Select(o => o.Id)));
            Assert.True(SourceComparer.Same(before.Single(o => o.Id == "C1-column"), after.Single(o => o.Id == "C1-column")));
            Assert.False(SourceComparer.Same(before.Single(o => o.Id == "B1-plate-2"), after.Single(o => o.Id == "B1-plate-2")));
        }

        [Fact]
        public void TeklaAndIfcAreListedButNotConnected()
        {
            foreach (UnconnectedSourceProvider source in new[] { UnconnectedSourceProvider.TeklaStructures, UnconnectedSourceProvider.IfcFile })
            {
                Assert.False(source.IsAvailable);
                Assert.StartsWith("Not connected", source.AvailabilityText);
                Assert.Throws<InvalidOperationException>(() => source.CreateObjects());
            }
        }

        [Fact]
        public void TheDialogStartsOnTheTestBodiesAndASourceNotConnectedCannotBeChosen()
        {
            var bench = new Bench();
            NewDrawingViewModel dialog = bench.Workspace.ShowNewDrawing();

            Assert.Same(dialog, Assert.Single(bench.Requests).Model);
            Assert.Equal(DrawingDialogKind.NewDrawing, bench.Requests[0].Kind);
            Assert.Equal(new[] { "Test bodies", "Test bodies, changed", "Tekla Structures", "IFC file…" }, dialog.Sources.Select(s => s.Name));
            Assert.Same(bench.Original, dialog.SelectedSource.Provider);

            dialog.Sources[2].IsSelected = true;

            Assert.False(dialog.Sources[2].IsSelected);
            Assert.Same(bench.Original, dialog.SelectedSource.Provider);
            Assert.True(dialog.IsSourceStep);
            Assert.Equal("Next", dialog.NextLabel);
            Assert.True(dialog.NextCommand.CanExecute(null));
        }

        [Fact]
        public async Task TheAssemblyPresetTicksTheBeamAndItsEndPlatesAndNamesTheDrawingAfterIt()
        {
            var bench = new Bench();
            NewDrawingViewModel dialog = bench.Workspace.ShowNewDrawing();

            await dialog.NextAsync();

            Assert.True(dialog.IsObjectsStep);
            Assert.Equal(16, dialog.Objects.Count);
            Assert.Equal(new[] { "B1-beam", "B1-plate-1", "B1-plate-2" }, dialog.Objects.Where(o => o.IsChecked).Select(o => o.Object.Id));
            Assert.Equal("B1", dialog.Number);
            Assert.Equal("Beam B1", dialog.Name);
            Assert.Equal("Picked: 1 assembly · 3 parts", dialog.PickedText);
            Assert.Equal("A2", dialog.SheetSize);
        }

        [Fact]
        public async Task ThePartPresetTicksOnePartTheGeneralPresetEveryObjectAndAPickedRowChoosesTheAssembly()
        {
            var bench = new Bench();
            NewDrawingViewModel dialog = bench.Workspace.ShowNewDrawing();
            await dialog.NextAsync();

            dialog.Preset = "Part";
            Assert.Equal(new[] { "B1-beam" }, dialog.Objects.Where(o => o.IsChecked).Select(o => o.Object.Id));
            Assert.Equal("A3", dialog.SheetSize);

            dialog.Preset = "General";
            Assert.All(dialog.Objects, o => Assert.True(o.IsChecked));
            Assert.Equal("GA-1", dialog.Number);
            Assert.Equal("A1", dialog.SheetSize);

            dialog.Preset = "Assembly";
            dialog.FocusedObject = dialog.Objects.Single(o => o.Object.Id == "C1-bolt-2");
            Assert.Equal(10, dialog.Objects.Count(o => o.IsChecked));
            Assert.All(dialog.Objects.Where(o => o.IsChecked), o => Assert.Equal("C1", o.Object.ParentId));
            Assert.Equal("C1", dialog.Number);
            Assert.Equal("Column C1", dialog.Name);
        }

        [Fact]
        public async Task ATickChangedByHandIsKeptWhenThePresetChanges()
        {
            var bench = new Bench();
            NewDrawingViewModel dialog = bench.Workspace.ShowNewDrawing();
            await dialog.NextAsync();

            dialog.Objects.Single(o => o.Object.Id == "B1-plate-2").IsChecked = false;
            dialog.Preset = "General";

            Assert.Equal(new[] { "B1-beam", "B1-plate-1" }, dialog.Objects.Where(o => o.IsChecked).Select(o => o.Object.Id));
            Assert.Equal("General", dialog.Preset);
        }

        [Fact]
        public async Task CreateMakesTheDrawingAndShowsItOnItsSheetWithTheDrawingTab()
        {
            var bench = new Bench();
            NewDrawingViewModel dialog = bench.Workspace.ShowNewDrawing();
            bool? closed = null;
            dialog.CloseRequested += (s, done) => closed = done;
            await dialog.NextAsync();
            await dialog.NextAsync();
            dialog.Number = "B-101";

            Assert.True(dialog.IsSettingsStep);
            Assert.Equal("Create", dialog.NextLabel);
            Assert.Equal("Front, Top, Right end", dialog.SummaryViews);

            ModelDrawing made = await dialog.NextAsync();

            Assert.True(closed);
            Assert.Same(made, dialog.Result);
            Assert.Same(made, bench.Workspace.Current);
            Assert.Same(made.Document, bench.Model.Document);
            Assert.Equal("Sheet", bench.Model.Space.Name);
            Assert.Equal("B-101", made.Number);
            Assert.Equal("Beam B1", made.Name);
            Assert.Equal("A", made.Revision);
            Assert.Equal("Assembly", made.PresetName);
            Assert.Equal(new[] { "B1" }, made.WholeAssemblies);
            Assert.True(bench.Workspace.IsTabVisible);
            Assert.Equal("B-101 · Beam B1 · Sheet", bench.Model.Title);
            Assert.Equal("Source: Test bodies · connected", bench.Model.StatusText);
            Assert.Contains(bench.Model.History, l => l.StartsWith("Made B-101 (Beam B1) from Test bodies", StringComparison.Ordinal));
        }

        [Fact]
        public async Task StopDropsTheObjectsBeingReadAndLeavesTheDialogOnItsStep()
        {
            var slow = new SlowSource();
            var bench = new Bench(slow);
            NewDrawingViewModel dialog = bench.Workspace.ShowNewDrawing();
            dialog.Sources.Single(s => s.Provider == slow).IsSelected = true;

            Task<ModelDrawing> next = dialog.NextAsync();

            Assert.True(dialog.IsBusy);
            Assert.Equal("Reading the objects of Slow source…", dialog.BusyText);
            Assert.True(dialog.StopCommand.CanExecute(null));
            dialog.StopCommand.Execute(null);
            Assert.Null(await next);
            slow.Go();

            Assert.False(dialog.IsBusy);
            Assert.True(dialog.IsSourceStep);
            Assert.Equal("Stopped: the objects were not read.", dialog.Notice);
            Assert.Empty(dialog.Objects);
        }

        [Fact]
        public async Task ASourceThatFailsIsSaidInAnErrorMessage()
        {
            var broken = new SlowSource(fails: true);
            var bench = new Bench(broken);
            NewDrawingViewModel dialog = bench.Workspace.ShowNewDrawing();
            dialog.Sources.Single(s => s.Provider == broken).IsSelected = true;

            await dialog.NextAsync();

            MessageViewModel message = Assert.Single(bench.Messages);
            Assert.Equal(MessageKind.Error, message.Kind);
            Assert.Equal("Objects not read", message.Title);
            Assert.Contains("The model is locked.", message.Text);
            Assert.StartsWith("InvalidOperationException", message.Detail);
            Assert.True(dialog.IsSourceStep);
        }
    }
}
