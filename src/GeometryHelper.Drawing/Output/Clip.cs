using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Cuts the lines and arcs of a view at its crop, a box square to the view's axes, so that what is written is what the
    /// view shows.
    /// </summary>
    internal static class Clip
    {
        /// <summary>
        /// Cuts a straight line at a box (Liang and Barsky): the part inside, its ends on the box where it crosses it.
        /// </summary>
        /// <returns>Whether any of the line lies inside the box.</returns>
        public static bool Line(GeoPoint2 start, GeoPoint2 end, Extent box, out GeoPoint2 a, out GeoPoint2 b)
        {
            a = start;
            b = end;

            if (Inside(start, box) && Inside(end, box))
            {
                return true;
            }

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double t0 = 0.0,
                t1 = 1.0;

            if (
                !Narrow(-dx, start.X - box.X0, ref t0, ref t1)
                || !Narrow(dx, box.X1 - start.X, ref t0, ref t1)
                || !Narrow(-dy, start.Y - box.Y0, ref t0, ref t1)
                || !Narrow(dy, box.Y1 - start.Y, ref t0, ref t1)
            )
            {
                return false;
            }

            if (!(t1 > t0))
            {
                return false;
            }

            a = new GeoPoint2(start.X + t0 * dx, start.Y + t0 * dy);
            b = new GeoPoint2(start.X + t1 * dx, start.Y + t1 * dy);
            return true;
        }

        /// <summary>
        /// Cuts an arc running anticlockwise from a start angle through a sweep at a box: the pieces inside, each its start
        /// angle and sweep; a whole circle wholly inside is one null piece.
        /// </summary>
        public static List<double[]> Arc(
            GeoPoint2 centre,
            double radius,
            double start,
            double sweep,
            bool circle,
            Extent box
        )
        {
            var pieces = new List<double[]>();

            if (
                centre.X - radius >= box.X0
                && centre.X + radius <= box.X1
                && centre.Y - radius >= box.Y0
                && centre.Y + radius <= box.Y1
            )
            {
                pieces.Add(circle ? null : new[] { start, sweep });
                return pieces;
            }

            // The angles past the start where the circle crosses a side of the box, inside the sweep.
            var cuts = new List<double> { 0.0, sweep };
            Cross(centre.X, radius, box.X0, false, start, sweep, cuts);
            Cross(centre.X, radius, box.X1, false, start, sweep, cuts);
            Cross(centre.Y, radius, box.Y0, true, start, sweep, cuts);
            Cross(centre.Y, radius, box.Y1, true, start, sweep, cuts);
            cuts.Sort();

            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                double from = cuts[i];
                double to = cuts[i + 1];

                if (!(to - from > 1E-12))
                {
                    continue;
                }

                double middle = start + (from + to) / 2.0;
                var point = new GeoPoint2(centre.X + radius * Math.Cos(middle), centre.Y + radius * Math.Sin(middle));

                if (!Inside(point, box))
                {
                    continue;
                }

                // A piece that goes on from the one before is one arc with it.
                if (
                    pieces.Count > 0
                    && Math.Abs(pieces[pieces.Count - 1][0] + pieces[pieces.Count - 1][1] - (start + from)) <= 1E-12
                )
                {
                    pieces[pieces.Count - 1][1] += to - from;
                }
                else
                {
                    pieces.Add(new[] { start + from, to - from });
                }
            }

            return pieces;
        }

        private static bool Inside(GeoPoint2 p, Extent box) =>
            p.X >= box.X0 && p.X <= box.X1 && p.Y >= box.Y0 && p.Y <= box.Y1;

        private static bool Narrow(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < double.Epsilon)
            {
                return q >= 0.0;
            }

            double t = q / p;

            if (p < 0.0)
            {
                if (t > t1)
                {
                    return false;
                }

                t0 = Math.Max(t0, t);
            }
            else
            {
                if (t < t0)
                {
                    return false;
                }

                t1 = Math.Min(t1, t);
            }

            return true;
        }

        /// <summary>The angles past the start at which a circle crosses the line x = at (or y = at), inside the sweep.</summary>
        private static void Cross(
            double centre,
            double radius,
            double at,
            bool alongY,
            double start,
            double sweep,
            List<double> cuts
        )
        {
            double c = (at - centre) / radius;

            if (!(Math.Abs(c) < 1.0))
            {
                return;
            }

            double angle = alongY ? Math.Asin(c) : Math.Acos(c);
            double[] both = alongY ? new[] { angle, Math.PI - angle } : new[] { angle, -angle };

            foreach (double a in both)
            {
                double past = a - start;
                past -= 2.0 * Math.PI * Math.Floor(past / (2.0 * Math.PI));

                if (past > 0.0 && past < sweep)
                {
                    cuts.Add(past);
                }
            }
        }
    }
}
