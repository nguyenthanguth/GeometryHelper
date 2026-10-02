using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Which cells of a grid share part of a face: neighbours along an axis, cut apart by one plane with no joint between,
    /// whose material meets on it over some area.
    /// </summary>
    internal static class CellContact3
    {
        /// <summary>
        /// The neighbours of every cell, by their places in the grid's list, in ascending order.
        /// </summary>
        public static int[][] Adjacency(GeoCell3[] cells, Dictionary<(int, int, int), int[]> byIndex, GeoCoordinateSystem3 frame, double joint, Tolerance tolerance)
        {
            var adjacent = new SortedSet<int>[cells.Length];

            for (int n = 0; n < cells.Length; n++)
            {
                adjacent[n] = new SortedSet<int>();
            }

            if (joint == 0.0)
            {
                var directions = new[] { frame.XAxis, frame.YAxis, frame.ZAxis };

                for (int n = 0; n < cells.Length; n++)
                {
                    GeoCell3 cell = cells[n];

                    for (int a = 0; a < 3; a++)
                    {
                        (int, int, int) next = a == 0 ? (cell.I + 1, cell.J, cell.K) : a == 1 ? (cell.I, cell.J + 1, cell.K) : (cell.I, cell.J, cell.K + 1);

                        if (double.IsNaN(cell.High[a]) || !byIndex.TryGetValue(next, out int[] beyond))
                        {
                            continue;
                        }

                        foreach (int m in beyond)
                        {
                            GeoCell3 other = cells[m];

                            // Cut apart by one plane: the one bounding the first from above bounds the other from below.
                            if (double.IsNaN(other.Low[a]) || Math.Abs(other.Low[a] - cell.High[a]) > tolerance.EqualPoint)
                            {
                                continue;
                            }

                            if (Meet(cell, other, a, directions, frame, tolerance))
                            {
                                adjacent[n].Add(m);
                                adjacent[m].Add(n);
                            }
                        }
                    }
                }
            }

            var result = new int[cells.Length][];

            for (int n = 0; n < result.Length; n++)
            {
                result[n] = new int[adjacent[n].Count];
                adjacent[n].CopyTo(result[n]);
            }

            return result;
        }

        /// <summary>
        /// Whether two cells cut apart by a plane square to an axis meet on it over more than the point tolerance squared.
        /// </summary>
        private static bool Meet(GeoCell3 below, GeoCell3 above, int axis, GeoVector3[] directions, GeoCoordinateSystem3 frame, Tolerance tolerance)
        {
            double least = tolerance.EqualPoint * tolerance.EqualPoint;

            if (below.Material != null && above.Material != null)
            {
                // Two boxes: their spans across the plane overlap.
                double area = 1.0;

                for (int a = 0; a < 3; a++)
                {
                    if (a != axis)
                    {
                        area *= Math.Max(0.0, Math.Min(below.High[a], above.High[a]) - Math.Max(below.Low[a], above.Low[a]));
                    }
                }

                return area > least;
            }

            double at = below.High[axis];
            List<GeoFace2> lower = OnPlane(below.Solid, axis, at, 1.0, directions, frame, tolerance);
            List<GeoFace2> upper = OnPlane(above.Solid, axis, at, -1.0, directions, frame, tolerance);
            double shared = 0.0;

            foreach (GeoFace2 a in lower)
            {
                foreach (GeoFace2 b in upper)
                {
                    foreach (GeoFace2 common in Boolean2.Intersect(a, b, tolerance))
                    {
                        shared += common.Area;

                        if (shared > least)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The faces of a cell lying in a plane square to an axis, facing one way along it, laid out across it.
        /// </summary>
        private static List<GeoFace2> OnPlane(GeoSolid3 solid, int axis, double at, double facing, GeoVector3[] directions, GeoCoordinateSystem3 frame, Tolerance tolerance)
        {
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;
            var faces = new List<GeoFace2>();

            foreach (GeoFace3 face in solid.Faces)
            {
                if (face.Normal.DotProduct(directions[axis]) * facing < 1.0 - 1E-6 || !Lies(face, axis, at, directions, frame, tolerance))
                {
                    continue;
                }

                GeoPolygon2 boundary = Laid(face.Boundary.Vertices, u, v, directions, frame);

                if (boundary == null)
                {
                    continue;
                }

                var holes = new List<GeoPolygon2>(face.Holes.Count);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    GeoPolygon2 laid = Laid(hole.Vertices, u, v, directions, frame);

                    if (laid != null)
                    {
                        holes.Add(laid);
                    }
                }

                faces.Add(new GeoFace2(boundary, holes));
            }

            return faces;
        }

        private static bool Lies(GeoFace3 face, int axis, double at, GeoVector3[] directions, GeoCoordinateSystem3 frame, Tolerance tolerance)
        {
            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                if (Math.Abs(frame.Origin.GetVectorTo(corner).DotProduct(directions[axis]) - at) > tolerance.EqualPoint)
                {
                    return false;
                }
            }

            return true;
        }

        private static GeoPolygon2 Laid(IReadOnlyList<GeoPoint3> ring, int u, int v, GeoVector3[] directions, GeoCoordinateSystem3 frame)
        {
            var flat = new GeoPoint2[ring.Count];

            for (int i = 0; i < flat.Length; i++)
            {
                GeoVector3 offset = frame.Origin.GetVectorTo(ring[i]);
                flat[i] = new GeoPoint2(offset.DotProduct(directions[u]), offset.DotProduct(directions[v]));
            }

            GeoPolygon2 polygon = MeshLift3.LayOutPolygon(flat);
            return polygon != null && polygon.SignedArea < 0.0 ? polygon.Reverse() : polygon;
        }
    }
}
