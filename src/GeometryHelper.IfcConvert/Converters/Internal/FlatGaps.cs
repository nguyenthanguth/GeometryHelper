using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Closes the flat gaps a closed shell was written with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tekla writes the nut of a bolt with one face running straight across a corner the two faces beside it keep: the
    /// face skips a vertex its neighbours have, and a sliver of a triangle, three and a half square millimetres, is left
    /// open in the plane of that face. The file calls the shell closed, and the gap is in the file, so the body came out
    /// open: 1,320 bodies of one steel model, twelve to a bolt, each warned about and each unsure of what it contains.
    /// </para>
    /// <para>
    /// The rim of such a gap is the edges no face runs back along. Chained into loops, each loop lying in one plane is
    /// capped with the face that loop outlines, wound against the faces around it, which is how a face of a closed
    /// surface meets its neighbours. The body is kept only if that closes it; a gap that does not lie in one plane, or
    /// caps that leave the body open, leave it as it was.
    /// </para>
    /// </remarks>
    internal static class FlatGaps
    {
        /// <summary>
        /// Caps the flat gaps of a body that is not closed.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="tolerance">The tolerance deciding which corners are one and which loops are flat.</param>
        /// <param name="closed">The body with its gaps capped, when that closes it.</param>
        /// <returns>true when capping closed the body.</returns>
        internal static bool TryClose(GeoSolid3 solid, Tolerance tolerance, out GeoSolid3 closed)
        {
            closed = null;

            var corners = new Corners(tolerance.EqualPoint);
            var open = new Dictionary<(int From, int To), int>();

            foreach (GeoFace3 face in solid.Faces)
            {
                AddRing(face.Boundary.Vertices, false, corners, open);

                // A hole is held wound as the boundary is, so the material runs the other way round its rim.
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    AddRing(hole.Vertices, true, corners, open);
                }
            }

            // Each edge left over is a stretch of rim; a cap runs along it the other way.
            var next = new Dictionary<int, List<int>>();
            foreach (KeyValuePair<(int From, int To), int> edge in open)
            {
                for (int k = 0; k < edge.Value; k++)
                {
                    if (!next.TryGetValue(edge.Key.To, out List<int> ends))
                    {
                        next[edge.Key.To] = ends = new List<int>();
                    }

                    ends.Add(edge.Key.From);
                }
            }

            if (next.Count == 0)
            {
                return false;
            }

            var caps = new List<GeoFace3>();
            int budget = open.Values.Sum() + 1;

            while (next.Count > 0)
            {
                int start = next.Keys.First();
                var loop = new List<int> { start };
                int at = start;

                do
                {
                    if (!next.TryGetValue(at, out List<int> ends) || budget-- <= 0)
                    {
                        return false;
                    }

                    int to = ends[ends.Count - 1];
                    ends.RemoveAt(ends.Count - 1);

                    if (ends.Count == 0)
                    {
                        next.Remove(at);
                    }

                    at = to;
                    loop.Add(at);
                }
                while (at != start);

                loop.RemoveAt(loop.Count - 1);

                GeoPolygon3 rim;
                try
                {
                    rim = new GeoPolygon3(loop.Select(corners.At), tolerance);
                }
                catch (ArgumentException)
                {
                    // A loop with no area is a stretch of edge split on one side and not the other, which the body
                    // measures closed already; a loop off one plane cannot be capped with a face.
                    if (IsFlatLine(loop.Select(corners.At).ToList(), tolerance))
                    {
                        continue;
                    }

                    return false;
                }

                caps.Add(new GeoFace3(rim));
            }

            if (caps.Count == 0)
            {
                return false;
            }

            var capped = new GeoSolid3(solid.Faces.Concat(caps), solid.Openings);

            if (!capped.IsClosed(tolerance))
            {
                return false;
            }

            closed = capped;
            return true;
        }

        private static void AddRing(IReadOnlyList<GeoPoint3> ring, bool reversed, Corners corners, Dictionary<(int From, int To), int> open)
        {
            int count = ring.Count;

            for (int i = 0; i < count; i++)
            {
                int a = corners.Id(ring[reversed ? count - 1 - i : i]);
                int b = corners.Id(ring[reversed ? (2 * count - 2 - i) % count : (i + 1) % count]);

                if (a == b)
                {
                    continue;
                }

                // An edge some face has already run the other way along is inside the surface: both go.
                if (open.TryGetValue((b, a), out int against) && against > 0)
                {
                    if (against == 1)
                    {
                        open.Remove((b, a));
                    }
                    else
                    {
                        open[(b, a)] = against - 1;
                    }

                    continue;
                }

                open[(a, b)] = open.TryGetValue((a, b), out int already) ? already + 1 : 1;
            }
        }

        /// <summary>
        /// Checks whether points all lie along one line, so that a loop through them encloses nothing.
        /// </summary>
        private static bool IsFlatLine(List<GeoPoint3> points, Tolerance tolerance)
        {
            GeoPoint3 first = points[0];
            GeoPoint3 far = points.OrderByDescending(p => p.DistanceTo(first)).First();
            double length = first.DistanceTo(far);

            if (length <= tolerance.EqualPoint)
            {
                return true;
            }

            GeoVector3 along = first.GetVectorTo(far).Multiply(1.0 / length);
            return points.All(p => first.GetVectorTo(p).CrossProduct(along).Length <= tolerance.EqualPoint);
        }

        /// <summary>
        /// Numbers corners, giving points within the tolerance of one another the same number.
        /// </summary>
        private sealed class Corners
        {
            private readonly double _cell;
            private readonly Dictionary<(long, long, long), List<int>> _grid = new Dictionary<(long, long, long), List<int>>();
            private readonly List<GeoPoint3> _points = new List<GeoPoint3>();

            internal Corners(double tolerance)
            {
                _cell = Math.Max(tolerance, 1E-12);
            }

            internal GeoPoint3 At(int id) => _points[id];

            internal int Id(GeoPoint3 point)
            {
                long x = (long)Math.Floor(point.X / _cell), y = (long)Math.Floor(point.Y / _cell), z = (long)Math.Floor(point.Z / _cell);

                for (long i = x - 1; i <= x + 1; i++)
                {
                    for (long j = y - 1; j <= y + 1; j++)
                    {
                        for (long k = z - 1; k <= z + 1; k++)
                        {
                            if (_grid.TryGetValue((i, j, k), out List<int> ids))
                            {
                                foreach (int id in ids)
                                {
                                    if (_points[id].DistanceTo(point) <= _cell)
                                    {
                                        return id;
                                    }
                                }
                            }
                        }
                    }
                }

                _points.Add(point);
                if (!_grid.TryGetValue((x, y, z), out List<int> here))
                {
                    _grid[(x, y, z)] = here = new List<int>();
                }

                here.Add(_points.Count - 1);
                return _points.Count - 1;
            }
        }
    }
}
