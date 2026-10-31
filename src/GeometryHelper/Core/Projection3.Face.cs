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
        /// <remarks>
        /// Whether the foot lands on the material is read with the tolerance, so a foot within the point
        /// tolerance past a rim counts as on it and is kept: the point given back may sit up to the point
        /// tolerance outside the material. Over the square 100 by 100 in the floor with a corner at the origin
        /// and a hole 20 by 20 in its middle, the point (100.0005, 50, 30) keeps its foot (100.0005, 50, 0),
        /// 0.0005 past the outline, and the point (40.0005, 50, 30) its foot (40.0005, 50, 0), 0.0005 into the
        /// hole; each is then 30 away, where the rim itself is 30.000000004 away. A point carried onto the outline or the rim
        /// of a hole lands on a side as it is however short: at a hundredth, the face on the polygon (0, 0, 0), (10, 0, 0),
        /// (10, 0.006, 0), (0, 10, 0) carries (10.03, 0.003, 0) to (10, 0.003, 0), 0.03 away, where its side 0.006 long
        /// offered its start (10, 0, 0), 0.0301496 away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3 ProjectToFace(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
            => ProjectToFace(face, point, tolerance, Exactly);

        /// <summary>
        /// Gets the point of a face nearest another point, whether the foot lands on the material read within one tolerance
        /// and the point carried onto a side of a rim within another: nought for the closest points and the shortest lines,
        /// which measure a side no longer than the point tolerance as the side it is; the tolerance itself for the readings
        /// of whether a point is on a face, which still carry it to the side's start.
        /// </summary>
        internal static GeoPoint3 ProjectToFace(GeoFace3 face, GeoPoint3 point, Tolerance tolerance, Tolerance sides)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoPlane3 plane = face.GetPlane();
            GeoPoint3 flat = ProjectToPlane(plane, point);

            if (!Containment3.Contains(face.Boundary, flat, tolerance))
            {
                return ProjectToRim(face.Boundary, point, sides);
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                // Over a hole is over nothing, so the nearest material is the rim of the hole rather than
                // the surface that is not there behind it. The hole is read in the plane of the boundary, as
                // GeoFace3.Locate reads it: one standing a hair off that plane holds nothing of a point on it.
                if (Containment3.LocateInPlane(hole, plane, flat, tolerance) == PointLocation.Inside)
                {
                    return ProjectToRim(hole, point, sides);
                }
            }

            return flat;
        }
    }
}
