using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GeometryHelper.Core;
using GeometryHelper.Internal;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A curve in space that is not a chain of pieces but one smooth sweep: a NURBS, a B-spline whose control points may
    /// pull on it with different strengths, which is what an AutoCAD SPLINE and an IFC B-spline curve hold. It is the
    /// counterpart of <see cref="GeoSpline2"/> in space, made and read the same way.
    /// <para>
    /// It is given by its degree, its control points, its knots and, if it is rational, its weights. The knots cut the
    /// parameter into spans, and each span is a polynomial (or a ratio of two) of the degree; the curve is clamped, so it
    /// starts at its first control point and ends at its last. The control points of a cubic (0, 0, 0), (100, 150, 60),
    /// (200, 150, 60), (300, 0, 0) and the knots 0, 0, 0, 0, 1, 1, 1, 1 give the Bézier curve that rises to 112.5 and 45
    /// in the middle, at (150, 112.5, 45). A circle is the rational quadratic of nine control points that
    /// <see cref="FromCircle(GeoCircle3)"/> builds, every point of it at the radius to rounding.
    /// </para>
    /// <para>
    /// A spline is read over its <b>domain</b>, from <see cref="DomainStart"/> to <see cref="DomainEnd"/>, the first and
    /// last knot, which are whatever the knots say: the knots are kept as they were given, so a spline read from a file
    /// goes back out as it came. Two kinds of parameter are told apart by their names, as the other
    /// curves keep them: the members named for a <b>parameter</b>, such as <see cref="GetPointAtParameter(double)"/>, take a share of
    /// the domain from 0 to 1, and the ones named for a <b>knot</b>, such as <see cref="GetPointAtKnot(double)"/>, take a value of the
    /// domain itself, which is what the knots, and a file, say. Splines made here have the domain [0, 1], where the two are the same
    /// number. Neither is a length: the members named for a <b>distance</b> go by length.
    /// </para>
    /// <para>
    /// It is immutable: every operation gives a new spline. It is a class, as <see cref="GeoPolylineArc3"/> is, because it
    /// holds arrays, and what it hands out of them is read-only.
    /// </para>
    /// </summary>
    public sealed partial class GeoSpline3 : IEquatable<GeoSpline3>
    {
        private readonly NurbsCurve _curve;
        private readonly GeoPoint3[] _points;
        private readonly ReadOnlyCollection<GeoPoint3> _pointView;
        private readonly ReadOnlyCollection<double> _weightView;
        private readonly ReadOnlyCollection<double> _knotView;

        /// <summary>
        /// Initializes a spline from its degree, control points and knots, and its weights if it is rational.
        /// </summary>
        /// <param name="degree">The degree, from 1 to 10: 1 for straight pieces, 2 for the conics of an arc, 3 for the cubics that draughtsmen and AutoCAD use.</param>
        /// <param name="controlPoints">The control points, at least degree + 1 and not all one point.</param>
        /// <param name="knots">The knots, controlPoints + degree + 1 of them, not decreasing; the first degree + 1 equal and the last degree + 1 equal and above them, and no inner knot repeated more than degree times.</param>
        /// <param name="weights">The weights, one for each control point and above nought, or null for all of them one, and no more than 1E12 to one apart (past that the length cannot be worked out right). All one is a plain B-spline, which <see cref="IsRational"/> says.</param>
        /// <remarks>
        /// The numbers are copied, and kept as they are: nothing is rescaled or reordered. A spline of degree 3 with four
        /// control points takes the knots 0, 0, 0, 0, 1, 1, 1, 1; with a fifth it takes nine knots, such as
        /// 0, 0, 0, 0, 0.5, 1, 1, 1, 1.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the control points or the knots are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10, a weight is not a positive number, or the largest weight is more than 1E12 times the smallest.</exception>
        /// <exception cref="ArgumentException">Thrown when there are too few control points, a point or a knot is not a finite number, the knots are the wrong number or not clamped, or the control points are all one point.</exception>
        public GeoSpline3(int degree, IEnumerable<GeoPoint3> controlPoints, IEnumerable<double> knots, IEnumerable<double> weights = null)
        {
            if (controlPoints == null) throw new ArgumentNullException(nameof(controlPoints));
            if (knots == null) throw new ArgumentNullException(nameof(knots));

            double[] flat = Flatten(controlPoints);
            double[] knotArray = new List<double>(knots).ToArray();
            double[] weightArray = weights == null ? null : new List<double>(weights).ToArray();

            NurbsCurve.Check(degree, flat, 3, knotArray, weightArray);

            _curve = new NurbsCurve(3, degree, flat, knotArray, weightArray);
            _points = Unflatten(flat);
            _pointView = Array.AsReadOnly(_points);
            _weightView = Array.AsReadOnly(_curve.Weights);
            _knotView = Array.AsReadOnly(_curve.Knots);
        }

        /// <summary>
        /// Initializes a spline from a curve already checked, which it owns.
        /// </summary>
        internal GeoSpline3(NurbsCurve curve)
        {
            _curve = curve;
            _points = Unflatten(curve.Points);
            _pointView = Array.AsReadOnly(_points);
            _weightView = Array.AsReadOnly(curve.Weights);
            _knotView = Array.AsReadOnly(curve.Knots);
        }

        /// <summary>
        /// Gets the curve under the spline, for the code that maps it between the plane and space.
        /// </summary>
        internal NurbsCurve Curve => _curve;

        private static double[] Flatten(IEnumerable<GeoPoint3> points)
        {
            var list = new List<double>();

            foreach (GeoPoint3 point in points)
            {
                list.Add(point.X);
                list.Add(point.Y);
                list.Add(point.Z);
            }

            return list.ToArray();
        }

        private static GeoPoint3[] Unflatten(double[] flat)
        {
            var points = new GeoPoint3[flat.Length / 3];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GeoPoint3(flat[3 * i], flat[3 * i + 1], flat[3 * i + 2]);
            }

            return points;
        }

        #region Building

        /// <summary>
        /// Builds the clamped spline of a degree whose control points are given, with its knots as evenly spaced as they can
        /// be over [0, 1].
        /// </summary>
        /// <param name="degree">The degree, from 1 to 10.</param>
        /// <param name="controlPoints">The control points, at least degree + 1 and not all one point.</param>
        /// <returns>The spline, not rational.</returns>
        /// <remarks>
        /// Seven control points of degree 3 have the knots 0, 0, 0, 0, 1/4, 1/2, 3/4, 1, 1, 1, 1: the four spans of the
        /// curve take a quarter each of the parameter. With degree + 1 points it is the Bézier curve of the points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the control points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when there are too few control points, or they are all one point.</exception>
        public static GeoSpline3 FromControlPoints(int degree, IEnumerable<GeoPoint3> controlPoints)
        {
            if (controlPoints == null) throw new ArgumentNullException(nameof(controlPoints));

            double[] flat = Flatten(controlPoints);
            bool enough = degree >= 1 && degree <= NurbsCurve.MostDegree && flat.Length / 3 >= degree + 1;

            // With too few points or a degree out of range there are no knots to make, and the check names the fault
            // before it looks at them.
            double[] knots = enough ? NurbsCurve.UniformKnots(flat.Length / 3, degree) : new double[0];
            NurbsCurve.Check(degree, flat, 3, knots, null);

            return new GeoSpline3(new NurbsCurve(3, degree, flat, knots, null));
        }

        /// <summary>
        /// Builds the spline that passes through a list of fit points, as AutoCAD's fit-point splines do, with the curve
        /// left to settle its own direction at each end.
        /// </summary>
        /// <param name="fitPoints">The points to pass through, in order; at least degree + 1 once the ones that coincide are dropped.</param>
        /// <param name="degree">The degree, from 1 to 10, and at most one less than the number of points; 3 if not given.</param>
        /// <returns>The spline, not rational, over [0, 1].</returns>
        /// <remarks>
        /// <para>
        /// The method is global interpolation with chord-length parameters (Piegl and Tiller, <i>The NURBS Book</i>, 9.2.1).
        /// Each fit point is given a parameter equal to the share of the length of the chain through the points that comes
        /// before it, so that the points are met at parameters in proportion to how far apart they are; the inner knots are
        /// the averages of degree of those parameters in a row; and the control points are what makes the curve meet every
        /// fit point, found by solving one banded system, which costs the same for each point however many there are.
        /// </para>
        /// <para>
        /// A fit point within the point tolerance of the one before it is dropped, as a polyline drops a vertex, since two
        /// points the same make parameters the same and a system that cannot be solved.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when a point is not a finite point, or there are fewer than degree + 1 distinct points.</exception>
        public static GeoSpline3 Interpolate(IEnumerable<GeoPoint3> fitPoints, int degree = 3) => Interpolate(fitPoints, degree, Tolerance.Global);

        /// <summary>
        /// Builds the spline that passes through a list of fit points, within a tolerance that decides which neighbouring fit
        /// points are one point.
        /// </summary>
        /// <param name="fitPoints">The points to pass through, in order; at least degree + 1 once the ones that coincide are dropped.</param>
        /// <param name="degree">The degree, from 1 to 10, and at most one less than the number of points.</param>
        /// <param name="tolerance">The tolerance: a fit point within its point tolerance of the one kept before it is dropped.</param>
        /// <returns>The spline, not rational, over [0, 1].</returns>
        /// <remarks>
        /// See <see cref="Interpolate(IEnumerable{GeoPoint3}, int)"/>. Points just above the tolerance are kept, and the chord
        /// lengths the parameters come from are then tiny beside the rest, so the curve can swing far between them, by hundreds of
        /// thousands where the chain turns, as it can in the plane (see <see cref="GeoSpline2.Interpolate(IEnumerable{GeoPoint2}, int, Tolerance)"/>). Drop such points before
        /// asking, or give a tolerance that does.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when a point is not a finite point, or there are fewer than degree + 1 distinct points.</exception>
        public static GeoSpline3 Interpolate(IEnumerable<GeoPoint3> fitPoints, int degree, Tolerance tolerance)
        {
            double[] fit = DistinctFit(fitPoints, degree, tolerance);

            if (fit.Length / 3 < degree + 1)
            {
                throw new ArgumentException("A spline of degree " + degree + " passes through at least " + (degree + 1) + " distinct fit points; " + fit.Length / 3 + " were given.", nameof(fitPoints));
            }

            return new GeoSpline3(NurbsCurve.Interpolate(3, fit, degree, null, null));
        }

        /// <summary>
        /// Builds the spline that passes through a list of fit points and leaves the first and arrives at the last in given
        /// directions.
        /// </summary>
        /// <param name="fitPoints">The points to pass through, in order; at least two once the ones that coincide are dropped.</param>
        /// <param name="startTangent">The direction at the first point; its length does not matter, and it may not be nought.</param>
        /// <param name="endTangent">The direction at the last point; its length does not matter, and it may not be nought.</param>
        /// <param name="degree">The degree, from 2 to 10, and at most one more than the number of points; 3 if not given.</param>
        /// <returns>The spline, not rational, over [0, 1].</returns>
        /// <remarks>
        /// As <see cref="Interpolate(IEnumerable{GeoPoint3}, int)"/>, with the two end conditions added to the system: the
        /// curve has two more control points. A tangent gives a direction and nothing else: the curve leaves and arrives at
        /// the speed it keeps on average, which over [0, 1] is the length of the chain through the fit points, so that
        /// three points 100 apart along X with the tangent (1, 0, 0) at each end give the straight line through them, run
        /// at an even speed of 200, and a tangent of any other length gives the same curve.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 2 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when a point or a tangent is not finite, a tangent has no length, or there are too few distinct points for the degree.</exception>
        public static GeoSpline3 Interpolate(IEnumerable<GeoPoint3> fitPoints, GeoVector3 startTangent, GeoVector3 endTangent, int degree = 3)
            => Interpolate(fitPoints, startTangent, endTangent, degree, Tolerance.Global);

        /// <summary>
        /// Builds the spline that passes through a list of fit points and leaves the first and arrives at the last in given
        /// directions, within a tolerance that decides which neighbouring fit points are one point.
        /// </summary>
        /// <param name="fitPoints">The points to pass through, in order; at least two once the ones that coincide are dropped.</param>
        /// <param name="startTangent">The direction at the first point; its length does not matter, and it may not be nought.</param>
        /// <param name="endTangent">The direction at the last point; its length does not matter, and it may not be nought.</param>
        /// <param name="degree">The degree, from 2 to 10, and at most one more than the number of points.</param>
        /// <param name="tolerance">The tolerance: a fit point within its point tolerance of the one kept before it is dropped.</param>
        /// <returns>The spline, not rational, over [0, 1].</returns>
        /// <remarks>See <see cref="Interpolate(IEnumerable{GeoPoint3}, GeoVector3, GeoVector3, int)"/> and, for fit points just above the tolerance, <see cref="Interpolate(IEnumerable{GeoPoint3}, int, Tolerance)"/>.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 2 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when a point or a tangent is not finite, a tangent has no length, or there are too few distinct points for the degree.</exception>
        public static GeoSpline3 Interpolate(IEnumerable<GeoPoint3> fitPoints, GeoVector3 startTangent, GeoVector3 endTangent, int degree, Tolerance tolerance)
        {
            if (degree < 2 || degree > NurbsCurve.MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "A spline with a tangent at each end has a degree from 2 to " + NurbsCurve.MostDegree + ".");
            }

            double[] fit = DistinctFit(fitPoints, degree, tolerance);
            double[] start = Direction(startTangent, nameof(startTangent));
            double[] end = Direction(endTangent, nameof(endTangent));

            if (fit.Length / 3 < 2 || degree > fit.Length / 3 + 1)
            {
                throw new ArgumentException("A spline of degree " + degree + " with a tangent at each end passes through at least " + Math.Max(2, degree - 1) + " distinct fit points; " + fit.Length / 3 + " were given.", nameof(fitPoints));
            }

            return new GeoSpline3(NurbsCurve.Interpolate(3, fit, degree, start, end));
        }

        private static double[] Direction(GeoVector3 tangent, string name)
        {
            double length = tangent.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A tangent needs a direction: a finite vector of some length.", name);
            }

            return new[] { tangent.X / length, tangent.Y / length, tangent.Z / length };
        }

        private static double[] DistinctFit(IEnumerable<GeoPoint3> fitPoints, int degree, Tolerance tolerance)
        {
            if (fitPoints == null) throw new ArgumentNullException(nameof(fitPoints));

            if (degree < 1 || degree > NurbsCurve.MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "The degree of a spline is from 1 to " + NurbsCurve.MostDegree + ".");
            }

            var kept = new List<GeoPoint3>();

            foreach (GeoPoint3 point in fitPoints)
            {
                if (!point.IsValid)
                {
                    throw new ArgumentException("A fit point is not a finite point.", nameof(fitPoints));
                }

                if (kept.Count > 0 && kept[kept.Count - 1].IsEqualTo(point, tolerance))
                {
                    continue;
                }

                kept.Add(point);
            }

            return Flatten(kept);
        }

        /// <summary>
        /// Builds the spline of degree one that runs along a polyline.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <returns>The spline, whose control points are the vertices, over [0, 1] with each vertex at the share of the polyline's length that comes before it.</returns>
        /// <remarks>
        /// Nothing is lost: the spline is the polyline. A polyline (0, 0, 0), (100, 0, 0), (100, 50, 0) of length 150 gives
        /// the knots 0, 0, 2/3, 1, 1.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoSpline3 FromPolyline(GeoPolyline3 polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return new GeoSpline3(NurbsCurve.FromChain(3, Flatten(polyline.Vertices)));
        }

        /// <summary>
        /// Builds the spline that is exactly an arc: rational quadratic pieces of a quarter turn at most, each the arc of
        /// its angle of a circle, to rounding.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <returns>The spline, of degree 2 and rational, running the way the arc runs, over [0, 1] with each piece taking an equal share.</returns>
        /// <remarks>
        /// A quarter circle of radius 100 about the origin in the floor is one piece: the control points (100, 0, 0),
        /// (100, 100, 0), (0, 100, 0) with the weights 1, 0.7071, 1. A half circle is two pieces, a whole one four. The ends
        /// are the arc's own: the spline starts at <see cref="GeoArc3.StartPoint"/> and ends at its end point,
        /// but that an arc of a whole turn, and so the circle, ends on its start exactly, its first control point repeated.
        /// </remarks>
        public static GeoSpline3 FromArc(GeoArc3 arc)
        {
            double sweep = arc.SweptAngle;
            int pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI * 0.5) - 1E-9));
            double step = sweep / pieces;
            var ends = new double[(pieces + 1) * 3];
            var apexes = new double[pieces * 3];
            var weights = new double[pieces];

            for (int i = 0; i <= pieces; i++)
            {
                GeoPoint3 end = arc.GetPointAtAngle(i == pieces ? arc.StartAngle + sweep : arc.StartAngle + i * step);
                ends[3 * i] = end.X;
                ends[3 * i + 1] = end.Y;
                ends[3 * i + 2] = end.Z;
            }

            // A whole turn ends where it starts, exactly: the point at the start angle and a whole turn on is worked out a last digit away.
            if (Math.Abs(sweep) >= Math.PI * 2.0)
            {
                Array.Copy(ends, 0, ends, pieces * 3, 3);
            }

            double cosine = Math.Cos(Math.Abs(step) * 0.5);

            for (int i = 0; i < pieces; i++)
            {
                GeoPoint3 middle = arc.GetPointAtAngle(arc.StartAngle + (i + 0.5) * step);
                apexes[3 * i] = arc.Center.X + (middle.X - arc.Center.X) / cosine;
                apexes[3 * i + 1] = arc.Center.Y + (middle.Y - arc.Center.Y) / cosine;
                apexes[3 * i + 2] = arc.Center.Z + (middle.Z - arc.Center.Z) / cosine;
                weights[i] = cosine;
            }

            return new GeoSpline3(NurbsCurve.FromArcPieces(3, ends, apexes, weights));
        }

        /// <summary>
        /// Builds the spline that is exactly a circle: the rational quadratic of nine control points, four pieces of a
        /// quarter turn.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <returns>The spline, of degree 2 and rational, from the circle's point at angle nought round counter-clockwise about its normal, closed.</returns>
        /// <remarks>
        /// Every point of the curve is at the radius from the centre to rounding, in the plane of the circle, and its
        /// length is 2 pi times the radius to a relative 1E-12.
        /// </remarks>
        public static GeoSpline3 FromCircle(GeoCircle3 circle) => FromArc(new GeoArc3(circle.Center, circle.Normal, circle.Radius, 0.0, 0.0));

        #endregion

        #region Reading the data

        /// <summary>
        /// Gets the degree of the spline.
        /// </summary>
        public int Degree => _curve.Degree;

        /// <summary>
        /// Gets the control points, in order, as a read-only list.
        /// </summary>
        public IReadOnlyList<GeoPoint3> ControlPoints => _pointView;

        /// <summary>
        /// Gets the number of control points.
        /// </summary>
        public int ControlPointCount => _points.Length;

        /// <summary>
        /// Gets the weight of each control point, as a read-only list: all one for a spline that is not rational.
        /// </summary>
        public IReadOnlyList<double> Weights => _weightView;

        /// <summary>
        /// Gets the knots, in order, as a read-only list: control points plus degree plus one of them.
        /// </summary>
        public IReadOnlyList<double> Knots => _knotView;

        /// <summary>
        /// Gets a value indicating whether any weight is other than one, so that the control points pull with different
        /// strengths and the spline can be a circle. A spline that is not rational is a plain B-spline.
        /// </summary>
        public bool IsRational => _curve.Rational;

        /// <summary>
        /// Gets the first parameter of the spline: the first knot.
        /// </summary>
        public double DomainStart => _curve.Start;

        /// <summary>
        /// Gets the last parameter of the spline: the last knot.
        /// </summary>
        public double DomainEnd => _curve.End;

        /// <summary>
        /// Gets the point the spline starts at: its first control point.
        /// </summary>
        public GeoPoint3 StartPoint => _points[0];

        /// <summary>
        /// Gets the point the spline ends at: its last control point.
        /// </summary>
        public GeoPoint3 EndPoint => _points[_points.Length - 1];

        /// <summary>
        /// Determines whether the spline ends where it starts, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A closed spline is an ordinary clamped one whose first and last control points coincide, as the nine control
        /// points of <see cref="FromCircle(GeoCircle3)"/> do. It is not periodic: there is no smooth join at the end.
        /// </remarks>
        public bool IsClosed() => IsClosed(Tolerance.Global);

        /// <summary>
        /// Determines whether the spline ends within the point tolerance of where it starts.
        /// </summary>
        public bool IsClosed(Tolerance tolerance) => StartPoint.IsEqualTo(EndPoint, tolerance);

        #endregion

        #region Measuring

        /// <summary>
        /// Gets the length of the spline, measured along it.
        /// </summary>
        /// <remarks>
        /// The integral of the speed, by Gauss-Legendre of ten points over each span, a span cut in halves wherever the rule
        /// over the whole and over its two halves disagree, until they agree to 1E-14 of the length of the span's control
        /// polygon, and cut at each place the speed is least, where a curve that turns back has a corner in what is
        /// integrated; a rational span whose weights are more than two to one apart is cut first. It is good to a relative
        /// 1E-12 and in practice to 1E-15: the circle of radius 100 comes out as 628.31853071796, which is 2 pi times 100.
        /// Where the speed falls to nought, at a cusp, it is good to a little less. It is worked out once.
        /// </remarks>
        public double Length => _curve.Length;

        /// <summary>
        /// Gets the point of the spline at a normalised parameter, where 0 is its start and 1 its end.
        /// </summary>
        /// <param name="parameter">The share of the domain, from 0 to 1; one outside is taken as the nearer end. It is a share of the domain in the knots, not of the length: <see cref="GetPointAtDistance(double)"/> goes by length.</param>
        /// <returns>The point, as <see cref="GetPointAtKnot(double)"/> gives it at the knot value that share of the domain comes to.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        public GeoPoint3 GetPointAtParameter(double parameter)
        {
            Guard.Finite(parameter, nameof(parameter), "A parameter has to be a finite number.");

            return GetPointAtKnot(_curve.KnotAtParameter(parameter));
        }

        /// <summary>
        /// Gets the point of the spline at a value of its knots.
        /// </summary>
        /// <param name="knot">A value of the domain, from <see cref="DomainStart"/> to <see cref="DomainEnd"/>; one outside is taken as the nearer end.</param>
        /// <returns>The point, by de Boor's scheme in homogeneous coordinates: a weighted mean of the control points of the span, exact to rounding.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot value is not a finite number.</exception>
        public GeoPoint3 GetPointAtKnot(double knot)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            var result = new double[3];
            _curve.PointAt(knot, result);

            return new GeoPoint3(result[0], result[1], result[2]);
        }

        /// <summary>
        /// Gets the normalised parameter, from 0 to 1, of a value of the knots.
        /// </summary>
        /// <param name="knot">A value of the domain; one outside is taken as the nearer end.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot value is not a finite number.</exception>
        public double GetParameterAtKnot(double knot)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            return _curve.ParameterAtKnot(knot);
        }

        /// <summary>
        /// Gets the value of the knots at a normalised parameter, where 0 is <see cref="DomainStart"/> and 1 is <see cref="DomainEnd"/>.
        /// </summary>
        /// <param name="parameter">The share of the domain; one outside 0 to 1 is taken as the nearer end.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        public double GetKnotAtParameter(double parameter)
        {
            Guard.Finite(parameter, nameof(parameter), "A parameter has to be a finite number.");

            return _curve.KnotAtParameter(parameter);
        }

        /// <summary>
        /// Gets a derivative of the spline at a value of its knots, taken with respect to the knot value.
        /// </summary>
        /// <param name="knot">A value of the domain; one outside is taken as the nearer end. At a knot where the spline is not smooth the derivative after the knot is given, and at the end of the domain the one before it.</param>
        /// <param name="order">Which derivative, from 1 (the velocity) to 10 (more than the degree gives nought for a spline that is not rational).</param>
        /// <returns>The derivative as a vector. Its length depends on the domain: the spline over [0, 2] runs half as fast as the same one over [0, 1].</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot value is not a finite number or the order is outside 1 to 10.</exception>
        public GeoVector3 GetDerivativeAtKnot(double knot, int order)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            if (order < 1 || order > NurbsCurve.MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(order), order, "The order of a derivative is from 1 to " + NurbsCurve.MostDegree + ".");
            }

            double[] row = _curve.Derivatives(knot, order)[order];

            return new GeoVector3(row[0], row[1], row[2]);
        }

        /// <summary>
        /// Gets the direction the spline runs in at a normalised parameter.
        /// </summary>
        /// <param name="parameter">The share of the domain, from 0 to 1; one outside is taken as the nearer end.</param>
        /// <returns>The unit vector, as <see cref="GetTangentAtKnot(double)"/> gives it.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline stands still there: every derivative to the degree vanishes.</exception>
        public GeoVector3 GetTangentAtParameter(double parameter)
        {
            Guard.Finite(parameter, nameof(parameter), "A parameter has to be a finite number.");

            return GetTangentAtKnot(_curve.KnotAtParameter(parameter));
        }

        /// <summary>
        /// Gets the direction the spline runs in at a value of its knots.
        /// </summary>
        /// <param name="knot">A value of the domain; one outside is taken as the nearer end.</param>
        /// <returns>The unit vector along the first derivative; where that is nought, as at a cusp or at an end whose first two control points coincide, along the first higher derivative that is not.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot value is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when every derivative to the degree vanishes there. That is a spline that stands still at that place, as one does along a span whose control points all coincide, a stretch that a spline made from other splines can have; the spline as a whole always moves, since its control points are not all one point.</exception>
        public GeoVector3 GetTangentAtKnot(double knot)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            var tangent = new double[3];

            if (!_curve.TryTangent(knot, tangent))
            {
                throw new InvalidOperationException("The spline stands still at that knot value.");
            }

            return new GeoVector3(tangent[0], tangent[1], tangent[2]);
        }

        /// <summary>
        /// Gets the curvature of the spline at a normalised parameter: how fast its direction turns for each unit of length.
        /// </summary>
        /// <param name="parameter">The share of the domain, from 0 to 1; one outside is taken as the nearer end.</param>
        /// <returns>The curvature, as <see cref="GetCurvatureAtKnot(double)"/> gives it.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline stands still there, where it has no direction to turn.</exception>
        public double GetCurvatureAtParameter(double parameter)
        {
            Guard.Finite(parameter, nameof(parameter), "A parameter has to be a finite number.");

            return GetCurvatureAtKnot(_curve.KnotAtParameter(parameter));
        }

        /// <summary>
        /// Gets the curvature of the spline at a value of its knots: how fast its direction turns for each unit of length,
        /// which is the reciprocal of the radius of the circle that fits it there.
        /// </summary>
        /// <param name="knot">A value of the domain; one outside is taken as the nearer end.</param>
        /// <returns>
        /// Never negative, since in space a curve has no left or right to turn to: the circle of radius 100 has 0.01
        /// everywhere, and a straight run has nought.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot value is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline stands still at the knot value, where it has no direction to turn.</exception>
        public double GetCurvatureAtKnot(double knot)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            double[][] derivatives = _curve.Derivatives(knot, 2);
            var velocity = new GeoVector3(derivatives[1][0], derivatives[1][1], derivatives[1][2]);
            var acceleration = new GeoVector3(derivatives[2][0], derivatives[2][1], derivatives[2][2]);
            double speed = velocity.Length;

            if (!(speed > _curve.StillSpeed))
            {
                throw new InvalidOperationException("The spline stands still at that knot value, where it has no curvature.");
            }

            return velocity.CrossProduct(acceleration).Length / (speed * speed * speed);
        }

        /// <summary>
        /// Gets the normalised parameter at which the spline has run a length from its start.
        /// </summary>
        /// <param name="distance">The length along the spline; nought or less gives 0 and the whole <see cref="Length"/> or more gives 1.</param>
        /// <returns>The share of the domain, from 0 to 1, found by Newton's method on the integral of the speed, kept inside a bracket: the length from the start to the point at it is the length asked for to a relative 4E-14 of its span.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public double GetParameterAtDistance(double distance)
        {
            Guard.Finite(distance, nameof(distance), "A distance has to be a finite number.");

            return _curve.ParameterAtKnot(_curve.ParameterAtLength(distance));
        }

        /// <summary>
        /// Gets the point of the spline a length along it from its start.
        /// </summary>
        /// <param name="distance">The length along the spline; nought or less gives the start and the whole <see cref="Length"/> or more the end.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a finite number.</exception>
        public GeoPoint3 GetPointAtDistance(double distance)
        {
            Guard.Finite(distance, nameof(distance), "A distance has to be a finite number.");

            return GetPointAtKnot(_curve.ParameterAtLength(distance));
        }

        /// <summary>
        /// Gets the length along the spline from its start to a normalised parameter.
        /// </summary>
        /// <param name="parameter">The share of the domain, from 0 to 1; one outside is taken as the nearer end.</param>
        /// <returns>The length: nought at 0 and <see cref="Length"/> at 1, to a relative 1E-12.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        public double GetDistanceAtParameter(double parameter)
        {
            Guard.Finite(parameter, nameof(parameter), "A parameter has to be a finite number.");

            return _curve.LengthTo(_curve.KnotAtParameter(parameter));
        }

        /// <summary>
        /// Gets the box with sides along the axes that the spline fills exactly, from its smallest to its largest
        /// coordinates.
        /// </summary>
        /// <returns>The box.</returns>
        /// <remarks>
        /// Not the box of the control points, which is larger: the cubic with the control points (0, 0, 0), (100, 150, 60), (200, 150, 60),
        /// (300, 0, 0) fills 300 by 112.5 by 45, where its control points fill 300 by 150 by 60. Each end of a span, and each place where a
        /// coordinate stops rising or falling, which is a root of a polynomial of the span found without missing any, is
        /// measured; the box is exact to rounding.
        /// </remarks>
        public GeoAabb3 GetAabb()
        {
            var min = new double[3];
            var max = new double[3];
            _curve.Bounds(min, max);

            return new GeoAabb3(new GeoPoint3(min[0], min[1], min[2]), new GeoPoint3(max[0], max[1], max[2]));
        }

        #endregion

        #region Cutting, turning and moving

        /// <summary>
        /// Cuts the spline in two at a normalised parameter.
        /// </summary>
        /// <param name="parameter">The share of the domain to cut at, above 0 and below 1.</param>
        /// <returns>The part before the parameter and the part after it, over the two parts of the domain.</returns>
        /// <remarks>
        /// Exact, as <see cref="SplitAtKnot(double)"/> is: the share is turned into the value of the knots it comes to and the
        /// spline is cut there. A share so near an end that its knot value is the end's, to a double, is refused.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not above 0 and below 1, or comes to an end of the domain.</exception>
        /// <exception cref="ArgumentException">Thrown when a part would be a spline that stands still: all its control points one point.</exception>
        public GeoSpline3[] Split(double parameter)
        {
            if (!(parameter > 0.0 && parameter < 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(parameter), parameter, "A spline is cut inside its domain, above 0 and below 1.");
            }

            double knot = _curve.KnotAtParameter(parameter);

            if (!(knot > _curve.Start && knot < _curve.End))
            {
                throw new ArgumentOutOfRangeException(nameof(parameter), parameter, "A spline is cut inside its domain, and this share comes to the knot value of an end, " + knot.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ".");
            }

            return Cut(knot, nameof(parameter));
        }

        /// <summary>
        /// Cuts the spline in two at a value of its knots.
        /// </summary>
        /// <param name="knot">The knot value to cut at, above <see cref="DomainStart"/> and below <see cref="DomainEnd"/>.</param>
        /// <returns>The part before the knot value, over [DomainStart, knot], and the part after it, over [knot, DomainEnd].</returns>
        /// <remarks>
        /// Exact: the knot at the value is raised to the degree, which cuts the control polygon without moving the
        /// curve, so the two parts are the two pieces of the spline, with the same degree and weights, and meet at the point
        /// the spline has there, which is the last control point of the first and the first of the second, to the last bit.
        /// A point of the first part at a knot value is the spline's point at it, to rounding.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot value is not above the first knot and below the last.</exception>
        /// <exception cref="ArgumentException">Thrown when a part would be a spline that stands still: all its control points one point, as a cut inside a span whose control points all coincide would leave.</exception>
        public GeoSpline3[] SplitAtKnot(double knot) => Cut(knot, nameof(knot));

        private GeoSpline3[] Cut(double knot, string name)
        {
            if (!(knot > _curve.Start && knot < _curve.End))
            {
                throw new ArgumentOutOfRangeException(name, knot, "A spline is cut inside its domain, above its first knot and below its last.");
            }

            _curve.Split(knot, out NurbsCurve first, out NurbsCurve second);

            if (NurbsCurve.AllTheSame(first.Points, 3) || NurbsCurve.AllTheSame(second.Points, 3))
            {
                throw new ArgumentException("The spline stands still where it is cut, at the knot value " + knot.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", and a part of it would be one point.", name);
            }

            return new[] { new GeoSpline3(first), new GeoSpline3(second) };
        }

        /// <summary>
        /// Gets the spline running the other way, from its end to its start.
        /// </summary>
        /// <returns>The spline with its control points and weights in the opposite order and its knots mirrored about the middle of the domain, so that it has the same domain: the point at a knot value is the point of the original at the knot value as far from the other end.</returns>
        /// <remarks>
        /// Each distinct knot is mirrored once, so knots that were equal stay equal and knots that were apart stay apart: where rounding
        /// would put two mirrored knots on one number the later is moved one unit in the last place on, and one that would land on the
        /// end of the domain one back. A mirrored knot is rounded to the last place of the domain's ends, which moves the curve by the
        /// speed times that.
        /// </remarks>
        public GeoSpline3 Reverse() => new GeoSpline3(_curve.Reversed());

        /// <summary>
        /// Creates a copy of this spline.
        /// </summary>
        /// <remarks>
        /// A spline is immutable and what it hands out is read-only, so nothing is shared that could be changed. It exists
        /// so that every geometry type offers the same way to ask for a copy.
        /// </remarks>
        public GeoSpline3 Clone() => new GeoSpline3(_curve);

        /// <summary>
        /// Gets the spline moved by a vector.
        /// </summary>
        /// <param name="vector">How far to move it.</param>
        /// <returns>The moved spline: the control points moved, the weights and knots as they were.</returns>
        public GeoSpline3 Translate(GeoVector3 vector)
        {
            var moved = new double[_curve.Points.Length];

            for (int i = 0; i < _points.Length; i++)
            {
                moved[3 * i] = _points[i].X + vector.X;
                moved[3 * i + 1] = _points[i].Y + vector.Y;
                moved[3 * i + 2] = _points[i].Z + vector.Z;
            }

            return new GeoSpline3(_curve.WithPoints(moved));
        }

        /// <summary>
        /// Gets the spline under a transformation.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed spline: the control points carried by the transformation, the weights and knots as they were.</returns>
        /// <remarks>
        /// A NURBS is carried by any affine map by carrying its control points: a rotation, a scaling that differs between
        /// the axes, a shear and a mirror all give the spline that is the image of the old one, exactly, a circle among them,
        /// which an uneven scaling makes the ellipse it is. The matrix of a transformation holds nothing but the affine part,
        /// so there is nothing that cannot be carried but a transformation that squeezes space onto a point, which is refused.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation carries every control point to one point.</exception>
        public GeoSpline3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));

            var moved = new double[_curve.Points.Length];

            for (int i = 0; i < _points.Length; i++)
            {
                GeoPoint3 point = transform.Transform(_points[i]);
                moved[3 * i] = point.X;
                moved[3 * i + 1] = point.Y;
                moved[3 * i + 2] = point.Z;
            }

            if (NurbsCurve.AllTheSame(moved, 3))
            {
                throw new InvalidOperationException("This transformation squeezes the spline onto one point.");
            }

            return new GeoSpline3(_curve.WithPoints(moved));
        }

        /// <summary>
        /// Translates a spline by a vector.
        /// </summary>
        public static GeoSpline3 operator +(GeoSpline3 spline, GeoVector3 vector) => spline.Translate(vector);

        /// <summary>
        /// Translates a spline backwards by a vector.
        /// </summary>
        public static GeoSpline3 operator -(GeoSpline3 spline, GeoVector3 vector) => spline.Translate(-vector);

        /// <summary>
        /// Lays this spline out in the plane of a frame, projecting it square onto that plane: each control point laid
        /// where the frame puts it with its distance from the plane dropped, the weights and knots as they were.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The spline in the plane. Exact, for an orthogonal projection is affine: a spline lying in the plane comes back as it is.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the spline stands square to the plane, so that every control point lands on one point.</exception>
        public GeoSpline2 ProjectToSpline2(GeoCoordinateSystem3 frame) => PlanarMap.ProjectToSpline2(frame, this);

        #endregion

        #region Approximating by straight pieces

        /// <summary>
        /// Approximates the spline as a chain of straight pieces, none of which strays further from it than a chord
        /// tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between the spline and the chain, in drawing units; above nought. There is no automatic one: a spline has no radius to take a share of.</param>
        /// <returns>The chain, from the start of the spline to its end, its corners on the spline.</returns>
        /// <remarks>
        /// <para>
        /// The promise holds however fine the tolerance, however sharp the bend and whatever the weights: every span is cut
        /// in halves until the piece is within the tolerance of the chord from its start to its end, as for
        /// <see cref="GeoSpline2.Flatten(double)"/>: every control point of a piece within it, or the second differences of a
        /// piece that is not rational small enough, or the middle control point of a conic low enough. Pieces are cut in halves,
        /// so the circle of radius 100 within 0.01 is 256 chords where 223 would hold.
        /// </para>
        /// <para>
        /// A tolerance that would need more than 1 048 576 pieces is refused. The result is a chain, which merges corners
        /// closer together than the point tolerance (1E-3 by default), so a tolerance whose pieces would be shorter than that
        /// is not kept, as for <see cref="GeoPolylineArc2.Flatten(double)"/>.
        /// </para>
        /// <para>
        /// A spline no longer than the point tolerance, all of it within the tolerance of its start, which a chain would merge into one
        /// point, is the chain of its corners as they are, the repeats of one point to the bit left out: the part 0.000245 long that
        /// <see cref="Split(double)"/> at a millionth cuts off the fit through (0, 0), (30, 20), (60, -10), (100, 0) is the chord from its
        /// start to its end, where it was refused. <see cref="Split(double)"/> keeps such a part, exactly, as it keeps any other.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is not a positive number, or needs more than 1 048 576 pieces.</exception>
        /// <exception cref="ArgumentException">Thrown when every corner the spline is cut at is the same point, to the bit: a spline that stands still, or a closed one whose chord tolerance is so large that it is cut at its ends alone.</exception>
        public GeoPolyline3 Flatten(double chordTolerance)
        {
            Guard.Positive(chordTolerance, nameof(chordTolerance), "A chord tolerance has to be a positive number.");

            var corners = new List<double>();
            _curve.Flatten(chordTolerance, corners);
            GeoPoint3[] points = Unflatten(corners.ToArray());

            // A chain keeps a corner that is not within the point tolerance of the last it kept, so it has two corners when one of
            // them is not within the tolerance of the first, as for every spline longer than the tolerance.
            for (int i = 1; i < points.Length; i++)
            {
                if (!points[i].IsEqualTo(points[0]))
                {
                    return new GeoPolyline3(points);
                }
            }

            // A spline that lies within the point tolerance of its start, shorter than the tolerance, as the part Split cuts off
            // near an end can be, is the chain of its corners as they are, the repeats of one point to the bit left out.
            var kept = new List<GeoPoint3>(points.Length);

            foreach (GeoPoint3 point in points)
            {
                if (kept.Count == 0 || point.X != kept[kept.Count - 1].X || point.Y != kept[kept.Count - 1].Y || point.Z != kept[kept.Count - 1].Z)
                {
                    kept.Add(point);
                }
            }

            if (kept.Count < 2)
            {
                throw new ArgumentException("Every corner the spline is cut at is one point: a spline that stands still, or a closed one that the chord tolerance leaves cut at its ends alone.");
            }

            return new GeoPolyline3(kept.ToArray(), true);
        }

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another spline holds exactly the same degree, control points, weights and knots.
        /// </summary>
        /// <remarks>
        /// Two splines that draw the same curve may be written differently, a knot inserted that did not move it: they are
        /// not equal, since what they hold is not.
        /// </remarks>
        public bool Equals(GeoSpline3 other)
        {
            if (other is null || Degree != other.Degree || _points.Length != other._points.Length)
            {
                return false;
            }

            for (int i = 0; i < _points.Length; i++)
            {
                if (!_points[i].Equals(other._points[i]) || !_curve.Weights[i].Equals(other._curve.Weights[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < _curve.Knots.Length; i++)
            {
                if (!_curve.Knots[i].Equals(other._curve.Knots[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified object is an equal spline.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoSpline3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this spline.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Degree;

                for (int i = 0; i < _points.Length; i++)
                {
                    hash = hash * 397 ^ _points[i].GetHashCode();
                    hash = hash * 397 ^ _curve.Weights[i].GetHashCode();
                }

                for (int i = 0; i < _curve.Knots.Length; i++)
                {
                    hash = hash * 397 ^ _curve.Knots[i].GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Determines whether two splines hold exactly the same degree, control points, weights and knots.
        /// </summary>
        public static bool operator ==(GeoSpline3 left, GeoSpline3 right) => left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Determines whether two splines hold different degrees, control points, weights or knots.
        /// </summary>
        public static bool operator !=(GeoSpline3 left, GeoSpline3 right) => !(left == right);

        /// <summary>
        /// Describes the spline.
        /// </summary>
        public override string ToString()
        {
            return $"GeoSpline3[Degree:{Degree}, {ControlPointCount} control points, Domain:[{DomainStart:0.###}, {DomainEnd:0.###}]{(IsRational ? ", Rational" : string.Empty)}]";
        }

        #endregion
    }
}
