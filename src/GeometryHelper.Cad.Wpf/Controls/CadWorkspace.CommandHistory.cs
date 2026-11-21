using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The history above the command line, made like AutoCAD 2021's: a read-only text box that holds every line the command
    /// line wrote, as tall as a whole number of lines. The top edge of the command line is dragged to make it taller or
    /// shorter (0 lines leaves the input alone); the number is kept in the settings. The wheel and a thin bar scroll it, a
    /// drag selects lines, Ctrl+C or the menu copies them, and a key typed there goes back to the input.
    /// </summary>
    public partial class CadWorkspace
    {
        private bool _historyShowsWelcome;
        private int _historyShownLines = -1;
        private double _historyLineHeight;
        private double _gripFrom;
        private double _gripStartHeight;
        private bool _gripChanged;

        /// <summary>Makes the history follow the size of the drawing area: the lines that fit are counted again when the window is resized.</summary>
        private void StartHistory()
        {
            DrawingArea.SizeChanged += (s, e) => ApplyHistoryLines();
            HistoryBox.Loaded += (s, e) => ApplyHistoryLines();
            HistoryBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnHistoryScrolled));
        }

        /// <summary>
        /// The box grew, or its view changed height: it scrolls to the end where it was at its end before, so that a new line is
        /// followed, and stays where it is where the reader had scrolled up. Scrolling back to the end follows again.
        /// </summary>
        private void OnHistoryScrolled(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0.0 && e.ViewportHeightChange == 0.0)
            {
                return;
            }

            double offsetBefore = e.VerticalOffset - e.VerticalChange;
            double viewportBefore = e.ViewportHeight - e.ViewportHeightChange;
            double extentBefore = e.ExtentHeight - e.ExtentHeightChange;

            if (offsetBefore + viewportBefore >= extentBefore - 1.0)
            {
                HistoryBox.ScrollToEnd();
            }
        }

        /// <summary>Gets the lines of the history box now shown, 0 when the input is alone.</summary>
        internal int HistoryLinesShown => _historyShownLines;

        /// <summary>Gets the box that holds the history, so that a test can read what it shows.</summary>
        internal TextBox HistoryText => HistoryBox;

        /// <summary>The model's history followed from now: the box filled with it, and each new line added to the end.</summary>
        private void FollowHistory()
        {
            _model.History.CollectionChanged += OnHistoryChanged;
            FillHistory();
            ApplyHistoryLines();
        }

        private void ForgetHistory() => _model.History.CollectionChanged -= OnHistoryChanged;

        /// <summary>The whole history put in the box, scrolled to its end: at the start, and after a Reset; with no line yet, the welcome line.</summary>
        private void FillHistory()
        {
            _historyShowsWelcome = _model.History.Count == 0;
            HistoryBox.Text = _historyShowsWelcome
                ? _model.RecentHistory.FirstOrDefault()
                : string.Join(Environment.NewLine, _model.History);
            HistoryBox.ScrollToEnd();
        }

        /// <summary>
        /// A line written: put at the end of the box, which is added to rather than set again, since a new text would lose the
        /// selection and the place the reader scrolled to. The box scrolls to the new line only where it was at its end
        /// (<see cref="OnHistoryScrolled"/>, once it is laid out again).
        /// </summary>
        private void OnHistoryChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_model == null)
            {
                return;
            }

            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null)
            {
                FillHistory();
                return;
            }

            string lines = string.Join(Environment.NewLine, e.NewItems.Cast<string>());

            if (_historyShowsWelcome)
            {
                _historyShowsWelcome = false;
                HistoryBox.Text = lines;
            }
            else
            {
                HistoryBox.AppendText(Environment.NewLine + lines);
            }
        }

        /// <summary>The welcome line, which stands for an empty history, shown again after an extension changed it.</summary>
        private void ShowWelcome()
        {
            if (_model != null && _historyShowsWelcome)
            {
                HistoryBox.Text = _model.RecentHistory.FirstOrDefault();
            }
        }

        /// <summary>The height of one line of the history: measured from the box once it has text laid out, else from the font.</summary>
        private double HistoryLineHeight()
        {
            if (_historyLineHeight <= 0.0 && HistoryBox.IsMeasureValid && HistoryBox.LineCount > 0)
            {
                Rect first = HistoryBox.GetRectFromCharacterIndex(0);

                if (!first.IsEmpty && first.Height > 0.0)
                {
                    _historyLineHeight = first.Height;
                }
            }

            return _historyLineHeight > 0.0
                ? _historyLineHeight
                : HistoryBox.FontFamily.LineSpacing * HistoryBox.FontSize;
        }

        /// <summary>
        /// Gives the history the lines the settings keep, or as many as fit where the window is too small: at most half the
        /// drawing area. The number kept is not changed here. With none the history row goes and the cross of the command
        /// line stands before the prompt mark.
        /// </summary>
        private void ApplyHistoryLines()
        {
            if (_model == null)
            {
                return;
            }

            double line = HistoryLineHeight();
            int kept = _model.Settings.CommandLineHistoryLines;
            int most =
                DrawingArea.ActualHeight > 0.0 ? CommandHistoryRules.MostLines(DrawingArea.ActualHeight, line) : kept;
            ShowHistoryLines(CommandHistoryRules.Shown(kept, most));
        }

        private void ShowHistoryLines(int lines)
        {
            // The height is set again where the line was measured since: the font's guess gives way to the box's own.
            double height = lines * HistoryLineHeight();

            if (lines == _historyShownLines && Math.Abs(HistoryBox.Height - height) < 0.01)
            {
                return;
            }

            _historyShownLines = lines;
            HistoryRow.Visibility = lines > 0 ? Visibility.Visible : Visibility.Collapsed;
            InputClose.Visibility = lines > 0 ? Visibility.Collapsed : Visibility.Visible;
            HistoryBox.Height = height;
        }

        // ---- The top edge, dragged

        private void OnHistoryGripStarted(object sender, DragStartedEventArgs e) =>
            BeginHistoryDrag(Mouse.GetPosition(DrawingArea).Y);

        private void OnHistoryGripDelta(object sender, DragDeltaEventArgs e) =>
            DragHistoryTo(Mouse.GetPosition(DrawingArea).Y);

        private void OnHistoryGripCompleted(object sender, DragCompletedEventArgs e) => EndHistoryDrag();

        /// <summary>The top edge pressed: where the pointer is, and how tall the history is, are what the drag counts from.</summary>
        /// <param name="pointerY">The pointer's y in the drawing area, which the drag does not move (the thumb it holds does).</param>
        internal void BeginHistoryDrag(double pointerY)
        {
            _gripFrom = pointerY;
            _gripStartHeight = Math.Max(0, _historyShownLines) * HistoryLineHeight();
            _gripChanged = false;
        }

        /// <summary>The pointer moved: the history is given the whole lines that its height at the start less the way the pointer went down comes to, and the count is told beside the pointer.</summary>
        /// <param name="pointerY">The pointer's y in the drawing area.</param>
        internal void DragHistoryTo(double pointerY)
        {
            double line = HistoryLineHeight();
            int most = CommandHistoryRules.MostLines(DrawingArea.ActualHeight, line);
            int lines = CommandHistoryRules.LinesForDrag(_gripStartHeight, pointerY - _gripFrom, line, most);

            if (lines != _historyShownLines)
            {
                _gripChanged = true;
                ShowHistoryLines(lines);
            }

            HistoryTipText.Text = lines == 1 ? "1 line" : lines + " lines";
            HistoryTip.HorizontalOffset = Mouse.GetPosition(DrawingArea).X + 16.0;
            HistoryTip.VerticalOffset = pointerY - 32.0;
            HistoryTip.IsOpen = true;
        }

        /// <summary>The edge let go: the tip goes, and the lines are kept in the settings once, if the drag changed them.</summary>
        internal void EndHistoryDrag()
        {
            HistoryTip.IsOpen = false;

            if (_gripChanged && _model != null)
            {
                _model.KeepHistoryLines(_historyShownLines);
            }

            _gripChanged = false;
        }

        // ---- Keys, the menu

        /// <summary>
        /// The history holds the keyboard and a key is pressed: if it is the input's (Enter, Space, Esc with nothing selected,
        /// Tab, Back, Delete, the F keys, a shortcut), the input takes the keyboard before the window reads the key.
        /// </summary>
        /// <returns>Whether the key is the input's and the input now holds the keyboard.</returns>
        private bool TakeHistoryKey(KeyEventArgs e)
        {
            if (Keyboard.FocusedElement != HistoryBox)
            {
                return false;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (
                CommandHistoryRules.Classify(key, Keyboard.Modifiers, HistoryBox.SelectionLength > 0)
                != CommandHistoryKey.ToInput
            )
            {
                return false;
            }

            FocusInput();
            return true;
        }

        private void FocusInput()
        {
            CommandBox.Focus();
            CommandBox.CaretIndex = CommandBox.Text.Length;
        }

        /// <summary>A key kept by the history: Ctrl+C copies the selection through the model's clipboard, and Esc lets the selection go.</summary>
        private void OnHistoryKeyDown(object sender, KeyEventArgs e)
        {
            if (_model == null || Keyboard.FocusedElement != HistoryBox)
            {
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _model.CopyHistorySelection(HistoryBox.SelectedText);
                e.Handled = true;
            }
            else if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && HistoryBox.SelectionLength > 0)
            {
                HistoryBox.Select(HistoryBox.SelectionStart + HistoryBox.SelectionLength, 0);
                e.Handled = true;
            }
        }

        /// <summary>A character typed with the history holding the keyboard: put at the end of the input, which takes the keyboard.</summary>
        private void OnHistoryTextInput(object sender, TextCompositionEventArgs e)
        {
            if (_model == null || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
            {
                return;
            }

            _model.CommandText += e.Text;
            FocusInput();
            e.Handled = true;
        }

        /// <summary>A right click on the history: the menu of a right click on the input, its Copy taking the lines selected here.</summary>
        private void OnHistoryRightUp(object sender, MouseButtonEventArgs e)
        {
            if (_model != null)
            {
                ShowMenu(_model.CommandLineMenu(HistoryBox.SelectedText), HistoryBox);
                e.Handled = true;
            }
        }
    }
}
