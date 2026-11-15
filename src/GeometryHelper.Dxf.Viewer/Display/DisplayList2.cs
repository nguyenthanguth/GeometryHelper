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

        // The items in an array of its own, so that a stretch of them is taken from an older list by one copy (see CopyRuns).
        private DisplayItem2[] _items;
        private int _count;
        private ItemView _view;

        // The insertion points by handle, for the snap Insertion: looked up only, never enumerated.
        private Dictionary<string, Insertions> _insertions;

        // Whether the table above is the older list's, which a patch leaves as it is until it has to change it.
        private bool _insertionsShared;

        // Whether an insertion point was kept since the last run closed: set by the entity drawn, read as its run is closed.
        private bool _runInserted;
        private DisplayBox _bounds = DisplayBox.Empty;

        // The entities of the space in the order they were drawn, each with its items and the layers it read, so that an edit
        // draws again only the entities it changed (see DisplayBuilder.Patch).
        private RunRecord[] _runs;
        private int _runCount;

        // What the runs read, by the number of the run, in the order of the runs: few runs read anything, and a run with
        // nothing to read keeps no room for it.
        private List<ReadEntry> _reads;
        private RunList _runList;

        // How many runs belong to an entity with no handle, which a patch draws anew each time.
        private int _unnamedRuns;

        // The box of the items added, the older list's, and whether what a patch took from it and what it dropped leave the
        // older list's box as it was: see EndPatch.
        private DisplayBox _olderBounds;
        private bool _tracking;
        private bool _newApart = true;
        private bool _droppedInside = true;
        private bool _copiedWhole;

        // The items the runs close are in order and leave no gap between them: where they do not, a patch is made entity by entity.
        private int _tiledEnd;
        private bool _tiled = true;

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
            _items =
                older == null ? Array.Empty<DisplayItem2>() : new DisplayItem2[older._count + (older._count >> 4) + 16];
            // The insertion points of every entity of the older list, taken whole and not copied until a patch changes one (a
            // lookup and an insert for each of the 220 000 entities of a drawing of 1 000 000 that have a point took 55 ms of a
            // patch): those of an entity drawn again are taken out as the patch comes to it (DropInsertions), and put back as it
            // is drawn, in a copy of the table made then.
            _insertions =
                older == null
                    ? new Dictionary<string, Insertions>(StringComparer.OrdinalIgnoreCase)
                    : older._insertions;
            _insertionsShared = older != null;
            _runs =
                older == null ? Array.Empty<RunRecord>() : new RunRecord[older._runCount + (older._runCount >> 4) + 16];

            if (older != null)
            {
                _olderBounds = older._bounds;
                _tracking = true;
            }
        }

        /// <summary>Gets the items in the order they are drawn, the first at the back.</summary>
        public IReadOnlyList<DisplayItem2> Items => _view ?? (_view = new ItemView(this));

        /// <summary>
        /// Gets the box that holds every item, of a <see cref="DisplayRay2"/> its base point alone, as AutoCAD's ZOOM Extents
        /// takes an XLINE or a RAY: a line 100 by 50 from the origin and an XLINE through (200, 300) are held from (0, 0) to
        /// (200, 300).
        /// </summary>
        public DisplayBox Bounds => _bounds;

        /// <summary>Gets the number of items.</summary>
        public int Count => _count;

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

            if (_count == _items.Length)
            {
                GrowItems(_count + 1);
            }

            _items[_count++] = item;
            DisplayBox box = BoxOf(item);
            _bounds = _bounds.Union(box);

            if (_tracking)
            {
                _newApart = _newApart && ApartFrom(box, _olderBounds);
            }
        }

        /// <summary>The box an item adds to the list's: its visible bounds, of a ray its base point alone.</summary>
        private static DisplayBox BoxOf(DisplayItem2 item) =>
            item is DisplayRay2 ray ? DisplayBox.Empty.Add(ray.X, ray.Y) : item.VisibleBounds;

        private void GrowItems(int least)
        {
            int size = Math.Max(Math.Max(4, _items.Length * 2), least);
            Array.Resize(ref _items, size);
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

            _runInserted = true;
            var point = new GeoPoint2(x, y);
            OwnInsertions();

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
            // A list full to 9 tenths is left as it is, as List.TrimExcess leaves one.
            if (_count < _items.Length * 0.9)
            {
                Array.Resize(ref _items, _count);
            }

            if (_runCount < _runs.Length * 0.9)
            {
                Array.Resize(ref _runs, _runCount);
            }

            _reads?.TrimExcess();

            // The entries of a dictionary grown by doubling: up to 4 tenths of them empty, at 48 bytes.
            _insertions = new Dictionary<string, Insertions>(_insertions, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Gets the entities of the space in the order they were drawn, each with its place in <see cref="Items"/> and the layers it read.</summary>
        internal IReadOnlyList<DisplayRun> Runs => _runList ?? (_runList = new RunList(this));

        /// <summary>Gets or sets the height of the view the points of a relative size were sized by when the list was made.</summary>
        internal double PointViewHeight { get; set; }

        /// <summary>
        /// Gets or sets the list of entities of the space the runs were drawn from, one run for each, in their order, and its
        /// version then (<see cref="DxfSpace.Version"/>): where the space is the same and its version is, the runs and the
        /// entities still stand in the same places, whatever the entities say. Null where the runs were not drawn so.
        /// </summary>
        internal object SourceSpace { get; set; }

        /// <summary>Gets or sets the version of <see cref="SourceSpace"/> when the runs were drawn.</summary>
        internal int SourceVersion { get; set; }

        /// <summary>Gets or sets whether the list was patched from an older one by finding the entities drawn again by their places, without a walk of the entities.</summary>
        internal bool PatchedInPlace { get; set; }

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
            AddRecord(handle, start, _count - start, layers, reads, _runInserted);

        /// <summary>
        /// Takes the run of an entity from an older list of the same space as it was drawn there: its items and its layers; its
        /// insertion points came with the list's own (see <see cref="DisplayList2(DisplayList2)"/>).
        /// </summary>
        internal void CopyRun(DisplayList2 from, DisplayRun run)
        {
            int start = _count;
            int end = run.Start + run.Count;

            for (int i = run.Start; i < end; i++)
            {
                Add(from._items[i]);
            }

            AddRecord(run.Handle, start, run.Count, run.Layers, run.Reads, run.Inserted);
        }

        /// <summary>
        /// Takes a stretch of runs, the first <paramref name="count"/> from <paramref name="first"/>, from an older list of the
        /// same space as they were drawn there: their items by one copy and their records moved to where the items now stand, with
        /// what they read. The box of the list is not grown by them: <see cref="EndPatch"/> finds it once the patch is made.
        /// </summary>
        internal void CopyRuns(DisplayList2 from, int first, int count)
        {
            if (count <= 0)
            {
                return;
            }

            int last = first + count - 1;
            int start = from._runs[first].Start;
            int items = from._runs[last].Start + from._runs[last].Count - start;

            if (_count + items > _items.Length)
            {
                GrowItems(_count + items);
            }

            if (_runCount + count > _runs.Length)
            {
                Array.Resize(ref _runs, Math.Max(Math.Max(4, _runs.Length * 2), _runCount + count));
            }

            Array.Copy(from._items, start, _items, _count, items);
            int shift = _count - start;

            if (shift == 0)
            {
                Array.Copy(from._runs, first, _runs, _runCount, count);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    _runs[_runCount + i] = new RunRecord(from._runs[first + i], shift);
                }
            }

            if (from._reads != null)
            {
                for (int e = from.FirstRead(first); e < from._reads.Count && from._reads[e].Run <= last; e++)
                {
                    EnsureReads().Add(new ReadEntry(_runCount + from._reads[e].Run - first, from._reads[e].Reads));
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(_runs[_runCount + i].Handle))
                {
                    _unnamedRuns++;
                }
            }

            Track(_count, items);
            _count += items;
            _runCount += count;
            _copiedWhole = true;
        }

        /// <summary>Notes the stretch of items from <paramref name="start"/> as a run closing after it would: they stand in order, with no gap.</summary>
        private void Track(int start, int length)
        {
            if (start != _tiledEnd)
            {
                _tiled = false;
            }

            _tiledEnd = start + length;
        }

        private List<ReadEntry> EnsureReads() => _reads ?? (_reads = new List<ReadEntry>());

        /// <summary>The place in the list of what the runs read of the first entry for a run, or after the last run if none.</summary>
        private int FirstRead(int run)
        {
            int low = 0;
            int high = _reads.Count;

            while (low < high)
            {
                int middle = (low + high) >> 1;

                if (_reads[middle].Run < run)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>How many runs the list holds.</summary>
        internal int RunCount => _runCount;

        /// <summary>How many runs are of an entity with no handle, which a patch draws anew each time.</summary>
        internal int UnnamedRuns => _unnamedRuns;

        /// <summary>Whether the runs close the items in order with none left between them: a patch can then take stretches of runs whole.</summary>
        internal bool IsTiled => _tiled && _tiledEnd == _count;

        /// <summary>The handle of a run.</summary>
        internal string RunHandle(int run) => _runs[run].Handle;

        /// <summary>The layers a run read, as <see cref="DisplayRun.Layers"/>.</summary>
        internal ulong RunLayers(int run) => _runs[run].Layers;

        /// <summary>Puts the numbers of the runs that read a layer of a mask into a list, in order.</summary>
        internal void AddRunsReading(ulong mask, List<int> into)
        {
            for (int i = 0; i < _runCount; i++)
            {
                if ((_runs[i].Layers & mask) != 0)
                {
                    into.Add(i);
                }
            }
        }

        /// <summary>The runs that read something, each with the handles it read: the numbers of the runs, in order.</summary>
        internal int ReadingRuns => _reads == null ? 0 : _reads.Count;

        /// <summary>The number of the run of the entry at a place of what the runs read.</summary>
        internal int ReadRun(int entry) => _reads[entry].Run;

        /// <summary>What the run of the entry at a place read.</summary>
        internal string[] ReadOf(int entry) => _reads[entry].Reads;

        /// <summary>The run of a number as <see cref="Runs"/> gives it.</summary>
        internal DisplayRun RunAt(int run)
        {
            RunRecord record = _runs[run];
            string[] reads = null;

            if (_reads != null)
            {
                int entry = FirstRead(run);

                if (entry < _reads.Count && _reads[entry].Run == run)
                {
                    reads = _reads[entry].Reads;
                }
            }

            return new DisplayRun(record.Handle, record.Start, record.Count, record.Layers, reads, record.Inserted);
        }

        /// <summary>
        /// Notes that a run of the older list is not taken by this patch, because it is drawn again or its entity is gone: if
        /// one of its items touches the older list's box, the box of the patch is found again from every item.
        /// </summary>
        internal void NoteDropped(DisplayList2 older, int run)
        {
            if (!_droppedInside)
            {
                return;
            }

            // An edit that drops many runs, a layer switched, is likely to drop one that touches the box: the box is found again
            // from every item, as it is found when one does, and the boxes of the runs dropped are not looked at.
            if (++_dropped > MostDropped)
            {
                _droppedInside = false;
                return;
            }

            RunRecord record = older._runs[run];
            int end = record.Start + record.Count;

            for (int i = record.Start; _droppedInside && i < end; i++)
            {
                _droppedInside = InsideOf(BoxOf(older._items[i]), _olderBounds);
            }
        }

        /// <summary>The most runs a patch drops that the box of the older list is kept for: a move of a few entities, not a layer switched.</summary>
        private const int MostDropped = 64;

        private int _dropped;

        /// <summary>
        /// Gives the list its box once a patch that took stretches of runs whole is made. The older list's box is the union of
        /// its items'; where no item dropped, and no item added, has a side on a side of it, the items kept still reach every
        /// side, so the box is the older list's grown by the items added, to the bit; otherwise it is found again from every item.
        /// </summary>
        internal void EndPatch(DisplayList2 older)
        {
            if (!_copiedWhole)
            {
                return;
            }

            if (_droppedInside && _newApart && !_olderBounds.IsEmpty)
            {
                _bounds = _olderBounds.Union(_bounds);
                return;
            }

            DisplayBox box = DisplayBox.Empty;

            for (int i = 0; i < _count; i++)
            {
                box = box.Union(BoxOf(_items[i]));
            }

            _bounds = box;
        }

        /// <summary>Whether a box lies wholly inside another, on every side and not touching it; an empty box holds nothing and lies inside.</summary>
        private static bool InsideOf(DisplayBox box, DisplayBox whole) =>
            box.IsEmpty
            || (box.MinX > whole.MinX && box.MinY > whole.MinY && box.MaxX < whole.MaxX && box.MaxY < whole.MaxY);

        /// <summary>Whether a box is on no side level with the same side of another; an empty box adds nothing and is apart.</summary>
        private static bool ApartFrom(DisplayBox box, DisplayBox whole) =>
            box.IsEmpty
            || (
                (box.MinX < whole.MinX || box.MinX > whole.MinX)
                && (box.MinY < whole.MinY || box.MinY > whole.MinY)
                && (box.MaxX < whole.MaxX || box.MaxX > whole.MaxX)
                && (box.MaxY < whole.MaxY || box.MaxY > whole.MaxY)
            );

        /// <summary>
        /// Takes out the insertion points an older list's run of an entity kept, which a patch that draws the entity again, or
        /// does not draw it at all, no longer has: the points were taken whole with the older list.
        /// </summary>
        internal void DropInsertions(DisplayRun run)
        {
            if (run.Inserted && !string.IsNullOrEmpty(run.Handle) && _insertions.ContainsKey(run.Handle))
            {
                OwnInsertions();
                _insertions.Remove(run.Handle);
            }
        }

        /// <summary>Makes the table of insertion points the list's own, a copy of the older list's it shared, before it is changed.</summary>
        private void OwnInsertions()
        {
            if (_insertionsShared)
            {
                _insertions = new Dictionary<string, Insertions>(_insertions, StringComparer.OrdinalIgnoreCase);
                _insertionsShared = false;
            }
        }

        private void AddRecord(string handle, int start, int count, ulong layers, string[] reads, bool inserted)
        {
            _runInserted = false;

            if (reads != null)
            {
                EnsureReads().Add(new ReadEntry(_runCount, reads));
            }

            if (string.IsNullOrEmpty(handle))
            {
                _unnamedRuns++;
            }

            if (_runCount == _runs.Length)
            {
                Array.Resize(ref _runs, Math.Max(4, _runs.Length * 2));
            }

            Track(start, count);
            _runs[_runCount++] = new RunRecord(handle, start, count, layers, inserted);
        }

        /// <summary>What a run read, with the number of the run.</summary>
        private readonly struct ReadEntry
        {
            public ReadEntry(int run, string[] reads)
            {
                Run = run;
                Reads = reads;
            }

            public int Run { get; }

            public string[] Reads { get; }
        }

        /// <summary>The items as the read-only list <see cref="Items"/> gives: those of the list at the moment they are read.</summary>
        private sealed class ItemView : IReadOnlyList<DisplayItem2>
        {
            private readonly DisplayList2 _list;

            public ItemView(DisplayList2 list) => _list = list;

            public int Count => _list._count;

            public DisplayItem2 this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)_list._count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return _list._items[index];
                }
            }

            public IEnumerator<DisplayItem2> GetEnumerator()
            {
                for (int i = 0; i < _list._count; i++)
                {
                    yield return _list._items[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>A run as the list keeps it: 24 bytes where a run with its reads took 32, a million of them in a drawing of 1 000 000 entities.</summary>
        private readonly struct RunRecord
        {
            // The count of items, and in the top bit whether the entity kept an insertion point: the record stays 24 bytes.
            private readonly int _countAndInserted;

            public RunRecord(string handle, int start, int count, ulong layers, bool inserted)
            {
                Handle = handle;
                Start = start;
                _countAndInserted = inserted ? count | int.MinValue : count;
                Layers = layers;
            }

            /// <summary>The record of another run moved to a place of items <paramref name="shift"/> further on.</summary>
            public RunRecord(RunRecord run, int shift)
            {
                Handle = run.Handle;
                Start = run.Start + shift;
                _countAndInserted = run._countAndInserted;
                Layers = run.Layers;
            }

            public string Handle { get; }

            public int Start { get; }

            public int Count => _countAndInserted & int.MaxValue;

            public bool Inserted => _countAndInserted < 0;

            public ulong Layers { get; }
        }

        /// <summary>The runs as <see cref="DisplayRun"/>, each made when asked for from its record and what it read.</summary>
        private sealed class RunList : IReadOnlyList<DisplayRun>
        {
            private readonly DisplayList2 _list;

            public RunList(DisplayList2 list) => _list = list;

            public int Count => _list._runCount;

            public DisplayRun this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)_list._runCount)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return _list.RunAt(index);
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
        public DisplayRun(string handle, int start, int count, ulong layers, string[] reads, bool inserted)
        {
            Handle = handle;
            Start = start;
            Count = count;
            Layers = layers;
            Reads = reads;
            Inserted = inserted;
        }

        public string Handle { get; }

        public int Start { get; }

        public int Count { get; }

        public ulong Layers { get; }

        public string[] Reads { get; }

        /// <summary>Whether the entity kept an insertion point of its own when it was drawn.</summary>
        public bool Inserted { get; }
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
