using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas menus as AutoCAD 2021 fills them as it runs: the object menus of its menu file merged into the Edit menu
    /// for the kind of object selected, Recent Input in the Command menu, Pan and Zoom run inside the command, a line after
    /// .Z among the point filters, and SHORTCUTMENU, its bits as AutoCAD's (probed: 0 to 31 taken, 11 at start). The room has
    /// its bottom wall from (0, 0) to (4 000, 0), a column of radius 100 about (2 000, 1 500) and its top wall from (0, 3 000)
    /// to (4 000, 3 000); the texts NOTE stand at (500, 500) and (500, 800), the polyline runs open from (1 000, 500) to
    /// (2 000, 500) to (2 000, 1 000).
    /// </summary>
    public sealed class CanvasMenuFollowUpTests
    {
        private static MainViewModel Open(out string bottom, out string[] texts, out string polyline)
        {
            DxfDatabase db = TestFiles.Room(out bottom, out _);
            var first = new DxfDBText
            {
                Position = new GeoPoint3(500, 500, 0),
                Height = 50,
                TextString = "NOTE",
            };
            var second = new DxfDBText
            {
                Position = new GeoPoint3(500, 800, 0),
                Height = 50,
                TextString = "NOTE",
            };
            var path = new DxfPolyline();
            path.AddVertexAt(0, new GeoPoint2(1000, 500), 0, 0, 0);
            path.AddVertexAt(1, new GeoPoint2(2000, 500), 0, 0, 0);
            path.AddVertexAt(2, new GeoPoint2(2000, 1000), 0, 0, 0);
            db.ModelSpace.AppendEntity(first);
            db.ModelSpace.AppendEntity(second);
            db.ModelSpace.AppendEntity(path);
            texts = new[] { first.Handle, second.Handle };
            polyline = path.Handle;
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(db, "menus.dxf"));
            return model;
        }

        private static string[] Headers(IEnumerable<CadMenuItemViewModel> items) =>
            items.Select(i => i.Header).ToArray();

        private static CadMenuItemViewModel Item(IEnumerable<CadMenuItemViewModel> items, string header) =>
            items.Single(i => i.Header == header);

        [Fact]
        public void OneTextSelected_PutsAutoCadsTextMenuFirstInTheEditMenu()
        {
            MainViewModel model = Open(out _, out string[] texts, out _);
            model.Document.Selection.Set(new[] { texts[0] });

            IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

            Assert.Equal(
                new[] { "Edit...", "Annotative Object Scale", string.Empty, "Clipboard" },
                Headers(items).Take(4)
            );
            Assert.Equal('i', items[0].AccessKey);
            Assert.True(items[0].IsEnabled);
            Assert.False(items[1].IsEnabled);
        }

        [Fact]
        public void TwoTexts_OrObjectsOfTwoKinds_MergeNoObjectMenu()
        {
            MainViewModel model = Open(out string bottom, out string[] texts, out _);

            model.Document.Selection.Set(texts);
            Assert.Equal("Clipboard", model.CanvasMenu()[0].Header);

            model.Document.Selection.Set(new[] { texts[0], bottom });
            Assert.Equal("Clipboard", model.CanvasMenu()[0].Header);

            model.Document.Selection.Set(new[] { bottom });
            Assert.Equal("Clipboard", model.CanvasMenu()[0].Header);
        }

        [Fact]
        public void OnePolyline_OffersThePolylineMenu_WhoseCloseClosesIt()
        {
            MainViewModel model = Open(out _, out _, out string polyline);
            model.Document.Selection.Set(new[] { polyline });

            CadMenuItemViewModel menu = model.CanvasMenu()[0];

            Assert.Equal("Polyline", menu.Header);
            Assert.Equal(
                new[]
                {
                    "Edit Polyline",
                    string.Empty,
                    "Close",
                    "Open",
                    string.Empty,
                    "Join",
                    "Reverse",
                    "Width",
                    string.Empty,
                    "Curve Fit",
                    "Spline Fit",
                    "Decurve",
                },
                Headers(menu.Items)
            );
            Assert.Equal(
                new[] { 'E', '\0', 'C', 'O', '\0', 'J', 'R', 'W', '\0', 'F', 'S', 'D' },
                menu.Items.Select(i => i.AccessKey).ToArray()
            );

            Item(menu.Items, "Close").Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            Assert.True(((DxfPolyline)model.Document.FindEntity(polyline)).Closed);
        }

        [Fact]
        public void ADimension_OffersStyleAndPrecision_PrecisionSettingItsPlaces()
        {
            MainViewModel model = Open(out _, out _, out _);
            model.Execute("DIMLINEAR");
            model.Execute("0,0");
            model.Execute("1000,0");
            model.Execute("500,-300");
            DxfDimension dimension = model.Document.ModelSpace.Block.OfType<DxfDimension>().Single();
            model.Document.Selection.Set(new[] { dimension.Handle });

            IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

            Assert.Equal(
                new[]
                {
                    "Dimension Style",
                    "Precision",
                    "Remove Style Overrides",
                    "Edit Constraint",
                    "Dimension Name Format",
                    "Annotative Object Scale",
                    string.Empty,
                    "Clipboard",
                },
                Headers(items).Skip(2).Take(8)
            );
            Assert.Equal("Repeat DIMLINEAR", items[0].Header);
            Assert.Equal(
                new[] { "0", "0.0", "0.00", "0.000", "0.0000", "0.00000", "0.000000" },
                Headers(Item(items, "Precision").Items)
            );
            Assert.Contains(Item(items, "Dimension Style").Items, i => i.Header == dimension.DimensionStyleName);

            Item(Item(items, "Precision").Items, "0.00").Command.Execute(null);

            Assert.Equal(2, ((DxfDimension)model.Document.FindEntity(dimension.Handle)).Dimdec);
        }

        [Fact]
        public void AWholeTable_OffersAutoCadsTableMenu_WhoseSizeColumnsEquallyGivesEachTheMean()
        {
            MainViewModel model = Open(out _, out _, out _);
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
            Assert.True(CadTableEditor.SetColumnWidths(model.Document, handle, 0, new[] { 100.0 }).Succeeded);
            model.Document.Selection.Set(new[] { handle });

            IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

            Assert.Equal(
                new[]
                {
                    "Table Style",
                    "Size Columns Equally",
                    "Size Rows Equally",
                    "Remove All Property Overrides",
                    "Export...",
                    "Table Indicator Color...",
                    string.Empty,
                    "Clipboard",
                },
                Headers(items).Skip(2).Take(8)
            );
            Assert.False(Item(items, "Table Style").IsEnabled);
            Assert.True(Item(items, "Export...").IsEnabled);

            Item(items, "Size Columns Equally").Command.Execute(null);

            var table = (DxfTable)model.Document.FindEntity(handle);
            Assert.Equal(60, table.ColumnWidth(0), 9);
            Assert.Equal(60, table.ColumnWidth(1), 9);
            Assert.Equal(60, table.ColumnWidth(2), 9);
        }

        [Fact]
        public void AViewport_OffersAutoCadsViewportMenu_WhoseDisplayLockedLocksIt()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.OpenDocument(
                    CadDocument.From(TestFiles.RoomWithViewport(folder, out _, out string viewport), "sheet.dxf")
                );
                model.Document.CurrentSpace = model.Document.Spaces.First(s => !s.IsModel);
                model.Document.Selection.Set(new[] { viewport });

                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

                Assert.Equal(
                    new[]
                    {
                        "Maximize Viewport",
                        "Viewport Clip",
                        "Display Viewport Objects",
                        "Display Locked",
                        "Remove Viewport Overrides for All Layers",
                        "Shade plot",
                        string.Empty,
                        "Clipboard",
                    },
                    Headers(items).Take(8)
                );

                Item(Item(items, "Display Locked").Items, "Yes").Command.Execute(null);

                Assert.True(((DxfViewport)model.Document.FindEntity(viewport)).Locked);
            }
        }

        [Fact]
        public void TheCommandMenu_OffersTheAnswersTyped_AsRecentInput_NewestFirst()
        {
            MainViewModel model = Open(out _, out _, out _);
            int lines = model.Document.ModelSpace.Block.OfType<DxfLine>().Count();
            model.StartCommand("LINE");
            model.Execute("0,0");
            model.Execute("1000,0");
            model.Execute("1000,1000");

            CadMenuItemViewModel recent = Item(model.CanvasMenu(), "Recent Input");

            Assert.Equal(new[] { "1000.0000,1000.0000", "1000.0000,0.0000", "0.0000,0.0000" }, Headers(recent.Items));

            Item(recent.Items, "0.0000,0.0000").Command.Execute(null);

            Assert.True(model.IsCommandRunning);
            Assert.Equal(lines + 3, model.Document.ModelSpace.Block.OfType<DxfLine>().Count());
            Assert.Contains("Specify next point or [Close/Undo]: 0.0000,0.0000", model.History);
        }

        [Fact]
        public void PanAndZoomOfTheCommandMenu_RunInsideTheCommand_GreyInZoom()
        {
            MainViewModel model = Open(out _, out _, out _);
            model.StartCommand("LINE");
            model.Execute("0,0");
            model.Execute("1000,0");

            Item(model.CanvasMenu(), "Zoom").Command.Execute(null);

            Assert.StartsWith(">>Specify corner of window", model.Prompt);
            IReadOnlyList<CadMenuItemViewModel> inZoom = model.CanvasMenu();
            Assert.False(Item(inZoom, "Pan").IsEnabled);
            Assert.False(Item(inZoom, "Zoom").IsEnabled);

            model.Execute("E");

            Assert.True(model.IsCommandRunning);
            Assert.Equal("Specify next point or [Undo]:", model.Prompt);
            Assert.Contains("Resuming LINE command.", model.History);
        }

        [Fact]
        public void PointFilters_PartTheOneCoordinateFiltersFromTheTwo()
        {
            MainViewModel model = Open(out _, out _, out _);
            model.StartCommand("LINE");

            CadMenuItemViewModel filters = Item(model.SnapOverrideMenu(), "Point Filters");

            Assert.Equal(new[] { ".X", ".Y", ".Z", string.Empty, ".XY", ".XZ", ".YZ" }, Headers(filters.Items));
            Assert.True(filters.Items[3].IsSeparator);
        }

        [Fact]
        public void ShortcutMenu_IsAutoCadsVariable_ItsBitsChoosingTheMenus()
        {
            MainViewModel model = Open(out string bottom, out _, out _);

            model.Execute("SHORTCUTMENU");
            Assert.Equal("Enter new value for SHORTCUTMENU <11>:", model.Prompt);
            model.Execute("32");
            Assert.Contains("Requires an integer between 0 and 31.", model.History);
            model.Execute("10");
            Assert.Equal(10, model.Settings.ShortcutMenu);

            // Without 1 no Default menu: the right-click is Enter.
            Assert.Null(model.CanvasMenu());
            model.Document.Selection.Set(new[] { bottom });
            Assert.NotNull(model.CanvasMenu());
            model.Document.Selection.Clear();

            // With 4 the Command menu shows at a prompt that offers no options.
            model.Execute("SHORTCUTMENU");
            model.Execute("15");
            model.StartCommand("LINE");
            Assert.Equal(
                new[] { "Enter", "Cancel", string.Empty, "Osnap Overrides" },
                Headers(model.CanvasMenu()).Take(4)
            );

            // With neither 4 nor 8 none.
            model.Escape();
            model.Execute("SHORTCUTMENU");
            model.Execute("3");
            model.StartCommand("LINE");
            model.Execute("0,0");
            Assert.Null(model.CanvasMenu());
        }

        [Fact]
        public void OnTheCanvas_ShortcutMenuSixteen_MakesAQuickRightClickEnter_AndAHeldOneTheMenu()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    int asked = 0;
                    canvas.ShortcutMenuRequested += (s, e) => asked++;
                    canvas.ShortcutMenu = 27;

                    RightClick(canvas, At(canvas, 2000, 2200), 100);
                    Assert.Equal(0, asked);

                    RightClick(canvas, At(canvas, 2000, 2200), 300);
                    Assert.Equal(1, asked);

                    // LINE's next point offers Undo: held, the menu; quick, Enter, ending the line.
                    model.StartCommand("LINE");
                    Click(canvas, At(canvas, 500, 500));
                    Click(canvas, At(canvas, 800, 900));
                    RightClick(canvas, At(canvas, 800, 900), 300);
                    Assert.Equal(2, asked);
                    Assert.True(model.IsCommandRunning);

                    RightClick(canvas, At(canvas, 800, 900), 100);
                    Assert.Equal(2, asked);
                    Assert.False(model.IsCommandRunning);

                    // SHORTCUTMENU 15: the Command menu at "Specify first point:" too.
                    canvas.ShortcutMenu = 15;
                    model.StartCommand("LINE");
                    Click(canvas, At(canvas, 800, 900), MouseButton.Right);
                    Assert.Equal(3, asked);
                    Assert.True(model.IsCommandRunning);
                }
            );
        }

        private static void RightClick(CadCanvas canvas, Point at, int held)
        {
            canvas.Pointer = _ => at;
            Mouse(canvas, UIElement.MouseMoveEvent, at);
            canvas.RaiseEvent(
                new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 1000, MouseButton.Right)
                {
                    RoutedEvent = UIElement.MouseDownEvent,
                }
            );
            canvas.RaiseEvent(
                new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 1000 + held, MouseButton.Right)
                {
                    RoutedEvent = UIElement.MouseUpEvent,
                }
            );
        }

        private static void OnCanvas(Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    MainViewModel model = Open(out _, out _, out _);
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                    };
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.SelectionChanged += (s, e) => model.RefreshHistory();
                    canvas.Measure(new Size(800, 600));
                    canvas.Arrange(new Rect(0, 0, 800, 600));
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
    }
}
