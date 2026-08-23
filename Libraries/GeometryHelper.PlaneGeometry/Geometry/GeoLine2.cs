using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.PlaneGeometry.Core;

namespace GeometryHelper.PlaneGeometry.Geometry
{
    /// <summary>
    /// Represents a 2D line segment between start and end points.
    /// </summary>
    public readonly struct GeoLine2 : IEquatable<GeoLine2>
    {
        /// <summary>
        /// Gets the start point of the line segment.
        /// </summary>
        public GeoPoint2 StartPoint { get; }

        /// <summary>
        /// Gets the end point of the line segment.
        /// </summary>
        public GeoPoint2 EndPoint { get; }

        /// <summary>
        /// Initializes a new GeoLine2 instance from start and end points.
        /// </summary>
        /// <param name="startPoint">Start point.</param>
        /// <param name="endPoint">End point.</param>
        public GeoLine2(GeoPoint2 startPoint, GeoPoint2 endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        /// <summary>
        /// Initializes a new GeoLine2 instance from the coordinates of the endpoints.
        /// </summary>
        public GeoLine2(double startX, double startY, double endX, double endY)
            : this(new GeoPoint2(startX, startY), new GeoPoint2(endX, endY))
        {
        }

        /// <summary>
        /// Creates a copy of this line segment.
        /// </summary>
        /// <remarks>
        /// Line segment is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        /// <returns>A new line segment with the same endpoints.</returns>
        public GeoLine2 Clone() => new GeoLine2(StartPoint, EndPoint);

        /// <summary>
        /// Gets the GeoVector2 pointing from start point to end point.
        /// </summary>
        public GeoVector2 Direction => StartPoint.GetVectorTo(EndPoint);

        /// <summary>
        /// Gets the length of the line segment.
        /// </summary>
        public double Length => StartPoint.DistanceTo(EndPoint);

        /// <summary>
        /// Gets the squared length of the line segment.
        /// </summary>
        public double LengthSquared => StartPoint.GetDistanceSquaredTo(EndPoint);

        /// <summary>
        /// Gets the midpoint of the line segment.
        /// </summary>
        public GeoPoint2 MidPoint => StartPoint.GetMiddlePoint(EndPoint);

        /// <summary>
        /// Gets the point on the line segment at parameter t (t=0 is the start point, t=1 is the end point).
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Parametrization2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point on this line segment closest to the supplied point.
        /// The point need not lie on the segment, so the result may fall outside [0, 1].
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Parametrization2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at an arc length measured from the start point of this line segment.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Parametrization2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from the start point of this line segment to the point on it closest to the
        /// supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Parametrization2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the start point of this line segment to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start point of this line segment.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization2.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Gets the closest point on this line segment to a target point, clamped to the endpoints.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Projection2.ProjectToLine(this, point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on another line segment using default tolerance.
        /// </summary>
        /// <param name="other">The other line segment.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the other line segment.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoLine2 other) => Projection2.GetClosestSegment(this, other, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on another line segment within tolerance.
        /// </summary>
        /// <param name="other">The other line segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the other line segment.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoLine2 other, Tolerance tolerance) => Projection2.GetClosestSegment(this, other, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on the circumference of a circle using default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the circumference of the circle.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoCircle2 circle) => Projection2.GetClosestSegment(this, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on the circumference of a circle within tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the circumference of the circle.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoCircle2 circle, Tolerance tolerance) => Projection2.GetClosestSegment(this, circle, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on the boundary of a rectangle using default tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the boundary of the rectangle.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoRectangle2 rect) => Projection2.GetClosestSegment(this, rect, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on the boundary of a rectangle within tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the boundary of the rectangle.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetClosestSegment(this, rect, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on a polyline using default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the polyline.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoPolyline2 polyline) => Projection2.GetClosestSegment(this, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on a polyline within tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the polyline.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetClosestSegment(this, polyline, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on the boundary of a polygon using default tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the boundary of the polygon.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoPolygon2 poly) => Projection2.GetClosestSegment(this, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on this line segment to a point on the boundary of a polygon within tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A <see cref="GeoLine2"/> connecting this line segment to the boundary of the polygon.</returns>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.PlaneGeometry.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetClosestOnBoundary(GeoPolygon2 poly, Tolerance tolerance) => Projection2.GetClosestSegment(this, poly, tolerance);

        /// <summary>
        /// Calculates the distance from a point to the closest point on this line segment.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this line segment to another line segment using default tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 other) => Distance2.DistanceTo(this, other, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this line segment to another line segment within tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 other, Tolerance tolerance) => Distance2.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this line segment to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the shortest distance from this line segment to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(poly, this);

        /// <summary>
        /// Calculates the shortest distance from this line segment to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the shortest distance from this line segment to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Checks whether a point lies on the line segment using default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 GeoPoint2) => IsPointOn(GeoPoint2, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on the line segment within tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 GeoPoint2, Tolerance tolerance) => Containment2.IsPointOn(this, GeoPoint2, tolerance);

        /// <summary>
        /// Classifies the location of a point relative to this line segment (OnSide or OutSide) using default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point, Tolerance.Global);

        /// <summary>
        /// Classifies the location of a point relative to this line segment (OnSide or OutSide) within tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this line segment collides / intersects with another line segment using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 other) => Collision2.CollidesWith(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment collides / intersects with another line segment within tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this line segment collides / intersects with a rectangle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Collision2.CollidesWith(rect, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment collides / intersects with a rectangle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Collision2.CollidesWith(rect, this, tolerance);

        /// <summary>
        /// Checks whether this line segment collides / intersects with a polygon using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly) => Collision2.CollidesWith(poly, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment collides / intersects with a polygon within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly, Tolerance tolerance) => Collision2.CollidesWith(poly, this, tolerance);

        /// <summary>
        /// Checks whether this line segment collides / intersects with a circle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Collision2.CollidesWith(circle, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment collides / intersects with a circle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Collision2.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this line segment collides / intersects with a polyline using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => Collision2.CollidesWith(polyline, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment collides / intersects with a polyline within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Collision2.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Tries to calculate the intersection with another line segment using default tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine2 other, out GeoPoint2 intersection) => TryIntersectWith(other, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to calculate the intersection with another line segment within tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine2 other, out GeoPoint2 intersection, Tolerance tolerance) => Intersection2.TryIntersectWith(this, other, out intersection, tolerance);

        /// <summary>
        /// Gets the intersection point with another line segment using default tolerance.
        /// Returns null if lines do not intersect.
        /// </summary>
        public GeoPoint2? GetIntersection(GeoLine2 other) => Intersection2.GetIntersection(this, other, Tolerance.Global);

        /// <summary>
        /// Gets the intersection point with another line segment within tolerance.
        /// Returns null if lines do not intersect.
        /// </summary>
        public GeoPoint2? GetIntersection(GeoLine2 other, Tolerance tolerance) => Intersection2.GetIntersection(this, other, tolerance);

        /// <summary>
        /// Gets all intersection points with a rectangle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Intersection2.GetIntersections(rect, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a rectangle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Intersection2.GetIntersections(rect, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a polygon using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly) => Intersection2.GetIntersections(poly, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polygon within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly, Tolerance tolerance) => Intersection2.GetIntersections(poly, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a circle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Intersection2.GetIntersections(circle, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a circle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Intersection2.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Gets all intersection points with a polyline using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(polyline, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polyline within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Splits this segment at a point lying on it, using the default tolerance.
        /// </summary>
        /// <param name="point">The point to split at, which must lie on this segment.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <returns>true if the segment was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint2 point, out GeoLine2 first, out GeoLine2 second)
            => Splition2.TrySplitBy(this, point, out first, out second, Tolerance.Global);

        /// <summary>
        /// Splits this segment at a point lying on it, within tolerance.
        /// </summary>
        /// <param name="point">The point to split at, which must lie on this segment.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segment was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint2 point, out GeoLine2 first, out GeoLine2 second, Tolerance tolerance)
            => Splition2.TrySplitBy(this, point, out first, out second, tolerance);

        /// <summary>
        /// Splits this segment where a cutting line segment crosses it, using the default tolerance.
        /// </summary>
        /// <param name="cutter">The cutting line segment.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <returns>true if the segment was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoLine2 cutter, out GeoLine2 first, out GeoLine2 second)
            => Splition2.TrySplitBy(this, cutter, out first, out second, Tolerance.Global);

        /// <summary>
        /// Splits this segment where a cutting line segment crosses it, within tolerance.
        /// </summary>
        /// <param name="cutter">The cutting line segment.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segment was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoLine2 cutter, out GeoLine2 first, out GeoLine2 second, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutter, out first, out second, tolerance);

        /// <summary>
        /// Splits this segment against a polygon, sorting the parts by which side of its boundary they
        /// fall on, using the default tolerance.
        /// </summary>
        /// <param name="cutter">The polygon to split against.</param>
        /// <param name="inside">The parts lying inside the polygon, in order along this segment.</param>
        /// <param name="outside">The parts lying outside the polygon, in order along this segment.</param>
        /// <returns>true if the polygon boundary crosses this segment; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolygon2 cutter, out GeoLine2[] inside, out GeoLine2[] outside)
            => Splition2.TrySplitBy(this, cutter, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Splits this segment against a polygon, sorting the parts by which side of its boundary they
        /// fall on, within tolerance.
        /// </summary>
        /// <param name="cutter">The polygon to split against.</param>
        /// <param name="inside">The parts lying inside the polygon, in order along this segment.</param>
        /// <param name="outside">The parts lying outside the polygon, in order along this segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygon boundary crosses this segment; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolygon2 cutter, out GeoLine2[] inside, out GeoLine2[] outside, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutter, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment everywhere a list of cutting line segments crosses it, using the default tolerance.
        /// </summary>
        /// <param name="cutters">The cutting line segments.</param>
        /// <param name="pieces">The resulting pieces in order along this segment.</param>
        /// <returns>true if this segment was split by at least one cutter; otherwise, false.</returns>
        public bool TrySplitBy(GeoLine2[] cutters, out GeoLine2[] pieces)
            => Splition2.TrySplitBy(this, cutters, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits this segment everywhere a list of cutting line segments crosses it, within tolerance.
        /// </summary>
        /// <param name="cutters">The cutting line segments.</param>
        /// <param name="pieces">The resulting pieces in order along this segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if this segment was split by at least one cutter; otherwise, false.</returns>
        public bool TrySplitBy(GeoLine2[] cutters, out GeoLine2[] pieces, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutters, out pieces, tolerance);

        /// <summary>
        /// Splits this segment everywhere a cutting polyline crosses it, using the default tolerance.
        /// </summary>
        /// <param name="cutter">The cutting polyline.</param>
        /// <param name="pieces">The resulting pieces in order along this segment.</param>
        /// <returns>true if this segment was split by the polyline; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolyline2 cutter, out GeoLine2[] pieces)
            => Splition2.TrySplitBy(this, cutter, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits this segment everywhere a cutting polyline crosses it, within tolerance.
        /// </summary>
        /// <param name="cutter">The cutting polyline.</param>
        /// <param name="pieces">The resulting pieces in order along this segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if this segment was split by the polyline; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolyline2 cutter, out GeoLine2[] pieces, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutter, out pieces, tolerance);

        /// <summary>
        /// Splits this segment everywhere a list of points lies on it, using the default tolerance.
        /// </summary>
        /// <param name="points">The points to split at.</param>
        /// <param name="pieces">The resulting pieces in order along this segment.</param>
        /// <returns>true if this segment was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint2[] points, out GeoLine2[] pieces)
            => Splition2.TrySplitBy(this, points, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits this segment everywhere a list of points lies on it, within tolerance.
        /// </summary>
        /// <param name="points">The points to split at.</param>
        /// <param name="pieces">The resulting pieces in order along this segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if this segment was split; otherwise, false.</returns>
        public bool TrySplitBy(GeoPoint2[] points, out GeoLine2[] pieces, Tolerance tolerance)
            => Splition2.TrySplitBy(this, points, out pieces, tolerance);

        /// <summary>
        /// Splits this segment everywhere a list of polygon boundaries crosses it, separating the parts that fall
        /// inside the polygons from those that fall outside, using the default tolerance.
        /// </summary>
        /// <param name="cutters">The polygons to split against.</param>
        /// <param name="inside">The parts lying inside the polygons, in order along this segment.</param>
        /// <param name="outside">The parts lying outside the polygons, in order along this segment.</param>
        /// <returns>true if at least one polygon boundary crosses this segment; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolygon2[] cutters, out GeoLine2[] inside, out GeoLine2[] outside)
            => Splition2.TrySplitBy(this, cutters, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Splits this segment everywhere a list of polygon boundaries crosses it, separating the parts that fall
        /// inside the polygons from those that fall outside, within tolerance.
        /// </summary>
        /// <param name="cutters">The polygons to split against.</param>
        /// <param name="inside">The parts lying inside the polygons, in order along this segment.</param>
        /// <param name="outside">The parts lying outside the polygons, in order along this segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if at least one polygon boundary crosses this segment; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolygon2[] cutters, out GeoLine2[] inside, out GeoLine2[] outside, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutters, out inside, out outside, tolerance);

        /// <summary>
        /// Splits this segment everywhere a list of polylines crosses it, using the default tolerance.
        /// </summary>
        /// <param name="cutters">The cutting polylines.</param>
        /// <param name="pieces">The resulting pieces in order along this segment.</param>
        /// <returns>true if at least one polyline crosses this segment; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolyline2[] cutters, out GeoLine2[] pieces)
            => Splition2.TrySplitBy(this, cutters, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits this segment everywhere a list of polylines crosses it, within tolerance.
        /// </summary>
        /// <param name="cutters">The cutting polylines.</param>
        /// <param name="pieces">The resulting pieces in order along this segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if at least one polyline crosses this segment; otherwise, false.</returns>
        public bool TrySplitBy(GeoPolyline2[] cutters, out GeoLine2[] pieces, Tolerance tolerance)
            => Splition2.TrySplitBy(this, cutters, out pieces, tolerance);

        /// <summary>
        /// Splits this segment at an arc length measured from its start point, using the default tolerance.
        /// </summary>
        /// <param name="distance">Arc length from the start point.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <returns>true if the segment was split; otherwise, false.</returns>
        public bool TrySplitAtDistance(double distance, out GeoLine2 first, out GeoLine2 second)
            => Splition2.TrySplitAtDistance(this, distance, out first, out second, Tolerance.Global);

        /// <summary>
        /// Splits this segment at an arc length measured from its start point, within tolerance.
        /// </summary>
        /// <param name="distance">Arc length from the start point.</param>
        /// <param name="first">The piece holding the start point.</param>
        /// <param name="second">The piece holding the end point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segment was split; otherwise, false.</returns>
        public bool TrySplitAtDistance(double distance, out GeoLine2 first, out GeoLine2 second, Tolerance tolerance)
            => Splition2.TrySplitAtDistance(this, distance, out first, out second, tolerance);

        /// <summary>
        /// Splits this segment at several arc lengths measured from its start point, using the default
        /// tolerance.
        /// </summary>
        /// <param name="distances">Arc lengths from the start point, in any order.</param>
        /// <returns>The pieces in order along this segment.</returns>
        public GeoLine2[] SplitAtDistances(IEnumerable<double> distances)
            => Splition2.SplitAtDistances(this, distances, Tolerance.Global);

        /// <summary>
        /// Splits this segment at several arc lengths measured from its start point, within tolerance.
        /// </summary>
        /// <param name="distances">Arc lengths from the start point, in any order.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces in order along this segment.</returns>
        public GeoLine2[] SplitAtDistances(IEnumerable<double> distances, Tolerance tolerance)
            => Splition2.SplitAtDistances(this, distances, tolerance);

        /// <summary>
        /// Checks whether this line segment is parallel to another line segment using default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine2 other) => Parallel2.IsParallel(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment is parallel to another line segment within angular tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine2 other, Tolerance tolerance) => Parallel2.IsParallel(this, other, tolerance);

        /// <summary>
        /// Checks whether this line segment is parallel to a vector using default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoVector2 vector) => Parallel2.IsParallel(this, vector, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment is parallel to a vector within angular tolerance.
        /// </summary>
        public bool IsParallelTo(GeoVector2 vector, Tolerance tolerance) => Parallel2.IsParallel(this, vector, tolerance);

        /// <summary>
        /// Checks whether this line segment is parallel to any edge of a rotated rectangle using default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoRectangle2 rect) => Parallel2.IsParallel(rect, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment is parallel to any edge of a rotated rectangle within angular tolerance.
        /// </summary>
        public bool IsParallelTo(GeoRectangle2 rect, Tolerance tolerance) => Parallel2.IsParallel(rect, this, tolerance);

        /// <summary>
        /// Checks whether this line segment is perpendicular to another line segment using default tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoLine2 other) => Parallel2.IsPerpendicular(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment is perpendicular to another line segment within angular tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoLine2 other, Tolerance tolerance) => Parallel2.IsPerpendicular(this, other, tolerance);

        /// <summary>
        /// Checks whether this line segment is perpendicular to a vector using default tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoVector2 vector) => Parallel2.IsPerpendicular(this, vector, Tolerance.Global);

        /// <summary>
        /// Checks whether this line segment is perpendicular to a vector within angular tolerance.
        /// </summary>
        public bool IsPerpendicularTo(GeoVector2 vector, Tolerance tolerance) => Parallel2.IsPerpendicular(this, vector, tolerance);

        /// <summary>
        /// Indicates whether the current line segment is equal to another line segment.
        /// </summary>
        /// <param name="other">A line segment to compare with this line segment.</param>
        /// <returns>true if the current line segment is equal to the other parameter; otherwise, false.</returns>
        public bool Equals(GeoLine2 other) => StartPoint.Equals(other.StartPoint) && EndPoint.Equals(other.EndPoint);

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is GeoLine2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (StartPoint.GetHashCode() * 397) ^ EndPoint.GetHashCode();
            }
        }

        /// <summary>
        /// Compares two GeoLine2 instances for equality.
        /// </summary>
        /// <param name="left">The first line segment.</param>
        /// <param name="right">The second line segment.</param>
        /// <returns>true if they are equal; otherwise, false.</returns>
        public static bool operator ==(GeoLine2 left, GeoLine2 right) => left.Equals(right);

        /// <summary>
        /// Compares two GeoLine2 instances for inequality.
        /// </summary>
        /// <param name="left">The first line segment.</param>
        /// <param name="right">The second line segment.</param>
        /// <returns>true if they are not equal; otherwise, false.</returns>
        public static bool operator !=(GeoLine2 left, GeoLine2 right) => !left.Equals(right);

        /// <summary>
        /// Returns the string representation of the line segment.
        /// </summary>
        /// <returns>A string representation of the start and end points of the line.</returns>
        public override string ToString() => $"GeoLine2[{StartPoint} -> {EndPoint}]";
    }
}
