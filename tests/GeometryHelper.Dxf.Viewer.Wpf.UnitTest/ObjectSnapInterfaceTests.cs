using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The object snaps as AutoCAD's interface has them (C6): the OSNAP menu, DSETTINGS and Drafting Settings, the
    /// Shift+right-click menu and an override typed at a point prompt for the next point alone.
    /// </summary>
    public sealed class ObjectSnapInterfaceTests : IDisposable
    {
        private static readonly string[] AutoCadSnaps =
        {
            "Endpoint", "Midpoint", "Center", "Geometric Center", "Node", "Quadrant", "Intersection", "Extension", "Insertion",
            "Perpendicular", "Tangent", "Nearest", "Apparent Intersection", "Parallel",
        };

        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void TheOsnapMenuListsAutoCADsFourteenSnaps_TheOnesOnTicked_TheOnesNotBuiltOff_ThenTheSettings()
        {
            var model = new MainViewModel();

            IReadOnlyList<CadMenuItemViewModel> items = model.OsnapMenu();

            Assert.Equal(AutoCadSnaps.Concat(new[] { string.Empty, "Object Snap Settings..." }), items.Select(i => i.Header));
            Assert.True(items.Single(i => i.Header == "Endpoint").IsChecked);
            Assert.False(items.Single(i => i.Header == "Nearest").IsChecked);
            Assert.False(items.Single(i => i.Header == "Node").IsChecked);

            foreach (string name in new[] { "Extension", "Insertion", "Apparent Intersection", "Parallel" })
            {
                CadMenuItemViewModel item = items.Single(i => i.Header == name);
                Assert.True(item.IsEnabled);
                Assert.Null(item.ToolTip);
                Assert.True(item.Command.CanExecute(null));
            }

            Assert.All(items.Where(i => !i.IsSeparator && i.Header != "Object Snap Settings..."), i => Assert.True(i.IsCheckable));
        }

        [Fact]
        public void ASnapChosenFromTheMenuIsSwitchedForTheSnapsAndTheSettings_AndRaisesTheChangeTheWindowSaves()
        {
            var model = new MainViewModel();
            var raised = new List<string>();
            model.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
            CadSnapModes before = model.SnapModes;

            model.OsnapMenu().Single(i => i.Header == "Node").Command.Execute(null);
            model.OsnapMenu().Single(i => i.Header == "Midpoint").Command.Execute(null);

            CadSnapModes expected = (before | CadSnapModes.Node) & ~CadSnapModes.Midpoint;
            Assert.Equal(expected, model.SnapModes);
            Assert.Equal(expected, model.Snap.Modes);
            Assert.Equal(expected, model.Settings.Snaps);
            Assert.Equal(2, raised.Count(n => n == nameof(MainViewModel.SnapModes)));
            Assert.True(model.OsnapMenu().Single(i => i.Header == "Node").IsChecked);
        }

        [Fact]
        public void DsettingsAndItsAliasesAskTheWindow_AndTheMenusSettingsItemToo()
        {
            var model = new MainViewModel();
            OpenRoom(model);

            model.Execute("DSETTINGS");
            Assert.Contains("Drafting Settings are shown by the window.", model.History);

            int asked = 0;
            model.DraftingSettingsRequested += (s, e) => asked++;
            model.Execute("DS");
            model.Execute("OS");
            model.OsnapMenu().Single(i => i.Header == "Object Snap Settings...").Command.Execute(null);

            Assert.Equal(3, asked);
            Assert.False(model.IsCommandRunning);
        }

        [Fact]
        public void DraftingSettingsTicksTheSnapsOn_SelectAllTakesOnlyThoseBuilt_AndApplyGivesThemToTheWindow()
        {
            var model = new MainViewModel();
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings();

            Assert.True(dialog.IsObjectSnapTab);
            Assert.Equal(AutoCadSnaps, dialog.Modes.Select(r => r.Name));
            Assert.True(dialog.Modes.Single(r => r.Name == "Endpoint").IsChecked);
            Assert.True(dialog.IsSnapOn);

            dialog.SelectAllCommand.Execute(null);
            Assert.All(dialog.Modes, r => Assert.Equal(r.IsAvailable, r.IsChecked));
            Assert.True(dialog.Modes.Single(r => r.Name == "Parallel").IsChecked);
            Assert.Null(dialog.Modes.Single(r => r.Name == "Parallel").ToolTip);

            dialog.ClearAllCommand.Execute(null);
            dialog.Modes.Single(r => r.Name == "Geometric Center").IsChecked = true;
            dialog.Modes.Single(r => r.Name == "Tangent").IsChecked = true;
            dialog.IsSnapOn = false;

            Assert.True(dialog.Apply());

            Assert.Equal(CadSnapModes.GeometricCenter | CadSnapModes.Tangent, model.SnapModes);
            Assert.Equal(CadSnapModes.GeometricCenter | CadSnapModes.Tangent, model.Settings.Snaps);
            Assert.False(model.Toggles.Single(t => t.Name == "OSNAP").IsOn);
            Assert.False(model.Snap.IsOn);
        }

        [Fact]
        public void DraftingSettingsRefusesASpacingNotAboveNought_AndTakesTheSnapSpacingAndThePolarIncrement()
        {
            var model = new MainViewModel();
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);
            bool? closed = null;
            dialog.CloseRequested += (s, ok) => closed = ok;
            Assert.True(dialog.IsSnapAndGridTab);
            Assert.Equal("10", dialog.SnapX);
            Assert.Equal("90", dialog.IncrementAngle);

            dialog.SnapX = "0";
            Assert.False(dialog.Ok());
            Assert.Equal("The snap spacing must be a number above nought.", dialog.Notice);
            Assert.Null(closed);

            dialog.SnapX = "25";
            dialog.SnapY = "12.5";
            dialog.IncrementAngle = "45";
            dialog.IsPolarOn = true;
            dialog.IsGridSnapOn = false;

            Assert.True(dialog.Ok());

            Assert.True(closed);
            Assert.Equal(25.0, model.Snap.GridX);
            Assert.Equal(12.5, model.Snap.GridY);
            Assert.Equal(Math.PI / 4.0, model.Snap.PolarAngle, 12);
            Assert.True(model.Snap.Polar);
            Assert.False(model.Snap.GridSnap);
            Assert.Equal(string.Empty, dialog.Notice);
        }

        [Fact]
        public void EndTypedAtAPointPromptTakesTheEndpointWithOsnapOff_ForThatPointAlone()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
            model.Toggles.Single(t => t.Name == "POLAR").IsOn = false;
            model.StartCommand("LINE");

            model.Execute("END");
            Assert.Equal(CadSnapModes.Endpoint, model.Snap.Override);
            model.ClickAt(new GeoPoint2(3999.5, 0.4), 2.0);
            Assert.Null(model.Snap.Override);
            model.ClickAt(new GeoPoint2(3999.5, 300.4), 2.0);
            model.Escape();

            DxfLine drawn = model.Document.ModelSpace.Block.OfType<DxfLine>().Last();
            Assert.Equal(4000.0, drawn.StartPoint.X, 12);
            Assert.Equal(0.0, drawn.StartPoint.Y, 12);
            Assert.Equal(3999.5, drawn.EndPoint.X, 12);
            Assert.Equal(300.4, drawn.EndPoint.Y, 12);
            Assert.Contains("Specify first point: _end of", model.History);
        }

        [Fact]
        public void TheShiftRightClickMenuIsEmptyWithNoPointAsked_AndItsChoiceTakesThatPointAlone()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            Assert.Empty(model.SnapOverrideMenu());
            model.StartCommand("LINE");

            IReadOnlyList<CadMenuItemViewModel> items = model.SnapOverrideMenu();

            Assert.Equal(new[] { "Temporary track point", "From", "Mid Between 2 Points", "Point Filters", string.Empty, "Endpoint", "Midpoint", "Intersection", "Apparent Intersection", "Extension", "Center", "Geometric Center", "Quadrant", "Tangent", "Perpendicular", "Parallel", "Node", "Insertion", "Nearest", "None", string.Empty, "Osnap Settings..." },
                items.Select(i => i.Header));
            items.Single(i => i.Header == "Midpoint").Command.Execute(null);
            Assert.Equal(CadSnapModes.Midpoint, model.Snap.Override);
            Assert.Equal(CadSnapKind.Midpoint, model.SnapAt(new GeoPoint2(2001, 1), 2.0).Kind);
            Assert.Equal(CadSnapKind.None, model.SnapAt(new GeoPoint2(3999.5, 0.4), 2.0).Kind);

            model.ClickAt(new GeoPoint2(2001, 1), 2.0);

            Assert.Null(model.Snap.Override);
            Assert.Equal(CadSnapKind.Endpoint, model.SnapAt(new GeoPoint2(3999.5, 0.4), 2.0).Kind);
            Assert.Contains("Specify first point: _mid of", model.History);
        }

        [Fact]
        public void NoneTypedTakesNoSnapForThePoint_AfterExtTyped()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            model.StartCommand("LINE");

            model.Execute("EXT");
            Assert.Equal(CadSnapModes.Extension, model.Snap.Override);

            model.Execute("NON");
            Assert.Equal(CadSnapKind.None, model.SnapAt(new GeoPoint2(3999.5, 0.4), 2.0).Kind);
            Assert.True(model.IsCommandRunning);
        }

        [Fact]
        public void AnOverrideEndsWhenTheCommandIsCancelled()
        {
            var model = new MainViewModel();
            OpenRoom(model);
            Assert.False(model.OverrideSnap(CadSnapModes.Endpoint));
            model.StartCommand("LINE");
            Assert.True(model.OverrideSnap(CadSnapModes.Nearest));

            model.Escape();

            Assert.Null(model.Snap.Override);
        }

        /// <summary>The room shown, the snap grid (SNAP, on by default) off so that a point not snapped is the cursor.</summary>
        private static void OpenRoom(MainViewModel model)
        {
            model.OpenDocument(CadDocument.From(TestFiles.Room(out _, out _), "room.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
        }
    }
}
