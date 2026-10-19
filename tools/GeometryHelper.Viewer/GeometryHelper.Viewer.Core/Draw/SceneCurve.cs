using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What a drawn curve is, to the snaps and the picking.
    /// </summary>
    public enum CurveShape
    {
        /// <summary>
        /// A straight segment between two ends.
        /// </summary>
        Segment,

        /// <summary>
        /// A circle, an ellipse, or an arc of one: <c>P(t) = Center + AxisU * RadiusU * cos t + AxisV * RadiusV * sin t</c>.
        /// </summary>
        Conic,
    }

    /// <summary>
    /// One exact curve of a scene: a straight segment, or a circle, an ellipse or an arc of one, held as the numbers in the item's
    /// data and not as the chords it is drawn with, so that an end, a middle or a centre is exact and the nearest point of a curve
    /// to the cursor is the nearest point of the curve itself.
    /// </summary>
    public sealed class SceneCurve
    {
        private const double TwoPi = 2.0 * Math.PI;

        private readonly Vec3 _start;
        private readonly Vec3 _end;
        private readonly Vec3? _middle;
        private readonly Vec3 _radial;
        private readonly Vec3 _tangent;
        private readonly bool _anchored;

        private SceneCurve(int itemIndex, Vec3 a, Vec3 b)
        {
            ItemIndex = itemIndex;
            Shape = CurveShape.Segment;
            _start = a;
            _end = b;
        }

        private SceneCurve(int itemIndex, Vec3 center, Vec3 u, Vec3 v, double radiusU, double radiusV, double startAngle, double sweep, Vec3? start, Vec3? end, Vec3? middle)
        {
            ItemIndex = itemIndex;
            Shape = CurveShape.Conic;
            Center = center;
            AxisU = u;
            AxisV = v;
            Normal = Vec3.Cross(u, v).Normalized();
            RadiusU = radiusU;
            RadiusV = radiusV;
            StartAngle = startAngle;
            Sweep = sweep;
            IsFull = Math.Abs(sweep) >= TwoPi - 1E-12;
            _start = start ?? PointAtCentre(startAngle);

            // A circle is measured from its start, not from its centre: with the centre a million times the radius away, as it is for the arc of
            // a bulge of 1E-6 on a long chord, a coordinate measured from it has lost the last digits it would need. From the start the
            // numbers are the size of the arc, so the arc is as exact as its start.
            if (radiusU == radiusV)
            {
                _radial = u * Math.Cos(startAngle) + v * Math.Sin(startAngle);
                _tangent = v * Math.Cos(startAngle) - u * Math.Sin(startAngle);
                _anchored = true;
            }

            _end = end ?? PointAt(startAngle + sweep);
            _middle = IsFull ? null : middle ?? PointAt(startAngle + sweep * 0.5);
        }

        internal static SceneCurve Segment(int itemIndex, Vec3 a, Vec3 b) => new SceneCurve(itemIndex, a, b);

        internal static SceneCurve Conic(int itemIndex, Vec3 center, Vec3 u, Vec3 v, double radiusU, double radiusV, double startAngle, double sweep)
            => new SceneCurve(itemIndex, center, u, v, radiusU, radiusV, startAngle, sweep, null, null, null);

        /// <summary>
        /// Makes the arc of an edge of a chain with a bulge, whose ends are exactly the vertices and whose middle is the vertices'
        /// middle pushed out by the sagitta, a bulge times half the chord. A bulge too small for a circle of a sensible radius, under
        /// a billionth, gives the straight segment.
        /// </summary>
        internal static SceneCurve Edge(int itemIndex, Vec3 from, Vec3 to, double bulge, Vec3 normal)
        {
            Vec3 chord = to - from;
            double length = chord.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                return null;
            }

            if (double.IsNaN(bulge) || double.IsInfinity(bulge) || Math.Abs(bulge) < 1E-9 || !(normal.Length > 0.0))
            {
                return Segment(itemIndex, from, to);
            }

            Vec3 left = Vec3.Cross(normal, chord) * (1.0 / length);
            Vec3 middle = (from + to) * 0.5;
            Vec3 center = middle + left * (length * 0.5 * (1.0 - bulge * bulge) / (2.0 * bulge));
            double radius = length * (1.0 + bulge * bulge) / (4.0 * Math.Abs(bulge));
            Vec3 u = (left * -(length * 0.5 * (1.0 - bulge * bulge) / (2.0 * bulge)) - chord * (0.5)).Normalized();
            Vec3 v = Vec3.Cross(normal, u);

            return new SceneCurve(itemIndex, center, u, v, radius, radius, 0.0, 4.0 * Math.Atan(bulge), from, to, middle - left * (bulge * length * 0.5));
        }

        /// <summary>
        /// Gets the index of the item in <see cref="Scene.Items"/> the curve belongs to.
        /// </summary>
        public int ItemIndex { get; }

        /// <summary>
        /// Gets whether the curve is a segment or a conic.
        /// </summary>
        public CurveShape Shape { get; }

        /// <summary>
        /// Gets the first end; for a circle or an ellipse, the point where its parameter starts.
        /// </summary>
        public Vec3 Start => _start;

        /// <summary>
        /// Gets the last end; for a circle or an ellipse, the same point as <see cref="Start"/>.
        /// </summary>
        public Vec3 End => _end;

        /// <summary>
        /// Gets whether the curve has ends: every segment and every arc, but not a full circle or ellipse.
        /// </summary>
        public bool HasEnds => Shape == CurveShape.Segment || !IsFull;

        /// <summary>
        /// Gets the middle of a segment, or of an arc along the curve; none for a full circle or ellipse.
        /// </summary>
        public Vec3? Middle => Shape == CurveShape.Segment ? (_start + _end) * 0.5 : _middle;

        /// <summary>
        /// Gets the centre of a conic; the origin for a segment.
        /// </summary>
        public Vec3 Center { get; }

        /// <summary>
        /// Gets the unit vector the parameter of a conic starts along; the zero vector for a segment.
        /// </summary>
        public Vec3 AxisU { get; }

        /// <summary>
        /// Gets the unit vector a quarter turn from <see cref="AxisU"/> in the plane of a conic; the zero vector for a segment.
        /// </summary>
        public Vec3 AxisV { get; }

        /// <summary>
        /// Gets the unit normal of the plane of a conic, <see cref="AxisU"/> crossed with <see cref="AxisV"/>; the zero vector for a segment.
        /// </summary>
        public Vec3 Normal { get; }

        /// <summary>
        /// Gets the half length of a conic along <see cref="AxisU"/>, its radius where it is a circle; 0 for a segment.
        /// </summary>
        public double RadiusU { get; }

        /// <summary>
        /// Gets the half length of a conic along <see cref="AxisV"/>; 0 for a segment.
        /// </summary>
        public double RadiusV { get; }

        /// <summary>
        /// Gets the parameter an arc starts at, in radians; for an arc of a circle the angle round the normal.
        /// </summary>
        public double StartAngle { get; }

        /// <summary>
        /// Gets the angle an arc runs through, positive counter-clockwise round <see cref="Normal"/>; a full turn for a circle.
        /// </summary>
        public double Sweep { get; }

        /// <summary>
        /// Gets whether a conic is a whole circle or ellipse and not an arc.
        /// </summary>
        public bool IsFull { get; }

        /// <summary>
        /// Gets whether the curve is a circle or an arc of one.
        /// </summary>
        public bool IsCircular => Shape == CurveShape.Conic && RadiusU == RadiusV;

        /// <summary>
        /// Gets the point of a conic at a parameter.
        /// </summary>
        /// <param name="t">The parameter in radians.</param>
        public Vec3 PointAt(double t)
        {
            if (!_anchored)
            {
                return PointAtCentre(t);
            }

            // The start and the turn from it: R (cos f - 1) is -2 R sin^2 (f / 2), which keeps its digits where cos f - 1 loses them.
            double f = t - StartAngle;
            double half = Math.Sin(f * 0.5);
            return _start + _radial * (-2.0 * RadiusU * half * half) + _tangent * (RadiusU * Math.Sin(f));
        }

        /// <summary>
        /// Gets the unit vector from the centre of a circle to its start.
        /// </summary>
        internal Vec3 RadialAtStart => _radial;

        /// <summary>
        /// Gets the unit vector along a circle at its start, the way the parameter grows.
        /// </summary>
        internal Vec3 TangentAtStart => _tangent;

        private Vec3 PointAtCentre(double t) => Center + AxisU * (RadiusU * Math.Cos(t)) + AxisV * (RadiusV * Math.Sin(t));

        /// <summary>
        /// Gets where a point is to a circle measured from its start: along the radius at the start, along the tangent there, and out of the
        /// plane. The circle is the points where <c>(u + R)^2 + v^2 = R^2</c> and the gap of a point in the plane from it is
        /// <c>(u^2 + v^2 + 2 R u) / (sqrt((u + R)^2 + v^2) + R)</c>, which keeps its digits where the difference of two radii does not.
        /// </summary>
        internal void ToStartFrame(Vec3 point, out double u, out double v, out double z)
        {
            Vec3 w = point - _start;
            u = Vec3.Dot(w, _radial);
            v = Vec3.Dot(w, _tangent);
            z = Vec3.Dot(w, Normal);
        }

        /// <summary>
        /// Gets how far a point is from the circle in its plane, positive outside it, and how far it is out of the plane. Only for a circle or an arc.
        /// </summary>
        internal double GapFromCircle(Vec3 point, out double z)
        {
            ToStartFrame(point, out double u, out double v, out z);
            double r = RadiusU;
            return (u * u + v * v + 2.0 * r * u) / (Math.Sqrt((u + r) * (u + r) + v * v) + r);
        }

        /// <summary>
        /// Gets the parameter at which the conic is nearest, in its plane, to a point.
        /// </summary>
        /// <param name="point">The point.</param>
        public double ParameterOf(Vec3 point)
        {
            if (_anchored)
            {
                ToStartFrame(point, out double u, out double v, out _);
                return StartAngle + Math.Atan2(v, u + RadiusU);
            }

            Vec3 d = point - Center;
            return Math.Atan2(Vec3.Dot(d, AxisV) / RadiusV, Vec3.Dot(d, AxisU) / RadiusU);
        }

        /// <summary>
        /// Gets whether a parameter lies on the curve, within an angle.
        /// </summary>
        /// <param name="t">The parameter in radians.</param>
        /// <param name="angleTolerance">The angle in radians the parameter may be outside the arc by.</param>
        public bool ContainsParameter(double t, double angleTolerance)
        {
            if (IsFull)
            {
                return true;
            }

            double along = Wrap(Sweep >= 0.0 ? t - StartAngle : StartAngle - t);
            return along <= Math.Abs(Sweep) + angleTolerance || along >= TwoPi - angleTolerance;
        }

        private static double Wrap(double angle)
        {
            double wrapped = angle % TwoPi;
            return wrapped < 0.0 ? wrapped + TwoPi : wrapped;
        }

        /// <summary>
        /// Gets a sphere that holds the whole curve.
        /// </summary>
        internal void BoundingSphere(out Vec3 center, out double radius)
        {
            if (Shape == CurveShape.Segment)
            {
                center = (_start + _end) * 0.5;
                radius = (_end - _start).Length * 0.5;
            }
            else if (!IsFull && Math.Abs(Sweep) <= Math.PI && IsCircular)
            {
                // A minor arc lies inside the disc that has its chord for a diameter.
                center = (_start + _end) * 0.5;
                radius = (_end - _start).Length * 0.5;
            }
            else
            {
                center = Center;
                radius = Math.Max(RadiusU, RadiusV);
            }
        }

        /// <summary>
        /// Finds the point of the curve nearest to a line of sight.
        /// </summary>
        /// <param name="origin">A point of the line.</param>
        /// <param name="direction">The unit direction of the line.</param>
        /// <param name="forwardOnly">Whether only the part of the curve in front of the origin counts, as for the ray of a perspective eye.</param>
        /// <param name="onCurve">The nearest point of the curve.</param>
        /// <returns>How far that point is from the line; infinity when no part of the curve is in front.</returns>
        public double DistanceToLine(Vec3 origin, Vec3 direction, bool forwardOnly, out Vec3 onCurve)
        {
            if (Shape == CurveShape.Segment)
            {
                return SegmentToLine(_start, _end, origin, direction, forwardOnly, out onCurve);
            }

            return ConicToLine(origin, direction, forwardOnly, out onCurve);
        }

        private static double SegmentToLine(Vec3 a, Vec3 b, Vec3 origin, Vec3 direction, bool forwardOnly, out Vec3 closest)
        {
            if (forwardOnly)
            {
                double da = Vec3.Dot(a - origin, direction);
                double db = Vec3.Dot(b - origin, direction);

                if (da < 0.0 && db < 0.0)
                {
                    closest = a;
                    return double.PositiveInfinity;
                }

                if (da < 0.0)
                {
                    a = a + (b - a) * (da / (da - db));
                }
                else if (db < 0.0)
                {
                    b = b + (a - b) * (db / (db - da));
                }
            }

            Vec3 e = b - a;
            Vec3 w = a - origin;
            Vec3 ePerp = e - direction * Vec3.Dot(e, direction);
            Vec3 wPerp = w - direction * Vec3.Dot(w, direction);
            double ee = Vec3.Dot(ePerp, ePerp);
            double s = ee > 0.0 ? Math.Max(0.0, Math.Min(1.0, -Vec3.Dot(wPerp, ePerp) / ee)) : 0.0;

            closest = a + e * s;
            return (wPerp + ePerp * s).Length;
        }

        private double ConicToLine(Vec3 origin, Vec3 direction, bool forwardOnly, out Vec3 onCurve)
        {
            double Lateral2(double t)
            {
                Vec3 w = PointAt(t) - origin;
                double along = Vec3.Dot(w, direction);

                if (forwardOnly && along < 0.0)
                {
                    return double.PositiveInfinity;
                }

                Vec3 lateral = w - direction * along;
                return Vec3.Dot(lateral, lateral);
            }

            int n = IsFull ? 128 : (int)Math.Max(16.0, Math.Ceiling(Math.Abs(Sweep) / TwoPi * 128.0));
            int count = IsFull ? n : n + 1;
            var values = new double[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = Lateral2(StartAngle + Sweep * i / n);
            }

            double best = double.PositiveInfinity;
            double bestT = StartAngle;

            for (int i = 0; i < count; i++)
            {
                int before = i - 1;
                int after = i + 1;

                if (IsFull)
                {
                    before = (i + count - 1) % count;
                    after = (i + 1) % count;
                }

                bool low = (before < 0 || values[i] <= values[before]) && (after >= count || values[i] <= values[after]);

                if (!low || double.IsPositiveInfinity(values[i]))
                {
                    continue;
                }

                double lo = StartAngle + Sweep * (IsFull || before >= 0 ? i - 1 : i) / n;
                double hi = StartAngle + Sweep * (IsFull || after < count ? i + 1 : i) / n;
                double t = Golden(Lateral2, lo, hi);
                double value = Lateral2(t);

                if (value > values[i])
                {
                    t = StartAngle + Sweep * i / n;
                    value = values[i];
                }

                if (value < best)
                {
                    best = value;
                    bestT = t;
                }
            }

            onCurve = PointAt(bestT);
            return Math.Sqrt(best);
        }

        private static double Golden(Func<double, double> f, double lo, double hi)
        {
            const double Ratio = 0.6180339887498949;
            double a = lo;
            double b = hi;
            double c = b - Ratio * (b - a);
            double d = a + Ratio * (b - a);
            double fc = f(c);
            double fd = f(d);

            for (int i = 0; i < 80; i++)
            {
                if (fc <= fd)
                {
                    b = d;
                    d = c;
                    fd = fc;
                    c = b - Ratio * (b - a);
                    fc = f(c);
                }
                else
                {
                    a = c;
                    c = d;
                    fc = fd;
                    d = a + Ratio * (b - a);
                    fd = f(d);
                }
            }

            return (a + b) * 0.5;
        }

        /// <summary>
        /// Gets the point of the curve nearest to a point.
        /// </summary>
        /// <param name="point">The point.</param>
        public Vec3 ClosestPointTo(Vec3 point)
        {
            if (Shape == CurveShape.Segment)
            {
                Vec3 e = _end - _start;
                double ee = Vec3.Dot(e, e);
                double s = ee > 0.0 ? Math.Max(0.0, Math.Min(1.0, Vec3.Dot(point - _start, e) / ee)) : 0.0;
                return _start + e * s;
            }

            if (IsCircular)
            {
                ToStartFrame(point, out double x, out double y, out _);
                x += RadiusU;

                if (x * x + y * y > 0.0)
                {
                    double t = StartAngle + Math.Atan2(y, x);

                    if (ContainsParameter(t, 0.0))
                    {
                        return PointAt(t);
                    }
                }
                else if (IsFull)
                {
                    return _start;
                }

                return (point - _start).Length <= (point - _end).Length ? _start : _end;
            }

            double Distance2(double t)
            {
                Vec3 w = PointAt(t) - point;
                return Vec3.Dot(w, w);
            }

            int n = IsFull ? 128 : (int)Math.Max(16.0, Math.Ceiling(Math.Abs(Sweep) / TwoPi * 128.0));
            int count = IsFull ? n : n + 1;
            double best = double.PositiveInfinity;
            double bestT = StartAngle;
            var values = new double[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = Distance2(StartAngle + Sweep * i / n);
            }

            for (int i = 0; i < count; i++)
            {
                int before = IsFull ? (i + count - 1) % count : i - 1;
                int after = IsFull ? (i + 1) % count : i + 1;
                bool low = (before < 0 || values[i] <= values[before]) && (after >= count || values[i] <= values[after]);

                if (!low)
                {
                    continue;
                }

                double lo = StartAngle + Sweep * (IsFull || before >= 0 ? i - 1 : i) / n;
                double hi = StartAngle + Sweep * (IsFull || after < count ? i + 1 : i) / n;
                double t = Golden(Distance2, lo, hi);
                double value = Distance2(t);

                if (value > values[i])
                {
                    t = StartAngle + Sweep * i / n;
                    value = values[i];
                }

                if (value < best)
                {
                    best = value;
                    bestT = t;
                }
            }

            return PointAt(bestT);
        }
    }
}
