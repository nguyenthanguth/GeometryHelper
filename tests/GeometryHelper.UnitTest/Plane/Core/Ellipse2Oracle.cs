using System;
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
    }
}
