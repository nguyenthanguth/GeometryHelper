using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The material of a body, for the questions that read its faces as where the material ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A body keeps its openings as whole bodies subtracted from it, so its faces run straight across every
    /// hole. Every question that reads the faces — or the mesh made from them — as the boundary of the solid
    /// goes through here first, and gets a body without openings whose faces are where the material ends.
    /// </para>
    /// <para>
    /// <b>How many openings are cut in depends on the question.</b> Whether a probe touches the body, or where
    /// it crosses it, only needs the openings the probe can reach: an opening out of its reach takes away
    /// nothing the probe could meet, so a bolt checked against a plate with twenty holes costs one cut, not
    /// twenty. How far apart the two are needs them all, because the place of the body nearest the probe can
    /// lie inside an opening the probe never comes near, and the true answer is then on that opening's rim. A
    /// caller asking many such questions of one body cuts it once with
    /// <see cref="GeoSolid3.TryCutOpenings(out GeoSolid3)"/> and asks the result.
    /// </para>
    /// <para>
    /// A body whose openings take all of its material is answered by its faces, the only thing it still
    /// describes — the same as <see cref="Projection3.ProjectToSolid(GeoSolid3, GeoPoint3, Tolerance)"/> does.
    /// </para>
    /// </remarks>
    internal static class Material3
    {
        /// <summary>
        /// The material of a body within reach of a box.
        /// </summary>
        internal static GeoSolid3 Near(GeoSolid3 solid, GeoAabb3 reach, Tolerance tolerance)
            => Near(solid, opening => opening.GetAabb().CollidesWith(reach, tolerance), tolerance);

        /// <summary>
        /// The material of a body along a ray.
        /// </summary>
        internal static GeoSolid3 Near(GeoSolid3 solid, GeoRay3 ray, Tolerance tolerance)
            => Near(solid, opening => RayMeetsBox(ray, opening.GetAabb(), tolerance), tolerance);

        /// <summary>
        /// The material of a body where a plane passes through it.
        /// </summary>
        internal static GeoSolid3 Near(GeoSolid3 solid, GeoPlane3 plane, Tolerance tolerance)
            => Near(solid, opening => PlaneMeetsBox(plane, opening.GetAabb(), tolerance), tolerance);

        /// <summary>
        /// The whole material of a body, every opening cut in.
        /// </summary>
        internal static GeoSolid3 Whole(GeoSolid3 solid, Tolerance tolerance)
            => Near(solid, opening => true, tolerance);

        private static GeoSolid3 Near(GeoSolid3 solid, Func<GeoSolid3, bool> reaches, Tolerance tolerance)
        {
            if (solid.Openings.Count == 0)
            {
                return solid;
            }

            var chosen = new List<GeoSolid3>();

            foreach (GeoSolid3 opening in solid.Openings)
            {
                if (reaches(opening))
                {
                    chosen.Add(opening);
                }
            }

            return Boolean3.TryCutOpenings(solid, chosen, out GeoSolid3 material, tolerance)
                ? material
                : new GeoSolid3(solid.Faces);
        }

        /// <summary>
        /// Determines whether a ray passes through a box, or within the tolerance of it.
        /// </summary>
        private static bool RayMeetsBox(GeoRay3 ray, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return false;
            }

            double pad = tolerance.EqualPoint;
            double enter = 0.0;
            double leave = double.MaxValue;

            double[] origin = { ray.Origin.X, ray.Origin.Y, ray.Origin.Z };
            double[] along = { ray.Direction.X, ray.Direction.Y, ray.Direction.Z };
            double[] low = { box.Min.X - pad, box.Min.Y - pad, box.Min.Z - pad };
            double[] high = { box.Max.X + pad, box.Max.Y + pad, box.Max.Z + pad };

            for (int axis = 0; axis < 3; axis++)
            {
                if (Math.Abs(along[axis]) < 1E-15)
                {
                    if (origin[axis] < low[axis] || origin[axis] > high[axis])
                    {
                        return false;
                    }

                    continue;
                }

                double t0 = (low[axis] - origin[axis]) / along[axis];
                double t1 = (high[axis] - origin[axis]) / along[axis];

                enter = Math.Max(enter, Math.Min(t0, t1));
                leave = Math.Min(leave, Math.Max(t0, t1));

                if (enter > leave)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a plane passes through a box, or within the tolerance of it.
        /// </summary>
        private static bool PlaneMeetsBox(GeoPlane3 plane, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return false;
            }

            double lowest = double.MaxValue;
            double highest = double.MinValue;

            foreach (GeoPoint3 corner in box.GetCorners())
            {
                double side = plane.SignedDistanceTo(corner);

                lowest = Math.Min(lowest, side);
                highest = Math.Max(highest, side);
            }

            return lowest <= tolerance.EqualPoint && highest >= -tolerance.EqualPoint;
        }
    }
}
