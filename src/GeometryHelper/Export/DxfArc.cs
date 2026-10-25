using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An ARC of a DXF file, as AutoCAD's <c>Arc</c>: the part of the circle of <see cref="Radius"/> about
    /// <see cref="Center"/> that runs counter-clockwise, seen from the tip of its <see cref="Normal"/>, from
    /// <see cref="StartAngle"/> to <see cref="EndAngle"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The angles are in radians, as AutoCAD's .NET API gives them, from the x axis of the system the normal makes by the
    /// arbitrary axis algorithm of the DXF reference, from nought up to 2 pi; the file holds them in degrees (groups 50 and
    /// 51), and an angle set is written in degrees from nought up to 360. The arc of radius 10 about (0, 0, 0) from 0 to
    /// pi / 2 runs from (10, 0, 0) to (0, 10, 0): <see cref="TotalAngle"/> pi / 2, <see cref="Length"/> 15.708. An end angle
    /// at or below the start runs on past 2 pi: from 3 pi / 2 to pi / 2 is half a turn through 0.
    /// </para>
    /// <para>
    /// Its parameter is the angle, as AutoCAD's is: <see cref="DxfCurve.StartParam"/> is the start angle and
    /// <see cref="DxfCurve.EndParam"/> the end angle, 2 pi more where it is not above the start. The centre is in world
    /// coordinates and the file holds it in the system of the normal, as for a <see cref="DxfCircle"/>; every other group
    /// of the record is kept as it was.
    /// </para>
    /// </remarks>
    public sealed class DxfArc : DxfCurve
    {
        // The groups of AcDbCircle and AcDbArc in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 39, 10, 20, 30, 40, 210, 220, 230, 100, 50, 51 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private static readonly GeoVector3 Up = new GeoVector3(0.0, 0.0, 1.0);

        private const double Turn = 2.0 * Math.PI;

        /// <summary>
        /// Initializes an arc of no radius at the origin on the normal (0, 0, 1), both angles nought, as AutoCAD's
        /// <c>new Arc()</c>, on layer 0 in no file; give it its radius and angles before it is drawn.
        /// </summary>
        public DxfArc()
            : this(Origin, Up, 0.0, 0.0, 0.0)
        {
        }

        /// <summary>
        /// Initializes an arc about a centre on the normal (0, 0, 1), as AutoCAD's <c>new Arc(center, radius, startAngle,
        /// endAngle)</c>, on layer 0 with no look of its own, in no file until it is added to one.
        /// </summary>
        /// <param name="center">The centre, in world coordinates.</param>
        /// <param name="radius">The radius, nought or more.</param>
        /// <param name="startAngle">The angle it starts at, in radians from the x axis.</param>
        /// <param name="endAngle">The angle it ends at, in radians, counter-clockwise from the start.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is below nought, or a number is not finite.</exception>
        public DxfArc(GeoPoint3 center, double radius, double startAngle, double endAngle)
            : this(center, Up, radius, startAngle, endAngle)
        {
        }

        /// <summary>
        /// Initializes an arc about a centre in the plane square to a normal, as AutoCAD's <c>new Arc(center, normal, radius,
        /// startAngle, endAngle)</c>, on layer 0 with no look of its own, in no file until it is added to one.
        /// </summary>
        /// <param name="center">The centre, in world coordinates.</param>
        /// <param name="normal">The normal of the arc's plane, of any length; written made of unit length.</param>
        /// <param name="radius">The radius, nought or more.</param>
        /// <param name="startAngle">The angle it starts at, in radians from the x axis of the normal's own system.</param>
        /// <param name="endAngle">The angle it ends at, in radians, counter-clockwise from the start about the normal.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, or the normal has no length.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is below nought, or a number is not finite.</exception>
        public DxfArc(GeoPoint3 center, GeoVector3 normal, double radius, double startAngle, double endAngle)
            : base("ARC", Groups(center, normal, radius, startAngle, endAngle))
        {
        }

        private DxfArc(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the centre of the arc, in world coordinates, as AutoCAD's <c>Center</c>: the groups 10, 20 and 30 taken
        /// from the system of the <see cref="Normal"/> into the world; the origin where the record gives none.
        /// </summary>
        /// <value>Set, a point in world coordinates, written in the system of the normal; the angles are kept.</value>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public GeoPoint3 Center
        {
            get => OcsToWorld(GetNormal(), GetPoint(10, Origin));

            set
            {
                GeoPoint3 now = Center;

                if (!(Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z)))
                {
                    SetPoint(10, WorldToOcs(GetNormal(), value), Origin);
                }
            }
        }

        /// <summary>Gets or sets the radius of the arc (group 40), as AutoCAD's <c>Radius</c>; 1 where the record gives none.</summary>
        /// <value>Set, a finite number, nought or more; the angles are kept.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius set is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public double Radius
        {
            get => GetDouble(40, 1.0);

            set
            {
                Guard.NonNegative(value, nameof(value), "A radius has to be a finite number, nought or more.");
                SetDouble(40, value, 1.0);
            }
        }

        /// <summary>
        /// Gets or sets the normal of the arc (groups 210, 220, 230), square to its plane, of unit length; (0, 0, 1) where the
        /// record gives none. Set, the centre keeps its place in the world, as AutoCAD keeps it, and the angles are kept in
        /// the system of the new normal.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();

            set
            {
                GeoVector3 unit = DxfCircle.Unit(value, nameof(value));
                GeoVector3 now = GetNormal();

                if (!(Same(unit.X, now.X) && Same(unit.Y, now.Y) && Same(unit.Z, now.Z)))
                {
                    GeoPoint3 center = Center;
                    SetGroups(Join(NormalGroups(unit), PointGroups(10, WorldToOcs(unit, center), Origin)));
                }
            }
        }

        /// <summary>
        /// Gets or sets how far the arc stands up along its <see cref="Normal"/> (group 39), a curved wall of that height;
        /// nought where the record gives none.
        /// </summary>
        /// <value>Set, a finite number; nought takes the group out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public double Thickness
        {
            get => GetThickness();
            set => SetThickness(value);
        }

        /// <summary>
        /// Gets or sets the angle the arc starts at (group 50, in degrees), as AutoCAD's <c>StartAngle</c>: in radians from the
        /// x axis of the normal's own system, from nought up to 2 pi; nought where the record gives none.
        /// </summary>
        /// <value>Set, a finite number of radians, written in degrees from nought up to 360.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public double StartAngle
        {
            get => Radians(GetDouble(50, 0.0));
            set => SetAngle(50, value, StartAngle, 0.0);
        }

        /// <summary>
        /// Gets or sets the angle the arc ends at (group 51, in degrees), as AutoCAD's <c>EndAngle</c>: in radians from the x
        /// axis of the normal's own system, from nought up to 2 pi; nought where the record gives none, which DXF reads as 360.
        /// </summary>
        /// <value>Set, a finite number of radians, written in degrees from nought up to 360.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public double EndAngle
        {
            get => Radians(GetDouble(51, 360.0));
            set => SetAngle(51, value, EndAngle, 360.0);
        }

        /// <summary>
        /// Gets the angle the arc sweeps, as AutoCAD's <c>TotalAngle</c>: the end angle less the start, 2 pi more where that is
        /// not above nought; from 3 pi / 2 to pi / 2 is pi.
        /// </summary>
        public double TotalAngle
        {
            get
            {
                double start = StartAngle;
                double end = EndAngle;
                return end > start ? end - start : end - start + Turn;
            }
        }

        /// <summary>Gets the length of the arc, its radius times its <see cref="TotalAngle"/>, as AutoCAD's <c>Length</c>.</summary>
        public double Length => Radius * TotalAngle;

        /// <summary>Gets the parameter at the start of the arc: its <see cref="StartAngle"/>, as AutoCAD's.</summary>
        public override double StartParam => StartAngle;

        /// <summary>Gets the parameter at the end of the arc: its <see cref="EndAngle"/>, 2 pi more where that is not above the start.</summary>
        public override double EndParam
        {
            get
            {
                double start = StartAngle;
                double end = EndAngle;
                return end > start ? end : end + Turn;
            }
        }

        /// <summary>Gets false: an arc is not closed.</summary>
        public override bool Closed => false;

        /// <summary>
        /// Gets the area between the arc and the chord from its start to its end, as AutoCAD's <c>Area</c> closes an open curve:
        /// a half of the square of the radius times the angle less its sine; 78.540 for the half of a circle of radius 10.
        /// </summary>
        public override double Area
        {
            get
            {
                double radius = Radius;
                double swept = EndParam - StartParam;
                return 0.5 * radius * radius * (swept - Math.Sin(swept));
            }
        }

        /// <summary>
        /// Gets the point at a parameter, an angle in radians from the x axis of the normal's own system, as AutoCAD's
        /// <c>GetPointAtParameter</c>.
        /// </summary>
        /// <param name="value">The angle, from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.</param>
        /// <returns>The point, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is past either end by more than the point tolerance along the arc.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            double radius = Radius;
            CheckAngle(value, radius, nameof(value));
            return DxfCircle.At(GetNormal(), GetPoint(10, Origin), radius, value);
        }

        /// <summary>
        /// Gets the parameter at a point of the arc, its angle from the x axis of the normal's own system, as AutoCAD's
        /// <c>GetParameterAtPoint</c>: from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.
        /// </summary>
        /// <param name="point">The point, within the point tolerance of the arc.</param>
        /// <returns>The angle, in radians.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the arc.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point)
        {
            GeoVector3 normal = GetNormal();
            GeoPoint3 centre = GetPoint(10, Origin);
            GeoPoint3 local = WorldToOcs(normal, point);
            double radius = Radius;
            double dx = local.X - centre.X;
            double dy = local.Y - centre.Y;
            double slack = Tolerance.Default.EqualPoint;

            if (Math.Abs(Math.Sqrt(dx * dx + dy * dy) - radius) <= slack && Math.Abs(local.Z - centre.Z) <= slack
                && TryParameter(Math.Atan2(dy, dx), radius, out double parameter))
            {
                return parameter;
            }

            throw new ArgumentException("The point " + point + " is not on the arc.", nameof(point));
        }

        /// <summary>Gets the distance along the arc from its start to a parameter: the radius times the angle past the start.</summary>
        /// <param name="value">The angle, from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is past either end.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            double radius = Radius;
            CheckAngle(value, radius, nameof(value));
            return radius * (value - StartParam);
        }

        /// <summary>Gets the parameter at a distance along the arc from its start: the start angle and the distance over the radius.</summary>
        /// <param name="dist">The distance, from nought to the <see cref="Length"/>.</param>
        /// <returns>The angle, in radians.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            double radius = Radius;
            Within(dist, Length, nameof(dist));
            return StartParam + (radius > 0.0 ? dist / radius : 0.0);
        }

        /// <summary>Gets the distance along the arc from its start to a point of it.</summary>
        /// <param name="point">The point, on the arc.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the arc.</exception>
        public override double GetDistAtPoint(GeoPoint3 point) => Radius * (GetParameterAtPoint(point) - StartParam);

        /// <summary>Gets the point at a distance along the arc from its start.</summary>
        /// <param name="value">The distance, from nought to the <see cref="Length"/>.</param>
        /// <returns>The point, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override GeoPoint3 GetPointAtDist(double value)
        {
            double radius = Radius;
            Within(value, Length, nameof(value));
            return DxfCircle.At(GetNormal(), GetPoint(10, Origin), radius, StartParam + (radius > 0.0 ? value / radius : 0.0));
        }

        /// <summary>
        /// Gets the point of the arc nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: that of the point seen along the
        /// normal where it falls within the arc's angles, else the nearer end; on the whole circle where the arc is extended.
        /// </summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Whether the arc is taken as its whole circle.</param>
        /// <returns>The nearest point, in world coordinates.</returns>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            GeoVector3 normal = GetNormal();
            GeoPoint3 centre = GetPoint(10, Origin);
            GeoPoint3 local = WorldToOcs(normal, givenPoint);
            double radius = Radius;
            double dx = local.X - centre.X;
            double dy = local.Y - centre.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double start = StartParam;
            double angle = length > 0.0 ? Math.Atan2(dy, dx) : start;

            if (extend || FromStart(angle, start) <= EndParam)
            {
                return length > 0.0
                    ? OcsToWorld(normal, new GeoPoint3(centre.X + radius * (dx / length), centre.Y + radius * (dy / length), centre.Z))
                    : DxfCircle.At(normal, centre, radius, angle);
            }

            GeoPoint3 first = DxfCircle.At(normal, centre, radius, start);
            GeoPoint3 last = DxfCircle.At(normal, centre, radius, EndParam);
            return first.DistanceTo(givenPoint) <= last.DistanceTo(givenPoint) ? first : last;
        }

        /// <summary>
        /// Gets the derivative of the arc at a parameter, as AutoCAD's <c>GetFirstDerivative</c>: the tangent, of the length of
        /// the radius, pointing the way the angle grows.
        /// </summary>
        /// <param name="value">The angle, from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.</param>
        /// <returns>The derivative, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is past either end.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            double radius = Radius;
            CheckAngle(value, radius, nameof(value));
            return DxfCircle.Turned(GetNormal(), -radius * Math.Sin(value), radius * Math.Cos(value));
        }

        /// <summary>Gets the second derivative of the arc at a parameter: from the point to the centre, of the length of the radius.</summary>
        /// <param name="value">The angle, from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.</param>
        /// <returns>The second derivative, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is past either end.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            double radius = Radius;
            CheckAngle(value, radius, nameof(value));
            return DxfCircle.Turned(GetNormal(), -radius * Math.Cos(value), -radius * Math.Sin(value));
        }

        /// <summary>
        /// Gets the arc at a distance from this one, as AutoCAD's <c>GetOffsetCurves</c>: about the same centre between the same
        /// angles, its radius the larger by the distance, so that a distance above nought offsets it outwards.
        /// </summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one arc, of the same look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number, or takes the radius to nought or below.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
        {
            Guard.Finite(offsetDist, nameof(offsetDist), "An offset has to be a finite number.");
            double radius = Radius + offsetDist;

            if (!(radius > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(offsetDist), offsetDist, "The offset takes the radius of the arc to nought or below.");
            }

            var copy = (DxfArc)Copy();
            copy.Radius = radius;
            return new DxfCurve[] { copy };
        }

        /// <summary>
        /// Gets the arc at a distance from this one in the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>: as <see cref="GetOffsetCurves(double)"/>, the plane being the arc's own.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane, along the arc's <see cref="Normal"/> either way.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one arc, of the same look, in no file.</returns>
        /// <exception cref="ArgumentException">Thrown when the normal is not along the arc's within the vector tolerance.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number, or takes the radius to nought or below.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            if (!planeNormal.IsValid || planeNormal.IsZeroLength(Tolerance.Default) || !planeNormal.IsParallelTo(GetNormal(), Tolerance.Default))
            {
                throw new ArgumentException("An arc is offset in its own plane: the normal " + planeNormal + " is not along the arc's.", nameof(planeNormal));
            }

            return GetOffsetCurves(offsetDist);
        }

        /// <summary>
        /// Turns the arc to run from its end to its start, as AutoCAD's <c>ReverseCurve</c>: its normal turned over, its centre
        /// kept where it is in the world, and its angles those of its old end and start in the system of the new normal. The
        /// record is written once.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public override void ReverseCurve()
        {
            GeoVector3 normal = GetNormal().Negate();
            GeoPoint3 center = Center;
            GeoPoint3 centre = WorldToOcs(normal, center);
            GeoPoint3 start = WorldToOcs(normal, StartPoint);
            GeoPoint3 end = WorldToOcs(normal, EndPoint);
            double from = Degrees(Math.Atan2(end.Y - centre.Y, end.X - centre.X));
            double to = Degrees(Math.Atan2(start.Y - centre.Y, start.X - centre.X));
            SetGroups(Join(NormalGroups(normal), PointGroups(10, centre, Origin), NumberGroups(50, from, GetDouble(50, 0.0)), NumberGroups(51, to, GetDouble(51, 360.0))));
        }

        /// <summary>
        /// Extends the arc round its circle to a parameter past one of its ends, as AutoCAD's <c>Extend</c>: below
        /// <see cref="DxfCurve.StartParam"/> moves its start back, above <see cref="DxfCurve.EndParam"/> moves its end on.
        /// </summary>
        /// <param name="newParameter">The angle, in radians, below the start or above the end, short of a whole turn.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle lies on the arc, would make it a whole turn or
        /// more, or is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public override void Extend(double newParameter)
        {
            Guard.Finite(newParameter, nameof(newParameter), "A parameter has to be a finite number.");
            double start = StartParam;
            double end = EndParam;

            if (newParameter >= start && newParameter <= end)
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter lies on the arc, which runs from its start angle to its end angle: one past an end extends it.");
            }

            if ((newParameter < start ? end - newParameter : newParameter - start) >= Turn)
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter would take the arc round a whole turn or more.");
            }

            if (newParameter < start)
            {
                StartAngle = newParameter;
            }
            else
            {
                EndAngle = newParameter;
            }
        }

        /// <summary>Extends the arc at one end round its circle to a point of the circle, as AutoCAD's <c>Extend</c>.</summary>
        /// <param name="extendStart">Whether the start is moved, else the end.</param>
        /// <param name="toPoint">The point, within the point tolerance of the arc's circle.</param>
        /// <exception cref="ArgumentException">Thrown when the point is off the circle.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint)
        {
            GeoPoint3 centre = GetPoint(10, Origin);
            GeoPoint3 local = WorldToOcs(GetNormal(), toPoint);
            double dx = local.X - centre.X;
            double dy = local.Y - centre.Y;
            double slack = Tolerance.Default.EqualPoint;

            if (!(Math.Abs(Math.Sqrt(dx * dx + dy * dy) - Radius) <= slack && Math.Abs(local.Z - centre.Z) <= slack))
            {
                throw new ArgumentException("The point " + toPoint + " is not on the circle of the arc.", nameof(toPoint));
            }

            double angle = Math.Atan2(dy, dx);

            if (extendStart)
            {
                StartAngle = angle;
            }
            else
            {
                EndAngle = angle;
            }
        }

        /// <summary>
        /// Gets the pieces the arc is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: arcs of the same look in no
        /// file, from the start to the first angle, on to the next, and to the end, the angles taken in increasing order, those
        /// at an end or within the point tolerance along the arc of another cutting nothing.
        /// </summary>
        /// <param name="parameters">The angles to cut at, from <see cref="DxfCurve.StartParam"/> to <see cref="DxfCurve.EndParam"/>.</param>
        /// <returns>The pieces, in the order of the arc.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is past either end.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            double radius = Radius;
            double start = StartParam;
            double end = EndParam;
            double slack = radius > 0.0 ? Tolerance.Default.EqualPoint / radius : double.PositiveInfinity;
            var cuts = new List<double>();

            foreach (double parameter in parameters)
            {
                CheckAngle(parameter, radius, nameof(parameters));
                cuts.Add(parameter);
            }

            cuts.Sort();
            var pieces = new List<DxfCurve>();
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

        /// <summary>Gives the arc the centre, normal, radius and angles of a <see cref="GeoArc3"/>, as AutoCAD's <c>SetFromGeCurve</c>, written as <see cref="DxfWriter"/> writes one.</summary>
        /// <param name="geCurve">The arc, a <see cref="GeoArc3"/> short of a whole turn.</param>
        /// <exception cref="ArgumentException">Thrown when the shape is not a <see cref="GeoArc3"/>, is a whole turn, which is
        /// written as a CIRCLE, or is not one of finite numbers and some radius.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        public override void SetFromGeCurve(object geCurve)
        {
            if (!(geCurve is GeoArc3 arc))
            {
                throw new ArgumentException("An arc takes a GeoArc3, and was given " + (geCurve == null ? "null" : "a " + geCurve.GetType().Name) + ".", nameof(geCurve));
            }

            SetShape(arc);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the arc, as AutoCAD's <c>TransformBy</c> does: its centre, normal, radius and
        /// thickness are carried as a <see cref="DxfCircle"/>'s, and its angles as ezdxf carries an arc's, each turned into the
        /// system of the new normal and swapped where a mirror turns the arc about. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation, one that scales the arc's plane alike both ways.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation scales the plane of the arc more one way than the
        /// other, which would make it a part of an ellipse, as AutoCAD refuses one, or flattens it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the arc is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfCircle.Unmoved(transform))
            {
                return;
            }

            GeoVector3 old = GetNormal();
            double thickness = Thickness;
            DxfCircle.Moved(transform, old, GetPoint(10, Origin), Radius, thickness, out GeoVector3 normal, out GeoPoint3 centre, out double radius, out double raised);
            double start = GetDouble(50, 0.0);
            double end = GetDouble(51, 360.0);
            double from = start;
            double to = end;

            // As ezdxf: an arc that spans a whole turn keeps its angles; any other has them turned, and swapped where the turn
            // they span comes out the other way round.
            if (!IsClose(SpanDegrees(start, end), 360.0, 0.0))
            {
                Turned(transform, old, normal, start * (Math.PI / 180.0), end * (Math.PI / 180.0), out double first, out double last);
                from = Wrapped(first * (180.0 / Math.PI));
                to = Wrapped(last * (180.0 / Math.PI));
            }

            SetGroups(Join(NormalGroups(normal), PointGroups(10, centre, Origin), NumberGroups(40, radius, Radius), NumberGroups(50, from, start), NumberGroups(51, to, end)));

            if (thickness != 0.0)
            {
                Thickness = raised;
            }
        }

        [DxfType("ARC")]
        internal static DxfEntity Create(DxfItem item) => new DxfArc(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The groups of an arc made in code: those of its circle, then its marker and its angles in degrees.</summary>
        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 center, GeoVector3 normal, double radius, double startAngle, double endAngle)
        {
            GeoVector3 unit = DxfCircle.Unit(normal, nameof(normal));
            Guard.NonNegative(radius, nameof(radius), "A radius has to be a finite number, nought or more.");
            Guard.Finite(startAngle, nameof(startAngle), "An angle has to be a finite number.");
            Guard.Finite(endAngle, nameof(endAngle), "An angle has to be a finite number.");

            if (!center.IsValid)
            {
                throw new ArgumentException("A centre has to be of finite numbers: " + center + " is not.", nameof(center));
            }

            GeoPoint3 local = WorldToOcs(unit, center);
            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDbCircle"),
                new DxfTypedValue(10, Numbers.Exact(local.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(local.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(local.Z + 0.0)),
                new DxfTypedValue(40, Numbers.Exact(radius + 0.0)),
            };

            if (!(unit.X == 0.0 && unit.Y == 0.0 && unit.Z == 1.0))
            {
                groups.Add(new DxfTypedValue(210, Numbers.Exact(unit.X + 0.0)));
                groups.Add(new DxfTypedValue(220, Numbers.Exact(unit.Y + 0.0)));
                groups.Add(new DxfTypedValue(230, Numbers.Exact(unit.Z + 0.0)));
            }

            groups.Add(new DxfTypedValue(100, "AcDbArc"));
            groups.Add(new DxfTypedValue(50, Numbers.Exact(Degrees(startAngle) + 0.0)));
            groups.Add(new DxfTypedValue(51, Numbers.Exact(Degrees(endAngle) + 0.0)));
            return groups;
        }

        /// <summary>An angle of the file in degrees as radians, from nought up to 2 pi, as the reader turns one.</summary>
        private static double Radians(double degrees)
        {
            double angle = (degrees * Math.PI / 180.0) % Turn;

            if (angle < 0.0)
            {
                angle += Turn;
            }

            return angle >= Turn ? angle - Turn : angle;
        }

        /// <summary>An angle in radians as the degrees written for it, from nought up to 360.</summary>
        private static double Degrees(double radians) => Wrapped(radians * 180.0 / Math.PI);

        /// <summary>Degrees brought from nought up to 360.</summary>
        private static double Wrapped(double degrees)
        {
            double wrapped = degrees % 360.0;

            if (wrapped < 0.0)
            {
                wrapped += 360.0;
            }

            return wrapped >= 360.0 ? wrapped - 360.0 : wrapped;
        }

        /// <summary>Sets an angle in radians, written in degrees; nothing where it is the angle read, to the bit.</summary>
        private void SetAngle(int code, double value, double now, double fallback)
        {
            Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

            if (!Same(value, now))
            {
                SetDouble(code, Degrees(value), fallback);
            }
        }

        /// <summary>
        /// The parameter of an angle of the arc's plane: the angle taken round from the start, and at an end where it lies past
        /// it by no more than a slack along the arc. False where it lies off the arc.
        /// </summary>
        private bool TryParameter(double angle, double radius, out double parameter)
        {
            double start = StartParam;
            double end = EndParam;
            parameter = FromStart(angle, start);

            if (parameter <= end)
            {
                return true;
            }

            double slack = Tolerance.Default.EqualPoint;
            double past = parameter - end;
            double before = start + Turn - parameter;
            parameter = past <= before ? end : start;
            return radius * Math.Min(past, before) <= slack;
        }

        /// <summary>An angle taken round to lie from the start of the arc up to a whole turn past it.</summary>
        private static double FromStart(double angle, double start)
        {
            double turned = (angle - start) % Turn;
            return start + (turned < 0.0 ? turned + Turn : turned);
        }

        /// <summary>A copy of the arc from one angle along it to another.</summary>
        private DxfArc Piece(double from, double to)
        {
            var copy = (DxfArc)Copy();
            copy.SetGroups(Join(NumberGroups(50, Degrees(from), copy.GetDouble(50, 0.0)), NumberGroups(51, Degrees(to), copy.GetDouble(51, 360.0))));
            return copy;
        }

        /// <summary>Refuses an angle past either end of the arc by more than the point tolerance along it, or not a number.</summary>
        private void CheckAngle(double value, double radius, string parameter)
        {
            Guard.Finite(value, parameter, "A parameter has to be a finite number.");
            double start = StartParam;
            Within((value - start) * radius, (EndParam - start) * radius, parameter);
        }

        /// <summary>The group that sets a number, written as <see cref="DxfWriter"/> writes one; none where it is the number read, to the bit.</summary>
        private static KeyValuePair<int, string>[] NumberGroups(int code, double value, double now)
            => Same(value, now) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0)) };

        /// <summary>
        /// The start and end angles of an arc turned by a transformation into the system of its new normal, as ezdxf's
        /// <c>transform_ccw_arc_angles</c> turns them: each angle's direction taken into the world, moved and taken into the
        /// new system; swapped where the span they make differs from the old, which a mirror does. Half a turn is told by a
        /// third direction, a radian on from the start.
        /// </summary>
        private static void Turned(GeoTransform3 transform, GeoVector3 old, GeoVector3 normal, double start, double end, out double first, out double last)
        {
            double oldSpan = SpanRadians(start, end);
            first = TurnedAngle(transform, old, normal, start);
            last = TurnedAngle(transform, old, normal, end);
            double newSpan;

            if (IsClose(oldSpan, Math.PI, 0.0))
            {
                oldSpan = 1.0;
                newSpan = SpanRadians(first, TurnedAngle(transform, old, normal, start + oldSpan));
            }
            else if (IsClose(oldSpan, Turn, 0.0))
            {
                last = first + Turn;
                return;
            }
            else
            {
                newSpan = SpanRadians(first, last);
            }

            if (!(Math.Abs(oldSpan - newSpan) <= 1E-8 * Math.Max(Math.Abs(oldSpan), Math.Abs(newSpan))))
            {
                double swap = first;
                first = last;
                last = swap;
            }
        }

        /// <summary>The angle in the system of a new normal of the direction at an angle of the old one, moved by a transformation.</summary>
        private static double TurnedAngle(GeoTransform3 transform, GeoVector3 old, GeoVector3 normal, double angle)
        {
            GeoVector3 moved = transform.Transform(DxfCircle.Turned(old, Math.Cos(angle), Math.Sin(angle)));
            GeoPoint3 local = WorldToOcs(normal, new GeoPoint3(moved.X, moved.Y, moved.Z));
            return Math.Atan2(local.Y, local.X);
        }

        /// <summary>The span counter-clockwise from one angle in degrees to another, as ezdxf's <c>arc_angle_span_deg</c>: 360 for a whole turn, nought for equal angles.</summary>
        private static double SpanDegrees(double start, double end) => Span(start, end, 360.0, 1E-13);

        /// <summary>The span counter-clockwise from one angle in radians to another, as ezdxf's <c>arc_angle_span_rad</c>.</summary>
        private static double SpanRadians(double start, double end) => Span(start, end, Turn, 1E-15);

        private static double Span(double start, double end, double turn, double slack)
        {
            if (IsClose(start, end, slack))
            {
                return 0.0;
            }

            start = Modulo(start, turn);

            if (IsClose(start, Modulo(end, turn), slack))
            {
                return turn;
            }

            if (!IsClose(end, turn, slack))
            {
                end = Modulo(end, turn);
            }

            if (end < start)
            {
                end += turn;
            }

            return end - start;
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

        /// <summary>Python's <c>math.isclose</c> with its relative tolerance of 1E-9 and an absolute one given.</summary>
        private static bool IsClose(double a, double b, double absolute)
            => a == b || Math.Abs(a - b) <= Math.Max(1E-9 * Math.Max(Math.Abs(a), Math.Abs(b)), absolute);
    }
}
