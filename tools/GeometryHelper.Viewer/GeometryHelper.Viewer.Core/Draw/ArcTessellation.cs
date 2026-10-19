using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// How finely curves are cut into chords, and where the cutting leaves what it learnt: the points where the exact curve
    /// reaches furthest along each axis, and whether the cap on the number of pieces was reached.
    /// </summary>
    /// <remarks>
    /// A piece is cut so that its chord lies no further than <see cref="ChordTolerance"/> from the curve and so that it turns at
    /// most 2 degrees; a full turn is cut in at least 16 pieces and at most 65 536. At a chord tolerance of 1E-4 (a tenth of
    /// the default point tolerance) a circle of radius 500 takes 4 968 pieces a turn, and one of radius 60 000 takes 54 414.
    /// </remarks>
    public sealed class ChordRule
    {
        /// <summary>
        /// The most an arc turns between two drawn points, in radians: 2 degrees.
        /// </summary>
        public const double MaxPieceAngle = 2.0 * Math.PI / 180.0;

        /// <summary>
        /// The fewest pieces a full turn is cut in.
        /// </summary>
        public const int MinPiecesPerTurn = 16;

        /// <summary>
        /// The most pieces a full turn is cut in; an arc takes the same share of it as it turns of a full turn.
        /// </summary>
        public const int MaxPiecesPerTurn = 65536;

        /// <summary>
        /// Initializes a rule.
        /// </summary>
        /// <param name="chordTolerance">The furthest a chord may lie from its curve, in millimetres; above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is not a finite number above nought.</exception>
        public ChordRule(double chordTolerance)
        {
            if (double.IsNaN(chordTolerance) || double.IsInfinity(chordTolerance) || chordTolerance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(chordTolerance), "A chord tolerance is a number above nought.");
            }

            ChordTolerance = chordTolerance;
        }

        /// <summary>
        /// Initializes a rule that may leave the chord tolerance aside and cut by the angle alone, which is how a scene too big to draw
        /// at its chord tolerance has its biggest curves cut: 2 degrees a piece, 180 pieces a turn.
        /// </summary>
        internal ChordRule(double chordTolerance, bool angleOnly)
            : this(chordTolerance)
        {
            AngleOnly = angleOnly;
        }

        /// <summary>
        /// Gets whether the rule cuts by the angle alone: at most 2 degrees a piece and at least 16 a turn, whatever the chord tolerance.
        /// </summary>
        public bool AngleOnly { get; }

        /// <summary>
        /// Gets the furthest a chord may lie from its curve.
        /// </summary>
        public double ChordTolerance { get; }

        /// <summary>
        /// Gets whether some curve would have taken more pieces than <see cref="MaxPiecesPerTurn"/> allows and was cut in fewer.
        /// </summary>
        public bool Capped { get; private set; }

        /// <summary>
        /// Gets the points of the exact curves cut so far where they reach furthest along an axis, either way, for the box
        /// round them: a chord cut a little short of the extreme leaves it out.
        /// </summary>
        public List<Vec3> Extremes { get; } = new List<Vec3>();

        /// <summary>
        /// Gets the number of pieces a curve turning through an angle is cut in.
        /// </summary>
        /// <param name="sweptAngle">The angle in radians; its sign does not matter.</param>
        /// <param name="radius">The radius of the curve; for an ellipse the longer radius, which no curvature of it exceeds.</param>
        /// <returns>At least one.</returns>
        public int Pieces(double sweptAngle, double radius)
        {
            double turn = Math.Abs(sweptAngle);

            if (double.IsNaN(turn) || double.IsInfinity(turn))
            {
                return 1;
            }

            double wanted = Math.Max(Math.Ceiling(turn / MaxPieceAngle), Math.Ceiling(MinPiecesPerTurn * turn / (2.0 * Math.PI)));

            if (!AngleOnly && radius > 0.0 && !double.IsInfinity(radius))
            {
                // The sagitta of a piece turning through a is 2 r sin^2(a / 4), so a piece may turn through 4 asin(sqrt(s / 2 r)).
                double ratio = ChordTolerance / (2.0 * radius);
                double step = ratio >= 1.0 ? Math.PI * 2.0 : 4.0 * Math.Asin(Math.Sqrt(ratio));
                wanted = Math.Max(wanted, Math.Ceiling(turn / step));
            }

            double cap = Math.Max(1.0, Math.Ceiling(MaxPiecesPerTurn * turn / (2.0 * Math.PI)));

            if (wanted > cap)
            {
                wanted = cap;
                Capped = true;
            }

            return (int)Math.Max(1.0, wanted);
        }

        /// <summary>
        /// Notes where the curve P(t) = c + u a cos t + v b sin t, for t from <paramref name="start"/> through
        /// <paramref name="sweep"/>, reaches furthest along each axis.
        /// </summary>
        internal void NoteExtremes(Vec3 c, Vec3 u, Vec3 v, double a, double b, double start, double sweep)
        {
            double[] us = { u.X, u.Y, u.Z };
            double[] vs = { v.X, v.Y, v.Z };
            double turn = 2.0 * Math.PI;

            for (int axis = 0; axis < 3; axis++)
            {
                double t = Math.Atan2(b * vs[axis], a * us[axis]);

                for (int side = 0; side < 2; side++, t += Math.PI)
                {
                    double along = sweep >= 0.0 ? Wrap(t - start) : Wrap(start - t);

                    if (Math.Abs(sweep) >= turn - 1E-12 || along <= Math.Abs(sweep))
                    {
                        Extremes.Add(c + u * (a * Math.Cos(t)) + v * (b * Math.Sin(t)));
                    }
                }
            }
        }

        private static double Wrap(double angle)
        {
            double turn = 2.0 * Math.PI;
            double wrapped = angle % turn;
            return wrapped < 0.0 ? wrapped + turn : wrapped;
        }
    }

    /// <summary>
    /// Cuts arcs, circles and ellipses into the chords the viewer draws. It is the viewer's own and never the library's
    /// <c>Flatten</c>, so a fault in the library's flattening shows in the drawing as the fault it is, not as an arc drawn
    /// with the same error.
    /// </summary>
    public static class ArcTessellation
    {
        /// <summary>
        /// Gets the points along an arc in the plane, the first and last exactly the ends.
        /// </summary>
        /// <param name="rule">How finely to cut.</param>
        /// <param name="center">The centre.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="startAngle">The angle it starts at, in radians.</param>
        /// <param name="sweptAngle">The angle it turns through, positive counter-clockwise.</param>
        public static Vec3[] Arc2(ChordRule rule, Vec3 center, double radius, double startAngle, double sweptAngle)
        {
            int pieces = rule.Pieces(sweptAngle, radius);
            var points = new Vec3[pieces + 1];

            for (int i = 0; i <= pieces; i++)
            {
                double angle = startAngle + sweptAngle * i / pieces;
                points[i] = new Vec3(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle), center.Z);
            }

            rule.NoteExtremes(center, new Vec3(1, 0, 0), new Vec3(0, 1, 0), radius, radius, startAngle, sweptAngle);
            return points;
        }

        /// <summary>
        /// Gets the points along an arc in space. Angles are measured counter-clockwise round the normal from the first axis
        /// <see cref="GeoPlane3.GetAxes"/> gives for the plane, as the library measures them.
        /// </summary>
        /// <param name="rule">How finely to cut.</param>
        /// <param name="center">The centre.</param>
        /// <param name="normal">The normal of the plane of the arc.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="startAngle">The angle it starts at, in radians.</param>
        /// <param name="sweptAngle">The angle it turns through, positive counter-clockwise round the normal.</param>
        /// <exception cref="ArgumentException">Thrown when the normal has no length.</exception>
        public static Vec3[] Arc3(ChordRule rule, Vec3 center, Vec3 normal, double radius, double startAngle, double sweptAngle)
        {
            Axes(normal, out Vec3 u, out Vec3 v);

            int pieces = rule.Pieces(sweptAngle, radius);
            var points = new Vec3[pieces + 1];

            for (int i = 0; i <= pieces; i++)
            {
                double angle = startAngle + sweptAngle * i / pieces;
                points[i] = center + u * (radius * Math.Cos(angle)) + v * (radius * Math.Sin(angle));
            }

            rule.NoteExtremes(center, u, v, radius, radius, startAngle, sweptAngle);
            return points;
        }

        /// <summary>
        /// Gets the points round a circle in the plane, not repeating the first.
        /// </summary>
        /// <param name="rule">How finely to cut.</param>
        /// <param name="center">The centre.</param>
        /// <param name="radius">The radius.</param>
        public static Vec3[] Circle2(ChordRule rule, Vec3 center, double radius)
        {
            Vec3[] round = Arc2(rule, center, radius, 0.0, 2.0 * Math.PI);
            Array.Resize(ref round, round.Length - 1);
            return round;
        }

        /// <summary>
        /// Gets the points round a circle in space, not repeating the first.
        /// </summary>
        /// <param name="rule">How finely to cut.</param>
        /// <param name="center">The centre.</param>
        /// <param name="normal">The normal of the plane of the circle.</param>
        /// <param name="radius">The radius.</param>
        /// <exception cref="ArgumentException">Thrown when the normal has no length.</exception>
        public static Vec3[] Circle3(ChordRule rule, Vec3 center, Vec3 normal, double radius)
        {
            Vec3[] round = Arc3(rule, center, normal, radius, 0.0, 2.0 * Math.PI);
            Array.Resize(ref round, round.Length - 1);
            return round;
        }

        /// <summary>
        /// Gets the points round an ellipse, not repeating the first, one for each stretch of the parameter that keeps a chord
        /// within the rule: the chord of a stretch of the parameter lies no further from the curve than that of a circle as
        /// large as the longer radius.
        /// </summary>
        /// <param name="rule">How finely to cut.</param>
        /// <param name="center">The centre.</param>
        /// <param name="majorDirection">The direction of the major axis; the ellipse is drawn along this and the direction a quarter turn from it.</param>
        /// <param name="minorDirection">The direction of the minor axis, square to the major.</param>
        /// <param name="majorRadius">The half length of the major axis.</param>
        /// <param name="minorRadius">The half length of the minor axis.</param>
        public static Vec3[] Ellipse(ChordRule rule, Vec3 center, Vec3 majorDirection, Vec3 minorDirection, double majorRadius, double minorRadius)
        {
            int pieces = rule.Pieces(2.0 * Math.PI, Math.Max(Math.Abs(majorRadius), Math.Abs(minorRadius)));
            var points = new Vec3[pieces];

            for (int i = 0; i < pieces; i++)
            {
                double t = 2.0 * Math.PI * i / pieces;
                points[i] = center + majorDirection * (majorRadius * Math.Cos(t)) + minorDirection * (minorRadius * Math.Sin(t));
            }

            rule.NoteExtremes(center, majorDirection, minorDirection, majorRadius, minorRadius, 0.0, 2.0 * Math.PI);
            return points;
        }

        /// <summary>
        /// Gets the points along a chain or loop of edges that may be arcs in the plane. An edge leaves its vertex with the
        /// bulge of that index: the tangent of a quarter of the angle it turns through, positive counter-clockwise.
        /// </summary>
        /// <param name="rule">How finely to cut.</param>
        /// <param name="vertices">The vertices.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex; a missing one is straight.</param>
        /// <param name="closed">Whether the last vertex joins back to the first; the first is then not repeated.</param>
        public static Vec3[] Chain2(ChordRule rule, IReadOnlyList<Vec3> vertices, IReadOnlyList<double> bulges, bool closed)
        {
            var points = new List<Vec3>();
            int edges = closed ? vertices.Count : vertices.Count - 1;

            for (int i = 0; i < edges; i++)
            {
                Vec3 from = vertices[i];
                Vec3 to = vertices[(i + 1) % vertices.Count];
                double bulge = i < bulges.Count ? bulges[i] : 0.0;

                points.Add(from);

                if (bulge != 0.0 && !double.IsNaN(bulge) && !double.IsInfinity(bulge))
                {
                    AddInterior(rule, points, from, to, bulge, new Vec3(0.0, 0.0, 1.0));
                }
            }

            if (!closed && vertices.Count > 0)
            {
                points.Add(vertices[vertices.Count - 1]);
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets the points along a chain or loop of edges that may be arcs in space; each arc is read in the plane of its
        /// normal.
        /// </summary>
        /// <param name="rule">How finely to cut.</param>
        /// <param name="vertices">The vertices.</param>
        /// <param name="bulges">The bulge of the edge leaving each vertex; a missing one is straight.</param>
        /// <param name="normals">The normal each bulge is read about; a missing one, and one of no length, draws its edge straight.</param>
        /// <param name="closed">Whether the last vertex joins back to the first; the first is then not repeated.</param>
        public static Vec3[] Chain3(ChordRule rule, IReadOnlyList<Vec3> vertices, IReadOnlyList<double> bulges, IReadOnlyList<Vec3> normals, bool closed)
        {
            var points = new List<Vec3>();
            int edges = closed ? vertices.Count : vertices.Count - 1;

            for (int i = 0; i < edges; i++)
            {
                Vec3 from = vertices[i];
                Vec3 to = vertices[(i + 1) % vertices.Count];
                double bulge = i < bulges.Count ? bulges[i] : 0.0;
                Vec3 normal = i < normals.Count ? normals[i].Normalized() : default(Vec3);

                points.Add(from);

                if (bulge != 0.0 && !double.IsNaN(bulge) && !double.IsInfinity(bulge) && normal.Length > 0.0)
                {
                    AddInterior(rule, points, from, to, bulge, normal);
                }
            }

            if (!closed && vertices.Count > 0)
            {
                points.Add(vertices[vertices.Count - 1]);
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gives the two directions the library measures angles from in a plane: the first is the one <see cref="GeoPlane3.GetAxes"/>
        /// gives, the second a quarter turn counter-clockwise round the normal from it.
        /// </summary>
        internal static void Axes(Vec3 normal, out Vec3 u, out Vec3 v)
        {
            new GeoPlane3(new GeoPoint3(0.0, 0.0, 0.0), new GeoVector3(normal.X, normal.Y, normal.Z)).GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);
            u = new Vec3(uAxis.X, uAxis.Y, uAxis.Z);
            v = new Vec3(vAxis.X, vAxis.Y, vAxis.Z);
        }

        /// <summary>
        /// Adds the points strictly between the ends of an arc edge. The arc turns through four times the arctangent of the
        /// bulge; its centre lies on the left of the chord, looking from the start to the end, for a positive bulge and on the
        /// right for a negative one, the other side of the chord once the arc turns past a half circle.
        /// </summary>
        private static void AddInterior(ChordRule rule, List<Vec3> points, Vec3 from, Vec3 to, double bulge, Vec3 normal)
        {
            Vec3 chord = to - from;
            double length = chord.Length;

            if (length <= 0.0)
            {
                return;
            }

            double swept = 4.0 * Math.Atan(bulge);
            double half = swept * 0.5;
            Vec3 left = Vec3.Cross(normal, chord) * (1.0 / length);
            Vec3 middle = (from + to) * 0.5;
            Vec3 center = middle + left * (length * 0.5 / Math.Tan(half));
            Vec3 start = from - center;
            double radius = start.Length;
            Vec3 across = Vec3.Cross(normal, start);

            int pieces = rule.Pieces(swept, radius);

            for (int i = 1; i < pieces; i++)
            {
                double angle = swept * i / pieces;
                points.Add(center + start * Math.Cos(angle) + across * Math.Sin(angle));
            }

            if (radius > 0.0 && !double.IsInfinity(radius))
            {
                Vec3 u = start * (1.0 / radius);
                rule.NoteExtremes(center, u, Vec3.Cross(normal, u), radius, radius, 0.0, swept);
            }
        }
    }
}
