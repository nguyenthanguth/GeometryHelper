using System;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Snap and Grid tab of Drafting Settings as AutoCAD 2021's: SNAPUNIT, POLARDIST, SNAPTYPE, SNAPSTYL, GRIDSTYLE,
    /// GRIDUNIT, GRIDMAJOR and GRIDDISPLAY read as the dialog opens, the boxes greyed by the snap type and style, Equal X and
    /// Y spacing, the isometric snap's doubled Y, the boxes refused, and OK writing what changed; driven through its view
    /// model, and the window made over it on an STA thread.
    /// </summary>
    public sealed class SnapAndGridTabTests : IDisposable
    {
        private static readonly double Root3 = Math.Sqrt(3.0);

        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>The room with a snap of 2.5 by 5, a grid of 10 by 20, a major line every 7 and GRIDDISPLAY 5.</summary>
        private MainViewModel Room(Action<DxfViewportTableRecord> set = null)
        {
            DxfDatabase db = TestFiles.Room(out _, out _);
            DxfViewportTableRecord active = db.ViewportTable["*Active"];
            active.SnapIncrements = new GeoVector2(2.5, 5);
            active.GridIncrements = new GeoVector2(10, 20);
            active.GridMajor = 7;
            active.GridDisplay = 5;
            set?.Invoke(active);
            var model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", db)));
            return model;
        }

        private static DxfViewportTableRecord Active(MainViewModel model) =>
            model.Document.Database.ViewportTable["*Active"];

        private static void Near(GeoVector2 expected, GeoVector2 actual)
        {
            Assert.Equal(expected.X, actual.X, 12);
            Assert.Equal(expected.Y, actual.Y, 12);
        }

        [Fact]
        public void TheTab_ReadsTheDrawingsSnapAndGrid_AndTheWindowsSnapTypeAndGridStyle()
        {
            MainViewModel model = Room();
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);

            Assert.Equal("2.5", dialog.SnapX);
            Assert.Equal("5", dialog.SnapY);
            Assert.Equal("10", dialog.GridX);
            Assert.Equal("20", dialog.GridY);
            Assert.Equal("7", dialog.MajorLineEvery);
            Assert.Equal("0", dialog.PolarDistance);
            Assert.False(dialog.IsEqualSpacing);

            // SNAPTYPE 0 and SNAPSTYL 0: Polar distance greyed, the rest open.
            Assert.True(dialog.IsGridSnapType);
            Assert.True(dialog.IsRectangularSnap);
            Assert.False(dialog.CanEditPolarDistance);
            Assert.True(dialog.CanEditSnapX);
            Assert.True(dialog.CanEditSnapY);
            Assert.True(dialog.CanEqualSpacing);
            Assert.True(dialog.CanChooseSnapStyle);
            Assert.True(dialog.CanEditGridX);

            // GRIDDISPLAY 5: beyond the limits and subdivided, not adaptive, so subdivision is greyed but kept.
            Assert.True(dialog.IsBeyondLimits);
            Assert.False(dialog.IsAdaptiveGrid);
            Assert.True(dialog.IsSubdividing);
            Assert.False(dialog.CanSubdivide);
            Assert.False(dialog.IsFollowingUcs);

            // GRIDSTYLE 0: lines everywhere.
            Assert.False(dialog.IsDottedInModel);
            Assert.False(dialog.IsDottedInBlockEditor);
            Assert.False(dialog.IsDottedInLayout);
        }

        [Fact]
        public void PolarSnap_GreysTheSnapSpacingAndStyle_AndOkWritesWhatChanged_ASpacingBelowNoughtTakenAsItsSize()
        {
            MainViewModel model = Room();
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);

            dialog.IsPolarSnapType = true;
            Assert.False(dialog.IsGridSnapType);
            Assert.True(dialog.CanEditPolarDistance);
            Assert.False(dialog.CanEditSnapX);
            Assert.False(dialog.CanEditSnapY);
            Assert.False(dialog.CanEqualSpacing);
            Assert.False(dialog.CanChooseSnapStyle);
            Assert.True(dialog.IsRectangularSnap);
            Assert.True(dialog.CanEditGridX);

            dialog.PolarDistance = "25";
            dialog.GridX = "-3";
            dialog.GridY = "6";
            dialog.MajorLineEvery = "10";
            dialog.IsAdaptiveGrid = true;
            Assert.True(dialog.CanSubdivide);
            dialog.IsFollowingUcs = true;
            dialog.IsDottedInModel = true;
            dialog.IsDottedInLayout = true;

            Assert.True(dialog.Ok());

            DxfViewportTableRecord active = Active(model);
            Near(new GeoVector2(2.5, 5), active.SnapIncrements);
            Near(new GeoVector2(3, 6), active.GridIncrements);
            Assert.Equal(10, active.GridMajor);
            Assert.Equal(15, active.GridDisplay);
            Assert.Equal(3.0, model.GridSpacingX);
            Assert.Equal(10, model.GridMajor);
            Assert.True(model.Settings.PolarSnap);
            Assert.Equal(25.0, model.Settings.PolarDistance);
            Assert.Equal(5, model.Settings.GridStyle);
            Assert.True(model.Snap.PolarSnap);
            Assert.Equal(25.0, model.Snap.PolarDistance);

            // Opened again, the dialog shows what was written, the grid's size where it was typed below nought.
            DraftingSettingsViewModel again = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);
            Assert.True(again.IsPolarSnapType);
            Assert.Equal("25", again.PolarDistance);
            Assert.Equal("3", again.GridX);
            Assert.Equal("10", again.MajorLineEvery);
            Assert.True(again.IsDottedInModel);
            Assert.False(again.IsDottedInBlockEditor);
            Assert.True(again.IsDottedInLayout);

            // The window keeps SNAPTYPE, POLARDIST and GRIDSTYLE in its settings file, as AutoCAD keeps them in the registry.
            string path = System.IO.Path.Combine(_folder.FullPath, "settings.json");
            model.Settings.Save(path);
            GlobalSettings saved = GlobalSettings.Load(path);
            Assert.True(saved.PolarSnap);
            Assert.Equal(25.0, saved.PolarDistance);
            Assert.Equal(5, saved.GridStyle);
        }

        [Fact]
        public void WithPolarSnapOnOk_ALinesSecondPointOnAPolarAngle_IsTakenAtAWholeStepOfThePolarDistance()
        {
            MainViewModel model = Room();
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);
            dialog.IsGridSnapOn = true;
            dialog.IsPolarSnapType = true;
            dialog.PolarDistance = "250";
            dialog.IsPolarOn = true;
            dialog.IsSnapOn = false;
            Assert.True(dialog.Ok());

            model.StartCommand("LINE");
            model.ClickAt(new GeoPoint2(1000.3, 1000.6), 2.0);
            model.ClickAt(new GeoPoint2(1620, 1000.9), 2.0);
            model.Escape();

            DxfLine drawn = model.Document.ModelSpace.Block.OfType<DxfLine>().Last();
            Assert.Equal(1000.3, drawn.StartPoint.X, 12);
            Assert.Equal(1000.6, drawn.StartPoint.Y, 12);
            Assert.Equal(1500.3, drawn.EndPoint.X, 9);
            Assert.Equal(1000.6, drawn.EndPoint.Y, 9);
        }

        [Fact]
        public void TheIsometricSnap_ShowsTwiceTheYTheDrawingHolds_GreysX_AndOkWritesTheYTypedAsSnapStyleIsometricDoes()
        {
            MainViewModel model = Room(a =>
            {
                a.IsometricSnapEnabled = true;
                a.SnapIncrements = new GeoVector2(5 * Root3, 2.5);
                a.GridIncrements = new GeoVector2(5 * Root3, 2.5);
            });
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);

            Assert.True(dialog.IsIsometricSnap);
            Assert.Equal("8.6603", dialog.SnapX);
            Assert.Equal("5", dialog.SnapY);
            Assert.Equal("8.6603", dialog.GridX);
            Assert.Equal("5", dialog.GridY);
            Assert.False(dialog.CanEditSnapX);
            Assert.True(dialog.CanEditSnapY);
            Assert.False(dialog.CanEditGridX);
            Assert.False(dialog.CanEqualSpacing);
            Assert.False(dialog.IsEqualSpacing);

            dialog.SnapY = "10";
            dialog.GridY = "0";
            Assert.True(dialog.Apply());

            DxfViewportTableRecord active = Active(model);
            Assert.True(active.IsometricSnapEnabled);
            Near(new GeoVector2(10 * Root3, 5), active.SnapIncrements);
            Near(new GeoVector2(0, 0), active.GridIncrements);
            Assert.Equal(5.0, model.Snap.GridY, 12);
            Assert.Equal("10", dialog.SnapY);
            Assert.Equal("0", dialog.GridY);
        }

        [Fact]
        public void IsometricChosenOnARectangularSnap_IsWrittenWithTheSnapAndGridMadeFromTheirY()
        {
            MainViewModel model = Room(a => a.SnapIncrements = new GeoVector2(10, 10));
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);
            Assert.Null(model.Isoplane);

            dialog.IsIsometricSnap = true;
            Assert.False(dialog.IsRectangularSnap);
            Assert.False(dialog.CanEditSnapX);
            Assert.False(dialog.CanEditGridX);

            // Nothing is written until OK.
            Assert.False(Active(model).IsometricSnapEnabled);
            Assert.True(dialog.Ok());

            DxfViewportTableRecord active = Active(model);
            Assert.True(active.IsometricSnapEnabled);
            Near(new GeoVector2(10 * Root3, 5), active.SnapIncrements);
            Near(new GeoVector2(20 * Root3, 10), active.GridIncrements);
            Assert.Equal(CadIsoplane.Left, model.Isoplane);
            Assert.Equal(CadIsoplane.Left, model.Snap.Isoplane);
        }

        [Fact]
        public void EqualSpacing_IsTickedWhereSnapAndGridAreSquare_AndCopiesEachXToItsYAndBack()
        {
            MainViewModel model = Room(a =>
            {
                a.SnapIncrements = new GeoVector2(10, 10);
                a.GridIncrements = new GeoVector2(0, 0);
            });
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);
            Assert.True(dialog.IsEqualSpacing);

            dialog.SnapX = "4";
            Assert.Equal("4", dialog.SnapY);
            dialog.GridY = "7";
            Assert.Equal("7", dialog.GridX);

            // Unticked, the boxes go their own ways; ticked again, each Y takes its X.
            dialog.IsEqualSpacing = false;
            dialog.SnapX = "3";
            Assert.Equal("4", dialog.SnapY);
            dialog.GridX = "2";
            Assert.Equal("7", dialog.GridY);
            dialog.IsEqualSpacing = true;
            Assert.Equal("3", dialog.SnapY);
            Assert.Equal("2", dialog.GridY);

            // Greyed by PolarSnap, it copies nothing.
            dialog.IsPolarSnapType = true;
            dialog.GridX = "9";
            Assert.Equal("2", dialog.GridY);
        }

        [Fact]
        public void ABoxRefused_IsNamedWithAutoCadsWords_NothingIsWritten_AndTypingInItClearsTheNotice()
        {
            MainViewModel model = Room();
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);
            bool? closed = null;
            dialog.CloseRequested += (s, ok) => closed = ok;

            dialog.MajorLineEvery = "2.5";
            dialog.SnapY = "0";
            dialog.GridX = "4";
            Assert.False(dialog.Ok());
            Assert.Null(closed);

            // The first box at fault, in the order of the tab.
            Assert.Equal(DraftingSettingsViewModel.SnapYBox, dialog.FaultyBox);
            Assert.Equal("The snap spacing must be a number above nought.", dialog.Notice);
            Near(new GeoVector2(10, 20), Active(model).GridIncrements);

            dialog.SnapY = "5";
            Assert.Equal(string.Empty, dialog.FaultyBox);
            Assert.Equal(string.Empty, dialog.Notice);

            Assert.False(dialog.Apply());
            Assert.Equal(DraftingSettingsViewModel.MajorLineEveryBox, dialog.FaultyBox);
            Assert.Equal("Requires an integer between 1 and 100.", dialog.Notice);
            dialog.MajorLineEvery = "101";
            Assert.False(dialog.Apply());
            Assert.Equal("Requires an integer between 1 and 100.", dialog.Notice);
            Assert.Equal(7, Active(model).GridMajor);

            dialog.MajorLineEvery = "100";
            dialog.GridY = "abc";
            Assert.False(dialog.Apply());
            Assert.Equal(DraftingSettingsViewModel.GridYBox, dialog.FaultyBox);
            Assert.Equal("The grid spacing must be a number, nought or above.", dialog.Notice);

            dialog.GridY = "0";
            dialog.IsPolarSnapType = true;
            dialog.PolarDistance = "-1";
            Assert.False(dialog.Apply());
            Assert.Equal(DraftingSettingsViewModel.PolarDistanceBox, dialog.FaultyBox);
            Assert.Equal("The polar distance must be a number, nought or above.", dialog.Notice);
            Assert.False(model.Settings.PolarSnap);

            dialog.PolarDistance = "0";
            Assert.True(dialog.Ok());
            Assert.True(closed);
            Assert.Equal(100, Active(model).GridMajor);
            Near(new GeoVector2(4, 0), Active(model).GridIncrements);
            Assert.Equal(string.Empty, dialog.Notice);
        }

        [Fact]
        public void TheWindow_Is580High_AndIsMadeOverTheTab_Isometric_AndWithABoxRefused()
        {
            string path = _folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", TestFiles.Room(out _, out _));
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var model = new MainViewModel();
                    Assert.True(model.Open(path));

                    DraftingSettingsViewModel isometric = model.CreateDraftingSettings(
                        DraftingSettingsViewModel.SnapAndGridTab
                    );
                    isometric.IsIsometricSnap = true;
                    DraftingSettingsViewModel refused = model.CreateDraftingSettings(
                        DraftingSettingsViewModel.SnapAndGridTab
                    );
                    refused.SnapY = "0";
                    Assert.False(refused.Apply());

                    foreach (DraftingSettingsViewModel dialog in new[] { isometric, refused })
                    {
                        var window = new DraftingSettingsWindow(dialog);
                        Assert.Equal(560.0, window.Width);
                        Assert.Equal(580.0, window.Height);
                        var body = (FrameworkElement)window.Content;
                        window.Content = null;
                        body.DataContext = window.DataContext;
                        body.Measure(new Size(window.Width, window.Height));
                        body.Arrange(new Rect(0, 0, window.Width, window.Height));
                        body.UpdateLayout();
                        Assert.True(body.ActualWidth > 0);
                        window.Close();
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(failure);
        }
    }
}
