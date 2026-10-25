using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The vertices of a polyline as AutoCAD walks them: segment after segment, a line or, where the vertex it leaves has a
    /// bulge, an arc; the parameter the index of the vertex a segment leaves plus how far along it, in angle on an arc.
    /// </summary>
    /// <remarks>
    /// The points are in the polyline's own coordinates (those of its extrusion, the world's for a 3D polyline) and the
    /// shapes worked out there, each point then taken to the world as the reader takes one. An arc is worked out as AutoCAD
    /// and ezdxf work one out: the sweep four times the arc tangent of the bulge, the radius half the chord over the sine of
    /// half the sweep, the centre off the middle of the chord to its left by half the chord over the tangent of half the sweep.
    /// A segment whose ends are the same point, or whose bulge is nought, is a line, of no length in the first case.
    /// </remarks>
    internal sealed class DxfBulgeChain
    {
        private readonly GeoVector3 _normal;
        private readonly GeoVector3 _u;
        private readonly GeoVector3 _v;
        private readonly bool _world;
        private readonly List<GeoPoint3> _points;
        private readonly Segment[] _segments;
        private readonly double[] _cumulative;

        /// <summary>The chain of the points (in the coordinates of the normal's system) and the bulges of the segments they start.</summary>
        /// <param name="normal">The normal of the points' system, of unit length.</param>
        /// <param name="u">The x axis of the system in the world.</param>
        /// <param name="v">The y axis of the system in the world.</param>
        /// <param name="points">The vertices.</param>
        /// <param name="bulges">The bulge of each vertex; null for none.</param>
        /// <param name="closed">Whether a segment runs from the last vertex back to the first.</param>
        internal DxfBulgeChain(GeoVector3 normal, GeoVector3 u, GeoVector3 v, IReadOnlyList<GeoPoint3> points, IReadOnlyList<double> bulges, bool closed)
        {
            _normal = normal;
            _u = u;
            _v = v;
            _world = normal.X == 0.0 && normal.Y == 0.0 && normal.Z == 1.0;
            _points = new List<GeoPoint3>(points);
            int n = _points.Count;
            int count = n < 2 ? 0 : closed ? n : n - 1;
            _segments = new Segment[count];
            _cumulative = new double[count + 1];

            for (int i = 0; i < count; i++)
            {
                _segments[i] = new Segment(_points[i], _points[(i + 1) % n], bulges == null ? 0.0 : bulges[i]);
                _cumulative[i + 1] = _cumulative[i] + _segments[i].Length;
            }
        }

        /// <summary>How many segments the chain has: one less than its vertices, as many where it is closed, none below two.</summary>
        internal int SegmentCount => _segments.Length;

        /// <summary>The parameter at the end: the count of the segments.</summary>
        internal double EndParam => _segments.Length;

        /// <summary>The length, the sum of those of the segments.</summary>
        internal double Length => _cumulative[_cumulative.Length - 1];

        /// <summary>Whether a segment is an arc.</summary>
        internal bool IsArc(int segment) => _segments[segment].Arc;

        /// <summary>
        /// The area the chain closes in its plane, as AutoCAD reads that of a polyline: the polygon of its vertices, the last
        /// joined to the first, with what each arc adds or takes off; of the plane of its first two coordinates.
        /// </summary>
        internal double Area
        {
            get
            {
                int n = _points.Count;
                double area = 0.0;

                for (int i = 0; i < n; i++)
                {
                    GeoPoint3 p = _points[i];
                    GeoPoint3 q = _points[(i + 1) % n];
                    area += p.X * q.Y - q.X * p.Y;
                }

                area *= 0.5;

                foreach (Segment segment in _segments)
                {
                    area += segment.Area;
                }

                return Math.Abs(area);
            }
        }

        /// <summary>The point at a parameter, in the world.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the chain has no segment and no vertex.</exception>
        internal GeoPoint3 PointAt(double parameter)
        {
            if (_segments.Length == 0)
            {
                if (_points.Count == 0)
                {
                    throw new InvalidOperationException("The polyline has no vertex.");
                }

                return World(_points[0]);
            }

            Locate(parameter, out int i, out double f);
            return World(_segments[i].Point(f));
        }

        /// <summary>The derivative of the point by the parameter, in the world.</summary>
        internal GeoVector3 FirstDerivative(double parameter)
        {
            if (_segments.Length == 0)
            {
                return new GeoVector3(0.0, 0.0, 0.0);
            }

            Locate(parameter, out int i, out double f);
            return WorldVector(_segments[i].FirstDerivative(f));
        }

        /// <summary>The second derivative of the point by the parameter, in the world: nought on a line.</summary>
        internal GeoVector3 SecondDerivative(double parameter)
        {
            if (_segments.Length == 0)
            {
                return new GeoVector3(0.0, 0.0, 0.0);
            }

            Locate(parameter, out int i, out double f);
            return WorldVector(_segments[i].SecondDerivative(f));
        }

        /// <summary>The distance along the chain from its start to a parameter.</summary>
        internal double DistanceAt(double parameter)
        {
            if (_segments.Length == 0)
            {
                return 0.0;
            }

            Locate(parameter, out int i, out double f);
            return _cumulative[i] + _segments[i].Length * f;
        }

        /// <summary>The parameter at a distance along the chain from its start; the first where segments of no length meet.</summary>
        internal double ParameterAt(double distance)
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                if (distance <= _cumulative[i + 1] || i == _segments.Length - 1)
                {
                    double length = _segments[i].Length;
                    double f = length > 0.0 ? (distance - _cumulative[i]) / length : 0.0;
                    return i + Math.Max(0.0, Math.Min(1.0, f));
                }
            }

            return 0.0;
        }

        /// <summary>
        /// The parameter of the point of the chain nearest a point of the world, and that point: the first segment's where two
        /// are as near.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the chain has no vertex.</exception>
        internal double ClosestParameter(GeoPoint3 point, out GeoPoint3 closest)
        {
            if (_segments.Length == 0)
            {
                closest = PointAt(0.0);
                return 0.0;
            }

            GeoPoint3 q = Local(point);
            double best = double.PositiveInfinity;
            double parameter = 0.0;
            GeoPoint3 nearest = default(GeoPoint3);

            for (int i = 0; i < _segments.Length; i++)
            {
                double f = _segments[i].Closest(q);
                GeoPoint3 at = _segments[i].Point(f);
                double d = at.GetVectorTo(q).LengthSquared;

                if (d < best)
                {
                    best = d;
                    parameter = i + f;
                    nearest = at;
                }
            }

            closest = World(nearest);
            return parameter;
        }

        /// <summary>The segment and the fraction along it of a parameter: the last segment's end for the end parameter.</summary>
        private void Locate(double parameter, out int segment, out double fraction)
        {
            double floor = Math.Floor(parameter);
            segment = (int)Math.Max(0.0, Math.Min(_segments.Length - 1, floor));
            fraction = Math.Max(0.0, Math.Min(1.0, parameter - segment));
        }

        private GeoPoint3 World(GeoPoint3 p)
        {
            if (_world)
            {
                return p;
            }

            return new GeoPoint3(_u.X * p.X + _v.X * p.Y + _normal.X * p.Z, _u.Y * p.X + _v.Y * p.Y + _normal.Y * p.Z, _u.Z * p.X + _v.Z * p.Y + _normal.Z * p.Z);
        }

        private GeoVector3 WorldVector(GeoVector3 d)
        {
            GeoPoint3 p = World(new GeoPoint3(d.X, d.Y, d.Z));
            return new GeoVector3(p.X, p.Y, p.Z);
        }

        private GeoPoint3 Local(GeoPoint3 p)
        {
            if (_world)
            {
                return p;
            }

            return new GeoPoint3(_u.X * p.X + _u.Y * p.Y + _u.Z * p.Z, _v.X * p.X + _v.Y * p.Y + _v.Z * p.Z, _normal.X * p.X + _normal.Y * p.Y + _normal.Z * p.Z);
        }

        /// <summary>One segment: a line from one point to the next, or an arc of the plane at the height of its start.</summary>
        private readonly struct Segment
        {
            private readonly GeoPoint3 _p1;
            private readonly GeoPoint3 _p2;
            private readonly double _theta;
            private readonly double _r;
            private readonly double _cx;
            private readonly double _cy;
            private readonly double _a0;
            private readonly double _bulge;

            internal Segment(GeoPoint3 p1, GeoPoint3 p2, double bulge)
            {
                _p1 = p1;
                _p2 = p2;
                _bulge = bulge;
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double c = Math.Sqrt(dx * dx + dy * dy);

                if (Math.Abs(bulge) < 1E-300 || c == 0.0)
                {
                    Arc = false;
                    Length = p1.GetVectorTo(p2).Length;
                    _theta = 0.0;
                    _r = 0.0;
                    _cx = 0.0;
                    _cy = 0.0;
                    _a0 = 0.0;
                    return;
                }

                Arc = true;
                _theta = 4.0 * Math.Atan(bulge);
                _r = c / (2.0 * Math.Sin(Math.Abs(_theta) / 2.0));
                double h = c / 2.0 / Math.Tan(_theta / 2.0);
                double lx = -dy / c;
                double ly = dx / c;
                _cx = (p1.X + p2.X) * 0.5 + lx * h;
                _cy = (p1.Y + p2.Y) * 0.5 + ly * h;
                _a0 = Math.Atan2(p1.Y - _cy, p1.X - _cx);
                Length = _r * Math.Abs(_theta);
            }

            internal bool Arc { get; }

            internal double Length { get; }

            /// <summary>The signed area between the chord and the arc: what a bulge above nought adds to a loop counter-clockwise.</summary>
            internal double Area
            {
                get
                {
                    if (!Arc)
                    {
                        return 0.0;
                    }

                    double t = Math.Abs(_theta);
                    double s = 0.5 * _r * _r * (t - Math.Sin(t));
                    return _bulge > 0.0 ? s : -s;
                }
            }

            internal GeoPoint3 Point(double f)
            {
                if (!Arc)
                {
                    return new GeoPoint3(_p1.X + (_p2.X - _p1.X) * f, _p1.Y + (_p2.Y - _p1.Y) * f, _p1.Z + (_p2.Z - _p1.Z) * f);
                }

                double a = _a0 + _theta * f;
                return new GeoPoint3(_cx + Math.Cos(a) * _r, _cy + Math.Sin(a) * _r, _p1.Z);
            }

            internal GeoVector3 FirstDerivative(double f)
            {
                if (!Arc)
                {
                    return _p1.GetVectorTo(_p2);
                }

                double a = _a0 + _theta * f;
                return new GeoVector3(-Math.Sin(a) * (_r * _theta), Math.Cos(a) * (_r * _theta), 0.0);
            }

            internal GeoVector3 SecondDerivative(double f)
            {
                if (!Arc)
                {
                    return new GeoVector3(0.0, 0.0, 0.0);
                }

                double a = _a0 + _theta * f;
                double k = -_r * _theta * _theta;
                return new GeoVector3(Math.Cos(a) * k, Math.Sin(a) * k, 0.0);
            }

            /// <summary>The fraction along the segment of its point nearest a point, the start where two are as near.</summary>
            internal double Closest(GeoPoint3 q)
            {
                if (!Arc)
                {
                    GeoVector3 d = _p1.GetVectorTo(_p2);
                    double ll = d.LengthSquared;
                    return ll > 0.0 ? Math.Max(0.0, Math.Min(1.0, _p1.GetVectorTo(q).DotProduct(d) / ll)) : 0.0;
                }

                double dx = q.X - _cx;
                double dy = q.Y - _cy;

                if (dx == 0.0 && dy == 0.0)
                {
                    return 0.0;
                }

                double a = Math.Atan2(dy, dx);
                double turn = 2.0 * Math.PI;
                double delta = _theta > 0.0 ? a - _a0 : _a0 - a;
                delta -= turn * Math.Floor(delta / turn);
                double f = delta / Math.Abs(_theta);

                if (f <= 1.0)
                {
                    return f;
                }

                double toStart = Point(0.0).GetVectorTo(q).LengthSquared;
                double toEnd = Point(1.0).GetVectorTo(q).LengthSquared;
                return toEnd < toStart ? 1.0 : 0.0;
            }
        }
    }
}
