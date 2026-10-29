using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An XLINE of a DXF file, as AutoCAD's <c>Xline</c>: the line without ends through <see cref="BasePoint"/> along
    /// <see cref="UnitDir"/>, both ways, in world coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The xline through (0, 5, 0) along (0, 1, 0) is the y axis moved 5 along itself: it runs through (0, -100, 0) and
    /// (0, 100, 0) alike. Its parameter is the distance from its base point along its direction, below nought behind it, so
    /// <see cref="DxfCurve.StartParam"/> and <see cref="DxfCurve.EndParam"/> are infinite and it has no start or end point.
    /// </para>
    /// <para>
    /// The file holds the base point (groups 10, 20, 30) and the direction (groups 11, 21, 31), as AutoCAD gives them; a
    /// direction set is written made of unit length, and every other group of the record is kept as it was. A file of R12
    /// has no xlines: one made in code is added to a drawing of R13 or later.
    /// </para>
    /// </remarks>
    public sealed class DxfXline : DxfCurve
    {
        // The groups of AcDbXline in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 11, 21, 31 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private static readonly GeoPoint3 Up = new GeoPoint3(0.0, 0.0, 1.0);

        /// <summary>
        /// Initializes an xline through the origin along the x axis, as AutoCAD's <c>new Xline()</c>, on layer 0 in no file;
        /// give it its base point and direction before it is drawn.
        /// </summary>
        public DxfXline()
            : base("XLINE", DxfRay.Groups("AcDbXline"))
        {
        }

        private DxfXline(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the point the xline is placed by (groups 10, 20, 30), in world coordinates, as AutoCAD's <c>BasePoint</c>; the origin where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the xline is erased.</exception>
        public GeoPoint3 BasePoint
        {
            get => GetPoint(10, Origin);
            set => SetPoint(10, value, Origin);
        }

        /// <summary>
        /// Gets or sets the direction of the xline (groups 11, 21, 31), as AutoCAD's <c>UnitDir</c>: as the file holds it, of
        /// unit length in a file AutoCAD wrote; (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the xline is erased.</exception>
        public GeoVector3 UnitDir
        {
            get
            {
                GeoPoint3 raw = GetPoint(11, Up);
                return new GeoVector3(raw.X, raw.Y, raw.Z);
            }

            set => SetDirection(value);
        }

        /// <summary>
        /// Gets or sets a second point of the xline, as AutoCAD's <c>SecondPoint</c>: the base point and the direction; set, the
        /// direction is made the way from the base point to it.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the point set is the base point, or not of finite numbers.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the xline is erased.</exception>
        public GeoPoint3 SecondPoint
        {
            get => BasePoint.Add(UnitDir);

            set
            {
                GeoPoint3 now = SecondPoint;

                if (!(Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z)))
                {
                    SetDirection(BasePoint.GetVectorTo(value));
                }
            }
        }

        /// <summary>Gets the parameter at the start of the xline: minus infinity, as it runs without end behind its base point.</summary>
        public override double StartParam => double.NegativeInfinity;

        /// <summary>Gets the parameter at the end of the xline: infinite, as it runs without end.</summary>
        public override double EndParam => double.PositiveInfinity;

        /// <summary>Refuses: an xline runs without end both ways, and has no start point, as AutoCAD's has none.</summary>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override GeoPoint3 StartPoint
        {
            get => throw new NotSupportedException("An xline runs without end both ways: it has no start point.");
            set => throw new NotSupportedException("An xline runs without end both ways: it has no start point.");
        }

        /// <summary>Refuses: an xline runs without end both ways, and has no end point, as AutoCAD's has none.</summary>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override GeoPoint3 EndPoint
        {
            get => throw new NotSupportedException("An xline runs without end both ways: it has no end point.");
            set => throw new NotSupportedException("An xline runs without end both ways: it has no end point.");
        }

        /// <summary>Gets false: an xline is not closed.</summary>
        public override bool Closed => false;

        /// <summary>Gets nought: an xline closes no area.</summary>
        public override double Area => 0.0;

        /// <summary>Gets the point at a parameter, the distance from the base point along the direction, as AutoCAD's <c>GetPointAtParameter</c>.</summary>
        /// <param name="value">The distance, below nought behind the base point.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            Guard.Finite(value, nameof(value), "A parameter has to be a finite number.");
            return BasePoint.Add(DxfRay.Direction(UnitDir).Multiply(value));
        }

        /// <summary>Gets the parameter at a point of the xline, the distance from the base point along the direction, as AutoCAD's <c>GetParameterAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the xline.</param>
        /// <returns>The distance, below nought behind the base point.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the xline.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point)
        {
            GeoPoint3 origin = BasePoint;
            GeoVector3 direction = DxfRay.Direction(UnitDir);
            double along = origin.GetVectorTo(point).DotProduct(direction);

            if (!(origin.Add(direction.Multiply(along)).DistanceTo(point) <= Tolerance.Default.EqualPoint))
            {
                throw new ArgumentException("The point " + point + " is not on the xline.", nameof(point));
            }

            return along;
        }

        /// <summary>Gets the distance from the base point at a parameter: the parameter itself, below nought behind the base point.</summary>
        /// <param name="value">The parameter.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            Guard.Finite(value, nameof(value), "A parameter has to be a finite number.");
            return value;
        }

        /// <summary>Gets the parameter at a distance from the base point: the distance itself.</summary>
        /// <param name="dist">The distance, below nought behind the base point.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            Guard.Finite(dist, nameof(dist), "A distance has to be a finite number.");
            return dist;
        }

        /// <summary>Gets the distance from the base point to a point of the xline, below nought behind it.</summary>
        /// <param name="point">The point, on the xline.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the xline.</exception>
        public override double GetDistAtPoint(GeoPoint3 point) => GetParameterAtPoint(point);

        /// <summary>Gets the point at a distance from the base point, below nought behind it.</summary>
        /// <param name="value">The distance.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public override GeoPoint3 GetPointAtDist(double value) => GetPointAtParameter(value);

        /// <summary>Gets the point of the xline nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: the foot of the point on it.</summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Of no account: an xline has no ends to extend.</param>
        /// <returns>The nearest point.</returns>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            GeoPoint3 origin = BasePoint;
            GeoVector3 direction = DxfRay.Direction(UnitDir);
            return origin.Add(direction.Multiply(origin.GetVectorTo(givenPoint).DotProduct(direction)));
        }

        /// <summary>Gets the derivative of the xline at a parameter, as AutoCAD's <c>GetFirstDerivative</c>: its direction made of unit length.</summary>
        /// <param name="value">The parameter.</param>
        /// <returns>The direction.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            Guard.Finite(value, nameof(value), "A parameter has to be a finite number.");
            return DxfRay.Direction(UnitDir);
        }

        /// <summary>Gets the second derivative of the xline: nought, as an xline does not bend.</summary>
        /// <param name="value">The parameter.</param>
        /// <returns>The vector of no length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            Guard.Finite(value, nameof(value), "A parameter has to be a finite number.");
            return new GeoVector3(0.0, 0.0, 0.0);
        }

        /// <summary>Refuses: an xline has no plane of its own to be offset in; <see cref="GetOffsetCurvesGivenPlaneNormal"/> takes one.</summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>Nothing: it throws.</returns>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
            => throw new NotSupportedException("An xline has no plane of its own to be offset in: give one by GetOffsetCurvesGivenPlaneNormal.");

        /// <summary>
        /// Gets the xline at a distance from this one in the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>: to the left of its direction seen from the tip of the normal where the
        /// distance is above nought, as a <see cref="DxfLine"/> is offset, AutoCAD 2021's side for a line.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane; only its part square to the xline counts.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one xline, of the same look, in no file.</returns>
        /// <exception cref="ArgumentException">Thrown when the normal has no length or runs along the xline.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            GeoVector3 shift = DxfRay.Sideways(planeNormal, DxfRay.Direction(UnitDir), -offsetDist);
            var copy = (DxfXline)Copy();
            copy.BasePoint = BasePoint.Add(shift);
            return new DxfCurve[] { copy };
        }

        /// <summary>Turns the xline to run the other way, as AutoCAD's <c>ReverseCurve</c>: its direction turned over, its base point kept.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the xline is erased.</exception>
        public override void ReverseCurve() => UnitDir = UnitDir.Negate();

        /// <summary>Refuses, as AutoCAD does: an xline runs without end both ways and has no end to be extended.</summary>
        /// <param name="newParameter">The parameter.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override void Extend(double newParameter) => throw new NotSupportedException("An xline runs without end both ways: it has no end to be extended.");

        /// <summary>Refuses, as AutoCAD does: an xline runs without end both ways and has no end to be extended.</summary>
        /// <param name="extendStart">Whether the start is moved.</param>
        /// <param name="toPoint">The point.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint) => throw new NotSupportedException("An xline runs without end both ways: it has no end to be extended.");

        /// <summary>
        /// Gets the pieces the xline is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: a ray from the first
        /// parameter running back, lines from each parameter to the next, and a ray from the last running on, all of the same
        /// look in no file; the parameters taken in increasing order, one within the point tolerance of another cutting
        /// nothing, and an xline cut nowhere given whole, as a copy.
        /// </summary>
        /// <param name="parameters">The distances from the base point to cut at, below nought behind it.</param>
        /// <returns>The pieces, in the order of the xline.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is not a finite number.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var cuts = new List<double>();

            foreach (double parameter in parameters)
            {
                Guard.Finite(parameter, nameof(parameters), "A parameter has to be a finite number.");
                cuts.Add(parameter);
            }

            if (cuts.Count == 0)
            {
                return new DxfCurve[] { (DxfXline)Copy() };
            }

            cuts.Sort();
            GeoPoint3 origin = BasePoint;
            GeoVector3 direction = DxfRay.Direction(UnitDir);
            double slack = Tolerance.Default.EqualPoint;
            var pieces = new List<DxfCurve> { Ray(origin.Add(direction.Multiply(cuts[0])), direction.Negate()) };
            double from = cuts[0];

            foreach (double cut in cuts)
            {
                if (cut - from > slack)
                {
                    pieces.Add(DxfRay.Segment(this, origin.Add(direction.Multiply(from)), origin.Add(direction.Multiply(cut))));
                    from = cut;
                }
            }

            pieces.Add(Ray(origin.Add(direction.Multiply(from)), direction));
            return pieces;
        }

        /// <summary>Gives the xline the origin and direction of a <see cref="GeoXLine3"/>, as AutoCAD's <c>SetFromGeCurve</c>, written as <see cref="DxfWriter"/> writes one.</summary>
        /// <param name="geCurve">The line, a <see cref="GeoXLine3"/>.</param>
        /// <exception cref="ArgumentException">Thrown when the shape is not a <see cref="GeoXLine3"/>, or not one of finite numbers and some direction.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the xline is erased.</exception>
        public override void SetFromGeCurve(object geCurve)
        {
            if (!(geCurve is GeoXLine3 xline))
            {
                throw new ArgumentException("An xline takes a GeoXLine3, and was given " + (geCurve == null ? "null" : "a " + geCurve.GetType().Name) + ".", nameof(geCurve));
            }

            SetShape(xline);
        }

        /// <summary>
        /// Moves, turns, scales, mirrors or shears the xline, as AutoCAD's <c>TransformBy</c> does: its base point moved and its
        /// direction turned and made of unit length, as ezdxf moves one. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation takes the direction to no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the xline is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            DxfRay.Moved(transform, BasePoint, UnitDir, out GeoPoint3 origin, out GeoPoint3 direction);
            SetGroups(Join(PointGroups(10, origin, Origin), PointGroups(11, direction, Up)));
        }

        [DxfType("XLINE")]
        internal static DxfEntity Create(DxfItem item) => new DxfXline(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>A ray of the look of the xline, in no file, as a piece of it.</summary>
        private DxfRay Ray(GeoPoint3 origin, GeoVector3 direction)
        {
            var ray = new DxfRay { BasePoint = origin, UnitDir = direction };
            ray.SetPropertiesFrom(this);
            ray.XData = XData;
            return ray;
        }

        /// <summary>Sets the direction made of unit length; nothing where it is the direction read, to the bit.</summary>
        private void SetDirection(GeoVector3 value)
        {
            GeoVector3 now = UnitDir;

            if (Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z))
            {
                return;
            }

            GeoVector3 unit = DxfCircle.Unit(value, nameof(value));
            SetPoint(11, new GeoPoint3(unit.X, unit.Y, unit.Z), Up);
        }
    }
}
