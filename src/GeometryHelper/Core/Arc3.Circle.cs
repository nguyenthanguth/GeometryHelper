using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a circle in space crosses a plane, a segment or a ray, and whether it touches one.
    /// </summary>
    /// <remarks>
    /// A circle is an arc that sweeps a whole turn, which <see cref="GeoArc3"/> takes as equal start and end
    /// angles, so every pair here hands the work to the arc rather than working it out a second time. Nothing
    /// is lost: a whole turn reaches every point of its circle, so the sweep test the arc applies never
    /// rejects anything.
    /// </remarks>
    public static partial class Arc3
    {
        /// <summary>
        /// Reads a circle as the arc that sweeps all of it.
        /// </summary>
        internal static GeoArc3 AsArc(GeoCircle3 circle)
            => new GeoArc3(circle.Center, circle.Normal, circle.Radius, 0.0, 0.0);

        /// <summary>
        /// Gets every point where a circle crosses a plane.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoPlane3 plane)
            => GetIntersections(AsArc(circle), plane);

        /// <summary>
        /// Gets every point where a circle crosses a plane, within a tolerance.
        /// </summary>
        /// <returns>
        /// At most two points. A circle lying in the plane crosses it nowhere and gives none, which
        /// <see cref="CollidesWith(GeoCircle3, GeoPlane3, Tolerance)"/> reports instead.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoPlane3 plane, Tolerance tolerance)
            => GetIntersections(AsArc(circle), plane, tolerance);

        /// <summary>
        /// Determines whether a circle touches a plane.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoPlane3 plane) => CollidesWith(AsArc(circle), plane);

        /// <summary>
        /// Determines whether a circle touches a plane, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoPlane3 plane, Tolerance tolerance)
            => CollidesWith(AsArc(circle), plane, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses a plane.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoPlane3 plane, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), plane, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), plane, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses a segment.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoLine3 line)
            => GetIntersections(AsArc(circle), line);

        /// <summary>
        /// Gets every point where a circle crosses a segment, within a tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoLine3 line, Tolerance tolerance)
            => GetIntersections(AsArc(circle), line, tolerance);

        /// <summary>
        /// Determines whether a circle touches a segment.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoLine3 line) => CollidesWith(AsArc(circle), line);

        /// <summary>
        /// Determines whether a circle touches a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The rim is what counts, not the disc: a segment crossing the middle of the circle without reaching
        /// the rim touches nothing here. <see cref="Containment3.Contains(GeoCircle3, GeoPoint3)"/> is the one
        /// that reads a circle as filled.
        /// </remarks>
        public static bool CollidesWith(GeoCircle3 circle, GeoLine3 line, Tolerance tolerance)
            => CollidesWith(AsArc(circle), line, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses a segment.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), line, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), line, out intersections, tolerance);

        /// <summary>
        /// Gets every point where a circle crosses a ray.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoRay3 ray) => GetIntersections(AsArc(circle), ray);

        /// <summary>
        /// Gets every point where a circle crosses a ray, within a tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoRay3 ray, Tolerance tolerance)
            => GetIntersections(AsArc(circle), ray, tolerance);

        /// <summary>
        /// Determines whether a circle touches a ray.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoRay3 ray) => CollidesWith(AsArc(circle), ray);

        /// <summary>
        /// Determines whether a circle touches a ray, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 circle, GeoRay3 ray, Tolerance tolerance)
            => CollidesWith(AsArc(circle), ray, tolerance);

        /// <summary>
        /// Tries to find where a circle crosses a ray.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(circle), ray, out intersections);

        /// <summary>
        /// Tries to find where a circle crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 circle, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(circle), ray, out intersections, tolerance);
    }
}
