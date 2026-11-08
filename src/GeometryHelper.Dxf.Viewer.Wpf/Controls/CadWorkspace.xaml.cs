using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Win32;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The presenter's whole window content, laid out as AutoCAD's own (A20): the title bar, the ribbon, the DRAWINGS list on
    /// the left, the canvas with the command line under it, Properties on the right and the status bar. Its data context is a
    /// <see cref="MainViewModel"/>; a window or a host such as a Tekla plugin puts it in and lets it run.
    /// </summary>
    /// <remarks>
    /// Keys as AutoCAD's: Esc cancels the command, then clears the selection; Ctrl+O opens a DXF, Ctrl+N asks a host for a new
    /// drawing from a model (<see cref="MainViewModel.NewFromModelRequested"/>), Ctrl+S saves, Ctrl+Shift+S
    /// saves as, Ctrl+Z undoes and Ctrl+Y redoes, Delete erases the selection; F3 OSNAP, F7 GRID, F8 ORTHO, F9 SNAP, F10 POLAR.
    /// Letters typed with the focus on the canvas go to the command line, where Enter or Space runs the line (Space types a
    /// space at a prompt for text) and Up and Down recall what was typed before. A value of Properties is committed by Enter
    /// or by leaving it, and Esc puts the old one back. DXF files dropped on it are opened.
    /// </remarks>
    public partial class CadWorkspace : UserControl
    {
        /// <summary>Identifies the <see cref="TitleBarContent"/> property.</summary>
        public static readonly DependencyProperty TitleBarContentProperty = DependencyProperty.Register(
            nameof(TitleBarContent), typeof(object), typeof(CadWorkspace), new PropertyMetadata(null));

        private MainViewModel _model;

        /// <summary>Makes the workspace.</summary>
        public CadWorkspace()
        {
            InitializeComponent();
            AllowDrop = true;
            DataContextChanged += OnDataContextChanged;
            CanvasView.CursorMoved += (s, e) => _model?.SetCursor(e.X, e.Y);
            CanvasView.ViewChanged += (s, e) => _model?.NotifyViewChangedByUser();
            CanvasView.PixelSizeChanged += (s, e) => _model?.SetCanvasSize(CanvasView.PixelWidth, CanvasView.PixelHeight);
            CanvasView.HistoryWritten += (s, e) => _model?.RefreshHistory();
            CanvasView.SelectionChanged += (s, e) => _model?.RefreshHistory();
        }

        /// <summary>Gets or sets what the far right of the title bar holds, as a window's minimise, maximise and close buttons.</summary>
        public object TitleBarContent
        {
            get => GetValue(TitleBarContentProperty);
            set => SetValue(TitleBarContentProperty, value);
        }

        /// <summary>Gets the canvas, so that a host can draw it at once (<see cref="CadCanvas.RenderNow"/>) before a screenshot.</summary>
        public CadCanvas DrawingCanvas => CanvasView;

        /// <summary>Shows the Open dialog for DXF files and opens the one chosen.</summary>
        public void ShowOpenDialog()
        {
            if (_model == null)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "DXF drawings (*.dxf)|*.dxf|All files (*.*)|*.*",
                Title = "Open a DXF",
            };

            if (_model.Document?.FileName != null && Path.IsPathRooted(_model.Document.FileName))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(_model.Document.FileName);
            }

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                _model.Open(dialog.FileName);
            }
        }

        /// <summary>Shows the Save As dialog and saves the drawing to the file chosen.</summary>
        public void ShowSaveAsDialog()
        {
            if (_model?.Document == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "DXF drawings (*.dxf)|*.dxf",
                Title = "Save the drawing as",
                FileName = _model.Document.Title.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) ? _model.Document.Title : _model.Document.Title + ".dxf",
            };

            if (_model.Document.FileName != null && Path.IsPathRooted(_model.Document.FileName))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(_model.Document.FileName);
            }

            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            {
                _model.SaveAs(dialog.FileName);
            }
        }

        /// <summary>Shows the Layer manager of the drawing shown, as a dialog over the window.</summary>
        public void ShowLayerManager()
        {
            LayerManagerViewModel manager = _model?.CreateLayerManager();

            if (manager == null)
            {
                return;
            }

            var dialog = new LayerManagerWindow(manager) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
        }

        /// <inheritdoc/>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (_model == null)
            {
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            ModifierKeys modifiers = Keyboard.Modifiers;
            bool inEditor = Keyboard.FocusedElement is TextBox box && box != CommandBox;

            if (key == Key.Escape && !inEditor)
            {
                if (!CanvasView.CancelGesture())
                {
                    _model.Escape();
                }

                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && key == Key.O)
            {
                ShowOpenDialog();
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && key == Key.N && _model.NewFromModelCommand.CanExecute(null))
            {
                _model.NewFromModelCommand.Execute(null);
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && key == Key.S)
            {
                _model.Save();
                e.Handled = true;
            }
            else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.S)
            {
                ShowSaveAsDialog();
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && key == Key.Z && !inEditor)
            {
                _model.Undo();
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && key == Key.Y && !inEditor)
            {
                _model.Redo();
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.None && key == Key.Delete && !(Keyboard.FocusedElement is TextBox))
            {
                _model.EraseSelection();
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.None && Toggle(key))
            {
                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            base.OnPreviewTextInput(e);

            // As in AutoCAD, typing with the focus on the canvas types into the command line.
            if (!(Keyboard.FocusedElement is TextBox) && !(Keyboard.FocusedElement is ComboBox) && !string.IsNullOrEmpty(e.Text)
                && !char.IsControl(e.Text[0]) && e.Text != " " && _model != null)
            {
                _model.CommandText += e.Text;
                CommandBox.Focus();
                CommandBox.CaretIndex = CommandBox.Text.Length;
                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Space or Enter with the focus on the canvas is Enter, as in AutoCAD: it ends a command or runs the last again.
            if (_model != null && (e.Key == Key.Space || e.Key == Key.Enter) && !(Keyboard.FocusedElement is TextBox) && !(Keyboard.FocusedElement is ButtonBase))
            {
                _model.Enter();
                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            if (_model != null && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (string file in files.Where(f => f.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)))
                {
                    _model.Open(file);
                }

                e.Handled = true;
            }
        }

        private bool Toggle(Key key)
        {
            string name;

            switch (key)
            {
                case Key.F3:
                    name = "OSNAP";
                    break;
                case Key.F7:
                    name = "GRID";
                    break;
                case Key.F8:
                    name = "ORTHO";
                    break;
                case Key.F9:
                    name = "SNAP";
                    break;
                case Key.F10:
                    name = "POLAR";
                    break;
                default:
                    return false;
            }

            StatusToggleViewModel toggle = _model.Toggles.First(t => t.Name == name);
            toggle.IsOn = !toggle.IsOn;
            return true;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_model != null)
            {
                _model.OpenRequested -= OnOpenRequested;
                _model.SaveAsRequested -= OnSaveAsRequested;
                _model.LayerManagerRequested -= OnLayerManagerRequested;
            }

            _model = e.NewValue as MainViewModel;

            if (_model != null)
            {
                _model.OpenRequested += OnOpenRequested;
                _model.SaveAsRequested += OnSaveAsRequested;
                _model.LayerManagerRequested += OnLayerManagerRequested;

                if (CanvasView.PixelWidth > 0 && CanvasView.PixelHeight > 0)
                {
                    _model.SetCanvasSize(CanvasView.PixelWidth, CanvasView.PixelHeight);
                }
            }
        }

        private void OnOpenRequested(object sender, EventArgs e) => ShowOpenDialog();

        private void OnSaveAsRequested(object sender, EventArgs e) => ShowSaveAsDialog();

        private void OnLayerManagerRequested(object sender, EventArgs e) => ShowLayerManager();

        private void OnCommandKeyDown(object sender, KeyEventArgs e)
        {
            if (_model == null)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Space when _model.PromptTakesSpaces:
                    break;
                case Key.Enter:
                case Key.Space:
                    // Space ends the line as Enter does, as in AutoCAD.
                    _model.SubmitCommand();
                    e.Handled = true;
                    break;
                case Key.Up:
                    _model.RecallPrevious();
                    CommandBox.CaretIndex = CommandBox.Text.Length;
                    e.Handled = true;
                    break;
                case Key.Down:
                    _model.RecallNext();
                    CommandBox.CaretIndex = CommandBox.Text.Length;
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>A value of Properties: Enter commits it, Esc puts the old one back.</summary>
        private void OnValueKeyDown(object sender, KeyEventArgs e)
        {
            var editor = (TextBox)sender;

            if (e.Key == Key.Enter)
            {
                editor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                editor.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                editor.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                CanvasView.Focus();
                e.Handled = true;
            }
        }
    }
}
