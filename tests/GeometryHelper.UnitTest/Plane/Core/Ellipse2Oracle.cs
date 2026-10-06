using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// What the ellipse tests hold <see cref="GeoEllipse2"/> against, worked out without it: lengths by adaptive
    /// Gauss-Kronrod quadrature of the speed round the rim, closest points by a dense sample refined by golden section,
    /// and the sag of a chord by golden section on the rim it cuts off. None of it uses the arithmetic-geometric mean,
    /// Carlson's integrals or Eberly's bisection the type is built on, so an agreement between the two means something.
    /// Everything is read in the ellipse's own frame: x along the major axis and y along the minor, from the centre.
    /// </summary>
    internal static class Ellipse2Oracle
    {
        private const double Turn = 2.0 * Math.PI;

        #region Ellipses

        /// <summary>
        /// 300 by 100 about (40, -25), its major axis turned 30 degrees, so that neither axis lines up with X or Y.
        /// Its rim is 1 336.489 long, and its curvature runs from 1/33.3 at the ends of the major axis to 1/900 at the
        /// ends of the minor.
        /// </summary>
        public static GeoEllipse2 Tilted() =>
            new GeoEllipse2(new GeoPoint2(40, -25), new GeoVector2(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)), 300, 100);

        /// <summary>
        /// An ellipse of the given radii about (40, -25), its major axis along X, so that a point on one of its axes has
        /// a frame coordinate of exactly nought and no tie is decided by rounding.
        /// </summary>
        public static GeoEllipse2 Upright(double majorRadius, double minorRadius) =>
            new GeoEllipse2(new GeoPoint2(40, -25), GeoVector2.XAxis, majorRadius, minorRadius);

        #endregion

        #region The frame

        /// <summary>Reads a point in the ellipse's frame.</summary>
        public static void ToFrame(GeoEllipse2 ellipse, GeoPoint2 point, out double x, out double y)
        {
            double dx = point.X - ellipse.Center.X;
            double dy = point.Y - ellipse.Center.Y;
            x = dx * ellipse.MajorAxis.X + dy * ellipse.MajorAxis.Y;
            y = -dx * ellipse.MajorAxis.Y + dy * ellipse.MajorAxis.X;
        }

        /// <summary>The point at frame coordinates (x, y).</summary>
        public static GeoPoint2 FromFrame(GeoEllipse2 ellipse, double x, double y) => new GeoPoint2(
            ellipse.Center.X + x * ellipse.MajorAxis.X - y * ellipse.MajorAxis.Y,
            ellipse.Center.Y + x * ellipse.MajorAxis.Y + y * ellipse.MajorAxis.X);

        /// <summary>The rim point at eccentric angle t: (a cos t, b sin t) in the frame.</summary>
        public static GeoPoint2 Rim(GeoEllipse2 ellipse, double t) =>
            FromFrame(ellipse, ellipse.MajorRadius * Math.Cos(t), ellipse.MinorRadius * Math.Sin(t));

        /// <summary>
        /// The point an offset from the rim point at t along the outward normal, (b cos t, a sin t) made unit: outside
        /// when the offset is positive. A point so moved lies exactly that far from the rim, inside as long as the offset
        /// stays below the radius of curvature there.
        /// </summary>
        public static GeoPoint2 OffRim(GeoEllipse2 ellipse, double t, double offset)
        {
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double nx = b * Math.Cos(t);
            double ny = a * Math.Sin(t);
            double n = Math.Sqrt(nx * nx + ny * ny);
            return FromFrame(ellipse, a * Math.Cos(t) + offset * nx / n, b * Math.Sin(t) + offset * ny / n);
        }

        /// <summary>The eccentric angle of a point, atan2(y/b, x/a), in [0, 2 pi).</summary>
        public static double AngleOf(GeoEllipse2 ellipse, GeoPoint2 point)
        {
            ToFrame(ellipse, point, out double x, out double y);
            double t = Math.Atan2(y / ellipse.MinorRadius, x / ellipse.MajorRadius);
            return t < 0.0 ? t + Turn : t;
        }

        /// <summary>
        /// How far a point near the rim lies off it, to first order: the rim's equation over the length of its gradient.
        /// Good to rounding for a point a hair off; not a distance for a point far away.
        /// </summary>
        public static double GapToRim(GeoEllipse2 ellipse, GeoPoint2 point)
        {
            ToFrame(ellipse, point, out double x, out double y);
            double a2 = ellipse.MajorRadius * ellipse.MajorRadius;
            double b2 = ellipse.MinorRadius * ellipse.MinorRadius;
            double f = x * x / a2 + y * y / b2 - 1.0;
            double gx = 2.0 * x / a2;
            double gy = 2.0 * y / b2;
            return Math.Abs(f) / Math.Sqrt(gx * gx + gy * gy);
        }

        #endregion

        #region Length along the rim

        /// <summary>
        /// The length along the rim of an a by b ellipse from eccentric angle <paramref name="from"/> to
        /// <paramref name="to"/>: the integral of the speed (a² sin² t + b² cos² t)^½, by adaptive 15-point Gauss-Kronrod,
        /// each panel halved until its Kronrod and Gauss sums agree to 1E-15 of the panel.
        /// </summary>
        public static double ArcLength(double a, double b, double from, double to)
        {
            Func<double, double> speed = t =>
            {
                double s = a * Math.Sin(t);
                double c = b * Math.Cos(t);
                return Math.Sqrt(s * s + c * c);
            };

            return Adaptive(speed, from, to, 0);
        }

        /// <summary>The whole rim of an a by b ellipse, by <see cref="ArcLength"/> once round.</summary>
        public static double Perimeter(double a, double b) => ArcLength(a, b, 0.0, Turn);

        private static double Adaptive(Func<double, double> f, double lo, double hi, int depth)
        {
            Kronrod(f, lo, hi, out double kronrod, out double gauss);

            if (depth >= 48 || Math.Abs(kronrod - gauss) <= 1E-15 * Math.Abs(kronrod))
            {
                return kronrod;
            }

            double mid = 0.5 * (lo + hi);
            return Adaptive(f, lo, mid, depth + 1) + Adaptive(f, mid, hi, depth + 1);
        }

        // The nodes and weights of QUADPACK's QK15: Kronrod on 15 points, the 7-point Gauss rule on every other one.
        // Checked in Python: the Kronrod sums integrate x^0..x^22 and the Gauss sums x^0..x^13 over [-1, 1] to rounding.
        private static readonly double[] Xk =
        {
            0.991455371120812639206854697526329, 0.949107912342758524526189684047851,
            0.864864423359769072789712788640926, 0.741531185599394439863864773280788,
            0.586087235467691130294144845693013, 0.405845151377397166906606412076961,
            0.207784955007898467600689403773245, 0.0,
        };

        private static readonly double[] Wk =
        {
            0.022935322010529224963732008058970, 0.063092092629978553290700663189204,
            0.104790010322250183839876322541518, 0.140653259715525918745189590510238,
            0.169004726639267902826583426598550, 0.190350578064785409913256402421014,
            0.204432940075298892414161999234649, 0.209482141084727828012999174891714,
        };

        private static readonly double[] Wg =
        {
            0.129484966168869693270611432679082, 0.279705391489276667901467771423780,
            0.381830050505118944950369775488975, 0.417959183673469387755102040816327,
        };

        private static void Kronrod(Func<double, double> f, double lo, double hi, out double kronrod, out double gauss)
        {
            double half = 0.5 * (hi - lo);
            double mid = 0.5 * (hi + lo);
            double centre = f(mid);

            kronrod = Wk[7] * centre;
            gauss = Wg[3] * centre;

            for (int i = 0; i < 7; i++)
            {
                double pair = f(mid - half * Xk[i]) + f(mid + half * Xk[i]);
                kronrod += Wk[i] * pair;

                if (i % 2 == 1)
                {
                    gauss += Wg[i / 2] * pair;
                }
            }

            kronrod *= half;
            gauss *= half;
        }

        #endregion

        #region Closest point

        /// <summary>
        /// The shortest distance from frame point (x, y) to the rim of an a by b ellipse: the rim sampled at 65 536 even
        /// steps of the eccentric angle, and every local minimum of the sample refined by golden section between its
        /// neighbours. The gap has at most two minima, so refining each one finds the least.
        /// </summary>
        public static double ClosestDistance(double a, double b, double x, double y)
        {
            const int samples = 65536;
            double step = Turn / samples;
            Func<double, double> gap = t =>
            {
                double dx = a * Math.Cos(t) - x;
                double dy = b * Math.Sin(t) - y;
                return Math.Sqrt(dx * dx + dy * dy);
            };

            var sampled = new double[samples];
            for (int k = 0; k < samples; k++)
            {
                sampled[k] = gap(k * step);
            }

            double best = double.PositiveInfinity;
            for (int k = 0; k < samples; k++)
            {
                double here = sampled[k];
                if (here <= sampled[(k + samples - 1) % samples] && here <= sampled[(k + 1) % samples])
                {
                    best = Math.Min(best, Golden(gap, (k - 1) * step, (k + 1) * step, true));
                }
            }

            return best;
        }

        #endregion

        #region Sag of a chord

        /// <summary>
        /// The largest gap between a polygon inscribed in the ellipse and the rim: for each edge, the rim between its
        /// ends is searched by golden section for the point furthest from the chord. The ends are placed on the rim by
        /// their eccentric angles, read independently of the type with <see cref="AngleOf"/>.
        /// </summary>
        public static double LargestSag(GeoEllipse2 ellipse, GeoPolygon2 polygon)
        {
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double worst = 0.0;

            for (int i = 0; i < polygon.VertexCount; i++)
            {
                ToFrame(ellipse, polygon[i], out double x0, out double y0);
                ToFrame(ellipse, polygon[(i + 1) % polygon.VertexCount], out double x1, out double y1);

                double t0 = AngleOf(ellipse, polygon[i]);
                double t1 = AngleOf(ellipse, polygon[(i + 1) % polygon.VertexCount]);
                if (t1 <= t0)
                {
                    t1 += Turn;
                }

                double dx = x1 - x0;
                double dy = y1 - y0;
                double chord = Math.Sqrt(dx * dx + dy * dy);
                Func<double, double> off = t => Math.Abs((a * Math.Cos(t) - x0) * dy - (b * Math.Sin(t) - y0) * dx) / chord;

                worst = Math.Max(worst, Golden(off, t0, t1, false));
            }

            return worst;
        }

        #endregion

        #region Shapes placed in the frame

        /// <summary>The segment between two points given in the ellipse's frame.</summary>
        public static GeoLine2 FrameLine(GeoEllipse2 ellipse, double x0, double y0, double x1, double y1) =>
            new GeoLine2(FromFrame(ellipse, x0, y0), FromFrame(ellipse, x1, y1));

        /// <summary>The polygon through points given in the ellipse's frame as x, y pairs.</summary>
        public static GeoPolygon2 FramePolygon(GeoEllipse2 ellipse, params double[] xy) => new GeoPolygon2(FramePoints(ellipse, xy));

        /// <summary>The polyline through points given in the ellipse's frame as x, y pairs.</summary>
        public static GeoPolyline2 FramePolyline(GeoEllipse2 ellipse, params double[] xy) => new GeoPolyline2(FramePoints(ellipse, xy));

        /// <summary>The points given in the ellipse's frame as x, y pairs.</summary>
        public static GeoPoint2[] FramePoints(GeoEllipse2 ellipse, params double[] xy)
        {
            var points = new GeoPoint2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = FromFrame(ellipse, xy[2 * i], xy[2 * i + 1]);
            }

            return points;
        }

        /// <summary>The circle about a point given in the ellipse's frame.</summary>
        public static GeoCircle2 FrameCircle(GeoEllipse2 ellipse, double x, double y, double radius) =>
            new GeoCircle2(FromFrame(ellipse, x, y), radius);

        /// <summary>
        /// The arc about a point given in the ellipse's frame, from one angle to another counter-clockwise, the angles
        /// read in the frame, so that the arc sits on the ellipse the same way however the ellipse is turned.
        /// </summary>
        public static GeoArc2 FrameArc(GeoEllipse2 ellipse, double x, double y, double radius, double fromRad, double toRad)
        {
            double turn = Math.Atan2(ellipse.MajorAxis.Y, ellipse.MajorAxis.X);
            return new GeoArc2(FromFrame(ellipse, x, y), radius, fromRad + turn, toRad + turn);
        }

        /// <summary>An ellipse placed in the frame of another: its centre at (x, y) and its major axis turned by an angle from the other's.</summary>
        public static GeoEllipse2 FrameEllipse(GeoEllipse2 ellipse, double x, double y, double turnRad, double majorRadius, double minorRadius)
        {
            double turn = Math.Atan2(ellipse.MajorAxis.Y, ellipse.MajorAxis.X) + turnRad;
            return new GeoEllipse2(FromFrame(ellipse, x, y), new GeoVector2(Math.Cos(turn), Math.Sin(turn)), majorRadius, minorRadius);
        }

        #endregion

        #region Shapes a gap above the top

        // Each of these lies above the end of the minor axis, (0, b) in the frame, nearest it at (0, b + gap) and only
        // there: on a flat side square to the minor axis, or on a curve tighter than the rim's 900 there for the 300 by
        // 100, so the gap between them is the gap asked for, exactly in the frame of an upright ellipse. A flat side is
        // used rather than a corner because a corner's sides, run on, cross the rim well beyond the corner, where no
        // crossing is reported however loose the tolerance; a flat side is read in the tangent band.

        /// <summary>The segment y = b + gap from x = -200 to 200.</summary>
        public static GeoLine2 LineAbove(GeoEllipse2 e, double gap) =>
            FrameLine(e, -200, e.MinorRadius + gap, 200, e.MinorRadius + gap);

        /// <summary>A trough whose flat bottom is y = b + gap from x = -100 to 100.</summary>
        public static GeoPolyline2 PolylineAbove(GeoEllipse2 e, double gap) => FramePolyline(
            e, -200, e.MinorRadius + gap + 50, -100, e.MinorRadius + gap, 100, e.MinorRadius + gap, 200, e.MinorRadius + gap + 50);

        /// <summary>A triangle on its side y = b + gap from x = -100 to 100, its apex above.</summary>
        public static GeoPolygon2 PolygonAbove(GeoEllipse2 e, double gap) =>
            FramePolygon(e, -100, e.MinorRadius + gap, 100, e.MinorRadius + gap, 0, e.MinorRadius + 100);

        /// <summary>The same triangle as a <see cref="GeoTriangle2"/>.</summary>
        public static GeoTriangle2 TriangleAbove(GeoEllipse2 e, double gap)
        {
            GeoPolygon2 polygon = PolygonAbove(e, gap);
            return new GeoTriangle2(polygon[0], polygon[1], polygon[2]);
        }

        /// <summary>A 100 by 60 rectangle square to the axes, its bottom side y = b + gap.</summary>
        public static GeoRectangle2 RectangleAbove(GeoEllipse2 e, double gap) =>
            new GeoRectangle2(FromFrame(e, 0, e.MinorRadius + gap + 30), 100, 60, Math.Atan2(e.MajorAxis.Y, e.MajorAxis.X));

        /// <summary>The circle of radius 50 standing on (0, b + gap).</summary>
        public static GeoCircle2 CircleAbove(GeoEllipse2 e, double gap) => FrameCircle(e, 0, e.MinorRadius + gap + 50, 50);

        /// <summary>The lower half of <see cref="CircleAbove"/>.</summary>
        public static GeoArc2 ArcAbove(GeoEllipse2 e, double gap) => FrameArc(e, 0, e.MinorRadius + gap + 50, 50, Math.PI, 2 * Math.PI);

        /// <summary>A chain: the lower half of <see cref="CircleAbove"/>, left to right, then a segment up and away.</summary>
        public static GeoPolylineArc2 ChainAbove(GeoEllipse2 e, double gap) => new GeoPolylineArc2(
            FramePoints(e, -50, e.MinorRadius + gap + 50, 50, e.MinorRadius + gap + 50, 100, e.MinorRadius + 200), new[] { 1.0, 0.0 });

        /// <summary>A loop: the lower half of <see cref="CircleAbove"/>, closed by two segments up to a point above.</summary>
        public static GeoPolygonArc2 LoopAbove(GeoEllipse2 e, double gap) => new GeoPolygonArc2(
            FramePoints(e, -50, e.MinorRadius + gap + 50, 50, e.MinorRadius + gap + 50, 0, e.MinorRadius + 150), new[] { 1.0, 0.0, 0.0 });

        /// <summary>A copy of the ellipse standing on (0, b + gap).</summary>
        public static GeoEllipse2 EllipseAbove(GeoEllipse2 e, double gap) =>
            FrameEllipse(e, 0, 2 * e.MinorRadius + gap, 0, e.MajorRadius, e.MinorRadius);

        #endregion

        #region Distances to other shapes

        /// <summary>x²/a² + y²/b² − 1 for a point in the ellipse's frame: negative inside, positive outside.</summary>
        public static double Equation(GeoEllipse2 ellipse, GeoPoint2 point)
        {
            ToFrame(ellipse, point, out double x, out double y);
            double u = x / ellipse.MajorRadius;
            double v = y / ellipse.MinorRadius;
            return u * u + v * v - 1.0;
        }

        /// <summary>The distance from a point to a segment, in closed form: to the foot of the perpendicular, or to the nearer end.</summary>
        public static double PointToSegment(GeoPoint2 point, GeoLine2 line)
        {
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            double length2 = dx * dx + dy * dy;
            double s = length2 > 0.0 ? ((point.X - line.StartPoint.X) * dx + (point.Y - line.StartPoint.Y) * dy) / length2 : 0.0;
            s = Math.Max(0.0, Math.Min(1.0, s));
            double fx = line.StartPoint.X + s * dx - point.X;
            double fy = line.StartPoint.Y + s * dy - point.Y;
            return Math.Sqrt(fx * fx + fy * fy);
        }

        /// <summary>
        /// The distance from a point to an arc, in closed form: to the circle carrying it where the direction from the
        /// centre falls within the sweep, and to the nearer end otherwise.
        /// </summary>
        public static double PointToArc(GeoPoint2 point, GeoArc2 arc)
        {
            double angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
            double into = arc.SweptAngle > 0.0 ? angle - arc.StartAngle : arc.StartAngle - angle;
            into = into - Turn * Math.Floor(into / Turn);

            if (into <= Math.Abs(arc.SweptAngle))
            {
                return Math.Abs(point.DistanceTo(arc.Center) - arc.Radius);
            }

            return Math.Min(point.DistanceTo(arc.StartPoint), point.DistanceTo(arc.EndPoint));
        }

        /// <summary>The distance from a point to the edges of a polygon, its outline only.</summary>
        public static double PointToOutline(GeoPoint2 point, GeoPolygon2 polygon)
        {
            double best = double.PositiveInfinity;
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                best = Math.Min(best, PointToSegment(point, new GeoLine2(polygon[i], polygon[(i + 1) % polygon.VertexCount])));
            }

            return best;
        }

        /// <summary>
        /// The least of a distance from the rim to something: the rim sampled at 65 536 even steps of t, and every local
        /// minimum of the sample refined by golden section between its neighbours. Read with a closed-form distance from a
        /// point, this is the gap between the rim and a curve that does not cross it, from inside or out.
        /// </summary>
        public static double RimTo(GeoEllipse2 ellipse, Func<GeoPoint2, double> distance)
        {
            const int samples = 65536;
            double step = Turn / samples;
            Func<double, double> along = t => distance(Rim(ellipse, t));

            var sampled = new double[samples];
            for (int k = 0; k < samples; k++)
            {
                sampled[k] = along(k * step);
            }

            double best = double.PositiveInfinity;
            for (int k = 0; k < samples; k++)
            {
                double here = sampled[k];
                if (here <= sampled[(k + samples - 1) % samples] && here <= sampled[(k + 1) % samples])
                {
                    best = Math.Min(best, Math.Min(here, along(GoldenAt(along, (k - 1) * step, (k + 1) * step))));
                }
            }

            return best;
        }

        /// <summary>
        /// The shortest distance between the rims of two ellipses that do not cross. Each rim is sampled at 1 024 even
        /// steps of t and 1 024 even steps of the direction of its normal, which crowds the samples round the sharp ends
        /// of a thin ellipse; every pair of samples is measured, the pairs nearest for a local minimum along the first rim
        /// are polished by Newton's method on the squared distance in (t, s), and the least is kept. It shares nothing
        /// with the closest-point method of the type.
        /// </summary>
        public static double RimToRim(GeoEllipse2 first, GeoEllipse2 second)
        {
            double[] ts = RimSamples(first);
            double[] ss = RimSamples(second);
            var px = new double[ss.Length];
            var py = new double[ss.Length];
            for (int j = 0; j < ss.Length; j++)
            {
                GeoPoint2 q = Rim(second, ss[j]);
                px[j] = q.X;
                py[j] = q.Y;
            }

            int n = ts.Length;
            var nearest = new double[n];
            var at = new int[n];
            for (int i = 0; i < n; i++)
            {
                GeoPoint2 p = Rim(first, ts[i]);
                double best = double.PositiveInfinity;
                for (int j = 0; j < ss.Length; j++)
                {
                    double dx = p.X - px[j], dy = p.Y - py[j];
                    double d2 = dx * dx + dy * dy;
                    if (d2 < best)
                    {
                        best = d2;
                        at[i] = j;
                    }
                }

                nearest[i] = best;
            }

            double least = double.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (nearest[i] <= nearest[(i + n - 1) % n] && nearest[i] <= nearest[(i + 1) % n])
                {
                    least = Math.Min(least, Polish(first, second, ts[i], ss[at[i]]));
                }
            }

            return least;
        }

        private static double[] RimSamples(GeoEllipse2 ellipse)
        {
            const int n = 1024;
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            var ts = new double[2 * n];
            for (int k = 0; k < n; k++)
            {
                ts[2 * k] = Turn * k / n;
                double normal = Turn * (k + 0.5) / n;
                double t = Math.Atan2(a * Math.Sin(normal), b * Math.Cos(normal));
                ts[2 * k + 1] = t < 0.0 ? t + Turn : t;
            }

            Array.Sort(ts);
            return ts;
        }

        /// <summary>Newton's method on half the squared distance between P(t) on one rim and Q(s) on the other, halving any step that does not bring them nearer.</summary>
        private static double Polish(GeoEllipse2 first, GeoEllipse2 second, double t, double s)
        {
            double Half(double tt, double ss)
            {
                GeoPoint2 p = Rim(first, tt), q = Rim(second, ss);
                double dx = p.X - q.X, dy = p.Y - q.Y;
                return 0.5 * (dx * dx + dy * dy);
            }

            double f = Half(t, s);
            for (int iteration = 0; iteration < 100; iteration++)
            {
                Derivatives(first, t, out double p1x, out double p1y, out double d1x, out double d1y, out double dd1x, out double dd1y);
                Derivatives(second, s, out double p2x, out double p2y, out double d2x, out double d2y, out double dd2x, out double dd2y);
                double dx = p1x - p2x, dy = p1y - p2y;
                double g1 = dx * d1x + dy * d1y;
                double g2 = -(dx * d2x + dy * d2y);
                double h11 = d1x * d1x + d1y * d1y + dx * dd1x + dy * dd1y;
                double h22 = d2x * d2x + d2y * d2y - dx * dd2x - dy * dd2y;
                double h12 = -(d1x * d2x + d1y * d2y);
                double det = h11 * h22 - h12 * h12;

                double st, ss2;
                if (h11 > 0.0 && det > 0.0)
                {
                    st = -(h22 * g1 - h12 * g2) / det;
                    ss2 = -(h11 * g2 - h12 * g1) / det;
                }
                else
                {
                    double scale = Math.Max(Math.Max(Math.Abs(h11), Math.Abs(h22)), 1.0);
                    st = -g1 / scale;
                    ss2 = -g2 / scale;
                }

                bool moved = false;
                for (int halving = 0; halving < 60; halving++)
                {
                    double candidate = Half(t + st, s + ss2);
                    if (candidate < f)
                    {
                        t += st;
                        s += ss2;
                        f = candidate;
                        moved = true;
                        break;
                    }

                    st *= 0.5;
                    ss2 *= 0.5;
                }

                if (!moved || Math.Abs(st) + Math.Abs(ss2) < 1E-16)
                {
                    break;
                }
            }

            return Math.Sqrt(2.0 * f);
        }

        private static void Derivatives(GeoEllipse2 e, double t, out double px, out double py, out double dx, out double dy, out double ddx, out double ddy)
        {
            double c = Math.Cos(t), s = Math.Sin(t);
            double a = e.MajorRadius, b = e.MinorRadius;
            double mx = e.MajorAxis.X, my = e.MajorAxis.Y;
            px = e.Center.X + a * c * mx - b * s * my;
            py = e.Center.Y + a * c * my + b * s * mx;
            dx = -a * s * mx - b * c * my;
            dy = -a * s * my + b * c * mx;
            ddx = -(px - e.Center.X);
            ddy = -(py - e.Center.Y);
        }

        #endregion

        #region Crossings

        /// <summary>
        /// The points where the rim crosses a curve given by a side function, negative on one side of it and positive on
        /// the other: the rim sampled at 65 536 even steps of t, and every change of sign closed in on by bisection.
        /// Sorted by t from 0. A touching, where the sign does not change, is not found: the tests work those out by hand.
        /// </summary>
        public static GeoPoint2[] RimCrossings(GeoEllipse2 ellipse, Func<GeoPoint2, double> side)
        {
            const int samples = 65536;
            double step = Turn / samples;
            var found = new List<GeoPoint2>();
            double before = side(Rim(ellipse, 0.0));

            for (int k = 1; k <= samples; k++)
            {
                double here = side(Rim(ellipse, k * step));
                if ((before < 0.0) != (here < 0.0))
                {
                    double lo = (k - 1) * step, hi = k * step;
                    for (int i = 0; i < 100; i++)
                    {
                        double mid = 0.5 * (lo + hi);
                        if ((side(Rim(ellipse, mid)) < 0.0) == (before < 0.0))
                        {
                            lo = mid;
                        }
                        else
                        {
                            hi = mid;
                        }
                    }

                    found.Add(Rim(ellipse, 0.5 * (lo + hi)));
                }

                before = here;
            }

            return found.ToArray();
        }

        /// <summary>The side of a circle a point lies on: its distance from the centre less the radius.</summary>
        public static Func<GeoPoint2, double> SideOfCircle(GeoPoint2 center, double radius) => p => p.DistanceTo(center) - radius;

        /// <summary>
        /// The side of a convex polygon a point lies on: the largest of its signed distances to the lines carrying the
        /// edges, positive outside, nought on the outline, whichever way the corners run.
        /// </summary>
        public static Func<GeoPoint2, double> SideOfConvex(params GeoPoint2[] corners)
        {
            double twiceArea = 0.0;
            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint2 p = corners[i], q = corners[(i + 1) % corners.Length];
                twiceArea += p.X * q.Y - q.X * p.Y;
            }

            double turn = twiceArea > 0.0 ? 1.0 : -1.0;
            return point =>
            {
                double side = double.NegativeInfinity;
                for (int i = 0; i < corners.Length; i++)
                {
                    GeoPoint2 p = corners[i], q = corners[(i + 1) % corners.Length];
                    double dx = q.X - p.X, dy = q.Y - p.Y;
                    double left = (dx * (point.Y - p.Y) - dy * (point.X - p.X)) / Math.Sqrt(dx * dx + dy * dy);
                    side = Math.Max(side, -turn * left);
                }

                return side;
            };
        }

        /// <summary>The side of an ellipse a point lies on, by its equation.</summary>
        public static Func<GeoPoint2, double> SideOfEllipse(GeoEllipse2 other) => p => Equation(other, p);

        #endregion

        /// <summary>The least (or greatest) value of a function with one extreme between two angles, by golden section.</summary>
        private static double Golden(Func<double, double> f, double lo, double hi, bool least)
        {
            double ratio = (Math.Sqrt(5.0) - 1.0) / 2.0;

            for (int i = 0; i < 200 && hi - lo > 1E-15 * Math.Max(1.0, Math.Abs(hi)); i++)
            {
                double left = hi - ratio * (hi - lo);
                double right = lo + ratio * (hi - lo);
                bool keepLeft = least ? f(left) < f(right) : f(left) > f(right);

                if (keepLeft)
                {
                    hi = right;
                }
                else
                {
                    lo = left;
                }
            }

            return f(0.5 * (lo + hi));
        }

        /// <summary>Where a function with one minimum between two angles is least, by golden section.</summary>
        private static double GoldenAt(Func<double, double> f, double lo, double hi)
        {
            double ratio = (Math.Sqrt(5.0) - 1.0) / 2.0;

            for (int i = 0; i < 200 && hi - lo > 1E-15 * Math.Max(1.0, Math.Abs(hi)); i++)
            {
                double left = hi - ratio * (hi - lo);
                double right = lo + ratio * (hi - lo);

                if (f(left) < f(right))
                {
                    hi = right;
                }
                else
                {
                    lo = left;
                }
            }

            return 0.5 * (lo + hi);
        }
    }
}
