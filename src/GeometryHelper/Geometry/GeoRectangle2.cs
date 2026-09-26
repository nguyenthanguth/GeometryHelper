using System;
using System.Linq;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a 2D Oriented Bounding Box (OBB) that can be rotated.
    /// </summary>
    public readonly struct GeoRectangle2 : IEquatable<GeoRectangle2>
    {
        /// <summary>
        /// Gets the center point of the rectangle.
        /// </summary>
        public GeoPoint2 Center { get; }

        /// <summary>
        /// Gets the width of the rectangle (along its local X-axis).
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Gets the height of the rectangle (along its local Y-axis).
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Gets the rotation angle in radians (counter-clockwise).
        /// </summary>
        public double AngleRad { get; }

        /// <summary>
        /// Gets the rotation angle in degrees (counter-clockwise).
        /// </summary>
        public double AngleDeg => Angle.ToDegrees(AngleRad);

        /// <summary>
        /// Gets a value indicating whether the rectangle is rotated, that is whether its rotation angle
        /// differs from zero by more than the angular tolerance once full turns are removed.
        /// </summary>
        public bool IsRotated => Math.Abs(Angle.FromRadians(AngleRad).NormalizeSigned().Radians) > Tolerance.Global.EqualAngleRad;

        /// <summary>
        /// Gets the perimeter (total boundary length) of the rectangle.
        /// </summary>
        public double Length => 2.0 * (Width + Height);

        /// <summary>
        /// Initializes a new GeoRectangle2 instance from center, width, height, and rotation angle.
        /// </summary>
        /// <param name="center">Center point of the rectangle.</param>
        /// <param name="width">Rectangle width.</param>
        /// <param name="height">Rectangle height.</param>
        /// <param name="angleRad">Rotation angle in radians.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when width or height is negative.</exception>
        public GeoRectangle2(GeoPoint2 center, double width, double height, double angleRad = 0.0)
        {
            if (width < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width cannot be negative.");
            }

            if (height < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height cannot be negative.");
            }

            Center = center;
            Width = width;
            Height = height;
            AngleRad = angleRad;
        }

        /// <summary>
        /// Gets the local coordinate system of the rectangle: its centre and its two orthonormal axes.
        /// </summary>
        /// <remarks>
        /// The counterpart of <see cref="GeoObb3.CoordinateSystem"/> in the plane, and the way to read a
        /// point in the rectangle's own coordinates: <c>rectangle.CoordinateSystem.ToLocal(point)</c> gives
        /// it measured along the sides, with the centre at nought.
        /// </remarks>
        public GeoCoordinateSystem2 CoordinateSystem => new GeoCoordinateSystem2(Center, AngleRad);

        /// <summary>
        /// Initializes a rectangle from its own coordinate system and its size along each of its axes.
        /// </summary>
        /// <param name="coordinateSystem">Where the rectangle sits and which way it is turned.</param>
        /// <param name="width">The size along the system's X axis.</param>
        /// <param name="height">The size along the system's Y axis.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a size is negative or not a number.</exception>
        public GeoRectangle2(GeoCoordinateSystem2 coordinateSystem, double width, double height)
            : this(coordinateSystem.Origin, width, height, coordinateSystem.AngleRad)
        {
        }

        /// <summary>
        /// Initializes a new GeoRectangle2 instance from the bottom-left corner position (unrotated) and dimensions.
        /// </summary>
        /// <param name="x">X coordinate of the bottom-left corner when unrotated.</param>
        /// <param name="y">Y coordinate of the bottom-left corner when unrotated.</param>
        /// <param name="width">Rectangle width.</param>
        /// <param name="height">Rectangle height.</param>
        public GeoRectangle2(double x, double y, double width, double height)
            : this(new GeoPoint2(x + width * 0.5, y + height * 0.5), width, height, 0.0)
        {
        }

        /// <summary>
        /// Creates a copy of this rectangle.
        /// </summary>
        /// <remarks>
        /// Rectangle is a readonly struct, so plain assignment already produces an independent copy and
        /// this method is not needed to avoid sharing. It exists so that every geometry type offers the
        /// same way to ask for a copy.
        /// </remarks>
        /// <returns>A new rectangle with the same center, size, and rotation.</returns>
        public GeoRectangle2 Clone() => new GeoRectangle2(Center, Width, Height, AngleRad);

        /// <summary>
        /// Converts this rectangle into a solid 2D GeoPolygon2.
        /// </summary>
        /// <returns>A new GeoPolygon2 instance representing this rectangle.</returns>
        public GeoPolygon2 ToPolygon()
        {
            return new GeoPolygon2(GetVertices());
        }

        /// <summary>
        /// Converts this rectangle's boundary into a closed 2D GeoPolyline2.
        /// The boundary is closed by repeating the first vertex at the end of the chain.
        /// </summary>
        /// <returns>A new GeoPolyline2 instance representing the rectangle boundary.</returns>
        public GeoPolyline2 ToPolyline()
        {
            GeoPoint2[] v = GetVertices();
            var polylineVertices = new GeoPoint2[5];
            Array.Copy(v, polylineVertices, 4);
            polylineVertices[4] = v[0];
            return new GeoPolyline2(polylineVertices);
        }

        /// <summary>
        /// Gets the point at a normalized parameter along this rectangle perimeter, where 0 is the LowerLeft corner and 1 is the end.
        /// Values outside [0, 1] wrap around, so 1.25 is the same position as 0.25.
        /// </summary>
        public GeoPoint2 GetPointAtParameter(double parameter) => Parametrization2.GetPointAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter of the point on this rectangle perimeter closest to the supplied point.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint2 point) => Parametrization2.GetParameterAtPoint(this, point);

        /// <summary>
        /// Gets the point at an arc length measured from the LowerLeft corner of this rectangle perimeter.
        /// </summary>
        public GeoPoint2 GetPointAtDistance(double distance) => Parametrization2.GetPointAtDistance(this, distance);

        /// <summary>
        /// Gets the arc length from the LowerLeft corner of this rectangle perimeter to the point on it closest to the supplied point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => Parametrization2.GetDistanceAtPoint(this, point);

        /// <summary>
        /// Gets the arc length from the LowerLeft corner of this rectangle perimeter to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => Parametrization2.GetDistanceAtParameter(this, parameter);

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the LowerLeft corner of this rectangle perimeter.
        /// </summary>
        public double GetParameterAtDistance(double distance) => Parametrization2.GetParameterAtDistance(this, distance);

        /// <summary>
        /// Translates the rectangle by a displacement vector.
        /// </summary>
        /// <param name="vector">The displacement vector.</param>
        /// <returns>A new translated GeoRectangle2 keeping the same size and rotation.</returns>
        public GeoRectangle2 Translate(GeoVector2 vector) => new GeoRectangle2(Center.Add(vector), Width, Height, AngleRad);

        /// <summary>
        /// Rotates the rectangle around a center point by an angle in radians (counter-clockwise).
        /// </summary>
        /// <param name="angleRad">Rotation angle in radians.</param>
        /// <param name="center">Center of rotation.</param>
        /// <returns>A new rotated GeoRectangle2.</returns>
        public GeoRectangle2 RotateBy(double angleRad, GeoPoint2 center) => new GeoRectangle2(Center.RotateBy(angleRad, center), Width, Height, AngleRad + angleRad);

        /// <summary>
        /// Grows this rectangle by a distance on every side, or shrinks it when the distance is negative,
        /// keeping its corners square, using default tolerance.
        /// </summary>
        /// <param name="distance">How far each side moves: outward when positive.</param>
        /// <param name="result">The rectangle with the same center and angle, or this one when the method returns false.</param>
        /// <returns>true if a rectangle is left; false if it shrinks to nothing.</returns>
        public bool TryOffset(double distance, out GeoRectangle2 result) => Offset2.TryOffset(this, distance, out result, Tolerance.Global);

        /// <summary>
        /// Grows this rectangle by a distance on every side, or shrinks it when the distance is negative,
        /// keeping its corners square, within tolerance.
        /// </summary>
        /// <param name="distance">How far each side moves: outward when positive.</param>
        /// <param name="result">The rectangle with the same center and angle, or this one when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if a rectangle is left; false if it shrinks to nothing.</returns>
        public bool TryOffset(double distance, out GeoRectangle2 result, Tolerance tolerance) => Offset2.TryOffset(this, distance, out result, tolerance);

        /// <summary>
        /// Combines this rectangle with another rectangle, returning a new rectangle that encloses both.
        /// The resulting rectangle maintains the same orientation (angle) as this rectangle.
        /// </summary>
        /// <param name="other">The other rectangle to combine with.</param>
        /// <returns>A new GeoRectangle2 containing both rectangles, oriented along this rectangle's axis.</returns>
        public GeoRectangle2 Combine(GeoRectangle2 other) => Combine((IEnumerable<GeoPoint2>)other.GetVertices());

        /// <summary>
        /// Combines this rectangle with a point, returning a new rectangle that encloses both.
        /// The resulting rectangle maintains the same orientation (angle) as this rectangle.
        /// </summary>
        /// <param name="point">The point to combine with.</param>
        /// <returns>A new GeoRectangle2 containing this rectangle and the point, oriented along this rectangle's axis.</returns>
        public GeoRectangle2 Combine(GeoPoint2 point)
        {
            double cos = Math.Cos(AngleRad);
            double sin = Math.Sin(AngleRad);

            double minX = Width * -0.5;
            double maxX = Width * 0.5;
            double minY = Height * -0.5;
            double maxY = Height * 0.5;

            ExpandLocalBounds(point, cos, sin, ref minX, ref maxX, ref minY, ref maxY);

            return FromLocalBounds(minX, maxX, minY, maxY, cos, sin);
        }

        /// <summary>
        /// Combines this rectangle with an array of points, returning a new rectangle that encloses this rectangle and all points.
        /// The resulting rectangle maintains the same orientation (angle) as this rectangle.
        /// </summary>
        /// <param name="points">The points to combine with. A null or empty array leaves the rectangle unchanged.</param>
        /// <returns>A new GeoRectangle2 containing this rectangle and all points, oriented along this rectangle's axis.</returns>
        public GeoRectangle2 Combine(params GeoPoint2[] points) => Combine((IEnumerable<GeoPoint2>)points);

        /// <summary>
        /// Combines this rectangle with a collection of points, returning a new rectangle that encloses this rectangle and all points.
        /// The resulting rectangle maintains the same orientation (angle) as this rectangle.
        /// </summary>
        /// <param name="points">The points to combine with. A null or empty sequence leaves the rectangle unchanged.</param>
        /// <returns>A new GeoRectangle2 containing this rectangle and all points, oriented along this rectangle's axis.</returns>
        public GeoRectangle2 Combine(IEnumerable<GeoPoint2> points)
        {
            if (points == null)
            {
                return this;
            }

            double cos = Math.Cos(AngleRad);
            double sin = Math.Sin(AngleRad);

            double minX = Width * -0.5;
            double maxX = Width * 0.5;
            double minY = Height * -0.5;
            double maxY = Height * 0.5;

            bool anyPoint = false;
            foreach (var pt in points)
            {
                anyPoint = true;
                ExpandLocalBounds(pt, cos, sin, ref minX, ref maxX, ref minY, ref maxY);
            }

            if (!anyPoint)
            {
                return this;
            }

            return FromLocalBounds(minX, maxX, minY, maxY, cos, sin);
        }

        /// <summary>
        /// Projects a point into this rectangle's local axes and widens the running bounds to reach it.
        /// </summary>
        private void ExpandLocalBounds(GeoPoint2 point, double cos, double sin, ref double minX, ref double maxX, ref double minY, ref double maxY)
        {
            double dx = point.X - Center.X;
            double dy = point.Y - Center.Y;

            double localX = dx * cos + dy * sin;
            double localY = -dx * sin + dy * cos;

            if (localX < minX) minX = localX;
            if (localX > maxX) maxX = localX;
            if (localY < minY) minY = localY;
            if (localY > maxY) maxY = localY;
        }

        /// <summary>
        /// Rebuilds a rectangle on this one's axes from bounds expressed in its local coordinate system.
        /// </summary>
        private GeoRectangle2 FromLocalBounds(double minX, double maxX, double minY, double maxY, double cos, double sin)
        {
            double localCenterX = (minX + maxX) * 0.5;
            double localCenterY = (minY + maxY) * 0.5;

            double worldCenterX = Center.X + localCenterX * cos - localCenterY * sin;
            double worldCenterY = Center.Y + localCenterX * sin + localCenterY * cos;

            return new GeoRectangle2(new GeoPoint2(worldCenterX, worldCenterY), maxX - minX, maxY - minY, AngleRad);
        }

        /// <summary>
        /// Gets the bottom-left corner coordinates.
        /// </summary>
        public GeoPoint2 LowerLeft
        {
            get
            {
                double cos = Math.Cos(AngleRad);
                double sin = Math.Sin(AngleRad);
                double halfW = Width * 0.5;
                double halfH = Height * 0.5;
                return new GeoPoint2(
                    Center.X - halfW * cos + halfH * sin,
                    Center.Y - halfW * sin - halfH * cos);
            }
        }

        /// <summary>
        /// Gets the bottom-right corner coordinates.
        /// </summary>
        public GeoPoint2 LowerRight
        {
            get
            {
                double cos = Math.Cos(AngleRad);
                double sin = Math.Sin(AngleRad);
                double halfW = Width * 0.5;
                double halfH = Height * 0.5;
                return new GeoPoint2(
                    Center.X + halfW * cos + halfH * sin,
                    Center.Y + halfW * sin - halfH * cos);
            }
        }

        /// <summary>
        /// Gets the top-left corner coordinates.
        /// </summary>
        public GeoPoint2 UpperLeft
        {
            get
            {
                double cos = Math.Cos(AngleRad);
                double sin = Math.Sin(AngleRad);
                double halfW = Width * 0.5;
                double halfH = Height * 0.5;
                return new GeoPoint2(
                    Center.X - halfW * cos - halfH * sin,
                    Center.Y - halfW * sin + halfH * cos);
            }
        }

        /// <summary>
        /// Gets the top-right corner coordinates.
        /// </summary>
        public GeoPoint2 UpperRight
        {
            get
            {
                double cos = Math.Cos(AngleRad);
                double sin = Math.Sin(AngleRad);
                double halfW = Width * 0.5;
                double halfH = Height * 0.5;
                return new GeoPoint2(
                    Center.X + halfW * cos - halfH * sin,
                    Center.Y + halfW * sin + halfH * cos);
            }
        }

        /// <summary>
        /// Gets the middle point of the bottom edge (between LowerLeft and LowerRight).
        /// </summary>
        public GeoPoint2 LowerMiddle => LowerLeft.GetMiddlePoint(LowerRight);

        /// <summary>
        /// Gets the middle point of the right edge (between LowerRight and UpperRight).
        /// </summary>
        public GeoPoint2 RightMiddle => LowerRight.GetMiddlePoint(UpperRight);

        /// <summary>
        /// Gets the middle point of the top edge (between UpperRight and UpperLeft).
        /// </summary>
        public GeoPoint2 UpperMiddle => UpperRight.GetMiddlePoint(UpperLeft);

        /// <summary>
        /// Gets the middle point of the left edge (between UpperLeft and LowerLeft).
        /// </summary>
        public GeoPoint2 LeftMiddle => UpperLeft.GetMiddlePoint(LowerLeft);

        /// <summary>
        /// Gets the 4 vertices of the rectangle in counter-clockwise order: LowerLeft, LowerRight, UpperRight, UpperLeft.
        /// </summary>
        public GeoPoint2[] GetVertices()
        {
            return new[] { LowerLeft, LowerRight, UpperRight, UpperLeft };
        }

        /// <summary>
        /// Gets the 4 closed edges of the rectangle, sequentially connecting the vertices returned by <see cref="GetVertices"/>.
        /// </summary>
        public GeoLine2[] GetEdges()
        {
            GeoPoint2[] v = GetVertices();
            return new[]
            {
                new GeoLine2(v[0], v[1]),
                new GeoLine2(v[1], v[2]),
                new GeoLine2(v[2], v[3]),
                new GeoLine2(v[3], v[0])
            };
        }

        /// <summary>
        /// Gets the closest point on the boundary of this rectangle to a target point, including for points
        /// inside the rectangle.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Projection2.ProjectToRectangle(this, point);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on a line segment using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Projection2.GetShortestLineTo(this, line, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Projection2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on the circumference of a circle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Projection2.GetShortestLineTo(this, circle, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on the circumference of a circle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Projection2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on the boundary of another rectangle using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 other) => Projection2.GetShortestLineTo(this, other, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on the boundary of another rectangle within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 other, Tolerance tolerance) => Projection2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on a polyline using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Projection2.GetShortestLineTo(this, polyline, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on a polyline within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on the boundary of a polygon using default tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly) => Projection2.GetShortestLineTo(this, poly, Tolerance.Global);

        /// <summary>
        /// Finds the shortest line segment connecting a point on the boundary of this rectangle to a point on the boundary of a polygon within tolerance.
        /// </summary>
        /// <remarks>
        /// Both ends of the returned segment sit on a boundary, so a shape lying entirely inside another
        /// still reports the gap out to its outline rather than zero. <see cref="GeometryHelper.Core.Distance2"/>
        /// takes the opposite view and treats a closed shape as a filled region, returning zero for that
        /// same pair.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly, Tolerance tolerance) => Projection2.GetShortestLineTo(this, poly, tolerance);

        /// <summary>
        /// Calculates the shortest Euclidean distance from this rectangle to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this rectangle to a point, negative for a point within it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the outline, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint2)"/>
        /// reads this rectangle as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Distance2.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this rectangle to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest boundary distance from this rectangle to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(this, poly);

        /// <summary>
        /// Calculates the shortest boundary distance from this rectangle to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine2 GeoLine2) => Distance2.DistanceTo(this, GeoLine2);

        /// <summary>
        /// Calculates the shortest boundary distance from this rectangle to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest boundary distance from this rectangle to another rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Distance2.DistanceTo(this, arc);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Distance2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this rectangle to an arc.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Projection2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment joining this rectangle to an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Projection2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a curved loop.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(this, loop);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this rectangle to a curved loop.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Projection2.GetShortestLineTo(this, loop);

        /// <summary>
        /// Gets the shortest segment joining this rectangle to a curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Projection2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a curved chain.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(this, chain);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(this, chain, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this rectangle to a curved chain.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Projection2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment joining this rectangle to a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps an arc.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => Collision2.CollidesWith(this, arc);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Collision2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Gets the points where this rectangle meets an arc.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => Intersection2.GetIntersections(this, arc);

        /// <summary>
        /// Gets the points where this rectangle meets an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Intersection2.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps a curved loop.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop) => Collision2.CollidesWith(this, loop);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps a curved loop, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Collision2.CollidesWith(this, loop, tolerance);

        /// <summary>
        /// Gets the points where this rectangle meets a curved loop.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Intersection2.GetIntersections(this, loop);

        /// <summary>
        /// Gets the points where this rectangle meets a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Intersection2.GetIntersections(this, loop, tolerance);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps a curved chain.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain) => Collision2.CollidesWith(this, chain);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps a curved chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => Collision2.CollidesWith(this, chain, tolerance);

        /// <summary>
        /// Gets the points where this rectangle meets a curved chain.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => Intersection2.GetIntersections(this, chain);

        /// <summary>
        /// Gets the points where this rectangle meets a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Intersection2.GetIntersections(this, chain, tolerance);

        /// <summary>
        /// Gets the edge of this rectangle nearest a point.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoPoint2 point) => ClosestEdge2.GetClosestEdge(this, point);

        /// <summary>
        /// Gets the edge of this rectangle nearest a segment.
        /// </summary>
        /// <remarks>
        /// The answer is one of this rectangle's own edges, not a segment joining the two shapes.
        /// For that, see <see cref="GetShortestLineTo(GeoLine2)"/>.
        /// </remarks>
        public GeoLine2 GetClosestEdge(GeoLine2 line) => ClosestEdge2.GetClosestEdge(this, line);

        /// <summary>
        /// Gets the edge of this rectangle nearest a circle.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoCircle2 circle) => ClosestEdge2.GetClosestEdge(this, circle);

        /// <summary>
        /// Checks whether the rectangle contains a point.
        /// </summary>
        public bool Contains(GeoPoint2 GeoPoint2) => Containment2.Contains(this, GeoPoint2);

        /// <summary>
        /// Checks whether the rectangle entirely contains a line segment using default tolerance.
        /// </summary>
        public bool Contains(GeoLine2 line) => Containment2.Contains(this, line, Tolerance.Global);

        /// <summary>
        /// Checks whether the rectangle entirely contains a line segment within tolerance.
        /// </summary>
        public bool Contains(GeoLine2 line, Tolerance tolerance) => Containment2.Contains(this, line, tolerance);

        /// <summary>
        /// Checks whether the rectangle entirely contains a polyline using default tolerance.
        /// </summary>
        public bool Contains(GeoPolyline2 polyline) => Containment2.Contains(this, polyline, Tolerance.Global);

        /// <summary>
        /// Checks whether the rectangle entirely contains a polyline within tolerance.
        /// </summary>
        public bool Contains(GeoPolyline2 polyline, Tolerance tolerance) => Containment2.Contains(this, polyline, tolerance);

        /// <summary>
        /// Classifies the location of a point relative to this rectangle (Inside, OutSide, or OnSide) using default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Containment2.Locate(this, point, Tolerance.Global);

        /// <summary>
        /// Classifies the location of a point relative to this rectangle (Inside, OutSide, or OnSide) within tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Containment2.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with another rectangle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 other) => Collision2.CollidesWith(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with another rectangle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with a line segment using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 geoLine) => Collision2.CollidesWith(this, geoLine, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with a line segment within tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 geoLine, Tolerance tolerance) => Collision2.CollidesWith(this, geoLine, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with a polygon using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly) => Collision2.CollidesWith(this, poly, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with a polygon within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly, Tolerance tolerance) => Collision2.CollidesWith(this, poly, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with a circle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Collision2.CollidesWith(circle, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with a circle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Collision2.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with a polyline using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => Collision2.CollidesWith(polyline, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with a polyline within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Collision2.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this rectangle reaches the material of a face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face) => Face2.CollidesWith(face, this);

        /// <summary>
        /// Checks whether this rectangle reaches the material of a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => Face2.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Gets every point where this rectangle crosses the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => Face2.GetIntersections(face, this);

        /// <summary>
        /// Gets every point where this rectangle crosses the boundary of a face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Face2.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this rectangle and landing on the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Face2.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this rectangle and landing on the boundary of a face, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Face2.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets all intersection points with a line segment using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Intersection2.GetIntersections(this, line, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a line segment within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Intersection2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets all intersection points with another rectangle using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 other) => Intersection2.GetIntersections(this, other, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with another rectangle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 other, Tolerance tolerance) => Intersection2.GetIntersections(this, other, tolerance);

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
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Intersection2.GetIntersections(this, circle, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a circle within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Intersection2.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets all intersection points with a polyline using default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(polyline, this, Tolerance.Global);

        /// <summary>
        /// Gets all intersection points with a polyline within tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this rectangle touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

        /// <summary>
        /// Gets the distance from this rectangle to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge)
            => edge.IsArc
                ? DistanceTo(edge.ToArc())
                : DistanceTo(edge.ToLine());

        /// <summary>
        /// Gets every point where this rectangle crosses an edge.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where this rectangle crosses an edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this rectangle and landing on an edge.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this rectangle and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetShortestLineTo(edge.ToArc(), tolerance)
                : GetShortestLineTo(edge.ToLine(), tolerance);

        /// <summary>
        /// Checks whether a line segment is parallel to this rectangle's axes using default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine2 line) => Parallel2.IsParallel(this, line, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment is parallel to this rectangle's axes within angular tolerance.
        /// </summary>
        public bool IsParallelTo(GeoLine2 line, Tolerance tolerance) => Parallel2.IsParallel(this, line, tolerance);

        /// <summary>
        /// Checks whether another rectangle is parallel in orientation to this rectangle using default tolerance.
        /// </summary>
        public bool IsParallelTo(GeoRectangle2 other) => Parallel2.IsParallel(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether another rectangle is parallel in orientation to this rectangle within angular tolerance.
        /// </summary>
        public bool IsParallelTo(GeoRectangle2 other, Tolerance tolerance) => Parallel2.IsParallel(this, other, tolerance);

        /// <summary>
        /// Translates a rectangle by a vector.
        /// </summary>
        public static GeoRectangle2 operator +(GeoRectangle2 rect, GeoVector2 vector) => rect.Translate(vector);

        /// <summary>
        /// Translates a rectangle backwards by a vector.
        /// </summary>
        public static GeoRectangle2 operator -(GeoRectangle2 rect, GeoVector2 vector) => rect.Translate(-vector);

        /// <summary>
        /// Indicates whether the current rectangle is equal to another rectangle.
        /// </summary>
        /// <param name="other">A rectangle to compare with this rectangle.</param>
        /// <returns>true if the current rectangle is equal to the other parameter; otherwise, false.</returns>
        public bool Equals(GeoRectangle2 other)
        {
            return Center.Equals(other.Center) &&
                   Width.Equals(other.Width) &&
                   Height.Equals(other.Height) &&
                   AngleRad.Equals(other.AngleRad);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is GeoRectangle2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = (hash * 397) ^ Width.GetHashCode();
                hash = (hash * 397) ^ Height.GetHashCode();
                hash = (hash * 397) ^ AngleRad.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Compares two GeoRectangle2 instances for equality.
        /// </summary>
        /// <param name="left">The first rectangle.</param>
        /// <param name="right">The second rectangle.</param>
        /// <returns>true if they are equal; otherwise, false.</returns>
        public static bool operator ==(GeoRectangle2 left, GeoRectangle2 right) => left.Equals(right);

        /// <summary>
        /// Compares two GeoRectangle2 instances for inequality.
        /// </summary>
        /// <param name="left">The first rectangle.</param>
        /// <param name="right">The second rectangle.</param>
        /// <returns>true if they are not equal; otherwise, false.</returns>
        public static bool operator !=(GeoRectangle2 left, GeoRectangle2 right) => !left.Equals(right);

        /// <summary>
        /// Returns the string representation of the rectangle.
        /// </summary>
        /// <returns>A string representation detailing center, width, height, and angle.</returns>
        public override string ToString() => $"GeoRectangle2[Center:{Center}, Width:{Width:0.###}, Height:{Height:0.###}, AngleRad:{AngleRad:0.###}]";

        /// <summary>
        /// Gets the area the rectangle encloses. Turning it does not change it.
        /// </summary>
        public double Area => Width * Height;

        /// <summary>
        /// Applies a transformation to this rectangle.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the transformation would turn the rectangle into a parallelogram.</exception>
        /// <summary>
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed rectangle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        public GeoRectangle2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Determines whether another rectangle covers the same region, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoRectangle2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another rectangle covers the same region, within a tolerance.
        /// </summary>
        /// <param name="other">The rectangle to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rectangles are the same within tolerance; otherwise, false.</returns>
        /// <remarks>
        /// The comparison is of the shape rather than of the numbers: the same rectangle turned by a half
        /// turn, or described with its width and height swapped and turned a quarter, covers the same
        /// region and counts as equal. What is compared is the four corners, in any starting order.
        /// </remarks>
        public bool IsEqualTo(GeoRectangle2 other, Tolerance tolerance)
        {
            return ToPolygon().IsEqualTo(other.ToPolygon(), tolerance)
                || ToPolygon().IsEqualTo(new GeoPolygon2(other.ToPolygon().Vertices.Reverse().ToArray()), tolerance);
        }
    }
}
