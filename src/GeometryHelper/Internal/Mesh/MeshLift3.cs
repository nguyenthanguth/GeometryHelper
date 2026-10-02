using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Lays the rings of a flat shape of space out in a frame, for the meshing of the plane, and puts the vertices of the
    /// mesh it gives back into space.
    /// <para>
    /// A corner of the shape goes back to the corner it was laid out from, exactly, wherever the shape has it. A shape a
    /// hair out of flat, as the faces from a modeller are, keeps its corners where they are, off the plane by up to the
    /// planar tolerance; a point the meshing put on one of its sides goes back onto that side in space, at the same share
    /// of its length, so that the faces along the rim meet the shape's edges; and a point inside goes onto the plane.
    /// Put onto the plane as well, a point on a side beside a corner off it would stand between the two, and the faces there
    /// would stand on end.
    /// </para>
    /// </summary>
    internal static class MeshLift3
    {
        /// <summary>
        /// Lays a ring out in a frame, each corner where it stands in the plane, the distance from it dropped.
        /// </summary>
        public static GeoPoint2[] LayOut(GeoCoordinateSystem3 frame, IReadOnlyList<GeoPoint3> ring)
        {
            var flat = new GeoPoint2[ring.Count];

            for (int i = 0; i < flat.Length; i++)
            {
                GeoPoint3 local = frame.ToLocal(ring[i]);
                flat[i] = new GeoPoint2(local.X, local.Y);
            }

            return flat;
        }

        /// <summary>
        /// Lays a ring out in a frame as a polygon of the plane, corners that come to stand on each other there dropped.
        /// </summary>
        /// <returns>The polygon, or null when fewer than three corners are left apart.</returns>
        public static GeoPolygon2 LayOutPolygon(GeoPoint2[] flat)
        {
            var kept = new List<GeoPoint2>(flat.Length);

            foreach (GeoPoint2 point in flat)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].Equals(point))
                {
                    kept.Add(point);
                }
            }

            while (kept.Count > 1 && kept[kept.Count - 1].Equals(kept[0]))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            return kept.Count < 3 ? null : new GeoPolygon2(kept.ToArray(), kept.Count);
        }

        /// <summary>
        /// Puts the vertices of a mesh laid out in a frame back into space.
        /// </summary>
        /// <param name="flat">The mesh, laid out in the frame.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="rings">The shape's rings in space.</param>
        /// <param name="laid">The same rings laid out in the frame, point for point, as the mesh was given them.</param>
        /// <param name="straight">
        /// Whether the rings' sides are the shape's own, so that a point on one belongs on the side in space; false for rings
        /// flattened from arcs, whose chords the shape does not have.
        /// </param>
        /// <returns>Where each vertex of the mesh stands in space, in its order.</returns>
        public static GeoPoint3[] Lift(GeoMesh2 flat, GeoCoordinateSystem3 frame, IReadOnlyList<IReadOnlyList<GeoPoint3>> rings, IReadOnlyList<GeoPoint2[]> laid, bool straight)
        {
            var corners = new Dictionary<GeoPoint2, GeoPoint3>();
            double scale = 0.0;
            double offFlat = 0.0;

            for (int r = 0; r < rings.Count; r++)
            {
                for (int i = 0; i < laid[r].Length; i++)
                {
                    if (!corners.ContainsKey(laid[r][i]))
                    {
                        corners.Add(laid[r][i], rings[r][i]);
                    }

                    scale = Math.Max(scale, Math.Max(Math.Abs(laid[r][i].X), Math.Abs(laid[r][i].Y)));
                    offFlat = Math.Max(offFlat, Math.Abs(frame.ToLocal(rings[r][i]).Z));
                }
            }

            GeoPoint2[] points = flat.VertexArray;
            var lifted = new GeoPoint3[points.Length];

            // A shape flat to the last digits needs nothing but its corners back: the plane is its sides.
            Sides sides = straight && offFlat > 1E-12 * Math.Max(1.0, scale) ? new Sides(rings, laid, scale) : null;

            for (int v = 0; v < points.Length; v++)
            {
                GeoPoint2 point = points[v];

                if (corners.TryGetValue(point, out GeoPoint3 corner))
                {
                    lifted[v] = corner;
                }
                else if (sides != null && sides.TryLift(point, out GeoPoint3 onSide))
                {
                    lifted[v] = onSide;
                }
                else
                {
                    lifted[v] = frame.ToGlobal(new GeoPoint3(point.X, point.Y, 0.0));
                }
            }

            return lifted;
        }

        /// <summary>
        /// The sides of the rings, found by where a point stands, through a grid of cells about as wide as a side is long.
        /// </summary>
        private sealed class Sides
        {
            private readonly List<(GeoPoint2 A, GeoPoint2 B, GeoPoint3 A3, GeoPoint3 B3)> _sides = new List<(GeoPoint2, GeoPoint2, GeoPoint3, GeoPoint3)>();
            private readonly Dictionary<(long, long), List<int>> _cells = new Dictionary<(long, long), List<int>>();
            private readonly double _size;
            private readonly double _reach;

            public Sides(IReadOnlyList<IReadOnlyList<GeoPoint3>> rings, IReadOnlyList<GeoPoint2[]> laid, double scale)
            {
                double total = 0.0;

                for (int r = 0; r < rings.Count; r++)
                {
                    int n = laid[r].Length;

                    for (int i = 0; i < n; i++)
                    {
                        GeoPoint2 a = laid[r][i];
                        GeoPoint2 b = laid[r][(i + 1) % n];

                        if (!a.Equals(b))
                        {
                            _sides.Add((a, b, rings[r][i], rings[r][(i + 1) % n]));
                            total += a.DistanceTo(b);
                        }
                    }
                }

                // A point the meshing put on a side stands on it but for the rounding of the crossing it was computed as.
                _reach = Math.Max(1E-9, 1E-12 * scale);
                _size = Math.Max(_sides.Count > 0 ? total / _sides.Count : 1.0, 64.0 * _reach);

                for (int s = 0; s < _sides.Count; s++)
                {
                    (GeoPoint2 a, GeoPoint2 b, _, _) = _sides[s];
                    int steps = (int)Math.Min(1E6, Math.Ceiling(2.0 * a.DistanceTo(b) / _size)) + 1;

                    for (int k = 0; k <= steps; k++)
                    {
                        double t = (double)k / steps;
                        (long, long) key = Key(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));

                        if (!_cells.TryGetValue(key, out List<int> list))
                        {
                            list = new List<int>(2);
                            _cells.Add(key, list);
                        }

                        if (list.Count == 0 || list[list.Count - 1] != s)
                        {
                            list.Add(s);
                        }
                    }
                }
            }

            /// <summary>
            /// Puts a point standing on a side, apart from its ends, onto the side in space, at the same share of its length.
            /// </summary>
            public bool TryLift(GeoPoint2 point, out GeoPoint3 lifted)
            {
                (long x, long y) = Key(point.X, point.Y);

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        if (!_cells.TryGetValue((x + dx, y + dy), out List<int> list))
                        {
                            continue;
                        }

                        foreach (int s in list)
                        {
                            (GeoPoint2 a, GeoPoint2 b, GeoPoint3 a3, GeoPoint3 b3) = _sides[s];
                            double ex = b.X - a.X;
                            double ey = b.Y - a.Y;
                            double lengthSquared = ex * ex + ey * ey;
                            double t = ((point.X - a.X) * ex + (point.Y - a.Y) * ey) / lengthSquared;

                            if (!(t > 0.0 && t < 1.0))
                            {
                                continue;
                            }

                            double ox = a.X + t * ex - point.X;
                            double oy = a.Y + t * ey - point.Y;

                            if (ox * ox + oy * oy <= _reach * _reach)
                            {
                                lifted = new GeoPoint3(a3.X + t * (b3.X - a3.X), a3.Y + t * (b3.Y - a3.Y), a3.Z + t * (b3.Z - a3.Z));
                                return true;
                            }
                        }
                    }
                }

                lifted = default;
                return false;
            }

            private (long, long) Key(double x, double y) => ((long)Math.Floor(x / _size), (long)Math.Floor(y / _size));
        }
    }
}
