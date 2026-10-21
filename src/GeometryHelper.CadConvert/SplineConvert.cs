using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Converts splines between AutoCAD and GeometryHelper.
    /// </summary>
    /// <remarks>
    /// An AutoCAD <see cref="Spline"/> holds a NURBS curve, read through its <see cref="Spline.NurbsData"/>: a degree, control
    /// points, knots and, where it is rational, weights. A <see cref="GeoSpline3"/> holds the same four things and keeps them as
    /// they are given, so the reading is exact both ways, every number to the bit. A spline AutoCAD made from fit points is read by
    /// the control points AutoCAD keeps for it; the fit points themselves are not carried. GeometryHelper's splines are clamped,
    /// starting at their first control point and ending at their last, so a periodic spline, which is how AutoCAD closes a spline
    /// smoothly, is refused.
    /// </remarks>
    public static class SplineConvert
    {
        /// <summary>The control point and knot tolerances a spline is written to AutoCAD with, as a DXF file of DxfWriter writes them.</summary>
        private const double Fitting = 1E-10;

        /// <summary>
        /// Converts an AutoCAD Spline to a GeometryHelper spline in space.
        /// </summary>
        /// <param name="spline">The AutoCAD Spline to convert.</param>
        /// <returns>The converted <see cref="GeoSpline3"/>, with AutoCAD's degree, control points, knots and, where it is rational, weights.</returns>
        /// <remarks>
        /// A cubic of AutoCAD's with the control points (0, 0, 0), (100, 150, 0), (200, 150, 0), (300, 0, 0) and the knots
        /// 0, 0, 0, 0, 1, 1, 1, 1 is the same cubic, rising to 112.5 at (150, 112.5, 0). Weights are read only for a rational spline;
        /// a spline that is not has them all one.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="spline"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the spline is periodic, or its numbers do not make a clamped spline GeometryHelper takes.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when its degree is above 10 or a weight is not above nought.</exception>
        public static GeoSpline3 ToGeoSpline3(this Spline spline)
        {
            if (spline == null) throw new ArgumentNullException(nameof(spline));

            NurbsData data = spline.NurbsData;

            if (data.Periodic)
            {
                throw new ArgumentException(
                    "This AutoCAD spline is periodic, closed smoothly with knots that run on past its ends, and GeometryHelper's splines are clamped; only a clamped spline is read.",
                    nameof(spline));
            }

            List<GeoPoint3> points = data.GetControlPoints().Cast<Point3d>().Select(p => p.ToGeoPoint3()).ToList();
            List<double> knots = data.GetKnots().Cast<double>().ToList();
            List<double> weights = data.GetWeights().Cast<double>().ToList();

            return new GeoSpline3(data.Degree, points, knots, data.Rational && weights.Count > 0 ? weights : null);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Splines to a list of GeometryHelper splines in space.
        /// </summary>
        /// <param name="splines">The sequence of AutoCAD Splines to convert.</param>
        /// <returns>A list of converted <see cref="GeoSpline3"/>.</returns>
        public static List<GeoSpline3> ToGeoSpline3(this IEnumerable<Spline> splines) => splines.Select(ToGeoSpline3).ToList();

        /// <summary>
        /// Converts an AutoCAD Spline to a GeometryHelper spline in the plane, as it is seen in plan.
        /// </summary>
        /// <param name="spline">The AutoCAD Spline to convert.</param>
        /// <returns>The spline seen square onto the XY plane, as <see cref="GeoSpline3.ProjectToSpline2(GeoCoordinateSystem3)"/> lays it out in <see cref="GeoCoordinateSystem3.Global"/>.</returns>
        /// <remarks>
        /// A spline in the plan is read as it is drawn. A tilted one, or one that bends out of a plane, is the spline its outline
        /// makes in plan, which is exact: a NURBS seen square onto a plane is the NURBS of its control points seen so, with the same
        /// knots and weights, so a cubic rising from (0, 0, 0) to (300, 0, 200) is the cubic along X from (0, 0) to (300, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="spline"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the spline is periodic, or its numbers do not make a clamped spline GeometryHelper takes.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when its degree is above 10 or a weight is not above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline stands square to the XY plane, where in plan every control point lands on one point.</exception>
        public static GeoSpline2 ToGeoSpline2(this Spline spline) => spline.ToGeoSpline3().ProjectToSpline2(GeoCoordinateSystem3.Global);

        /// <summary>
        /// Converts a sequence of AutoCAD Splines to a list of GeometryHelper splines in the plane, as they are seen in plan.
        /// </summary>
        /// <param name="splines">The sequence of AutoCAD Splines to convert.</param>
        /// <returns>A list of converted <see cref="GeoSpline2"/>.</returns>
        public static List<GeoSpline2> ToGeoSpline2(this IEnumerable<Spline> splines) => splines.Select(ToGeoSpline2).ToList();

        /// <summary>
        /// Tries to convert an AutoCAD Spline to a GeometryHelper spline in the plane, as it is seen in plan.
        /// </summary>
        /// <param name="spline">The AutoCAD Spline to convert.</param>
        /// <param name="result">The converted <see cref="GeoSpline2"/>, or null when it could not be read.</param>
        /// <returns>true when the spline is clamped, GeometryHelper takes its numbers, and it does not stand square to the XY plane; false otherwise.</returns>
        public static bool TryToGeoSpline2(this Spline spline, out GeoSpline2 result)
        {
            result = null;

            if (spline == null)
            {
                return false;
            }

            try
            {
                result = spline.ToGeoSpline2();
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Converts a GeometryHelper spline in space to an AutoCAD Spline.
        /// </summary>
        /// <param name="geoSpline">The GeometryHelper spline to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Spline"/>, of the spline's degree, control points, knots and, where it is rational, weights, not periodic.</returns>
        /// <remarks>
        /// The numbers go over as they are, nothing refitted, so the circle of radius 100 that <see cref="GeoSpline3.FromCircle(GeoCircle3)"/>
        /// builds is a rational Spline of degree 2 with its 9 control points, 9 weights and 12 knots. It is told it is closed only where its first
        /// and last control points are the same point exactly, as that circle's are; ends only near each other do not make it closed. The
        /// control point and knot tolerances are 1E-10, as a DXF file of DxfWriter has them.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="geoSpline"/> is null.</exception>
        public static Spline ToAcadSpline(this GeoSpline3 geoSpline)
        {
            if (geoSpline == null) throw new ArgumentNullException(nameof(geoSpline));

            var points = new Point3dCollection();

            foreach (GeoPoint3 point in geoSpline.ControlPoints)
            {
                points.Add(point.ToAcadPoint3());
            }

            return Make(geoSpline.Degree, geoSpline.IsRational, geoSpline.StartPoint.Equals(geoSpline.EndPoint), points, geoSpline.Knots, geoSpline.Weights);
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper splines in space to a list of AutoCAD Splines.
        /// </summary>
        /// <param name="splines">The sequence of GeometryHelper splines to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Spline"/>.</returns>
        public static List<Spline> ToAcadSpline(this IEnumerable<GeoSpline3> splines) => splines.Select(ToAcadSpline).ToList();

        /// <summary>
        /// Converts a GeometryHelper spline in the plane to an AutoCAD Spline in the XY plane.
        /// </summary>
        /// <param name="geoSpline">The GeometryHelper spline to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Spline"/>, every control point at z = 0, as <see cref="ToAcadSpline(GeoSpline3)"/> writes one in space.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="geoSpline"/> is null.</exception>
        public static Spline ToAcadSpline(this GeoSpline2 geoSpline)
        {
            if (geoSpline == null) throw new ArgumentNullException(nameof(geoSpline));

            var points = new Point3dCollection();

            foreach (GeoPoint2 point in geoSpline.ControlPoints)
            {
                points.Add(point.ToAcadPoint3());
            }

            return Make(geoSpline.Degree, geoSpline.IsRational, geoSpline.StartPoint.Equals(geoSpline.EndPoint), points, geoSpline.Knots, geoSpline.Weights);
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper splines in the plane to a list of AutoCAD Splines.
        /// </summary>
        /// <param name="splines">The sequence of GeometryHelper splines to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Spline"/>.</returns>
        public static List<Spline> ToAcadSpline(this IEnumerable<GeoSpline2> splines) => splines.Select(ToAcadSpline).ToList();

        private static Spline Make(int degree, bool rational, bool closed, Point3dCollection points, IReadOnlyList<double> knots, IReadOnlyList<double> weights)
        {
            var knotValues = new DoubleCollection();

            foreach (double knot in knots)
            {
                knotValues.Add(knot);
            }

            // A spline that is not rational is given no weights, as AutoCAD gives none for one.
            var weightValues = new DoubleCollection();

            if (rational)
            {
                foreach (double weight in weights)
                {
                    weightValues.Add(weight);
                }
            }

            return new Spline(degree, rational, closed, false, points, knotValues, weightValues, Fitting, Fitting);
        }
    }
}
