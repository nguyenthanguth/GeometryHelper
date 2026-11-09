using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The entities that have been selected, by handle. An entity is picked and selected as a whole: all the display items
    /// it was drawn as are selected with it.
    /// </summary>
    public sealed class CadSelection
    {
        private readonly HashSet<string> _handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised when the set of selected handles changes.</summary>
        public event EventHandler Changed;

        /// <summary>Gets the handles of the selected entities.</summary>
        public IReadOnlyCollection<string> Handles => _handles;

        /// <summary>Gets the number of selected entities.</summary>
        public int Count => _handles.Count;

        /// <summary>Gets whether an entity is selected.</summary>
        /// <param name="handle">Its handle.</param>
        /// <returns>True when selected.</returns>
        public bool Contains(string handle) => handle != null && _handles.Contains(handle);

        /// <summary>Selects entities, those already selected staying so.</summary>
        /// <param name="handles">The handles.</param>
        public void Add(IEnumerable<string> handles)
        {
            bool changed = false;

            foreach (string handle in handles)
            {
                changed |= handle != null && _handles.Add(handle);
            }

            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Takes entities out of the selection.</summary>
        /// <param name="handles">The handles.</param>
        public void Remove(IEnumerable<string> handles)
        {
            bool changed = false;

            foreach (string handle in handles)
            {
                changed |= handle != null && _handles.Remove(handle);
            }

            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Selects an entity that is not selected and deselects one that is.</summary>
        /// <param name="handle">The handle.</param>
        public void Toggle(string handle)
        {
            if (handle == null)
            {
                return;
            }

            if (!_handles.Remove(handle))
            {
                _handles.Add(handle);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Makes the selection exactly these entities.</summary>
        /// <param name="handles">The handles.</param>
        public void Set(IEnumerable<string> handles)
        {
            var next = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);

            if (next.SetEquals(_handles))
            {
                return;
            }

            _handles.Clear();

            foreach (string handle in next)
            {
                _handles.Add(handle);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Selects nothing.</summary>
        public void Clear()
        {
            if (_handles.Count == 0)
            {
                return;
            }

            _handles.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// A grid over the items of one display list that answers, without looking at every item, which of them lie near a
    /// point or in a box. Picking and window selection never use the pixels: they ask this index in drawing coordinates.
    /// </summary>
    public sealed class PickIndex
    {
        private readonly DisplayList2 _list;

        // The items of each cell, ascending, one run after another: those of cell c from _cellStart[c] to _cellStart[c + 1].
        private readonly int[] _cellStart;
        private readonly int[] _cellItems;
        private readonly List<int> _large = new List<int>();
        private readonly int _n;
        private readonly double _x0;
        private readonly double _y0;
        private readonly double _cellW;
        private readonly double _cellH;

        // The places of the items of each handle, made the first time a window or a highlight asks; looked up only, never enumerated.
        private Dictionary<string, List<int>> _byHandle;
        private RunsByHandle _runs;

        /// <summary>Indexes a display list.</summary>
        /// <param name="list">The list.</param>
        /// <exception cref="ArgumentNullException">Thrown when the list is null.</exception>
        public PickIndex(DisplayList2 list)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            DisplayBox bounds = list.Bounds;
            _n = Math.Max(1, Math.Min(512, (int)Math.Sqrt(Math.Max(1, list.Count))));
            _x0 = bounds.IsEmpty ? 0.0 : bounds.MinX;
            _y0 = bounds.IsEmpty ? 0.0 : bounds.MinY;
            _cellW = Math.Max(bounds.Width / _n, 1E-9);
            _cellH = Math.Max(bounds.Height / _n, 1E-9);
            _cellStart = new int[_n * _n + 1];

            // Counted first and then filled, so that the cells take one array between them and none grows as it fills.
            IReadOnlyList<DisplayItem2> items = list.Items;

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (!Cells(items[i].Bounds, out int ix0, out int iy0, out int ix1, out int iy1))
                    {
                        if (pass == 0 && ix0 < 0)
                        {
                            _large.Add(i);
                        }

                        continue;
                    }

                    for (int iy = iy0; iy <= iy1; iy++)
                    {
                        for (int ix = ix0; ix <= ix1; ix++)
                        {
                            int c = iy * _n + ix;

                            if (pass == 0)
                            {
                                _cellStart[c + 1]++;
                            }
                            else
                            {
                                _cellItems[_cellStart[c]++] = i;
                            }
                        }
                    }
                }

                if (pass == 0)
                {
                    for (int c = 0; c < _n * _n; c++)
                    {
                        _cellStart[c + 1] += _cellStart[c];
                    }

                    _cellItems = new int[_cellStart[_n * _n]];
                }
                else
                {
                    // Each start was moved on to the next cell's while filling: put back.
                    for (int c = _n * _n; c > 0; c--)
                    {
                        _cellStart[c] = _cellStart[c - 1];
                    }

                    _cellStart[0] = 0;
                }
            }
        }

        /// <summary>
        /// The cells an item's box takes; false for one in no cell, with <paramref name="ix0"/> below nought for one looked at
        /// for every point and box: a line without end, which reaches past the grid, and an item over more than a quarter of it.
        /// </summary>
        private bool Cells(DisplayBox box, out int ix0, out int iy0, out int ix1, out int iy1)
        {
            ix0 = iy0 = ix1 = iy1 = 0;

            if (box.IsEmpty)
            {
                return false;
            }

            if (double.IsInfinity(box.MinX) || double.IsInfinity(box.MinY) || double.IsInfinity(box.MaxX) || double.IsInfinity(box.MaxY))
            {
                ix0 = -1;
                return false;
            }

            CellRange(box, out ix0, out iy0, out ix1, out iy1);

            if ((ix1 - ix0 + 1) * (iy1 - iy0 + 1) > _n * _n / 4 + 4)
            {
                ix0 = -1;
                return false;
            }

            return true;
        }

        /// <summary>
        /// The places of the items of an entity, ascending: from the runs of the list where it was made by a document, each
        /// entity's items standing together; else from the items one by one, the places of each handle made once.
        /// </summary>
        private IReadOnlyList<int> PlacesOf(string handle)
        {
            if (string.IsNullOrEmpty(handle))
            {
                return Array.Empty<int>();
            }

            RunsByHandle runs = _runs ?? (_runs = new RunsByHandle(_list));

            if (runs.IsWhole)
            {
                return runs.PlacesOf(handle);
            }

            return ByHandle().TryGetValue(handle, out List<int> group) ? group : (IReadOnlyList<int>)Array.Empty<int>();
        }

        /// <summary>The places of the items of each handle, made once.</summary>
        private Dictionary<string, List<int>> ByHandle()
        {
            Dictionary<string, List<int>> byHandle = _byHandle;

            if (byHandle != null)
            {
                return byHandle;
            }

            byHandle = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<DisplayItem2> items = _list.Items;

            for (int i = 0; i < items.Count; i++)
            {
                string handle = items[i].Handle;

                if (handle != null && handle.Length > 0)
                {
                    if (!byHandle.TryGetValue(handle, out List<int> group))
                    {
                        group = new List<int>();
                        byHandle[handle] = group;
                    }

                    group.Add(i);
                }
            }

            _byHandle = byHandle;
            return byHandle;
        }

        /// <summary>Gets the display list this index was made from.</summary>
        public DisplayList2 List => _list;

        /// <summary>
        /// Gets whether a pick puts the entity drawn on top first among those at about the same distance, as AutoCAD does
        /// while bit 1 of SORTENTS (object selection) is set; true until set. <see cref="CadDocument.GetPickIndex"/> sets it
        /// from the drawing's <see cref="DxfDatabase.SortEnts"/> each time it gives the index.
        /// </summary>
        internal bool ByDrawOrder { get; set; } = true;

        /// <summary>
        /// Gets the entities whose drawing lies within a distance of a point, the nearest first. The distance is in drawing
        /// units, so a caller working in pixels divides by the zoom first. Entities whose distances lie within a tenth of the
        /// tolerance of the nearest of them are of one distance class: while the drawing's SORTENTS holds its bit 1 for object
        /// selection, as AutoCAD's default 127 does, the one drawn on top (last in the draw order) comes first among them, as
        /// AutoCAD picks it; else the handle decides. Two lines crossing at the point come back the one drawn later first; at
        /// 0.6 from the one drawn later and on the other, with a tolerance of 3, the other first.
        /// </summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <param name="tolerance">The greatest distance.</param>
        /// <returns>The handles, the nearest first.</returns>
        public IReadOnlyList<string> Pick(double x, double y, double tolerance)
        {
            var best = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var box = new DisplayBox(x - tolerance, y - tolerance, x + tolerance, y + tolerance);

            foreach (int i in Candidates(box))
            {
                DisplayItem2 item = _list.Items[i];

                if (string.IsNullOrEmpty(item.Handle) || !item.Bounds.Inflate(tolerance).Contains(x, y))
                {
                    continue;
                }

                double distance = item.DistanceTo(x, y);

                if (distance <= tolerance && (!best.TryGetValue(item.Handle, out double known) || distance < known))
                {
                    best[item.Handle] = distance;
                }
            }

            var handles = new List<string>(best.Keys);
            handles.Sort((a, b) =>
            {
                int byDistance = best[a].CompareTo(best[b]);
                return byDistance != 0 ? byDistance : string.CompareOrdinal(a, b);
            });

            if (ByDrawOrder && handles.Count > 1)
            {
                TopFirst(handles, best, tolerance / 10.0);
            }

            return handles;
        }

        /// <summary>
        /// Puts the entity drawn on top first in each distance class of handles sorted nearest first: a class runs from its
        /// nearest to the last within <paramref name="slack"/> of it; on top is last in the list, an entity's items lying
        /// together; the handle decides a tie.
        /// </summary>
        private void TopFirst(List<string> handles, Dictionary<string, double> distances, double slack)
        {
            IComparer<string> topFirst = null;

            for (int start = 0; start < handles.Count;)
            {
                int end = start + 1;

                while (end < handles.Count && distances[handles[end]] - distances[handles[start]] <= slack)
                {
                    end++;
                }

                if (end - start > 1)
                {
                    topFirst = topFirst ?? Comparer<string>.Create((a, b) =>
                    {
                        int byOrder = Top(b).CompareTo(Top(a));
                        return byOrder != 0 ? byOrder : string.CompareOrdinal(a, b);
                    });
                    handles.Sort(start, end - start, topFirst);
                }

                start = end;
            }
        }

        /// <summary>The place in the list of the last item an entity is drawn as: the higher, the nearer the top.</summary>
        private int Top(string handle)
        {
            List<int> group = _byHandle[handle];
            return group[group.Count - 1];
        }

        /// <summary>Gets the entities drawn wholly inside a box, as a window selection takes them.</summary>
        /// <param name="box">The box.</param>
        /// <returns>The handles in the order of the drawing.</returns>
        public IReadOnlyList<string> Window(DisplayBox box)
        {
            var inside = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var spoiled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var handles = new List<string>();

            foreach (int i in Candidates(box))
            {
                string handle = _list.Items[i].Handle;

                if (!string.IsNullOrEmpty(handle))
                {
                    inside.Add(handle);
                }
            }

            foreach (string handle in inside)
            {
                bool all = true;

                foreach (int i in PlacesOf(handle))
                {
                    if (!_list.Items[i].IsInside(box))
                    {
                        all = false;
                        break;
                    }
                }

                if (all)
                {
                    handles.Add(handle);
                }
                else
                {
                    spoiled.Add(handle);
                }
            }

            handles.Sort(StringComparer.Ordinal);
            return handles;
        }

        /// <summary>Gets the entities that cross or lie in a box, as a crossing selection takes them.</summary>
        /// <param name="box">The box.</param>
        /// <returns>The handles, in order.</returns>
        public IReadOnlyList<string> Crossing(DisplayBox box)
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (int i in Candidates(box))
            {
                DisplayItem2 item = _list.Items[i];

                if (!string.IsNullOrEmpty(item.Handle) && !found.Contains(item.Handle) && item.Touches(box))
                {
                    found.Add(item.Handle);
                }
            }

            var handles = new List<string>(found);
            handles.Sort(StringComparer.Ordinal);
            return handles;
        }

        /// <summary>
        /// Gets the items whose box meets a box, in the order they are drawn, as object snaps ask for what lies under the
        /// cursor's aperture. Items with no handle are left out.
        /// </summary>
        /// <param name="box">The box, in drawing units.</param>
        /// <returns>The items.</returns>
        public IReadOnlyList<DisplayItem2> ItemsNear(DisplayBox box)
        {
            var indices = new List<int>();

            foreach (int i in Candidates(box))
            {
                DisplayItem2 item = _list.Items[i];

                if (!string.IsNullOrEmpty(item.Handle) && item.Bounds.Intersects(box))
                {
                    indices.Add(i);
                }
            }

            indices.Sort();
            var items = new List<DisplayItem2>(indices.Count);

            foreach (int i in indices)
            {
                items.Add(_list.Items[i]);
            }

            return items;
        }

        /// <summary>
        /// Gets the items an entity is drawn as, by their place in <see cref="List"/>, in the order they are drawn: a block
        /// of 40 lines inserted once gives 40 places. The canvas draws a hovered or selected entity again over the drawing
        /// from them, without looking at the other items.
        /// </summary>
        /// <param name="handle">The handle of the entity.</param>
        /// <returns>The places, ascending; empty where the entity draws nothing.</returns>
        public IReadOnlyList<int> IndicesOf(string handle)
        {
            return PlacesOf(handle);
        }

        /// <summary>
        /// Gets the items whose box meets a box, by their place in <see cref="List"/>, in the order they are drawn, items with
        /// no handle among them: what the canvas draws of a view. A view of a tenth of a drawing of 100 000 items looks at
        /// about a tenth of them.
        /// </summary>
        /// <param name="box">The box, in drawing units.</param>
        /// <returns>The places, ascending.</returns>
        public IReadOnlyList<int> IndicesIn(DisplayBox box)
        {
            var found = new List<int>();

            if (box.IsEmpty)
            {
                return found;
            }

            IReadOnlyList<DisplayItem2> items = _list.Items;

            if (box.Contains(_list.Bounds))
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Bounds.Intersects(box))
                    {
                        found.Add(i);
                    }
                }

                return found;
            }

            CellRange(box, out int ix0, out int iy0, out int ix1, out int iy1);

            if ((ix1 - ix0 + 1) * (iy1 - iy0 + 1) * 16 <= _n * _n)
            {
                // A view of a sixteenth of the grid or less: the items met, sorted and each kept once, rather than a flag for
                // every item of the list, which on a drawing of a million entities would be megabytes a frame.
                foreach (int i in _large)
                {
                    if (items[i].Bounds.Intersects(box))
                    {
                        found.Add(i);
                    }
                }

                for (int iy = iy0; iy <= iy1; iy++)
                {
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        int c = iy * _n + ix;

                        for (int k = _cellStart[c]; k < _cellStart[c + 1]; k++)
                        {
                            if (items[_cellItems[k]].Bounds.Intersects(box))
                            {
                                found.Add(_cellItems[k]);
                            }
                        }
                    }
                }

                found.Sort();
                int kept = 0;

                for (int k = 0; k < found.Count; k++)
                {
                    if (kept == 0 || found[k] != found[kept - 1])
                    {
                        found[kept++] = found[k];
                    }
                }

                found.RemoveRange(kept, found.Count - kept);
                return found;
            }

            var seen = new bool[items.Count];

            foreach (int i in _large)
            {
                if (!seen[i] && items[i].Bounds.Intersects(box))
                {
                    seen[i] = true;
                    found.Add(i);
                }
            }

            for (int iy = iy0; iy <= iy1; iy++)
            {
                for (int ix = ix0; ix <= ix1; ix++)
                {
                    int c = iy * _n + ix;

                    for (int k = _cellStart[c]; k < _cellStart[c + 1]; k++)
                    {
                        int i = _cellItems[k];

                        if (!seen[i] && items[i].Bounds.Intersects(box))
                        {
                            seen[i] = true;
                            found.Add(i);
                        }
                    }
                }
            }

            found.Sort();
            return found;
        }

        private IEnumerable<int> Candidates(DisplayBox box)
        {
            var seen = new HashSet<int>();

            foreach (int i in _large)
            {
                if (seen.Add(i))
                {
                    yield return i;
                }
            }

            if (box.IsEmpty)
            {
                yield break;
            }

            CellRange(box, out int ix0, out int iy0, out int ix1, out int iy1);

            for (int iy = iy0; iy <= iy1; iy++)
            {
                for (int ix = ix0; ix <= ix1; ix++)
                {
                    int c = iy * _n + ix;

                    for (int k = _cellStart[c]; k < _cellStart[c + 1]; k++)
                    {
                        int i = _cellItems[k];

                        if (seen.Add(i))
                        {
                            yield return i;
                        }
                    }
                }
            }
        }

        private void CellRange(DisplayBox box, out int ix0, out int iy0, out int ix1, out int iy1)
        {
            ix0 = Clamp((int)Math.Floor((box.MinX - _x0) / _cellW));
            iy0 = Clamp((int)Math.Floor((box.MinY - _y0) / _cellH));
            ix1 = Clamp((int)Math.Floor((box.MaxX - _x0) / _cellW));
            iy1 = Clamp((int)Math.Floor((box.MaxY - _y0) / _cellH));
        }

        private int Clamp(int cell) => cell < 0 ? 0 : cell >= _n ? _n - 1 : cell;

        /// <summary>
        /// The runs of a display list a document made by handle: each entity's items stand together, so an entity is found by
        /// its run, not item by item. Whole only where the runs hold every item and each item carries its run's handle.
        /// </summary>
        private sealed class RunsByHandle
        {
            // The first run of each handle, and the next run of the same handle after each (-1 for none); looked up only, never enumerated.
            private readonly Dictionary<string, int> _first = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private readonly int[] _next;
            private readonly IReadOnlyList<DisplayRun> _runs;

            public RunsByHandle(DisplayList2 list)
            {
                _runs = list.Runs;
                _next = new int[_runs.Count];
                IReadOnlyList<DisplayItem2> items = list.Items;
                int covered = 0;

                for (int r = 0; r < _runs.Count; r++)
                {
                    DisplayRun run = _runs[r];
                    _next[r] = -1;

                    if (run.Start != covered)
                    {
                        return;
                    }

                    covered += run.Count;

                    for (int i = run.Start; i < run.Start + run.Count; i++)
                    {
                        string handle = items[i].Handle;

                        if (!ReferenceEquals(handle, run.Handle) && !(string.IsNullOrEmpty(handle) && string.IsNullOrEmpty(run.Handle)) && !string.Equals(handle, run.Handle, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }

                    if (string.IsNullOrEmpty(run.Handle) || run.Count == 0)
                    {
                        continue;
                    }

                    if (_first.TryGetValue(run.Handle, out int before))
                    {
                        // Two entities of one handle, as a damaged file may hold: the later run chained after the earlier.
                        while (_next[before] >= 0)
                        {
                            before = _next[before];
                        }

                        _next[before] = r;
                    }
                    else
                    {
                        _first[run.Handle] = r;
                    }
                }

                IsWhole = covered == items.Count;
            }

            public bool IsWhole { get; }

            public IReadOnlyList<int> PlacesOf(string handle)
            {
                if (!_first.TryGetValue(handle, out int r))
                {
                    return Array.Empty<int>();
                }

                int count = 0;

                for (int k = r; k >= 0; k = _next[k])
                {
                    count += _runs[k].Count;
                }

                var places = new int[count];
                int at = 0;

                for (int k = r; k >= 0; k = _next[k])
                {
                    for (int i = 0; i < _runs[k].Count; i++)
                    {
                        places[at++] = _runs[k].Start + i;
                    }
                }

                return places;
            }
        }
    }
}
