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

        /// <summary>
        /// Creates the arc of a circle already known, running counter-clockwise round its normal from a point on it.
        /// </summary>
        /// <param name="center">The centre.</param>
        /// <param name="unitNormal">A unit normal; the arc runs counter-clockwise round it when the sweep is positive.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="start">The point the arc starts at, on the circle.</param>
        /// <param name="sweptAngle">The signed sweep, never nought.</param>
        /// <returns>The arc, its normal turned over where the sweep is negative so that it runs counter-clockwise.</returns>
        /// <remarks>
        /// Nothing is solved: an arc a hundredth long that bows a thousandth of its chord off it is built as exactly as
        /// a half circle, where the three points of <see cref="FromThreePoints(GeoPoint3, GeoPoint3, GeoPoint3, Tolerance)"/>
        /// would lie within a point tolerance of one line. The arc comes out as the three-point build gives it.
        /// </remarks>
        internal static GeoArc3 FromCircle(GeoPoint3 center, GeoVector3 unitNormal, double radius, GeoPoint3 start, double sweptAngle)
        {
            GeoVector3 normal = sweptAngle < 0.0 ? unitNormal.Multiply(-1.0) : unitNormal;
            double sweep = Math.Abs(sweptAngle);

            return new GeoArc3(center, normal, radius, AngleOf(center, normal, start), sweep, false, true);
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
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation would skew or stretch the arc into part of an ellipse, so that the arc through where
        /// its ends and its middle land would land further than the global point tolerance from where its points are put, or,
        /// for an arc of a whole turn, would skew or stretch it, as <see cref="GeoTransform3.Transform(GeoCircle3)"/> refuses a
        /// circle.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a skew, a transformation <see cref="GeoTransform3.Transform(GeoCircle3)"/> would refuse for a circle in the
        /// arc's plane, brings the two ends of an arc past a half turn within the global point tolerance of each other, so that
        /// no arc can be told through them and its middle.
        /// </exception>
        /// <remarks>
        /// A turn, a shift, a mirror or an even scaling of the arc's plane moves the arc whole, by its centre and its radius, so
        /// an arc of any sweep moves: the arc of radius 100 sweeping a whole turn less 1E-9 turned 0.7 radians about any axis,
        /// which threw, its ends being too close to tell a chord between. Any other transformation, a skew or a stretch of its
        /// plane, makes part of an ellipse of the arc, and the arc through where its two ends and its middle land stands for it
        /// only where it lands within the global point tolerance of where the transformation puts the rest of it, asked at
        /// every sixteenth of its sweep, and is refused otherwise: a stretch of 3E-6 moves the arc of radius 100 a whole turn
        /// less 1 at the scale it is drawn, 0.00035 off, and refuses it scaled by 1 000, where it would be 0.35 off. A skew that
        /// keeps both axes of the plane one length is judged alike: the one that sends X to (1, 0, 0) and Y to (0.6, 0.8, 0)
        /// refuses the arc of radius 100 in the plan from 0.5 to 2 radians, which went to an arc of radius 177.7 far off the
        /// ellipse it becomes, and a turn of 0.7529 radians of a stretch of 1 + 4.29E-9 and a shear of 9.28E-5, scaled by
        /// 55.57, refuses the arc of radius 9 069.6, which it took to one 9.16 off. Each point is read from the moved start
        /// across the arc, so an arc bowing a hair off its chord is judged as surely as a half circle. An arc of a whole turn has
        /// no ends to go through: it is the whole circle, and is refused under a skew, since it would be an ellipse; one past a
        /// half turn whose ends the skew brings within the global point tolerance of each other is refused too. Where the ends
        /// come that close under a transformation that carries a circle, a turn whose numbers are rounded to six places or to a
        /// float, the arc is moved whole as the whole turn is.
        /// </remarks>
        public GeoArc3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GetPlane().GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);
            GeoPoint3 start = transform.Transform(StartPoint);

            // A whole turn is the whole circle, refused under a skew or an uneven stretch as the circle is. It has no chord:
            // it is the moved circle, run the way its carried axes say, which a mirror turns over.
            if (Math.Abs(SweptAngle) >= FullTurn)
            {
                GeoCircle3 moved = transform.Transform(GetCircle());
                GeoVector3 across = transform.Transform(uAxis).CrossProduct(transform.Transform(vAxis));

                return FromCircle(moved.Center, across.Multiply(1.0 / across.Length), moved.Radius, start, SweptAngle);
            }

            // A turn, a shift, a mirror or an even scaling of its plane moves any other arc whole as well, by its centre and
            // its radius, so that an arc a hair short of a whole turn, whose ends no chord can be told between, moves too.
            GeoVector3 movedU = transform.Transform(uAxis);
            GeoVector3 movedV = transform.Transform(vAxis);

            GeoVector3 toEnd = Across(uAxis, vAxis, StartAngle, SweptAngle);

            // Past a half turn, ends within the global point tolerance of each other are a hair short of a whole turn, whose
            // chord says nothing about which way round the arc runs: such an arc is moved whole, as the whole turn is, where
            // the circle would be carried, and refused under a skew.
            bool endsMeet = Math.Abs(SweptAngle) > Math.PI && !(transform.Transform(toEnd).Length > Tolerance.Global.EqualPoint);

            if (GeoTransform3.KeepsRound(movedU, movedV) || (endsMeet && GeoTransform3.KeepsCircles(movedU, movedV, Radius)))
            {
                GeoVector3 across = movedU.CrossProduct(movedV);

                return FromCircle(transform.Transform(Center), across.Multiply(1.0 / across.Length), Radius * movedU.Length, start, SweptAngle);
            }

            // Axes of unequal length stretch the arc into part of an ellipse.
            bool stretched = !GeoTransform3.EquallyLong(movedU, movedV);

            if (endsMeet)
            {
                if (stretched)
                {
                    throw new InvalidOperationException("This transformation stretches the circle into an ellipse.");
                }

                throw new ArgumentException("This transformation brings the ends of the arc together, so no arc runs through them and its middle.", nameof(transform));
            }

            // Any other is the arc through where its ends and its middle land, skewed or stretched, where that arc lands within the
            // global point tolerance of where the transformation puts the rest of it. They are reached from the start across the
            // arc, not from the centre, so an arc bowing a thousandth of its chord off it is not three points on a line.
            GeoVector3 toMiddle = Across(uAxis, vAxis, StartAngle, SweptAngle * 0.5);

            if (!Arc3.TryBulgeThrough(transform.Transform(toEnd), transform.Transform(toMiddle), out double bulge, out GeoVector3 normal))
            {
                throw new InvalidOperationException("Three points on one line have no arc through them.");
            }

            GeoArc3 through = new GeoEdge3(start, start.Add(transform.Transform(toEnd)), bulge, normal).ToArc();

            if (!LandsWithin(transform, through))
            {
                throw new InvalidOperationException("This transformation stretches the circle into an ellipse.");
            }

            return through;
        }

        /// <summary>
        /// Determines whether this arc, moved to another, lands within the global point tolerance of where a transformation
        /// puts its points, asked at every sixteenth of its sweep.
        /// </summary>
        /// <remarks>
        /// Each point is read from the moved start, across the arc, and its distance from the moved circle is worked out from
        /// there as well, its height over the circle's plane and, in that plane, the difference of two squares over their sum,
        /// so that an arc bowing a hair off its chord, its radius far past the reach of its digits, is read as surely as a half
        /// circle, as in the plane (<see cref="GeoArc2.LandsWithin(GeoTransform2, GeoArc2)"/>).
        /// </remarks>
        internal bool LandsWithin(GeoTransform3 transform, GeoArc3 moved)
        {
            double tolerance = Tolerance.Global.EqualPoint;

            GetPlane().GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);
            GeoVector3 normal = moved.Normal;
            GeoVector3 toCentre = transform.Transform(StartPoint).GetVectorTo(moved.Center);
            toCentre = toCentre.Subtract(normal.Multiply(toCentre.DotProduct(normal)));
            double radius = toCentre.Length;

            // Read off the circle the moved arc lies on, which an arc shorter than the tolerance has as surely as any other.
            for (int i = 1; i < 16; i++)
            {
                GeoVector3 across = transform.Transform(Across(uAxis, vAxis, StartAngle, SweptAngle * (i / 16.0)));
                double height = across.DotProduct(normal);
                GeoVector3 flat = across.Subtract(normal.Multiply(height));
                double off = (flat.LengthSquared - 2.0 * flat.DotProduct(toCentre)) / (flat.Subtract(toCentre).Length + radius);

                if (Math.Sqrt(height * height + off * off) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Lays this arc out in the plane of a frame, dropping each point's distance from that plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The arc in the plane of the frame.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the arc stands square to the plane, where it would flatten into a segment rather than an arc.</exception>
        /// <remarks>
        /// <para>
        /// An arc tilted out of the plane of the frame is, seen square onto it, an elliptical arc, which has no type
        /// here. It comes back as the circular arc through the projections of its start, its middle and its end, so
        /// those three points are right and the points between them are not: an arc of radius 300 over a quarter turn,
        /// tilted 60° about the line from its centre to its start, strays up to 10.8 from its true shadow. <see cref="PlanarMap.TryToArc2(GeoCoordinateSystem3, GeoArc3, out GeoArc2)"/>
        /// refuses an arc that is not in the plane, and <see cref="GeoCircle3.ProjectToEllipse2(GeoCoordinateSystem3)"/>
        /// gives the true shadow of a whole circle.
        /// </para>
        /// <para>
        /// A whole turn has to be laid out from its centre rather than from three of its points, because its
        /// start and its end are in the same place and there is no third point to be found among them. Any
        /// pair of angles describes the whole of a circle, so nought to nought is as good as any. Only an arc that
        /// sweeps a whole turn is one. An arc within a degree of one is laid out from its centre too, its start and its end
        /// being too near each other to find a circle through, but from where it starts and as far as it sweeps: the arc of
        /// radius 10 from 1 radian sweeping 359.5 degrees comes down as that arc, not as the whole circle from angle nought.
        /// An arc tilted to the frame comes down as part of an ellipse, which an arc cannot be, and one within a degree of a
        /// whole turn is laid out on its own radius about its centre, so its start, middle and end stand off where they come
        /// down: some 0.056 for that arc with its normal (0.2, 0, 1), and 0.0036 with (0.05, 0, 1).
        /// </para>
        /// </remarks>
        public GeoArc2 ProjectToArc2(GeoCoordinateSystem3 frame)
        {
            // A whole turn is one the arc sweeps, as TransformBy reads it.
            if (Math.Abs(SweptAngle) >= FullTurn)
            {
                return new GeoArc2(ToLocal(frame, Center), Radius, 0.0, 0.0, IsClockwise);
            }

            GeoPoint2 start = ToLocal(frame, StartPoint);

            // Within a degree of one, the start and the end are too near each other to find a circle through them and the
            // middle, and the arc is laid out from its centre, as a whole turn is, from where it starts and as far as it sweeps.
            if (Math.Abs(SweptAngle) >= FullTurn - Tolerance.Global.EqualAngleRad)
            {
                GeoPoint2 centre = ToLocal(frame, Center);
                double from = Math.Atan2(start.Y - centre.Y, start.X - centre.X);
                double sweep = Normal.DotProduct(frame.ZAxis) < 0.0 ? -SweptAngle : SweptAngle;

                return new GeoArc2(centre, Radius, from, from + sweep, sweep < 0.0);
            }

            GeoPoint2 middle = ToLocal(frame, MidPoint);
            GeoPoint2 end = ToLocal(frame, EndPoint);

            // The three points refuse an arc when they lie within the point tolerance of one line, the test
            // GeoArc2.FromThreePoints makes. Standing square to the plane, the arc flattens into a segment and is
            // refused; lying in it, it only bows less than the tolerance, an arc 0.01 long bowing a thousandth of its
            // chord, and is laid out from its two ends and its sweep, which nothing about its bow can upset.
            double twiceArea = start.GetVectorTo(middle).CrossProduct(start.GetVectorTo(end));
            double point = Tolerance.Global.EqualPoint;

            if (Math.Abs(twiceArea) > point * point || !Normal.IsParallelTo(frame.ZAxis))
            {
                return GeoArc2.FromThreePoints(start, middle, end);
            }

            double sweepInFrame = Normal.DotProduct(frame.ZAxis) > 0.0 ? SweptAngle : -SweptAngle;

            return GeoArc2.FromBulge(start, end, Math.Tan(sweepInFrame * 0.25));
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
        /// <remarks>
        /// The promise holds however fine the tolerance and however large the radius: a quarter turn of radius 50 000 cut
        /// within 1E-4 is 12 419 pieces, not the 1 024 that 4 096 pieces a turn would allow. An arc that the tolerance would
        /// cut into more than 1 048 576 pieces is refused. Points closer together than the point tolerance are merged by the
        /// chain, so chords shorter than that are not kept, as for <see cref="GeoPolylineArc2.Flatten(double)"/>.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number, or needs more than 1 048 576 pieces.</exception>
        public GeoPolyline3 ToPolylineByChordTolerance(double chordTolerance)
        {
            return ToPolyline(Internal.Tessellation.SegmentsForArc(Radius, Math.Abs(SweptAngle), chordTolerance));
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
        /// Gets whether this is a shape a constructor could have made: the centre is a valid point, the normal of unit length, the radius a positive number and the angles numbers.
        /// </summary>
        /// <remarks>
        /// A value made by a constructor always is. A <c>default</c> one — an element of a new array, or what
        /// the <c>out</c> of a <c>Try</c> method holds when the method said false — may not be, and answers
        /// questions without complaint all the same. A default one has no radius and no normal.
        /// </remarks>
        public bool IsValid => Center.IsValid && Guard.IsUnit(Normal) && Radius > 0.0 && Guard.IsFinite(Radius) && Guard.IsFinite(StartAngle) && Guard.IsFinite(SweptAngle);

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

        /// <summary>
        /// The vector from the point of the circle at one angle to the point a turn further round, worked out as the
        /// chord between them rather than as the difference of two points a radius from the centre.
        /// </summary>
        private GeoVector3 Across(GeoVector3 uAxis, GeoVector3 vAxis, double from, double turn)
        {
            double half = turn * 0.5;
            double toward = from + half;
            double chord = 2.0 * Radius * Math.Sin(half);

            return uAxis.Multiply(-chord * Math.Sin(toward)).Add(vAxis.Multiply(chord * Math.Cos(toward)));
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
        #region Cutting

        /// <summary>
        /// Cuts the arc at a normalized parameter, where 0 is its start and 1 its end.
        /// </summary>
        /// <remarks>
        /// The centre, the radius and the plane all stay; only the sweep is shared out, so the pieces put back
        /// end to end draw exactly what went in.
        /// </remarks>
        public bool TrySplitAt(double parameter, out GeoArc3[] pieces) => Arc3.TrySplitAt(this, parameter, out pieces);

        /// <summary>
        /// Cuts the arc at a normalized parameter, within a tolerance.
        /// </summary>
        public bool TrySplitAt(double parameter, out GeoArc3[] pieces, Tolerance tolerance)
            => Arc3.TrySplitAt(this, parameter, out pieces, tolerance);

        /// <summary>
        /// Cuts the arc at the point of it nearest a point.
        /// </summary>
        public bool TrySplitAt(GeoPoint3 point, out GeoArc3[] pieces) => Arc3.TrySplitAt(this, point, out pieces);

        /// <summary>
        /// Cuts the arc at the point of it nearest a point, within a tolerance.
        /// </summary>
        public bool TrySplitAt(GeoPoint3 point, out GeoArc3[] pieces, Tolerance tolerance)
            => Arc3.TrySplitAt(this, point, out pieces, tolerance);

        /// <summary>
        /// Cuts the arc at an arc length measured from its start.
        /// </summary>
        public bool TrySplitAtDistance(double distance, out GeoArc3[] pieces) => Arc3.TrySplitAtDistance(this, distance, out pieces);

        /// <summary>
        /// Cuts the arc at an arc length measured from its start, within a tolerance.
        /// </summary>
        public bool TrySplitAtDistance(double distance, out GeoArc3[] pieces, Tolerance tolerance)
            => Arc3.TrySplitAtDistance(this, distance, out pieces, tolerance);

        #endregion

    }
}
