using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What one space of a drawing, model space or a layout, is drawn as: its items in drawing order, the box that holds
    /// them, and the layers they sit on. It is made once from the entities and kept until one changes.
    /// </summary>
    public sealed class DisplayList2
    {
        private static readonly IReadOnlyList<GeoPoint2> NoPoints = Array.Empty<GeoPoint2>();

        private readonly List<DisplayItem2> _items;

        // The insertion points by handle, for the snap Insertion: looked up only, never enumerated.
        private readonly Dictionary<string, List<GeoPoint2>> _insertions = new Dictionary<string, List<GeoPoint2>>(
            StringComparer.OrdinalIgnoreCase
        );
        private DisplayBox _bounds = DisplayBox.Empty;

        // The entities of the space in the order they were drawn, each with its items and the layers it read, so that an edit
        // draws again only the entities it changed (see DisplayBuilder.Patch).
        private readonly List<DisplayRun> _runs;

        // The bit of each layer the runs read, numbered as the layers were first met; looked up only, never enumerated.
        private readonly Dictionary<string, int> _layerBits;

        /// <summary>Makes an empty list.</summary>
        public DisplayList2()
            : this(null) { }

        /// <summary>Makes an empty list that numbers its layers as an older list of the same space did, so that it can be patched as that one could.</summary>
        internal DisplayList2(DisplayList2 older)
        {
            _layerBits =
                older == null
                    ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(older._layerBits, StringComparer.OrdinalIgnoreCase);

            // Sized as the older list, which a patch mostly copies: grown by doubling, a list of 178 000 items and 100 000 runs
            // makes some 12 MB of arrays on the large object heap, and the collections they bring land in the patch.
            _items = older == null ? new List<DisplayItem2>() : new List<DisplayItem2>(older._items.Count);
            _runs = older == null ? new List<DisplayRun>() : new List<DisplayRun>(older._runs.Count);
        }

        /// <summary>Gets the items in the order they are drawn, the first at the back.</summary>
        public IReadOnlyList<DisplayItem2> Items => _items;

        /// <summary>
        /// Gets the box that holds every item, of a <see cref="DisplayRay2"/> its base point alone, as AutoCAD's ZOOM Extents
        /// takes an XLINE or a RAY: a line 100 by 50 from the origin and an XLINE through (200, 300) are held from (0, 0) to
        /// (200, 300).
        /// </summary>
        public DisplayBox Bounds => _bounds;

        /// <summary>Gets the number of items.</summary>
        public int Count => _items.Count;

        /// <summary>Adds an item at the front.</summary>
        /// <param name="item">The item.</param>
        public void Add(DisplayItem2 item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            _items.Add(item);
            _bounds = _bounds.Union(item is DisplayRay2 ray ? DisplayBox.Empty.Add(ray.X, ray.Y) : item.Bounds);
        }

        /// <summary>
        /// The insertion points of an entity, as the snap Insertion takes them: of an INSERT its insertion point and those of
        /// its attributes shown, of a TEXT or an MTEXT the point it is placed by; empty for any other entity. In drawing units,
        /// in the order they were drawn.
        /// </summary>
        internal IReadOnlyList<GeoPoint2> InsertionPointsOf(string handle) =>
            handle != null && _insertions.TryGetValue(handle, out List<GeoPoint2> points) ? points : NoPoints;

        /// <summary>Keeps an insertion point of an entity.</summary>
        internal void AddInsertionPoint(string handle, double x, double y)
        {
            if (string.IsNullOrEmpty(handle))
            {
                return;
            }

            if (!_insertions.TryGetValue(handle, out List<GeoPoint2> points))
            {
                points = new List<GeoPoint2>();
                _insertions[handle] = points;
            }

            points.Add(new GeoPoint2(x, y));
        }

        /// <summary>Gets the entities of the space in the order they were drawn, each with its place in <see cref="Items"/> and the layers it read.</summary>
        internal IReadOnlyList<DisplayRun> Runs => _runs;

        /// <summary>Gets or sets the height of the view the points of a relative size were sized by when the list was made.</summary>
        internal double PointViewHeight { get; set; }

        /// <summary>Gets or sets the folder the fonts were first looked for in when the list was made; null for none.</summary>
        internal string FontFolder { get; set; }

        /// <summary>The bit a layer takes in the masks of the runs: one each for the first 63 layers met, the last shared by the rest.</summary>
        internal ulong LayerBit(string layer)
        {
            if (!_layerBits.TryGetValue(layer ?? string.Empty, out int bit))
            {
                bit = _layerBits.Count;
                _layerBits[layer ?? string.Empty] = bit;
            }

            return 1UL << Math.Min(bit, 63);
        }

        /// <summary>The mask of the bits of some layers; nought where no run read any of them.</summary>
        internal ulong LayerMask(IEnumerable<string> layers)
        {
            ulong mask = 0;

            foreach (string layer in layers)
            {
                if (_layerBits.TryGetValue(layer ?? string.Empty, out int bit))
                {
                    mask |= 1UL << Math.Min(bit, 63);
                }
            }

            return mask;
        }

        /// <summary>Closes the run of an entity drawn: the items added since <paramref name="start"/>, the layers it read, and the other entities and anonymous blocks it read (null for none).</summary>
        internal void AddRun(string handle, int start, ulong layers, string[] reads) =>
            _runs.Add(new DisplayRun(handle, start, _items.Count - start, layers, reads));

        /// <summary>Takes the run of an entity from an older list of the same space as it was drawn there: its items, its insertion points and its layers.</summary>
        internal void CopyRun(DisplayList2 from, DisplayRun run)
        {
            int start = _items.Count;
            int end = run.Start + run.Count;

            for (int i = run.Start; i < end; i++)
            {
                Add(from._items[i]);
            }

            if (
                !string.IsNullOrEmpty(run.Handle)
                && from._insertions.TryGetValue(run.Handle, out List<GeoPoint2> points)
            )
            {
                _insertions[run.Handle] = points;
            }

            _runs.Add(new DisplayRun(run.Handle, start, run.Count, run.Layers, run.Reads));
        }
    }

    /// <summary>
    /// One entity of a space as its display list holds it: its handle, its items from <see cref="Start"/>, the bits of the
    /// layers it read, and the handles of what else it read that an edit of another entity can change: the anonymous blocks
    /// it drew (a dimension's picture, which a copy of it shares) and the words a leader runs its hook on to.
    /// </summary>
    internal readonly struct DisplayRun
    {
        public DisplayRun(string handle, int start, int count, ulong layers, string[] reads)
        {
            Handle = handle;
            Start = start;
            Count = count;
            Layers = layers;
            Reads = reads;
        }

        public string Handle { get; }

        public int Start { get; }

        public int Count { get; }

        public ulong Layers { get; }

        public string[] Reads { get; }
    }

    /// <summary>
    /// A viewport of a layout: a window onto model space at a scale, which the canvas draws by clipping to its frame and
    /// drawing the model-space items under the viewport's transformation.
    /// </summary>
    public sealed class DisplayViewport2 : DisplayItem2
    {
        private readonly DisplayBox _bounds;

        /// <summary>Makes a viewport.</summary>
        /// <param name="centerX">The X of the centre of the frame, in paper units.</param>
        /// <param name="centerY">The Y of the centre of the frame, in paper units.</param>
        /// <param name="width">The width of the frame in paper units.</param>
        /// <param name="height">The height of the frame in paper units.</param>
        /// <param name="viewCenterX">The X of the point of model space at the centre of the frame.</param>
        /// <param name="viewCenterY">The Y of the point of model space at the centre of the frame.</param>
        /// <param name="scale">The paper length of one unit of model space: a viewport at 1:20 has 0.05.</param>
        /// <param name="twist">The angle the view is turned by, in radians.</param>
        /// <param name="model">The model-space items it shows.</param>
        /// <param name="frozenLayers">The layers frozen in this viewport, which it leaves out; may be null.</param>
        /// <param name="style">How its frame is drawn.</param>
        /// <param name="handle">The handle of the viewport entity.</param>
        /// <param name="layer">The layer of the viewport entity.</param>
        public DisplayViewport2(
            double centerX,
            double centerY,
            double width,
            double height,
            double viewCenterX,
            double viewCenterY,
            double scale,
            double twist,
            DisplayList2 model,
            ISet<string> frozenLayers,
            DisplayStyle style,
            string handle,
            string layer
        )
            : base(style, handle, layer)
        {
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
            ViewCenterX = viewCenterX;
            ViewCenterY = viewCenterY;
            Scale = scale;
            Twist = twist;
            Model = model ?? throw new ArgumentNullException(nameof(model));
            FrozenLayers = frozenLayers ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _bounds = new DisplayBox(
                centerX - width / 2.0,
                centerY - height / 2.0,
                centerX + width / 2.0,
                centerY + height / 2.0
            );
        }

        /// <summary>Gets the X of the centre of the frame, in paper units.</summary>
        public double CenterX { get; }

        /// <summary>Gets the Y of the centre of the frame, in paper units.</summary>
        public double CenterY { get; }

        /// <summary>Gets the width of the frame in paper units.</summary>
        public double Width { get; }

        /// <summary>Gets the height of the frame in paper units.</summary>
        public double Height { get; }

        /// <summary>Gets the X of the point of model space at the centre of the frame.</summary>
        public double ViewCenterX { get; }

        /// <summary>Gets the Y of the point of model space at the centre of the frame.</summary>
        public double ViewCenterY { get; }

        /// <summary>Gets the paper length of one unit of model space.</summary>
        public double Scale { get; }

        /// <summary>Gets the angle the view is turned by, in radians.</summary>
        public double Twist { get; }

        /// <summary>Gets the model-space items the viewport shows.</summary>
        public DisplayList2 Model { get; }

        /// <summary>Gets the layers frozen in this viewport.</summary>
        public ISet<string> FrozenLayers { get; }

        /// <summary>Gets the transformation from model space to the paper, as the viewport shows it.</summary>
        public Affine2 ModelToPaper
        {
            get
            {
                Affine2 toView = Affine2.Translation(-ViewCenterX, -ViewCenterY);
                Affine2 turn = Affine2.ScaleRotate(Scale, Scale, Twist);
                return toView.Then(turn).Then(Affine2.Translation(CenterX, CenterY));
            }
        }

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            double dx = Math.Min(Math.Abs(x - _bounds.MinX), Math.Abs(x - _bounds.MaxX));
            double dy = Math.Min(Math.Abs(y - _bounds.MinY), Math.Abs(y - _bounds.MaxY));
            bool insideX = x >= _bounds.MinX && x <= _bounds.MaxX;
            bool insideY = y >= _bounds.MinY && y <= _bounds.MaxY;

            if (insideX && insideY)
            {
                return Math.Min(dx, dy);
            }

            if (insideX)
            {
                return dy;
            }

            return insideY ? dx : Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
