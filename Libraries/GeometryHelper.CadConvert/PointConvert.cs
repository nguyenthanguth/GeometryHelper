using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper.PlaneGeometry.Geometry;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Converts points between AutoCAD and GeometryHelper.
    /// </summary>
    public static class PointConvert
    {
        /// <summary>
        /// Converts a AutoCAD 2D point to a SolidGeometry 2D point.
        /// </summary>
        /// <param name="point">The AutoCAD 2D point to convert.</param>
        /// <returns>The converted <see cref="GeoPoint2"/>.</returns>
        public static GeoPoint2 ToGeoPoint2(this Point2d point) => new GeoPoint2(point.X, point.Y);

        /// <summary>
        /// Converts a sequence of AutoCAD 2D points to a list of SolidGeometry 2D points.
        /// </summary>
        /// <param name="points">The sequence of AutoCAD 2D points to convert.</param>
        /// <returns>A list of converted <see cref="GeoPoint2"/>.</returns>
        public static List<GeoPoint2> ToGeoPoint2(this IEnumerable<Point2d> points) => points.Select(ToGeoPoint2).ToList();

        /// <summary>
        /// Converts a AutoCAD 3D point to a SolidGeometry 2D point (discarding the Z coordinate).
        /// </summary>
        /// <param name="point">The AutoCAD 3D point to convert.</param>
        /// <returns>The converted <see cref="GeoPoint2"/>.</returns>
        public static GeoPoint2 ToGeoPoint2(this Point3d point) => new GeoPoint2(point.X, point.Y);

        /// <summary>
        /// Converts a sequence of AutoCAD 3D points to a list of SolidGeometry 2D points (discarding their Z coordinates).
        /// </summary>
        /// <param name="points">The sequence of AutoCAD 3D points to convert.</param>
        /// <returns>A list of converted <see cref="GeoPoint2"/>.</returns>
        public static List<GeoPoint2> ToGeoPoint2(this IEnumerable<Point3d> points) => points.Select(ToGeoPoint2).ToList();

        /// <summary>
        /// Converts a AutoCAD 3D point to a SolidGeometry 3D point.
        /// </summary>
        /// <param name="point">The AutoCAD 3D point to convert.</param>
        /// <returns>The converted <see cref="GeoPoint3"/>.</returns>
        public static GeoPoint3 ToGeoPoint3(this Point3d point) => new GeoPoint3(point.X, point.Y, point.Z);

        /// <summary>
        /// Converts a sequence of AutoCAD 3D points to a list of SolidGeometry 3D points.
        /// </summary>
        /// <param name="points">The sequence of AutoCAD 3D points to convert.</param>
        /// <returns>A list of converted <see cref="GeoPoint3"/>.</returns>
        public static List<GeoPoint3> ToGeoPoint3(this IEnumerable<Point3d> points) => points.Select(ToGeoPoint3).ToList();

        /// <summary>
        /// Converts a SolidGeometry 2D point to a AutoCAD 2D point.
        /// </summary>
        /// <param name="point">The SolidGeometry 2D point to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Point2d"/>.</returns>
        public static Point2d ToAcadPoint2(this GeoPoint2 point) => new Point2d(point.X, point.Y);

        /// <summary>
        /// Converts a sequence of SolidGeometry 2D points to a list of AutoCAD 2D points.
        /// </summary>
        /// <param name="points">The sequence of SolidGeometry 2D points to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Point2d"/>.</returns>
        public static List<Point2d> ToAcadPoint2(this IEnumerable<GeoPoint2> points) => points.Select(ToAcadPoint2).ToList();

        /// <summary>
        /// Converts a SolidGeometry 2D point to a AutoCAD 3D point (with Z = 0).
        /// </summary>
        /// <param name="point">The SolidGeometry 2D point to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Point3d"/>.</returns>
        public static Point3d ToAcadPoint3(this GeoPoint2 point) => new Point3d(point.X, point.Y, 0.0);

        /// <summary>
        /// Converts a sequence of SolidGeometry 2D points to a list of AutoCAD 3D points (with Z = 0).
        /// </summary>
        /// <param name="points">The sequence of SolidGeometry 2D points to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Point3d"/>.</returns>
        public static List<Point3d> ToAcadPoint3(this IEnumerable<GeoPoint2> points) => points.Select(ToAcadPoint3).ToList();

        /// <summary>
        /// Converts a SolidGeometry 3D point to a AutoCAD 3D point.
        /// </summary>
        /// <param name="point">The SolidGeometry 3D point to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Point3d"/>.</returns>
        public static Point3d ToAcadPoint3(this GeoPoint3 point) => new Point3d(point.X, point.Y, point.Z);

        /// <summary>
        /// Converts a sequence of SolidGeometry 3D points to a list of AutoCAD 3D points.
        /// </summary>
        /// <param name="points">The sequence of SolidGeometry 3D points to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Point3d"/>.</returns>
        public static List<Point3d> ToAcadPoint3(this IEnumerable<GeoPoint3> points) => points.Select(ToAcadPoint3).ToList();
    }
}
