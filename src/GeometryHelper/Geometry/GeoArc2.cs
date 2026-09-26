using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a circular arc in the plane: part of a circle, running from one angle to another.
    /// <para>
    /// An arc is a curve, so it encloses nothing and has no area. It is held as a centre, a radius, the
    /// angle it starts at and the angle it sweeps through, signed: positive sweeps counter-clockwise and
    /// negative clockwise. A sweep of a whole turn is allowed and draws the full circle; a sweep of
    /// nothing is not, because an arc that goes nowhere has no direction and no length.
    /// </para>
    /// </summary>
    public readonly partial struct GeoArc2 : IEquatable<GeoArc2>
    {
        private const double FullTurn = Math.PI * 2.0;

        /// <summary>
        /// Gets the centre of the circle the arc lies on.
        /// </summary>
        public GeoPoint2 Center { get; }

        /// <summary>
        /// Gets the radius of the arc.
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// Gets the angle the arc starts at, in radians, measured counter-clockwise from the X axis and
        /// wrapped into the range from zero to a full turn.
        /// </summary>
        public double StartAngle { get; }

        /// <summary>
        /// Gets the angle the arc sweeps through, in radians: positive counter-clockwise, negative
        /// clockwise, never zero, and never more than a whole turn either way.
        /// </summary>
        public double SweptAngle { get; }

        /// <summary>
        /// Initializes an arc running counter-clockwise from one angle to another.
        /// </summary>
        /// <param name="center">The centre of the circle the arc lies on.</param>
        /// <param name="radius">The radius; it must be positive.</param>
        /// <param name="startAngle">The angle the arc starts at, in radians.</param>
        /// <param name="endAngle">The angle the arc ends at, in radians. Equal angles mean a whole turn.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number, or an angle is not a number.</exception>
        public GeoArc2(GeoPoint2 center, double radius, double startAngle, double endAngle)
            : this(center, radius, startAngle, endAngle, false)
        {
        }

        /// <summary>
        /// Initializes an arc running from one angle to another, either way round.
        /// </summary>
        /// <param name="center">The centre of the circle the arc lies on.</param>
        /// <param name="radius">The radius; it must be positive.</param>
        /// <param name="startAngle">The angle the arc starts at, in radians.</param>
        /// <param name="endAngle">The angle the arc ends at, in radians. Equal angles mean a whole turn.</param>
        /// <param name="clockwise">true to sweep clockwise from the start angle; false to sweep counter-clockwise.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number, or an angle is not a number.</exception>
        public GeoArc2(GeoPoint2 center, double radius, double startAngle, double endAngle, bool clockwise)
        {
            RequirePositive(radius, nameof(radius));
            RequireNumber(startAngle, nameof(startAngle));
            RequireNumber(endAngle, nameof(endAngle));

            Center = center;
            Radius = radius;
            StartAngle = Wrap(startAngle);

            double sweep = clockwise ? Wrap(startAngle - endAngle) : Wrap(endAngle - startAngle);

            // Going nowhere is not an arc; coming back to where it started is a whole turn.
            if (sweep <= 0.0)
            {
                sweep = FullTurn;
            }

            SweptAngle = clockwise ? -sweep : sweep;
        }

        /// <summary>
        /// Initializes an arc from its start angle and the angle it sweeps through.
        /// </summary>
        private GeoArc2(GeoPoint2 center, double radius, double startAngle, double sweptAngle, bool clockwise, bool bySweep)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            SweptAngle = sweptAngle;
        }

        #region Factories

        /// <summary>
        /// Creates the arc through three points, using the default tolerance.
        /// </summary>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="middle">A point the arc passes through between its ends.</param>
        /// <param name="end">Where the arc ends.</param>
        /// <returns>The arc from the start through the middle to the end.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the three points share a line, so no circle passes through them.</exception>
        public static GeoArc2 FromThreePoints(GeoPoint2 start, GeoPoint2 middle, GeoPoint2 end)
        {
            return FromThreePoints(start, middle, end, Tolerance.Global);
        }

        /// <summary>
        /// Creates the arc through three points, within a tolerance.
        /// </summary>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="middle">A point the arc passes through between its ends.</param>
        /// <param name="end">Where the arc ends.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The arc from the start through the middle to the end.</returns>
        /// <remarks>
        /// Which way the arc turns follows the middle point: it sweeps the way that passes through it, so
        /// swapping the start and the end reverses the arc rather than giving the other part of the circle.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the three points share a line, so no circle passes through them.</exception>
        public static GeoArc2 FromThreePoints(GeoPoint2 start, GeoPoint2 middle, GeoPoint2 end, Tolerance tolerance)
        {
            GeoVector2 toMiddle = start.GetVectorTo(middle);
            GeoVector2 toEnd = start.GetVectorTo(end);

            double twiceArea = toMiddle.CrossProduct(toEnd);

            if (Math.Abs(twiceArea) <= tolerance.EqualPoint * tolerance.EqualPoint)
            {
                throw new InvalidOperationException("Three points on one line have no arc through them.");
            }

            // The centre is where the perpendicular bisectors of the two chords meet, written in terms of
            // the squared lengths so that it needs no division until the end.
            double middleSquared = toMiddle.LengthSquared;
            double endSquared = toEnd.LengthSquared;
            double factor = 0.5 / twiceArea;

            var offset = new GeoVector2(
                (toEnd.Y * middleSquared - toMiddle.Y * endSquared) * factor,
                (toMiddle.X * endSquared - toEnd.X * middleSquared) * factor);

            GeoPoint2 center = start.Add(offset);
            double radius = offset.Length;

            // The arc turns the way that carries it through the middle point, which is the way the three
            // points wind.
            bool clockwise = twiceArea < 0.0;

            return new GeoArc2(center, radius, AngleOf(center, start), AngleOf(center, end), clockwise);
        }

        /// <summary>
        /// Creates the arc between two points with a given bulge, the number AutoCAD stores per vertex.
        /// </summary>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="end">Where the arc ends.</param>
        /// <param name="bulge">The tangent of a quarter of the angle the arc sweeps: positive counter-clockwise, negative clockwise.</param>
        /// <returns>The arc from the start to the end.</returns>
        /// <remarks>
        /// A bulge of one is a half turn, and the sign says which way it goes. A bulge of zero would be a
        /// straight segment rather than an arc, which is why it is refused here: a chain stores that as an
        /// edge with no bulge instead.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the bulge is zero or not a number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the two points are the same, so there is no chord to bulge.</exception>
        public static GeoArc2 FromBulge(GeoPoint2 start, GeoPoint2 end, double bulge)
        {
            if (double.IsNaN(bulge) || bulge == 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(bulge), "A bulge of zero is a straight segment, not an arc.");
            }

            GeoVector2 chord = start.GetVectorTo(end);
            double chordLength = chord.Length;

            if (chordLength <= 0.0)
            {
                throw new InvalidOperationException("Two points in the same place have no chord to bulge.");
            }

            double sweep = 4.0 * Math.Atan(bulge);
            double radius = Math.Abs(chordLength / (2.0 * Math.Sin(sweep * 0.5)));

            // The centre sits off the middle of the chord, on the side the bulge leans away from.
            double apothem = Math.Sqrt(Math.Max(0.0, radius * radius - chordLength * chordLength * 0.25));
            GeoVector2 unit = chord.Multiply(1.0 / chordLength);
            GeoVector2 toCenter = new GeoVector2(-unit.Y, unit.X).Multiply(Math.Abs(sweep) > Math.PI ? -apothem : apothem);

            if (sweep < 0.0)
            {
                toCenter = toCenter.Multiply(-1.0);
            }

            GeoPoint2 middleOfChord = new GeoPoint2((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5);
            GeoPoint2 center = middleOfChord.Add(toCenter);

            return new GeoArc2(center, radius, AngleOf(center, start), sweep, sweep < 0.0, true);
        }

        #endregion

        #region Measurements

        /// <summary>
        /// Gets the angle the arc ends at, in radians, wrapped into the range from zero to a full turn.
        /// </summary>
        public double EndAngle => Wrap(StartAngle + SweptAngle);

        /// <summary>
        /// Gets a value indicating whether the arc sweeps clockwise.
        /// </summary>
        public bool IsClockwise => SweptAngle < 0.0;

        /// <summary>
        /// Gets the length of the arc.
        /// </summary>
        public double Length => Math.Abs(SweptAngle) * Radius;

        /// <summary>
        /// Gets the bulge of the arc: the tangent of a quarter of the angle it sweeps, the number AutoCAD
        /// stores per vertex, signed the same way as <see cref="SweptAngle"/>.
        /// </summary>
        public double Bulge => Math.Tan(SweptAngle * 0.25);

        /// <summary>
        /// Gets the point the arc starts at.
        /// </summary>
        public GeoPoint2 StartPoint => GetPointAtAngle(StartAngle);

        /// <summary>
        /// Gets the point the arc ends at.
        /// </summary>
        public GeoPoint2 EndPoint => GetPointAtAngle(StartAngle + SweptAngle);

        /// <summary>
        /// Gets the point halfway along the arc.
        /// </summary>
        public GeoPoint2 MidPoint => GetPointAtAngle(StartAngle + SweptAngle * 0.5);

        /// <summary>
        /// Gets the chord of the arc: the straight segment from where it starts to where it ends.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the arc sweeps a whole turn, so its ends are in the same place.</exception>
        public GeoLine2 GetChord() => new GeoLine2(StartPoint, EndPoint);

        /// <summary>
        /// Gets the circle the arc lies on.
        /// </summary>
        public GeoCircle2 GetCircle() => new GeoCircle2(Center, Radius);

        /// <summary>
        /// Gets the point of the arc at an angle, whether or not the arc reaches that far.
        /// </summary>
        /// <param name="angleRad">The angle in radians, measured counter-clockwise from the X axis.</param>
        public GeoPoint2 GetPointAtAngle(double angleRad)
        {
            return new GeoPoint2(
                Center.X + Radius * Math.Cos(angleRad),
                Center.Y + Radius * Math.Sin(angleRad));
        }

        /// <summary>
        /// Gets the point at a normalized parameter along the arc, where 0 is its start and 1 its end.
        /// </summary>
        /// <param name="parameter">The parameter. Values outside 0 to 1 run on around the circle.</param>
        public GeoPoint2 GetPointAtParameter(double parameter) => GetPointAtAngle(StartAngle + SweptAngle * parameter);

        /// <summary>
        /// Gets the point at an arc length measured from the start of the arc.
        /// </summary>
        /// <param name="distance">The length along the arc. Values outside its length run on around the circle.</param>
        public GeoPoint2 GetPointAtDistance(double distance) => GetPointAtParameter(distance / Length);

        /// <summary>
        /// Gets the normalized parameter of the point of the arc nearest a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the arc.</param>
        /// <returns>The parameter, from 0 at the start of the arc to 1 at its end, clamped to that range.</returns>
        public double GetParameterAtPoint(GeoPoint2 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the normalized parameter of the point of the arc nearest a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the arc.</param>
        /// <param name="tolerance">The tolerance: a point at the centre has no direction and gives the start.</param>
        /// <returns>The parameter, from 0 at the start of the arc to 1 at its end, clamped to that range.</returns>
        /// <remarks>
        /// A point off the ends of the arc is answered with whichever end is nearer it, so the parameter
        /// never runs outside the arc.
        /// </remarks>
        public double GetParameterAtPoint(GeoPoint2 point, Tolerance tolerance)
        {
            GeoVector2 fromCenter = Center.GetVectorTo(point);

            if (fromCenter.Length <= tolerance.EqualPoint)
            {
                return 0.0;
            }

            double angle = Math.Atan2(fromCenter.Y, fromCenter.X);
            double along = IsClockwise ? Wrap(StartAngle - angle) : Wrap(angle - StartAngle);
            double sweep = Math.Abs(SweptAngle);

            if (along <= sweep)
            {
                return along / sweep;
            }

            // Beyond the end: the far side of the circle belongs to whichever end of the arc is nearer.
            double past = along - sweep;
            double back = FullTurn - along;

            return past <= back ? 1.0 : 0.0;
        }

        /// <summary>
        /// Gets the arc length from the start of the arc to the point of it nearest a point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => GetParameterAtPoint(point) * Length;

        /// <summary>
        /// Gets the arc length from the start of the arc to the point of it nearest a point, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point, Tolerance tolerance) => GetParameterAtPoint(point, tolerance) * Length;

        /// <summary>
        /// Gets the arc length from the start of the arc to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => parameter * Length;

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start of the arc.
        /// </summary>
        public double GetParameterAtDistance(double distance) => distance / Length;

        #endregion

        #region Shaping

        /// <summary>
        /// Gets the arc running the other way, from this one's end to its start.
        /// </summary>
        public GeoArc2 Reverse() => new GeoArc2(Center, Radius, EndAngle, -SweptAngle, !IsClockwise, true);

        /// <summary>
        /// Creates a copy of this arc.
        /// </summary>
        /// <remarks>
        /// Arc is a readonly struct, so plain assignment already produces an independent copy and this
        /// method is not needed to avoid sharing. It exists so that every geometry type offers the same way
        /// to ask for a copy.
        /// </remarks>
        public GeoArc2 Clone() => new GeoArc2(Center, Radius, StartAngle, SweptAngle, IsClockwise, true);

        /// <summary>
        /// Moves the arc by a vector.
        /// </summary>
        public GeoArc2 Translate(GeoVector2 vector) => new GeoArc2(Center.Add(vector), Radius, StartAngle, SweptAngle, IsClockwise, true);

        /// <summary>
        /// Turns the arc about a point.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise.</param>
        /// <param name="center">The point to turn about.</param>
        public GeoArc2 RotateBy(double angleRad, GeoPoint2 center)
        {
            GeoVector2 fromCenter = center.GetVectorTo(Center).RotateBy(angleRad);

            return new GeoArc2(center.Add(fromCenter), Radius, StartAngle + angleRad, SweptAngle, IsClockwise, true);
        }

        /// <summary>
        /// Applies a transformation to this arc.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed arc.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation would stretch the arc into part of an ellipse.</exception>
        /// <remarks>
        /// An arc survives a transformation only when every direction is stretched by the same amount, just
        /// as a circle does. A transformation that turns shapes round, a mirror or a negative scaling, also
        /// reverses the way the arc sweeps.
        /// </remarks>
        public GeoArc2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoVector2 alongX = transform.Transform(GeoVector2.XAxis);
            GeoVector2 alongY = transform.Transform(GeoVector2.YAxis);

            if (Math.Abs(alongX.Length - alongY.Length) > Tolerance.Global.EqualVector)
            {
                throw new InvalidOperationException("This transformation stretches the arc into part of an ellipse.");
            }

            GeoPoint2 center = transform.Transform(Center);
            GeoPoint2 start = transform.Transform(StartPoint);
            GeoPoint2 middle = transform.Transform(MidPoint);

            bool mirrored = transform.GetDeterminant() < 0.0;
            double sweep = mirrored ? -SweptAngle : SweptAngle;

            return new GeoArc2(center, Radius * alongX.Length, AngleOf(center, start), sweep, sweep < 0.0, true)
                .WithMiddleThrough(middle);
        }

        /// <summary>
        /// Returns this arc, or the one sweeping the other way, whichever passes nearer a point.
        /// </summary>
        private GeoArc2 WithMiddleThrough(GeoPoint2 middle)
        {
            GeoArc2 other = new GeoArc2(Center, Radius, StartAngle, -Math.Sign(SweptAngle) * (FullTurn - Math.Abs(SweptAngle)), SweptAngle > 0.0, true);

            return MidPoint.GetDistanceSquaredTo(middle) <= other.MidPoint.GetDistanceSquaredTo(middle) ? this : other;
        }

        /// <summary>
        /// Gets the arc concentric with this one at a distance from it, using the default tolerance.
        /// </summary>
        /// <param name="distance">How far to move it: outward from the centre when positive, inward when negative.</param>
        /// <param name="result">The offset arc, or this arc when the method returns false.</param>
        /// <returns>true if the offset arc has a positive radius; otherwise, false.</returns>
        public bool TryOffset(double distance, out GeoArc2 result) => TryOffset(distance, out result, Tolerance.Global);

        /// <summary>
        /// Gets the arc concentric with this one at a distance from it, within a tolerance.
        /// </summary>
        /// <param name="distance">How far to move it: outward from the centre when positive, inward when negative.</param>
        /// <param name="result">The offset arc, or this arc when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the offset arc has a positive radius; otherwise, false.</returns>
        /// <remarks>
        /// The offset of an arc is exactly an arc: same centre, same angles, a different radius. An offset
        /// inward by more than the radius would turn it inside out, which is refused rather than answered.
        /// </remarks>
        public bool TryOffset(double distance, out GeoArc2 result, Tolerance tolerance)
        {
            double radius = Radius + distance;

            if (radius <= tolerance.EqualPoint)
            {
                result = this;
                return false;
            }

            result = new GeoArc2(Center, radius, StartAngle, SweptAngle, IsClockwise, true);
            return true;
        }

        #endregion

        #region Approximating by straight pieces

        /// <summary>
        /// Approximates the arc as a chain, cut finely enough that it strays no further from the arc than
        /// the automatic share of its radius.
        /// </summary>
        /// <returns>A chain from the start of the arc to its end.</returns>
        public GeoPolyline2 ToPolyline() => ToPolylineByChordTolerance(0.0);

        /// <summary>
        /// Approximates the arc as a chain that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc, in drawing units. Zero picks the automatic share of the radius.</param>
        /// <returns>A chain from the start of the arc to its end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public GeoPolyline2 ToPolylineByChordTolerance(double chordTolerance)
        {
            return ToPolyline(Internal.Tessellation.SegmentsForChordTolerance(Radius, Math.Abs(SweptAngle), chordTolerance));
        }

        /// <summary>
        /// Approximates the arc as a chain whose points are no further apart than a spacing.
        /// </summary>
        /// <param name="spacing">The largest distance allowed between two points, measured along the arc.</param>
        /// <returns>A chain from the start of the arc to its end, its points evenly spread.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolyline2 ToPolylineBySpacing(double spacing)
        {
            return ToPolyline(Internal.Tessellation.SegmentsForSpacing(Length, Math.Abs(SweptAngle), spacing));
        }

        /// <summary>
        /// Approximates the arc as a chain with a given number of pieces.
        /// </summary>
        /// <param name="segmentCount">How many pieces the chain should have; at least one.</param>
        /// <returns>A chain from the start of the arc to its end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than one piece is asked for.</exception>
        public GeoPolyline2 ToPolyline(int segmentCount)
        {
            Internal.Tessellation.RequireSegmentCount(segmentCount, 1);

            GeoPoint2[] points = new GeoPoint2[segmentCount + 1];

            for (int i = 0; i <= segmentCount; i++)
            {
                points[i] = GetPointAtParameter((double)i / segmentCount);
            }

            return new GeoPolyline2(points);
        }

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another arc holds exactly the same centre, radius and angles.
        /// </summary>
        public bool Equals(GeoArc2 other)
        {
            return Center.Equals(other.Center)
                && Radius.Equals(other.Radius)
                && StartAngle.Equals(other.StartAngle)
                && SweptAngle.Equals(other.SweptAngle);
        }

        /// <summary>
        /// Determines whether the specified object is an equal arc.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoArc2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this arc.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = hash * 397 ^ Radius.GetHashCode();
                hash = hash * 397 ^ StartAngle.GetHashCode();
                hash = hash * 397 ^ SweptAngle.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Determines whether another arc draws the same curve, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoArc2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another arc draws the same curve, within a tolerance.
        /// </summary>
        /// <param name="other">The arc to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the arcs are the same within tolerance; otherwise, false.</returns>
        /// <remarks>
        /// An arc and the same arc reversed are not equal: which way a curve runs is part of it, as it is
        /// for a segment and for a chain.
        /// </remarks>
        public bool IsEqualTo(GeoArc2 other, Tolerance tolerance)
        {
            return Center.IsEqualTo(other.Center, tolerance)
                && Math.Abs(Radius - other.Radius) <= tolerance.EqualPoint
                && StartPoint.IsEqualTo(other.StartPoint, tolerance)
                && EndPoint.IsEqualTo(other.EndPoint, tolerance)
                && Math.Sign(SweptAngle) == Math.Sign(other.SweptAngle);
        }

        /// <summary>
        /// Determines whether two arcs hold exactly the same values.
        /// </summary>
        public static bool operator ==(GeoArc2 left, GeoArc2 right) => left.Equals(right);

        /// <summary>
        /// Determines whether two arcs hold different values.
        /// </summary>
        public static bool operator !=(GeoArc2 left, GeoArc2 right) => !left.Equals(right);

        /// <summary>
        /// Describes the arc.
        /// </summary>
        public override string ToString()
        {
            return $"GeoArc2[Center:{Center}, R:{Radius:0.###}, {StartAngle * 180.0 / Math.PI:0.#}° sweep {SweptAngle * 180.0 / Math.PI:0.#}°]";
        }

        #endregion

        #region Helpers

        private static double AngleOf(GeoPoint2 center, GeoPoint2 point)
        {
            GeoVector2 fromCenter = center.GetVectorTo(point);

            return Math.Atan2(fromCenter.Y, fromCenter.X);
        }

        private static double Wrap(double angle)
        {
            double wrapped = angle % FullTurn;

            return wrapped < 0.0 ? wrapped + FullTurn : wrapped;
        }

        private static void RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "An arc must have a positive radius.");
            }
        }

        private static void RequireNumber(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "An angle must be a number.");
            }
        }

        #endregion

        #region Operations

        /// <summary>
        /// Determines whether a point lies on this arc.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => Arc2.IsPointOn(this, point);

        /// <summary>
        /// Determines whether a point lies on this arc, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => Arc2.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Says where a point sits relative to this arc; a curve never answers Inside.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Arc2.Locate(this, point);

        /// <summary>
        /// Says where a point sits relative to this arc, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Arc2.Locate(this, point, tolerance);

        /// <summary>
        /// Splits this arc at a normalized parameter.
        /// </summary>
        public bool TrySplitAt(double parameter, out GeoArc2[] pieces) => Arc2.TrySplitAt(this, parameter, out pieces);

        /// <summary>
        /// Splits this arc at a normalized parameter, within a tolerance.
        /// </summary>
        public bool TrySplitAt(double parameter, out GeoArc2[] pieces, Tolerance tolerance) => Arc2.TrySplitAt(this, parameter, out pieces, tolerance);

        /// <summary>
        /// Splits this arc at the point of it nearest a point.
        /// </summary>
        public bool TrySplitAt(GeoPoint2 point, out GeoArc2[] pieces) => Arc2.TrySplitAt(this, point, out pieces);

        /// <summary>
        /// Splits this arc at the point of it nearest a point, within a tolerance.
        /// </summary>
        public bool TrySplitAt(GeoPoint2 point, out GeoArc2[] pieces, Tolerance tolerance) => Arc2.TrySplitAt(this, point, out pieces, tolerance);

        #endregion
    }
}
