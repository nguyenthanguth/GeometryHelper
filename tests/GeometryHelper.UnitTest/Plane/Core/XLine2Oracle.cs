using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// What an infinite line does to a shape in the plane, worked out in closed form from the shape's own numbers and
    /// sharing no code with the library: the shape is taken to pieces (segments, arcs, a whole ellipse, a ray, a line), and
    /// for each piece the signed distance from the line runs over an interval that is read off the piece.
    /// </summary>
    /// <remarks>
    /// Signed distance from the line <c>o + t d</c> is <c>f(p) = n . (p - o)</c> with <c>n = (-d.y, d.x)</c>. A segment's
    /// <c>f</c> runs between its two ends; an arc's is <c>s + r cos(a - phi)</c>, which runs between the ends and the
    /// extremes the arc reaches; a whole ellipse's is <c>s + R cos(a - psi)</c>. The distance to a piece is nought when its
    /// interval holds nought and the nearer end of the interval otherwise, and the distance to a shape is the least over its
    /// boundary: an infinite line cannot lie wholly inside a closed shape, so a shape filled or not is as far as its
    /// boundary. The crossings are the roots of <c>f</c> on the piece.
    /// </remarks>
    internal abstract class OraclePiece
    {
        /// <summary>The least and the greatest signed distance of the piece from the line.</summary>
        internal abstract void Interval(GeoXLine2 line, out double lo, out double hi);

        /// <summary>The points where the line crosses the piece, each root once.</summary>
        internal abstract IEnumerable<GeoPoint2> Crossings(GeoXLine2 line);

        /// <summary>The distance of a point from the piece.</summary>
        internal abstract double DistanceFrom(GeoPoint2 point);

        /// <summary>Whether the line passes so near an end or a tangent of the piece that a tolerance may decide the answer.</summary>
        internal abstract bool Delicate(GeoXLine2 line, double margin);

        internal static double Signed(GeoXLine2 line, GeoPoint2 p) => -line.Direction.Y * (p.X - line.Origin.X) + line.Direction.X * (p.Y - line.Origin.Y);

        /// <summary>The sine of the angle tolerance of the tolerance the tests ask under, a degree.</summary>
        internal static readonly double AngleSine = XLine2Probe.Strict.EqualAngleSin;

        internal static double Gap(double lo, double hi) => lo > 0 ? lo : hi < 0 ? -hi : 0.0;
    }

    internal sealed class SegmentPiece : OraclePiece
    {
        private readonly GeoPoint2 _a;
        private readonly GeoPoint2 _b;

        internal SegmentPiece(GeoPoint2 a, GeoPoint2 b)
        {
            _a = a;
            _b = b;
        }

        internal override void Interval(GeoXLine2 line, out double lo, out double hi)
        {
            double fa = Signed(line, _a);
            double fb = Signed(line, _b);

            lo = Math.Min(fa, fb);
            hi = Math.Max(fa, fb);
        }

        internal override IEnumerable<GeoPoint2> Crossings(GeoXLine2 line)
        {
            double fa = Signed(line, _a);
            double fb = Signed(line, _b);

            if ((fa < 0 && fb > 0) || (fa > 0 && fb < 0))
            {
                double t = fa / (fa - fb);

                yield return new GeoPoint2(_a.X + t * (_b.X - _a.X), _a.Y + t * (_b.Y - _a.Y));
            }
        }

        internal override double DistanceFrom(GeoPoint2 p)
        {
            double dx = _b.X - _a.X;
            double dy = _b.Y - _a.Y;
            double t = Math.Max(0, Math.Min(1, ((p.X - _a.X) * dx + (p.Y - _a.Y) * dy) / (dx * dx + dy * dy)));

            return Math.Sqrt(Math.Pow(p.X - (_a.X + t * dx), 2) + Math.Pow(p.Y - (_a.Y + t * dy), 2));
        }

        internal override bool Delicate(GeoXLine2 line, double margin)
            => Math.Abs(Signed(line, _a)) < margin || Math.Abs(Signed(line, _b)) < margin;
    }

    /// <summary>
    /// A counter-clockwise arc, worked out from its start point, so that an arc of a radius of 1E11 is as exact as one of 50.
    /// </summary>
    /// <remarks>
    /// The point a turn of <c>t</c> from the start is the start point plus the chord of that turn, <c>2 r sin(t/2)</c> long
    /// along the direction <c>tau + t/2</c>, where <c>tau</c> is the direction the arc leaves its start in. Nothing in it is a
    /// difference of two large numbers, which a centre 1E11 away and a radius of 1E11 would make of every point. The roots
    /// and the nearest point are found by bisection on that, between the extremes of the signed distance, which are where the
    /// direction of travel is square to the normal of the line.
    /// </remarks>
    internal sealed class ArcPiece : OraclePiece
    {
        private readonly GeoPoint2 _start;
        private readonly double _r;
        private readonly double _tau;

        /// <summary>The radius of the circle the arc lies on.</summary>
        internal double Radius => _r;

        private readonly double _sweep;

        /// <summary>An arc from its start point, its radius, the direction it leaves in and its sweep, positive.</summary>
        internal ArcPiece(GeoPoint2 start, double radius, double tau, double sweep)
        {
            _start = start;
            _r = radius;
            _tau = tau;
            _sweep = sweep;
        }

        /// <summary>The arc about a centre from a start angle through a positive sweep, whole at a turn.</summary>
        internal static ArcPiece About(GeoPoint2 center, double radius, double start, double sweep)
        {
            return new ArcPiece(new GeoPoint2(center.X + radius * Math.Cos(start), center.Y + radius * Math.Sin(start)), radius, start + Math.PI / 2, sweep);
        }

        private GeoPoint2 At(double turn)
        {
            double chord = 2 * _r * Math.Sin(turn / 2);
            double heading = _tau + turn / 2;

            return new GeoPoint2(_start.X + chord * Math.Cos(heading), _start.Y + chord * Math.Sin(heading));
        }

        /// <summary>The turns, within the sweep, at which the signed distance from the line is at an extreme.</summary>
        private List<double> Extremes(GeoXLine2 line)
        {
            double normal = Math.Atan2(line.Direction.X, -line.Direction.Y);
            var turns = new List<double>();

            foreach (double sign in new[] { 1.0, -1.0 })
            {
                double turn = normal + sign * Math.PI / 2 - _tau;

                turn -= 2 * Math.PI * Math.Floor(turn / (2 * Math.PI));

                if (turn < _sweep)
                {
                    turns.Add(turn);
                }
            }

            turns.Sort();
            return turns;
        }

        private double F(GeoXLine2 line, double turn) => Signed(line, At(turn));

        internal override void Interval(GeoXLine2 line, out double lo, out double hi)
        {
            lo = Math.Min(F(line, 0), F(line, _sweep));
            hi = Math.Max(F(line, 0), F(line, _sweep));

            foreach (double turn in Extremes(line))
            {
                double f = F(line, turn);

                lo = Math.Min(lo, f);
                hi = Math.Max(hi, f);
            }
        }

        internal override IEnumerable<GeoPoint2> Crossings(GeoXLine2 line)
        {
            var knots = new List<double> { 0 };

            knots.AddRange(Extremes(line));
            knots.Add(_sweep);

            for (int i = 0; i + 1 < knots.Count; i++)
            {
                double lo = knots[i];
                double hi = knots[i + 1];
                double flo = F(line, lo);
                double fhi = F(line, hi);

                if (!((flo < 0 && fhi > 0) || (flo > 0 && fhi < 0)))
                {
                    continue;
                }

                for (int step = 0; step < 200; step++)
                {
                    double mid = (lo + hi) / 2;
                    double fmid = F(line, mid);

                    if ((fmid < 0) == (flo < 0))
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid;
                    }
                }

                yield return At((lo + hi) / 2);
            }
        }

        internal override double DistanceFrom(GeoPoint2 p)
        {
            // Sampled, then narrowed by ternary search about the best sample on either side.
            const int Samples = 256;
            double best = double.PositiveInfinity;
            int at = 0;

            for (int i = 0; i <= Samples; i++)
            {
                double d = p.DistanceTo(At(_sweep * i / Samples));

                if (d < best)
                {
                    best = d;
                    at = i;
                }
            }

            double lo = _sweep * Math.Max(0, at - 1) / Samples;
            double hi = _sweep * Math.Min(Samples, at + 1) / Samples;

            for (int step = 0; step < 100; step++)
            {
                double m1 = lo + (hi - lo) / 3;
                double m2 = hi - (hi - lo) / 3;

                if (p.DistanceTo(At(m1)) < p.DistanceTo(At(m2)))
                {
                    hi = m2;
                }
                else
                {
                    lo = m1;
                }
            }

            return Math.Min(best, p.DistanceTo(At((lo + hi) / 2)));
        }

        internal override bool Delicate(GeoXLine2 line, double margin)
        {
            if (Math.Abs(F(line, 0)) < margin || Math.Abs(F(line, _sweep)) < margin)
            {
                return true;
            }

            return Extremes(line).Any(turn => Math.Abs(F(line, turn)) < margin);
        }
    }

    /// <summary>A whole ellipse: a closed curve, read as filled by every question the line asks.</summary>
    internal sealed class EllipsePiece : OraclePiece
    {
        private readonly GeoPoint2 _c;
        private readonly GeoVector2 _u;
        private readonly GeoVector2 _v;
        private readonly double _a;
        private readonly double _b;

        internal EllipsePiece(GeoPoint2 center, GeoVector2 majorUnit, double majorRadius, double minorRadius)
        {
            _c = center;
            _u = majorUnit;
            _v = new GeoVector2(-majorUnit.Y, majorUnit.X);
            _a = majorRadius;
            _b = minorRadius;
        }

        private void Terms(GeoXLine2 line, out double s, out double reach, out double psi)
        {
            GeoVector2 n = new GeoVector2(-line.Direction.Y, line.Direction.X);
            double alpha = _a * (n.X * _u.X + n.Y * _u.Y);
            double beta = _b * (n.X * _v.X + n.Y * _v.Y);

            s = Signed(line, _c);
            reach = Math.Sqrt(alpha * alpha + beta * beta);
            psi = Math.Atan2(beta, alpha);
        }

        internal override void Interval(GeoXLine2 line, out double lo, out double hi)
        {
            Terms(line, out double s, out double reach, out _);

            lo = s - reach;
            hi = s + reach;
        }

        internal override IEnumerable<GeoPoint2> Crossings(GeoXLine2 line)
        {
            Terms(line, out double s, out double reach, out double psi);

            if (Math.Abs(s) >= reach)
            {
                yield break;
            }

            double spread = Math.Acos(-s / reach);

            foreach (double t in new[] { psi + spread, psi - spread })
            {
                yield return new GeoPoint2(
                    _c.X + _a * Math.Cos(t) * _u.X + _b * Math.Sin(t) * _v.X,
                    _c.Y + _a * Math.Cos(t) * _u.Y + _b * Math.Sin(t) * _v.Y);
            }
        }

        internal override double DistanceFrom(GeoPoint2 p)
        {
            // How far a point is from the ellipse is not a closed form; how far it is from satisfying the equation, scaled
            // back by the smaller radius, is within a hair of it near the curve, which is all the check made with it asks.
            double x = (p.X - _c.X) * _u.X + (p.Y - _c.Y) * _u.Y;
            double y = (p.X - _c.X) * _v.X + (p.Y - _c.Y) * _v.Y;

            return Math.Abs(Math.Sqrt(x * x / (_a * _a) + y * y / (_b * _b)) - 1.0) * Math.Min(_a, _b);
        }

        internal override bool Delicate(GeoXLine2 line, double margin)
        {
            Terms(line, out double s, out double reach, out _);

            return Math.Abs(Math.Abs(s) - reach) < margin;
        }
    }

    /// <summary>A half-line: the ray from an origin along a unit direction.</summary>
    internal sealed class RayPiece : OraclePiece
    {
        private readonly GeoPoint2 _o;
        private readonly GeoVector2 _d;

        internal RayPiece(GeoPoint2 origin, GeoVector2 direction)
        {
            _o = origin;
            _d = direction;
        }

        internal override void Interval(GeoXLine2 line, out double lo, out double hi)
        {
            double f = Signed(line, _o);
            double rate = -line.Direction.Y * _d.X + line.Direction.X * _d.Y;

            lo = rate < 0 ? double.NegativeInfinity : f;
            hi = rate > 0 ? double.PositiveInfinity : f;

            if (rate == 0)
            {
                lo = f;
                hi = f;
            }
        }

        internal override IEnumerable<GeoPoint2> Crossings(GeoXLine2 line)
        {
            double f = Signed(line, _o);
            double rate = -line.Direction.Y * _d.X + line.Direction.X * _d.Y;
            double t = -f / rate;

            // Two directions within the angle tolerance of parallel are refused a crossing, as two rays are.
            if (t > 0 && Math.Abs(rate) > AngleSine)
            {
                yield return new GeoPoint2(_o.X + t * _d.X, _o.Y + t * _d.Y);
            }
        }

        internal override double DistanceFrom(GeoPoint2 p)
        {
            double t = Math.Max(0, (p.X - _o.X) * _d.X + (p.Y - _o.Y) * _d.Y);

            return p.DistanceTo(new GeoPoint2(_o.X + t * _d.X, _o.Y + t * _d.Y));
        }

        internal override bool Delicate(GeoXLine2 line, double margin)
        {
            double sine = Math.Abs(line.Direction.X * _d.Y - line.Direction.Y * _d.X);

            return Math.Abs(Signed(line, _o)) < margin || (sine > AngleSine * 0.5 && sine < AngleSine * 2);
        }
    }

    /// <summary>A whole line: the same as the line asked about when it lies along it, and everything otherwise.</summary>
    internal sealed class LinePiece : OraclePiece
    {
        private readonly GeoPoint2 _o;
        private readonly GeoVector2 _d;

        internal LinePiece(GeoPoint2 origin, GeoVector2 direction)
        {
            _o = origin;
            _d = direction;
        }

        internal override void Interval(GeoXLine2 line, out double lo, out double hi)
        {
            double f = Signed(line, _o);
            double rate = -line.Direction.Y * _d.X + line.Direction.X * _d.Y;

            if (rate == 0)
            {
                lo = f;
                hi = f;
            }
            else
            {
                lo = double.NegativeInfinity;
                hi = double.PositiveInfinity;
            }
        }

        internal override IEnumerable<GeoPoint2> Crossings(GeoXLine2 line)
        {
            double f = Signed(line, _o);
            double rate = -line.Direction.Y * _d.X + line.Direction.X * _d.Y;

            if (Math.Abs(rate) > AngleSine)
            {
                double t = -f / rate;

                yield return new GeoPoint2(_o.X + t * _d.X, _o.Y + t * _d.Y);
            }
        }

        internal override double DistanceFrom(GeoPoint2 p)
        {
            double off = _d.X * (p.Y - _o.Y) - _d.Y * (p.X - _o.X);

            return Math.Abs(off);
        }

        internal override bool Delicate(GeoXLine2 line, double margin)
        {
            double sine = Math.Abs(line.Direction.X * _d.Y - line.Direction.Y * _d.X);

            return sine > AngleSine * 0.5 && sine < AngleSine * 2;
        }
    }

    internal sealed class PointPiece : OraclePiece
    {
        private readonly GeoPoint2 _p;

        internal PointPiece(GeoPoint2 p)
        {
            _p = p;
        }

        internal override void Interval(GeoXLine2 line, out double lo, out double hi)
        {
            lo = Signed(line, _p);
            hi = lo;
        }

        internal override IEnumerable<GeoPoint2> Crossings(GeoXLine2 line) => Enumerable.Empty<GeoPoint2>();

        internal override double DistanceFrom(GeoPoint2 point) => point.DistanceTo(_p);

        internal override bool Delicate(GeoXLine2 line, double margin) => false;
    }

    /// <summary>
    /// A shape the line is asked about, with the pieces its boundary is worked out from.
    /// </summary>
    internal sealed class OracleShape
    {
        internal string Kind;
        internal object Geo;
        internal List<OraclePiece> Pieces = new List<OraclePiece>();
        internal GeoPoint2 Center;
        internal double Radius;

        /// <summary>Whether the shape's boundary is closed: the crossings of a closed one come in pairs.</summary>
        internal bool Closed;

        /// <summary>The distance of the line from the shape.</summary>
        internal double Distance(GeoXLine2 line)
        {
            double least = double.PositiveInfinity;

            foreach (OraclePiece piece in Pieces)
            {
                piece.Interval(line, out double lo, out double hi);
                least = Math.Min(least, OraclePiece.Gap(lo, hi));
            }

            return least;
        }

        /// <summary>Whether two crossings of the last line asked about lie so near a thousandth apart that whether they are one is the tolerance's to say.</summary>
        internal bool Ambiguous;

        /// <summary>
        /// The crossings of the line with the boundary, those within a thousandth of one another merged: two edges meeting at a
        /// sharp corner a hair from the line cross it a hair apart, and the library calls that one crossing.
        /// </summary>
        internal GeoPoint2[] Crossings(GeoXLine2 line)
        {
            var points = new List<GeoPoint2>();

            Ambiguous = false;

            foreach (OraclePiece piece in Pieces)
            {
                foreach (GeoPoint2 p in piece.Crossings(line))
                {
                    if (points.Any(q => q.DistanceTo(p) >= 0.5E-3 && q.DistanceTo(p) <= 2E-3))
                    {
                        Ambiguous = true;
                    }

                    if (!points.Any(q => q.DistanceTo(p) < 1E-3))
                    {
                        points.Add(p);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// How far the library's own picture of the shape can be from the true one: an arc is kept as a centre and a radius, and
        /// on a circle of a radius of 1E8 a double places a point to within a few times 1E-8.
        /// </summary>
        internal double Imprecision => Pieces.OfType<ArcPiece>().Select(a => a.Radius).DefaultIfEmpty(0.0).Max() * 4E-16;

        internal bool Delicate(GeoXLine2 line, double margin) => Pieces.Any(p => p.Delicate(line, margin));

        /// <summary>The distance of a point from the boundary.</summary>
        internal double DistanceFromBoundary(GeoPoint2 point) => Pieces.Min(p => p.DistanceFrom(point));
    }

    /// <summary>
    /// Random shapes of each kind, drawn on a seeded generator, with the pieces of their boundary.
    /// </summary>
    internal static class OracleShapes
    {
        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static double Between(Random random, double lo, double hi) => lo + (hi - lo) * random.NextDouble();

        /// <summary>A star-shaped ring of corners about a centre: simple, with no sliver in it.</summary>
        private static GeoPoint2[] Star(Random random, GeoPoint2 center, double radius, int corners)
        {
            var ring = new GeoPoint2[corners];

            for (int i = 0; i < corners; i++)
            {
                double angle = 2 * Math.PI * (i + Between(random, 0.15, 0.85)) / corners;
                double reach = radius * Between(random, 0.45, 1.0);

                ring[i] = P(center.X + reach * Math.Cos(angle), center.Y + reach * Math.Sin(angle));
            }

            return ring;
        }

        private static void Ring(List<OraclePiece> pieces, GeoPoint2[] ring, bool closed)
        {
            for (int i = 0; i < ring.Length - (closed ? 0 : 1); i++)
            {
                pieces.Add(new SegmentPiece(ring[i], ring[(i + 1) % ring.Length]));
            }
        }

        /// <summary>The arc piece of the edge from <paramref name="p"/> to <paramref name="q"/> with a bulge, or the segment when there is none.</summary>
        private static OraclePiece Edge(GeoPoint2 p, GeoPoint2 q, double bulge)
        {
            if (bulge == 0)
            {
                return new SegmentPiece(p, q);
            }

            // A clockwise arc is the counter-clockwise one from its other end.
            if (bulge < 0)
            {
                return Edge(q, p, -bulge);
            }

            double sweep = 4 * Math.Atan(bulge);
            double chord = p.DistanceTo(q);
            double radius = chord / (2 * Math.Sin(sweep / 2));
            double heading = Math.Atan2(q.Y - p.Y, q.X - p.X) - sweep / 2;

            return new ArcPiece(p, radius, heading, sweep);
        }

        /// <summary>
        /// In a third of the draws, one edge of a loop or a chain becomes an arc that is nearly straight: a bulge of 1E-10 to 1E-3
        /// either way, on a circle of a radius up to 1E10 times the chord.
        /// </summary>
        private static void FlattenOne(Random random, double[] bulges)
        {
            if (random.Next(3) == 0)
            {
                bulges[random.Next(bulges.Length)] = (random.Next(2) == 0 ? 1.0 : -1.0) * Math.Pow(10.0, Between(random, -10, -3));
            }
        }

        private static OracleShape Make(string kind, object geo, GeoPoint2 center, double radius, bool closed)
        {
            return new OracleShape { Kind = kind, Geo = geo, Center = center, Radius = radius, Closed = closed };
        }

        private static GeoPoint2 Anywhere(Random random) => P(Between(random, -60, 60), Between(random, -60, 60));

        /// <summary>The kinds of shape the line is asked about, in the order the fuzz walks them.</summary>
        internal static readonly string[] Kinds =
        {
            "Point2", "Line2", "Ray2", "XLine2", "Arc2", "Circle2", "Ellipse2", "Edge2 straight", "Edge2 curved", "Polyline2", "Polygon2",
            "PolylineArc2", "PolygonArc2", "Rectangle2", "Triangle2", "Face2"
        };

        internal static OracleShape Draw(string kind, Random random)
        {
            GeoPoint2 c = Anywhere(random);
            double radius = Between(random, 15, 80);

            switch (kind)
            {
                case "Point2":
                {
                    GeoPoint2 p = Anywhere(random);
                    OracleShape s = Make(kind, p, p, 0, false);

                    s.Pieces.Add(new PointPiece(p));
                    return s;
                }

                case "Line2":
                {
                    double angle = Between(random, 0, 2 * Math.PI);
                    double length = Between(random, 5, 150);
                    GeoPoint2 q = P(c.X + length * Math.Cos(angle), c.Y + length * Math.Sin(angle));
                    OracleShape s = Make(kind, new GeoLine2(c, q), P((c.X + q.X) / 2, (c.Y + q.Y) / 2), length / 2, false);

                    s.Pieces.Add(new SegmentPiece(c, q));
                    return s;
                }

                case "Ray2":
                {
                    double angle = Between(random, 0, 2 * Math.PI);
                    GeoRay2 ray = new GeoRay2(c, new GeoVector2(Math.Cos(angle), Math.Sin(angle)));
                    OracleShape s = Make(kind, ray, c, 20, false);

                    s.Pieces.Add(new RayPiece(ray.Origin, ray.Direction));
                    return s;
                }

                case "XLine2":
                {
                    double angle = Between(random, 0, 2 * Math.PI);
                    GeoXLine2 other = new GeoXLine2(c, new GeoVector2(Math.Cos(angle), Math.Sin(angle)));
                    OracleShape s = Make(kind, other, c, 20, false);

                    s.Pieces.Add(new LinePiece(other.Origin, other.Direction));
                    return s;
                }

                case "Arc2":
                {
                    double start = Between(random, -Math.PI, Math.PI);
                    double sweep = Between(random, 0.4, 2 * Math.PI - 0.4);
                    OracleShape s = Make(kind, new GeoArc2(c, radius, start, start + sweep), c, radius, false);

                    s.Pieces.Add(ArcPiece.About(c, radius, start, sweep));
                    return s;
                }

                case "Circle2":
                {
                    OracleShape s = Make(kind, new GeoCircle2(c, radius), c, radius, true);

                    s.Pieces.Add(ArcPiece.About(c, radius, 0, 2 * Math.PI));
                    return s;
                }

                case "Ellipse2":
                {
                    double turn = Between(random, 0, Math.PI);
                    double minor = Between(random, 10, radius);
                    GeoVector2 major = new GeoVector2(Math.Cos(turn), Math.Sin(turn));
                    OracleShape s = Make(kind, new GeoEllipse2(c, major, radius, minor), c, radius, true);

                    s.Pieces.Add(new EllipsePiece(c, major, radius, minor));
                    return s;
                }

                case "Edge2 straight":
                {
                    GeoPoint2 q = P(c.X + Between(random, -80, 80), c.Y + Between(random, -80, 80));
                    OracleShape s = Make(kind, new GeoEdge2(c, q), c, 80, false);

                    s.Pieces.Add(new SegmentPiece(c, q));
                    return s;
                }

                case "Edge2 curved":
                {
                    double start = Between(random, -Math.PI, Math.PI);
                    double sweep = Between(random, 0.4, 2 * Math.PI - 0.4);
                    OracleShape s = Make(kind, new GeoEdge2(new GeoArc2(c, radius, start, start + sweep)), c, radius, false);

                    s.Pieces.Add(ArcPiece.About(c, radius, start, sweep));
                    return s;
                }

                case "Polyline2":
                {
                    int count = random.Next(2, 7);
                    var vertices = new GeoPoint2[count];

                    for (int i = 0; i < count; i++)
                    {
                        vertices[i] = P(c.X + Between(random, -70, 70), c.Y + Between(random, -70, 70));
                    }

                    OracleShape s = Make(kind, new GeoPolyline2(vertices), c, 100, false);

                    Ring(s.Pieces, vertices, false);
                    return s;
                }

                case "Polygon2":
                {
                    GeoPoint2[] ring = Star(random, c, radius, random.Next(3, 10));
                    OracleShape s = Make(kind, new GeoPolygon2(ring), c, radius, true);

                    Ring(s.Pieces, ring, true);
                    return s;
                }

                case "PolylineArc2":
                {
                    int count = random.Next(2, 6);
                    var vertices = new GeoPoint2[count];
                    var bulges = new double[count - 1];

                    for (int i = 0; i < count; i++)
                    {
                        vertices[i] = P(c.X + Between(random, -70, 70), c.Y + Between(random, -70, 70));
                    }

                    for (int i = 0; i < bulges.Length; i++)
                    {
                        bulges[i] = random.Next(3) == 0 ? 0.0 : Between(random, -1.2, 1.2);
                    }

                    FlattenOne(random, bulges);

                    OracleShape s = Make(kind, new GeoPolylineArc2(vertices, bulges), c, 160, false);

                    for (int i = 0; i < bulges.Length; i++)
                    {
                        s.Pieces.Add(Edge(vertices[i], vertices[i + 1], bulges[i]));
                    }

                    return s;
                }

                case "PolygonArc2":
                {
                    GeoPoint2[] ring = Star(random, c, radius, random.Next(3, 8));
                    var bulges = new double[ring.Length];

                    for (int i = 0; i < ring.Length; i++)
                    {
                        bulges[i] = random.Next(2) == 0 ? 0.0 : Between(random, -0.25, 0.25);
                    }

                    FlattenOne(random, bulges);

                    OracleShape s = Make(kind, new GeoPolygonArc2(ring, bulges), c, radius * 1.5, true);

                    for (int i = 0; i < ring.Length; i++)
                    {
                        s.Pieces.Add(Edge(ring[i], ring[(i + 1) % ring.Length], bulges[i]));
                    }

                    return s;
                }

                case "Rectangle2":
                {
                    double width = Between(random, 10, 120);
                    double height = Between(random, 10, 120);
                    double angle = Between(random, 0, Math.PI);
                    double cos = Math.Cos(angle);
                    double sin = Math.Sin(angle);
                    GeoPoint2 Corner(double sx, double sy) => P(c.X + sx * width / 2 * cos - sy * height / 2 * sin, c.Y + sx * width / 2 * sin + sy * height / 2 * cos);
                    GeoPoint2[] corners = { Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, 1) };
                    OracleShape s = Make(kind, new GeoRectangle2(c, width, height, angle), c, Math.Sqrt(width * width + height * height) / 2, true);

                    Ring(s.Pieces, corners, true);
                    return s;
                }

                case "Triangle2":
                {
                    GeoPoint2[] ring;

                    do
                    {
                        ring = new[] { P(c.X + Between(random, -80, 80), c.Y + Between(random, -80, 80)), P(c.X + Between(random, -80, 80), c.Y + Between(random, -80, 80)), P(c.X + Between(random, -80, 80), c.Y + Between(random, -80, 80)) };
                    }
                    while (Math.Abs((ring[1].X - ring[0].X) * (ring[2].Y - ring[0].Y) - (ring[1].Y - ring[0].Y) * (ring[2].X - ring[0].X)) < 500);

                    OracleShape s = Make(kind, new GeoTriangle2(ring[0], ring[1], ring[2]), c, 120, true);

                    Ring(s.Pieces, ring, true);
                    return s;
                }

                case "Face2":
                {
                    // Five corners or more, none further apart than about two radians, so that no side of the outline comes within
                    // a fifth of the radius of the centre and the hole, within a seventh of it, stays inside.
                    GeoPoint2[] outer = Star(random, c, radius, random.Next(5, 9));
                    GeoPoint2[] hole = Star(random, c, radius * 0.14, random.Next(3, 6));
                    OracleShape s = Make(kind, new GeoFace2(new GeoPolygon2(outer), new[] { new GeoPolygon2(hole) }), c, radius, true);

                    Ring(s.Pieces, outer, true);
                    Ring(s.Pieces, hole, true);
                    return s;
                }
            }

            throw new ArgumentException(kind);
        }

        /// <summary>
        /// A line for a shape: through the shape, beside it parallel, or starting very far along; or, when the shape has a
        /// straight piece, parallel to it a hair off.
        /// </summary>
        internal static GeoXLine2 DrawLine(OracleShape shape, Random random, int mode)
        {
            double angle = Between(random, 0, 2 * Math.PI);
            var direction = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            var normal = new GeoVector2(-direction.Y, direction.X);
            double along = Between(random, -150, 150);

            switch (mode % 4)
            {
                case 0:
                    // Anywhere in the neighbourhood.
                    return new GeoXLine2(P(shape.Center.X + Between(random, -150, 150), shape.Center.Y + Between(random, -150, 150)), direction);

                case 1:
                {
                    // Beside the shape: a gap of its reach and a little more, on either side.
                    double gap = (shape.Radius + Between(random, 0.5, 80)) * (random.Next(2) == 0 ? 1 : -1);

                    return new GeoXLine2(P(shape.Center.X + gap * normal.X + along * direction.X, shape.Center.Y + gap * normal.Y + along * direction.Y), direction);
                }

                case 2:
                {
                    // Through the shape, starting thousands of units away.
                    double far = Between(random, 1E3, 1E6) * (random.Next(2) == 0 ? 1 : -1);
                    double off = Between(random, -0.6, 0.6) * shape.Radius;

                    return new GeoXLine2(P(shape.Center.X + off * normal.X + far * direction.X, shape.Center.Y + off * normal.Y + far * direction.Y), direction);
                }

                default:
                {
                    // Near the shape, either side of a hair.
                    double gap = (shape.Radius * Between(random, 0.2, 1.4)) * (random.Next(2) == 0 ? 1 : -1);

                    return new GeoXLine2(P(shape.Center.X + gap * normal.X, shape.Center.Y + gap * normal.Y), direction);
                }
            }
        }
    }
}
