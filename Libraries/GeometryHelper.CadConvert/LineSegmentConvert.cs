using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper.PlaneGeometry.Geometry;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Converts line segments and lines between AutoCAD and GeometryHelper.
    /// </summary>
    public static class LineSegmentConvert
    {
        /// <summary>
        /// Converts a AutoCAD 2D line segment to a SolidGeometry 2D line.
        /// </summary>
        /// <param name="lineSegment">The AutoCAD 2D line segment to convert.</param>
        /// <returns>The converted <see cref="GeoLine2"/>.</returns>
        public static GeoLine2 ToGeoLine2(this LineSegment2d lineSegment)
        {
            return new GeoLine2(lineSegment.StartPoint.ToGeoPoint2(), lineSegment.EndPoint.ToGeoPoint2());
        }

        /// <summary>
        /// Converts a sequence of AutoCAD 2D line segments to a list of SolidGeometry 2D lines.
        /// </summary>
        /// <param name="segments">The sequence of AutoCAD 2D line segments to convert.</param>
        /// <returns>A list of converted <see cref="GeoLine2"/>.</returns>
        public static List<GeoLine2> ToGeoLine2(this IEnumerable<LineSegment2d> segments) => segments.Select(ToGeoLine2).ToList();

        /// <summary>
        /// Converts a AutoCAD 3D line segment to a SolidGeometry 2D line (discarding the Z coordinate).
        /// </summary>
        /// <param name="lineSegment">The AutoCAD 3D line segment to convert.</param>
        /// <returns>The converted <see cref="GeoLine2"/>.</returns>
        public static GeoLine2 ToGeoLine2(this LineSegment3d lineSegment)
        {
            return new GeoLine2(lineSegment.StartPoint.ToGeoPoint2(), lineSegment.EndPoint.ToGeoPoint2());
        }

        /// <summary>
        /// Converts a sequence of AutoCAD 3D line segments to a list of SolidGeometry 2D lines (discarding their Z coordinates).
        /// </summary>
        /// <param name="segments">The sequence of AutoCAD 3D line segments to convert.</param>
        /// <returns>A list of converted <see cref="GeoLine2"/>.</returns>
        public static List<GeoLine2> ToGeoLine2(this IEnumerable<LineSegment3d> segments) => segments.Select(ToGeoLine2).ToList();

        /// <summary>
        /// Converts a AutoCAD Database Line entity to a SolidGeometry 2D line (discarding the Z coordinate).
        /// </summary>
        /// <param name="line">The AutoCAD Line entity to convert.</param>
        /// <returns>The converted <see cref="GeoLine2"/>.</returns>
        public static GeoLine2 ToGeoLine2(this Line line)
        {
            return new GeoLine2(line.StartPoint.ToGeoPoint2(), line.EndPoint.ToGeoPoint2());
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Line entities to a list of SolidGeometry 2D lines.
        /// </summary>
        /// <param name="lines">The sequence of AutoCAD Line entities to convert.</param>
        /// <returns>A list of converted <see cref="GeoLine2"/>.</returns>
        public static List<GeoLine2> ToGeoLine2(this IEnumerable<Line> lines) => lines.Select(ToGeoLine2).ToList();

        /// <summary>
        /// Converts a SolidGeometry 2D line to a AutoCAD Database Line entity (with Z = 0).
        /// </summary>
        /// <param name="geoLine">The SolidGeometry 2D line to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Line"/>.</returns>
        public static Line ToAcadLine(this GeoLine2 geoLine)
        {
            if (geoLine == null) throw new ArgumentNullException(nameof(geoLine));
            return new Line(new Point3d(geoLine.StartPoint.X, geoLine.StartPoint.Y, 0.0), new Point3d(geoLine.EndPoint.X, geoLine.EndPoint.Y, 0.0));
        }

        /// <summary>
        /// Converts a sequence of SolidGeometry 2D lines to a list of AutoCAD Line entities.
        /// </summary>
        /// <param name="lines">The sequence of SolidGeometry 2D lines to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Line"/>.</returns>
        public static List<Line> ToAcadLine(this IEnumerable<GeoLine2> lines) => lines.Select(ToAcadLine).ToList();
    }
}
