using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// One chain in space against another: the six pairs the walk over edge pairs answers.
    /// </summary>
    internal static partial class ArcChain3
    {

        /// <summary>
        /// Gets every point where a curved chain crosses a chain.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPolylineArc3 other)
            => GetIntersections(chain, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place the two meet, each named once; two edges lying along each other name none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPolylineArc3 other, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return CrossingsOfChains(chain.GetEdges(), other.GetEdges(), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a chain.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPolylineArc3 other) => CollidesWith(chain, other, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere, whether at a place or along a length; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPolylineArc3 other, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return TouchesChain(chain.GetEdges(), other.GetEdges(), tolerance);
        }

        /// <summary>
        /// Tries to find where a curved chain crosses a chain.
        /// </summary>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoPolylineArc3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(chain, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved chain crosses a chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoPolylineArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a curved chain crosses a loop.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPolygonArc3 other)
            => GetIntersections(chain, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a loop, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place the two meet, each named once; two edges lying along each other name none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPolygonArc3 other, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return CrossingsOfChains(chain.GetEdges(), other.GetEdges(), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a loop.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPolygonArc3 other) => CollidesWith(chain, other, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a loop, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere, whether at a place or along a length; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPolygonArc3 other, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return TouchesChain(chain.GetEdges(), other.GetEdges(), tolerance);
        }

        /// <summary>
        /// Tries to find where a curved chain crosses a loop.
        /// </summary>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoPolygonArc3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(chain, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved chain crosses a loop, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoPolygonArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a curved chain crosses a straight chain.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPolyline3 other)
            => GetIntersections(chain, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a straight chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The straight chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place the two meet, each named once; two edges lying along each other name none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPolyline3 other, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return CrossingsOfChains(chain.GetEdges(), EdgesOfStraight(other), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a straight chain.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPolyline3 other) => CollidesWith(chain, other, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a straight chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The straight chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere, whether at a place or along a length; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPolyline3 other, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return TouchesChain(chain.GetEdges(), EdgesOfStraight(other), tolerance);
        }

        /// <summary>
        /// Tries to find where a curved chain crosses a straight chain.
        /// </summary>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoPolyline3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(chain, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved chain crosses a straight chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="other">The straight chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc3 chain, GeoPolyline3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a chain.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPolylineArc3 other)
            => GetIntersections(loop, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a chain, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place the two meet, each named once; two edges lying along each other name none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPolylineArc3 other, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return CrossingsOfChains(loop.GetEdges(), other.GetEdges(), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a chain.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPolylineArc3 other) => CollidesWith(loop, other, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a chain, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere, whether at a place or along a length; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPolylineArc3 other, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return TouchesChain(loop.GetEdges(), other.GetEdges(), tolerance);
        }

        /// <summary>
        /// Tries to find where a curved loop crosses a chain.
        /// </summary>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoPolylineArc3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(loop, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved loop crosses a chain, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoPolylineArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a loop.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPolygonArc3 other)
            => GetIntersections(loop, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place the two meet, each named once; two edges lying along each other name none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPolygonArc3 other, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return CrossingsOfChains(loop.GetEdges(), other.GetEdges(), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a loop.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPolygonArc3 other) => CollidesWith(loop, other, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere, whether at a place or along a length; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPolygonArc3 other, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return TouchesChain(loop.GetEdges(), other.GetEdges(), tolerance);
        }

        /// <summary>
        /// Tries to find where a curved loop crosses a loop.
        /// </summary>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoPolygonArc3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(loop, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved loop crosses a loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoPolygonArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a straight chain.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPolyline3 other)
            => GetIntersections(loop, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a straight chain, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The straight chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every place the two meet, each named once; two edges lying along each other name none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPolyline3 other, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return CrossingsOfChains(loop.GetEdges(), EdgesOfStraight(other), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a straight chain.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPolyline3 other) => CollidesWith(loop, other, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a straight chain, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The straight chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet anywhere, whether at a place or along a length; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPolyline3 other, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return TouchesChain(loop.GetEdges(), EdgesOfStraight(other), tolerance);
        }

        /// <summary>
        /// Tries to find where a curved loop crosses a straight chain.
        /// </summary>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoPolyline3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(loop, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a curved loop crosses a straight chain, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="other">The straight chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc3 loop, GeoPolyline3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, other, tolerance);

            return intersections.Length > 0;
        }
    }
}
