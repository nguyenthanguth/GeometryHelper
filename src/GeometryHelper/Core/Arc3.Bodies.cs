using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where an arc in space crosses the surface of a box or a body, and whether it touches one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The surface of a box or a body is flat faces, so this is the union of what the arc does against each of
    /// them, with the shared edges counted once. Nothing new is worked out here.
    /// </para>
    /// <para>
    /// A pierced body needs the same care its signed distance needs. Its boundary is its own faces
    /// <b>and</b> the walls of every opening, openings of openings included, so the walk gathers candidates
    /// from all of them and then keeps only the ones the body itself calls
    /// <see cref="PointLocation.OnSide"/> — which throws out a point on an outer face where an opening has
    /// taken the material away.
    /// </para>
    /// <para>
    /// Touching is not the same as crossing here, because a body has an inside. An arc wholly within the
    /// material crosses no face and touches all the same, so where it starts is asked as well. An arc wholly
    /// within an opening is not in the material, and that same question rules it out.
    /// </para>
    /// </remarks>
    public static partial class Arc3
    {
        #region Against a body

        /// <summary>
        /// Gathers the points where an arc meets the boundary of a body, openings included.
        /// </summary>
        private static void AddCrossingsOfBody(List<GeoPoint3> found, GeoArc3 arc, GeoSolid3 body, GeoSolid3 part, Tolerance tolerance)
        {
            foreach (GeoFace3 face in part.Faces)
            {
                foreach (GeoPoint3 candidate in GetIntersections(arc, face, tolerance))
                {
                    if (Containment3.Locate(body, candidate, tolerance) == PointLocation.OnSide)
                    {
                        AddOnce(found, candidate, tolerance);
                    }
                }
            }

            foreach (GeoSolid3 opening in part.Openings)
            {
                AddCrossingsOfBody(found, arc, body, opening, tolerance);
            }
        }

        /// <summary>
        /// Gets every point where an arc crosses the surface of a solid.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoSolid3 solid) => GetIntersections(arc, solid, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the arc goes in and where it comes out, the walls of any opening counted as surface. An arc
        /// lying wholly inside the material crosses nothing and gives none, which
        /// <see cref="CollidesWith(GeoArc3, GeoSolid3, Tolerance)"/> reports instead.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfBody(found, arc, solid, solid, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether an arc touches a solid.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoSolid3 solid) => CollidesWith(arc, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches a solid, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// Where the arc starts is asked before any face is tried, because an arc inside the body crosses its
        /// surface nowhere and is in it all the same. The same question keeps an arc inside an opening out: an
        /// opening is not material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoArc3 arc, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return Containment3.Contains(solid, arc.StartPoint, tolerance)
                || GetIntersections(arc, solid, tolerance).Length > 0;
        }

        /// <summary>
        /// Tries to find where an arc crosses the surface of a solid.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoSolid3 solid, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, solid, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, solid, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a box

        /// <summary>
        /// Gets every point where an arc crosses the surface of an oriented box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoObb3 box) => GetIntersections(arc, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The points where the arc meets one of the six faces, each corner or edge counted once.</returns>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoObb3 box, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPolygon3 face in box.GetFaces())
            {
                foreach (GeoPoint3 candidate in GetIntersections(arc, face, tolerance))
                {
                    AddOnce(found, candidate, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether an arc touches an oriented box.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoObb3 box) => CollidesWith(arc, box, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// An arc wholly inside the box crosses no face and is in it all the same, so where it starts is asked
        /// before any face is tried.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 arc, GeoObb3 box, Tolerance tolerance)
        {
            return Containment3.Contains(box, arc.StartPoint, tolerance)
                || GetIntersections(arc, box, tolerance).Length > 0;
        }

        /// <summary>
        /// Tries to find where an arc crosses the surface of an oriented box.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where an arc crosses the surface of an axis-aligned box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoAabb3 box) => GetIntersections(arc, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="box">The box; an empty one holds nothing and is crossed nowhere.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// An axis-aligned box is an oriented box whose axes happen to be the world's, so this is the oriented
        /// answer rather than an approximation of it.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return new GeoPoint3[0];
            }

            return GetIntersections(arc, box.ToObb(), tolerance);
        }

        /// <summary>
        /// Determines whether an arc touches an axis-aligned box.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoAabb3 box) => CollidesWith(arc, box, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="box">The box; an empty one is touched by nothing.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoArc3 arc, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return false;
            }

            return CollidesWith(arc, box.ToObb(), tolerance);
        }

        /// <summary>
        /// Tries to find where an arc crosses the surface of an axis-aligned box.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, box, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region A circle against the same

        /// <summary>
        /// Gets every point where a circle crosses the surface of a solid.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoSolid3 solid)
            => GetIntersections(AsArc(circle), solid);

        /// <summary>
        /// Gets every point where a circle crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoSolid3 solid, Tolerance tolerance)
            => GetIntersections(AsArc(circle), solid, tolerance);

        /// <summary>
        /// Determines whether a circle touches a solid.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoSolid3 solid) => CollidesWith(AsArc(circle), solid);

        /// <summary>
        /// Determines whether a circle touches a solid, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoCircle3 circle, GeoSolid3 solid, Tolerance tolerance)
            => CollidesWith(AsArc(circle), solid, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses the surface of a solid.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoSolid3 solid, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), solid, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), solid, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses the surface of an oriented box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoObb3 box) => GetIntersections(AsArc(circle), box);

        /// <summary>
        /// Gets every point where a circle crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoObb3 box, Tolerance tolerance)
            => GetIntersections(AsArc(circle), box, tolerance);

        /// <summary>
        /// Determines whether a circle touches an oriented box.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoObb3 box) => CollidesWith(AsArc(circle), box);

        /// <summary>
        /// Determines whether a circle touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoCircle3 circle, GeoObb3 box, Tolerance tolerance)
            => CollidesWith(AsArc(circle), box, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses the surface of an oriented box.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), box, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), box, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses the surface of an axis-aligned box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoAabb3 box) => GetIntersections(AsArc(circle), box);

        /// <summary>
        /// Gets every point where a circle crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoAabb3 box, Tolerance tolerance)
            => GetIntersections(AsArc(circle), box, tolerance);

        /// <summary>
        /// Determines whether a circle touches an axis-aligned box.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoAabb3 box) => CollidesWith(AsArc(circle), box);

        /// <summary>
        /// Determines whether a circle touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoCircle3 circle, GeoAabb3 box, Tolerance tolerance)
            => CollidesWith(AsArc(circle), box, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses the surface of an axis-aligned box.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), box, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), box, out intersections, tolerance);

        #endregion
    }
}
