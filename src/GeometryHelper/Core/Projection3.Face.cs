using System;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The point of a face nearest another point.
    /// </summary>
    /// <remarks>
    /// A face is a flat region with holes in it, so the nearest point of one is not simply the foot of the
    /// perpendicular: a point over a hole is over nothing, and the nearest material is the rim of that hole.
    /// The reading matches <see cref="ProjectToPolygon(GeoPolygon3, GeoPoint3)"/>, which answers with a point
    /// of the region where there is region under the foot and a point of the outline where there is not.
    /// </remarks>
    public static partial class Projection3
    {
        /// <summary>
        /// Gets the point of a face nearest another point.
        /// </summary>
        public static GeoPoint3 ProjectToFace(GeoFace3 face, GeoPoint3 point) => ProjectToFace(face, point, Tolerance.Global);

        /// <summary>
        /// Gets the point of a face nearest another point, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point">The point; it does not have to lie on the plane of the face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The foot of the perpendicular where that lands on the material, the rim of a hole where it lands
        /// in one, and the outline where it lands outside the face altogether.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3 ProjectToFace(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoPoint3 flat = ProjectToPlane(face.GetPlane(), point);

            if (!Containment3.Contains(face.Boundary, flat, tolerance))
            {
                return ProjectToPolygonBoundary(face.Boundary, point, tolerance);
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                // Over a hole is over nothing, so the nearest material is the rim of the hole rather than
                // the surface that is not there behind it.
                if (Containment3.Locate(hole, flat, tolerance) == PointLocation.Inside)
                {
                    return ProjectToPolygonBoundary(hole, point, tolerance);
                }
            }

            return flat;
        }
    }
}
