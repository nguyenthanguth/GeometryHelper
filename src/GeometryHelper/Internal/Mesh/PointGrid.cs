using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Items filed under a point, so that the ones whose point lies within the tolerance of another are found
    /// without looking at every item.
    /// </summary>
    /// <remarks>
    /// Space is cut into cubes twice the tolerance wide. Two points within the tolerance of each other differ by
    /// less than a cube along every axis, so they sit in the same cube or in neighbouring ones, and a search of the
    /// 27 cubes about a point finds every item that could match it — along with some that do not, which the caller
    /// rules out with the exact test it would have made anyway. That is what lets a comparison of every pair keep
    /// its answer while only looking at the pairs that could agree.
    /// </remarks>
    internal sealed class PointGrid
    {
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly double _cellSize;

        public PointGrid(Tolerance tolerance)
        {
            _cellSize = tolerance.EqualPoint > 0.0 ? tolerance.EqualPoint * 2.0 : 1E-9;
        }

        /// <summary>
        /// Files an item under a point.
        /// </summary>
        public void Add(GeoPoint3 point, int item)
        {
            long key = Key(Cell(point.X), Cell(point.Y), Cell(point.Z));

            if (!_cells.TryGetValue(key, out List<int> items))
            {
                items = new List<int>();
                _cells[key] = items;
            }

            items.Add(item);
        }

        /// <summary>
        /// Adds to a list every item filed within a cube of a point, in ascending order and each once: all the
        /// items whose point lies within the tolerance of it, and possibly others.
        /// </summary>
        public void Near(GeoPoint3 point, List<int> found)
        {
            found.Clear();

            long x = Cell(point.X), y = Cell(point.Y), z = Cell(point.Z);

            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    for (long dz = -1; dz <= 1; dz++)
                    {
                        if (_cells.TryGetValue(Key(x + dx, y + dy, z + dz), out List<int> items))
                        {
                            found.AddRange(items);
                        }
                    }
                }
            }

            // Two cubes can share a key, and then their items come twice.
            found.Sort();

            int kept = 0;
            for (int i = 0; i < found.Count; i++)
            {
                if (i == 0 || found[i] != found[i - 1])
                {
                    found[kept++] = found[i];
                }
            }

            found.RemoveRange(kept, found.Count - kept);
        }

        private long Cell(double value) => (long)Math.Floor(value / _cellSize);

        private static long Key(long x, long y, long z)
        {
            unchecked
            {
                return (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L);
            }
        }
    }
}
