using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The eighteen <c>vlax-curve-*</c> functions: a curve is walked by its parameter and by the distance along it, as AutoCAD's
    /// <c>AcDbCurve</c> is. They are defined by the first <c>(vl-load-com)</c>, as AutoCAD's are, and take the name of a LINE,
    /// an ARC, a CIRCLE, an ELLIPSE, a polyline, a SPLINE, an XLINE or a RAY. The geometry is the one of the entity classes of
    /// the DXF layer (<see cref="DxfCurve"/>), which was written to AutoCAD's own numbers; this file adds what AutoCAD's functions
    /// do on top of it: nil where an answer is out of range or off the curve, a parameter that goes round a circle, and the
    /// derivatives of a line and of a bulge as AutoCAD gives them.
    /// </summary>
    internal static partial class LispDrawing
    {
        // A point is on a curve, and a parameter or a distance is in its range, within this fraction of (1 + the size of the curve)
        // for the functions below: AutoCAD takes 1E-4 off a line as off it already (probe a4), far less than the 1E-3 the entity
        // classes read a point with.
        private const double CurveFraction = 1E-9;

        static partial void RegisterCurves(LispInterpreter lisp, CadCommandSession session)
        {
            DrawingState state = StateOf(lisp);

            lisp.OnLoadCom(() =>
            {
                Curve1(lisp, session, state, "VLAX-CURVE-GETSTARTPARAM", c => RealOrNil(StartParam(c)));
                Curve1(lisp, session, state, "VLAX-CURVE-GETENDPARAM", c => RealOrNil(EndParam(c)));
                Curve1(lisp, session, state, "VLAX-CURVE-GETSTARTPOINT", c => PointOrNil(StartPoint(c)));
                Curve1(lisp, session, state, "VLAX-CURVE-GETENDPOINT", c => PointOrNil(EndPoint(c)));
                Curve1(lisp, session, state, "VLAX-CURVE-GETAREA", c => RealOf(AreaOf(c)));
                Curve1(lisp, session, state, "VLAX-CURVE-ISCLOSED", c => lisp.Bool(c.Closed));
                Curve1(lisp, session, state, "VLAX-CURVE-ISPERIODIC", c => lisp.Bool(c.IsPeriodic));
                Curve1(lisp, session, state, "VLAX-CURVE-ISPLANAR", c => lisp.Bool(IsPlanarCurve(c)));

                CurveNumber(lisp, session, state, "VLAX-CURVE-GETPOINTATPARAM", (c, v) =>
                    PointOrNil(PointAtParam(c, v))
                );
                CurveNumber(lisp, session, state, "VLAX-CURVE-GETDISTATPARAM", (c, v) => RealOrNil(DistAtParam(c, v)));
                CurveNumber(lisp, session, state, "VLAX-CURVE-GETPARAMATDIST", (c, v) => RealOrNil(ParamAtDist(c, v)));
                CurveNumber(lisp, session, state, "VLAX-CURVE-GETPOINTATDIST", (c, v) => PointOrNil(PointAtDist(c, v)));
                CurveNumber(lisp, session, state, "VLAX-CURVE-GETFIRSTDERIV", (c, v) => VectorOrNil(FirstDeriv(c, v)));
                CurveNumber(lisp, session, state, "VLAX-CURVE-GETSECONDDERIV", (c, v) =>
                    VectorOrNil(SecondDeriv(c, v))
                );

                CurvePoint(lisp, session, state, "VLAX-CURVE-GETPARAMATPOINT", (c, p) => RealOrNil(ParamAtPoint(c, p)));
                CurvePoint(lisp, session, state, "VLAX-CURVE-GETDISTATPOINT", (c, p) => RealOrNil(DistAtPoint(c, p)));

                lisp.Define("VLAX-CURVE-GETCLOSESTPOINTTO", 2, 3, a => lisp.OnOwner(() =>
                    {
                        DxfCurve curve = CurveOf(session, state, a[0]);
                        GeoPoint3 point = CurvePointArgument(a[1]);
                        return PointOrNil(ClosestPoint(curve, point, a.Length > 2 && a[2] != null));
                    }));
                lisp.Define("VLAX-CURVE-GETCLOSESTPOINTTOPROJECTION", 3, 4, a => lisp.OnOwner(() =>
                    {
                        DxfCurve curve = CurveOf(session, state, a[0]);
                        GeoPoint3 point = CurvePointArgument(a[1]);
                        GeoPoint3 normal = CurvePointArgument(a[2]);
                        return PointOrNil(
                            ClosestPointAlong(
                                curve,
                                point,
                                new GeoVector3(normal.X, normal.Y, normal.Z),
                                a.Length > 3 && a[3] != null
                            )
                        );
                    }));
            });
        }

        #region The curve of an argument

        /// <summary>
        /// The curve an argument names: an entity name, or the VLA-OBJECT of an entity (AutoCAD takes both), of a LINE, an ARC, a
        /// CIRCLE, an ELLIPSE, a polyline, a SPLINE, an XLINE or a RAY. A value that names no entity is <c>unable to get ObjectID:
        /// nil</c>, as AutoCAD words it; an entity that is no curve, or is erased, is <c>bad argument value: AcDbCurve n</c>, where
        /// AutoCAD's <c>n</c> is a counter of its own and ours is the decimal value of the handle (a block or a table record gives
        /// it too, ours); a released value is <c>null interface pointer</c> as every use of one is. Owner thread only.
        /// </summary>
        /// <param name="session">The session whose drawing holds the entity.</param>
        /// <param name="state">What the functions of the interpreter keep: the handle an entity brought back by <c>entdel</c> has now.</param>
        /// <param name="value">The argument.</param>
        /// <returns>The curve.</returns>
        internal static DxfCurve CurveOf(CadCommandSession session, DrawingState state, object value)
        {
            string handle;

            if (value is LispEntityName name)
            {
                handle = state.Resolve(name.Handle);
            }
            else if (value is LispVlaObject target && !target.IsReleased && HasHandle(target))
            {
                // The value of an object stands for its handle as it is now, so an entity brought back by entdel is the curve again
                // and one erased is not (AutoCAD 2021, GUI g2: bad argument value: AcDbCurve 45).
                handle = state.Resolve(target.Key);
            }
            else if (value is LispVlaObject released && released.IsReleased)
            {
                throw LispActiveX.Released();
            }
            else
            {
                throw new LispException("unable to get ObjectID: " + LispInterpreter.Print(value, true));
            }

            if (session.Document.FindEntity(handle) is DxfCurve curve && !(curve is DxfLeader))
            {
                return curve;
            }

            long number = long.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : 0;
            throw new LispException("bad argument value: AcDbCurve " + number.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Whether the value of an object stands for a handle: an entity, a block or a table record; not the application, the document, a collection, a selection set or a color.</summary>
        private static bool HasHandle(LispVlaObject target) =>
            target.Kind == LispVlaKind.Entity || target.Kind == LispVlaKind.Block || target.Kind == LispVlaKind.Record;

        /// <summary>A number argument: <c>bad argument type: numberp: "a"</c> for anything else.</summary>
        private static double NumberArgument(object value) =>
            value is int whole ? whole
            : value is double real ? real
            : throw LispInterpreter.TypeError("numberp", value, colon: true);

        /// <summary>A point argument, a list of two or three numbers (the Z is 0 for two): <c>bad argument type: 2D/3D point: 5</c> for anything else.</summary>
        private static GeoPoint3 CurvePointArgument(object value) =>
            PointOf(value) ?? throw LispInterpreter.TypeError("2D/3D point", value, colon: true);

        private static void Curve1(
            LispInterpreter lisp,
            CadCommandSession session,
            DrawingState state,
            string name,
            Func<DxfCurve, object> body
        ) => lisp.Define(name, 1, 1, a => lisp.OnOwner(() => body(CurveOf(session, state, a[0]))));

        private static void CurveNumber(
            LispInterpreter lisp,
            CadCommandSession session,
            DrawingState state,
            string name,
            Func<DxfCurve, double, object> body
        ) => lisp.Define(name, 2, 2, a => lisp.OnOwner(() =>
                {
                    DxfCurve curve = CurveOf(session, state, a[0]);
                    return body(curve, NumberArgument(a[1]));
                }));

        private static void CurvePoint(
            LispInterpreter lisp,
            CadCommandSession session,
            DrawingState state,
            string name,
            Func<DxfCurve, GeoPoint3, object> body
        ) => lisp.Define(name, 2, 2, a => lisp.OnOwner(() =>
                {
                    DxfCurve curve = CurveOf(session, state, a[0]);
                    return body(curve, CurvePointArgument(a[1]));
                }));

        #endregion

        #region Values

        /// <summary>A real as a LISP value, a negative nought as nought; nil for none.</summary>
        private static object RealOrNil(double? value) => value.HasValue ? (object)(value.Value + 0.0) : null;

        private static object RealOf(double value) => value + 0.0;

        /// <summary>A point as a list of three reals; nil for none.</summary>
        private static object PointOrNil(GeoPoint3? point) =>
            point.HasValue
                ? LispInterpreter.ListOf(new object[] { point.Value.X + 0.0, point.Value.Y + 0.0, point.Value.Z + 0.0 })
                : null;

        private static object VectorOrNil(GeoVector3? vector) =>
            vector.HasValue
                ? LispInterpreter.ListOf(
                    new object[] { vector.Value.X + 0.0, vector.Value.Y + 0.0, vector.Value.Z + 0.0 }
                )
                : null;

        #endregion

        #region The ends of a curve

        /// <summary>An xline has no start, and neither has its parameter (probe a4): nil.</summary>
        private static double? StartParam(DxfCurve curve) => curve is DxfXline ? (double?)null : curve.StartParam;

        /// <summary>An xline and a ray have no end, and neither has their parameter (probe a4): nil.</summary>
        private static double? EndParam(DxfCurve curve) =>
            curve is DxfXline || curve is DxfRay ? (double?)null : curve.EndParam;

        private static GeoPoint3? StartPoint(DxfCurve curve) => curve is DxfXline ? (GeoPoint3?)null : curve.StartPoint;

        /// <summary>
        /// The end point: that of an xline and a ray is nil. A circle and a whole ellipse end where they began, to the last digit
        /// (AutoCAD prints (25.0 0.0 0.0) for both ends of a circle about (20 0)), not where the sine of a whole turn puts them.
        /// </summary>
        private static GeoPoint3? EndPoint(DxfCurve curve)
        {
            if (curve is DxfXline || curve is DxfRay)
            {
                return null;
            }

            return curve is DxfCircle || (curve is DxfEllipse && curve.Closed) ? curve.StartPoint : curve.EndPoint;
        }

        /// <summary>
        /// The area the curve closes, as the entity class gives it, but for a 3D polyline and a spline, which the classes give none
        /// for: that of the polygon of the vertices of the polyline, or of 4096 points along the spline, closed by the chord if it is
        /// open, in its own plane (probe a4: 55.9017 for three vertices in a tilted plane; the spline's is ours, as AutoCAD was not
        /// asked, and good to a millionth of the area).
        /// </summary>
        private static double AreaOf(DxfCurve curve)
        {
            var vertices = new List<GeoPoint3>();

            if (curve is DxfPolyline3d polyline)
            {
                foreach (DxfPolylineVertex3d vertex in polyline)
                {
                    vertices.Add(vertex.Position);
                }
            }
            else if (curve is DxfSpline spline)
            {
                const int Samples = 4096;
                double start = spline.StartParam;
                double span = spline.EndParam - start;

                for (int i = 0; i < Samples; i++)
                {
                    vertices.Add(spline.GetPointAtParameter(Math.Min(spline.EndParam, start + span * i / Samples)));
                }

                vertices.Add(spline.GetPointAtParameter(spline.EndParam));
            }
            else
            {
                return curve.Area;
            }

            double x = 0.0;
            double y = 0.0;
            double z = 0.0;

            for (int i = 0; i < vertices.Count; i++)
            {
                GeoPoint3 a = vertices[i];
                GeoPoint3 b = vertices[(i + 1) % vertices.Count];
                x += a.Y * b.Z - a.Z * b.Y;
                y += a.Z * b.X - a.X * b.Z;
                z += a.X * b.Y - a.Y * b.X;
            }

            return 0.5 * Math.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// Whether the curve lies in one plane. The class of a spline reads a flag of its record that a spline made by the SPLINE
        /// command does not carry, so a spline is judged by its control points.
        /// </summary>
        private static bool IsPlanarCurve(DxfCurve curve)
        {
            if (!(curve is DxfSpline spline))
            {
                return curve.IsPlanar;
            }

            IReadOnlyList<GeoPoint3> points = spline.ControlPoints;
            return points.Count < 3 || new GeoPolylineArc3(points).IsPlanar(Tolerance.Default);
        }

        #endregion

        #region Ranges

        /// <summary>The length of the curve, infinite for an xline and a ray.</summary>
        private static double LengthOf(DxfCurve curve)
        {
            if (curve is DxfXline || curve is DxfRay)
            {
                return double.PositiveInfinity;
            }

            try
            {
                return curve.GetDistanceAtParameter(curve.EndParam);
            }
            catch (InvalidOperationException)
            {
                // A line of no length has no shape the library reads.
                return 0.0;
            }
        }

        /// <summary>How far a value may lie outside a range and still be at its end: a thousand-millionth of 1 plus the size.</summary>
        private static double Slack(double size) =>
            double.IsInfinity(size) ? CurveFraction : CurveFraction * (1.0 + Math.Abs(size));

        /// <summary>
        /// Takes a value to the range [<paramref name="low"/>, <paramref name="high"/>], where it is within the slack of it; false
        /// where it is further out (AutoCAD gives nil past an end, a3 and a4).
        /// </summary>
        private static bool InRange(ref double value, double low, double high)
        {
            double slack = Slack(high - low);

            if (double.IsNaN(value) || value < low - slack || value > high + slack)
            {
                return false;
            }

            value = Math.Max(low, Math.Min(high, value));
            return true;
        }

        /// <summary>Runs a reading of the curve, nil where it refuses: a value out of range, a point off the curve, a curve of no length.</summary>
        private static T? Read<T>(Func<T> body)
            where T : struct
        {
            try
            {
                return body();
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        #endregion

        #region The parameter and the distance

        // A curve that goes round (a circle, a whole ellipse, a closed periodic spline) takes a parameter past its end round, and
        // a distance past its length: parameter 7.0 on a circle of radius 10 is the point at angle 7.0, at distance 70.0 (probe a5).

        private static GeoPoint3? PointAtParam(DxfCurve curve, double value)
        {
            if (curve.IsPeriodic)
            {
                return Wrapped(curve, value, (c, p) => c.GetPointAtParameter(p), out GeoPoint3 point)
                    ? point
                    : (GeoPoint3?)null;
            }

            double low = curve.StartParam;
            double high = curve.EndParam;
            return InRange(ref value, low, high) ? Read(() => curve.GetPointAtParameter(value)) : null;
        }

        private static double? DistAtParam(DxfCurve curve, double value)
        {
            // A line gives the parameter as it is, whatever it is (probe a4: -0.5).
            if (curve is DxfLine)
            {
                return value;
            }

            if (curve.IsPeriodic)
            {
                double length = LengthOf(curve);
                double start = curve.StartParam;
                double span = curve.EndParam - start;

                if (value < start - Slack(span) || double.IsNaN(value))
                {
                    return null;
                }

                double turns = Math.Floor((value - start) / span);
                double inside = Math.Max(start, Math.Min(curve.EndParam, value - turns * span));
                double? distance = Read(() => curve.GetDistanceAtParameter(inside));
                return distance.HasValue ? turns * length + distance.Value : (double?)null;
            }

            double low = curve.StartParam;
            double high = curve.EndParam;
            return InRange(ref value, low, high) ? Read(() => curve.GetDistanceAtParameter(value)) : null;
        }

        private static double? ParamAtDist(DxfCurve curve, double value)
        {
            double length = LengthOf(curve);

            if (curve.IsPeriodic && length > 0.0)
            {
                return value < -Slack(length) || double.IsNaN(value)
                    ? (double?)null
                    : Read(() => curve.GetParameterAtDistance(AroundLength(value, length)));
            }

            return InRange(ref value, 0.0, length) ? Read(() => curve.GetParameterAtDistance(value)) : null;
        }

        private static GeoPoint3? PointAtDist(DxfCurve curve, double value)
        {
            double length = LengthOf(curve);

            if (curve.IsPeriodic && length > 0.0)
            {
                return value < -Slack(length) || double.IsNaN(value)
                    ? (GeoPoint3?)null
                    : Read(() => curve.GetPointAtDist(AroundLength(value, length)));
            }

            return InRange(ref value, 0.0, length) ? Read(() => curve.GetPointAtDist(value)) : null;
        }

        /// <summary>A distance taken round a curve of a length, from nought up to the length.</summary>
        private static double AroundLength(double distance, double length) =>
            Math.Max(0.0, Math.Min(length, distance - Math.Floor(distance / length) * length));

        /// <summary>Takes a parameter round a periodic curve into its range and reads the curve there.</summary>
        private static bool Wrapped<T>(DxfCurve curve, double value, Func<DxfCurve, double, T> read, out T result)
        {
            result = default(T);
            double start = curve.StartParam;
            double span = curve.EndParam - start;

            if (double.IsNaN(value) || value < start - Slack(span))
            {
                return false;
            }

            double inside = Math.Max(
                start,
                Math.Min(curve.EndParam, value - Math.Floor((value - start) / span) * span)
            );

            try
            {
                result = read(curve, inside);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        #endregion

        #region A point of the curve

        /// <summary>
        /// The point of the curve nearest a point, when it is on the curve: within a thousand-millionth of 1 plus the size of the
        /// curve, as the functions below take a point (AutoCAD takes one 1E-4 off a line as off it already, a4); null where it is not.
        /// </summary>
        private static GeoPoint3? OnTheCurve(DxfCurve curve, GeoPoint3 point)
        {
            GeoPoint3? nearest = Read(() => curve.GetClosestPointTo(point, false));

            if (!nearest.HasValue)
            {
                return null;
            }

            double size = LengthOf(curve);

            if (double.IsInfinity(size))
            {
                size = Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z)));
            }

            return (point - nearest.Value).Length <= Slack(size) ? nearest : null;
        }

        private static double? ParamAtPoint(DxfCurve curve, GeoPoint3 point)
        {
            GeoPoint3? on = OnTheCurve(curve, point);
            return on.HasValue ? Read(() => curve.GetParameterAtPoint(on.Value)) : null;
        }

        private static double? DistAtPoint(DxfCurve curve, GeoPoint3 point)
        {
            GeoPoint3? on = OnTheCurve(curve, point);
            return on.HasValue ? Read(() => curve.GetDistAtPoint(on.Value)) : null;
        }

        private static GeoPoint3? ClosestPoint(DxfCurve curve, GeoPoint3 point, bool extend) =>
            Read(() => curve.GetClosestPointTo(point, extend));

        /// <summary>
        /// The point of the curve whose projection along a direction is nearest the projection of a point, as AutoCAD's
        /// <c>getClosestPointTo</c> with a normal measures: both are projected along the normal onto a plane, and the nearest point
        /// of the flat curve is taken back to the curve. For a curve in a plane the normal is perpendicular to, that is the point
        /// nearest the point projected onto the plane (a3: (5 1 7) along (0 0 1) on a line on the X axis gives (5 0 0)). A line,
        /// a ray and an xline are solved exactly, and extended where asked; every other curve is searched along its parameter (a
        /// sample every 1/256 of its range, 64 to a segment of a polyline or a span of a spline, then a bisection), and is never
        /// extended: the probes did not ask AutoCAD for it, so that is ours.
        /// </summary>
        private static GeoPoint3? ClosestPointAlong(DxfCurve curve, GeoPoint3 point, GeoVector3 direction, bool extend)
        {
            if (direction.Length < Tolerance.DefaultEqualVector)
            {
                return ClosestPoint(curve, point, extend);
            }

            GeoVector3 unit = direction / direction.Length;

            if (curve is DxfLine || curve is DxfRay || curve is DxfXline)
            {
                return Read(() => ClosestOnStraight(curve, point, unit, extend));
            }

            double low = curve.StartParam;
            double high = curve.EndParam;

            double Distance(double parameter)
            {
                GeoVector3 away = curve.GetPointAtParameter(Math.Min(high, parameter)) - point;
                GeoVector3 flat = away - unit * away.DotProduct(unit);
                return flat.LengthSquared;
            }

            double Slope(double parameter)
            {
                double at = Math.Min(high, parameter);
                GeoVector3 away = curve.GetPointAtParameter(at) - point;
                GeoVector3 speed = curve.GetFirstDerivative(at);
                return (away - unit * away.DotProduct(unit)).DotProduct(speed - unit * speed.DotProduct(unit));
            }

            return Read(() =>
            {
                int samples =
                    curve is DxfPolyline || curve is DxfPolyline2d || curve is DxfSpline
                        ? (int)Math.Max(256.0, Math.Min(65536.0, 64.0 * Math.Ceiling(high - low)))
                        : 256;
                double step = (high - low) / samples;
                int best = 0;
                double bestValue = double.PositiveInfinity;

                for (int i = 0; i <= samples; i++)
                {
                    double value = Distance(Math.Min(high, low + i * step));

                    if (value < bestValue)
                    {
                        bestValue = value;
                        best = i;
                    }
                }

                // The nearest point lies between the two samples beside the best one: bisect on the sign of the slope of the
                // squared distance there, which is the flat offset dotted with the flat derivative (a golden section of a function
                // that flat at its least stops 1E-7 short).
                double a = Math.Max(low, low + (best - 1) * step);
                double b = Math.Min(high, low + (best + 1) * step);

                for (int i = 0; i < 200 && b - a > 1E-15 * (1.0 + Math.Abs(b)); i++)
                {
                    double middle = 0.5 * (a + b);

                    if (Slope(middle) > 0.0)
                    {
                        b = middle;
                    }
                    else
                    {
                        a = middle;
                    }
                }

                double found = 0.5 * (a + b);
                return curve.GetPointAtParameter(Math.Min(high, found));
            });
        }

        /// <summary>
        /// The point of a straight curve (a line, a ray, an xline) whose projection along a direction is nearest the projection of
        /// a point: the foot of the perpendicular in the flat view, kept on the curve unless it is to be extended.
        /// </summary>
        private static GeoPoint3 ClosestOnStraight(DxfCurve curve, GeoPoint3 point, GeoVector3 unit, bool extend)
        {
            GeoPoint3 from = curve.GetPointAtParameter(0.0);
            GeoVector3 along = curve.GetPointAtParameter(1.0) - from;
            GeoVector3 flatAlong = along - unit * along.DotProduct(unit);
            GeoVector3 toPoint = point - from;
            GeoVector3 flatTo = toPoint - unit * toPoint.DotProduct(unit);
            double square = flatAlong.LengthSquared;
            double t = square > 0.0 ? flatTo.DotProduct(flatAlong) / square : 0.0;

            if (curve is DxfLine line)
            {
                double length = line.Length;
                t = extend ? t : Math.Max(0.0, Math.Min(length, t));
            }
            else if (curve is DxfRay)
            {
                t = Math.Max(0.0, t);
            }

            return from + along * t;
        }

        #endregion

        #region Derivatives

        /// <summary>
        /// The first derivative as AutoCAD gives it: that of a line is the vector from its start to its end whatever the
        /// parameter (a3: (10 0 0) on a line of length 10, though its parameter is the distance), and that of a bulge is the radius
        /// times the direction of travel, per radian, not per unit of the polyline's parameter (a4: (0 5 0) at the middle of a
        /// half circle of radius 5). The entity classes give the true derivatives, of unit length on a line and of radius times the
        /// sweep on a bulge.
        /// </summary>
        private static GeoVector3? FirstDeriv(DxfCurve curve, double value)
        {
            GeoVector3? derivative = Read(() => curve.GetFirstDerivative(value));

            if (!derivative.HasValue)
            {
                return null;
            }

            if (curve is DxfLine line)
            {
                return derivative.Value * line.Length;
            }

            double? sweep = BulgeSweep(curve, value);
            return sweep.HasValue ? derivative.Value / sweep.Value : derivative;
        }

        /// <summary>The second derivative as AutoCAD gives it: on a bulge the radius per square radian, as <see cref="FirstDeriv"/> gives per radian.</summary>
        private static GeoVector3? SecondDeriv(DxfCurve curve, double value)
        {
            GeoVector3? derivative = Read(() => curve.GetSecondDerivative(value));

            if (!derivative.HasValue)
            {
                return null;
            }

            double? sweep = BulgeSweep(curve, value);
            return sweep.HasValue ? derivative.Value / (sweep.Value * sweep.Value) : derivative;
        }

        /// <summary>
        /// The angle a bulge of a polyline sweeps where the parameter is, from the two derivatives the class gives (the speed of
        /// an arc is its radius times the sweep, and its acceleration the radius times the square of it); null on a straight segment
        /// and on every other curve.
        /// </summary>
        private static double? BulgeSweep(DxfCurve curve, double value)
        {
            if (!(curve is DxfPolyline) && !(curve is DxfPolyline2d))
            {
                return null;
            }

            GeoVector3? first = Read(() => curve.GetFirstDerivative(value));
            GeoVector3? second = Read(() => curve.GetSecondDerivative(value));

            if (!first.HasValue || !second.HasValue || first.Value.Length < Tolerance.DefaultEqualVector)
            {
                return null;
            }

            double sweep = second.Value.Length / first.Value.Length;
            return sweep < Tolerance.DefaultEqualVector ? (double?)null : sweep;
        }

        #endregion
    }
}
