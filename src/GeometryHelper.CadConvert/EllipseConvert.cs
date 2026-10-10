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
    /// Converts ellipses between AutoCAD and GeometryHelper, and reads an AutoCAD circle as it is seen in plan.
    /// </summary>
    /// <remarks>
    /// An AutoCAD ellipse is held as a centre, a unit normal, a major axis whose length is the major radius, and the ratio
    /// of the minor radius to the major one. A <see cref="GeoEllipse3"/> holds the same four things, so the reading is
    /// exact both ways. AutoCAD also draws elliptical arcs as <see cref="Ellipse"/> entities, and GeometryHelper has no
    /// elliptical arc, so only an ellipse that sweeps a whole turn is read.
    /// </remarks>
    public static class EllipseConvert
    {
        /// <summary>
        /// Tells whether an AutoCAD ellipse sweeps a whole turn, by the rule <see cref="ArcConvert"/> refuses an arc with:
        /// a sweep within 1E-9 of nothing or of a whole turn is not an arc.
        /// </summary>
        private static bool IsWholeTurn(Ellipse ellipse)
        {
            double sweep = ellipse.EndAngle - ellipse.StartAngle;

            return Math.Abs(sweep) < 1E-9 || Math.Abs(Math.Abs(sweep) - Math.PI * 2.0) < 1E-9;
        }

        /// <summary>
        /// Converts an AutoCAD Ellipse to a GeometryHelper ellipse in space, keeping the plane it lies in.
        /// </summary>
        /// <param name="ellipse">The AutoCAD Ellipse to convert.</param>
        /// <returns>The converted <see cref="GeoEllipse3"/>, with AutoCAD's centre, normal and major axis.</returns>
        /// <remarks>
        /// The major radius is the length of AutoCAD's major axis and the minor radius that times the radius ratio, so an
        /// ellipse with the major axis (300, 0, 0) and a radius ratio of 0.5 is the ellipse 300 by 150 along X.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ellipse"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the ellipse is an elliptical arc, sweeping less than a whole turn.</exception>
        public static GeoEllipse3 ToGeoEllipse3(this Ellipse ellipse)
        {
            if (ellipse == null) throw new ArgumentNullException(nameof(ellipse));

            if (!IsWholeTurn(ellipse))
            {
                throw new ArgumentException(
                    "This AutoCAD ellipse is an elliptical arc, sweeping less than a whole turn, and GeometryHelper has no elliptical arc; only a whole ellipse is read.",
                    nameof(ellipse));
            }

            GeoVector3 majorAxis = ellipse.MajorAxis.ToGeoVector3();
            double majorRadius = majorAxis.Length;

            return new GeoEllipse3(ellipse.Center.ToGeoPoint3(), ellipse.Normal.ToGeoVector3(), majorAxis, majorRadius, majorRadius * ellipse.RadiusRatio);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Ellipses to a list of GeometryHelper ellipses in space.
        /// </summary>
        /// <param name="ellipses">The sequence of AutoCAD Ellipses to convert.</param>
        /// <returns>A list of converted <see cref="GeoEllipse3"/>.</returns>
        public static List<GeoEllipse3> ToGeoEllipse3(this IEnumerable<Ellipse> ellipses) => ellipses.Select(ToGeoEllipse3).ToList();

        /// <summary>
        /// Converts an AutoCAD Ellipse to a GeometryHelper 2D ellipse, as it is seen in plan.
        /// </summary>
        /// <param name="ellipse">The AutoCAD Ellipse to convert.</param>
        /// <returns>The ellipse seen square onto the XY plane, as <see cref="GeoEllipse3.ProjectToEllipse2(GeoCoordinateSystem3)"/> lays it out in <see cref="GeoCoordinateSystem3.Global"/>.</returns>
        /// <remarks>
        /// An ellipse lying in the plan is read as it is drawn. A tilted one is the ellipse its outline makes in plan: an
        /// ellipse 300 by 200 tilted 60° about its major axis is, in plan, an ellipse 300 by 100.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ellipse"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the ellipse is an elliptical arc, sweeping less than a whole turn.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the ellipse stands square to the XY plane, where in plan it would flatten into a segment rather than an ellipse.</exception>
        public static GeoEllipse2 ToGeoEllipse2(this Ellipse ellipse) => ellipse.ToGeoEllipse3().ProjectToEllipse2(GeoCoordinateSystem3.Global);

        /// <summary>
        /// Converts a sequence of AutoCAD Ellipses to a list of GeometryHelper 2D ellipses, as they are seen in plan.
        /// </summary>
        /// <param name="ellipses">The sequence of AutoCAD Ellipses to convert.</param>
        /// <returns>A list of converted <see cref="GeoEllipse2"/>.</returns>
        public static List<GeoEllipse2> ToGeoEllipse2(this IEnumerable<Ellipse> ellipses) => ellipses.Select(ToGeoEllipse2).ToList();

        /// <summary>
        /// Tries to convert an AutoCAD Ellipse to a GeometryHelper 2D ellipse, as it is seen in plan.
        /// </summary>
        /// <param name="ellipse">The AutoCAD Ellipse to convert.</param>
        /// <param name="result">The converted <see cref="GeoEllipse2"/>, or the default when it could not be read in plan.</param>
        /// <returns>true when the ellipse sweeps a whole turn and does not stand square to the XY plane; false otherwise.</returns>
        public static bool TryToGeoEllipse2(this Ellipse ellipse, out GeoEllipse2 result)
        {
            result = default(GeoEllipse2);

            if (ellipse == null || !IsWholeTurn(ellipse))
            {
                return false;
            }

            try
            {
                result = ellipse.ToGeoEllipse2();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Converts an AutoCAD Circle to a GeometryHelper 2D ellipse, as it is seen in plan.
        /// </summary>
        /// <param name="circle">The AutoCAD Circle to convert.</param>
        /// <returns>The circle seen square onto the XY plane, as <see cref="GeoCircle3.ProjectToEllipse2(GeoCoordinateSystem3)"/> lays it out in <see cref="GeoCoordinateSystem3.Global"/>.</returns>
        /// <remarks>
        /// A circle lying in the plan is read as an ellipse whose two radii are the radius. A tilted one is the ellipse its
        /// outline makes in plan, where <see cref="PolylineConvert.ToGeoCircle2(Circle)"/> keeps only the centre and the
        /// radius: a circle of radius 300 tilted 60° from the plan is, in plan, an ellipse 300 by 150.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="circle"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the circle stands square to the XY plane, where in plan it would flatten into a segment rather than an ellipse.</exception>
        public static GeoEllipse2 ToGeoEllipse2(this Circle circle) => circle.ToGeoCircle3().ProjectToEllipse2(GeoCoordinateSystem3.Global);

        /// <summary>
        /// Converts a sequence of AutoCAD Circles to a list of GeometryHelper 2D ellipses, as they are seen in plan.
        /// </summary>
        /// <param name="circles">The sequence of AutoCAD Circles to convert.</param>
        /// <returns>A list of converted <see cref="GeoEllipse2"/>.</returns>
        public static List<GeoEllipse2> ToGeoEllipse2(this IEnumerable<Circle> circles) => circles.Select(ToGeoEllipse2).ToList();

        /// <summary>
        /// Tries to convert an AutoCAD Circle to a GeometryHelper 2D ellipse, as it is seen in plan.
        /// </summary>
        /// <param name="circle">The AutoCAD Circle to convert.</param>
        /// <param name="result">The converted <see cref="GeoEllipse2"/>, or the default when it could not be read in plan.</param>
        /// <returns>true when the circle does not stand square to the XY plane; false otherwise.</returns>
        public static bool TryToGeoEllipse2(this Circle circle, out GeoEllipse2 result)
        {
            result = default(GeoEllipse2);

            if (circle == null)
            {
                return false;
            }

            try
            {
                result = circle.ToGeoEllipse2();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Converts a GeometryHelper ellipse in space to an AutoCAD Ellipse in its own plane.
        /// </summary>
        /// <param name="geoEllipse">The GeometryHelper ellipse to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Ellipse"/>, sweeping a whole turn from 0 to 2π.</returns>
        /// <remarks>
        /// The major axis is written as long as the major radius, and the radius ratio is the minor radius over the major
        /// one, so the ellipse 300 by 150 is written with a major axis 300 long and a radius ratio of 0.5.
        /// </remarks>
        public static Ellipse ToAcadEllipse(this GeoEllipse3 geoEllipse)
        {
            return new Ellipse(
                geoEllipse.Center.ToAcadPoint3(),
                geoEllipse.Normal.ToAcadVector3(),
                geoEllipse.MajorAxis.Multiply(geoEllipse.MajorRadius).ToAcadVector3(),
                geoEllipse.MinorRadius / geoEllipse.MajorRadius,
                0.0,
                Math.PI * 2.0);
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper ellipses in space to a list of AutoCAD Ellipses.
        /// </summary>
        /// <param name="ellipses">The sequence of GeometryHelper ellipses to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Ellipse"/>.</returns>
        public static List<Ellipse> ToAcadEllipse(this IEnumerable<GeoEllipse3> ellipses) => ellipses.Select(ToAcadEllipse).ToList();

        /// <summary>
        /// Converts a GeometryHelper 2D ellipse to an AutoCAD Ellipse in the XY plane.
        /// </summary>
        /// <param name="geoEllipse">The GeometryHelper 2D ellipse to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Ellipse"/>, its normal along Z, sweeping a whole turn from 0 to 2π.</returns>
        /// <remarks>
        /// The ellipse is lifted into the XY plane as <see cref="GeoEllipse2.ToEllipse3(GeoCoordinateSystem3)"/> lifts it into
        /// <see cref="GeoCoordinateSystem3.Global"/>, so an ellipse 300 by 150 centred at (100, 200) is written centred at
        /// (100, 200, 0) with a major axis 300 long and a radius ratio of 0.5.
        /// </remarks>
        public static Ellipse ToAcadEllipse(this GeoEllipse2 geoEllipse) => geoEllipse.ToEllipse3(GeoCoordinateSystem3.Global).ToAcadEllipse();

        /// <summary>
        /// Converts a sequence of GeometryHelper 2D ellipses to a list of AutoCAD Ellipses.
        /// </summary>
        /// <param name="ellipses">The sequence of GeometryHelper 2D ellipses to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Ellipse"/>.</returns>
        public static List<Ellipse> ToAcadEllipse(this IEnumerable<GeoEllipse2> ellipses) => ellipses.Select(ToAcadEllipse).ToList();
    }
}
