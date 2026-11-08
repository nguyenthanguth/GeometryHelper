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
    /// Keys as AutoCAD's: Esc cancels the command, then clears the selection; Ctrl+O opens a DXF, Ctrl+W closes it, Ctrl+N asks a host for a new
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

        private readonly System.Windows.Threading.DispatcherTimer _autosave = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        private MainViewModel _model;
        private DateTime _lastAutosave = DateTime.UtcNow;

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

            // Autosave (Global settings › Save and export): looked at every minute while the workspace is shown.
            _autosave.Tick += (s, e) => AutosaveIfDue();
            Loaded += (s, e) => _autosave.Start();
            Unloaded += (s, e) => _autosave.Stop();
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

        /// <summary>Shows the Plugin manager of the window's plugins, as a dialog over the window; nothing without a plugin manager.</summary>
        public void ShowPluginManager()
        {
            PluginManagerViewModel manager = _model?.CreatePluginManager();

            if (manager == null)
            {
                return;
            }

            var dialog = new PluginManagerWindow(manager) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
        }

        /// <summary>Shows Global settings as a dialog over the window, on a category; null for the first.</summary>
        /// <param name="category">The category, as Shortcuts.</param>
        public void ShowGlobalSettings(string category = null)
        {
            if (_model == null)
            {
                return;
            }

            var dialog = new GlobalSettingsWindow(_model.CreateGlobalSettings(category)) { Owner = Window.GetWindow(this) };
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
            else if (modifiers == ModifierKeys.Control && key == Key.W && _model.HasDocument)
            {
                _model.CloseDocument();
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
            else if (!inEditor && (modifiers != ModifierKeys.None || (key >= Key.F1 && key <= Key.F24)) && _model.RunKey(Gesture(key, modifiers)))
            {
                // A key the shortcuts give a command or a toggle (Global settings › Shortcuts), as Ctrl+1 gives PROPERTIES.
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

                // A plugin package dropped is installed, as the Plugin manager's Install does.
                foreach (string file in files.Where(f => f.EndsWith(".ghplugin", StringComparison.OrdinalIgnoreCase)))
                {
                    _model.InstallPlugin(file);
                }

                e.Handled = true;
            }
        }

        private bool Toggle(Key key)
        {
            string name;

            switch (key)
            {
                case Key.F3 when _model.Settings.F3TogglesSnaps:
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
                _model.PluginLoadRequested -= OnPluginLoadRequested;
                _model.PluginInstallRequested -= OnPluginInstallRequested;
                _model.PluginManagerRequested -= OnPluginManagerRequested;
                _model.SettingsRequested -= OnSettingsRequested;
                _model.SettingsApplied -= OnSettingsApplied;
                _model.CloseRequested -= OnCloseRequested;
            }

            _model = e.NewValue as MainViewModel;

            if (_model != null)
            {
                _model.OpenRequested += OnOpenRequested;
                _model.SaveAsRequested += OnSaveAsRequested;
                _model.LayerManagerRequested += OnLayerManagerRequested;
                _model.PluginLoadRequested += OnPluginLoadRequested;
                _model.PluginInstallRequested += OnPluginInstallRequested;
                _model.PluginManagerRequested += OnPluginManagerRequested;
                _model.SettingsRequested += OnSettingsRequested;
                _model.SettingsApplied += OnSettingsApplied;
                _model.CloseRequested += OnCloseRequested;
                _model.AskSaveChanges = document => CadQuestionWindow.AskSave(Window.GetWindow(this), new[] { document });
                _model.AskSaveAll = documents => CadQuestionWindow.AskSave(Window.GetWindow(this), documents);

                if (CanvasView.PixelWidth > 0 && CanvasView.PixelHeight > 0)
                {
                    _model.SetCanvasSize(CanvasView.PixelWidth, CanvasView.PixelHeight);
                }
            }
        }

        private void OnOpenRequested(object sender, EventArgs e) => ShowOpenDialog();

        private void OnSaveAsRequested(object sender, EventArgs e) => ShowSaveAsDialog();

        private void OnLayerManagerRequested(object sender, EventArgs e) => ShowLayerManager();

        private void OnPluginLoadRequested(object sender, EventArgs e) => PluginManagerWindow.PickPlugin(Window.GetWindow(this), path => _model.LoadPlugin(path));

        private void OnPluginInstallRequested(object sender, EventArgs e) => PluginManagerWindow.PickPackage(Window.GetWindow(this), path => _model.InstallPlugin(path));

        private void OnPluginManagerRequested(object sender, EventArgs e) => ShowPluginManager();

        private void OnSettingsRequested(object sender, string category) => ShowGlobalSettings(category);

        /// <summary>CLOSE typed: the drawing shown closed once the command has ended.</summary>
        private void OnCloseRequested(object sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() => _model?.CloseDocument()));

        /// <summary>Saves the copies of the drawings with changes once the minutes of Global settings have gone by since the last.</summary>
        private void AutosaveIfDue()
        {
            int minutes = _model?.Settings.AutosaveMinutes ?? 0;

            if (minutes > 0 && (DateTime.UtcNow - _lastAutosave).TotalMinutes >= minutes)
            {
                _lastAutosave = DateTime.UtcNow;
                _model.Autosave();
            }
        }

        /// <summary>Gives the canvas what Global settings say of it: colours, lineweights, font, wheel zoom, pickbox and aperture.</summary>
        private void OnSettingsApplied(object sender, EventArgs e)
        {
            GlobalSettings settings = _model.Settings;
            CanvasView.Options.Background = settings.CanvasBackground;
            CanvasView.Options.Paper = settings.Paper;
            CanvasView.Options.SelectionColor = settings.Selection;
            CanvasView.Options.ShowLineWeights = settings.ShowLineWeights;
            CanvasView.Options.FontName = settings.DrawingFont;
            CanvasView.WheelZoomFactor = settings.WheelZoomFactor;
            CanvasView.PickboxPixels = settings.PickboxPixels;
            CanvasView.AperturePixels = settings.AperturePixels;
            CanvasView.Options.VisibleLineColor = settings.VisibleLines;
            CanvasView.Options.HiddenLineColor = settings.HiddenLines;
            CanvasView.Options.GripColor = settings.Grip;
            CanvasView.CrosshairPercent = settings.CrosshairPercent;

            if (CanvasView.PixelWidth > 0 && CanvasView.PixelHeight > 0)
            {
                CanvasView.RenderNow();
            }

            CanvasView.InvalidateVisual();
        }

        /// <summary>The key pressed as the shortcuts write it: Ctrl+1, Ctrl+Shift+P, F3.</summary>
        private static string Gesture(Key key, ModifierKeys modifiers)
        {
            string name = key >= Key.D0 && key <= Key.D9 ? ((int)(key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : key >= Key.NumPad0 && key <= Key.NumPad9 ? ((int)(key - Key.NumPad0)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : key.ToString();
            return ((modifiers & ModifierKeys.Control) != 0 ? "Ctrl+" : string.Empty) + ((modifiers & ModifierKeys.Alt) != 0 ? "Alt+" : string.Empty)
                + ((modifiers & ModifierKeys.Shift) != 0 ? "Shift+" : string.Empty) + name;
        }

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
