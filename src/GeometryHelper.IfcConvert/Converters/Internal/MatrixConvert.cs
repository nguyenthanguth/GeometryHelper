using System;
using GeometryHelper.Geometry;
using Xbim.Common.Geometry;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Converts transformation matrices between xBIM and GeometryHelper.
    /// <para>
    /// xBIM stores transformation matrices in row-vector convention (v * M, with translation in OffsetX/Y/Z),
    /// whereas <see cref="GeoTransform3"/> uses column-vector convention (M * v, with translation in column 3).
    /// The conversions here transpose the linear 3x3 block and place offsets appropriately so that transformed
    /// coordinates match exactly.
    /// </para>
    /// </summary>
    internal static class MatrixConvert
    {
        /// <summary>
        /// Converts an <see cref="XbimMatrix3D"/> to a <see cref="GeoTransform3"/>, with an optional coordinate scale factor.
        /// </summary>
        /// <param name="matrix">The xBIM 4x4 matrix.</param>
        /// <param name="scale">The coordinate scale factor applied to translations (default is 1.0).</param>
        /// <returns>The equivalent <see cref="GeoTransform3"/>.</returns>
        public static GeoTransform3 ToGeoTransform3(this XbimMatrix3D matrix, double scale = 1.0)
        {
            double[,] m = new double[4, 4]
            {
                { matrix.M11, matrix.M21, matrix.M31, matrix.OffsetX * scale },
                { matrix.M12, matrix.M22, matrix.M32, matrix.OffsetY * scale },
                { matrix.M13, matrix.M23, matrix.M33, matrix.OffsetZ * scale },
                { matrix.M14, matrix.M24, matrix.M34, matrix.M44 }
            };

            return new GeoTransform3(m);
        }

        /// <summary>
        /// Converts a <see cref="GeoTransform3"/> to an <see cref="XbimMatrix3D"/>, with an optional coordinate scale factor.
        /// </summary>
        /// <param name="transform">The GeometryHelper 3D transformation.</param>
        /// <param name="scale">The coordinate scale factor applied to translations (default is 1.0).</param>
        /// <returns>The equivalent <see cref="XbimMatrix3D"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is null.</exception>
        public static XbimMatrix3D ToXbimMatrix3D(this GeoTransform3 transform, double scale = 1.0)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return new XbimMatrix3D(
                transform[0, 0], transform[1, 0], transform[2, 0], transform[3, 0],
                transform[0, 1], transform[1, 1], transform[2, 1], transform[3, 1],
                transform[0, 2], transform[1, 2], transform[2, 2], transform[3, 2],
                transform[0, 3] * scale, transform[1, 3] * scale, transform[2, 3] * scale, transform[3, 3]);
        }

        /// <summary>
        /// Converts an <see cref="IIfcObjectPlacement"/> to its global <see cref="GeoTransform3"/> matrix.
        /// </summary>
        /// <param name="placement">The IFC object placement.</param>
        /// <param name="scale">The coordinate scale factor (default is 1.0).</param>
        /// <returns>The computed <see cref="GeoTransform3"/>; or identity if placement is null.</returns>
        public static GeoTransform3 ToGeoTransform3(this IIfcObjectPlacement placement, double scale = 1.0)
        {
            if (placement == null)
            {
                return GeoTransform3.Identity;
            }

            XbimMatrix3D matrix = placement.ToMatrix3D();
            return matrix.ToGeoTransform3(scale);
        }
    }
}
