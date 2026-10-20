using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A piece of a shape in space as the oracle reads it: how far a point is from it. It shares no code with the library: the formulas
    /// are written out again here, for plain doubles.
    /// </summary>
    internal abstract class Meet3Part
    {
        /// <summary>Gets the distance from a point to the piece as it is read: a surface piece as the surface, a flat region as filled.</summary>
        internal abstract double Gap(double x, double y, double z);
    }

    /// <summary>A segment, a ray or a line without ends in space.</summary>
    internal sealed class Meet3Segment : Meet3Part
    {
        private readonly double _ox, _oy, _oz, _dx, _dy, _dz, _length;
        private readonly bool _ray, _endless;

        internal Meet3Segment(double ox, double oy, double oz, double dx, double dy, double dz, double length, bool ray, bool endless)
        {
            _ox = ox;
            _oy = oy;
            _oz = oz;
            _dx = dx;
            _dy = dy;
            _dz = dz;
            _length = length;
            _ray = ray;
            _endless = endless;
        }

        internal static Meet3Segment Between(GeoPoint3 a, GeoPoint3 b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double dz = b.Z - a.Z;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            return length > 0.0 ? new Meet3Segment(a.X, a.Y, a.Z, dx / length, dy / length, dz / length, length, false, false) : new Meet3Segment(a.X, a.Y, a.Z, 1.0, 0.0, 0.0, 0.0, false, false);
        }

        internal override double Gap(double x, double y, double z)
        {
            double t = (x - _ox) * _dx + (y - _oy) * _dy + (z - _oz) * _dz;

            if (!_endless)
            {
                t = Math.Max(t, 0.0);

                if (!_ray)
                {
                    t = Math.Min(t, _length);
                }
            }

            double ex = x - (_ox + t * _dx);
            double ey = y - (_oy + t * _dy);
            double ez = z - (_oz + t * _dz);

            return Math.Sqrt(ex * ex + ey * ey + ez * ez);
        }

        public override string ToString() => (_endless ? "xline" : _ray ? "ray" : "segment") + "(" + _ox.ToString("R") + ", " + _oy.ToString("R") + ", " + _oz.ToString("R") + " along " + _dx.ToString("R") + ", " + _dy.ToString("R") + ", " + _dz.ToString("R") + ", length " + _length.ToString("R") + ")";
    }

    /// <summary>A plane: the distance to it is the height over it.</summary>
    internal sealed class Meet3Plane : Meet3Part
    {
        private readonly double _ox, _oy, _oz, _nx, _ny, _nz;

        internal Meet3Plane(double ox, double oy, double oz, double nx, double ny, double nz)
        {
            _ox = ox;
            _oy = oy;
            _oz = oz;
            _nx = nx;
            _ny = ny;
            _nz = nz;
        }

        internal override double Gap(double x, double y, double z) => Math.Abs((x - _ox) * _nx + (y - _oy) * _ny + (z - _oz) * _nz);

        public override string ToString() => "plane(" + _ox.ToString("R") + ", " + _oy.ToString("R") + ", " + _oz.ToString("R") + " normal " + _nx.ToString("R") + ", " + _ny.ToString("R") + ", " + _nz.ToString("R") + ")";
    }

    /// <summary>
    /// A flat region in a plane given by an origin and two unit directions square to each other, read filled: the rings of corners
    /// are given by their coordinates in the plane, and a place stands over the region by the even-odd rule over all of them.
    /// </summary>
    internal sealed class Meet3Flat : Meet3Part
    {
        private readonly double[] _o, _u, _v, _n;
        private readonly List<GeoPoint2[]> _rings;

        internal Meet3Flat(double[] o, double[] u, double[] v, List<GeoPoint2[]> rings)
        {
            _o = o;
            _u = u;
            _v = v;
            _n = new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
            _rings = rings;
        }

        internal GeoPoint3 Corner(GeoPoint2 p) => new GeoPoint3(_o[0] + p.X * _u[0] + p.Y * _v[0], _o[1] + p.X * _u[1] + p.Y * _v[1], _o[2] + p.X * _u[2] + p.Y * _v[2]);

        internal override double Gap(double x, double y, double z)
        {
            double rx = x - _o[0];
            double ry = y - _o[1];
            double rz = z - _o[2];
            double height = rx * _n[0] + ry * _n[1] + rz * _n[2];
            double a = rx * _u[0] + ry * _u[1] + rz * _u[2];
            double b = rx * _v[0] + ry * _v[1] + rz * _v[2];
            bool inside = false;

            foreach (GeoPoint2[] ring in _rings)
            {
                if (Spline2MeetOracle.InRing(ring, a, b))
                {
                    inside = !inside;
                }
            }

            if (inside)
            {
                return Math.Abs(height);
            }

            double best = double.MaxValue;

            foreach (GeoPoint2[] ring in _rings)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    best = Math.Min(best, Meet3Segment.Between(Corner(ring[i]), Corner(ring[(i + 1) % ring.Length])).Gap(x, y, z));
                }
            }

            return best;
        }

        /// <summary>Gets the sides of every ring as segments.</summary>
        internal IEnumerable<Meet3Part> Sides()
        {
            foreach (GeoPoint2[] ring in _rings)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    yield return Meet3Segment.Between(Corner(ring[i]), Corner(ring[(i + 1) % ring.Length]));
                }
            }
        }

        public override string ToString() => "flat(" + string.Join(" | ", _rings.Select(r => string.Join(" ", r.Select(p => Corner(p).ToString())))) + ")";
    }

    /// <summary>
    /// An ellipse in space, or a circle, as the oracle reads it: the rim (distance from the curve) or the disc (filled), by the
    /// distance in the plane, found by the bisection of Eberly's method, and the height over the plane.
    /// </summary>
    internal sealed class Meet3Ellipse : Meet3Part
    {
        private readonly double[] _c, _u, _v, _n;
        private readonly double _a, _b;
        private readonly bool _disc;
        private readonly MeetEllipse _flat;

        internal Meet3Ellipse(double[] c, double[] u, double[] v, double a, double b, bool disc)
        {
            _c = c;
            _u = u;
            _v = v;
            _n = new[] { u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0] };
            _a = a;
            _b = b;
            _disc = disc;
            _flat = new MeetEllipse(0.0, 0.0, 1.0, 0.0, a, b);
        }

        internal override double Gap(double x, double y, double z)
        {
            double rx = x - _c[0];
            double ry = y - _c[1];
            double rz = z - _c[2];
            double height = rx * _n[0] + ry * _n[1] + rz * _n[2];
            double p = rx * _u[0] + ry * _u[1] + rz * _u[2];
            double q = rx * _v[0] + ry * _v[1] + rz * _v[2];

            if (_disc && (p / _a) * (p / _a) + (q / _b) * (q / _b) <= 1.0)
            {
                return Math.Abs(height);
            }

            double d = _a == _b ? Math.Abs(Math.Sqrt(p * p + q * q) - _a) : _flat.Gap(p, q);

            return Math.Sqrt(height * height + d * d);
        }

        public override string ToString() => (_disc ? "disc" : "rim") + "(" + string.Join(", ", _c.Select(v => v.ToString("R"))) + " u " + string.Join(", ", _u.Select(v => v.ToString("R"))) + " v " + string.Join(", ", _v.Select(v => v.ToString("R"))) + " radii " + _a.ToString("R") + ", " + _b.ToString("R") + ")";
    }

    /// <summary>An arc of a circle in space: the points of the plane square to the normal within its angle, and its two ends.</summary>
    internal sealed class Meet3Arc : Meet3Part
    {
        private readonly double[] _c, _s, _t, _n;
        private readonly double _r, _sweep;

        /// <summary>Makes the arc from its centre, the unit direction to its start, the unit direction a quarter turn on, the radius and the signed sweep.</summary>
        internal Meet3Arc(double[] c, double[] s, double[] t, double r, double sweep)
        {
            _c = c;
            _s = s;
            _t = t;
            _n = new[] { s[1] * t[2] - s[2] * t[1], s[2] * t[0] - s[0] * t[2], s[0] * t[1] - s[1] * t[0] };
            _r = r;
            _sweep = sweep;
        }

        private double[] At(double angle) => new[]
        {
            _c[0] + _r * (Math.Cos(angle) * _s[0] + Math.Sin(angle) * _t[0]),
            _c[1] + _r * (Math.Cos(angle) * _s[1] + Math.Sin(angle) * _t[1]),
            _c[2] + _r * (Math.Cos(angle) * _s[2] + Math.Sin(angle) * _t[2])
        };

        internal override double Gap(double x, double y, double z)
        {
            double rx = x - _c[0];
            double ry = y - _c[1];
            double rz = z - _c[2];
            double height = rx * _n[0] + ry * _n[1] + rz * _n[2];
            double p = rx * _s[0] + ry * _s[1] + rz * _s[2];
            double q = rx * _t[0] + ry * _t[1] + rz * _t[2];
            double rho = Math.Sqrt(p * p + q * q);
            bool whole = Math.Abs(_sweep) >= 2.0 * Math.PI;

            if (rho > 0.0)
            {
                double angle = Math.Atan2(q, p);
                double along = _sweep >= 0.0 ? angle : -angle;
                along -= 2.0 * Math.PI * Math.Floor(along / (2.0 * Math.PI));

                if (whole || along <= Math.Abs(_sweep))
                {
                    return Math.Sqrt(height * height + (rho - _r) * (rho - _r));
                }
            }
            else if (whole)
            {
                return Math.Sqrt(height * height + _r * _r);
            }

            double[] start = At(0.0);
            double[] end = At(_sweep);

            return Math.Min(
                Math.Sqrt((x - start[0]) * (x - start[0]) + (y - start[1]) * (y - start[1]) + (z - start[2]) * (z - start[2])),
                Math.Sqrt((x - end[0]) * (x - end[0]) + (y - end[1]) * (y - end[1]) + (z - end[2]) * (z - end[2])));
        }

        public override string ToString() => "arc(" + string.Join(", ", _c.Select(v => v.ToString("R"))) + " r " + _r.ToString("R") + " from " + string.Join(", ", _s.Select(v => v.ToString("R"))) + " sweep " + _sweep.ToString("R") + ")";
    }

    /// <summary>The surface of a ball: the distance to it is the difference of the distance from the centre and the radius.</summary>
    internal sealed class Meet3Sphere : Meet3Part
    {
        private readonly double _cx, _cy, _cz, _r;

        internal Meet3Sphere(double cx, double cy, double cz, double r)
        {
            _cx = cx;
            _cy = cy;
            _cz = cz;
            _r = r;
        }

        internal override double Gap(double x, double y, double z) => Math.Abs(Math.Sqrt((x - _cx) * (x - _cx) + (y - _cy) * (y - _cy) + (z - _cz) * (z - _cz)) - _r);

        public override string ToString() => "sphere(" + _cx.ToString("R") + ", " + _cy.ToString("R") + ", " + _cz.ToString("R") + " r " + _r.ToString("R") + ")";
    }

    /// <summary>The skin of a box with its own axes: the distance to it from outside and from inside.</summary>
    internal sealed class Meet3Block : Meet3Part
    {
        private readonly double[] _c;
        private readonly double[][] _a;
        private readonly double[] _h;

        internal Meet3Block(double[] c, double[][] a, double[] h)
        {
            _c = c;
            _a = a;
            _h = h;
        }

        internal bool Holds(double x, double y, double z)
        {
            for (int k = 0; k < 3; k++)
            {
                double l = (x - _c[0]) * _a[k][0] + (y - _c[1]) * _a[k][1] + (z - _c[2]) * _a[k][2];

                if (Math.Abs(l) > _h[k])
                {
                    return false;
                }
            }

            return true;
        }

        internal override double Gap(double x, double y, double z)
        {
            double outside = 0.0;
            double inside = double.MaxValue;

            for (int k = 0; k < 3; k++)
            {
                double l = Math.Abs((x - _c[0]) * _a[k][0] + (y - _c[1]) * _a[k][1] + (z - _c[2]) * _a[k][2]);
                double over = Math.Max(0.0, l - _h[k]);
                outside += over * over;
                inside = Math.Min(inside, _h[k] - l);
            }

            return outside > 0.0 ? Math.Sqrt(outside) : inside;
        }

        public override string ToString() => "box(" + string.Join(", ", _c.Select(v => v.ToString("R"))) + " axes " + string.Join(" ", _a.Select(v => "[" + string.Join(", ", v.Select(w => w.ToString("R"))) + "]")) + " half " + string.Join(", ", _h.Select(v => v.ToString("R"))) + ")";
    }

    /// <summary>
    /// A shape as the oracle reads it, and the library's own object drawn from the same numbers: the pieces the distance is measured
    /// to, the pieces its crossings are those of where they are not the same, and the test of its material where it is a body.
    /// </summary>
    internal sealed class Meet3Shape
    {
        internal object Shape;
        internal string Kind;
        internal List<Meet3Part> Parts = new List<Meet3Part>();
        internal List<Meet3Part> Rims;
        internal Func<double, double, double, bool> Inside;

        internal List<Meet3Part> Crossing => Rims ?? Parts;
    }

    /// <summary>
    /// The oracle for a spline against a shape in space: the spline is sampled densely over every span, the least of its distance to
    /// each piece found by golden section round each least of the samples, and a place is such a least within the tolerance that no
    /// other piece is nearer at; where two places are within the tolerance of each other they are one. Points are those of the
    /// Cox-de Boor recursion in plain doubles of the spline oracle, so the numbers share nothing with the library.
    /// </summary>
    internal static class Spline3MeetOracle
    {
        /// <summary>The result of asking: the distance, the distance to the surface alone, and the places.</summary>
        internal sealed class Answer
        {
            internal double Distance;

            /// <summary>The distance to the surface or outline alone: what the shortest segment is as long as, where a spline inside a body is nought from it.</summary>
            internal double Boundary;

            /// <summary>Every place the spline comes near the shape: each least of the distance to a piece within the tolerance that no other piece is nearer at, those within the tolerance of each other one place.</summary>
            internal List<double[]> Places = new List<double[]>();

            /// <summary>The value of every least of the distance to a piece that is within twice the tolerance: a case with one at the tolerance is not clear-cut.</summary>
            internal List<double> Leasts = new List<double>();

            /// <summary>The length of the longest stretch of the spline that is within the tolerance of the shape with its distance changing by no more than half the tolerance.</summary>
            internal double LongestLevel;
        }

        private static double Length(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]));

        /// <summary>
        /// Asks the oracle about a spline and a shape.
        /// </summary>
        internal static Answer Ask(SplineOracle spline, Meet3Shape shape, double tolerance, int samples = 500)
        {
            var answer = new Answer();
            double[] breaks = spline.Breaks();
            var us = new List<double>();

            for (int i = 0; i + 1 < breaks.Length; i++)
            {
                int n = Math.Max(samples, 40) / Math.Max(1, breaks.Length - 1) + 40;

                for (int j = 0; j < n; j++)
                {
                    us.Add(breaks[i] + (breaks[i + 1] - breaks[i]) * j / n);
                }
            }

            us.Add(breaks[breaks.Length - 1]);

            // How large the spline is, not how far from the origin: a spline a hundred long thirty million out is as fine to sample as at the origin.
            double reach = Math.Max(1.0, Enumerable.Range(0, 3).Max(k => Enumerable.Range(0, spline.Count).Max(i => spline.Points[3 * i + k]) - Enumerable.Range(0, spline.Count).Min(i => spline.Points[3 * i + k])));
            double spacing = 5E-4;
            double[][] points = us.Select(u => spline.FastPoint(u)).ToArray();
            Func<double[], double> toShape = p => shape.Parts.Min(part => part.Gap(p[0], p[1], p[2]));
            double[] shapeGaps = points.Select(toShape).ToArray();

            // Two neighbours within a segment's length of the shape, as near it as that, are given a sample between, down to a thousandth of
            // a millionth of the size: a curve can come within the tolerance of a piece between two samples that are both farther.
            for (int round = 0; round < 40 && us.Count < 400000; round++)
            {
                var nextUs = new List<double>(us.Count);
                var nextPoints = new List<double[]>(us.Count);
                var nextShapeGaps = new List<double>(us.Count);
                bool split = false;

                for (int i = 0; i < us.Count; i++)
                {
                    nextUs.Add(us[i]);
                    nextPoints.Add(points[i]);
                    nextShapeGaps.Add(shapeGaps[i]);

                    if (i + 1 < us.Count)
                    {
                        double gap = Length(points[i], points[i + 1]);
                        double middle = 0.5 * (us[i] + us[i + 1]);
                        bool wide = gap > spacing * reach;
                        bool close = us.Count < 60000 && Math.Min(shapeGaps[i], shapeGaps[i + 1]) <= 1.5 * gap && gap > 1E-5 * reach;

                        if ((wide || close) && middle > us[i] && middle < us[i + 1])
                        {
                            double[] p = spline.FastPoint(middle);
                            nextUs.Add(middle);
                            nextPoints.Add(p);
                            nextShapeGaps.Add(toShape(p));
                            split = true;
                        }
                    }
                }

                us = nextUs;
                points = nextPoints.ToArray();
                shapeGaps = nextShapeGaps.ToArray();

                if (!split)
                {
                    break;
                }
            }

            // The longest stretch that stays within the tolerance of the shape and level, which the library reads as lying along it.
            double level = 0.0;
            double levelLow = 0.0;
            double levelHigh = 0.0;
            double run = 0.0;
            bool inLevel = false;

            for (int i = 0; i < us.Count; i++)
            {
                double d = shapeGaps[i];

                if (d <= tolerance && inLevel && Math.Max(levelHigh, d) - Math.Min(levelLow, d) <= 0.5 * tolerance)
                {
                    run += Length(points[i], points[i - 1]);
                    levelLow = Math.Min(levelLow, d);
                    levelHigh = Math.Max(levelHigh, d);
                }
                else
                {
                    inLevel = d <= tolerance;
                    run = 0.0;
                    levelLow = d;
                    levelHigh = d;
                }

                level = Math.Max(level, run);
            }

            answer.LongestLevel = level;

            double least = double.MaxValue;
            var nearCandidates = new List<double[]>();

            // The least of the distance to each piece, from each least of the samples: the places are those of the pieces the crossings are of.
            foreach (Meet3Part part in shape.Parts)
            {
                bool isCrossing = shape.Crossing.Contains(part);
                var gaps = new double[us.Count];

                for (int i = 0; i < us.Count; i++)
                {
                    gaps[i] = part.Gap(points[i][0], points[i][1], points[i][2]);
                }

                for (int i = 0; i < us.Count; i++)
                {
                    bool isLeast = (i == 0 || gaps[i] <= gaps[i - 1]) && (i == us.Count - 1 || gaps[i] <= gaps[i + 1]);

                    if (!isLeast)
                    {
                        continue;
                    }

                    double v = Spline2MeetOracle.Golden(u => { double[] p = spline.FastPoint(u); return part.Gap(p[0], p[1], p[2]); }, us[Math.Max(i - 1, 0)], us[Math.Min(i + 1, us.Count - 1)], out double leastAt);

                    if (gaps[i] < v)
                    {
                        v = gaps[i];
                        leastAt = us[i];
                    }

                    least = Math.Min(least, v);

                    if (!isCrossing)
                    {
                        continue;
                    }

                    if (v <= 2.0 * tolerance)
                    {
                        answer.Leasts.Add(v);
                    }

                    if (v <= tolerance)
                    {
                        nearCandidates.Add(new[] { leastAt, v });
                    }
                }
            }

            double[] start = spline.FastPoint(spline.Start);
            bool inside = shape.Inside != null && shape.Inside(start[0], start[1], start[2]);
            answer.Distance = inside ? 0.0 : least;
            answer.Boundary = least;

            // Every place: each least within the tolerance that no other piece of the crossings is nearer at, those within the tolerance
            // of each other, one from the next, being one place.
            var found = new List<double[]>();
            double slack = 1E-9 * Math.Max(1.0, reach);

            foreach (double[] candidate in nearCandidates.OrderBy(c => c[0]))
            {
                double[] p = spline.FastPoint(candidate[0]);

                if (shape.Crossing.All(part => part.Gap(p[0], p[1], p[2]) >= candidate[1] - slack))
                {
                    found.Add(p);
                }
            }

            var parent = Enumerable.Range(0, found.Count).ToArray();
            Func<int, int> find = null;
            find = i => parent[i] == i ? i : (parent[i] = find(parent[i]));

            for (int i = 0; i < found.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (Length(found[i], found[j]) <= tolerance)
                    {
                        parent[find(i)] = find(j);
                    }
                }
            }

            for (int i = 0; i < found.Count; i++)
            {
                if (find(i) == i)
                {
                    answer.Places.Add(found[i]);
                }
            }

            return answer;
        }

        /// <summary>The result of asking about two splines: the distance, and the places they come within the tolerance of each other as points of the first.</summary>
        internal sealed class SplineAnswer
        {
            internal double Distance;
            internal List<double[]> Places = new List<double[]>();
        }

        /// <summary>
        /// Asks the oracle about two splines: both are sampled, each least of the grid of sample pairs is brought to a least by a compass
        /// search that keeps inside the domains, and a place is a least within the tolerance, those within the tolerance of each other
        /// being one place.
        /// </summary>
        internal static SplineAnswer AskSplines(SplineOracle a, SplineOracle b, double tolerance, int samples = 240)
        {
            var answer = new SplineAnswer();
            double[] ua = Enumerable.Range(0, samples + 1).Select(i => a.Start + (a.End - a.Start) * i / samples).ToArray();
            double[] ub = Enumerable.Range(0, samples + 1).Select(i => b.Start + (b.End - b.Start) * i / samples).ToArray();
            double[][] pa = ua.Select(u => a.FastPoint(u)).ToArray();
            double[][] pb = ub.Select(u => b.FastPoint(u)).ToArray();
            var grid = new double[samples + 1, samples + 1];

            for (int i = 0; i <= samples; i++)
            {
                for (int j = 0; j <= samples; j++)
                {
                    grid[i, j] = Length(pa[i], pb[j]);
                }
            }

            double best = double.MaxValue;
            var found = new List<double[]>();

            for (int i = 0; i <= samples; i++)
            {
                for (int j = 0; j <= samples; j++)
                {
                    bool isLeast = true;

                    for (int di = -1; di <= 1 && isLeast; di++)
                    {
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int ii = i + di;
                            int jj = j + dj;

                            if ((di == 0 && dj == 0) || ii < 0 || jj < 0 || ii > samples || jj > samples)
                            {
                                continue;
                            }

                            if (grid[ii, jj] < grid[i, j])
                            {
                                isLeast = false;
                                break;
                            }
                        }
                    }

                    if (!isLeast)
                    {
                        continue;
                    }

                    double s = ua[i];
                    double t = ub[j];
                    double value = Compass(a, b, ref s, ref t, (a.End - a.Start) / samples, (b.End - b.Start) / samples, grid[i, j]);
                    best = Math.Min(best, value);

                    if (value <= tolerance)
                    {
                        found.Add(new[] { s, t, value });
                    }
                }
            }

            answer.Distance = best;
            var points = new List<double[]>();

            foreach (double[] f in found.OrderBy(f => f[0]))
            {
                double[] p = a.FastPoint(f[0]);

                if (!points.Any(q => Length(p, q) <= tolerance))
                {
                    points.Add(p);
                }
            }

            answer.Places = points;
            return answer;
        }

        /// <summary>A compass search for a least of the distance between two splines, kept inside their domains, from a start.</summary>
        private static double Compass(SplineOracle a, SplineOracle b, ref double s, ref double t, double stepS, double stepT, double value)
        {
            double Distance(double ss, double tt) => Length(a.FastPoint(ss), b.FastPoint(tt));

            for (int iteration = 0; iteration < 4000 && stepS > 1E-15 * (a.End - a.Start); iteration++)
            {
                bool moved = false;

                for (int d = 0; d < 8 && !moved; d++)
                {
                    double ds = d == 0 || d == 4 || d == 5 ? stepS : d == 1 || d == 6 || d == 7 ? -stepS : 0.0;
                    double dt = d == 2 || d == 4 || d == 6 ? stepT : d == 3 || d == 5 || d == 7 ? -stepT : 0.0;
                    double ns = Math.Min(Math.Max(s + ds, a.Start), a.End);
                    double nt = Math.Min(Math.Max(t + dt, b.Start), b.End);
                    double nv = Distance(ns, nt);

                    if (nv < value)
                    {
                        value = nv;
                        s = ns;
                        t = nt;
                        moved = true;
                    }
                }

                if (!moved)
                {
                    stepS *= 0.5;
                    stepT *= 0.5;
                }
            }

            return value;
        }
    }
}
