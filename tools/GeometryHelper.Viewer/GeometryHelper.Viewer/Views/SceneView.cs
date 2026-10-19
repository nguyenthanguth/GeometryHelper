using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.SharpDX.Core;
using HelixToolkit.SharpDX.Core.Model.Scene;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using MeshGeometry3D = HelixToolkit.SharpDX.Core.MeshGeometry3D;

namespace GeometryHelper.Viewer.Views
{
    /// <summary>
    /// The elements of one 3D view for the scene of a <see cref="SceneModel"/>: one set of meshes, lines, points and labels for each item, built
    /// relative to a render origin of the view's own so that the graphics card, which works in floats, is handed small numbers. It owns what
    /// the view shows of the state: its render mode, its grid, the origin; which items are visible and selected it reads from the model, which the
    /// views share.
    /// </summary>
    public sealed class SceneView : IDisposable
    {
        private const double LineThickness = 1.4;
        internal const double LabelFontPixels = 24.0;
        private const double SelectedLineThickness = 3.2;
        private const double HoverLineThickness = 2.6;
        private const double SectionThickness = 2.6;
        private static readonly Color SelectionColor = Color.FromRgb(0xFF, 0x7A, 0x00);
        private static readonly Color HoverColor = Color.FromRgb(0xFF, 0xC4, 0x40);

        private readonly Viewport3DX _viewport;
        private readonly SceneModel _model;
        private readonly List<ItemVisual> _visuals = new List<ItemVisual>();
        private readonly List<Element3D> _extras = new List<Element3D>();
        private Primitives _drawn;
        private RenderMode _mode = RenderMode.ShadedEdges;
        private WorkPlane _workPlane = WorkPlane.Global;
        private Vec3 _wantedOrigin;
        private int _depthBias = 8;
        private readonly AttachGate _gate = new AttachGate();
        private GeometryCache<ItemGeometry[]>.Entry _lease;
        private ClipResult _applied;
        private bool _building;
        private bool _again;
        private bool _grid;
        private ScreenMap _labelMap;

        /// <summary>
        /// Initializes a view over a viewport, which should hold the lights already, and shows the scene of the model in it if there is one.
        /// </summary>
        /// <param name="viewport">The viewport.</param>
        /// <param name="model">What the views share.</param>
        public SceneView(Viewport3DX viewport, SceneModel model)
        {
            _viewport = viewport;
            _model = model;
            Attach();
        }

        /// <summary>
        /// Gets whether the view holds the elements of the scene and listens to the model.
        /// </summary>
        public bool IsAttached => _gate.IsAttached;

        /// <summary>
        /// Starts listening to the model and makes the elements of the scene it shows, relative to the middle of the scene. A view that is not shown is
        /// detached, and is attached again when it is.
        /// </summary>
        public void Attach()
        {
            if (!_gate.Attach())
            {
                return;
            }

            _model.SceneChanged += OnSceneChanged;
            _model.StateChanged += OnStateChanged;
            _model.HoverChanged += OnHoverChanged;
            _model.ClipChanged += OnClipChanged;

            if (_model.Scene != null)
            {
                Build();
            }
        }

        /// <summary>
        /// Stops listening to the model and lets go of every element and the geometry on the graphics card, so that a view that is not shown costs nothing.
        /// What it was making for the card when this was called is dropped when it comes back.
        /// </summary>
        public void Detach()
        {
            if (!_gate.Detach())
            {
                return;
            }

            _model.SceneChanged -= OnSceneChanged;
            _model.StateChanged -= OnStateChanged;
            _model.HoverChanged -= OnHoverChanged;
            _model.ClipChanged -= OnClipChanged;
            ClearElements();
        }

        /// <summary>
        /// Gets the scene shown, or null.
        /// </summary>
        public Scene Scene => _model.Scene;

        /// <summary>
        /// Gets what the scene is drawn with before the clip planes cut it, or null.
        /// </summary>
        public Primitives Primitives => _model.Primitives;

        /// <summary>
        /// Gets the point every coordinate handed to the graphics card is measured from. It starts as the middle of the scene and follows the camera of
        /// this view, which is why each view has its own.
        /// </summary>
        public Vec3 Origin { get; private set; }

        /// <summary>
        /// Gets or sets how the scene is drawn in this view.
        /// </summary>
        public RenderMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                Refresh();
            }
        }

        /// <summary>
        /// Gets or sets whether a grid is drawn on the work plane.
        /// </summary>
        public bool Grid
        {
            get => _grid;
            set
            {
                _grid = value;
                RebuildGrid();
            }
        }

        /// <summary>
        /// Gets or sets the work plane the grid lies on; the global plane z = 0 until it is set.
        /// </summary>
        public WorkPlane WorkPlane
        {
            get => _workPlane;
            set
            {
                _workPlane = value ?? WorkPlane.Global;
                RebuildGrid();
            }
        }

        /// <summary>
        /// Raised, on the thread that moved it, when the render origin has moved; the camera has to be sent again relative to it.
        /// </summary>
        public event EventHandler OriginChanged;

        /// <summary>
        /// Gets whether geometry is being made for the graphics card on another thread, for a move of the origin or a cut of the clip planes.
        /// </summary>
        public bool IsRebasing => _building;

        /// <summary>
        /// Lets go of the elements and stops listening to the model.
        /// </summary>
        public void Dispose() => Detach();

        private void OnSceneChanged(object sender, EventArgs e)
        {
            ClearElements();

            if (_model.Scene != null)
            {
                Build();
            }
        }

        private void OnStateChanged(object sender, EventArgs e) => Refresh();

        private void OnHoverChanged(int before, int after)
        {
            if (before >= 0 && before < _visuals.Count)
            {
                Apply(_visuals[before]);
            }

            if (after >= 0 && after < _visuals.Count)
            {
                Apply(_visuals[after]);
            }
        }

        private async void OnClipChanged(object sender, EventArgs e)
        {
            if (_model.Scene == null)
            {
                return;
            }

            if (_building)
            {
                // The geometry under way is for the planes before; it is made again once it has been put in place.
                _again = true;
                return;
            }

            // A fault in the work reaches the unhandled handler of the application, which says it in the output pane and the log.
            await SwapAsync();
        }

        private void Build()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            ClipResult clip = _model.Clip;
            _drawn = clip.Clipped;
            Origin = _model.Primitives.Bounds.IsEmpty ? default(Vec3) : _model.Primitives.Bounds.Center;
            _wantedOrigin = Origin;
            _labelMap = null;

            // The geometry for this origin and this cut is made by the first view to ask and held by every view that has them: four views open a scene with one build.
            Vec3 origin = Origin;
            Primitives drawn = _drawn;
            TimeSpan before = clock.Elapsed;
            _lease = _model.Geometry.Take(_model.Generation, clip, origin, () => MakeAll(drawn, clip, origin, null, null, default(Vec3)), true);
            _applied = clip;
            ItemGeometry[] made = _lease.Made.Result;
            _model.GeometryMilliseconds.Add((clock.Elapsed - before).TotalMilliseconds);

            for (int i = 0; i < _model.Scene.Items.Count; i++)
            {
                var visual = new ItemVisual { Index = i, BaseColor = ColorOf(_model.Scene.Items[i]) };
                ApplyGeometry(visual, made[i], _drawn.Items[i], Origin);
                _visuals.Add(visual);
            }

            RebuildGrid();
            Refresh();
            _model.ViewBuildMilliseconds.Add(clock.Elapsed.TotalMilliseconds);
        }

        private void ClearElements()
        {
            foreach (ItemVisual visual in _visuals)
            {
                foreach (Element3D element in visual.Elements)
                {
                    _viewport.Items.Remove(element);
                    element.Dispose();
                }
            }

            foreach (Element3D element in _extras)
            {
                _viewport.Items.Remove(element);
                element.Dispose();
            }

            _extras.Clear();
            _visuals.Clear();
            _drawn = null;
            _applied = null;
            _model.Geometry.Release(_lease);
            _lease = null;
        }

        /// <summary>
        /// Makes the geometry of every item of a cut scene relative to a render origin. It holds no screen type, so it can be made on any thread, and the one set it gives
        /// is shared by the views that have that origin and that cut.
        /// </summary>
        /// <param name="drawn">What is drawn, as the cut leaves it.</param>
        /// <param name="clip">The cut.</param>
        /// <param name="origin">The render origin.</param>
        /// <param name="before">The geometry already made for the same scene at the same origin for the cut before, or null; an item that the cut left as it was takes its geometry from here.</param>
        /// <param name="beforeClip">The cut before.</param>
        /// <param name="beforeOrigin">The render origin of the geometry before.</param>
        private static ItemGeometry[] MakeAll(Primitives drawn, ClipResult clip, Vec3 origin, ItemGeometry[] before, ClipResult beforeClip, Vec3 beforeOrigin)
        {
            var made = new ItemGeometry[drawn.Items.Count];
            bool sameOrigin = before != null && beforeClip != null && beforeOrigin.Equals(origin) && before.Length == made.Length;

            for (int i = 0; i < made.Length; i++)
            {
                made[i] = sameOrigin && Unchanged(beforeClip, clip, i) ? before[i] : MakeGeometry(drawn.Items[i], origin, clip.Sections[i], clip.Caps[i]);
            }

            return made;
        }

        /// <summary>
        /// Gets whether the cut of an item is the same in two cuts of the scene: the very same item, section lines and caps, or an item with nothing in it either time, which is
        /// what a plane that has moved from beyond an item to beyond it again leaves. What is the same needs no new geometry for the graphics card and no new elements.
        /// </summary>
        private static bool Unchanged(ClipResult before, ClipResult after, int i)
        {
            ItemPrimitives a = before.Clipped.Items[i];
            ItemPrimitives b = after.Clipped.Items[i];

            if (ReferenceEquals(a, b) && ReferenceEquals(before.Sections[i], after.Sections[i]) && ReferenceEquals(before.Caps[i], after.Caps[i]))
            {
                return true;
            }

            return IsBare(a) && IsBare(b) && before.Sections[i].Count == 0 && after.Sections[i].Count == 0 && before.Caps[i].Count == 0 && after.Caps[i].Count == 0;
        }

        private static bool IsBare(ItemPrimitives p) => p.Points.Count == 0 && p.Polylines.Count == 0 && p.Triangles.Count == 0 && p.Labels.Count == 0;

        /// <summary>
        /// Gives every label the height it is drawn at for a view, so that it can be read at any zoom: a label with no height of its own
        /// is drawn at a size that suits the screen, and one with a height keeps it while that shows as a readable number of pixels.
        /// </summary>
        /// <param name="map">The view.</param>
        public void UpdateLabels(ScreenMap map)
        {
            _labelMap = map;

            foreach (ItemVisual visual in _visuals)
            {
                if (visual.Labels != null && visual.Labels.Geometry is BillboardText3D text && _drawn != null)
                {
                    ScaleLabels(_drawn.Items[visual.Index], text, map);
                }
            }
        }

        /// <summary>
        /// Gets the height in pixels of the first drawn character of each item's labels, as the graphics card has them laid out, for a check
        /// that a change of scale reached what is drawn; an item the card has not drawn yet gives nothing.
        /// </summary>
        internal IReadOnlyList<double> LabelHeights()
        {
            var heights = new List<double>();

            foreach (ItemVisual visual in _visuals)
            {
                if (visual.Labels != null && visual.Labels.Geometry is BillboardText3D text && text.BillboardVertices != null && text.BillboardVertices.Count > 0)
                {
                    heights.Add(text.BillboardVertices.Max(v => v.OffTL.Y - v.OffBR.Y));
                }
            }

            return heights;
        }

        private static void ScaleLabels(ItemPrimitives p, BillboardText3D text, ScreenMap map)
        {
            bool changed = false;

            for (int k = 0; k < p.Labels.Count && k < text.TextInfo.Count; k++)
            {
                DrawLabel label = p.Labels[k];
                float scale = (float)(LabelSize.Pixels(label.Height, map == null ? 0.0 : map.PixelSizeAt(label.At)) / LabelFontPixels);

                if (text.TextInfo[k].Scale != scale)
                {
                    text.TextInfo[k].Scale = scale;
                    changed = true;
                }
            }

            // Scale is a plain property of the label; the card lays the text out again only when the geometry is told it is stale.
            if (changed)
            {
                text.Invalidate();
            }
        }

        private static ItemGeometry MakeGeometry(ItemPrimitives p, Vec3 origin, IReadOnlyList<DrawPolyline> section, IReadOnlyList<DrawTriangle> cap)
        {
            var geometry = new ItemGeometry();
            Vector3 Rel(Vec3 v)
            {
                Vec3F f = RenderSpace.Relative(v, origin);
                return new Vector3(f.X, f.Y, f.Z);
            }

            geometry.Mesh = Surface(p.Triangles, Rel);
            geometry.Cap = Surface(cap, Rel);
            geometry.Lines = Lines(p.Polylines, Rel);
            geometry.Section = Lines(section, Rel);

            if (p.Points.Count > 0)
            {
                var positions = new Vector3Collection();
                var indices = new IntCollection();

                foreach (Vec3 point in p.Points)
                {
                    indices.Add(positions.Count);
                    positions.Add(Rel(point));
                }

                geometry.Points = new PointGeometry3D { Positions = positions, Indices = indices };
            }

            return geometry;
        }

        private static MeshGeometry3D Surface(IReadOnlyList<DrawTriangle> triangles, Func<Vec3, Vector3> rel)
        {
            if (triangles == null || triangles.Count == 0)
            {
                return null;
            }

            var positions = new Vector3Collection(triangles.Count * 3);
            var normals = new Vector3Collection(triangles.Count * 3);
            var indices = new IntCollection(triangles.Count * 3);

            foreach (DrawTriangle t in triangles)
            {
                Vector3 n = new Vector3((float)t.Normal.X, (float)t.Normal.Y, (float)t.Normal.Z);
                int first = positions.Count;
                indices.Add(first);
                indices.Add(first + 1);
                indices.Add(first + 2);
                positions.Add(rel(t.A));
                positions.Add(rel(t.B));
                positions.Add(rel(t.C));
                normals.Add(n);
                normals.Add(n);
                normals.Add(n);
            }

            return new MeshGeometry3D { Positions = positions, Normals = normals, Indices = indices };
        }

        private static LineGeometry3D Lines(IReadOnlyList<DrawPolyline> polylines, Func<Vec3, Vector3> rel)
        {
            if (polylines == null || polylines.Count == 0)
            {
                return null;
            }

            var positions = new Vector3Collection();
            var indices = new IntCollection();

            foreach (DrawPolyline line in polylines)
            {
                int first = positions.Count;

                foreach (Vec3 point in line.Points)
                {
                    positions.Add(rel(point));
                }

                int count = line.Points.Length;
                int segments = line.Closed ? count : count - 1;

                for (int s = 0; s < segments; s++)
                {
                    indices.Add(first + s);
                    indices.Add(first + (s + 1) % count);
                }
            }

            return new LineGeometry3D { Positions = positions, Indices = indices };
        }

        private BillboardText3D MakeLabels(ItemPrimitives p, Vec3 origin, Color color)
        {
            var text = new BillboardText3D();

            foreach (DrawLabel label in p.Labels)
            {
                Vec3F f = RenderSpace.Relative(label.At, origin);
                text.TextInfo.Add(new TextInfo(label.Text, new Vector3(f.X, f.Y, f.Z))
                {
                    Foreground = new Color4(color.R / 255f, color.G / 255f, color.B / 255f, 1f),
                    Background = new Color4(1f, 1f, 1f, 0.6f),
                    Scale = (float)(LabelSize.Pixels(label.Height, _labelMap == null ? 0.0 : _labelMap.PixelSizeAt(label.At)) / LabelFontPixels),
                });
            }

            return text;
        }

        /// <summary>
        /// Puts the geometry made for an item on its elements, making the elements that the item has not had yet: a cut can leave an item that had no
        /// surface a surface, and take its surface away.
        /// </summary>
        private void ApplyGeometry(ItemVisual visual, ItemGeometry g, ItemPrimitives p, Vec3 origin)
        {
            visual.HasMesh = g.Mesh != null;
            visual.HasLines = g.Lines != null;
            visual.HasPoints = g.Points != null;
            visual.HasSection = g.Section != null;
            visual.HasCap = g.Cap != null;
            visual.HasLabels = p.Labels.Count > 0;

            if (g.Cap != null)
            {
                if (visual.Cap == null)
                {
                    visual.Cap = new MeshGeometryModel3D
                    {
                        Geometry = g.Cap,
                        Material = new PhongMaterial { DiffuseColor = C4(CapColor(visual.BaseColor)), AmbientColor = C4(CapColor(visual.BaseColor)), SpecularColor = C4(Color.FromRgb(20, 20, 20)), SpecularShininess = 10f },
                        CullMode = SharpDX.Direct3D11.CullMode.None,
                        DepthBias = _depthBias,
                        SlopeScaledDepthBias = 1f,
                        IsHitTestVisible = false,
                    };
                    Attach(visual, visual.Cap);
                }
                else
                {
                    visual.Cap.Geometry = g.Cap;
                }
            }

            if (g.Mesh != null)
            {
                if (visual.Mesh == null)
                {
                    visual.Mesh = new MeshGeometryModel3D
                    {
                        Geometry = g.Mesh,
                        Material = new PhongMaterial { DiffuseColor = C4(visual.BaseColor), AmbientColor = C4(visual.BaseColor), SpecularColor = C4(Color.FromRgb(40, 40, 40)), SpecularShininess = 20f },
                        CullMode = SharpDX.Direct3D11.CullMode.None,
                        DepthBias = _depthBias,
                        SlopeScaledDepthBias = 1f,
                        IsHitTestVisible = true,
                    };
                    Attach(visual, visual.Mesh);
                }
                else
                {
                    visual.Mesh.Geometry = g.Mesh;
                }
            }

            if (g.Lines != null)
            {
                if (visual.Lines == null)
                {
                    visual.Lines = new LineGeometryModel3D
                    {
                        Geometry = g.Lines,
                        Color = visual.BaseColor,
                        Thickness = LineThickness,
                        HitTestThickness = 6,
                        Smoothness = 0,
                    };
                    Attach(visual, visual.Lines);
                }
                else
                {
                    visual.Lines.Geometry = g.Lines;
                }
            }

            if (g.Section != null)
            {
                if (visual.Section == null)
                {
                    visual.Section = new LineGeometryModel3D
                    {
                        Geometry = g.Section,
                        Color = Darker(visual.BaseColor, false),
                        Thickness = SectionThickness,
                        Smoothness = 0,
                        IsHitTestVisible = false,
                    };
                    Attach(visual, visual.Section);
                }
                else
                {
                    visual.Section.Geometry = g.Section;
                }
            }

            if (g.Points != null)
            {
                if (visual.Points == null)
                {
                    visual.Points = new PointGeometryModel3D
                    {
                        Geometry = g.Points,
                        Color = visual.BaseColor,
                        Figure = PointFigure.Ellipse,
                        Size = new Size(7, 7),
                        HitTestThickness = 8,
                    };
                    Attach(visual, visual.Points);
                }
                else
                {
                    visual.Points.Geometry = g.Points;
                }
            }

            if (p.Labels.Count > 0)
            {
                BillboardText3D text = MakeLabels(p, origin, visual.BaseColor);

                if (visual.Labels == null)
                {
                    visual.Labels = new BillboardTextModel3D { Geometry = text, FixedSize = true };
                    Attach(visual, visual.Labels);
                }
                else
                {
                    visual.Labels.Geometry = text;
                }
            }
        }


        private void Attach(ItemVisual visual, Element3D element)
        {
            visual.Elements.Add(element);
            _viewport.Items.Add(element);
        }

        /// <summary>
        /// Moves the render origin, which every coordinate handed to the graphics card is measured from, to a new place. The
        /// geometry is made again on another thread and the old geometry stays on the screen until the new one is ready; then
        /// the two are swapped, with the origin, in one step.
        /// </summary>
        /// <param name="newOrigin">The new origin, in world coordinates.</param>
        /// <returns>true when the origin moved; false when the scene changed meanwhile or a move was under way.</returns>
        public async Task<bool> RebaseAsync(Vec3 newOrigin)
        {
            if (_model.Scene == null || _building)
            {
                return false;
            }

            _wantedOrigin = newOrigin;
            return await SwapAsync();
        }

        /// <summary>
        /// Makes the geometry again, on another thread, from what the clip planes leave and from the origin that is wanted, and swaps it in for the geometry on
        /// the screen in one step. A cut that comes while this goes on is made once it has been put in place.
        /// </summary>
        private async Task<bool> SwapAsync()
        {
            _building = true;
            bool swapped = false;

            try
            {
                do
                {
                    _again = false;
                    int ticket = _gate.Ticket;
                    int generation = _model.Generation;
                    ClipResult clip = _model.Clip;
                    Vec3 origin = _wantedOrigin;
                    Primitives drawn = clip.Clipped;

                    // Another view with this origin and this cut is making the same geometry, or has made it: it is waited for, and none is made again. An item that this cut leaves
                    // as the one before did keeps the geometry it has.
                    ItemGeometry[] before = _lease != null && _lease.Made.IsCompletedSuccessfully ? _lease.Made.Result : null;
                    ClipResult beforeClip = _applied;
                    Vec3 beforeOrigin = Origin;
                    GeometryCache<ItemGeometry[]>.Entry entry = _model.Geometry.Take(generation, clip, origin, () => MakeAll(drawn, clip, origin, before, beforeClip, beforeOrigin), false);
                    ItemGeometry[] made;

                    try
                    {
                        made = await entry.Made;
                    }
                    catch
                    {
                        _model.Geometry.Release(entry);
                        throw;
                    }

                    if (generation != _model.Generation || !_gate.IsAttached)
                    {
                        // Another scene was shown meanwhile, and was built whole, or the view was let go of. A cut that came meanwhile is of the new scene, which this
                        // does not make again: it is made now, from the scene as it is.
                        _model.Geometry.Release(entry);

                        if (_again && _gate.IsAttached && _model.Scene != null)
                        {
                            continue;
                        }

                        return false;
                    }

                    if (!_gate.IsCurrent(ticket))
                    {
                        // The view let go of its elements and took them again while this was made: these are not the elements it was made for, and the elements
                        // that were made on taking them are of the scene as it is. A cut that came since is made.
                        _model.Geometry.Release(entry);

                        if (_again)
                        {
                            continue;
                        }

                        return false;
                    }

                    bool moved = !origin.Equals(Origin);
                    ClipResult applied = _applied;
                    _drawn = drawn;

                    for (int i = 0; i < _visuals.Count; i++)
                    {
                        // An item the cut leaves as it was has its elements as they are, which for a plane moved through a scene of many items is nearly all of them.
                        if (moved || applied == null || !Unchanged(applied, clip, i))
                        {
                            ApplyGeometry(_visuals[i], made[i], drawn.Items[i], origin);
                        }
                    }

                    _applied = clip;
                    _model.Geometry.Release(_lease);
                    _lease = entry;
                    Origin = origin;
                    RebuildGrid();
                    Refresh();

                    if (moved)
                    {
                        OriginChanged?.Invoke(this, EventArgs.Empty);
                    }

                    swapped = true;
                }
                while (_again);

                return swapped;
            }
            finally
            {
                _building = false;
            }
        }

        /// <summary>
        /// Sets how far the surfaces are pushed back from their edges, in units of the smallest step of the depth buffer, so
        /// that the edges drawn on them show.
        /// </summary>
        /// <param name="units">The push, from 0.</param>
        public void SetDepthBias(int units)
        {
            if (units == _depthBias)
            {
                return;
            }

            _depthBias = units;

            foreach (ItemVisual visual in _visuals)
            {
                if (visual.Mesh != null)
                {
                    visual.Mesh.DepthBias = units;
                }

                if (visual.Cap != null)
                {
                    visual.Cap.DepthBias = units;
                }
            }
        }

        private static Color4 C4(Color color) => new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        private Color ColorOf(SceneItem item)
        {
            string text = item.Color ?? _model.Scene.FindLayer(item.Layer)?.Color;
            return SceneColor.TryParse(text, out byte r, out byte g, out byte b) ? Color.FromRgb(r, g, b) : Colors.Gray;
        }

        /// <summary>
        /// Applies render mode, visibility and selection to the elements.
        /// </summary>
        private void Refresh()
        {
            if (!_gate.IsAttached || _model.Scene == null)
            {
                return;
            }

            foreach (ItemVisual visual in _visuals)
            {
                Apply(visual);
            }
        }

        private void Apply(ItemVisual visual)
        {
            bool shown = _model.IsVisible(visual.Index);
            bool selected = _model.Selected.Contains(visual.Index);
            bool hovered = !selected && visual.Index == _model.Hover;
            Color color = selected ? SelectionColor : hovered ? Mix(visual.BaseColor, HoverColor, 0.55) : visual.BaseColor;
            bool hasSurface = visual.HasMesh;

            if (visual.Mesh != null)
            {
                bool see = _mode != RenderMode.Wireframe;
                visual.Mesh.Visibility = shown && see && visual.HasMesh ? Visibility.Visible : Visibility.Collapsed;
                visual.Mesh.IsHitTestVisible = shown && see && visual.HasMesh;

                if (visual.Mesh.SceneNode is MaterialGeometryNode node)
                {
                    // A see-through surface writes no depth, so the edges behind it are still drawn.
                    node.IsTransparent = _mode == RenderMode.Transparent;
                }

                var material = (PhongMaterial)visual.Mesh.Material;
                material.DiffuseColor = C4(_mode == RenderMode.Transparent
                    ? Color.FromArgb(90, color.R, color.G, color.B)
                    : color);
            }

            if (visual.Cap != null)
            {
                bool see = _mode != RenderMode.Wireframe;
                visual.Cap.Visibility = shown && see && visual.HasCap ? Visibility.Visible : Visibility.Collapsed;

                if (visual.Cap.SceneNode is MaterialGeometryNode node)
                {
                    node.IsTransparent = _mode == RenderMode.Transparent;
                }

                Color fill = selected ? Mix(CapColor(visual.BaseColor), SelectionColor, 0.5) : CapColor(visual.BaseColor);
                ((PhongMaterial)visual.Cap.Material).DiffuseColor = C4(_mode == RenderMode.Transparent ? Color.FromArgb(120, fill.R, fill.G, fill.B) : fill);
            }

            if (visual.Lines != null)
            {
                bool see = _mode != RenderMode.Shaded || !hasSurface;
                visual.Lines.Visibility = visual.HasLines && shown && (see || selected || hovered) ? Visibility.Visible : Visibility.Collapsed;
                visual.Lines.Color = _mode == RenderMode.Wireframe || !hasSurface ? color : Darker(color, selected || hovered);
                visual.Lines.Thickness = selected ? SelectedLineThickness : hovered ? HoverLineThickness : LineThickness;
            }

            if (visual.Section != null)
            {
                visual.Section.Visibility = visual.HasSection && shown ? Visibility.Visible : Visibility.Collapsed;
                visual.Section.Color = selected ? SelectionColor : Darker(visual.BaseColor, false);
            }

            if (visual.Points != null)
            {
                visual.Points.Visibility = visual.HasPoints && shown ? Visibility.Visible : Visibility.Collapsed;
                visual.Points.Color = color;
            }

            if (visual.Labels != null)
            {
                visual.Labels.Visibility = visual.HasLabels && shown ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Gets a colour part of the way from one to another: a hovered item is its own colour warmed, so it can still be told apart.
        /// </summary>
        private static Color Mix(Color from, Color to, double share)
            => Color.FromRgb((byte)(from.R + (to.R - from.R) * share), (byte)(from.G + (to.G - from.G) * share), (byte)(from.B + (to.B - from.B) * share));

        /// <summary>
        /// Gets the colour a section is filled with: the item's own, a fifth of the way to white, so that the cut face stands out from the surfaces round it.
        /// </summary>
        private static Color CapColor(Color color) => Mix(color, Colors.White, 0.2);

        private static Color Darker(Color color, bool selected)
            => selected ? color : Color.FromRgb((byte)(color.R * 0.55), (byte)(color.G * 0.55), (byte)(color.B * 0.55));

        private void RebuildGrid()
        {
            foreach (Element3D element in _extras)
            {
                _viewport.Items.Remove(element);
                element.Dispose();
            }

            _extras.Clear();

            if (!_gate.IsAttached || !_grid || _model.Primitives == null || _model.Primitives.Bounds.IsEmpty)
            {
                return;
            }

            var positions = new Vector3Collection();
            var indices = new IntCollection();

            foreach (DrawPolyline line in _workPlane.GridLines(_model.Primitives.Bounds))
            {
                foreach (Vec3 end in line.Points)
                {
                    Vec3F f = RenderSpace.Relative(end, Origin);
                    indices.Add(positions.Count);
                    positions.Add(new Vector3(f.X, f.Y, f.Z));
                }
            }

            var grid = new LineGeometryModel3D
            {
                Geometry = new LineGeometry3D { Positions = positions, Indices = indices },
                Color = Color.FromRgb(190, 190, 190),
                Thickness = 0.8,
                IsHitTestVisible = false,
            };

            _extras.Add(grid);
            _viewport.Items.Add(grid);
        }

        private sealed class ItemVisual
        {
            internal int Index;
            internal Color BaseColor;
            internal bool HasMesh;
            internal bool HasLines;
            internal bool HasPoints;
            internal bool HasSection;
            internal bool HasCap;
            internal bool HasLabels;
            internal MeshGeometryModel3D Mesh;
            internal MeshGeometryModel3D Cap;
            internal LineGeometryModel3D Lines;
            internal LineGeometryModel3D Section;
            internal PointGeometryModel3D Points;
            internal BillboardTextModel3D Labels;
            internal readonly List<Element3D> Elements = new List<Element3D>();
        }
    }
}
