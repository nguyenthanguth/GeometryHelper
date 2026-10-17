using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a polygon is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolygon2
    {
        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to another polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the shortest boundary distance from this polygon to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(this, line);

        /// <summary>
        /// Calculates the shortest distance from this polygon boundary to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this polygon to a point, negative for a point within it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the outline, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint2)"/>
        /// reads this polygon as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Distance2.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this polygon to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polygon to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Distance2.DistanceTo(this, arc);

        /// <summary>
        /// Calculates the shortest distance from this polygon to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Distance2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a curved loop.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(loop, this);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a curved chain.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(chain, this);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Gets the distance from this polygon to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge) => ArcChain2.Edge(edge, DistanceTo, DistanceTo, DistanceTo);

        #region Triangles

        /// <summary>
        /// Gets the distance from this polygon to a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle2 triangle) => Triangle2.DistanceTo(triangle, this);

        #endregion

        /// <summary>
        /// Calculates the shortest distance from this polygon to the material of a face: the same answer as
        /// <see cref="GeoFace2.DistanceTo(GeoPolygon2)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this polygon is nought from it where the
        /// two meet, and as far as the nearest rim where it lies wholly in a hole. The square 20 by 20 about (50,
        /// 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle
        /// and is 10 from its rim; about (130, 50) it is 20 from the outline.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face) => Face2.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance from this polygon to the material of a face, within a tolerance: the same
        /// answer as <see cref="GeoFace2.DistanceTo(GeoPolygon2, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material, its outline less its holes, so this polygon is nought from it where the
        /// two meet, and as far as the nearest rim where it lies wholly in a hole. The square 20 by 20 about (50,
        /// 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle
        /// and is 10 from its rim; about (130, 50) it is 20 from the outline.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Face2.DistanceTo(face, this, tolerance);

        #region Rays

        /// <summary>
        /// Calculates the shortest distance from this polygon to a ray, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoRay2 ray) => Distance2.DistanceTo(this, ray);

        /// <summary>
        /// Calculates the shortest distance from this polygon to a ray, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRay2 ray, Tolerance tolerance) => Distance2.DistanceTo(this, ray, tolerance);

        #endregion
    }
}
