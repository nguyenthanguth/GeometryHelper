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
        /// <para>
        /// <see cref="Projection3.ProjectToSolid(GeoSolid3, GeoPoint3, Tolerance)"/> walks the faces of the
        /// body it is given and nothing else, so each opening is offered in turn, and each opening offers its
        /// own openings, the way <see cref="Containment3.Locate(GeoSolid3, GeoPoint3, Tolerance)"/> reaches
        /// them.
        /// </para>
        /// <para>
        /// Each point offered is then held against the body itself and kept only if the body agrees it is on
        /// the boundary. That is what keeps the answer and <c>Locate</c> saying the same thing: a duct bored
        /// right through a slab runs out past both faces, and the part of its wall out there bounds nothing,
        /// so a point below the slab is measured to the underside rather than to the mouth of the duct
        /// hanging past it.
        /// </para>
        /// </remarks>
        private static double ReachToSurface(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            double onBoundary = double.MaxValue;
            double anywhere = double.MaxValue;

            Offer(solid, solid, point, tolerance, ref onBoundary, ref anywhere);

            // A body always has a boundary, so the fallback is there for shapes too broken to have one
            // rather than for any case worth naming.
            return onBoundary < double.MaxValue ? onBoundary : anywhere;
        }

        /// <summary>
        /// Offers the nearest point of one part of a body, and of every opening within that part.
        /// </summary>
        private static void Offer(
            GeoSolid3 body,
            GeoSolid3 part,
            GeoPoint3 point,
            Tolerance tolerance,
            ref double onBoundary,
            ref double anywhere)
        {
            GeoPoint3 candidate = Projection3.ProjectToSolid(part, point, tolerance);
            double reach = point.DistanceTo(candidate);

            anywhere = Math.Min(anywhere, reach);

            if (Containment3.Locate(body, candidate, tolerance) == PointLocation.OnSide)
            {
                onBoundary = Math.Min(onBoundary, reach);
            }

            foreach (GeoSolid3 opening in part.Openings)
            {
                Offer(body, opening, point, tolerance, ref onBoundary, ref anywhere);
            }
        }

        /// <summary>
        /// Turns a distance to a surface into a signed one, by where the point sits.
        /// </summary>
        private static double Signed(double reach, PointLocation where)
        {
            return where == PointLocation.Inside ? -reach : reach;
        }
    }
}
