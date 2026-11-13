using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The union, difference and intersection of planar bodies (regions in one plane), keeping arcs and ellipses as arcs and
    /// ellipses where it can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library's booleans (<see cref="Boolean2"/>) know straight edges only. Each curve is cut into chords first, a circle
    /// on a grid of angles that depends on its radius alone, so that two regions sharing an arc of one circle cut it alike and
    /// leave no sliver between them; the booleans are run on the chords; then every edge of the answer is traced back to the
    /// chord, and so the curve, it lies on. A run of edges on one curve is one line, arc or ellipse again, its ends the exact
    /// meeting of the two curves it lies between (found by Newton's method from the point the booleans gave) or the end of an
    /// input curve where it is within the point tolerance of one. A whole circle or ellipse that no cut touched stays one edge.
    /// </para>
    /// <para>
    /// An edge that lies on no chord (the booleans moved a point further than the tolerance) is kept straight.
    /// </para>
    /// </remarks>
    internal static class AcisBoolean
    {
        /// <summary>The share of the point tolerance a chord may stray from its arc (a quarter of it).</summary>
        private const double ChordShare = 0.25;

        /// <summary>The frame of the plane the bodies lie in: an origin, two axes and the normal.</summary>
        private sealed class Frame
        {
            internal Frame(AcisVec origin, AcisVec normal)
            {
                Origin = origin;
                Normal = normal;
                X = AcisBody.ArbitraryX(normal);
                Y = normal.Cross(X);
            }

            internal AcisVec Origin { get; }

            internal AcisVec Normal { get; }

            internal AcisVec X { get; }

            internal AcisVec Y { get; }

            internal GeoPoint2 To2(AcisVec p)
            {
                AcisVec d = p - Origin;
                return new GeoPoint2(d.Dot(X), d.Dot(Y));
            }

            internal AcisVec To3(GeoPoint2 p) => Origin + X * p.X + Y * p.Y;
        }

        /// <summary>A straight piece of an input curve: the two points and the curve it lies on.</summary>
        private readonly struct Chord
        {
            internal Chord(GeoPoint2 a, GeoPoint2 b, int curve)
            {
                A = a;
                B = b;
                Curve = curve;
            }

            internal GeoPoint2 A { get; }

            internal GeoPoint2 B { get; }

            internal int Curve { get; }
        }

        /// <summary>Everything the pieces of the answer are traced back to: the input curves, their chords and an index over the chords.</summary>
        private sealed class Sources
        {
            internal readonly List<AcisCurve> Curves = new List<AcisCurve>();
            internal readonly List<Chord> Chords = new List<Chord>();
            internal readonly List<AcisVec> Ends = new List<AcisVec>();
            private Dictionary<long, List<int>> _cells;
            private List<int> _long;
            private double _cell;
            private int[] _stamp;
            private int _stamped;

            /// <summary>Builds the index over the chords: a grid of cells a few chords wide; a chord crossing more than 64 cells goes to a list every query reads.</summary>
            internal void Index(double tolerance)
            {
                double sum = 0.0;

                foreach (Chord chord in Chords)
                {
                    sum += Math.Max(Math.Abs(chord.A.X - chord.B.X), Math.Abs(chord.A.Y - chord.B.Y));
                }

                _cell = Math.Max(Chords.Count == 0 ? 1.0 : 4.0 * sum / Chords.Count, 20.0 * tolerance);
                _cells = new Dictionary<long, List<int>>(PackedKeys.Instance);
                _long = new List<int>();
                _stamp = new int[Chords.Count];

                for (int i = 0; i < Chords.Count; i++)
                {
                    Chord chord = Chords[i];
                    long x0 = Cell(Math.Min(chord.A.X, chord.B.X) - tolerance);
                    long x1 = Cell(Math.Max(chord.A.X, chord.B.X) + tolerance);
                    long y0 = Cell(Math.Min(chord.A.Y, chord.B.Y) - tolerance);
                    long y1 = Cell(Math.Max(chord.A.Y, chord.B.Y) + tolerance);

                    if ((x1 - x0 + 1) * (y1 - y0 + 1) > 64)
                    {
                        _long.Add(i);
                        continue;
                    }

                    for (long x = x0; x <= x1; x++)
                    {
                        for (long y = y0; y <= y1; y++)
                        {
                            long key = Key(x, y);

                            if (!_cells.TryGetValue(key, out List<int> list))
                            {
                                _cells.Add(key, list = new List<int>());
                            }

                            list.Add(i);
                        }
                    }
                }
            }

            private long Cell(double v) => (long)Math.Floor(v / _cell);

            private static long Key(long x, long y) => (x << 32) ^ (y & 0xFFFFFFFFL);

            /// <summary>The chord an edge lies on: the one whose farther end is nearest it, within the tolerance; -1 where none.</summary>
            internal int Find(GeoPoint2 u, GeoPoint2 v, double tolerance, out bool forward)
            {
                _stamped++;
                int best = -1;
                double bestScore = tolerance;
                bool along = true;
                long x0 = Cell(Math.Min(u.X, v.X) - tolerance);
                long x1 = Cell(Math.Max(u.X, v.X) + tolerance);
                long y0 = Cell(Math.Min(u.Y, v.Y) - tolerance);
                long y1 = Cell(Math.Max(u.Y, v.Y) + tolerance);

                void Try(int i)
                {
                    if (_stamp[i] == _stamped)
                    {
                        return;
                    }

                    _stamp[i] = _stamped;
                    Chord chord = Chords[i];
                    double score = Math.Max(Distance(u, chord), Distance(v, chord));

                    if (score < bestScore || (score == bestScore && best >= 0 && i < best))
                    {
                        best = i;
                        bestScore = score;
                        along = (v.X - u.X) * (chord.B.X - chord.A.X) + (v.Y - u.Y) * (chord.B.Y - chord.A.Y) >= 0.0;
                    }
                }

                foreach (int i in _long)
                {
                    Try(i);
                }

                for (long x = x0; x <= x1; x++)
                {
                    for (long y = y0; y <= y1; y++)
                    {
                        if (_cells.TryGetValue(Key(x, y), out List<int> list))
                        {
                            foreach (int i in list)
                            {
                                Try(i);
                            }
                        }
                    }
                }

                forward = along;
                return best;
            }

            private static double Distance(GeoPoint2 p, Chord chord)
            {
                double dx = chord.B.X - chord.A.X;
                double dy = chord.B.Y - chord.A.Y;
                double length2 = dx * dx + dy * dy;
                double t = length2 <= 0.0 ? 0.0 : ((p.X - chord.A.X) * dx + (p.Y - chord.A.Y) * dy) / length2;
                t = Math.Max(0.0, Math.Min(1.0, t));
                double ex = chord.A.X + t * dx - p.X;
                double ey = chord.A.Y + t * dy - p.Y;
                return Math.Sqrt(ex * ex + ey * ey);
            }
        }

        /// <summary>The union of bodies in one plane; null where nothing is left.</summary>
        /// <exception cref="ArgumentException">Thrown when the bodies do not lie in one plane.</exception>
        internal static AcisBody Unite(IReadOnlyList<AcisBody> bodies, Tolerance tolerance) =>
            Run(bodies, Array.Empty<AcisBody>(), Kind.Union, tolerance);

        /// <summary>The bodies less the tools, the bodies taken as their union first; null where nothing is left.</summary>
        /// <exception cref="ArgumentException">Thrown when the bodies and tools do not lie in one plane.</exception>
        internal static AcisBody Subtract(
            IReadOnlyList<AcisBody> bodies,
            IReadOnlyList<AcisBody> tools,
            Tolerance tolerance
        ) => Run(bodies, tools, Kind.Subtract, tolerance);

        /// <summary>What every one of the bodies covers; null where nothing is.</summary>
        /// <exception cref="ArgumentException">Thrown when the bodies do not lie in one plane.</exception>
        internal static AcisBody Intersect(IReadOnlyList<AcisBody> bodies, Tolerance tolerance) =>
            Run(bodies, Array.Empty<AcisBody>(), Kind.Intersect, tolerance);

        private enum Kind
        {
            Union,
            Subtract,
            Intersect,
        }

        private static AcisBody Run(
            IReadOnlyList<AcisBody> bodies,
            IReadOnlyList<AcisBody> tools,
            Kind kind,
            Tolerance tolerance
        )
        {
            AcisFace first = bodies.SelectMany(b => b.Faces).First();
            var frame = new Frame(first.Origin, first.Normal);
            double point = tolerance.EqualPoint;

            foreach (AcisBody body in bodies.Concat(tools))
            {
                if (!Lies(body, frame, point))
                {
                    throw new ArgumentException("The regions do not lie in one plane.");
                }
            }

            var sources = new Sources();
            var operands = bodies.Select(b => Faces(b, frame, sources, point)).ToList();
            var toolFaces = tools.Select(b => Faces(b, frame, sources, point)).ToList();
            sources.Index(point);
            GeoFace2[] answer;

            switch (kind)
            {
                case Kind.Union:
                    answer = Boolean2.Union(operands.SelectMany(f => f), tolerance);
                    break;
                case Kind.Subtract:
                    GeoFace2[] held = Boolean2.Union(operands.SelectMany(f => f), tolerance);
                    GeoFace2[] gone = Boolean2.Union(toolFaces.SelectMany(f => f), tolerance);
                    var left = new List<GeoFace2>();

                    foreach (GeoFace2 face in held)
                    {
                        left.AddRange(gone.Length == 0 ? new[] { face } : Boolean2.Subtract(face, gone, tolerance));
                    }

                    answer = left.ToArray();
                    break;
                default:
                    GeoFace2[] kept = Boolean2.Union(operands[0], tolerance);

                    for (int i = 1; i < operands.Count && kept.Length > 0; i++)
                    {
                        GeoFace2[] other = Boolean2.Union(operands[i], tolerance);
                        var common = new List<GeoFace2>();

                        foreach (GeoFace2 a in kept)
                        {
                            foreach (GeoFace2 b in other)
                            {
                                common.AddRange(Boolean2.Intersect(a, b, tolerance));
                            }
                        }

                        kept = Boolean2.Union(common, tolerance);
                    }

                    answer = kept;
                    break;
            }

            return Build(answer, frame, sources, tolerance);
        }

        /// <summary>Whether every edge of a body lies in the plane of the first face of another, within a tolerance.</summary>
        internal static bool Coplanar(AcisBody body, AcisBody other, double tolerance)
        {
            AcisFace face = other.Faces[0];
            return Lies(body, new Frame(face.Origin, face.Normal), tolerance);
        }

        private static bool Lies(AcisBody body, Frame frame, double tolerance)
        {
            foreach (AcisFace face in body.Faces)
            {
                foreach (AcisLoop loop in face.Loops)
                {
                    foreach (AcisCurve curve in loop.Curves)
                    {
                        if (!InPlane(curve, frame, tolerance))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool InPlane(AcisCurve curve, Frame frame, double tolerance)
        {
            bool flat =
                Math.Abs((curve.Start - frame.Origin).Dot(frame.Normal)) <= tolerance
                && Math.Abs((curve.End - frame.Origin).Dot(frame.Normal)) <= tolerance;

            if (!curve.IsLine)
            {
                flat =
                    flat
                    && Math.Abs((curve.Center - frame.Origin).Dot(frame.Normal)) <= tolerance
                    && curve.Normal.Cross(frame.Normal).Length <= 1E-6;
            }

            return flat;
        }

        #region Cutting the curves into chords

        /// <summary>The faces of a body as faces of polygons in the plane, the chords of every curve noted in <paramref name="sources"/>.</summary>
        private static List<GeoFace2> Faces(AcisBody body, Frame frame, Sources sources, double tolerance)
        {
            var faces = new List<GeoFace2>();

            foreach (AcisFace face in body.Faces)
            {
                GeoPolygon2 outer = null;
                var holes = new List<GeoPolygon2>();

                foreach (AcisLoop loop in face.Loops)
                {
                    GeoPolygon2 polygon = Flatten(loop, frame, sources, tolerance);

                    if (loop.IsHole)
                    {
                        holes.Add(polygon);
                    }
                    else
                    {
                        outer = polygon;
                    }
                }

                if (outer != null)
                {
                    faces.Add(new GeoFace2(outer, holes));
                }
            }

            return faces;
        }

        private static GeoPolygon2 Flatten(AcisLoop loop, Frame frame, Sources sources, double tolerance)
        {
            var points = new List<GeoPoint2>();

            foreach (AcisCurve curve in loop.Curves)
            {
                int id = sources.Curves.Count;
                sources.Curves.Add(curve);
                sources.Ends.Add(curve.Start);
                sources.Ends.Add(curve.End);
                List<GeoPoint2> cut = Cut(curve, frame, tolerance * ChordShare);

                for (int i = 0; i + 1 < cut.Count; i++)
                {
                    sources.Chords.Add(new Chord(cut[i], cut[i + 1], id));
                }

                points.AddRange(cut.Take(cut.Count - 1));
            }

            return new GeoPolygon2(points);
        }

        /// <summary>
        /// The points a curve is cut at, both its ends among them: a line has none between; a circle's lie on a grid of angles about
        /// its centre counted from the plane's x axis, as many as its radius asks of a stray of <paramref name="sagitta"/> (a
        /// multiple of four, so that the quarter points are cut at); an ellipse's on a grid of its own parameter.
        /// </summary>
        private static List<GeoPoint2> Cut(AcisCurve curve, Frame frame, double sagitta)
        {
            var cut = new List<GeoPoint2> { frame.To2(curve.Start) };

            if (!curve.IsLine)
            {
                bool circle = Math.Abs(curve.Ratio - 1.0) < 1E-12;
                double radius = circle ? curve.Major.Length : curve.Major.Length * curve.Ratio * curve.Ratio;
                int steps = Steps(radius, sagitta);
                double step = 2.0 * Math.PI / steps;

                // The circle's parameter is the angle of its point from its major axis, turning
                // as its normal does, so the grid of angles about the plane's x axis is a grid of parameters.
                double offset = 0.0;
                double sign = 1.0;

                if (circle)
                {
                    AcisVec major = curve.Major;
                    offset = Math.Atan2(major.Dot(frame.Y), major.Dot(frame.X));
                    sign = curve.Normal.Dot(frame.Normal) >= 0.0 ? 1.0 : -1.0;
                }

                // The grid in the frame's angles (a circle) or in the parameter itself (an ellipse), the numbers of its lines
                // between the two ends, and the parameter each stands for.
                double low = circle ? offset + Math.Min(sign * curve.T0, sign * curve.T1) : curve.T0;
                double high = circle ? offset + Math.Max(sign * curve.T0, sign * curve.T1) : curve.T1;
                var inside = new List<double>();

                for (int k = (int)Math.Ceiling(low / step); k <= (int)Math.Floor(high / step); k++)
                {
                    double t = circle ? sign * (k * step - offset) : k * step;

                    if (t > curve.T0 + 1E-9 && t < curve.T1 - 1E-9)
                    {
                        inside.Add(t);
                    }
                }

                inside.Sort();

                foreach (double t in inside)
                {
                    cut.Add(frame.To2(curve.At(t)));
                }
            }

            cut.Add(frame.To2(curve.End));
            return cut;
        }

        /// <summary>How many chords a whole turn of radius <paramref name="radius"/> is cut into to stray no further than <paramref name="sagitta"/>: a multiple of four, 16 at least.</summary>
        private static int Steps(double radius, double sagitta)
        {
            double ratio = Math.Min(1.0, Math.Max(1E-9, sagitta / Math.Max(radius, 1E-12)));
            double half = Math.Acos(1.0 - ratio);
            int steps = (int)Math.Ceiling(Math.PI / Math.Max(half, 1E-6));
            steps = Math.Max(16, Math.Min(steps, 1 << 18));
            return (steps + 3) / 4 * 4;
        }

        #endregion

        #region Tracing the answer back to the curves

        /// <summary>One edge of a loop of the answer: where it runs from and to (the loop's points), the curve it lies on (-1 for none) and whether it runs with that chord.</summary>
        private struct Piece
        {
            internal int Curve;
            internal bool Forward;
        }

        private static AcisBody Build(GeoFace2[] answer, Frame frame, Sources sources, Tolerance tolerance)
        {
            var built = new List<KeyValuePair<GeoPoint2, AcisFace>>();

            foreach (GeoFace2 face in answer)
            {
                IReadOnlyList<AcisCurve> outer = Trace(face.Boundary.Vertices, false, frame, sources, tolerance);
                var holes = new List<KeyValuePair<GeoPoint2, IReadOnlyList<AcisCurve>>>();

                foreach (GeoPolygon2 hole in face.Holes)
                {
                    IReadOnlyList<AcisCurve> chain = Trace(hole.Vertices, true, frame, sources, tolerance);

                    if (chain.Count > 0)
                    {
                        holes.Add(new KeyValuePair<GeoPoint2, IReadOnlyList<AcisCurve>>(Corner(hole.Vertices), chain));
                    }
                }

                if (outer.Count == 0)
                {
                    continue;
                }

                // The holes in the order AutoCAD lists them, the latest cut first: from the right, then from the top (a stable
                // sort, so that two on one corner keep the order the booleans gave them).
                IReadOnlyList<IReadOnlyList<AcisCurve>> ordered = holes
                    .OrderByDescending(h => h.Key.X)
                    .ThenByDescending(h => h.Key.Y)
                    .Select(h => h.Value)
                    .ToList();
                AcisBody one = AcisBody.Create(outer, ordered, tolerance.EqualPoint);
                built.Add(new KeyValuePair<GeoPoint2, AcisFace>(Corner(face.Boundary.Vertices), one.Faces[0]));
            }

            if (built.Count == 0)
            {
                return null;
            }

            // The faces from the left, then from the bottom, a stable sort again.
            return new AcisBody(built.OrderBy(b => b.Key.X).ThenBy(b => b.Key.Y).Select(b => b.Value).ToList());
        }

        /// <summary>The lower left corner of the box of a loop.</summary>
        private static GeoPoint2 Corner(IReadOnlyList<GeoPoint2> loop) =>
            new GeoPoint2(loop.Min(p => p.X), loop.Min(p => p.Y));

        /// <summary>
        /// The edges of one loop of the answer, a counter-clockwise outer loop or a clockwise hole: each run of its sides on one
        /// input curve is that curve between the two points the run ends at, a side on none a line.
        /// </summary>
        private static IReadOnlyList<AcisCurve> Trace(
            IReadOnlyList<GeoPoint2> vertices,
            bool hole,
            Frame frame,
            Sources sources,
            Tolerance tolerance
        )
        {
            var ring = new List<GeoPoint2>(vertices);
            double area = 0.0;

            for (int i = 0; i < ring.Count; i++)
            {
                GeoPoint2 a = ring[i];
                GeoPoint2 b = ring[(i + 1) % ring.Count];
                area += a.X * b.Y - b.X * a.Y;
            }

            if ((area > 0.0) == hole)
            {
                ring.Reverse();
            }

            double point = tolerance.EqualPoint;
            int n = ring.Count;
            var pieces = new Piece[n];

            for (int i = 0; i < n; i++)
            {
                pieces[i].Curve = -1;
                int chord = sources.Find(ring[i], ring[(i + 1) % n], point, out bool forward);

                if (chord >= 0)
                {
                    pieces[i].Curve = sources.Chords[chord].Curve;
                    pieces[i].Forward = forward;
                }
            }

            // The runs: from a side where the curve changes, round the loop. A loop whose sides all lie on one curve is that
            // curve whole, where it is a closed one.
            int start = -1;

            for (int i = 0; i < n && start < 0; i++)
            {
                if (pieces[i].Curve != pieces[(i + n - 1) % n].Curve || pieces[i].Curve < 0)
                {
                    start = i;
                }
            }

            var curves = new List<AcisCurve>();

            if (start < 0)
            {
                AcisCurve whole = sources.Curves[pieces[0].Curve];

                if (whole.IsFull)
                {
                    // A circle or ellipse that no cut touched, run the way the loop runs.
                    curves.Add(pieces[0].Forward ? whole : whole.Reversed());
                    return curves;
                }

                start = 0;
            }

            var runs = new List<int[]>();

            for (int s = 0; s < n; )
            {
                int index = (start + s) % n;
                int curve = pieces[index].Curve;
                int length = 1;

                // A run goes on while the next side lies on the same curve (an arc, the way it turns the same too).
                while (
                    curve >= 0
                    && s + length < n
                    && pieces[(start + s + length) % n].Curve == curve
                    && (
                        sources.Curves[curve].IsLine
                        || pieces[(start + s + length) % n].Forward == pieces[index].Forward
                    )
                )
                {
                    length++;
                }

                runs.Add(new[] { index, length });
                s += length;
            }

            // The points the runs meet at: each the exact meeting of its two curves, or the end of an input curve it is near.
            var meets = new List<AcisVec>();

            for (int r = 0; r < runs.Count; r++)
            {
                int[] next = runs[r];
                int[] before = runs[(r + runs.Count - 1) % runs.Count];
                AcisVec at = frame.To3(ring[next[0]]);
                int a = pieces[before[0]].Curve;
                int b = pieces[next[0]].Curve;
                meets.Add(Meet(at, a, b, sources, point));
            }

            for (int r = 0; r < runs.Count; r++)
            {
                int[] run = runs[r];
                AcisVec from = meets[r];
                AcisVec to = meets[(r + 1) % runs.Count];
                int curve = pieces[run[0]].Curve;
                AcisCurve made;

                if (curve < 0 || sources.Curves[curve].IsLine)
                {
                    made = AcisCurve.Line(from, to);
                }
                else
                {
                    made = Arc(sources.Curves[curve], ring, run, from, to, frame);
                }

                if (made != null && (made.IsFull || made.Start.DistanceTo(made.End) > point * 0.01))
                {
                    curves.Add(made);
                }
            }

            return curves;
        }

        /// <summary>
        /// The arc of an input ellipse a run of sides follows, between two points: from the parameter of the first to that of the
        /// second, turning as the sides do (the sweep nearest the sum of the sides' own turns).
        /// </summary>
        private static AcisCurve Arc(
            AcisCurve curve,
            List<GeoPoint2> ring,
            int[] run,
            AcisVec from,
            AcisVec to,
            Frame frame
        )
        {
            int n = ring.Count;
            double turned = 0.0;

            for (int i = 0; i < run[1]; i++)
            {
                double a = curve.ParameterOf(frame.To3(ring[(run[0] + i) % n]));
                double b = curve.ParameterOf(frame.To3(ring[(run[0] + i + 1) % n]));
                double d = b - a;

                while (d > Math.PI)
                {
                    d -= 2.0 * Math.PI;
                }

                while (d < -Math.PI)
                {
                    d += 2.0 * Math.PI;
                }

                turned += d;
            }

            double t0 = curve.ParameterOf(from);
            double sweep = curve.ParameterOf(to) - t0;
            sweep -= 2.0 * Math.PI * Math.Round((sweep - turned) / (2.0 * Math.PI));

            if (Math.Abs(sweep) < 1E-12)
            {
                return null;
            }

            bool circle = Math.Abs(curve.Ratio - 1.0) < 1E-12;

            if (circle)
            {
                return AcisCurve.Arc(curve.Center, curve.Normal, curve.At(t0), sweep);
            }

            AcisCurve forwardArc =
                sweep > 0.0
                    ? AcisCurve.Ellipse(curve.Center, curve.Normal, curve.Major, curve.Ratio, t0, t0 + sweep)
                    : AcisCurve.Ellipse(curve.Center, curve.Normal, curve.Major, curve.Ratio, t0 + sweep, t0);
            return sweep > 0.0 ? forwardArc : forwardArc.Reversed();
        }

        /// <summary>
        /// Where two runs meet: the end of an input curve within half the point tolerance of the point the booleans gave, else the
        /// meeting of the two curves nearest that point, else the point as it is.
        /// </summary>
        private static AcisVec Meet(AcisVec at, int a, int b, Sources sources, double tolerance)
        {
            AcisVec best = at;
            double bestDistance = tolerance * 0.5;
            bool snapped = false;

            foreach (AcisVec end in sources.Ends)
            {
                double d = end.DistanceTo(at);

                if (d < bestDistance)
                {
                    best = end;
                    bestDistance = d;
                    snapped = true;
                }
            }

            if (snapped || a < 0 || b < 0 || a == b)
            {
                return best;
            }

            AcisCurve first = sources.Curves[a];
            AcisCurve second = sources.Curves[b];
            double s = first.ParameterOf(at);
            double t = second.ParameterOf(at);

            for (int i = 0; i < 30; i++)
            {
                AcisVec gap = first.PointAt(s) - second.PointAt(t);

                if (gap.Length < 1E-13 * (1.0 + at.Length))
                {
                    break;
                }

                AcisVec u = first.VelocityAt(s);
                AcisVec v = second.VelocityAt(t) * -1.0;

                // Least squares on the plane of the two curves: solve [u v] [ds dt] = -gap through the normal equations.
                double uu = u.Dot(u);
                double uv = u.Dot(v);
                double vv = v.Dot(v);
                double det = uu * vv - uv * uv;

                if (Math.Abs(det) < 1E-18 * uu * vv)
                {
                    return at;
                }

                double gu = -gap.Dot(u);
                double gv = -gap.Dot(v);
                s += (gu * vv - gv * uv) / det;
                t += (uu * gv - uv * gu) / det;
            }

            AcisVec met = (first.PointAt(s) + second.PointAt(t)) * 0.5;
            return
                (first.PointAt(s) - second.PointAt(t)).Length <= tolerance * 0.01
                && met.DistanceTo(at) <= tolerance * 4.0
                ? met
                : at;
        }

        #endregion
    }
}
