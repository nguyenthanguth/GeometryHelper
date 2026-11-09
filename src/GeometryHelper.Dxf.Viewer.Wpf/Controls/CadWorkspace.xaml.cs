using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
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
    /// Keys as AutoCAD's: Esc cancels the command, then clears the selection; Ctrl+O opens a DXF, Ctrl+W switches selection cycling, Ctrl+N asks a host for a new
    /// drawing from a model (<see cref="MainViewModel.NewFromModelRequested"/>), Ctrl+S saves, Ctrl+Shift+S
    /// saves as, Ctrl+Z undoes and Ctrl+Y redoes, Delete erases the selection; F3 OSNAP, F7 GRID, F8 ORTHO, F9 SNAP, F10 POLAR,
    /// F11 OTRACK, F12 DYN (the prompt and what is typed shown beside the cursor, Tab moving between its fields at a point prompt), and
    /// Ctrl+9 shows or hides the command line. In the DRAWINGS list a click selects a row, a double click or Enter opens it,
    /// Delete does nothing and a right click shows Open and the Close items; OSNAP of the status bar has the menu of the object
    /// snaps on its arrow and its right click, and Shift+right-click on the canvas while a point is asked gives the snap menu.
    /// The arrows of Trim, Fillet and Array on the ribbon drop their other commands (Extend, Chamfer, Polar Array), the button
    /// then showing and running the one picked until another is, and the title of Modify the rest of the group, as AutoCAD
    /// 2021's panel slides out.
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
            CanvasView.CursorMoved += (s, e) =>
            {
                _model?.SetCursor(e.X, e.Y);
                PlaceDynamicInput(e.X, e.Y);
            };
            CanvasView.ViewChanged += (s, e) => _model?.NotifyViewChangedByUser();
            CanvasView.PixelSizeChanged += (s, e) => _model?.SetCanvasSize(CanvasView.PixelWidth, CanvasView.PixelHeight);
            CanvasView.HistoryWritten += (s, e) => _model?.RefreshHistory();
            CanvasView.SelectionChanged += (s, e) => _model?.RefreshHistory();
            CanvasView.SnapMenuRequested += (s, e) => ShowMenu(_model?.SnapOverrideMenu(), CanvasView);

            // Text edited in place: a double click on a text edits it; the editor follows the view; a click outside it ends it.
            CanvasView.ObjectDoubleClicked += (s, e) => _model?.EditTextAt(e.Handle);
            CanvasView.ViewChanged += (s, e) => PlaceInPlace(false);
            CanvasView.PixelSizeChanged += (s, e) => PlaceInPlace(false);
            CanvasView.PreviewMouseDown += OnCanvasDownWhileEditing;
            MTextEditorBox.EscapePressed += (s, e) => _model?.EscapeTextEditor();
            MTextEditorBox.CloseRequested += (s, e) => _model?.CloseTextEditor(true);
            MTextEditorBox.SizeChanged += (s, e) => PlaceInPlace(false);

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

        /// <summary>Shows Drafting Settings as a dialog over the window, on a tab; null for Object Snap.</summary>
        /// <param name="tab">The tab, as "Snap and Grid".</param>
        public void ShowDraftingSettings(string tab = null)
        {
            if (_model == null)
            {
                return;
            }

            var dialog = new DraftingSettingsWindow(_model.CreateDraftingSettings(tab)) { Owner = Window.GetWindow(this) };
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
            bool inEditor = (Keyboard.FocusedElement is TextBox box && box != CommandBox) || Keyboard.FocusedElement is CadMTextEditBox;

            if (modifiers == ModifierKeys.None && FileList.IsKeyboardFocusWithin && (key == Key.Enter || key == Key.Delete))
            {
                // In the DRAWINGS list Enter opens the row clicked, and Delete does nothing: it neither erases objects nor removes a file.
                e.Handled = _model.FileListKey(key == Key.Enter ? "Enter" : "Delete");
            }
            else if (key == Key.Escape && !inEditor)
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
            else if (modifiers == ModifierKeys.None && key == Key.Delete && !(Keyboard.FocusedElement is TextBox) && !inEditor)
            {
                _model.EraseSelection();
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.None && key == Key.Tab && (Keyboard.FocusedElement == CommandBox || Keyboard.FocusedElement == CanvasView)
                     && _model.TabDynamicInput())
            {
                // Tab moves between the fields of dynamic input, locking a number typed, as in AutoCAD.
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.None && !(Keyboard.FocusedElement is CadMTextEditBox) && Toggle(key))
            {
                e.Handled = true;
            }
            else if (!inEditor && (modifiers != ModifierKeys.None || (key >= Key.F1 && key <= Key.F24)) && _model.RunKey(Gesture(key, modifiers)))
            {
                // A key the shortcuts give a command or a toggle (Global settings › Shortcuts), as Ctrl+1 gives PROPERTIES; Ctrl+9
                // hides the command line, and the canvas then takes the keys typed.
                if (!_model.IsCommandLineShown && CommandBox.IsKeyboardFocusWithin)
                {
                    CanvasView.Focus();
                }

                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            base.OnPreviewTextInput(e);

            // As in AutoCAD, typing with the focus on the canvas types into the command line; hidden (Ctrl+9), the text is kept
            // there all the same, and dynamic input shows it beside the cursor.
            if (!(Keyboard.FocusedElement is TextBox) && !(Keyboard.FocusedElement is ComboBox) && !(Keyboard.FocusedElement is CadMTextEditBox) && !string.IsNullOrEmpty(e.Text)
                && !char.IsControl(e.Text[0]) && e.Text != " " && _model != null)
            {
                _model.CommandText += e.Text;

                if (_model.IsCommandLineShown)
                {
                    CommandBox.Focus();
                    CommandBox.CaretIndex = CommandBox.Text.Length;
                }

                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_model == null || Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is ButtonBase || Keyboard.FocusedElement is CadMTextEditBox)
            {
                return;
            }

            // Text typed with the command line hidden: Backspace takes a letter back, Enter or Space runs it (Space types a
            // space at a prompt for text).
            if (_model.CommandText.Length > 0 && e.Key == Key.Back)
            {
                _model.CommandText = _model.CommandText.Substring(0, _model.CommandText.Length - 1);
                e.Handled = true;
            }
            else if (_model.CommandText.Length > 0 && e.Key == Key.Space && _model.PromptTakesSpaces)
            {
                _model.CommandText += " ";
                e.Handled = true;
            }
            else if (_model.CommandText.Length > 0 && (e.Key == Key.Space || e.Key == Key.Enter))
            {
                _model.SubmitCommand();
                e.Handled = true;
            }
            else if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Shift && _model.Document != null && _model.Document.SelectionCycling >= 1)
            {
                // Shift+Space arms selection cycling, as AutoCAD's: each click at one place then takes the next object there.
                CanvasView.ArmCycling();
                e.Handled = true;
            }
            else if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                // Space or Enter with the focus on the canvas is Enter, as in AutoCAD: it ends a command or runs the last again.
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
                case Key.F11:
                    name = "OTRACK";
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
                _model.DraftingSettingsRequested -= OnDraftingSettingsRequested;
                _model.StyleDialogRequested -= OnStyleDialogRequested;
                _model.InPlaceEditChanged -= OnInPlaceEditChanged;
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
                _model.DraftingSettingsRequested += OnDraftingSettingsRequested;
                _model.StyleDialogRequested += OnStyleDialogRequested;
                _model.AskSaveChanges = document => CadQuestionWindow.AskSave(Window.GetWindow(this), new[] { document });
                _model.AskSaveAll = documents => CadQuestionWindow.AskSave(Window.GetWindow(this), documents);
                _model.PickColor = dialog => SelectColorWindow.Pick(Window.GetWindow(this), dialog);
                _model.ShowLinetypeManager = manager => LinetypeManagerWindow.Show(Window.GetWindow(this), manager);
                _model.ShowHatchSettings = settings => HatchSettingsWindow.Show(Window.GetWindow(this), settings);
                _model.ShowMissingShx = dialog => MissingShxWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowPointStyle = dialog => PointStyleWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowBlockDefinition = dialog => BlockDefinitionWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowBlocksPalette = dialog => BlocksPaletteWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowWriteBlock = dialog => WriteBlockWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowAttributeDefinition = dialog => AttributeDefinitionWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowAttributeEditor = dialog => AttributeEditorWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowPasteSpecial = dialog => PasteSpecialWindow.Show(Window.GetWindow(this), dialog);
                _model.AskKeepMTextChanges = () => CadQuestionWindow.AskKeepMTextChanges(Window.GetWindow(this));
                _model.InPlaceEditChanged += OnInPlaceEditChanged;
                _model.ShowPlot = dialog => PlotWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowPlotPreview = preview => PlotPreviewWindow.Show(Window.GetWindow(this), preview);
                _model.AskPlotFile = AskPlotFile;
                _model.WindowsPrinters = WindowsPrinters;
                _model.ShowQuickSelect = dialog => QuickSelectWindow.Show(Window.GetWindow(this), dialog);
                _model.ShowFindReplace = dialog => FindReplaceWindow.Show(Window.GetWindow(this), dialog);

                if (CanvasView.PixelWidth > 0 && CanvasView.PixelHeight > 0)
                {
                    _model.SetCanvasSize(CanvasView.PixelWidth, CanvasView.PixelHeight);
                }
            }
        }

        /// <summary>Browse for Plot File: where a plot is written, a PDF, the folder and name offered; null where it is cancelled.</summary>
        private string AskPlotFile(string folder, string name)
        {
            var dialog = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", Title = "Browse for Plot File", FileName = name, DefaultExt = ".pdf" };

            if (folder != null && Directory.Exists(folder))
            {
                dialog.InitialDirectory = folder;
            }

            return dialog.ShowDialog(Window.GetWindow(this)) == true ? dialog.FileName : null;
        }

        /// <summary>The printers of Windows by name, each with whether it is on the network, as the print spooler lists them.</summary>
        private static System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, bool>> WindowsPrinters()
        {
            var printers = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, bool>>();

            using (var server = new System.Printing.LocalPrintServer())
            {
                foreach (System.Printing.PrintQueue queue in server.GetPrintQueues(new[] { System.Printing.EnumeratedPrintQueueTypes.Local, System.Printing.EnumeratedPrintQueueTypes.Connections }))
                {
                    using (queue)
                    {
                        printers.Add(new System.Collections.Generic.KeyValuePair<string, bool>(queue.FullName, queue.FullName.StartsWith(@"\\", StringComparison.Ordinal)));
                    }
                }
            }

            return printers;
        }

        private void OnOpenRequested(object sender, EventArgs e) => ShowOpenDialog();

        private void OnSaveAsRequested(object sender, EventArgs e) => ShowSaveAsDialog();

        private void OnLayerManagerRequested(object sender, EventArgs e) => ShowLayerManager();

        private void OnPluginLoadRequested(object sender, EventArgs e) => PluginManagerWindow.PickPlugin(Window.GetWindow(this), path => _model.LoadPlugin(path));

        private void OnPluginInstallRequested(object sender, EventArgs e) => PluginManagerWindow.PickPackage(Window.GetWindow(this), path => _model.InstallPlugin(path));

        private void OnPluginManagerRequested(object sender, EventArgs e) => ShowPluginManager();

        private void OnSettingsRequested(object sender, string category) => ShowGlobalSettings(category);

        /// <summary>DSETTINGS and the menus' settings item: Drafting Settings, after the menu or the command line is done with.</summary>
        private void OnDraftingSettingsRequested(object sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() => ShowDraftingSettings()));

        /// <summary>STYLE, DIMSTYLE and the style boxes' last items: the Text Style dialog or the Dimension Style Manager, after the command or the box is done with.</summary>
        private void OnStyleDialogRequested(object sender, string name) => Dispatcher.BeginInvoke(new Action(() => StyleDialogs.Show(Window.GetWindow(this), _model, name)));

        /// <summary>A click on a row of the DRAWINGS list selects it, Ctrl and Shift adding; a double click opens it.</summary>
        private void OnFileRowDown(object sender, MouseButtonEventArgs e)
        {
            if (_model == null || !(((FrameworkElement)sender).DataContext is FileRowViewModel row))
            {
                return;
            }

            FileList.Focus();

            if (e.ClickCount == 2)
            {
                _model.Activate(row);
            }
            else
            {
                ModifierKeys modifiers = Keyboard.Modifiers;
                _model.SelectFileRow(row, (modifiers & ModifierKeys.Control) != 0, (modifiers & ModifierKeys.Shift) != 0);
            }

            e.Handled = true;
        }

        /// <summary>A right click on a row: the row selected alone unless it is selected already, then its menu.</summary>
        private void OnFileRowRightUp(object sender, MouseButtonEventArgs e)
        {
            if (_model == null || !(((FrameworkElement)sender).DataContext is FileRowViewModel row))
            {
                return;
            }

            if (!row.IsSelected)
            {
                _model.SelectFileRow(row);
            }

            ShowMenu(_model.FileRowMenu(row), (UIElement)sender);
            e.Handled = true;
        }

        /// <summary>A right click on OSNAP of the status bar: the menu of the object snaps, as its arrow gives.</summary>
        private void OnStatusToggleRightUp(object sender, MouseButtonEventArgs e)
        {
            if (_model != null && ((FrameworkElement)sender).DataContext is StatusToggleViewModel toggle && toggle.Name == "OSNAP")
            {
                ShowMenu(_model.OsnapMenu(), (UIElement)sender);
                e.Handled = true;
            }
        }

        private void OnOsnapArrow(object sender, RoutedEventArgs e) => ShowMenu(_model?.OsnapMenu(), (UIElement)sender);

        /// <summary>The arrow of a split button of the ribbon, or the title of Modify: its menu dropped under it, as AutoCAD's ribbon drops one.</summary>
        private void OnRibbonMenu(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            ContextMenu menu = button.ContextMenu;

            if (menu != null)
            {
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        /// <summary>
        /// An item of a split button's menu picked: the button beside the arrow takes its label, icon and command, as AutoCAD's
        /// split buttons show the one used last, until another is picked. Extend picked from Trim's arrow leaves the button
        /// reading Extend and running EXTEND. Kept for the window's session only.
        /// </summary>
        private void OnSplitChoice(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;

            if (item.Parent is ContextMenu menu && menu.PlacementTarget is Button arrow && arrow.Parent is Panel split && split.Children.Count > 0 && split.Children[0] is Button main)
            {
                string name = AutomationProperties.GetName(item);
                main.Content = string.IsNullOrEmpty(name) ? item.Header : name;
                main.Tag = item.Tag;
                main.CommandParameter = item.CommandParameter;
                main.ToolTip = item.ToolTip;
            }
        }

        /// <summary>Opens a menu of a view model's items at the mouse.</summary>
        private static void ShowMenu(System.Collections.Generic.IReadOnlyList<CadMenuItemViewModel> items, UIElement target)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            ContextMenu menu = CadMenus.Make(items);
            menu.PlacementTarget = target;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

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

        /// <summary>Gives the canvas what Global settings say of it: colours, lineweights, font, wheel zoom, pickbox, aperture and whether a box may be dragged.</summary>
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
            CanvasView.SelectByDragging = settings.SelectByDragging;
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

        /// <summary>
        /// Puts the box of dynamic input below and to the right of the cursor, as AutoCAD's tooltip, or on the other side of it
        /// where it would leave the canvas.
        /// </summary>
        private void PlaceDynamicInput(double x, double y)
        {
            CadView view = CanvasView.View;

            if (view == null || CanvasView.ActualWidth <= 0.0 || CanvasView.PixelWidth <= 0)
            {
                return;
            }

            const double Offset = 18.0;
            double scale = CanvasView.PixelWidth / CanvasView.ActualWidth;
            view.DrawingToPixel(x, y, out double px, out double py);
            double left = px / scale + Offset;
            double top = py / scale + Offset;

            if (left + DynamicInputBox.ActualWidth > CanvasView.ActualWidth)
            {
                left = Math.Max(0.0, px / scale - Offset - DynamicInputBox.ActualWidth);
            }

            if (top + DynamicInputBox.ActualHeight > CanvasView.ActualHeight)
            {
                top = Math.Max(0.0, py / scale - Offset - DynamicInputBox.ActualHeight);
            }

            Canvas.SetLeft(DynamicInputBox, left);
            Canvas.SetTop(DynamicInputBox, top);
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

        /// <summary>The arrow under Paste: Paste, Paste as Block, Paste to Original Coordinates, Paste Special.</summary>
        private void OnPasteMenu(object sender, RoutedEventArgs e) => ShowMenu(_model?.PasteMenu(), (UIElement)sender);

        /// <summary>A menu of the Text Editor tab: Symbol, Justification, Line Spacing, Case, Clear; the editor keeps the keyboard.</summary>
        private void OnEditorMenu(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            if (button.DataContext is TextEditorViewModel editor)
            {
                ShowMenu(editor.Menu(button.CommandParameter as string ?? button.Tag as string), button);
            }
        }

        /// <summary>A box of the Text Editor tab (height, oblique, tracking, width): Enter sets the value and gives the keyboard back to the words.</summary>
        private void OnEditorBoxKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                var box = (TextBox)sender;

                if (e.Key == Key.Enter)
                {
                    box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }
                else
                {
                    box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                }

                MTextEditorBox.Focus();
                e.Handled = true;
            }
        }

        /// <summary>The edit box of a single-line text: Enter ends the edit with its words, Esc cancels it, as AutoCAD 2021's TEXTEDIT.</summary>
        private void OnInPlaceKey(object sender, KeyEventArgs e)
        {
            if (_model == null)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _model.CommitInPlace(InPlaceTextBox.Text);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _model.CancelInPlace();
            }
        }

        /// <summary>The edit box losing the keyboard to another part of the window ends the edit with its words, as a click outside it does.</summary>
        private void OnInPlaceLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_model != null && _model.IsTextEditShown && InPlaceTextBox.IsVisible && !InPlaceTextBox.IsKeyboardFocusWithin && e.NewFocus is UIElement)
            {
                _model.CommitInPlace(InPlaceTextBox.Text);
            }
        }

        /// <summary>A click on the canvas while a text is edited in place ends the edit with its words, as in AutoCAD; it picks nothing.</summary>
        private void OnCanvasDownWhileEditing(object sender, MouseButtonEventArgs e)
        {
            if (_model == null || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (_model.IsTextEditShown)
            {
                _model.CommitInPlace(InPlaceTextBox.Text);
                e.Handled = true;
            }
            else if (_model.IsMTextEditorShown)
            {
                _model.CloseTextEditor(true);
                e.Handled = true;
            }
        }

        private void OnInPlaceEditChanged(object sender, EventArgs e) => PlaceInPlace(true);

        /// <summary>
        /// Puts the editor of the text edited in place over it: the edit box of a single-line text with its baseline on the
        /// text's, as high as the text on the canvas and turned with it, its words all selected; the MTEXT editor's frame with
        /// the MTEXT's attachment point on its own. Hidden when no text is edited.
        /// </summary>
        /// <param name="opened">Whether the edit has just started, so that the editor takes the keyboard and its words.</param>
        private void PlaceInPlace(bool opened)
        {
            CadInPlaceEdit edit = _model?.InPlaceEdit;
            CadView view = CanvasView.View;

            if (edit == null || view == null || CanvasView.ActualWidth <= 0.0 || CanvasView.PixelWidth <= 0)
            {
                InPlaceTextBox.Visibility = Visibility.Collapsed;
                MTextEditorBox.Visibility = Visibility.Collapsed;

                if (MTextEditorBox.Model != null)
                {
                    MTextEditorBox.Model = null;
                }

                if (opened && (InPlaceTextBox.IsKeyboardFocusWithin || MTextEditorBox.IsKeyboardFocused))
                {
                    CanvasView.Focus();
                }

                return;
            }

            double scale = CanvasView.PixelWidth / CanvasView.ActualWidth;
            double unit = view.PixelsPerUnit / scale;
            view.DrawingToPixel(edit.Location.X, edit.Location.Y, out double px, out double py);
            double x = px / scale;
            double y = py / scale;
            double degrees = -edit.Rotation * 180.0 / Math.PI;

            if (edit.Kind == CadInPlaceKind.Text)
            {
                MTextEditorBox.Visibility = Visibility.Collapsed;
                double size = Math.Max(8.0, edit.Height * unit / 0.72);
                string family = _model.TrueTypeFamilyOf(edit.StyleName) ?? "Arial";
                int rgb = _model.DrawnRgbOf(edit.Handle);
                rgb = ((rgb >> 16) & 0xFF) + ((rgb >> 8) & 0xFF) + (rgb & 0xFF) < 60 ? 0xFFFFFF : rgb;
                InPlaceTextBox.FontFamily = new System.Windows.Media.FontFamily(family);
                InPlaceTextBox.FontSize = size;
                InPlaceTextBox.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
                InPlaceTextBox.MinWidth = Math.Max(40.0, size * 3.0);

                // The box's baseline on the text's: the font's ascent and the box's padding and border above it.
                double ascent = size * InPlaceTextBox.FontFamily.Baseline;
                double left = x - 5.0;
                double top = y - ascent - 1.0;
                Canvas.SetLeft(InPlaceTextBox, left);
                Canvas.SetTop(InPlaceTextBox, top);
                InPlaceTextBox.RenderTransform = new System.Windows.Media.RotateTransform(degrees, 5.0, y - top);
                InPlaceTextBox.Visibility = Visibility.Visible;

                if (opened)
                {
                    InPlaceTextBox.Text = edit.Value;
                    InPlaceTextBox.UpdateLayout();
                    InPlaceTextBox.Focus();
                    InPlaceTextBox.SelectAll();
                }

                return;
            }

            InPlaceTextBox.Visibility = Visibility.Collapsed;

            if (MTextEditorBox.Model != _model.TextEditor)
            {
                MTextEditorBox.Model = _model.TextEditor;
            }

            MTextEditorBox.PixelsPerUnit = unit;
            MTextEditorBox.DefaultRgb = edit.Handle != null ? _model.DrawnRgbOf(edit.Handle) : _model.LayerRgbOf(null);
            MTextEditorBox.LayerRgb = _model.LayerRgbOf(edit.Handle);
            MTextEditorBox.Visibility = Visibility.Visible;
            MTextEditorBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Point anchor = MTextEditorBox.Anchor;
            Canvas.SetLeft(MTextEditorBox, x - anchor.X);
            Canvas.SetTop(MTextEditorBox, y - anchor.Y);
            MTextEditorBox.RenderTransform = new System.Windows.Media.RotateTransform(degrees, anchor.X, anchor.Y);

            if (opened)
            {
                MTextEditorBox.Focus();
                Keyboard.Focus(MTextEditorBox);
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
