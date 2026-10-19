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
using GeometryHelper.Viewer.Builds;
using Microsoft.Win32;

namespace GeometryHelper.Viewer
{
    public partial class MainWindow
    {
        private readonly ObservableCollection<CompareRow> _compareRows = new ObservableCollection<CompareRow>();
        private CompareState _compare;
        private CancellationTokenSource _compareCts;
        private bool _pendingLeft;
        private bool _pendingRight;
        private CompareRow _comparePressed;
        private bool _syncingCompare;

        /// <summary>
        /// What is open when the window shows a comparison: the script, the two builds, what each gave, and how they were laid over each other.
        /// </summary>
        private sealed class CompareState
        {
            internal CompareSetup Setup;
            internal SideRunResult LeftRun;
            internal SideRunResult RightRun;
            internal ComparisonScene Layout;
            internal CompareView View = CompareView.Both;

            // The items the side shown hides, as opposed to the ones the user hid: only the first are shown again when the side changes.
            internal HashSet<int> HiddenByView = new HashSet<int>();
            internal DllWatcher LeftWatch;
            internal DllWatcher RightWatch;
        }

        /// <summary>
        /// A row of the table of the comparison.
        /// </summary>
        private sealed class CompareRow
        {
            public int Number { get; set; }

            public string Status { get; set; }

            public string Layer { get; set; }

            public string Item { get; set; }

            public string Kind { get; set; }

            public string Vertices { get; set; }

            public string LengthLeft { get; set; }

            public string LengthRight { get; set; }

            public string AreaLeft { get; set; }

            public string AreaRight { get; set; }

            public string VolumeLeft { get; set; }

            public string VolumeRight { get; set; }

            public string Identical { get; set; }

            public string Difference { get; set; }

            public Brush Foreground { get; set; }

            internal int Index { get; set; }

            public override string ToString()
                => "#" + Number.ToString(CultureInfo.InvariantCulture) + " " + Status + " | " + Layer + " | " + Item + " | " + Kind + " | vertices " + Vertices
                   + " | length " + LengthLeft + " / " + LengthRight + " | area " + AreaLeft + " / " + AreaRight + " | volume " + VolumeLeft + " / " + VolumeRight
                   + " | bit-identical " + Identical + (Difference.Length > 0 ? " | " + Difference : string.Empty);
        }

        #region Starting

        private CompareEnvironment NewCompareEnvironment(int timeoutSeconds)
        {
            CompareEnvironment environment = CompareEnvironment.CreateDefault();
            environment.RunTimeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
            environment.CacheKeep = Math.Max(1, _settings.CompareCacheKeep);
            return environment;
        }

        private static string FindRepository(string start)
        {
            if (string.IsNullOrEmpty(start))
            {
                return null;
            }

            for (DirectoryInfo directory = File.Exists(start) ? new FileInfo(start).Directory : new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }
            }

            return null;
        }

        private void ShowCompareDialog()
        {
            if (_compareCts != null)
            {
                SetMessage("a comparison is running (Esc to stop)");
                return;
            }

            string script = _currentPath != null && SceneLoader.IsScript(_currentPath) ? _currentPath : _settings.Recent.FirstOrDefault(SceneLoader.IsScript);
            string repository = _settings.CompareRepository;

            if (string.IsNullOrEmpty(repository) || !Directory.Exists(repository))
            {
                repository = FindRepository(script) ?? FindRepository(AppContext.BaseDirectory) ?? string.Empty;
            }

            BuildSource.TryParse(_settings.CompareLeft, repository, out BuildSource left, out _);
            BuildSource.TryParse(_settings.CompareRight, repository, out BuildSource right, out _);
            var dialog = new CompareDialog(
                this,
                new CompareSetup { Script = script, Left = left ?? BuildSource.Own, Right = right ?? BuildSource.Own, Repository = repository, TimeoutSeconds = _settings.CompareTimeoutSeconds, Watch = _settings.CompareWatch });

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            CompareSetup setup = dialog.Result;
            _settings.CompareLeft = setup.Left.ToString();
            _settings.CompareRight = setup.Right.ToString();
            _settings.CompareRepository = setup.Repository;
            _settings.CompareTimeoutSeconds = setup.TimeoutSeconds;
            _settings.CompareWatch = setup.Watch;
            _ = RunCompareAsync(new CompareState { Setup = setup }, true, true, false);
        }

        /// <summary>
        /// Runs the script against the sides asked for, in child processes at once, compares what they wrote, and shows the two laid over each other.
        /// A side that is not asked for keeps what it gave last time.
        /// </summary>
        /// <returns>Whether the comparison is shown.</returns>
        private async Task<bool> RunCompareAsync(CompareState state, bool runLeft, bool runRight, bool keepView)
        {
            if (_compareCts != null)
            {
                SetMessage("a comparison is running (Esc to stop)");
                return false;
            }

            if (_running != null)
            {
                SetMessage("a script is still running (Esc to stop)");
                return false;
            }

            var cts = new CancellationTokenSource();
            _compareCts = cts;
            CompareSetup setup = state.Setup;
            CompareEnvironment environment = NewCompareEnvironment(setup.TimeoutSeconds);
            BusyText.Text = "Comparing " + Path.GetFileName(setup.Script) + "... Esc to stop";
            BusyText.Visibility = Visibility.Visible;
            Log("--- comparing " + setup.Script);
            Log("    left : " + setup.Left.Describe());
            Log("    right: " + setup.Right.Describe());

            void Say(string line) => Dispatcher.BeginInvoke(new Action(() => Log(line)));

            try
            {
                // A script that was edited since a side ran is run again on both sides: what each gave is of a text that is gone.
                string scriptHash = SideRunner.HashOfScript(setup.Script);

                if (scriptHash != null && ((state.LeftRun != null && state.LeftRun.ScriptHash != scriptHash) || (state.RightRun != null && state.RightRun.ScriptHash != scriptHash)))
                {
                    runLeft = true;
                    runRight = true;
                }

                Task<SideRunResult> left = runLeft || state.LeftRun == null
                    ? SideRunner.RunAsync(environment, setup.Script, setup.Left, "left", Say, cts.Token)
                    : Task.FromResult(state.LeftRun);
                Task<SideRunResult> right = runRight || state.RightRun == null
                    ? SideRunner.RunAsync(environment, setup.Script, setup.Right, "right", Say, cts.Token)
                    : Task.FromResult(state.RightRun);
                await Task.WhenAll(left, right);
                SideRunResult leftRun = left.Result;
                SideRunResult rightRun = right.Result;

                // What a side gave stays for the next run of the other side alone, while it is good.
                if (leftRun.Succeeded)
                {
                    state.LeftRun = leftRun;
                }

                if (rightRun.Succeeded)
                {
                    state.RightRun = rightRun;
                }

                foreach (SideRunResult run in new[] { leftRun, rightRun })
                {
                    Log("    " + run.Label + ": " + (run.Succeeded ? "ok" : "FAILED") + ", " + run.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" + (run.LibraryLine != null ? ", library " + run.LibraryLine : string.Empty));

                    foreach (string error in run.Errors)
                    {
                        Log("    " + run.Label + " error: " + error);
                    }
                }

                if (!leftRun.Succeeded || !rightRun.Succeeded)
                {
                    Log("The previous scene stays shown.");
                    SetMessage("compare failed");
                    return false;
                }

                SceneComparison comparison = await Task.Run(() => SceneComparison.Compare(leftRun.Scene, rightRun.Scene));
                ComparisonScene layout = ComparisonScene.Build(comparison, leftRun.Scene, rightRun.Scene);
                string dashWarning = null;
                var built = await Task.Run(() =>
                {
                    Primitives drawn = Primitives.Build(layout.Scene);
                    dashWarning = layout.ApplyDashes(drawn);
                    return Tuple.Create(drawn, ExactGeometry.Build(layout.Scene, drawn));
                });

                if (cts.IsCancellationRequested)
                {
                    return false;
                }

                foreach (string warning in built.Item1.Warnings.Concat(built.Item2.Warnings).Concat(dashWarning == null ? new string[0] : new[] { dashWarning }))
                {
                    Log("warning: " + warning);
                }

                ClipSet cutBy = _clips;
                ClipResult clipped = await ClipAsync(built.Item1, cutBy);

                if (cts.IsCancellationRequested)
                {
                    return false;
                }

                state.Layout = layout;
                state.HiddenByView = new HashSet<int>();
                LeaveCompare(false);
                _compare = state;
                _currentPath = setup.Script;
                Title = layout.Scene.Title + " - GeometryHelper Viewer";
                _model.Show(layout.Scene, built.Item1, clipped);
                CutAfterShow(cutBy);
                ResetTools(built.Item2, keepView);
                BuildTree();
                ShowProperties();
                UpdateSelectionText();
                ToleranceText.Text = "EqualPoint " + layout.Scene.EqualPoint.ToString("G", CultureInfo.InvariantCulture);
                FillCompareRows(comparison);
                CompareTab.Visibility = Visibility.Visible;
                BottomTabs.SelectedItem = CompareTab;

                // The table wants room: a dozen rows and the buttons above them.
                if (OutputRow.ActualHeight < 280.0)
                {
                    OutputRow.Height = new GridLength(280.0);
                }

                state.View = keepView ? state.View : CompareView.Both;
                ApplyCompareView(state.View, false);

                if (!keepView)
                {
                    FitAllViews();

                    // The comparison opens at the spot the script saved, as a scene opened by itself does; a --view on the command line wins.
                    if (_command.View == null)
                    {
                        RestoreFirstSavedView();
                    }
                }
                else
                {
                    UpdateAllCameras();
                }

                Log("compare: " + comparison.Summary);

                foreach (string note in comparison.Notes)
                {
                    Log("compare: " + note);
                }

                SetMessage(string.Empty);
                WatchFile(setup.Script);
                SetUpCompareWatchers(state, environment);
                return true;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Log("Cannot compare: " + e.GetType().Name + ": " + e.Message);
                SetMessage("compare failed");
                return false;
            }
            finally
            {
                if (ReferenceEquals(_compareCts, cts))
                {
                    _compareCts = null;
                    BusyText.Visibility = Visibility.Collapsed;
                }

                cts.Dispose();

                // A save of the script while the comparison ran is run again once it has ended, as a script is.
                if (_rerunQueued && _compareCts == null)
                {
                    _rerunQueued = false;

                    if (_compare != null && WatchMenu.IsChecked == true)
                    {
                        Log("The file changed while the comparison ran.");
                        _pendingLeft = true;
                        _pendingRight = true;
                    }
                }

                // A DLL that was rebuilt while the run went on is run again once.
                if (_compare != null && (_pendingLeft || _pendingRight) && _compareCts == null)
                {
                    bool again = _pendingLeft;
                    bool againRight = _pendingRight;
                    _pendingLeft = false;
                    _pendingRight = false;
                    Log("A DLL changed while the comparison ran.");
                    _ = Dispatcher.BeginInvoke(new Action(() => _ = RunCompareAsync(_compare, again, againRight, true)));
                }
            }
        }

        private void RerunCompare()
        {
            if (_compare == null)
            {
                return;
            }

            _ = RunCompareAsync(_compare, true, true, true);
        }

        #endregion

        #region The table and the view

        private void FillCompareRows(SceneComparison comparison)
        {
            _syncingCompare = true;

            try
            {
                _compareRows.Clear();
                var identical = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));
                var changed = new SolidColorBrush(Color.FromRgb(0xB0, 0x10, 0x10));
                var onlyLeft = new SolidColorBrush(Color.FromRgb(0xB0, 0x60, 0x00));
                var onlyRight = new SolidColorBrush(Color.FromRgb(0x10, 0x50, 0xA0));
                var numbers = new SolidColorBrush(Color.FromRgb(0x9A, 0x7A, 0x00));

                for (int i = 0; i < comparison.Items.Count; i++)
                {
                    ComparedItem item = comparison.Items[i];
                    string text(double? value) => value.HasValue ? value.Value.ToString("G10", CultureInfo.InvariantCulture) : "-";
                    string count(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "-";

                    _compareRows.Add(new CompareRow
                    {
                        Index = i,
                        Number = item.Number,
                        Status = StatusText(item),
                        Layer = item.Layer,
                        Item = item.Name ?? string.Empty,
                        Kind = item.RightKind == null ? item.Kind : item.Kind + " -> " + item.RightKind,
                        Vertices = count(item.Left?.Vertices) + " / " + count(item.Right?.Vertices),
                        LengthLeft = text(item.Left?.Length),
                        LengthRight = text(item.Right?.Length),
                        AreaLeft = text(item.Left?.Area),
                        AreaRight = text(item.Right?.Area),
                        VolumeLeft = text(item.Left?.Volume),
                        VolumeRight = text(item.Right?.Volume),
                        Identical = item.Status == CompareStatus.Identical || item.Status == CompareStatus.NumbersDiffer ? (item.DataIdentical ? "yes" : "no") : item.Status == CompareStatus.Changed ? "no" : "-",
                        Difference = item.Difference,
                        Foreground = item.Status == CompareStatus.Identical ? identical : item.Status == CompareStatus.NumbersDiffer ? numbers : item.Status == CompareStatus.Changed ? changed : item.Status == CompareStatus.OnlyLeft ? onlyLeft : onlyRight,
                    });
                }

                CompareSummary.Text = comparison.Summary;
            }
            finally
            {
                _syncingCompare = false;
            }
        }

        private static string StatusText(ComparedItem item)
        {
            string text;

            switch (item.Status)
            {
                case CompareStatus.Identical: text = "same"; break;
                case CompareStatus.NumbersDiffer: text = "numbers differ"; break;
                case CompareStatus.Changed: text = "changed"; break;
                case CompareStatus.OnlyLeft: text = "only left"; break;
                default: text = "only right"; break;
            }

            // How the two were found to be the one item, where it was not by a name the script gave.
            if (item.Match == CompareMatch.Order)
            {
                text += " (by order)";
            }
            else if (item.Match == CompareMatch.Data)
            {
                text += " (by data)";
            }

            return item.Reordered ? text + ", moved" : text;
        }

        private void SetCompareView(CompareView view)
        {
            if (_compare == null)
            {
                SetMessage("no comparison is open");
                return;
            }

            _compare.View = view;
            ApplyCompareView(view, true);
        }

        private void ApplyCompareView(CompareView view, bool say)
        {
            ComparisonScene layout = _compare.Layout;
            var hidden = new HashSet<int>(layout.HiddenFor(view));
            HashSet<int> before = _compare.HiddenByView;

            // Only what the last side hid is shown again, and only what the user had not hidden already is hidden now: an item the user hid stays hidden.
            _model.SetItemsHidden(before.Where(i => !hidden.Contains(i)).OrderBy(i => i).ToList(), false);
            var hiding = hidden.Where(i => before.Contains(i) || !_model.IsItemHidden(i)).OrderBy(i => i).ToList();
            _model.SetItemsHidden(hiding, true);
            _compare.HiddenByView = new HashSet<int>(hiding);
            SyncTreeChecks();

            if (say)
            {
                SetMessage("showing " + (view == CompareView.Both ? "both sides" : view == CompareView.Left ? "the left side" : "the right side"));
            }
        }

        private void OnCompareLeft(object sender, RoutedEventArgs e) => SetCompareView(CompareView.Left);

        private void OnCompareRight(object sender, RoutedEventArgs e) => SetCompareView(CompareView.Right);

        private void OnCompareBoth(object sender, RoutedEventArgs e) => SetCompareView(CompareView.Both);

        private void OnCompareRerun(object sender, RoutedEventArgs e) => RerunCompare();

        private void OnCompareLeave(object sender, RoutedEventArgs e) => LeaveCompare(true);

        private void OnCompareWatchToggled(object sender, RoutedEventArgs e)
        {
            _settings.CompareWatch = CompareWatchMenu.IsChecked == true;

            if (_compare != null)
            {
                SetUpCompareWatchers(_compare, NewCompareEnvironment(_compare.Setup.TimeoutSeconds));
            }
        }

        /// <summary>
        /// Stops comparing: the watchers go, the table closes, and the scene stays on the screen as it is.
        /// </summary>
        private void LeaveCompare(bool say)
        {
            if (_compare != null)
            {
                _compare.LeftWatch?.Dispose();
                _compare.RightWatch?.Dispose();
            }

            bool was = _compare != null;
            _compare = null;
            _syncingCompare = true;

            try
            {
                _compareRows.Clear();
            }
            finally
            {
                _syncingCompare = false;
            }

            CompareTab.Visibility = Visibility.Collapsed;

            if (ReferenceEquals(BottomTabs.SelectedItem, CompareTab))
            {
                BottomTabs.SelectedItem = OutputTab;
            }

            if (say)
            {
                _pendingLeft = false;
                _pendingRight = false;
            }

            if (say && was)
            {
                SetMessage("comparison closed");
            }
        }

        private void OnCompareRowSelected(object sender, SelectionChangedEventArgs e)
        {
            if (!_syncingCompare && CompareList.SelectedItem is CompareRow row)
            {
                FocusCompareRow(row);
            }
        }

        // A click on the row already selected selects nothing new; it zooms again, which is what is wanted after the view has been moved away.
        private void OnCompareMouseDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject container = ItemsControl.ContainerFromElement(CompareList, e.OriginalSource as DependencyObject);
            _comparePressed = (container as ListViewItem)?.DataContext is CompareRow row && ReferenceEquals(row, CompareList.SelectedItem) ? row : null;
        }

        private void OnCompareMouseUp(object sender, MouseButtonEventArgs e)
        {
            CompareRow row = _comparePressed;
            _comparePressed = null;
            DependencyObject container = ItemsControl.ContainerFromElement(CompareList, e.OriginalSource as DependencyObject);

            if (row != null && ReferenceEquals((container as ListViewItem)?.DataContext, row))
            {
                FocusCompareRow(row);
            }
        }

        /// <summary>
        /// Selects the items of a row and zooms to them. Hidden by the view of the moment, an item is shown first: a row that cannot be seen is no use.
        /// </summary>
        private void FocusCompareRow(CompareRow row)
        {
            if (_compare == null || row.Index < 0 || row.Index >= _compare.Layout.Comparison.Items.Count)
            {
                return;
            }

            IReadOnlyList<int> items = _compare.Layout.ItemsOfRow(row.Index);
            _model.Select(items.Where(i => !_model.IsItemHidden(i)).DefaultIfEmpty(items[0]));
            AfterSelectionChanged(true);
            FitSelection();
            SetMessage(row.ToString());
        }

        #endregion

        #region Watching the DLLs

        private void SetUpCompareWatchers(CompareState state, CompareEnvironment environment)
        {
            state.LeftWatch?.Dispose();
            state.RightWatch?.Dispose();
            state.LeftWatch = null;
            state.RightWatch = null;

            if (!state.Setup.Watch || CompareWatchMenu.IsChecked != true)
            {
                return;
            }

            state.LeftWatch = Watch(state.Setup.Left, environment, true);
            state.RightWatch = Watch(state.Setup.Right, environment, false);
        }

        private DllWatcher Watch(BuildSource source, CompareEnvironment environment, bool left)
        {
            string path = source.Kind == BuildSourceKind.Own ? environment.OwnLibrary : source.Kind == BuildSourceKind.Dll ? source.Dll : null;

            if (path == null || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return new DllWatcher(path, TimeSpan.FromSeconds(1), () => Dispatcher.BeginInvoke(new Action(() => OnSideDllChanged(left))));
            }
            catch (Exception e) when (e is ArgumentException || e is IOException)
            {
                Log("Cannot watch " + path + ": " + e.Message);
                return null;
            }
        }

        private void OnSideDllChanged(bool left)
        {
            if (_compare == null)
            {
                return;
            }

            Log("The " + (left ? "left" : "right") + " DLL changed on disk.");

            if (_compareCts != null)
            {
                _pendingLeft |= left;
                _pendingRight |= !left;
                return;
            }

            _ = RunCompareAsync(_compare, left, !left, true);
        }

        #endregion

        #region Exporting a test

        /// <summary>
        /// Writes a test skeleton for the selected items. With no file given, it goes to the clipboard and to a file the user picks in a dialog;
        /// with a file, to that file alone, which is how the headless commands use it.
        /// </summary>
        private async Task ExportTestAsync(string file)
        {
            if (!HasScene)
            {
                SetMessage("open a scene first");
                return;
            }

            Scene scene = _model.Scene;
            var items = new List<KeyValuePair<Scene, SceneItem>>();

            foreach (int index in _model.Selected.OrderBy(i => i))
            {
                if (_compare != null && index < _compare.Layout.Scene.Items.Count)
                {
                    // The copy that is selected stands for its side: the item as that side's script made it, with the numbers its build gave, at its tolerance.
                    items.Add(_compare.Layout.SourceOf(index));
                }
                else
                {
                    items.Add(new KeyValuePair<Scene, SceneItem>(scene, scene.Items[index]));
                }
            }

            if (items.Count == 0)
            {
                SetMessage("select the items to write a test for");
                Log("Export test: select the items first.");
                return;
            }

            string text = await Task.Run(() => TestExporter.Export(items));

            if (file != null)
            {
                File.WriteAllText(file, text, new UTF8Encoding(false));
                Log("test for " + items.Count.ToString(CultureInfo.InvariantCulture) + " item(s) written to " + file + " (" + text.Length.ToString(CultureInfo.InvariantCulture) + " characters)");
                return;
            }

            try
            {
                Clipboard.SetText(text);
                Log("test for " + items.Count.ToString(CultureInfo.InvariantCulture) + " item(s) copied to the clipboard (" + text.Length.ToString(CultureInfo.InvariantCulture) + " characters)");
            }
            catch (System.Runtime.InteropServices.ExternalException e)
            {
                Log("The clipboard was busy: " + e.Message);
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save the test skeleton",
                Filter = "C# (*.cs)|*.cs",
                FileName = "ViewerExportTests.cs",
                InitialDirectory = _settings.LastFolder != null && Directory.Exists(_settings.LastFolder) ? _settings.LastFolder : null,
            };

            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
                    Log("test written to " + dialog.FileName);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Log("Cannot save " + dialog.FileName + ": " + e.Message);
                }
            }
        }

        #endregion

        #region Headless commands

        /// <summary>
        /// <c>compare own commit:9ee3f57 [repo=path] [timeout=seconds] [watch]</c>: runs the open script against the two builds and shows the comparison;
        /// with <c>watch</c> a side whose DLL changes on disk is run again, which is how the watching is looked at with no hand on the window.
        /// </summary>
        private async Task CompareCommandAsync(string[] words)
        {
            if (words.Length < 3 || _currentPath == null || !SceneLoader.IsScript(_currentPath))
            {
                Log("--do: compare <left> <right> [repo=path] [timeout=seconds] needs a script open; a source is own, dll:<path> or commit:<hash>");
                return;
            }

            string repository = FindRepository(_currentPath) ?? FindRepository(AppContext.BaseDirectory);
            int timeout = _settings.CompareTimeoutSeconds;

            bool watch = false;

            foreach (string word in words.Skip(3))
            {
                if (word.Equals("watch", StringComparison.OrdinalIgnoreCase))
                {
                    watch = true;
                }
                else if (word.StartsWith("repo=", StringComparison.OrdinalIgnoreCase))
                {
                    repository = word.Substring(5);
                }
                else if (word.StartsWith("timeout=", StringComparison.OrdinalIgnoreCase) && int.TryParse(word.Substring(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
                {
                    timeout = seconds;
                }
            }

            if (!BuildSource.TryParse(words[1], repository, out BuildSource left, out string problem) || !BuildSource.TryParse(words[2], repository, out BuildSource right, out problem))
            {
                Log("--do: " + problem);
                return;
            }

            var setup = new CompareSetup { Script = Path.GetFullPath(_currentPath), Left = left, Right = right, Repository = repository, TimeoutSeconds = timeout, Watch = watch };
            await RunCompareAsync(new CompareState { Setup = setup }, true, true, false);
        }

        /// <summary>
        /// <c>comparedialog file.png</c>: draws the window that asks for a comparison, with the settings as they stand, into a picture, so that its layout
        /// can be looked at with no hand on the window. The dialog is not shown.
        /// </summary>
        private void CompareDialogCommand(string file)
        {
            string script = _currentPath != null && SceneLoader.IsScript(_currentPath) ? _currentPath : string.Empty;
            string repository = FindRepository(script) ?? FindRepository(AppContext.BaseDirectory) ?? string.Empty;
            var dialog = new CompareDialog(
                null,
                new CompareSetup { Script = script, Left = BuildSource.Own, Right = BuildSource.FromCommit("9ee3f57", repository), Repository = repository, TimeoutSeconds = _settings.CompareTimeoutSeconds, Watch = true });
            var content = (FrameworkElement)dialog.Content;
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            content.Arrange(new Rect(content.DesiredSize));
            content.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.DesiredSize.Width), (int)Math.Ceiling(content.DesiredSize.Height), 96.0, 96.0, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));

            using (FileStream stream = File.Create(file))
            {
                encoder.Save(stream);
            }

            Log("dialog written to " + file);
        }

        private void CompareRowsCommand()
        {
            if (_compare == null)
            {
                Log("--do: no comparison is open");
                return;
            }

            Log("compare rows (" + _compareRows.Count.ToString(CultureInfo.InvariantCulture) + "):");

            foreach (CompareRow row in _compareRows)
            {
                Log("  " + row);
            }
        }

        private void CompareViewCommand(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "left": SetCompareView(CompareView.Left); break;
                case "right": SetCompareView(CompareView.Right); break;
                default: SetCompareView(CompareView.Both); break;
            }
        }

        private void CompareClickCommand(string number)
        {
            if (_compare != null && int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= _compareRows.Count)
            {
                // The row is chosen as a click chooses it, quietly, and the row is then done as the click does it.
                _syncingCompare = true;

                try
                {
                    CompareList.SelectedIndex = n - 1;
                    CompareList.ScrollIntoView(CompareList.SelectedItem);
                }
                finally
                {
                    _syncingCompare = false;
                }

                FocusCompareRow(_compareRows[n - 1]);
            }
            else
            {
                Log("--do: no comparison row " + number);
            }
        }

        #endregion
    }
}
