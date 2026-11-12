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
    /// The third round of snap and grid follow-ups, checked against AutoCAD 2021's console: the snap and grid variables of a
    /// layout's paper space kept in its viewport 1, those worked through a viewport kept in that viewport and its grid drawn
    /// through it, and a point typed at SETVAR leaving the last point where it was.
    /// </summary>
    public sealed class SnapFollowUpsThreeTests : IDisposable
    {
        private const int Width = 800;
        private const int Height = 600;

        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>The room with Layout1's viewport 200 by 150 about (150, 100) at 1:20, model space's grid 4 by 6 and snap 10.</summary>
        private MainViewModel RoomWithViewport(out string viewport)
        {
            DxfDatabase db = TestFiles.RoomWithViewport(_folder, out _, out viewport);
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

        private static DxfViewport Frame(MainViewModel model, string handle) =>
            (DxfViewport)model.Document.FindEntity(handle);

        /// <summary>Model space's *Active viewport as the room gives it: snap 10 off, grid 4 by 6 off, the rest as AutoCAD's defaults.</summary>
        private static void ModelSpaceLeft(MainViewModel model)
        {
            DxfViewportTableRecord active = Active(model);
            Assert.Equal(new GeoVector2(10, 10), active.SnapIncrements);
            Assert.Equal(new GeoVector2(4, 6), active.GridIncrements);
            Assert.False(active.SnapEnabled);
            Assert.False(active.GridEnabled);
            Assert.Equal(GeoPoint2.Origin, active.SnapBase);
            Assert.Equal(0.0, active.SnapAngle);
            Assert.Equal(5, (int)active.GridMajor);
        }

        [Fact]
        public void InALayoutsPaperSpace_TheSnapAndGridVariables_AreItsViewport1s_ModelSpacesLeftAsTheyWere()
        {
            MainViewModel model = RoomWithViewport(out _);
            model.SelectSpace(Layout1(model));

            // As AutoCAD 2021 in Layout1's paper space: GRIDUNIT 3,3, GRIDDISPLAY 0, SNAPUNIT 2,2, SNAPMODE 1, GRIDMAJOR 7,
            // SNAPBASE 1,1, SNAPANG 30, SNAP 4 and GRID 6 all go to the sheet's viewport 1, model space's *Active untouched.
            model.Execute("GRIDUNIT 3,3");
            Assert.Equal(new GeoVector2(3, 3), Sheet(model).GridIncrement);
            Assert.Equal(3.0, model.GridSpacingX);
            model.Execute("GRIDDISPLAY 0");
            Assert.False(Sheet(model).GridAdaptive);
            Assert.True(Sheet(model).GridBoundToLimits);
            Assert.Equal(0, model.GridDisplay);
            model.Execute("SNAPUNIT 2,2");
            Assert.Equal(new GeoVector2(2, 2), Sheet(model).SnapIncrement);
            Assert.Equal(2.0, model.Snap.GridX);
            model.Execute("SNAPMODE 1");
            Assert.True(Sheet(model).SnapOn);
            Assert.True(model.Snap.GridSnap);
            model.Execute("GRIDMAJOR 7");
            Assert.Equal(7, (int)Sheet(model).GridMajor);
            model.Execute("SNAPBASE 1,1");
            Assert.Equal(new GeoPoint2(1, 1), Sheet(model).SnapBasePoint);
            model.Execute("SNAPANG 30");
            Assert.Equal(Math.PI / 6.0, Sheet(model).SnapAngle, 9);
            model.Execute("SNAP 4");
            Assert.Equal(new GeoVector2(4, 4), Sheet(model).SnapIncrement);
            model.Execute("GRID 6");
            Assert.Equal(new GeoVector2(6, 6), Sheet(model).GridIncrement);
            Assert.True(Sheet(model).GridOn);
            ModelSpaceLeft(model);

            // Model space shows its own again, and the layout its own when it comes back.
            model.SelectSpace(model.Document.ModelSpace);
            model.Execute("GRIDUNIT");
            Assert.Equal("Enter new value for GRIDUNIT <4.0000,6.0000>:", model.Prompt);
            model.Enter();
            Assert.Equal(10.0, model.Snap.GridX);
            Assert.False(model.Snap.GridSnap);
            Assert.Equal(3, model.GridDisplay);

            model.SelectSpace(Layout1(model));
            Assert.Equal(4.0, model.Snap.GridX);
            Assert.True(model.Snap.GridSnap);
            Assert.Equal(6.0, model.GridSpacingX);
            Assert.Equal(7, model.GridMajor);
        }

        [Fact]
        public void ThroughAViewport_TheSnapAndGridVariables_AreThatViewports()
        {
            MainViewModel model = RoomWithViewport(out string viewport);
            model.SelectSpace(Layout1(model));
            model.Execute("MSPACE");
            Assert.Equal(viewport, model.Document.ActiveViewport.Handle);
            DxfViewport frame = Frame(model, viewport);

            // As AutoCAD 2021 through Layout1's viewport: GRIDUNIT 5,5, GRIDDISPLAY 1, GRIDMODE 1, SNAPUNIT 0.25,0.25, SNAP 8
            // and GRID 9 go to that viewport, neither the sheet's viewport 1 nor model space's *Active.
            model.Execute("GRIDUNIT 5,5");
            Assert.Equal(new GeoVector2(5, 5), frame.GridIncrement);
            model.Execute("GRIDDISPLAY 1");
            Assert.False(frame.GridAdaptive);
            Assert.False(frame.GridBoundToLimits);
            model.Execute("GRIDMODE 1");
            Assert.True(frame.GridOn);
            Assert.True(model.GridOn);
            model.Execute("SNAPUNIT 0.25,0.25");
            Assert.Equal(new GeoVector2(0.25, 0.25), frame.SnapIncrement);
            model.Execute("SNAP 8");
            Assert.Equal(new GeoVector2(8, 8), frame.SnapIncrement);
            Assert.True(frame.SnapOn);
            Assert.Equal(8.0, model.Snap.GridX);
            model.Execute("GRID 9");
            Assert.Equal(new GeoVector2(9, 9), frame.GridIncrement);
            ModelSpaceLeft(model);
            Assert.False(Sheet(model).GridOn);
            Assert.False(Sheet(model).SnapOn);
            Assert.NotEqual(new GeoVector2(9, 9), Sheet(model).GridIncrement);

            // Back on the paper the sheet's own are shown, and through the viewport again its own.
            model.Execute("PSPACE");
            Assert.False(model.GridOn);
            Assert.False(model.Snap.GridSnap);
            model.Execute("GRIDUNIT");
            Assert.Equal("Enter new value for GRIDUNIT <10.0000,10.0000>:", model.Prompt);
            model.Enter();

            model.Execute("MSPACE");
            Assert.True(model.GridOn);
            Assert.True(model.Snap.GridSnap);
            Assert.Equal(9.0, model.GridSpacingX);
            model.Execute("GRID OFF");
            Assert.False(frame.GridOn);
            Assert.False(Active(model).GridEnabled);
        }

        [Fact]
        public void APointTypedAtSetvar_LeavesTheLastPointWhereItWas()
        {
            MainViewModel model = RoomWithViewport(out _);
            model.Execute("ID");
            model.Execute("10,20");
            Assert.Equal(new GeoPoint2(10, 20), model.Session.LastPoint);

            // As AutoCAD 2021: SNAPBASE 3,4, GRIDUNIT 6,7 and SNAPUNIT @2,3 (read from the last point, 12,23) leave LASTPOINT.
            model.Execute("SNAPBASE 3,4");
            Assert.Equal(new GeoPoint2(10, 20), model.Session.LastPoint);
            model.Execute("GRIDUNIT 6,7");
            Assert.Equal(new GeoPoint2(10, 20), model.Session.LastPoint);
            model.Execute("SNAPUNIT @2,3");
            Assert.Equal(12.0, model.Snap.GridX, 9);
            Assert.Equal(23.0, model.Snap.GridY, 9);
            Assert.Equal(new GeoPoint2(10, 20), model.Session.LastPoint);

            // A number, a distance from the last point the way the cursor lies, leaves it too.
            model.Session.Cursor = new GeoPoint2(13, 24);
            model.Execute("GRIDUNIT");
            model.Execute("5");
            Assert.Equal(13.0, Active(model).GridIncrements.X, 9);
            Assert.Equal(new GeoPoint2(10, 20), model.Session.LastPoint);
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

        private static int Pixel(CadCanvas canvas, GeoPoint2 paper)
        {
            canvas.RenderNow();
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            canvas.View.DrawingToPixel(paper.X, paper.Y, out double px, out double py);
            return pixels[(int)Math.Round(py) * Width + (int)Math.Round(px)] & 0xFFFFFF;
        }

        [Fact]
        public void ThroughAViewport_ItsOwnGridIsDrawnInsideItsFrame_NotTheSheets()
        {
            string viewport = null;

            MainViewModel Make()
            {
                MainViewModel made = RoomWithViewport(out viewport);
                DxfViewport sheet = Sheet(made);
                sheet.GridIncrement = new GeoVector2(0.7, 0.7);
                sheet.GridAdaptive = false;
                sheet.GridBoundToLimits = false;

                // The viewport moved onto the sheet of 12 by 9: 8 by 6 about (6, 4.5), the room at 1:500.
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

            OnCanvas(Make, (model, canvas) =>
            {
                // The viewport shows model space at 1:500 about (2 000, 1 500) in its frame about (6, 4.5) on the sheet: its
                // grid line x 2 000, a minor one of 1 000 every 5, lies at paper x 6, 8 per cent darker than the white paper.
                var inside = new GeoPoint2(6, 2.5);
                var outside = new GeoPoint2(0.7, 4.25);
                Assert.Equal(0xFFFFFF, Pixel(canvas, inside));
                Assert.Equal(0xFFFFFF, Pixel(canvas, outside));

                // On the paper, the sheet's grid of 0.7 is drawn at x 0.7, x 6 none of its lines; through the
                // viewport the sheet's is not, and the viewport's own is drawn in its frame alone.
                canvas.ShowGrid = true;
                Assert.Equal(0xEBEBEB, Pixel(canvas, outside));
                Assert.Equal(0xFFFFFF, Pixel(canvas, inside));

                model.Execute("MSPACE");
                Assert.Equal(viewport, model.Document.ActiveViewport.Handle);
                Assert.Equal(0xFFFFFF, Pixel(canvas, outside));
                Assert.Equal(0xEBEBEB, Pixel(canvas, inside));
                Assert.Equal(0xFFFFFF, Pixel(canvas, new GeoPoint2(6.5, 2.5)));
            });
        }
    }
}
