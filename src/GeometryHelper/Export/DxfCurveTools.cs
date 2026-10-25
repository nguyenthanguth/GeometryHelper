using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// What the curves of the editing model share when one is made into another: the plane points lie in, the bulge of the arc
    /// through three points, and the cuts of a list of parameters kept apart along a curve.
    /// </summary>
    internal static class DxfCurveTools
    {
        /// <summary>
        /// The normal of the plane some points lie in within the point tolerance, false where they lie in none. The normal
        /// preferred is taken where the points lie in a plane square to it; otherwise the plane is that of the points (by
        /// Newell's sum round them), its normal turned to the side its largest part is above nought on; a row of points on
        /// one line lies in the plane through it nearest the world's floor.
        /// </summary>
        internal static bool TryPlane(IReadOnlyList<GeoPoint3> points, GeoVector3 preferred, out GeoVector3 normal)
        {
            double slack = Tolerance.Default.EqualPoint;
            normal = new GeoVector3(0.0, 0.0, 1.0);

            if (points.Count == 0)
            {
                return true;
            }

            if (preferred.IsValid && preferred.Length > 0.0 && Within(points, preferred.Normalize(), slack))
            {
                normal = preferred.Normalize();
                return true;
            }

            double nx = 0.0, ny = 0.0, nz = 0.0;
            GeoPoint3 o = points[0];

            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint3 a = points[i];
                GeoPoint3 b = points[(i + 1) % points.Count];
                double ax = a.X - o.X, ay = a.Y - o.Y, az = a.Z - o.Z;
                double bx = b.X - o.X, by = b.Y - o.Y, bz = b.Z - o.Z;
                nx += (ay - by) * (az + bz);
                ny += (az - bz) * (ax + bx);
                nz += (ax - bx) * (ay + by);
            }

            var newell = new GeoVector3(nx, ny, nz);

            if (newell.Length > 0.0 && Within(points, newell.Normalize(), slack))
            {
                normal = Upward(newell.Normalize());
                return true;
            }

            // On one line, or nearly: the plane through the line nearest the floor.
            GeoVector3 along = default(GeoVector3);

            foreach (GeoPoint3 p in points)
            {
                GeoVector3 d = o.GetVectorTo(p);

                if (d.Length > along.Length)
                {
                    along = d;
                }
            }

            if (!(along.Length > slack))
            {
                return true;
            }

            GeoVector3 u = along.Normalize();
            var up = new GeoVector3(0.0, 0.0, 1.0);
            GeoVector3 square = up.Add(u.Multiply(-up.DotProduct(u)));

            if (!(square.Length > 1E-9))
            {
                var x = new GeoVector3(1.0, 0.0, 0.0);
                square = x.Add(u.Multiply(-x.DotProduct(u)));
            }

            GeoVector3 candidate = Upward(square.Normalize());

            if (Within(points, candidate, slack))
            {
                normal = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// A normal turned to the side its largest part is above nought on: (0, 0, 1) rather than (0, 0, -1). Parts as large to
        /// the bit are told apart by their axis, z before y before x, so that (1, 0, -1) is turned to (-1, 0, 1) and (-1, 1, 0) is
        /// kept, whichever way it came.
        /// </summary>
        internal static GeoVector3 Upward(GeoVector3 n)
        {
            double ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
            double big;

            if (az >= ax && az >= ay)
            {
                big = n.Z;
            }
            else if (ay >= ax)
            {
                big = n.Y;
            }
            else
            {
                big = n.X;
            }

            return big < 0.0 ? n.Negate() : n;
        }

        /// <summary>Whether points all lie within a distance of the plane through the first square to a normal.</summary>
        private static bool Within(IReadOnlyList<GeoPoint3> points, GeoVector3 normal, double slack)
        {
            GeoPoint3 o = points[0];

            foreach (GeoPoint3 p in points)
            {
                if (Math.Abs(o.GetVectorTo(p).DotProduct(normal)) > slack)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The pieces of the rational quadratic that is exactly a conic, a part of a circle or ellipse given by its centre and
        /// its point at each parameter (centre plus one axis times the cosine plus the other times the sine): a piece for each
        /// quarter turn or less, its apex where the tangents at its ends meet, weighted by the cosine of half its sweep. The
        /// points, weights and the knots, the parameters at the ends of the pieces each standing twice inside; with
        /// <paramref name="exact"/> the last point is the first, for a whole turn.
        /// </summary>
        internal static void Conic(GeoPoint3 centre, Func<double, GeoPoint3> at, double start, double end, bool exact, List<GeoPoint3> points, List<double> weights, List<double> knots)
        {
            double sweep = end - start;
            int pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI * 0.5) - 1E-9));
            double step = sweep / pieces;
            double cosine = Math.Cos(Math.Abs(step) * 0.5);
            GeoPoint3 first = at(start);
            points.Add(first);
            weights.Add(1.0);
            knots.Add(start);
            knots.Add(start);
            knots.Add(start);

            for (int i = 0; i < pieces; i++)
            {
                GeoPoint3 middle = at(start + (i + 0.5) * step);
                points.Add(new GeoPoint3(centre.X + (middle.X - centre.X) / cosine, centre.Y + (middle.Y - centre.Y) / cosine, centre.Z + (middle.Z - centre.Z) / cosine));
                weights.Add(cosine);
                double knot = i == pieces - 1 ? end : start + (i + 1) * step;
                points.Add(i == pieces - 1 && exact ? first : at(knot));
                weights.Add(1.0);
                knots.Add(knot);
                knots.Add(knot);
            }

            knots.Add(end);
        }

        /// <summary>
        /// The bulge of the arc from one point through a second to a third, in the plane of their first two coordinates: the
        /// tangent of a quarter of the angle it sweeps, above nought counter-clockwise; nought where the three are on a line.
        /// </summary>
        internal static double Bulge(double ax, double ay, double mx, double my, double bx, double by)
        {
            double ux = ax - mx, uy = ay - my;
            double vx = bx - mx, vy = by - my;
            double cross = (mx - ax) * (by - my) - (my - ay) * (bx - mx);
            double dot = ux * vx + uy * vy;
            double angle = Math.Atan2(Math.Abs(ux * vy - uy * vx), dot);

            if (!(angle > 0.0) || angle >= Math.PI || cross == 0.0)
            {
                return 0.0;
            }

            double b = Math.Tan((Math.PI - angle) / 2.0);
            return cross > 0.0 ? b : -b;
        }

        /// <summary>
        /// The parameters a curve is cut at: those given, each checked within the curve's range, in increasing order, those
        /// within the point tolerance along the curve of an end or of a cut before kept out; for a closed curve one within the
        /// tolerance of the first cut round the end kept out too. The curve is measured once, from each cut to the next in
        /// increasing order, by the length between two parameters, so that a thousand cuts cost one walk along the curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        internal static List<double> Cuts(IReadOnlyList<double> parameters, double start, double end, bool closed, Func<double, double, double> between, Action<double> check)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            double slack = Tolerance.Default.EqualPoint;
            var sorted = new List<double>(parameters.Count);

            foreach (double parameter in parameters)
            {
                check(parameter);
                sorted.Add(Math.Max(start, Math.Min(end, parameter)));
            }

            sorted.Sort();
            var measured = new List<KeyValuePair<double, double>>(sorted.Count);
            double from = start;
            double run = 0.0;

            foreach (double cut in sorted)
            {
                run += cut > from ? between(from, cut) : 0.0;
                from = Math.Max(from, cut);
                measured.Add(new KeyValuePair<double, double>(cut, run));
            }

            double length = run + (end > from ? between(from, end) : 0.0);
            var cuts = new List<KeyValuePair<double, double>>(measured.Count);

            foreach (KeyValuePair<double, double> cut in measured)
            {
                // Round a closed curve its end is its start; an open one is not cut at either end.
                if (closed && length - cut.Value <= slack)
                {
                    cuts.Add(new KeyValuePair<double, double>(start, 0.0));
                }
                else if (closed || (cut.Value > slack && length - cut.Value > slack))
                {
                    cuts.Add(cut);
                }
            }

            cuts.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Value.CompareTo(b.Value));
            var kept = new List<KeyValuePair<double, double>>();
            double last = double.NegativeInfinity;

            foreach (KeyValuePair<double, double> cut in cuts)
            {
                if (cut.Value - last > slack)
                {
                    kept.Add(cut);
                    last = cut.Value;
                }
            }

            if (closed && kept.Count > 1 && length - kept[kept.Count - 1].Value + kept[0].Value <= slack)
            {
                kept.RemoveAt(kept.Count - 1);
            }

            return kept.ConvertAll(k => k.Key);
        }
    }
}
