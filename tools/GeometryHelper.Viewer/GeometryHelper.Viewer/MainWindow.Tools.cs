using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GeometryHelper.Viewer.Views;

namespace GeometryHelper.Viewer
{
    public partial class MainWindow
    {
        /// <summary>
        /// What the next click on the view does.
        /// </summary>
        private enum PickMode
        {
            None,
            RotationCentre,
            Distance,
            Angle,
            Radius,
            WorkPlaneThreePoints,
            WorkPlaneFace,
            ClipPoint,
            ClipFace,
        }

        /// <summary>
        /// The most curves a scene may hold for the snap under the cursor to be looked for as the mouse moves with no tool in use; a
        /// scene with more still snaps while a tool is.
        /// </summary>
        private const int IdleSnapCurves = 200000;

        private ExactGeometry _geometry;
        private SnapKinds _snaps = SnapKinds.End | SnapKinds.Mid | SnapKinds.Intersection | SnapKinds.Centre;
        private PickMode _pickMode;
        private readonly List<ResolvedPoint> _picked = new List<ResolvedPoint>();
        private readonly List<DimensionDrawing> _dimensions = new List<DimensionDrawing>();
        private Vec3? _rotationCentre;
        private DispatcherTimer _probeTimer;
        private Point _probeAt;
        private ViewPane _probePane;
        private Point? _lastMouse;
        private ViewPane _lastMousePane;
        private ViewPane _dragPane;
        private Point _bandStart;
        private bool _asking;
        private int _askGeneration;

        #region State

        private bool HasScene => _model.Scene != null && _geometry != null;

        private bool Surfaces => Active.Mode != RenderMode.Wireframe;

        /// <summary>
        /// Starts a scene over: the picks, the dimensions and the tool in use belong to the one before. The rotation centre stays where a
        /// script is run again on the same file, since it is run again to be looked at from the same place.
        /// </summary>
        private void ResetTools(ExactGeometry geometry, bool keepRotationCentre)
        {
            _geometry = geometry;
            ClearDiagnostics("nothing run yet");
            _askGeneration++;
            _asking = false;
            _pickMode = PickMode.None;
            _picked.Clear();
            _dimensions.Clear();
            _overlay.SetDrawings(_dimensions);
            _overlay.SetSnap(null, SnapKinds.None);

            if (!keepRotationCentre)
            {
                _rotationCentre = null;
                _overlay.SetRotationCentre(null);
            }

            SnapText.Text = "snap: none";
            UpdateSnapsText();
        }

        /// <summary>
        /// Ends the tool in use and takes the dimensions off the view.
        /// </summary>
        /// <returns>false when there was nothing to end.</returns>
        private bool EndTools()
        {
            if (_pickMode == PickMode.None && _picked.Count == 0 && _dimensions.Count == 0 && !_asking)
            {
                return false;
            }

            // A library answer still on its way is no longer wanted.
            _askGeneration++;
            _asking = false;
            _pickMode = PickMode.None;
            _picked.Clear();
            _dimensions.Clear();
            _overlay.SetDrawings(_dimensions);
            SetMessage(string.Empty);
            return true;
        }

        #endregion

        #region Actions

        private void ExecuteTool(string action)
        {
            switch (action)
            {
                case "SetRotationCentre": Arm(PickMode.RotationCentre); break;

                case "ClearRotationCentre":
                    _rotationCentre = null;
                    _overlay.SetRotationCentre(null);
                    SetMessage("turning about the middle of the view");
                    break;

                case "MeasureDistance":
                    if (HasScene && _model.Selected.Count == 2)
                    {
                        _ = MeasureSelectedAsync();
                    }
                    else
                    {
                        Arm(PickMode.Distance);
                    }

                    break;

                case "MeasureAngle": Arm(PickMode.Angle); break;
                case "MeasureRadius": Arm(PickMode.Radius); break;
                case "SnapEnd": ToggleSnap(SnapKinds.End, "end"); break;
                case "SnapMid": ToggleSnap(SnapKinds.Mid, "mid"); break;
                case "SnapIntersection": ToggleSnap(SnapKinds.Intersection, "intersection"); break;
                case "SnapCentre": ToggleSnap(SnapKinds.Centre, "centre"); break;
                case "SnapNearest": ToggleSnap(SnapKinds.Nearest, "nearest"); break;
            }
        }

        private void Arm(PickMode mode)
        {
            if (!HasScene)
            {
                SetMessage("open a scene first");
                return;
            }

            _picked.Clear();
            _pickMode = mode;
            ShowPending();
            string prompt = Prompt();
            SetMessage(prompt);
            Log(prompt + " (Esc ends it)");
        }

        private string Prompt()
        {
            switch (_pickMode)
            {
                case PickMode.RotationCentre: return "rotation centre: click the point to turn about";
                case PickMode.Distance: return _picked.Count == 0 ? "distance: click the first point" : "distance: click the second point";
                case PickMode.Angle: return _picked.Count == 0 ? "angle: click a point on the first leg" : _picked.Count == 1 ? "angle: click the vertex" : "angle: click a point on the second leg";
                case PickMode.Radius: return "radius: click an arc or a circle";
                case PickMode.WorkPlaneThreePoints: return _picked.Count == 0 ? "work plane: click the origin" : _picked.Count == 1 ? "work plane: click a point along the x axis" : "work plane: click a point in the plane, on the side the y axis points to";
                case PickMode.WorkPlaneFace: return "work plane: click a face";
                case PickMode.ClipPoint: return "clip plane: click a point; the plane goes through it square to the view and cuts away what is nearer the eye";
                case PickMode.ClipFace: return "clip plane: click a face; the plane lies on it and cuts away what is in front of it";
                default: return string.Empty;
            }
        }

        private void ToggleSnap(SnapKinds kind, string name)
        {
            _snaps ^= kind;
            UpdateSnapsText();
            SetMessage("snap to " + name + ": " + ((_snaps & kind) != 0 ? "on" : "off"));

            if (!_snaps.HasFlag(SnapKinds.End) && !_snaps.HasFlag(SnapKinds.Mid) && !_snaps.HasFlag(SnapKinds.Intersection) && !_snaps.HasFlag(SnapKinds.Centre) && !_snaps.HasFlag(SnapKinds.Nearest))
            {
                _overlay.SetSnap(null, SnapKinds.None);
                SnapText.Text = "snap: none";
            }
        }

        private void OnSnapMenu(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string action)
            {
                bool wanted = item.IsChecked;
                SnapKinds kind = action == "SnapEnd" ? SnapKinds.End : action == "SnapMid" ? SnapKinds.Mid : action == "SnapIntersection" ? SnapKinds.Intersection : action == "SnapCentre" ? SnapKinds.Centre : SnapKinds.Nearest;

                if (((_snaps & kind) != 0) != wanted)
                {
                    ExecuteTool(action);
                }
            }
        }

        private void UpdateSnapsText()
        {
            SnapsText.Text = "snaps " + string.Concat(
                (_snaps & SnapKinds.End) != 0 ? "E" : "-",
                (_snaps & SnapKinds.Mid) != 0 ? "M" : "-",
                (_snaps & SnapKinds.Intersection) != 0 ? "I" : "-",
                (_snaps & SnapKinds.Centre) != 0 ? "C" : "-",
                (_snaps & SnapKinds.Nearest) != 0 ? "N" : "-");
            SnapEndMenu.IsChecked = (_snaps & SnapKinds.End) != 0;
            SnapMidMenu.IsChecked = (_snaps & SnapKinds.Mid) != 0;
            SnapIntersectionMenu.IsChecked = (_snaps & SnapKinds.Intersection) != 0;
            SnapCentreMenu.IsChecked = (_snaps & SnapKinds.Centre) != 0;
            SnapNearestMenu.IsChecked = (_snaps & SnapKinds.Nearest) != 0;
        }

        /// <summary>
        /// Turns the camera by angles, about the rotation centre where one is set and about the target of the view where not.
        /// </summary>
        private void TurnCamera(double azimuth, double elevation)
        {
            if (_rotationCentre.HasValue)
            {
                Active.Camera.RotateAbout(_rotationCentre.Value, azimuth, elevation);
            }
            else
            {
                Active.Camera.Rotate(azimuth, elevation);
            }

            UpdateCamera();
        }

        #endregion

        #region Picking points and measuring

        /// <summary>
        /// Takes a click while a tool is in use: a point for the rotation centre, the points of a distance or an angle, the curve of a radius.
        /// </summary>
        private async Task AcquireAsync(Point p)
        {
            // A click while the library is being asked is not a point of the next measurement.
            if (_asking)
            {
                SetMessage("the library is still measuring");
                return;
            }

            try
            {
                ScreenMap map = MakeMap();
                Scene scene = _model.Scene;

                if (_pickMode == PickMode.Radius)
                {
                    SceneCurve curve = PickEngine.PickConic(_geometry, map, p.X, p.Y, _model.IsVisible, out Vec3 at);

                    if (curve == null)
                    {
                        SetMessage("radius: no arc or circle there; click one");
                        return;
                    }

                    Finish(Measurements.Radius(curve, at));
                    return;
                }

                if (_pickMode == PickMode.WorkPlaneFace || _pickMode == PickMode.ClipFace)
                {
                    PickFaceForPlane(map, p);
                    return;
                }

                // A point to turn about, found on nothing, is on the plane through the target square to the view, not on the work plane (the plane z = 0 where none is set).
                ResolvedPoint point = PickEngine.ResolvePoint(_model.Primitives, _geometry, map, p.X, p.Y, _snaps, scene.EqualPoint, _model.IsVisible, Surfaces, _pickMode != PickMode.RotationCentre, _workPlane);

                if (_pickMode == PickMode.ClipPoint)
                {
                    AddClipThrough(point.Point, "through " + point.Point + " (" + point.Source + ")");
                    return;
                }

                if (_pickMode == PickMode.WorkPlaneThreePoints)
                {
                    TakeWorkPlanePoint(point);
                    return;
                }

                if (_pickMode == PickMode.RotationCentre)
                {
                    _rotationCentre = point.Point;
                    _overlay.SetRotationCentre(point.Point);
                    _pickMode = PickMode.None;
                    string where = "rotation centre " + point.Point + " (" + point.Source + ")";
                    SetMessage(where);
                    Log(where + ". Shift+V turns about the middle of the view again.");
                    return;
                }

                _picked.Add(point);

                if (_pickMode == PickMode.Distance && _picked.Count == 2)
                {
                    ResolvedPoint a = _picked[0];
                    ResolvedPoint b = _picked[1];
                    MeasureResult result = Measurements.Distance(a.Point, b.Point, _workPlane);

                    result.AddLine("  picked on " + Describe(a, scene) + " and " + Describe(b, scene));

                    if (a.ItemIndex >= 0 && b.ItemIndex >= 0 && a.ItemIndex != b.ItemIndex)
                    {
                        int first = a.ItemIndex;
                        int second = b.ItemIndex;
                        SetMessage("asking the library...");
                        int generation = ++_askGeneration;
                        _asking = true;
                        LibraryDistance answer = await Task.Run(() => LibraryMeasure.Between(scene, first, second));

                        // Esc or another scene may have come while the library worked: its answer is then nobody's, and what they cleared stays cleared.
                        if (generation != _askGeneration)
                        {
                            return;
                        }

                        _asking = false;
                        result.AddLibrary(answer);
                    }

                    Finish(result);
                }
                else if (_pickMode == PickMode.Angle && _picked.Count == 3)
                {
                    try
                    {
                        Finish(Measurements.Angle(_picked[0].Point, _picked[1].Point, _picked[2].Point));
                    }
                    catch (ArgumentException e)
                    {
                        _picked.Clear();
                        ShowPending();
                        SetMessage("angle: " + e.Message);
                        Log("angle: " + e.Message);
                    }
                }
                else
                {
                    ShowPending();
                    SetMessage(Prompt());
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                _asking = false;
                Log("Error in the measuring: " + e);
            }
        }

        private static string Describe(ResolvedPoint point, Scene scene)
        {
            if (point.ItemIndex < 0 || point.ItemIndex >= scene.Items.Count)
            {
                return point.Source;
            }

            SceneItem item = scene.Items[point.ItemIndex];
            return (item.Name ?? item.Kind) + " (" + point.Source + ")";
        }

        /// <summary>
        /// Measures between two selected items with the library alone.
        /// </summary>
        private async Task MeasureSelectedAsync()
        {
            try
            {
                Scene scene = _model.Scene;
                int[] items = _model.Selected.OrderBy(i => i).ToArray();
                SetMessage("asking the library...");
                int generation = ++_askGeneration;
                _asking = true;
                LibraryDistance library = await Task.Run(() => LibraryMeasure.Between(scene, items[0], items[1]));

                if (generation == _askGeneration)
                {
                    _asking = false;
                    Finish(Measurements.Items(library));
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                _asking = false;
                Log("Error in the measuring: " + e);
            }
        }

        private void Finish(MeasureResult result)
        {
            _picked.Clear();
            _dimensions.Add(result.Drawing);
            _overlay.SetDrawings(_dimensions);

            foreach (string line in result.Lines)
            {
                Log(line);
            }

            SetMessage(result.Kind + " " + (result.Kind == "angle" ? result.Value * 180.0 / Math.PI : result.Value).ToString("G6", CultureInfo.InvariantCulture) + "; " + Prompt());
        }

        private void ShowPending()
        {
            var pending = new DimensionDrawing();

            foreach (ResolvedPoint point in _picked)
            {
                pending.Points.Add(point.Point);
            }

            _overlay.SetDrawings(_dimensions.Concat(new[] { pending }));
        }

        #endregion

        #region Hover and snaps under the mouse

        /// <summary>
        /// Looks for the item and the snap under the mouse soon, once for however many moves come before then, so that the work follows
        /// the mouse without being done for every pixel of it.
        /// </summary>
        private void ProbeLater(ViewPane pane, Point p)
        {
            _probePane = pane;
            _probeAt = p;

            if (_probeTimer == null)
            {
                _probeTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(25) };
                _probeTimer.Tick += (s, e) =>
                {
                    _probeTimer.Stop();
                    Probe(_probePane, _probeAt);
                };
            }

            if (!_probeTimer.IsEnabled)
            {
                _probeTimer.Start();
            }
        }

        private void Probe(ViewPane pane, Point p)
        {
            if (!HasScene)
            {
                return;
            }

            try
            {
                ScreenMap map = MapOf(pane);
                // A scene with very many curves is not searched for its lines at every move of the mouse unless a tool is waiting for a point.
                bool curves = _pickMode != PickMode.None || _geometry.Curves.Count <= IdleSnapCurves;
                PickHit hit = PickEngine.Pick(_model.Primitives, curves ? _geometry : null, map, p.X, p.Y, _model.IsVisible, pane.Mode != RenderMode.Wireframe);
                _model.SetHover(hit == null ? -1 : hit.ItemIndex);

                bool look = _snaps != SnapKinds.None && curves;
                SnapHit snap = look
                    ? SnapEngine.Find(_geometry, map, p.X, p.Y, _snaps, SnapEngine.DefaultRadius, _model.Scene.EqualPoint, _model.IsVisible)
                    : null;

                if (snap == null)
                {
                    _overlay.SetSnap(null, SnapKinds.None);
                    SnapText.Text = "snap: none";
                    return;
                }

                _overlay.SetSnap(snap.Point, snap.Kind);
                string format = "F" + RenderSpace.DecimalsFor(map.PixelSizeAt(snap.Point)).ToString(CultureInfo.InvariantCulture);
                SnapText.Text = "snap: " + snap.Name + " " + snap.Point.X.ToString(format, CultureInfo.InvariantCulture) + ", " + snap.Point.Y.ToString(format, CultureInfo.InvariantCulture) + ", " + snap.Point.Z.ToString(format, CultureInfo.InvariantCulture);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Log("Error finding what is under the cursor: " + e.Message);
            }
        }

        private void ClearProbe()
        {
            _lastMouse = null;
            _probeTimer?.Stop();

            if (_model.Scene != null)
            {
                _model.SetHover(-1);
            }

            _overlay.SetSnap(null, SnapKinds.None);
            SnapText.Text = "snap: none";
        }

        #endregion

        #region Box select

        private void UpdateBand(Point start, Point now)
        {
            // The band is drawn over the view the drag began in, which is the active one: the view is activated when the button goes down.
            Rectangle band = Active.Band;
            bool window = BoxSelect.ModeOf(start.X, now.X) == BoxMode.Window;
            band.Stroke = window ? new SolidColorBrush(Color.FromRgb(0x1F, 0x4F, 0xD8)) : new SolidColorBrush(Color.FromRgb(0x1B, 0x8E, 0x3C));
            band.Fill = window ? new SolidColorBrush(Color.FromArgb(0x28, 0x1F, 0x4F, 0xD8)) : new SolidColorBrush(Color.FromArgb(0x28, 0x1B, 0x8E, 0x3C));
            band.StrokeDashArray = window ? new DoubleCollection() : new DoubleCollection { 5.0, 3.0 };
            Canvas.SetLeft(band, Math.Min(start.X, now.X));
            Canvas.SetTop(band, Math.Min(start.Y, now.Y));
            band.Width = Math.Abs(now.X - start.X);
            band.Height = Math.Abs(now.Y - start.Y);
            band.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Selects what a rectangle dragged over the view holds or touches: a window from left to right, a crossing from right to left.
        /// </summary>
        /// <param name="modifiers">Shift adds the result to the selection, Ctrl toggles it, none replaces the selection.</param>
        private void ApplyBox(Point start, Point end, ModifierKeys modifiers)
        {
            Active.Band.Visibility = Visibility.Collapsed;

            if (!HasScene)
            {
                return;
            }

            BoxMode mode = BoxSelect.ModeOf(start.X, end.X);
            List<int> inside = BoxSelect.Select(_model.Primitives, MakeMap(), start.X, start.Y, end.X, end.Y, mode, _model.IsVisible);
            var selection = new HashSet<int>(_model.Selected);

            if ((modifiers & ModifierKeys.Shift) != 0)
            {
                selection.UnionWith(inside);
            }
            else if ((modifiers & ModifierKeys.Control) != 0)
            {
                foreach (int i in inside)
                {
                    if (!selection.Remove(i))
                    {
                        selection.Add(i);
                    }
                }
            }
            else
            {
                selection = new HashSet<int>(inside);
            }

            _model.Select(selection.OrderBy(i => i));
            AfterSelectionChanged(true);
            SetMessage((mode == BoxMode.Window ? "window" : "crossing") + ": " + inside.Count + " item(s)");
        }

        #endregion

        #region Headless

        /// <summary>
        /// Does what the commands of <c>--do</c> say, in the process, through the code a click and a drag go through: <c>select 0 2</c>,
        /// <c>hover P</c>, <c>box P P [add|toggle]</c>, <c>band P P</c> (the rubber band as if mid-drag), <c>mode distance|angle|radius|centre</c>
        /// followed by <c>click P</c>, <c>keys F8 F9</c> (the actions of the keymap), <c>wait ms</c>, <c>labelpx</c> (says how tall the labels are laid out), <c>diag edges crossings [own]</c> (runs the named checks, or <c>all</c>, or the ticked ones, and waits), <c>diagshow n</c> (clicks the n-th finding), <c>diagclear</c>, <c>compare own commit:9ee3f57 [watch]</c> (runs the open script against two builds and shows the comparison), <c>comparerows</c>, <c>comparedialog file.png</c>, <c>compareview left|right|both</c>, <c>compareclick n</c>, <c>exporttest file.cs</c> (the test skeleton of the selected items, to a file only) and <c>windowshot file.png</c> (the whole window, panes included). A place P is two numbers, device independent pixels of
        /// the 3D view, or one word <c>@x,y,z</c>, a point of the scene taken to where it shows. Commands are separated by semicolons.
        /// </summary>
        private async Task RunCommandsAsync(string commands)
        {
            foreach (string command in commands.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] words = command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (words.Length == 0)
                {
                    continue;
                }

                int at = 1;

                Point Place()
                {
                    if (words[at].StartsWith("@", StringComparison.Ordinal))
                    {
                        double[] c = words[at++].Substring(1).Split(',').Select(w => double.Parse(w, CultureInfo.InvariantCulture)).ToArray();

                        if (!MakeMap().TryProject(new Vec3(c[0], c[1], c.Length > 2 ? c[2] : 0.0), out double px, out double py, out _))
                        {
                            throw new InvalidOperationException("the point " + words[at - 1] + " is not in the picture");
                        }

                        return new Point(px, py);
                    }

                    var p = new Point(double.Parse(words[at], CultureInfo.InvariantCulture), double.Parse(words[at + 1], CultureInfo.InvariantCulture));
                    at += 2;
                    return p;
                }

                switch (words[0].ToLowerInvariant())
                {
                    case "select":
                        _model.Select(words.Skip(1).Select(w => int.Parse(w, CultureInfo.InvariantCulture)));
                        AfterSelectionChanged(true);
                        break;

                    case "hover":
                        Probe(Active, Place());
                        break;

                    case "box":
                    {
                        Point from = Place();
                        Point to = Place();
                        string how = at < words.Length ? words[at] : string.Empty;
                        ApplyBox(from, to, how == "add" ? ModifierKeys.Shift : how == "toggle" ? ModifierKeys.Control : ModifierKeys.None);
                        break;
                    }

                    case "band":
                    {
                        Point from = Place();
                        UpdateBand(from, Place());
                        break;
                    }

                    case "mode":
                        Execute(words[1].ToLowerInvariant() == "distance" ? "MeasureDistance" : words[1].ToLowerInvariant() == "angle" ? "MeasureAngle" : words[1].ToLowerInvariant() == "radius" ? "MeasureRadius" : "SetRotationCentre");
                        break;

                    case "click":
                        if (_pickMode != PickMode.None)
                        {
                            await AcquireAsync(Place());
                        }

                        break;

                    case "labelpx":
                        Log("label heights in pixels: " + string.Join(", ", Active.View.LabelHeights().Select(h => h.ToString("F1", CultureInfo.InvariantCulture))));
                        break;

                    case "wait":
                        await Task.Delay(int.Parse(words[1], CultureInfo.InvariantCulture));
                        break;

                    case "diag":
                    {
                        // diag all | diag edges crossings ... [own]: the checks named, on the selection or on everything, waited for; "own" leaves the library out.
                        DiagnosticChecks named = DiagnosticChecks.None;
                        bool own = false;

                        foreach (string word in words.Skip(1))
                        {
                            if (string.Equals(word, "own", StringComparison.OrdinalIgnoreCase))
                            {
                                own = true;
                            }
                            else if (DiagnosticNames.TryParse(word, out DiagnosticChecks one))
                            {
                                named |= one;
                            }
                            else
                            {
                                Log("--do: no check called '" + word + "'");
                            }
                        }

                        await RunDiagnosticsAsync(named == DiagnosticChecks.None ? (DiagnosticChecks?)null : named, own);
                        break;
                    }

                    case "diagshow":
                        if (_diagReport != null && int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= _diagReport.Findings.Count)
                        {
                            // The row is chosen as a click chooses it, quietly, and the finding is then done as the click does it.
                            _syncingDiag = true;

                            try
                            {
                                DiagList.SelectedIndex = number - 1;
                                DiagList.ScrollIntoView(DiagList.SelectedItem);
                            }
                            finally
                            {
                                _syncingDiag = false;
                            }

                            FocusFinding(_diagReport.Findings[number - 1]);
                        }
                        else
                        {
                            Log("--do: no finding " + words[1]);
                        }

                        break;

                    case "diagclear":
                        ClearDiagnostics();
                        break;

                    case "compare":
                        await CompareCommandAsync(words);
                        break;

                    case "comparedialog":
                        CompareDialogCommand(words.Length > 1 ? words[1] : "comparedialog.png");
                        break;

                    case "comparerows":
                        CompareRowsCommand();
                        break;

                    case "compareview":
                        CompareViewCommand(words.Length > 1 ? words[1] : "both");
                        break;

                    case "compareclick":
                        CompareClickCommand(words.Length > 1 ? words[1] : "0");
                        break;

                    case "exporttest":
                        await ExportTestAsync(words.Length > 1 ? words[1] : null);
                        break;

                    case "windowshot":
                    {
                        // The whole window, panes and all, as the window draws itself; the picture of the view alone cannot show a pane. The graphics card
                        // draws on its own schedule, so the view is given a few frames to show what the commands before this did.
                        for (int frame = 0; frame < 8; frame++)
                        {
                            await Task.Delay(150);
                            InvalidateViews();
                        }

                        UpdateLayout();
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96.0, 96.0, PixelFormats.Pbgra32);
                        bitmap.Render(this);
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(words[1])));

                        using (System.IO.FileStream stream = System.IO.File.Create(words[1]))
                        {
                            encoder.Save(stream);
                        }

                        Log("window written to " + words[1]);
                        break;
                    }

                    case "keys":
                        foreach (string key in words.Skip(1))
                        {
                            Execute(key);
                        }

                        break;

                    default:
                        if (!await ViewCommandAsync(words))
                        {
                            Log("--do: unknown command '" + words[0] + "'");
                        }

                        break;
                }
            }
        }
        #endregion
    }
}
