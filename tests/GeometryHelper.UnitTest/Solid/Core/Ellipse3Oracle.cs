using System;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// What the tests of the ellipse in space hold <see cref="GeoEllipse3"/> against, worked out without it. Each
    /// ellipse is built from a frame the test writes down, a centre C and three unit directions U (the major axis), V
    /// (the minor) and N (the normal), with U × V = N, so the oracle never reads the frame back from the type it is
    /// checking. A point is read in that frame as u along U, v along V and h along N. The plane questions are then
    /// those of the 2D ellipse centred on the origin along X, which <see cref="Ellipse2Oracle"/> answers by dense
    /// sampling and quadrature, and the closest points in space by a dense sample of the rim refined by golden section.
    /// </summary>
    internal static class Ellipse3Oracle
    {
        private const double Turn = 2.0 * Math.PI;

        #region Ellipses in space

        /// <summary>
        /// An ellipse written down by its frame: centre, major axis U, minor axis V, normal N, and the two radii.
        /// </summary>
        internal sealed class Specimen
        {
            public Specimen(GeoPoint3 center, GeoVector3 u, GeoVector3 v, double a, double b)
            {
                Center = center;
                U = u;
                V = v;
                N = u.CrossProduct(v);
                A = a;
                B = b;
            }

            public GeoPoint3 Center { get; }

            public GeoVector3 U { get; }

            public GeoVector3 V { get; }

            public GeoVector3 N { get; }

            public double A { get; }

            public double B { get; }

            /// <summary>The ellipse under test, built through its public constructor from this frame.</summary>
            public GeoEllipse3 Build() => new GeoEllipse3(Center, N, U, A, B);

            /// <summary>The same disc with its normal turned over: the same major axis, the minor axis reversed.</summary>
            public Specimen Flipped() => new Specimen(Center, U, V.Multiply(-1.0), A, B);

            /// <summary>The 2D ellipse this one is when laid out in its own frame: centred on the origin, along X.</summary>
            public GeoEllipse2 Flat() => new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, A, B);

            /// <summary>The point C + u U + v V + h N.</summary>
            public GeoPoint3 At(double u, double v, double h) =>
                Center.Add(U.Multiply(u)).Add(V.Multiply(v)).Add(N.Multiply(h));

            /// <summary>A point of the plane laid out, put back: C + x U + y V.</summary>
            public GeoPoint3 Lift(GeoPoint2 flat) => At(flat.X, flat.Y, 0.0);

            /// <summary>The rim point at eccentric angle t: C + a cos t U + b sin t V.</summary>
            public GeoPoint3 Rim(double t) => At(A * Math.Cos(t), B * Math.Sin(t), 0.0);

            /// <summary>
            /// The point an offset from the rim point at t, out along the rim's normal in the plane, and h above the
            /// plane: in-plane it lies exactly that offset from the rim (<see cref="Ellipse2Oracle.OffRim"/>).
            /// </summary>
            public GeoPoint3 OffRim(double t, double offset, double h) => Lift(Ellipse2Oracle.OffRim(Flat(), t, offset)).Add(N.Multiply(h));

            /// <summary>Reads a point in the frame.</summary>
            public void Read(GeoPoint3 point, out double u, out double v, out double h)
            {
                GeoVector3 d = Center.GetVectorTo(point);
                u = d.DotProduct(U);
                v = d.DotProduct(V);
                h = d.DotProduct(N);
            }

            /// <summary>The foot of a point on the plane, laid out.</summary>
            public GeoPoint2 Foot(GeoPoint3 point)
            {
                Read(point, out double u, out double v, out _);
                return new GeoPoint2(u, v);
            }
        }

        /// <summary>
        /// 300 by 100 about (40, -25, 60), tilted so that no axis lines up with X, Y or Z: N = (1, 2, 2)/3,
        /// U = (2, 1, -2)/3 and V = N × U = (-2, 2, -1)/3, all three rational, so the frame is orthonormal to rounding.
        /// </summary>
        public static Specimen Tilted() => new Specimen(
            new GeoPoint3(40, -25, 60), new GeoVector3(2.0 / 3, 1.0 / 3, -2.0 / 3), new GeoVector3(-2.0 / 3, 2.0 / 3, -1.0 / 3), 300, 100);

        /// <summary>The same frame as <see cref="Tilted"/> with other radii.</summary>
        public static Specimen Tilted(double a, double b) => new Specimen(
            new GeoPoint3(40, -25, 60), new GeoVector3(2.0 / 3, 1.0 / 3, -2.0 / 3), new GeoVector3(-2.0 / 3, 2.0 / 3, -1.0 / 3), a, b);

        /// <summary>An ellipse of the given radii about (40, -25, 60) in the plane z = 60, its major axis along X.</summary>
        public static Specimen Upright(double a, double b) => new Specimen(new GeoPoint3(40, -25, 60), GeoVector3.XAxis, GeoVector3.YAxis, a, b);

        /// <summary>
        /// A circle of radius 300 about (40, -25, 60) whose plane holds X and the direction 60 degrees up from Y,
        /// (0, cos 60°, sin 60°): its normal is (0, -sin 60°, cos 60°), 60 degrees from Z. Seen in plan it is an
        /// ellipse 300 by 300 cos 60° = 150, along X.
        /// </summary>
        public static GeoCircle3 CircleTiltedSixty(double degrees = 60)
        {
            double angle = degrees * Math.PI / 180;
            return new GeoCircle3(new GeoPoint3(40, -25, 60), new GeoVector3(0, -Math.Sin(angle), Math.Cos(angle)), 300);
        }

        #endregion

        #region The rim, sampled

        /// <summary>
        /// The rim point nearest a point in space: the rim sampled at 65 536 even steps of the eccentric angle, and every
        /// local minimum of the sample refined by golden section between its neighbours. The distance is the true 3D
        /// distance, so nothing here leans on the rim lying at one height.
        /// </summary>
        public static GeoPoint3 ClosestOnRim(Specimen e, GeoPoint3 point, out double distance)
        {
            const int samples = 65536;
            double step = Turn / samples;
            Func<double, double> gap = t => e.Rim(t).DistanceTo(point);

            var sampled = new double[samples];
            for (int k = 0; k < samples; k++)
            {
                sampled[k] = gap(k * step);
            }

            double bestT = 0.0;
            distance = double.PositiveInfinity;
            for (int k = 0; k < samples; k++)
            {
                double here = sampled[k];
                if (here <= sampled[(k + samples - 1) % samples] && here <= sampled[(k + 1) % samples])
                {
                    double t = Polish(e, point, Golden(gap, (k - 1) * step, (k + 1) * step, true), step);
                    double d = gap(t);
                    if (d < distance)
                    {
                        distance = d;
                        bestT = t;
                    }
                }
            }

            return e.Rim(bestT);
        }

        /// <summary>
        /// How far the rim reaches along a world direction, highest and lowest: the dot product of the rim with the
        /// direction sampled at 65 536 even steps, the best sample refined by golden section.
        /// </summary>
        public static void Reach(Specimen e, GeoVector3 direction, out double low, out double high)
        {
            const int samples = 65536;
            double step = Turn / samples;
            Func<double, double> along = t =>
            {
                GeoPoint3 p = e.Rim(t);
                return p.X * direction.X + p.Y * direction.Y + p.Z * direction.Z;
            };

            int kMax = 0;
            int kMin = 0;
            for (int k = 1; k < samples; k++)
            {
                if (along(k * step) > along(kMax * step)) kMax = k;
                if (along(k * step) < along(kMin * step)) kMin = k;
            }

            high = along(Golden(along, (kMax - 1) * step, (kMax + 1) * step, false));
            low = along(Golden(along, (kMin - 1) * step, (kMin + 1) * step, true));
        }

        /// <summary>
        /// Golden section finds the angle of a minimum only to about the root of the rounding, the gap being flat there, so
        /// the angle is polished by Newton on the slope of the squared gap, (P - p)·P', which crosses nought there, its own
        /// slope |P'|² + (P - p)·P'' with P'' = -(P - C). A step of more than the sampling step is not taken.
        /// </summary>
        private static double Polish(Specimen e, GeoPoint3 point, double t, double step)
        {
            for (int i = 0; i < 8; i++)
            {
                GeoVector3 d = point.GetVectorTo(e.Rim(t));
                GeoVector3 speed = e.U.Multiply(-e.A * Math.Sin(t)).Add(e.V.Multiply(e.B * Math.Cos(t)));
                GeoVector3 bend = e.Center.GetVectorTo(e.Rim(t)).Multiply(-1.0);
                double slope = d.DotProduct(speed);
                double curve = speed.DotProduct(speed) + d.DotProduct(bend);

                if (!(curve > 0.0) || Math.Abs(slope / curve) > step)
                {
                    break;
                }

                t -= slope / curve;
            }

            return t;
        }

        private static double Golden(Func<double, double> f, double lo, double hi, bool minimum)
        {
            double sign = minimum ? 1.0 : -1.0;
            double ratio = (Math.Sqrt(5.0) - 1.0) / 2.0;
            double x1 = hi - ratio * (hi - lo);
            double x2 = lo + ratio * (hi - lo);
            double f1 = sign * f(x1);
            double f2 = sign * f(x2);

            for (int i = 0; i < 200 && hi - lo > 1E-15; i++)
            {
                if (f1 < f2)
                {
                    hi = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = hi - ratio * (hi - lo);
                    f1 = sign * f(x1);
                }
                else
                {
                    lo = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = lo + ratio * (hi - lo);
                    f2 = sign * f(x2);
                }
            }

            return 0.5 * (lo + hi);
        }

        #endregion

        #region Reading a result

        /// <summary>
        /// Reads a point in the frame an ellipse under test reports, the minor axis taken as N × U here rather than read
        /// from the type, so that a wrong <see cref="GeoEllipse3.MinorAxis"/> cannot hide a wrong rim.
        /// </summary>
        public static void ReadIn(GeoEllipse3 e, GeoPoint3 point, out double u, out double v, out double h)
        {
            GeoVector3 d = e.Center.GetVectorTo(point);
            GeoVector3 minor = e.Normal.CrossProduct(e.MajorAxis);
            u = d.DotProduct(e.MajorAxis);
            v = d.DotProduct(minor);
            h = d.DotProduct(e.Normal);
        }

        /// <summary>
        /// How far a point near the rim of an ellipse under test lies off it in its plane, to first order, and how far
        /// off its plane; both near nought for a point of its rim.
        /// </summary>
        public static double GapToRim(GeoEllipse3 e, GeoPoint3 point, out double offPlane)
        {
            ReadIn(e, point, out double u, out double v, out offPlane);
            var flat = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, e.MajorRadius, e.MinorRadius);
            return Ellipse2Oracle.GapToRim(flat, new GeoPoint2(u, v));
        }

        /// <summary>The eccentric angle of a point in the frame of an ellipse under test, atan2(v/b, u/a), in [0, 2π).</summary>
        public static double AngleIn(GeoEllipse3 e, GeoPoint3 point)
        {
            ReadIn(e, point, out double u, out double v, out _);
            double t = Math.Atan2(v / e.MinorRadius, u / e.MajorRadius);
            return t < 0.0 ? t + Turn : t;
        }

        #endregion
    }
}
