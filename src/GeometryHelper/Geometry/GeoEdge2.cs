using System;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// One piece of a chain: a straight segment, or an arc when it carries a bulge.
    /// <para>
    /// The bulge is the tangent of a quarter of the angle the arc sweeps, the number AutoCAD stores against
    /// each vertex of a polyline: zero is a straight segment, one is a half turn counter-clockwise, and the
    /// sign says which way it goes. Storing it rather than a centre and two angles is what makes a round
    /// trip through a drawing exact, and it is why an edge needs no more than its two ends and one number.
    /// </para>
    /// </summary>
    public readonly partial struct GeoEdge2 : IEquatable<GeoEdge2>
    {
        /// <summary>
        /// Gets the point the edge starts at.
        /// </summary>
        public GeoPoint2 StartPoint { get; }

        /// <summary>
        /// Gets the point the edge ends at.
        /// </summary>
        public GeoPoint2 EndPoint { get; }

        /// <summary>
        /// Gets the point half way along the edge: the middle of its segment or of its arc.
        /// </summary>
        public GeoPoint2 MidPoint => IsArc ? ToArc().MidPoint : ToLine().MidPoint;

        /// <summary>
        /// Gets the bulge: the tangent of a quarter of the angle an arc sweeps, or zero for a straight
        /// segment.
        /// </summary>
        public double Bulge { get; }

        /// <summary>
        /// Initializes a straight edge between two points.
        /// </summary>
        /// <param name="startPoint">Where the edge starts.</param>
        /// <param name="endPoint">Where the edge ends.</param>
        public GeoEdge2(GeoPoint2 startPoint, GeoPoint2 endPoint)
            : this(startPoint, endPoint, 0.0)
        {
        }

        /// <summary>
        /// Initializes an edge between two points, straight or bulged into an arc.
        /// </summary>
        /// <param name="startPoint">Where the edge starts.</param>
        /// <param name="endPoint">Where the edge ends.</param>
        /// <param name="bulge">The bulge: zero for a straight segment, otherwise the tangent of a quarter of the angle the arc sweeps.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the bulge is not a number.</exception>
        public GeoEdge2(GeoPoint2 startPoint, GeoPoint2 endPoint, double bulge)
        {
            if (double.IsNaN(bulge) || double.IsInfinity(bulge))
            {
                throw new ArgumentOutOfRangeException(nameof(bulge), "A bulge must be a number.");
            }

            StartPoint = startPoint;
            EndPoint = endPoint;
            Bulge = bulge;
        }

        /// <summary>
        /// Initializes the edge that draws an arc.
        /// </summary>
        /// <param name="arc">The arc to hold as an edge.</param>
        /// <remarks>
        /// An arc sweeping a whole turn has its two ends in the same place and cannot be told from a point
        /// by its ends alone, so it is refused: a chain says a full circle with a circle, not with an edge.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the arc sweeps a whole turn.</exception>
        public GeoEdge2(GeoArc2 arc)
        {
            if (Math.Abs(Math.Abs(arc.SweptAngle) - Math.PI * 2.0) <= 1E-12)
            {
                throw new ArgumentOutOfRangeException(nameof(arc), "An arc sweeping a whole turn has no chord, so it is no edge of a chain.");
            }

            StartPoint = arc.StartPoint;
            EndPoint = arc.EndPoint;
            Bulge = arc.Bulge;
        }

        /// <summary>
        /// Gets a value indicating whether the edge is an arc rather than a straight segment.
        /// </summary>
        /// <remarks>
        /// An edge that bows less off its chord than the centre of its arc can be placed to is read as its chord, and is not
        /// an arc: its sag no more than 2.5 parts in 1E16 of how far the centre and the radius reach from the origin. The edge
        /// from (0, 0) to (100, 0) with a bulge of 1E-12 bows 5E-11 on a radius of 2.5E13, and its arc put its middle at
        /// (50.002, 0); it is the segment, and measures, cuts and flattens as one. A bulge of a ten-millionth over the same
        /// chord, bowing 5E-6, is an arc. Read so, its middle, its length and the area it bounds are the chord's, off the true
        /// arc's by no more than its sag, which is less than the rounding of the arc's centre; split, its pieces are straight,
        /// their bulge nought. The reading follows the reach of the edge from the origin, so moving or turning an edge near the
        /// threshold can change it.
        /// </remarks>
        public bool IsArc => Bulge != 0.0 && (BowsPlainly() || !ReadsAsChord());

        /// <summary>
        /// Determines whether the edge, carrying a bulge, bows plainly more than the centre of its arc can be placed to,
        /// settled with neither a root nor a division.
        /// </summary>
        /// <remarks>
        /// The chord measured along the axes, l, is no less than the chord c and no more than c times the root of 2, so the
        /// sag, c |b| / 2, is no less than 0.35 |b| l. The centre stands within the radius, c (1 + b²) / 4|b|, of the chord's
        /// middle, so the reach is no more than that middle's plus three radii, and up to a half turn no more than the middle's
        /// plus 1.5 l / |b|. Past a half turn the sag is more than half the chord, and plain.
        /// </remarks>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool BowsPlainly()
        {
            double magnitude = Math.Abs(Bulge);
            double along = Math.Abs(EndPoint.X - StartPoint.X) + Math.Abs(EndPoint.Y - StartPoint.Y);
            double middle = Math.Abs(StartPoint.X + EndPoint.X) + Math.Abs(StartPoint.Y + EndPoint.Y);

            return !(magnitude <= 1.0)
                || magnitude * magnitude * along * 0.35 > (magnitude * middle * 0.5 + 1.5 * along) * Core.Arc2.FlatFraction;
        }

        /// <summary>
        /// Determines whether the edge, carrying a bulge of no more than one, bows so little that its chord is read for it, as
        /// <see cref="Core.Arc2.IsFlat"/> reads its arc; an edge with no chord is an arc as before.
        /// </summary>
        private bool ReadsAsChord()
        {
            double chord = StartPoint.DistanceTo(EndPoint);

            if (!(chord > 0.0))
            {
                return false;
            }

            GeoArc2 arc = ToArc();

            return Core.Arc2.IsFlat(Math.Abs(Bulge) * chord * 0.5, arc.Center, arc.Radius);
        }

        /// <summary>
        /// Gets the length of the edge: along the arc when it is one, and straight across otherwise.
        /// </summary>
        public double Length => IsArc ? ToArc().Length : StartPoint.DistanceTo(EndPoint);

        /// <summary>
        /// Gets the chord of the edge: the straight segment between its ends, whether or not it bulges.
        /// </summary>
        public GeoLine2 GetChord() => new GeoLine2(StartPoint, EndPoint);

        /// <summary>
        /// Gets the edge as a straight segment.
        /// </summary>
        /// <returns>The segment between the two ends of the edge.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the edge is an arc.</exception>
        public GeoLine2 ToLine()
        {
            if (IsArc)
            {
                throw new InvalidOperationException("This edge is an arc; ask for its chord if a straight segment is what is wanted.");
            }

            return new GeoLine2(StartPoint, EndPoint);
        }

        /// <summary>
        /// Gets the edge as an arc.
        /// </summary>
        /// <returns>The arc the bulge describes.</returns>
        /// <remarks>
        /// An edge that bows so little it is read as its chord (see <see cref="IsArc"/>) still gives the arc its bulge
        /// describes, to a caller who asks for that arc; the edge's own members, and every query, read it as the segment.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the edge is straight, its bulge nought.</exception>
        public GeoArc2 ToArc()
        {
            if (Bulge == 0.0)
            {
                throw new InvalidOperationException("This edge is a straight segment, so there is no arc to give.");
            }

            return GeoArc2.FromBulge(StartPoint, EndPoint, Bulge);
        }

        /// <summary>
        /// Gets the point at a normalized parameter along the edge, where 0 is its start and 1 its end.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter)
        {
            if (IsArc)
            {
                return ToArc().GetPointAtParameter(parameter);
            }

            return new GeoPoint2(
                StartPoint.X + (EndPoint.X - StartPoint.X) * parameter,
                StartPoint.Y + (EndPoint.Y - StartPoint.Y) * parameter);
        }

        /// <summary>
        /// Gets the normalized parameter of the point of the edge nearest a point, where 0 is its start and
        /// 1 its end.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the normalized parameter of the point of the edge nearest a point, within a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point, Tolerance tolerance)
        {
            if (IsArc)
            {
                return ToArc().GetParameterAtPoint(point, tolerance);
            }

            double length = StartPoint.DistanceTo(EndPoint);

            if (length <= tolerance.EqualPoint)
            {
                return 0.0;
            }

            GeoVector2 along = StartPoint.GetVectorTo(EndPoint);
            double reach = StartPoint.GetVectorTo(point).DotProduct(along) / (length * length);

            return reach < 0.0 ? 0.0 : reach > 1.0 ? 1.0 : reach;
        }

        /// <summary>
        /// Splits the edge at a normalized parameter along it.
        /// </summary>
        /// <param name="parameter">Where to cut, between 0 and 1.</param>
        /// <param name="first">The piece from the start of the edge to the cut.</param>
        /// <param name="second">The piece from the cut to the end of the edge.</param>
        /// <param name="tolerance">The tolerance: a cut at either end leaves nothing to cut off and is refused.</param>
        /// <returns>true when the edge was cut in two; otherwise, false.</returns>
        public bool TrySplitAtParameter(double parameter, out GeoEdge2 first, out GeoEdge2 second, Tolerance tolerance)
        {
            first = this;
            second = this;

            if (double.IsNaN(parameter) || parameter <= 0.0 || parameter >= 1.0)
            {
                return false;
            }

            GeoPoint2 at = GetPointAtParameter(parameter);

            if (at.IsEqualTo(StartPoint, tolerance) || at.IsEqualTo(EndPoint, tolerance))
            {
                return false;
            }

            if (!IsArc)
            {
                first = new GeoEdge2(StartPoint, at);
                second = new GeoEdge2(at, EndPoint);
                return true;
            }

            // A bulge is the tangent of a quarter of the sweep, and the cut divides the sweep in the same
            // proportion as the parameter.
            double swept = ToArc().SweptAngle;

            first = new GeoEdge2(StartPoint, at, Math.Tan(swept * parameter * 0.25));
            second = new GeoEdge2(at, EndPoint, Math.Tan(swept * (1.0 - parameter) * 0.25));
            return true;
        }

        /// <summary>
        /// Gets the circular segment of the edge: the piece between its arc and its chord.
        /// </summary>
        /// <param name="point">The point to test.</param>
        /// <returns>true when the point lies strictly inside the piece the arc cuts off its chord.</returns>
        /// <remarks>
        /// This is what tells an arc loop from the straight loop through its vertices: walking the arc
        /// rather than the chord adds that piece where the arc bulges out and takes it away where the arc
        /// bulges in. A straight edge cuts nothing off, so it never holds a point. The side of the chord is
        /// read exactly, by the rule the straight loop's own test reads it with (see
        /// <see cref="Core.ArcChain2.BeforeChord"/>), so that the two agree on every point near the chord.
        /// </remarks>
        internal bool CutsOff(GeoPoint2 point)
        {
            if (!IsArc)
            {
                return false;
            }

            GeoArc2 arc = ToArc();

            if (arc.Center.GetDistanceSquaredTo(point) >= arc.Radius * arc.Radius)
            {
                return false;
            }

            // Inside the circle, the chord cuts the disc in two and the piece wanted is the one the middle
            // of the arc lies in. That holds whichever way the arc leans and however far it sweeps. The middle is
            // told by the bulge, which puts it to the right of the way the chord runs when positive, and not
            // worked out from the centre: on an arc of nearly no bulge the centre lies thousands away, and the
            // middle worked out from it can land on the wrong side of the chord, and the piece wanted become the
            // whole disc. A point a chord's length out from the chord's middle on that side stands clear of the
            // line, and tells the same side.
            GeoVector2 along = StartPoint.GetVectorTo(EndPoint);
            double side = Bulge > 0.0 ? 1.0 : -1.0;
            var onTheArcsSide = new GeoPoint2(
                (StartPoint.X + EndPoint.X) * 0.5 + side * along.Y,
                (StartPoint.Y + EndPoint.Y) * 0.5 - side * along.X);

            return Core.ArcChain2.BeforeChord(EndPoint, StartPoint, point) == Core.ArcChain2.BeforeChord(EndPoint, StartPoint, onTheArcsSide);
        }

        /// <summary>
        /// Gets the edge running the other way, from this one's end to its start.
        /// </summary>
        /// <remarks>
        /// Turning an edge round turns its arc round with it, so the bulge changes sign.
        /// </remarks>
        public GeoEdge2 Reverse() => new GeoEdge2(EndPoint, StartPoint, -Bulge);

        /// <summary>
        /// Gets the edge moved by a vector.
        /// </summary>
        /// <param name="vector">How far to move it.</param>
        /// <returns>The moved edge, bulged exactly as this one is.</returns>
        public GeoEdge2 Translate(GeoVector2 vector) => new GeoEdge2(StartPoint.Add(vector), EndPoint.Add(vector), Bulge);

        /// <summary>
        /// Gets the edge turned about a point.
        /// </summary>
        /// <param name="angleRad">How far to turn it, in radians, counter-clockwise.</param>
        /// <param name="center">The point to turn it about.</param>
        /// <returns>The turned edge, bulged exactly as this one is.</returns>
        /// <remarks>
        /// Turning bends nothing, so the bulge is untouched: it measures the arc against its own chord,
        /// which turns with it.
        /// </remarks>
        public GeoEdge2 RotateBy(double angleRad, GeoPoint2 center)
        {
            return new GeoEdge2(StartPoint.RotateBy(angleRad, center), EndPoint.RotateBy(angleRad, center), Bulge);
        }

        /// <summary>
        /// Gets the edge under a transformation.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed edge.</returns>
        /// <remarks>
        /// A bulge is the shape of the arc against its own chord, so moving, turning and scaling evenly
        /// leave it alone, and mirroring changes its sign because the arc then leans the other way. A
        /// transformation that skews the axes or scales them differently makes an arc part of an ellipse: the edge is then the
        /// edge from where its ends land through where its middle lands, as in space
        /// (<see cref="GeoEdge3.TransformBy(GeoTransform3)"/>), where that lands within the global point tolerance of where the
        /// transformation puts the rest of it, as the arc is (<see cref="GeoArc2.TransformBy(GeoTransform2)"/>), and is refused
        /// otherwise rather than answered with an arc visibly off the ellipse. The edge of bulge 0.5 from (0, 0) to (100, 0)
        /// under the skew that sends Y to (0.000001, 1) ends at (100, 0); under the one that sends X to (1, 0) and Y to (0.6, 0.8)
        /// it is refused, the arc through those three points standing far off the ellipse it becomes. An edge 100 long bowing
        /// by 1E-12 moves under a turn rounded to six places, where it was refused for want of digits.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation would skew or stretch an arc into part of an ellipse it would land visibly off.</exception>
        public GeoEdge2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoPoint2 start = transform.Transform(StartPoint);
            GeoPoint2 end = transform.Transform(EndPoint);

            if (Bulge == 0.0)
            {
                return new GeoEdge2(start, end);
            }

            GeoVector2 alongX = transform.Transform(GeoVector2.XAxis);
            GeoVector2 alongY = transform.Transform(GeoVector2.YAxis);

            // A turn, a shift, a mirror or an even scaling: the arc refuses nothing and works the new bulge out for it.
            if (GeoTransform2.KeepsRound(alongX, alongY))
            {
                return new GeoEdge2(ToArc().TransformBy(transform));
            }

            // The middle stands half the chord times the bulge off the chord's middle, to the right of the chord for a
            // positive bulge: worked out from the chord, so however little the edge bows.
            GeoVector2 chord = StartPoint.GetVectorTo(EndPoint);
            GeoVector2 right = new GeoVector2(chord.Y, -chord.X);
            GeoVector2 toMiddle = chord.Multiply(0.5).Add(right.Multiply(Bulge * 0.5));

            if (!Core.Arc2.TryBulgeThrough(start.GetVectorTo(end), transform.Transform(toMiddle), out double bulge))
            {
                throw new InvalidOperationException("Three points on one line have no arc through them.");
            }

            var through = new GeoEdge2(start, end, bulge);

            // A skew or a stretch makes part of an ellipse of the arc: it stands for it where it lands within the global point
            // tolerance of where the transformation puts the rest of the edge.
            if (!ToArc().LandsWithin(transform, through.ToArc()))
            {
                throw new InvalidOperationException("This transformation stretches the arc into part of an ellipse.");
            }

            return through;
        }

        /// <summary>
        /// Creates a copy of this edge.
        /// </summary>
        public GeoEdge2 Clone() => new GeoEdge2(StartPoint, EndPoint, Bulge);

        /// <summary>
        /// Approximates the edge as points, close enough that no piece strays further from it than a chord
        /// tolerance, and appends them to a list without repeating the point already at its end.
        /// </summary>
        /// <param name="into">The list to append to; the start of the edge is appended only when the list is empty.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc, in drawing units. Zero picks the automatic share of the radius. A straight edge ignores it.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number, or needs more than 1 048 576 pieces for the arc.</exception>
        internal void AppendFlattened(System.Collections.Generic.List<GeoPoint2> into, double chordTolerance)
        {
            if (into.Count == 0)
            {
                into.Add(StartPoint);
            }

            if (!IsArc)
            {
                into.Add(EndPoint);
                return;
            }

            GeoArc2 arc = ToArc();
            int pieces = Internal.Tessellation.SegmentsForArc(arc.Radius, Math.Abs(arc.SweptAngle), chordTolerance);

            for (int i = 1; i < pieces; i++)
            {
                into.Add(arc.GetPointAtParameter((double)i / pieces));
            }

            into.Add(EndPoint);
        }

        /// <summary>
        /// Determines whether another edge holds exactly the same ends and bulge.
        /// </summary>
        public bool Equals(GeoEdge2 other)
        {
            return StartPoint.Equals(other.StartPoint) && EndPoint.Equals(other.EndPoint) && Bulge.Equals(other.Bulge);
        }

        /// <summary>
        /// Determines whether the specified object is an equal edge.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoEdge2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this edge.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StartPoint.GetHashCode();
                hash = hash * 397 ^ EndPoint.GetHashCode();
                hash = hash * 397 ^ Bulge.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Determines whether another edge draws the same piece, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoEdge2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another edge draws the same piece, within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoEdge2 other, Tolerance tolerance)
        {
            return StartPoint.IsEqualTo(other.StartPoint, tolerance)
                && EndPoint.IsEqualTo(other.EndPoint, tolerance)
                && Math.Abs(Bulge - other.Bulge) <= tolerance.EqualVector;
        }

        /// <summary>
        /// Determines whether two edges hold exactly the same values.
        /// </summary>
        public static bool operator ==(GeoEdge2 left, GeoEdge2 right) => left.Equals(right);

        /// <summary>
        /// Determines whether two edges hold different values.
        /// </summary>
        public static bool operator !=(GeoEdge2 left, GeoEdge2 right) => !left.Equals(right);

        /// <summary>
        /// Gets whether this is a shape a constructor could have made: both ends are valid points and the bulge a number.
        /// </summary>
        /// <remarks>
        /// A value made by a constructor always is. A <c>default</c> one — an element of a new array, or what
        /// the <c>out</c> of a <c>Try</c> method holds when the method said false — may not be, and answers
        /// questions without complaint all the same.
        /// </remarks>
        public bool IsValid => StartPoint.IsValid && EndPoint.IsValid && Guard.IsFinite(Bulge);

        /// <summary>
        /// Describes the edge.
        /// </summary>
        public override string ToString()
        {
            return IsArc
                ? $"GeoEdge2[{StartPoint} -> {EndPoint}, bulge {Bulge:0.####}]"
                : $"GeoEdge2[{StartPoint} -> {EndPoint}]";
        }
        #region Extending and trimming

        /// <summary>
        /// Lengthens the edge at one end, along itself.
        /// </summary>
        /// <remarks>
        /// A straight leg carries on straight and a bend carries on round, keeping its radius, so the distance is
        /// measured along the edge either way. Both readings hand back an edge, which is why this can be offered
        /// at all: a direction is only offered here where a segment and a bend answer with the same shape of call.
        /// </remarks>
        public GeoEdge2 Extend(double distance, LineEnd end) => Extend(distance, end, Tolerance.Global);

        /// <summary>
        /// Lengthens the edge at one end, along itself, within a tolerance.
        /// </summary>
        /// <param name="distance">How much length to add, measured along the edge; a negative distance takes it away.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoEdge2 Extend(double distance, LineEnd end, Tolerance tolerance)
        {
            if (IsArc)
            {
                return new GeoEdge2(ToArc().Extend(distance, end, tolerance));
            }

            GeoLine2 carried = ToLine().Extend(distance, end, tolerance);

            return new GeoEdge2(carried.StartPoint, carried.EndPoint);
        }

        /// <summary>
        /// Lengthens or shortens the edge at one end until it is a given length.
        /// </summary>
        public GeoEdge2 ExtendToLength(double length, LineEnd end) => ExtendToLength(length, end, Tolerance.Global);

        /// <summary>
        /// Lengthens or shortens the edge at one end until it is a given length, within a tolerance.
        /// </summary>
        /// <param name="length">The length it should end up with, measured along the edge.</param>
        /// <param name="end">Which end to move; the other stays where it is.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoEdge2 ExtendToLength(double length, LineEnd end, Tolerance tolerance)
        {
            if (IsArc)
            {
                return new GeoEdge2(ToArc().ExtendToLength(length, end, tolerance));
            }

            GeoLine2 carried = ToLine().ExtendToLength(length, end, tolerance);

            return new GeoEdge2(carried.StartPoint, carried.EndPoint);
        }

        /// <summary>
        /// Shortens the edge at one end back to a point on it.
        /// </summary>
        public bool TryTrimTo(GeoPoint2 point, LineEnd end, out GeoEdge2 result) => TryTrimTo(point, end, out result, Tolerance.Global);

        /// <summary>
        /// Shortens the edge at one end back to a point on it, within a tolerance.
        /// </summary>
        /// <param name="point">The point to stop at; it has to lie on the edge.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="result">The shortened edge, or the edge unchanged.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the edge was shortened; otherwise, false.</returns>
        public bool TryTrimTo(GeoPoint2 point, LineEnd end, out GeoEdge2 result, Tolerance tolerance)
        {
            if (IsArc)
            {
                bool cut = ToArc().TryTrimTo(point, end, out GeoArc2 trimmed, tolerance);

                result = cut ? new GeoEdge2(trimmed) : this;

                return cut;
            }

            bool shortened = ToLine().TryTrimTo(point, end, out var line, tolerance);

            result = shortened ? new GeoEdge2(line.StartPoint, line.EndPoint) : this;

            return shortened;
        }

        #endregion

        /// <summary>
        /// Determines whether a point lies on this edge.
        /// </summary>
        /// <remarks>
        /// The edge reads itself as whichever of the two it is, so this is a point on a segment or a point on
        /// an arc. <see cref="GeoEdge3"/> has answered this all along; the plane's edge had neither this nor
        /// <see cref="Locate(GeoPoint2)"/>.
        /// </remarks>
        public bool IsPointOn(GeoPoint2 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on this edge, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance)
            => IsArc ? ToArc().IsPointOn(point, tolerance) : ToLine().IsPointOn(point, tolerance);

        /// <summary>
        /// Says where a point lies with respect to this edge.
        /// </summary>
        /// <remarks>
        /// An edge is a curve and encloses nothing, so the answer is only ever <c>OnSide</c> or <c>OutSide</c>.
        /// </remarks>
        public PointLocation Locate(GeoPoint2 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Says where a point lies with respect to this edge, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns><c>OnSide</c> when the point is on the edge; otherwise, <c>OutSide</c>.</returns>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance)
            => IsPointOn(point, tolerance) ? PointLocation.OnSide : PointLocation.OutSide;

    }
}
