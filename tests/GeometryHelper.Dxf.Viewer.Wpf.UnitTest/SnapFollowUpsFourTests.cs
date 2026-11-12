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
    /// The fourth round of snap and grid follow-ups, checked against AutoCAD 2021's console: SNAPMODE 2 kept by each space,
    /// undo and redo showing the snap and grid the drawing holds again, the grid of every viewport whose grid is on drawn in
    /// its frame, the dotted grid of model space through a viewport, and the limits of a twisted viewport kept exactly.
    /// </summary>
    public sealed class SnapFollowUpsFourTests : IDisposable
    {
        private const int Width = 800;
        private const int Height = 600;

        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>The room with Layout1's viewport 200 by 150 about (150, 100) at 1:20, model space's grid 4 by 6 and snap 10.</summary>
        private MainViewModel RoomWithViewport(out string viewport, Action<DxfDatabase> more = null)
        {
            DxfDatabase db = TestFiles.RoomWithViewport(_folder, out _, out viewport);
            DxfViewportTableRecord active = db.ViewportTable["*Active"];
            active.SnapIncrements = new GeoVector2(10, 10);
            active.GridIncrements = new GeoVector2(4, 6);
            active.SnapEnabled = false;
            active.GridEnabled = false;
            more?.Invoke(db);
            var model = new MainViewModel();
            Assert.True(model.Open(_folder.Write("room-" + Guid.NewGuid().ToString("N") + ".dxf", db)));
            return model;
        }

        private static CadSpace Layout1(MainViewModel model) => model.Document.Spaces.Single(s => s.Name == "Layout1");

        private static DxfViewport Frame(MainViewModel model, string handle) =>
            (DxfViewport)model.Document.FindEntity(handle);

        /// <summary>SNAPMODE as SETVAR shows it in its angle brackets, the prompt ended with Enter.</summary>
        private static string SnapMode(MainViewModel model)
        {
            model.Execute("SNAPMODE");
            string prompt = model.Prompt;
            model.Enter();
            return prompt;
        }

        [Fact]
        public void SnapMode2_IsKeptByEachSpace_AsAutoCADReadsItBack()
        {
            MainViewModel model = RoomWithViewport(out string viewport);

            // As AutoCAD 2021: SNAPMODE 2 set in model space reads 2 there again after the layout's paper set 1, and the
            // viewport worked through keeps its own 2 apart from the paper's 1.
            model.Execute("SNAPMODE 2");
            Assert.Equal("Enter new value for SNAPMODE <2>:", SnapMode(model));

            model.SelectSpace(Layout1(model));
            Assert.Null(model.Document.ActiveViewport);
            Assert.Equal("Enter new value for SNAPMODE <0>:", SnapMode(model));
            model.Execute("SNAPMODE 1");
            Assert.Equal("Enter new value for SNAPMODE <1>:", SnapMode(model));

            model.Execute("MSPACE");
            Assert.Equal(viewport, model.Document.ActiveViewport.Handle);
            Assert.Equal("Enter new value for SNAPMODE <0>:", SnapMode(model));
            model.Execute("SNAPMODE 2");
            Assert.Equal("Enter new value for SNAPMODE <2>:", SnapMode(model));
            model.Execute("PSPACE");
            Assert.Equal("Enter new value for SNAPMODE <1>:", SnapMode(model));
            model.Execute("MSPACE");
            Assert.Equal("Enter new value for SNAPMODE <2>:", SnapMode(model));

            model.SelectSpace(model.Document.ModelSpace);
            Assert.Equal("Enter new value for SNAPMODE <2>:", SnapMode(model));

            // SNAP switched reads 1 again, in that space alone.
            model.Execute("SNAP OFF");
            model.Execute("SNAP ON");
            Assert.Equal("Enter new value for SNAPMODE <1>:", SnapMode(model));
            model.SelectSpace(Layout1(model));
            model.Execute("MSPACE");
            Assert.Equal("Enter new value for SNAPMODE <2>:", SnapMode(model));
        }

        [Fact]
        public void UndoAndRedo_ShowTheSnapAndGridTheViewportWorkedThroughHoldsAgain()
        {
            MainViewModel model = RoomWithViewport(out string viewport);
            model.SelectSpace(Layout1(model));
            model.Execute("MSPACE");
            DxfViewport frame = Frame(model, viewport);
            Assert.False(frame.GridOn);
            Assert.False(model.GridOn);

            // A line drawn through the viewport, then GRID 50 typed there: U takes the drawing back to before the line, the
            // viewport's grid off and of the spacing it had, and the toggle and the spacing shown follow it, as AutoCAD's
            // UNDO takes back a grid set there.
            model.Execute("LINE");
            model.Execute("1000,1000");
            model.Execute("2000,1000");
            model.Enter();
            model.Execute("GRID 50");
            model.Execute("SNAP 25");
            Assert.True(model.GridOn);
            Assert.Equal(50.0, model.GridSpacingX);
            Assert.Equal(25.0, model.Snap.GridX);

            model.Execute("U");
            Assert.False(Frame(model, viewport).GridOn);
            Assert.False(model.GridOn);
            Assert.False(model.Snap.GridSnap);
            Assert.NotEqual(50.0, model.GridSpacingX);
            Assert.NotEqual(25.0, model.Snap.GridX);

            // A second line drawn with the grid on keeps it in the drawing; switched off after, REDO of that line shows it on.
            model.Execute("GRID ON");
            model.Execute("LINE");
            model.Execute("1000,2000");
            model.Execute("2000,2000");
            model.Enter();
            model.Execute("U");
            Assert.False(model.GridOn);
            model.Execute("REDO");
            Assert.True(Frame(model, viewport).GridOn);
            Assert.True(model.GridOn);
        }

        [Fact]
        public void Undo_ShowsTheSnapAndGridModelSpaceHoldsAgain()
        {
            MainViewModel model = RoomWithViewport(out _);
            Assert.False(model.GridOn);

            model.Execute("LINE");
            model.Execute("0,0");
            model.Execute("100,0");
            model.Enter();
            model.Execute("GRID 7");
            Assert.True(model.GridOn);
            Assert.Equal(7.0, model.GridSpacingX);

            model.Execute("U");
            Assert.False(model.Document.Database.ViewportTable["*Active"].GridEnabled);
            Assert.False(model.GridOn);
            Assert.Equal(4.0, model.GridSpacingX);
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

        private static int[] Picture(CadCanvas canvas)
        {
            canvas.RenderNow();
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            return pixels;
        }

        private static int At(int[] pixels, CadView view, GeoPoint2 point)
        {
            view.DrawingToPixel(point.X, point.Y, out double px, out double py);
            return pixels[(int)Math.Round(py) * Width + (int)Math.Round(px)] & 0xFFFFFF;
        }

        private static int Pixel(CadCanvas canvas, GeoPoint2 paper) => At(Picture(canvas), canvas.View, paper);

        /// <summary>
        /// The room with Layout1's viewport moved onto the sheet of 12 by 9: 8 by 6 about (6, 4.5), the room at 1:500 about
        /// (2 000, 1 500), its grid of 1 000 every 5, not adaptive and beyond the limits; the sheet's grid off.
        /// </summary>
        private MainViewModel SheetWithViewport(out string viewport, Action<DxfDatabase> more = null)
        {
            MainViewModel made = RoomWithViewport(out viewport, more);
            DxfViewport frame = Frame(made, viewport);
            frame.CenterPoint = new GeoPoint3(6, 4.5, 0);
            frame.Width = 8;
            frame.Height = 6;
            frame.ViewHeight = 3000;
            frame.GridIncrement = new GeoVector2(1000, 1000);
            frame.GridMajor = 5;
            frame.GridAdaptive = false;
            frame.GridBoundToLimits = false;
            made.SelectSpace(Layout1(made));
            return made;
        }

        [Fact]
        public void AViewportsGrid_IsDrawnInItsFrame_OnThePaperAndThroughAnotherViewport()
        {
            string viewport = null;
            string other = null;

            // A second viewport, 2 by 2 about (1.5, 7.5), its grid off.
            void AddSecond(DxfDatabase db)
            {
                DxfLayout layout = db.Layouts.Single(l => l.LayoutName == "Layout1");
                var second = new DxfViewport
                {
                    CenterPoint = new GeoPoint3(1.5, 7.5, 0),
                    Width = 2,
                    Height = 2,
                    ViewHeight = 1000,
                    ViewCenter = new GeoPoint2(2000, 1500),
                };
                layout.BlockTableRecord.AppendEntity(second);
                second.On = true;
                other = second.Handle;
            }

            MainViewModel Make()
            {
                MainViewModel made = SheetWithViewport(out viewport, AddSecond);
                Frame(made, viewport).GridOn = true;
                return made;
            }

            OnCanvas(Make, (model, canvas) =>
            {
                // On the paper, the sheet's grid off: the viewport's own line x 2 000 at paper x 6, a minor one, 8 per cent
                // darker than the white paper, inside its frame alone, as AutoCAD keeps a grid for each viewport.
                Assert.False(model.GridOn);
                var inside = new GeoPoint2(6, 2.5);
                var outside = new GeoPoint2(6, 8.0);
                Assert.Equal(0xEBEBEB, Pixel(canvas, inside));
                Assert.Equal(0xFFFFFF, Pixel(canvas, outside));

                // Through the other viewport, its grid off, the first's is drawn still.
                Assert.True(model.Document.EnterViewport(other));
                Assert.Equal(0xEBEBEB, Pixel(canvas, inside));

                // Switched off in the first viewport, its grid goes.
                model.Document.LeaveViewport();
                Frame(model, viewport).GridOn = false;
                Assert.Equal(0xFFFFFF, Pixel(canvas, inside));
            });
        }

        [Fact]
        public void GridStyle1_DrawsTheGridThroughAViewportAsDots_AsInModelSpace()
        {
            OnCanvas(
                () => SheetWithViewport(out _),
                (model, canvas) =>
                {
                    // GRIDSTYLE 1, dots in 2D model space: through the viewport, which shows model space, the line x 2 000 is
                    // left out between the crossings and a crossing drawn as a dot, as strong as a major line.
                    model.Execute("MSPACE");
                    model.Execute("GRID ON");
                    canvas.ShowGrid = true;
                    Assert.Equal(0xEBEBEB, Pixel(canvas, new GeoPoint2(6, 2.5)));

                    canvas.GridStyle = 1;
                    Assert.Equal(0xFFFFFF, Pixel(canvas, new GeoPoint2(6, 2.5)));
                    Assert.Equal(0xD2D2D2, Pixel(canvas, new GeoPoint2(6, 3.5)));

                    // GRIDSTYLE 4, dots in layouts, leaves the grid through the viewport in lines.
                    canvas.GridStyle = 4;
                    Assert.Equal(0xEBEBEB, Pixel(canvas, new GeoPoint2(6, 2.5)));
                }
            );
        }

        [Fact]
        public void ATwistedViewportsGrid_IsKeptWithinTheLimitsAsTheyAreTurned()
        {
            string viewport = null;
            GeoPoint2 middle = default;

            MainViewModel Make()
            {
                MainViewModel made = SheetWithViewport(out viewport);
                DxfViewport frame = Frame(made, viewport);
                frame.TwistAngle = Math.PI / 6.0;
                frame.GridBoundToLimits = true;
                frame.GridOn = true;

                // The limits of model space 2 000 by 2 000 about the point at the middle of the frame, the grid laid from it.
                middle = CadViewports.ToModel(frame, new GeoPoint2(6, 4.5));
                made.Document.Database.Limmin = new GeoPoint2(middle.X - 1000, middle.Y - 1000);
                made.Document.Database.Limmax = new GeoPoint2(middle.X + 1000, middle.Y + 1000);
                frame.SnapBasePoint = middle;
                return made;
            }

            OnCanvas(Make, (model, canvas) =>
            {
                model.Execute("MSPACE");
                canvas.ShowGrid = true;
                CadView through = CadViewports.ModelView(canvas.View, Frame(model, viewport));
                int[] pixels = Picture(canvas);

                // The view turned by 30 degrees: 900 up the line through the middle lies within the limits, 0.9 of paper
                // across from the middle, beyond the corners (+-0.73) of the two limits corners it was cut by; 1 400 up it
                // lies outside them, still inside the frame. The line through the middle, where the grid is laid from, is a major one.
                Assert.Equal(0xD2D2D2, At(pixels, through, new GeoPoint2(middle.X, middle.Y + 900)));
                Assert.Equal(0xD2D2D2, At(pixels, through, new GeoPoint2(middle.X, middle.Y - 900)));
                Assert.Equal(0xFFFFFF, At(pixels, through, new GeoPoint2(middle.X, middle.Y + 1400)));
                Assert.Equal(0xFFFFFF, At(pixels, through, new GeoPoint2(middle.X + 1400, middle.Y)));
            });
        }
    }
}
