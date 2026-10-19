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

        private void OnGridToggled(object sender, RoutedEventArgs e) => SetGrid(GridMenu.IsChecked == true);

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

            // 1, 2 and 3 choose the side of a comparison; with none open they are only keys.
            if (_compare == null && (action == "CompareLeft" || action == "CompareRight" || action == "CompareBoth"))
            {
                return;
            }

            // What is typed into a text box, an offset of a clip plane, is the text box's: a letter or a digit, with Shift for a capital, and the keys that move through
            // the text and take from it, alone or with Ctrl (by the word) and Shift (selecting). The keys of the viewer with Ctrl, Ctrl+B, Ctrl+R, stay the viewer's.
            if (Keyboard.FocusedElement is TextBox)
            {
                bool editing = name == "Left" || name == "Right" || name == "Up" || name == "Down" || name == "Home" || name == "End" || name == "Backspace" || name == "Delete" || name == "PageUp" || name == "PageDown";

                if (((modifiers & ~ModifierKeys.Shift) == ModifierKeys.None && (name.Length == 1 || editing)) || (editing && (modifiers & ~(ModifierKeys.Shift | ModifierKeys.Control)) == ModifierKeys.None))
                {
                    return;
                }
            }

            if (navigating && modifiers == ModifierKeys.None && (name == "Left" || name == "Right" || name == "Up" || name == "Down" || name == "PageUp" || name == "PageDown" || name == "F" || name == "V" || name == "K" || name == "L" || name == "W"))
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
                            RerunCurrent();
                        }

                        break;

                    case "Compare": ShowCompareDialog(); break;
                    case "CompareLeft": SetCompareView(CompareView.Left); break;
                    case "CompareRight": SetCompareView(CompareView.Right); break;
                    case "CompareBoth": SetCompareView(CompareView.Both); break;
                    case "ExportTest": _ = ExportTestAsync(null); break;

                    case "ToggleDiagnostics": ToggleDiagnosticsPane(); break;

                    case "Cancel":
                        // A diagnostic run is stopped before anything else; then a tool in use, or the dimensions it drew; a script that is running is stopped before those.
                        if (CancelDiagnostics())
                        {
                            break;
                        }

                        // A comparison under way is stopped next: its child processes and the ones they started are killed.
                        if (_compareCts != null)
                        {
                            _compareCts.Cancel();
                            SetMessage("stopping the comparison...");
                            Log("Stop asked for: the processes the comparison started are being killed.");
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

                        _model.Select(new int[0]);
                        AfterSelectionChanged(true);
                        break;

                    case "ZoomIn": Active.Camera.Zoom(1.25); UpdateCamera(); break;
                    case "ZoomOut": Active.Camera.Zoom(0.8); UpdateCamera(); break;
                    case "PanLeft": Active.Camera.PanBy(-0.1, 0.0, Active.Aspect); UpdateCamera(); break;
                    case "PanRight": Active.Camera.PanBy(0.1, 0.0, Active.Aspect); UpdateCamera(); break;
                    case "PanUp": Active.Camera.PanBy(0.0, 0.1, Active.Aspect); UpdateCamera(); break;
                    case "PanDown": Active.Camera.PanBy(0.0, -0.1, Active.Aspect); UpdateCamera(); break;
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
                        _model.SetItemsHidden(_model.Selected.ToList(), true);
                        SyncTreeChecks();
                        break;

                    case "ShowAll":
                        _model.ShowAll();
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

                    case "ViewLayout1": SetLayout(1); break;
                    case "ViewLayout2": SetLayout(2); break;
                    case "ViewLayout4": SetLayout(4); break;
                    case "LinkViews": ToggleLink(); break;
                    case "SaveView": SaveActiveView(); break;
                    case "Screenshot": _ = ScreenshotWithDialogAsync(); break;

                    case "WorkPlaneThreePoints":
                    case "WorkPlaneFromFace":
                    case "WorkPlaneFromView":
                    case "WorkPlaneGlobal":
                        ExecuteWorkPlane(action);
                        break;

                    case "ClipAddView":
                    case "ClipAddFace":
                    case "ClipBox":
                    case "ClipFlip":
                    case "ClipClear":
                    case "ToggleClipPane":
                        ExecuteClip(action);
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
            Active.Mode = mode;
            Active.UpdateCaption(_layout.Count > 1);
            UpdateModeText();
        }

        private void SetView(StandardView view)
        {
            Active.Camera.SetView(view);
            Active.Saved3D = null;
            UpdateCamera();
            Active.UpdateCaption(_layout.Count > 1);
        }

        private void TogglePlan()
        {
            ViewPane pane = Active;

            if (pane.Saved3D != null)
            {
                Vec3 keepTarget = pane.Camera.Target;
                double keepDistance = pane.Camera.Distance;
                pane.Camera.Azimuth = pane.Saved3D.Azimuth;
                pane.Camera.Elevation = pane.Saved3D.Elevation;
                pane.Camera.Orthographic = pane.Saved3D.Orthographic;
                pane.Camera.Target = keepTarget;
                pane.Camera.Distance = keepDistance;
                pane.Saved3D = null;
            }
            else
            {
                pane.Saved3D = pane.Camera.Clone();
                pane.Camera.SetView(StandardView.Top);
            }

            UpdateCamera();
            pane.UpdateCaption(_layout.Count > 1);
        }

        private void FitAll()
        {
            Active.Camera.Fit(_model.VisibleBounds(), Active.Aspect);
            UpdateCamera();
        }

        /// <summary>
        /// Fits every view that is shown to the scene: what a scene that has just been opened is looked at with. The views that are not shown are fitted
        /// when they are, so a view that was fitted to the scene before is not left looking at it.
        /// </summary>
        private void FitAllViews()
        {
            _layout.ClearFitted();

            foreach (int slot in _layout.Shown)
            {
                ViewPane pane = _panes[slot];
                pane.Camera.Fit(_model.VisibleBounds(), pane.Aspect);
                pane.Fitted = true;
            }

            UpdateAllCameras();
        }

        private void FitSelection()
        {
            if (_model.Selected.Count == 0)
            {
                SetMessage("nothing is selected");
                return;
            }

            Active.Camera.Fit(_model.BoundsOf(_model.Selected), Active.Aspect);
            UpdateCamera();
        }

        #endregion

        #region Camera

        private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewPane pane = PaneOf(sender);

            if (pane != null)
            {
                UpdateCamera(pane, false);
            }
        }

        /// <summary>
        /// Puts the camera of the active view on its viewport, and moves the views that are linked to it, in coordinates measured from the middle of the scene.
        /// </summary>
        private void UpdateCamera() => UpdateCamera(Active, true);

        /// <summary>
        /// Puts the camera of every view that is shown on its viewport.
        /// </summary>
        private void UpdateAllCameras()
        {
            foreach (int slot in _layout.Shown)
            {
                if (_panes[slot] != null)
                {
                    UpdateCamera(_panes[slot], false);
                }
            }
        }

        /// <summary>
        /// Puts the camera of a view on its viewport, in coordinates measured from the render origin of the view.
        /// </summary>
        /// <param name="pane">The view.</param>
        /// <param name="follow">Whether the views linked to it take its target and distance first.</param>
        private void UpdateCamera(ViewPane pane, bool follow)
        {
            if (follow)
            {
                foreach (int slot in _layout.SyncLinked(pane.Number))
                {
                    if (_panes[slot] != null)
                    {
                        UpdateCamera(_panes[slot], false);
                    }
                }
            }

            ViewCamera camera = pane.Camera;
            Bounds3 scene = _model.Primitives?.Bounds ?? default(Bounds3);
            Vec3 origin = pane.View.Origin;
            Vec3 eye = camera.EyePosition(scene) - origin;
            Vec3 forward = camera.Forward;
            Vec3 up = camera.Up;
            camera.ClipPlanes(scene, out double near, out double far);

            if (camera.Orthographic)
            {
                pane.Orthographic.Position = new Point3D(eye.X, eye.Y, eye.Z);
                pane.Orthographic.LookDirection = new Vector3D(forward.X, forward.Y, forward.Z);
                pane.Orthographic.UpDirection = new Vector3D(up.X, up.Y, up.Z);
                pane.Orthographic.Width = 2.0 * camera.Distance * camera.HalfHeightRatio * pane.Aspect;
                pane.Orthographic.NearPlaneDistance = near;
                pane.Orthographic.FarPlaneDistance = far;

                if (!ReferenceEquals(pane.Viewport.Camera, pane.Orthographic))
                {
                    pane.Viewport.Camera = pane.Orthographic;
                }
            }
            else
            {
                pane.Perspective.Position = new Point3D(eye.X, eye.Y, eye.Z);
                pane.Perspective.LookDirection = new Vector3D(forward.X, forward.Y, forward.Z);
                pane.Perspective.UpDirection = new Vector3D(up.X, up.Y, up.Z);
                pane.Perspective.NearPlaneDistance = near;
                pane.Perspective.FarPlaneDistance = far;

                if (!ReferenceEquals(pane.Viewport.Camera, pane.Perspective))
                {
                    pane.Viewport.Camera = pane.Perspective;
                }
            }

            pane.Headlight.Direction = new Vector3D(forward.X, forward.Y, forward.Z);

            // The surfaces are pushed back from their edges by a few steps of the depth buffer, the fewest that keep the edges
            // showing: a step is the depth of the view over 2^24 in an orthographic view, and about d^2 (f - n) / (f n) over 2^24
            // in a perspective one, d being the distance to the target. The aim is 2E-4 mm of push, which no view deeper than
            // about 3 m can reach, the depth buffer being no finer than that itself.
            double unit = camera.Orthographic
                ? (far - near) / 16777216.0
                : camera.Distance * camera.Distance * (far - near) / (far * near) / 16777216.0;
            pane.View.SetDepthBias((int)Math.Max(2.0, Math.Min(400.0, Math.Round(2E-4 / Math.Max(unit, 1E-300)))));

            if (ReferenceEquals(pane, Active))
            {
                UpdateModeText();
            }

            ScheduleRebase(pane);

            // Labels keep a readable size at any zoom, and the snap under a mouse that is still follows the scene moving beneath it.
            pane.View.UpdateLabels(MapOf(pane));

            if (ReferenceEquals(pane, Active))
            {
                ScheduleLens();
            }

            if (_lastMouse.HasValue && _dragButton == null && ReferenceEquals(pane, _lastMousePane))
            {
                ProbeLater(pane, _lastMouse.Value);
            }
        }

        private bool NeedsRebase(ViewPane pane) => _model.Scene != null && RenderSpace.NeedsRebase(pane.View.Origin, pane.Camera, Math.Max(1.0, pane.Height));

        /// <summary>
        /// Asks for the render origin of a view to move to its target once its camera has rested a moment, if it has strayed too far.
        /// </summary>
        private void ScheduleRebase(ViewPane pane)
        {
            if (!NeedsRebase(pane))
            {
                pane.RebaseTimer?.Stop();
                return;
            }

            if (pane.RebaseTimer == null)
            {
                pane.RebaseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                pane.RebaseTimer.Tick += async (s, e) =>
                {
                    pane.RebaseTimer.Stop();
                    await RebaseNowAsync(pane);
                };
            }

            pane.RebaseTimer.Stop();
            pane.RebaseTimer.Start();
        }

        /// <summary>
        /// Moves the render origin of a view to the target of its camera if it has strayed too far, and sends the camera again when the
        /// new geometry is in place; the old geometry is drawn until then.
        /// </summary>
        private async Task RebaseNowAsync(ViewPane pane)
        {
            if (!NeedsRebase(pane))
            {
                return;
            }

            if (pane.View.IsRebasing)
            {
                ScheduleRebase(pane);
                return;
            }

            await pane.View.RebaseAsync(pane.Camera.Target);
        }

        #endregion

        #region Mouse

        private void OnViewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewPane pane = PaneOf(sender);

            if (pane == null || (e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right))
            {
                return;
            }

            Activate(pane);
            ViewHost.Focus();
            Point p = e.GetPosition(pane.Viewport);
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
            _dragPane = pane;
            pane.Viewport.CaptureMouse();
            e.Handled = true;

            // The handle of the selected clip plane is taken hold of at once: it moves the plane along its normal, and is no box select.
            if (e.ChangedButton == MouseButton.Left && _pickMode == PickMode.None && BeginClipDrag(pane, p))
            {
                _dragging = true;
                _dragAction = "ClipDrag";
            }
        }

        private void OnViewMouseMove(object sender, MouseEventArgs e)
        {
            ViewPane pane = PaneOf(sender);

            if (pane == null)
            {
                return;
            }

            // The view under the cursor is the one the keys and the mouse act on.
            if (_dragButton == null)
            {
                Activate(pane);
            }

            Point p = e.GetPosition(pane.Viewport);
            ShowCursorPosition(pane, p);

            if (_dragButton == null)
            {
                _lastMouse = p;
                _lastMousePane = pane;
                ProbeLater(pane, p);
                return;
            }

            if (!ReferenceEquals(pane, _dragPane))
            {
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
                    _model.SetHover(-1);
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
                    pane.Camera.Pan(dx, dy, pane.Height);
                    UpdateCamera();
                }
                else if (_dragAction == "Rotate")
                {
                    if (_rotationCentre.HasValue)
                    {
                        pane.Camera.OrbitAbout(_rotationCentre.Value, dx, dy);
                    }
                    else
                    {
                        pane.Camera.Orbit(dx, dy);
                    }

                    pane.Saved3D = null;
                    UpdateCamera();
                }
                else if (_dragAction == "BoxSelect")
                {
                    UpdateBand(_bandStart, p);
                }
                else if (_dragAction == "ClipDrag")
                {
                    DragClip(pane, p);
                }
            }

            _dragLast = p;
        }

        private void OnViewMouseUp(object sender, MouseButtonEventArgs e)
        {
            ViewPane pane = PaneOf(sender);

            if (pane == null || _dragButton != e.ChangedButton)
            {
                return;
            }

            Point p = e.GetPosition(pane.Viewport);
            bool wasDrag = _dragging;
            MouseButton button = _dragButton.Value;
            ModifierKeys modifiers = _dragModifiers;

            _dragButton = null;
            _dragging = false;
            pane.Viewport.ReleaseMouseCapture();
            e.Handled = true;

            if (!wasDrag)
            {
                Click(p, button, modifiers);
            }
            else if (_dragAction == "BoxSelect")
            {
                ApplyBox(_bandStart, p, modifiers);
            }
            else if (_dragAction == "ClipDrag")
            {
                EndClipDrag();
            }

            _dragAction = null;
            _lastMouse = p;
            _lastMousePane = pane;
            ProbeLater(pane, p);
        }

        private void OnViewMouseLeave(object sender, MouseEventArgs e)
        {
            ViewPane pane = PaneOf(sender);
            CursorText.Text = "x -   y -   z -";

            if (_dragButton == null && ReferenceEquals(pane, _lastMousePane))
            {
                ClearProbe();
            }
        }

        private void OnViewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ViewPane pane = PaneOf(sender);
            string action = _keymap.ActionFor(Gesture.Parse(ModifierText(Keyboard.Modifiers) + "Wheel"));

            if (pane == null || action != "ZoomAtCursor")
            {
                return;
            }

            Activate(pane);
            Point p = e.GetPosition(pane.Viewport);
            double w = Math.Max(1.0, pane.Width);
            double h = Math.Max(1.0, pane.Height);
            pane.Camera.ZoomAt(Math.Pow(1.2, e.Delta / 120.0), 2.0 * p.X / w - 1.0, 1.0 - 2.0 * p.Y / h, pane.Aspect);
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

            PickHit hit = HasScene ? PickEngine.Pick(_model.Primitives, _geometry, MakeMap(), p.X, p.Y, _model.IsVisible, Surfaces) : null;
            int index = hit == null ? -1 : hit.ItemIndex;
            var selection = new HashSet<int>(_model.Selected);

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

            _model.Select(selection.OrderBy(i => i));
            AfterSelectionChanged(true);
        }

        /// <summary>
        /// Writes a number as a readout does, with no minus sign on a nought: a point of a tilted plane that is 1E-15 from an axis reads 0.000 and not -0.000.
        /// </summary>
        private static string Plain(double value, string format)
        {
            string text = value.ToString(format, CultureInfo.InvariantCulture);
            return text.StartsWith("-", StringComparison.Ordinal) && text.Skip(1).All(c => c == '0' || c == '.') ? text.Substring(1) : text;
        }

        /// <summary>
        /// Shows where the cursor is on the work plane: the offsets along its axes, and for a work plane that is not the global one the point of the scene too.
        /// </summary>
        private void ShowCursorPosition(ViewPane pane, Point p)
        {
            double w = Math.Max(1.0, pane.Width);
            double h = Math.Max(1.0, pane.Height);
            pane.Camera.GetRay(2.0 * p.X / w - 1.0, 1.0 - 2.0 * p.Y / h, pane.Aspect, _model.Primitives?.Bounds ?? default(Bounds3), out Vec3 origin, out Vec3 direction);
            double pixel = 2.0 * pane.Camera.Distance * pane.Camera.HalfHeightRatio / h;
            string format = "F" + RenderSpace.DecimalsFor(pixel).ToString(CultureInfo.InvariantCulture);

            if (!_workPlane.IsGlobal)
            {
                if (!_workPlane.TryIntersect(origin, direction, pane.Camera.Orthographic, out Vec3 onWork))
                {
                    CursorText.Text = "x -   y -   z 0 (the view is edge-on to the work plane, or the plane is behind it)";
                    return;
                }

                Vec3 local = _workPlane.ToLocal(onWork);
                CursorText.Text = "x " + Plain(local.X, format) + "   y " + Plain(local.Y, format) + "   z 0 (work plane)   world "
                    + Plain(onWork.X, format) + ", " + Plain(onWork.Y, format) + ", " + Plain(onWork.Z, format);
                return;
            }

            if (Math.Abs(direction.Z) < 1E-9)
            {
                CursorText.Text = "x -   y -   z 0 (the view is edge-on to the plane z = 0)";
                return;
            }

            double t = -origin.Z / direction.Z;

            // An orthographic ray runs both ways; a perspective one only forwards.
            if (t < 0.0 && !pane.Camera.Orthographic)
            {
                CursorText.Text = "x -   y -   z 0 (behind the camera)";
                return;
            }

            Vec3 onPlane = origin + direction * t;
            CursorText.Text = "x " + onPlane.X.ToString(format, CultureInfo.InvariantCulture) + "   y " + onPlane.Y.ToString(format, CultureInfo.InvariantCulture) + "   z 0 (plane z = 0)";
        }

        #endregion

        #region Headless

        private void ParseHeadlessOptions()
        {
            if (_command.Views.HasValue)
            {
                SetLayout(_command.Views.Value);
            }

            // The render mode of a picture is the mode of every view in it.
            if (_command.Mode != null)
            {
                RenderMode mode;

                switch (_command.Mode.ToLowerInvariant())
                {
                    case "wireframe": mode = RenderMode.Wireframe; break;
                    case "shaded": mode = RenderMode.Shaded; break;
                    case "transparent": mode = RenderMode.Transparent; break;
                    default: mode = RenderMode.ShadedEdges; break;
                }

                foreach (int slot in _layout.Shown)
                {
                    _panes[slot].Mode = mode;
                }
            }

            if (_command.View != null)
            {
                switch (_command.View.ToLowerInvariant())
                {
                    case "top": Active.Camera.SetView(StandardView.Top); break;
                    case "front": Active.Camera.SetView(StandardView.Front); break;
                    case "right": Active.Camera.SetView(StandardView.Right); break;
                    default: Active.Camera.SetView(StandardView.Iso); break;
                }
            }
        }

        #endregion
    }
}
