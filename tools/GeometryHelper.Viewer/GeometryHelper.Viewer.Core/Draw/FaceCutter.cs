using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Cuts a face into the triangles it is drawn with: the library's own cut, except for a big convex face with no hole, which the library
    /// cuts by taking its corners off one at a time, each time looking at every other corner, so that the time grows with the square of the
    /// corners (a ring of 4 000 corners 0.1 s, one of 16 000 1.4 s, one of 64 000 15 s). Such a face is cut here into the triangles the
    /// library gives, to the bit, in time that grows with the corners.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On a convex loop the library takes the first corner as an ear every time, so what it gives is the fan round the last corner:
    /// the triangles (last, k, k + 1) for k from 0 to n - 4, then (n - 3, n - 2, last), each left out where it
    /// is degenerate at the tolerance. This is that, and only where the library is certain to give it: the loop has no hole, is convex with
    /// every corner turning between 1E-6 and 30 degrees, goes round once, has no edge shorter and is nowhere thinner than 8 times the point
    /// tolerance (so that no two edges come within it, which would send the library to cut the face in strips), and every triangle of the fan
    /// turns the right way by more than the rounding of the library's own working could undo. Any face that does not pass is cut by the library,
    /// whatever its size.
    /// </para>
    /// </remarks>
    internal static class FaceCutter
    {
        /// <summary>
        /// The fewest corners a face has to have before the fan is considered: below it the library's cut takes under 5 ms.
        /// </summary>
        internal const int FanFrom = 256;

        /// <summary>
        /// Cuts a face into triangles at the tolerance in force on this thread: the library's cut, or the same triangles by the fan.
        /// </summary>
        internal static GeoTriangle3[] Cut(GeoFace3 face) => CutFrom(face, FanFrom);

        internal static GeoTriangle3[] CutFrom(GeoFace3 face, int fanFrom)
            => TryFan(face, Tolerance.Global, fanFrom, out GeoTriangle3[] fan) ? fan : face.TriangulateSurface();

        /// <summary>
        /// Cuts a face for the cap of a section, which can have no more corners than the library cuts in a second or so: a face with more that is not a convex fan is not cut.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="libraryLimit">The most corners the library is asked to cut.</param>
        /// <param name="triangles">The triangles, or null when the face has more corners than the limit and is not a convex fan.</param>
        /// <returns>false when the face was left uncut.</returns>
        internal static bool TryCutBounded(GeoFace3 face, int libraryLimit, out GeoTriangle3[] triangles)
        {
            if (TryFan(face, Tolerance.Global, FanFrom, out triangles))
            {
                return true;
            }

            int corners = face.Boundary.Vertices.Count;

            foreach (GeoPolygon3 hole in face.Holes)
            {
                corners += hole.Vertices.Count;
            }

            if (corners > libraryLimit)
            {
                triangles = null;
                return false;
            }

            triangles = face.TriangulateSurface();
            return true;
        }

        /// <summary>
        /// Cuts a face by the fan where the library's cut is certain to be the fan.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="fanFrom">The fewest corners worth it.</param>
        /// <param name="triangles">The triangles as the library gives them; null when this returns false.</param>
        /// <returns>false when the face is not one the fan is certain for, and the library has to cut it.</returns>
        internal static bool TryFan(GeoFace3 face, Tolerance tolerance, int fanFrom, out GeoTriangle3[] triangles)
        {
            triangles = null;
            IReadOnlyList<GeoPoint3> corners = face.Boundary.Vertices;
            int n = corners.Count;

            if (face.Holes.Count > 0 || n < Math.Max(fanFrom, 4))
            {
                return false;
            }

            double[] x = new double[n];
            double[] y = new double[n];
            double[] z = new double[n];

            for (int i = 0; i < n; i++)
            {
                x[i] = corners[i].X;
                y[i] = corners[i].Y;
                z[i] = corners[i].Z;

                if (double.IsNaN(x[i] + y[i] + z[i]) || double.IsInfinity(x[i] + y[i] + z[i]))
                {
                    return false;
                }
            }

            GeoVector3 normal = face.GetPlane().Normal;
            double nx = normal.X, ny = normal.Y, nz = normal.Z;

            // The size of the face, for the rounding the library's working has.
            double size = 0.0;

            for (int i = 1; i < n; i++)
            {
                size = Math.Max(size, Math.Max(Math.Abs(x[i] - x[0]), Math.Max(Math.Abs(y[i] - y[0]), Math.Abs(z[i] - z[0]))));
            }

            if (!(size > 0.0))
            {
                return false;
            }

            double minEdge = 8.0 * tolerance.EqualPoint;
            double turned = 0.0;
            double perimeter = 0.0;
            double twiceArea = 0.0;

            for (int i = 0; i < n; i++)
            {
                int p = (i + n - 1) % n;
                int q = (i + 1) % n;
                double ax = x[i] - x[p], ay = y[i] - y[p], az = z[i] - z[p];
                double bx = x[q] - x[i], by = y[q] - y[i], bz = z[q] - z[i];
                double la = Math.Sqrt(ax * ax + ay * ay + az * az);
                double lb = Math.Sqrt(bx * bx + by * by + bz * bz);

                if (lb < minEdge)
                {
                    return false;
                }

                // The corner as the library sees it from the plane: twice the area of the triangle, along the normal.
                double cross = (ay * bz - az * by) * nx + (az * bx - ax * bz) * ny + (ax * by - ay * bx) * nz;
                double dot = ax * bx + ay * by + az * bz;

                if (!(cross > 1E-6 * la * lb) || !(cross <= 0.5 * la * lb) || !(dot > 0.0))
                {
                    return false;
                }

                turned += Math.Atan2(cross, dot);
                perimeter += lb;

                // Twice the area as the sum of the triangles from the first corner, along the normal.
                double ux = x[i] - x[0], uy = y[i] - y[0], uz = z[i] - z[0];
                double vx = x[q] - x[0], vy = y[q] - y[0], vz = z[q] - z[0];
                twiceArea += (uy * vz - uz * vy) * nx + (uz * vx - ux * vz) * ny + (ux * vy - uy * vx) * nz;
            }

            if (Math.Abs(turned - 2.0 * Math.PI) > 1E-6 || !(twiceArea / perimeter >= minEdge))
            {
                return false;
            }

            // The fan round the last corner: each triangle must turn the right way by more than the rounding of the library's local coordinates.
            int last = n - 1;

            for (int k = 0; k + 1 < last; k++)
            {
                double ax = x[k] - x[last], ay = y[k] - y[last], az = z[k] - z[last];
                double bx = x[k + 1] - x[last], by = y[k + 1] - y[last], bz = z[k + 1] - z[last];
                double la = Math.Sqrt(ax * ax + ay * ay + az * az);
                double lb = Math.Sqrt(bx * bx + by * by + bz * bz);
                double cross = (ay * bz - az * by) * nx + (az * bx - ax * bz) * ny + (ax * by - ay * bx) * nz;

                if (!(cross > 1E-9 * la * lb) || !(cross > 1E-14 * size * Math.Max(la, lb)))
                {
                    return false;
                }
            }

            var result = new List<GeoTriangle3>(n - 2);

            for (int k = 0; k <= n - 4; k++)
            {
                Add(result, corners[last], corners[k], corners[k + 1], tolerance);
            }

            Add(result, corners[n - 3], corners[n - 2], corners[last], tolerance);

            if (result.Count == 0)
            {
                // The library gives none when every triangle is degenerate, and then falls back to the strips: leave that to it.
                return false;
            }

            triangles = result.ToArray();
            return true;
        }

        private static void Add(List<GeoTriangle3> result, GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, Tolerance tolerance)
        {
            var triangle = new GeoTriangle3(a, b, c);

            if (!triangle.IsDegenerate(tolerance))
            {
                result.Add(triangle);
            }
        }
    }
}
