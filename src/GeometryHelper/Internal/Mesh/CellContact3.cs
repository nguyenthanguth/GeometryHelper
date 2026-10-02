using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Which cells of a grid share part of a face: cells cut apart by one plane with no joint between, whose material meets on
    /// it over some area.
    /// <para>
    /// Neighbours are found by where they stand, not by their indexes: a cut moved onto a corner, or one not made, leaves a
    /// cell meeting one two places on along the axis, and each slab and bar is cut to its own corners, so that a cell can
    /// meet one a place over across it. A cell's material strays from its line on each axis by no more than the snap
    /// distance and a needle's point, which the cells themselves say; only the cells that far along each line are tried.
    /// </para>
    /// </summary>
    internal static class CellContact3
    {
        /// <summary>
        /// The neighbours of every cell, by their places in the grid's list, in ascending order.
        /// </summary>
        /// <param name="cells">The cells.</param>
        /// <param name="starts">Where each cell starts along each axis, from the frame's origin.</param>
        /// <param name="ends">Where each cell ends along each axis, from the frame's origin.</param>
        /// <param name="frame">The grid's frame.</param>
        /// <param name="joint">The gap between neighbouring cells; with one, no cell meets another.</param>
        /// <param name="tolerance">The tolerance the cells were cut within.</param>
        public static int[][] Adjacency(GeoCell3[] cells, double[][] starts, double[][] ends, GeoCoordinateSystem3 frame, double joint, Tolerance tolerance)
        {
            var adjacent = new List<int>[cells.Length];

            for (int n = 0; n < cells.Length; n++)
            {
                adjacent[n] = new List<int>();
            }

            if (joint == 0.0 && cells.Length > 1)
            {
                new Search(cells, starts, ends, frame, tolerance).Run(adjacent);
            }

            var result = new int[cells.Length][];

            for (int n = 0; n < result.Length; n++)
            {
                List<int> list = adjacent[n];
                list.Sort();
                int kept = 0;

                for (int i = 0; i < list.Count; i++)
                {
                    if (kept == 0 || list[kept - 1] != list[i])
                    {
                        list[kept++] = list[i];
                    }
                }

                list.RemoveRange(kept, list.Count - kept);
                result[n] = list.ToArray();
            }

            return result;
        }

        /// <summary>
        /// The search for neighbours through one grid: each cell's extent in the grid's frame, the cells near each line, and
        /// the faces each cell has on the planes bounding it, laid out as they are first asked for.
        /// </summary>
        private sealed class Search
        {
            private readonly GeoCell3[] _cells;
            private readonly double[][] _starts;
            private readonly double[][] _ends;
            private readonly GeoCoordinateSystem3 _frame;
            private readonly Tolerance _tolerance;
            private readonly double[][] _min;
            private readonly double[][] _max;
            private readonly int[][] _from;
            private readonly int[][] _to;
            private readonly List<Laid>[] _laid;

            public Search(GeoCell3[] cells, double[][] starts, double[][] ends, GeoCoordinateSystem3 frame, Tolerance tolerance)
            {
                _cells = cells;
                _starts = starts;
                _ends = ends;
                _frame = frame;
                _tolerance = tolerance;
                _min = new double[cells.Length][];
                _max = new double[cells.Length][];
                _laid = new List<Laid>[cells.Length * 6];

                // How far the material of a cell strays from its line on each axis, at the most over the grid.
                var stray = new double[3];

                for (int n = 0; n < cells.Length; n++)
                {
                    Extent(n);

                    for (int a = 0; a < 3; a++)
                    {
                        int index = IndexOf(cells[n], a);
                        stray[a] = Math.Max(stray[a], Math.Max(starts[a][index] - _min[n][a], _max[n][a] - ends[a][index]));
                    }
                }

                // The lines along each axis whose cells can reach those of a line: their spans grown by the stray overlap.
                _from = new int[3][];
                _to = new int[3][];

                for (int a = 0; a < 3; a++)
                {
                    int count = starts[a].Length;
                    double grow = stray[a] + tolerance.EqualPoint;
                    _from[a] = new int[count];
                    _to[a] = new int[count];

                    for (int index = 0; index < count; index++)
                    {
                        int from = index;
                        int to = index;

                        while (from > 0 && ends[a][from - 1] + grow > starts[a][index] - grow)
                        {
                            from--;
                        }

                        while (to < count - 1 && starts[a][to + 1] - grow < ends[a][index] + grow)
                        {
                            to++;
                        }

                        _from[a][index] = from;
                        _to[a][index] = to;
                    }
                }
            }

            public void Run(List<int>[] adjacent)
            {
                double near = _tolerance.EqualPoint;

                for (int a = 0; a < 3; a++)
                {
                    int u = (a + 1) % 3;
                    int v = (a + 2) % 3;

                    // The cells a cut bounds from below, filed by their lines across the axis and sorted by where the cut stands.
                    var filed = new Dictionary<(int, int), List<(double At, int Cell)>>();

                    for (int m = 0; m < _cells.Length; m++)
                    {
                        double low = _cells[m].Low[a];

                        if (double.IsNaN(low))
                        {
                            continue;
                        }

                        (int, int) key = (IndexOf(_cells[m], u), IndexOf(_cells[m], v));

                        if (!filed.TryGetValue(key, out List<(double, int)> list))
                        {
                            list = new List<(double, int)>();
                            filed.Add(key, list);
                        }

                        list.Add((low, m));
                    }

                    foreach (List<(double At, int Cell)> list in filed.Values)
                    {
                        list.Sort((x, y) => x.At.CompareTo(y.At));
                    }

                    for (int n = 0; n < _cells.Length; n++)
                    {
                        double high = _cells[n].High[a];

                        if (double.IsNaN(high))
                        {
                            continue;
                        }

                        int iu = IndexOf(_cells[n], u);
                        int iv = IndexOf(_cells[n], v);

                        for (int ju = _from[u][iu]; ju <= _to[u][iu]; ju++)
                        {
                            for (int jv = _from[v][iv]; jv <= _to[v][iv]; jv++)
                            {
                                if (!filed.TryGetValue((ju, jv), out List<(double At, int Cell)> list))
                                {
                                    continue;
                                }

                                // Cut apart by one plane: the one bounding the first from above bounds the other from below.
                                for (int s = LowerBound(list, high - near); s < list.Count && list[s].At <= high + near; s++)
                                {
                                    int m = list[s].Cell;

                                    if (m != n && Across(n, m, u, v) && Meet(n, m, a, u, v))
                                    {
                                        adjacent[n].Add(m);
                                        adjacent[m].Add(n);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            private static int IndexOf(GeoCell3 cell, int axis) => axis == 0 ? cell.I : axis == 1 ? cell.J : cell.K;

            private static int LowerBound(List<(double At, int Cell)> list, double at)
            {
                int low = 0, high = list.Count;

                while (low < high)
                {
                    int middle = low + (high - low) / 2;

                    if (list[middle].At < at)
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

            /// <summary>
            /// The extent of a cell's material along each axis of the frame: a box cell's own, a body's from its corners.
            /// </summary>
            private void Extent(int n)
            {
                GeoCell3 cell = _cells[n];
                var min = new double[3];
                var max = new double[3];

                if (cell.Material != null)
                {
                    for (int a = 0; a < 3; a++)
                    {
                        min[a] = cell.Low[a];
                        max[a] = cell.High[a];
                    }
                }
                else
                {
                    for (int a = 0; a < 3; a++)
                    {
                        min[a] = double.MaxValue;
                        max[a] = double.MinValue;
                    }

                    foreach (GeoFace3 face in cell.Solid.Faces)
                    {
                        foreach (GeoPoint3 corner in face.Boundary.Vertices)
                        {
                            GeoPoint3 local = _frame.ToLocal(corner);
                            min[0] = Math.Min(min[0], local.X);
                            max[0] = Math.Max(max[0], local.X);
                            min[1] = Math.Min(min[1], local.Y);
                            max[1] = Math.Max(max[1], local.Y);
                            min[2] = Math.Min(min[2], local.Z);
                            max[2] = Math.Max(max[2], local.Z);
                        }
                    }
                }

                _min[n] = min;
                _max[n] = max;
            }

            /// <summary>
            /// Whether the extents of two cells overlap across the plane between them, both ways.
            /// </summary>
            private bool Across(int n, int m, int u, int v)
                => Math.Min(_max[n][u], _max[m][u]) > Math.Max(_min[n][u], _min[m][u])
                && Math.Min(_max[n][v], _max[m][v]) > Math.Max(_min[n][v], _min[m][v]);

            /// <summary>
            /// Whether two cells cut apart by a plane square to an axis meet on it over more than the point tolerance squared.
            /// </summary>
            private bool Meet(int below, int above, int axis, int u, int v)
            {
                double least = _tolerance.EqualPoint * _tolerance.EqualPoint;

                if (_cells[below].Material != null && _cells[above].Material != null)
                {
                    // Two boxes: their spans across the plane overlap.
                    double area = (Math.Min(_max[below][u], _max[above][u]) - Math.Max(_min[below][u], _min[above][u]))
                        * (Math.Min(_max[below][v], _max[above][v]) - Math.Max(_min[below][v], _min[above][v]));
                    return area > least;
                }

                List<Laid> lower = LaidOn(below, axis, 1);
                List<Laid> upper = LaidOn(above, axis, 0);
                double shared = 0.0;

                foreach (Laid a in lower)
                {
                    foreach (Laid b in upper)
                    {
                        if (!(Math.Min(a.MaxU, b.MaxU) > Math.Max(a.MinU, b.MinU) && Math.Min(a.MaxV, b.MaxV) > Math.Max(a.MinV, b.MinV)))
                        {
                            continue;
                        }

                        shared += a.Convex && b.Convex ? ConvexOverlap(a.Ring, b.Ring) : Overlap(a.Face, b.Face);

                        if (shared > least)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// The faces of a cell on the plane bounding it along an axis, from above (side 1) or below (side 0), facing out of
            /// the cell, laid out across the axis.
            /// </summary>
            private List<Laid> LaidOn(int n, int axis, int side)
            {
                int slot = (n * 3 + axis) * 2 + side;

                if (_laid[slot] != null)
                {
                    return _laid[slot];
                }

                GeoCell3 cell = _cells[n];
                double at = side == 1 ? cell.High[axis] : cell.Low[axis];
                double facing = side == 1 ? 1.0 : -1.0;
                GeoVector3 direction = axis == 0 ? _frame.XAxis : axis == 1 ? _frame.YAxis : _frame.ZAxis;
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;
                var laid = new List<Laid>();

                foreach (GeoFace3 face in cell.Solid.Faces)
                {
                    if (face.Normal.DotProduct(direction) * facing < 1.0 - 1E-6 || !Lies(face, axis, at))
                    {
                        continue;
                    }

                    GeoPoint2[] boundary = Ring(face.Boundary.Vertices, u, v);

                    if (boundary == null)
                    {
                        continue;
                    }

                    var holes = new List<GeoPolygon2>(face.Holes.Count);

                    foreach (GeoPolygon3 hole in face.Holes)
                    {
                        GeoPoint2[] ring = Ring(hole.Vertices, u, v);

                        if (ring != null)
                        {
                            holes.Add(new GeoPolygon2(ring, ring.Length));
                        }
                    }

                    laid.Add(new Laid(new GeoFace2(new GeoPolygon2(boundary, boundary.Length), holes), boundary, holes.Count == 0 && IsConvex(boundary)));
                }

                _laid[slot] = laid;
                return laid;
            }

            private bool Lies(GeoFace3 face, int axis, double at)
            {
                foreach (GeoPoint3 corner in face.Boundary.Vertices)
                {
                    GeoPoint3 local = _frame.ToLocal(corner);
                    double d = axis == 0 ? local.X : axis == 1 ? local.Y : local.Z;

                    if (Math.Abs(d - at) > _tolerance.EqualPoint)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// A ring laid out across an axis, counter-clockwise, corners standing on the one before dropped; null when fewer
            /// than three are left.
            /// </summary>
            private GeoPoint2[] Ring(IReadOnlyList<GeoPoint3> ring, int u, int v)
            {
                var flat = new GeoPoint2[ring.Count];

                for (int i = 0; i < flat.Length; i++)
                {
                    GeoPoint3 local = _frame.ToLocal(ring[i]);
                    flat[i] = new GeoPoint2(u == 0 ? local.X : u == 1 ? local.Y : local.Z, v == 0 ? local.X : v == 1 ? local.Y : local.Z);
                }

                GeoPolygon2 polygon = MeshLift3.LayOutPolygon(flat);

                if (polygon == null)
                {
                    return null;
                }

                var points = new GeoPoint2[polygon.VertexCount];

                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = polygon[i];
                }

                if (polygon.SignedArea < 0.0)
                {
                    Array.Reverse(points);
                }

                return points;
            }

            private double Overlap(GeoFace2 a, GeoFace2 b)
            {
                double area = 0.0;

                foreach (GeoFace2 common in Boolean2.Intersect(a, b, _tolerance))
                {
                    area += common.Area;
                }

                return area;
            }
        }

        /// <summary>
        /// A face of a cell on a plane bounding it, laid out across the plane's axis.
        /// </summary>
        private sealed class Laid
        {
            public Laid(GeoFace2 face, GeoPoint2[] ring, bool convex)
            {
                Face = face;
                Ring = ring;
                Convex = convex;
                MinU = double.MaxValue;
                MinV = double.MaxValue;
                MaxU = double.MinValue;
                MaxV = double.MinValue;

                foreach (GeoPoint2 point in ring)
                {
                    MinU = Math.Min(MinU, point.X);
                    MinV = Math.Min(MinV, point.Y);
                    MaxU = Math.Max(MaxU, point.X);
                    MaxV = Math.Max(MaxV, point.Y);
                }
            }

            public GeoFace2 Face { get; }

            public GeoPoint2[] Ring { get; }

            public bool Convex { get; }

            public double MinU { get; }

            public double MinV { get; }

            public double MaxU { get; }

            public double MaxV { get; }
        }

        /// <summary>
        /// Whether a counter-clockwise ring turns right at none of its corners.
        /// </summary>
        private static bool IsConvex(GeoPoint2[] ring)
        {
            int n = ring.Length;

            for (int i = 0; i < n; i++)
            {
                GeoPoint2 a = ring[i];
                GeoPoint2 b = ring[(i + 1) % n];
                GeoPoint2 c = ring[(i + 2) % n];

                if ((b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X) < 0.0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The area two convex counter-clockwise rings both cover: the one clipped by each side of the other in turn.
        /// </summary>
        private static double ConvexOverlap(GeoPoint2[] subject, GeoPoint2[] clip)
        {
            var output = new List<GeoPoint2>(subject);
            var input = new List<GeoPoint2>(subject.Length + clip.Length);

            for (int c = 0; c < clip.Length && output.Count > 0; c++)
            {
                GeoPoint2 p = clip[c];
                GeoPoint2 q = clip[(c + 1) % clip.Length];
                List<GeoPoint2> swap = input;
                input = output;
                output = swap;
                output.Clear();

                for (int i = 0; i < input.Count; i++)
                {
                    GeoPoint2 s = input[i];
                    GeoPoint2 e = input[(i + 1) % input.Count];
                    double ds = Side(p, q, s);
                    double de = Side(p, q, e);

                    if (ds >= 0.0)
                    {
                        output.Add(s);
                    }

                    if ((ds >= 0.0) != (de >= 0.0))
                    {
                        double t = ds / (ds - de);
                        output.Add(new GeoPoint2(s.X + t * (e.X - s.X), s.Y + t * (e.Y - s.Y)));
                    }
                }
            }

            if (output.Count < 3)
            {
                return 0.0;
            }

            // Measured from the first corner, so that a face far out costs no precision.
            GeoPoint2 first = output[0];
            double twice = 0.0;

            for (int i = 1; i + 1 < output.Count; i++)
            {
                twice += (output[i].X - first.X) * (output[i + 1].Y - first.Y) - (output[i].Y - first.Y) * (output[i + 1].X - first.X);
            }

            return Math.Max(0.0, 0.5 * twice);
        }

        /// <summary>
        /// Twice the signed area of the triangle a side and a point make: positive with the point on the side's left.
        /// </summary>
        private static double Side(GeoPoint2 p, GeoPoint2 q, GeoPoint2 s) => (q.X - p.X) * (s.Y - p.Y) - (q.Y - p.Y) * (s.X - p.X);
    }
}
