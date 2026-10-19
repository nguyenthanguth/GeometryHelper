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
        }

        /// <summary>
        /// The most curves a scene may hold for the snap under the cursor to be looked for as the mouse moves with no tool in use; a
        /// scene with more still snaps while a tool is.
        /// </summary>
        private const int IdleSnapCurves = 200000;

        private ExactGeometry _geometry;
        private OverlayView _overlay;
        private SnapKinds _snaps = SnapKinds.End | SnapKinds.Mid | SnapKinds.Intersection | SnapKinds.Centre;
        private PickMode _pickMode;
        private readonly List<ResolvedPoint> _picked = new List<ResolvedPoint>();
        private readonly List<DimensionDrawing> _dimensions = new List<DimensionDrawing>();
        private Vec3? _rotationCentre;
        private DispatcherTimer _probeTimer;
        private Point _probeAt;
        private Point? _lastMouse;
        private Point _bandStart;
        private bool _asking;
        private int _askGeneration;

        #region State

        private bool HasScene => _sceneView.Scene != null && _geometry != null;

        private ScreenMap MakeMap()
            => new ScreenMap(_camera, _sceneView.Primitives?.Bounds ?? default(Bounds3), Viewport.ActualWidth, Viewport.ActualHeight);

        private bool Surfaces => _sceneView.Mode != RenderMode.Wireframe;

        /// <summary>
        /// Starts a scene over: the picks, the dimensions and the tool in use belong to the one before. The rotation centre stays where a
        /// script is run again on the same file, since it is run again to be looked at from the same place.
        /// </summary>
        private void ResetTools(ExactGeometry geometry, bool keepRotationCentre)
        {
            _geometry = geometry;
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
                    if (HasScene && _sceneView.Selected.Count == 2)
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
                _camera.RotateAbout(_rotationCentre.Value, azimuth, elevation);
            }
            else
            {
                _camera.Rotate(azimuth, elevation);
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
                Scene scene = _sceneView.Scene;

                if (_pickMode == PickMode.Radius)
                {
                    SceneCurve curve = PickEngine.PickConic(_geometry, map, p.X, p.Y, _sceneView.IsVisible, out Vec3 at);

                    if (curve == null)
                    {
                        SetMessage("radius: no arc or circle there; click one");
                        return;
                    }

                    Finish(Measurements.Radius(curve, at));
                    return;
                }

                // A point to turn about, found on nothing, is on the plane through the target square to the view, not on the plane z = 0.
                ResolvedPoint point = PickEngine.ResolvePoint(_sceneView.Primitives, _geometry, map, p.X, p.Y, _snaps, scene.EqualPoint, _sceneView.IsVisible, Surfaces, _pickMode != PickMode.RotationCentre);

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
                    MeasureResult result = Measurements.Distance(a.Point, b.Point);

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
                Scene scene = _sceneView.Scene;
                int[] items = _sceneView.Selected.OrderBy(i => i).ToArray();
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
        private void ProbeLater(Point p)
        {
            _probeAt = p;

            if (_probeTimer == null)
            {
                _probeTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(25) };
                _probeTimer.Tick += (s, e) =>
                {
                    _probeTimer.Stop();
                    Probe(_probeAt);
                };
            }

            if (!_probeTimer.IsEnabled)
            {
                _probeTimer.Start();
            }
        }

        private void Probe(Point p)
        {
            if (!HasScene)
            {
                return;
            }

            try
            {
                ScreenMap map = MakeMap();
                // A scene with very many curves is not searched for its lines at every move of the mouse unless a tool is waiting for a point.
                bool curves = _pickMode != PickMode.None || _geometry.Curves.Count <= IdleSnapCurves;
                PickHit hit = PickEngine.Pick(_sceneView.Primitives, curves ? _geometry : null, map, p.X, p.Y, _sceneView.IsVisible, Surfaces);
                _sceneView.SetHover(hit == null ? -1 : hit.ItemIndex);

                bool look = _snaps != SnapKinds.None && curves;
                SnapHit snap = look
                    ? SnapEngine.Find(_geometry, map, p.X, p.Y, _snaps, SnapEngine.DefaultRadius, _sceneView.Scene.EqualPoint, _sceneView.IsVisible)
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

            if (_sceneView.Scene != null)
            {
                _sceneView.SetHover(-1);
            }

            _overlay.SetSnap(null, SnapKinds.None);
            SnapText.Text = "snap: none";
        }

        #endregion

        #region Box select

        private void UpdateBand(Point start, Point now)
        {
            bool window = BoxSelect.ModeOf(start.X, now.X) == BoxMode.Window;
            Band.Stroke = window ? new SolidColorBrush(Color.FromRgb(0x1F, 0x4F, 0xD8)) : new SolidColorBrush(Color.FromRgb(0x1B, 0x8E, 0x3C));
            Band.Fill = window ? new SolidColorBrush(Color.FromArgb(0x28, 0x1F, 0x4F, 0xD8)) : new SolidColorBrush(Color.FromArgb(0x28, 0x1B, 0x8E, 0x3C));
            Band.StrokeDashArray = window ? new DoubleCollection() : new DoubleCollection { 5.0, 3.0 };
            Canvas.SetLeft(Band, Math.Min(start.X, now.X));
            Canvas.SetTop(Band, Math.Min(start.Y, now.Y));
            Band.Width = Math.Abs(now.X - start.X);
            Band.Height = Math.Abs(now.Y - start.Y);
            Band.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Selects what a rectangle dragged over the view holds or touches: a window from left to right, a crossing from right to left.
        /// </summary>
        /// <param name="modifiers">Shift adds the result to the selection, Ctrl toggles it, none replaces the selection.</param>
        private void ApplyBox(Point start, Point end, ModifierKeys modifiers)
        {
            Band.Visibility = Visibility.Collapsed;

            if (!HasScene)
            {
                return;
            }

            BoxMode mode = BoxSelect.ModeOf(start.X, end.X);
            List<int> inside = BoxSelect.Select(_sceneView.Primitives, MakeMap(), start.X, start.Y, end.X, end.Y, mode, _sceneView.IsVisible);
            var selection = new HashSet<int>(_sceneView.Selected);

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

            _sceneView.Select(selection.OrderBy(i => i));
            AfterSelectionChanged(true);
            SetMessage((mode == BoxMode.Window ? "window" : "crossing") + ": " + inside.Count + " item(s)");
        }

        #endregion

        #region Headless

        /// <summary>
        /// Does what the commands of <c>--do</c> say, in the process, through the code a click and a drag go through: <c>select 0 2</c>,
        /// <c>hover P</c>, <c>box P P [add|toggle]</c>, <c>band P P</c> (the rubber band as if mid-drag), <c>mode distance|angle|radius|centre</c>
        /// followed by <c>click P</c>, <c>keys F8 F9</c> (the actions of the keymap), <c>wait ms</c>, and <c>labelpx</c> (says how tall the labels are laid out). A place P is two numbers, device independent pixels of
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
                        _sceneView.Select(words.Skip(1).Select(w => int.Parse(w, CultureInfo.InvariantCulture)));
                        AfterSelectionChanged(true);
                        break;

                    case "hover":
                        Probe(Place());
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
                        Log("label heights in pixels: " + string.Join(", ", _sceneView.LabelHeights().Select(h => h.ToString("F1", CultureInfo.InvariantCulture))));
                        break;

                    case "wait":
                        await Task.Delay(int.Parse(words[1], CultureInfo.InvariantCulture));
                        break;

                    case "keys":
                        foreach (string key in words.Skip(1))
                        {
                            Execute(key);
                        }

                        break;

                    default:
                        Log("--do: unknown command '" + words[0] + "'");
                        break;
                }
            }
        }
        #endregion
    }
}
