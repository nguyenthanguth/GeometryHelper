using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Internal.Planar
{
    /// <summary>
    /// Segments bucketed on a grid, to tell quickly whether an edge lies along one of them, and which of them
    /// come near a point or an edge.
    /// <para>
    /// The cells are as wide as the root mean square of the segments' reach (the larger side of each one's box,
    /// margin included), so that a long thin chain of short segments is not piled into a few cells, and a few
    /// long segments do not fill many: the segments take at most four cells each on average, whatever their
    /// lengths. The grid is laid when it is first asked, once every segment is in; a few segments are simply
    /// all walked.
    /// </para>
    /// </summary>
    internal sealed class SegmentIndex
    {
        /// <summary>
        /// Up to this many segments, every query walks them all: a grid costs more to lay than it saves.
        /// </summary>
        private const int FewSegments = 16;

        private readonly List<GeoPoint2> _from = new List<GeoPoint2>();
        private readonly List<GeoPoint2> _to = new List<GeoPoint2>();
        private readonly List<double> _margins = new List<double>();
        private readonly Dictionary<(long, long), List<int>> _cells = new Dictionary<(long, long), List<int>>();
        private readonly double _smallest;
        private double _cell;
        private List<int> _all;

        /// <param name="extent">The largest coordinate the segments reach, which bounds how fine the grid gets.</param>
        public SegmentIndex(double extent)
        {
            _smallest = Math.Max(extent, 1.0) * 1e-9;
        }

        /// <summary>
        /// The number of segments added.
        /// </summary>
        public int Count => _from.Count;

        /// <summary>
        /// Adds a segment, found by the queries from points within the margin of it.
        /// </summary>
        public void Add(GeoPoint2 a, GeoPoint2 b, double margin)
        {
            _from.Add(a);
            _to.Add(b);
            _margins.Add(margin);
        }

        public GeoPoint2 From(int id) => _from[id];

        public GeoPoint2 To(int id) => _to[id];

        /// <summary>
        /// Whether an edge lies along one of the segments: both its ends within the margin of it.
        /// </summary>
        public bool Covers(GeoPoint2 p, GeoPoint2 q, double margin)
        {
            Lay();
            GeoPoint2 middle = PlanarMath.Midpoint(p, q);
            List<int> list = _all;

            if (list == null && !_cells.TryGetValue((Cell(middle.X), Cell(middle.Y)), out list))
            {
                return false;
            }

            foreach (int id in list)
            {
                if (DistanceToSegment(p, _from[id], _to[id]) <= margin && DistanceToSegment(q, _from[id], _to[id]) <= margin)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The segments added with a margin that reaches a point, and perhaps a few more, in the order they were
        /// added.
        /// </summary>
        public List<int> Near(GeoPoint2 point)
        {
            Lay();

            if (_all != null)
            {
                return _all;
            }

            return _cells.TryGetValue((Cell(point.X), Cell(point.Y)), out List<int> list) ? list : new List<int>();
        }

        /// <summary>
        /// The segments added with a margin that reaches some point of an edge, and perhaps a few more, each once
        /// and in the order they were added.
        /// </summary>
        public List<int> Near(GeoPoint2 p, GeoPoint2 q)
        {
            Lay();

            if (_all != null)
            {
                return _all;
            }

            List<int> found = new List<int>();
            long x0 = Cell(Math.Min(p.X, q.X));
            long x1 = Cell(Math.Max(p.X, q.X));
            long y0 = Cell(Math.Min(p.Y, q.Y));
            long y1 = Cell(Math.Max(p.Y, q.Y));

            for (long x = x0; x <= x1; x++)
            {
                for (long y = y0; y <= y1; y++)
                {
                    if (_cells.TryGetValue((x, y), out List<int> list))
                    {
                        found.AddRange(list);
                    }
                }
            }

            if (x0 == x1 && y0 == y1)
            {
                return found;
            }

            found.Sort();
            int kept = 0;

            for (int i = 0; i < found.Count; i++)
            {
                if (kept == 0 || found[i] != found[kept - 1])
                {
                    found[kept++] = found[i];
                }
            }

            found.RemoveRange(kept, found.Count - kept);
            return found;
        }

        public static double DistanceToSegment(GeoPoint2 point, GeoPoint2 a, GeoPoint2 b)
        {
            GeoVector2 direction = b - a;
            double lengthSquared = direction.LengthSquared;
            double t = lengthSquared > 0.0 ? Math.Max(0.0, Math.Min(1.0, (point - a).Dot(direction) / lengthSquared)) : 0.0;
            return point.DistanceTo(a + direction * t);
        }

        /// <summary>
        /// Lays the grid over the segments added, unless it is laid already.
        /// </summary>
        private void Lay()
        {
            if (_cell > 0.0 || _all != null)
            {
                return;
            }

            if (_from.Count <= FewSegments)
            {
                _all = new List<int>(_from.Count);

                for (int id = 0; id < _from.Count; id++)
                {
                    _all.Add(id);
                }

                return;
            }

            double sum = 0.0;

            for (int id = 0; id < _from.Count; id++)
            {
                double reach = Math.Max(Math.Abs(_to[id].X - _from[id].X), Math.Abs(_to[id].Y - _from[id].Y)) + 2.0 * _margins[id];
                sum += reach * reach;
            }

            _cell = Math.Max(Math.Sqrt(sum / Math.Max(_from.Count, 1)), _smallest);

            for (int id = 0; id < _from.Count; id++)
            {
                GeoPoint2 a = _from[id];
                GeoPoint2 b = _to[id];
                double margin = _margins[id];
                long x0 = Cell(Math.Min(a.X, b.X) - margin);
                long x1 = Cell(Math.Max(a.X, b.X) + margin);
                long y0 = Cell(Math.Min(a.Y, b.Y) - margin);
                long y1 = Cell(Math.Max(a.Y, b.Y) + margin);

                for (long x = x0; x <= x1; x++)
                {
                    for (long y = y0; y <= y1; y++)
                    {
                        if (!_cells.TryGetValue((x, y), out List<int> list))
                        {
                            list = new List<int>();
                            _cells.Add((x, y), list);
                        }

                        list.Add(id);
                    }
                }
            }
        }

        private long Cell(double value) => (long)Math.Floor(value / _cell);
    }
}
