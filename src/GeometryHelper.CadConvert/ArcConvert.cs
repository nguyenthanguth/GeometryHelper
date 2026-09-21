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
    /// Converts arcs and circles between AutoCAD and GeometryHelper.
    /// </summary>
    /// <remarks>
    /// An AutoCAD arc is held in the plane its normal defines and always sweeps counter-clockwise about
    /// that normal, so which way round it goes is carried by the normal rather than by the angles. The
    /// conversions here read it through its own start, middle and end points, which are in world
    /// coordinates whatever the normal is, so an arc drawn on a flipped plane comes back the way it looks.
    /// </remarks>
    public static class ArcConvert
    {
        /// <summary>
        /// Gets the point halfway along an AutoCAD arc, in world coordinates.
        /// </summary>
        private static GeoPoint3 GetMiddlePoint(Arc arc)
        {
            GeoPoint3 center = arc.Center.ToGeoPoint3();
            GeoVector3 normal = arc.Normal.ToGeoVector3();
            GeoVector3 toStart = center.GetVectorTo(arc.StartPoint.ToGeoPoint3());

            return center.Add(toStart.RotateBy(arc.TotalAngle * 0.5, normal));
        }

        /// <summary>
        /// Refuses an arc that closes on itself, which has no three points to be read through.
        /// </summary>
        private static void RequireOpenSweep(Arc arc)
        {
            if (Math.Abs(arc.TotalAngle) < 1E-9 || Math.Abs(Math.Abs(arc.TotalAngle) - Math.PI * 2.0) < 1E-9)
            {
                throw new ArgumentException(
                    "This AutoCAD arc sweeps nothing or a whole turn; a whole turn is a Circle, and ToGeoCircle2 or ToGeoCircle3 reads it.",
                    nameof(arc));
            }
        }

        /// <summary>
        /// Converts an AutoCAD Arc to a GeometryHelper 2D arc, as it is seen in plan.
        /// </summary>
        /// <param name="arc">The AutoCAD Arc to convert.</param>
        /// <returns>The converted <see cref="GeoArc2"/>, running the way the arc is drawn.</returns>
        /// <remarks>
        /// The arc has to lie in a plane parallel to XY, because a tilted one is an ellipse in plan and this
        /// gives an arc rather than an approximation of one. <see cref="ToGeoArc3(Arc)"/> reads any arc.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="arc"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the arc is tilted out of the XY plane, or sweeps a whole turn.</exception>
        public static GeoArc2 ToGeoArc2(this Arc arc)
        {
            if (arc == null) throw new ArgumentNullException(nameof(arc));

            RequireOpenSweep(arc);

            if (Math.Abs(arc.Normal.X) > 1E-9 || Math.Abs(arc.Normal.Y) > 1E-9)
            {
                throw new ArgumentException(
                    "This AutoCAD arc is tilted out of the XY plane, so in plan it is an ellipse; ToGeoArc3 reads it as it is.",
                    nameof(arc));
            }

            GeoPoint3 middle = GetMiddlePoint(arc);

            return GeoArc2.FromThreePoints(
                arc.StartPoint.ToGeoPoint2(),
                new GeoPoint2(middle.X, middle.Y),
                arc.EndPoint.ToGeoPoint2());
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Arcs to a list of GeometryHelper 2D arcs.
        /// </summary>
        /// <param name="arcs">The sequence of AutoCAD Arcs to convert.</param>
        /// <returns>A list of converted <see cref="GeoArc2"/>.</returns>
        public static List<GeoArc2> ToGeoArc2(this IEnumerable<Arc> arcs) => arcs.Select(ToGeoArc2).ToList();

        /// <summary>
        /// Tries to convert an AutoCAD Arc to a GeometryHelper 2D arc.
        /// </summary>
        /// <param name="arc">The AutoCAD Arc to convert.</param>
        /// <param name="result">The converted <see cref="GeoArc2"/>, or the default when it could not be read in plan.</param>
        /// <returns>true when the arc lies in a plane parallel to XY and sweeps less than a whole turn; false otherwise.</returns>
        public static bool TryToGeoArc2(this Arc arc, out GeoArc2 result)
        {
            result = default(GeoArc2);

            if (arc == null || Math.Abs(arc.Normal.X) > 1E-9 || Math.Abs(arc.Normal.Y) > 1E-9)
            {
                return false;
            }

            if (Math.Abs(arc.TotalAngle) < 1E-9 || Math.Abs(Math.Abs(arc.TotalAngle) - Math.PI * 2.0) < 1E-9)
            {
                return false;
            }

            result = arc.ToGeoArc2();
            return true;
        }

        /// <summary>
        /// Converts an AutoCAD Arc to a GeometryHelper 3D arc, whatever plane it lies in.
        /// </summary>
        /// <param name="arc">The AutoCAD Arc to convert.</param>
        /// <returns>The converted <see cref="GeoArc3"/>, running the way the arc is drawn.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="arc"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the arc sweeps nothing or a whole turn.</exception>
        public static GeoArc3 ToGeoArc3(this Arc arc)
        {
            if (arc == null) throw new ArgumentNullException(nameof(arc));

            RequireOpenSweep(arc);

            return GeoArc3.FromThreePoints(
                arc.StartPoint.ToGeoPoint3(),
                GetMiddlePoint(arc),
                arc.EndPoint.ToGeoPoint3());
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Arcs to a list of GeometryHelper 3D arcs.
        /// </summary>
        /// <param name="arcs">The sequence of AutoCAD Arcs to convert.</param>
        /// <returns>A list of converted <see cref="GeoArc3"/>.</returns>
        public static List<GeoArc3> ToGeoArc3(this IEnumerable<Arc> arcs) => arcs.Select(ToGeoArc3).ToList();

        /// <summary>
        /// Converts a GeometryHelper 2D arc to an AutoCAD Arc in the XY plane.
        /// </summary>
        /// <param name="geoArc">The GeometryHelper 2D arc to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Arc"/>, drawing the same curve.</returns>
        /// <remarks>
        /// An AutoCAD arc sweeps counter-clockwise about its normal and has nowhere else to put a direction,
        /// so a clockwise arc is written with its two angles exchanged. It draws the same curve, and reading
        /// it back gives the counter-clockwise arc through the same points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="geoArc"/> is null.</exception>
        public static Arc ToAcadArc(this GeoArc2 geoArc)
        {
            var center = new Point3d(geoArc.Center.X, geoArc.Center.Y, 0.0);

            return geoArc.IsClockwise
                ? new Arc(center, Vector3d.ZAxis, geoArc.Radius, geoArc.EndAngle, geoArc.StartAngle)
                : new Arc(center, Vector3d.ZAxis, geoArc.Radius, geoArc.StartAngle, geoArc.EndAngle);
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper 2D arcs to a list of AutoCAD Arcs.
        /// </summary>
        /// <param name="arcs">The sequence of GeometryHelper 2D arcs to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Arc"/>.</returns>
        public static List<Arc> ToAcadArc(this IEnumerable<GeoArc2> arcs) => arcs.Select(ToAcadArc).ToList();

        /// <summary>
        /// Converts a GeometryHelper 3D arc to an AutoCAD Arc in its own plane.
        /// </summary>
        /// <param name="geoArc">The GeometryHelper 3D arc to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Arc"/>, drawing the same curve.</returns>
        /// <remarks>
        /// The arc is written through three of its own points rather than through its angles, because
        /// AutoCAD measures them from an axis of its own choosing rather than from the one
        /// <see cref="GeoPlane3.GetAxes"/> gives.
        /// </remarks>
        public static Arc ToAcadArc(this GeoArc3 geoArc)
        {
            var plane = new Plane(geoArc.Center.ToAcadPoint3(), geoArc.Normal.ToAcadVector3());

            // ParameterOf gives the point in the plane's own axes, which AutoCAD picks the same way for
            // an arc of that normal, so the angle between them is the one the entity wants.
            Point2d atStart = plane.ParameterOf(geoArc.StartPoint.ToAcadPoint3());
            Point2d atEnd = plane.ParameterOf(geoArc.EndPoint.ToAcadPoint3());

            double start = Math.Atan2(atStart.Y, atStart.X);
            double end = Math.Atan2(atEnd.Y, atEnd.X);

            return geoArc.IsClockwise
                ? new Arc(geoArc.Center.ToAcadPoint3(), geoArc.Normal.ToAcadVector3(), geoArc.Radius, end, start)
                : new Arc(geoArc.Center.ToAcadPoint3(), geoArc.Normal.ToAcadVector3(), geoArc.Radius, start, end);
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper 3D arcs to a list of AutoCAD Arcs.
        /// </summary>
        /// <param name="arcs">The sequence of GeometryHelper 3D arcs to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Arc"/>.</returns>
        public static List<Arc> ToAcadArc(this IEnumerable<GeoArc3> arcs) => arcs.Select(ToAcadArc).ToList();

        /// <summary>
        /// Converts an AutoCAD Circle to a GeometryHelper 3D circle, keeping the plane it lies in.
        /// </summary>
        /// <param name="circle">The AutoCAD Circle to convert.</param>
        /// <returns>The converted <see cref="GeoCircle3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="circle"/> is null.</exception>
        public static GeoCircle3 ToGeoCircle3(this Circle circle)
        {
            if (circle == null) throw new ArgumentNullException(nameof(circle));

            return new GeoCircle3(circle.Center.ToGeoPoint3(), circle.Normal.ToGeoVector3(), circle.Radius);
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Circles to a list of GeometryHelper 3D circles.
        /// </summary>
        /// <param name="circles">The sequence of AutoCAD Circles to convert.</param>
        /// <returns>A list of converted <see cref="GeoCircle3"/>.</returns>
        public static List<GeoCircle3> ToGeoCircle3(this IEnumerable<Circle> circles) => circles.Select(ToGeoCircle3).ToList();

        /// <summary>
        /// Converts a GeometryHelper 2D circle to an AutoCAD Circle in the XY plane.
        /// </summary>
        /// <param name="geoCircle">The GeometryHelper 2D circle to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Circle"/>.</returns>
        public static Circle ToAcadCircle(this GeoCircle2 geoCircle)
        {
            return new Circle(new Point3d(geoCircle.Center.X, geoCircle.Center.Y, 0.0), Vector3d.ZAxis, geoCircle.Radius);
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper 2D circles to a list of AutoCAD Circles.
        /// </summary>
        /// <param name="circles">The sequence of GeometryHelper 2D circles to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Circle"/>.</returns>
        public static List<Circle> ToAcadCircle(this IEnumerable<GeoCircle2> circles) => circles.Select(ToAcadCircle).ToList();

        /// <summary>
        /// Converts a GeometryHelper 3D circle to an AutoCAD Circle in its own plane.
        /// </summary>
        /// <param name="geoCircle">The GeometryHelper 3D circle to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Circle"/>.</returns>
        public static Circle ToAcadCircle(this GeoCircle3 geoCircle)
        {
            return new Circle(geoCircle.Center.ToAcadPoint3(), geoCircle.Normal.ToAcadVector3(), geoCircle.Radius);
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper 3D circles to a list of AutoCAD Circles.
        /// </summary>
        /// <param name="circles">The sequence of GeometryHelper 3D circles to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Circle"/>.</returns>
        public static List<Circle> ToAcadCircle(this IEnumerable<GeoCircle3> circles) => circles.Select(ToAcadCircle).ToList();
    }
}
