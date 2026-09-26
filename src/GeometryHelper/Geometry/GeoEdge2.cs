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
        public bool IsArc => Bulge != 0.0;

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
        /// <exception cref="InvalidOperationException">Thrown when the edge is straight.</exception>
        public GeoArc2 ToArc()
        {
            if (!IsArc)
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
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the point lies strictly inside the piece the arc cuts off its chord.</returns>
        /// <remarks>
        /// This is what tells an arc loop from the straight loop through its vertices: walking the arc
        /// rather than the chord adds that piece where the arc bulges out and takes it away where the arc
        /// bulges in. A straight edge cuts nothing off, so it never holds a point.
        /// </remarks>
        internal bool CutsOff(GeoPoint2 point, Tolerance tolerance)
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
            // of the arc lies in. That holds whichever way the arc leans and however far it sweeps.
            GeoVector2 along = StartPoint.GetVectorTo(EndPoint);
            double atPoint = along.CrossProduct(StartPoint.GetVectorTo(point));
            double atMiddle = along.CrossProduct(StartPoint.GetVectorTo(arc.MidPoint));

            if (Math.Abs(atPoint) <= tolerance.EqualPoint * along.Length)
            {
                return false;
            }

            return atPoint > 0.0 == atMiddle > 0.0;
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
        /// transformation that scales the axes differently would make an arc part of an ellipse, and is
        /// refused rather than answered with an averaged one.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation would stretch an arc into part of an ellipse.</exception>
        public GeoEdge2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (!IsArc)
            {
                return new GeoEdge2(transform.Transform(StartPoint), transform.Transform(EndPoint));
            }

            // The arc refuses an uneven scaling on the edge's behalf, and works the new bulge out for it.
            return new GeoEdge2(ToArc().TransformBy(transform));
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
            int pieces = Internal.Tessellation.SegmentsForChordTolerance(arc.Radius, Math.Abs(arc.SweptAngle), chordTolerance);

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
