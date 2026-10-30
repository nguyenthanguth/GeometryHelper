using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The NURBS data of a <see cref="DxfSpline"/> in one value, as AutoCAD's <c>NurbsData</c>: its degree, whether it is rational,
    /// closed and periodic, its control points, knots and weights, and the tolerances of its points and knots.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is what <see cref="DxfSpline.NurbsData"/> gives and takes: a cubic of 4 control points from (0, 0, 0) to (30, 0, 0)
    /// has the knots 0, 0, 0, 0, 1, 1, 1, 1 and no weights. The lists are copied when the value is made and given out read-only,
    /// so a value once made does not change.
    /// </para>
    /// <para>
    /// The value is not checked when it is made: a spline checks it when it is given one, as AutoCAD does.
    /// </para>
    /// </remarks>
    public readonly struct DxfNurbsData
    {
        private static readonly IReadOnlyList<GeoPoint3> NoPoints = Array.AsReadOnly(new GeoPoint3[0]);
        private static readonly IReadOnlyList<double> NoNumbers = Array.AsReadOnly(new double[0]);

        private readonly IReadOnlyList<GeoPoint3> _controlPoints;
        private readonly IReadOnlyList<double> _knots;
        private readonly IReadOnlyList<double> _weights;

        /// <summary>
        /// Initializes the NURBS data of a spline, as AutoCAD's <c>new NurbsData(degree, rational, closed, periodic,
        /// controlPoints, knots, weights, controlPointTolerance, knotTolerance)</c>.
        /// </summary>
        /// <param name="degree">The degree.</param>
        /// <param name="rational">Whether the weights count.</param>
        /// <param name="closed">Whether the spline is closed.</param>
        /// <param name="periodic">Whether it is periodic.</param>
        /// <param name="controlPoints">The control points, in the world.</param>
        /// <param name="knots">The knots.</param>
        /// <param name="weights">The weights, one to each control point; null or empty for none.</param>
        /// <param name="controlPointTolerance">The tolerance of the control points (group 43 of a SPLINE).</param>
        /// <param name="knotTolerance">The tolerance of the knots (group 42).</param>
        /// <exception cref="ArgumentNullException">Thrown when the control points or the knots are null.</exception>
        public DxfNurbsData(int degree, bool rational, bool closed, bool periodic, IEnumerable<GeoPoint3> controlPoints, IEnumerable<double> knots, IEnumerable<double> weights, double controlPointTolerance, double knotTolerance)
        {
            if (controlPoints == null)
            {
                throw new ArgumentNullException(nameof(controlPoints));
            }

            if (knots == null)
            {
                throw new ArgumentNullException(nameof(knots));
            }

            Degree = degree;
            Rational = rational;
            Closed = closed;
            Periodic = periodic;
            _controlPoints = new List<GeoPoint3>(controlPoints).AsReadOnly();
            _knots = new List<double>(knots).AsReadOnly();
            _weights = weights == null ? NoNumbers : new List<double>(weights).AsReadOnly();
            ControlPointTolerance = controlPointTolerance;
            KnotTolerance = knotTolerance;
        }

        /// <summary>Gets the degree, as AutoCAD's <c>Degree</c>.</summary>
        public int Degree { get; }

        /// <summary>Gets whether the weights count, as AutoCAD's <c>Rational</c>.</summary>
        public bool Rational { get; }

        /// <summary>Gets whether the spline is closed, as AutoCAD's <c>Closed</c>.</summary>
        public bool Closed { get; }

        /// <summary>
        /// Gets whether the spline is periodic, as AutoCAD's <c>Periodic</c>: of a SPLINE, the flag 2 of its group 70, the periodic
        /// of the DXF reference, which AutoCAD writes on its closed splines of a smooth seam too; <see cref="DxfSpline.IsPeriodic"/>
        /// is AutoCAD's reading, the flag 2048.
        /// </summary>
        public bool Periodic { get; }

        /// <summary>Gets the tolerance of the control points, as AutoCAD's <c>ControlPointTolerance</c>.</summary>
        public double ControlPointTolerance { get; }

        /// <summary>Gets the tolerance of the knots, as AutoCAD's <c>KnotTolerance</c>.</summary>
        public double KnotTolerance { get; }

        /// <summary>Gets the control points, in the world, as AutoCAD's <c>GetControlPoints</c>.</summary>
        /// <returns>The points, read-only; none for a value made by default.</returns>
        public IReadOnlyList<GeoPoint3> GetControlPoints() => _controlPoints ?? NoPoints;

        /// <summary>Gets the knots, as AutoCAD's <c>GetKnots</c>.</summary>
        /// <returns>The knots, read-only.</returns>
        public IReadOnlyList<double> GetKnots() => _knots ?? NoNumbers;

        /// <summary>Gets the weights, as AutoCAD's <c>GetWeights</c>: none for a spline that is not rational.</summary>
        /// <returns>The weights, read-only.</returns>
        public IReadOnlyList<double> GetWeights() => _weights ?? NoNumbers;

        /// <summary>
        /// Gets whether this data is the same as another's within <see cref="Tolerance.Default"/>, as AutoCAD's <c>IsEqualTo</c>:
        /// the same degree and flags, the same counts, and points, knots and weights each within the point tolerance.
        /// </summary>
        /// <param name="other">The other data.</param>
        /// <returns>Whether they are the same.</returns>
        public bool IsEqualTo(DxfNurbsData other) => IsEqualTo(other, Tolerance.Default);

        /// <summary>Gets whether this data is the same as another's within a tolerance, as AutoCAD's <c>IsEqualTo</c>.</summary>
        /// <param name="other">The other data.</param>
        /// <param name="tolerance">The tolerance: points, knots and weights are compared within its point tolerance.</param>
        /// <returns>Whether they are the same.</returns>
        public bool IsEqualTo(DxfNurbsData other, Tolerance tolerance)
        {
            if (Degree != other.Degree || Rational != other.Rational || Closed != other.Closed || Periodic != other.Periodic)
            {
                return false;
            }

            IReadOnlyList<GeoPoint3> points = GetControlPoints();
            IReadOnlyList<GeoPoint3> otherPoints = other.GetControlPoints();

            if (points.Count != otherPoints.Count)
            {
                return false;
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (!points[i].IsEqualTo(otherPoints[i], tolerance))
                {
                    return false;
                }
            }

            return SameNumbers(GetKnots(), other.GetKnots(), tolerance) && SameNumbers(GetWeights(), other.GetWeights(), tolerance);
        }

        /// <summary>Gives the data as a line for debugging: its degree and counts.</summary>
        /// <returns>The data as a line, such as <c>degree 3, 4 control points, 8 knots</c>.</returns>
        public override string ToString()
            => "degree " + Degree.ToString(CultureInfo.InvariantCulture) + ", " + GetControlPoints().Count.ToString(CultureInfo.InvariantCulture) + " control points, " + GetKnots().Count.ToString(CultureInfo.InvariantCulture) + " knots";

        private static bool SameNumbers(IReadOnlyList<double> a, IReadOnlyList<double> b, Tolerance tolerance)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!(Math.Abs(a[i] - b[i]) <= tolerance.EqualPoint))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
