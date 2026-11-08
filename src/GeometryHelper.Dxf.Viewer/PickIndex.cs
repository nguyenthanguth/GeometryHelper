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
        private readonly List<int>[] _cells;
        private readonly List<int> _large = new List<int>();
        private readonly Dictionary<string, List<int>> _byHandle = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly int _n;
        private readonly double _x0;
        private readonly double _y0;
        private readonly double _cellW;
        private readonly double _cellH;

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
            _cells = new List<int>[_n * _n];

            for (int i = 0; i < list.Count; i++)
            {
                DisplayItem2 item = list.Items[i];

                if (item.Handle != null && item.Handle.Length > 0)
                {
                    if (!_byHandle.TryGetValue(item.Handle, out List<int> group))
                    {
                        group = new List<int>();
                        _byHandle[item.Handle] = group;
                    }

                    group.Add(i);
                }

                DisplayBox box = item.Bounds;

                if (box.IsEmpty)
                {
                    continue;
                }

                CellRange(box, out int ix0, out int iy0, out int ix1, out int iy1);

                if ((ix1 - ix0 + 1) * (iy1 - iy0 + 1) > _n * _n / 4 + 4)
                {
                    _large.Add(i);
                    continue;
                }

                for (int iy = iy0; iy <= iy1; iy++)
                {
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        int c = iy * _n + ix;
                        (_cells[c] ?? (_cells[c] = new List<int>())).Add(i);
                    }
                }
            }
        }

        /// <summary>Gets the display list this index was made from.</summary>
        public DisplayList2 List => _list;

        /// <summary>
        /// Gets the entities whose drawing lies within a distance of a point, the nearest first. The distance is in drawing
        /// units, so a caller working in pixels divides by the zoom first.
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
            return handles;
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

                foreach (int i in _byHandle[handle])
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
                    List<int> cell = _cells[iy * _n + ix];

                    if (cell == null)
                    {
                        continue;
                    }

                    foreach (int i in cell)
                    {
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
    }
}
