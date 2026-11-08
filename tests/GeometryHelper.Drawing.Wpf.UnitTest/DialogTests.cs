using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>Update from model, Drawing settings and Export, run with no window.</summary>
    public class DialogTests
    {
        [Fact]
        public async Task UpdateFromTheChangedBodiesListsTheChangesKeepsTheHandEditAndIsOneUndoStep()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            string dimension = Bench.FirstOf(made, "Dimension");
            Assert.True(made.Document.Owners.TryApply(made.Document, new CadEdit(dimension, CadEditKind.Move, null, null, "0,25")));

            UpdateFromModelViewModel dialog = bench.Workspace.ShowUpdate();
            Assert.Equal("B1 · Beam B1", dialog.Title);
            dialog.Source = bench.Changed;
            UpdateReport preview = await dialog.PreviewAsync();

            Assert.NotNull(preview);
            Assert.True(dialog.HasChanges);
            Assert.StartsWith("The model changed since ", dialog.Summary);
            Assert.Contains(": 2 changed, 1 added, 0 removed.", dialog.Summary);
            Assert.Contains(dialog.Changes, c => c.Object == "B1 · Beam" && c.Change == ModelChangeKind.Changed && c.Detail == "Length 3 000 → 3 500");
            Assert.Contains(dialog.Changes, c => c.Object == "P1 · End plate" && c.Change == ModelChangeKind.Changed && c.Detail == "Moved 500");
            Assert.Contains(dialog.Changes, c => c.Object == "S1 · Stiffener" && c.Change == ModelChangeKind.Added);
            HandEditRow edit = Assert.Single(dialog.Edits);
            Assert.True(edit.IsKept);
            Assert.StartsWith("Dimension ", edit.Edit);
            Assert.DoesNotContain(made.Drawing.Source.Objects, o => o.Id == "B1-stiffener-1");

            CadUndoStep before = made.Document.NextUndo;
            UpdateReport report = await dialog.UpdateAsync();

            Assert.NotNull(report);
            Assert.Equal(new[] { "B1-stiffener-1" }, report.AddedObjects);
            Assert.Single(report.EditsKept);
            Assert.Equal(4, made.Drawing.Source.Objects.Count);
            Assert.Same(bench.Changed, made.Provider);
            Assert.Equal("UPDATE", made.Document.NextUndo.Name);
            Assert.Contains(bench.Model.History, l => l == "B1 updated: 1 hand edit kept, 0 no longer apply.");

            Assert.True(bench.Model.Undo());
            Assert.Same(before, made.Document.NextUndo);
        }

        [Fact]
        public async Task ApplyingSettingsIsOneUndoStepKeepingAHandEditAndALineDrawnByHandAndUndoTakesTheSettingsBack()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            Drawing first = made.Drawing;
            string dimension = Bench.FirstOf(made, "Dimension");
            Assert.True(made.Document.Owners.TryApply(made.Document, new CadEdit(dimension, CadEditKind.Move, null, null, "0,25")));
            CadDocument document = made.Document;
            Assert.True(document.ApplyEdit("LINE", null, scope => scope.Append(document.ModelSpace, new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(500, 700, 0)))).Succeeded);

            DrawingSettingsViewModel dialog = bench.Workspace.ShowSettings();
            Assert.Equal("B1 · Beam B1 · preset Assembly", dialog.Title);
            Assert.Equal(new[] { "Views", "Scales", "Dimensions", "Marks", "Sheet" }, dialog.Tabs);
            Assert.False(dialog.ApplyCommand.CanExecute(null));
            SettingRow isometric = dialog.Sections.SelectMany(s => s.Rows).Single(r => r.Label == "Isometric");
            isometric.IsOn = true;
            Assert.True(dialog.IsDirty);
            Assert.True(dialog.ApplyCommand.CanExecute(null));

            Assert.True(await dialog.ApplyAsync());

            Assert.Same(first, made.Drawing);
            Assert.Equal("UPDATE", made.Document.NextUndo.Name);
            Assert.Contains(made.Drawing.Views, v => v.Kind == ViewKind.Isometric);
            Assert.Single(made.Drawing.Edits);
            Assert.Same(made.Document, bench.Model.Document);
            Assert.False(dialog.IsDirty);
            Assert.Equal("Made again: 1 hand edit kept.", dialog.Notice);
            Assert.Contains(made.Document.Database.ModelSpace.Cast<DxfEntity>(), e => e is DxfLine line && !e.IsErased
                && !DrawingEntityTags.TryRead(e, out _, out _) && Math.Abs(line.EndPoint.Y - 700.0) < Bench.Tol.EqualPoint);
            Assert.Equal("Changed", made.PresetName);

            // Undo takes the step back: the settings, the views and the document as they were, the hand edit with them.
            Assert.True(bench.Model.Undo());
            Assert.DoesNotContain(ViewKind.Isometric, made.Drawing.Settings.Views.Kinds);
            Assert.Single(made.Drawing.Edits);
            Assert.Contains(made.Document.Database.ModelSpace.Cast<DxfEntity>(), e => e is DxfLine && !e.IsErased && !DrawingEntityTags.TryRead(e, out _, out _));
        }

        [Fact]
        public async Task ANumberTheSettingsRefuseShowsWhyAndApplyWaits()
        {
            var bench = new Bench();
            await bench.MakeAsync();
            DrawingSettingsViewModel dialog = bench.Workspace.ShowSettings();
            dialog.Tab = "Dimensions";
            SettingRow height = dialog.Sections.SelectMany(s => s.Rows).Single(r => r.Label == "Text height");

            height.Value = "-1";
            Assert.True(height.HasError);
            Assert.True(dialog.HasErrors);
            Assert.False(dialog.ApplyCommand.CanExecute(null));
            Assert.False(dialog.OkCommand.CanExecute(null));

            height.Value = "abc";
            Assert.Equal("Not a number.", height.Error);

            height.Value = "3.5";
            Assert.False(height.HasError);
            Assert.Equal(3.5, dialog.Settings.Dimensions.TextHeight);
            Assert.True(dialog.ApplyCommand.CanExecute(null));
        }

        [Fact]
        public async Task TheLastScaleAllowedCannotBeTurnedOff()
        {
            var bench = new Bench();
            await bench.MakeAsync();
            DrawingSettingsViewModel dialog = bench.Workspace.ShowSettings();
            dialog.Tab = "Scales";
            var chips = dialog.Sections[0].Rows[0].Chips;

            foreach (SettingChip chip in chips)
            {
                chip.IsOn = false;
            }

            Assert.Single(chips, c => c.IsOn);
            Assert.Single(dialog.Settings.Scales);
            Assert.Equal("One scale at least is allowed.", dialog.Notice);
        }

        [Fact]
        public async Task ExportWritesOneDxfPerDrawingThatOpensAgainAsAModelDrawing()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            string folder = Bench.NewFolder();

            try
            {
                ExportViewModel dialog = bench.Workspace.ShowExport();
                dialog.Folder = folder;
                Assert.Equal("1 of 1 selected", dialog.SelectedText);
                Assert.Equal("Export 1", dialog.ExportText);
                Assert.Equal("AutoCAD 2013 (AC1027)", dialog.Version);
                Assert.True(dialog.LayoutAndViewports);

                var written = await dialog.ExportAsync();

                string path = Assert.Single(written);
                Assert.Equal(Path.Combine(folder, "B1 Beam B1.dxf"), path);
                Drawing again = Drawing.Open(path, null, Bench.Tol);
                Assert.Equal(made.Drawing.Id, again.Id);
                Assert.Equal(DrawingExportMode.LayoutAndViewports, again.Settings.ExportMode);
                MessageViewModel done = bench.Messages.Single();
                Assert.Equal(MessageKind.Done, done.Kind);
                Assert.Equal("Open folder", done.ActionText);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }

        [Fact]
        public async Task ExportingTheSheetInModelSpaceLeavesTheDrawingShownAsItIs()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            string dimension = Bench.FirstOf(made, "Dimension");
            Assert.True(made.Document.Owners.TryApply(made.Document, new CadEdit(dimension, CadEditKind.Move, null, null, "0,25")));
            Drawing shown = made.Drawing;
            string folder = Bench.NewFolder();

            try
            {
                ExportViewModel dialog = bench.Workspace.ShowExport();
                dialog.Folder = folder;
                dialog.FileNames = "<Number>-<Revision>";
                dialog.SheetInModelSpace = true;

                string path = Assert.Single(await dialog.ExportAsync());

                Assert.Equal(Path.Combine(folder, "B1-A.dxf"), path);
                Drawing again = Drawing.Open(path, null, Bench.Tol);
                Assert.Equal(DrawingExportMode.SheetInModelSpace, again.Settings.ExportMode);
                Assert.Single(again.Edits);
                Assert.Same(shown, made.Drawing);
                Assert.Equal(DrawingExportMode.LayoutAndViewports, made.Drawing.Settings.ExportMode);
            }
            finally
            {
                Bench.Delete(folder);
            }
        }
    }
}
