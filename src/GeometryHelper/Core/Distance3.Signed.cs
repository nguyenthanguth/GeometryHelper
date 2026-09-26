using System;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// How far a point stands from a body, and whether it is inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same idea as in the plane: <see cref="DistanceTo(GeoSolid3, GeoPoint3)"/> reads a body as solid
    /// through, so a point anywhere inside is nought away, and <c>SignedDistanceTo</c> keeps the depth
    /// instead. The magnitude is the distance to the surface, whichever side of it the point is on, and the
    /// sign says which side.
    /// </para>
    /// <para>
    /// The rule is tied to <see cref="Containment3.Locate(GeoSolid3, GeoPoint3)"/>: <b>negative</b> where it
    /// answers <see cref="PointLocation.Inside"/>, <b>nought</b> where it answers
    /// <see cref="PointLocation.OnSide"/>, <b>positive</b> where it answers
    /// <see cref="PointLocation.OutSide"/>.
    /// </para>
    /// <para>
    /// Only the shapes that enclose a volume are offered. A <see cref="GeoTriangle3"/>, a
    /// <see cref="GeoPolygon3"/> and a <see cref="GeoCircle3"/> are flat regions standing in space and
    /// enclose nothing, so a point is inside one only when it is also on its plane: a sign for them would be
    /// negative on a set of no thickness at all and would read as though it meant more.
    /// <see cref="GeoPlane3.SignedDistanceTo(GeoPoint3)"/> already answers the question that does make sense
    /// for something flat, which is which side of it a point is on.
    /// </para>
    /// </remarks>
    public static partial class Distance3
    {
        /// <summary>
        /// Calculates the distance from a solid to a point, negative for a point inside the body.
        /// </summary>
        public static double SignedDistanceTo(GeoSolid3 solid, GeoPoint3 point) => SignedDistanceTo(solid, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from a solid to a point, negative for a point inside the body, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The distance out to the surface, negative when the point lies in the material.</returns>
        /// <remarks>
        /// The surface of a pierced body is its faces and the walls of every opening in it, openings of
        /// openings included, because that is what <see cref="Containment3.Locate(GeoSolid3, GeoPoint3)"/>
        /// calls the boundary. So a point in the material beside a duct is measured to the wall of the duct
        /// rather than out to the far skin of the body.
        /// </remarks>
        public static double SignedDistanceTo(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return Signed(ReachToSurface(solid, point, tolerance), Containment3.Locate(solid, point, tolerance));
        }

        /// <summary>
        /// Calculates the distance from an oriented box to a point, negative for a point inside it.
        /// </summary>
        public static double SignedDistanceTo(GeoObb3 box, GeoPoint3 point) => SignedDistanceTo(box, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from an oriented box to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        /// <returns>The distance out to the nearest face, negative when the point lies within the box.</returns>
        /// <remarks>
        /// <see cref="GeoObb3.GetClosestPointOnBoundary(GeoPoint3)"/> clamps a point into the box and so
        /// hands an interior point straight back; the magnitude here comes from
        /// <see cref="Projection3.ProjectToObbSurface(GeoObb3, GeoPoint3)"/>, which pushes it out to a face.
        /// </remarks>
        public static double SignedDistanceTo(GeoObb3 box, GeoPoint3 point, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return Signed(
                point.DistanceTo(Projection3.ProjectToObbSurface(box, point)),
                Containment3.Locate(box, point, tolerance));
        }

        /// <summary>
        /// Calculates the distance from an axis-aligned box to a point, negative for a point inside it.
        /// </summary>
        public static double SignedDistanceTo(GeoAabb3 box, GeoPoint3 point) => SignedDistanceTo(box, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from an axis-aligned box to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        /// <returns>The distance out to the nearest face, negative when the point lies within the box.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty, since it has no surface to measure to.</exception>
        public static double SignedDistanceTo(GeoAabb3 box, GeoPoint3 point, Tolerance tolerance)
        {
            return Signed(
                point.DistanceTo(Projection3.ProjectToObbSurface(box.ToObb(), point)),
                box.Locate(point, tolerance));
        }

        /// <summary>
        /// Gets the distance from a point to the boundary of a body, opening walls included.
        /// </summary>
        /// <remarks>
        /// The boundary is where the material ends, which is what
        /// <see cref="Projection3.ProjectToSolid(GeoSolid3, GeoPoint3, Tolerance)"/> lands on: the openings in
        /// reach are cut in first, so a point below a duct bored through a slab is measured to the rim of the
        /// duct's mouth and not to the underside across it, where there is no material. The earlier way — the
        /// nearest point of the faces and of each opening, kept only where the body called it boundary — could
        /// not find that rim at all, since it is a point of neither alone, and fell back on a distance of nought.
        /// </remarks>
        private static double ReachToSurface(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
            => point.DistanceTo(Projection3.ProjectToSolid(solid, point, tolerance));

        /// <summary>
        /// Turns a distance to a surface into a signed one, by where the point sits.
        /// </summary>
        private static double Signed(double reach, PointLocation where)
        {
            return where == PointLocation.Inside ? -reach : reach;
        }
    }
}
