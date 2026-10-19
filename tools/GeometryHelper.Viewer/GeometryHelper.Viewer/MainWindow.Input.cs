using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using GeometryHelper.Viewer.Views;

namespace GeometryHelper.Viewer
{
    public partial class MainWindow
    {
        private const double RotateStep = 15.0 * Math.PI / 180.0;

        private MouseButton? _dragButton;
        private Point _dragStart;
        private Point _dragLast;
        private bool _dragging;
        private ModifierKeys _dragModifiers;
        private string _dragAction;

        #region Menus and keys

        private void LabelMenusWithKeys()
        {
            void Label(ItemCollection items)
            {
                foreach (object entry in items)
                {
                    if (entry is MenuItem item)
                    {
                        if (item.Tag is string action)
                        {
                            Gesture first = _keymap.GesturesOf(action).FirstOrDefault();
                            item.InputGestureText = first == null ? string.Empty : first.ToString();
                        }

                        Label(item.Items);
                    }
                }
            }

            Label(MainMenu.Items);
        }

        private void OnMenuAction(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string action)
            {
                Execute(action);
            }
        }

        private void OnExit(object sender, RoutedEventArgs e) => Close();

        private void OnGridToggled(object sender, RoutedEventArgs e) => _sceneView.Grid = GridMenu.IsChecked == true;

        private void OnPropertiesToggled(object sender, RoutedEventArgs e) => SetPropertiesPane(PropertiesMenu.IsChecked == true);

        private void OnWatchToggled(object sender, RoutedEventArgs e) => SetMessage(WatchMenu.IsChecked == true ? "run again when the file is saved: on" : "run again when the file is saved: off");

        private void OnShowKeys(object sender, RoutedEventArgs e)
        {
            var text = new StringBuilder();

            foreach (KeymapAction action in KeymapActions.All)
            {
                text.Append(string.Join(", ", _keymap.GesturesOf(action.Name).Select(g => g.ToString())).PadRight(28)).Append(action.Name).Append(action.Implemented ? string.Empty : "  (later)").AppendLine();
            }

            text.AppendLine().Append("Change them by copying keymap.json to ").Append(Settings.Folder).Append('.');

            var box = new Window
            {
                Title = "Keys",
                Owner = this,
                Width = 620,
                Height = 700,
                Content = new TextBox { Text = text.ToString(), IsReadOnly = true, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0) },
            };

            box.Show();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            string name = KeyName(key);

            if (name == null)
            {
                return;
            }

            ModifierKeys modifiers = Keyboard.Modifiers;
            string text = ModifierText(modifiers) + name;

            if (!Gesture.TryParse(text, out Gesture gesture, out _))
            {
                return;
            }

            string action = _keymap.ActionFor(gesture);

            if (action == null)
            {
                return;
            }

            // The arrow keys move through the tree and the properties when one of them has the focus.
            bool navigating = Keyboard.FocusedElement is TreeViewItem || Keyboard.FocusedElement is ListViewItem || Keyboard.FocusedElement is TextBox;

            if (navigating && modifiers == ModifierKeys.None && (name == "Left" || name == "Right" || name == "Up" || name == "Down" || name == "PageUp" || name == "PageDown" || name == "F" || name == "V"))
            {
                return;
            }

            e.Handled = true;
            Execute(action);
        }

        private static string ModifierText(ModifierKeys modifiers)
            => ((modifiers & ModifierKeys.Control) != 0 ? "Ctrl+" : string.Empty) + ((modifiers & ModifierKeys.Alt) != 0 ? "Alt+" : string.Empty) + ((modifiers & ModifierKeys.Shift) != 0 ? "Shift+" : string.Empty);

        private static string KeyName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
            {
                return ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture);
            }

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return ((int)(key - Key.NumPad0)).ToString(CultureInfo.InvariantCulture);
            }

            if (key >= Key.A && key <= Key.Z)
            {
                return key.ToString();
            }

            if (key >= Key.F1 && key <= Key.F24)
            {
                return key.ToString();
            }

            switch (key)
            {
                case Key.Escape: return "Esc";
                case Key.Return: return "Enter";
                case Key.Prior: return "PageUp";
                case Key.Next: return "PageDown";
                case Key.Back: return "Backspace";
                case Key.Left:
                case Key.Right:
                case Key.Up:
                case Key.Down:
                case Key.Space:
                case Key.Tab:
                case Key.Delete:
                case Key.Insert:
                case Key.Home:
                case Key.End:
                    return key.ToString();
                default:
                    return null;
            }
        }

        #endregion

        #region Actions

        private void Execute(string action)
        {
            try
            {
                switch (action)
                {
                    case "OpenFile": OpenWithDialog(); break;
                    case "SaveSceneAs": SaveSceneAs(); break;
                    case "RunScript":
                    case "RerunScript":
                        if (_currentPath == null)
                        {
                            OpenWithDialog();
                        }
                        else
                        {
                            _ = OpenFileAsync(_currentPath);
                        }

                        break;

                    case "ToggleDiagnostics": ToggleDiagnosticsPane(); break;

                    case "Cancel":
                        // A diagnostic run is stopped before anything else; then a tool in use, or the dimensions it drew; a script that is running is stopped before those.
                        if (CancelDiagnostics())
                        {
                            break;
                        }

                        if (_running == null && EndTools())
                        {
                            break;
                        }

                        if (_running != null && _running.IsCancellationRequested)
                        {
                            AbandonRun();
                        }
                        else if (_running != null)
                        {
                            _running.Cancel();
                            SetMessage("stopping the script...");
                            Log("Stop asked for. A script stops at its next Show, Note or Scene.Add, or where it tests Cancel; a loop that does none of these runs to its end, and no other script starts until then. Esc again, or F5, lets go of it: the window is free and the thread keeps running until it ends.");
                        }

                        _sceneView.Select(new int[0]);
                        AfterSelectionChanged(true);
                        break;

                    case "ZoomIn": _camera.Zoom(1.25); UpdateCamera(); break;
                    case "ZoomOut": _camera.Zoom(0.8); UpdateCamera(); break;
                    case "PanLeft": _camera.PanBy(-0.1, 0.0, Aspect); UpdateCamera(); break;
                    case "PanRight": _camera.PanBy(0.1, 0.0, Aspect); UpdateCamera(); break;
                    case "PanUp": _camera.PanBy(0.0, 0.1, Aspect); UpdateCamera(); break;
                    case "PanDown": _camera.PanBy(0.0, -0.1, Aspect); UpdateCamera(); break;
                    case "RotateLeft": TurnCamera(RotateStep, 0.0); break;
                    case "RotateRight": TurnCamera(-RotateStep, 0.0); break;
                    case "RotateUp": TurnCamera(0.0, -RotateStep); break;
                    case "RotateDown": TurnCamera(0.0, RotateStep); break;
                    case "TogglePlan": TogglePlan(); break;
                    case "FitAll": FitAll(); break;
                    case "FitSelection": FitSelection(); break;
                    case "ViewIso": SetView(StandardView.Iso); break;
                    case "ViewTop": SetView(StandardView.Top); break;
                    case "ViewFront": SetView(StandardView.Front); break;
                    case "ViewRight": SetView(StandardView.Right); break;
                    case "RenderWireframe": SetMode(RenderMode.Wireframe); break;
                    case "RenderShaded": SetMode(RenderMode.Shaded); break;
                    case "RenderShadedEdges": SetMode(RenderMode.ShadedEdges); break;
                    case "RenderTransparent": SetMode(RenderMode.Transparent); break;

                    case "HideSelected":
                        _sceneView.SetItemsHidden(_sceneView.Selected.ToList(), true);
                        SyncTreeChecks();
                        break;

                    case "ShowAll":
                        _sceneView.ShowAll();
                        SyncTreeChecks();
                        break;

                    case "Properties":
                        SetPropertiesPane(true);
                        ShowProperties();
                        break;

                    case "Select":
                    case "SelectAdd":
                    case "SelectToggle":
                    case "BoxSelect":
                        break;

                    case "SetRotationCentre":
                    case "ClearRotationCentre":
                    case "MeasureDistance":
                    case "MeasureAngle":
                    case "MeasureRadius":
                    case "SnapEnd":
                    case "SnapMid":
                    case "SnapIntersection":
                    case "SnapCentre":
                    case "SnapNearest":
                        ExecuteTool(action);
                        break;

                    default:
                        KeymapAction info = KeymapActions.All.FirstOrDefault(a => a.Name == action);
                        SetMessage(info != null && !info.Implemented ? action + " is not in this version yet" : "nothing to do for " + action);
                        break;
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Log("Error in " + action + ": " + e);
            }
        }

        private void SetMode(RenderMode mode)
        {
            _sceneView.Mode = mode;
            UpdateModeText();
        }

        private void SetView(StandardView view)
        {
            _camera.SetView(view);
            _saved3D = null;
            UpdateCamera();
        }

        private void TogglePlan()
        {
            if (_saved3D != null)
            {
                Vec3 keepTarget = _camera.Target;
                double keepDistance = _camera.Distance;
                _camera.Azimuth = _saved3D.Azimuth;
                _camera.Elevation = _saved3D.Elevation;
                _camera.Orthographic = _saved3D.Orthographic;
                _camera.Target = keepTarget;
                _camera.Distance = keepDistance;
                _saved3D = null;
            }
            else
            {
                _saved3D = _camera.Clone();
                _camera.SetView(StandardView.Top);
            }

            UpdateCamera();
        }

        private void FitAll()
        {
            _camera.Fit(_sceneView.VisibleBounds(), Aspect);
            UpdateCamera();
        }

        private void FitSelection()
        {
            if (_sceneView.Selected.Count == 0)
            {
                SetMessage("nothing is selected");
                return;
            }

            _camera.Fit(_sceneView.BoundsOf(_sceneView.Selected), Aspect);
            UpdateCamera();
        }

        #endregion

        #region Camera

        private double Aspect => Viewport.ActualHeight > 0.0 ? Viewport.ActualWidth / Viewport.ActualHeight : 1.5;

        private void OnViewSizeChanged(object sender, SizeChangedEventArgs e) => UpdateCamera();

        /// <summary>
        /// Puts the camera of the viewer on the 3D view, in coordinates measured from the middle of the scene.
        /// </summary>
        private void UpdateCamera()
        {
            Bounds3 scene = _sceneView.Primitives?.Bounds ?? default(Bounds3);
            Vec3 origin = _sceneView.Origin;
            Vec3 eye = _camera.EyePosition(scene) - origin;
            Vec3 forward = _camera.Forward;
            Vec3 up = _camera.Up;
            _camera.ClipPlanes(scene, out double near, out double far);

            if (_camera.Orthographic)
            {
                _orthographic.Position = new Point3D(eye.X, eye.Y, eye.Z);
                _orthographic.LookDirection = new Vector3D(forward.X, forward.Y, forward.Z);
                _orthographic.UpDirection = new Vector3D(up.X, up.Y, up.Z);
                _orthographic.Width = 2.0 * _camera.Distance * _camera.HalfHeightRatio * Aspect;
                _orthographic.NearPlaneDistance = near;
                _orthographic.FarPlaneDistance = far;

                if (!ReferenceEquals(Viewport.Camera, _orthographic))
                {
                    Viewport.Camera = _orthographic;
                }
            }
            else
            {
                _perspective.Position = new Point3D(eye.X, eye.Y, eye.Z);
                _perspective.LookDirection = new Vector3D(forward.X, forward.Y, forward.Z);
                _perspective.UpDirection = new Vector3D(up.X, up.Y, up.Z);
                _perspective.NearPlaneDistance = near;
                _perspective.FarPlaneDistance = far;

                if (!ReferenceEquals(Viewport.Camera, _perspective))
                {
                    Viewport.Camera = _perspective;
                }
            }

            Headlight.Direction = new Vector3D(forward.X, forward.Y, forward.Z);

            // The surfaces are pushed back from their edges by a few steps of the depth buffer, the fewest that keep the edges
            // showing: a step is the depth of the view over 2^24 in an orthographic view, and about d^2 (f - n) / (f n) over 2^24
            // in a perspective one, d being the distance to the target. The aim is 2E-4 mm of push, which no view deeper than
            // about 3 m can reach, the depth buffer being no finer than that itself.
            double unit = _camera.Orthographic
                ? (far - near) / 16777216.0
                : _camera.Distance * _camera.Distance * (far - near) / (far * near) / 16777216.0;
            _sceneView.SetDepthBias((int)Math.Max(2.0, Math.Min(400.0, Math.Round(2E-4 / Math.Max(unit, 1E-300)))));

            UpdateModeText();
            ScheduleRebase();

            // Labels keep a readable size at any zoom, and the snap under a mouse that is still follows the scene moving beneath it.
            _sceneView.UpdateLabels(MakeMap());
            ScheduleLens();

            if (_lastMouse.HasValue && _dragButton == null)
            {
                ProbeLater(_lastMouse.Value);
            }
        }

        private DispatcherTimer _rebaseTimer;

        private bool NeedsRebase => _sceneView.Scene != null && RenderSpace.NeedsRebase(_sceneView.Origin, _camera, Math.Max(1.0, Viewport.ActualHeight));

        /// <summary>
        /// Asks for the render origin to move to the target once the camera has rested a moment, if it has strayed too far.
        /// </summary>
        private void ScheduleRebase()
        {
            if (!NeedsRebase)
            {
                _rebaseTimer?.Stop();
                return;
            }

            if (_rebaseTimer == null)
            {
                _rebaseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _rebaseTimer.Tick += async (s, e) =>
                {
                    _rebaseTimer.Stop();
                    await RebaseNowAsync();
                };
            }

            _rebaseTimer.Stop();
            _rebaseTimer.Start();
        }

        /// <summary>
        /// Moves the render origin to the target of the camera if it has strayed too far, and sends the camera again when the
        /// new geometry is in place; the old geometry is drawn until then.
        /// </summary>
        private async Task RebaseNowAsync()
        {
            if (!NeedsRebase)
            {
                return;
            }

            if (_sceneView.IsRebasing)
            {
                ScheduleRebase();
                return;
            }

            await _sceneView.RebaseAsync(_camera.Target);
        }

        #endregion

        #region Mouse

        private void OnViewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right)
            {
                return;
            }

            ViewHost.Focus();
            Point p = e.GetPosition(Viewport);
            ModifierKeys modifiers = Keyboard.Modifiers;

            // A second click in quick succession is a second point while a tool waits for one, not a double click.
            if (e.ClickCount == 2 && _pickMode == PickMode.None)
            {
                string action = _keymap.ActionFor(Gesture.Parse(ModifierText(modifiers) + ButtonPrefix(e.ChangedButton) + "DoubleClick"));

                if (action != null)
                {
                    Execute(action);
                }

                e.Handled = true;
                return;
            }

            _dragButton = e.ChangedButton;
            _dragStart = p;
            _dragLast = p;
            _dragging = false;
            _dragModifiers = modifiers;
            Viewport.CaptureMouse();
            e.Handled = true;
        }

        private void OnViewMouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(Viewport);
            ShowCursorPosition(p);

            if (_dragButton == null)
            {
                _lastMouse = p;
                ProbeLater(p);
                return;
            }

            if (!_dragging && (p - _dragStart).Length > 4.0)
            {
                _dragging = true;
                _dragAction = _keymap.ActionFor(Gesture.Parse(ModifierText(_dragModifiers) + ButtonPrefix(_dragButton.Value) + "Drag"));

                // Shift and Ctrl held with the box drag add to the selection and toggle it, and so are not gestures of their own.
                if (_dragAction == null && _dragButton == MouseButton.Left && _dragModifiers != ModifierKeys.None && (_dragModifiers & ~(ModifierKeys.Shift | ModifierKeys.Control)) == 0
                    && _keymap.ActionFor(Gesture.Parse("Drag")) == "BoxSelect")
                {
                    _dragAction = "BoxSelect";
                }

                // A tool waiting for a click has no use for a drag.
                if (_dragAction == "BoxSelect" && _pickMode != PickMode.None)
                {
                    _dragAction = null;
                }
                else if (_dragAction == "BoxSelect")
                {
                    _bandStart = _dragStart;
                    _sceneView.SetHover(-1);
                }
                else if (_dragAction != "Pan" && _dragAction != "Rotate")
                {
                    SetMessage(_dragAction == null ? "no action for this drag" : _dragAction + " is not in this version yet");
                }
            }

            if (_dragging)
            {
                double dx = p.X - _dragLast.X;
                double dy = p.Y - _dragLast.Y;

                if (_dragAction == "Pan")
                {
                    _camera.Pan(dx, dy, Viewport.ActualHeight);
                    UpdateCamera();
                }
                else if (_dragAction == "Rotate")
                {
                    if (_rotationCentre.HasValue)
                    {
                        _camera.OrbitAbout(_rotationCentre.Value, dx, dy);
                    }
                    else
                    {
                        _camera.Orbit(dx, dy);
                    }

                    _saved3D = null;
                    UpdateCamera();
                }
                else if (_dragAction == "BoxSelect")
                {
                    UpdateBand(_bandStart, p);
                }
            }

            _dragLast = p;
        }

        private void OnViewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragButton != e.ChangedButton)
            {
                return;
            }

            Point p = e.GetPosition(Viewport);
            bool wasDrag = _dragging;
            MouseButton button = _dragButton.Value;
            ModifierKeys modifiers = _dragModifiers;

            _dragButton = null;
            _dragging = false;
            Viewport.ReleaseMouseCapture();
            e.Handled = true;

            if (!wasDrag)
            {
                Click(p, button, modifiers);
            }
            else if (_dragAction == "BoxSelect")
            {
                ApplyBox(_bandStart, p, modifiers);
            }

            _dragAction = null;
            _lastMouse = p;
            ProbeLater(p);
        }

        private void OnViewMouseLeave(object sender, MouseEventArgs e)
        {
            CursorText.Text = "x -   y -   z -";

            if (_dragButton == null)
            {
                ClearProbe();
            }
        }

        private void OnViewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            string action = _keymap.ActionFor(Gesture.Parse(ModifierText(Keyboard.Modifiers) + "Wheel"));

            if (action != "ZoomAtCursor")
            {
                return;
            }

            Point p = e.GetPosition(Viewport);
            double w = Math.Max(1.0, Viewport.ActualWidth);
            double h = Math.Max(1.0, Viewport.ActualHeight);
            _camera.ZoomAt(Math.Pow(1.2, e.Delta / 120.0), 2.0 * p.X / w - 1.0, 1.0 - 2.0 * p.Y / h, Aspect);
            UpdateCamera();
            e.Handled = true;
        }

        private static string ButtonPrefix(MouseButton button) => button == MouseButton.Left ? string.Empty : button.ToString();

        private void Click(Point p, MouseButton button, ModifierKeys modifiers)
        {
            // While a tool waits for a point, the left button gives it one and nothing else it does selects.
            if (_pickMode != PickMode.None)
            {
                if (button == MouseButton.Left && HasScene)
                {
                    _ = AcquireAsync(p);
                }

                return;
            }

            string action = _keymap.ActionFor(Gesture.Parse(ModifierText(modifiers) + ButtonPrefix(button) + "Click"));

            if (action != "Select" && action != "SelectAdd" && action != "SelectToggle")
            {
                return;
            }

            PickHit hit = HasScene ? PickEngine.Pick(_sceneView.Primitives, _geometry, MakeMap(), p.X, p.Y, _sceneView.IsVisible, Surfaces) : null;
            int index = hit == null ? -1 : hit.ItemIndex;
            var selection = new HashSet<int>(_sceneView.Selected);

            switch (action)
            {
                case "Select":
                    selection.Clear();

                    if (index >= 0)
                    {
                        selection.Add(index);
                    }

                    break;

                case "SelectAdd":
                    if (index >= 0)
                    {
                        selection.Add(index);
                    }

                    break;

                default:
                    if (index >= 0 && !selection.Remove(index))
                    {
                        selection.Add(index);
                    }

                    break;
            }

            _sceneView.Select(selection.OrderBy(i => i));
            AfterSelectionChanged(true);
        }

        private void ShowCursorPosition(Point p)
        {
            double w = Math.Max(1.0, Viewport.ActualWidth);
            double h = Math.Max(1.0, Viewport.ActualHeight);
            _camera.GetRay(2.0 * p.X / w - 1.0, 1.0 - 2.0 * p.Y / h, Aspect, _sceneView.Primitives?.Bounds ?? default(Bounds3), out Vec3 origin, out Vec3 direction);

            if (Math.Abs(direction.Z) < 1E-9)
            {
                CursorText.Text = "x -   y -   z 0 (the view is edge-on to the plane z = 0)";
                return;
            }

            double t = -origin.Z / direction.Z;

            // An orthographic ray runs both ways; a perspective one only forwards.
            if (t < 0.0 && !_camera.Orthographic)
            {
                CursorText.Text = "x -   y -   z 0 (behind the camera)";
                return;
            }

            double pixel = 2.0 * _camera.Distance * _camera.HalfHeightRatio / h;
            string format = "F" + RenderSpace.DecimalsFor(pixel).ToString(CultureInfo.InvariantCulture);
            Vec3 onPlane = origin + direction * t;
            CursorText.Text = "x " + onPlane.X.ToString(format, CultureInfo.InvariantCulture) + "   y " + onPlane.Y.ToString(format, CultureInfo.InvariantCulture) + "   z 0 (plane z = 0)";
        }
        #endregion

        #region Headless

        private void ParseHeadlessOptions()
        {
            if (_command.Mode != null)
            {
                switch (_command.Mode.ToLowerInvariant())
                {
                    case "wireframe": _sceneView.Mode = RenderMode.Wireframe; break;
                    case "shaded": _sceneView.Mode = RenderMode.Shaded; break;
                    case "transparent": _sceneView.Mode = RenderMode.Transparent; break;
                    default: _sceneView.Mode = RenderMode.ShadedEdges; break;
                }
            }

            if (_command.View != null)
            {
                switch (_command.View.ToLowerInvariant())
                {
                    case "top": _camera.SetView(StandardView.Top); break;
                    case "front": _camera.SetView(StandardView.Front); break;
                    case "right": _camera.SetView(StandardView.Right); break;
                    default: _camera.SetView(StandardView.Iso); break;
                }
            }
        }

        /// <summary>
        /// Writes the view to a picture and ends the program: 0 when the file was read and the picture written, 1 when not.
        /// </summary>
        private async Task TakeScreenshotAsync(bool opened)
        {
            if (!opened)
            {
                Console.Error.WriteLine("FAILED to open " + (_command.File ?? "(no file)") + "; no picture.");
                Application.Current.Shutdown(1);
                return;
            }

            ViewHost.UpdateLayout();
            FitAll();
            await RebaseNowAsync();

            if (_command.Commands != null)
            {
                await RunCommandsAsync(_command.Commands);
                Console.WriteLine(Output.Text);
                Console.WriteLine("status: " + SnapText.Text + " | " + SnapsText.Text + " | " + SelectionText.Text + " | " + MessageText.Text);
            }

            // The graphics card draws the scene on its own schedule; a few frames are given to it before the picture is taken.
            for (int frame = 0; frame < 12; frame++)
            {
                await Task.Delay(150);
                Viewport.InvalidateRender();
            }

            try
            {
                int width = (int)Math.Ceiling(ViewHost.ActualWidth);
                int height = (int)Math.Ceiling(ViewHost.ActualHeight);
                var bitmap = new RenderTargetBitmap(width, height, 96.0, 96.0, PixelFormats.Pbgra32);
                bitmap.Render(ViewHost);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                string directory = Path.GetDirectoryName(Path.GetFullPath(_command.Screenshot));
                Directory.CreateDirectory(directory);

                using (FileStream stream = File.Create(_command.Screenshot))
                {
                    encoder.Save(stream);
                }

                Console.WriteLine("Wrote " + _command.Screenshot + " (" + width + " x " + height + ").");
                Application.Current.Shutdown(0);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Console.Error.WriteLine("FAILED to write " + _command.Screenshot + ": " + e.Message);
                Application.Current.Shutdown(1);
            }
        }

        #endregion
    }
}
