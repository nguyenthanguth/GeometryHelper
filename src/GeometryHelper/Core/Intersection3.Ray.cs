using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a ray runs into a body.
    /// </summary>
    public static partial class Intersection3
    {
        /// <summary>
        /// Gets the points where a ray passes through the surface of a solid.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoSolid3 solid) => GetIntersections(ray, solid, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray passes through the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings in the order the ray meets them, with repeats left out.</returns>
        /// <remarks>
        /// The surface is walked as triangles, so a crossing that falls on the seam between two of them
        /// arrives twice and is reported once, the two within the point tolerance of each other. However small the tolerance,
        /// a triangle holds a crossing to the rounding of the coordinates, 1E-15 of the largest of the body's and the ray's
        /// origin, on its plane and past its rim; below that rounding, and only there, the two crossings of a seam within eight
        /// times it of each other are one, and a ray from further off than the body is large is read from a diagonal short of
        /// it, along its line, so that the rounding stays the body's. At a tolerance of nought the ray along the seam of two
        /// sides of a cylinder of radius 20, through its axis, is given its two crossings from 60 out and from a hundred
        /// million out, where it was given one or none. A ray starting inside the body has an odd number of
        /// crossings, and one starting outside an even number, give or take a face it merely grazes.
        /// The list is held to <see cref="Collision3.CollidesWith(GeoRay3, GeoSolid3, Tolerance)"/>, which reads the ray by the
        /// point tolerance and the body to a billionth (further out than a million, to 1E-15 of the largest coordinate): a ray
        /// starting within the planar tolerance of a face meets it at its
        /// origin only where the origin stands within the point tolerance of the surface, whatever it meets further on, and one
        /// that collides and crosses no face is given its point nearest the surface, where that stands within the point tolerance
        /// of it, to the rounding of numbers the size of the body's, or the body holds the origin. With a point tolerance of a
        /// hundredth and a planar one of five hundredths, the ray from 0.04 over the top of the box 100 by 100 by 50, rising a
        /// hundredth for each unit along it, is given nothing, where it was given its origin and collided with nothing; the one
        /// from 0.04 off the inner wall of a U, across its gap, is given the walls of the far prong only, and the one from 0.04
        /// under the top of the box only where it leaves, where each was given its origin besides; at a tolerance of nought the
        /// ray from the middle of a box 100 by 80 by 50 turned about in space is given where it leaves, where the rounding
        /// refused the crossing of every face and it collided and was given nothing.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; read the material the probe can reach instead.
            solid = Material3.Near(solid, ray, tolerance);

            var reaches = new List<double>();

            // Below the rounding of the coordinates a face holds a crossing within it, and a ray from far off is read from near
            // the body, along its line, so that what the face holds is worked out from numbers the body's size.
            GeoRay3 near = CastNear(ray, solid.GetAabb());
            double rounding = Rounding(solid.GetAabb(), near.Origin);
            Tolerance holding = Holding(rounding, tolerance);
            GeoRay3 cast = holding.Equals(tolerance) ? ray : near;

            foreach (GeoTriangle3 face in solid.TriangulateQuietly(tolerance))
            {
                if (TryCrossTriangle(cast, face, out GeoPoint3 meeting, tolerance, holding, false))
                {
                    reaches.Add(cast.GetDistanceAtPoint(meeting));
                }
            }

            reaches.Sort();

            // A ray starting within the planar tolerance of a face is met at its origin, which may stand further off than the point
            // tolerance, or lie deeper in the body: the meeting is given only where the origin stands within the point tolerance of
            // the surface, whatever the ray meets further on. A face is crossed only where it holds the crossing, which at a
            // tolerance of nought the rounding of a body turned about in space can refuse: a ray that touches the body, as
            // CollidesWith reads it, and crosses no face is given its point nearest the surface.
            if (reaches.Count > 0 && !(reaches[0] > 0.0) && !Collision3.StartTouches(solid, ray, tolerance))
            {
                reaches.RemoveAll(reach => !(reach > 0.0));
            }

            if (reaches.Count == 0)
            {
                if (!NearBox(ray, solid, tolerance) || !Collision3.RayTouches(solid, ray, tolerance))
                {
                    return new GeoPoint3[0];
                }

                // The nearest point is given where it stands within the point tolerance of the surface, or twice a billionth, the
                // most the reading behind CollidesWith holds at the edge of two faces near the origin, and the rounding of numbers
                // the size of the body's, ten times the 1E-15 of them that reading holds further out than a million; or where the
                // body holds the origin as Locate reads it at the tolerance given.
                GeoPoint3 nearest = NearestOnRay(solid, ray, out double gap);
                GeoAabb3 box = solid.GetAabb();
                double size = Math.Max(Math.Max(Math.Abs(box.Min.X), Math.Abs(box.Max.X)), Math.Max(
                    Math.Max(Math.Abs(box.Min.Y), Math.Abs(box.Max.Y)), Math.Max(Math.Abs(box.Min.Z), Math.Abs(box.Max.Z))));

                return !(gap > Math.Max(tolerance.EqualPoint, 2E-9) + 1E-14 * (1.0 + size))
                    || Containment3.Locate(solid, ray.Origin, tolerance) != Enums.PointLocation.OutSide
                    ? new[] { nearest }
                    : new GeoPoint3[0];
            }

            var hits = new List<GeoPoint3>();

            // Below the rounding a seam is one crossing to eight times it, which at a tolerance of nought is what its two triangles
            // differ by along the ray.
            double once = SeamWidth(rounding, tolerance);

            foreach (double reach in reaches)
            {
                if (hits.Count > 0 && reach - cast.GetDistanceAtPoint(hits[hits.Count - 1]) <= once)
                {
                    continue;
                }

                hits.Add(cast.GetPointAtDistance(reach));
            }

            return hits.ToArray();
        }

        /// <summary>
        /// Determines whether a ray comes within the reach of the point tolerance, and as far again as the body holds a point
        /// outside it (a billionth, or further out than a million 1E-15 of the largest coordinate), of the box of a body, as far
        /// as the farthest corner of the box: further, it neither touches the body nor starts in it.
        /// </summary>
        /// <remarks>
        /// A fixed billionth gave nothing 5E7 out at a tolerance of nought for the ray from 2.2E-8 over the top of a box, pointing
        /// away, which the body holds and so collides with.
        /// </remarks>
        private static bool NearBox(GeoRay3 ray, GeoSolid3 material, Tolerance tolerance)
        {
            GeoAabb3 box = material.GetAabb();
            double reach = ray.Origin.DistanceTo(box.Center) + 0.5 * box.Min.DistanceTo(box.Max);

            return !(Gap3.BoxGap(ray.Origin, ray.GetPointAtDistance(reach), box) > Collision3.Reach(tolerance) + Gap3.Fine(material, ray.Origin));
        }

        /// <summary>
        /// Gets the point of a ray nearest the surface of a body, as far as the farthest corner of the body's box: of points as near,
        /// the one nearest the origin, and its gap to the surface.
        /// </summary>
        private static GeoPoint3 NearestOnRay(GeoSolid3 material, GeoRay3 ray, out double nearestGap)
        {
            GeoAabb3 box = material.GetAabb();
            GeoPoint3 end = ray.GetPointAtDistance(ray.Origin.DistanceTo(box.Center) + 0.5 * box.Min.DistanceTo(box.Max));
            GeoPoint3 nearest = ray.Origin;
            double best = double.MaxValue;
            double bestAlong = double.MaxValue;

            for (int i = 0; i < material.Faces.Count; i++)
            {
                Gap3.Region region = material.RegionOf(i);
                GeoPoint3 point = region.Nearest(ray.Origin, end);
                double gap = region.PointGap(point);
                double along = ray.GetDistanceAtPoint(point);

                if (gap < best || (gap == best && along < bestAlong))
                {
                    nearest = point;
                    best = gap;
                    bestAlong = along;
                }
            }

            nearestGap = best;
            return nearest;
        }
    }
}
