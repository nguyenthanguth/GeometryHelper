using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a circular arc in space: part of a circle lying in a plane, running from one angle to
    /// another.
    /// <para>
    /// It is the counterpart of <see cref="GeoArc2"/> and holds the same things, with a normal for the
    /// plane it lies in. Angles are measured counter-clockwise around that normal from the reference
    /// direction the plane supplies, as <see cref="GeoCircle3"/> measures them: a circle in space has no
    /// natural place to start, so the zero angle sits on the first of the two axes
    /// <see cref="GeoPlane3.GetAxes"/> returns.
    /// </para>
    /// </summary>
    public readonly partial struct GeoArc3 : IEquatable<GeoArc3>
    {
        private const double FullTurn = Math.PI * 2.0;

        /// <summary>
        /// Gets the centre of the circle the arc lies on.
        /// </summary>
        public GeoPoint3 Center { get; }

        /// <summary>
        /// Gets the unit normal of the plane carrying the arc.
        /// </summary>
        public GeoVector3 Normal { get; }

        /// <summary>
        /// Gets the radius of the arc.
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// Gets the angle the arc starts at, in radians, wrapped into the range from zero to a full turn.
        /// </summary>
        public double StartAngle { get; }

        /// <summary>
        /// Gets the angle the arc sweeps through, in radians: positive counter-clockwise around the normal,
        /// negative clockwise, never zero, and never more than a whole turn either way.
        /// </summary>
        public double SweptAngle { get; }

        /// <summary>
        /// Initializes an arc running counter-clockwise around its normal from one angle to another.
        /// </summary>
        /// <param name="center">The centre of the circle the arc lies on.</param>
        /// <param name="normal">The normal of the carrying plane; it is normalized on construction.</param>
        /// <param name="radius">The radius; it must be positive.</param>
        /// <param name="startAngle">The angle the arc starts at, in radians.</param>
        /// <param name="endAngle">The angle the arc ends at, in radians. Equal angles mean a whole turn.</param>
        /// <exception cref="ArgumentException">Thrown when the normal has no length.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number, or an angle is not a number.</exception>
        public GeoArc3(GeoPoint3 center, GeoVector3 normal, double radius, double startAngle, double endAngle)
            : this(center, normal, radius, startAngle, endAngle, false)
        {
        }

        /// <summary>
        /// Initializes an arc running from one angle to another, either way round its normal.
        /// </summary>
        /// <param name="center">The centre of the circle the arc lies on.</param>
        /// <param name="normal">The normal of the carrying plane; it is normalized on construction.</param>
        /// <param name="radius">The radius; it must be positive.</param>
        /// <param name="startAngle">The angle the arc starts at, in radians.</param>
        /// <param name="endAngle">The angle the arc ends at, in radians. Equal angles mean a whole turn.</param>
        /// <param name="clockwise">true to sweep clockwise around the normal; false to sweep counter-clockwise.</param>
        /// <exception cref="ArgumentException">Thrown when the normal has no length.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number, or an angle is not a number.</exception>
        public GeoArc3(GeoPoint3 center, GeoVector3 normal, double radius, double startAngle, double endAngle, bool clockwise)
        {
            if (!normal.TryGetNormal(out GeoVector3 unit))
            {
                throw new ArgumentException("An arc needs a normal of non-zero length.", nameof(normal));
            }

            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "An arc must have a positive radius.");
            }

            if (double.IsNaN(startAngle) || double.IsInfinity(startAngle) || double.IsNaN(endAngle) || double.IsInfinity(endAngle))
            {
                throw new ArgumentOutOfRangeException(nameof(startAngle), "An angle must be a number.");
            }

            Center = center;
            Normal = unit;
            Radius = radius;
            StartAngle = Wrap(startAngle);

            double sweep = clockwise ? Wrap(startAngle - endAngle) : Wrap(endAngle - startAngle);

            if (sweep <= 0.0)
            {
                sweep = FullTurn;
            }

            SweptAngle = clockwise ? -sweep : sweep;
        }

        /// <summary>
        /// Initializes an arc from a normal that is already normalized and a sweep that is already signed.
        /// </summary>
        /// <remarks>
        /// Re-normalizing a unit vector shifts its last digits rather than leaving it alone, so a copy taken
        /// through the public constructor would come back unequal to its original, and a sweep already
        /// worked out would be worked out again from two angles that cannot tell a whole turn from none.
        /// </remarks>
        private GeoArc3(GeoPoint3 center, GeoVector3 unitNormal, double radius, double startAngle, double sweptAngle, bool clockwise, bool bySweep)
        {
            Center = center;
            Normal = unitNormal;
            Radius = radius;
            StartAngle = startAngle;
            SweptAngle = sweptAngle;
        }

        #region Factories

        /// <summary>
        /// Creates the arc through three points in space, using the default tolerance.
        /// </summary>
        public static GeoArc3 FromThreePoints(GeoPoint3 start, GeoPoint3 middle, GeoPoint3 end)
        {
            return FromThreePoints(start, middle, end, Tolerance.Global);
        }

        /// <summary>
        /// Creates the arc through three points in space, within a tolerance.
        /// </summary>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="middle">A point the arc passes through between its ends.</param>
        /// <param name="end">Where the arc ends.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The arc from the start through the middle to the end, in the plane the three points share.</returns>
        /// <remarks>
        /// Three points that do not share a line settle the plane as well as the arc, so nothing else has
        /// to be given. The work is done in that plane by <see cref="GeoArc2.FromThreePoints(GeoPoint2, GeoPoint2, GeoPoint2, Tolerance)"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the three points share a line, so no circle passes through them.</exception>
        public static GeoArc3 FromThreePoints(GeoPoint3 start, GeoPoint3 middle, GeoPoint3 end, Tolerance tolerance)
        {
            GeoVector3 toMiddle = start.GetVectorTo(middle);
            GeoVector3 toEnd = start.GetVectorTo(end);
            GeoVector3 normal = toMiddle.CrossProduct(toEnd);

            if (!normal.TryGetNormal(out GeoVector3 unit))
            {
                throw new InvalidOperationException("Three points on one line have no arc through them.");
            }

            var frame = new GeoCoordinateSystem3(new GeoPlane3(start, unit));

            GeoArc2 flat = GeoArc2.FromThreePoints(
                ToLocal(frame, start), ToLocal(frame, middle), ToLocal(frame, end), tolerance);

            GeoPoint3 center = frame.ToGlobal(new GeoPoint3(flat.Center.X, flat.Center.Y, 0.0));

            // The plane of the arc is the plane of the three points; its own frame then fixes where angles
            // are measured from, which need not be the frame used for the flat solve.
            GeoPoint3 startOnArc = frame.ToGlobal(new GeoPoint3(flat.StartPoint.X, flat.StartPoint.Y, 0.0));

            return new GeoArc3(
                center,
                unit,
                flat.Radius,
                AngleOf(center, unit, startOnArc),
                flat.SweptAngle,
                flat.IsClockwise,
                true);
        }

        #endregion

        #region Measurements

        /// <summary>
        /// Gets the angle the arc ends at, in radians, wrapped into the range from zero to a full turn.
        /// </summary>
        public double EndAngle => Wrap(StartAngle + SweptAngle);

        /// <summary>
        /// Gets a value indicating whether the arc sweeps clockwise around its normal.
        /// </summary>
        public bool IsClockwise => SweptAngle < 0.0;

        /// <summary>
        /// Gets the length of the arc.
        /// </summary>
        public double Length => Math.Abs(SweptAngle) * Radius;

        /// <summary>
        /// Gets the point the arc starts at.
        /// </summary>
        public GeoPoint3 StartPoint => GetPointAtAngle(StartAngle);

        /// <summary>
        /// Gets the point the arc ends at.
        /// </summary>
        public GeoPoint3 EndPoint => GetPointAtAngle(StartAngle + SweptAngle);

        /// <summary>
        /// Gets the point halfway along the arc.
        /// </summary>
        public GeoPoint3 MidPoint => GetPointAtAngle(StartAngle + SweptAngle * 0.5);

        /// <summary>
        /// Gets the plane carrying the arc.
        /// </summary>
        public GeoPlane3 GetPlane() => new GeoPlane3(Center, Normal);

        /// <summary>
        /// Gets the circle the arc lies on.
        /// </summary>
        public GeoCircle3 GetCircle() => new GeoCircle3(Center, Normal, Radius);

        /// <summary>
        /// Gets the chord of the arc: the straight segment from where it starts to where it ends.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the arc sweeps a whole turn, so its ends are in the same place.</exception>
        public GeoLine3 GetChord() => new GeoLine3(StartPoint, EndPoint);

        /// <summary>
        /// Gets the point of the arc at an angle, whether or not the arc reaches that far.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise around the normal.</param>
        public GeoPoint3 GetPointAtAngle(double angleRad)
        {
            GetPlane().GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            return Center
                .Add(uAxis.Multiply(Radius * Math.Cos(angleRad)))
                .Add(vAxis.Multiply(Radius * Math.Sin(angleRad)));
        }

        /// <summary>
        /// Gets the point at a normalized parameter along the arc, where 0 is its start and 1 its end.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter) => GetPointAtAngle(StartAngle + SweptAngle * parameter);

        /// <summary>
        /// Gets the point at an arc length measured from the start of the arc.
        /// </summary>
        public GeoPoint3 GetPointAtDistance(double distance) => GetPointAtParameter(distance / Length);

        /// <summary>
        /// Gets the normalized parameter of the point of the arc nearest a point, using the default tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the normalized parameter of the point of the arc nearest a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the arc or even in its plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The parameter, from 0 at the start of the arc to 1 at its end, clamped to that range.</returns>
        /// <remarks>
        /// A point off the plane is answered by what stands under it: only the direction from the centre
        /// within the plane decides where along the arc it falls.
        /// </remarks>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance)
        {
            return ToFlat().GetParameterAtPoint(ToLocal(GetFrame(), point), tolerance);
        }

        /// <summary>
        /// Gets the arc length from the start of the arc to the point of it nearest a point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint3 point) => GetParameterAtPoint(point) * Length;

        /// <summary>
        /// Gets the arc length from the start of the arc to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => parameter * Length;

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start of the arc.
        /// </summary>
        public double GetParameterAtDistance(double distance) => distance / Length;

        #endregion

        #region Operations

        /// <summary>
        /// Determines whether a point lies on this arc.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on this arc, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Says where a point sits relative to this arc; a curve never answers Inside.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Says where a point sits relative to this arc, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => IsPointOn(point, tolerance) ? PointLocation.OnSide : PointLocation.OutSide;

        /// <summary>
        /// Determines whether the arc reaches a given angle.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise around the normal.</param>
        /// <returns>true if the angle lies between the start of the arc and its end, the way it sweeps.</returns>
        public bool CoversAngle(double angleRad)
        {
            double along = IsClockwise ? Wrap(StartAngle - angleRad) : Wrap(angleRad - StartAngle);

            return along <= Math.Abs(SweptAngle);
        }

        /// <summary>
        /// Gets the axis-aligned box enclosing the arc.
        /// </summary>
        /// <returns>The smallest world-aligned box the arc fits in.</returns>
        /// <remarks>
        /// The box holds the arc itself rather than the whole circle: along each world axis the arc reaches
        /// its furthest either at one of its ends or where it turns back, and the turning point counts only
        /// when the arc sweeps that far.
        /// </remarks>
        public GeoAabb3 GetAabb()
        {
            GetPlane().GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            double[] low = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] high = { double.MinValue, double.MinValue, double.MinValue };

            void Include(GeoPoint3 point)
            {
                double[] at = { point.X, point.Y, point.Z };

                for (int axis = 0; axis < 3; axis++)
                {
                    low[axis] = Math.Min(low[axis], at[axis]);
                    high[axis] = Math.Max(high[axis], at[axis]);
                }
            }

            Include(StartPoint);
            Include(EndPoint);

            double[] u = { uAxis.X, uAxis.Y, uAxis.Z };
            double[] v = { vAxis.X, vAxis.Y, vAxis.Z };

            for (int axis = 0; axis < 3; axis++)
            {
                // Along one world axis the arc reads C + r(u cos t + v sin t), which turns back where its
                // derivative vanishes: at atan2(v, u) and half a turn from there.
                double turning = Math.Atan2(v[axis], u[axis]);

                foreach (double angle in new[] { turning, turning + Math.PI })
                {
                    if (CoversAngle(angle))
                    {
                        Include(GetPointAtAngle(angle));
                    }
                }
            }

            return new GeoAabb3(
                new GeoPoint3(low[0], low[1], low[2]),
                new GeoPoint3(high[0], high[1], high[2]));
        }

        /// <summary>
        /// Gets the arc running the other way, from this one's end to its start.
        /// </summary>
        public GeoArc3 Reverse() => new GeoArc3(Center, Normal, Radius, EndAngle, -SweptAngle, !IsClockwise, true);

        /// <summary>
        /// Creates a copy of this arc.
        /// </summary>
        public GeoArc3 Clone() => new GeoArc3(Center, Normal, Radius, StartAngle, SweptAngle, IsClockwise, true);

        /// <summary>
        /// Gets the arc concentric with this one at a distance from it, using the default tolerance.
        /// </summary>
        public bool TryOffset(double distance, out GeoArc3 result) => TryOffset(distance, out result, Tolerance.Global);

        /// <summary>
        /// Gets the arc concentric with this one at a distance from it, within a tolerance.
        /// </summary>
        /// <param name="distance">How far to move it: outward from the centre when positive, inward when negative.</param>
        /// <param name="result">The offset arc, or this arc when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the offset arc has a positive radius; otherwise, false.</returns>
        public bool TryOffset(double distance, out GeoArc3 result, Tolerance tolerance)
        {
            double radius = Radius + distance;

            if (radius <= tolerance.EqualPoint)
            {
                result = this;
                return false;
            }

            result = new GeoArc3(Center, Normal, radius, StartAngle, SweptAngle, IsClockwise, true);
            return true;
        }

        /// <summary>
        /// Moves the arc by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The arc in its new place.</returns>
        public GeoArc3 Translate(GeoVector3 vector) => new GeoArc3(Center.Add(vector), Normal, Radius, StartAngle, StartAngle + SweptAngle);

        /// <summary>
        /// Applies a transformation to this arc.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed arc.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation would stretch the arc into part of an ellipse.</exception>
        public GeoArc3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            // A circle survives only under an even stretch, and the circle test carries the arc with it.
            GeoCircle3 moved = transform.Transform(GetCircle());

            GeoPoint3 start = transform.Transform(StartPoint);
            GeoPoint3 middle = transform.Transform(MidPoint);
            GeoPoint3 end = transform.Transform(EndPoint);

            return FromThreePoints(start, middle, end);
        }

        /// <summary>
        /// Lays this arc out in the plane of a frame, dropping each point's distance from that plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The arc in the plane of the frame.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the arc stands square to the plane, where it would flatten into a segment rather than an arc.</exception>
        /// <remarks>
        /// A whole turn has to be laid out from its centre rather than from three of its points, because its
        /// start and its end are in the same place and there is no third point to be found among them. Any
        /// pair of angles describes the whole of a circle, so nought to nought is as good as any.
        /// </remarks>
        public GeoArc2 ProjectToArc2(GeoCoordinateSystem3 frame)
        {
            if (Math.Abs(SweptAngle) >= 2.0 * Math.PI - Tolerance.Global.EqualAngleRad)
            {
                return new GeoArc2(ToLocal(frame, Center), Radius, 0.0, 0.0, IsClockwise);
            }

            return GeoArc2.FromThreePoints(ToLocal(frame, StartPoint), ToLocal(frame, MidPoint), ToLocal(frame, EndPoint));
        }

        #endregion

        #region Approximating by straight pieces

        /// <summary>
        /// Approximates the arc as a chain, cut by the automatic chord tolerance.
        /// </summary>
        public GeoPolyline3 ToPolyline() => ToPolylineByChordTolerance(0.0);

        /// <summary>
        /// Approximates the arc as a chain that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc, in drawing units. Zero picks the automatic share of the radius.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number.</exception>
        public GeoPolyline3 ToPolylineByChordTolerance(double chordTolerance)
        {
            return ToPolyline(Internal.Tessellation.SegmentsForChordTolerance(Radius, Math.Abs(SweptAngle), chordTolerance));
        }

        /// <summary>
        /// Approximates the arc as a chain whose points are no further apart than a spacing.
        /// </summary>
        /// <param name="spacing">The largest distance allowed between two points, measured along the arc.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolyline3 ToPolylineBySpacing(double spacing)
        {
            return ToPolyline(Internal.Tessellation.SegmentsForSpacing(Length, Math.Abs(SweptAngle), spacing));
        }

        /// <summary>
        /// Approximates the arc as a chain with a given number of pieces.
        /// </summary>
        /// <param name="segmentCount">How many pieces the chain should have; at least one.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than one piece is asked for.</exception>
        public GeoPolyline3 ToPolyline(int segmentCount)
        {
            Internal.Tessellation.RequireSegmentCount(segmentCount, 1);

            GeoPoint3[] points = new GeoPoint3[segmentCount + 1];

            for (int i = 0; i <= segmentCount; i++)
            {
                points[i] = GetPointAtParameter((double)i / segmentCount);
            }

            return new GeoPolyline3(points);
        }

        #endregion

        #region Extending and trimming

        /// <summary>
        /// Lengthens the arc along itself at one end, keeping its centre and radius.
        /// </summary>
        /// <remarks>
        /// The distance is arc length and not chord, which is the only reading that leaves the curve where it
        /// was. A negative distance takes length away, and an extension past a whole turn is refused.
        /// </remarks>
        public GeoArc3 Extend(double distance, LineEnd end) => Lengthen3.Extend(this, distance, end);

        /// <summary>
        /// Lengthens the arc along itself at one end, within a tolerance.
        /// </summary>
        public GeoArc3 Extend(double distance, LineEnd end, Tolerance tolerance) => Lengthen3.Extend(this, distance, end, tolerance);

        /// <summary>
        /// Lengthens the arc along itself at both ends at once.
        /// </summary>
        public GeoArc3 Extend(double startDistance, double endDistance) => Lengthen3.Extend(this, startDistance, endDistance);

        /// <summary>
        /// Lengthens the arc along itself at both ends at once, within a tolerance.
        /// </summary>
        public GeoArc3 Extend(double startDistance, double endDistance, Tolerance tolerance)
            => Lengthen3.Extend(this, startDistance, endDistance, tolerance);

        /// <summary>
        /// Lengthens or shortens the arc at one end until it is a given arc length.
        /// </summary>
        public GeoArc3 ExtendToLength(double length, LineEnd end) => Lengthen3.ExtendToLength(this, length, end);

        /// <summary>
        /// Lengthens or shortens the arc at one end until it is a given arc length, within a tolerance.
        /// </summary>
        public GeoArc3 ExtendToLength(double length, LineEnd end, Tolerance tolerance) => Lengthen3.ExtendToLength(this, length, end, tolerance);

        /// <summary>
        /// Lengthens the arc at one end until it reaches a point on its own circle.
        /// </summary>
        public bool TryExtendTo(GeoPoint3 point, LineEnd end, out GeoArc3 result) => Lengthen3.TryExtendTo(this, point, end, out result);

        /// <summary>
        /// Lengthens the arc at one end until it reaches a point on its own circle, within a tolerance.
        /// </summary>
        public bool TryExtendTo(GeoPoint3 point, LineEnd end, out GeoArc3 result, Tolerance tolerance)
            => Lengthen3.TryExtendTo(this, point, end, out result, tolerance);

        /// <summary>
        /// Shortens the arc at one end back to a point on it.
        /// </summary>
        public bool TryTrimTo(GeoPoint3 point, LineEnd end, out GeoArc3 result) => Lengthen3.TryTrimTo(this, point, end, out result);

        /// <summary>
        /// Shortens the arc at one end back to a point on it, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPoint3 point, LineEnd end, out GeoArc3 result, Tolerance tolerance)
            => Lengthen3.TryTrimTo(this, point, end, out result, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another arc holds exactly the same values.
        /// </summary>
        public bool Equals(GeoArc3 other)
        {
            return Center.Equals(other.Center)
                && Normal.Equals(other.Normal)
                && Radius.Equals(other.Radius)
                && StartAngle.Equals(other.StartAngle)
                && SweptAngle.Equals(other.SweptAngle);
        }

        /// <summary>
        /// Determines whether the specified object is an equal arc.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoArc3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this arc.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = hash * 397 ^ Normal.GetHashCode();
                hash = hash * 397 ^ Radius.GetHashCode();
                hash = hash * 397 ^ StartAngle.GetHashCode();
                hash = hash * 397 ^ SweptAngle.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Determines whether another arc draws the same curve, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoArc3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another arc draws the same curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// An arc and the same arc reversed are not equal: which way a curve runs is part of it.
        /// </remarks>
        public bool IsEqualTo(GeoArc3 other, Tolerance tolerance)
        {
            return Center.IsEqualTo(other.Center, tolerance)
                && Math.Abs(Radius - other.Radius) <= tolerance.EqualPoint
                && StartPoint.IsEqualTo(other.StartPoint, tolerance)
                && EndPoint.IsEqualTo(other.EndPoint, tolerance)
                && MidPoint.IsEqualTo(other.MidPoint, tolerance);
        }

        /// <summary>
        /// Determines whether two arcs hold exactly the same values.
        /// </summary>
        public static bool operator ==(GeoArc3 left, GeoArc3 right) => left.Equals(right);

        /// <summary>
        /// Determines whether two arcs hold different values.
        /// </summary>
        public static bool operator !=(GeoArc3 left, GeoArc3 right) => !left.Equals(right);

        /// <summary>
        /// Describes the arc.
        /// </summary>
        public override string ToString()
        {
            return $"GeoArc3[Center:{Center}, R:{Radius:0.###}, {StartAngle * 180.0 / Math.PI:0.#}° sweep {SweptAngle * 180.0 / Math.PI:0.#}°]";
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the frame the arc measures its angles in: the plane of the arc with its own axes.
        /// </summary>
        private GeoCoordinateSystem3 GetFrame() => new GeoCoordinateSystem3(GetPlane());

        /// <summary>
        /// Gets this arc as it stands in the plane of its own frame.
        /// </summary>
        private GeoArc2 ToFlat() => new GeoArc2(GeoPoint2.Origin, Radius, StartAngle, StartAngle + SweptAngle, IsClockwise);

        private static double AngleOf(GeoPoint3 center, GeoVector3 unitNormal, GeoPoint3 pointOnCircle)
        {
            GeoPoint2 flat = ToLocal(new GeoCoordinateSystem3(new GeoPlane3(center, unitNormal)), pointOnCircle);

            return Math.Atan2(flat.Y, flat.X);
        }

        private static GeoPoint2 ToLocal(GeoCoordinateSystem3 frame, GeoPoint3 point)
        {
            GeoPoint3 local = frame.ToLocal(point);

            return new GeoPoint2(local.X, local.Y);
        }

        private static double Wrap(double angle)
        {
            double wrapped = angle % FullTurn;

            return wrapped < 0.0 ? wrapped + FullTurn : wrapped;
        }

        #endregion
    }
}
