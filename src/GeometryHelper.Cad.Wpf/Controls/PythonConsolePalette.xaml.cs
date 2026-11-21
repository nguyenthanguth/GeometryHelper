using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The PYTHON palette, docked under the drawing: a header with the drawing's name, the tool row (Run file, Load, Stop, Clear,
    /// Reset console), the transcript, the input and the status line, as the design draws them. Its logic is
    /// <see cref="PythonConsoleViewModel"/>; this file lays the transcript out (one paragraph a line, coloured by its kind),
    /// keeps the input's keys (Enter runs, Shift+Enter adds a line, Up and Down walk the entries), sizes the transcript by its top
    /// edge in whole lines, and takes a <c>.py</c> file dropped on it.
    /// </summary>
    public partial class PythonConsolePalette : UserControl
    {
        private const double LineHeight = 16.0;
        private const double TranscriptPadding = 12.0;
        private const int FewestLines = 3;
        private const int DefaultLines = 8;

        private static readonly Brush EchoMark = Frozen(0x6C, 0xB6, 0xFF);
        private static readonly Brush EchoText = Frozen(0xE6, 0xE9, 0xED);
        private static readonly Brush ErrorText = Frozen(0xFF, 0x8A, 0x7A);
        private static readonly Brush HostText = Frozen(0x9A, 0xA4, 0xB1);

        private PythonConsoleViewModel _model;
        private ObservableCollection<PythonLine> _lines;
        private readonly List<PythonLine> _mirrored = new List<PythonLine>();
        private bool _flushQueued;
        private int _transcriptLines = DefaultLines;
        private double _gripFrom;
        private double _gripStartLines;
        private bool _focusLostToNothing;

        /// <summary>Makes the palette; its data context is the <see cref="PythonConsoleViewModel"/> of the window.</summary>
        public PythonConsolePalette()
        {
            InitializeComponent();
            ShowLines(DefaultLines);
            DataContextChanged += (s, e) => Follow(e.NewValue as PythonConsoleViewModel);

            // A run greys the input and the keyboard goes nowhere: that is told from a click on another control.
            IsKeyboardFocusWithinChanged += (s, e) =>
                _focusLostToNothing = !(bool)e.NewValue && Keyboard.FocusedElement == null;
            Transcript.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTranscriptScrolled));
        }

        /// <summary>Gets the lines of the transcript shown, so that a test can read what it holds.</summary>
        internal RichTextBox TranscriptBox
        {
            get
            {
                Flush();
                return Transcript;
            }
        }

        /// <summary>Gets the input box, so that a test can type in it.</summary>
        internal TextBox Input => InputBox;

        /// <summary>Gets the lines of the transcript the palette is tall enough for now.</summary>
        internal int TranscriptLines => _transcriptLines;

        private static Brush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // ---- The view model followed

        private void Follow(PythonConsoleViewModel model)
        {
            if (_model != null)
            {
                _model.PropertyChanged -= OnModelChanged;
                _model.InputFocusRequested -= OnFocusRequested;
            }

            _model = model;

            if (_model != null)
            {
                _model.PropertyChanged += OnModelChanged;
                _model.InputFocusRequested += OnFocusRequested;
            }

            ShowLines(_transcriptLines);

            Rebuild();
        }

        private void OnModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PythonConsoleViewModel.Lines))
            {
                Rebuild();
            }
            else if (e.PropertyName == nameof(PythonConsoleViewModel.IsUsable))
            {
                ShowLines(_transcriptLines);
            }
            else if (e.PropertyName == nameof(PythonConsoleViewModel.IsInputEnabled) && _model.IsInputEnabled)
            {
                // The run ended: the keyboard comes back to the input if it was lost to the greying of the input, not clicked away.
                if (_focusLostToNothing)
                {
                    _focusLostToNothing = false;
                    FocusInput();
                }
            }
        }

        private void OnFocusRequested(object sender, EventArgs e) => FocusInput();

        private void FocusInput() =>
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (InputBox.IsEnabled)
                    {
                        InputBox.Focus();
                        InputBox.CaretIndex = InputBox.Text.Length;
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Input
            );

        // ---- The transcript: a paragraph for each line

        private void Rebuild()
        {
            if (_lines != null)
            {
                _lines.CollectionChanged -= OnLinesChanged;
            }

            _lines = _model?.Lines;
            Transcript.Document.Blocks.Clear();
            _mirrored.Clear();

            if (_lines != null)
            {
                _lines.CollectionChanged += OnLinesChanged;
                Flush();
            }

            Transcript.ScrollToEnd();
        }

        // A line is put in the control once the window is idle, not as it is written: a script that prints thousands of lines
        // costs one pass over what changed, not a layout for each line.
        private void OnLinesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_flushQueued)
            {
                _flushQueued = true;
                Dispatcher.BeginInvoke(new Action(Flush), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        /// <summary>Brings the paragraphs of the control in line with the lines of the view model: those dropped from the front taken off, those written over replaced, those added put at the end.</summary>
        private void Flush()
        {
            _flushQueued = false;

            if (_lines == null)
            {
                return;
            }

            BlockCollection blocks = Transcript.Document.Blocks;
            int dropped = 0;

            if (_lines.Count > 0)
            {
                while (dropped < _mirrored.Count && !ReferenceEquals(_mirrored[dropped], _lines[0]))
                {
                    dropped++;
                }

                if (dropped == _mirrored.Count)
                {
                    // Nothing of what is shown is left: all is put again.
                    blocks.Clear();
                    _mirrored.Clear();
                    dropped = 0;
                }
            }

            for (int i = 0; i < dropped; i++)
            {
                blocks.Remove(blocks.FirstBlock);
            }

            _mirrored.RemoveRange(0, dropped);
            Block block = blocks.FirstBlock;
            int common = Math.Min(_mirrored.Count, _lines.Count);

            for (int i = 0; i < common; i++)
            {
                Block next = block.NextBlock;

                if (!ReferenceEquals(_mirrored[i], _lines[i]))
                {
                    blocks.InsertBefore(block, Paragraph(_lines[i]));
                    blocks.Remove(block);
                    _mirrored[i] = _lines[i];
                }

                block = next;
            }

            while (_mirrored.Count > _lines.Count)
            {
                blocks.Remove(blocks.LastBlock);
                _mirrored.RemoveAt(_mirrored.Count - 1);
            }

            List<PythonLine> added = _lines.Skip(_mirrored.Count).ToList();
            blocks.AddRange(added.Select(Paragraph));
            _mirrored.AddRange(added);
        }

        private static Paragraph Paragraph(PythonLine line)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                LineHeight = LineHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            };

            if (line.Mark.Length > 0)
            {
                paragraph.Inlines.Add(new Run(line.Mark) { Foreground = EchoMark });
            }

            Brush text =
                line.Kind == PythonLineKind.Error ? ErrorText
                : line.Kind == PythonLineKind.Host ? HostText
                : EchoText;
            paragraph.Inlines.Add(new Run(line.Text) { Foreground = text });
            return paragraph;
        }

        /// <summary>The transcript grew or its view changed: it scrolls to the end where it was at its end before, and stays where the reader scrolled to.</summary>
        private void OnTranscriptScrolled(object sender, ScrollChangedEventArgs e)
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
                Transcript.ScrollToEnd();
            }
        }

        private void OnTranscriptRightUp(object sender, MouseButtonEventArgs e)
        {
            var menu = new ContextMenu { Style = (Style)FindResource("GhContextMenu"), PlacementTarget = Transcript };
            menu.Items.Add(Item("Copy", () => Transcript.Copy()));
            menu.Items.Add(Item("Copy all", () =>
                {
                    Transcript.SelectAll();
                    Transcript.Copy();
                }));
            menu.Items.Add(Item("Clear", () => _model?.Clear()));
            menu.IsOpen = true;
            e.Handled = true;
        }

        private MenuItem Item(string header, Action run)
        {
            var item = new MenuItem { Header = header, Style = (Style)FindResource("GhMenuItem") };
            item.Click += (s, e) => run();
            return item;
        }

        // ---- A .py file dropped

        private static string DroppedFile(DragEventArgs e) =>
            e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files
                ? files.FirstOrDefault(f =>
                    string.Equals(Path.GetExtension(f), ".py", StringComparison.OrdinalIgnoreCase)
                )
                : null;

        private void OnTranscriptDragOver(object sender, DragEventArgs e)
        {
            e.Effects =
                DroppedFile(e) != null && _model != null && _model.IsInputEnabled
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnTranscriptDrop(object sender, DragEventArgs e)
        {
            string file = DroppedFile(e);

            if (file != null)
            {
                _model?.DropFile(file);
            }

            e.Handled = true;
        }

        // ---- The input's keys

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (
                _model == null
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)
            )
            {
                return;
            }

            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (e.Key == Key.Enter && !shift)
            {
                _model.Submit();
                InputBox.CaretIndex = InputBox.Text.Length;
                e.Handled = true;
            }
            else if (e.Key == Key.Up && !shift && InputBox.GetLineIndexFromCharacterIndex(InputBox.CaretIndex) <= 0)
            {
                e.Handled = _model.HistoryUp();
                InputBox.CaretIndex = InputBox.Text.Length;
            }
            else if (
                e.Key == Key.Down
                && !shift
                && InputBox.GetLineIndexFromCharacterIndex(InputBox.CaretIndex) >= InputBox.LineCount - 1
            )
            {
                e.Handled = _model.HistoryDown();
                InputBox.CaretIndex = InputBox.Text.Length;
            }
            else if (e.Key == Key.Tab && !shift)
            {
                // A block is indented by four spaces, as Python's own style has it.
                int at = InputBox.SelectionStart;
                InputBox.SelectedText = "    ";
                InputBox.CaretIndex = at + 4;
                e.Handled = true;
            }
        }

        // ---- The top edge, dragged: the transcript's height in whole lines

        private void OnGripStarted(object sender, DragStartedEventArgs e) => BeginGrip(Mouse.GetPosition(ParentArea).Y);

        private void OnGripDelta(object sender, DragDeltaEventArgs e) => DragGripTo(Mouse.GetPosition(ParentArea).Y);

        private FrameworkElement ParentArea => Parent as FrameworkElement ?? this;

        /// <summary>The top edge pressed: where the pointer is, and how tall the transcript is, are what the drag counts from.</summary>
        /// <param name="pointerY">The pointer's y in the area the palette is docked in.</param>
        internal void BeginGrip(double pointerY)
        {
            _gripFrom = pointerY;
            _gripStartLines = _transcriptLines;
        }

        /// <summary>The pointer moved: the transcript gets the whole lines its height at the start less the way the pointer went down comes to, from 3 to half the drawing area.</summary>
        /// <param name="pointerY">The pointer's y in the area the palette is docked in.</param>
        internal void DragGripTo(double pointerY)
        {
            double wanted = _gripStartLines - (pointerY - _gripFrom) / LineHeight;
            ShowLines((int)Math.Round(wanted, MidpointRounding.AwayFromZero));
        }

        /// <summary>Gives the transcript a number of lines, from 3 to what fits in half of the area it is docked in.</summary>
        /// <param name="lines">The lines wanted.</param>
        internal void ShowLines(int lines)
        {
            double area = (Parent as FrameworkElement)?.ActualHeight ?? 0.0;
            double chrome = 28 + 32 + 38 + 22;
            int most =
                area > 0.0 ? (int)Math.Floor((area / 2.0 - chrome - TranscriptPadding) / LineHeight) : DefaultLines;
            _transcriptLines = Math.Max(FewestLines, Math.Min(lines, Math.Max(FewestLines, most)));

            // With a banner over it (Python is not available) the area is as tall as the banner and the lines under it need.
            TranscriptArea.Height =
                _model != null && !_model.IsUsable ? double.NaN : _transcriptLines * LineHeight + TranscriptPadding;
        }
    }
}
