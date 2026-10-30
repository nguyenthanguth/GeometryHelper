using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The fit data of a <see cref="DxfSpline"/> in one value, as AutoCAD's <c>FitData</c>: the points it passes through, its
    /// degree, the tolerance it is fitted within, the directions it leaves its first point and reaches its last in, and how its
    /// parameters are given.
    /// </summary>
    /// <remarks>
    /// It is what <see cref="DxfSpline.FitData"/> gives and takes: a spline through (0, 0, 0), (10, 5, 0) and (20, 0, 0) of
    /// degree 3, fitted within nought and with no tangents, is <c>new DxfFitData(points, 3, 0)</c>. The points are copied when the
    /// value is made and given out read-only; the other parts are set as AutoCAD's are.
    /// </remarks>
    public struct DxfFitData
    {
        private static readonly IReadOnlyList<GeoPoint3> NoPoints = Array.AsReadOnly(new GeoPoint3[0]);

        private readonly IReadOnlyList<GeoPoint3> _fitPoints;

        /// <summary>
        /// Initializes the fit data of a spline through points, as AutoCAD's fit data of points, a degree and a tolerance: no
        /// tangents at the ends, its parameters by the chord, not periodic.
        /// </summary>
        /// <param name="fitPoints">The points, in the world.</param>
        /// <param name="degree">The degree.</param>
        /// <param name="fitTolerance">The tolerance it is fitted within: nought to pass through every point.</param>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        public DxfFitData(IEnumerable<GeoPoint3> fitPoints, int degree, double fitTolerance)
        {
            if (fitPoints == null)
            {
                throw new ArgumentNullException(nameof(fitPoints));
            }

            _fitPoints = new List<GeoPoint3>(fitPoints).AsReadOnly();
            Degree = degree;
            FitTolerance = fitTolerance;
            StartTangent = default(GeoVector3);
            EndTangent = default(GeoVector3);
            TangentsExist = false;
            KnotParam = DxfKnotParameterizationEnum.Chord;
            Periodic = false;
        }

        /// <summary>Gets or sets the degree, as AutoCAD's <c>Degree</c>.</summary>
        public int Degree { get; set; }

        /// <summary>Gets or sets the tolerance the spline is fitted within, as AutoCAD's <c>FitTolerance</c>.</summary>
        public double FitTolerance { get; set; }

        /// <summary>Gets or sets the direction the spline leaves its first point in, as AutoCAD's <c>StartTangent</c>: read where <see cref="TangentsExist"/> is true.</summary>
        public GeoVector3 StartTangent { get; set; }

        /// <summary>Gets or sets the direction the spline reaches its last point in, as AutoCAD's <c>EndTangent</c>: read where <see cref="TangentsExist"/> is true.</summary>
        public GeoVector3 EndTangent { get; set; }

        /// <summary>Gets or sets whether the tangents at the ends are given, as AutoCAD's <c>TangentsExist</c>.</summary>
        public bool TangentsExist { get; set; }

        /// <summary>Gets or sets how the points are given their parameters, as AutoCAD's <c>KnotParam</c>.</summary>
        public DxfKnotParameterizationEnum KnotParam { get; set; }

        /// <summary>
        /// Gets or sets whether the spline runs round on itself through its points, as AutoCAD's <c>Periodic</c>: of a SPLINE, the
        /// flag 2 of its group 70, as <see cref="DxfNurbsData.Periodic"/>.
        /// </summary>
        public bool Periodic { get; set; }

        /// <summary>Gets the fit points, in the world, as AutoCAD's <c>GetFitPoints</c>.</summary>
        /// <returns>The points, read-only; none for a value made by default.</returns>
        public IReadOnlyList<GeoPoint3> GetFitPoints() => _fitPoints ?? NoPoints;

        /// <summary>
        /// Gets whether this data is the same as another's within <see cref="Tolerance.Default"/>, as AutoCAD's <c>IsEqualTo</c>:
        /// the same degree, flags and counts, and points, tangents and tolerance each within the tolerance.
        /// </summary>
        /// <param name="other">The other data.</param>
        /// <returns>Whether they are the same.</returns>
        public bool IsEqualTo(DxfFitData other) => IsEqualTo(other, Tolerance.Default);

        /// <summary>Gets whether this data is the same as another's within a tolerance, as AutoCAD's <c>IsEqualTo</c>.</summary>
        /// <param name="other">The other data.</param>
        /// <param name="tolerance">The tolerance: points within its point tolerance, tangents within its vector tolerance.</param>
        /// <returns>Whether they are the same.</returns>
        public bool IsEqualTo(DxfFitData other, Tolerance tolerance)
        {
            if (Degree != other.Degree || TangentsExist != other.TangentsExist || KnotParam != other.KnotParam || Periodic != other.Periodic
                || !(Math.Abs(FitTolerance - other.FitTolerance) <= tolerance.EqualPoint))
            {
                return false;
            }

            if (TangentsExist && (!StartTangent.IsEqualTo(other.StartTangent, tolerance) || !EndTangent.IsEqualTo(other.EndTangent, tolerance)))
            {
                return false;
            }

            IReadOnlyList<GeoPoint3> points = GetFitPoints();
            IReadOnlyList<GeoPoint3> otherPoints = other.GetFitPoints();

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

            return true;
        }

        /// <summary>Gives the data as a line for debugging: its degree and count of points.</summary>
        /// <returns>The data as a line, such as <c>degree 3, 5 fit points</c>.</returns>
        public override string ToString()
            => "degree " + Degree.ToString(CultureInfo.InvariantCulture) + ", " + GetFitPoints().Count.ToString(CultureInfo.InvariantCulture) + " fit points";
    }
}
