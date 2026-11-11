using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The second round of snap and grid follow-ups, checked against AutoCAD 2021's console: POLARDIST and GRIDUNIT given by
    /// points, SNAPGRIDLEGACY and the cursor it holds on the grid, SNAPMODE 2 saved as SNAP on, a POLARDIST below nought in
    /// Drafting Settings, the grid of a layout's paper space with its own GRIDMODE, and "Grid too dense to display".
    /// </summary>
    public sealed class SnapFollowUpsTwoTests : IDisposable
    {
        private const int Width = 800;
        private const int Height = 600;

        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>The room with a snap of 10 by 10, a grid of 4 by 6, snap and grid off.</summary>
        private MainViewModel Room()
        {
            DxfDatabase db = TestFiles.Room(out _, out _);
            DxfViewportTableRecord active = db.ViewportTable["*Active"];
            active.SnapIncrements = new GeoVector2(10, 10);
            active.GridIncrements = new GeoVector2(4, 6);
            active.SnapEnabled = false;
            active.GridEnabled = false;
            var model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", db)));
            return model;
        }

        private static DxfViewportTableRecord Active(MainViewModel model) =>
            model.Document.Database.ViewportTable["*Active"];

        private static CadSpace Layout1(MainViewModel model) => model.Document.Spaces.Single(s => s.Name == "Layout1");

        private static DxfViewport Sheet(MainViewModel model) =>
            Layout1(model).Layout.GetViewports().Single(v => v.Number == 1);

        [Fact]
        public void SetvarPolardist_TakesTwoPoints_AndKeepsTheDistanceBetweenThem()
        {
            MainViewModel model = Room();

            // As AutoCAD 2021: "Enter new value for POLARDIST <0.0000>: 0,0  Specify second point: 3,4" sets 5.
            model.Execute("POLARDIST");
            model.Execute("0,0");
            Assert.Equal("Specify second point:", model.Prompt);
            model.Execute("abc");
            Assert.Equal("Requires numeric distance or two points.", model.History.Last());
            model.Execute("3,4");

            Assert.False(model.IsCommandRunning);
            Assert.Equal(5.0, model.Settings.PolarDistance);
            Assert.Equal(5.0, model.Snap.PolarDistance);
        }

        [Fact]
        public void SetvarGridunit_TakesANumberAsADistanceFromTheLastPoint()
        {
            MainViewModel model = Room();
            model.Execute("ID");
            model.Execute("10,20");

            // As AutoCAD 2021's GRIDUNIT 5 with the last point at (10, 20): the point 5 from it, the way the cursor lies.
            model.Session.Cursor = new GeoPoint2(13, 24);
            model.Execute("GRIDUNIT");
            model.Execute("5");

            Assert.False(model.IsCommandRunning);
            Assert.Equal(13.0, Active(model).GridIncrements.X, 9);
            Assert.Equal(24.0, Active(model).GridIncrements.Y, 9);
        }

        [Fact]
        public void Snapgridlegacy_IsAVariableOf0And1_SetBySnapLegacy_AndKeptInTheSettingsFile()
        {
            MainViewModel model = Room();

            model.Execute("SETVAR SNAPGRIDLEGACY");
            Assert.Equal("Enter new value for SNAPGRIDLEGACY <0>:", model.Prompt);
            model.Execute("2");
            Assert.Equal("Requires 0 or 1 only.", model.History.Last());
            model.Execute("1");
            Assert.True(model.SnapGridLegacy);
            Assert.True(model.Settings.SnapGridLegacy);

            model.Execute("SNAP L");
            Assert.Equal("Maintain legacy behavior of always snapping to the grid? [Yes/No] <Yes>:", model.Prompt);
            model.Execute("N");
            Assert.False(model.SnapGridLegacy);
            model.Execute("SNAP L Y");
            Assert.True(model.SnapGridLegacy);

            string path = System.IO.Path.Combine(_folder.FullPath, "settings.json");
            model.Settings.Save(path);
            Assert.True(GlobalSettings.Load(path).SnapGridLegacy);
        }

        [Fact]
        public void Snapmode2_IsSavedAsSnapOn_AsAutoCadWritesIt()
        {
            MainViewModel model = Room();

            // AutoCAD 2021 reads SNAPMODE 2 back, and writes the *Active viewport's 75 as 1.
            model.Execute("SNAPMODE 2");

            Assert.True(Active(model).SnapEnabled);
            Assert.True(model.Snap.GridSnap);
        }

        [Fact]
        public void DraftingSettings_TakesAPolarDistanceBelowNought_AsSetvarDoes()
        {
            MainViewModel model = Room();
            model.Execute("POLARDIST -1");

            // A dialog opened over a POLARDIST of -1 shows it, and OK keeps it.
            DraftingSettingsViewModel dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);
            dialog.IsPolarSnapType = true;
            Assert.Equal("-1", dialog.PolarDistance);
            Assert.True(dialog.Ok());
            Assert.Equal(-1.0, model.Settings.PolarDistance);

            dialog = model.CreateDraftingSettings(DraftingSettingsViewModel.SnapAndGridTab);
            dialog.PolarDistance = "abc";
            Assert.False(dialog.Ok());
            Assert.Equal(DraftingSettingsViewModel.PolarDistanceBox, dialog.FaultyBox);
            Assert.Equal("The polar distance must be a number.", dialog.Notice);
            dialog.PolarDistance = "-2.5";
            Assert.True(dialog.Ok());

            Assert.Equal(-2.5, model.Settings.PolarDistance);
            Assert.Equal(-2.5, model.Snap.PolarDistance);
        }

        [Fact]
        public void GridInALayout_IsItsOwn_KeptInTheViewport1OfItsSheet()
        {
            MainViewModel model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("room.dxf", TestFiles.RoomWithViewport(_folder, out _, out _))));
            model.Execute("GRIDMODE 0");
            Assert.False(Active(model).GridEnabled);

            // As AutoCAD 2021: GRIDMODE 1 in Layout1 is the sheet's, model space's left off, and each comes back with its space.
            model.SelectSpace(Layout1(model));
            Assert.False(model.GridOn);
            model.Execute("GRIDMODE 1");
            Assert.True(model.GridOn);
            Assert.True(Sheet(model).GridOn);
            Assert.False(Active(model).GridEnabled);

            model.SelectSpace(model.Document.ModelSpace);
            Assert.False(model.GridOn);
            model.Execute("GRIDMODE");
            Assert.Equal("Enter new value for GRIDMODE <0>:", model.Prompt);
            model.Enter();

            model.SelectSpace(Layout1(model));
            Assert.True(model.GridOn);
            model.Execute("GRID OFF");
            Assert.False(Sheet(model).GridOn);
        }

        /// <summary>Runs a test on an STA thread with a view model made there and a canvas of 800 by 600 over its drawing.</summary>
        private static void OnCanvas(Func<MainViewModel> make, Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    MainViewModel model = make();
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                    };
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.Measure(new Size(Width, Height));
                    canvas.Arrange(new Rect(0, 0, Width, Height));
                    canvas.RenderNow();
                    test(model, canvas);
                    canvas.Dispose();
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

        private static int Pixel(CadCanvas canvas, GeoPoint2 world)
        {
            canvas.RenderNow();
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            canvas.View.DrawingToPixel(world.X, world.Y, out double px, out double py);
            return pixels[(int)Math.Round(py) * Width + (int)Math.Round(px)] & 0xFFFFFF;
        }

        [Fact]
        public void TheGridOfALayout_IsDrawnOnItsPaper_ByItsSheetsSpacing()
        {
            MainViewModel Make()
            {
                var made = new MainViewModel();
                Assert.True(made.Open(_folder.Write("room.dxf", TestFiles.RoomWithViewport(_folder, out _, out _))));
                made.SelectSpace(Layout1(made));
                DxfViewport sheet = Sheet(made);
                sheet.GridIncrement = new GeoVector2(3, 3);
                sheet.GridMajor = 2;
                sheet.GridAdaptive = false;
                sheet.GridBoundToLimits = false;
                return made;
            }

            OnCanvas(Make, (model, canvas) =>
            {
                // The sheet of 12 by 9, away from the viewport (50 to 250 by 25 to 175): a major line of its grid, every
                // second line of 3, at x 6, 18 per cent darker than the white paper; off, the paper is white there.
                DisplayBox paper = canvas.Space.PaperBounds;
                Assert.Equal(12.0, paper.MaxX - paper.MinX, 9);
                Assert.True(3 * canvas.View.PixelsPerUnit >= 6);
                var on = new GeoPoint2(6, 4.5);
                Assert.Equal(0xFFFFFF, Pixel(canvas, on));

                canvas.ShowGrid = true;
                Assert.Equal(0xD2D2D2, Pixel(canvas, on));
                Assert.Equal(0xEBEBEB, Pixel(canvas, new GeoPoint2(4.5, 3)));
                Assert.Equal(0xFFFFFF, Pixel(canvas, new GeoPoint2(4.5, 4)));
            });
        }

        [Fact]
        public void AGridTooDenseToDisplay_IsSaidOnceAsItComesOn()
        {
            OnCanvas(Room, (model, canvas) =>
            {
                canvas.GridDisplay = 1;
                canvas.GridSpacing = 0.01;
                canvas.GridSpacingY = 0.01;
                canvas.ShowGrid = true;
                canvas.RenderNow();
                Assert.Equal("Grid too dense to display", model.Session.History.Last());

                // Drawn again with nothing changed, it is not said again; shown and then too dense again, it is.
                canvas.RenderNow();
                canvas.View.Pan(5, 0);
                canvas.RenderNow();
                Assert.Single(model.Session.History, l => l == "Grid too dense to display");
                canvas.GridSpacing = 1000;
                canvas.GridSpacingY = 1000;
                canvas.RenderNow();
                canvas.GridSpacing = 0.01;
                canvas.GridSpacingY = 0.01;
                canvas.RenderNow();
                Assert.Equal(2, model.Session.History.Count(l => l == "Grid too dense to display"));

                // An adaptive grid is never too dense.
                canvas.ShowGrid = false;
                canvas.RenderNow();
                canvas.GridDisplay = 3;
                canvas.ShowGrid = true;
                canvas.RenderNow();
                Assert.Equal(2, model.Session.History.Count(l => l == "Grid too dense to display"));
            });
        }

        [Fact]
        public void WithSnapgridlegacy_TheCursorIsHeldOnTheGrid_WithNoPointAsked()
        {
            MainViewModel Make()
            {
                MainViewModel made = Room();
                made.Execute("SNAPUNIT 500,500");
                made.Execute("SNAPMODE 1");
                return made;
            }

            OnCanvas(Make, (model, canvas) =>
            {
                double x = 0;
                double y = 0;
                canvas.CursorMoved += (s, e) =>
                {
                    x = e.X;
                    y = e.Y;
                };

                // Off, the cursor goes free; on, it is at the grid point nearest, and a click there picks the bottom wall.
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, CanvasMouseTests.At(canvas, 1130, 70));
                Assert.Equal(1130, x, 6);
                Assert.Equal(70, y, 6);

                canvas.SnapGridLegacy = true;
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, CanvasMouseTests.At(canvas, 1130, 70));
                Assert.Equal(1000, x, 6);
                Assert.Equal(0, y, 6);

                CanvasMouseTests.Click(canvas, CanvasMouseTests.At(canvas, 1130, 70));
                Assert.Equal(1, model.Document.Selection.Count);

                // SNAP off, the cursor goes free again.
                model.Execute("SNAPMODE 0");
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, CanvasMouseTests.At(canvas, 1130, 70));
                Assert.Equal(1130, x, 6);
            });
        }
    }
}
