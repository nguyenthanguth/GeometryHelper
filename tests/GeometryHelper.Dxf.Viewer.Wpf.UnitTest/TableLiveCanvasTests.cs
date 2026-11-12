using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas while a cell of a table is typed in: the table is drawn from the rows the words typed lay out, its last line
    /// lower as the row grows and back where it was as the lines are taken out, and the table in the drawing is not drawn under it.
    /// </summary>
    public sealed class TableLiveCanvasTests
    {
        private const int Width = 800;
        private const int Height = 600;

        private static void OnCanvas(Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var model = new MainViewModel();
                    model.OpenDocument(CadDocument.From(CadTemplate.CreateMetric(), "tables.dxf"));
                    model.ShowInsertTable = dialog =>
                    {
                        dialog.Columns = "3";
                        dialog.ColumnWidth = "40";
                        dialog.DataRows = "2";
                        dialog.Ok();
                        return true;
                    };
                    model.Execute("TABLE");
                    model.Execute("0,0");
                    model.Escape();
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                    };
                    model.TableLiveChanged += (s, e) => canvas.RefreshTableLive();
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.Measure(new Size(Width, Height));
                    canvas.Arrange(new Rect(0, 0, Width, Height));
                    canvas.RenderNow();
                    model.ZoomExtents();
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

        private static int[] Pixels(CadCanvas canvas)
        {
            canvas.RenderNow();
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            return pixels;
        }

        /// <summary>Whether anything but the background is drawn within two pixels of a point of the drawing.</summary>
        private static bool IsInked(CadCanvas canvas, int[] pixels, double x, double y)
        {
            Point at = CanvasMouseTests.At(canvas, x, y);
            int background = pixels[5 * Width + 5] & 0xFFFFFF;

            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int px = (int)Math.Round(at.X) + dx;
                    int py = (int)Math.Round(at.Y) + dy;

                    if ((pixels[py * Width + px] & 0xFFFFFF) != background)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [Fact]
        public void TheLastLineOfTheTable_FollowsTheRowGrowingAsTheLinesAreTyped_AndBackAsTheyAreTakenOut()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    // Title 11, header 9 and two rows of data 9: the table ends at -38 until the third row grows.
                    Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));
                    Assert.True(model.TableCellKey("F2", false));
                    int[] before = Pixels(canvas);
                    Assert.True(IsInked(canvas, before, 60, -38));

                    model.TextEditor.Editor.Type("one\ntwo\nthree");
                    double grown = model.TextEditor.Edit.TableLive.RowHeights[2] - 9.0;
                    Assert.True(grown > 5.0);
                    int[] typed = Pixels(canvas);
                    Assert.False(IsInked(canvas, typed, 60, -38));
                    Assert.True(IsInked(canvas, typed, 60, -38 - grown));

                    model.TextEditor.Editor.SelectAll();
                    model.TextEditor.Editor.Type("one");
                    int[] back = Pixels(canvas);
                    Assert.True(IsInked(canvas, back, 60, -38));
                    Assert.False(IsInked(canvas, back, 60, -38 - grown));
                }
            );
        }
    }
}
