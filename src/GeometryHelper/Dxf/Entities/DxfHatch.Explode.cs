using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfHatch
    {
        /// <summary>
        /// Gets the lines a HATCH filled with a pattern breaks into, as AutoCAD's EXPLODE breaks one: the dashes of each line of
        /// its pattern (<see cref="GetPatternDefinitionAt"/>), as the HATCH holds them, cut by its loops, each a LINE with the
        /// HATCH's look, a dot of the pattern a LINE of no length, as ezdxf's <c>hatch_entity</c> draws them. The lines of a pattern
        /// line run from its base point and every offset from it; each is cut where it crosses the loops, its stretches inside
        /// them kept by the even-odd rule, and its dashes laid from the line's own base point. The loops it is cut by are those
        /// the <see cref="HatchStyle"/> fills, by how deep each lies among the others as <see cref="Area"/> counts it: every
        /// loop in the normal style, the outermost and those just inside them in the outer style, the outermost alone ignoring
        /// islands. Lines, arcs, ellipses and splines of a loop are cut where they are crossed, an arc and an ellipse exactly, a
        /// spline to the bit within each sixteenth of a span of its knots; a dot that falls on a loop is kept. A square 30 by 20
        /// filled with ANSI31, the one line at 45 degrees 3.175 apart, gives 11 LINEs; the same round a circle of radius 5 about
        /// its middle in the normal style gives 14, the three lines that cross the circle broken in two. The parts run pattern
        /// line by pattern line, then by the index of the line from the base point, the least first, then along the line, each
        /// from its start along the angle of its pattern line.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown for a solid fill and a gradient, which the library does not break; and for
        /// a pattern the HATCH holds no lines of, AutoCAD's own pattern file being AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown, before anything is made, when a loop has an edge that is no shape;
        /// when the pattern would give more than the 2 000 000 entities of <see cref="DxfReadOptions.MaxPlacedEntities"/>, or its
        /// lines would cross the loops more than 32 000 000 times, which holds what the crossings take to half a gigabyte, the
        /// parts they give aside; or when the loops lie more than 2^52 of the pattern's
        /// spacings, or of the lengths of its dashes, from its base point, where one line or length is not told from the next.</exception>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            if (IsGradient)
            {
                throw new NotSupportedException("A gradient fill is not broken into other entities by the library: what AutoCAD's EXPLODE makes of one is not worked out.");
            }

            Parts record = Record();

            if (Whole(record.Text(70), 1) == 1)
            {
                throw new NotSupportedException("A solid fill is not broken into other entities by the library: what AutoCAD's EXPLODE makes of one is not worked out.");
            }

            List<double[]> definitions = Lines(record);

            if (definitions.Count == 0)
            {
                throw new NotSupportedException("The pattern " + PatternName + " of the HATCH holds no lines: they are in AutoCAD's own pattern file, which the library does not have.");
            }

            Snapshot snapshot = Snap();
            List<List<PatternCurve>> loops = Cutting(snapshot);
            int most = new DxfReadOptions().MaxPlacedEntities;
            var sets = new List<Stretches>();
            long count = 0;

            foreach (double[] definition in definitions)
            {
                var stretches = new Stretches(definition);
                stretches.Cut(loops, most - count);
                count += stretches.Dashes();

                if (count > most)
                {
                    throw new InvalidOperationException("The pattern of the HATCH " + (Handle ?? string.Empty) + " would give more than " + most.ToString(CultureInfo.InvariantCulture) + " entities, the bound of DxfReadOptions.MaxPlacedEntities.");
                }

                sets.Add(stretches);
            }

            var look = new Look(this);
            GeoVector3 normal = snapshot.Normal;
            double elevation = snapshot.Elevation;
            var parts = new List<DxfEntity>((int)count);

            foreach (Stretches stretches in sets)
            {
                stretches.Lay((a, b) =>
                {
                    var line = new DxfLine(OcsToWorld(normal, new GeoPoint3(a.X, a.Y, elevation)), OcsToWorld(normal, new GeoPoint3(b.X, b.Y, elevation)));
                    look.GiveTo(line);
                    parts.Add(line);
                });
            }

            return parts.AsReadOnly();
        }

        /// <summary>
        /// The loops the pattern is cut by, as the <see cref="HatchStyle"/> fills them, each the curves it runs along in the plane
        /// of the HATCH, the end of each on the start of the next where they meet within <see cref="Tolerance.Default"/>, so
        /// that a pattern line through a corner crosses the loop there once or not at all.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a loop has an edge that is no shape.</exception>
        private List<List<PatternCurve>> Cutting(Snapshot snapshot)
        {
            DxfHatchStyle style = HatchStyle;
            double tolerance = Tolerance.Default.EqualPoint;
            var loops = new List<List<PatternCurve>>();

            if (style != DxfHatchStyle.Normal)
            {
                Measured(snapshot);
            }

            for (int i = 0; i < snapshot.Loops.Count; i++)
            {
                int depth = style == DxfHatchStyle.Normal ? 0 : Depth(snapshot, i, tolerance);

                if (depth > (style == DxfHatchStyle.Outer ? 1 : 0))
                {
                    continue;
                }

                LoopData loop = LoopData.Read(snapshot.Loops[i]);
                var curves = new List<PatternCurve>();

                try
                {
                    if (loop.IsPolyline)
                    {
                        Bulged(loop, curves);
                    }
                    else
                    {
                        foreach (Edge edge in loop.Edges)
                        {
                            curves.Add(edge.Curve());
                        }
                    }
                }
                catch (ArgumentException e)
                {
                    throw new InvalidOperationException("The loop " + i.ToString(CultureInfo.InvariantCulture) + " of the HATCH has an edge that is no shape: " + e.Message, e);
                }

                for (int c = 0; c < curves.Count; c++)
                {
                    PatternCurve next = curves[(c + 1) % curves.Count];

                    if (curves[c].End.DistanceTo(next.Start) <= tolerance)
                    {
                        curves[c].End = next.Start;
                    }
                }

                loops.Add(curves);
            }

            return loops;
        }

        /// <summary>The segments of a loop of a polyline: a line or the arc of its bulge from each vertex to the next, and back to the first, closed or not.</summary>
        private static void Bulged(LoopData loop, List<PatternCurve> curves)
        {
            int n = loop.Vertices.Count;

            for (int i = 0; i < n && n > 1; i++)
            {
                double[] a = loop.Vertices[i];
                double[] b = loop.Vertices[(i + 1) % n];
                var start = new GeoPoint2(a[0], a[1]);
                var end = new GeoPoint2(b[0], b[1]);
                double angle = 4.0 * Math.Atan(a[2]);
                double dx = b[0] - a[0];
                double dy = b[1] - a[1];
                double chord = Math.Sqrt(dx * dx + dy * dy);
                double sine = Math.Sin(angle * 0.5);

                if (a[2] == 0.0 || (!loop.Closed && i == n - 1) || chord == 0.0 || sine == 0.0)
                {
                    curves.Add(PatternCurve.Line(start, end));
                    continue;
                }

                double radius = chord / (2.0 * sine);
                double toCentre = radius * Math.Cos(angle * 0.5);
                double cx = (a[0] + b[0]) * 0.5 - dy / chord * toCentre;
                double cy = (a[1] + b[1]) * 0.5 + dx / chord * toCentre;
                double from = Math.Atan2(a[1] - cy, a[0] - cx);
                double r = Math.Abs(radius);
                curves.Add(PatternCurve.Conic(start, end, cx, cy, r, 0.0, 0.0, r, from, from + angle));
            }
        }

        /// <summary>
        /// A curve of a loop as a pattern cuts it, in the plane of the HATCH, from its start to its end as it runs: a line; a
        /// conic, an arc or an ellipse, its point at a parameter t the centre and cos t of one axis and sin t of the other; or a
        /// spline.
        /// </summary>
        private sealed class PatternCurve
        {
            private double _cx;
            private double _cy;
            private double _ux;
            private double _uy;
            private double _vx;
            private double _vy;
            private GeoSpline2 _spline;

            private PatternCurve(GeoPoint2 start, GeoPoint2 end)
            {
                Start = start;
                End = end;
            }

            /// <summary>Where it starts, as it runs.</summary>
            internal GeoPoint2 Start { get; }

            /// <summary>Where it ends, as it runs: the start of the next curve of its loop where they meet.</summary>
            internal GeoPoint2 End { get; set; }

            /// <summary>Whether it is a conic.</summary>
            internal bool IsConic { get; private set; }

            /// <summary>Whether it is a spline.</summary>
            internal bool IsSpline => _spline != null;

            /// <summary>The parameter it starts at: 0 for a line.</summary>
            internal double From { get; private set; }

            /// <summary>The parameter it ends at: 1 for a line.</summary>
            internal double To { get; private set; } = 1.0;

            internal static PatternCurve Line(GeoPoint2 start, GeoPoint2 end) => new PatternCurve(start, end);

            internal static PatternCurve Conic(GeoPoint2 start, GeoPoint2 end, double cx, double cy, double ux, double uy, double vx, double vy, double from, double to)
                => new PatternCurve(start, end) { IsConic = true, _cx = cx, _cy = cy, _ux = ux, _uy = uy, _vx = vx, _vy = vy, From = from, To = to };

            internal static PatternCurve Of(GeoPoint2 start, GeoPoint2 end, GeoSpline2 spline)
                => new PatternCurve(start, end) { _spline = spline, From = spline.DomainStart, To = spline.DomainEnd };

            /// <summary>Its point at a parameter.</summary>
            internal GeoPoint2 At(double t)
            {
                if (IsConic)
                {
                    double c = Math.Cos(t);
                    double s = Math.Sin(t);
                    return new GeoPoint2(_cx + c * _ux + s * _vx, _cy + c * _uy + s * _vy);
                }

                if (IsSpline)
                {
                    return _spline.GetPointAtKnot(t);
                }

                return new GeoPoint2(Start.X + (End.X - Start.X) * t, Start.Y + (End.Y - Start.Y) * t);
            }

            /// <summary>
            /// The parameters it is cut at so that each piece between two runs one way across the lines of a direction: a line
            /// whole; a conic where it turns back across them, square to them; a spline at sixteen even steps of each span of its
            /// knots. From its start to its end as it runs.
            /// </summary>
            internal List<double> Breaks(double dx, double dy)
            {
                var breaks = new List<double> { From };

                if (IsConic)
                {
                    // Across the lines the curve runs as dx (y - cy) - dy (x - cx), a cos t + b sin t: it turns back where t is the
                    // angle of (a, b), and half a turn on.
                    double a = dx * _uy - dy * _ux;
                    double b = dx * _vy - dy * _vx;
                    double turn = Math.Atan2(b, a);
                    double low = Math.Min(From, To);
                    double high = Math.Max(From, To);
                    var inside = new List<double>();

                    // A conic runs a turn at most, its parameters within a few turns of nought: it turns back three times at most.
                    double t = turn + Math.Ceiling((low - turn) / Math.PI) * Math.PI;

                    for (int step = 0; step < 4; step++, t += Math.PI)
                    {
                        if (!(t < high))
                        {
                            break;
                        }

                        if (t > low)
                        {
                            inside.Add(t);
                        }
                    }

                    if (To < From)
                    {
                        inside.Reverse();
                    }

                    breaks.AddRange(inside);
                }
                else if (IsSpline)
                {
                    IReadOnlyList<double> knots = _spline.Knots;

                    for (int k = 0; k + 1 < knots.Count; k++)
                    {
                        double a = Math.Max(knots[k], From);
                        double b = Math.Min(knots[k + 1], To);

                        for (int step = 1; b > a && step <= 16; step++)
                        {
                            double t = step == 16 ? b : a + (b - a) * step / 16.0;

                            if (t > breaks[breaks.Count - 1] && t < To)
                            {
                                breaks.Add(t);
                            }
                        }
                    }
                }

                breaks.Add(To);
                return breaks;
            }
        }

        /// <summary>
        /// The lines of one pattern line cut by the loops: the stretches of each that lie inside them, by the index of the line
        /// from the base point, and the dashes laid along them from the line's own base point.
        /// </summary>
        private sealed class Stretches
        {
            // 2^52: past it a whole number of lines or of lengths of the pattern and the next are no longer two numbers apart.
            private const double Farthest = 4503599627370496.0;

            // The most crossings of the lines with the loops worked out, half a gigabyte of them.
            private const double MostCrossings = 32000000.0;

            private readonly double _dx;
            private readonly double _dy;
            private readonly double _bx;
            private readonly double _by;
            private readonly double _ox;
            private readonly double _oy;
            private readonly double _apart;
            private readonly double[] _dashes;
            private readonly double _length;
            private readonly List<long> _lines = new List<long>();
            private readonly List<double> _from = new List<double>();
            private readonly List<double> _to = new List<double>();
            private long _parts;

            /// <summary>A pattern line as the HATCH holds it: its angle in degrees, its base point, its offset and its dashes.</summary>
            internal Stretches(double[] definition)
            {
                double angle = definition[0] * (Math.PI / 180.0);
                _dx = Math.Cos(angle);
                _dy = Math.Sin(angle);
                _bx = definition[1];
                _by = definition[2];
                _ox = definition[3];
                _oy = definition[4];
                _apart = _dx * _oy - _dy * _ox;
                _dashes = new double[definition.Length - 5];
                Array.Copy(definition, 5, _dashes, 0, _dashes.Length);

                foreach (double dash in _dashes)
                {
                    _length += Math.Abs(dash);
                }
            }

            /// <summary>
            /// Cuts the lines by the loops: each piece of a curve crosses the lines whose index lies in [the least of the indices at
            /// its two ends, the greatest), whichever way it runs, so that a line through the point two pieces share crosses one of
            /// them; the crossings of each line, by their distance along it from its base point, paired off from the first, two
            /// that meet joined; the parts of each stretch counted, one that gives none not kept.
            /// </summary>
            /// <exception cref="InvalidOperationException">Thrown, before they are worked out, when the crossings would be more than
            /// 32 000 000, which holds them to half a gigabyte, or a line's index is past 2^52, where the next index is no other
            /// number; or when the parts counted pass the bound given.</exception>
            internal void Cut(List<List<PatternCurve>> loops, long most)
            {
                // Lines on one another, a pattern line whose offset runs along it, are no lines apart: nothing to cut.
                if (!(Math.Abs(_apart) > 1E-12))
                {
                    return;
                }

                var pieces = new List<Piece>();
                double crossings = 0.0;

                foreach (List<PatternCurve> loop in loops)
                {
                    foreach (PatternCurve curve in loop)
                    {
                        List<double> breaks = curve.Breaks(_dx, _dy);

                        for (int i = 0; i + 1 < breaks.Count; i++)
                        {
                            GeoPoint2 a = i == 0 ? curve.Start : curve.At(breaks[i]);
                            GeoPoint2 b = i + 2 == breaks.Count ? curve.End : curve.At(breaks[i + 1]);
                            var piece = new Piece(curve, breaks[i], breaks[i + 1], a, b, Index(a), Index(b));

                            if (!(Math.Abs(piece.Ua) <= Farthest) || !(Math.Abs(piece.Ub) <= Farthest))
                            {
                                throw new InvalidOperationException("The loops of the HATCH lie more than 2^52 of its pattern's spacings from the pattern's base point, where one line cannot be told from the next.");
                            }

                            crossings += Math.Ceiling(Math.Max(piece.Ua, piece.Ub)) - Math.Ceiling(Math.Min(piece.Ua, piece.Ub));
                            pieces.Add(piece);
                        }
                    }
                }

                // A bound of memory, not of the parts: each crossing takes 16 bytes, so 32 000 000 of them half a gigabyte.
                if (crossings > MostCrossings)
                {
                    throw new InvalidOperationException("The pattern of the HATCH would cross its loops more than " + MostCrossings.ToString("0", CultureInfo.InvariantCulture) + " times, which the library does not hold in memory.");
                }

                var found = new List<KeyValuePair<long, double>>((int)crossings);

                foreach (Piece piece in pieces)
                {
                    long end = (long)Math.Ceiling(Math.Max(piece.Ua, piece.Ub));

                    for (long k = (long)Math.Ceiling(Math.Min(piece.Ua, piece.Ub)); k < end; k++)
                    {
                        GeoPoint2 at = Crossing(piece, k);
                        double along = _dx * (at.X - (_bx + k * _ox)) + _dy * (at.Y - (_by + k * _oy));
                        found.Add(new KeyValuePair<long, double>(k, along));
                    }
                }

                found.Sort((p, q) => p.Key != q.Key ? p.Key.CompareTo(q.Key) : p.Value.CompareTo(q.Value));
                double tolerance = Tolerance.Default.EqualPoint;

                for (int i = 0; i < found.Count;)
                {
                    int j = i;

                    while (j < found.Count && found[j].Key == found[i].Key)
                    {
                        j++;
                    }

                    // From the first crossing in, the second out; a loop left open leaves its last crossing alone.
                    int first = _from.Count;

                    for (int c = i; c + 1 < j; c += 2)
                    {
                        int last = _to.Count - 1;

                        if (last >= first && found[c].Value - _to[last] <= tolerance)
                        {
                            _to[last] = found[c + 1].Value;
                        }
                        else if (found[c + 1].Value > found[c].Value)
                        {
                            _lines.Add(found[i].Key);
                            _from.Add(found[c].Value);
                            _to.Add(found[c + 1].Value);
                        }
                    }

                    // The parts of the line's stretches counted, a stretch that gives none, shorter than the gaps it falls in,
                    // not kept: those kept give a part at least, so that past the bound no more are kept.
                    int keep = first;

                    for (int t = first; t < _from.Count; t++)
                    {
                        long count = Count(_from[t], _to[t], most - _parts);

                        if (count > 0)
                        {
                            _lines[keep] = _lines[t];
                            _from[keep] = _from[t];
                            _to[keep] = _to[t];
                            keep++;
                            _parts += count;
                        }

                        if (_parts > most)
                        {
                            throw new InvalidOperationException("The pattern of the HATCH would give more than " + new DxfReadOptions().MaxPlacedEntities.ToString(CultureInfo.InvariantCulture) + " entities, the bound of DxfReadOptions.MaxPlacedEntities.");
                        }
                    }

                    _lines.RemoveRange(keep, _lines.Count - keep);
                    _from.RemoveRange(keep, _from.Count - keep);
                    _to.RemoveRange(keep, _to.Count - keep);
                    i = j;
                }
            }

            /// <summary>How many parts the stretches kept give, as <see cref="Lay"/> lays them.</summary>
            internal long Dashes() => _parts;

            /// <summary>
            /// How many parts a stretch gives, as <see cref="Lay"/> lays them: one for a line with no dashes, else each dash and
            /// dot laid along it; counted no further than past a bound.
            /// </summary>
            /// <exception cref="InvalidOperationException">Thrown when the stretch reaches past 2^52 lengths of the pattern from the
            /// line's base point, where one length cannot be told from the next.</exception>
            private long Count(double from, double to, long most)
            {
                if (!(_length > 1E-12))
                {
                    return 1;
                }

                // A whole length of the pattern inside a stretch gives each dash and each dot of it.
                long whole = 0;

                foreach (double dash in _dashes)
                {
                    whole += dash >= 0.0 ? 1 : 0;
                }

                double first = Periods(from, to, out double last);
                long count = Period(first, from, to, null);

                if (last > first)
                {
                    count += Period(last, from, to, null);
                    double between = (last - first - 1.0) * whole;
                    count += between > most ? most + 1L : (long)between;
                }

                return count;
            }

            /// <summary>Lays the dashes along each stretch in its order, each from its start to its end in the plane of the HATCH; a dot from a point to itself.</summary>
            internal void Lay(Action<GeoPoint2, GeoPoint2> part)
            {
                for (int s = 0; s < _from.Count; s++)
                {
                    long k = _lines[s];
                    double ox = _bx + k * _ox;
                    double oy = _by + k * _oy;
                    double from = _from[s];
                    double to = _to[s];

                    GeoPoint2 Along(double t) => new GeoPoint2(ox + _dx * t, oy + _dy * t);

                    if (!(_length > 1E-12))
                    {
                        part(Along(from), Along(to));
                        continue;
                    }

                    long first = (long)Periods(from, to, out double end);

                    for (long m = first; m <= (long)end; m++)
                    {
                        Period(m, from, to, (a, b) => part(Along(a), Along(b)));
                    }
                }
            }

            /// <summary>The first and the last length of the pattern a stretch reaches, by their number from the line's base point.</summary>
            /// <exception cref="InvalidOperationException">Thrown when either is past 2^52.</exception>
            private double Periods(double from, double to, out double last)
            {
                double first = Math.Floor(from / _length);
                last = Math.Floor(to / _length);

                if (!(Math.Abs(first) <= Farthest) || !(Math.Abs(last) <= Farthest))
                {
                    throw new InvalidOperationException("A line of the HATCH's pattern runs more than 2^52 lengths of its dashes from its base point, where one length cannot be told from the next.");
                }

                return first;
            }

            /// <summary>The dashes and dots of one length of the pattern, the m-th from the line's base point, within a stretch: how many, each given where asked from its start to its end along the line.</summary>
            private long Period(double m, double from, double to, Action<double, double> part)
            {
                long count = 0;
                double at = m * _length;

                foreach (double dash in _dashes)
                {
                    if (dash > 0.0)
                    {
                        double a = Math.Max(at, from);
                        double b = Math.Min(at + dash, to);

                        if (b > a)
                        {
                            count++;
                            part?.Invoke(a, b);
                        }
                    }
                    else if (dash == 0.0 && at >= from && at <= to)
                    {
                        count++;
                        part?.Invoke(at, at);
                    }

                    at += Math.Abs(dash);
                }

                return count;
            }

            /// <summary>The index of the line through a point, from the base point by the offset: a whole number on a line.</summary>
            private double Index(GeoPoint2 p) => (_dx * (p.Y - _by) - _dy * (p.X - _bx)) / _apart;

            /// <summary>Where a piece crosses the line of an index: along a line in proportion, along a curve by halving its parameters.</summary>
            private GeoPoint2 Crossing(Piece piece, double k)
            {
                if (piece.Ua == k)
                {
                    return piece.A;
                }

                if (piece.Ub == k)
                {
                    return piece.B;
                }

                if (!piece.Curve.IsConic && !piece.Curve.IsSpline)
                {
                    double f = (k - piece.Ua) / (piece.Ub - piece.Ua);
                    return new GeoPoint2(piece.A.X + (piece.B.X - piece.A.X) * f, piece.A.Y + (piece.B.Y - piece.A.Y) * f);
                }

                double lo = piece.Ta;
                double hi = piece.Tb;
                bool below = piece.Ua < k;

                for (int i = 0; i < 100; i++)
                {
                    double mid = 0.5 * (lo + hi);

                    if (mid == lo || mid == hi)
                    {
                        break;
                    }

                    if ((Index(piece.Curve.At(mid)) < k) == below)
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid;
                    }
                }

                return piece.Curve.At(0.5 * (lo + hi));
            }

            /// <summary>A piece of a curve that runs one way across the lines: its parameters, its ends and the indices of the lines through them.</summary>
            private sealed class Piece
            {
                internal Piece(PatternCurve curve, double ta, double tb, GeoPoint2 a, GeoPoint2 b, double ua, double ub)
                {
                    Curve = curve;
                    Ta = ta;
                    Tb = tb;
                    A = a;
                    B = b;
                    Ua = ua;
                    Ub = ub;
                }

                internal PatternCurve Curve { get; }

                internal double Ta { get; }

                internal double Tb { get; }

                internal GeoPoint2 A { get; }

                internal GeoPoint2 B { get; }

                internal double Ua { get; }

                internal double Ub { get; }
            }
        }
    }
}
