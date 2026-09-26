using System;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// What a polygon in space can answer by working in its own plane: combining it with another flat shape lying in that same plane.
    /// </summary>
    /// <remarks>
    /// A shape that does not lie in this one's plane is <b>refused</b> rather than projected in, because
    /// projecting would report two plates a metre apart as overlapping and say nothing about it.
    /// <see cref="SharesPlaneWith(GeoPlane3)"/> asks the question beforehand. Everything comes back as
    /// <see cref="GeoFace3"/>, because joining two areas can leave a hole in the middle and only a face can
    /// hold one.
    /// </remarks>
    public sealed partial class GeoPolygon3
    {
        #region Combining with another shape in the same plane

        /// <summary>
        /// Determines whether a plane is the plane this polygon lies in.
        /// </summary>
        public bool SharesPlaneWith(GeoPlane3 other) => SharesPlaneWith(other, Tolerance.Global);

        /// <summary>
        /// Determines whether a plane is the plane this polygon lies in, within a tolerance.
        /// </summary>
        /// <param name="other">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the two are the same plane, so that work in this shape's frame is exact.</returns>
        /// <remarks>
        /// Parallel is not enough: two parallel planes a metre apart share nothing at all. Ask this before
        /// combining two shapes, which refuses a second shape lying anywhere else.
        /// </remarks>
        public bool SharesPlaneWith(GeoPlane3 other, Tolerance tolerance)
            => Boolean3.SharesPlane(GetPlane(), other, other.Origin, tolerance);

        /// <summary>
        /// Joins this polygon to a polygon.
        /// </summary>
        public GeoFace3[] Union(GeoPolygon3 other) => Union(other, Tolerance.Global);

        /// <summary>
        /// Joins this polygon to a polygon, within a tolerance.
        /// </summary>
        /// <param name="other">The polygon; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Union(GeoPolygon3 other, Tolerance tolerance) => Boolean3.Union(this, other, tolerance);

        /// <summary>
        /// Joins this polygon to a face.
        /// </summary>
        public GeoFace3[] Union(GeoFace3 other) => Union(other, Tolerance.Global);

        /// <summary>
        /// Joins this polygon to a face, within a tolerance.
        /// </summary>
        /// <param name="other">The face; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Union(GeoFace3 other, Tolerance tolerance) => Boolean3.Union(this, other, tolerance);

        /// <summary>
        /// Joins this polygon to a curved loop.
        /// </summary>
        public GeoFace3[] Union(GeoPolygonArc3 other) => Union(other, Tolerance.Global);

        /// <summary>
        /// Joins this polygon to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="other">The curved loop; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Union(GeoPolygonArc3 other, Tolerance tolerance) => new GeoPolygonArc3(this).Union(other, tolerance);

        /// <summary>
        /// Keeps what this polygon and a polygon both cover.
        /// </summary>
        public GeoFace3[] Intersect(GeoPolygon3 other) => Intersect(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this polygon and a polygon both cover, within a tolerance.
        /// </summary>
        /// <param name="other">The polygon; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Intersect(GeoPolygon3 other, Tolerance tolerance) => Boolean3.Intersect(this, other, tolerance);

        /// <summary>
        /// Keeps what this polygon and a face both cover.
        /// </summary>
        public GeoFace3[] Intersect(GeoFace3 other) => Intersect(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this polygon and a face both cover, within a tolerance.
        /// </summary>
        /// <param name="other">The face; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Intersect(GeoFace3 other, Tolerance tolerance) => Boolean3.Intersect(this, other, tolerance);

        /// <summary>
        /// Keeps what this polygon and a curved loop both cover.
        /// </summary>
        public GeoFace3[] Intersect(GeoPolygonArc3 other) => Intersect(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this polygon and a curved loop both cover, within a tolerance.
        /// </summary>
        /// <param name="other">The curved loop; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Intersect(GeoPolygonArc3 other, Tolerance tolerance) => new GeoPolygonArc3(this).Intersect(other, tolerance);

        /// <summary>
        /// Takes a polygon out of this polygon.
        /// </summary>
        public GeoFace3[] Subtract(GeoPolygon3 tool) => Subtract(tool, Tolerance.Global);

        /// <summary>
        /// Takes a polygon out of this polygon, within a tolerance.
        /// </summary>
        /// <param name="tool">The polygon; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Subtract(GeoPolygon3 tool, Tolerance tolerance) => Boolean3.Subtract(this, tool, tolerance);

        /// <summary>
        /// Takes a face out of this polygon.
        /// </summary>
        public GeoFace3[] Subtract(GeoFace3 tool) => Subtract(tool, Tolerance.Global);

        /// <summary>
        /// Takes a face out of this polygon, within a tolerance.
        /// </summary>
        /// <param name="tool">The face; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Subtract(GeoFace3 tool, Tolerance tolerance) => Boolean3.Subtract(this, tool, tolerance);

        /// <summary>
        /// Takes a curved loop out of this polygon.
        /// </summary>
        public GeoFace3[] Subtract(GeoPolygonArc3 tool) => Subtract(tool, Tolerance.Global);

        /// <summary>
        /// Takes a curved loop out of this polygon, within a tolerance.
        /// </summary>
        /// <param name="tool">The curved loop; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Subtract(GeoPolygonArc3 tool, Tolerance tolerance) => new GeoPolygonArc3(this).Subtract(tool, tolerance);

        /// <summary>
        /// Keeps what this polygon and a polygon cover between them but do not share.
        /// </summary>
        public GeoFace3[] Xor(GeoPolygon3 other) => Xor(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this polygon and a polygon cover between them but do not share, within a tolerance.
        /// </summary>
        /// <param name="other">The polygon; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Xor(GeoPolygon3 other, Tolerance tolerance) => Boolean3.Xor(this, other, tolerance);

        /// <summary>
        /// Keeps what this polygon and a face cover between them but do not share.
        /// </summary>
        public GeoFace3[] Xor(GeoFace3 other) => Xor(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this polygon and a face cover between them but do not share, within a tolerance.
        /// </summary>
        /// <param name="other">The face; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Xor(GeoFace3 other, Tolerance tolerance) => Boolean3.Xor(this, other, tolerance);

        /// <summary>
        /// Keeps what this polygon and a curved loop cover between them but do not share.
        /// </summary>
        public GeoFace3[] Xor(GeoPolygonArc3 other) => Xor(other, Tolerance.Global);

        /// <summary>
        /// Keeps what this polygon and a curved loop cover between them but do not share, within a tolerance.
        /// </summary>
        /// <param name="other">The curved loop; it has to lie in this polygon's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when it lies in another plane.</exception>
        public GeoFace3[] Xor(GeoPolygonArc3 other, Tolerance tolerance) => new GeoPolygonArc3(this).Xor(other, tolerance);

        #endregion
    }
}
