using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A LINE of a DXF file, as AutoCAD's <c>Line</c>: the segment from <see cref="StartPoint"/> to <see cref="EndPoint"/>
    /// in world coordinates, with the <see cref="Normal"/> its <see cref="Thickness"/> stands up along.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The line from (0, 0, 0) to (30, 40, 0) is <see cref="Length"/> 50, <see cref="Delta"/> (30, 40, 0) and
    /// <see cref="Angle"/> 0.927 radians. Its parameter is the distance from its start, as AutoCAD's is: it runs from 0 at
    /// <see cref="StartPoint"/> to 50 at <see cref="EndPoint"/>.
    /// </para>
    /// <para>
    /// A point set is written in the groups 10, 20 and 30 (11, 21 and 31 for the end) as <see cref="DxfWriter"/> writes a
    /// number, only the coordinates that change, and read back to the bit; every other group of the record is kept as it was.
    /// <c>new DxfLine(start, end) { Layer = "steel" }</c> makes one in code, to be added to a drawing by
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfLine : DxfCurve
    {
        // The groups of AcDbLine in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 39, 10, 20, 30, 11, 21, 31, 210, 220, 230 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        /// <summary>
        /// Initializes a line of no length at the origin, as AutoCAD's <c>new Line()</c>, on layer 0 in no file; give it its
        /// ends before it is drawn.
        /// </summary>
        public DxfLine()
            : this(Origin, Origin)
        {
        }

        /// <summary>
        /// Initializes a line from one point to another, as AutoCAD's <c>new Line(start, end)</c>, on layer 0 with no look of
        /// its own, in no file until it is added to one.
        /// </summary>
        /// <param name="startPoint">Where the line starts, in world coordinates.</param>
        /// <param name="endPoint">Where it ends.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfLine(GeoPoint3 startPoint, GeoPoint3 endPoint)
            : base(new GeoLine3(startPoint, endPoint))
        {
        }

        private DxfLine(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets where the line starts (groups 10, 20, 30), in world coordinates; the origin where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line is erased.</exception>
        public override GeoPoint3 StartPoint
        {
            get => GetPoint(10, Origin);
            set => SetPoint(10, value, Origin);
        }

        /// <summary>
        /// Gets or sets where the line ends (groups 11, 21, 31), in world coordinates; the origin where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line is erased.</exception>
        public override GeoPoint3 EndPoint
        {
            get => GetPoint(11, Origin);
            set => SetPoint(11, value, Origin);
        }

        /// <summary>
        /// Gets or sets the normal of the line (groups 210, 220, 230), the direction its <see cref="Thickness"/> stands up
        /// along, of unit length; (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();
            set => SetNormal(value);
        }

        /// <summary>
        /// Gets or sets how far the line stands up along its <see cref="Normal"/> (group 39), a wall of that height; nought
        /// where the record gives none.
        /// </summary>
        /// <value>Set, a finite number; nought takes the group out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line is erased.</exception>
        public double Thickness
        {
            get => GetThickness();
            set => SetThickness(value);
        }

        /// <summary>Gets the vector from the start of the line to its end, as AutoCAD's <c>Delta</c>.</summary>
        public GeoVector3 Delta => StartPoint.GetVectorTo(EndPoint);

        /// <summary>Gets the length of the line, as AutoCAD's <c>Length</c>.</summary>
        public double Length => Delta.Length;

        /// <summary>
        /// Gets the angle of the line in the plane of its <see cref="Normal"/>, as AutoCAD's <c>Angle</c>: in radians from the
        /// x axis of the normal's own system, counter-clockwise seen from its tip, from nought up to 2 pi; the line from
        /// (0, 0, 0) to (0, 10, 0) is pi / 2.
        /// </summary>
        public double Angle
        {
            get
            {
                GeoVector3 normal = Normal;
                OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
                GeoVector3 delta = Delta;
                double angle = Math.Atan2(delta.DotProduct(v), delta.DotProduct(u));
                return angle < 0.0 ? angle + 2.0 * Math.PI : angle;
            }
        }

        /// <summary>Gets the parameter at the end of the line: its <see cref="Length"/>, as AutoCAD's.</summary>
        public override double EndParam => Length;

        /// <summary>Gets nought: a line is not closed.</summary>
        public override bool Closed => false;

        /// <summary>Gets nought: a line closes no area.</summary>
        public override double Area => 0.0;

        /// <summary>
        /// Gets the point at a parameter, the distance from the start of the line, as AutoCAD's <c>GetPointAtParameter</c>.
        /// </summary>
        /// <param name="value">The distance from the start, from nought to <see cref="Length"/>.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end by more than the point tolerance.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            double length = Length;
            Within(value, length, nameof(value));
            GeoPoint3 start = StartPoint;
            return length > 0.0 ? start.Add(Delta.Multiply(value / length)) : start;
        }

        /// <summary>
        /// Gets the parameter at a point of the line, the distance from its start, as AutoCAD's <c>GetParameterAtPoint</c>.
        /// </summary>
        /// <param name="point">The point, within the point tolerance of the line.</param>
        /// <returns>The distance from the start to the point.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the line.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point)
        {
            GeoLine3 segment = Segment();

            if (!segment.GetClosestPointOnBoundary(point).IsEqualTo(point, Tolerance.Default))
            {
                throw new ArgumentException("The point " + point + " is not on the line.", nameof(point));
            }

            double length = segment.Length;
            return length > 0.0 ? Math.Max(0.0, Math.Min(length, segment.StartPoint.GetVectorTo(point).DotProduct(segment.Direction) / length)) : 0.0;
        }

        /// <summary>Gets the distance at a parameter: the parameter itself, as on any AutoCAD line.</summary>
        /// <param name="value">The parameter, from nought to <see cref="Length"/>.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            Within(value, Length, nameof(value));
            return value;
        }

        /// <summary>Gets the parameter at a distance: the distance itself.</summary>
        /// <param name="dist">The distance, from nought to <see cref="Length"/>.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            Within(dist, Length, nameof(dist));
            return dist;
        }

        /// <summary>Gets the distance from the start of the line to a point of it.</summary>
        /// <param name="point">The point, on the line.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the line.</exception>
        public override double GetDistAtPoint(GeoPoint3 point) => GetParameterAtPoint(point);

        /// <summary>Gets the point at a distance from the start of the line.</summary>
        /// <param name="value">The distance, from nought to <see cref="Length"/>.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override GeoPoint3 GetPointAtDist(double value) => GetPointAtParameter(value);

        /// <summary>
        /// Gets the point of the line nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: on the segment, or on the
        /// infinite line through it where it is extended.
        /// </summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Whether the line is taken as the infinite line through it.</param>
        /// <returns>The nearest point; the start for a line of no length.</returns>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            GeoLine3 segment = Segment();
            double lengthSquared = segment.LengthSquared;

            if (!(lengthSquared > 0.0))
            {
                return segment.StartPoint;
            }

            double t = segment.StartPoint.GetVectorTo(givenPoint).DotProduct(segment.Direction) / lengthSquared;
            t = extend ? t : Math.Max(0.0, Math.Min(1.0, t));
            return segment.StartPoint.Add(segment.Direction.Multiply(t));
        }

        /// <summary>
        /// Gets the derivative of the line at a parameter, as AutoCAD's <c>GetFirstDerivative</c>: the unit direction from its
        /// start to its end, the parameter being the distance; nought for a line of no length.
        /// </summary>
        /// <param name="value">The parameter, from nought to <see cref="Length"/>.</param>
        /// <returns>The direction.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            double length = Length;
            Within(value, length, nameof(value));
            return length > 0.0 ? Delta.Divide(length) : new GeoVector3(0.0, 0.0, 0.0);
        }

        /// <summary>Gets the second derivative of the line: nought, as a line does not bend.</summary>
        /// <param name="value">The parameter, from nought to <see cref="Length"/>.</param>
        /// <returns>The vector of no length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            Within(value, Length, nameof(value));
            return new GeoVector3(0.0, 0.0, 0.0);
        }

        /// <summary>
        /// Gets the line at a distance from this one in the plane of its <see cref="Normal"/>, as AutoCAD's
        /// <c>GetOffsetCurves</c>: to the right of its direction seen from the tip of the normal where the distance is above
        /// nought, to the left where below. The side follows AutoCAD's convention for a circle and an arc, whose radius grows
        /// with a distance above nought, the side to the right of an arc running counter-clockwise, so that every curve of the
        /// library offsets to the same side as a <see cref="DxfArc"/>, a <see cref="DxfPolyline"/> and a <see cref="DxfSpline"/>
        /// do: the line from (0, 0, 0) to (30, 40, 0) offset by 5 runs from (4, -3, 0) to (34, 37, 0).
        /// </summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one line, of the same look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line has no length to give it a direction.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist) => GetOffsetCurvesGivenPlaneNormal(Normal, offsetDist);

        /// <summary>
        /// Gets the line at a distance from this one in the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>: to the right of its direction seen from the tip of the normal where the
        /// distance is above nought, as <see cref="GetOffsetCurves(double)"/> says, so to the left about the normal turned over.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane; only its part square to the line counts.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one line, of the same look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        /// <exception cref="ArgumentException">Thrown when the normal has no length or runs along the line.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line has no length to give it a direction.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            // The library's offset in a plane goes to the left: the right is the left of the distance turned over.
            GeoLine3 moved = Segment().OffsetInPlane(-offsetDist, planeNormal, Tolerance.Default);
            var copy = (DxfLine)Copy();
            copy.StartPoint = moved.StartPoint;
            copy.EndPoint = moved.EndPoint;
            return new DxfCurve[] { copy };
        }

        /// <summary>Turns the line to run from its end to its start, as AutoCAD's <c>ReverseCurve</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the line is erased.</exception>
        public override void ReverseCurve()
        {
            GeoPoint3 start = StartPoint;
            GeoPoint3 end = EndPoint;
            SetGroups(Join(PointGroups(10, end, Origin), PointGroups(11, start, Origin)));
        }

        /// <summary>
        /// Extends the line along itself to a parameter past one of its ends, as AutoCAD's <c>Extend</c>: below nought moves
        /// its start back, above <see cref="Length"/> moves its end on.
        /// </summary>
        /// <param name="newParameter">The parameter, the distance from the start along the line, below nought or above its length.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter lies on the line, or is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line has no length to give it a direction, or is erased.</exception>
        public override void Extend(double newParameter)
        {
            double length = Length;
            Guard.Finite(newParameter, nameof(newParameter), "A parameter has to be a finite number.");

            if (!(length > 0.0))
            {
                throw new InvalidOperationException("A line of no length has no direction to be extended along.");
            }

            if (newParameter >= 0.0 && newParameter <= length)
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter lies on the line, which runs from nought to its length: one past an end extends it.");
            }

            GeoPoint3 start = StartPoint;
            GeoPoint3 point = start.Add(Delta.Multiply(newParameter / length));

            if (newParameter < 0.0)
            {
                StartPoint = point;
            }
            else
            {
                EndPoint = point;
            }
        }

        /// <summary>Extends the line at one end to a point on the infinite line through it, as AutoCAD's <c>Extend</c>.</summary>
        /// <param name="extendStart">Whether the start is moved, else the end.</param>
        /// <param name="toPoint">The point, within the point tolerance of the infinite line through the line.</param>
        /// <exception cref="ArgumentException">Thrown when the point is off the line extended.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line is erased.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint)
        {
            if (!GetClosestPointTo(toPoint, true).IsEqualTo(toPoint, Tolerance.Default))
            {
                throw new ArgumentException("The point " + toPoint + " is not on the line extended.", nameof(toPoint));
            }

            if (extendStart)
            {
                StartPoint = toPoint;
            }
            else
            {
                EndPoint = toPoint;
            }
        }

        /// <summary>
        /// Gets the pieces the line is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: lines of the same look in no
        /// file, from the start to the first parameter, on to the next, and to the end, the parameters taken in increasing
        /// order, those at an end or within the point tolerance of another cutting nothing.
        /// </summary>
        /// <param name="parameters">The distances from the start to cut at.</param>
        /// <returns>The pieces, in the order of the line.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is past either end.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            double length = Length;
            double slack = Tolerance.Default.EqualPoint;
            var cuts = new List<double>();

            foreach (double parameter in parameters)
            {
                Within(parameter, length, nameof(parameters));
                cuts.Add(parameter);
            }

            cuts.Sort();
            var pieces = new List<DxfCurve>();
            double from = 0.0;

            foreach (double cut in cuts)
            {
                if (cut - from > slack && length - cut > slack)
                {
                    pieces.Add(Piece(from, cut));
                    from = cut;
                }
            }

            pieces.Add(Piece(from, length));
            return pieces;
        }

        /// <summary>Gives the line the ends of a <see cref="GeoLine3"/>, as AutoCAD's <c>SetFromGeCurve</c>.</summary>
        /// <param name="geCurve">The segment, a <see cref="GeoLine3"/>.</param>
        /// <exception cref="ArgumentException">Thrown when the shape is not a <see cref="GeoLine3"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line is erased.</exception>
        public override void SetFromGeCurve(object geCurve)
        {
            if (!(geCurve is GeoLine3 segment))
            {
                throw new ArgumentException("A line takes a GeoLine3, and was given " + (geCurve == null ? "null" : "a " + geCurve.GetType().Name) + ".", nameof(geCurve));
            }

            if (!segment.StartPoint.IsValid || !segment.EndPoint.IsValid)
            {
                throw new ArgumentException("A line has to be of finite numbers.", nameof(geCurve));
            }

            SetGroups(Join(PointGroups(10, segment.StartPoint, Origin), PointGroups(11, segment.EndPoint, Origin)));
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the line, as AutoCAD's <c>TransformBy</c> does: its ends are moved, and its normal
        /// and thickness are carried as ezdxf carries them, the normal scaled by the thickness taken by the transformation,
        /// its direction the new normal and its length the new thickness. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation takes the normal to no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the line is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoPoint3 start = transform.Transform(StartPoint);
            GeoPoint3 end = transform.Transform(EndPoint);
            double thickness = Thickness;
            GeoVector3 raised = transform.Transform(Normal.Multiply(thickness != 0.0 ? thickness : 1.0));
            double length = raised.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("The transformation takes the normal of the line to no direction.", nameof(transform));
            }

            SetGroups(Join(PointGroups(10, start, Origin), PointGroups(11, end, Origin), NormalGroups(raised.Divide(length))));

            if (thickness != 0.0)
            {
                Thickness = thickness < 0.0 ? -length : length;
            }
        }

        [DxfType("LINE")]
        internal static DxfEntity Create(DxfItem item) => new DxfLine(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The segment of the line, as read now; of no length where it has none.</summary>
        private GeoLine3 Segment() => new GeoLine3(StartPoint, EndPoint);

        /// <summary>A copy of the line from one distance along it to another.</summary>
        private DxfLine Piece(double from, double to)
        {
            var copy = (DxfLine)Copy();
            copy.SetGroups(Join(copy.PointGroups(10, GetPointAtParameter(from), Origin), copy.PointGroups(11, GetPointAtParameter(to), Origin)));
            return copy;
        }
    }
}
