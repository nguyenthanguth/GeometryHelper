using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using GeometryHelper.Geometry;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Converts infinite lines, the AutoCAD Xline entities, between AutoCAD and GeometryHelper.
    /// </summary>
    /// <remarks>
    /// An AutoCAD <see cref="Xline"/> is held as a base point and a unit direction, and runs for ever both ways. A
    /// <see cref="GeoXLine3"/> holds the same two things, so the reading keeps both; the direction is made unit length again on the way in,
    /// so a round trip can differ in the last digit of it. The line is in space: a line in the
    /// plane is <see cref="GeoXLine3.ProjectToXLine2(GeoCoordinateSystem3)"/> of it.
    /// </remarks>
    public static class XlineConvert
    {
        /// <summary>
        /// Converts an AutoCAD Xline to a GeometryHelper line without ends in space.
        /// </summary>
        /// <param name="xline">The AutoCAD Xline to convert.</param>
        /// <returns>The converted <see cref="GeoXLine3"/>, through AutoCAD's base point along its unit direction.</returns>
        /// <remarks>
        /// An Xline through (100, 200, 0) along X is the line through (100, 200, 0) along (1, 0, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="xline"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the Xline has a unit direction of no length.</exception>
        public static GeoXLine3 ToGeoXLine3(this Xline xline)
        {
            if (xline == null) throw new ArgumentNullException(nameof(xline));
            return new GeoXLine3(xline.BasePoint.ToGeoPoint3(), xline.UnitDir.ToGeoVector3());
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Xlines to a list of GeometryHelper lines without ends in space.
        /// </summary>
        /// <param name="xlines">The sequence of AutoCAD Xlines to convert.</param>
        /// <returns>A list of converted <see cref="GeoXLine3"/>.</returns>
        public static List<GeoXLine3> ToGeoXLine3(this IEnumerable<Xline> xlines) => xlines.Select(ToGeoXLine3).ToList();

        /// <summary>
        /// Converts a GeometryHelper line without ends in space to an AutoCAD Xline.
        /// </summary>
        /// <param name="geoXLine">The GeometryHelper line to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Xline"/>, its base point the line's origin and its unit direction the line's.</returns>
        /// <exception cref="ArgumentException">Thrown when the line has no direction, as a <c>default</c> one has, or holds a number that is not finite.</exception>
        public static Xline ToAcadXline(this GeoXLine3 geoXLine)
        {
            if (!geoXLine.IsValid) throw new ArgumentException("A line with no direction, or a number that is not finite, has no Xline.", nameof(geoXLine));
            return new Xline { BasePoint = geoXLine.Origin.ToAcadPoint3(), UnitDir = geoXLine.Direction.ToAcadVector3() };
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper lines without ends in space to a list of AutoCAD Xlines.
        /// </summary>
        /// <param name="xlines">The sequence of GeometryHelper lines to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Xline"/>.</returns>
        public static List<Xline> ToAcadXline(this IEnumerable<GeoXLine3> xlines) => xlines.Select(ToAcadXline).ToList();
    }
}
