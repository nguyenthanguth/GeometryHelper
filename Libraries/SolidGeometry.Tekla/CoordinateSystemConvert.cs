using SolidGeometry.Geometry;
using System;
using TSG = Tekla.Structures.Geometry3d;

namespace SolidGeometry.Tekla
{
    /// <summary>
    /// Provides extension methods to convert coordinate systems between Tekla Structures and SolidGeometry.
    /// </summary>
    public static class CoordinateSystemConvert
    {
        /// <summary>
        /// Converts a Tekla coordinate system to a SolidGeometry coordinate system.
        /// </summary>
        /// <param name="system">The Tekla coordinate system to convert.</param>
        /// <returns>The converted <see cref="GeoCoordinateSystem3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="system"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the two axes are parallel or one of them has zero length, since neither pins down a
        /// frame.
        /// </exception>
        /// <remarks>
        /// Tekla stores two axes and leaves the third implied; SolidGeometry stores three and keeps them
        /// orthonormal. A Tekla frame whose Y axis is not quite square to its X axis is therefore squared
        /// up on the way in rather than carried across as a skewed frame.
        /// </remarks>
        public static GeoCoordinateSystem3 ToGeoCoordinateSystem3(this TSG.CoordinateSystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            return new GeoCoordinateSystem3(
                system.Origin.ToGeoPoint3(),
                system.AxisX.ToGeoVector3(),
                system.AxisY.ToGeoVector3());
        }

        /// <summary>
        /// Converts a SolidGeometry coordinate system to a Tekla coordinate system.
        /// </summary>
        /// <param name="system">The SolidGeometry coordinate system to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.CoordinateSystem"/>.</returns>
        public static TSG.CoordinateSystem ToTeklaCoordinateSystem(this GeoCoordinateSystem3 system)
        {
            return new TSG.CoordinateSystem(
                system.Origin.ToTeklaPoint(),
                system.XAxis.ToTeklaVector(),
                system.YAxis.ToTeklaVector());
        }
    }
}
