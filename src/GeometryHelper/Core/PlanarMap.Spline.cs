using System;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Carries splines between the plane and space.
    /// </summary>
    /// <remarks>
    /// A spline is carried by carrying its control points, which is exact: the laying of the plane into space and the
    /// projection of space onto the plane are both affine, and an affine map takes a NURBS to the NURBS of the carried
    /// control points with the same weights and knots. A spline lying in the plane of the frame comes back from the plane
    /// as it went in, to rounding.
    /// </remarks>
    public static partial class PlanarMap
    {
        #region Splines

        /// <summary>
        /// Lays a spline in space out in a frame, projecting it square onto the plane of that frame.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="spline">The spline in space.</param>
        /// <returns>The spline in the plane of the frame, with the same degree, weights and knots; each control point is laid out with its distance from the plane dropped.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the spline is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the spline stands square to the plane, so that every control point lands on one point.</exception>
        public static GeoSpline2 ProjectToSpline2(GeoCoordinateSystem3 frame, GeoSpline3 spline)
        {
            if (spline == null) throw new ArgumentNullException(nameof(spline));

            var flat = new double[spline.ControlPointCount * 2];

            for (int i = 0; i < spline.ControlPointCount; i++)
            {
                GeoPoint2 point = ProjectToPoint2(frame, spline.ControlPoints[i]);
                flat[2 * i] = point.X;
                flat[2 * i + 1] = point.Y;
            }

            if (NurbsCurve.AllTheSame(flat, 2))
            {
                throw new InvalidOperationException("The spline stands square to the plane, and would flatten onto one point.");
            }

            return new GeoSpline2(new NurbsCurve(2, spline.Degree, flat, spline.Curve.Knots, spline.Curve.Rational ? spline.Curve.Weights : null));
        }

        /// <summary>
        /// Puts a spline of the plane back into space, exactly: each control point carried by the frame, the degree, weights
        /// and knots as they were.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <param name="spline">The spline in the plane.</param>
        /// <returns>The spline in space, on the plane of the frame.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the spline is null.</exception>
        public static GeoSpline3 ToSpline3(GeoCoordinateSystem3 frame, GeoSpline2 spline)
        {
            if (spline == null) throw new ArgumentNullException(nameof(spline));

            var lifted = new double[spline.ControlPointCount * 3];

            for (int i = 0; i < spline.ControlPointCount; i++)
            {
                GeoPoint3 point = ToPoint3(frame, spline.ControlPoints[i]);
                lifted[3 * i] = point.X;
                lifted[3 * i + 1] = point.Y;
                lifted[3 * i + 2] = point.Z;
            }

            return new GeoSpline3(new NurbsCurve(3, spline.Degree, lifted, spline.Curve.Knots, spline.Curve.Rational ? spline.Curve.Weights : null));
        }

        #endregion
    }
}
