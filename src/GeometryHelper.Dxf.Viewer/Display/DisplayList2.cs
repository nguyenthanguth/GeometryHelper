using System;
using System.Collections;
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
        private Dictionary<string, Insertions> _insertions;
        private DisplayBox _bounds = DisplayBox.Empty;

        // The entities of the space in the order they were drawn, each with its items and the layers it read, so that an edit
        // draws again only the entities it changed (see DisplayBuilder.Patch).
        private readonly List<RunRecord> _runs;

        // What the runs read, by the number of the run: few runs read anything, and a run with nothing to read keeps no room for it.
        private Dictionary<int, string[]> _reads;
        private RunList _runList;

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
            _insertions =
                older == null
                    ? new Dictionary<string, Insertions>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, Insertions>(older._insertions.Count, StringComparer.OrdinalIgnoreCase);
            _runs = older == null ? new List<RunRecord>() : new List<RunRecord>(older._runs.Count);
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

            if (_clip != null && item.BlockClip == null)
            {
                item.BlockClip = _clip;
            }

            _items.Add(item);
            _bounds = _bounds.Union(item is DisplayRay2 ray ? DisplayBox.Empty.Add(ray.X, ray.Y) : item.VisibleBounds);
        }

        // The region every item added is given while a clipped block reference is being drawn; null at any other time.
        private DisplayClipRegion _clip;

        /// <summary>
        /// Starts clipping the items added from now on to a ring, within the clip already in force, until <see cref="PopClip"/>:
        /// the items of a clipped block reference are drawn between the two.
        /// </summary>
        internal DisplayClipRegion PushClip(DisplayClip2 outline)
        {
            DisplayClipRegion before = _clip;
            _clip = new DisplayClipRegion(outline, before);
            return before;
        }

        /// <summary>Ends the clip <see cref="PushClip"/> started, back to the one it returned.</summary>
        internal void PopClip(DisplayClipRegion before) => _clip = before;

        /// <summary>
        /// The insertion points of an entity, as the snap Insertion takes them: of an INSERT its insertion point and those of
        /// its attributes shown, of a TEXT or an MTEXT the point it is placed by; empty for any other entity. In drawing units,
        /// in the order they were drawn.
        /// </summary>
        internal IReadOnlyList<GeoPoint2> InsertionPointsOf(string handle) =>
            handle != null && _insertions.TryGetValue(handle, out Insertions found) ? found.ToList() : NoPoints;

        /// <summary>Keeps an insertion point of an entity.</summary>
        internal void AddInsertionPoint(string handle, double x, double y)
        {
            if (string.IsNullOrEmpty(handle))
            {
                return;
            }

            var point = new GeoPoint2(x, y);

            if (!_insertions.TryGetValue(handle, out Insertions found))
            {
                _insertions[handle] = new Insertions { First = point };
                return;
            }

            if (found.More == null)
            {
                found.More = new List<GeoPoint2>(1);
                _insertions[handle] = found;
            }

            found.More.Add(point);
        }

        /// <summary>
        /// The insertion points of one entity: nearly every entity that has any has one, kept in the entry itself, and the rest,
        /// of an INSERT with attributes, in a list made then. A list of one in an array of one took 72 bytes more than the
        /// 16 the point takes.
        /// </summary>
        private struct Insertions
        {
            public GeoPoint2 First;
            public List<GeoPoint2> More;

            public IReadOnlyList<GeoPoint2> ToList()
            {
                var points = new GeoPoint2[More == null ? 1 : 1 + More.Count];
                points[0] = First;
                More?.CopyTo(points, 1);
                return points;
            }
        }

        /// <summary>Gives back the room the lists of items and runs made by doubling left empty; a list that is full to 9 tenths is left as it is.</summary>
        internal void TrimExcess()
        {
            _items.TrimExcess();
            _runs.TrimExcess();

            // The entries of a dictionary grown by doubling: up to 4 tenths of them empty, at 48 bytes.
            _insertions = new Dictionary<string, Insertions>(_insertions, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets the entities of the space in the order they were drawn, each with its place in <see cref="Items"/> and the layers it read.</summary>
        internal IReadOnlyList<DisplayRun> Runs => _runList ?? (_runList = new RunList(this));

        /// <summary>Gets or sets the height of the view the points of a relative size were sized by when the list was made.</summary>
        internal double PointViewHeight { get; set; }

        /// <summary>Gets or sets the folder the fonts were first looked for in when the list was made; null for none.</summary>
        internal string FontFolder { get; set; }

        /// <summary>Gets or sets the annotation scale annotative objects were drawn at when the list was made, with ANNOALLVISIBLE; null for none.</summary>
        internal string AnnotationKey { get; set; }

        /// <summary>Gets or sets whether an annotative object was met when the list was made, which a viewport of another scale draws model space again for.</summary>
        internal bool AnnotativeSeen { get; set; }

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
            AddRecord(handle, start, _items.Count - start, layers, reads);

        /// <summary>Takes the run of an entity from an older list of the same space as it was drawn there: its items, its insertion points and its layers.</summary>
        internal void CopyRun(DisplayList2 from, DisplayRun run)
        {
            int start = _items.Count;
            int end = run.Start + run.Count;

            for (int i = run.Start; i < end; i++)
            {
                Add(from._items[i]);
            }

            if (!string.IsNullOrEmpty(run.Handle) && from._insertions.TryGetValue(run.Handle, out Insertions points))
            {
                _insertions[run.Handle] = points;
            }

            AddRecord(run.Handle, start, run.Count, run.Layers, run.Reads);
        }

        private void AddRecord(string handle, int start, int count, ulong layers, string[] reads)
        {
            if (reads != null)
            {
                if (_reads == null)
                {
                    _reads = new Dictionary<int, string[]>();
                }

                _reads[_runs.Count] = reads;
            }

            _runs.Add(new RunRecord(handle, start, count, layers));
        }

        /// <summary>A run as the list keeps it: 24 bytes where a run with its reads took 32, a million of them in a drawing of 1 000 000 entities.</summary>
        private readonly struct RunRecord
        {
            public RunRecord(string handle, int start, int count, ulong layers)
            {
                Handle = handle;
                Start = start;
                Count = count;
                Layers = layers;
            }

            public string Handle { get; }

            public int Start { get; }

            public int Count { get; }

            public ulong Layers { get; }
        }

        /// <summary>The runs as <see cref="DisplayRun"/>, each made when asked for from its record and what it read.</summary>
        private sealed class RunList : IReadOnlyList<DisplayRun>
        {
            private readonly DisplayList2 _list;

            public RunList(DisplayList2 list) => _list = list;

            public int Count => _list._runs.Count;

            public DisplayRun this[int index]
            {
                get
                {
                    RunRecord record = _list._runs[index];
                    string[] reads = null;
                    _list._reads?.TryGetValue(index, out reads);
                    return new DisplayRun(record.Handle, record.Start, record.Count, record.Layers, reads);
                }
            }

            public IEnumerator<DisplayRun> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                {
                    yield return this[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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

        /// <summary>
        /// Gets the outline the viewport is clipped to (VPCLIP), in paper units: model space shows through it in place of the
        /// frame, and it is the border of the viewport; null for a viewport that is its frame.
        /// </summary>
        public DisplayClip2 Clip { get; internal set; }

        /// <inheritdoc/>
        public override DisplayBox Bounds => _bounds;

        /// <inheritdoc/>
        public override double DistanceTo(double x, double y)
        {
            if (Clip != null)
            {
                return Clip.DistanceToEdge(x, y);
            }

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
