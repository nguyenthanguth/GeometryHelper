using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An entity that is a curve, as AutoCAD's <c>Curve</c> is: a line, an arc, a circle, an ellipse, a polyline, a spline,
    /// a ray or an xline; walked by its parameter and by the distance along it, as AutoCAD walks one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The distances are measured on the library's shape for the entity, <see cref="GetGeCurve"/>, from its start: a curve
    /// 100 long runs from distance 0 to 100. The parameter is AutoCAD's for the type: the distance itself on a line, the
    /// angle in radians on an arc or a circle, and so on, each class saying which; this class takes it to be the distance
    /// from <see cref="StartParam"/> where a class says nothing else.
    /// </para>
    /// <para>
    /// A point is on the curve where it is within <see cref="Tolerance.Default"/> of it, as the shapes of a file are read; a
    /// parameter or a distance past either end by more than that is refused, as AutoCAD refuses one.
    /// </para>
    /// </remarks>
    public abstract class DxfCurve : DxfEntity
    {
        /// <summary>The object of a record of a file.</summary>
        private protected DxfCurve(DxfItem item)
            : base(item)
        {
        }

        /// <summary>A curve made in code from a shape, as <see cref="DxfEntity"/> makes one.</summary>
        private protected DxfCurve(object shape)
            : base(shape)
        {
        }

        /// <summary>A curve made in code from its groups, as <see cref="DxfEntity"/> makes one.</summary>
        private protected DxfCurve(string dxfName, IEnumerable<DxfTypedValue> groups)
            : base(dxfName, groups)
        {
        }

        /// <summary>
        /// Gets or sets the parameter at the start of the curve, as AutoCAD's <c>StartParam</c>: nought unless the class says
        /// otherwise; set where the class can move it, as an ellipse's.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when set on a curve whose start parameter the class does not move.</exception>
        public virtual double StartParam
        {
            get => 0.0;
            set => throw new NotSupportedException("The start parameter of a " + GetType().Name + " is not set.");
        }

        /// <summary>
        /// Gets or sets the parameter at the end of the curve, as AutoCAD's <c>EndParam</c>: its length past
        /// <see cref="StartParam"/> unless the class says otherwise; set where the class can move it, as an ellipse's.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the curve has no shape the library reads, as one of no length.</exception>
        /// <exception cref="NotSupportedException">Thrown when set on a curve whose end parameter the class does not move.</exception>
        public virtual double EndParam
        {
            get => GetParameterAtDistance(LengthOf(Curve()));
            set => throw new NotSupportedException("The end parameter of a " + GetType().Name + " is not set.");
        }

        /// <summary>
        /// Gets or sets the point where the curve starts, as AutoCAD's <c>StartPoint</c>; set where the class can move it.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when set on a curve whose start the class does not move.</exception>
        public virtual GeoPoint3 StartPoint
        {
            get => GetPointAtParameter(StartParam);
            set => throw new NotSupportedException("The start of a " + GetType().Name + " is not set.");
        }

        /// <summary>
        /// Gets or sets the point where the curve ends, as AutoCAD's <c>EndPoint</c>; set where the class can move it.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when set on a curve whose end the class does not move.</exception>
        public virtual GeoPoint3 EndPoint
        {
            get => GetPointAtParameter(EndParam);
            set => throw new NotSupportedException("The end of a " + GetType().Name + " is not set.");
        }

        /// <summary>
        /// Gets whether the curve is closed, as AutoCAD's <c>Closed</c>: a circle, a whole ellipse, a closed polyline or
        /// spline; false for a line.
        /// </summary>
        public virtual bool Closed
        {
            get
            {
                object shape = GetGeCurve();
                return shape is GeoCircle3 || shape is GeoEllipse3 || shape is GeoPolygon3 || shape is GeoPolygonArc3 || (shape is GeoSpline3 spline && spline.IsClosed(Tolerance.Default));
            }
        }

        /// <summary>
        /// Gets the area the curve closes, as AutoCAD's <c>Area</c>: that of a circle, a whole ellipse or a closed polyline;
        /// nought for an open curve unless the class says otherwise.
        /// </summary>
        public virtual double Area
        {
            get
            {
                switch (GetGeCurve())
                {
                    case GeoCircle3 circle: return circle.Area;
                    case GeoEllipse3 ellipse: return ellipse.Area;
                    case GeoPolygon3 polygon: return polygon.Area;
                    case GeoPolygonArc3 polygon: return polygon.Area;
                    default: return 0.0;
                }
            }
        }

        /// <summary>Gets whether the curve runs round on itself, as AutoCAD's <c>IsPeriodic</c>: false unless the class says otherwise.</summary>
        public virtual bool IsPeriodic => false;

        /// <summary>
        /// Gets the point at a parameter, as AutoCAD's <c>GetPointAtParameter</c>.
        /// </summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The point of the curve at the parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public virtual GeoPoint3 GetPointAtParameter(double value) => GetPointAtDist(GetDistanceAtParameter(value));

        /// <summary>
        /// Gets the parameter at a point of the curve, as AutoCAD's <c>GetParameterAtPoint</c>.
        /// </summary>
        /// <param name="point">The point, on the curve.</param>
        /// <returns>The parameter there.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the curve.</exception>
        public virtual double GetParameterAtPoint(GeoPoint3 point) => GetParameterAtDistance(GetDistAtPoint(point));

        /// <summary>
        /// Gets the distance along the curve from its start to a parameter, as AutoCAD's <c>GetDistanceAtParameter</c>.
        /// </summary>
        /// <param name="value">The parameter, from <see cref="StartParam"/> to <see cref="EndParam"/>.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is past either end.</exception>
        public virtual double GetDistanceAtParameter(double value)
        {
            double distance = value - StartParam;
            Within(distance, LengthOf(Curve()), nameof(value));
            return distance;
        }

        /// <summary>
        /// Gets the parameter at a distance along the curve from its start, as AutoCAD's <c>GetParameterAtDistance</c>.
        /// </summary>
        /// <param name="dist">The distance, from nought to the curve's length.</param>
        /// <returns>The parameter there.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public virtual double GetParameterAtDistance(double dist)
        {
            Within(dist, LengthOf(Curve()), nameof(dist));
            return dist + StartParam;
        }

        /// <summary>
        /// Gets the distance along the curve from its start to a point of it, as AutoCAD's <c>GetDistAtPoint</c>.
        /// </summary>
        /// <param name="point">The point, on the curve.</param>
        /// <returns>The distance.</returns>
        /// <exception cref="ArgumentException">Thrown when the point is not on the curve.</exception>
        public virtual double GetDistAtPoint(GeoPoint3 point)
        {
            object curve = Curve();
            OnCurve(curve, point);

            switch (curve)
            {
                case GeoLine3 s: return s.GetDistanceAtPoint(point);
                case GeoArc3 s: return s.GetDistanceAtPoint(point);
                case GeoCircle3 s: return s.GetDistanceAtPoint(point);
                case GeoEllipse3 s: return s.GetDistanceAtPoint(point);
                case GeoSpline3 s: return s.GetDistanceAtPoint(point);
                case GeoPolyline3 s: return s.GetDistanceAtPoint(point);
                case GeoPolygon3 s: return s.GetDistanceAtPoint(point);
                case GeoPolylineArc3 s: return s.GetDistanceAtPoint(point);
                case GeoPolygonArc3 s: return s.GetDistanceAtPoint(point);
                case GeoRay3 s: return s.GetDistanceAtPoint(point);
                case GeoXLine3 s: return s.GetDistanceAtPoint(point);
                default: throw NotACurve(curve);
            }
        }

        /// <summary>
        /// Gets the point at a distance along the curve from its start, as AutoCAD's <c>GetPointAtDist</c>.
        /// </summary>
        /// <param name="value">The distance, from nought to the curve's length.</param>
        /// <returns>The point there.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is past either end.</exception>
        public virtual GeoPoint3 GetPointAtDist(double value)
        {
            object curve = Curve();
            Within(value, LengthOf(curve), nameof(value));

            switch (curve)
            {
                case GeoLine3 s: return s.GetPointAtDistance(value);
                case GeoArc3 s: return s.GetPointAtDistance(value);
                case GeoCircle3 s: return s.GetPointAtDistance(value);
                case GeoEllipse3 s: return s.GetPointAtDistance(value);
                case GeoSpline3 s: return s.GetPointAtDistance(value);
                case GeoPolyline3 s: return s.GetPointAtDistance(value);
                case GeoPolygon3 s: return s.GetPointAtDistance(value);
                case GeoPolylineArc3 s: return s.GetPointAtDistance(value);
                case GeoPolygonArc3 s: return s.GetPointAtDistance(value);
                case GeoRay3 s: return s.GetPointAtDistance(value);
                case GeoXLine3 s: return s.GetPointAtDistance(value);
                default: throw NotACurve(curve);
            }
        }

        /// <summary>
        /// Gets the point of the curve nearest a point, as AutoCAD's <c>GetClosestPointTo</c>.
        /// </summary>
        /// <param name="givenPoint">The point.</param>
        /// <param name="extend">Whether the curve is taken as extended past its ends where its class can extend it: a line
        /// to the infinite line, an arc to its circle.</param>
        /// <returns>The nearest point.</returns>
        public virtual GeoPoint3 GetClosestPointTo(GeoPoint3 givenPoint, bool extend) => ClosestOn(Curve(), givenPoint);

        /// <summary>
        /// Gets the derivative of the curve at a parameter, as AutoCAD's <c>GetFirstDerivative</c>: how the point moves as the
        /// parameter grows.
        /// </summary>
        /// <param name="value">The parameter.</param>
        /// <returns>The derivative.</returns>
        /// <exception cref="NotSupportedException">Thrown where the class does not work it out.</exception>
        public virtual GeoVector3 GetFirstDerivative(double value) => throw NotHere("first derivative");

        /// <summary>
        /// Gets the derivative of the curve at a point of it, as AutoCAD's <c>GetFirstDerivative</c> at a point.
        /// </summary>
        /// <param name="point">The point, on the curve.</param>
        /// <returns>The derivative at the parameter of the point.</returns>
        public GeoVector3 GetFirstDerivative(GeoPoint3 point) => GetFirstDerivative(GetParameterAtPoint(point));

        /// <summary>Gets the second derivative of the curve at a parameter, as AutoCAD's <c>GetSecondDerivative</c>.</summary>
        /// <param name="value">The parameter.</param>
        /// <returns>The second derivative.</returns>
        /// <exception cref="NotSupportedException">Thrown where the class does not work it out.</exception>
        public virtual GeoVector3 GetSecondDerivative(double value) => throw NotHere("second derivative");

        /// <summary>Gets the second derivative of the curve at a point of it, as AutoCAD's <c>GetSecondDerivative</c> at a point.</summary>
        /// <param name="point">The point, on the curve.</param>
        /// <returns>The second derivative at the parameter of the point.</returns>
        public GeoVector3 GetSecondDerivative(GeoPoint3 point) => GetSecondDerivative(GetParameterAtPoint(point));

        /// <summary>
        /// Gets the curves at a distance from this one, as AutoCAD's <c>GetOffsetCurves</c>: new entities of the same look, in
        /// no file, to be added to one.
        /// </summary>
        /// <param name="offsetDist">The distance, as the class says which side it runs to.</param>
        /// <returns>The curves.</returns>
        /// <exception cref="NotSupportedException">Thrown where the class does not offset.</exception>
        public virtual IReadOnlyList<DxfCurve> GetOffsetCurves(double offsetDist) => throw NotHere("offset");

        /// <summary>
        /// Gets the curves at a distance from this one within the plane of a normal, as AutoCAD's
        /// <c>GetOffsetCurvesGivenPlaneNormal</c>.
        /// </summary>
        /// <param name="planeNormal">The normal of the plane the curve is offset in.</param>
        /// <param name="offsetDist">The distance, as the class says which side it runs to.</param>
        /// <returns>The curves.</returns>
        /// <exception cref="NotSupportedException">Thrown where the class does not offset.</exception>
        public virtual IReadOnlyList<DxfCurve> GetOffsetCurvesGivenPlaneNormal(GeoVector3 planeNormal, double offsetDist) => throw NotHere("offset");

        /// <summary>Turns the curve to run the other way, from its end to its start, as AutoCAD's <c>ReverseCurve</c>.</summary>
        /// <exception cref="NotSupportedException">Thrown where the class does not turn it.</exception>
        public virtual void ReverseCurve() => throw NotHere("reversal");

        /// <summary>Extends the curve to a parameter past one of its ends, as AutoCAD's <c>Extend</c>.</summary>
        /// <param name="newParameter">The parameter the curve is to reach, below <see cref="StartParam"/> or above <see cref="EndParam"/>.</param>
        /// <exception cref="NotSupportedException">Thrown where the class does not extend.</exception>
        public virtual void Extend(double newParameter) => throw NotHere("extension");

        /// <summary>Extends the curve at one end to a point, as AutoCAD's <c>Extend</c>.</summary>
        /// <param name="extendStart">Whether the start is moved, else the end.</param>
        /// <param name="toPoint">The point, on the curve extended.</param>
        /// <exception cref="NotSupportedException">Thrown where the class does not extend.</exception>
        public virtual void Extend(bool extendStart, GeoPoint3 toPoint) => throw NotHere("extension");

        /// <summary>Gets the pieces the curve is cut into at parameters, as AutoCAD's <c>GetSplitCurves</c>: new entities in no file.</summary>
        /// <param name="parameters">The parameters to cut at.</param>
        /// <returns>The pieces, in the order of the curve.</returns>
        /// <exception cref="NotSupportedException">Thrown where the class does not split.</exception>
        public virtual IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<double> parameters) => throw NotHere("splitting");

        /// <summary>Gets the pieces the curve is cut into at points of it, as AutoCAD's <c>GetSplitCurves</c>: new entities in no file.</summary>
        /// <param name="points">The points to cut at, on the curve.</param>
        /// <returns>The pieces, in the order of the curve.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a point is not on the curve.</exception>
        /// <exception cref="NotSupportedException">Thrown where the class does not split.</exception>
        public IReadOnlyList<DxfCurve> GetSplitCurves(IReadOnlyList<GeoPoint3> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var parameters = new List<double>(points.Count);

            foreach (GeoPoint3 point in points)
            {
                parameters.Add(GetParameterAtPoint(point));
            }

            return GetSplitCurves(parameters);
        }

        /// <summary>
        /// Gets the library's shape for the curve, as AutoCAD's <c>GetGeCurve</c> gives its geometry's: the one shape of
        /// <see cref="DxfEntity.GetGeometry"/>, a <see cref="GeoLine3"/> for a line; null where the reader reads none, as for a
        /// line of no length.
        /// </summary>
        /// <returns>The shape.</returns>
        public object GetGeCurve()
        {
            IReadOnlyList<object> shapes = GetGeometry();
            return shapes.Count == 1 ? shapes[0] : null;
        }

        /// <summary>Gives the curve the geometry of a shape of the library, as AutoCAD's <c>SetFromGeCurve</c>: a line a <see cref="GeoLine3"/>.</summary>
        /// <param name="geCurve">The shape, of the kind the class takes.</param>
        /// <exception cref="NotSupportedException">Thrown where the class takes none.</exception>
        public virtual void SetFromGeCurve(object geCurve) => throw NotHere("setting from a shape");

        /// <summary>
        /// Makes the curve of a shape of the library, as AutoCAD's <c>CreateFromGeCurve</c>: a <see cref="DxfLine"/> of a
        /// <see cref="GeoLine3"/>, in no file, on layer 0 with no look of its own.
        /// </summary>
        /// <param name="geCurve">The shape.</param>
        /// <returns>The curve, of the class its type has.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the shape is not written as a curve the library has a class of.</exception>
        public static DxfCurve CreateFromGeCurve(object geCurve)
        {
            if (geCurve == null)
            {
                throw new ArgumentNullException(nameof(geCurve));
            }

            if (Of(DxfFile.Detached(geCurve)) is DxfCurve curve)
            {
                return curve;
            }

            throw new ArgumentException("A " + geCurve.GetType().Name + " is not written as a curve of a class of the library's.", nameof(geCurve));
        }

        /// <summary>The library's shape for the curve; refused where the reader reads none, as for a line of no length.</summary>
        private protected object Curve()
        {
            object curve = GetGeCurve();

            if (curve == null)
            {
                throw new InvalidOperationException("The " + Item.Type + " has no shape the library reads, as one of no length has none, to be walked.");
            }

            return curve;
        }

        /// <summary>The length of a shape; infinite for a ray and an xline.</summary>
        private protected static double LengthOf(object curve)
        {
            switch (curve)
            {
                case GeoLine3 s: return s.Length;
                case GeoArc3 s: return s.Length;
                case GeoCircle3 s: return s.Length;
                case GeoEllipse3 s: return s.Length;
                case GeoSpline3 s: return s.Length;
                case GeoPolyline3 s: return s.Length;
                case GeoPolygon3 s: return s.Length;
                case GeoPolylineArc3 s: return s.Length;
                case GeoPolygonArc3 s: return s.Length;
                case GeoRay3 _: return double.PositiveInfinity;
                case GeoXLine3 _: return double.PositiveInfinity;
                default: throw NotACurve(curve);
            }
        }

        /// <summary>The point of a shape nearest a point.</summary>
        private protected static GeoPoint3 ClosestOn(object curve, GeoPoint3 point)
        {
            switch (curve)
            {
                case GeoLine3 s: return s.GetClosestPointOnBoundary(point);
                case GeoArc3 s: return s.GetClosestPointOnBoundary(point);
                case GeoCircle3 s: return s.GetClosestPointOnBoundary(point);
                case GeoEllipse3 s: return s.GetClosestPointOnBoundary(point);
                case GeoSpline3 s: return s.GetClosestPointOnBoundary(point);
                case GeoPolyline3 s: return s.GetClosestPointOnBoundary(point);
                case GeoPolygon3 s: return s.GetClosestPointOnBoundary(point);
                case GeoPolylineArc3 s: return s.GetClosestPointOnBoundary(point);
                case GeoPolygonArc3 s: return s.GetClosestPointOnBoundary(point);
                case GeoRay3 s: return s.GetClosestPointOnBoundary(point);
                case GeoXLine3 s: return s.GetClosestPointOnBoundary(point);
                default: throw NotACurve(curve);
            }
        }

        /// <summary>Refuses a point farther from a shape than <see cref="Tolerance.Default"/>.</summary>
        private protected static void OnCurve(object curve, GeoPoint3 point)
        {
            if (!ClosestOn(curve, point).IsEqualTo(point, Tolerance.Default))
            {
                throw new ArgumentException("The point " + point + " is not on the curve.", nameof(point));
            }
        }

        /// <summary>Refuses a distance below nought or past a length by more than <see cref="Tolerance.Default"/>, or not a number.</summary>
        private protected static void Within(double distance, double length, string parameter)
        {
            double slack = Tolerance.Default.EqualPoint;

            if (double.IsNaN(distance) || distance < -slack || distance > length + slack)
            {
                throw new ArgumentOutOfRangeException(parameter, distance, "The value runs past the curve, which runs from nought to " + length.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " along it.");
            }
        }

        private static ArgumentException NotACurve(object curve)
            => new ArgumentException("A " + (curve == null ? "null" : curve.GetType().Name) + " is not a curve the library walks.", nameof(curve));

        private NotSupportedException NotHere(string what) => new NotSupportedException("A " + GetType().Name + " has no " + what + " of the library's yet.");
    }
}
