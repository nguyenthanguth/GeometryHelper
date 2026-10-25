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
        // The least ratio of an ellipse's axes AutoCAD takes: a conic projected thinner than this is a line.
        private const double MinRatio = 1E-6;

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

        /// <summary>
        /// Gets the spline that is exactly the curve, as AutoCAD's <c>Spline</c>: a new <see cref="DxfSpline"/> in no file, on
        /// layer 0 with no look of its own (give it the curve's with <see cref="DxfEntity.SetPropertiesFrom(DxfEntity)"/>), its
        /// knots the curve's parameters at the ends of its pieces. A line is a spline of degree 1 from nought to its length; an
        /// arc, a circle and an ellipse the rational quadratic of a piece for each quarter turn or less, over their angles or
        /// parameters (a circle of radius 10 is 9 control points over 0 to 2 pi, at a quarter turn on the circle's point there,
        /// between such knots on the circle but not at the same angle); a polyline of the plane the same of its arcs and its
        /// lines, each segment over one step of its parameter, or of degree 1 where it has no arc; a 3D polyline of degree 1 over
        /// its parameter; a spline its own data.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown for a ray or an xline, which run without end.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the curve has no shape the library reads, as a line of no length.</exception>
        public virtual DxfSpline Spline
        {
            get
            {
                var points = new List<GeoPoint3>();
                var weights = new List<double>();
                var knots = new List<double>();
                int degree = 2;
                bool closed = false;

                switch (this)
                {
                    case DxfLine line:
                        degree = 1;
                        points.Add(line.StartPoint);
                        points.Add(line.EndPoint);
                        knots.AddRange(new[] { 0.0, 0.0, line.Length, line.Length });

                        if (!(line.Length > 0.0))
                        {
                            throw new InvalidOperationException("A line of no length has no spline.");
                        }

                        break;

                    case DxfCircle circle:
                        closed = true;
                        DxfCurveTools.Conic(circle.Center, circle.GetPointAtParameter, 0.0, circle.EndParam, true, points, weights, knots);
                        break;

                    case DxfArc arc:
                        DxfCurveTools.Conic(arc.Center, arc.GetPointAtParameter, arc.StartParam, arc.EndParam, false, points, weights, knots);
                        break;

                    case DxfEllipse ellipse:
                        closed = ellipse.Closed;
                        DxfCurveTools.Conic(ellipse.Center, ellipse.GetPointAtParameter, ellipse.StartParam, ellipse.EndParam, closed, points, weights, knots);
                        break;

                    case DxfPolyline polyline:
                        closed = polyline.Closed;
                        degree = Chain(polyline.Model(), closed, polyline.Normal, points, weights, knots);
                        break;

                    case DxfPolyline2d polyline:
                        closed = polyline.Closed;
                        degree = Chain(polyline.Model(out _), closed, polyline.Normal, points, weights, knots);
                        break;

                    case DxfPolyline3d polyline:
                        closed = polyline.Closed;
                        degree = Chain(polyline.Model(out _), closed, null, points, weights, knots);
                        break;

                    default:
                        throw new NotSupportedException("A " + GetType().Name + " runs without end, and no spline is it.");
                }

                bool rational = weights.Exists(w => w != 1.0);
                return new DxfSpline(degree, rational, closed, false, points, knots, rational ? weights : null, 1E-10, 1E-10);
            }
        }

        /// <summary>
        /// Gets the curve projected onto a plane along a direction, as AutoCAD's <c>GetProjectedCurve</c>: a new curve of the
        /// same look in no file, each point of it moved along the direction until it meets the plane. A line, ray, xline, 3D
        /// polyline and spline stay of their kind, their points projected (a spline's fit data taken out, as the projected
        /// control points are its curve exactly); an arc, circle or ellipse in a plane parallel to the plane stays of its kind,
        /// moved, and otherwise becomes the part of the ellipse it projects to (a circle of radius 10 on the floor projected
        /// along (0, 0, 1) onto the plane y + z = 0 is an ellipse of radii 14.14 and 10); a polyline of the plane stays one where
        /// its plane is parallel or it has no arc, its vertices taken into the new plane, and otherwise becomes the
        /// <see cref="DxfSpline"/> of its projected <see cref="Spline"/>, its arcs being parts of ellipses there. A curve that
        /// projects to a point, its length there within the point tolerance (a 3D polyline standing along the direction), is
        /// refused, and so is a conic seen edge on, or so nearly that its minor axis is within the point tolerance or 1E-6 of its
        /// major one, AutoCAD's least ratio of an ellipse's axes: it projects to a line.
        /// </summary>
        /// <param name="planeToProjectOn">The plane.</param>
        /// <param name="projectionDirection">The direction to project along, not in the plane.</param>
        /// <returns>The projected curve.</returns>
        /// <exception cref="ArgumentException">Thrown when the direction has no length, is not finite, or runs along the plane
        /// within the vector tolerance.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the curve projects to a point or, for a conic seen edge on, to a
        /// line, or has no shape the library reads.</exception>
        public virtual DxfCurve GetProjectedCurve(GeoPlane3 planeToProjectOn, GeoVector3 projectionDirection)
        {
            DxfCurve result = Projected(planeToProjectOn, projectionDirection);
            double along;

            switch (result)
            {
                case DxfPolyline p: along = p.NumberOfVertices < 2 ? 0.0 : p.Length; break;
                case DxfPolyline2d p: along = p.Model(out _).Count < 2 ? 0.0 : p.Length; break;
                case DxfPolyline3d p: along = p.Model(out _).Count < 2 ? 0.0 : p.Length; break;
                case DxfSpline s: along = s.GetDistanceAtParameter(s.EndParam); break;
                default: along = double.PositiveInfinity; break;
            }

            if (!(along > Tolerance.Default.EqualPoint))
            {
                throw new InvalidOperationException("The " + Item.Type + " runs along the direction and projects to a point.");
            }

            return result;
        }

        /// <summary>The curve projected, before its length is checked.</summary>
        private DxfCurve Projected(GeoPlane3 planeToProjectOn, GeoVector3 projectionDirection)
        {
            GeoVector3 n = planeToProjectOn.Normal.Normalize();
            double length = projectionDirection.Length;

            if (!projectionDirection.IsValid || !(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A curve is projected along a direction of finite numbers and some length: " + projectionDirection + " is not.", nameof(projectionDirection));
            }

            GeoVector3 d = projectionDirection.Divide(length);
            double across = d.DotProduct(n);

            if (!(Math.Abs(across) > Tolerance.Default.EqualVector))
            {
                throw new ArgumentException("The direction " + projectionDirection + " runs along the plane, and projects nothing onto it.", nameof(projectionDirection));
            }

            GeoPoint3 o = planeToProjectOn.Origin;
            GeoPoint3 Project(GeoPoint3 x) => x.Add(d.Multiply(-o.GetVectorTo(x).DotProduct(n) / across));
            GeoVector3 Along(GeoVector3 v) => v.Add(d.Multiply(-v.DotProduct(n) / across));
            var m = new double[4, 4];

            for (int r = 0; r < 3; r++)
            {
                double dr = r == 0 ? d.X : r == 1 ? d.Y : d.Z;
                double[] nn = { n.X, n.Y, n.Z };

                for (int c = 0; c < 3; c++)
                {
                    m[r, c] = (r == c ? 1.0 : 0.0) - dr * nn[c] / across;
                }

                m[r, 3] = dr * (o.X * n.X + o.Y * n.Y + o.Z * n.Z) / across;
            }

            m[3, 3] = 1.0;
            var projection = new GeoTransform3(m);
            DxfCurve result;

            switch (this)
            {
                case DxfLine line:
                {
                    var copy = (DxfLine)Copy();
                    GeoPoint3 start = Project(line.StartPoint);
                    GeoPoint3 end = Project(line.EndPoint);
                    Apart(start, end);
                    copy.StartPoint = start;
                    copy.EndPoint = end;
                    return copy;
                }

                case DxfRay ray:
                {
                    var copy = (DxfRay)Copy();
                    GeoVector3 dir = Along(ray.UnitDir);
                    Apart(dir);
                    copy.BasePoint = Project(ray.BasePoint);
                    copy.UnitDir = dir.Normalize();
                    return copy;
                }

                case DxfXline xline:
                {
                    var copy = (DxfXline)Copy();
                    GeoVector3 dir = Along(xline.UnitDir);
                    Apart(dir);
                    copy.BasePoint = Project(xline.BasePoint);
                    copy.UnitDir = dir.Normalize();
                    return copy;
                }

                case DxfCircle circle:
                    return Conic(circle.Center, circle.Normal, circle.GetPointAtParameter(0.0), circle.GetPointAtParameter(Math.PI / 2.0), 0.0, 2.0 * Math.PI, true, n, Project, Along);

                case DxfArc arc:
                    return Conic(arc.Center, arc.Normal, arc.GetPointAtParameter(arc.StartParam), arc.GetPointAtParameter(arc.StartParam + Math.PI / 2.0 < arc.EndParam ? arc.StartParam + Math.PI / 2.0 : arc.StartParam), arc.StartParam, arc.EndParam, false, n, Project, Along);

                case DxfEllipse ellipse:
                    return Conic(ellipse.Center, ellipse.Normal, ellipse.Center.Add(ellipse.MajorAxis), ellipse.Center.Add(ellipse.MinorAxis), ellipse.StartParam, ellipse.EndParam, ellipse.Closed, n, Project, Along);

                case DxfPolyline polyline:
                {
                    GeoVector3 normal = polyline.Normal;

                    if (normal.IsParallelTo(n, Tolerance.Default) || !polyline.HasBulges)
                    {
                        List<DxfPolyVertex> vertices = Flat(polyline.Model(), normal, n, Project, out GeoVector3 to, out double elevation);
                        var copy = (DxfPolyline)Copy();
                        copy.Take(vertices, polyline.Closed, to, elevation, polyline.Thickness, polyline.Plinegen, polyline.GetDouble(43, 0.0));
                        return copy;
                    }

                    break;
                }

                case DxfPolyline2d polyline:
                {
                    GeoVector3 normal = polyline.Normal;
                    List<DxfPolyVertex> model = polyline.Model(out _);

                    if (normal.IsParallelTo(n, Tolerance.Default) || !model.Exists(q => q.Bulge != 0.0))
                    {
                        List<DxfPolyVertex> vertices = Flat(model, normal, n, Project, out GeoVector3 to, out double elevation);
                        var copy = (DxfPolyline2d)Copy();
                        copy.Take(vertices, polyline.Closed, to, elevation, polyline.Thickness, polyline.LinetypeGenerationOn);
                        return copy;
                    }

                    break;
                }

                case DxfPolyline3d _:
                {
                    var copy = (DxfPolyline3d)Copy();
                    copy.TransformBy(projection);
                    return copy;
                }

                case DxfSpline spline:
                {
                    var copy = (DxfSpline)Copy();
                    DxfNurbsData data = spline.NurbsData;
                    var moved = new List<GeoPoint3>(data.GetControlPoints().Count);

                    foreach (GeoPoint3 point in data.GetControlPoints())
                    {
                        moved.Add(Project(point));
                    }

                    copy.SetNurbsData(data.Degree, data.Rational, data.Closed, data.Periodic, moved, data.GetKnots(), data.GetWeights());

                    if (copy.GetString(210) != null || copy.GetString(220) != null || copy.GetString(230) != null)
                    {
                        copy.SetNormal(n);
                    }

                    return copy;
                }

                default:
                    throw new NotSupportedException("A " + GetType().Name + " is not projected by the library.");
            }

            // A polyline with arcs onto a plane not its own: its arcs are parts of ellipses there, and it is the spline of them.
            DxfSpline shape = Spline;
            DxfNurbsData nurbs = shape.NurbsData;
            var points = new List<GeoPoint3>(nurbs.GetControlPoints().Count);

            foreach (GeoPoint3 point in nurbs.GetControlPoints())
            {
                points.Add(Project(point));
            }

            shape.SetNurbsData(nurbs.Degree, nurbs.Rational, nurbs.Closed, nurbs.Periodic, points, nurbs.GetKnots(), nurbs.GetWeights());
            shape.SetPropertiesFrom(this);
            result = shape;
            return result;
        }

        /// <summary>
        /// Gets the curve projected square onto a plane, as AutoCAD's <c>GetOrthoProjectedCurve</c>: as
        /// <see cref="GetProjectedCurve(GeoPlane3, GeoVector3)"/> along the plane's normal.
        /// </summary>
        /// <param name="planeToProjectOn">The plane.</param>
        /// <returns>The projected curve.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the curve projects to a point or, for a conic seen edge on, to a
        /// line, or has no shape the library reads.</exception>
        public DxfCurve GetOrthoProjectedCurve(GeoPlane3 planeToProjectOn) => GetProjectedCurve(planeToProjectOn, planeToProjectOn.Normal);

        /// <summary>
        /// The control points, weights and knots of a polyline's spline: of degree 1 through its vertices where it has no arc,
        /// otherwise of degree 2, a line its ends and its middle, an arc its rational quadratic of a piece for each quarter turn,
        /// each segment over one step of the parameter. The points are taken to the world through a normal (null for a 3D
        /// polyline's, in the world already).
        /// </summary>
        private static int Chain(List<DxfPolyVertex> v, bool closed, GeoVector3? normal, List<GeoPoint3> points, List<double> weights, List<double> knots)
        {
            int segments = closed ? v.Count : v.Count - 1;

            if (segments < 1)
            {
                throw new InvalidOperationException("A polyline of fewer than two vertices has no spline.");
            }

            GeoPoint3 World(DxfPolyVertex q) => normal.HasValue ? OcsToWorld(normal.Value, new GeoPoint3(q.X, q.Y, q.Z)) : new GeoPoint3(q.X, q.Y, q.Z);
            bool arcs = false;

            for (int i = 0; i < segments; i++)
            {
                arcs |= DxfPolylineEdit.IsArc(v[i], v[(i + 1) % v.Count]);
            }

            if (!arcs)
            {
                knots.Add(0.0);

                for (int i = 0; i <= segments; i++)
                {
                    points.Add(World(v[i % v.Count]));
                    weights.Add(1.0);
                    knots.Add(i);
                }

                knots.Add(segments);
                return 1;
            }

            knots.AddRange(new[] { 0.0, 0.0, 0.0 });
            points.Add(World(v[0]));
            weights.Add(1.0);

            for (int i = 0; i < segments; i++)
            {
                DxfPolyVertex a = v[i];
                DxfPolyVertex b = v[(i + 1) % v.Count];

                if (!DxfPolylineEdit.IsArc(a, b))
                {
                    points.Add(World(DxfPolylineEdit.At(a, b, 0.5)));
                    weights.Add(1.0);
                    points.Add(World(b));
                    weights.Add(1.0);
                    knots.Add(i + 1);
                    knots.Add(i + 1);
                    continue;
                }

                // The arc walked by the angle it has swept, its parameter that angle over its whole sweep.
                DxfPolylineEdit.Circle(a, b, out double cx, out double cy, out _, out _, out double theta);
                double sweep = Math.Abs(theta);
                var piece = new List<GeoPoint3>();
                var pieceWeights = new List<double>();
                var pieceKnots = new List<double>();
                DxfCurveTools.Conic(World(new DxfPolyVertex(cx, cy, a.Z, 0.0, 0.0, 0.0, false)), psi => World(psi >= sweep ? b : DxfPolylineEdit.At(a, b, psi / sweep)), 0.0, sweep, false, piece, pieceWeights, pieceKnots);

                for (int k = 1; k < piece.Count; k++)
                {
                    points.Add(k == piece.Count - 1 ? World(b) : piece[k]);
                    weights.Add(pieceWeights[k]);
                }

                for (int k = 3; k < pieceKnots.Count - 1; k++)
                {
                    knots.Add(pieceKnots[k] >= sweep ? i + 1 : i + pieceKnots[k] / sweep);
                }
            }

            knots.Add(segments);
            return 2;
        }

        /// <summary>
        /// The vertices of a polyline of the plane projected onto a plane, in the system of the new normal: the plane's normal
        /// turned to the polyline's own side, so that the polyline keeps the way it runs as seen from it; and its elevation.
        /// </summary>
        private static List<DxfPolyVertex> Flat(List<DxfPolyVertex> v, GeoVector3 normal, GeoVector3 n, Func<GeoPoint3, GeoPoint3> project, out GeoVector3 to, out double elevation)
        {
            to = n.DotProduct(normal) < 0.0 ? n.Negate() : n;
            var flat = new List<DxfPolyVertex>(v.Count);
            elevation = 0.0;

            for (int i = 0; i < v.Count; i++)
            {
                GeoPoint3 world = project(OcsToWorld(normal, new GeoPoint3(v[i].X, v[i].Y, v[i].Z)));
                GeoPoint3 local = WorldToOcs(to, world);

                if (i == 0)
                {
                    elevation = local.Z;
                }

                DxfPolyVertex q = v[i];
                q.X = local.X;
                q.Y = local.Y;
                q.Z = elevation;
                flat.Add(q);
            }

            return flat;
        }

        /// <summary>
        /// A conic projected: moved where its plane is parallel to the plane, of its own class; otherwise the part of the
        /// ellipse its two conjugate semi-axes (from the centre to the points at the parameter nought and a quarter turn on)
        /// project to, its parameters turned by the angle at which the projected axes give the major one.
        /// </summary>
        private DxfCurve Conic(GeoPoint3 centre, GeoVector3 normal, GeoPoint3 atZero, GeoPoint3 atQuarter, double start, double end, bool whole, GeoVector3 n, Func<GeoPoint3, GeoPoint3> project, Func<GeoVector3, GeoVector3> along)
        {
            GeoPoint3 moved = project(centre);

            if (normal.IsParallelTo(n, Tolerance.Default))
            {
                var copy = (DxfCurve)Copy();
                copy.TransformBy(GeoTransform3.Translation(centre.GetVectorTo(moved)));
                return copy;
            }

            // The point at a parameter t is c + a1 cos(t - s0) + a2 sin(t - s0), s0 the parameter of atZero.
            double s0 = this is DxfArc ? start : 0.0;
            GeoVector3 a1 = along(centre.GetVectorTo(atZero));
            GeoVector3 a2 = along(centre.GetVectorTo(atQuarter));

            if (this is DxfArc && !(start + Math.PI / 2.0 < end))
            {
                // An arc of a quarter turn or less: its second axis from its own normal.
                GeoVector3 r = centre.GetVectorTo(atZero);
                a2 = along(normal.Normalize().CrossProduct(r));
            }

            GeoVector3 plane = a1.CrossProduct(a2);

            double phi = 0.5 * Math.Atan2(2.0 * a1.DotProduct(a2), a1.LengthSquared - a2.LengthSquared);
            GeoVector3 major = a1.Multiply(Math.Cos(phi)).Add(a2.Multiply(Math.Sin(phi)));
            GeoVector3 minor = a1.Multiply(-Math.Sin(phi)).Add(a2.Multiply(Math.Cos(phi)));

            if (minor.Length > major.Length)
            {
                phi += Math.PI / 2.0;
                GeoVector3 swap = major;
                major = minor;
                minor = swap.Negate();
            }

            // Seen edge on, or so nearly that the ellipse would be thinner than the point tolerance or than AutoCAD's least ratio
            // of its axes, 1E-6, the conic projects to a line.
            double ratio = major.Length > 0.0 ? minor.Length / major.Length : 0.0;

            if (!(minor.Length > Tolerance.Default.EqualPoint) || !(ratio >= MinRatio))
            {
                throw new InvalidOperationException("The " + Item.Type + " is seen edge on along the direction, or so nearly that its minor axis is within the point tolerance or 1E-6 of its major one, and projects to a line, no curve of its kind.");
            }

            // The ellipse lies in the plane projected onto, its normal turned to the side the conic runs counter-clockwise about.
            GeoVector3 normal2 = plane.DotProduct(n) < 0.0 ? n.Negate() : n;
            var ellipse = new DxfEllipse(moved, normal2, major, Math.Min(1.0, ratio), 0.0, 2.0 * Math.PI);

            if (!whole)
            {
                double turn = 2.0 * Math.PI;
                double from = start - s0 - phi;
                from -= turn * Math.Floor(from / turn);
                double to = from + (end - start);
                ellipse.StartParam = from;
                ellipse.EndParam = to;
            }

            ellipse.SetPropertiesFrom(this);
            return ellipse;
        }

        /// <summary>Refuses a curve projected to a point: two ends within the point tolerance.</summary>
        private void Apart(GeoPoint3 start, GeoPoint3 end)
        {
            if (start.IsEqualTo(end, Tolerance.Default))
            {
                throw new InvalidOperationException("The " + Item.Type + " runs along the direction and projects to a point.");
            }
        }

        /// <summary>Refuses a direction projected to none.</summary>
        private void Apart(GeoVector3 direction)
        {
            if (direction.IsZeroLength(Tolerance.Default))
            {
                throw new InvalidOperationException("The " + Item.Type + " runs along the direction and projects to a point.");
            }
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
