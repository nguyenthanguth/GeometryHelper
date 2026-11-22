using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The window's side of AutoLISP's <c>grread</c> (L8, D1459): the keys pressed on the workspace, the cursor's moves, the
    /// left and the right button on the canvas, each given to the routine that waits and never to the command line, the snap
    /// or the shortcut menu.
    /// </summary>
    public sealed class GrReadInputTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private MainViewModel Open()
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            Assert.True(model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        private static HwndSource Host(CadWorkspace workspace)
        {
            var source = new HwndSource(
                new HwndSourceParameters("GrReadInputTests")
                {
                    ParentWindow = new IntPtr(-3),
                    WindowStyle = 0,
                    Width = 1600,
                    Height = 960,
                }
            )
            {
                RootVisual = workspace,
            };
            workspace.Measure(new Size(1600, 960));
            workspace.Arrange(new Rect(0, 0, 1600, 960));
            workspace.UpdateLayout();
            return source;
        }

        private static KeyEventArgs Press(CadWorkspace workspace, HwndSource source, Key key)
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            workspace.RaiseEvent(args);
            return args;
        }

        private static TextCompositionEventArgs Type(CadWorkspace workspace, string text)
        {
            var args = new TextCompositionEventArgs(
                Keyboard.PrimaryDevice,
                new TextComposition(InputManager.Current, workspace, text)
            )
            {
                RoutedEvent = UIElement.PreviewTextInputEvent,
            };
            workspace.RaiseEvent(args);
            return args;
        }

        // What the form gives, as the line after it.
        private static string Value(MainViewModel model, string form)
        {
            model.Execute(form);
            return model.History.Last();
        }

        private static double[] Numbers(string text) =>
            Regex
                .Matches(text, @"-?\d+(\.\d+)?(E[-+]?\d+)?")
                .Cast<Match>()
                .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))
                .ToArray();

        #region Keys

        [Fact]
        public void APrintableKeyAtAGrReadPrompt_ReachesTheRoutine_AndTheCommandLineShowsNothingTyped()
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                var workspace = new CadWorkspace { DataContext = model };
                using (HwndSource source = Host(workspace))
                {
                    model.Execute("(setq g (grread))");
                    Assert.True(model.Session.Prompt.ReadsInput);

                    TextCompositionEventArgs typed = Type(workspace, "A");

                    Assert.True(typed.Handled);
                    Assert.Equal(string.Empty, model.CommandText);
                    Assert.False(model.IsCommandRunning);
                    Assert.Equal("(2 65)", Value(model, "!g"));
                }
            });
        }

        [Theory]
        [InlineData(Key.Enter, 13)]
        [InlineData(Key.Space, 32)]
        [InlineData(Key.Back, 8)]
        [InlineData(Key.Tab, 9)]
        public void EnterSpaceBackspaceAndTabAtAGrReadPrompt_AreGivenAsTheirCodes_NotAsCommands(Key key, int code)
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                var workspace = new CadWorkspace { DataContext = model };
                using (HwndSource source = Host(workspace))
                {
                    model.Execute("(setq g (grread))");

                    KeyEventArgs pressed = Press(workspace, source, key);

                    Assert.True(pressed.Handled);
                    Assert.Equal(string.Empty, model.CommandText);
                    Assert.Equal("(2 " + code + ")", Value(model, "!g"));
                }
            });
        }

        [Fact]
        public void AKeyLoop_ReadsEachKeyPressed_AndEndsAtEnter()
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                var workspace = new CadWorkspace { DataContext = model };
                using (HwndSource source = Host(workspace))
                {
                    model.Execute("(setq s \"\")");
                    model.Execute("(while (/= 13 (cadr (setq g (grread)))) (setq s (strcat s (chr (cadr g)))))");

                    Type(workspace, "L");
                    Type(workspace, "i");
                    Type(workspace, "ne");
                    Assert.True(model.IsCommandRunning);
                    Press(workspace, source, Key.Enter);

                    Assert.False(model.IsCommandRunning);
                    Assert.Equal("\"Line\"", Value(model, "!s"));
                    Assert.DoesNotContain("LINE", model.History.Select(h => h.ToUpperInvariant()).Skip(3));
                }
            });
        }

        [Fact]
        public void EscAtAGrReadPrompt_CancelsTheRoutine()
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                var workspace = new CadWorkspace { DataContext = model };
                using (HwndSource source = Host(workspace))
                {
                    model.Execute("(grread T)");

                    KeyEventArgs pressed = Press(workspace, source, Key.Escape);

                    Assert.True(pressed.Handled);
                    Assert.False(model.IsCommandRunning);
                    Assert.Equal("; error: Function cancelled", model.History.Last());
                }
            });
        }

        [Fact]
        public void AKeyWhereNoRoutineReads_GoesToTheCommandLineAsBefore()
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                var workspace = new CadWorkspace { DataContext = model };
                using (HwndSource source = Host(workspace))
                {
                    TextCompositionEventArgs typed = Type(workspace, "L");

                    Assert.True(typed.Handled);
                    Assert.Equal("L", model.CommandText);
                }
            });
        }

        #endregion

        #region The mouse

        private static void OnCanvas(MainViewModel model, Action<CadCanvas> test)
        {
            var canvas = new CadCanvas
            {
                Document = model.Document,
                Space = model.Space,
                View = model.View,
                Session = model.Session,
                Snap = model.Snap,
            };
            canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
            canvas.Measure(new Size(800, 600));
            canvas.Arrange(new Rect(0, 0, 800, 600));
            canvas.RenderNow();
            test(canvas);
            canvas.Dispose();
        }

        [Fact]
        public void TheCursorMoving_ReachesTheRoutineOnlyWhereItReadsMoves()
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                OnCanvas(model, canvas =>
                {
                    model.Execute("(setq g (grread))");
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, new Point(300, 200));

                    // No track: the move is not given, the routine goes on waiting.
                    Assert.True(model.IsCommandRunning);

                    model.Session.Escape();
                    model.Execute("(setq g (grread T))");
                    canvas.View.ScreenToDrawing(310, 210, out double x, out double y);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, new Point(310, 210));

                    Assert.False(model.IsCommandRunning);
                    double[] given = Numbers(Value(model, "!g"));
                    Assert.Equal(5.0, given[0]);
                    Assert.Equal(x, given[1], 3);
                    Assert.Equal(y, given[2], 3);
                });
            });
        }

        [Fact]
        public void ALeftClickAtAGrReadPrompt_GivesThePointAsItLies_WithNoSnap()
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                OnCanvas(model, canvas =>
                {
                    // The room's bottom wall runs along y = 0 from the origin: a click a hair off its end would snap there.
                    model.Execute("(setq g (grread))");
                    Point at = CanvasMouseTests.At(canvas, 3.0, 4.0);
                    canvas.View.ScreenToDrawing(at.X, at.Y, out double x, out double y);

                    CanvasMouseTests.Click(canvas, at);

                    Assert.False(model.IsCommandRunning);
                    double[] given = Numbers(Value(model, "!g"));
                    Assert.Equal(3.0, given[0]);
                    Assert.Equal(x, given[1], 3);
                    Assert.Equal(y, given[2], 3);
                });
            });
        }

        [Fact]
        public void ARightClickAtAGrReadPrompt_IsGivenAsEleven_AndOpensNoMenuAndIsNoEnter()
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                OnCanvas(model, canvas =>
                {
                    int asked = 0;
                    canvas.ShortcutMenuRequested += (s, e) => asked++;

                    // SHORTCUTMENU 4: the menu at every right click while a command runs.
                    canvas.ShortcutMenu = 4;
                    model.Execute("(setq g (grread))");

                    CanvasMouseTests.Click(canvas, new Point(300, 200), MouseButton.Right);

                    Assert.Equal(0, asked);
                    Assert.False(model.IsCommandRunning);
                    Assert.Equal("(11 0)", Value(model, "!g"));
                });
            });
        }

        [Fact]
        public void TheMiddleButtonAndTheWheel_PanAndZoomAsEver_AndGiveTheRoutineNothing()
        {
            OnSta(() =>
            {
                MainViewModel model = Open();
                OnCanvas(model, canvas =>
                {
                    model.Execute("(setq g (grread T))");
                    double zoom = canvas.View.PixelsPerUnit;
                    Point before = CanvasMouseTests.At(canvas, 1000, 1000);

                    // No move before the press: the move that would answer a grread with track is not part of a pan.
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, new Point(100, 100), MouseButton.Middle);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, new Point(150, 130));
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, new Point(150, 130), MouseButton.Middle);
                    Point after = CanvasMouseTests.At(canvas, 1000, 1000);

                    Assert.Equal(before.X + 50, after.X, 6);
                    Assert.Equal(before.Y + 30, after.Y, 6);
                    Assert.True(model.IsCommandRunning);

                    CanvasMouseTests.Mouse(canvas, UIElement.MouseWheelEvent, new Point(300, 200), delta: 120);

                    Assert.True(canvas.View.PixelsPerUnit > zoom);
                    Assert.True(model.IsCommandRunning);
                });
            });
        }

        #endregion
    }
}
