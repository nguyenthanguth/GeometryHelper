using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The cells of a table on the canvas, to the designs TableCellGrips, TableCellRange, TableCellMenu and TableCellDrags: the
    /// column letters and row numbers (TABLEINDICATOR), a range dragged across cells, the cell's grips and the whole table's,
    /// the autofill and column width drags, and the right-click menu of the cells, with every dialog stubbed.
    /// </summary>
    public sealed class TableCellCanvasTests
    {
        private const int Width = 800;
        private const int Height = 600;
        private const int Accent = 0x3D9BFF;

        /// <summary>
        /// Runs a test on an STA thread: a metric drawing with a table of three columns 40 wide, a title 11 high, a header and
        /// two rows of data 9 high, made by TABLE at the origin and zoomed to, under a canvas of 800 by 600 wired to the view
        /// model as the window wires it.
        /// </summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas, string> test)
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
                    string handle = model.Document.ModelSpace.Block.OfType<DxfTable>().Single().Handle;
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                        CellClicked = (point, shift) => model.SelectTableCellAt(point, shift),
                        CellOutline = () => model.TableCellOutline(),
                        CellsLetGo = () => model.ClearTableCell(),
                        CellRangeDragged = (anchor, cursor) => model.SelectTableRange(anchor, cursor),
                        CellGripPressed = grip => model.StartTableCellDrag(grip),
                        TableDragEnded = drag => model.EndTableDrag(drag),
                    };
                    model.CellGripDragRequested = grip => canvas.DragCellGrip(grip);
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.Measure(new Size(Width, Height));
                    canvas.Arrange(new Rect(0, 0, Width, Height));
                    canvas.RenderNow();
                    model.ZoomExtents();
                    canvas.RenderNow();
                    test(model, canvas, handle);
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

        private static Point At(CadCanvas canvas, double x, double y) => CanvasMouseTests.At(canvas, x, y);

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

        /// <summary>The colours of the pixels of a box of the canvas, from a point by a size, each counted.</summary>
        private static Dictionary<int, int> Colours(int[] pixels, Point from, double width, double height)
        {
            var counts = new Dictionary<int, int>();

            for (int y = (int)Math.Round(from.Y); y < (int)Math.Round(from.Y + height); y++)
            {
                for (int x = (int)Math.Round(from.X); x < (int)Math.Round(from.X + width); x++)
                {
                    int rgb = pixels[y * Width + x] & 0xFFFFFF;
                    counts[rgb] = counts.TryGetValue(rgb, out int n) ? n + 1 : 1;
                }
            }

            return counts;
        }

        private static int Pixel(int[] pixels, Point at) =>
            pixels[(int)Math.Round(at.Y) * Width + (int)Math.Round(at.X)] & 0xFFFFFF;

        [Fact]
        public void ACellSelected_DrawsTheColumnLettersAndRowNumbers_InTheIndicatorColour_AndTheAutofillDiamond()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));

                    // Column A's band, 16 px over the table's top edge: the indicator colour and a white letter A.
                    Point top = At(canvas, 20, 0);
                    Dictionary<int, int> band = Colours(Pixels(canvas), new Point(top.X - 20, top.Y - 15), 40, 14);
                    Assert.True(band.TryGetValue(0x5B6573, out int fill) && fill > 100);
                    Assert.True(band.Keys.Count(c => (c & 0xFF) > 0xD0 && (c >> 16) > 0xD0) > 0);

                    // Row 3's number, 22 px left of the table, on amber: the cell's row.
                    Point left = At(canvas, 0, -24.5);
                    Dictionary<int, int> numbers = Colours(Pixels(canvas), new Point(left.X - 20, left.Y - 2), 4, 4);
                    Assert.DoesNotContain(0x5B6573, numbers.Keys);

                    // The autofill diamond at B3's bottom right corner, 3 px right of it and 1 below.
                    Point corner = At(canvas, 80, -29);
                    Assert.Equal(Accent, Pixel(Pixels(canvas), new Point(corner.X + 3, corner.Y + 1)));

                    // Table Indicator Color and TABLEINDICATOR 0.
                    canvas.TableIndicatorColor = 0x804020;
                    band = Colours(Pixels(canvas), new Point(top.X - 20, top.Y - 15), 40, 14);
                    Assert.True(band.ContainsKey(0x804020));
                    canvas.TableIndicator = false;
                    band = Colours(Pixels(canvas), new Point(top.X - 20, top.Y - 15), 40, 14);
                    Assert.False(band.ContainsKey(0x804020));
                }
            );
        }

        [Fact]
        public void APressInACellDragged_SelectsTheRange_HeldAtTheTablesEdge_AndGrownToTheMergedTitle()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    CanvasMouseTests.Drag(canvas, At(canvas, 20, -25), At(canvas, 100, -33));
                    Assert.Equal("A3:C4", model.TableCell.CellName);
                    Assert.Equal(0, model.Document.Selection.Count);
                    Assert.Equal("TABLE", model.Document.NextUndo.Name);

                    // Off the table below and right, held at the last row and column; into the title, A to C.
                    CanvasMouseTests.Drag(canvas, At(canvas, 60, -25), At(canvas, 300, -300));
                    Assert.Equal("B3:C4", model.TableCell.CellName);
                    CanvasMouseTests.Drag(canvas, At(canvas, 60, -25), At(canvas, 60, -4));
                    Assert.Equal("A1:C3", model.TableCell.CellName);

                    // A click in a cell still selects it alone.
                    CanvasMouseTests.Click(canvas, At(canvas, 20, -34));
                    Assert.Equal("A4", model.TableCell.CellName);
                }
            );
        }

        [Fact]
        public void TheAutofillDiamond_DraggedDownACell_FillsTheSeries_InOneUndoStep_AndSelectsBoth()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    ((DxfTable)model.Document.FindEntity(handle)).SetValue(2, 0, 1.0);
                    CanvasMouseTests.Click(canvas, At(canvas, 20, -25));
                    Assert.Equal("A3", model.TableCell.CellName);

                    Point diamond = At(canvas, 40, -29);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, diamond);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, diamond);
                    Assert.NotNull(canvas.TableDrag);
                    Assert.Equal(CadTableDragKind.Autofill, canvas.TableDrag.Kind);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 20, -34));
                    Assert.Equal(new DxfCellRange(3, 0, 3, 0), canvas.TableDrag.FillRange);
                    IReadOnlyList<CadMenuItemViewModel> ways = model.TableFillMenu(canvas.TableDrag);
                    Assert.Equal(
                        new[]
                        {
                            "Fill Series",
                            "Fill Series Without Formatting",
                            "Copy Cells",
                            "Copy Cells Without Formatting",
                            "Fill Formatting Only",
                        },
                        ways.Select(w => w.Header).ToArray()
                    );
                    Assert.True(ways[0].IsChecked);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, At(canvas, 20, -34));

                    Assert.Null(canvas.TableDrag);
                    DxfTable table = (DxfTable)model.Document.FindEntity(handle);
                    Assert.Equal(
                        2.0,
                        double.Parse(table.TextString(3, 0), System.Globalization.CultureInfo.InvariantCulture)
                    );
                    Assert.Equal("Autofill", model.Document.NextUndo.Name);
                    Assert.Equal("A3:A4", model.TableCell.CellName);
                }
            );
        }

        [Fact]
        public void AColumnGripOfTheWholeTable_DraggedRight_WidensTheLeftColumn_KeepsTheWidth_AndEscDropsIt()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    model.Document.Selection.Set(new[] { handle });
                    canvas.RenderNow();

                    // The grip between A and B, at 40 on the top edge, dragged 8 to the right.
                    Point grip = At(canvas, 40, 0);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, grip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, grip);
                    Assert.Equal(CadTableDragKind.ColumnLine, canvas.TableDrag.Kind);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 48, 0));
                    Assert.Equal("A width = 48.0000", canvas.TableDrag.Tip);
                    Assert.True(canvas.CancelGesture());
                    Assert.Null(canvas.TableDrag);
                    Assert.Equal(40.0, ((DxfTable)model.Document.FindEntity(handle)).ColumnWidth(0), 9);

                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, grip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, grip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 48, 0));
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, At(canvas, 48, 0));
                    DxfTable table = (DxfTable)model.Document.FindEntity(handle);
                    Assert.Equal(48.0, table.ColumnWidth(0), 6);
                    Assert.Equal(32.0, table.ColumnWidth(1), 6);
                    Assert.Equal("Column Width", model.Document.NextUndo.Name);

                    // The stretch grips are triangles and the breaking grip grey; hovered, it says it is not offered.
                    int[] pixels = Pixels(canvas);
                    Point breaking = At(canvas, 60, -38);
                    Assert.Equal(0x5B6573, Pixel(pixels, new Point(breaking.X, breaking.Y + 8)));
                }
            );
        }

        /// <summary>The box (width, height) of the pixels near a colour within 30 pixels of a point; 0 by 0 where there are none.</summary>
        private static Size Extent(int[] pixels, Point at, int colour)
        {
            int left = int.MaxValue,
                right = int.MinValue,
                top = int.MaxValue,
                bottom = int.MinValue;

            for (int y = (int)at.Y - 30; y <= (int)at.Y + 30; y++)
            {
                for (int x = (int)at.X - 30; x <= (int)at.X + 30; x++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height)
                    {
                        continue;
                    }

                    int rgb = pixels[y * Width + x] & 0xFFFFFF;
                    int off =
                        Math.Abs(((rgb >> 16) & 0xFF) - ((colour >> 16) & 0xFF))
                        + Math.Abs(((rgb >> 8) & 0xFF) - ((colour >> 8) & 0xFF))
                        + Math.Abs((rgb & 0xFF) - (colour & 0xFF));

                    if (off > 60)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            return right < left ? new Size(0, 0) : new Size(right - left + 1, bottom - top + 1);
        }

        /// <summary>How many pixels in a row, from a point one step at a time, are near a colour.</summary>
        private static int Run(int[] pixels, Point from, int dx, int dy, int colour)
        {
            int n = 0;

            while (true)
            {
                int x = (int)Math.Round(from.X) + n * dx,
                    y = (int)Math.Round(from.Y) + n * dy;

                if (x < 0 || y < 0 || x >= Width || y >= Height)
                {
                    return n;
                }

                int rgb = pixels[y * Width + x] & 0xFFFFFF;
                int off =
                    Math.Abs(((rgb >> 16) & 0xFF) - ((colour >> 16) & 0xFF))
                    + Math.Abs(((rgb >> 8) & 0xFF) - ((colour >> 8) & 0xFF))
                    + Math.Abs((rgb & 0xFF) - (colour & 0xFF));

                if (off > 60)
                {
                    return n;
                }

                n++;
            }
        }

        [Theory]
        [InlineData(5, 15)]
        [InlineData(10, 25)]
        public void TheStretchTrianglesOfATable_AreAsLargeAsTheSquare_AtEveryGripSize(int size, int square)
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    model.Document.Selection.Set(new[] { handle });
                    canvas.GripSize = size;
                    int[] pixels = Pixels(canvas);

                    // The width grip at the top right corner points right, the height grip at the bottom left points down, each
                    // out of the table: the run of grip colour from the point to the tip is half the square's side less the dark
                    // edge, 6 or 7 at the default size and 11 or 12 at 10; the old fixed triangle ran 4.
                    int half = square / 2;
                    Assert.InRange(Run(pixels, At(canvas, 120, 0), 1, 0, Accent), half - 2, half);
                    Assert.InRange(Run(pixels, At(canvas, 0, -38), 0, 1, Accent), half - 2, half);
                }
            );
        }

        [Fact]
        public void ATablesStretchGripUnderTheCursor_IsPink_ThePinkOfTheSquares_AndOnlyThatOne()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    model.Document.Selection.Set(new[] { handle });
                    Point width = At(canvas, 120, 0);
                    Point height = At(canvas, 0, -38);
                    Assert.Equal(Accent, Pixel(Pixels(canvas), width));

                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, width);
                    int[] pixels = Pixels(canvas);
                    Assert.Equal(0xFF7F7F, Pixel(pixels, width));
                    Assert.Equal(Accent, Pixel(pixels, height));
                }
            );
        }

        [Fact]
        public void TheBreakingGripAndTheAutofillDiamond_GrowWithTheGripSize_AndKeepTheirSizeAtTheDefault()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    model.Document.Selection.Set(new[] { handle });
                    Point breaking = At(canvas, 60, -38);
                    int[] pixels = Pixels(canvas);
                    Size standard = Extent(pixels, breaking, 0x5B6573);
                    canvas.GripSize = 10;
                    Size large = Extent(Pixels(canvas), breaking, 0x5B6573);
                    Assert.True(large.Width > standard.Width + 4.0);
                    Assert.True(large.Height > standard.Height + 4.0);

                    // The diamond of a cell, 7 pixels from its centre to a point at the default size, 11.7 at 10; 8 across and 1 down is inside only at 10.
                    canvas.GripSize = 5;
                    model.Document.Selection.Clear();
                    Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));
                    Point corner = At(canvas, 80, -29);
                    Assert.NotEqual(Accent, Pixel(Pixels(canvas), new Point(corner.X + 8, corner.Y + 1)));
                    canvas.GripSize = 10;
                    Assert.Equal(Accent, Pixel(Pixels(canvas), new Point(corner.X + 8, corner.Y + 1)));
                }
            );
        }

        [Fact]
        public void TheRightClickMenuOfCells_IsAutoCads_TheWorkingItemsEditTheTable_TheRestGrey()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    Assert.True(model.SelectTableCellAt(new GeoPoint2(20, -25), false));
                    Assert.True(model.TableCellKey("Right", true));
                    IReadOnlyList<CadMenuItemViewModel> menu = model.ShortcutMenu();

                    Assert.Equal(
                        new[]
                        {
                            "Cut",
                            "Copy",
                            "Paste",
                            "",
                            "Cell Style",
                            "Background Fill…",
                            "Alignment",
                            "Borders…",
                            "Locking",
                            "Data Format…",
                            "",
                            "Match Cell",
                            "Remove All Property Overrides",
                            "Data Link…",
                            "",
                            "Insert",
                            "Edit Text",
                            "Manage Content…",
                            "Delete All Contents",
                            "",
                            "Columns",
                            "Rows",
                            "",
                            "Merge",
                            "Unmerge",
                            "",
                            "Properties",
                            "Quick Properties",
                        },
                        menu.Select(m => m.Header).ToArray()
                    );
                    CadMenuItemViewModel Item(string name) => menu.First(m => m.Header == name);
                    Assert.Equal(
                        new[]
                        {
                            "Background Fill…",
                            "Alignment",
                            "Remove All Property Overrides",
                            "Edit Text",
                            "Delete All Contents",
                            "Columns",
                            "Rows",
                            "Merge",
                            "Properties",
                        },
                        menu.Where(m => !m.IsSeparator && m.IsEnabled).Select(m => m.Header).ToArray()
                    );
                    Assert.Equal("Ctrl+1", Item("Properties").Shortcut);
                    Assert.Equal("Del", Item("Delete All Contents").Shortcut);
                    Assert.True(Item("Alignment").Items.Single(a => a.Header == "Top Center").IsChecked);
                    Assert.True(Item("Merge").Items.Single(a => a.Header == "By Row").IsEnabled);
                    Assert.False(Item("Merge").Items.Single(a => a.Header == "By Column").IsEnabled);

                    // Columns › Size Equally: A widened to 60 first, A3:B3's columns are then given their mean, 50, the width kept.
                    Assert.True(CadTableEditor.SetColumnWidths(model.Document, handle, 0, new[] { 60.0 }).Succeeded);
                    Item("Columns").Items.Single(a => a.Header == "Size Equally").Command.Execute(null);
                    DxfTable table = (DxfTable)model.Document.FindEntity(handle);
                    Assert.Equal(
                        new[] { 50.0, 50.0, 40.0 },
                        new[] { table.ColumnWidth(0), table.ColumnWidth(1), table.ColumnWidth(2) }
                    );
                    Assert.Equal("Column Width", model.Document.NextUndo.Name);

                    // Background Fill through the Select Color dialog, stubbed.
                    model.PickColor = dialog =>
                    {
                        dialog.PickIndex(8);
                        return true;
                    };
                    model.ShortcutMenu().First(m => m.Header == "Background Fill…").Command.Execute(null);
                    Assert.Equal("Color 8", model.TableCell.Fill.Name);

                    // Merge › By Row on A3:B4 merges each row, one undo step for both.
                    Assert.True(model.TableCellKey("Down", true));
                    Assert.Equal("A3:B4", model.TableCell.CellName);
                    model.TableCell.MergeByRowCommand.Execute(null);
                    table = (DxfTable)model.Document.FindEntity(handle);
                    Assert.True(table.IsMergedCell(2, 1, out _) && table.IsMergedCell(3, 1, out _));
                    Assert.True(model.Document.Undo());
                    table = (DxfTable)model.Document.FindEntity(handle);
                    Assert.False(table.IsMergedCell(2, 1, out _) || table.IsMergedCell(3, 1, out _));

                    // Rows › Delete is grey where every row is selected; the grey parents open nothing.
                    model.SelectTableRange(new GeoPoint2(20, -5), new GeoPoint2(20, -500));
                    menu = model.ShortcutMenu();
                    Assert.False(Item("Rows").Items.Single(a => a.Header == "Delete").IsEnabled);
                    Assert.False(Item("Cell Style").IsEnabled);
                    Assert.False(Item("Insert").IsEnabled);
                }
            );
        }

        [Fact]
        public void ColumnsAndRowsResize_StartTheDragOfTheCellsRightOrBottomLine_AndAClickMakesIt()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    Assert.True(model.SelectTableCellAt(new GeoPoint2(20, -25), false));
                    CadMenuItemViewModel Resize(string parent) =>
                        model.ShortcutMenu().First(m => m.Header == parent).Items.Single(a => a.Header == "Resize");
                    Assert.True(Resize("Columns").IsEnabled);
                    Assert.True(Resize("Rows").IsEnabled);

                    // Columns › Resize on A3: the line right of A follows the cursor, and a click at 48 makes A 48 wide, the
                    // columns right of it moving along, as a cell's edge grip does.
                    Resize("Columns").Command.Execute(null);
                    Assert.Equal(CadTableDragKind.ColumnLine, canvas.TableDrag.Kind);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 48, -25));
                    Assert.Equal("A width = 48.0000", canvas.TableDrag.Tip);
                    CanvasMouseTests.Click(canvas, At(canvas, 48, -25));
                    Assert.Null(canvas.TableDrag);
                    DxfTable table = (DxfTable)model.Document.FindEntity(handle);
                    Assert.Equal(
                        new[] { 48.0, 40.0, 40.0 },
                        new[] { 0, 1, 2 }.Select(c => Math.Round(table.ColumnWidth(c), 6))
                    );
                    Assert.Equal("Column Width", model.Document.NextUndo.Name);

                    // Rows › Resize: the line under row 3, at -29, dragged down to -35 makes row 3 15 high; Esc first drops it.
                    Resize("Rows").Command.Execute(null);
                    Assert.Equal(CadTableDragKind.RowLine, canvas.TableDrag.Kind);
                    Assert.True(canvas.TableDragKey(Key.Escape));
                    Assert.Null(canvas.TableDrag);
                    Resize("Rows").Command.Execute(null);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 20, -35));
                    Assert.Equal("Row 3 height = 15.0000", canvas.TableDrag.Tip);
                    CanvasMouseTests.Click(canvas, At(canvas, 20, -35));
                    Assert.Equal(15.0, ((DxfTable)model.Document.FindEntity(handle)).RowHeight(2), 6);
                    Assert.Equal("Row Height", model.Document.NextUndo.Name);
                }
            );
        }

        [Fact]
        public void TheDragsOfATable_FollowOsnapAndOrtho_AsTheOtherGripDragsDo()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    // A line below the table, its end at (53, -50).
                    model.Execute("LINE");
                    model.Execute("53,-50");
                    model.Execute("53,-80");
                    model.Escape();
                    model.Snap.IsOn = true;
                    model.Snap.Modes = CadSnapModes.Endpoint;
                    model.Document.Selection.Set(new[] { handle });
                    canvas.RenderNow();

                    // The grip between A and B dragged near the line's end: the line of the columns goes to its 53.
                    Point grip = At(canvas, 40, 0);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, grip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, grip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 52.6, -49.7));
                    Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);
                    Assert.Equal("A width = 53.0000", canvas.TableDrag.Tip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, At(canvas, 52.6, -49.7));
                    Assert.Equal(53.0, ((DxfTable)model.Document.FindEntity(handle)).ColumnWidth(0), 6);

                    // With ORTHO, the corner's stretch grip dragged right and a little down stretches the width alone.
                    model.Snap.IsOn = false;
                    model.Snap.Ortho = true;
                    model.Document.Selection.Set(new[] { handle });
                    canvas.RenderNow();
                    Point corner = At(canvas, 120, -38);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, corner);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, corner);
                    Assert.Equal(CadTableDragKind.Size, canvas.TableDrag.Kind);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 160, -45));
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, At(canvas, 160, -45));
                    DxfTable table = (DxfTable)model.Document.FindEntity(handle);
                    Assert.Equal(160.0, table.Width, 6);
                    Assert.Equal(38.0, table.Height, 6);
                }
            );
        }

        [Fact]
        public void ARowLineDragged_WithDynamicInput_ShowsTheHeightField_LeftOfTheTable_AndTakesAHeightTyped()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    canvas.DynamicInput = true;
                    Assert.True(model.SelectTableCellAt(new GeoPoint2(20, -25), false));

                    // The bottom edge grip of A3, at -29, dragged down to -35: a dimension 30 px left of the table from the row's
                    // top line to the new line, the field of 15.0000 on it.
                    Point grip = At(canvas, 20, -29);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, grip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, grip);
                    Assert.Equal(CadTableDragKind.RowLine, canvas.TableDrag.Kind);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 20, -35));
                    Point middle = At(canvas, 0, -27.5);
                    Dictionary<int, int> field = Colours(
                        Pixels(canvas),
                        new Point(middle.X - 30 - 12, middle.Y + 8),
                        24,
                        2
                    );
                    Assert.True(field.TryGetValue(0x1A1E24, out int fill) && fill > 20);

                    // 12 typed, then Enter: row 3 is 12 high.
                    Assert.True(canvas.TableDragKey(Key.D1));
                    Assert.True(canvas.TableDragKey(Key.D2));
                    Assert.True(canvas.TableDragKey(Key.Enter));
                    Assert.Equal(12.0, ((DxfTable)model.Document.FindEntity(handle)).RowHeight(2), 6);
                }
            );
        }

        [Fact]
        public void ATableWhoseRowsFlowUp_DrawsTheWidthField_OverTheColumnDragged()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    // Mirrored in y = 0, the rows flow up from the origin; the column line between A and B dragged to 48.
                    var table = (DxfTable)model.Document.FindEntity(handle);
                    table.TransformBy(GeoTransform3.Scaling(1.0, -1.0, 1.0));
                    table.RecomputeTableBlock();
                    Assert.Equal(DxfFlowDirection.BottomToTop, table.FlowDirection);
                    canvas.DynamicInput = true;
                    model.Document.Selection.Set(new[] { handle });
                    canvas.RenderNow();
                    Point grip = At(canvas, 40, 0);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, grip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, grip);
                    Assert.Equal(CadTableDragKind.ColumnLine, canvas.TableDrag.Kind);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 48, 0));

                    // The field is on the dimension of A, from 0 to 48, 30 px off the edge the grips are on: its middle over 24.
                    Point middle = At(canvas, 24, 0);
                    Dictionary<int, int> field = Colours(
                        Pixels(canvas),
                        new Point(middle.X - 12, middle.Y + 30 + 8),
                        24,
                        2
                    );
                    Assert.True(field.TryGetValue(0x1A1E24, out int fill) && fill > 20);
                    Assert.True(canvas.CancelGesture());
                }
            );
        }

        [Fact]
        public void TableIndicator_IsKeptOnTheMachine_AndChangesNothingOfTheDrawing()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    // As AutoCAD 2021 keeps it, in the registry: set to 0 it left DBMOD 0, where LUPREC made it 4, and a
                    // session after it read 0.
                    model.Execute("TABLEINDICATOR");
                    model.Execute("0");
                    Assert.False(model.Settings.TableIndicator);
                    Assert.Equal("TABLE", model.Document.NextUndo.Name);
                    model.Execute("TABLEINDICATOR");
                    model.Execute("1");
                    Assert.True(model.Settings.TableIndicator);
                }
            );
        }

        [Fact]
        public void TableIndicator_IsAVariableOf0Or1_AndTableIndicatorColor_IsOfferedForAWholeTable()
        {
            OnCanvas(
                (model, canvas, handle) =>
                {
                    Assert.True(model.Settings.TableIndicator);
                    model.Execute("TABLEINDICATOR");
                    model.Execute("2");
                    Assert.Contains("Requires 0 or 1 only.", model.History);
                    model.Execute("0");
                    Assert.False(model.Settings.TableIndicator);
                    model.Execute("TABLEINDICATOR");
                    model.Execute("1");
                    Assert.True(model.Settings.TableIndicator);

                    model.Document.Selection.Set(new[] { handle });
                    CadMenuItemViewModel item = Assert.Single(model.ShortcutMenu());
                    Assert.Equal("Table Indicator Color…", item.Header);
                    model.PickColor = dialog =>
                    {
                        dialog.PickIndex(1);
                        return true;
                    };
                    item.Command.Execute(null);
                    Assert.Equal(0xFF0000, model.Settings.TableIndicatorColor);
                }
            );
        }
    }
}
