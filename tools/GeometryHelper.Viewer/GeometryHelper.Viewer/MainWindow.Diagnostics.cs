using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GeometryHelper.Viewer
{
    public partial class MainWindow
    {
        /// <summary>
        /// The most findings written to the output pane after a run; the pane lists them all.
        /// </summary>
        private const int MaxLoggedFindings = 400;

        private static readonly KeyValuePair<DiagnosticChecks, string>[] CheckLabels =
        {
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Edges, "Open, non-manifold and same-way edges (bodies, meshes)"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Report, "Body report: Validate(), IsClosed(), volume"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Normals, "Face normals as arrows"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Flatness, "Faces not flat"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Crossings, "Loops crossing themselves"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Winding, "Winding of loops as arrows"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Duplicates, "Duplicate vertices"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Needles, "Needle spikes"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Slivers, "Slivers"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Gap, "Gap between two items, magnified"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Spheres, "Tolerance spheres at the vertices"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Pieces, "Pieces of a layer, coloured one by one"),
            new KeyValuePair<DiagnosticChecks, string>(DiagnosticChecks.Clash, "Clash between bodies: volume shared, touching"),
        };

        private readonly ObservableCollection<FindingRow> _findings = new ObservableCollection<FindingRow>();
        private readonly List<KeyValuePair<DiagnosticChecks, CheckBox>> _ticks = new List<KeyValuePair<DiagnosticChecks, CheckBox>>();
        private CancellationTokenSource _diagCts;
        private int _diagGeneration;
        private DiagnosticReport _diagReport;
        private bool _syncingDiag;
        private DispatcherTimer _lensTimer;
        // The scale of the gap lens in each view, which each view works out from its own zoom; -1 for a view with no lens.
        private readonly double[] _lensScales = { -1.0, -1.0, -1.0, -1.0 };

        /// <summary>
        /// A row of the findings list.
        /// </summary>
        private sealed class FindingRow
        {
            public int Number { get; set; }

            public string Item { get; set; }

            public string Kind { get; set; }

            public string Text { get; set; }

            public Brush Foreground { get; set; }

            internal DiagnosticFinding Finding { get; set; }
        }

        #region The pane

        private void BuildDiagnosticsPane()
        {
            DiagList.ItemsSource = _findings;
            DiagnosticChecks ticked = _settings.DiagnosticsTicked.HasValue ? (DiagnosticChecks)_settings.DiagnosticsTicked.Value & DiagnosticChecks.All : new DiagnosticOptions().Checks;

            foreach (KeyValuePair<DiagnosticChecks, string> label in CheckLabels)
            {
                var box = new CheckBox { Content = label.Value, IsChecked = (ticked & label.Key) != 0, Margin = new Thickness(0, 0, 12, 2), ToolTip = ToolTipOf(label.Key) };
                _ticks.Add(new KeyValuePair<DiagnosticChecks, CheckBox>(label.Key, box));
                DiagTicks.Children.Add(box);
            }

            SetDiagnosticsPane(_settings.ShowDiagnostics);
        }

        private static string ToolTipOf(DiagnosticChecks check)
        {
            switch (check)
            {
                case DiagnosticChecks.Edges: return "Red: used by one face. Magenta: used by three or more. Orange: both faces run it the same way. Where the library reads a body (Validate) it is asked first.";
                case DiagnosticChecks.Report: return "What GeoSolid3.Validate(), IsClosed() and the volume say of each body";
                case DiagnosticChecks.Gap: return "Select exactly two items: the library's DistanceTo and GetShortestLineTo, and the gap drawn larger so it is about 40 pixels";
                case DiagnosticChecks.Pieces: return "The items of each layer, in the order of the scene, coloured one by one with their number and area";
                case DiagnosticChecks.Clash: return "The library's clash check (Clash3.Find) between the selected bodies, or between one selected body and the rest; a finding selects both bodies";
                default: return null;
            }
        }

        private DiagnosticChecks TickedChecks()
            => _ticks.Where(t => t.Value.IsChecked == true).Aggregate(DiagnosticChecks.None, (all, t) => all | t.Key);

        private void SetDiagnosticsPane(bool show)
        {
            DiagnosticsPane.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsColumn.Width = show ? new GridLength(Math.Max(240.0, _settings.DiagnosticsWidth)) : new GridLength(0.0);
            DiagnosticsColumn.MinWidth = show ? 240.0 : 0.0;
            DiagnosticsSplitterColumn.Width = show ? new GridLength(5.0) : new GridLength(0.0);
            DiagnosticsMenu.IsChecked = show;
        }

        private void ToggleDiagnosticsPane() => SetDiagnosticsPane(DiagnosticsPane.Visibility != Visibility.Visible);

        private void OnDiagnosticsPaneMenu(object sender, RoutedEventArgs e) => SetDiagnosticsPane(DiagnosticsMenu.IsChecked == true);

        private void OnDiagRun(object sender, RoutedEventArgs e)
        {
            SetDiagnosticsPane(true);
            _ = RunDiagnosticsAsync(null, false);
        }

        private void OnDiagCancel(object sender, RoutedEventArgs e) => CancelDiagnostics();

        private void OnDiagClear(object sender, RoutedEventArgs e) => ClearDiagnostics();

        private void SaveDiagnosticsSettings()
        {
            _settings.ShowDiagnostics = DiagnosticsPane.Visibility == Visibility.Visible;
            _settings.DiagnosticsWidth = DiagnosticsPane.Visibility == Visibility.Visible ? DiagnosticsColumn.ActualWidth : _settings.DiagnosticsWidth;
            _settings.DiagnosticsTicked = (int)TickedChecks();
        }

        #endregion

        #region Running

        /// <summary>
        /// Runs the checks on the selected items, or on every item when none is selected, off the UI thread. A run still going is stopped
        /// and replaced.
        /// </summary>
        /// <param name="checks">The checks to run; null runs the ticked ones.</param>
        /// <param name="own">Whether to leave the library out and make the viewer's own checks alone.</param>
        private async Task RunDiagnosticsAsync(DiagnosticChecks? checks, bool own)
        {
            if (!HasScene)
            {
                SetMessage("open a scene first");
                return;
            }

            DiagnosticChecks wanted = checks ?? TickedChecks();

            if (wanted == DiagnosticChecks.None)
            {
                DiagStatus.Text = "tick a check first";
                return;
            }

            CancelDiagnostics();
            Scene scene = _model.Scene;
            Primitives primitives = _model.Primitives;
            ExactGeometry geometry = _geometry;
            int[] items = _model.Selected.OrderBy(i => i).ToArray();
            var cts = new CancellationTokenSource();
            _diagCts = cts;
            int generation = ++_diagGeneration;
            DiagRunButton.IsEnabled = false;
            DiagCancelButton.IsEnabled = true;
            DiagStatus.Text = "running on " + (items.Length == 0 ? scene.Items.Count : items.Length).ToString(CultureInfo.InvariantCulture) + " item(s)...";
            var clock = Stopwatch.StartNew();

            try
            {
                var options = new DiagnosticOptions { Checks = wanted, UseLibrary = !own };
                DiagnosticReport report = await Task.Run(() => SceneDiagnostics.Run(scene, primitives, geometry, items, options, cts.Token), cts.Token);

                if (generation == _diagGeneration)
                {
                    ShowReport(report, clock.Elapsed);
                }
            }
            catch (OperationCanceledException)
            {
                if (generation == _diagGeneration)
                {
                    DiagStatus.Text = "cancelled";
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Log("Error in the diagnostics: " + e);

                // A run that was stopped or replaced while it failed has nothing to say to the pane, which belongs to the run that came after.
                if (generation == _diagGeneration)
                {
                    DiagStatus.Text = "failed: " + e.Message;
                }
            }
            finally
            {
                if (ReferenceEquals(_diagCts, cts))
                {
                    _diagCts = null;
                    DiagRunButton.IsEnabled = true;
                    DiagCancelButton.IsEnabled = false;
                }

                cts.Dispose();
            }
        }

        /// <summary>
        /// Stops the run in progress.
        /// </summary>
        /// <returns>false when none was running.</returns>
        private bool CancelDiagnostics()
        {
            if (_diagCts == null)
            {
                return false;
            }

            _diagGeneration++;
            _diagCts.Cancel();
            _diagCts = null;
            DiagRunButton.IsEnabled = true;
            DiagCancelButton.IsEnabled = false;
            DiagStatus.Text = "cancelled";
            return true;
        }

        private void ShowReport(DiagnosticReport report, TimeSpan took)
        {
            _diagReport = report;
            _syncingDiag = true;

            try
            {
                _findings.Clear();
                var fault = new SolidColorBrush(Color.FromRgb(0xB0, 0x10, 0x10));
                var fact = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));

                foreach (DiagnosticFinding f in report.Findings)
                {
                    _findings.Add(new FindingRow
                    {
                        Number = f.Number,
                        Item = f.ItemName ?? string.Empty,
                        Kind = f.Kind.ToString(),
                        Text = f.Message + "   [" + f.Source + "]",
                        Foreground = f.IsFault ? fault : fact,
                        Finding = f,
                    });
                }
            }
            finally
            {
                _syncingDiag = false;
            }

            _overlay.SetMarker(null);
            _overlay.SetDiagnostics(report.Overlay);
            RefreshLens(true);
            string summary = report.Findings.Count.ToString(CultureInfo.InvariantCulture) + " finding(s), " + report.FaultCount.ToString(CultureInfo.InvariantCulture) + " fault(s), "
                + report.Items.Count.ToString(CultureInfo.InvariantCulture) + " item(s), " + took.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";
            DiagStatus.Text = summary;
            Log("diagnostics: " + summary);

            foreach (DiagnosticFinding f in report.Findings.Take(MaxLoggedFindings))
            {
                Log("  " + f);
            }

            if (report.Findings.Count > MaxLoggedFindings)
            {
                Log("  ... and " + (report.Findings.Count - MaxLoggedFindings).ToString(CultureInfo.InvariantCulture) + " more, in the diagnostics pane");
            }
        }

        /// <summary>
        /// Stops a run and takes the findings, what they draw, the marker and the magnifier off the view.
        /// </summary>
        private void ClearDiagnostics(string status = "cleared")
        {
            CancelDiagnostics();
            _diagReport = null;
            _findings.Clear();
            _lensScales[0] = _lensScales[1] = _lensScales[2] = _lensScales[3] = -1.0;
            _overlay.SetDiagnostics(null);
            _overlay.SetLens(null);
            _overlay.SetMarker(null);
            DiagStatus.Text = status;
        }

        #endregion

        #region Clicking a finding

        private void OnDiagSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingDiag || !(DiagList.SelectedItem is FindingRow row))
            {
                return;
            }

            FocusFinding(row.Finding);
        }

        private FindingRow _rowPressed;

        private static FindingRow RowUnder(ListView list, MouseButtonEventArgs e)
        {
            DependencyObject container = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject);
            return (container as ListViewItem)?.DataContext as FindingRow;
        }

        // A click on the finding already selected selects nothing new, so the list says nothing; it zooms to it again, which is what is wanted
        // after the view has been moved away.
        private void OnDiagMouseDown(object sender, MouseButtonEventArgs e) => _rowPressed = RowUnder(DiagList, e) is FindingRow row && ReferenceEquals(row, DiagList.SelectedItem) ? row : null;

        private void OnDiagMouseUp(object sender, MouseButtonEventArgs e)
        {
            FindingRow row = _rowPressed;
            _rowPressed = null;

            if (row != null && ReferenceEquals(RowUnder(DiagList, e), row))
            {
                FocusFinding(row.Finding);
            }
        }

        /// <summary>
        /// Selects the item a finding is on, zooms to the spot, and marks it.
        /// </summary>
        private void FocusFinding(DiagnosticFinding finding)
        {
            if (!HasScene)
            {
                return;
            }

            if (finding.ItemIndex >= 0 && finding.ItemIndex < _model.Scene.Items.Count)
            {
                // A finding about a pair, such as a clash, selects both its items.
                bool pair = finding.OtherItemIndex >= 0 && finding.OtherItemIndex < _model.Scene.Items.Count;
                _model.Select(pair ? new[] { finding.ItemIndex, finding.OtherItemIndex } : new[] { finding.ItemIndex });
                AfterSelectionChanged(true);
            }

            SetMessage(finding.ToString());

            if (finding.Kind == DiagnosticKind.Note)
            {
                return;
            }

            Active.Camera.Fit(finding.Spot, Active.Aspect);
            UpdateCamera();
            _overlay.SetMarker(finding.At);
        }

        #endregion

        #region The gap magnifier

        /// <summary>
        /// Draws the magnifier of the gap in the report at the zoom of the view now; it is drawn again, when the scale it needs has changed,
        /// a moment after the view stops moving.
        /// </summary>
        private void RefreshLens(bool force)
        {
            GapReading gap = _diagReport?.Gap;

            // Each view that is shown has a lens of its own: how many times the gap is drawn larger depends on how far the view is zoomed in on it.
            foreach (GeometryHelper.Viewer.Views.ViewPane pane in _panes.Where(p => p != null))
            {
                int slot = pane.Number;

                if (gap == null || !HasScene || !_layout.Shown.Contains(slot))
                {
                    if (_lensScales[slot] >= 0.0)
                    {
                        _lensScales[slot] = -1.0;
                        pane.Overlay.SetLens(null);
                    }

                    continue;
                }

                Vec3 middle = (gap.From + gap.To) * 0.5;
                double pixel = MapOf(pane).PixelSizeAt(middle);
                double scale = GapMagnifier.ChooseScale(gap.Distance, pixel);

                if (!force && scale == _lensScales[slot])
                {
                    continue;
                }

                _lensScales[slot] = scale;
                pane.Overlay.SetLens(GapMagnifier.Build(_geometry, gap, pixel).Overlay);
            }
        }

        private void ScheduleLens()
        {
            if (_diagReport?.Gap == null)
            {
                return;
            }

            if (_lensTimer == null)
            {
                _lensTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                _lensTimer.Tick += (s, e) =>
                {
                    _lensTimer.Stop();
                    RefreshLens(false);
                };
            }

            _lensTimer.Stop();
            _lensTimer.Start();
        }

        #endregion
    }
}
