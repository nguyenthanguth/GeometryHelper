using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The curve at a distance from a spline in the plane of a normal, as <see cref="DxfSpline.GetOffsetCurvesGivenPlaneNormal"/>
    /// gives it: the spline cut at its corners (knots standing as many times as its degree where its direction turns), each
    /// smooth piece offset by a cubic fitted to the true offset span by span, and the pieces joined as a polyline's offsets
    /// join them, run on to where they meet past a corner they part at, cut back to where they cross at one they overlap at.
    /// </summary>
    /// <remarks>
    /// A piece is fitted through points of the true offset, each the spline's point moved the distance square to its direction
    /// there, four to a span at first; a span is given twice as many points, up to 512, until the fit keeps within the point
    /// tolerance of the true offset both ways, halfway between each two points: the true offset's point there from the fit, and
    /// the fit's point there from the true offset. A span that does not meet it at 512 is left as it is.
    /// </remarks>
    internal static class DxfSplineOffset
    {
        private const int First = 4;
        private const int Most = 512;

        /// <summary>
        /// The control points and knots of the cubic at a distance from a spline, and whether it is closed: over the spline's
        /// domain where the spline has no corner; past corners, from the spline's start over the length along the offset.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the spline runs along the normal somewhere, or stands still.</exception>
        internal static void Offset(DxfNurbs nurbs, IReadOnlyList<double> knots, int degree, bool closed, GeoVector3 normal, double distance, out List<GeoPoint3> points, out List<double> knotsOut, out bool closedOut)
        {
            var corners = new List<double> { nurbs.Start };
            var spans = new List<double> { nurbs.Start };

            for (int i = 0; i < knots.Count; i++)
            {
                double knot = knots[i];

                if (knot > spans[spans.Count - 1] && knot < nurbs.End)
                {
                    spans.Add(knot);
                    int times = 0;

                    for (int j = i; j < knots.Count && knots[j] == knot; j++)
                    {
                        times++;
                    }

                    // A knot standing as many times as the degree may be a corner; it is one where the directions on either side
                    // differ, not where the curve runs on smoothly through it, as the pieces of a circle's spline do.
                    if (times >= degree && !Direction(nurbs, knot, -1).Normalize().IsEqualTo(Direction(nurbs, knot, 1).Normalize(), Tolerance.Default))
                    {
                        corners.Add(knot);
                    }
                }
            }

            spans.Add(nurbs.End);
            corners.Add(nurbs.End);
            var pieces = new List<Piece>();

            for (int c = 0; c + 1 < corners.Count; c++)
            {
                pieces.Add(Fitted(nurbs, spans.FindAll(t => t >= corners[c] && t <= corners[c + 1]), normal, distance));
            }

            GeoVector3 startWay = Direction(nurbs, nurbs.Start, 1).Normalize();
            GeoVector3 endWay = Direction(nurbs, nurbs.End, -1).Normalize();
            bool seam = closed && !startWay.IsEqualTo(endWay, Tolerance.Default);

            if (pieces.Count == 1 && !seam)
            {
                GeoSpline3 fit = pieces[0].Fit;
                points = new List<GeoPoint3>(fit.ControlPoints);
                knotsOut = new List<double>(fit.Knots.Count);

                foreach (double knot in fit.Knots)
                {
                    knotsOut.Add(knot == 1.0 ? nurbs.End : nurbs.Start + (nurbs.End - nurbs.Start) * knot);
                }

                closedOut = closed && fit.StartPoint.IsEqualTo(fit.EndPoint, Tolerance.Default);
                return;
            }

            // The joins: between each piece and the next, and round the start of a closed spline that has a corner there.
            int joins = seam ? pieces.Count : pieces.Count - 1;
            var corner = new List<GeoPoint3>[joins];

            for (int j = 0; j < joins; j++)
            {
                corner[j] = Join(nurbs, pieces[j], pieces[(j + 1) % pieces.Count], normal);
            }

            foreach (Piece piece in pieces)
            {
                if (piece.Trimmed)
                {
                    piece.Fit = Refit(nurbs, piece);
                }
            }

            var parts = new List<KeyValuePair<List<double>, List<double[]>>>();

            for (int i = 0; i < pieces.Count; i++)
            {
                parts.Add(Part(pieces[i].Fit));

                if (i < joins)
                {
                    for (int k = 0; k + 1 < corner[i].Count; k++)
                    {
                        parts.Add(Line(corner[i][k], corner[i][k + 1]));
                    }
                }
            }

            KeyValuePair<List<double>, List<double[]>> whole = parts[0];

            for (int i = 1; i < parts.Count; i++)
            {
                whole = Join(whole, parts[i]);
            }

            DxfNurbsEdit.Split(whole.Value, out points, out _);
            knotsOut = whole.Key.ConvertAll(k => nurbs.Start + k);
            closedOut = closed && points[0].IsEqualTo(points[points.Count - 1], Tolerance.Default);
        }

        /// <summary>The direction of a spline at a parameter: its derivative from the side asked for (below nought the one before, above the one after), or near it where it stands still.</summary>
        internal static GeoVector3 Direction(DxfNurbs nurbs, double t, int side)
        {
            double step = (nurbs.End - nurbs.Start) * 1E-9;
            double at = side < 0 && t - step >= nurbs.Start ? t - step : t;
            nurbs.Evaluate(at, out _, out GeoVector3 d, out _);

            if (d.Length > 0.0)
            {
                return d;
            }

            double near = t + step <= nurbs.End ? t + step : t - step;
            nurbs.Evaluate(near, out GeoPoint3 a, out _, out _);
            nurbs.Evaluate(t, out GeoPoint3 b, out _, out _);
            return near > t ? b.GetVectorTo(a) : a.GetVectorTo(b);
        }

        /// <summary>The point of a spline at a parameter moved a distance to the right of its direction there seen from the tip of a normal.</summary>
        internal static GeoPoint3 Moved(DxfNurbs nurbs, double t, int side, GeoVector3 normal, double distance)
        {
            GeoPoint3 at = nurbs.PointAt(t);
            GeoVector3 across = Direction(nurbs, t, side).CrossProduct(normal);
            double length = across.Length;

            if (!(length > 0.0))
            {
                throw new InvalidOperationException("The spline runs along the normal at " + t.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", where it has no side to be offset to.");
            }

            return at.Add(across.Multiply(distance / length));
        }

        /// <summary>A smooth piece of the spline offset: the cubic fitted to the true offset, its spans given more points until it keeps within the point tolerance both ways.</summary>
        private static Piece Fitted(DxfNurbs nurbs, List<double> spans, GeoVector3 normal, double distance)
        {
            int count = spans.Count - 1;
            var each = new int[count];

            for (int s = 0; s < count; s++)
            {
                each[s] = First;
            }

            var piece = new Piece { Normal = normal, Distance = distance };
            double slack = Tolerance.Default.EqualPoint;

            while (true)
            {
                var t = new List<double>();

                for (int s = 0; s < count; s++)
                {
                    for (int k = s == 0 ? 0 : 1; k <= each[s]; k++)
                    {
                        t.Add(k == each[s] ? spans[s + 1] : spans[s] + (spans[s + 1] - spans[s]) * k / each[s]);
                    }
                }

                piece.T = t;
                piece.P = new List<GeoPoint3>(t.Count);

                for (int i = 0; i < t.Count; i++)
                {
                    piece.P.Add(Moved(nurbs, t[i], i == 0 ? 1 : i == t.Count - 1 ? -1 : 0, normal, distance));
                }

                GeoSpline3 fit = Fit(nurbs, piece);

                if (fit == null)
                {
                    if (piece.Fit == null)
                    {
                        throw new InvalidOperationException("The spline's offset stands still: no curve passes through its points.");
                    }

                    return piece;
                }

                piece.Fit = fit;
                var bad = new bool[count];
                t = piece.T;

                for (int i = 0; i + 1 < t.Count; i++)
                {
                    int s = SpanOf(spans, 0.5 * (t[i] + t[i + 1]));

                    if (bad[s])
                    {
                        continue;
                    }

                    GeoPoint3 truth = Moved(nurbs, 0.5 * (t[i] + t[i + 1]), 0, normal, distance);
                    bool far = fit.GetClosestPointOnBoundary(truth).DistanceTo(truth) > slack;

                    if (!far && piece.U != null)
                    {
                        GeoPoint3 q = fit.GetPointAtParameter(0.5 * (piece.U[i] + piece.U[i + 1]));
                        far = Nearest(nurbs, q, t[i], t[i + 1], normal, distance) > slack;
                    }

                    bad[s] = far;
                }

                bool grew = false;

                for (int s = 0; s < count; s++)
                {
                    if (bad[s] && each[s] < Most)
                    {
                        each[s] *= 2;
                        grew = true;
                    }
                }

                if (!grew)
                {
                    return piece;
                }
            }
        }

        /// <summary>
        /// The cubic through a piece's points, leaving and reaching its ends in the spline's directions there, and the parameter
        /// of each point on it, the share of the chain through them before it, as the library's interpolation gives them; null
        /// where no curve passes through them.
        /// </summary>
        private static GeoSpline3 Fit(DxfNurbs nurbs, Piece piece)
        {
            // The points the interpolation keeps: one within the point tolerance of the one kept before goes.
            var kept = new List<int>();

            for (int i = 0; i < piece.P.Count; i++)
            {
                if (kept.Count == 0 || !piece.P[kept[kept.Count - 1]].IsEqualTo(piece.P[i], Tolerance.Default))
                {
                    kept.Add(i);
                }
            }

            if (kept.Count < 2)
            {
                return null;
            }

            var t = new List<double>(kept.Count);
            var p = new List<GeoPoint3>(kept.Count);

            foreach (int i in kept)
            {
                t.Add(piece.T[i]);
                p.Add(piece.P[i]);
            }

            piece.T = t;
            piece.P = p;
            double chain = 0.0;
            var u = new List<double>(p.Count) { 0.0 };

            for (int i = 1; i < p.Count; i++)
            {
                chain += p[i - 1].DistanceTo(p[i]);
                u.Add(chain);
            }

            piece.U = u.ConvertAll(x => chain > 0.0 ? x / chain : 0.0);
            piece.U[piece.U.Count - 1] = 1.0;

            try
            {
                return GeoSpline3.Interpolate(p, Direction(nurbs, t[0], 1), Direction(nurbs, t[t.Count - 1], -1), 3, Tolerance.Default);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>The span of the knots a parameter lies in: the last whose start is not above it.</summary>
        private static int SpanOf(List<double> spans, double t)
        {
            int low = 0;
            int high = spans.Count - 2;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;

                if (spans[mid] <= t)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        /// <summary>How far a point is from the true offset between two parameters, by golden-section steps along it.</summary>
        private static double Nearest(DxfNurbs nurbs, GeoPoint3 q, double a, double b, GeoVector3 normal, double distance)
        {
            double ratio = (Math.Sqrt(5.0) - 1.0) / 2.0;
            double x1 = b - ratio * (b - a);
            double x2 = a + ratio * (b - a);
            double f1 = Moved(nurbs, x1, 0, normal, distance).DistanceTo(q);
            double f2 = Moved(nurbs, x2, 0, normal, distance).DistanceTo(q);

            for (int i = 0; i < 40; i++)
            {
                if (f1 <= f2)
                {
                    b = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = b - ratio * (b - a);
                    f1 = Moved(nurbs, x1, 0, normal, distance).DistanceTo(q);
                }
                else
                {
                    a = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = a + ratio * (b - a);
                    f2 = Moved(nurbs, x2, 0, normal, distance).DistanceTo(q);
                }
            }

            return Math.Min(f1, f2);
        }

        /// <summary>
        /// Joins the end of one offset piece to the start of the next at a corner of the spline, as a polyline's offset joins two
        /// segments: where they part, the points of the lines run on along their ends to where those meet; where they overlap,
        /// none, each piece cut back to where its points cross the other's, refitted after; where neither, the line between
        /// their ends. Nothing where the ends meet within the point tolerance.
        /// </summary>
        private static List<GeoPoint3> Join(DxfNurbs nurbs, Piece a, Piece b, GeoVector3 normal)
        {
            GeoPoint3 end = a.P[a.P.Count - 1];
            GeoPoint3 start = b.P[0];

            if (end.IsEqualTo(start, Tolerance.Default))
            {
                return new List<GeoPoint3>();
            }

            GeoVector3 ta = Direction(nurbs, a.T[a.T.Count - 1], -1).Normalize();
            GeoVector3 tb = Direction(nurbs, b.T[0], 1).Normalize();
            GeoVector3 d = end.GetVectorTo(start);
            double ab = ta.DotProduct(tb);
            double det = ab * ab - 1.0;

            if (Math.Abs(det) > 1E-12)
            {
                // end + s ta = start + r tb.
                double s = (ab * d.DotProduct(tb) - d.DotProduct(ta)) / det;
                double r = (d.DotProduct(tb) - ab * d.DotProduct(ta)) / det;

                if (s > 0.0 && r < 0.0)
                {
                    return new List<GeoPoint3> { end, end.Add(ta.Multiply(s)), start };
                }

                if (s < 0.0 && r > 0.0 && Cross(a, b, normal))
                {
                    return new List<GeoPoint3>();
                }
            }

            return new List<GeoPoint3> { end, start };
        }

        /// <summary>
        /// Cuts two pieces back to where the chains through their points cross nearest their ends, in the plane of a normal: the
        /// first after its last crossing, the second before its first; false where they do not cross.
        /// </summary>
        private static bool Cross(Piece a, Piece b, GeoVector3 normal)
        {
            GeoVector3 u = Math.Abs(normal.X) < 0.9 ? new GeoVector3(1, 0, 0).CrossProduct(normal).Normalize() : new GeoVector3(0, 1, 0).CrossProduct(normal).Normalize();
            GeoVector3 v = normal.Normalize().CrossProduct(u);

            for (int i = a.P.Count - 2; i >= 0; i--)
            {
                for (int j = 0; j + 1 < b.P.Count; j++)
                {
                    if (Crossing(a.P[i], a.P[i + 1], b.P[j], b.P[j + 1], u, v, out double fa, out double fb))
                    {
                        GeoPoint3 x = a.P[i].Add(a.P[i].GetVectorTo(a.P[i + 1]).Multiply(fa));
                        double ta = a.T[i] + (a.T[i + 1] - a.T[i]) * fa;
                        double tb = b.T[j] + (b.T[j + 1] - b.T[j]) * fb;
                        a.T = a.T.GetRange(0, i + 1);
                        a.P = a.P.GetRange(0, i + 1);
                        a.T.Add(ta);
                        a.P.Add(x);
                        var bt = new List<double> { tb };
                        var bp = new List<GeoPoint3> { x };
                        bt.AddRange(b.T.GetRange(j + 1, b.T.Count - j - 1));
                        bp.AddRange(b.P.GetRange(j + 1, b.P.Count - j - 1));
                        b.T = bt;
                        b.P = bp;
                        a.Trimmed = true;
                        b.Trimmed = true;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether two segments cross in a plane of axes u and v, and the share along each where they do.</summary>
        private static bool Crossing(GeoPoint3 p1, GeoPoint3 p2, GeoPoint3 q1, GeoPoint3 q2, GeoVector3 u, GeoVector3 v, out double fa, out double fb)
        {
            GeoVector3 r = p1.GetVectorTo(p2);
            GeoVector3 s = q1.GetVectorTo(q2);
            GeoVector3 w = p1.GetVectorTo(q1);
            double rx = r.DotProduct(u), ry = r.DotProduct(v);
            double sx = s.DotProduct(u), sy = s.DotProduct(v);
            double wx = w.DotProduct(u), wy = w.DotProduct(v);
            double den = rx * sy - ry * sx;
            fa = 0.0;
            fb = 0.0;

            if (Math.Abs(den) <= 1E-300)
            {
                return false;
            }

            fa = (wx * sy - wy * sx) / den;
            fb = (wx * ry - wy * rx) / den;
            return fa >= 0.0 && fa <= 1.0 && fb >= 0.0 && fb <= 1.0;
        }

        /// <summary>A piece cut back fitted again through the points left, its ends leaving and reaching in the spline's directions there.</summary>
        private static GeoSpline3 Refit(DxfNurbs nurbs, Piece piece) => Fit(nurbs, piece) ?? piece.Fit;

        /// <summary>The knots, each the length along so far, and the homogeneous points of a fitted cubic.</summary>
        private static KeyValuePair<List<double>, List<double[]>> Part(GeoSpline3 fit)
        {
            double length = fit.Length;
            var knots = new List<double>(fit.Knots.Count);

            foreach (double knot in fit.Knots)
            {
                knots.Add(knot * length);
            }

            return new KeyValuePair<List<double>, List<double[]>>(knots, DxfNurbsEdit.Homogeneous(fit.ControlPoints, null));
        }

        /// <summary>A straight cubic from one point to another, over its length.</summary>
        private static KeyValuePair<List<double>, List<double[]>> Line(GeoPoint3 a, GeoPoint3 b)
        {
            double length = a.DistanceTo(b);
            GeoVector3 step = a.GetVectorTo(b).Divide(3.0);
            var points = new List<GeoPoint3> { a, a.Add(step), a.Add(step.Multiply(2.0)), b };
            var knots = new List<double> { 0.0, 0.0, 0.0, 0.0, length, length, length, length };
            return new KeyValuePair<List<double>, List<double[]>>(knots, DxfNurbsEdit.Homogeneous(points, null));
        }

        /// <summary>Two clamped cubics joined where the first ends, the second's knots moved on to follow the first's.</summary>
        private static KeyValuePair<List<double>, List<double[]>> Join(KeyValuePair<List<double>, List<double[]>> a, KeyValuePair<List<double>, List<double[]>> b)
        {
            const int P = 3;
            List<double> ua = a.Key;
            List<double> ub = b.Key;
            double end = ua[ua.Count - 1];
            double shift = end - ub[0];
            var knots = ua.GetRange(0, ua.Count - (P + 1));

            for (int i = 0; i < P; i++)
            {
                knots.Add(end);
            }

            for (int i = P + 1; i < ub.Count; i++)
            {
                knots.Add(ub[i] + shift);
            }

            var points = new List<double[]>(a.Value);

            for (int i = 1; i < b.Value.Count; i++)
            {
                points.Add(b.Value[i]);
            }

            return new KeyValuePair<List<double>, List<double[]>>(knots, points);
        }

        /// <summary>A smooth piece of the offset: its parameters on the spline, its points, their shares along the fit, and the fit.</summary>
        private sealed class Piece
        {
            internal GeoVector3 Normal { get; set; }

            internal double Distance { get; set; }

            internal List<double> T { get; set; }

            internal List<GeoPoint3> P { get; set; }

            internal List<double> U { get; set; }

            internal GeoSpline3 Fit { get; set; }

            internal bool Trimmed { get; set; }
        }
    }
}
