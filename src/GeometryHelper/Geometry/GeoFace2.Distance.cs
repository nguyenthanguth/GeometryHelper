using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a face is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoFace2
    {
        /// <summary>
        /// Calculates the shortest distance from this face to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read as filled, so a point on the material is nought away and a point in one of
        /// its holes is measured to the rim it sits in.
        /// </remarks>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this face to a point, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the distance from this face to a point, negative for a point on the material.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest edge, its outline or the rim of a hole, measured exactly, negative when the
        /// point lies on the material and nought when the global point tolerance calls it on an edge.
        /// </returns>
        /// <remarks>
        /// The magnitude is the distance to the boundary, whichever side of it the point is on, and the sign
        /// says which side: negative on the material, nought on an edge, positive off it.
        /// <see cref="DistanceTo(GeoPoint2)"/> reads the face as filled and so answers nothing at all for a
        /// point on the material, which is the one place the two part company. The boundary of a face is its
        /// outline and the rim of every hole, so a point in a hole is off the material.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Distance2.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this face to a point, negative for a point on the material, within a tolerance.
        /// </summary>
        /// <returns>
        /// The distance out to the nearest edge, its outline or the rim of a hole, measured exactly, negative when the
        /// point lies on the material and nought when the tolerance given calls it on an edge.
        /// </returns>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to an arc.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The upper half of the
        /// circle of radius 10 about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and
        /// a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        public double DistanceTo(GeoArc2 arc) => Face2.DistanceTo(this, arc);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to an arc, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The upper
        /// half of the circle of radius 10 about (50, 50) lies in the hole of the square 100 by 100 with a corner at
        /// the origin and a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Face2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a circle.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The circle of radius
        /// 10 about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by 40
        /// in its middle and is 10 from its rim; about (150, 50) it is 40 from the outline; about (15, 50), and of
        /// radius 200 about (50, 50), holding the whole face, it is nought away.
        /// </remarks>
        public double DistanceTo(GeoCircle2 circle) => Face2.DistanceTo(this, circle);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a circle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The circle
        /// of radius 10 about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole
        /// 40 by 40 in its middle and is 10 from its rim; about (150, 50) it is 40 from the outline; about (15, 50),
        /// and of radius 200 about (50, 50), holding the whole face, it is nought away.
        /// </remarks>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Face2.DistanceTo(this, circle, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to an edge.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The straight edge from
        /// (40, 50) to (60, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by
        /// 40 in its middle and is 10 from its rim.
        /// </remarks>
        public double DistanceTo(GeoEdge2 edge) => Face2.DistanceTo(this, edge);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to an edge, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The straight
        /// edge from (40, 50) to (60, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a
        /// hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance) => Face2.DistanceTo(this, edge, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a segment.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The segment from (40,
        /// 50) to (60, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by 40 in
        /// its middle and is 10 from its rim; the one from (10, 50) to (20, 50) lies on the material and is nought
        /// away.
        /// </remarks>
        public double DistanceTo(GeoLine2 line) => Face2.DistanceTo(this, line);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a segment, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The segment
        /// from (40, 50) to (60, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole
        /// 40 by 40 in its middle and is 10 from its rim; the one from (10, 50) to (20, 50) lies on the material and is
        /// nought away.
        /// </remarks>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Face2.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a polygon.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The square 20 by 20
        /// about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by 40 in
        /// its middle and is 10 from its rim; about (130, 50) it is 20 from the outline.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 polygon) => Face2.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a polygon, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The
        /// square 20 by 20 about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a
        /// hole 40 by 40 in its middle and is 10 from its rim; about (130, 50) it is 20 from the outline.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 polygon, Tolerance tolerance)
            => Face2.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a curved loop.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The loop with
        /// corners (40, 40), (60, 40), (60, 60), (40, 60) and straight edges lies in the hole of the square 100 by 100
        /// with a corner at the origin and a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop) => Face2.DistanceTo(this, loop);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a curved loop, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The
        /// loop with corners (40, 40), (60, 40), (60, 60), (40, 60) and straight edges lies in the hole of the square
        /// 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Face2.DistanceTo(this, loop, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a polyline.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The chain (40, 40),
        /// (60, 40), (60, 60) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by 40
        /// in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline) => Face2.DistanceTo(this, polyline);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a polyline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The
        /// chain (40, 40), (60, 40), (60, 60) lies in the hole of the square 100 by 100 with a corner at the origin and
        /// a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance)
            => Face2.DistanceTo(this, polyline, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a curved chain.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The straight
        /// chain (40, 40), (60, 40), (60, 60) lies in the hole of the square 100 by 100 with a corner at the origin and
        /// a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain) => Face2.DistanceTo(this, chain);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a curved chain, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The
        /// straight chain (40, 40), (60, 40), (60, 60) lies in the hole of the square 100 by 100 with a corner at the
        /// origin and a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance)
            => Face2.DistanceTo(this, chain, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a rectangle.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The rectangle 20
        /// by 20 about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40 by
        /// 40 in its middle and is 10 from its rim.
        /// </remarks>
        public double DistanceTo(GeoRectangle2 rect) => Face2.DistanceTo(this, rect);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a rectangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The
        /// rectangle 20 by 20 about (50, 50) lies in the hole of the square 100 by 100 with a corner at the origin and
        /// a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Face2.DistanceTo(this, rect, tolerance);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a triangle.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The triangle (40,
        /// 40), (60, 40), (50, 60) lies in the hole of the square 100 by 100 with a corner at the origin and a hole 40
        /// by 40 in its middle and is 10 from its rim.
        /// </remarks>
        public double DistanceTo(GeoTriangle2 triangle) => Face2.DistanceTo(this, triangle);

        /// <summary>
        /// Calculates the shortest distance from the material of this face to a triangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. The
        /// triangle (40, 40), (60, 40), (50, 60) lies in the hole of the square 100 by 100 with a corner at the origin
        /// and a hole 40 by 40 in its middle and is 10 from its rim.
        /// </remarks>
        public double DistanceTo(GeoTriangle2 triangle, Tolerance tolerance)
            => Face2.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance between the materials of this face and another.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. A square 20 by 20 about
        /// (50, 50) is 10 from the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle, the
        /// width of the gap round it in the hole; about (130, 50) it is 20 away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other face is null.</exception>
        public double DistanceTo(GeoFace2 other) => Face2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance between the materials of this face and another, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material, its outline less its holes: nought where the two meet, otherwise the
        /// gap between the boundaries, measured exactly. A square 20
        /// by 20 about (50, 50) is 10 from the square 100 by 100 with a corner at the origin and a hole 40 by 40 in its
        /// middle, the width of the gap round it in the hole; about (130, 50) it is 20 away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other face is null.</exception>
        public double DistanceTo(GeoFace2 other, Tolerance tolerance) => Face2.DistanceTo(this, other, tolerance);

        #region Rays

        /// <summary>
        /// Gets the distance from the material of this face to a ray, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay2 ray) => Face2.DistanceTo(this, ray);

        /// <summary>
        /// Gets the distance from the material of this face to a ray, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay2 ray, Tolerance tolerance) => Face2.DistanceTo(this, ray, tolerance);

        #endregion

        #region XLines

        /// <summary>
        /// Calculates the shortest distance from this face to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this face to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine2 xline, Tolerance tolerance) => Face2.DistanceTo(this, xline, tolerance);

        #endregion
    }
}
