using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GeometryHelper.Cad.Python;
using GeometryHelper.UnitTest;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonConsoleKit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The PYTHON palette as the window draws it (D720, the design in <c>design/notes.md</c>): its view model over a fake host,
    /// in a window of messages that is never shown, and the keys, buttons and lines read off the controls. The palette's
    /// place in the workspace and the keys of the window are tested with a <see cref="CadWorkspace"/> at the end.
    /// </summary>
    public sealed class PythonConsolePaletteTests
    {
        /// <summary>A palette over a fake host and a drawing, laid out in a window of messages 900 by 400.</summary>
        private sealed class Rig : IDisposable
        {
            private readonly HwndSource _source;

            public Rig(Action<FakePythonConsoleHost> prepare)
            {
                prepare?.Invoke(Host);
                Model = new PythonConsoleViewModel(Host, command => { }, () => SettingsOpened++);
                Model.Follow(Session.Document, Session);
                Palette = new PythonConsolePalette { DataContext = Model };
                _source = PythonWindowKit.Host(Palette, 900, 400);
            }

            public FakePythonConsoleHost Host { get; } = new FakePythonConsoleHost();

            public CadCommandSession Session { get; } = Drawing("plan.dxf");

            public PythonConsoleViewModel Model { get; }

            public PythonConsolePalette Palette { get; }

            public int SettingsOpened { get; private set; }

            public void Dispose() => _source.Dispose();
        }

        private static void OnRig(Action<Rig> test, Action<FakePythonConsoleHost> prepare = null) =>
            OnWindowThread(() =>
            {
                using (var rig = new Rig(prepare))
                {
                    test(rig);
                }
            });

        /// <summary>The key goes down on an element, as the window routes it: tunnelling from the root.</summary>
        /// <returns>Whether a handler took it.</returns>
        private static bool Press(FrameworkElement target, Key key)
        {
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            target.RaiseEvent(e);
            return e.Handled;
        }

        private static Button ButtonNamed(FrameworkElement root, string name) =>
            Descendants(root)
                .OfType<Button>()
                .Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == name);

        /// <summary>The button pressed: its command run, as a click runs it; fails where the button is greyed.</summary>
        private static void Click(FrameworkElement root, string name)
        {
            Button button = ButtonNamed(root, name);
            Assert.True(button.IsEnabled, name + " is greyed.");
            button.Command.Execute(button.CommandParameter);
        }

        private static string[] Paragraphs(PythonConsolePalette palette) =>
            palette
                .TranscriptBox.Document.Blocks.OfType<Paragraph>()
                .Select(p => new TextRange(p.ContentStart, p.ContentEnd).Text)
                .ToArray();

        private static Color ColourOf(Inline inline) => ((SolidColorBrush)inline.Foreground).Color;

        private static bool IsShown(FrameworkElement root, string text) =>
            Descendants(root).OfType<TextBlock>().Any(t => t.Text == text && t.IsVisible);

        private static void Type(Rig rig, string text)
        {
            rig.Palette.Input.Text = text;
            Assert.Equal(text, rig.Model.InputText);
        }

        private static void Print(Rig rig, int lines)
        {
            rig.Host.Start("PYRUN", rig.Session.Document);

            for (int i = 1; i <= lines; i++)
            {
                rig.Host.Print("line " + i);
            }

            rig.Host.Finish("PYRUN", rig.Session.Document);
        }

        // ---- The input's keys

        [Fact]
        public void Enter_InTheInput_RunsTheEntry_EchoesIt_AndEmptiesTheBox_WithNoLineBreakLeftInIt()
        {
            OnRig(rig =>
            {
                Type(rig, "x = 2 + 3");

                bool taken = Press(rig.Palette.Input, Key.Enter);

                Assert.True(taken);
                Assert.Equal("x = 2 + 3", Assert.Single(rig.Host.Entered).Text);
                Assert.Equal("", rig.Palette.Input.Text);
                Assert.Equal(new[] { ">>> x = 2 + 3" }, Paragraphs(rig.Palette));
            });
        }

        [Fact]
        public void TheInputIsABoxOfSeveralLines_SoShiftEnterAddsOne_AndItGrowsToSixLinesThenScrolls()
        {
            OnRig(rig =>
            {
                // Enter is taken by the palette; Shift+Enter is let through to the box, which then adds a line.
                Assert.True(rig.Palette.Input.AcceptsReturn);
                Assert.Equal(6 * 16, rig.Palette.Input.MaxHeight);
                Assert.Equal(ScrollBarVisibility.Auto, rig.Palette.Input.VerticalScrollBarVisibility);
            });
        }

        [Fact]
        public void AMarkStandsBeforeEachLineOfTheInput_TheFirstAsGreaterThans_TheOthersAsDots()
        {
            OnRig(rig =>
            {
                Type(rig, "def f():\n    return 1\n");
                Layout(rig.Palette, 900, 400);

                TextBlock marks = Descendants(rig.Palette).OfType<TextBlock>().Single(t => t.Text.StartsWith(">>>"));

                Assert.Equal(new[] { ">>>", "...", "..." }, marks.Text.Split('\n'));
            });
        }

        [Fact]
        public void UpAtTheFirstLine_BringsBackTheLastEntry_AndDownGoesForwardAgain()
        {
            OnRig(rig =>
            {
                Type(rig, "a = 1");
                Press(rig.Palette.Input, Key.Enter);
                Type(rig, "b = 2");
                Press(rig.Palette.Input, Key.Enter);

                Assert.True(Press(rig.Palette.Input, Key.Up));
                Assert.Equal("b = 2", rig.Palette.Input.Text);
                Assert.True(Press(rig.Palette.Input, Key.Up));
                Assert.Equal("a = 1", rig.Palette.Input.Text);
                Assert.True(Press(rig.Palette.Input, Key.Down));
                Assert.Equal("b = 2", rig.Palette.Input.Text);
                Assert.True(Press(rig.Palette.Input, Key.Down));
                Assert.Equal("", rig.Palette.Input.Text);
            });
        }

        [Fact]
        public void UpInALaterLineOfTheInput_IsLeftToTheBoxToMoveItsCaret_AndTheHistoryIsNotWalked()
        {
            OnRig(rig =>
            {
                Type(rig, "a = 1");
                Press(rig.Palette.Input, Key.Enter);
                Type(rig, "if a:\n    b = 2");
                rig.Palette.Input.CaretIndex = rig.Palette.Input.Text.Length;

                bool taken = Press(rig.Palette.Input, Key.Up);

                Assert.False(taken);
                Assert.Equal("if a:\n    b = 2", rig.Palette.Input.Text);
            });
        }

        [Fact]
        public void Tab_InTheInput_IndentsByFourSpaces_AndDoesNotLeaveTheBox()
        {
            OnRig(rig =>
            {
                Type(rig, "if a:\n");
                rig.Palette.Input.CaretIndex = rig.Palette.Input.Text.Length;

                bool taken = Press(rig.Palette.Input, Key.Tab);

                Assert.True(taken);
                Assert.Equal("if a:\n    ", rig.Palette.Input.Text);
            });
        }

        // ---- The transcript

        [Fact]
        public void TheFourKindsOfLine_AreDrawnInTheDesignsColours()
        {
            OnRig(rig =>
            {
                Type(rig, "x");
                Press(rig.Palette.Input, Key.Enter);
                rig.Host.Start("PYRUN", rig.Session.Document);
                rig.Host.Print("5");
                rig.Host.PrintError("ZeroDivisionError: division by zero");
                rig.Host.Finish("PYRUN", rig.Session.Document);

                Paragraph[] lines = rig.Palette.TranscriptBox.Document.Blocks.OfType<Paragraph>().ToArray();

                // echo: ">>> " in #6CB6FF, the code in #E6E9ED; host #9AA4B1; output #E6E9ED; error #FF8A7A.
                Assert.Equal(4, lines.Length);
                Inline[] echo = lines[0].Inlines.ToArray();
                Assert.Equal(Color.FromRgb(0x6C, 0xB6, 0xFF), ColourOf(echo[0]));
                Assert.Equal(Color.FromRgb(0xE6, 0xE9, 0xED), ColourOf(echo[1]));
                Assert.Equal(Color.FromRgb(0x9A, 0xA4, 0xB1), ColourOf(lines[1].Inlines.Last()));
                Assert.Equal(Color.FromRgb(0xE6, 0xE9, 0xED), ColourOf(lines[2].Inlines.Last()));
                Assert.Equal(Color.FromRgb(0xFF, 0x8A, 0x7A), ColourOf(lines[3].Inlines.Last()));
            });
        }

        [Fact]
        public void TheTranscriptIsReadOnly_SoTypedTextCannotChangeWhatTheHostWrote()
        {
            OnRig(rig => Assert.True(rig.Palette.TranscriptBox.IsReadOnly));
        }

        [Fact]
        public void After5003Lines_TheTranscriptHolds5000Paragraphs_TheOldestGone()
        {
            OnRig(rig =>
            {
                Print(rig, 5003);

                string[] shown = Paragraphs(rig.Palette);

                // The host line of the run's start and 5 003 lines are 5 004; the first four are dropped.
                Assert.Equal(5000, shown.Length);
                Assert.Equal("line 4", shown[0]);
                Assert.Equal("line 5003", shown[4999]);
            });
        }

        [Fact]
        public void AScriptThatPrints20000Lines_FillsTheTranscriptInUnderTenSeconds()
        {
            // Every print of a script is one Output line, raised on the window's thread while the script runs: the transcript
            // keeps the last 5 000, and each new line must cost no more than the ten thousand before it did. One attempt: the
            // limit is far above what a good transcript takes, and a slow one takes several minutes for each.
            OnRig(rig =>
            {
                SpeedGuard.AssertWithin(10.0, () => Print(rig, 20000), attempts: 1);

                Assert.Equal(5000, Paragraphs(rig.Palette).Length);
            });
        }

        [Fact]
        public void Clear_EmptiesTheTranscriptOfTheControl_AndFollowingAnotherDrawingShowsItsOwn()
        {
            OnRig(rig =>
            {
                CadCommandSession other = Drawing("other.dxf");
                Type(rig, "a = 1");
                Press(rig.Palette.Input, Key.Enter);
                Assert.Equal(new[] { ">>> a = 1" }, Paragraphs(rig.Palette));

                rig.Model.Follow(other.Document, other);
                Assert.Empty(Paragraphs(rig.Palette));
                Type(rig, "b = 2");
                Press(rig.Palette.Input, Key.Enter);
                Assert.Equal(new[] { ">>> b = 2" }, Paragraphs(rig.Palette));

                rig.Model.Follow(rig.Session.Document, rig.Session);
                Assert.Equal(new[] { ">>> a = 1" }, Paragraphs(rig.Palette));

                Click(rig.Palette, "Clear");
                Assert.Empty(Paragraphs(rig.Palette));
            });
        }

        [Fact]
        public void ANewLine_ScrollsToTheEnd_WhereTheViewWasAtTheEnd_AndLeavesTheViewWhereTheReaderScrolledTo()
        {
            OnRig(rig =>
            {
                ScrollViewer view = Descendants(rig.Palette.TranscriptBox).OfType<ScrollViewer>().First();
                Print(rig, 100);
                Settle(rig.Palette);
                Assert.True(view.ScrollableHeight > 0.0, "100 lines are taller than 8.");
                Assert.InRange(view.VerticalOffset, view.ScrollableHeight - 1.0, view.ScrollableHeight);

                Print(rig, 1);
                Settle(rig.Palette);
                Assert.InRange(view.VerticalOffset, view.ScrollableHeight - 1.0, view.ScrollableHeight);

                view.ScrollToHome();
                Settle(rig.Palette);
                Print(rig, 1);
                Settle(rig.Palette);
                Assert.Equal(0.0, view.VerticalOffset);
            });
        }

        private static void Settle(FrameworkElement root)
        {
            Layout(root, 900, 400);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Layout(root, 900, 400);
        }

        // ---- The states on the controls

        [Fact]
        public void AtRest_RunFileLoadClearAndResetAreOn_AndStopIsGreyed()
        {
            OnRig(rig =>
            {
                Assert.True(ButtonNamed(rig.Palette, "Run file").IsEnabled);
                Assert.True(ButtonNamed(rig.Palette, "Load").IsEnabled);
                Assert.False(ButtonNamed(rig.Palette, "Stop").IsEnabled);
                Assert.True(ButtonNamed(rig.Palette, "Clear").IsEnabled);
                Assert.True(ButtonNamed(rig.Palette, "Reset console").IsEnabled);
                Assert.True(rig.Palette.Input.IsEnabled);
            });
        }

        [Fact]
        public void WhileAFileRuns_RunFileLoadAndResetAreGreyed_StopIsLive_AndTheInputGivesItsReason()
        {
            OnRig(rig =>
            {
                Assert.True(rig.Model.DropFile(@"C:\scripts\circles.py"));
                rig.Host.Start("PYRUN", rig.Session.Document);

                Assert.False(ButtonNamed(rig.Palette, "Run file").IsEnabled);
                Assert.False(ButtonNamed(rig.Palette, "Load").IsEnabled);
                Assert.True(ButtonNamed(rig.Palette, "Stop").IsEnabled);
                Assert.True(ButtonNamed(rig.Palette, "Clear").IsEnabled);
                Assert.False(ButtonNamed(rig.Palette, "Reset console").IsEnabled);
                Assert.False(rig.Palette.Input.IsEnabled);
                Assert.Equal("circles.py is running: Esc stops it", rig.Palette.Input.Tag);
            });
        }

        [Fact]
        public void StopPressedAtAPrompt_CancelsTheScriptsQuestion_AndPressedWhilePythonComputes_StopsTheHost()
        {
            OnRig(rig =>
            {
                rig.Host.Start("PYRUN", rig.Session.Document);

                Click(rig.Palette, "Stop");
                Assert.Equal(1, rig.Host.Stops);

                Assert.True(rig.Session.Start(Question()));
                Assert.Equal("Answer on the command line, or press Esc", rig.Palette.Input.Tag);
                Click(rig.Palette, "Stop");

                Assert.False(rig.Session.IsRunning);
            });
        }

        [Fact]
        public void WithNoPython_TheBannerShowsTheProblemWordForWord_AndOnlyClearIsLeftOnTheToolRow()
        {
            const string problem =
                "No Python 3.11 to 3.15 (64-bit) was found. Install one from python.org, or set its python.exe in Global settings > Python.";

            OnRig(
                rig =>
                {
                    Assert.True(IsShown(rig.Palette, "Python is not available"));
                    Assert.True(IsShown(rig.Palette, problem));
                    Assert.True(IsShown(rig.Palette, "Everything else in GeometryHelper CAD works as before."));
                    Assert.False(ButtonNamed(rig.Palette, "Run file").IsEnabled);
                    Assert.False(ButtonNamed(rig.Palette, "Load").IsEnabled);
                    Assert.False(ButtonNamed(rig.Palette, "Stop").IsEnabled);
                    Assert.True(ButtonNamed(rig.Palette, "Clear").IsEnabled);
                    Assert.False(ButtonNamed(rig.Palette, "Reset console").IsEnabled);
                    Assert.False(rig.Palette.Input.IsEnabled);
                    Assert.Equal("No Python", rig.Palette.Input.Tag);
                    Assert.True(IsShown(rig.Palette, "No Python found"));
                },
                NoPython(problem)
            );
        }

        [Fact]
        public void WithPython_TheBannerIsNotShown_NorTheStatusLinkToGlobalSettings()
        {
            OnRig(rig =>
            {
                Assert.False(IsShown(rig.Palette, "Python is not available"));
                Assert.False(ButtonNamed(rig.Palette, "Global settings").IsVisible);
            });
        }

        [Fact]
        public void WithNoPython_TheBannerButtonAndTheStatusLink_BothOpenGlobalSettings()
        {
            OnRig(
                rig =>
                {
                    Click(rig.Palette, "Global settings");
                    Assert.Equal(1, rig.SettingsOpened);

                    Button banner = Descendants(rig.Palette)
                        .OfType<Button>()
                        .Single(b => b.Content as string == "Global settings...");
                    banner.Command.Execute(null);
                    Assert.Equal(2, rig.SettingsOpened);
                },
                NoPython("The Python set in Global settings (D:\\nowhere\\python.exe) was not found.")
            );
        }

        /// <summary>The host as the app has it with no usable Python: the finder's problem, no version and no program.</summary>
        private static Action<FakePythonConsoleHost> NoPython(string problem) =>
            host =>
            {
                host.Problem = problem;
                host.Executable = null;
                host.Version = null;
            };

        [Fact]
        public void TheCloseButton_HidesThePalette()
        {
            OnRig(rig =>
            {
                rig.Model.IsShown = true;

                Click(rig.Palette, "Close PYTHON");

                Assert.False(rig.Model.IsShown);
            });
        }

        [Fact]
        public void TheStatusLineShowsThePythonAtRest_AndWhatRunsWhileAFileRuns()
        {
            OnRig(rig =>
            {
                Assert.True(IsShown(rig.Palette, "Python 3.14.4 (venv314) · starts at the first run"));
                Assert.True(IsShown(rig.Palette, "Enter runs · Shift+Enter new line · Up/Down history"));

                Assert.True(rig.Model.DropFile(@"C:\scripts\circles.py"));
                rig.Host.Start("PYRUN", rig.Session.Document);

                Assert.True(IsShown(rig.Palette, "Running circles.py in " + rig.Session.Document.Title));
                Assert.True(IsShown(rig.Palette, "Esc stops it"));
            });
        }

        // ---- In the workspace

        /// <summary>
        /// A workspace over a window model with a drawing open, in a window of messages, never shown. <paramref name="python"/>
        /// gives the model its Python: the real host on a Python that is not there (no engine can start), or the palette over the
        /// host the screenshots use (<c>UsePythonSample</c>: Python 3.14 made up, idle or at a prompt).
        /// </summary>
        private static void OnWorkspace(Action<MainViewModel> python, Action<MainViewModel, CadWorkspace> test) =>
            OnWindowThread(() =>
            {
                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel();
                    model.ApplySettings(
                        new GlobalSettings
                        {
                            PythonPath = System.IO.Path.Combine(folder.FullPath, "nowhere", "python.exe"),
                        }
                    );
                    Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                    python(model);
                    var workspace = new CadWorkspace { DataContext = model };

                    using (Host(workspace))
                    {
                        try
                        {
                            test(model, workspace);
                        }
                        finally
                        {
                            workspace.DrawingCanvas.Dispose();
                        }
                    }
                }
            });

        private static void RealHost(MainViewModel model) => model.UsePython(Dispatcher.CurrentDispatcher);

        private static Action<MainViewModel> Sample(string state) => model => Assert.True(model.UsePythonSample(state));

        [Fact]
        public void ThePaletteIsHiddenAtFirst_ShownByPython_AndHiddenByPythonclose()
        {
            OnWorkspace(RealHost, (model, workspace) =>
            {
                Assert.Equal(Visibility.Collapsed, workspace.PythonPalette.Visibility);

                model.Execute("PYTHON");
                Layout(workspace);
                Assert.Equal(Visibility.Visible, workspace.PythonPalette.Visibility);

                model.Execute("PYTHONCLOSE");
                Layout(workspace);
                Assert.Equal(Visibility.Collapsed, workspace.PythonPalette.Visibility);
            });
        }

        [Fact]
        public void WithPythonNotUsed_TheWorkspaceHasNoPaletteToShow()
        {
            OnWorkspace(
                model => { },
                (model, workspace) =>
                {
                    Assert.Equal(Visibility.Collapsed, workspace.PythonPalette.Visibility);
                    Assert.Null(workspace.PythonPalette.DataContext);
                }
            );
        }

        [Fact]
        public void ShownPaletteSitsBetweenTheCanvasAndTheCommandLine_260High_AtItsDefaultOfEightLines()
        {
            OnWorkspace(Sample("idle"), (model, workspace) =>
            {
                Layout(workspace);
                PythonConsolePalette palette = workspace.PythonPalette;
                double Top(FrameworkElement e) => e.TransformToAncestor(workspace).Transform(new Point(0, 0)).Y;

                // 28 header + 32 tool row + 8 lines of 16 and the padding 12 + 38 input + 22 status line.
                Assert.Equal(Visibility.Visible, palette.Visibility);
                Assert.InRange(palette.ActualHeight, 259.0, 261.0);
                Assert.True(Top(palette) >= Top(workspace.DrawingCanvas) + workspace.DrawingCanvas.ActualHeight - 1.0);
                Assert.True(Top(palette) + palette.ActualHeight <= Top(workspace.CommandBox) + 1.0);
            });
        }

        [Fact]
        public void WithNoPython_ThePaletteIsAsTallAsItsBannerAndTheLinesUnderIt_NotACutOffBanner()
        {
            OnWorkspace(RealHost, (model, workspace) =>
            {
                model.Execute("PYTHON");
                Layout(workspace);
                PythonConsolePalette palette = workspace.PythonPalette;

                // The banner (8 + 3 lines of text + a button + 8) and three lines of transcript under it are all there.
                Assert.True(
                    palette.TranscriptBox.ActualHeight >= 3 * 16,
                    palette.TranscriptBox.ActualHeight.ToString()
                );
                Assert.True(palette.TranscriptBox.ActualWidth > 0.0);
                Assert.True(IsShown(palette, "Python is not available"));
            });
        }

        [Theory]
        [InlineData(39, 10)]
        [InlineData(41, 11)]
        public void DraggingTheTopEdgeUp_SnapsTheTranscriptToTheNearestWholeLine(int pixels, int lines)
        {
            // 8 lines at the start; 16 px a line: 39 px is 2.44 lines (to 2), 41 px is 2.56 (to 3). The halfway, 40 px, is not tested.
            OnWorkspace(Sample("idle"), (model, workspace) =>
            {
                workspace.PythonPalette.BeginGrip(500.0);
                workspace.PythonPalette.DragGripTo(500.0 - pixels);

                Assert.Equal(lines, workspace.PythonPalette.TranscriptLines);
            });
        }

        [Fact]
        public void DraggingTheTopEdgeDown_ShrinksTheTranscript_ButNeverBelowThreeLines()
        {
            OnWorkspace(Sample("idle"), (model, workspace) =>
            {
                PythonConsolePalette palette = workspace.PythonPalette;
                palette.BeginGrip(500.0);

                palette.DragGripTo(500.0 + 64.0);
                Assert.Equal(4, palette.TranscriptLines);

                palette.DragGripTo(500.0 + 400.0);
                Assert.Equal(3, palette.TranscriptLines);
            });
        }

        [Fact]
        public void DraggingTheTopEdgeFarUp_StopsAtHalfTheDrawingArea_WithinOneLine()
        {
            OnWorkspace(Sample("idle"), (model, workspace) =>
            {
                PythonConsolePalette palette = workspace.PythonPalette;
                var area = (FrameworkElement)palette.Parent;
                Layout(workspace);
                double half = area.ActualHeight / 2.0;

                palette.BeginGrip(900.0);
                palette.DragGripTo(-5000.0);
                Layout(workspace);

                Assert.True(palette.ActualHeight <= half + 1.0, palette.ActualHeight + " of half " + half);
                Assert.True(palette.ActualHeight > half - 16.0 - 1.0, palette.ActualHeight + " of half " + half);
            });
        }

        [Fact]
        public void EscPressedWithTheCommandLineHoldingTheKeyboard_CancelsTheQuestionOfAScript()
        {
            // The same question, answered as the design draws it, on the command line: the other half of "Esc or Stop cancels".
            OnWorkspace(Sample("prompt"), (model, workspace) =>
            {
                Layout(workspace);
                Assert.True(model.Session.IsRunning);
                Keyboard.Focus(workspace.CommandBox);
                Assert.Same(workspace.CommandBox, Keyboard.FocusedElement);

                Press(workspace.CommandBox, Key.Escape);

                Assert.False(model.Session.IsRunning);
            });
        }

        [Fact]
        public void EscPressedWithTheTranscriptHoldingTheKeyboard_CancelsTheQuestionOfAScript()
        {
            // The script waits at a prompt of its carrier command ("Specify base point") and the user has clicked in the
            // transcript to read an old line: Esc, as the status line says ("Esc or Stop cancels"), must end the question.
            OnWorkspace(Sample("prompt"), (model, workspace) =>
            {
                Layout(workspace);
                Assert.True(model.Session.IsRunning);
                Keyboard.Focus(workspace.PythonPalette.TranscriptBox);
                Assert.Same(workspace.PythonPalette.TranscriptBox, Keyboard.FocusedElement);

                Press(workspace.PythonPalette.TranscriptBox, Key.Escape);

                Assert.False(model.Session.IsRunning);
            });
        }
    }
}
