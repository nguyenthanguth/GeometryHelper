using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GeometryHelper.Core;
using GeometryHelper.Internal;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A curve in the plane that is not a chain of pieces but one smooth sweep: a NURBS, a B-spline whose control points
    /// may pull on it with different strengths, which is what an AutoCAD SPLINE and an IFC B-spline curve hold.
    /// <para>
    /// It is given by its degree, its control points, its knots and, if it is rational, its weights. The knots cut the
    /// parameter into spans, and each span is a polynomial (or a ratio of two) of the degree; the curve is clamped, so it
    /// starts at its first control point and ends at its last, and the control polygon, the chain through the control
    /// points, shows where it goes without touching it. The control points of a cubic with the corners (0, 0), (100, 150),
    /// (200, 150), (300, 0) and the knots 0, 0, 0, 0, 1, 1, 1, 1 give the Bézier curve that rises to 112.5 in the middle,
    /// at (150, 112.5), not to the 150 of the polygon. A circle is the rational quadratic of nine control points that
    /// <see cref="FromCircle(GeoCircle2)"/> builds, every point of it at the radius to rounding.
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
    /// It is immutable: every operation gives a new spline. It is a class, as <see cref="GeoPolylineArc2"/> is, because it
    /// holds arrays, and what it hands out of them is read-only.
    /// </para>
    /// </summary>
    public sealed partial class GeoSpline2 : IEquatable<GeoSpline2>
    {
        private readonly NurbsCurve _curve;
        private readonly GeoPoint2[] _points;
        private readonly ReadOnlyCollection<GeoPoint2> _pointView;
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
        /// The numbers are copied, and kept as they are: nothing is rescaled or reordered. A spline of degree 3 with the
        /// control points (0, 0), (100, 150), (200, 150), (300, 0) takes the knots 0, 0, 0, 0, 1, 1, 1, 1; with a fifth control
        /// point it takes nine knots, such as 0, 0, 0, 0, 0.5, 1, 1, 1, 1.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the control points or the knots are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10, a weight is not a positive number, or the largest weight is more than 1E12 times the smallest.</exception>
        /// <exception cref="ArgumentException">Thrown when there are too few control points, a point or a knot is not a finite number, the knots are the wrong number or not clamped, or the control points are all one point.</exception>
        public GeoSpline2(int degree, IEnumerable<GeoPoint2> controlPoints, IEnumerable<double> knots, IEnumerable<double> weights = null)
        {
            if (controlPoints == null) throw new ArgumentNullException(nameof(controlPoints));
            if (knots == null) throw new ArgumentNullException(nameof(knots));

            double[] flat = Flatten(controlPoints);
            double[] knotArray = new List<double>(knots).ToArray();
            double[] weightArray = weights == null ? null : new List<double>(weights).ToArray();

            NurbsCurve.Check(degree, flat, 2, knotArray, weightArray);

            _curve = new NurbsCurve(2, degree, flat, knotArray, weightArray);
            _points = Unflatten(flat);
            _pointView = Array.AsReadOnly(_points);
            _weightView = Array.AsReadOnly(_curve.Weights);
            _knotView = Array.AsReadOnly(_curve.Knots);
        }

        /// <summary>
        /// Initializes a spline from a curve already checked, which it owns.
        /// </summary>
        internal GeoSpline2(NurbsCurve curve)
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

        private static double[] Flatten(IEnumerable<GeoPoint2> points)
        {
            var list = new List<double>();

            foreach (GeoPoint2 point in points)
            {
                list.Add(point.X);
                list.Add(point.Y);
            }

            return list.ToArray();
        }

        private static GeoPoint2[] Unflatten(double[] flat)
        {
            var points = new GeoPoint2[flat.Length / 2];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GeoPoint2(flat[2 * i], flat[2 * i + 1]);
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
        public static GeoSpline2 FromControlPoints(int degree, IEnumerable<GeoPoint2> controlPoints)
        {
            if (controlPoints == null) throw new ArgumentNullException(nameof(controlPoints));

            double[] flat = Flatten(controlPoints);
            bool enough = degree >= 1 && degree <= NurbsCurve.MostDegree && flat.Length / 2 >= degree + 1;

            // With too few points or a degree out of range there are no knots to make, and the check names the fault
            // before it looks at them.
            double[] knots = enough ? NurbsCurve.UniformKnots(flat.Length / 2, degree) : new double[0];
            NurbsCurve.Check(degree, flat, 2, knots, null);

            return new GeoSpline2(new NurbsCurve(2, degree, flat, knots, null));
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
        /// fit point, found by solving one banded system, which costs the same for each point however many there are. Four
        /// points (0, 0), (100, 50), (200, 0), (300, 50) of degree 3 give the curve through all four, met at the parameters
        /// 0, a third, two thirds and 1, the three chords being as long.
        /// </para>
        /// <para>
        /// A fit point within the point tolerance of the one before it is dropped, as a polyline drops a vertex, since two
        /// points the same make parameters the same and a system that cannot be solved.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when a point is not a finite point, or there are fewer than degree + 1 distinct points.</exception>
        public static GeoSpline2 Interpolate(IEnumerable<GeoPoint2> fitPoints, int degree = 3) => Interpolate(fitPoints, degree, Tolerance.Global);

        /// <summary>
        /// Builds the spline that passes through a list of fit points, within a tolerance that decides which neighbouring fit
        /// points are one point.
        /// </summary>
        /// <param name="fitPoints">The points to pass through, in order; at least degree + 1 once the ones that coincide are dropped.</param>
        /// <param name="degree">The degree, from 1 to 10, and at most one less than the number of points.</param>
        /// <param name="tolerance">The tolerance: a fit point within its point tolerance of the one kept before it is dropped.</param>
        /// <returns>The spline, not rational, over [0, 1].</returns>
        /// <remarks>
        /// See <see cref="Interpolate(IEnumerable{GeoPoint2}, int)"/>. Points just above the tolerance are kept, and the chord
        /// lengths the parameters come from are then tiny beside the rest, so the curve can swing far between them: the chain
        /// (0, 0), (100, 0), (100.0011, 0.0005), (100.0022, 0), (200, 50), (300, 0) at the default tolerance of 0.001 keeps all six,
        /// its chords at the three near points 0.0012 long, and the curve through them swings 507 000 outside the box of the points;
        /// at a tolerance of 0.01 the two in the middle are dropped and it stays within about 13 of the box. Drop such points before
        /// asking, or give a tolerance that does.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 1 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when a point is not a finite point, or there are fewer than degree + 1 distinct points.</exception>
        public static GeoSpline2 Interpolate(IEnumerable<GeoPoint2> fitPoints, int degree, Tolerance tolerance)
        {
            double[] fit = DistinctFit(fitPoints, degree, tolerance);

            if (fit.Length / 2 < degree + 1)
            {
                throw new ArgumentException("A spline of degree " + degree + " passes through at least " + (degree + 1) + " distinct fit points; " + fit.Length / 2 + " were given.", nameof(fitPoints));
            }

            return new GeoSpline2(NurbsCurve.Interpolate(2, fit, degree, null, null));
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
        /// <para>
        /// As <see cref="Interpolate(IEnumerable{GeoPoint2}, int)"/>, with the two end conditions added to the system: the
        /// curve has two more control points. A tangent gives a direction and nothing else: the curve leaves and arrives at
        /// the speed it keeps on average, which over [0, 1] is the length of the chain through the fit points, so that
        /// three points 100 apart along X with the tangent (1, 0) at each end give the straight line through them, run at
        /// an even speed of 200, and a tangent of any other length gives the same curve.
        /// </para>
        /// <para>
        /// Two fit points with two tangents of degree 3 are the Hermite cubic: the curve from (0, 0) to (100, 0) leaving
        /// upwards and arriving upwards is an S, which rises above the line, crosses its middle at (50, 0) and dips below it
        /// before it comes up to the end.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 2 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when a point or a tangent is not finite, a tangent has no length, or there are too few distinct points for the degree.</exception>
        public static GeoSpline2 Interpolate(IEnumerable<GeoPoint2> fitPoints, GeoVector2 startTangent, GeoVector2 endTangent, int degree = 3)
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
        /// <remarks>See <see cref="Interpolate(IEnumerable{GeoPoint2}, GeoVector2, GeoVector2, int)"/> and, for fit points just above the tolerance, <see cref="Interpolate(IEnumerable{GeoPoint2}, int, Tolerance)"/>.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is outside 2 to 10.</exception>
        /// <exception cref="ArgumentException">Thrown when a point or a tangent is not finite, a tangent has no length, or there are too few distinct points for the degree.</exception>
        public static GeoSpline2 Interpolate(IEnumerable<GeoPoint2> fitPoints, GeoVector2 startTangent, GeoVector2 endTangent, int degree, Tolerance tolerance)
        {
            if (degree < 2 || degree > NurbsCurve.MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "A spline with a tangent at each end has a degree from 2 to " + NurbsCurve.MostDegree + ".");
            }

            double[] fit = DistinctFit(fitPoints, degree, tolerance);
            double[] start = Direction(startTangent, nameof(startTangent));
            double[] end = Direction(endTangent, nameof(endTangent));

            if (fit.Length / 2 < 2 || degree > fit.Length / 2 + 1)
            {
                throw new ArgumentException("A spline of degree " + degree + " with a tangent at each end passes through at least " + Math.Max(2, degree - 1) + " distinct fit points; " + fit.Length / 2 + " were given.", nameof(fitPoints));
            }

            return new GeoSpline2(NurbsCurve.Interpolate(2, fit, degree, start, end));
        }

        private static double[] Direction(GeoVector2 tangent, string name)
        {
            double length = tangent.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A tangent needs a direction: a finite vector of some length.", name);
            }

            return new[] { tangent.X / length, tangent.Y / length };
        }

        private static double[] DistinctFit(IEnumerable<GeoPoint2> fitPoints, int degree, Tolerance tolerance)
        {
            if (fitPoints == null) throw new ArgumentNullException(nameof(fitPoints));

            if (degree < 1 || degree > NurbsCurve.MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "The degree of a spline is from 1 to " + NurbsCurve.MostDegree + ".");
            }

            var kept = new List<GeoPoint2>();

            foreach (GeoPoint2 point in fitPoints)
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
        /// Nothing is lost: the spline is the polyline. A polyline (0, 0), (100, 0), (100, 50) of length 150 gives the knots
        /// 0, 0, 2/3, 1, 1.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoSpline2 FromPolyline(GeoPolyline2 polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return new GeoSpline2(NurbsCurve.FromChain(2, Flatten(polyline.Vertices)));
        }

        /// <summary>
        /// Builds the spline that is exactly an arc: rational quadratic pieces of a quarter turn at most, each the arc of
        /// its angle of a circle, to rounding.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <returns>The spline, of degree 2 and rational, running the way the arc runs, over [0, 1] with each piece taking an equal share.</returns>
        /// <remarks>
        /// A quarter circle of radius 100 about the origin is one piece: the control points (100, 0), (100, 100), (0, 100)
        /// with the weights 1, 0.7071, 1. A half circle is two pieces, a whole one four, and a sweep of 100 degrees two of
        /// 50. The ends are the arc's own: the spline starts at <see cref="GeoArc2.StartPoint"/> and ends at its end point.
        /// </remarks>
        public static GeoSpline2 FromArc(GeoArc2 arc)
        {
            double sweep = arc.SweptAngle;
            int pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI * 0.5) - 1E-9));
            double step = sweep / pieces;
            var ends = new double[(pieces + 1) * 2];
            var apexes = new double[pieces * 2];
            var weights = new double[pieces];

            for (int i = 0; i <= pieces; i++)
            {
                GeoPoint2 end = arc.GetPointAtAngle(i == pieces ? arc.StartAngle + sweep : arc.StartAngle + i * step);
                ends[2 * i] = end.X;
                ends[2 * i + 1] = end.Y;
            }

            double cosine = Math.Cos(Math.Abs(step) * 0.5);

            for (int i = 0; i < pieces; i++)
            {
                GeoPoint2 middle = arc.GetPointAtAngle(arc.StartAngle + (i + 0.5) * step);
                apexes[2 * i] = arc.Center.X + (middle.X - arc.Center.X) / cosine;
                apexes[2 * i + 1] = arc.Center.Y + (middle.Y - arc.Center.Y) / cosine;
                weights[i] = cosine;
            }

            return new GeoSpline2(NurbsCurve.FromArcPieces(2, ends, apexes, weights));
        }

        /// <summary>
        /// Builds the spline that is exactly a circle: the rational quadratic of nine control points, four pieces of a
        /// quarter turn.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <returns>The spline, of degree 2 and rational, from the circle's point at angle nought round counter-clockwise, closed.</returns>
        /// <remarks>
        /// A circle of radius 100 about the origin has the control points (100, 0), (100, 100), (0, 100), (-100, 100),
        /// (-100, 0), (-100, -100), (0, -100), (100, -100), (100, 0), the weights 1, 0.7071, 1, 0.7071, 1, 0.7071, 1, 0.7071, 1 and the
        /// knots 0, 0, 0, 1/4, 1/4, 1/2, 1/2, 3/4, 3/4, 1, 1, 1. Every point of the curve is at 100 from the origin to
        /// rounding, and its length is 2 pi times 100 to a relative 1E-12.
        /// </remarks>
        public static GeoSpline2 FromCircle(GeoCircle2 circle) => FromArc(new GeoArc2(circle.Center, circle.Radius, 0.0, 0.0));

        #endregion

        #region Reading the data

        /// <summary>
        /// Gets the degree of the spline.
        /// </summary>
        public int Degree => _curve.Degree;

        /// <summary>
        /// Gets the control points, in order, as a read-only list.
        /// </summary>
        public IReadOnlyList<GeoPoint2> ControlPoints => _pointView;

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
        public GeoPoint2 StartPoint => _points[0];

        /// <summary>
        /// Gets the point the spline ends at: its last control point.
        /// </summary>
        public GeoPoint2 EndPoint => _points[_points.Length - 1];

        /// <summary>
        /// Determines whether the spline ends where it starts, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A closed spline is an ordinary clamped one whose first and last control points coincide, as the nine control
        /// points of <see cref="FromCircle(GeoCircle2)"/> do. It is not periodic: there is no smooth join at the end.
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
        public GeoPoint2 GetPointAtParameter(double parameter)
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
        public GeoPoint2 GetPointAtKnot(double knot)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            var result = new double[2];
            _curve.PointAt(knot, result);

            return new GeoPoint2(result[0], result[1]);
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
        public GeoVector2 GetDerivativeAtKnot(double knot, int order)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            if (order < 1 || order > NurbsCurve.MostDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(order), order, "The order of a derivative is from 1 to " + NurbsCurve.MostDegree + ".");
            }

            double[] row = _curve.Derivatives(knot, order)[order];

            return new GeoVector2(row[0], row[1]);
        }

        /// <summary>
        /// Gets the direction the spline runs in at a normalised parameter.
        /// </summary>
        /// <param name="parameter">The share of the domain, from 0 to 1; one outside is taken as the nearer end.</param>
        /// <returns>The unit vector, as <see cref="GetTangentAtKnot(double)"/> gives it.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parameter is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline stands still there: every derivative to the degree vanishes.</exception>
        public GeoVector2 GetTangentAtParameter(double parameter)
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
        public GeoVector2 GetTangentAtKnot(double knot)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            var tangent = new double[2];

            if (!_curve.TryTangent(knot, tangent))
            {
                throw new InvalidOperationException("The spline stands still at that knot value.");
            }

            return new GeoVector2(tangent[0], tangent[1]);
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
        /// Positive where the spline turns counter-clockwise, negative where it turns clockwise, nought where it runs
        /// straight: the circle of radius 100 run counter-clockwise has 0.01 everywhere.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the knot value is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline stands still at the knot value, where it has no direction to turn.</exception>
        public double GetCurvatureAtKnot(double knot)
        {
            Guard.Finite(knot, nameof(knot), "A knot value has to be a finite number.");

            double[][] derivatives = _curve.Derivatives(knot, 2);
            double x1 = derivatives[1][0];
            double y1 = derivatives[1][1];
            double speed = Math.Sqrt(x1 * x1 + y1 * y1);

            if (!(speed > _curve.StillSpeed))
            {
                throw new InvalidOperationException("The spline stands still at that knot value, where it has no curvature.");
            }

            return (x1 * derivatives[2][1] - y1 * derivatives[2][0]) / (speed * speed * speed);
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
        public GeoPoint2 GetPointAtDistance(double distance)
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
        /// <returns>The box, as an unrotated rectangle.</returns>
        /// <remarks>
        /// Not the box of the control points, which is larger: the cubic with the control points (0, 0), (100, 150), (200, 150),
        /// (300, 0) fills 300 by 112.5, where its control points fill 300 by 150. Each end of a span, and each place where a
        /// coordinate stops rising or falling, which is a root of a polynomial of the span found without missing any, is
        /// measured; the box is exact to rounding. A rectangle has its centre and size, so its corners can differ from
        /// the extremes by a unit in the last place.
        /// </remarks>
        public GeoRectangle2 GetAabb()
        {
            var min = new double[2];
            var max = new double[2];
            _curve.Bounds(min, max);

            return new GeoRectangle2(min[0], min[1], max[0] - min[0], max[1] - min[1]);
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
        public GeoSpline2[] Split(double parameter)
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
        public GeoSpline2[] SplitAtKnot(double knot) => Cut(knot, nameof(knot));

        private GeoSpline2[] Cut(double knot, string name)
        {
            if (!(knot > _curve.Start && knot < _curve.End))
            {
                throw new ArgumentOutOfRangeException(name, knot, "A spline is cut inside its domain, above its first knot and below its last.");
            }

            _curve.Split(knot, out NurbsCurve first, out NurbsCurve second);

            if (NurbsCurve.AllTheSame(first.Points, 2) || NurbsCurve.AllTheSame(second.Points, 2))
            {
                throw new ArgumentException("The spline stands still where it is cut, at the knot value " + knot.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ", and a part of it would be one point.", name);
            }

            return new[] { new GeoSpline2(first), new GeoSpline2(second) };
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
        public GeoSpline2 Reverse() => new GeoSpline2(_curve.Reversed());

        /// <summary>
        /// Creates a copy of this spline.
        /// </summary>
        /// <remarks>
        /// A spline is immutable and what it hands out is read-only, so nothing is shared that could be changed. It exists
        /// so that every geometry type offers the same way to ask for a copy.
        /// </remarks>
        public GeoSpline2 Clone() => new GeoSpline2(_curve);

        /// <summary>
        /// Gets the spline moved by a vector.
        /// </summary>
        /// <param name="vector">How far to move it.</param>
        /// <returns>The moved spline: the control points moved, the weights and knots as they were.</returns>
        public GeoSpline2 Translate(GeoVector2 vector)
        {
            var moved = new double[_curve.Points.Length];

            for (int i = 0; i < _points.Length; i++)
            {
                moved[2 * i] = _points[i].X + vector.X;
                moved[2 * i + 1] = _points[i].Y + vector.Y;
            }

            return new GeoSpline2(_curve.WithPoints(moved));
        }

        /// <summary>
        /// Gets the spline under a transformation.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed spline: the control points carried by the transformation, the weights and knots as they were.</returns>
        /// <remarks>
        /// A NURBS is carried by any affine map by carrying its control points: a rotation, a scaling that differs between
        /// the axes, a shear and a mirror all give the spline that is the image of the old one, exactly, a circle among them,
        /// which an uneven scaling makes the ellipse it is. A mirror runs the spline the same way along itself. The matrix of
        /// a transformation holds nothing but the affine part, so there is nothing that cannot be carried but a
        /// transformation that squeezes the plane onto a point, which is refused.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation carries every control point to one point.</exception>
        public GeoSpline2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));

            var moved = new double[_curve.Points.Length];

            for (int i = 0; i < _points.Length; i++)
            {
                GeoPoint2 point = transform.Transform(_points[i]);
                moved[2 * i] = point.X;
                moved[2 * i + 1] = point.Y;
            }

            if (NurbsCurve.AllTheSame(moved, 2))
            {
                throw new InvalidOperationException("This transformation squeezes the spline onto one point.");
            }

            return new GeoSpline2(_curve.WithPoints(moved));
        }

        /// <summary>
        /// Translates a spline by a vector.
        /// </summary>
        public static GeoSpline2 operator +(GeoSpline2 spline, GeoVector2 vector) => spline.Translate(vector);

        /// <summary>
        /// Translates a spline backwards by a vector.
        /// </summary>
        public static GeoSpline2 operator -(GeoSpline2 spline, GeoVector2 vector) => spline.Translate(-vector);

        /// <summary>
        /// Puts this spline of the plane into space, exactly, on the plane of a frame: each control point laid where the
        /// frame puts it, the weights and knots as they were.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <returns>The spline in space.</returns>
        public GeoSpline3 ToSpline3(GeoCoordinateSystem3 frame) => PlanarMap.ToSpline3(frame, this);

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
        /// in halves until the piece is within the tolerance of the chord from its start to its end, which three proofs can
        /// say: every control point of a piece is within it (a piece lies in the hull of its control points); or, for a
        /// piece that is not rational, the second differences of its control points are small enough; or, for a rational
        /// quadratic, a conic, the height of its middle control point is. The circle of radius 100 within 0.01 is 256
        /// chords, every corner on the circle, where 223 would hold, since pieces are cut in halves; the arch of
        /// (0, 0), (100, 150), (200, 150), (300, 0) within 0.01 is 128 chords.
        /// </para>
        /// <para>
        /// A tolerance that would need more than 1 048 576 pieces is refused. The result is a chain, which merges corners
        /// closer together than the point tolerance (1E-3 by default), so a tolerance whose pieces would be shorter than that
        /// is not kept, as for <see cref="GeoPolylineArc2.Flatten(double)"/>; the writers (<c>ObjWriter</c>, <c>Wkt</c>,
        /// <c>DxfWriter</c>) keep every piece.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is not a positive number, or needs more than 1 048 576 pieces.</exception>
        /// <exception cref="ArgumentException">Thrown when the spline is so small that its corners merge into one point.</exception>
        public GeoPolyline2 Flatten(double chordTolerance)
        {
            Guard.Positive(chordTolerance, nameof(chordTolerance), "A chord tolerance has to be a positive number.");

            var corners = new List<double>();
            _curve.Flatten(chordTolerance, corners);

            return new GeoPolyline2(Unflatten(corners.ToArray()));
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
        public bool Equals(GeoSpline2 other)
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
        public override bool Equals(object obj) => obj is GeoSpline2 other && Equals(other);

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
        public static bool operator ==(GeoSpline2 left, GeoSpline2 right) => left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Determines whether two splines hold different degrees, control points, weights or knots.
        /// </summary>
        public static bool operator !=(GeoSpline2 left, GeoSpline2 right) => !(left == right);

        /// <summary>
        /// Describes the spline.
        /// </summary>
        public override string ToString()
        {
            return $"GeoSpline2[Degree:{Degree}, {ControlPointCount} control points, Domain:[{DomainStart:0.###}, {DomainEnd:0.###}]{(IsRational ? ", Rational" : string.Empty)}]";
        }

        #endregion
    }
}
