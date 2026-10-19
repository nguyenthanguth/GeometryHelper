using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
using GeometryHelper.Viewer.Views;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The window: the scene tree, the 3D view, the properties and output panes, and the status bar.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly CommandLine _command;
        private readonly Settings _settings;
        private readonly Keymap _keymap;
        private readonly string _keymapProblem;
        private readonly SceneModel _model = new SceneModel();
        private readonly ViewLayout _layout = new ViewLayout();
        private readonly ViewPane[] _panes = new ViewPane[ViewLayout.MaxViews];
        private readonly OverlayGroup _overlay = new OverlayGroup();
        private readonly ObservableCollection<TreeNode> _roots = new ObservableCollection<TreeNode>();
        private readonly ObservableCollection<PropertyRow> _rows = new ObservableCollection<PropertyRow>();
        private readonly List<TreeNode> _itemNodes = new List<TreeNode>();
        private string _currentPath;
        private CancellationTokenSource _running;
        private bool _syncingSelection;
        private bool _rerunQueued;
        private readonly HashSet<CancellationTokenSource> _abandoned = new HashSet<CancellationTokenSource>();
        private FileSystemWatcher _watcher;
        private DispatcherTimer _watchTimer;

        /// <summary>
        /// Initializes the window.
        /// </summary>
        internal MainWindow(CommandLine command)
        {
            _command = command;
            InitializeComponent();

            // Headless runs (a picture for a build) leave the user's settings alone.
            _settings = command.Headless ? new Settings() : Settings.Load();
            _keymap = Keymap.Load(command.Headless ? null : Path.Combine(Settings.Folder, "keymap.json"), out _keymapProblem);

            // One graphics device for all the views; each view has a viewport of its own, made in code when it is first shown.
            _effects = new HelixToolkit.SharpDX.Core.DefaultEffectsManager();
            Panes.Layout = _layout;
            _model.SceneChanged += (s, e) => RebuildSavedViewsMenu();
            Tree.ItemsSource = _roots;
            PropertiesList.ItemsSource = _rows;
            CompareList.ItemsSource = _compareRows;
            ClipList.ItemsSource = _clipRows;

            ApplySettings();
            ShowLayout();
            BuildDiagnosticsPane();
            LabelMenusWithKeys();
            RebuildRecentMenu();
            UpdateSnapsText();
        }

        private void ApplySettings()
        {
            if (_command.Headless)
            {
                Width = 1280.0;
                Height = 800.0;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            Width = Math.Max(600.0, _settings.Width);
            Height = Math.Max(400.0, _settings.Height);

            if (_settings.Left.HasValue && _settings.Top.HasValue
                && _settings.Left.Value > SystemParameters.VirtualScreenLeft - 50 && _settings.Left.Value < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
                && _settings.Top.Value > SystemParameters.VirtualScreenTop - 50 && _settings.Top.Value < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = _settings.Left.Value;
                Top = _settings.Top.Value;
            }

            if (_settings.Maximized)
            {
                WindowState = WindowState.Maximized;
            }

            TreeColumn.Width = new GridLength(Math.Max(120.0, _settings.TreeWidth));
            PropertiesColumn.Width = new GridLength(Math.Max(150.0, _settings.PropertiesWidth));
            OutputRow.Height = new GridLength(Math.Max(40.0, _settings.OutputHeight));
            SetPropertiesPane(_settings.ShowProperties);
            GridMenu.IsChecked = _settings.Grid;
            WatchMenu.IsChecked = _settings.RerunOnSave;
            CompareWatchMenu.IsChecked = _settings.CompareWatch;
            ClipCapsMenu.IsChecked = _settings.ClipCaps;
            _clipCaps = _settings.ClipCaps;

            // The layout of the views comes back as it was left; a settings file from before there were several views has only the render mode, which was the view's.
            if (_settings.Layout != null)
            {
                _layout.Restore(_settings.Layout);
            }
            else if (Enum.TryParse(_settings.RenderMode, out RenderMode mode) && Enum.IsDefined(typeof(RenderMode), mode))
            {
                _layout.ActiveSlot.Mode = mode;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // A picture for a build must end: with 0 when it is written, with 1 when anything goes wrong, and with 1 if it takes
            // more than a minute and a half.
            if (_command.Screenshot != null)
            {
                var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(90) };
                watchdog.Tick += (s, a) =>
                {
                    Console.Error.WriteLine("FAILED: no picture in 90 seconds.");
                    Application.Current.Shutdown(1);
                };
                watchdog.Start();
            }

            try
            {
                if (_keymapProblem != null)
                {
                    Log(_keymapProblem);
                }

                SetGrid(GridMenu.IsChecked == true);
                ParseHeadlessOptions();
                UpdateModeText();
                UpdateCamera();

                if (_command.File != null)
                {
                    bool opened = await OpenFileAsync(_command.File);

                    if (_command.Screenshot != null)
                    {
                        await TakeScreenshotAsync(opened);
                    }
                }
                else if (_command.Screenshot != null)
                {
                    await TakeScreenshotAsync(false);
                }
                else
                {
                    Log("Open a script (.csx), a scene (.ghscene.json) or an .obj, .dxf, .wkt or .svg file with File > Open, or drop one on the window. Keys: Help > Keys.");
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                Log("Error: " + ex);

                if (_command.Screenshot != null)
                {
                    Console.Error.WriteLine("FAILED: " + ex.Message);
                    Application.Current.Shutdown(1);
                }
            }
        }
        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _running?.Cancel();
            _compareCts?.Cancel();
            LeaveCompare(false);
            _watcher?.Dispose();

            if (!_command.Headless)
            {
                _settings.Maximized = WindowState == WindowState.Maximized;

                if (WindowState == WindowState.Normal)
                {
                    _settings.Left = Left;
                    _settings.Top = Top;
                    _settings.Width = Width;
                    _settings.Height = Height;
                }

                _settings.TreeWidth = TreeColumn.ActualWidth;
                _settings.PropertiesWidth = PropertiesPane.Visibility == Visibility.Visible ? PropertiesColumn.ActualWidth : _settings.PropertiesWidth;
                _settings.OutputHeight = OutputRow.ActualHeight;
                _settings.ShowProperties = PropertiesPane.Visibility == Visibility.Visible;
                SaveDiagnosticsSettings();
                _settings.RenderMode = Active.Mode.ToString();
                _settings.Layout = _layout.Capture();
                _settings.Grid = GridMenu.IsChecked == true;
                _settings.RerunOnSave = WatchMenu.IsChecked == true;
                _settings.CompareWatch = CompareWatchMenu.IsChecked == true;
                _settings.ClipCaps = ClipCapsMenu.IsChecked == true;
                _settings.Save();
            }

            _model.Clear();

            foreach (ViewPane pane in _panes)
            {
                pane?.Dispose();
            }
        }

        #region Files

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                _ = OpenFileAsync(files[0]);
            }
        }

        private void OpenWithDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open a script, a scene or a file of the library",
                Filter = "Scripts, scenes and files (*.csx;*.ghscene.json;*.json;*.obj;*.dxf;*.wkt;*.svg)|*.csx;*.ghscene.json;*.json;*.obj;*.dxf;*.wkt;*.svg|Scripts and scenes (*.csx;*.ghscene.json;*.json)|*.csx;*.ghscene.json;*.json|OBJ, DXF, WKT, SVG (*.obj;*.dxf;*.wkt;*.svg)|*.obj;*.dxf;*.wkt;*.svg|All files (*.*)|*.*",
                InitialDirectory = _settings.LastFolder != null && Directory.Exists(_settings.LastFolder) ? _settings.LastFolder : null,
            };

            if (dialog.ShowDialog(this) == true)
            {
                _ = OpenFileAsync(dialog.FileName);
            }
        }

        /// <summary>
        /// Opens a script or a scene file; the scene before stays shown where the file does not open. Only one file is opened at
        /// a time: while a script is alive, which a cancelled script is until it has ended, nothing else is started.
        /// </summary>
        /// <returns>Whether it opened.</returns>
        private async Task<bool> OpenFileAsync(string path)
        {
            if (!File.Exists(path))
            {
                Log("No such file: " + path);
                return false;
            }

            if (_compareCts != null)
            {
                SetMessage("a comparison is running (Esc to stop)");
                Log("A comparison is running; press Esc to stop it before opening another file.");
                return false;
            }

            if (_running != null)
            {
                if (!_running.IsCancellationRequested)
                {
                    SetMessage("a script is still running (Esc to stop)");
                    Log("A script is still running; press Esc to stop it before starting another.");
                    return false;
                }

                // Stop was already asked for and the script has not ended: asking again lets go of it.
                AbandonRun();
            }

            var cts = new CancellationTokenSource();
            _running = cts;
            bool script = SceneLoader.IsScript(path);

            BusyText.Text = script ? "Running " + Path.GetFileName(path) + "... Esc to stop" : "Reading " + Path.GetFileName(path) + "...";
            BusyText.Visibility = Visibility.Visible;
            Log("--- " + (script ? "running " : "reading ") + path);

            try
            {
                LoadResult result = await SceneLoader.LoadAsync(path, note => Dispatcher.BeginInvoke(new Action(() => { if (!_abandoned.Contains(cts)) { Log(note); } })), cts.Token);

                if (_abandoned.Remove(cts))
                {
                    // The window let go of this run while it went on; its result belongs to nobody.
                    return false;
                }

                foreach (string error in result.Errors)
                {
                    Log(error);
                }

                if (!result.Succeeded)
                {
                    Log("The previous scene stays shown.");
                    SetMessage("failed: " + Path.GetFileName(path));
                    return false;
                }

                var built = await Task.Run(() =>
                {
                    Primitives drawn = Primitives.Build(result.Scene);
                    return Tuple.Create(drawn, ExactGeometry.Build(result.Scene, drawn));
                });
                Primitives primitives = built.Item1;
                ExactGeometry geometry = built.Item2;

                foreach (string warning in primitives.Warnings.Concat(geometry.Warnings))
                {
                    Log("warning: " + warning);
                }

                foreach (string warning in result.Scene.LoadWarnings)
                {
                    Log("warning: " + warning);
                }

                foreach (string kind in result.Scene.UnknownKinds)
                {
                    Log("warning: the kind '" + kind + "' is kept but not drawn.");
                }

                MergeKeptViews(result.WasScript ? path : null, result.Scene);

                // The clip planes in force cut the new scene too: a script run again is looked at through the same section.
                ClipSet cutBy = _clips;
                ClipResult clipped = await ClipAsync(primitives, cutBy);

                bool sameFile = string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase) && _compare == null;
                LeaveCompare(false);
                _currentPath = path;
                Title = (string.IsNullOrEmpty(result.Scene.Title) ? Path.GetFileName(path) : result.Scene.Title) + " - GeometryHelper Viewer";
                _model.Show(result.Scene, primitives, clipped);
                ResetTools(geometry, sameFile && result.WasScript);
                BuildTree();
                ShowProperties();
                UpdateSelectionText();
                CutAfterShow(cutBy);
                ToleranceText.Text = "EqualPoint " + result.Scene.EqualPoint.ToString("G", CultureInfo.InvariantCulture);
                Log("OK: " + result.Scene.Items.Count + " item(s) on " + result.Scene.Layers.Count + " layer(s)" + (result.WasScript ? ", " + result.Notes.Count + " note(s)" : string.Empty) + ".");

                // A script run again keeps the view where the user put it, so that an edit can be watched; a new file is fitted, and opens at its first saved
                // view where it has one, which is how a repro opens at the spot it is about.
                if (!sameFile || !result.WasScript)
                {
                    FitAllViews();

                    // A --view on the command line is the view that was asked for; it wins over the first saved view of the scene.
                    if (_command.View == null)
                    {
                        RestoreFirstSavedView();
                    }
                }
                else
                {
                    UpdateAllCameras();
                }

                if (!_command.Headless)
                {
                    _settings.LastFolder = Path.GetDirectoryName(Path.GetFullPath(path));
                    _settings.AddRecent(Path.GetFullPath(path));
                    RebuildRecentMenu();
                }

                WatchFile(path);
                SetMessage(string.Empty);
                return true;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // Whatever a file does to the viewer, the busy text goes, the next file can be opened, and the scene before stays.
                Log("Cannot show " + path + ": " + e.GetType().Name + ": " + e.Message);
                SetMessage("failed: " + Path.GetFileName(path));
                return false;
            }
            finally
            {
                if (ReferenceEquals(_running, cts))
                {
                    BusyText.Visibility = Visibility.Collapsed;
                    _running = null;
                }

                _abandoned.Remove(cts);
                cts.Dispose();

                // A save that came while the script ran is run once more, with what the file says now.
                if (_rerunQueued)
                {
                    _rerunQueued = false;

                    if (_currentPath != null && WatchMenu.IsChecked == true)
                    {
                        Log("The file changed while it ran.");
                        _ = Dispatcher.BeginInvoke(new Action(() => RerunCurrent()));
                    }
                }
            }
        }
        /// <summary>
        /// Runs the open script again: the comparison again where one is open, otherwise the script in the window.
        /// </summary>
        private void RerunCurrent()
        {
            if (_compare != null)
            {
                RerunCompare();
            }
            else if (_currentPath != null)
            {
                _ = OpenFileAsync(_currentPath);
            }
        }

        private void RebuildRecentMenu()
        {
            RecentMenu.Items.Clear();

            foreach (string path in _settings.Recent)
            {
                var item = new MenuItem { Header = path.Replace("_", "__") };
                string captured = path;
                item.Click += (s, e) => _ = OpenFileAsync(captured);
                RecentMenu.Items.Add(item);
            }

            RecentMenu.IsEnabled = RecentMenu.Items.Count > 0;
        }

        private void SaveSceneAs()
        {
            if (_model.Scene == null)
            {
                SetMessage("no scene to save");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save the scene",
                Filter = "Scene (*.ghscene.json)|*.ghscene.json",
                FileName = (string.IsNullOrEmpty(_model.Scene.Title) ? "scene" : string.Concat(_model.Scene.Title.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'))) + ".ghscene.json",
                InitialDirectory = _settings.LastFolder != null && Directory.Exists(_settings.LastFolder) ? _settings.LastFolder : null,
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            if (InsideARepository(dialog.FileName)
                && MessageBox.Show(
                    this,
                    "This folder is inside a Git repository. A scene of a real model must never be saved where it can be committed.\n\nSave it here anyway?",
                    "Save a scene in a repository",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                new SceneWriter(_model.Scene).Save(dialog.FileName);
                Log("Saved " + dialog.FileName);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log("Cannot save " + dialog.FileName + ": " + e.Message);
            }
        }

        private static bool InsideARepository(string path)
        {
            for (DirectoryInfo directory = new FileInfo(path).Directory; directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return true;
                }
            }

            return false;
        }

        private void WatchFile(string path)
        {
            _watcher?.Dispose();
            _watcher = null;

            if (!SceneLoader.IsScript(path))
            {
                return;
            }

            try
            {
                string full = Path.GetFullPath(path);
                _watcher = new FileSystemWatcher(Path.GetDirectoryName(full), Path.GetFileName(full)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size, EnableRaisingEvents = true };
                _watcher.Changed += (s, e) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (WatchMenu.IsChecked != true)
                    {
                        return;
                    }

                    // An editor writes a file in several steps; the run waits for them to stop.
                    if (_watchTimer == null)
                    {
                        _watchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                        _watchTimer.Tick += (t, a) =>
                        {
                            _watchTimer.Stop();

                            if (_running != null || _compareCts != null)
                            {
                                _rerunQueued = true;
                            }
                            else if (_currentPath != null)
                            {
                                Log("The file changed.");
                                RerunCurrent();
                            }
                        };
                    }

                    _watchTimer.Stop();
                    _watchTimer.Start();
                }));
            }
            catch (Exception e) when (e is ArgumentException || e is IOException)
            {
                Log("Cannot watch " + path + ": " + e.Message);
            }
        }

        #endregion

        #region Tree and properties

        private void BuildTree()
        {
            _roots.Clear();
            _itemNodes.Clear();
            Scene scene = _model.Scene;

            if (scene == null)
            {
                return;
            }

            var layerNodes = new Dictionary<string, TreeNode>(StringComparer.Ordinal);

            foreach (Layer layer in scene.Layers)
            {
                SceneColor.TryParse(layer.Color, out byte r, out byte g, out byte b);
                string name = layer.Name;
                var node = new TreeNode(name + " (" + scene.Items.Count(i => i.Layer == name) + ")", new SolidColorBrush(Color.FromRgb(r, g, b)), -1, name, OnTreeChecked) { IsChecked = !_model.IsLayerHidden(name) };
                layerNodes[name] = node;
                _roots.Add(node);
            }

            for (int i = 0; i < scene.Items.Count; i++)
            {
                SceneItem item = scene.Items[i];
                string text = item.Color ?? scene.FindLayer(item.Layer)?.Color;
                SceneColor.TryParse(text, out byte r, out byte g, out byte b);
                var node = new TreeNode(item.Name ?? item.Kind, new SolidColorBrush(Color.FromRgb(r, g, b)), i, item.Layer, OnTreeChecked);
                layerNodes[item.Layer].Children.Add(node);
                _itemNodes.Add(node);
            }
        }

        private void OnTreeChecked(TreeNode node, bool isChecked)
        {
            if (_syncingSelection)
            {
                return;
            }

            if (node.ItemIndex < 0)
            {
                _model.SetLayerHidden(node.Layer, !isChecked);
            }
            else
            {
                _model.SetItemHidden(node.ItemIndex, !isChecked);
            }
        }

        private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_syncingSelection || !(Tree.SelectedItem is TreeNode node))
            {
                return;
            }

            _model.Select(node.ItemIndex >= 0 ? new[] { node.ItemIndex } : _model.Scene.Items.Select((item, i) => new { item, i }).Where(x => x.item.Layer == node.Layer).Select(x => x.i));
            AfterSelectionChanged(false);
        }

        private void AfterSelectionChanged(bool syncTree)
        {
            if (syncTree)
            {
                _syncingSelection = true;

                try
                {
                    foreach (TreeNode node in _itemNodes)
                    {
                        node.IsSelected = false;
                    }

                    if (_model.Selected.Count == 1)
                    {
                        TreeNode node = _itemNodes[_model.Selected.First()];
                        node.IsSelected = true;
                    }
                }
                finally
                {
                    _syncingSelection = false;
                }
            }

            UpdateSelectionText();
            ShowProperties();
        }

        private void SyncTreeChecks()
        {
            _syncingSelection = true;

            try
            {
                foreach (TreeNode layer in _roots)
                {
                    layer.SetCheckedQuietly(!_model.IsLayerHidden(layer.Layer));
                }

                foreach (TreeNode node in _itemNodes)
                {
                    node.SetCheckedQuietly(!_model.IsItemHidden(node.ItemIndex));
                }
            }
            finally
            {
                _syncingSelection = false;
            }
        }

        private void UpdateSelectionText()
        {
            SelectionText.Text = _model.Selected.Count + " selected";
        }

        private async void ShowProperties()
        {
            _rows.Clear();
            Scene scene = _model.Scene;

            if (scene == null)
            {
                return;
            }

            IReadOnlyCollection<int> selected = _model.Selected;

            if (selected.Count == 0)
            {
                _rows.Add(new PropertyRow("Scene", scene.Title ?? string.Empty));
                _rows.Add(new PropertyRow("Items", scene.Items.Count.ToString(CultureInfo.InvariantCulture)));
                _rows.Add(new PropertyRow("Layers", scene.Layers.Count.ToString(CultureInfo.InvariantCulture)));
                _rows.Add(new PropertyRow("EqualPoint", scene.EqualPoint.ToString("G", CultureInfo.InvariantCulture)));

                Bounds3 bounds = _model.Primitives.Bounds;

                if (!bounds.IsEmpty)
                {
                    _rows.Add(new PropertyRow("Bounds min", bounds.Min.ToString()));
                    _rows.Add(new PropertyRow("Bounds max", bounds.Max.ToString()));
                    _rows.Add(new PropertyRow("Bounds size", bounds.Size.ToString()));
                }

                return;
            }

            if (selected.Count > 1)
            {
                _rows.Add(new PropertyRow("Selected", selected.Count.ToString(CultureInfo.InvariantCulture)));

                foreach (IGrouping<string, int> kind in selected.GroupBy(i => scene.Items[i].Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    _rows.Add(new PropertyRow(kind.Key, kind.Count().ToString(CultureInfo.InvariantCulture)));
                }

                return;
            }

            int index = selected.First();
            SceneItem item = scene.Items[index];
            _rows.Add(new PropertyRow("Kind", item.Kind));
            _rows.Add(new PropertyRow("Name", item.Name ?? string.Empty));
            _rows.Add(new PropertyRow("Layer", item.Layer));
            _rows.Add(new PropertyRow("Colour", item.Color ?? scene.FindLayer(item.Layer)?.Color ?? string.Empty));

            foreach (KeyValuePair<string, string> pair in item.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                _rows.Add(new PropertyRow(pair.Key, pair.Value));
            }

            ItemPrimitives drawn = _model.Primitives.Items[index];
            _rows.Add(new PropertyRow("Measuring...", string.Empty));
            int measuring = _rows.Count - 1;

            // A body is closed and cut for its volume, which can take a while; the pane fills in when it is done.
            IReadOnlyList<KeyValuePair<string, string>> numbers = await Task.Run(() => ItemMeasures.Describe(item, scene, drawn));

            if (_model.Selected.Count == 1 && _model.Selected.First() == index && measuring < _rows.Count && _rows[measuring].Name == "Measuring...")
            {
                _rows.RemoveAt(measuring);

                foreach (KeyValuePair<string, string> pair in numbers)
                {
                    _rows.Add(new PropertyRow(pair.Key, pair.Value));
                }
            }
        }

        private void SetPropertiesPane(bool show)
        {
            PropertiesPane.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            PropertiesSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            PropertiesColumn.Width = show ? new GridLength(Math.Max(150.0, _settings.PropertiesWidth)) : new GridLength(0.0);
            PropertiesColumn.MinWidth = show ? 150.0 : 0.0;
            SplitterColumn.Width = show ? new GridLength(5.0) : new GridLength(0.0);
            PropertiesMenu.IsChecked = show;
        }

        #endregion

        #region Output and status

        private void Log(string line)
        {
            Output.AppendText(line + Environment.NewLine);
            Output.ScrollToEnd();
        }

        /// <summary>
        /// Says a line in the output pane.
        /// </summary>
        internal void Report(string line) => Log(line);

        /// <summary>
        /// Lets go of a script that was asked to stop and has not: the window is free, the script's result is ignored, and its
        /// thread keeps running until it ends, since a thread cannot be taken from a script that never looks at its token.
        /// </summary>
        private void AbandonRun()
        {
            if (_running == null)
            {
                return;
            }

            _abandoned.Add(_running);
            _running = null;
            BusyText.Visibility = Visibility.Collapsed;
            Log("The script was abandoned; the thread keeps running until it ends.");
            SetMessage("script abandoned");
        }

        private void SetMessage(string text) => MessageText.Text = text;

        private void UpdateModeText()
        {
            ModeText.Text = Active.Mode + (Active.Camera.Orthographic ? ", ortho" : ", perspective") + (_layout.Count > 1 ? ", view " + (_layout.Active + 1) : string.Empty);
        }

        #endregion
    }
}
