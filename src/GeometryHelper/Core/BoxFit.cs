using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The smallest rectangle round points in the plane, and a small box round points in space.
    /// </summary>
    /// <remarks>
    /// A rectangle of least area round a convex polygon has a side along one of its edges, so trying every edge
    /// of the hull finds it exactly. In space the box is tried on every face of the hull, each face's rectangle
    /// found the same way: the smallest box of all for a block, a prism, or anything with a flat face to stand
    /// on, and close to it otherwise.
    /// </remarks>
    internal static class BoxFit
    {
        internal static GeoRectangle2 Rectangle(IEnumerable<GeoPoint2> points, Tolerance tolerance) => Rectangle(points, tolerance, null);

        /// <summary>
        /// Gets the rectangle of least area round points, the first found of any as small, and, where a list is given for them, every
        /// rectangle tried, one along each edge of their hull, that is as small to rounding, in the order tried.
        /// </summary>
        private static GeoRectangle2 Rectangle(IEnumerable<GeoPoint2> points, Tolerance tolerance, List<GeoRectangle2> tried)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var all = new List<GeoPoint2>(points);

            if (all.Count == 0)
            {
                throw new ArgumentException("There are no points to fit round.", nameof(points));
            }

            IReadOnlyList<GeoPoint2> corners = ConvexHull2.TryOf(all, out GeoPolygon2 hull, tolerance) ? hull.Vertices : all;
            var directions = new List<GeoVector2>();

            if (hull != null)
            {
                for (int i = 0; i < corners.Count; i++)
                {
                    directions.Add(corners[i].GetVectorTo(corners[(i + 1) % corners.Count]));
                }
            }
            else
            {
                // All in one line, or all one point: the line through the two furthest apart.
                GeoPoint2 far = Farthest(all, all[0]);
                directions.Add(Farthest(all, far).GetVectorTo(far));
            }

            GeoRectangle2 best = default;
            double least = double.PositiveInfinity;
            var areas = tried != null ? new double[directions.Count] : null;

            for (int i = 0; i < directions.Count; i++)
            {
                double area = Spans(corners, directions[i], out GeoVector2 along, out GeoVector2 across, out double minS, out double maxS, out double minT, out double maxT);

                if (areas != null)
                {
                    areas[i] = area;
                }

                if (area < least || double.IsPositiveInfinity(least))
                {
                    least = area;
                    best = Around(along, across, minS, maxS, minT, maxT);
                }
            }

            // Those as small as the least to rounding, which the box weighs by their sides; any larger loses to the least.
            for (int i = 0; areas != null && i < directions.Count; i++)
            {
                if (!(Math.Abs(areas[i] - least) > 1E-9 * Math.Max(Math.Abs(areas[i]), Math.Abs(least))))
                {
                    Spans(corners, directions[i], out GeoVector2 along, out GeoVector2 across, out double minS, out double maxS, out double minT, out double maxT);
                    tried.Add(Around(along, across, minS, maxS, minT, maxT));
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the area of the rectangle round some corners along a direction, and how far they reach along it and across it.
        /// </summary>
        private static double Spans(IReadOnlyList<GeoPoint2> corners, GeoVector2 direction, out GeoVector2 along, out GeoVector2 across, out double minS, out double maxS, out double minT, out double maxT)
        {
            double length = direction.Length;
            along = length > 0.0 ? direction.Multiply(1.0 / length) : new GeoVector2(1.0, 0.0);
            across = new GeoVector2(-along.Y, along.X);
            minS = double.PositiveInfinity;
            maxS = double.NegativeInfinity;
            minT = double.PositiveInfinity;
            maxT = double.NegativeInfinity;

            foreach (GeoPoint2 corner in corners)
            {
                double s = corner.X * along.X + corner.Y * along.Y;
                double t = corner.X * across.X + corner.Y * across.Y;

                minS = Math.Min(minS, s);
                maxS = Math.Max(maxS, s);
                minT = Math.Min(minT, t);
                maxT = Math.Max(maxT, t);
            }

            return (maxS - minS) * (maxT - minT);
        }

        /// <summary>
        /// Gets the rectangle a direction and the reach of some corners along it and across it make.
        /// </summary>
        private static GeoRectangle2 Around(GeoVector2 along, GeoVector2 across, double minS, double maxS, double minT, double maxT)
        {
            double s0 = (minS + maxS) * 0.5;
            double t0 = (minT + maxT) * 0.5;
            var centre = new GeoPoint2(along.X * s0 + across.X * t0, along.Y * s0 + across.Y * t0);

            return new GeoRectangle2(centre, maxS - minS, maxT - minT, Math.Atan2(along.Y, along.X));
        }

        internal static GeoObb3 Box(IEnumerable<GeoPoint3> points, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var all = new List<GeoPoint3>(points);

            if (all.Count == 0)
            {
                throw new ArgumentException("There are no points to fit round.", nameof(points));
            }

            var corners = new List<GeoPoint3>();
            var normals = new List<GeoVector3>();
            HullCorners once = null;

            if (ConvexHull3.TryOf(all, out GeoSolid3 hull, tolerance))
            {
                foreach (GeoFace3 face in hull.Faces)
                {
                    normals.Add(face.Boundary.Normal);
                    corners.AddRange(face.Boundary.Vertices);
                }

                once = new HullCorners(hull, tolerance);
            }
            else
            {
                corners = all;
                normals.Add(FlatNormal(all, tolerance));
            }

            GeoObb3 best = null;
            double least = double.PositiveInfinity;
            var tried = new List<GeoRectangle2>();
            var flat = new List<GeoPoint2>(corners.Count);

            foreach (GeoVector3 normal in normals)
            {
                GetBasis(normal, out GeoVector3 u, out GeoVector3 v);

                // A face that cannot stand a box as small as the least so far would lose to it, and is passed over; see HullCorners.
                if (best != null && once != null && once.StandsOnlyLarger(normal, u, v, least))
                {
                    continue;
                }

                if (once == null || !once.TryProject(normal, u, v, flat, out double minN, out double maxN))
                {
                    flat.Clear();
                    minN = double.PositiveInfinity;
                    maxN = double.NegativeInfinity;

                    foreach (GeoPoint3 corner in corners)
                    {
                        var p = new GeoVector3(corner.X, corner.Y, corner.Z);
                        flat.Add(new GeoPoint2(p.DotProduct(u), p.DotProduct(v)));
                        double n = p.DotProduct(normal);
                        minN = Math.Min(minN, n);
                        maxN = Math.Max(maxN, n);
                    }
                }

                // Every rectangle on the face, each along an edge of its hull, is a box to compare: two on one face as large tie
                // as two on two faces do.
                tried.Clear();
                Rectangle(flat, tolerance, tried);
                double depth = maxN - minN;

                foreach (GeoRectangle2 base_ in tried)
                {
                    double volume = base_.Width * base_.Height * depth;

                    if (best == null || Smaller(volume, base_.Width, base_.Height, depth, least, best))
                    {
                        least = volume;
                        double cos = Math.Cos(base_.AngleRad);
                        double sin = Math.Sin(base_.AngleRad);
                        GeoVector3 axisX = u.Multiply(cos).Add(v.Multiply(sin));
                        GeoVector3 axisY = u.Multiply(-sin).Add(v.Multiply(cos));
                        GeoVector3 centre = u.Multiply(base_.Center.X).Add(v.Multiply(base_.Center.Y)).Add(normal.Multiply((minN + maxN) * 0.5));

                        best = new GeoObb3(new GeoPoint3(centre.X, centre.Y, centre.Z), base_.Width, base_.Height, depth, axisX, axisY);
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Whether the box standing on a face of the hull is to be taken over the best so far, standing on the same face or another:
        /// smaller beyond rounding, or as large to rounding and thinner, its sides compared least first, then the middle and then the
        /// longest; the first found where every side ties.
        /// </summary>
        /// <remarks>
        /// Two boxes as large are as good a fit, and the thickness a clash reads from the box is its least side: a prism 40
        /// long on a right triangle with legs 18 and 24 fits a box 18 by 24 by 40 along its legs and one 14.4 by 30 by 40
        /// along its long side, both 17 280, and the one found first, 18 thick, was taken, though the prism fits in 14.4.
        /// Two rectangles on one face as large are compared so too, and a body fits the same box however it is turned: the
        /// tetrahedron on (0, 0, 0), (24, 0, 0), (0, 18, 0) and (0, 0, 40) fits 13.548779 by 27.340984 by 46.647615, where
        /// turned it was given 13.548779 by 30 by 42.513057, as large, the one of the two on its long face found first.
        /// </remarks>
        private static bool Smaller(double volume, double width, double height, double depth, double least, GeoObb3 best)
        {
            if (Math.Abs(volume - least) > 1E-9 * Math.Max(Math.Abs(volume), Math.Abs(least)))
            {
                return volume < least;
            }

            double[] sides = { width, height, depth };
            double[] bests = { best.SizeX, best.SizeY, best.SizeZ };
            Array.Sort(sides);
            Array.Sort(bests);

            for (int i = 0; i < 3; i++)
            {
                if (sides[i] != bests[i])
                {
                    return sides[i] < bests[i];
                }
            }

            return false;
        }

        /// <summary>
        /// A normal to the plane points lie in when they span no volume; any normal to their line, or to nothing,
        /// when they span no area either.
        /// </summary>
        private static GeoVector3 FlatNormal(List<GeoPoint3> all, Tolerance tolerance)
        {
            GeoPoint3 a = Farthest(all, all[0]);
            GeoPoint3 b = Farthest(all, a);
            GeoVector3 along = a.GetVectorTo(b);

            if (!along.TryGetNormal(out GeoVector3 line, tolerance))
            {
                return GeoVector3.ZAxis;
            }

            GeoPoint3 c = all[0];
            double reach = -1.0;

            foreach (GeoPoint3 point in all)
            {
                double off = line.CrossProduct(a.GetVectorTo(point)).Length;

                if (off > reach)
                {
                    reach = off;
                    c = point;
                }
            }

            if (line.CrossProduct(a.GetVectorTo(c)).TryGetNormal(out GeoVector3 normal, tolerance))
            {
                return normal;
            }

            GetBasis(line, out GeoVector3 across, out _);
            return across;
        }

        private static GeoPoint2 Farthest(List<GeoPoint2> points, GeoPoint2 from)
        {
            GeoPoint2 best = from;
            double most = -1.0;

            foreach (GeoPoint2 point in points)
            {
                double d = from.DistanceTo(point);

                if (d > most)
                {
                    most = d;
                    best = point;
                }
            }

            return best;
        }

        private static GeoPoint3 Farthest(List<GeoPoint3> points, GeoPoint3 from)
        {
            GeoPoint3 best = from;
            double most = -1.0;

            foreach (GeoPoint3 point in points)
            {
                double d = from.DistanceTo(point);

                if (d > most)
                {
                    most = d;
                    best = point;
                }
            }

            return best;
        }

        private static void GetBasis(GeoVector3 axis, out GeoVector3 b1, out GeoVector3 b2)
        {
            GeoVector3 helper = Math.Abs(axis.X) < 0.9 ? GeoVector3.XAxis : GeoVector3.YAxis;

            b1 = helper.Subtract(axis.Multiply(helper.DotProduct(axis))).Normalize();
            b2 = axis.CrossProduct(b1);
        }

        /// <summary>
        /// The corners of a hull once each, and the edges between its faces: what a box standing on a face is fitted
        /// round, and what tells, before it is fitted, that it cannot be as small as one found.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each corner of a hull is a corner of several faces, and the box on a face was fitted round all their copies:
        /// 3 000 points on a sphere make a hull of 6 000 triangles, and each face sorted 18 000 points for the rectangle
        /// round them. Copies to the bit change nothing in the hull of the plane, the rectangles on it or the depth, so
        /// each corner is taken once, where no coordinate it is read at is nought or beyond what a product holds: those
        /// are the only numbers two copies of which compare as one and differ in their bits, which a sort could then
        /// order otherwise, and the face is fitted round every copy as it was.
        /// </para>
        /// <para>
        /// A face is passed over where every box on it is larger than the least so far beyond rounding, which it would
        /// lose to. The rectangle on a face holds the hull of the plane, which holds every point within the tolerance; so
        /// grown by the tolerance on every side it holds the hull of any of the points, and is no smaller than the least
        /// rectangle round them. The corners on the rim the hull shows along the normal, those on an edge between a face
        /// turned towards it and one turned away, are the ones on that hull, and calipers round them, each reach read no
        /// further than it goes, give no more than that least rectangle; less what growing by the tolerance adds, and
        /// times the depth, no box on the face is smaller. Of the 6 000 faces of the sphere 8 are fitted: 0.40 seconds
        /// where it took 7.7, and the same box to the bit. The bound is used only where the tolerance is more than a
        /// hundred-millionth of the size of the points, so that the rim's hull turns the wrong way nowhere by more than
        /// rounding allows for.
        /// </para>
        /// </remarks>
        private sealed class HullCorners
        {
            private readonly List<GeoPoint3> _corners = new List<GeoPoint3>();
            private readonly List<GeoVector3> _normals = new List<GeoVector3>();
            private readonly List<(int, int)> _sides = new List<(int, int)>();
            private readonly List<(int, int)> _ends = new List<(int, int)>();
            private readonly double[] _facing;
            private readonly int[] _taken;
            private readonly List<GeoPoint2> _rim = new List<GeoPoint2>();
            private readonly Tolerance _tolerance;
            private readonly double _reach;
            private readonly double _slack;
            private readonly bool _bounded;
            private int _stamp;

            internal HullCorners(GeoSolid3 hull, Tolerance tolerance)
            {
                _tolerance = tolerance;
                var index = new Dictionary<GeoPoint3, int>(SameBits.Instance);
                var edges = new Dictionary<long, int>(PackedKeys.Instance);
                IReadOnlyList<GeoFace3> faces = hull.Faces;

                for (int f = 0; f < faces.Count; f++)
                {
                    IReadOnlyList<GeoPoint3> vertices = faces[f].Boundary.Vertices;
                    _normals.Add(faces[f].Boundary.Normal);
                    var at = new int[vertices.Count];

                    for (int i = 0; i < at.Length; i++)
                    {
                        if (!index.TryGetValue(vertices[i], out at[i]))
                        {
                            at[i] = _corners.Count;
                            index.Add(vertices[i], at[i]);
                            _corners.Add(vertices[i]);
                        }
                    }

                    // Each edge once, with the face on either side of it; the second side is -1 until it is found.
                    for (int i = 0; i < at.Length; i++)
                    {
                        int a = at[i], b = at[(i + 1) % at.Length];
                        long key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);

                        if (edges.TryGetValue(key, out int edge) && _sides[edge].Item2 < 0)
                        {
                            _sides[edge] = (_sides[edge].Item1, f);
                        }
                        else
                        {
                            edges[key] = _sides.Count;
                            _sides.Add((f, -1));
                            _ends.Add((a, b));
                        }
                    }
                }

                _facing = new double[_normals.Count];
                _taken = new int[_corners.Count];

                // How far the corners lie from their middle, which bounds every side of a box round them by twice that.
                double x = 0.0, y = 0.0, z = 0.0, size = 0.0;

                foreach (GeoPoint3 corner in _corners)
                {
                    x += corner.X;
                    y += corner.Y;
                    z += corner.Z;
                    size = Math.Max(size, Math.Max(Math.Abs(corner.X), Math.Max(Math.Abs(corner.Y), Math.Abs(corner.Z))));
                }

                var middle = new GeoPoint3(x / _corners.Count, y / _corners.Count, z / _corners.Count);

                foreach (GeoPoint3 corner in _corners)
                {
                    _reach = Math.Max(_reach, middle.DistanceTo(corner));
                }

                _slack = tolerance.EqualPoint + 1E-9 * (1.0 + size);
                _bounded = tolerance.EqualPoint > 0.0 && _reach < 1E8 * tolerance.EqualPoint;
            }

            /// <summary>
            /// Projects the corners, each once, onto the plane of a face and its normal, where none is read at nought or
            /// beyond what a product holds; false, and the points to be projected copy by copy, where one is.
            /// </summary>
            internal bool TryProject(GeoVector3 normal, GeoVector3 u, GeoVector3 v, List<GeoPoint2> flat, out double minN, out double maxN)
            {
                flat.Clear();
                minN = double.PositiveInfinity;
                maxN = double.NegativeInfinity;

                foreach (GeoPoint3 corner in _corners)
                {
                    var p = new GeoVector3(corner.X, corner.Y, corner.Z);
                    var point = new GeoPoint2(p.DotProduct(u), p.DotProduct(v));
                    double n = p.DotProduct(normal);

                    if (!IsPlain(point.X) || !IsPlain(point.Y) || !IsPlain(n))
                    {
                        return false;
                    }

                    flat.Add(point);
                    minN = Math.Min(minN, n);
                    maxN = Math.Max(maxN, n);
                }

                return true;
            }

            /// <summary>
            /// Whether every box standing on the face with a normal is larger, beyond rounding, than the least so far.
            /// </summary>
            internal bool StandsOnlyLarger(GeoVector3 normal, GeoVector3 u, GeoVector3 v, double least)
            {
                if (!_bounded)
                {
                    return false;
                }

                double low = double.PositiveInfinity, high = double.NegativeInfinity;

                foreach (GeoPoint3 corner in _corners)
                {
                    double n = new GeoVector3(corner.X, corner.Y, corner.Z).DotProduct(normal);
                    low = Math.Min(low, n);
                    high = Math.Max(high, n);
                }

                for (int f = 0; f < _normals.Count; f++)
                {
                    _facing[f] = normal.DotProduct(_normals[f]);
                }

                // The corners on an edge between a face turned towards the normal and one turned away, or on an edge with one face.
                _stamp++;
                _rim.Clear();

                for (int e = 0; e < _sides.Count; e++)
                {
                    (int f, int g) = _sides[e];

                    if (g >= 0 && (_facing[f] > 0.0 && _facing[g] > 0.0 || _facing[f] < 0.0 && _facing[g] < 0.0))
                    {
                        continue;
                    }

                    TakeOnRim(_ends[e].Item1, u, v);
                    TakeOnRim(_ends[e].Item2, u, v);
                }

                // Less than the least rectangle round the rim's hull by rounding, and less what growing a rectangle by the
                // slack on every side adds to it, its sides no longer than twice the reach.
                double area = LeastArea(_rim, _tolerance) * (1.0 - 1E-6) - 8.0 * _slack * _reach - 4.0 * _slack * _slack;
                double volume = (high - low) * area * (1.0 - 1E-12);

                return volume > least * (1.0 + 3E-9);
            }

            private void TakeOnRim(int corner, GeoVector3 u, GeoVector3 v)
            {
                if (_taken[corner] != _stamp)
                {
                    _taken[corner] = _stamp;
                    var p = new GeoVector3(_corners[corner].X, _corners[corner].Y, _corners[corner].Z);
                    _rim.Add(new GeoPoint2(p.DotProduct(u), p.DotProduct(v)));
                }
            }

            /// <summary>
            /// No more than the least area of a rectangle round some points: calipers round their hull, along each edge in
            /// turn, each reach followed only as far as it grows, so that it reads no further than the furthest corner.
            /// </summary>
            private static double LeastArea(List<GeoPoint2> points, Tolerance tolerance)
            {
                if (!ConvexHull2.TryOf(points, out GeoPolygon2 hull, tolerance))
                {
                    return 0.0;
                }

                IReadOnlyList<GeoPoint2> c = hull.Vertices;
                int m = c.Count;
                int top = -1, right = -1, left = -1;
                double least = double.PositiveInfinity;

                for (int i = 0; i < m; i++)
                {
                    GeoPoint2 a = c[i], b = c[(i + 1) % m];
                    double length = a.DistanceTo(b);

                    if (!(length > 0.0))
                    {
                        continue;
                    }

                    double ax = (b.X - a.X) / length, ay = (b.Y - a.Y) / length;

                    if (top < 0)
                    {
                        top = right = left = 0;

                        for (int j = 1; j < m; j++)
                        {
                            top = Across(c[j], ax, ay) > Across(c[top], ax, ay) ? j : top;
                            right = Along(c[j], ax, ay) > Along(c[right], ax, ay) ? j : right;
                            left = Along(c[j], ax, ay) < Along(c[left], ax, ay) ? j : left;
                        }
                    }

                    // The corners furthest across, ahead and behind move on round the hull as the edge does.
                    for (int step = 0; step < m && Across(c[(top + 1) % m], ax, ay) > Across(c[top], ax, ay); step++)
                    {
                        top = (top + 1) % m;
                    }

                    for (int step = 0; step < m && Along(c[(right + 1) % m], ax, ay) > Along(c[right], ax, ay); step++)
                    {
                        right = (right + 1) % m;
                    }

                    for (int step = 0; step < m && Along(c[(left + 1) % m], ax, ay) < Along(c[left], ax, ay); step++)
                    {
                        left = (left + 1) % m;
                    }

                    double across = Across(c[top], ax, ay) - Math.Min(Across(a, ax, ay), Across(b, ax, ay));
                    double along = Along(c[right], ax, ay) - Along(c[left], ax, ay);
                    least = Math.Min(least, Math.Max(0.0, across) * Math.Max(0.0, along));
                }

                return double.IsPositiveInfinity(least) ? 0.0 : least;
            }

            private static double Along(GeoPoint2 p, double ax, double ay) => p.X * ax + p.Y * ay;

            private static double Across(GeoPoint2 p, double ax, double ay) => p.Y * ax - p.X * ay;

            /// <summary>
            /// Whether a number is one that two copies of compare as equal only where their bits are the same, and whose
            /// products with a unit neither vanish nor overflow: not nought, and from 1E-150 to 1E150 in size.
            /// </summary>
            private static bool IsPlain(double value) => Math.Abs(value) >= 1E-150 && Math.Abs(value) <= 1E150;
        }

        /// <summary>
        /// Points equal to the bit; two noughts of different sign are two points.
        /// </summary>
        private sealed class SameBits : IEqualityComparer<GeoPoint3>
        {
            internal static SameBits Instance { get; } = new SameBits();

            public bool Equals(GeoPoint3 a, GeoPoint3 b)
                => BitConverter.DoubleToInt64Bits(a.X) == BitConverter.DoubleToInt64Bits(b.X)
                && BitConverter.DoubleToInt64Bits(a.Y) == BitConverter.DoubleToInt64Bits(b.Y)
                && BitConverter.DoubleToInt64Bits(a.Z) == BitConverter.DoubleToInt64Bits(b.Z);

            public int GetHashCode(GeoPoint3 point)
            {
                ulong hash = (ulong)BitConverter.DoubleToInt64Bits(point.X);
                hash = (hash * 0x9E3779B97F4A7C15UL) ^ (ulong)BitConverter.DoubleToInt64Bits(point.Y);
                hash = (hash * 0x9E3779B97F4A7C15UL) ^ (ulong)BitConverter.DoubleToInt64Bits(point.Z);
                return unchecked((int)((hash * 0x9E3779B97F4A7C15UL) >> 32));
            }
        }
    }
}
