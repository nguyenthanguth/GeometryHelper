using GeometryHelper.SolidGeometry.Geometry;
using System;
using TSG = Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert geometric planes between Tekla Structures and GeometryHelper.SolidGeometry.
    /// </summary>
    public static class GeometricPlaneConvert
    {
        /// <summary>
        /// Converts a Tekla geometric plane to a SolidGeometry plane.
        /// </summary>
        /// <param name="plane">The Tekla geometric plane to convert.</param>
        /// <returns>The converted <see cref="GeoPlane3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plane"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the plane carries a zero-length normal.</exception>
        public static GeoPlane3 ToGeoPlane3(this TSG.GeometricPlane plane)
        {
            if (plane == null)
            {
                throw new ArgumentNullException(nameof(plane));
            }

            return new GeoPlane3(plane.Origin.ToGeoPoint3(), plane.Normal.ToGeoVector3());
        }

        /// <summary>
        /// Converts a SolidGeometry plane to a Tekla geometric plane.
        /// </summary>
        /// <param name="plane">The SolidGeometry plane to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.GeometricPlane"/>.</returns>
        public static TSG.GeometricPlane ToTeklaPlane(this GeoPlane3 plane)
        {
            return new TSG.GeometricPlane(plane.Origin.ToTeklaPoint(), plane.Normal.ToTeklaVector());
        }
    }
}
