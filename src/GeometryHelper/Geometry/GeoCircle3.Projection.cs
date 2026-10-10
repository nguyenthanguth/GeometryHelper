using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a circle nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoCircle3
    {
        /// <summary>
        /// Gets the point on the circumference closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToCircle(this, point);

        /// <summary>
        /// Gets the circle seen square onto a plane: every point of it projected along the plane's normal, which is an
        /// ellipse. A circle of radius 300 tilted 60° from the plan is, in the plan, an ellipse 300 by 150.
        /// </summary>
        /// <param name="plane">The plane to project onto.</param>
        /// <returns>The ellipse in that plane, its normal the plane's; a circle when this one lies parallel to the plane, its angle starting where this one's does.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the circle stands square to the plane, where it would flatten into a segment rather than an ellipse.</exception>
        /// <remarks>See <see cref="Ellipse3.ProjectToPlane(GeoEllipse3, GeoPlane3)"/>.</remarks>
        public GeoEllipse3 ProjectToPlane(GeoPlane3 plane) => Ellipse3.ProjectToPlane(this, plane);

        /// <summary>
        /// Lays this circle out in the plane of a frame, projecting it square onto that plane: an ellipse, or a circle where
        /// this one lies in or parallel to the plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <returns>The ellipse in the plane of the frame.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the circle stands square to the plane, where it would flatten into a segment rather than an ellipse.</exception>
        public GeoEllipse2 ProjectToEllipse2(GeoCoordinateSystem3 frame) => PlanarMap.ProjectToEllipse2(frame, this);
    }
}
