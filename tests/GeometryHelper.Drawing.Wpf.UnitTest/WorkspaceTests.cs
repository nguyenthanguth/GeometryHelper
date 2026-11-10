using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>The model drawing in the presenter: commands, the Drawing tab, the file list and Properties.</summary>
    public class WorkspaceTests
    {
        [Fact]
        public void DrawingNewTypedWithNoDocumentAndCtrlNAskForTheNewDrawingDialog()
        {
            var bench = new Bench();

            bench.Model.Execute("DN");
            Assert.True(bench.Model.NewFromModelCommand.CanExecute(null));
            bench.Model.NewFromModelCommand.Execute(null);

            Assert.Equal(2, bench.Requests.Count);
            Assert.All(bench.Requests, r => Assert.IsType<NewDrawingViewModel>(r.Model));
            Assert.DoesNotContain(bench.Model.History, l => l.StartsWith("No drawing is open", StringComparison.Ordinal));
        }

        [Fact]
        public void ThePresenterOfATest_KeepsItsSettingsInAFileOfTheTests_NotTheUsers()
        {
            var bench = new Bench();

            Assert.NotEqual(GlobalSettings.DefaultPath, bench.Model.SettingsPath);
            Assert.StartsWith(System.IO.Path.GetTempPath(), bench.Model.SettingsPath);
        }

        [Fact]
        public void TheStartScreenOfGeometryHelperDrawing_KeepsNewDrawingFromAModel_AndCtrlNAsksForIt()
        {
            var bench = new Bench();

            Assert.True(bench.Model.CanMakeFromModel);
            Assert.Equal("Open and edit any 2D drawing, or make drawings from a model. No file is open.", bench.Model.StartScreenText);
            Assert.Equal(new[] { MainViewModel.WelcomeText }, bench.Model.RecentHistory);
            Assert.Contains(bench.Model.CreateAppMenu("GeometryHelper Drawing").Items[0].Items, i => i.Name == "Drawing from a model");

            bench.Model.NewByShortcut();

            Assert.IsType<NewDrawingViewModel>(Assert.Single(bench.Requests).Model);
            Assert.False(bench.Model.HasDocument);
        }

        [Fact]
        public void TheModelDrawingCommandsOnAPlainDxfSayTheyWorkOnAModelDrawing()
        {
            var bench = new Bench();
            bench.Model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "plain.dxf"));

            bench.Model.Execute("DU");
            bench.Model.Execute("DS");
            bench.Model.Execute("AD");

            Assert.Empty(bench.Requests);
            Assert.Contains("DRAWINGUPDATE works on a model drawing; plain.dxf is a DXF.", bench.Model.History);
            Assert.Contains("DRAWINGSETTINGS works on a model drawing; plain.dxf is a DXF.", bench.Model.History);
            Assert.Contains("AUTODIM works on a model drawing; plain.dxf is a DXF.", bench.Model.History);
            Assert.False(bench.Workspace.IsTabVisible);
        }

        [Fact]
        public async Task TheDrawingTabShowsOnlyWhileAModelDrawingIsAndItsCommandsRunFromTheCommandLine()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            bench.Model.SelectedTab = bench.Workspace;
            Assert.True(bench.Workspace.IsTabSelected);
            Assert.False(bench.Model.IsHomeTabSelected);

            bench.Model.Execute("DU");
            bench.Model.Execute("DS");
            bench.Model.Execute("EXD");
            bench.Model.Execute("AD");
            bench.Model.Execute("VS");

            Assert.Equal(new[] { DrawingDialogKind.NewDrawing, DrawingDialogKind.Update, DrawingDialogKind.Settings, DrawingDialogKind.Export }, bench.Requests.Select(r => r.Kind));
            Assert.Contains("Select a view first: its frame, or anything in it.", bench.Model.History);
            Assert.StartsWith("Specify first point of the cut line", bench.Model.Prompt);
            bench.Model.Escape();
            Assert.True(bench.Workspace.AutoDimensionCommand.CanExecute(null));
            Assert.True(bench.Workspace.UpdateCommand.CanExecute(null));

            bench.Model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "plain.dxf"));

            Assert.False(bench.Workspace.IsTabVisible);
            Assert.True(bench.Model.IsHomeTabSelected);
            Assert.Null(bench.Workspace.Current);
            Assert.False(bench.Workspace.UpdateCommand.CanExecute(null));

            bench.Workspace.Show(made);

            Assert.True(bench.Workspace.IsTabVisible);
        }

        [Fact]
        public async Task TheFileListShowsAModelDrawingUnderItsSourceWithItsDot()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();

            FileRowViewModel header = bench.Model.FileRows.Single(r => r.IsHeader);
            FileRowViewModel row = bench.Model.FileRows.Single(r => !r.IsHeader);
            Assert.Equal("Test bodies · model drawings", header.Label);
            Assert.Equal("B1", row.Label);
            Assert.Equal("Beam B1 · rev A", row.Detail);
            Assert.Equal("A2", row.Right);
            Assert.Equal(CadFileState.UpToDate, row.State);
            Assert.True(row.IsCurrent);
            Assert.True(bench.Model.HasOwnFiles);
            Assert.Equal("Model drawings 1", bench.Model.OwnChipText);
            Assert.Equal("Out of date 0", bench.Model.OutOfDateChipText);
            Assert.Equal("DXF 0", bench.Model.DxfChipText);

            string mark = Bench.FirstOf(made, "Mark") ?? Bench.FirstOf(made, "Dimension");
            Assert.True(made.Document.Owners.TryApply(made.Document, new CadEdit(mark, CadEditKind.Move, null, null, "40,0")));

            Assert.Equal(CadFileState.Edited, bench.Model.FileRows.Single(r => !r.IsHeader).State);
            Assert.Equal("Edited · 1 hand edit", made.StatusText);

            bench.Model.FilterDxf = true;
            Assert.Empty(bench.Model.FileRows);
            bench.Model.FilterOwn = true;
            Assert.Equal(2, bench.Model.FileRows.Count);
        }

        [Fact]
        public async Task WithNothingSelectedPropertiesShowTheDrawing()
        {
            var bench = new Bench();
            await bench.MakeAsync();

            Assert.Equal("Drawing B1", bench.Model.SelectionLabel);
            Assert.Equal(new[] { "Drawing", "Sheet", "Title block", "Source" }, bench.Model.PropertyGroups.Select(g => g.Title));
            Assert.Null(bench.Model.PropertiesBanner);
            string Row(string label) => bench.Model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == label).Value;
            Assert.Equal("B1", Row("Number"));
            Assert.Equal("Beam B1", Row("Name"));
            Assert.Equal("Assembly", Row("Preset"));
            Assert.Equal("Up to date", Row("Status"));
            Assert.Equal("A2", Row("Size"));
            Assert.Equal("Third angle", Row("Projection"));
            Assert.Equal("Test bodies", Row("Source"));
            Assert.Equal("3 parts", Row("Objects"));
            Assert.All(bench.Model.PropertyGroups.SelectMany(g => g.Rows), r => Assert.True(r.IsReadOnly));
        }

        [Fact]
        public async Task ASelectedDimensionShowsTheOwnerBannerAndItsModelLink()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            string dimension = Bench.FirstOf(made, "Dimension");
            Assert.NotNull(dimension);

            made.Document.Selection.Set(new[] { dimension });

            Assert.Equal("Owned by drawing B1. You can move it, edit its text and points. Its value follows the model.", bench.Model.PropertiesBanner);
            PropertyGroupViewModel link = bench.Model.PropertyGroups.Last();
            Assert.Equal("Model link", link.Title);
            Assert.Equal(new[] { "Object", "Measures", "Value", "Edited" }, link.Rows.Select(r => r.Label));
            Assert.Contains("BEAM", link.Rows[0].Value);
            Assert.Equal("No", link.Rows[3].Value);
            Assert.Contains(bench.Model.PropertyGroups, g => g.Title == "General");
        }

        [Fact]
        public async Task ASelectedViewShowsItsGroupsAndTheBannerOfAView()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            string view = Bench.FirstOf(made, "View");

            made.Document.Selection.Set(new[] { view });

            Assert.Equal("View (1)", bench.Model.SelectionLabel);
            Assert.Equal("Part of drawing B1. The lines of a view come from the model: change the view, not the lines.", bench.Model.PropertiesBanner);
            Assert.Equal(new[] { "View", "Visible area", "Display", "Content" }, bench.Model.PropertyGroups.Select(g => g.Title));
            Assert.Equal("Along an axis (like MVIEW)", bench.Model.PropertyGroups[0].Rows.Single(r => r.Label == "Kind").Value);
            Assert.StartsWith("1:", bench.Model.PropertyGroups[0].Rows.Single(r => r.Label == "Scale").Value);

            made.Document.Selection.Clear();

            Assert.Null(bench.Model.PropertiesBanner);
            Assert.Equal("Drawing B1", bench.Model.SelectionLabel);
        }

        [Fact]
        public async Task ErasingALineOfTheModelIsRefusedAndSaidOnTheCommandLine()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            bench.Model.SelectSpace(made.Document.ModelSpace);
            string line = Bench.LineOf(made, "B1-beam");
            Assert.NotNull(line);

            made.Document.Selection.Set(new[] { line });
            Assert.StartsWith("Part of drawing B1. This line belongs to the model", bench.Model.PropertiesBanner);
            bench.Model.StartCommand("ERASE");

            Assert.Contains(bench.Model.History, l => l.Contains("belongs to the model"));
            Assert.NotNull(made.Document.FindEntity(line));
        }

        [Fact]
        public async Task AddViewMakesTheDrawingAgainWithTheIsometricViewAndKeepsAHandEdit()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            string dimension = Bench.FirstOf(made, "Dimension");
            Assert.True(made.Document.Owners.TryApply(made.Document, new CadEdit(dimension, CadEditKind.Move, null, null, "0,25")));
            Assert.Contains(ViewKind.Isometric, bench.Workspace.AddableViews);

            Assert.True(await bench.Workspace.AddViewAsync(ViewKind.Isometric));

            Assert.Contains(made.Drawing.Views, v => v.Kind == ViewKind.Isometric);
            Assert.DoesNotContain(ViewKind.Isometric, bench.Workspace.AddableViews);
            Assert.Single(made.Drawing.Edits);
            Assert.Same(made.Document, bench.Model.Document);
            Assert.Equal("Changed", made.PresetName);
            Assert.Contains(bench.Model.History, l => l.StartsWith("B1 made again with the new settings: 1 hand edit kept", StringComparison.Ordinal));
            Assert.Equal("UPDATE", made.Document.NextUndo.Name);
            Assert.False(await bench.Workspace.AddViewAsync(ViewKind.Section));
            Assert.Contains("A section is drawn by VIEWSECTION on the canvas.", bench.Model.History);
        }

        [Fact]
        public async Task AutoDimensionOfTheSelectedViewDimensionsItInOneUndoStep()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            Assert.Null(bench.Workspace.ReDimensionSelectedView());
            made.Document.Selection.Set(new[] { Tagged(made, "V-RIGHT") });
            Assert.Equal("V-RIGHT", bench.Workspace.SelectedViewId());
            Assert.DoesNotContain(made.Drawing.Elements, e => e.Kind == "Dimension" && e.ViewId == "V-RIGHT");

            UpdateReport report = bench.Workspace.ReDimensionSelectedView();

            Assert.NotNull(report);
            Assert.Contains(made.Drawing.Elements, e => e.Kind == "Dimension" && e.ViewId == "V-RIGHT");
            Assert.Equal("UPDATE", made.Document.NextUndo.Name);
            Assert.Contains(bench.Model.History, l => l.StartsWith("Dimensions of V-RIGHT: ", StringComparison.Ordinal));

            Assert.True(bench.Model.Undo());
            Assert.DoesNotContain(made.Drawing.Elements, e => e.Kind == "Dimension" && e.ViewId == "V-RIGHT");
        }

        [Fact]
        public async Task AutoMarkTypedRunsOnceTheCommandHasEnded()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            var later = new List<Action>();
            bench.Workspace.Deferrer = later.Add;
            made.Document.Selection.Set(new[] { Tagged(made, "V-TOP") });

            bench.Model.Execute("AM");

            Action run = Assert.Single(later);
            Assert.False(bench.Model.IsCommandRunning);
            run();
            Assert.Contains(bench.Model.History, l => l.StartsWith("Marks of V-TOP: ", StringComparison.Ordinal));
            Assert.Equal("UPDATE", made.Document.NextUndo.Name);
        }

        [Fact]
        public async Task ASectionCutAcrossTheFrontViewOnTheSheetAddsSectionA()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            var later = new List<Action>();
            bench.Workspace.Deferrer = later.Add;
            GeoRectangle2 front = made.Drawing.Views.Single(v => v.Kind == ViewKind.Front).SheetBox;
            string x = front.Center.X.ToString("R", CultureInfo.InvariantCulture);

            bench.Model.Execute("VS");
            bench.Model.Execute(x + "," + (front.Center.Y + 0.4 * front.Height).ToString("R", CultureInfo.InvariantCulture));
            bench.Model.Execute(x + "," + (front.Center.Y - 0.4 * front.Height).ToString("R", CultureInfo.InvariantCulture));
            Assert.StartsWith("Specify depth behind the cut", bench.Model.Prompt);
            bench.Model.Execute("100");

            Action run = Assert.Single(later);
            run();
            Assert.Contains(made.Drawing.Settings.Views.Sections, s => s.Name == "A");
            Assert.Contains(made.Drawing.Views, v => v.Kind == ViewKind.Section);
            Assert.Equal("UPDATE", made.Document.NextUndo.Name);
        }

        [Fact]
        public async Task ADetailPickedOnTheFrontViewIsDrawnAtTheScaleAsked()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            DrawingView view = made.Drawing.Views.Single(v => v.Kind == ViewKind.Front);
            GeoRectangle2 front = view.SheetBox;
            var a = new GeoPoint2(front.Center.X + 0.3 * front.Width, front.Center.Y - 0.4 * front.Height);
            var b = new GeoPoint2(front.Center.X + 0.48 * front.Width, front.Center.Y + 0.4 * front.Height);

            Assert.True(bench.Workspace.TryViewAt(a, out DrawingView found, out _));
            Assert.Equal(view.Id, found.Id);
            UpdateReport report = bench.Workspace.AddDetail(a, b, view.Scale / 2.0);

            Assert.NotNull(report);
            DrawingView detail = Assert.Single(made.Drawing.Views, v => v.Kind == ViewKind.Detail);
            Assert.Equal(view.Scale / 2.0, detail.Scale, 6);
            Assert.Null(bench.Workspace.AddDetail(new GeoPoint2(-1E6, -1E6), b, 2.0));
            Assert.Contains("The rectangle of the detail has to lie in one view of the drawing.", bench.Model.History);
        }

        [Fact]
        public async Task ClosingAModelDrawingTakesItOutOfTheListAndHidesTheDrawingTab()
        {
            var bench = new Bench();
            ModelDrawing made = await bench.MakeAsync();
            Assert.True(bench.Workspace.IsTabVisible);

            Assert.True(bench.Model.CloseDocument(made.Document));

            Assert.Empty(bench.Workspace.Drawings);
            Assert.Null(bench.Workspace.Current);
            Assert.False(bench.Workspace.IsTabVisible);
            Assert.False(bench.Model.HasDocument);
            Assert.Empty(bench.Model.FileRows);
            Assert.False(bench.Workspace.ExportDxfCommand.CanExecute(null));
        }

        private static string Tagged(ModelDrawing made, string id)
            => DrawingEntityTags.Find(made.Document.Database).First(e => !e.IsErased && DrawingEntityTags.TryRead(e, out string tag, out _) && tag == id).Handle;

        [Fact]
        public async Task TwoDrawingsOfOneBeamAreBothListed()
        {
            var bench = new Bench();
            await bench.MakeAsync("Assembly");
            await bench.MakeAsync("Part", d => d.Number = "B1-P");

            Assert.Equal(2, bench.Workspace.Drawings.Count);
            Assert.Equal(new[] { "B1", "B1-P" }, bench.Model.FileRows.Where(r => !r.IsHeader).Select(r => r.Label));
        }
    }
}
