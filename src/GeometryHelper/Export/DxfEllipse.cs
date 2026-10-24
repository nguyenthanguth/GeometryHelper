using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An ELLIPSE of a DXF file, as AutoCAD's <c>Ellipse</c>: about <see cref="Center"/>, its <see cref="MajorAxis"/> from
    /// the centre to the end of its long axis, its <see cref="MinorAxis"/> the <see cref="Normal"/> crossed with that and
    /// <see cref="RadiusRatio"/> times as long, running counter-clockwise about the normal from its start to its end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ellipse about (0, 0, 0) with the major axis (10, 0, 0) and the ratio 0.5 is <see cref="MajorRadius"/> 10,
    /// <see cref="MinorRadius"/> 5 and <see cref="DxfCurve.Area"/> 157.08, and runs through (0, 5, 0). Everything of it is in
    /// world coordinates, as the file holds it.
    /// </para>
    /// <para>
    /// Its parameter is the eccentric angle, as AutoCAD's is, which the file holds (groups 41 and 42): the point at the
    /// parameter t is the centre, the major axis times cos t and the minor axis times sin t. The angles AutoCAD calls
    /// <see cref="StartAngle"/> and <see cref="EndAngle"/> are another thing, those of the lines from the centre to the
    /// ends, from the major axis: on that ellipse the parameter pi / 4 is the point (7.071, 3.536, 0), at the angle
    /// 0.4636. A whole ellipse runs from 0 to 2 pi. Every other group of the record is kept as it was.
    /// </para>
    /// </remarks>
    public sealed class DxfEllipse : DxfCurve
    {
        // The groups of AcDbEllipse in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 11, 21, 31, 210, 220, 230, 40, 41, 42 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private static readonly GeoVector3 Up = new GeoVector3(0.0, 0.0, 1.0);

        private static readonly GeoPoint3 AlongX = new GeoPoint3(1.0, 0.0, 0.0);

        private const double Turn = 2.0 * Math.PI;

        private const double Quarter = 0.5 * Math.PI;

        /// <summary>
        /// Initializes the whole ellipse about the origin on the normal (0, 0, 1) with the major axis (1, 0, 0) and the ratio 1,
        /// as AutoCAD's <c>new Ellipse()</c>, on layer 0 in no file.
        /// </summary>
        public DxfEllipse()
            : this(Origin, Up, new GeoVector3(1.0, 0.0, 0.0), 1.0, 0.0, Turn)
        {
        }

        /// <summary>
        /// Initializes an ellipse, as AutoCAD's <c>new Ellipse(center, unitNormal, majorAxis, radiusRatio, startAngle,
        /// endAngle)</c>, on layer 0 with no look of its own, in no file until it is added to one: nought and 2 pi make it
        /// whole.
        /// </summary>
        /// <param name="center">The centre, in world coordinates.</param>
        /// <param name="unitNormal">The normal of its plane, of any length; written made of unit length.</param>
        /// <param name="majorAxis">The vector from the centre to the end of the long axis, square to the normal.</param>
        /// <param name="radiusRatio">The minor radius over the major, above nought and up to 1.</param>
        /// <param name="startAngle">The angle of its start from the major axis, in radians, as <see cref="StartAngle"/> reads it.</param>
        /// <param name="endAngle">The angle of its end from the major axis, in radians, as <see cref="EndAngle"/> reads it.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, the normal or the major axis has
        /// no length, or the major axis is not square to the normal within the vector tolerance.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the ratio is not above nought and up to 1, or an angle is not finite.</exception>
        public DxfEllipse(GeoPoint3 center, GeoVector3 unitNormal, GeoVector3 majorAxis, double radiusRatio, double startAngle, double endAngle)
            : base("ELLIPSE", Groups(center, unitNormal, majorAxis, radiusRatio, startAngle, endAngle))
        {
        }

        private DxfEllipse(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the centre of the ellipse (groups 10, 20, 30), in world coordinates, as AutoCAD's <c>Center</c>; the origin where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public GeoPoint3 Center
        {
            get => GetPoint(10, Origin);
            set => SetPoint(10, value, Origin);
        }

        /// <summary>
        /// Gets the vector from the centre to the end of the long axis (groups 11, 21, 31), in world coordinates, as AutoCAD's
        /// <c>MajorAxis</c>; (1, 0, 0) where the record gives none. It is set with the rest by <see cref="Set"/>.
        /// </summary>
        public GeoVector3 MajorAxis
        {
            get
            {
                GeoPoint3 end = GetPoint(11, AlongX);
                return new GeoVector3(end.X, end.Y, end.Z);
            }
        }

        /// <summary>
        /// Gets the vector from the centre to the end of the short axis, as AutoCAD's <c>MinorAxis</c>: the
        /// <see cref="Normal"/> crossed with the major axis, <see cref="MinorRadius"/> long.
        /// </summary>
        public GeoVector3 MinorAxis
        {
            get
            {
                GeoVector3 major = MajorAxis;
                return Minor(GetNormal(), major, major.Length * RadiusRatio);
            }
        }

        /// <summary>Gets the length of the major axis, as AutoCAD's <c>MajorRadius</c>.</summary>
        public double MajorRadius => MajorAxis.Length;

        /// <summary>Gets the length of the minor axis, the major radius times the <see cref="RadiusRatio"/>, as AutoCAD's <c>MinorRadius</c>.</summary>
        public double MinorRadius => MajorAxis.Length * RadiusRatio;

        /// <summary>
        /// Gets or sets the minor radius over the major (group 40), as AutoCAD's <c>RadiusRatio</c>; 1 where the record gives
        /// none. Set, the parameters are kept, so the ends keep their eccentric angles.
        /// </summary>
        /// <value>Set, above nought and up to 1.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the ratio set is not above nought and up to 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public double RadiusRatio
        {
            get => GetDouble(40, 1.0);

            set
            {
                // The ratio it has changes nothing, though the file's lie outside (0, 1].
                if (Same(value, RadiusRatio))
                {
                    return;
                }

                CheckRatio(value, nameof(value));
                SetDouble(40, value, 1.0);
            }
        }

        /// <summary>
        /// Gets the normal of the ellipse (groups 210, 220, 230), of unit length, as AutoCAD's <c>Normal</c>; (0, 0, 1) where the
        /// record gives none. It is set with the rest by <see cref="Set"/>.
        /// </summary>
        public GeoVector3 Normal => GetNormal();

        /// <summary>
        /// Gets or sets the parameter at the start of the ellipse (group 41), as AutoCAD's <c>StartParam</c>: an eccentric angle
        /// in radians; nought where the record gives none.
        /// </summary>
        /// <value>Set, a finite number, written as it is.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public override double StartParam
        {
            get => GetDouble(41, 0.0);
            set => SetDouble(41, value, 0.0);
        }

        /// <summary>
        /// Gets or sets the parameter at the end of the ellipse (group 42), as AutoCAD's <c>EndParam</c>: an eccentric angle in
        /// radians, 2 pi where the record gives none, and 2 pi more where the file's is not above the start, so that it runs
        /// from <see cref="StartParam"/> up.
        /// </summary>
        /// <value>Set, a finite number, written as it is.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public override double EndParam
        {
            get
            {
                double start = GetDouble(41, 0.0);
                double end = GetDouble(42, Turn);
                return end > start ? end : end + Turn;
            }

            set
            {
                Guard.Finite(value, nameof(value), "A parameter has to be a finite number.");

                if (!Same(value, EndParam))
                {
                    SetDouble(42, value, Turn);
                }
            }
        }

        /// <summary>
        /// Gets or sets the angle of the start of the ellipse, as AutoCAD's <c>StartAngle</c>: of the line from the centre to
        /// it, in radians from the major axis counter-clockwise about the normal, from nought up to 2 pi; not the parameter,
        /// which the file holds (group 41), but worked out from it and the <see cref="RadiusRatio"/>.
        /// </summary>
        /// <value>Set, a finite number of radians, written as the parameter at that angle.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public double StartAngle
        {
            get => AngleOf(GetDouble(41, 0.0), RadiusRatio);

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (!Same(value, StartAngle))
                {
                    SetDouble(41, ParameterOf(value, RadiusRatio), 0.0);
                }
            }
        }

        /// <summary>
        /// Gets or sets the angle of the end of the ellipse, as AutoCAD's <c>EndAngle</c>: of the line from the centre to it,
        /// in radians from the major axis; 2 pi for a whole ellipse that ends where the file's parameter is 2 pi.
        /// </summary>
        /// <value>Set, a finite number of radians, written as the parameter at that angle; 2 pi as 2 pi.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public double EndAngle
        {
            get => AngleOf(GetDouble(42, Turn), RadiusRatio);

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (!Same(value, EndAngle))
                {
                    SetDouble(42, ParameterOf(value, RadiusRatio), Turn);
                }
            }
        }

        /// <summary>Gets whether the ellipse is of nothing, as AutoCAD's <c>IsNull</c>: its major axis of no length, or its ratio nought.</summary>
        public bool IsNull => !(MajorRadius > 0.0) || !(RadiusRatio > 0.0);

        /// <summary>Gets whether the ellipse is whole: its parameters a whole turn apart, or the same, as the reader takes them.</summary>
        public override bool Closed => IsWhole();

        /// <summary>Gets whether the ellipse runs round on itself, as AutoCAD's <c>IsPeriodic</c>: true for a whole one.</summary>
        public override bool IsPeriodic => IsWhole();

        /// <summary>
        /// Gets the area the ellipse closes, as AutoCAD's <c>Area</c>: pi times its two radii for a whole one, and for a part the
        /// area between it and the chord from its start to its end.
        /// </summary>
        public override double Area
        {
            get
            {
                double a = MajorRadius;
                double b = MinorRadius;

                if (IsWhole())
                {
                    return Math.PI * a * b;
                }

                double swept = EndParam - StartParam;
                return 0.5 * a * b * (swept - Math.Sin(swept));
            }
        }

        /// <summary>
        /// Gets the angle from the major axis of the point at a parameter, as AutoCAD's <c>GetAngleAtParameter</c>, from nought
        /// up to 2 pi; 2 pi at the parameter 2 pi. On the ellipse of ratio 0.5 the parameter pi / 4 is at the angle 0.4636.
        /// </summary>
        /// <param name="value">The parameter, an eccentric angle in radians.</param>
        /// <returns>The angle, in radians.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        public double GetAngleAtParameter(double value)
        {
            Guard.Finite(value, nameof(value), "A parameter has to be a finite number.");
            return AngleOf(value, RadiusRatio);
        }

        /// <summary>
        /// Gets the parameter of the point at an angle from the major axis, as AutoCAD's <c>GetParameterAtAngle</c>, from nought
        /// up to 2 pi; 2 pi at the angle 2 pi.
        /// </summary>
        /// <param name="angle">The angle, in radians.</param>
        /// <returns>The parameter, an eccentric angle in radians.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is not a finite number.</exception>
        public double GetParameterAtAngle(double angle)
        {
            Guard.Finite(angle, nameof(angle), "An angle has to be a finite number.");
            return ParameterOf(angle, RadiusRatio);
        }

        /// <summary>
        /// Gives the ellipse a new centre, normal, major axis, ratio and end angles at once, as AutoCAD's <c>Set</c>: the record
        /// is written once, every other group kept.
        /// </summary>
        /// <param name="center">The centre, in world coordinates.</param>
        /// <param name="unitNormal">The normal of its plane, of any length; written made of unit length.</param>
        /// <param name="majorAxis">The vector from the centre to the end of the long axis, square to the normal.</param>
        /// <param name="radiusRatio">The minor radius over the major, above nought and up to 1.</param>
        /// <param name="startAngle">The angle of its start from the major axis, in radians.</param>
        /// <param name="endAngle">The angle of its end from the major axis, in radians; nought and 2 pi make it whole.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, the normal or the major axis has
        /// no length, or the major axis is not square to the normal within the vector tolerance.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the ratio is not above nought and up to 1, or an angle is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public void Set(GeoPoint3 center, GeoVector3 unitNormal, GeoVector3 majorAxis, double radiusRatio, double startAngle, double endAngle)
        {
            Check(center, unitNormal, majorAxis, radiusRatio, startAngle, endAngle, out GeoVector3 normal);
            SetGroups(Join(
                PointGroups(10, center, Origin),
                PointGroups(11, new GeoPoint3(majorAxis.X, majorAxis.Y, majorAxis.Z), AlongX),
                NormalGroups(normal),
                NumberGroups(40, radiusRatio, RadiusRatio),
                NumberGroups(41, ParameterOf(startAngle, radiusRatio), GetDouble(41, 0.0)),
                NumberGroups(42, ParameterOf(endAngle, radiusRatio), GetDouble(42, Turn))));
        }

        /// <summary>
        /// Gets the point at a parameter, an eccentric angle, as AutoCAD's <c>GetPointAtParameter</c>: the centre, the major axis
        /// times its cosine and the minor axis times its sine.
        /// </summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The point, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end by more than the point tolerance along the ellipse.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            CheckParameter(value, nameof(value));
            return At(value);
        }

        /// <summary>
        /// Gets the parameter at a point of the ellipse, as AutoCAD's <c>GetParameterAtPoint</c>: its eccentric angle, from
        /// <see cref="StartParam"/> to <see cref="EndParam"/>.
        /// </summary>
        /// <param name="point">The point, within the point tolerance of the ellipse.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the ellipse.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point)
        {
            double parameter = Nearest(point);

            if (At(parameter).DistanceTo(point) <= Tolerance.Default.EqualPoint && TryOnCurve(parameter, out double on))
            {
                return on;
            }

            throw new ArgumentException("The point " + point + " is not on the ellipse.", nameof(point));
        }

        /// <summary>Gets the distance along the ellipse from its start to a parameter, as AutoCAD's <c>GetDistanceAtParameter</c>.</summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            CheckParameter(value, nameof(value));
            return Distance(StartParam, value);
        }

        /// <summary>Gets the parameter at a distance along the ellipse from its start, as AutoCAD's <c>GetParameterAtDistance</c>.</summary>
        /// <param name="dist">The distance, from nought to the length of the ellipse.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            double start = StartParam;
            double end = EndParam;
            Within(dist, Distance(start, end), nameof(dist));
            return Math.Max(start, Math.Min(end, ParameterAt(start, dist)));
        }

        /// <summary>Gets the distance along the ellipse from its start to a point of it.</summary>
        /// <param name="point">The point, on the ellipse.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the ellipse.</exception>
        public override double GetDistAtPoint(GeoPoint3 point) => Distance(StartParam, GetParameterAtPoint(point));

        /// <summary>Gets the point at a distance along the ellipse from its start.</summary>
        /// <param name="value">The distance, from nought to the length of the ellipse.</param>
        /// <returns>The point, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override GeoPoint3 GetPointAtDist(double value) => At(GetParameterAtDistance(value));

        /// <summary>
        /// Gets the point of the ellipse nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: on the whole ellipse where it is
        /// whole or extended, else on its part, an end where that is nearer.
        /// </summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Whether a part is taken as the whole ellipse.</param>
        /// <returns>The nearest point, in world coordinates.</returns>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            double parameter = Nearest(givenPoint);

            if (extend || IsWhole() || FromStart(parameter) <= EndParam)
            {
                return At(parameter);
            }

            // The nearest point of the whole lies off the part: the part's own is an end or a dip of the distance within it,
            // found from the best of a row of samples and closed in on by halving.
            double start = StartParam;
            double end = EndParam;
            const int Samples = 64;
            double step = (end - start) / Samples;
            double best = start;
            double least = double.PositiveInfinity;

            for (int i = 0; i <= Samples; i++)
            {
                double at = i == Samples ? end : start + i * step;
                double distance = At(at).DistanceTo(givenPoint);

                if (distance < least)
                {
                    least = distance;
                    best = at;
                }
            }

            double low = Math.Max(start, best - step);
            double high = Math.Min(end, best + step);

            for (int round = 0; round < 200 && high - low > 1E-15 * Math.Max(1.0, Math.Abs(high)); round++)
            {
                double third = (high - low) / 3.0;

                if (At(low + third).DistanceTo(givenPoint) <= At(high - third).DistanceTo(givenPoint))
                {
                    high -= third;
                }
                else
                {
                    low += third;
                }
            }

            GeoPoint3 inside = At(0.5 * (low + high));
            GeoPoint3 first = At(start);
            GeoPoint3 last = At(end);
            GeoPoint3 nearest = inside;

            if (first.DistanceTo(givenPoint) < nearest.DistanceTo(givenPoint))
            {
                nearest = first;
            }

            if (last.DistanceTo(givenPoint) < nearest.DistanceTo(givenPoint))
            {
                nearest = last;
            }

            return nearest;
        }

        /// <summary>
        /// Gets the derivative of the ellipse at a parameter, as AutoCAD's <c>GetFirstDerivative</c>: the minor axis times the
        /// cosine less the major axis times the sine.
        /// </summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The derivative, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            CheckParameter(value, nameof(value));
            GeoVector3 major = MajorAxis;
            GeoVector3 minor = MinorAxis;
            return major.Multiply(-Math.Sin(value)).Add(minor.Multiply(Math.Cos(value)));
        }

        /// <summary>Gets the second derivative of the ellipse at a parameter: from the point to the centre.</summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The second derivative, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            CheckParameter(value, nameof(value));
            GeoVector3 major = MajorAxis;
            GeoVector3 minor = MinorAxis;
            return major.Multiply(Math.Cos(value)).Add(minor.Multiply(Math.Sin(value))).Negate();
        }

        /// <summary>Refuses: the curve at a distance from an ellipse is not an ellipse, and AutoCAD gives a spline for it, which the library does not make.</summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>Nothing: it throws.</returns>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
            => throw new NotSupportedException("The curve at a distance from an ellipse is not an ellipse: AutoCAD gives a spline, which the library does not make.");

        /// <summary>Refuses: the curve at a distance from an ellipse is not an ellipse, and AutoCAD gives a spline for it, which the library does not make.</summary>
        /// <param name="planeNormal">The normal of the plane.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>Nothing: it throws.</returns>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
            => GetOffsetCurves(offsetDist);

        /// <summary>
        /// Turns the ellipse to run from its end to its start, as AutoCAD's <c>ReverseCurve</c>: its normal turned over, which
        /// turns its minor axis over, its major axis kept, and its parameters those of its old end and start under that.
        /// The record is written once.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public override void ReverseCurve()
        {
            double start = StartParam;
            double end = EndParam;
            double first = Modulo(-end, Turn) + 0.0;
            SetGroups(Join(NormalGroups(GetNormal().Negate()), NumberGroups(41, first, GetDouble(41, 0.0)), NumberGroups(42, first + (end - start), GetDouble(42, Turn))));
        }

        /// <summary>
        /// Extends a part of an ellipse round the ellipse to a parameter past one of its ends, as AutoCAD's <c>Extend</c>:
        /// below <see cref="StartParam"/> moves its start back, above <see cref="EndParam"/> moves its end on.
        /// </summary>
        /// <param name="newParameter">The parameter, below the start or above the end, short of a whole turn.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter lies on the ellipse, would make it a whole turn
        /// or more, or is not a finite number.</exception>
        /// <exception cref="NotSupportedException">Thrown when the ellipse is whole, with no end to extend.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public override void Extend(double newParameter)
        {
            Guard.Finite(newParameter, nameof(newParameter), "A parameter has to be a finite number.");

            if (IsWhole())
            {
                throw new NotSupportedException("A whole ellipse is closed: it has no end to be extended.");
            }

            double start = StartParam;
            double end = EndParam;

            if (newParameter >= start && newParameter <= end)
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter lies on the ellipse, which runs from its start parameter to its end: one past an end extends it.");
            }

            if ((newParameter < start ? end - newParameter : newParameter - start) >= Turn)
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter would take the ellipse round a whole turn or more.");
            }

            if (newParameter < start)
            {
                StartParam = newParameter;
            }
            else
            {
                EndParam = newParameter;
            }
        }

        /// <summary>Extends a part of an ellipse at one end round the ellipse to a point of it, as AutoCAD's <c>Extend</c>.</summary>
        /// <param name="extendStart">Whether the start is moved, else the end.</param>
        /// <param name="toPoint">The point, within the point tolerance of the whole ellipse.</param>
        /// <exception cref="ArgumentException">Thrown when the point is off the ellipse.</exception>
        /// <exception cref="NotSupportedException">Thrown when the ellipse is whole, with no end to extend.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint)
        {
            if (IsWhole())
            {
                throw new NotSupportedException("A whole ellipse is closed: it has no end to be extended.");
            }

            double parameter = Nearest(toPoint);

            if (!(At(parameter).DistanceTo(toPoint) <= Tolerance.Default.EqualPoint))
            {
                throw new ArgumentException("The point " + toPoint + " is not on the ellipse.", nameof(toPoint));
            }

            if (extendStart)
            {
                StartParam = parameter;
            }
            else
            {
                SetDouble(42, parameter, Turn);
            }
        }

        /// <summary>
        /// Gets the pieces the ellipse is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: parts of it of the same
        /// look in no file. A part is cut from its start to the first parameter, on to the next and to its end; a whole one
        /// from each parameter to the next round it and from the last round to the first, given whole where it is cut at fewer
        /// than two places. A parameter at an end or within the point tolerance along it of another cuts nothing.
        /// </summary>
        /// <param name="parameters">The parameters to cut at, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The pieces, in the order of the ellipse.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is past either end.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            double start = StartParam;
            double end = EndParam;
            double major = MajorRadius;
            double slack = major > 0.0 ? Tolerance.Default.EqualPoint / major : double.PositiveInfinity;
            var cuts = new List<double>();

            foreach (double parameter in parameters)
            {
                CheckParameter(parameter, nameof(parameters));
                cuts.Add(Math.Max(start, Math.Min(end, parameter)));
            }

            cuts.Sort();
            var pieces = new List<DxfCurve>();

            if (IsWhole())
            {
                var kept = new List<double>();

                foreach (double cut in cuts)
                {
                    double at = cut >= end ? cut - Turn : cut;

                    if (kept.Count == 0 || at - kept[kept.Count - 1] > slack)
                    {
                        kept.Add(at);
                    }
                }

                kept.Sort();

                if (kept.Count > 1 && kept[0] + Turn - kept[kept.Count - 1] <= slack)
                {
                    kept.RemoveAt(kept.Count - 1);
                }

                if (kept.Count < 2)
                {
                    return new DxfCurve[] { (DxfEllipse)Copy() };
                }

                for (int i = 0; i < kept.Count; i++)
                {
                    pieces.Add(Piece(kept[i], i + 1 < kept.Count ? kept[i + 1] : kept[0] + Turn));
                }

                return pieces;
            }

            double from = start;

            foreach (double cut in cuts)
            {
                if (cut - from > slack && end - cut > slack)
                {
                    pieces.Add(Piece(from, cut));
                    from = cut;
                }
            }

            pieces.Add(Piece(from, end));
            return pieces;
        }

        /// <summary>Gives the ellipse the centre, normal, axes and radii of a whole <see cref="GeoEllipse3"/>, as AutoCAD's <c>SetFromGeCurve</c>, written as <see cref="DxfWriter"/> writes one.</summary>
        /// <param name="geCurve">The ellipse, a <see cref="GeoEllipse3"/>.</param>
        /// <exception cref="ArgumentException">Thrown when the shape is not a <see cref="GeoEllipse3"/>, or not one of finite numbers.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public override void SetFromGeCurve(object geCurve)
        {
            if (!(geCurve is GeoEllipse3 ellipse))
            {
                throw new ArgumentException("An ellipse takes a GeoEllipse3, and was given " + (geCurve == null ? "null" : "a " + geCurve.GetType().Name) + ".", nameof(geCurve));
            }

            SetShape(ellipse);
        }

        /// <summary>
        /// Moves, turns, scales, mirrors or shears the ellipse, as AutoCAD's <c>TransformBy</c> does: as ezdxf transforms one,
        /// its axes moved and, where they come out askew, made square again by Rytz's construction, the normal the new major
        /// axis crossed with the new minor, the parameters of its ends worked out anew on the new axes and swapped where the
        /// part comes out turned about, and the axes swapped where the minor comes out the longer. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation flattens the ellipse.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse is erased.</exception>
        public override void TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfCircle.Unmoved(transform))
            {
                return;
            }

            GeoPoint3 center = Center;
            GeoVector3 major = MajorAxis;
            double ratio = RadiusRatio;
            double oldStart = GetDouble(41, 0.0);
            double oldEnd = GetDouble(42, Turn);
            double start = oldStart;
            double end = oldEnd;
            GeoVector3 minor = Minor(GetNormal(), major, major.Length * ratio);
            GeoPoint3 newCenter = transform.Transform(center);
            GeoVector3 newMajor = transform.Transform(major);
            GeoVector3 newMinor = transform.Transform(minor);
            GeoVector3 normal;
            double newRatio;
            bool adjust;

            if (Math.Abs(Unit(newMajor).DotProduct(Unit(newMinor))) > 1E-6)
            {
                Rytz(newMajor, newMinor, out newMajor, out newMinor, out newRatio);
                normal = Unit(newMajor.CrossProduct(newMinor));
                adjust = true;
            }
            else
            {
                newRatio = newMinor.Length / newMajor.Length;
                normal = Unit(newMajor.CrossProduct(newMinor));
                newMinor = Minor(normal, newMajor, newMajor.Length * newRatio);
                adjust = false;
            }

            if (!normal.IsValid || !newMajor.IsValid || !(newMajor.Length > 0.0) || double.IsNaN(newRatio) || double.IsInfinity(newRatio))
            {
                throw new ArgumentException("The transformation flattens the ellipse.", nameof(transform));
            }

            if (adjust && !(Math.Abs(start - end) <= Math.Max(1E-9 * Math.Max(Math.Abs(start), Math.Abs(end)), 1E-9)))
            {
                // As ezdxf: the moved ends worked back to parameters on the new axes, and swapped where the span changed.
                GeoVector3 x = Unit(newMajor);
                GeoVector3 y = Unit(newMinor);
                double oldSpan = Modulo(end - start, Turn);
                double Parameter(GeoVector3 v) => Modulo(Math.Atan2(y.DotProduct(v) / newRatio, x.DotProduct(v)), Turn);
                start = Parameter(newCenter.GetVectorTo(transform.Transform(Vertex(start, major, minor, center, ratio))));
                end = Parameter(newCenter.GetVectorTo(transform.Transform(Vertex(end, major, minor, center, ratio))));

                if (!(Math.Abs(oldSpan - Math.PI) <= Math.Max(1E-9 * Math.PI, 1E-9)))
                {
                    double newSpan = Modulo(end - start, Turn);

                    if (!(Math.Abs(oldSpan - newSpan) <= Math.Max(1E-9 * Math.Max(oldSpan, newSpan), 1E-9)))
                    {
                        double swap = start;
                        start = end;
                        end = swap;
                    }
                }
                else
                {
                    GeoPoint3 oldCheck = transform.Transform(Vertex(Middle(oldStart, oldEnd), major, minor, center, ratio));
                    GeoPoint3 newCheck = Vertex(Middle(start, end), newMajor, newMinor, newCenter, newRatio);

                    if (!Close(oldCheck, newCheck))
                    {
                        double swap = start;
                        start = end;
                        end = swap;
                    }
                }
            }

            if (newRatio > 1.0)
            {
                newMajor = Minor(normal, newMajor, newMajor.Length * newRatio);
                newRatio = 1.0 / newRatio;

                if (!(start == 0.0 && Math.Abs(end - Turn) <= 1E-9 * Turn))
                {
                    start -= Quarter;
                    end -= Quarter;
                }
            }

            start = Modulo(start, Turn);
            end = Modulo(end, Turn);

            if (Math.Abs(start - end) <= 1E-9 * Math.Max(Math.Abs(start), Math.Abs(end)))
            {
                start = 0.0;
                end = Turn;
            }

            SetGroups(Join(
                PointGroups(10, newCenter, Origin),
                PointGroups(11, new GeoPoint3(newMajor.X, newMajor.Y, newMajor.Z), AlongX),
                NormalGroups(normal),
                NumberGroups(40, Math.Max(newRatio, 1E-6), ratio),
                NumberGroups(41, start, oldStart),
                NumberGroups(42, end, oldEnd)));
        }

        [DxfType("ELLIPSE")]
        internal static DxfEntity Create(DxfItem item) => new DxfEllipse(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The groups of an ellipse made in code: its marker, centre, major axis, normal, ratio and parameters.</summary>
        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 center, GeoVector3 unitNormal, GeoVector3 majorAxis, double radiusRatio, double startAngle, double endAngle)
        {
            Check(center, unitNormal, majorAxis, radiusRatio, startAngle, endAngle, out GeoVector3 normal);
            return new[]
            {
                new DxfTypedValue(100, "AcDbEllipse"),
                new DxfTypedValue(10, Numbers.Exact(center.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(center.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(center.Z + 0.0)),
                new DxfTypedValue(11, Numbers.Exact(majorAxis.X + 0.0)),
                new DxfTypedValue(21, Numbers.Exact(majorAxis.Y + 0.0)),
                new DxfTypedValue(31, Numbers.Exact(majorAxis.Z + 0.0)),
                new DxfTypedValue(210, Numbers.Exact(normal.X + 0.0)),
                new DxfTypedValue(220, Numbers.Exact(normal.Y + 0.0)),
                new DxfTypedValue(230, Numbers.Exact(normal.Z + 0.0)),
                new DxfTypedValue(40, Numbers.Exact(radiusRatio + 0.0)),
                new DxfTypedValue(41, Numbers.Exact(ParameterOf(startAngle, radiusRatio) + 0.0)),
                new DxfTypedValue(42, Numbers.Exact(ParameterOf(endAngle, radiusRatio) + 0.0)),
            };
        }

        /// <summary>Refuses an ellipse AutoCAD would not make: a normal or major axis of no length, the two askew, a ratio out of range.</summary>
        private static void Check(GeoPoint3 center, GeoVector3 unitNormal, GeoVector3 majorAxis, double radiusRatio, double startAngle, double endAngle, out GeoVector3 normal)
        {
            if (!center.IsValid)
            {
                throw new ArgumentException("A centre has to be of finite numbers: " + center + " is not.", nameof(center));
            }

            normal = DxfCircle.Unit(unitNormal, nameof(unitNormal));
            double length = majorAxis.Length;

            if (!majorAxis.IsValid || !(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A major axis has to be a direction of finite numbers and some length: " + majorAxis + " is not.", nameof(majorAxis));
            }

            if (!(Math.Abs(majorAxis.DotProduct(normal)) / length <= Tolerance.Default.EqualVector))
            {
                throw new ArgumentException("The major axis " + majorAxis + " is not square to the normal " + normal + ".", nameof(majorAxis));
            }

            CheckRatio(radiusRatio, nameof(radiusRatio));
            Guard.Finite(startAngle, nameof(startAngle), "An angle has to be a finite number.");
            Guard.Finite(endAngle, nameof(endAngle), "An angle has to be a finite number.");
        }

        private static void CheckRatio(double value, string parameter)
        {
            Guard.Positive(value, parameter, "A ratio of radii has to be a finite number above nought and up to 1.");

            if (value > 1.0)
            {
                throw new ArgumentOutOfRangeException(parameter, value, "A ratio of radii has to be a finite number above nought and up to 1.");
            }
        }

        /// <summary>
        /// The angle from the major axis of the point at a parameter, from nought up to 2 pi, and 2 pi at the parameter 2 pi:
        /// that of (cos t, ratio sin t).
        /// </summary>
        private static double AngleOf(double parameter, double ratio)
        {
            double angle = Modulo(Math.Atan2(ratio * Math.Sin(parameter), Math.Cos(parameter)), Turn);
            return Math.Abs(parameter - Turn) < 1E-15 ? Turn : angle;
        }

        /// <summary>The parameter of the point at an angle from the major axis, from nought up to 2 pi, and 2 pi at the angle 2 pi.</summary>
        private static double ParameterOf(double angle, double ratio)
            => Math.Abs(angle - Turn) < 1E-15 ? Turn : Modulo(Math.Atan2(Math.Sin(angle), ratio * Math.Cos(angle)), Turn);

        /// <summary>Whether the parameters are a whole turn apart, or the same, as the reader takes them.</summary>
        private bool IsWhole()
        {
            double span = (GetDouble(42, Turn) - GetDouble(41, 0.0)) % Turn;

            if (span < 0.0)
            {
                span += Turn;
            }

            return span <= 1E-12 || span >= Turn - 1E-9;
        }

        /// <summary>The point at a parameter, unchecked.</summary>
        private GeoPoint3 At(double parameter)
        {
            GeoVector3 major = MajorAxis;
            GeoVector3 minor = MinorAxis;
            return Center.Add(major.Multiply(Math.Cos(parameter))).Add(minor.Multiply(Math.Sin(parameter)));
        }

        /// <summary>
        /// The parameter of the point of the whole ellipse nearest a point, from nought up to 2 pi, by the library's nearest
        /// point on an ellipse: the point seen along the normal, in the frame of the axes.
        /// </summary>
        private double Nearest(GeoPoint3 point)
        {
            GeoVector3 major = MajorAxis;
            double a = major.Length;
            double ratio = RadiusRatio;

            if (!(a > 0.0) || !(ratio > 0.0))
            {
                return StartParam;
            }

            GeoVector3 x = major.Divide(a);
            GeoVector3 y = Unit(GetNormal().CrossProduct(x));
            GeoVector3 offset = Center.GetVectorTo(point);
            double along = offset.DotProduct(x);
            double across = offset.DotProduct(y);
            Ellipse2.UnitClosest(Math.Min(ratio, 1.0), Math.Abs(along) / a, Math.Abs(across) / a, out double cos, out double sin);
            return Modulo(Math.Atan2(across < 0.0 ? -sin : sin, along < 0.0 ? -cos : cos), Turn);
        }

        /// <summary>A parameter taken round to lie from the start up to a whole turn past it.</summary>
        private double FromStart(double parameter)
        {
            double start = StartParam;
            return start + Modulo(parameter - start, Turn);
        }

        /// <summary>The parameter of the curve of a parameter of the whole ellipse: taken round from the start, at an end where it lies past it by no more than the point tolerance along it.</summary>
        private bool TryOnCurve(double parameter, out double on)
        {
            double start = StartParam;
            double end = EndParam;
            on = FromStart(parameter);

            if (on <= end)
            {
                return true;
            }

            double slack = Tolerance.Default.EqualPoint;
            double past = Distance(end, on);
            double before = Distance(on, start + Turn);
            on = past <= before ? end : start;
            return Math.Min(past, before) <= slack;
        }

        /// <summary>
        /// The length along the ellipse from one parameter to a greater, by the library's arc length of an ellipse: the
        /// incomplete elliptic integral of the second kind, by quarters.
        /// </summary>
        private double Distance(double from, double to)
        {
            double a = MajorRadius;
            double ratio = Math.Min(RadiusRatio, 1.0);
            return a > 0.0 && ratio > 0.0 ? a * (Unwound(to, ratio) - Unwound(from, ratio)) : 0.0;
        }

        /// <summary>The parameter a distance along the ellipse from a parameter.</summary>
        private double ParameterAt(double from, double distance)
        {
            double a = MajorRadius;
            double ratio = Math.Min(RadiusRatio, 1.0);

            if (!(a > 0.0) || !(ratio > 0.0))
            {
                return from;
            }

            double quarter = Ellipse2.UnitArc(ratio, Quarter);
            double target = Unwound(from, ratio) + distance / a;
            double turns = Math.Floor(target / (4.0 * quarter));
            double left = target - turns * 4.0 * quarter;
            double within;

            if (left <= quarter)
            {
                within = Ellipse2.UnitAngleAtArc(ratio, left);
            }
            else if (left <= 2.0 * quarter)
            {
                within = Math.PI - Ellipse2.UnitAngleAtArc(ratio, 2.0 * quarter - left);
            }
            else if (left <= 3.0 * quarter)
            {
                within = Math.PI + Ellipse2.UnitAngleAtArc(ratio, left - 2.0 * quarter);
            }
            else
            {
                within = Turn - Ellipse2.UnitAngleAtArc(ratio, Math.Max(0.0, 4.0 * quarter - left));
            }

            return turns * Turn + within;
        }

        /// <summary>The length along the ellipse of major radius one from the parameter nought to a parameter, whole turns counted.</summary>
        private static double Unwound(double parameter, double ratio)
        {
            double quarter = Ellipse2.UnitArc(ratio, Quarter);
            double turns = Math.Floor(parameter / Turn);
            double t = parameter - turns * Turn;
            double length;

            if (t <= Quarter)
            {
                length = Ellipse2.UnitArc(ratio, t);
            }
            else if (t <= Math.PI)
            {
                length = 2.0 * quarter - Ellipse2.UnitArc(ratio, Math.PI - t);
            }
            else if (t <= 3.0 * Quarter)
            {
                length = 2.0 * quarter + Ellipse2.UnitArc(ratio, t - Math.PI);
            }
            else
            {
                length = 4.0 * quarter - Ellipse2.UnitArc(ratio, Math.Max(0.0, Turn - t));
            }

            return turns * 4.0 * quarter + length;
        }

        /// <summary>A copy of the ellipse from one parameter along it to another.</summary>
        private DxfEllipse Piece(double from, double to)
        {
            var copy = (DxfEllipse)Copy();
            copy.SetGroups(Join(NumberGroups(41, from, copy.GetDouble(41, 0.0)), NumberGroups(42, to, copy.GetDouble(42, Turn))));
            return copy;
        }

        /// <summary>Refuses a parameter past either end of the ellipse by more than the point tolerance along it, or not a number.</summary>
        private void CheckParameter(double value, string parameter)
        {
            Guard.Finite(value, parameter, "A parameter has to be a finite number.");
            double start = StartParam;
            double major = MajorRadius;
            Within((value - start) * major, (EndParam - start) * major, parameter);
        }

        /// <summary>The minor axis of an ellipse: the normal crossed with the major axis, of a length given, as ezdxf's <c>minor_axis</c>.</summary>
        private static GeoVector3 Minor(GeoVector3 normal, GeoVector3 major, double length)
        {
            GeoVector3 cross = normal.CrossProduct(major);
            double size = cross.Length;
            return size > 0.0 ? cross.Multiply(length / size) : new GeoVector3(0.0, 0.0, 0.0);
        }

        /// <summary>A vector made of unit length as ezdxf's <c>normalize</c> makes one.</summary>
        private static GeoVector3 Unit(GeoVector3 vector) => vector.Multiply(1.0 / vector.Length);

        /// <summary>The point at a parameter of an ellipse given by its axes, as ezdxf's <c>vertex</c>.</summary>
        private static GeoPoint3 Vertex(double parameter, GeoVector3 major, GeoVector3 minor, GeoPoint3 center, double ratio)
        {
            double rx = major.Length;
            double ry = rx * ratio;
            return center.Add(Unit(major).Multiply(Math.Cos(parameter) * rx)).Add(Unit(minor).Multiply(Math.Sin(parameter) * ry));
        }

        /// <summary>The parameter halfway from one to another counter-clockwise, as ezdxf's <c>mid_param</c>.</summary>
        private static double Middle(double start, double end) => 0.5 * (start + (end < start ? end + Turn : end));

        /// <summary>Whether two points are within 1E-9 of each other in every coordinate, as ezdxf's <c>isclose</c> with that absolute tolerance.</summary>
        private static bool Close(GeoPoint3 a, GeoPoint3 b)
            => Near(a.X, b.X) && Near(a.Y, b.Y) && Near(a.Z, b.Z);

        private static bool Near(double a, double b) => Math.Abs(a - b) <= Math.Max(1E-9 * Math.Max(Math.Abs(a), Math.Abs(b)), 1E-9);

        /// <summary>
        /// Rytz's construction of the axes of an ellipse from two conjugate half diameters, as ezdxf's
        /// <c>rytz_axis_construction</c>: the major and minor axes and their ratio.
        /// </summary>
        private static void Rytz(GeoVector3 d1, GeoVector3 d2, out GeoVector3 major, out GeoVector3 minor, out double ratio)
        {
            GeoVector3 q = d1;
            GeoVector3 p1;

            if (Math.Abs(d1.Z) <= 1E-9 && Math.Abs(d2.Z) <= 1E-9)
            {
                p1 = new GeoVector3(d2.Y, -d2.X, d2.Z);
            }
            else
            {
                GeoVector3 cross = d1.CrossProduct(d2).CrossProduct(d2);
                p1 = cross.Multiply(d2.Length / cross.Length);
            }

            GeoVector3 d = p1.Add(q.Subtract(p1).Multiply(0.5));
            double radius = d.Length;
            GeoVector3 toward = q.Subtract(p1);
            GeoVector3 shift = toward.Multiply(radius / toward.Length);
            GeoVector3 a = d.Subtract(shift);
            GeoVector3 b = d.Add(shift);
            double majorLength = a.Subtract(q).Length;
            double minorLength = b.Subtract(q).Length;

            if (!(majorLength > 0.0) || !(minorLength > 0.0) || !(a.Length > 1E-12) || !(b.Length > 1E-12))
            {
                throw new ArgumentException("The transformation flattens the ellipse.", "transform");
            }

            ratio = minorLength / majorLength;
            major = b.Multiply(majorLength / b.Length);
            minor = a.Multiply(minorLength / a.Length);
        }

        /// <summary>The remainder of a division with the sign of the divisor, as Python's <c>%</c> gives it.</summary>
        private static double Modulo(double value, double divisor)
        {
            double remainder = value % divisor;

            if (remainder != 0.0 && (remainder < 0.0) != (divisor < 0.0))
            {
                remainder += divisor;
            }

            return remainder;
        }

        /// <summary>The group that sets a number, written as <see cref="DxfWriter"/> writes one; none where it is the number read, to the bit.</summary>
        private static KeyValuePair<int, string>[] NumberGroups(int code, double value, double now)
            => Same(value, now) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0)) };
    }
}
