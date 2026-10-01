using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Breaks a face of a mesh of the plane into triangles on its own corners, every corner of it a corner of one.
    /// <para>
    /// A face of a mesh is a simple polygon running counter-clockwise, and may have corners in a row where the face beside
    /// it has a corner on their side. Clipping ears from it as it stands keeps them: a corner in a row is never the tip of
    /// an ear, since it does not turn, but is taken into the ear clipped beside it, and an ear whose closing side would pass
    /// through or touch another corner is not clipped. So no triangle has no area and none leaves a corner out, and the
    /// triangles of two faces side by side meet edge to edge as the faces do. It reads no tolerance: only rounding, a
    /// trillionth of the face's size, stands between a corner and a line it is on.
    /// </para>
    /// </summary>
    internal static class FaceEars
    {
        /// <summary>
        /// Clips the ears of a face, given by the indexes of its corners, counter-clockwise.
        /// </summary>
        /// <param name="vertices">The places the indexes stand for.</param>
        /// <param name="face">The corners of the face, in order.</param>
        /// <param name="triangles">Where the triangles go.</param>
        /// <returns>
        /// true with the face's triangles added; false, with nothing added, when the face turns out to be one no ear can be
        /// clipped from, as one touching itself is.
        /// </returns>
        public static bool TryTriangulate(IReadOnlyList<GeoPoint2> vertices, int[] face, List<GeoTriangle2> triangles)
        {
            int n = face.Length;
            GeoPoint2 reference = vertices[face[0]];
            var x = new double[n];
            var y = new double[n];
            double size = 0.0;

            // Laid out from the first corner, so that a face far out costs no precision.
            for (int i = 0; i < n; i++)
            {
                x[i] = vertices[face[i]].X - reference.X;
                y[i] = vertices[face[i]].Y - reference.Y;
                size = Math.Max(size, Math.Max(Math.Abs(x[i]), Math.Abs(y[i])));
            }

            double rounding = size * 1E-12;
            var ring = new List<int>(n);

            for (int i = 0; i < n; i++)
            {
                ring.Add(i);
            }

            var clipped = new List<(int A, int B, int C)>(n - 2);
            int at = 0;

            while (ring.Count > 3)
            {
                int m = ring.Count;
                int ear = -1;

                for (int step = 0; step < m && ear < 0; step++)
                {
                    int k = (at + step) % m;

                    if (IsEar(x, y, ring, k, rounding))
                    {
                        ear = k;
                    }
                }

                if (ear < 0)
                {
                    return false;
                }

                clipped.Add((ring[(ear + m - 1) % m], ring[ear], ring[(ear + 1) % m]));
                ring.RemoveAt(ear);
                at = ear % ring.Count;
            }

            if (!(Cross(x, y, ring[0], ring[1], ring[2]) > 0.0))
            {
                return false;
            }

            clipped.Add((ring[0], ring[1], ring[2]));

            foreach ((int a, int b, int c) in clipped)
            {
                triangles.Add(new GeoTriangle2(vertices[face[a]], vertices[face[b]], vertices[face[c]]));
            }

            return true;
        }

        /// <summary>
        /// Whether the corner at a place in the ring is the tip of an ear: turning left by more than rounding, with no
        /// other corner within or on the triangle it makes with its neighbours.
        /// </summary>
        private static bool IsEar(double[] x, double[] y, List<int> ring, int k, double rounding)
        {
            int m = ring.Count;
            int p = ring[(k + m - 1) % m];
            int i = ring[k];
            int q = ring[(k + 1) % m];

            double first = Length(x, y, p, i);
            double second = Length(x, y, i, q);
            double closing = Length(x, y, q, p);

            if (!(Cross(x, y, p, i, q) > rounding * (first + second)) || !(closing > 0.0))
            {
                return false;
            }

            for (int step = 2; step < m - 1; step++)
            {
                int w = ring[(k + step) % m];

                // A corner standing on a corner of the ear is that corner, and blocks nothing.
                if (Same(x, y, w, p) || Same(x, y, w, i) || Same(x, y, w, q))
                {
                    continue;
                }

                // Within the triangle, or on a side of it within rounding, counted as within.
                if (Cross(x, y, p, i, w) >= -rounding * first
                    && Cross(x, y, i, q, w) >= -rounding * second
                    && Cross(x, y, q, p, w) >= -rounding * closing)
                {
                    return false;
                }
            }

            return true;
        }

        private static double Cross(double[] x, double[] y, int a, int b, int c)
            => (x[b] - x[a]) * (y[c] - y[a]) - (y[b] - y[a]) * (x[c] - x[a]);

        private static double Length(double[] x, double[] y, int a, int b)
        {
            double dx = x[b] - x[a];
            double dy = y[b] - y[a];
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool Same(double[] x, double[] y, int a, int b) => x[a] == x[b] && y[a] == y[b];
    }
}
