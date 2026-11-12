using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The outline of a clipped viewport (VPCLIP): a closed ring of vertices in paper units, an edge going from a vertex to the
    /// next, an arc where the bulge of the first is not nought, as a polyline's bulge says. A circle of radius 30 is two
    /// vertices 60 apart, each with the bulge 1; an L-shape six vertices, every bulge nought.
    /// </summary>
    public sealed class DisplayClip2
    {
        private readonly double[] _xy;
        private readonly double[] _bulges;
        private readonly double[] _fine;

        /// <summary>Makes an outline.</summary>
        /// <param name="xy">The vertices as x, y pairs, the last joined to the first.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex: the tangent of a quarter of its sweep, positive for an
        /// arc turning left; nought for a straight edge.</param>
        /// <exception cref="ArgumentNullException">Thrown when an array is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the vertices are fewer than two, or the bulges are not one to a
        /// vertex.</exception>
        public DisplayClip2(double[] xy, double[] bulges)
        {
            _xy = xy ?? throw new ArgumentNullException(nameof(xy));
            _bulges = bulges ?? throw new ArgumentNullException(nameof(bulges));

            if (xy.Length < 4 || xy.Length % 2 != 0 || bulges.Length != xy.Length / 2)
            {
                throw new ArgumentException("An outline has two or more vertices, each with a bulge.", nameof(xy));
            }

            double least = double.MaxValue;
            double most = double.MinValue;

            foreach (double v in _xy)
            {
                least = Math.Min(least, v);
                most = Math.Max(most, v);
            }

            _fine = Flatten(Math.Max((most - least) * 1E-5, 1E-9));
        }

        /// <summary>Gets the number of vertices.</summary>
        public int Count => _bulges.Length;

        /// <summary>Gets the x of a vertex.</summary>
        /// <param name="index">The vertex, from 0.</param>
        /// <returns>The x, in paper units.</returns>
        public double X(int index) => _xy[2 * index];

        /// <summary>Gets the y of a vertex.</summary>
        /// <param name="index">The vertex, from 0.</param>
        /// <returns>The y, in paper units.</returns>
        public double Y(int index) => _xy[2 * index + 1];

        /// <summary>Gets the bulge of the edge leaving a vertex.</summary>
        /// <param name="index">The vertex, from 0.</param>
        /// <returns>The bulge.</returns>
        public double Bulge(int index) => _bulges[index];

        /// <summary>
        /// Gets the outline as a ring of straight edges, an arc cut into pieces whose chords lie no farther from it than a
        /// tolerance, so that a circle of radius 100 cut to 0.01 is a ring of about 220 points. The vertices of the outline are among the
        /// points.
        /// </summary>
        /// <param name="tolerance">The farthest a chord may lie from its arc, in paper units; above nought.</param>
        /// <returns>The ring as x, y pairs, the last not repeating the first.</returns>
        public double[] Flatten(double tolerance)
        {
            var points = new System.Collections.Generic.List<double>(_xy.Length + 16);
            int n = Count;

            for (int i = 0; i < n; i++)
            {
                double x0 = _xy[2 * i];
                double y0 = _xy[2 * i + 1];
                int next = (i + 1) % n;
                double x1 = _xy[2 * next];
                double y1 = _xy[2 * next + 1];
                points.Add(x0);
                points.Add(y0);
                double bulge = _bulges[i];

                if (Math.Abs(bulge) < 1E-12)
                {
                    continue;
                }

                double sweep = 4.0 * Math.Atan(bulge);
                double dx = x1 - x0;
                double dy = y1 - y0;
                double chord = Math.Sqrt(dx * dx + dy * dy);

                if (chord <= 0.0)
                {
                    continue;
                }

                double radius = chord / (2.0 * Math.Sin(Math.Abs(sweep) / 2.0));
                double centre = chord / 2.0 / Math.Tan(sweep / 2.0);
                double cx = (x0 + x1) / 2.0 - dy / chord * centre;
                double cy = (y0 + y1) / 2.0 + dx / chord * centre;
                double start = Math.Atan2(y0 - cy, x0 - cx);
                double ratio = Math.Max(-1.0, Math.Min(1.0, 1.0 - tolerance / radius));
                double step = 2.0 * Math.Acos(ratio);
                int pieces = step > 0.0 ? (int)Math.Ceiling(Math.Abs(sweep) / step) : 1;
                pieces = Math.Max(1, Math.Min(pieces, 4096));

                for (int k = 1; k < pieces; k++)
                {
                    double angle = start + sweep * k / pieces;
                    points.Add(cx + radius * Math.Cos(angle));
                    points.Add(cy + radius * Math.Sin(angle));
                }
            }

            return points.ToArray();
        }

        /// <summary>Gets whether a point lies inside the outline; a point on an edge may fall either side.</summary>
        /// <param name="x">The x, in paper units.</param>
        /// <param name="y">The y, in paper units.</param>
        /// <returns>Whether it does.</returns>
        public bool Contains(double x, double y)
        {
            int m = _fine.Length / 2;
            bool inside = false;

            for (int i = 0, j = m - 1; i < m; j = i++)
            {
                double xi = _fine[2 * i];
                double yi = _fine[2 * i + 1];
                double xj = _fine[2 * j];
                double yj = _fine[2 * j + 1];

                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Gets the distance from a point to the nearest edge of the outline, inside it or out.</summary>
        /// <param name="x">The x, in paper units.</param>
        /// <param name="y">The y, in paper units.</param>
        /// <returns>The distance, in paper units.</returns>
        public double DistanceToEdge(double x, double y)
        {
            int m = _fine.Length / 2;
            double best = double.MaxValue;

            for (int i = 0, j = m - 1; i < m; j = i++)
            {
                double ax = _fine[2 * j];
                double ay = _fine[2 * j + 1];
                double bx = _fine[2 * i];
                double by = _fine[2 * i + 1];
                double dx = bx - ax;
                double dy = by - ay;
                double length = dx * dx + dy * dy;
                double t = length > 0.0 ? Math.Max(0.0, Math.Min(1.0, ((x - ax) * dx + (y - ay) * dy) / length)) : 0.0;
                double px = ax + t * dx - x;
                double py = ay + t * dy - y;
                best = Math.Min(best, Math.Sqrt(px * px + py * py));
            }

            return best;
        }
    }
}
