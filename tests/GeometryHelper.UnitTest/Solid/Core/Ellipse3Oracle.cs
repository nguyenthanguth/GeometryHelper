using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;
using Xunit;

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

        #region Shapes placed for the queries

        /// <summary>The unit tangent of the rim at t, the way t runs: (-a sin t U + b cos t V) over its length.</summary>
        public static GeoVector3 Tangent(Specimen e, double t) =>
            e.U.Multiply(-e.A * Math.Sin(t)).Add(e.V.Multiply(e.B * Math.Cos(t))).Normalize();

        /// <summary>
        /// The normal of a plane holding a line through the ellipse's plane, leaning from that plane by an angle about the
        /// line: the ellipse's normal N turned by the angle about the line's direction D, N cos θ + (D × N) sin θ.
        /// </summary>
        public static GeoVector3 LeanAbout(Specimen e, GeoVector3 along, double leanRad)
        {
            GeoVector3 d = along.Normalize();
            return e.N.Multiply(Math.Cos(leanRad)).Add(d.CrossProduct(e.N).Multiply(Math.Sin(leanRad)));
        }

        /// <summary>
        /// An ellipse in the plane of a given unit normal through two given points of that plane. Its major axis is turned
        /// by an angle from the line joining them, its minor radius is the given ratio of its major, and its centre is
        /// moved off the joining line by an offset. Scaled across the major axis by 1/ratio the ellipse is a circle, the
        /// two points stay equally far from a centre on their scaled bisector, so both are on the rim exactly:
        /// with the frame of the axis (u, v) about the midpoint, and the half chord (du, dv), the centre is
        /// (-offset dv / ratio, ratio offset du) and the major radius √((du - cu)² + ((dv - cv) / ratio)²).
        /// </summary>
        public static Specimen ThroughTwo(GeoPoint3 first, GeoPoint3 second, GeoVector3 normal, double axisTurnRad, double ratio, double offset)
        {
            GeoVector3 n = normal.Normalize();
            GeoVector3 chord = first.GetVectorTo(second);
            GeoVector3 l = chord.Normalize();
            GeoVector3 m = n.CrossProduct(l);
            GeoVector3 u = l.Multiply(Math.Cos(axisTurnRad)).Add(m.Multiply(Math.Sin(axisTurnRad)));
            GeoVector3 v = n.CrossProduct(u);
            GeoPoint3 middle = first.Add(chord.Multiply(0.5));

            double du = 0.5 * chord.DotProduct(u);
            double dv = 0.5 * chord.DotProduct(v);
            double cu = -offset * dv / ratio;
            double cv = ratio * offset * du;
            double a = Math.Sqrt((du - cu) * (du - cu) + (dv - cv) * (dv - cv) / (ratio * ratio));

            return new Specimen(middle.Add(u.Multiply(cu)).Add(v.Multiply(cv)), u, v, a, ratio * a);
        }

        /// <summary>
        /// An ellipse a by b in the plane of a given unit normal whose rim touches a line of that plane at a point: the rim
        /// point at t0 is the point and the rim's tangent there runs along the line. With the line's direction D and
        /// M = N × D, the axis is U = D cos α + M sin α, where tan α = b cos t0 / (a sin t0) makes the tangent's part along
        /// M vanish, and the centre is the point less a cos t0 U + b sin t0 V.
        /// </summary>
        public static Specimen TouchingLineAt(GeoPoint3 point, GeoVector3 along, GeoVector3 normal, double a, double b, double t0)
        {
            GeoVector3 n = normal.Normalize();
            GeoVector3 d = along.Normalize();
            GeoVector3 m = n.CrossProduct(d);
            double alpha = Math.Atan2(b * Math.Cos(t0), a * Math.Sin(t0));
            GeoVector3 u = d.Multiply(Math.Cos(alpha)).Add(m.Multiply(Math.Sin(alpha)));
            GeoVector3 v = n.CrossProduct(u);
            GeoPoint3 center = point.Add(u.Multiply(-a * Math.Cos(t0))).Add(v.Multiply(-b * Math.Sin(t0)));

            return new Specimen(center, u, v, a, b);
        }

        /// <summary>A circle in space through two points of a plane, its centre an offset along the plane's bisector of them.</summary>
        public static GeoCircle3 CircleThroughTwo(GeoPoint3 first, GeoPoint3 second, GeoVector3 normal, double offset)
        {
            GeoVector3 n = normal.Normalize();
            GeoVector3 chord = first.GetVectorTo(second);
            GeoPoint3 center = first.Add(chord.Multiply(0.5)).Add(n.CrossProduct(chord.Normalize()).Multiply(offset));
            return new GeoCircle3(center, n, center.DistanceTo(first));
        }

        /// <summary>The 2D ellipse another ellipse in the same plane is when laid out in the frame of a first one.</summary>
        public static GeoEllipse2 LaidOutIn(Specimen frame, Specimen other)
        {
            frame.Read(other.Center, out double cu, out double cv, out _);
            double axisU = other.U.DotProduct(frame.U);
            double axisV = other.U.DotProduct(frame.V);
            return new GeoEllipse2(new GeoPoint2(cu, cv), new GeoVector2(axisU, axisV).Normalize(), other.A, other.B);
        }

        #endregion

        #region Comparing crossings

        /// <summary>
        /// Asserts that two lists of crossings name the same places in the same order, each point within a distance of
        /// its partner.
        /// </summary>
        public static void SamePointsInOrder(IReadOnlyList<GeoPoint3> expected, IReadOnlyList<GeoPoint3> actual, double within)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                double gap = expected[i].DistanceTo(actual[i]);
                Assert.True(gap <= within, $"crossing {i}: expected {expected[i]}, got {actual[i]}, {gap:E2} apart");
            }
        }

        /// <summary>
        /// Asserts that two lists of crossings name the same places in any order: each expected point has an actual
        /// one within the distance, and no actual point is left over.
        /// </summary>
        public static void SamePoints(IReadOnlyList<GeoPoint3> expected, IReadOnlyList<GeoPoint3> actual, double within)
        {
            Assert.Equal(expected.Count, actual.Count);
            var left = actual.ToList();
            foreach (GeoPoint3 point in expected)
            {
                GeoPoint3 nearest = left.OrderBy(p => p.DistanceTo(point)).First();
                Assert.True(nearest.DistanceTo(point) <= within, $"expected {point}, nearest found {nearest}, {nearest.DistanceTo(point):E2} apart");
                left.Remove(nearest);
            }
        }

        /// <summary>The points sorted along a direction, as a line through them would give them.</summary>
        public static GeoPoint3[] Along(GeoVector3 direction, params GeoPoint3[] points) =>
            points.OrderBy(p => p.X * direction.X + p.Y * direction.Y + p.Z * direction.Z).ToArray();

        #endregion

        #region Random cases for the queries

        /// <summary>How far every random case keeps clear of the default point tolerance: twenty times over.</summary>
        internal const double Clear = 20 * Tolerance.DefaultEqualPoint;

        /// <summary>The kinds of random case, each a pair type of decision 20 placed so its answer is known.</summary>
        internal static readonly string[] QueryKinds =
        {
            "plane across", "plane nearly one", "plane touching", "segment across", "segment piercing", "segment through the disc",
            "ray piercing", "ellipse across", "circle across", "arc across", "ellipse nearly one", "ellipse touching the line",
            "ellipse in one plane", "ellipse a ten-thousandth apart",
        };

        /// <summary>Asks the ellipse to try a crossing.</summary>
        internal delegate bool TryCrossing(GeoEllipse3 ellipse, Tolerance tolerance, out GeoPoint3[] found);

        /// <summary>
        /// One random query and what it must answer: the ellipse, the crossings expected, in order or as a set, how near
        /// they must come, and whether the two touch; the calls that ask the ellipse; and, when the ellipse is a circle,
        /// the calls that ask the same of <see cref="GeoCircle3"/>.
        /// </summary>
        internal sealed class QueryCase
        {
            public string Kind { get; set; }

            public string Description { get; set; }

            public Specimen Ellipse { get; set; }

            public GeoPoint3[] Expected { get; set; }

            public bool Ordered { get; set; }

            public double Within { get; set; }

            public bool Collides { get; set; }

            /// <summary>Whether the answer is read off planes nearly one or 1E-4 apart, or a graze, where the circle is no oracle.</summary>
            public bool IllConditioned { get; set; }

            public Func<GeoEllipse3, Tolerance, GeoPoint3[]> Cross { get; set; }

            public Func<GeoEllipse3, Tolerance, bool> Collide { get; set; }

            public TryCrossing Try { get; set; }

            public Func<Tolerance, GeoPoint3[]> CircleCross { get; set; }

            public Func<Tolerance, bool> CircleCollide { get; set; }

            public override string ToString() => $"{Kind}: {Description}";
        }

        /// <summary>
        /// Builds a random case of a kind, drawing again until the case keeps clear of every tolerance band: chords and
        /// sags of at least <see cref="Clear"/>, crossings at least two degrees apart in direction, nothing else of the two
        /// shapes within <see cref="Clear"/> of each other, and planes either one or tilted so each rim strays five times
        /// the tolerance from the other's plane (<see cref="StandOff"/>). With <paramref name="circle"/> the ellipse is a circle, and so is any curve it is paired with,
        /// so that the circle can be asked the same.
        /// </summary>
        internal static QueryCase MakeCase(string kind, Random random, bool circle)
        {
            for (int attempt = 0; attempt < 10000; attempt++)
            {
                QueryCase made = TryMake(kind, random, circle);
                if (made != null)
                {
                    return made;
                }
            }

            throw new InvalidOperationException($"no clear case of {kind} drawn in 10 000 attempts");
        }

        private static QueryCase TryMake(string kind, Random random, bool circle)
        {
            Specimen e = RandomEllipse(random, circle);
            double scale = Math.Sqrt(e.Center.X * e.Center.X + e.Center.Y * e.Center.Y + e.Center.Z * e.Center.Z) + e.A;
            double close = 1E-9 * scale;
            const double nearlyOne = Tolerance.DefaultEqualPoint;
            const double graze = 1E-4;
            double t1 = Draw(random, 0, Turn), t2 = Draw(random, 0, Turn);
            GeoPoint3 first = e.Rim(t1), second = e.Rim(t2);
            GeoVector3 chord = first.GetVectorTo(second);
            string where = $"ellipse {e.A:R} x {e.B:R} about {e.Center} N {e.N} U {e.U}, t {t1:R}, {t2:R}";
            var made = new QueryCase { Kind = kind, Ellipse = e, Within = close };

            bool across = kind != "plane touching" && kind != "segment piercing" && kind != "segment through the disc"
                && kind != "ray piercing" && kind != "ellipse touching the line" && kind != "ellipse in one plane" && kind != "ellipse a ten-thousandth apart";
            if (across && !ChordClear(e, t1, t2))
            {
                return null;
            }

            switch (kind)
            {
                case "plane across":
                case "plane nearly one":
                {
                    double lean = kind == "plane across" ? Draw(random, 5, 90) : Math.Pow(10, Draw(random, -3, 0));
                    var plane = new GeoPlane3(first, LeanAbout(e, chord, lean * Math.PI / 180));
                    if (StandOff(e, plane.Normal) < 5 * Tolerance.DefaultEqualPlanar)
                    {
                        return null;
                    }

                    made.Description = $"{where}, lean {lean:R} deg";
                    made.Expected = Along(e.N.CrossProduct(plane.Normal), first, second);
                    made.Ordered = true;
                    made.Collides = true;
                    made.IllConditioned = kind == "plane nearly one";
                    made.Within = made.IllConditioned ? nearlyOne : close;
                    made.Cross = (x, t) => x.GetIntersections(plane, t);
                    made.Collide = (x, t) => x.CollidesWith(plane, t);
                    made.Try = (GeoEllipse3 x, Tolerance t, out GeoPoint3[] f) => x.TryIntersectWith(plane, out f, t);
                    made.CircleCross = t => AsCircle(e).GetIntersections(plane, t);
                    made.CircleCollide = t => AsCircle(e).CollidesWith(plane, t);
                    return made;
                }

                case "plane touching":
                {
                    double lean = Draw(random, 5, 90);
                    var plane = new GeoPlane3(first, LeanAbout(e, Tangent(e, t1), lean * Math.PI / 180));
                    made.Description = $"{where}, lean {lean:R} deg";
                    made.Expected = new[] { first };
                    made.Collides = true;
                    made.IllConditioned = true;
                    made.Within = graze;
                    made.Cross = (x, t) => x.GetIntersections(plane, t);
                    made.Collide = (x, t) => x.CollidesWith(plane, t);
                    made.Try = (GeoEllipse3 x, Tolerance t, out GeoPoint3[] f) => x.TryIntersectWith(plane, out f, t);
                    made.CircleCross = t => AsCircle(e).GetIntersections(plane, t);
                    made.CircleCollide = t => AsCircle(e).CollidesWith(plane, t);
                    return made;
                }

                case "segment across":
                {
                    double before = Draw(random, 0.05, 0.5), after = Draw(random, 0.05, 0.5);
                    var line = new GeoLine3(first.Add(chord.Multiply(-before)), second.Add(chord.Multiply(after)));

                    // The circle reads a near graze by the discriminant against the square of the segment's length, so a
                    // chord whose half is under the tolerance times that length is one point to it: keep clear of it.
                    if (Math.Min(before, after) * chord.Length < Clear || (circle && 0.5 * chord.Length < 2 * Tolerance.DefaultEqualPoint * line.Length))
                    {
                        return null;
                    }

                    made.Description = $"{where}, before {before:R}, after {after:R}";
                    made.Expected = new[] { first, second };
                    made.Ordered = true;
                    made.Collides = true;
                    made.Cross = (x, t) => x.GetIntersections(line, t);
                    made.Collide = (x, t) => x.CollidesWith(line, t);
                    made.Try = (GeoEllipse3 x, Tolerance t, out GeoPoint3[] f) => x.TryIntersectWith(line, out f, t);
                    made.CircleCross = t => AsCircle(e).GetIntersections(line, t);
                    made.CircleCollide = t => AsCircle(e).CollidesWith(line, t);
                    return made;
                }

                case "segment piercing":
                case "ray piercing":
                case "segment through the disc":
                {
                    double tilt = Draw(random, 0, 80) * Math.PI / 180, turn = Draw(random, 0, Turn);
                    GeoVector3 d = e.N.Multiply(Math.Cos(tilt)).Add(e.U.Multiply(Math.Sin(tilt) * Math.Cos(turn))).Add(e.V.Multiply(Math.Sin(tilt) * Math.Sin(turn)));
                    GeoPoint3 through = first;
                    if (kind == "segment through the disc")
                    {
                        double shrink = Draw(random, 0, 0.98);
                        through = e.At(e.A * Math.Cos(t1) * shrink, e.B * Math.Sin(t1) * shrink, 0);
                        if (RimDistance(e, through) < Clear)
                        {
                            return null;
                        }
                    }

                    double below = Draw(random, 1, 100), above = Draw(random, 1, 100);

                    // The circle reads a line nearly touching its sphere by the same length-scaled band: a line through a
                    // rim point whose other crossing of the sphere is under the tolerance times its length away is one
                    // point to it, the foot of the centre, off the rim. Keep the circle's cases clear of that.
                    double length = kind == "ray piercing" ? 1.0 : below + above;
                    if (circle && Math.Abs(e.Center.GetVectorTo(through).DotProduct(d)) < 2 * Tolerance.DefaultEqualPoint * length)
                    {
                        return null;
                    }

                    made.Description = $"{where}, tilt {tilt:R}, turn {turn:R}";
                    made.Expected = kind == "segment through the disc" ? new GeoPoint3[0] : new[] { first };
                    made.Collides = made.Expected.Length > 0;
                    if (kind == "ray piercing")
                    {
                        var ray = new GeoRay3(through.Add(d.Multiply(-below)), d);
                        made.Cross = (x, t) => x.GetIntersections(ray, t);
                        made.Collide = (x, t) => x.CollidesWith(ray, t);
                        made.Try = (GeoEllipse3 x, Tolerance t, out GeoPoint3[] f) => x.TryIntersectWith(ray, out f, t);
                        made.CircleCross = t => AsCircle(e).GetIntersections(ray, t);
                        made.CircleCollide = t => AsCircle(e).CollidesWith(ray, t);
                    }
                    else
                    {
                        var line = new GeoLine3(through.Add(d.Multiply(-below)), through.Add(d.Multiply(above)));
                        made.Cross = (x, t) => x.GetIntersections(line, t);
                        made.Collide = (x, t) => x.CollidesWith(line, t);
                        made.Try = (GeoEllipse3 x, Tolerance t, out GeoPoint3[] f) => x.TryIntersectWith(line, out f, t);
                        made.CircleCross = t => AsCircle(e).GetIntersections(line, t);
                        made.CircleCollide = t => AsCircle(e).CollidesWith(line, t);
                    }

                    return made;
                }

                case "ellipse across":
                case "ellipse nearly one":
                {
                    double lean = kind == "ellipse across" ? Draw(random, 5, 90) : Math.Pow(10, Draw(random, -3, 0));
                    double sign = random.NextDouble() < 0.5 ? -1 : 1;
                    Specimen other = ThroughTwo(first, second, LeanAbout(e, chord, sign * lean * Math.PI / 180),
                        Draw(random, 0, Math.PI), circle ? 1.0 : Math.Pow(10, Draw(random, -2, 0)), Draw(random, -1.5, 1.5));
                    if (other.B < 0.1 || other.A > 5000 || StandOff(e, other.N) < 5 * Tolerance.DefaultEqualPlanar
                        || StandOff(other, e.N) < 5 * Tolerance.DefaultEqualPlanar || !CurvesClear(e, other, new[] { first, second }))
                    {
                        return null;
                    }

                    made.Description = $"{where}, lean {sign * lean:R} deg, other {other.A:R} x {other.B:R} about {other.Center} U {other.U}";
                    made.Expected = kind == "ellipse across" ? Along(e.N.CrossProduct(other.N), first, second) : new[] { first, second };
                    made.Ordered = kind == "ellipse across";
                    made.Collides = true;
                    made.IllConditioned = kind == "ellipse nearly one";
                    made.Within = made.IllConditioned ? nearlyOne : close;
                    SetEllipsePartner(made, e, other);
                    return made;
                }

                case "circle across":
                case "arc across":
                {
                    double lean = Draw(random, 5, 90) * (random.NextDouble() < 0.5 ? -1 : 1);
                    GeoCircle3 partner = CircleThroughTwo(first, second, LeanAbout(e, chord, lean * Math.PI / 180), Draw(random, -1.5, 1.5) * chord.Length);
                    var asSpecimen = new Specimen(partner.Center, partner.Center.GetVectorTo(first).Normalize(), partner.Normal.CrossProduct(partner.Center.GetVectorTo(first).Normalize()), partner.Radius, partner.Radius);
                    if (partner.Radius > 5000 || !CurvesClear(e, asSpecimen, new[] { first, second }))
                    {
                        return null;
                    }

                    made.Collides = true;
                    made.Ordered = true;
                    if (kind == "circle across")
                    {
                        made.Description = $"{where}, lean {lean:R} deg, circle {partner.Radius:R} about {partner.Center}";
                        made.Expected = Along(e.N.CrossProduct(partner.Normal), first, second);
                        made.Cross = (x, t) => x.GetIntersections(partner, t);
                        made.Collide = (x, t) => x.CollidesWith(partner, t);
                        made.Try = (GeoEllipse3 x, Tolerance t, out GeoPoint3[] f) => x.TryIntersectWith(partner, out f, t);
                        made.CircleCross = t => AsCircle(e).GetIntersections(partner, t);
                        made.CircleCollide = t => AsCircle(e).CollidesWith(partner, t);
                        return made;
                    }

                    // An arc of the circle over both points, by its far side, or about the first only.
                    GeoPoint3 far = partner.Center.Add(first.Add(chord.Multiply(0.5)).GetVectorTo(partner.Center).Normalize().Multiply(partner.Radius));
                    GeoVector3 toSecond = partner.Center.GetVectorTo(second);
                    double secondAt = Math.Abs(Math.Atan2(toSecond.DotProduct(asSpecimen.V), toSecond.DotProduct(asSpecimen.U)));
                    bool both = random.NextDouble() < 0.5;
                    double half = Math.Min(0.3, 0.5 * secondAt);
                    if ((!both && (secondAt - half) * partner.Radius < Clear) || half * partner.Radius < Clear)
                    {
                        return null;
                    }

                    GeoArc3 arc = both
                        ? GeoArc3.FromThreePoints(first, far, second)
                        : GeoArc3.FromThreePoints(asSpecimen.Rim(-half), asSpecimen.Rim(0.3 * half), asSpecimen.Rim(half));
                    made.Description = $"{where}, lean {lean:R} deg, arc of {partner.Radius:R} about {partner.Center}, {(both ? "over both" : "about the first")}";
                    made.Expected = both ? Along(arc.Normal.CrossProduct(e.N), first, second) : new[] { first };
                    made.Cross = (x, t) => x.GetIntersections(arc, t);
                    made.Collide = (x, t) => x.CollidesWith(arc, t);
                    made.Try = (GeoEllipse3 x, Tolerance t, out GeoPoint3[] f) => x.TryIntersectWith(arc, out f, t);
                    made.CircleCross = t => AsCircle(e).GetIntersections(arc, t);
                    made.CircleCollide = t => AsCircle(e).CollidesWith(arc, t);
                    return made;
                }

                case "ellipse touching the line":
                {
                    GeoVector3 line = Tangent(e, t1);
                    double along = Draw(random, 0.2, 1.5) * e.A * (random.NextDouble() < 0.5 ? -1 : 1);
                    double lean = Draw(random, 5, 90) * (random.NextDouble() < 0.5 ? -1 : 1);
                    GeoPoint3 beyond = first.Add(line.Multiply(along));
                    Specimen other = ThroughTwo(first, beyond, LeanAbout(e, line, lean * Math.PI / 180),
                        Draw(random, 0, Math.PI), circle ? 1.0 : Math.Pow(10, Draw(random, -2, 0)), Draw(random, -1.5, 1.5));
                    if (other.B < 0.1 || other.A > 5000 || RimDistance(e, beyond) < Clear || !CurvesClear(e, other, new[] { first }))
                    {
                        return null;
                    }

                    made.Description = $"{where}, along {along:R}, lean {lean:R} deg, other {other.A:R} x {other.B:R} about {other.Center} U {other.U}";
                    made.Expected = new[] { first };
                    made.Collides = true;
                    made.IllConditioned = true;
                    made.Within = graze;
                    SetEllipsePartner(made, e, other);
                    return made;
                }

                case "ellipse in one plane":
                case "ellipse a ten-thousandth apart":
                {
                    double turn = Draw(random, 0, Math.PI);
                    GeoVector3 u = e.U.Multiply(Math.Cos(turn)).Add(e.V.Multiply(Math.Sin(turn)));
                    double a = circle ? e.A * Math.Pow(10, Draw(random, -1, 0.3)) : e.A * Math.Pow(10, Draw(random, -1, 0.3));
                    double b = circle ? a : Math.Max(0.1, a * Math.Pow(10, Draw(random, -2, 0)));
                    double lift = kind == "ellipse in one plane" ? 0.0 : (random.NextDouble() < 0.5 ? -1E-4 : 1E-4);
                    var inPlane = new Specimen(e.At(Draw(random, -1.5, 1.5) * e.A, Draw(random, -1.5, 1.5) * e.B, 0), u, e.N.CrossProduct(u), Math.Max(a, b), Math.Min(a, b));
                    var other = new Specimen(inPlane.Center.Add(e.N.Multiply(lift)), inPlane.U, inPlane.V, inPlane.A, inPlane.B);
                    GeoPoint3[] crossings = Ellipse2Oracle.RimCrossings(e.Flat(), Ellipse2Oracle.SideOfEllipse(LaidOutIn(e, inPlane))).Select(e.Lift).ToArray();
                    if (!PointsApart(crossings) || !CurvesClear(e, inPlane, crossings))
                    {
                        return null;
                    }

                    made.Description = $"{where}, other {other.A:R} x {other.B:R} about {other.Center} U {other.U}, lifted {lift:R}";
                    made.Expected = crossings;
                    made.Collides = crossings.Length > 0;
                    made.Within = lift == 0.0 ? close : nearlyOne;
                    made.IllConditioned = lift != 0.0;
                    SetEllipsePartner(made, e, other);
                    return made;
                }

                default:
                    throw new ArgumentException(kind, nameof(kind));
            }
        }

        /// <summary>
        /// How far the rim strays either way from the plane through its centre square to a normal: √((a N·U)² + (b N·V)²).
        /// The circle's test, its sine times its radius (Arc3.cs:49-51), is this for a circle; for an ellipse that test
        /// read with the major radius, this one, and one read with the minor radius disagree between b sin θ and a sin θ, so
        /// the random cases keep this five times the tolerance out, where all three call the planes apart.
        /// </summary>
        internal static double StandOff(Specimen e, GeoVector3 normal)
        {
            GeoVector3 n = normal.Normalize();
            double alongU = e.A * n.DotProduct(e.U), alongV = e.B * n.DotProduct(e.V);
            return Math.Sqrt(alongU * alongU + alongV * alongV);
        }

        /// <summary>The ellipse partner of a case: asked as an ellipse, and, when both are circles, asked of the circle too.</summary>
        private static void SetEllipsePartner(QueryCase made, Specimen e, Specimen other)
        {
            GeoEllipse3 partner = other.Build();
            made.Cross = (x, t) => x.GetIntersections(partner, t);
            made.Collide = (x, t) => x.CollidesWith(partner, t);
            made.Try = (GeoEllipse3 x, Tolerance t, out GeoPoint3[] f) => x.TryIntersectWith(partner, out f, t);
            if (e.A == e.B && other.A == other.B)
            {
                GeoCircle3 asCircle = AsCircle(other);
                made.CircleCross = t => AsCircle(e).GetIntersections(asCircle, t);
                made.CircleCollide = t => AsCircle(e).CollidesWith(asCircle, t);
            }
        }

        /// <summary>The circle a specimen with equal radii is.</summary>
        internal static GeoCircle3 AsCircle(Specimen e) => new GeoCircle3(e.Center, e.N, e.A);

        /// <summary>
        /// A random ellipse: its centre anywhere within 1 000 of the origin, its normal and axis any way, its major radius
        /// from 1 to 1 000 spread evenly in its logarithm, and its minor radius down to a thousandth of that, no less than 0.1.
        /// </summary>
        internal static Specimen RandomEllipse(Random random, bool circle)
        {
            var center = new GeoPoint3(Draw(random, -1000, 1000), Draw(random, -1000, 1000), Draw(random, -1000, 1000));
            GeoVector3 n = RandomUnit(random);
            GeoVector3 seed = Math.Abs(n.X) < 0.9 ? GeoVector3.XAxis : GeoVector3.YAxis;
            GeoVector3 u0 = n.CrossProduct(seed).Normalize();
            GeoVector3 v0 = n.CrossProduct(u0);
            double turn = Draw(random, 0, Turn);
            GeoVector3 u = u0.Multiply(Math.Cos(turn)).Add(v0.Multiply(Math.Sin(turn)));
            double a = Math.Pow(10, Draw(random, 0, 3));
            double b = circle ? a : a * Math.Pow(10, Draw(random, Math.Log10(Math.Max(1E-3, 0.1 / a)), 0));
            return new Specimen(center, u, n.CrossProduct(u), a, b);
        }

        private static GeoVector3 RandomUnit(Random random)
        {
            double z = Draw(random, -1, 1), phi = Draw(random, 0, Turn), s = Math.Sqrt(1 - z * z);
            return new GeoVector3(s * Math.Cos(phi), s * Math.Sin(phi), z);
        }

        private static double Draw(Random random, double low, double high) => low + (high - low) * random.NextDouble();

        /// <summary>
        /// Whether the chord between two rim points is clear of the graze band: at least <see cref="Clear"/> long, and the
        /// rim on either side of it reaching at least that far from its line.
        /// </summary>
        private static bool ChordClear(Specimen e, double t1, double t2)
        {
            GeoPoint3 first = e.Rim(t1), second = e.Rim(t2);
            if (first.DistanceTo(second) < Clear)
            {
                return false;
            }

            GeoVector3 along = first.GetVectorTo(second).Normalize();
            double low = t1 < t2 ? t1 : t2, high = t1 < t2 ? t2 : t1;
            double inner = 0, outer = 0;
            for (int k = 1; k < 512; k++)
            {
                double s = k / 512.0;
                inner = Math.Max(inner, along.CrossProduct(first.GetVectorTo(e.Rim(low + s * (high - low)))).Length);
                outer = Math.Max(outer, along.CrossProduct(first.GetVectorTo(e.Rim(high + s * (Turn - (high - low))))).Length);
            }

            return inner >= Clear && outer >= Clear;
        }

        /// <summary>Whether no two points of a list are within <see cref="Clear"/> of each other.</summary>
        private static bool PointsApart(GeoPoint3[] points)
        {
            for (int i = 0; i < points.Length; i++)
            {
                for (int j = i + 1; j < points.Length; j++)
                {
                    if (points[i].DistanceTo(points[j]) < Clear)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Whether two rims come within <see cref="Clear"/> of each other only within 5 of the given points, each of them
        /// on both rims to 1E-9 of their size and crossed at two degrees or more: each rim sampled at 16 384 steps and
        /// measured to the other exactly.
        /// </summary>
        private static bool CurvesClear(Specimen first, Specimen second, GeoPoint3[] shared)
        {
            foreach (GeoPoint3 point in shared)
            {
                if (RimDistance(first, point) > 1E-9 * (first.A + second.A + 1) || RimDistance(second, point) > 1E-9 * (first.A + second.A + 1))
                {
                    return false;
                }

                first.Read(point, out double u1, out double v1, out _);
                second.Read(point, out double u2, out double v2, out _);
                GeoVector3 d1 = Tangent(first, Math.Atan2(v1 / first.B, u1 / first.A));
                GeoVector3 d2 = Tangent(second, Math.Atan2(v2 / second.B, u2 / second.A));
                if (d1.CrossProduct(d2).Length < Math.Sin(2 * Math.PI / 180))
                {
                    return false;
                }
            }

            return OnlyNear(first, second, shared) && OnlyNear(second, first, shared);
        }

        private static bool OnlyNear(Specimen walked, Specimen measured, GeoPoint3[] shared)
        {
            const int samples = 16384;
            for (int k = 0; k < samples; k++)
            {
                GeoPoint3 p = walked.Rim(k * Turn / samples);
                if (RimDistance(measured, p) < Clear && !shared.Any(q => q.DistanceTo(p) < 5))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The distance in space from a point to the rim of an ellipse: √(h² + d²), d the exact distance in the plane.</summary>
        internal static double RimDistance(Specimen e, GeoPoint3 point)
        {
            e.Read(point, out double u, out double v, out double h);
            double d = FlatDistance(e.A, e.B, u, v);
            return Math.Sqrt(h * h + d * d);
        }

        /// <summary>
        /// The distance from (x, y) to the ellipse x²/a² + y²/b² = 1, a ≥ b, by Eberly's robust bisection on the root of
        /// (a z0/(s + r))² + (z1/(s + 1))² = 1 (Distance from a Point to an Ellipse, 2013), written out here so the
        /// oracle shares nothing with the library.
        /// </summary>
        internal static double FlatDistance(double a, double b, double x, double y)
        {
            double y0 = Math.Abs(x), y1 = Math.Abs(y);
            if (y1 > 0)
            {
                if (y0 > 0)
                {
                    double z0 = y0 / a, z1 = y1 / b, g = z0 * z0 + z1 * z1 - 1;
                    if (g == 0)
                    {
                        return 0;
                    }

                    double r0 = (a / b) * (a / b);
                    double n0 = r0 * z0;
                    double s0 = z1 - 1;
                    double s1 = g < 0 ? 0 : Math.Sqrt(n0 * n0 + z1 * z1) - 1;
                    double s = 0;
                    for (int i = 0; i < 1100; i++)
                    {
                        s = 0.5 * (s0 + s1);
                        if (s == s0 || s == s1)
                        {
                            break;
                        }

                        double ratio0 = n0 / (s + r0), ratio1 = z1 / (s + 1);
                        double f = ratio0 * ratio0 + ratio1 * ratio1 - 1;
                        if (f > 0)
                        {
                            s0 = s;
                        }
                        else if (f < 0)
                        {
                            s1 = s;
                        }
                        else
                        {
                            break;
                        }
                    }

                    double x0 = r0 * y0 / (s + r0), x1 = y1 / (s + 1);
                    return Math.Sqrt((x0 - y0) * (x0 - y0) + (x1 - y1) * (x1 - y1));
                }

                return Math.Abs(y1 - b);
            }

            double numer = a * y0, denom = a * a - b * b;
            if (numer < denom)
            {
                double xa = numer / denom;
                double px = a * xa, py = b * Math.Sqrt(1 - xa * xa);
                return Math.Sqrt((px - y0) * (px - y0) + py * py);
            }

            return Math.Abs(y0 - a);
        }

        #endregion
    }
}
