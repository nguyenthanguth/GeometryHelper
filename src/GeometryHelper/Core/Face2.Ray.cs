using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What a face meets of a ray in the plane, and how far off it is.
    /// </summary>
    /// <remarks>
    /// A ray runs on for ever in one direction, so against a face it is cut: into the segment from its origin out past
    /// the farthest a corner of the outline reaches along it, by a margin of a millimetre or a hundred point tolerances,
    /// whichever is more. Every point of the face lies behind that end, so the segment meets the face where the ray does
    /// and is asked as any segment is. Against the square 100 by 100 with a corner at the origin and a hole 40 by 40 in
    /// its middle, the ray from (-50, 50) along X is asked as the segment from there to (101, 50).
    /// </remarks>
    public static partial class Face2
    {
        /// <summary>
        /// Gets every point where a ray crosses the boundary of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoRay2 ray) => GetIntersections(face, ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where a ray crosses the boundary of a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoFace2 face, GeoRay2 ray, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return GetIntersections(face, Ray2.CutPast(ray, face, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a ray reaches the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoFace2 face, GeoRay2 ray) => CollidesWith(face, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray reaches the material of a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoFace2 face, GeoRay2 ray, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return CollidesWith(face, Ray2.CutPast(ray, face, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a ray.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoRay2 ray) => GetShortestLineTo(face, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a face and landing on a ray, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoRay2 ray, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return GetShortestLineTo(face, Ray2.CutPast(ray, face, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the distance from the material of a face to a ray.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoRay2 ray) => DistanceTo(face, ray, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the material of a face to a ray, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where the ray crosses a rim or lies on the material; otherwise the length of <see cref="GetShortestLineTo(GeoFace2, GeoRay2, Tolerance)"/>. A ray starting in a hole
        /// always runs out of it into the material, so it is nought away; the ray from (-50, 150) along X passes 50 above
        /// the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoFace2 face, GeoRay2 ray, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return DistanceTo(face, Ray2.CutPast(ray, face, tolerance), tolerance);
        }
    }
}
