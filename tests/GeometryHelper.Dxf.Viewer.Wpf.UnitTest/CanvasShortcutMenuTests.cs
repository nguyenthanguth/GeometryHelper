using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas's right-click menus, as AutoCAD 2021's with its SHORTCUTMENU 11: the Default menu with nothing selected and no
    /// command, the Edit menu with objects selected, the Command menu while a command offers options (a right-click is Enter
    /// where it offers none), and the Grips menu while a grip edit runs; the items, order and access keys of AutoCAD's own menu
    /// file, what the presenter does not build grey. The room has its bottom wall from (0, 0) to (4 000, 0), a column of radius
    /// 100 about (2 000, 1 500) and its top wall from (0, 3 000) to (4 000, 3 000).
    /// </summary>
    public sealed class CanvasShortcutMenuTests
    {
        private static MainViewModel Open(TestFolder folder, out string bottom)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out bottom, out _))));
            return model;
        }

        private static string[] Headers(IEnumerable<CadMenuItemViewModel> items) =>
            items.Select(i => i.Header).ToArray();

        private static CadMenuItemViewModel Item(IEnumerable<CadMenuItemViewModel> items, string header) =>
            items.Single(i => i.Header == header);

        [Fact]
        public void TheDefaultMenu_WithNothingSelected_IsAutoCads_TheItemsNotBuiltGrey()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _);

                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

                // No command has run yet: no Repeat.
                Assert.Equal(
                    new[]
                    {
                        "Clipboard",
                        string.Empty,
                        "Isolate",
                        string.Empty,
                        "Undo",
                        "Redo",
                        "Pan",
                        "Zoom",
                        "SteeringWheels",
                        string.Empty,
                        "Action Recorder",
                        string.Empty,
                        "Convert PS to MS",
                        "Quick Select...",
                        "QuickCalc",
                        "Find...",
                        "Options...",
                    },
                    Headers(items)
                );
                Assert.Equal(
                    new[]
                    {
                        '\0',
                        '\0',
                        'I',
                        '\0',
                        'U',
                        'd',
                        'a',
                        'Z',
                        '\0',
                        '\0',
                        '\0',
                        '\0',
                        'v',
                        'Q',
                        '\0',
                        'F',
                        'O',
                    },
                    items.Select(i => i.AccessKey).ToArray()
                );
                Assert.Equal(
                    new[] { "SteeringWheels", "Action Recorder", "Convert PS to MS", "QuickCalc" },
                    items.Where(i => !i.IsSeparator && !i.IsEnabled).Select(i => i.Header).ToArray()
                );
                Assert.All(items.Where(i => !i.IsSeparator && !i.IsEnabled && i.Items.Count == 0), i =>
                    Assert.Equal(MainViewModel.NotBuiltYet, i.ToolTip)
                );

                CadMenuItemViewModel clipboard = Item(items, "Clipboard");
                Assert.Equal(
                    new[]
                    {
                        "Cut",
                        "Copy",
                        "Copy with Base Point",
                        "Paste",
                        "Paste as Block",
                        "Paste to Original Coordinates",
                    },
                    Headers(clipboard.Items)
                );
                Assert.Equal(
                    new[] { 't', 'C', 'B', 'P', 'k', 'd' },
                    clipboard.Items.Select(i => i.AccessKey).ToArray()
                );
                Assert.All(clipboard.Items, i => Assert.True(i.IsEnabled));
                Assert.Equal(
                    new[] { "Isolate Objects", "Hide Objects", "End Object Isolation" },
                    Headers(Item(items, "Isolate").Items)
                );

                // Zoom starts ZOOM; after it the menu repeats it, R its key.
                Item(items, "Zoom").Command.Execute(null);

                Assert.True(model.IsCommandRunning);
                Assert.StartsWith("Specify corner of window", model.Prompt);

                model.Escape();
                CadMenuItemViewModel repeat = model.CanvasMenu()[0];

                Assert.Equal("Repeat ZOOM", repeat.Header);
                Assert.Equal('R', repeat.AccessKey);
                Assert.True(model.CanvasMenu()[1].IsSeparator);

                repeat.Command.Execute(null);

                Assert.True(model.IsCommandRunning);
                Assert.StartsWith("Specify corner of window", model.Prompt);
            }
        }

        [Fact]
        public void TheEditMenu_WithObjectsSelected_IsAutoCads_AndItsItemsWorkOnTheSelection()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom);
                model.Document.Selection.Set(new[] { bottom });

                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

                Assert.Equal(
                    new[]
                    {
                        "Clipboard",
                        string.Empty,
                        "Isolate",
                        string.Empty,
                        "Erase",
                        "Move",
                        "Copy Selection",
                        "Scale",
                        "Rotate",
                        "Draw Order",
                        "Group",
                        string.Empty,
                        "Add Selected",
                        string.Empty,
                        "Select Similar",
                        "Deselect All",
                        string.Empty,
                        "Quick Select...",
                        "QuickCalc",
                        "Find...",
                        "Properties",
                        "Quick Properties",
                    },
                    Headers(items)
                );
                Assert.Equal(
                    new[]
                    {
                        '\0',
                        '\0',
                        'I',
                        '\0',
                        '\0',
                        'M',
                        'y',
                        'l',
                        'o',
                        'w',
                        '\0',
                        '\0',
                        'd',
                        '\0',
                        't',
                        'A',
                        '\0',
                        'Q',
                        '\0',
                        'F',
                        's',
                        '\0',
                    },
                    items.Select(i => i.AccessKey).ToArray()
                );
                Assert.Equal(
                    new[] { "Add Selected", "Select Similar", "QuickCalc", "Quick Properties" },
                    items.Where(i => !i.IsSeparator && !i.IsEnabled).Select(i => i.Header).ToArray()
                );

                CadMenuItemViewModel order = Item(items, "Draw Order");
                Assert.Equal(
                    new[]
                    {
                        "Bring to Front",
                        "Send to Back",
                        string.Empty,
                        "Bring Above Objects",
                        "Send Under Objects",
                    },
                    Headers(order.Items)
                );
                Assert.Equal(new[] { 'F', 'B', '\0', 'A', 'U' }, order.Items.Select(i => i.AccessKey).ToArray());
                CadMenuItemViewModel group = Item(items, "Group");
                Assert.Equal(new[] { "Group", "Ungroup", "Add to Group", "Remove from Group" }, Headers(group.Items));
                Assert.Equal(new[] { true, true, false, false }, group.Items.Select(i => i.IsEnabled).ToArray());

                // Bring to Front orders the wall selected at once, DRAWORDER's Front.
                Item(order.Items, "Bring to Front").Command.Execute(null);

                Assert.False(model.IsCommandRunning);
                Assert.Contains(model.History, l =>
                    l.StartsWith("Enter object ordering option", StringComparison.Ordinal) && l.EndsWith(" F")
                );

                // Deselect All lets the wall go; with nothing selected the menu is the Default one.
                model.Document.Selection.Set(new[] { bottom });
                Item(model.CanvasMenu(), "Deselect All").Command.Execute(null);

                Assert.Empty(model.Document.Selection.Handles);
                Assert.Contains(model.CanvasMenu(), i => i.Header == "Options...");

                // Erase erases the selection.
                model.Document.Selection.Set(new[] { bottom });
                Item(model.CanvasMenu(), "Erase").Command.Execute(null);

                Assert.Null(model.Document.FindEntity(bottom));
            }
        }

        [Fact]
        public void TheCommandMenu_OffersThePromptsOptions_AndARightClickWithNoOptionsHasNone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _);
                model.StartCommand("LINE");

                // "Specify first point:" offers no option: a right-click there is Enter, as AutoCAD's SHORTCUTMENU 11 has it.
                Assert.Null(model.CanvasMenu());

                model.Execute("0,0");
                model.Execute("1000,0");
                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

                Assert.Equal(
                    new[]
                    {
                        "Enter",
                        "Cancel",
                        "Recent Input",
                        string.Empty,
                        "Undo",
                        string.Empty,
                        "Osnap Overrides",
                        string.Empty,
                        "Pan",
                        "Zoom",
                        "SteeringWheels",
                        string.Empty,
                        "QuickCalc",
                    },
                    Headers(items)
                );
                Assert.Equal(
                    new[] { 'E', 'C', '\0', '\0', '\0', '\0', 'v', '\0', 'P', 'Z', '\0', '\0', '\0' },
                    items.Select(i => i.AccessKey).ToArray()
                );
                Assert.Equal(
                    new[] { "SteeringWheels", "QuickCalc" },
                    items.Where(i => !i.IsSeparator && !i.IsEnabled).Select(i => i.Header).ToArray()
                );

                // Osnap Overrides opens the Shift+right-click menu's items.
                CadMenuItemViewModel overrides = Item(items, "Osnap Overrides");
                Assert.True(overrides.IsEnabled);
                Assert.Equal(Headers(model.SnapOverrideMenu()), Headers(overrides.Items));

                // An option chosen answers the prompt: Undo takes the line back.
                Item(items, "Undo").Command.Execute(null);

                Assert.True(model.IsCommandRunning);
                Assert.Equal(3, model.Document.Database.ModelSpace.Cast<DxfEntity>().Count());

                // Close, offered after two segments, closes them.
                model.Escape();
                model.StartCommand("LINE");
                model.Execute("0,0");
                model.Execute("1000,0");
                model.Execute("1000,1000");
                Item(model.CanvasMenu(), "Close").Command.Execute(null);

                Assert.False(model.IsCommandRunning);

                // Enter ends a line, Cancel cancels.
                model.StartCommand("LINE");
                model.Execute("0,0");
                model.Execute("500,0");
                Item(model.CanvasMenu(), "Enter").Command.Execute(null);

                Assert.False(model.IsCommandRunning);

                model.StartCommand("LINE");
                model.Execute("0,0");
                model.Execute("500,0");
                Item(model.CanvasMenu(), "Cancel").Command.Execute(null);

                Assert.False(model.IsCommandRunning);
                Assert.EndsWith("*Cancel*", model.History.Last());
            }
        }

        [Fact]
        public void TheCommandMenuAwayFromAPointPrompt_GreysOsnapOverrides()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _);
                model.StartCommand("LAYOUT");

                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

                Assert.Equal(new[] { "New", "Rename", "Set", "?" }, Headers(items).Skip(3).Take(4));
                Assert.False(Item(items, "Osnap Overrides").IsEnabled);
            }
        }

        [Fact]
        public void TheGripsMenu_WhileAGripEditRuns_IsAutoCads_AndSwitchesTheMode()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom);
                model.Document.Selection.Set(new[] { bottom });
                Assert.True(model.Session.StartGripEdit(new[] { new KeyValuePair<string, int>(bottom, 0) }));
                model.RefreshHistory();

                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

                Assert.Equal(
                    new[]
                    {
                        "Enter",
                        string.Empty,
                        "Stretch",
                        "Move",
                        "Rotate",
                        "Scale",
                        "Mirror",
                        string.Empty,
                        "Base Point",
                        "Copy",
                        "Reference",
                        "Undo",
                        string.Empty,
                        "Exit",
                    },
                    Headers(items)
                );
                Assert.Equal(
                    new[] { 'E', '\0', 'S', 'M', 'R', 'l', 'i', '\0', 'B', 'C', 'f', 'U', '\0', 'x' },
                    items.Select(i => i.AccessKey).ToArray()
                );

                // Reference is ROTATE's and SCALE's alone.
                Assert.False(Item(items, "Reference").IsEnabled);

                Item(items, "Rotate").Command.Execute(null);

                Assert.StartsWith("Specify rotation angle", model.Prompt);
                Assert.Contains("** ROTATE **", model.History);
                Assert.True(Item(model.CanvasMenu(), "Reference").IsEnabled);

                // Enter goes on to the next mode, Exit ends the grip edit.
                Item(model.CanvasMenu(), "Enter").Command.Execute(null);

                Assert.StartsWith("Specify scale factor", model.Prompt);

                Item(model.CanvasMenu(), "Exit").Command.Execute(null);

                Assert.False(model.IsCommandRunning);
            }
        }

        [Fact]
        public void TheShiftRightClickMenu_TakesAutoCadsLabels_ApparentIntersectAndInsert_AndItsSeparators()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _);
                model.StartCommand("LINE");

                Assert.Equal(
                    new[]
                    {
                        "Endpoint",
                        "Midpoint",
                        "Intersection",
                        "Apparent Intersect",
                        "Extension",
                        string.Empty,
                        "Center",
                        "Geometric Center",
                        "Quadrant",
                        "Tangent",
                        string.Empty,
                        "Perpendicular",
                        "Parallel",
                        "Node",
                        "Insert",
                        "Nearest",
                        "None",
                    },
                    Headers(model.SnapOverrideMenu())
                        .SkipWhile(h => h != "Endpoint")
                        .TakeWhile(h => h != "Osnap Settings...")
                        .Take(17)
                );
                Assert.Equal('A', Item(model.SnapOverrideMenu(), "Apparent Intersect").AccessKey);
                Assert.Equal('s', Item(model.SnapOverrideMenu(), "Insert").AccessKey);
            }
        }

        [Fact]
        public void OnTheCanvas_ARightClickShowsTheCommandMenuWhereThePromptHasOptions_ElseItIsEnter()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    int asked = 0;
                    canvas.ShortcutMenuRequested += (s, e) => asked++;

                    // No command: the menu.
                    Click(canvas, At(canvas, 2000, 2200), MouseButton.Right);
                    Assert.Equal(1, asked);

                    // LINE's next point offers Undo: the menu, the line going on.
                    model.StartCommand("LINE");
                    Click(canvas, At(canvas, 500, 500));
                    Click(canvas, At(canvas, 800, 900));
                    Click(canvas, At(canvas, 800, 900), MouseButton.Right);

                    Assert.Equal(2, asked);
                    Assert.True(model.IsCommandRunning);

                    // "Select objects:" offers none: Enter, ending ERASE with nothing selected.
                    model.Escape();
                    model.StartCommand("ERASE");
                    Click(canvas, At(canvas, 2000, 2200), MouseButton.Right);

                    Assert.Equal(2, asked);
                    Assert.False(model.IsCommandRunning);
                }
            );
        }

        [Fact]
        public void OnTheCanvas_AClickOnNothingAtSelectObjects_AsksTheOppositeCorner_AndTheNextClickGathers()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.StartCommand("ERASE");
                    canvas.View.ZoomTo(new DisplayBox(-1000, -1000, 5000, 4000));
                    Click(canvas, At(canvas, -200, -200));

                    Assert.Equal("Specify opposite corner or [Fence/WPolygon/CPolygon]:", model.Prompt);
                    Assert.True(model.Session.IsWindowOpen);

                    Click(canvas, At(canvas, 4200, 200));

                    Assert.Equal("Select objects:", model.Prompt);
                    Assert.Contains(
                        "Select objects: Specify opposite corner or [Fence/WPolygon/CPolygon]:",
                        model.History
                    );
                    Assert.Equal("1 found", model.History.Last());
                    Assert.Single(model.Session.Gathering);
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
                    using (var folder = new TestFolder())
                    {
                        MainViewModel model = Open(folder, out _);
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
