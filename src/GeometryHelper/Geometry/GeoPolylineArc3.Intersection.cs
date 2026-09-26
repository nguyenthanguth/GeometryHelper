using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a curved chain crosses another shape.
    /// </summary>
    public sealed partial class GeoPolylineArc3
    {
        /// <summary>
        /// Gets every point where this curved chain crosses a plane.
        /// </summary>
        /// <param name="plane">The plane.</param>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => ArcChain3.GetIntersections(this, plane);

        /// <summary>
        /// Gets every point where this curved chain crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => ArcChain3.GetIntersections(this, plane, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses a triangle.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle) => ArcChain3.GetIntersections(this, triangle);

        /// <summary>
        /// Gets every point where this curved chain crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoTriangle3 triangle, Tolerance tolerance) => ArcChain3.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses a polygon.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon) => ArcChain3.GetIntersections(this, polygon);

        /// <summary>
        /// Gets every point where this curved chain crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoPolygon3 polygon, Tolerance tolerance) => ArcChain3.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses a face.
        /// </summary>
        /// <param name="face">The face.</param>
        public GeoPoint3[] GetIntersections(GeoFace3 face) => ArcChain3.GetIntersections(this, face);

        /// <summary>
        /// Gets every point where this curved chain crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoFace3 face, Tolerance tolerance) => ArcChain3.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses an axis-aligned box.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        public GeoPoint3[] GetIntersections(GeoAabb3 box) => ArcChain3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this curved chain crosses an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The axis-aligned box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoAabb3 box, Tolerance tolerance) => ArcChain3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses an oriented box.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        public GeoPoint3[] GetIntersections(GeoObb3 box) => ArcChain3.GetIntersections(this, box);

        /// <summary>
        /// Gets every point where this curved chain crosses an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The oriented box.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoObb3 box, Tolerance tolerance) => ArcChain3.GetIntersections(this, box, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses a solid.
        /// </summary>
        /// <param name="solid">The solid.</param>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid) => ArcChain3.GetIntersections(this, solid);

        /// <summary>
        /// Gets every point where this curved chain crosses a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoSolid3 solid, Tolerance tolerance) => ArcChain3.GetIntersections(this, solid, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses an arc.
        /// </summary>
        /// <param name="arc">The arc.</param>
        public GeoPoint3[] GetIntersections(GeoArc3 arc) => ArcChain3.GetIntersections(this, arc);

        /// <summary>
        /// Gets every point where this curved chain crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoArc3 arc, Tolerance tolerance) => ArcChain3.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses a circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle) => ArcChain3.GetIntersections(this, circle);

        /// <summary>
        /// Gets every point where this curved chain crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoPoint3[] GetIntersections(GeoCircle3 circle, Tolerance tolerance) => ArcChain3.GetIntersections(this, circle, tolerance);

    }
}
