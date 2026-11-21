using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The history above the command line, made like AutoCAD 2021's (design A1): the number of lines kept in the settings, the
    /// rules for how many fit and what a drag gives, which keys the history keeps, the copy of a selection, and the window laid
    /// out offscreen with a history of 24 lines.
    /// </summary>
    public sealed class CommandHistoryTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        private static readonly string[] Lines =
        {
            "Command: LINE",
            "Specify first point:",
            "Specify next point or [Undo]:",
            "Specify next point or [Close/Undo]: C",
            "Command: SELECTSIMILAR",
            "Select objects or [SEttings]: 1 found",
            "Select objects or [SEttings]:",
            "12 found",
            "Command: COPY",
            "Current settings: Copy mode = Multiple",
            "Specify base point or [Displacement/mOde] :",
            "Specify second point or [Array] : A",
            "Enter number of items to array: 4",
            "Specify second point or [Fit]:",
            "Command: MOVE",
            "Select objects: 3 found",
            "Specify base point or [Displacement] :",
            "Specify second point or :",
            "Command: DIMLINEAR",
            "Specify first extension line origin or :",
            "Specify second extension line origin:",
            "Dimension text = 2 400",
            "Command: COPY",
            "Current settings: Copy mode = Multiple",
        };

        // ---- The setting

        [Fact]
        public void TheCommandLineShowsThreeLinesOfHistoryByDefault_AndTheFileKeepsTheNumberUnderInput()
        {
            var settings = new GlobalSettings();
            Assert.Equal(3, settings.CommandLineHistoryLines);

            settings.CommandLineHistoryLines = 11;
            string path = Path.Combine(_folder.FullPath, "settings.json");
            settings.Save(path);

            Assert.Contains("\"historyLines\": 11", File.ReadAllText(path));
            Assert.Equal(11, GlobalSettings.Load(path).CommandLineHistoryLines);
            Assert.Equal(settings.ToJson(), GlobalSettings.Load(path).ToJson());
        }

        [Fact]
        public void ZeroLinesIsKept_SinceTheInputAloneIsAChoice()
        {
            var settings = new GlobalSettings { CommandLineHistoryLines = 0 };
            string path = Path.Combine(_folder.FullPath, "settings.json");
            settings.Save(path);

            Assert.Equal(0, GlobalSettings.Load(path).CommandLineHistoryLines);
        }

        [Theory]
        [InlineData(-4, 0)]
        [InlineData(0, 0)]
        [InlineData(7, 7)]
        [InlineData(50, 50)]
        [InlineData(51, 50)]
        [InlineData(5000, 50)]
        public void ALineCountOutOfRange_IsBroughtToTheNearestEnd(int set, int expected)
        {
            var settings = new GlobalSettings { CommandLineHistoryLines = set };

            Assert.Equal(expected, settings.CommandLineHistoryLines);
        }

        [Fact]
        public void AFileWithTooManyLines_ReadsAsTheMost_AndAFileWithoutTheKeyReadsAsThree()
        {
            string path = Path.Combine(_folder.FullPath, "settings.json");
            File.WriteAllText(path, "{ \"input\": { \"historyLines\": 900 } }");
            Assert.Equal(GlobalSettings.CommandLineHistoryLinesMost, GlobalSettings.Load(path).CommandLineHistoryLines);

            File.WriteAllText(path, "{ \"input\": { \"autoComplete\": false } }");
            GlobalSettings read = GlobalSettings.Load(path);
            Assert.Equal(3, read.CommandLineHistoryLines);
            Assert.False(read.AutoComplete);
        }

        [Fact]
        public void TheClonedSettings_CarryTheLineCount()
        {
            var settings = new GlobalSettings { CommandLineHistoryLines = 9 };

            Assert.Equal(9, settings.Clone().CommandLineHistoryLines);
        }

        // ---- The rules

        [Theory]
        [InlineData(800.0, 16.0, 25)]
        [InlineData(100.0, 16.0, 3)]
        [InlineData(20.0, 16.0, 0)]
        [InlineData(0.0, 16.0, 0)]
        [InlineData(800.0, 0.0, 0)]
        public void HalfTheDrawingAreaHoldsAWholeNumberOfLines(double height, double line, int expected)
        {
            Assert.Equal(expected, CommandHistoryRules.MostLines(height, line));
        }

        [Theory]
        [InlineData(10, 25, 10)]
        [InlineData(10, 6, 6)]
        [InlineData(10, 0, 0)]
        [InlineData(0, 25, 0)]
        public void ALargeKeptCountShowsAsManyLinesAsFit_WithoutChangingTheCount(int kept, int most, int shown)
        {
            Assert.Equal(shown, CommandHistoryRules.Shown(kept, most));
        }

        [Theory]
        [InlineData(48.0, 0.0, 3)]
        [InlineData(48.0, -32.0, 5)]
        [InlineData(48.0, -39.0, 5)]
        [InlineData(48.0, -40.0, 6)]
        [InlineData(48.0, -25.0, 5)]
        [InlineData(48.0, -23.0, 4)]
        [InlineData(48.0, -800.0, 25)]
        [InlineData(48.0, 48.0, 0)]
        [InlineData(48.0, 900.0, 0)]
        [InlineData(0.0, -16.0, 1)]
        public void ADragOfTheTopEdgeGivesWholeLines_TheNearestToTheHeightAtTheStart(
            double start,
            double down,
            int expected
        )
        {
            Assert.Equal(expected, CommandHistoryRules.LinesForDrag(start, down, 16.0, 25));
        }

        [Theory]
        [InlineData(Key.C, ModifierKeys.Control, false, "Stays")]
        [InlineData(Key.A, ModifierKeys.Control, true, "Stays")]
        [InlineData(Key.Up, ModifierKeys.None, false, "Stays")]
        [InlineData(Key.Down, ModifierKeys.Shift, true, "Stays")]
        [InlineData(Key.PageUp, ModifierKeys.None, false, "Stays")]
        [InlineData(Key.End, ModifierKeys.Control, false, "Stays")]
        [InlineData(Key.LeftShift, ModifierKeys.Shift, false, "Stays")]
        [InlineData(Key.Escape, ModifierKeys.None, true, "Stays")]
        [InlineData(Key.Escape, ModifierKeys.None, false, "ToInput")]
        [InlineData(Key.Enter, ModifierKeys.None, false, "ToInput")]
        [InlineData(Key.Enter, ModifierKeys.None, true, "ToInput")]
        [InlineData(Key.Space, ModifierKeys.None, false, "ToInput")]
        [InlineData(Key.Tab, ModifierKeys.None, false, "ToInput")]
        [InlineData(Key.Back, ModifierKeys.None, false, "ToInput")]
        [InlineData(Key.Delete, ModifierKeys.None, false, "ToInput")]
        [InlineData(Key.F8, ModifierKeys.None, false, "ToInput")]
        [InlineData(Key.Z, ModifierKeys.Control, false, "ToInput")]
        [InlineData(Key.C, ModifierKeys.Control | ModifierKeys.Shift, false, "ToInput")]
        [InlineData(Key.D1, ModifierKeys.Alt, false, "ToInput")]
        [InlineData(Key.L, ModifierKeys.None, false, "Types")]
        [InlineData(Key.L, ModifierKeys.Shift, true, "Types")]
        [InlineData(Key.D5, ModifierKeys.None, false, "Types")]
        [InlineData(Key.NumPad2, ModifierKeys.None, false, "Types")]
        [InlineData(Key.OemComma, ModifierKeys.None, false, "Types")]
        [InlineData(Key.OemMinus, ModifierKeys.None, false, "Types")]
        public void AKeyPressedInTheHistory_StaysThere_TypesIntoTheInput_OrIsTheInputs(
            Key key,
            ModifierKeys modifiers,
            bool hasSelection,
            string expected
        )
        {
            Assert.Equal(expected, CommandHistoryRules.Classify(key, modifiers, hasSelection).ToString());
        }

        // ---- The view model

        [Fact]
        public void CopyingASelectionOfTheHistory_WritesItToTheClipboard_AndNothingSelectedCopiesNothing()
        {
            var model = new MainViewModel();
            var written = new List<string>();

            Assert.False(model.CopyHistorySelection("Command: LINE"));

            model.WriteClipboardText = written.Add;

            Assert.True(model.CopyHistorySelection("Command: LINE" + Environment.NewLine + "Specify first point:"));
            Assert.False(model.CopyHistorySelection(string.Empty));
            Assert.False(model.CopyHistorySelection(null));
            Assert.Equal(new[] { "Command: LINE" + Environment.NewLine + "Specify first point:" }, written);
        }

        [Fact]
        public void TheMenuOfTheHistory_CopiesTheSelectionHandedToIt_AndCopyHistoryCopiesEveryLine()
        {
            var model = new MainViewModel();
            var written = new List<string>();
            model.WriteClipboardText = written.Add;
            model.Say("Command: LINE");
            model.Say("Specify first point:");

            IReadOnlyList<CadMenuItemViewModel> menu = model.CommandLineMenu("first point");
            CadMenuItemViewModel copy = menu.Single(i => i.Header == "Copy");
            CadMenuItemViewModel all = menu.Single(i => i.Header == "Copy History");

            Assert.True(copy.IsEnabled);
            copy.Command.Execute(null);
            all.Command.Execute(null);

            Assert.Equal(
                new[] { "first point", "Command: LINE" + Environment.NewLine + "Specify first point:" },
                written
            );
            Assert.False(model.CommandLineMenu(string.Empty).Single(i => i.Header == "Copy").IsEnabled);
        }

        [Fact]
        public void TheLinesDraggedTo_AreKeptInTheSettingsAndTheFileOnce_AndTheViewIsToldToShowThem()
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            int changed = 0;
            int applied = 0;
            model.SettingsChanged += (s, e) => changed++;
            model.SettingsApplied += (s, e) => applied++;

            model.KeepHistoryLines(9);

            Assert.Equal(9, model.Settings.CommandLineHistoryLines);
            Assert.Equal(1, changed);
            Assert.Equal(1, applied);

            model.KeepHistoryLines(500);

            Assert.Equal(GlobalSettings.CommandLineHistoryLinesMost, model.Settings.CommandLineHistoryLines);
        }

        [Fact]
        public void TheWelcomeLineAndTheRecentLines_AreStillWhatTheViewModelGives_ForTheTextWindowAndTheTests()
        {
            var model = new MainViewModel();
            Assert.Equal(new[] { MainViewModel.NewDrawingWelcomeText }, model.RecentHistory);

            foreach (string line in Lines.Take(5))
            {
                model.Say(line);
            }

            Assert.Equal(Lines.Skip(2).Take(3), model.RecentHistory);
            Assert.Equal(Lines.Take(5), model.History);
        }

        // ---- The window

        private static MainViewModel Full(TestFolder folder, int kept = 3)
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(folder.FullPath, "settings.json") };
            GlobalSettings settings = model.Settings.Clone();
            settings.CommandLineHistoryLines = kept;
            model.ApplySettings(settings);

            foreach (string line in Lines)
            {
                model.Say(line);
            }

            return model;
        }

        private static void Layout(CadWorkspace workspace, double width = 1600, double height = 960)
        {
            workspace.Measure(new Size(width, height));
            workspace.Arrange(new Rect(0, 0, width, height));
            workspace.UpdateLayout();
        }

        private static T Named<T>(CadWorkspace workspace, string name)
            where T : class => (T)workspace.FindName(name);

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        private static ScrollViewer Scroller(CadWorkspace workspace) =>
            Descendants(workspace.HistoryText).OfType<ScrollViewer>().First();

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "The test did not finish in 120 s.");
            Assert.Null(failure);
        }

        [Fact]
        public void TheHistoryBoxHoldsEveryLine_ThreeOfThemInSight_AtTheEnd_WithTheThinBarShown()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);

                TextBox box = workspace.HistoryText;
                Assert.Equal(string.Join(Environment.NewLine, Lines), box.Text);
                Assert.True(box.IsReadOnly);
                Assert.Equal(3, workspace.HistoryLinesShown);
                Assert.Equal(3.0, box.ActualHeight / (box.ExtentHeight / Lines.Length), 1);
                Assert.Equal(Lines.Length * (box.ExtentHeight / Lines.Length), box.ExtentHeight, 3);
                Assert.True(box.ViewportHeight < box.ExtentHeight);
                Assert.Equal(box.ExtentHeight, box.VerticalOffset + box.ViewportHeight, 0);
                Assert.Equal(Visibility.Visible, Scroller(workspace).ComputedVerticalScrollBarVisibility);
                Assert.Equal(Visibility.Visible, Named<FrameworkElement>(workspace, "HistoryRow").Visibility);
                Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(workspace, "InputClose").Visibility);
            });
        }

        [Fact]
        public void WithFewerLinesThanFit_TheThinBarIsNotShown_AndTheLinesStandAtTheBottom()
        {
            OnSta(() =>
            {
                var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
                model.Say("Command: LINE");
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);

                TextBox box = workspace.HistoryText;
                Assert.Equal(Visibility.Collapsed, Scroller(workspace).ComputedVerticalScrollBarVisibility);
                Assert.Equal(3, workspace.HistoryLinesShown);
                Assert.Equal("Command: LINE", box.Text);

                // The line is at the bottom of the box, next to the input, as AutoCAD's.
                Rect line = box.GetRectFromCharacterIndex(0);
                Assert.True(
                    line.Top > box.ActualHeight / 2.0,
                    "The line is at " + line.Top + " of " + box.ActualHeight
                );
            });
        }

        [Fact]
        public void BeforeAnyLine_TheWelcomeLineIsShown_AndTheFirstLineTakesItsPlace()
        {
            OnSta(() =>
            {
                var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);
                Assert.Equal(MainViewModel.NewDrawingWelcomeText, workspace.HistoryText.Text);

                model.Say("Command: LINE");
                Assert.Equal("Command: LINE", workspace.HistoryText.Text);

                model.Say("Specify first point:");
                Assert.Equal(
                    "Command: LINE" + Environment.NewLine + "Specify first point:",
                    workspace.HistoryText.Text
                );
            });
        }

        [Fact]
        public void ALineWritten_IsAddedToTheEnd_KeepingTheSelectionAndFollowingWhenTheBoxWasAtItsEnd()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);
                TextBox box = workspace.HistoryText;

                // Text selected from "COPY" to the middle of the next line, two thirds of the way down.
                int start = box.Text.IndexOf("COPY", StringComparison.Ordinal);
                box.Select(start, 20);
                model.Say("Select objects:");

                Assert.EndsWith("Select objects:", box.Text);
                Assert.Equal(start, box.SelectionStart);
                Assert.Equal(20, box.SelectionLength);
                Layout(workspace);
                Assert.Equal(box.ExtentHeight, box.VerticalOffset + box.ViewportHeight, 0);
            });
        }

        [Fact]
        public void ABoxScrolledUp_StaysWhereItIs_UntilTheReaderScrollsBackToTheEnd()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);
                TextBox box = workspace.HistoryText;

                box.ScrollToVerticalOffset(0.0);
                Layout(workspace);
                model.Say("a new line while reading");
                Layout(workspace);

                Assert.Equal(0.0, box.VerticalOffset, 3);
                Assert.EndsWith("a new line while reading", box.Text);

                box.ScrollToEnd();
                Layout(workspace);
                model.Say("a second new line");
                Layout(workspace);

                Assert.Equal(box.ExtentHeight, box.VerticalOffset + box.ViewportHeight, 0);
            });
        }

        [Fact]
        public void ZeroLines_LeavesTheInputAlone_WithTheCrossBeforeThePromptMark_AndTheGripStillThere()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder, kept: 0);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);

                Assert.Equal(0, workspace.HistoryLinesShown);
                FrameworkElement row = Named<FrameworkElement>(workspace, "HistoryRow");
                FrameworkElement cross = Named<FrameworkElement>(workspace, "InputClose");
                FrameworkElement grip = Named<FrameworkElement>(workspace, "HistoryGrip");
                Assert.Equal(Visibility.Collapsed, row.Visibility);
                Assert.Equal(Visibility.Visible, cross.Visibility);
                Assert.Equal(Visibility.Visible, grip.Visibility);
                Assert.Equal(6.0, grip.ActualHeight, 3);
                FrameworkElement input = Named<FrameworkElement>(workspace, "CommandInput");
                Assert.True(cross.TranslatePoint(new Point(0, 0), input).X < 40.0);
            });
        }

        [Fact]
        public void TheCommandLineIsTallerByTheLinesAdded_AndTheGripLiesAcrossItsTopEdge()
        {
            OnSta(() =>
            {
                var three = new CadWorkspace { DataContext = Full(_folder, 3) };
                Layout(three);
                var ten = new CadWorkspace { DataContext = Full(_folder, 10) };
                Layout(ten);

                FrameworkElement rowThree = Named<FrameworkElement>(three, "HistoryRow");
                FrameworkElement rowTen = Named<FrameworkElement>(ten, "HistoryRow");
                double topThree = rowThree.TranslatePoint(new Point(0, 0), three).Y;
                double topTen = rowTen.TranslatePoint(new Point(0, 0), ten).Y;
                double line = three.HistoryText.ExtentHeight / Lines.Length;
                Assert.Equal(7 * line, topThree - topTen, 1);
                Assert.Equal(10, ten.HistoryLinesShown);

                // The history row sits 6 px under the 1 px top border; the grip's 6 px are centred on that border.
                FrameworkElement area = Named<FrameworkElement>(ten, "DrawingArea");
                FrameworkElement grip = Named<FrameworkElement>(ten, "HistoryGrip");
                double border = topTen - 7.0;
                double gripMiddle = grip.TranslatePoint(new Point(0, 3), ten).Y;
                Assert.InRange(gripMiddle, border - 1.5, border + 2.5);
                Assert.Equal(area.ActualWidth, grip.ActualWidth, 1);
            });
        }

        [Fact]
        public void InAWindowTooSmallForTheCount_AsManyLinesShowAsFit_AndTheCountKeptIsNotChanged()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder, kept: 20);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace, 1600, 380);

                double area = Named<FrameworkElement>(workspace, "DrawingArea").ActualHeight;
                double line = workspace.HistoryText.ExtentHeight / Lines.Length;
                int fit = (int)Math.Floor(0.5 * area / line);

                Assert.True(fit < 20, fit.ToString());
                Assert.Equal(fit, workspace.HistoryLinesShown);
                Assert.Equal(20, model.Settings.CommandLineHistoryLines);

                Layout(workspace, 1600, 1400);
                Assert.Equal(20, workspace.HistoryLinesShown);
                Assert.Equal(20, model.Settings.CommandLineHistoryLines);
            });
        }

        [Fact]
        public void DraggingTheTopEdgeUp_ShowsMoreLinesAtOnce_AndLettingGoKeepsThemOnce()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);
                int changed = 0;
                model.SettingsChanged += (s, e) => changed++;
                double line = workspace.HistoryText.ExtentHeight / Lines.Length;

                workspace.BeginHistoryDrag(500.0);
                workspace.DragHistoryTo(500.0 - 2.0 * line);
                Layout(workspace);
                Assert.Equal(5, workspace.HistoryLinesShown);
                Assert.Equal(5.0, workspace.HistoryText.ActualHeight / line, 1);
                Assert.Equal(3, model.Settings.CommandLineHistoryLines);
                Assert.Equal(0, changed);
                Assert.Equal("5 lines", ((TextBlock)workspace.FindName("HistoryTipText")).Text);

                workspace.DragHistoryTo(500.0 - 7.0 * line);
                Assert.Equal(10, workspace.HistoryLinesShown);
                Assert.Equal(0, changed);

                workspace.EndHistoryDrag();

                Assert.Equal(10, model.Settings.CommandLineHistoryLines);
                Assert.Equal(1, changed);
                Assert.Equal(10, workspace.HistoryLinesShown);
            });
        }

        [Fact]
        public void DraggingTheTopEdgeDownToTheBottom_LeavesTheInputAlone_AndOneLineIsAsLineInTheTip()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);
                double line = workspace.HistoryText.ExtentHeight / Lines.Length;

                workspace.BeginHistoryDrag(500.0);
                workspace.DragHistoryTo(500.0 + 2.0 * line);
                Assert.Equal(1, workspace.HistoryLinesShown);
                Assert.Equal("1 line", ((TextBlock)workspace.FindName("HistoryTipText")).Text);

                workspace.DragHistoryTo(500.0 + 40.0 * line);
                Assert.Equal(0, workspace.HistoryLinesShown);
                Assert.Equal(Visibility.Visible, Named<FrameworkElement>(workspace, "InputClose").Visibility);
                workspace.EndHistoryDrag();

                Assert.Equal(0, model.Settings.CommandLineHistoryLines);

                // Dragged back up from 0, the history comes back, scrolled to its end.
                workspace.BeginHistoryDrag(500.0);
                workspace.DragHistoryTo(500.0 - 3.0 * line);
                Layout(workspace);
                Assert.Equal(3, workspace.HistoryLinesShown);
                Assert.Equal(Visibility.Collapsed, Named<FrameworkElement>(workspace, "InputClose").Visibility);
                Assert.Equal(
                    workspace.HistoryText.ExtentHeight,
                    workspace.HistoryText.VerticalOffset + workspace.HistoryText.ViewportHeight,
                    0
                );
            });
        }

        [Fact]
        public void AGripPressedAndLetGoWithoutMoving_KeepsNothing_SoAKeptCountTooLargeForTheWindowSurvives()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder, kept: 20);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace, 1600, 380);
                int changed = 0;
                model.SettingsChanged += (s, e) => changed++;

                workspace.BeginHistoryDrag(300.0);
                workspace.DragHistoryTo(300.0);
                workspace.EndHistoryDrag();

                Assert.Equal(0, changed);
                Assert.Equal(20, model.Settings.CommandLineHistoryLines);
            });
        }

        [Fact]
        public void ACharacterTypedWithTheHistoryHoldingTheKeyboard_IsTypedIntoTheInput()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder);
                var workspace = new CadWorkspace { DataContext = model };
                Layout(workspace);
                model.CommandText = "LIN";

                var typed = new TextCompositionEventArgs(
                    Keyboard.PrimaryDevice,
                    new TextComposition(InputManager.Current, workspace.HistoryText, "E")
                )
                {
                    RoutedEvent = UIElement.PreviewTextInputEvent,
                };
                workspace.HistoryText.RaiseEvent(typed);

                Assert.True(typed.Handled);
                Assert.Equal("LINE", model.CommandText);
                Assert.Equal(string.Join(Environment.NewLine, Lines), workspace.HistoryText.Text);
            });
        }

        private static void PressInTheHistory(CadWorkspace workspace, HwndSource source, Key key) =>
            workspace.HistoryText.RaiseEvent(
                new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent }
            );

        [Fact]
        public void EnterPressedWithTheHistoryHoldingTheKeyboard_RunsWhatIsTypedInTheInput()
        {
            OnSta(() =>
            {
                var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
                Assert.True(model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                var workspace = new CadWorkspace { DataContext = model };
                using (var source = Host(workspace))
                {
                    model.CommandText = "LINE";
                    Keyboard.Focus(workspace.HistoryText);
                    Assert.Same(workspace.HistoryText, Keyboard.FocusedElement);

                    PressInTheHistory(workspace, source, Key.Enter);

                    Assert.True(model.IsCommandRunning);
                    Assert.Equal(string.Empty, model.CommandText);
                    Assert.Same(workspace.FindName("CommandBox"), Keyboard.FocusedElement);
                }
            });
        }

        [Fact]
        public void EscWithLinesSelected_OnlyLetsThemGo_AndEscWithNoneIsTheInputsAndCancelsTheCommand()
        {
            OnSta(() =>
            {
                var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
                Assert.True(model.Open(_folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                var workspace = new CadWorkspace { DataContext = model };
                using (var source = Host(workspace))
                {
                    model.StartCommand("LINE");
                    Assert.True(model.IsCommandRunning);
                    Keyboard.Focus(workspace.HistoryText);
                    workspace.HistoryText.Select(0, 5);

                    PressInTheHistory(workspace, source, Key.Escape);

                    Assert.True(model.IsCommandRunning);
                    Assert.Equal(0, workspace.HistoryText.SelectionLength);
                    Assert.Same(workspace.HistoryText, Keyboard.FocusedElement);

                    PressInTheHistory(workspace, source, Key.Escape);

                    Assert.False(model.IsCommandRunning);
                    Assert.Same(workspace.FindName("CommandBox"), Keyboard.FocusedElement);
                }
            });
        }

        [Fact]
        public void ALetterTypedInTheHistory_StaysWhereItWas_UntilTheTextOfTheKeyIsTyped()
        {
            OnSta(() =>
            {
                MainViewModel model = Full(_folder);
                var workspace = new CadWorkspace { DataContext = model };
                using (var source = Host(workspace))
                {
                    Keyboard.Focus(workspace.HistoryText);

                    PressInTheHistory(workspace, source, Key.L);

                    Assert.Same(workspace.HistoryText, Keyboard.FocusedElement);
                    Assert.Equal(string.Empty, model.CommandText);
                }
            });
        }

        /// <summary>The workspace in a window of messages, never shown, which gives it a keyboard to hold focus.</summary>
        private static HwndSource Host(CadWorkspace workspace)
        {
            var source = new HwndSource(
                new HwndSourceParameters("CommandHistoryTests")
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
            Layout(workspace);
            return source;
        }
    }
}
