using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A RAY of a DXF file, as AutoCAD's <c>Ray</c>: the half line from <see cref="BasePoint"/> without end along
    /// <see cref="UnitDir"/>, in world coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ray from (1, 1, 0) along (0.7071, 0.7071, 0) runs through (2, 2, 0) and on. Its parameter is the distance from its
    /// base point, as AutoCAD's is: it runs from nought at <see cref="BasePoint"/> up without end, so
    /// <see cref="DxfCurve.EndParam"/> is infinite and it has no end point.
    /// </para>
    /// <para>
    /// The file holds the base point (groups 10, 20, 30) and the direction (groups 11, 21, 31), as AutoCAD gives them; a
    /// direction set is written made of unit length, and every other group of the record is kept as it was. A file of R12
    /// has no rays: one made in code, <c>new DxfRay { BasePoint = p, UnitDir = d }</c>, is added to a drawing of R13 or later.
    /// </para>
    /// </remarks>
    public sealed class DxfRay : DxfCurve
    {
        // The groups of AcDbRay in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 10, 20, 30, 11, 21, 31 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private static readonly GeoPoint3 Up = new GeoPoint3(0.0, 0.0, 1.0);

        /// <summary>
        /// Initializes a ray from the origin along the x axis, as AutoCAD's <c>new Ray()</c>, on layer 0 in no file; give it its
        /// base point and direction before it is drawn.
        /// </summary>
        public DxfRay()
            : base("RAY", Groups("AcDbRay"))
        {
        }

        private DxfRay(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the point the ray starts from (groups 10, 20, 30), in world coordinates, as AutoCAD's <c>BasePoint</c>; the origin where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ray is erased.</exception>
        public GeoPoint3 BasePoint
        {
            get => GetPoint(10, Origin);
            set => SetPoint(10, value, Origin);
        }

        /// <summary>
        /// Gets or sets the direction of the ray (groups 11, 21, 31), as AutoCAD's <c>UnitDir</c>: as the file holds it, of unit
        /// length in a file AutoCAD wrote; (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ray is erased.</exception>
        public GeoVector3 UnitDir
        {
            get => Raw(GetPoint(11, Up));
            set => SetDirection(value);
        }

        /// <summary>
        /// Gets or sets a second point of the ray, as AutoCAD's <c>SecondPoint</c>: the base point and the direction; set, the
        /// direction is made the way from the base point to it.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the point set is the base point, or not of finite numbers.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ray is erased.</exception>
        public GeoPoint3 SecondPoint
        {
            get => BasePoint.Add(UnitDir);

            set
            {
                if (!SamePoint(value, SecondPoint))
                {
                    SetDirection(BasePoint.GetVectorTo(value));
                }
            }
        }

        /// <summary>Gets or sets the point the ray starts from: its <see cref="BasePoint"/>.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ray is erased.</exception>
        public override GeoPoint3 StartPoint
        {
            get => BasePoint;
            set => BasePoint = value;
        }

        /// <summary>Refuses: a ray runs without end, and has no end point, as AutoCAD's has none.</summary>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override GeoPoint3 EndPoint
        {
            get => throw new NotSupportedException("A ray runs without end: it has no end point.");
            set => throw new NotSupportedException("A ray runs without end: it has no end point.");
        }

        /// <summary>Gets the parameter at the end of the ray: infinite, as it runs without end.</summary>
        public override double EndParam => double.PositiveInfinity;

        /// <summary>Gets false: a ray is not closed.</summary>
        public override bool Closed => false;

        /// <summary>Gets nought: a ray closes no area.</summary>
        public override double Area => 0.0;

        /// <summary>Gets the point at a parameter, the distance from the base point, as AutoCAD's <c>GetPointAtParameter</c>.</summary>
        /// <param name="value">The distance, nought or more.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is below nought by more than the point tolerance, or not a finite number.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            CheckDistance(value, nameof(value));
            return BasePoint.Add(Direction(UnitDir).Multiply(value));
        }

        /// <summary>Gets the parameter at a point of the ray, the distance from the base point, as AutoCAD's <c>GetParameterAtPoint</c>.</summary>
        /// <param name="point">The point, within the point tolerance of the ray.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the ray.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point)
        {
            GeoPoint3 origin = BasePoint;
            GeoVector3 direction = Direction(UnitDir);
            double along = Math.Max(0.0, origin.GetVectorTo(point).DotProduct(direction));

            if (!(origin.Add(direction.Multiply(along)).DistanceTo(point) <= Tolerance.Default.EqualPoint))
            {
                throw new ArgumentException("The point " + point + " is not on the ray.", nameof(point));
            }

            return along;
        }

        /// <summary>Gets the distance from the base point at a parameter: the parameter itself.</summary>
        /// <param name="value">The parameter, nought or more.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is below nought, or not a finite number.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            CheckDistance(value, nameof(value));
            return value;
        }

        /// <summary>Gets the parameter at a distance from the base point: the distance itself.</summary>
        /// <param name="dist">The distance, nought or more.</param>
        /// <returns>The parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is below nought, or not a finite number.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            CheckDistance(dist, nameof(dist));
            return dist;
        }

        /// <summary>Gets the distance from the base point to a point of the ray.</summary>
        /// <param name="point">The point, on the ray.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the ray.</exception>
        public override double GetDistAtPoint(GeoPoint3 point) => GetParameterAtPoint(point);

        /// <summary>Gets the point at a distance from the base point.</summary>
        /// <param name="value">The distance, nought or more.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is below nought, or not a finite number.</exception>
        public override GeoPoint3 GetPointAtDist(double value) => GetPointAtParameter(value);

        /// <summary>
        /// Gets the point of the ray nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: on the ray, or on the infinite
        /// line through it where it is extended.
        /// </summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Whether the ray is taken as the infinite line through it.</param>
        /// <returns>The nearest point.</returns>
        public override GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend)
        {
            GeoPoint3 origin = BasePoint;
            GeoVector3 direction = Direction(UnitDir);
            double along = origin.GetVectorTo(givenPoint).DotProduct(direction);
            return origin.Add(direction.Multiply(extend ? along : Math.Max(0.0, along)));
        }

        /// <summary>Gets the derivative of the ray at a parameter, as AutoCAD's <c>GetFirstDerivative</c>: its direction made of unit length.</summary>
        /// <param name="value">The parameter, nought or more.</param>
        /// <returns>The direction.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is below nought, or not a finite number.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            CheckDistance(value, nameof(value));
            return Direction(UnitDir);
        }

        /// <summary>Gets the second derivative of the ray: nought, as a ray does not bend.</summary>
        /// <param name="value">The parameter, nought or more.</param>
        /// <returns>The vector of no length.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is below nought, or not a finite number.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            CheckDistance(value, nameof(value));
            return new GeoVector3(0.0, 0.0, 0.0);
        }

        /// <summary>Refuses: a ray has no plane of its own to be offset in; <see cref="GetOffsetCurvesGivenPlaneNormal"/> takes one.</summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>Nothing: it throws.</returns>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
            => throw new NotSupportedException("A ray has no plane of its own to be offset in: give one by GetOffsetCurvesGivenPlaneNormal.");

        /// <summary>
        /// Gets the ray at a distance from this one in the plane of a normal, as AutoCAD's <c>GetOffsetCurvesGivenPlaneNormal</c>:
        /// to the right of its direction seen from the tip of the normal where the distance is above nought, the side every curve
        /// offsets to (AutoCAD's side for a circle and an arc, where the radius grows), as a
        /// <see cref="DxfLine"/> is offset.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane; only its part square to the ray counts.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one ray, of the same look, in no file.</returns>
        /// <exception cref="ArgumentException">Thrown when the normal has no length or runs along the ray.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            GeoVector3 shift = Sideways(planeNormal, Direction(UnitDir), offsetDist);
            var copy = (DxfRay)Copy();
            copy.BasePoint = BasePoint.Add(shift);
            return new DxfCurve[] { copy };
        }

        /// <summary>Refuses, as AutoCAD does: a ray runs one way from its base point, and turned it would be another ray.</summary>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override void ReverseCurve() => throw new NotSupportedException("A ray runs one way from its base point: it cannot be turned to run the other way.");

        /// <summary>
        /// Extends the ray back along itself to a parameter below nought, as AutoCAD's <c>Extend</c>: its base point is moved
        /// back there, so that its parameters start anew from it.
        /// </summary>
        /// <param name="newParameter">The parameter, the distance from the base point, below nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is nought or more, which lies on the ray, or is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ray is erased.</exception>
        public override void Extend(double newParameter)
        {
            Guard.Finite(newParameter, nameof(newParameter), "A parameter has to be a finite number.");

            if (newParameter >= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(newParameter), newParameter, "The parameter lies on the ray, which runs from nought without end: one below nought extends it back.");
            }

            BasePoint = BasePoint.Add(Direction(UnitDir).Multiply(newParameter));
        }

        /// <summary>Extends the ray back to a point on the line through it, as AutoCAD's <c>Extend</c>: the base point moved there.</summary>
        /// <param name="extendStart">Whether the start is moved; a ray has no end to move.</param>
        /// <param name="toPoint">The point, within the point tolerance of the line through the ray.</param>
        /// <exception cref="ArgumentException">Thrown when the point is off the line through the ray.</exception>
        /// <exception cref="NotSupportedException">Thrown when the end is to be moved: a ray has none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ray is erased.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint)
        {
            if (!extendStart)
            {
                throw new NotSupportedException("A ray runs without end: it has no end to be extended.");
            }

            if (!GetClosestPointTo(toPoint, true).IsEqualTo(toPoint, Tolerance.Default))
            {
                throw new ArgumentException("The point " + toPoint + " is not on the line through the ray.", nameof(toPoint));
            }

            BasePoint = toPoint;
        }

        /// <summary>
        /// Gets the pieces the ray is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: lines from the base point to
        /// the first parameter and on to the next, then a ray from the last, all of the same look in no file; the parameters
        /// taken in increasing order, one at the base point or within the point tolerance of another cutting nothing.
        /// </summary>
        /// <param name="parameters">The distances from the base point to cut at.</param>
        /// <returns>The pieces, in the order of the ray.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is below nought, or not a finite number.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var cuts = new List<double>();

            foreach (double parameter in parameters)
            {
                CheckDistance(parameter, nameof(parameters));
                cuts.Add(parameter);
            }

            cuts.Sort();
            GeoPoint3 origin = BasePoint;
            GeoVector3 direction = Direction(UnitDir);
            double slack = Tolerance.Default.EqualPoint;
            var pieces = new List<DxfCurve>();
            double from = 0.0;

            foreach (double cut in cuts)
            {
                if (cut - from > slack)
                {
                    pieces.Add(Segment(this, origin.Add(direction.Multiply(from)), origin.Add(direction.Multiply(cut))));
                    from = cut;
                }
            }

            var rest = (DxfRay)Copy();
            rest.BasePoint = origin.Add(direction.Multiply(from));
            pieces.Add(rest);
            return pieces;
        }

        /// <summary>Gives the ray the origin and direction of a <see cref="GeoRay3"/>, as AutoCAD's <c>SetFromGeCurve</c>, written as <see cref="DxfWriter"/> writes one.</summary>
        /// <param name="geCurve">The ray, a <see cref="GeoRay3"/>.</param>
        /// <exception cref="ArgumentException">Thrown when the shape is not a <see cref="GeoRay3"/>, or not one of finite numbers and some direction.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ray is erased.</exception>
        public override void SetFromGeCurve(object geCurve)
        {
            if (!(geCurve is GeoRay3 ray))
            {
                throw new ArgumentException("A ray takes a GeoRay3, and was given " + (geCurve == null ? "null" : "a " + geCurve.GetType().Name) + ".", nameof(geCurve));
            }

            SetShape(ray);
        }

        /// <summary>
        /// Moves, turns, scales, mirrors or shears the ray, as AutoCAD's <c>TransformBy</c> does: its base point moved and its
        /// direction turned and made of unit length, as ezdxf moves one. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation takes the direction to no length.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ray is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            Moved(transform, BasePoint, UnitDir, out GeoPoint3 origin, out GeoPoint3 direction);
            SetGroups(Join(PointGroups(10, origin, Origin), PointGroups(11, direction, Up)));
        }

        [DxfType("RAY")]
        internal static DxfEntity Create(DxfItem item) => new DxfRay(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The groups of a ray or an xline made in code: its marker, the origin and the x axis.</summary>
        internal static IEnumerable<DxfTypedValue> Groups(string marker)
            => new[]
            {
                new DxfTypedValue(100, marker),
                new DxfTypedValue(10, "0"), new DxfTypedValue(20, "0"), new DxfTypedValue(30, "0"),
                new DxfTypedValue(11, "1"), new DxfTypedValue(21, "0"), new DxfTypedValue(31, "0"),
            };

        /// <summary>The direction of a ray or an xline made of unit length as the reader makes it: kept where it is within 1E-12 of that.</summary>
        /// <exception cref="InvalidOperationException">Thrown where the file gives a direction of no length.</exception>
        internal static GeoVector3 Direction(GeoVector3 raw)
        {
            double length = raw.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new InvalidOperationException("The direction " + raw + " of the record has no length.");
            }

            return Math.Abs(length - 1.0) <= 1E-12 ? raw : raw.Divide(length);
        }

        /// <summary>The shift that offsets a ray or an xline in the plane of a normal: to the right of its direction seen from the tip of the normal.</summary>
        internal static GeoVector3 Sideways(GeoVector3 planeNormal, GeoVector3 direction, double offsetDist)
        {
            Guard.Finite(offsetDist, nameof(offsetDist), "An offset has to be a finite number.");
            GeoVector3 side = direction.CrossProduct(planeNormal);
            double length = side.Length;

            if (!planeNormal.IsValid || !(length > Tolerance.Default.EqualVector * Math.Max(1.0, planeNormal.Length)) || double.IsInfinity(length))
            {
                throw new ArgumentException("The normal " + planeNormal + " has no length or runs along the line.", nameof(planeNormal));
            }

            return side.Multiply(offsetDist / length);
        }

        /// <summary>A line from one point to another of the look of a curve, in no file, as a piece of it.</summary>
        internal static DxfLine Segment(DxfCurve of, GeoPoint3 start, GeoPoint3 end)
        {
            var line = new DxfLine(start, end);
            line.SetPropertiesFrom(of);
            line.XData = of.XData;
            return line;
        }

        /// <summary>A ray or an xline moved as ezdxf moves one: the base point moved, the direction turned and made of unit length.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation takes the direction to no length.</exception>
        internal static void Moved(GeoTransform3 transform, GeoPoint3 origin, GeoVector3 raw, out GeoPoint3 newOrigin, out GeoPoint3 newDirection)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            newOrigin = transform.Transform(origin);
            GeoVector3 turned = transform.Transform(raw);
            double length = turned.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("The transformation takes the direction to no length.", nameof(transform));
            }

            GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? turned : turned.Divide(length);
            newDirection = new GeoPoint3(unit.X, unit.Y, unit.Z);
        }

        private static GeoVector3 Raw(GeoPoint3 point) => new GeoVector3(point.X, point.Y, point.Z);

        private static bool SamePoint(GeoPoint3 a, GeoPoint3 b) => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);

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

        /// <summary>Refuses a distance below nought by more than the point tolerance, or not a finite number.</summary>
        private static void CheckDistance(double value, string parameter)
        {
            Guard.Finite(value, parameter, "A parameter has to be a finite number.");
            Within(value, double.PositiveInfinity, parameter);
        }
    }
}
