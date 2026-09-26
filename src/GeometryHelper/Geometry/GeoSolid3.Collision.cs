using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a solid touches another shape.
    /// </summary>
    public sealed partial class GeoSolid3
    {
        /// <summary>
        /// Determines whether this solid touches or overlaps a line segment.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps a line segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps a polyline.
        /// </summary>
        public bool CollidesWith(GeoPolyline3 polyline) => Collision3.CollidesWith(polyline, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps a polyline, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance) => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps a polygon.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(polygon, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(polygon, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps a face.
        /// </summary>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(face, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps an oriented box.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(box, this);

        /// <summary>
        /// Determines whether this solid touches or overlaps an oriented box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(box, this, tolerance);

        /// <summary>
        /// Determines whether this solid touches or overlaps another solid.
        /// </summary>
        public bool CollidesWith(GeoSolid3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Determines whether this solid touches or overlaps another solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One body sitting wholly inside the other counts as touching, even where no two faces meet.
        /// </remarks>
        public bool CollidesWith(GeoSolid3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Determines whether this solid is reached by a ray.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Determines whether this solid is reached by a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);

        /// <summary>
        /// Checks whether this solid touches an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(box, this);

        /// <summary>
        /// Checks whether this solid touches an axis-aligned box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(box, this, tolerance);
    }
}
