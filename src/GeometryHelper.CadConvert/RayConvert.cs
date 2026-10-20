using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using GeometryHelper.Geometry;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Converts rays, the AutoCAD Ray entities, between AutoCAD and GeometryHelper.
    /// </summary>
    /// <remarks>
    /// An AutoCAD <see cref="Ray"/> is held as a base point and a unit direction, and runs for ever from the base point. A
    /// <see cref="GeoRay3"/> holds the same two things, so the reading keeps both; the direction is made unit length again on the way in,
    /// so a round trip can differ in the last digit of it. The ray is in space: a ray in the plane is
    /// <see cref="GeoRay3.ProjectToRay2(GeoCoordinateSystem3)"/> of it.
    /// </remarks>
    public static class RayConvert
    {
        /// <summary>
        /// Converts an AutoCAD Ray to a GeometryHelper ray in space.
        /// </summary>
        /// <param name="ray">The AutoCAD Ray to convert.</param>
        /// <returns>The converted <see cref="GeoRay3"/>, from AutoCAD's base point along its unit direction.</returns>
        /// <remarks>
        /// A Ray from (100, 200, 0) along Y is the ray from (100, 200, 0) along (0, 1, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ray"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the Ray has a unit direction of no length.</exception>
        public static GeoRay3 ToGeoRay3(this Ray ray)
        {
            if (ray == null) throw new ArgumentNullException(nameof(ray));
            return new GeoRay3(ray.BasePoint.ToGeoPoint3(), ray.UnitDir.ToGeoVector3());
        }

        /// <summary>
        /// Converts a sequence of AutoCAD Rays to a list of GeometryHelper rays in space.
        /// </summary>
        /// <param name="rays">The sequence of AutoCAD Rays to convert.</param>
        /// <returns>A list of converted <see cref="GeoRay3"/>.</returns>
        public static List<GeoRay3> ToGeoRay3(this IEnumerable<Ray> rays) => rays.Select(ToGeoRay3).ToList();

        /// <summary>
        /// Converts a GeometryHelper ray in space to an AutoCAD Ray.
        /// </summary>
        /// <param name="geoRay">The GeometryHelper ray to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Ray"/>, its base point the ray's origin and its unit direction the ray's.</returns>
        /// <exception cref="ArgumentException">Thrown when the ray has no direction, as a <c>default</c> one has, or holds a number that is not finite.</exception>
        public static Ray ToAcadRay(this GeoRay3 geoRay)
        {
            if (!geoRay.IsValid) throw new ArgumentException("A ray with no direction, or a number that is not finite, has no Ray.", nameof(geoRay));
            return new Ray { BasePoint = geoRay.Origin.ToAcadPoint3(), UnitDir = geoRay.Direction.ToAcadVector3() };
        }

        /// <summary>
        /// Converts a sequence of GeometryHelper rays in space to a list of AutoCAD Rays.
        /// </summary>
        /// <param name="rays">The sequence of GeometryHelper rays to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Ray"/>.</returns>
        public static List<Ray> ToAcadRay(this IEnumerable<GeoRay3> rays) => rays.Select(ToAcadRay).ToList();
    }
}
