using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A vector in space with the arithmetic written out, so that the oracle below shares nothing with the library's own
    /// vectors, points and kernels.
    /// </summary>
    internal readonly struct Vec
    {
        internal readonly double X;
        internal readonly double Y;
        internal readonly double Z;

        internal Vec(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal static Vec Of(GeoPoint3 p) => new Vec(p.X, p.Y, p.Z);

        internal static Vec Of(GeoVector3 v) => new Vec(v.X, v.Y, v.Z);

        internal GeoPoint3 ToPoint() => new GeoPoint3(X, Y, Z);

        public static Vec operator +(Vec a, Vec b) => new Vec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec operator -(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec operator *(Vec a, double s) => new Vec(a.X * s, a.Y * s, a.Z * s);

        public static Vec operator *(double s, Vec a) => new Vec(a.X * s, a.Y * s, a.Z * s);

        internal double Dot(Vec b) => X * b.X + Y * b.Y + Z * b.Z;

        internal Vec Cross(Vec b) => new Vec(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

        internal double Length => Math.Sqrt(Dot(this));

        internal Vec Unit() => this * (1.0 / Length);

        internal double DistanceTo(Vec b) => (this - b).Length;
    }

    /// <summary>An infinite line: a point and a direction of unit length.</summary>
    internal readonly struct Ln
    {
        internal readonly Vec O;
        internal readonly Vec D;

        internal Ln(GeoXLine3 line)
        {
            O = Vec.Of(line.Origin);
            D = Vec.Of(line.Direction).Unit();
        }

        /// <summary>The part of a point's offset from the line square to it.</summary>
        internal Vec Perp(Vec p)
        {
            Vec w = p - O;

            return w - D * w.Dot(D);
        }

        internal double Along(Vec p) => (p - O).Dot(D);

        internal Vec At(double t) => O + D * t;
    }

    /// <summary>
    /// What an infinite line does to a shape in space, worked out in closed form or by dense sampling from the shape's own
    /// numbers and sharing no code with the library: the shape is taken to pieces (segments, rays, lines, arcs, whole
    /// ellipses, flat regions with holes, planes and points), and for each piece the distance from the line is a closed form or
    /// the least of a dense sample refined by golden section.
    /// </summary>
    /// <remarks>
    /// The distance from a line to a point of a segment is <c>|P_perp|</c>, the square of which is a quadratic in the
    /// parameter, so a segment, a ray and a line are exact; a rim is a smooth function of its angle, sampled at 4 000 angles
    /// and refined by golden section on the best three; a flat region is nought where the line pierces it (the crossing of its
    /// plane lies inside its outline and outside its holes) and otherwise the least of its rim edges, since a line that does
    /// not pierce a region comes nearest it on its rim; a body is its faces. The crossings are where the line meets a piece
    /// exactly, to a billionth.
    /// </remarks>
    internal abstract class Piece
    {
        /// <summary>The distance of the line from the piece.</summary>
        internal abstract double Gap(Ln line);

        /// <summary>The points where the line meets the piece exactly, each once.</summary>
        internal abstract IEnumerable<Vec> Meetings(Ln line);

        /// <summary>Whether the line passes so near the piece that a tolerance, not geometry, may decide whether it meets it.</summary>
        internal abstract bool Delicate(Ln line);

        /// <summary>The distance of a point from the piece.</summary>
        internal abstract double GapFromPoint(Vec point);

        /// <summary>The sine of the angle tolerance of the tolerance the tests ask under.</summary>
        internal static readonly double AngleSine = XLine3Probe.Strict.EqualAngleSin;

        /// <summary>The band round the point tolerance that the distance may not be in for a crossing to be asked of.</summary>
        internal const double Band = 4E-3;

        internal static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

        /// <summary>The distance of a point from a segment.</summary>
        internal static double PointToSegment(Vec p, Vec a, Vec b)
        {
            Vec w = b - a;
            double ww = w.Dot(w);
            double t = ww > 0 ? Clamp((p - a).Dot(w) / ww, 0, 1) : 0;

            return (p - (a + w * t)).Length;
        }

        /// <summary>The distance of a line from a segment, and the parameter on the segment where they come nearest.</summary>
        internal static double LineToSegment(Ln line, Vec a, Vec b, out double t)
        {
            Vec u = line.Perp(a);
            Vec w = (b - a) - line.D * (b - a).Dot(line.D);
            double ww = w.Dot(w);

            t = ww > 1E-30 ? Clamp(-u.Dot(w) / ww, 0, 1) : 0;
            return (u + w * t).Length;
        }

        /// <summary>
        /// The least of a function over an interval, by a dense sample refined on the three best samples by golden section.
        /// </summary>
        internal static double MinOver(Func<double, double> f, double lo, double hi, bool wraps)
        {
            const int Samples = 4096;
            var values = new double[Samples + 1];

            for (int i = 0; i <= Samples; i++)
            {
                values[i] = f(lo + (hi - lo) * i / Samples);
            }

            double best = values.Min();
            int[] order = Enumerable.Range(0, Samples + 1).OrderBy(i => values[i]).Take(3).ToArray();

            foreach (int i in order)
            {
                double a = lo + (hi - lo) * Math.Max(0, i - 1) / Samples;
                double b = lo + (hi - lo) * Math.Min(Samples, i + 1) / Samples;

                if (wraps && (i == 0 || i == Samples))
                {
                    a = lo - (hi - lo) / Samples;
                    b = lo + (hi - lo) / Samples;
                }

                const double Golden = 0.6180339887498949;
                double c = b - Golden * (b - a);
                double d = a + Golden * (b - a);
                double fc = f(c);
                double fd = f(d);

                for (int k = 0; k < 90; k++)
                {
                    if (fc < fd)
                    {
                        b = d;
                        d = c;
                        fd = fc;
                        c = b - Golden * (b - a);
                        fc = f(c);
                    }
                    else
                    {
                        a = c;
                        c = d;
                        fc = fd;
                        d = a + Golden * (b - a);
                        fd = f(d);
                    }
                }

                best = Math.Min(best, Math.Min(fc, fd));
            }

            return best;
        }
    }

    /// <summary>A point.</summary>
    internal sealed class PointPiece : Piece
    {
        private readonly Vec _p;

        internal PointPiece(GeoPoint3 p)
        {
            _p = Vec.Of(p);
        }

        internal override double Gap(Ln line) => line.Perp(_p).Length;

        internal override IEnumerable<Vec> Meetings(Ln line) => Enumerable.Empty<Vec>();

        internal override bool Delicate(Ln line) => false;

        internal override double GapFromPoint(Vec point) => (point - _p).Length;
    }

    /// <summary>A segment.</summary>
    internal sealed class SegPiece : Piece
    {
        private readonly Vec _a;
        private readonly Vec _b;

        internal SegPiece(GeoPoint3 a, GeoPoint3 b)
        {
            _a = Vec.Of(a);
            _b = Vec.Of(b);
        }

        internal override double Gap(Ln line) => LineToSegment(line, _a, _b, out _);

        internal override IEnumerable<Vec> Meetings(Ln line)
        {
            double gap = LineToSegment(line, _a, _b, out double t);
            Vec d = (_b - _a).Unit();

            // A segment along the line meets it everywhere and names no place.
            if (gap <= 1E-9 && d.Cross(line.D).Length > 1E-9)
            {
                yield return _a + (_b - _a) * t;
            }
        }

        internal override bool Delicate(Ln line)
        {
            double gap = LineToSegment(line, _a, _b, out _);

            // A segment crossed at a slant of less than three degrees is read as parallel by the angle tolerance of a degree and
            // by the drift of the shorter piece: the two readings part company there, and the line is not asked of it.
            return (gap > 1E-9 && gap <= Band) || (gap <= 1E-9 && (_b - _a).Unit().Cross(line.D).Length < 0.05);
        }

        internal override double GapFromPoint(Vec point) => PointToSegment(point, _a, _b);
    }

    /// <summary>A ray.</summary>
    internal sealed class RayPiece : Piece
    {
        private readonly Vec _o;
        private readonly Vec _d;

        internal RayPiece(GeoRay3 ray)
        {
            _o = Vec.Of(ray.Origin);
            _d = Vec.Of(ray.Direction).Unit();
        }

        private double Near(Ln line, out double u)
        {
            Vec p = line.Perp(_o);
            Vec w = _d - line.D * _d.Dot(line.D);
            double ww = w.Dot(w);

            u = ww > 1E-30 ? Math.Max(0, -p.Dot(w) / ww) : 0;
            return (p + w * u).Length;
        }

        internal override double Gap(Ln line) => Near(line, out _);

        internal override IEnumerable<Vec> Meetings(Ln line)
        {
            double gap = Near(line, out double u);

            if (gap <= 1E-9 && _d.Cross(line.D).Length > AngleSine)
            {
                yield return _o + _d * u;
            }
        }

        internal override bool Delicate(Ln line)
        {
            double gap = Near(line, out _);

            return gap > 1E-9 && gap <= Band;
        }

        internal override double GapFromPoint(Vec point) => (point - (_o + _d * Math.Max(0, (point - _o).Dot(_d)))).Length;
    }

    /// <summary>An infinite line.</summary>
    internal sealed class LinePiece : Piece
    {
        private readonly Vec _o;
        private readonly Vec _d;

        internal LinePiece(GeoXLine3 xline)
        {
            _o = Vec.Of(xline.Origin);
            _d = Vec.Of(xline.Direction).Unit();
        }

        internal override double Gap(Ln line)
        {
            Vec n = line.D.Cross(_d);

            return n.Length > 1E-12 ? Math.Abs((_o - line.O).Dot(n)) / n.Length : line.Perp(_o).Length;
        }

        internal override IEnumerable<Vec> Meetings(Ln line)
        {
            Vec n = line.D.Cross(_d);

            if (n.Length > AngleSine && Gap(line) <= 1E-9)
            {
                // The point of the other line nearest this one.
                Vec w = _o - line.O;
                double s = (w.Cross(line.D)).Dot(n) / n.Dot(n);

                yield return _o + _d * s;
            }
        }

        internal override bool Delicate(Ln line)
        {
            double gap = Gap(line);

            return gap > 1E-9 && gap <= Band;
        }

        internal override double GapFromPoint(Vec point) => (point - _o - _d * (point - _o).Dot(_d)).Length;
    }

    /// <summary>A plane.</summary>
    internal sealed class PlanePiece : Piece
    {
        private readonly Vec _o;
        private readonly Vec _n;

        internal PlanePiece(GeoPlane3 plane)
        {
            _o = Vec.Of(plane.Origin);
            _n = Vec.Of(plane.Normal).Unit();
        }

        internal override double Gap(Ln line) => Math.Abs(_n.Dot(line.D)) > 1E-12 ? 0.0 : Math.Abs(_n.Dot(line.O - _o));

        internal override IEnumerable<Vec> Meetings(Ln line)
        {
            double den = _n.Dot(line.D);

            if (Math.Abs(den) > 1E-9)
            {
                yield return line.At(_n.Dot(_o - line.O) / den);
            }
        }

        internal override bool Delicate(Ln line) => false;

        internal override double GapFromPoint(Vec point) => Math.Abs(_n.Dot(point - _o));
    }

    /// <summary>A circular arc, from its start point, its centre and its normal, about which it turns counter-clockwise.</summary>
    internal sealed class ArcPiece : Piece
    {
        private readonly Vec _c;
        private readonly Vec _n;
        private readonly Vec _s;
        private readonly Vec _t;
        private readonly double _r;
        private readonly double _sweep;

        internal ArcPiece(GeoArc3 arc)
            : this(Vec.Of(arc.Center), Vec.Of(arc.Normal).Unit(), Vec.Of(arc.StartPoint), arc.SweptAngle)
        {
        }

        internal ArcPiece(Vec center, Vec normal, Vec start, double sweep)
        {
            _c = center;
            _n = normal;
            _s = start - center;
            _t = _n.Cross(_s);
            _r = _s.Length;
            _sweep = sweep;
        }

        private Vec At(double phi) => _c + _s * Math.Cos(phi) + _t * Math.Sin(phi);

        private bool Full => Math.Abs(_sweep) >= 2 * Math.PI - 1E-12;

        internal override double Gap(Ln line)
        {
            double lo = Math.Min(0, _sweep);
            double hi = Math.Max(0, _sweep);

            return Math.Sqrt(MinOver(phi => { Vec p = line.Perp(At(phi)); return p.Dot(p); }, lo, hi, Full));
        }

        internal override IEnumerable<Vec> Meetings(Ln line)
        {
            double den = _n.Dot(line.D);

            if (Math.Abs(den) <= 1E-9)
            {
                yield break;
            }

            Vec p = line.At(_n.Dot(_c - line.O) / den);
            Vec offset = p - _c;
            double x = offset.Dot(_s) / (_r * _r);
            double y = offset.Dot(_t) / (_r * _r);

            if (Math.Abs(offset.Length - _r) > 1E-9 * Math.Max(1, _r))
            {
                yield break;
            }

            double phi = Math.Atan2(y, x);
            double reached = phi * Math.Sign(_sweep == 0 ? 1 : _sweep);

            reached -= 2 * Math.PI * Math.Floor(reached / (2 * Math.PI));

            if (reached <= Math.Abs(_sweep))
            {
                yield return p;
            }
        }

        internal override bool Delicate(Ln line)
        {
            double den = _n.Dot(line.D);

            if (Math.Abs(den) <= 3E-4)
            {
                return true;
            }

            Vec p = line.At(_n.Dot(_c - line.O) / den);
            double off = Math.Abs((p - _c).Length - _r);

            if (off > Band)
            {
                return false;
            }

            if (off > 1E-9 * Math.Max(1, _r))
            {
                return true;
            }

            // On the circle: delicate when near an end of the arc, and when the line grazes it, so near the tangent that the stretch of
            // it within the tolerance of the rim is long enough to be read as two crossings.
            Vec tangent = _n.Cross(p - _c).Unit();

            // The segment kernels find where a line meets a rim as where it meets the sphere about its centre, and a line that is square to
            // the radius there grazes the sphere: its two roots, a double one in exact arithmetic, are only as good as a square root of
            // rounding and may come out as two crossings a thousandth apart, or one a millionth off.
            bool grazesTheSphere = Math.Abs((p - _c).Dot(line.D)) < 0.02 * _r;

            return (Math.Min((p - At(0)).Length, (p - At(_sweep)).Length) <= Band && !Full) || tangent.Cross(line.D).Length < 0.03 || grazesTheSphere;
        }

        internal override double GapFromPoint(Vec point)
        {
            double lo = Math.Min(0, _sweep);
            double hi = Math.Max(0, _sweep);

            return Math.Sqrt(MinOver(phi => { Vec d = point - At(phi); return d.Dot(d); }, lo, hi, Full));
        }
    }

    /// <summary>The rim of an ellipse.</summary>
    internal sealed class EllipsePiece : Piece
    {
        private readonly Vec _c;
        private readonly Vec _n;
        private readonly Vec _m;
        private readonly Vec _w;
        private readonly double _a;
        private readonly double _b;

        internal EllipsePiece(GeoEllipse3 ellipse)
        {
            _c = Vec.Of(ellipse.Center);
            _n = Vec.Of(ellipse.Normal).Unit();
            _m = Vec.Of(ellipse.MajorAxis).Unit();
            _w = _n.Cross(_m);
            _a = ellipse.MajorRadius;
            _b = ellipse.MinorRadius;
        }

        private Vec At(double theta) => _c + _m * (_a * Math.Cos(theta)) + _w * (_b * Math.Sin(theta));

        internal override double Gap(Ln line)
            => Math.Sqrt(MinOver(theta => { Vec p = line.Perp(At(theta)); return p.Dot(p); }, 0, 2 * Math.PI, true));

        private double Deviation(Vec p)
        {
            double x = (p - _c).Dot(_m) / _a;
            double y = (p - _c).Dot(_w) / _b;

            return Math.Abs(x * x + y * y - 1.0);
        }

        internal override IEnumerable<Vec> Meetings(Ln line)
        {
            double den = _n.Dot(line.D);

            if (Math.Abs(den) > 1E-9)
            {
                Vec p = line.At(_n.Dot(_c - line.O) / den);

                if (Deviation(p) <= 1E-10)
                {
                    yield return p;
                }
            }
        }

        internal override bool Delicate(Ln line)
        {
            double den = _n.Dot(line.D);

            if (Math.Abs(den) <= 3E-4)
            {
                return true;
            }

            Vec p = line.At(_n.Dot(_c - line.O) / den);
            double deviation = Deviation(p);

            if (deviation <= 1E-10)
            {
                // On the rim: delicate where the line grazes it, as for an arc.
                double x = (p - _c).Dot(_m);
                double y = (p - _c).Dot(_w);
                Vec tangent = (_m * (-_a * y / _b) + _w * (_b * x / _a)).Unit();

                return tangent.Cross(line.D).Length < 0.03;
            }

            return deviation <= 2 * Band * Math.Max(1.0 / _a, 1.0 / _b) * 4;
        }

        internal override double GapFromPoint(Vec point)
            => Math.Sqrt(MinOver(theta => { Vec d = point - At(theta); return d.Dot(d); }, 0, 2 * Math.PI, true));
    }

    /// <summary>A flat region: an outline and holes in one plane, read filled.</summary>
    internal sealed class FlatPiece : Piece
    {
        private readonly Vec[] _outline;
        private readonly Vec[][] _holes;
        private readonly Vec _n;
        private readonly Vec _v0;
        private readonly int _drop;

        internal FlatPiece(IReadOnlyList<GeoPoint3> outline, IEnumerable<IReadOnlyList<GeoPoint3>> holes)
        {
            _outline = outline.Select(Vec.Of).ToArray();
            _holes = (holes ?? Enumerable.Empty<IReadOnlyList<GeoPoint3>>()).Select(h => h.Select(Vec.Of).ToArray()).ToArray();
            _v0 = _outline[0];

            // The normal by Newell's sum, which does not care which corners are collinear.
            Vec sum = new Vec(0, 0, 0);

            for (int i = 0; i < _outline.Length; i++)
            {
                Vec a = _outline[i];
                Vec b = _outline[(i + 1) % _outline.Length];

                sum = sum + new Vec((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
            }

            _n = sum.Unit();
            _drop = Math.Abs(_n.X) >= Math.Abs(_n.Y) && Math.Abs(_n.X) >= Math.Abs(_n.Z) ? 0 : Math.Abs(_n.Y) >= Math.Abs(_n.Z) ? 1 : 2;
        }

        private double U(Vec p) => _drop == 0 ? p.Y : _drop == 1 ? p.Z : p.X;

        private double W(Vec p) => _drop == 0 ? p.Z : _drop == 1 ? p.X : p.Y;

        private bool InRing(Vec[] ring, Vec p)
        {
            bool inside = false;

            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                double ui = U(ring[i]), wi = W(ring[i]), uj = U(ring[j]), wj = W(ring[j]);

                if ((wi > W(p)) != (wj > W(p)) && U(p) < (uj - ui) * (W(p) - wi) / (wj - wi) + ui)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Whether a point of the plane lies on the material.</summary>
        internal bool Holds(Vec p) => InRing(_outline, p) && !_holes.Any(h => InRing(h, p));

        internal Vec Normal => _n;

        private bool Pierce(Ln line, out Vec point)
        {
            double den = _n.Dot(line.D);

            point = default(Vec);

            if (Math.Abs(den) <= 1E-12)
            {
                return false;
            }

            point = line.At(_n.Dot(_v0 - line.O) / den);
            return Holds(point);
        }

        private IEnumerable<(Vec a, Vec b)> Edges()
        {
            foreach (Vec[] ring in new[] { _outline }.Concat(_holes))
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    yield return (ring[i], ring[(i + 1) % ring.Length]);
                }
            }
        }

        internal override double Gap(Ln line)
        {
            if (Pierce(line, out _))
            {
                return 0.0;
            }

            return Edges().Min(e => LineToSegment(line, e.a, e.b, out _));
        }

        internal override IEnumerable<Vec> Meetings(Ln line)
        {
            if (Pierce(line, out Vec point))
            {
                yield return point;
            }
        }

        internal override bool Delicate(Ln line)
        {
            double den = _n.Dot(line.D);

            if (Math.Abs(den) <= 1E-6)
            {
                return true;
            }

            Vec point = line.At(_n.Dot(_v0 - line.O) / den);

            return Edges().Min(e => PointToSegment(point, e.a, e.b)) <= Band;
        }

        internal override double GapFromPoint(Vec point)
        {
            double h = _n.Dot(point - _v0);
            Vec foot = point - _n * h;

            return Holds(foot) ? Math.Abs(h) : Edges().Min(e => PointToSegment(point, e.a, e.b));
        }
    }

    /// <summary>
    /// A shape taken to pieces, with the answers the line is held to.
    /// </summary>
    internal sealed class XLineSpecimen
    {
        internal XLineSpecimen(string kind, object shape, IReadOnlyList<Piece> pieces, Func<Random, Vec> pointOn, bool measured)
        {
            Kind = kind;
            Shape = shape;
            Pieces = pieces;
            PointOn = pointOn;
            Measured = measured;
        }

        /// <summary>The name of the kind of shape.</summary>
        internal string Kind { get; }

        /// <summary>The shape of the library.</summary>
        internal object Shape { get; }

        internal IReadOnlyList<Piece> Pieces { get; }

        /// <summary>A random point of the shape, to run a line through.</summary>
        internal Func<Random, Vec> PointOn { get; }

        /// <summary>Whether the shape is measured: whether it has a distance and a shortest line.</summary>
        internal bool Measured { get; }

        /// <summary>The distance of the line from the shape.</summary>
        internal double Distance(Ln line) => Pieces.Min(p => p.Gap(line));

        /// <summary>The distance of a point from the shape.</summary>
        internal double DistanceFrom(Vec point) => Pieces.Min(p => p.GapFromPoint(point));

        /// <summary>Whether the line passes so near a piece that a tolerance may decide what it meets.</summary>
        internal bool Delicate(Ln line)
        {
            foreach (Piece piece in Pieces)
            {
                if (piece.Delicate(line))
                {
                    return true;
                }

                double gap = piece.Gap(line);

                if (gap > 1E-9 && gap <= Piece.Band)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The places the line meets the shape, once each, in order along the line.</summary>
        internal Vec[] Meetings(Ln line)
        {
            var found = new List<Vec>();

            foreach (Piece piece in Pieces)
            {
                foreach (Vec point in piece.Meetings(line))
                {
                    if (!found.Any(f => f.DistanceTo(point) <= 1E-6))
                    {
                        found.Add(point);
                    }
                }
            }

            return found.OrderBy(p => line.Along(p)).ToArray();
        }
    }
}
