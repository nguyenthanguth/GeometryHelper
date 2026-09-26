using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a curved chain or loop in space crosses another shape, and whether it touches one.
    /// </summary>
    internal static partial class ArcChain3
    {
        /// <summary>
        /// Gets every point where a curved chain crosses a plane.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPlane3 plane)
            => GetIntersections(chain, plane, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPlane3 plane, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(plane, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a plane.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPlane3 plane) => CollidesWith(chain, plane, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a plane, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPlane3 plane, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(plane, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved chain crosses a triangle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoTriangle3 triangle)
            => GetIntersections(chain, triangle, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(triangle, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a triangle.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoTriangle3 triangle) => CollidesWith(chain, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(triangle, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved chain crosses a polygon.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPolygon3 polygon)
            => GetIntersections(chain, polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(polygon, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a polygon.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPolygon3 polygon) => CollidesWith(chain, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(polygon, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved chain crosses a face.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoFace3 face)
            => GetIntersections(chain, face, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a face, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoFace3 face, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(face, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a face.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoFace3 face) => CollidesWith(chain, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a face, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoFace3 face, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(face, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved chain crosses an axis-aligned box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoAabb3 box)
            => GetIntersections(chain, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoAabb3 box, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(box, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches an axis-aligned box.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoAabb3 box) => CollidesWith(chain, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoAabb3 box, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(box, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved chain crosses an oriented box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoObb3 box)
            => GetIntersections(chain, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses an oriented box, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoObb3 box, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(box, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches an oriented box.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoObb3 box) => CollidesWith(chain, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoObb3 box, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(box, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved chain crosses a solid.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoSolid3 solid)
            => GetIntersections(chain, solid, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a solid, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoSolid3 solid, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(solid, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a solid.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoSolid3 solid) => CollidesWith(chain, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a solid, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoSolid3 solid, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(solid, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved chain crosses an arc.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoArc3 arc)
            => GetIntersections(chain, arc, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoArc3 arc, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(arc, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches an arc.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoArc3 arc) => CollidesWith(chain, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches an arc, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoArc3 arc, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(arc, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved chain crosses a circle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoCircle3 circle)
            => GetIntersections(chain, circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoCircle3 circle, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Crossings(chain.GetEdges(), edge => edge.GetIntersections(circle, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved chain touches a circle.
        /// </summary>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoCircle3 circle) => CollidesWith(chain, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved chain touches a circle, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc3 chain, GeoCircle3 circle, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            return Touches(chain.GetEdges(), edge => edge.CollidesWith(circle, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a plane.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPlane3 plane)
            => GetIntersections(loop, plane, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPlane3 plane, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(plane, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a plane.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPlane3 plane) => CollidesWith(loop, plane, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a plane, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPlane3 plane, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(plane, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a triangle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoTriangle3 triangle)
            => GetIntersections(loop, triangle, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(triangle, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a triangle.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoTriangle3 triangle) => CollidesWith(loop, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(triangle, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a polygon.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPolygon3 polygon)
            => GetIntersections(loop, polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(polygon, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a polygon.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPolygon3 polygon) => CollidesWith(loop, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(polygon, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a face.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoFace3 face)
            => GetIntersections(loop, face, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a face, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoFace3 face, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(face, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a face.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoFace3 face) => CollidesWith(loop, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a face, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoFace3 face, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(face, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses an axis-aligned box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoAabb3 box)
            => GetIntersections(loop, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoAabb3 box, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(box, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches an axis-aligned box.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoAabb3 box) => CollidesWith(loop, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoAabb3 box, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(box, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses an oriented box.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoObb3 box)
            => GetIntersections(loop, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses an oriented box, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoObb3 box, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(box, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches an oriented box.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoObb3 box) => CollidesWith(loop, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoObb3 box, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(box, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a solid.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoSolid3 solid)
            => GetIntersections(loop, solid, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a solid, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoSolid3 solid, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(solid, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a solid.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoSolid3 solid) => CollidesWith(loop, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a solid, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoSolid3 solid, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(solid, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses an arc.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoArc3 arc)
            => GetIntersections(loop, arc, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoArc3 arc, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(arc, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches an arc.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoArc3 arc) => CollidesWith(loop, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches an arc, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoArc3 arc, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(arc, tolerance));
        }

        /// <summary>
        /// Gets every point where a curved loop crosses a circle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoCircle3 circle)
            => GetIntersections(loop, circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoCircle3 circle, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Crossings(loop.GetEdges(), edge => edge.GetIntersections(circle, tolerance), tolerance);
        }

        /// <summary>
        /// Determines whether a curved loop touches a circle.
        /// </summary>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoCircle3 circle) => CollidesWith(loop, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether a curved loop touches a circle, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc3 loop, GeoCircle3 circle, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }
            return Touches(loop.GetEdges(), edge => edge.CollidesWith(circle, tolerance));
        }

    }
}
