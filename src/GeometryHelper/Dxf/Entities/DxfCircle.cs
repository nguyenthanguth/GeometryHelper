using System;
using System.Collections.Generic;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A CIRCLE of a DXF file, as AutoCAD's <c>Circle</c>: the circle of <see cref="Radius"/> about <see cref="Center"/>, in
    /// the plane square to its <see cref="Normal"/>, with the <see cref="Thickness"/> it stands up along the normal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The circle of radius 5 about (10, 20, 0) is <see cref="Diameter"/> 10, <see cref="Circumference"/> 31.416 and
    /// <see cref="DxfCurve.Area"/> 78.540. Its parameter is the angle in radians, as AutoCAD's is: from nought at the point
    /// on the x axis of the normal's own system, (15, 20, 0) here, counter-clockwise seen from the tip of the normal to
    /// 2 pi back at that point.
    /// </para>
    /// <para>
    /// The file holds the centre in the system the normal makes by the arbitrary axis algorithm of the DXF reference; the
    /// centre here is in world coordinates, as AutoCAD gives it, and is written back into that system. A circle of a file
    /// whose normal is (0, 0, -1) and whose groups 10, 20 and 30 are (10, 20, 5) has its centre at (-10, 20, -5). Setting
    /// the normal keeps the centre where it is in the world. Every other group of the record is kept as it was;
    /// <c>new DxfCircle(center, normal, radius) { Layer = "steel" }</c> makes one in code, to be added to a drawing by
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.
    /// </para>
    /// </remarks>
    public sealed class DxfCircle : DxfCurve
    {
        // The groups of AcDbCircle in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 39, 10, 20, 30, 40, 210, 220, 230 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private static readonly GeoVector3 Up = new GeoVector3(0.0, 0.0, 1.0);

        private const double Turn = 2.0 * Math.PI;

        /// <summary>
        /// Initializes a circle of no radius at the origin on the normal (0, 0, 1), as AutoCAD's <c>new Circle()</c>, on layer 0
        /// in no file; give it its radius before it is drawn.
        /// </summary>
        public DxfCircle()
            : this(Origin, Up, 0.0)
        {
        }

        /// <summary>
        /// Initializes a circle about a centre in the plane square to a normal, as AutoCAD's <c>new Circle(center, normal,
        /// radius)</c>, on layer 0 with no look of its own, in no file until it is added to one.
        /// </summary>
        /// <param name="center">The centre, in world coordinates.</param>
        /// <param name="normal">The normal of the circle's plane, of any length; written made of unit length.</param>
        /// <param name="radius">The radius, nought or more.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, or the normal has no length.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is below nought or not a finite number.</exception>
        public DxfCircle(GeoPoint3 center, GeoVector3 normal, double radius)
            : base("CIRCLE", Groups(center, normal, radius))
        {
        }

        private DxfCircle(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the centre of the circle, in world coordinates, as AutoCAD's <c>Center</c>: the groups 10, 20 and 30
        /// taken from the system of the <see cref="Normal"/> into the world; the origin where the record gives none.
        /// </summary>
        /// <value>Set, a point in world coordinates, written in the system of the normal.</value>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
        public GeoPoint3 Center
        {
            get => OcsToWorld(GetNormal(), GetPoint(10, Origin));

            set
            {
                if (!SamePoint(value, Center))
                {
                    SetPoint(10, WorldToOcs(GetNormal(), value), Origin);
                }
            }
        }

        /// <summary>Gets or sets the radius of the circle (group 40), as AutoCAD's <c>Radius</c>; 1 where the record gives none.</summary>
        /// <value>Set, a finite number, nought or more.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius set is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
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
        /// Gets or sets the normal of the circle (groups 210, 220, 230), square to its plane, of unit length; (0, 0, 1) where
        /// the record gives none. Set, the centre keeps its place in the world, as AutoCAD keeps it: the circle turns about it.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();
            set => SetNormalKeepingCentre(value);
        }

        /// <summary>
        /// Gets or sets how far the circle stands up along its <see cref="Normal"/> (group 39), a tube of that height; nought
        /// where the record gives none.
        /// </summary>
        /// <value>Set, a finite number; nought takes the group out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
        public double Thickness
        {
            get => GetThickness();
            set => SetThickness(value);
        }

        /// <summary>Gets or sets the diameter of the circle, twice its <see cref="Radius"/>, as AutoCAD's <c>Diameter</c>.</summary>
        /// <value>Set, a finite number, nought or more; the radius is set to half of it.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the diameter set is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
        public double Diameter
        {
            get => 2.0 * Radius;

            set
            {
                if (!Same(value, Diameter))
                {
                    Radius = value / 2.0;
                }
            }
        }

        /// <summary>Gets or sets the length round the circle, 2 pi times its <see cref="Radius"/>, as AutoCAD's <c>Circumference</c>.</summary>
        /// <value>Set, a finite number, nought or more; the radius is set to it over 2 pi.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is below nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
        public double Circumference
        {
            get => Turn * Radius;

            set
            {
                if (!Same(value, Circumference))
                {
                    Radius = value / Turn;
                }
            }
        }

        /// <summary>Gets the parameter at the end of the circle: 2 pi, the angle of a whole turn, as AutoCAD's.</summary>
        public override double EndParam => Turn;

        /// <summary>Gets true: a circle is closed.</summary>
        public override bool Closed => true;

        /// <summary>Gets true: a circle runs round on itself, as AutoCAD's <c>IsPeriodic</c> says of one.</summary>
        public override bool IsPeriodic => true;

        /// <summary>Gets the area within the circle, pi times the square of its radius; 78.540 for a radius of 5.</summary>
        public override double Area
        {
            get
            {
                double radius = Radius;
                return Math.PI * radius * radius;
            }
        }

        /// <summary>
        /// Gets the point at a parameter, an angle in radians from the x axis of the normal's own system, as AutoCAD's
        /// <c>GetPointAtParameter</c>.
        /// </summary>
        /// <param name="value">The angle, from nought to 2 pi.</param>
        /// <returns>The point, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is past either end by more than the point tolerance along the circle.</exception>
        public override GeoPoint3 GetPointAtParameter(double value)
        {
            double radius = Radius;
            CheckAngle(value, radius, nameof(value));
            return At(GetNormal(), GetPoint(10, Origin), radius, value);
        }

        /// <summary>
        /// Gets the parameter at a point of the circle, its angle from the x axis of the normal's own system, as AutoCAD's
        /// <c>GetParameterAtPoint</c>: from nought up to 2 pi.
        /// </summary>
        /// <param name="point">The point, within the point tolerance of the circle.</param>
        /// <returns>The angle, in radians.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the circle.</exception>
        public override double GetParameterAtPoint(GeoPoint3 point)
        {
            GeoVector3 normal = GetNormal();
            GeoPoint3 centre = GetPoint(10, Origin);
            GeoPoint3 local = WorldToOcs(normal, point);
            double dx = local.X - centre.X;
            double dy = local.Y - centre.Y;
            double slack = Tolerance.Default.EqualPoint;

            if (!(Math.Abs(Math.Sqrt(dx * dx + dy * dy) - Radius) <= slack && Math.Abs(local.Z - centre.Z) <= slack))
            {
                throw new ArgumentException("The point " + point + " is not on the circle.", nameof(point));
            }

            double angle = Math.Atan2(dy, dx);
            return angle < 0.0 ? angle + Turn : angle;
        }

        /// <summary>Gets the distance along the circle from its start to a parameter: the radius times the angle.</summary>
        /// <param name="value">The angle, from nought to 2 pi.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is past either end.</exception>
        public override double GetDistanceAtParameter(double value)
        {
            double radius = Radius;
            CheckAngle(value, radius, nameof(value));
            return radius * value;
        }

        /// <summary>Gets the parameter at a distance along the circle from its start: the distance over the radius.</summary>
        /// <param name="dist">The distance, from nought to the <see cref="Circumference"/>.</param>
        /// <returns>The angle, in radians; nought on a circle of no radius.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override double GetParameterAtDistance(double dist)
        {
            double radius = Radius;
            Within(dist, Turn * radius, nameof(dist));
            return radius > 0.0 ? dist / radius : 0.0;
        }

        /// <summary>Gets the distance along the circle from its start to a point of it.</summary>
        /// <param name="point">The point, on the circle.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the circle.</exception>
        public override double GetDistAtPoint(GeoPoint3 point) => Radius * GetParameterAtPoint(point);

        /// <summary>Gets the point at a distance along the circle from its start.</summary>
        /// <param name="value">The distance, from nought to the <see cref="Circumference"/>.</param>
        /// <returns>The point, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public override GeoPoint3 GetPointAtDist(double value)
        {
            double radius = Radius;
            Within(value, Turn * radius, nameof(value));
            return At(GetNormal(), GetPoint(10, Origin), radius, radius > 0.0 ? value / radius : 0.0);
        }

        /// <summary>
        /// Gets the point of the circle nearest a point, as AutoCAD's <c>GetClosestPointTo</c>: that of the point seen along
        /// the normal; the start for the centre itself or a point on the axis through it.
        /// </summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Of no account: a circle has no ends to extend.</param>
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

            if (!(length > 0.0))
            {
                return At(normal, centre, radius, 0.0);
            }

            return OcsToWorld(normal, new GeoPoint3(centre.X + radius * (dx / length), centre.Y + radius * (dy / length), centre.Z));
        }

        /// <summary>
        /// Gets the derivative of the circle at a parameter, as AutoCAD's <c>GetFirstDerivative</c>: the tangent, of the
        /// length of the radius, pointing the way the angle grows.
        /// </summary>
        /// <param name="value">The angle, from nought to 2 pi.</param>
        /// <returns>The derivative, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is past either end.</exception>
        public override GeoVector3 GetFirstDerivative(double value)
        {
            double radius = Radius;
            CheckAngle(value, radius, nameof(value));
            return Turned(GetNormal(), -radius * Math.Sin(value), radius * Math.Cos(value));
        }

        /// <summary>Gets the second derivative of the circle at a parameter: from the point to the centre, of the length of the radius.</summary>
        /// <param name="value">The angle, from nought to 2 pi.</param>
        /// <returns>The second derivative, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is past either end.</exception>
        public override GeoVector3 GetSecondDerivative(double value)
        {
            double radius = Radius;
            CheckAngle(value, radius, nameof(value));
            return Turned(GetNormal(), -radius * Math.Cos(value), -radius * Math.Sin(value));
        }

        /// <summary>
        /// Gets the circle at a distance from this one, as AutoCAD's <c>GetOffsetCurves</c>: about the same centre, its radius
        /// the larger by the distance, so that a distance above nought offsets it outwards.
        /// </summary>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one circle, of the same look, in no file.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number, or takes the radius to nought or below.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist)
        {
            Guard.Finite(offsetDist, nameof(offsetDist), "An offset has to be a finite number.");
            double radius = Radius + offsetDist;

            if (!(radius > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(offsetDist), offsetDist, "The offset takes the radius of the circle to nought or below.");
            }

            var copy = (DxfCircle)Copy();
            copy.Radius = radius;
            return new DxfCurve[] { copy };
        }

        /// <summary>
        /// Gets the circle at a distance from this one in the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>: as <see cref="GetOffsetCurves(double)"/>, the plane being the circle's own, its
        /// radius growing with a distance above nought whichever way the normal given points, as AutoCAD offsets a circle by its
        /// radius. An arc, a circle and an ellipse are the exceptions to the side every other curve offsets to, the right seen from
        /// the tip of the normal given.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane, along the circle's <see cref="Normal"/> either way.</param>
        /// <param name="offsetDist">The distance.</param>
        /// <returns>The one circle, of the same look, in no file.</returns>
        /// <exception cref="ArgumentException">Thrown when the normal is not along the circle's within the vector tolerance.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number, or takes the radius to nought or below.</exception>
        public override IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist)
        {
            if (!planeNormal.IsValid || planeNormal.IsZeroLength(Tolerance.Default) || !planeNormal.IsParallelTo(GetNormal(), Tolerance.Default))
            {
                throw new ArgumentException("A circle is offset in its own plane: the normal " + planeNormal + " is not along the circle's.", nameof(planeNormal));
            }

            return GetOffsetCurves(offsetDist);
        }

        /// <summary>
        /// Turns the circle to run the other way, as AutoCAD's <c>ReverseCurve</c>: its normal turned over, its centre kept
        /// where it is in the world.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
        public override void ReverseCurve() => SetNormalKeepingCentre(GetNormal().Negate());

        /// <summary>Refuses, as AutoCAD does: a circle is closed and has no end to be extended.</summary>
        /// <param name="newParameter">The parameter.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override void Extend(double newParameter) => throw new NotSupportedException("A circle is closed: it has no end to be extended.");

        /// <summary>Refuses, as AutoCAD does: a circle is closed and has no end to be extended.</summary>
        /// <param name="extendStart">Whether the start is moved.</param>
        /// <param name="toPoint">The point.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public override void Extend(bool extendStart, GeoPoint3 toPoint) => throw new NotSupportedException("A circle is closed: it has no end to be extended.");

        /// <summary>
        /// Gets the pieces the circle is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: arcs of the same look in
        /// no file, from each angle to the next counter-clockwise and from the last round to the first. The angles are taken
        /// round the circle from nought; one within the point tolerance along the circle of another cuts nothing, and a circle
        /// cut at fewer than two places is given whole, as a copy.
        /// </summary>
        /// <param name="parameters">The angles to cut at, from nought to 2 pi.</param>
        /// <returns>The pieces, in the order of the circle from its first cut.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the parameters are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a parameter is past either end.</exception>
        public override IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            double radius = Radius;
            var cuts = new List<double>();

            foreach (double parameter in parameters)
            {
                CheckAngle(parameter, radius, nameof(parameters));
                double angle = parameter >= Turn ? parameter - Turn : Math.Max(0.0, parameter);
                cuts.Add(angle);
            }

            cuts.Sort();
            double slack = radius > 0.0 ? Tolerance.Default.EqualPoint / radius : 0.0;
            var kept = new List<double>();

            foreach (double cut in cuts)
            {
                if (kept.Count == 0 || cut - kept[kept.Count - 1] > slack)
                {
                    kept.Add(cut);
                }
            }

            if (kept.Count > 1 && kept[0] + Turn - kept[kept.Count - 1] <= slack)
            {
                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count < 2)
            {
                return new DxfCurve[] { (DxfCircle)Copy() };
            }

            GeoPoint3 center = Center;
            GeoVector3 normal = GetNormal();
            double thickness = Thickness;
            var pieces = new List<DxfCurve>();

            for (int i = 0; i < kept.Count; i++)
            {
                double end = i + 1 < kept.Count ? kept[i + 1] : kept[0];
                var arc = new DxfArc(center, normal, radius, kept[i], end);
                arc.SetPropertiesFrom(this);
                arc.Thickness = thickness;
                arc.XData = XData;
                pieces.Add(arc);
            }

            return pieces;
        }

        /// <summary>Gives the circle the centre, normal and radius of a <see cref="GeoCircle3"/>, as AutoCAD's <c>SetFromGeCurve</c>, written as <see cref="DxfWriter"/> writes one.</summary>
        /// <param name="geCurve">The circle, a <see cref="GeoCircle3"/>.</param>
        /// <exception cref="ArgumentException">Thrown when the shape is not a <see cref="GeoCircle3"/>, or not one of finite numbers and some radius.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
        public override void SetFromGeCurve(object geCurve)
        {
            if (!(geCurve is GeoCircle3 circle))
            {
                throw new ArgumentException("A circle takes a GeoCircle3, and was given " + (geCurve == null ? "null" : "a " + geCurve.GetType().Name) + ".", nameof(geCurve));
            }

            SetShape(circle);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the circle, as AutoCAD's <c>TransformBy</c> does: its centre, normal, radius and
        /// thickness are carried as ezdxf carries a circle's, in the system of its normal, the new normal square to the
        /// images of the old system's x and y axes. The record is written once.
        /// </summary>
        /// <param name="transform">The transformation, one that scales the circle's plane alike both ways.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation scales the plane of the circle more one way than
        /// the other or shears it, which would make it an ellipse, as AutoCAD refuses one, or flattens it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (Unmoved(transform))
            {
                return;
            }

            double thickness = Thickness;
            Moved(transform, GetNormal(), GetPoint(10, Origin), Radius, thickness, out GeoVector3 normal, out GeoPoint3 centre, out double radius, out double raised);
            SetGroups(Join(NormalGroups(normal), PointGroups(10, centre, Origin), NumberGroups(40, radius, Radius)));

            if (thickness != 0.0)
            {
                Thickness = raised;
            }
        }

        [DxfType("CIRCLE")]
        internal static DxfEntity Create(DxfItem item) => new DxfCircle(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>
        /// Whether a transformation is the identity to the bit, which moves nothing: the record is then left as it is, where
        /// working it through the system of the normal and back could change a last digit.
        /// </summary>
        internal static bool Unmoved(GeoTransform3 transform)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    if (!Same(transform[row, column], row == column ? 1.0 : 0.0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// A circle or an arc moved as ezdxf's <c>OCSTransform</c> moves one: the new normal is the image of the old system's x
        /// axis crossed with that of its y axis, the centre is taken into the world, moved and taken into the new system, the
        /// radius is the length of the image of the radius along the old x axis, and the thickness is the height along the
        /// new normal of the image of the old thickness. Refused where the images of the two axes differ in length or are not
        /// square.
        /// </summary>
        internal static void Moved(GeoTransform3 transform, GeoVector3 normal, GeoPoint3 centre, double radius, double thickness, out GeoVector3 newNormal, out GeoPoint3 newCentre, out double newRadius, out double newThickness)
        {
            OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
            GeoVector3 x = transform.Transform(u);
            GeoVector3 y = transform.Transform(v);
            GeoVector3 cross = x.CrossProduct(y);
            double length = cross.Length;

            // ezdxf's test of a plane scaled alike both ways, math.isclose(|x|², |y|², rel_tol=1e-9, abs_tol=1e-9), and the axes
            // kept square: a shear of axes of one length makes an ellipse of a circle all the same.
            if (!DxfOcsTransform.IsUniform(x, y) || !(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("The transformation scales the plane of the circle more one way than the other, shears it or flattens it: a circle stays a circle only under one that scales it alike.", nameof(transform));
            }

            newNormal = cross.Divide(length);
            newCentre = WorldToOcs(newNormal, transform.Transform(OcsToWorld(normal, centre)));
            newRadius = transform.Transform(u.Multiply(radius)).Length;
            GeoVector3 raised = transform.Transform(normal.Multiply(thickness));
            newThickness = WorldToOcs(newNormal, new GeoPoint3(raised.X, raised.Y, raised.Z)).Z;
        }

        /// <summary>A direction of the system of a normal taken into the world.</summary>
        internal static GeoVector3 Turned(GeoVector3 normal, double x, double y)
        {
            GeoPoint3 world = OcsToWorld(normal, new GeoPoint3(x, y, 0.0));
            return new GeoVector3(world.X, world.Y, world.Z);
        }

        /// <summary>The point at an angle of a circle given by its normal and its centre in the normal's system, in world coordinates.</summary>
        internal static GeoPoint3 At(GeoVector3 normal, GeoPoint3 centre, double radius, double angle)
            => OcsToWorld(normal, new GeoPoint3(centre.X + Math.Cos(angle) * radius, centre.Y + Math.Sin(angle) * radius, centre.Z));

        /// <summary>The group that sets a number, written as <see cref="DxfWriter"/> writes one; none where it is the number read, to the bit.</summary>
        private static KeyValuePair<int, string>[] NumberGroups(int code, double value, double now)
            => Same(value, now) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0)) };

        /// <summary>Whether two points are the same to the bit.</summary>
        private static bool SamePoint(GeoPoint3 a, GeoPoint3 b) => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);

        /// <summary>The groups of a circle made in code: its marker, its centre in the system of its normal, its radius and its normal where it is not (0, 0, 1).</summary>
        private static IEnumerable<DxfTypedValue> Groups(GeoPoint3 center, GeoVector3 normal, double radius)
        {
            GeoVector3 unit = Unit(normal, nameof(normal));
            Guard.NonNegative(radius, nameof(radius), "A radius has to be a finite number, nought or more.");

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

            return groups;
        }

        /// <summary>A normal made of unit length as the record is written: kept where it is within 1E-12 of that; refused where it has no length.</summary>
        internal static GeoVector3 Unit(GeoVector3 normal, string parameter)
        {
            double length = normal.Length;

            if (!normal.IsValid || !(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A normal has to be a direction of finite numbers and some length: " + normal + " is not.", parameter);
            }

            return Math.Abs(length - 1.0) <= 1E-12 ? normal : normal.Divide(length);
        }

        /// <summary>Sets the normal, the centre written anew in its system so that it keeps its place in the world, in one writing.</summary>
        private void SetNormalKeepingCentre(GeoVector3 value)
        {
            GeoVector3 unit = Unit(value, nameof(value));
            GeoVector3 now = GetNormal();

            if (Same(unit.X, now.X) && Same(unit.Y, now.Y) && Same(unit.Z, now.Z))
            {
                return;
            }

            GeoPoint3 center = Center;
            SetGroups(Join(NormalGroups(unit), PointGroups(10, WorldToOcs(unit, center), Origin)));
        }

        /// <summary>Refuses an angle past either end of the circle by more than the point tolerance along it, or not a number.</summary>
        private static void CheckAngle(double value, double radius, string parameter)
        {
            Guard.Finite(value, parameter, "A parameter has to be a finite number.");
            Within(value * radius, Turn * radius, parameter);
        }
    }
}
