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
    /// How the scene is drawn.
    /// </summary>
    public enum RenderMode
    {
        /// <summary>
        /// Lines only.
        /// </summary>
        Wireframe,

        /// <summary>
        /// Surfaces only; shapes with no surface keep their lines.
        /// </summary>
        Shaded,

        /// <summary>
        /// Surfaces and their edges.
        /// </summary>
        ShadedEdges,

        /// <summary>
        /// Surfaces see-through, with their edges.
        /// </summary>
        Transparent,
    }

    /// <summary>
    /// The elements of the 3D view for a scene: one set of meshes, lines, points and labels for each item, built relative to
    /// the centre of the scene so that the graphics card, which works in floats, is handed small numbers. It also owns what
    /// the view shows of the state: render mode, which items are visible, which are selected.
    /// </summary>
    public sealed class SceneView
    {
        private const double LineThickness = 1.4;
        internal const double LabelFontPixels = 24.0;
        private const double SelectedLineThickness = 3.2;
        private const double HoverLineThickness = 2.6;
        private static readonly Color SelectionColor = Color.FromRgb(0xFF, 0x7A, 0x00);
        private static readonly Color HoverColor = Color.FromRgb(0xFF, 0xC4, 0x40);

        private readonly Viewport3DX _viewport;
        private readonly List<ItemVisual> _visuals = new List<ItemVisual>();
        private readonly HashSet<int> _selected = new HashSet<int>();
        private readonly HashSet<int> _hidden = new HashSet<int>();
        private readonly HashSet<string> _hiddenLayers = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Element3D> _extras = new List<Element3D>();
        private Scene _scene;
        private Primitives _primitives;
        private RenderMode _mode = RenderMode.ShadedEdges;
        private int _depthBias = 8;
        private int _generation;
        private bool _rebasing;
        private bool _grid;
        private int _hover = -1;
        private ScreenMap _labelMap;

        /// <summary>
        /// Initializes the view over a viewport, which should hold the lights already.
        /// </summary>
        public SceneView(Viewport3DX viewport)
        {
            _viewport = viewport;
        }

        /// <summary>
        /// Gets the scene shown, or null.
        /// </summary>
        public Scene Scene => _scene;

        /// <summary>
        /// Gets what the scene is drawn with, or null.
        /// </summary>
        public Primitives Primitives => _primitives;

        /// <summary>
        /// Gets the point every coordinate handed to the graphics card is measured from: the middle of the scene.
        /// </summary>
        public Vec3 Origin { get; private set; }

        /// <summary>
        /// Gets or sets how the scene is drawn.
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
        /// Gets or sets whether a grid is drawn on the plane z = 0.
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
        /// Gets the indexes of the selected items.
        /// </summary>
        public IReadOnlyCollection<int> Selected => _selected;

        /// <summary>
        /// Gets whether an item is hidden by itself, not counting its layer.
        /// </summary>
        public bool IsItemHidden(int index) => _hidden.Contains(index);

        /// <summary>
        /// Gets whether a layer is hidden.
        /// </summary>
        public bool IsLayerHidden(string layer) => _hiddenLayers.Contains(layer);

        /// <summary>
        /// Shows a scene, replacing the one before.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <param name="primitives">What it is drawn with.</param>
        public void Show(Scene scene, Primitives primitives)
        {
            Clear();
            _generation++;
            _scene = scene;
            _primitives = primitives;
            _hidden.Clear();
            _hiddenLayers.Clear();
            _selected.Clear();
            _hover = -1;
            _labelMap = null;

            foreach (Layer layer in scene.Layers.Where(l => !l.Visible))
            {
                _hiddenLayers.Add(layer.Name);
            }

            Origin = primitives.Bounds.IsEmpty ? default(Vec3) : primitives.Bounds.Center;

            for (int i = 0; i < scene.Items.Count; i++)
            {
                ItemVisual visual = Build(scene.Items[i], primitives.Items[i], i);
                _visuals.Add(visual);

                foreach (Element3D element in visual.Elements)
                {
                    _viewport.Items.Add(element);
                }
            }

            RebuildGrid();
            Refresh();
        }

        /// <summary>
        /// Takes the scene off the view.
        /// </summary>
        public void Clear()
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
            _scene = null;
            _primitives = null;
        }

        /// <summary>
        /// Shows or hides an item, apart from its layer.
        /// </summary>
        public void SetItemHidden(int index, bool hidden)
        {
            if (hidden ? _hidden.Add(index) : _hidden.Remove(index))
            {
                Refresh();
            }
        }

        /// <summary>
        /// Shows or hides several items at once, drawing the change once.
        /// </summary>
        public void SetItemsHidden(IEnumerable<int> items, bool hidden)
        {
            bool changed = false;

            foreach (int index in items)
            {
                changed |= hidden ? _hidden.Add(index) : _hidden.Remove(index);
            }

            if (changed)
            {
                Refresh();
            }
        }

        /// <summary>
        /// Shows or hides a layer.
        /// </summary>
        public void SetLayerHidden(string layer, bool hidden)
        {
            if (hidden ? _hiddenLayers.Add(layer) : _hiddenLayers.Remove(layer))
            {
                Refresh();
            }
        }

        /// <summary>
        /// Shows every item and layer again.
        /// </summary>
        public void ShowAll()
        {
            _hidden.Clear();
            _hiddenLayers.Clear();
            Refresh();
        }

        /// <summary>
        /// Gets whether an item is shown: not hidden itself, and not on a hidden layer.
        /// </summary>
        public bool IsVisible(int index)
            => _scene != null && !_hidden.Contains(index) && !_hiddenLayers.Contains(_scene.Items[index].Layer);

        /// <summary>
        /// Replaces the selection.
        /// </summary>
        public void Select(IEnumerable<int> items)
        {
            _selected.Clear();

            foreach (int index in items)
            {
                if (_scene != null && index >= 0 && index < _scene.Items.Count)
                {
                    _selected.Add(index);
                }
            }

            Refresh();
        }

        /// <summary>
        /// Gets the item the cursor is over, or -1; it is drawn lighter than the rest, as a pre-highlight of what a click would select.
        /// </summary>
        public int Hover => _hover;

        /// <summary>
        /// Sets the item the cursor is over; -1 for none.
        /// </summary>
        public void SetHover(int index)
        {
            if (_scene == null || index == _hover)
            {
                return;
            }

            int before = _hover;
            _hover = index >= 0 && index < _visuals.Count ? index : -1;

            if (before >= 0 && before < _visuals.Count)
            {
                Apply(_visuals[before]);
            }

            if (_hover >= 0)
            {
                Apply(_visuals[_hover]);
            }
        }

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
                if (visual.Labels != null && visual.Labels.Geometry is BillboardText3D text)
                {
                    ScaleLabels(_primitives.Items[visual.Index], text, map);
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

        /// <summary>
        /// Gets the box round the selected items, or round the visible ones where none is selected.
        /// </summary>
        public Bounds3 BoundsOf(IEnumerable<int> items)
        {
            Bounds3 bounds = default(Bounds3);

            foreach (int index in items)
            {
                bounds = bounds.Include(_primitives.Items[index].Bounds);
            }

            return bounds;
        }

        /// <summary>
        /// Gets the box round the visible items.
        /// </summary>
        public Bounds3 VisibleBounds()
            => _primitives == null ? default(Bounds3) : BoundsOf(Enumerable.Range(0, _primitives.Items.Count).Where(IsVisible));

        /// <summary>
        /// What the graphics card is given for an item, relative to a render origin. It holds no screen type, so it can be made
        /// on another thread while the old geometry is still being drawn.
        /// </summary>
        private sealed class ItemGeometry
        {
            internal MeshGeometry3D Mesh;
            internal LineGeometry3D Lines;
            internal PointGeometry3D Points;
        }

        private static ItemGeometry MakeGeometry(ItemPrimitives p, Vec3 origin)
        {
            var geometry = new ItemGeometry();
            Vector3 Rel(Vec3 v)
            {
                Vec3F f = RenderSpace.Relative(v, origin);
                return new Vector3(f.X, f.Y, f.Z);
            }

            if (p.Triangles.Count > 0)
            {
                var positions = new Vector3Collection(p.Triangles.Count * 3);
                var normals = new Vector3Collection(p.Triangles.Count * 3);
                var indices = new IntCollection(p.Triangles.Count * 3);

                foreach (DrawTriangle t in p.Triangles)
                {
                    Vector3 n = new Vector3((float)t.Normal.X, (float)t.Normal.Y, (float)t.Normal.Z);

                    foreach (Vec3 corner in new[] { t.A, t.B, t.C })
                    {
                        indices.Add(positions.Count);
                        positions.Add(Rel(corner));
                        normals.Add(n);
                    }
                }

                geometry.Mesh = new MeshGeometry3D { Positions = positions, Normals = normals, Indices = indices };
            }

            if (p.Polylines.Count > 0)
            {
                var positions = new Vector3Collection();
                var indices = new IntCollection();

                foreach (DrawPolyline line in p.Polylines)
                {
                    int first = positions.Count;

                    foreach (Vec3 point in line.Points)
                    {
                        positions.Add(Rel(point));
                    }

                    int count = line.Points.Length;
                    int segments = line.Closed ? count : count - 1;

                    for (int s = 0; s < segments; s++)
                    {
                        indices.Add(first + s);
                        indices.Add(first + (s + 1) % count);
                    }
                }

                geometry.Lines = new LineGeometry3D { Positions = positions, Indices = indices };
            }

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

        private ItemVisual Build(SceneItem item, ItemPrimitives p, int index)
        {
            var visual = new ItemVisual { Index = index, BaseColor = ColorOf(item) };
            ItemGeometry g = MakeGeometry(p, Origin);

            if (g.Mesh != null)
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
                visual.Elements.Add(visual.Mesh);
            }

            if (g.Lines != null)
            {
                visual.Lines = new LineGeometryModel3D
                {
                    Geometry = g.Lines,
                    Color = visual.BaseColor,
                    Thickness = LineThickness,
                    HitTestThickness = 6,
                    Smoothness = 0,
                };
                visual.Elements.Add(visual.Lines);
            }

            if (g.Points != null)
            {
                visual.Points = new PointGeometryModel3D
                {
                    Geometry = g.Points,
                    Color = visual.BaseColor,
                    Figure = PointFigure.Ellipse,
                    Size = new Size(7, 7),
                    HitTestThickness = 8,
                };
                visual.Elements.Add(visual.Points);
            }

            if (p.Labels.Count > 0)
            {
                visual.Labels = new BillboardTextModel3D { Geometry = MakeLabels(p, Origin, visual.BaseColor), FixedSize = true };
                visual.Elements.Add(visual.Labels);
            }

            return visual;
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
            if (_scene == null || _rebasing)
            {
                return false;
            }

            _rebasing = true;
            int generation = _generation;
            Primitives primitives = _primitives;

            try
            {
                ItemGeometry[] made = await Task.Run(() => primitives.Items.Select(p => MakeGeometry(p, newOrigin)).ToArray());

                if (generation != _generation)
                {
                    return false;
                }

                for (int i = 0; i < _visuals.Count; i++)
                {
                    ItemVisual visual = _visuals[i];

                    if (visual.Mesh != null)
                    {
                        visual.Mesh.Geometry = made[i].Mesh;
                    }

                    if (visual.Lines != null)
                    {
                        visual.Lines.Geometry = made[i].Lines;
                    }

                    if (visual.Points != null)
                    {
                        visual.Points.Geometry = made[i].Points;
                    }

                    if (visual.Labels != null)
                    {
                        visual.Labels.Geometry = MakeLabels(primitives.Items[i], newOrigin, visual.BaseColor);
                    }
                }

                Origin = newOrigin;
                RebuildGrid();
                OriginChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            finally
            {
                _rebasing = false;
            }
        }

        /// <summary>
        /// Raised, on the thread that moved it, when the render origin has moved; the camera has to be sent again relative to it.
        /// </summary>
        public event EventHandler OriginChanged;

        /// <summary>
        /// Gets whether the origin is being moved.
        /// </summary>
        public bool IsRebasing => _rebasing;

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
            }
        }
        private static Color4 C4(Color color) => new Color4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        private Color ColorOf(SceneItem item)
        {
            string text = item.Color ?? _scene.FindLayer(item.Layer)?.Color;
            return SceneColor.TryParse(text, out byte r, out byte g, out byte b) ? Color.FromRgb(r, g, b) : Colors.Gray;
        }
        /// <summary>
        /// Applies render mode, visibility and selection to the elements.
        /// </summary>
        private void Refresh()
        {
            if (_scene == null)
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
            bool shown = IsVisible(visual.Index);
            bool selected = _selected.Contains(visual.Index);
            bool hovered = !selected && visual.Index == _hover;
            Color color = selected ? SelectionColor : hovered ? Mix(visual.BaseColor, HoverColor, 0.55) : visual.BaseColor;
            bool hasSurface = visual.Mesh != null;

            if (visual.Mesh != null)
            {
                bool see = _mode != RenderMode.Wireframe;
                visual.Mesh.Visibility = shown && see ? Visibility.Visible : Visibility.Collapsed;
                visual.Mesh.IsHitTestVisible = shown && see;

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

            if (visual.Lines != null)
            {
                bool see = _mode != RenderMode.Shaded || !hasSurface;
                visual.Lines.Visibility = shown && (see || selected || hovered) ? Visibility.Visible : Visibility.Collapsed;
                visual.Lines.Color = _mode == RenderMode.Wireframe || !hasSurface ? color : Darker(color, selected || hovered);
                visual.Lines.Thickness = selected ? SelectedLineThickness : hovered ? HoverLineThickness : LineThickness;
            }

            if (visual.Points != null)
            {
                visual.Points.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
                visual.Points.Color = color;
            }

            if (visual.Labels != null)
            {
                visual.Labels.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        /// <summary>
        /// Gets a colour part of the way from one to another: a hovered item is its own colour warmed, so it can still be told apart.
        /// </summary>
        private static Color Mix(Color from, Color to, double share)
            => Color.FromRgb((byte)(from.R + (to.R - from.R) * share), (byte)(from.G + (to.G - from.G) * share), (byte)(from.B + (to.B - from.B) * share));

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

            if (!_grid || _primitives == null || _primitives.Bounds.IsEmpty)
            {
                return;
            }

            Bounds3 b = _primitives.Bounds;
            double span = Math.Max(b.Size.X, b.Size.Y);
            double step = NiceStep(Math.Max(span, 1.0) / 20.0);
            double minX = Math.Floor(b.Min.X / step - 1.0) * step;
            double maxX = Math.Ceiling(b.Max.X / step + 1.0) * step;
            double minY = Math.Floor(b.Min.Y / step - 1.0) * step;
            double maxY = Math.Ceiling(b.Max.Y / step + 1.0) * step;
            var positions = new Vector3Collection();
            var indices = new IntCollection();

            void Segment(double x1, double y1, double x2, double y2)
            {
                indices.Add(positions.Count);
                positions.Add(new Vector3((float)(x1 - Origin.X), (float)(y1 - Origin.Y), (float)(0.0 - Origin.Z)));
                indices.Add(positions.Count);
                positions.Add(new Vector3((float)(x2 - Origin.X), (float)(y2 - Origin.Y), (float)(0.0 - Origin.Z)));
            }

            for (double x = minX; x <= maxX + step * 0.5; x += step)
            {
                Segment(x, minY, x, maxY);
            }

            for (double y = minY; y <= maxY + step * 0.5; y += step)
            {
                Segment(minX, y, maxX, y);
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

        private static double NiceStep(double raw)
        {
            double power = Math.Pow(10.0, Math.Floor(Math.Log10(raw)));
            double scaled = raw / power;
            return (scaled <= 1.0 ? 1.0 : scaled <= 2.0 ? 2.0 : scaled <= 5.0 ? 5.0 : 10.0) * power;
        }

        private sealed class ItemVisual
        {
            internal int Index;
            internal Color BaseColor;
            internal MeshGeometryModel3D Mesh;
            internal LineGeometryModel3D Lines;
            internal PointGeometryModel3D Points;
            internal BillboardTextModel3D Labels;
            internal readonly List<Element3D> Elements = new List<Element3D>();
        }
    }
}
