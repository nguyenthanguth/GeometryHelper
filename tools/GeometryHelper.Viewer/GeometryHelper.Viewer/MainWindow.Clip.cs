using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GeometryHelper.Viewer.Views;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A row of the clip planes pane.
    /// </summary>
    public sealed class ClipRow
    {
        /// <summary>
        /// Gets or sets the number of the plane, from 1.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets whether the plane cuts: "on" or "off".
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// Gets or sets the point the plane passes through.
        /// </summary>
        public string Through { get; set; }

        /// <summary>
        /// Gets or sets the normal, which points to the side that stays.
        /// </summary>
        public string Normal { get; set; }

        /// <summary>
        /// Gets or sets how far the plane lies along its normal from where it was made.
        /// </summary>
        public string Offset { get; set; }
    }

    public partial class MainWindow
    {
        /// <summary>
        /// How near the handle of a clip plane the mouse has to go down, in pixels, to take hold of it.
        /// </summary>
        private const double ClipHandleRadius = 14.0;

        private readonly ObservableCollection<ClipRow> _clipRows = new ObservableCollection<ClipRow>();
        private ClipSet _clips = ClipSet.Empty;
        private int _clipSelected = -1;
        private bool _syncingClip;
        private bool _clipRunning;
        private bool _clipAgain;
        private bool _clipRunningPreview;
        private bool _clipCaps = true;
        private CancellationTokenSource _clipWork;
        private WorkPlane _workPlane = WorkPlane.Global;

        // The drag of a handle: the line it moves along is fixed where it was taken hold of, and the plane moves by how far the mouse has gone along it.
        private int _clipDragIndex = -1;
        private Vec3 _clipDragHandle;
        private double _clipDragStartParameter;
        private double _clipDragStartOffset;

        #region Work plane

        private void ExecuteWorkPlane(string action)
        {
            switch (action)
            {
                case "WorkPlaneGlobal":
                    ApplyWorkPlane(WorkPlane.Global);
                    break;

                case "WorkPlaneFromView":
                    ViewPane pane = Active;
                    ApplyWorkPlane(WorkPlane.FromView(pane.Camera.Target, pane.Camera.Right, pane.Camera.Up));
                    break;

                case "WorkPlaneThreePoints":
                    Arm(PickMode.WorkPlaneThreePoints);
                    break;

                case "WorkPlaneFromFace":
                    Arm(PickMode.WorkPlaneFace);
                    break;
            }
        }

        /// <summary>
        /// Makes a plane the work plane: the grid, the readout of the cursor, the point a click on nothing lands on and the offsets of a measured distance all follow it.
        /// </summary>
        private void ApplyWorkPlane(WorkPlane plane)
        {
            _workPlane = plane;

            foreach (ViewPane pane in _panes.Where(p => p != null))
            {
                pane.View.WorkPlane = plane;
            }

            _overlay.SetWorkPlane(plane);

            // A plane of one's own is drawn with its grid, to be seen; going back to the global plane leaves the grid as it is chosen in the menu.
            if (!plane.IsGlobal && GridMenu.IsChecked != true)
            {
                GridMenu.IsChecked = true;
                SetGrid(true);
            }

            WorkPlaneText.Text = "work plane: " + (plane.IsGlobal ? "global" : plane.Description);
            Log("work plane " + plane);
            SetMessage("work plane: " + (plane.IsGlobal ? "global, z = 0" : plane.Description));
        }

        private void TakeWorkPlanePoint(ResolvedPoint point)
        {
            _picked.Add(point);

            if (_picked.Count < 3)
            {
                ShowPending();
                SetMessage(Prompt());
                return;
            }

            try
            {
                WorkPlane plane = WorkPlane.FromThreePoints(_picked[0].Point, _picked[1].Point, _picked[2].Point);
                _pickMode = PickMode.None;
                _picked.Clear();
                ShowPending();
                ApplyWorkPlane(plane);
            }
            catch (ArgumentException e)
            {
                // The three points do not tell a plane: they are asked for again. The message of the exception names the argument too, which is not for a person.
                string reason = System.Text.RegularExpressions.Regex.Replace(e.Message, @" \(Parameter '[^']*'\)$", string.Empty);
                _picked.Clear();
                ShowPending();
                Log("work plane: " + reason);
                SetMessage("work plane: " + reason + " " + Prompt());
            }
        }

        /// <summary>
        /// Takes the face under a click for a work plane or a clip plane: its triangle, with the normal the drawing has, which is turned to face the eye for a work plane
        /// and turned away from it for a clip plane, so that the clip plane cuts away what is between the face and the eye.
        /// </summary>
        private void PickFaceForPlane(ScreenMap map, Point p)
        {
            // Curves are left out of the pick so that a click near the edge of a face is a click on the face.
            PickHit hit = PickEngine.Pick(_model.Primitives, null, map, p.X, p.Y, _model.IsVisible, true);

            if (hit == null || hit.Kind != PickKind.Surface || hit.Normal.Equals(default(Vec3)))
            {
                SetMessage("no face there; click a face (not in wireframe)");
                return;
            }

            Vec3 forward = Active.Camera.Forward;

            if (_pickMode == PickMode.WorkPlaneFace)
            {
                Vec3 normal = Vec3.Dot(hit.Normal, forward) > 0.0 ? -hit.Normal : hit.Normal;
                _pickMode = PickMode.None;
                ApplyWorkPlane(WorkPlane.FromNormal(hit.Point, normal, Active.Camera.Right));
                return;
            }

            Vec3 away = Vec3.Dot(hit.Normal, forward) < 0.0 ? -hit.Normal : hit.Normal;
            AddClip(new ClipPlane(hit.Point, away), "on the face of " + ItemName(hit.ItemIndex) + " at " + hit.Point);
        }

        private string ItemName(int index)
        {
            SceneItem item = _model.Scene.Items[index];
            return item.Name ?? item.Kind;
        }

        #endregion

        #region Clip planes

        private void ExecuteClip(string action)
        {
            switch (action)
            {
                case "ClipAddView":
                    Arm(PickMode.ClipPoint);
                    break;

                case "ClipAddFace":
                    Arm(PickMode.ClipFace);
                    break;

                case "ClipBox":
                    ClipToBox();
                    break;

                case "ClipFlip":
                    FlipClip();
                    break;

                case "ClipClear":
                    SetClips(ClipSet.Empty, -1);
                    SetMessage("no clip plane");
                    break;

                case "ToggleClipPane":
                    BottomTabs.SelectedItem = ClipTab;

                    if (OutputRow.ActualHeight < 200.0)
                    {
                        OutputRow.Height = new GridLength(220.0);
                    }

                    break;
            }
        }

        /// <summary>
        /// Adds a clip plane through a picked point square to the view of the active view; what is nearer the eye is cut away.
        /// </summary>
        private void AddClipThrough(Vec3 point, string how)
        {
            AddClip(new ClipPlane(point, Tidy(Active.Camera.Forward)), how);
        }

        /// <summary>
        /// Takes the rounding off a direction: a component under a trillionth is nought, so that the front view looks along (0, 1, 0) and not along (-6E-17, 1, 0).
        /// </summary>
        private static Vec3 Tidy(Vec3 direction)
        {
            double Clean(double c) => Math.Abs(c) < 1E-12 ? 0.0 : c;
            return new Vec3(Clean(direction.X), Clean(direction.Y), Clean(direction.Z)).Normalized();
        }

        private void AddClip(ClipPlane plane, string how)
        {
            _pickMode = PickMode.None;

            if (_clips.Count >= ClipSet.MaxPlanes)
            {
                SetMessage("there are " + ClipSet.MaxPlanes + " clip planes; remove one first");
                Log("clip: there are " + ClipSet.MaxPlanes + " planes already; remove one before adding another.");
                return;
            }

            SetClips(_clips.Add(plane), _clips.Count);
            Log("clip plane " + _clips.Count + " " + how + ", keeping the side of " + plane.Normal);
            SetMessage("clip plane " + _clips.Count + " added");
        }

        /// <summary>
        /// Clips to a box round the selection, or round everything shown where none is selected, with a margin of a hundredth of the box's diagonal so that
        /// what touches the selection is cut a little outside it.
        /// </summary>
        private void ClipToBox()
        {
            if (_model.Scene == null)
            {
                SetMessage("open a scene first");
                return;
            }

            Bounds3 box = _model.Selected.Count > 0 ? _model.BoundsOf(_model.Selected) : _model.VisibleBounds();

            if (box.IsEmpty)
            {
                SetMessage("nothing to put a box round");
                return;
            }

            double margin = box.Size.Length * 0.01;
            SetClips(ClipSet.FromBox(box, margin), -1);
            Log("clip box round " + (_model.Selected.Count > 0 ? _model.Selected.Count + " selected item(s)" : "everything shown") + ": " + box + ", " + margin.ToString("G6", CultureInfo.InvariantCulture) + " out on every side.");
            SetMessage("clip box");
        }

        private void FlipClip()
        {
            int index = _clipSelected >= 0 ? _clipSelected : _clips.Count - 1;

            if (index < 0)
            {
                SetMessage("no clip plane to flip");
                return;
            }

            SetClips(_clips.Replace(index, _clips.Planes[index].Flipped()), index);
            SetMessage("clip plane " + (index + 1) + " flipped");
        }

        /// <summary>
        /// Puts a set of clip planes in force: the pane, the overlays and the picking see it at once, and the scene is cut on another thread and the views
        /// make their geometry again when that is done. A set that comes while one is being cut takes its place.
        /// </summary>
        private void SetClips(ClipSet clips, int selected)
        {
            _clips = clips;
            _clipSelected = selected >= 0 && selected < clips.Count ? selected : -1;
            _overlay.SetClipPlanes(_clips, _clipSelected);
            RefreshClipPane();
            ApplyClipsAsync();
        }

        /// <summary>
        /// Cuts the scene by the planes in force, on another thread: one cut runs and at most one more waits for it, so that a plane dragged across a large scene
        /// is not cut a hundred times. A cut that a newer set has made out of date is asked to stop, and stops between thousands of triangles; the newest set is
        /// cut when it has, and what is shown is the cut of the set in force once it is done.
        /// </summary>
        private async void ApplyClipsAsync()
        {
            if (_clipRunning)
            {
                _clipAgain = true;

                // While a plane is dragged a cut that is only a look is let finish and is shown, and the newest set is cut after it: a cut that is stopped for every
                // move of the mouse never ends on a scene that takes longer to cut than the mouse takes to move, and the plane would seem to jump when the mouse stopped.
                if (!(_clipRunningPreview && _clipDragIndex >= 0))
                {
                    _clipWork?.Cancel();
                }

                return;
            }

            _clipRunning = true;

            try
            {
                do
                {
                    _clipAgain = false;
                    Primitives primitives = _model.Primitives;

                    if (primitives == null)
                    {
                        break;
                    }

                    ClipSet clips = _clips;
                    bool preview = _clipDragIndex >= 0;
                    bool caps = _clipCaps;
                    _clipRunningPreview = preview;
                    CancellationTokenSource work = _clipWork = new CancellationTokenSource();

                    try
                    {
                        // Under the mouse the scene is cut without its section lines and caps, which are made when the plane is let go.
                        ClipResult result = await Task.Run(() => preview ? ClipResult.BuildPreview(primitives, clips, work.Token) : ClipResult.Build(primitives, clips, caps, work.Token));

                        if (ReferenceEquals(primitives, _model.Primitives) && (preview || (!_clipAgain && ReferenceEquals(clips, _clips))))
                        {
                            _model.SetClip(result);

                            if (result.CapsRefused > 0)
                            {
                                Log("clip: " + result.CapsRefused + " section outline(s) not filled: the library refused to cut them into a cap, or there were too many; the section line is still drawn.");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // A newer set was asked for.
                    }
                }
                while (_clipAgain);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Log("Error cutting the scene by the clip planes: " + e.Message);
            }
            finally
            {
                _clipRunning = false;
            }
        }
        private void RefreshClipPane()
        {
            _syncingClip = true;

            try
            {
                _clipRows.Clear();

                for (int i = 0; i < _clips.Count; i++)
                {
                    ClipPlane plane = _clips.Planes[i];
                    _clipRows.Add(new ClipRow
                    {
                        Number = i + 1,
                        State = plane.Enabled ? "on" : "off",
                        Through = plane.Point.ToString(),
                        Normal = plane.Normal.ToString(),
                        Offset = plane.Offset.ToString("G10", CultureInfo.InvariantCulture),
                    });
                }

                ClipList.SelectedIndex = _clipSelected;
                ClipOffsetBox.Text = _clipSelected >= 0 ? _clips.Planes[_clipSelected].Offset.ToString("G10", CultureInfo.InvariantCulture) : string.Empty;
            }
            finally
            {
                _syncingClip = false;
            }
        }

        private void OnClipSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingClip)
            {
                return;
            }

            _clipSelected = ClipList.SelectedIndex;
            _overlay.SetClipPlanes(_clips, _clipSelected);
            ClipOffsetBox.Text = _clipSelected >= 0 ? _clips.Planes[_clipSelected].Offset.ToString("G10", CultureInfo.InvariantCulture) : string.Empty;
        }

        private void OnClipAddView(object sender, RoutedEventArgs e) => Execute("ClipAddView");

        private void OnClipAddFace(object sender, RoutedEventArgs e) => Execute("ClipAddFace");

        private void OnClipBox(object sender, RoutedEventArgs e) => Execute("ClipBox");

        private void OnClipFlip(object sender, RoutedEventArgs e) => Execute("ClipFlip");

        private void OnClipClearAll(object sender, RoutedEventArgs e) => Execute("ClipClear");

        private void OnClipToggle(object sender, RoutedEventArgs e)
        {
            if (_clipSelected < 0)
            {
                SetMessage("select a clip plane in the list");
                return;
            }

            ClipPlane plane = _clips.Planes[_clipSelected];
            SetClips(_clips.Replace(_clipSelected, plane.WithEnabled(!plane.Enabled)), _clipSelected);
        }

        private void OnClipRemove(object sender, RoutedEventArgs e)
        {
            if (_clipSelected < 0)
            {
                SetMessage("select a clip plane in the list");
                return;
            }

            SetClips(_clips.Remove(_clipSelected), -1);
        }

        private void OnClipOffsetKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                OnClipOffsetSet(sender, e);
                e.Handled = true;
            }
        }

        private void OnClipOffsetSet(object sender, RoutedEventArgs e)
        {
            if (_clipSelected < 0)
            {
                SetMessage("select a clip plane in the list");
                return;
            }

            if (!double.TryParse(ClipOffsetBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double offset) || double.IsNaN(offset) || double.IsInfinity(offset))
            {
                SetMessage("the offset is a number of millimetres, with a point for the decimals");
                return;
            }

            SetClips(_clips.Replace(_clipSelected, _clips.Planes[_clipSelected].WithOffset(offset)), _clipSelected);
            ViewHost.Focus();
        }

        /// <summary>
        /// Takes hold of the handle of the selected clip plane where the mouse went down near it.
        /// </summary>
        private bool BeginClipDrag(ViewPane pane, Point p)
        {
            if (_clipSelected < 0 || _model.Primitives == null || !_clips.Planes[_clipSelected].Enabled)
            {
                return false;
            }

            ClipPlane plane = _clips.Planes[_clipSelected];
            Bounds3 scene = _model.Primitives.Bounds;
            Vec3 handle = plane.HandleFor(scene);

            // The handle lies on the plane, which may be outside the others, so it is looked for in a view that cuts nothing.
            var bare = new ScreenMap(pane.Camera, scene, pane.Width, pane.Height);

            if (!bare.TryProject(handle, out double hx, out double hy, out _) || (hx - p.X) * (hx - p.X) + (hy - p.Y) * (hy - p.Y) > ClipHandleRadius * ClipHandleRadius)
            {
                return false;
            }

            bare.GetRay(p.X, p.Y, out Vec3 origin, out Vec3 direction);
            double start = ClipPlane.ParameterNearestRay(handle, plane.Normal, origin, direction);

            if (double.IsNaN(start))
            {
                SetMessage("the view looks along the plane's normal; turn it to move the plane");
                return false;
            }

            _clipDragIndex = _clipSelected;
            _clipDragHandle = handle;
            _clipDragStartParameter = start;
            _clipDragStartOffset = plane.Offset;
            return true;
        }

        private void DragClip(ViewPane pane, Point p)
        {
            if (_clipDragIndex < 0 || _clipDragIndex >= _clips.Count)
            {
                return;
            }

            ClipPlane plane = _clips.Planes[_clipDragIndex];
            var bare = new ScreenMap(pane.Camera, _model.Primitives?.Bounds ?? default(Bounds3), pane.Width, pane.Height);
            bare.GetRay(p.X, p.Y, out Vec3 origin, out Vec3 direction);
            double now = ClipPlane.ParameterNearestRay(_clipDragHandle, plane.Normal, origin, direction);

            if (double.IsNaN(now))
            {
                return;
            }

            double offset = _clipDragStartOffset + (now - _clipDragStartParameter);
            SetClips(_clips.Replace(_clipDragIndex, plane.WithOffset(offset)), _clipDragIndex);
            SetMessage("clip plane " + (_clipDragIndex + 1) + " offset " + offset.ToString("G6", CultureInfo.InvariantCulture));
        }

        private void EndClipDrag()
        {
            bool was = _clipDragIndex >= 0;
            _clipDragIndex = -1;

            // The plane is let go: the scene is cut in full, with its section lines and caps, for where it is.
            if (was)
            {
                ApplyClipsAsync();
            }
        }

        private void OnClipCapsToggled(object sender, RoutedEventArgs e)
        {
            _clipCaps = ClipCapsMenu.IsChecked == true;
            SetMessage(_clipCaps ? "sections are filled" : "sections are not filled");

            if (_clips.IsActive)
            {
                ApplyClipsAsync();
            }
        }

        /// <summary>
        /// Does the commands of <c>--do</c> that choose and move a clip plane the way the pane does: <c>clipselect n</c>, <c>clipoffset n d</c>, <c>clipoff n</c>, <c>clipon n</c>, the planes numbered from 1.
        /// </summary>
        private void ClipCommand(string[] words)
        {
            if (!int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < 1 || number > _clips.Count)
            {
                Log("--do: there is no clip plane " + words[1]);
                return;
            }

            int index = number - 1;
            ClipPlane plane = _clips.Planes[index];

            switch (words[0].ToLowerInvariant())
            {
                case "clipselect":
                {
                    SetClips(_clips, index);

                    // Where the handle is, in the world and in the active view, so that a drag of it can be asked for by place.
                    Vec3 handle = plane.HandleFor(_model.Primitives?.Bounds ?? default(Bounds3));
                    bool shown = new ScreenMap(Active.Camera, _model.Primitives?.Bounds ?? default(Bounds3), Active.Width, Active.Height).TryProject(handle, out double hx, out double hy, out _);
                    Log("clip plane " + number + ": handle at " + handle + (shown ? ", in the active view at " + hx.ToString("F1", CultureInfo.InvariantCulture) + " " + hy.ToString("F1", CultureInfo.InvariantCulture) : ", out of the active view"));
                    break;
                }

                case "clipdrag":
                {
                    // clipdrag n P P: the mouse goes down at the first place and is dragged to the second, which takes hold of the handle only where the first is near it.
                    SetClips(_clips, index);
                    int at = 2;
                    Point from = ParsePlace(words, ref at);
                    Point to = ParsePlace(words, ref at);

                    if (BeginClipDrag(Active, from))
                    {
                        DragClip(Active, to);
                        EndClipDrag();
                        Log("clip plane " + number + " dragged to offset " + _clips.Planes[index].Offset.ToString("G10", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        Log("--do: the handle of clip plane " + number + " is not under " + from.X.ToString("F1", CultureInfo.InvariantCulture) + " " + from.Y.ToString("F1", CultureInfo.InvariantCulture));
                    }

                    break;
                }

                case "clipoffset":
                    SetClips(_clips.Replace(index, plane.WithOffset(double.Parse(words[2], CultureInfo.InvariantCulture))), index);
                    break;

                case "clipoff":
                    SetClips(_clips.Replace(index, plane.WithEnabled(false)), _clipSelected);
                    break;

                default:
                    SetClips(_clips.Replace(index, plane.WithEnabled(true)), _clipSelected);
                    break;
            }
        }

        #endregion
    }
}
