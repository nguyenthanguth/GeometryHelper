using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Common.Geometry;

namespace GeometryHelper.IfcConvert.Converters
{
    /// <summary>
    /// Converts points and vectors between xBIM and GeometryHelper.SolidGeometry.
    /// </summary>
    public static class PointConvert
    {
        /// <summary>
        /// Converts an xBIM 3D point to a <see cref="GeoPoint3"/>, with an optional coordinate scale factor.
        /// </summary>
        /// <param name="point">The xBIM point.</param>
        /// <param name="scale">The coordinate scale factor (default is 1.0).</param>
        /// <returns>The converted <see cref="GeoPoint3"/>.</returns>
        public static GeoPoint3 ToGeoPoint3(this XbimPoint3D point, double scale = 1.0)
        {
            return new GeoPoint3(point.X * scale, point.Y * scale, point.Z * scale);
        }

        /// <summary>
        /// Converts a <see cref="GeoPoint3"/> to an xBIM 3D point, with an optional coordinate scale factor.
        /// </summary>
        /// <param name="point">The GeometryHelper 3D point.</param>
        /// <param name="scale">The coordinate scale factor (default is 1.0).</param>
        /// <returns>The converted <see cref="XbimPoint3D"/>.</returns>
        public static XbimPoint3D ToXbimPoint3D(this GeoPoint3 point, double scale = 1.0)
        {
            return new XbimPoint3D(point.X * scale, point.Y * scale, point.Z * scale);
        }

        /// <summary>
        /// Converts an xBIM 3D vector to a <see cref="GeoVector3"/>, with an optional coordinate scale factor.
        /// </summary>
        /// <param name="vector">The xBIM vector.</param>
        /// <param name="scale">The coordinate scale factor (default is 1.0).</param>
        /// <returns>The converted <see cref="GeoVector3"/>.</returns>
        public static GeoVector3 ToGeoVector3(this XbimVector3D vector, double scale = 1.0)
        {
            return new GeoVector3(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        /// <summary>
        /// Converts a <see cref="GeoVector3"/> to an xBIM 3D vector, with an optional coordinate scale factor.
        /// </summary>
        /// <param name="vector">The GeometryHelper 3D vector.</param>
        /// <param name="scale">The coordinate scale factor (default is 1.0).</param>
        /// <returns>The converted <see cref="XbimVector3D"/>.</returns>
        public static XbimVector3D ToXbimVector3D(this GeoVector3 vector, double scale = 1.0)
        {
            return new XbimVector3D(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        /// <summary>
        /// Converts a sequence of xBIM 3D points to a list of <see cref="GeoPoint3"/>, with an optional coordinate scale factor.
        /// </summary>
        /// <param name="points">The sequence of xBIM points.</param>
        /// <param name="scale">The coordinate scale factor (default is 1.0).</param>
        /// <returns>A list of converted <see cref="GeoPoint3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static List<GeoPoint3> ToGeoPoint3(this IEnumerable<XbimPoint3D> points, double scale = 1.0)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            return points.Select(p => p.ToGeoPoint3(scale)).ToList();
        }
    }
}
