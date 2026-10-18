using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static calculation methods for geometric distances, squared distances, and closest point projections.
    /// </summary>
    public static partial class Distance2
    {
        /// <summary>
        /// A tolerance of nought, for asking whether a point lies in a region with no slack: a point on the rim counts as
        /// in, and one a hair outside does not.
        /// </summary>
        private static Tolerance Exactly => default(Tolerance);

        #region Point - Point

        /// <summary>
        /// Calculates the Euclidean distance between two points.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>The Euclidean distance between the two points.</returns>
        public static double DistanceTo(GeoPoint2 p1, GeoPoint2 p2)
        {
            return Math.Sqrt(GetDistanceSquaredTo(p1, p2));
        }

        /// <summary>
        /// Calculates the squared Euclidean distance between two points.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>The squared Euclidean distance between the two points.</returns>
        public static double GetDistanceSquaredTo(GeoPoint2 p1, GeoPoint2 p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return dx * dx + dy * dy;
        }

        #endregion

        #region Line - Shapes

        /// <summary>
        /// Calculates the shortest distance from a line segment to a point.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The shortest Euclidean distance.</returns>
        public static double DistanceTo(GeoLine2 line, GeoPoint2 point)
        {
            return DistanceTo(line, point, Tolerance.Global);
        }

        /// <summary>
        /// Calculates the shortest distance from a line segment to a point, a segment no longer than the tolerance
        /// read as the point at its start.
        /// </summary>
        internal static double DistanceTo(GeoLine2 line, GeoPoint2 point, Tolerance tolerance)
        {
            return DistanceTo(point, Projection2.ProjectToLine(line, point, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance between two line segments using default tolerance.
        /// </summary>
        public static double DistanceTo(GeoLine2 line1, GeoLine2 line2)
        {
            return DistanceTo(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Calculates the shortest distance between two line segments within tolerance.
        /// </summary>
        /// <remarks>
        /// Nought exactly where the two truly cross or overlap, read with no slack past their ends; otherwise the least of
        /// the distances from an end of one to the other. The segment from the origin to (10, 0) and the one leaving
        /// (10.00135, 0.00078) at 60 degrees, whose lines cross 0.0009 past the first one's end, are 0.00156 apart at every
        /// tolerance.
        /// </remarks>
        public static double DistanceTo(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            // Not Intersection2.TryIntersectWith: that one reports no intersection for any pair meeting at
            // less than the angular tolerance, which is right when the intersection point is wanted but
            // would make two crossing segments measure the gap between their endpoints instead of zero,
            // by an amount that grows with their length. Nor its slack past the ends, which made two
            // corners up to twice the tolerance apart measure nought.
            if (Projection2.TryGetCrossingPoint(line1, line2, tolerance, false, out _))
            {
                return 0.0;
            }

            double d1 = DistanceTo(line1, line2.StartPoint, tolerance);
            double d2 = DistanceTo(line1, line2.EndPoint, tolerance);
            double d3 = DistanceTo(line2, line1.StartPoint, tolerance);
            double d4 = DistanceTo(line2, line1.EndPoint, tolerance);

            return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
        }

        /// <summary>
        /// Calculates the shortest distance between one side of a shape and a segment a caller gave: the side is the side
        /// it is however short, and the segment, if no longer than the tolerance, is read as its start point where it
        /// does not cross.
        /// </summary>
        /// <remarks>
        /// <see cref="DistanceTo(GeoLine2, GeoLine2, Tolerance)"/> reads both of its segments so, which is right for two
        /// segments a caller gave and wrong for a side of a polygon, a rectangle or a chain: the side from (0, 0) to
        /// (0.004, 0) of a polygon, at a tolerance of 0.01, would measure as its start point, and a segment leaving
        /// (0.002, 0.006) would read 0.00632 from it, not 0.006. Crossing is read with no slack, and a segment that
        /// crosses a side is nought from it however short, as it is from the shape.
        /// </remarks>
        internal static double DistanceToSide(GeoLine2 side, GeoLine2 line, Tolerance tolerance)
        {
            if (Projection2.TryGetCrossingPoint(side, line, Exactly, false, out _))
            {
                return 0.0;
            }

            double d1 = DistanceTo(side, line.StartPoint, Exactly);
            double d2 = DistanceTo(side, line.EndPoint, Exactly);
            double d3 = DistanceTo(line, side.StartPoint, tolerance);
            double d4 = DistanceTo(line, side.EndPoint, tolerance);

            return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
        }

        #endregion

        #region Circle - Shapes

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a point.
        /// Returns 0 if the point is inside the circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The point.</param>
        /// <returns>The shortest distance to the circle boundary.</returns>
        public static double DistanceTo(GeoCircle2 circle, GeoPoint2 point)
        {
            return Math.Max(0.0, DistanceTo(circle.Center, point) - circle.Radius);
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a line segment.
        /// Returns 0 if the line segment intersects or is inside the circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="line">The line segment.</param>
        /// <returns>The shortest distance between circle and line segment.</returns>
        public static double DistanceTo(GeoCircle2 circle, GeoLine2 line)
        {
            return DistanceTo(circle, line, Tolerance.Global);
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a line segment, the segment read under a tolerance.
        /// </summary>
        internal static double DistanceTo(GeoCircle2 circle, GeoLine2 line, Tolerance tolerance)
        {
            return Math.Max(0.0, DistanceTo(line, circle.Center, tolerance) - circle.Radius);
        }

        /// <summary>
        /// Calculates the shortest boundary distance between two circles.
        /// Returns 0 if the two circles intersect or overlap.
        /// </summary>
        /// <param name="c1">The first circle.</param>
        /// <param name="c2">The second circle.</param>
        /// <returns>The shortest boundary distance.</returns>
        public static double DistanceTo(GeoCircle2 c1, GeoCircle2 c2)
        {
            return Math.Max(0.0, DistanceTo(c1.Center, c2.Center) - (c1.Radius + c2.Radius));
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a rectangle.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="rect">The rectangle.</param>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly: how far the rectangle is from the centre,
        /// less the radius. A circle of radius 0.0001 about (100.00085, 100.00085) is 0.0011 from the square from the
        /// origin to (100, 100).
        /// </returns>
        public static double DistanceTo(GeoCircle2 circle, GeoRectangle2 rect)
        {
            // A centre in the rectangle is nought from it, so the distance to the centre needs no test of its own.
            return Math.Max(0.0, DistanceTo(rect, circle.Center) - circle.Radius);
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a polygon.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="poly">The polygon.</param>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly: how far the polygon is from the centre,
        /// less the radius.
        /// </returns>
        public static double DistanceTo(GeoCircle2 circle, GeoPolygon2 poly)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // A centre in the polygon is nought from it, so the distance to the centre needs no test of its own.
            return Math.Max(0.0, DistanceTo(poly, circle.Center) - circle.Radius);
        }

        #endregion

        #region Rectangle - Shapes

        /// <summary>
        /// Calculates the shortest Euclidean distance from a rectangle to a point.
        /// Returns 0 if the point is inside the rectangle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The shortest distance to the rectangle.</returns>
        public static double DistanceTo(GeoRectangle2 rect, GeoPoint2 point)
        {
            double dx = point.X - rect.Center.X;
            double dy = point.Y - rect.Center.Y;

            double cos = Math.Cos(rect.AngleRad);
            double sin = Math.Sin(rect.AngleRad);

            // Project point onto the local coordinate system around Center
            double localX = dx * cos + dy * sin;
            double localY = -dx * sin + dy * cos;

            double halfW = rect.Width * 0.5;
            double halfH = rect.Height * 0.5;

            double clampedX = Math.Max(-halfW, Math.Min(halfW, localX));
            double clampedY = Math.Max(-halfH, Math.Min(halfH, localY));

            double diffX = localX - clampedX;
            double diffY = localY - clampedY;

            return Math.Sqrt(diffX * diffX + diffY * diffY);
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a rectangle to a line segment.
        /// Returns 0 if the line segment intersects or lies inside the rectangle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="line">The line segment.</param>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. The segment from (100.00085, 100.00085) to
        /// (150, 150) is 0.0012 from the square from the origin to (100, 100), and the same segment run the other way is
        /// the same.
        /// </returns>
        public static double DistanceTo(GeoRectangle2 rect, GeoLine2 line)
        {
            if (Collision2.Meets(rect, line))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            GeoLine2[] rectEdges = rect.GetEdges();

            foreach (var re in rectEdges)
            {
                double d = DistanceToSide(re, line, Tolerance.Global);
                if (d < minDistance) minDistance = d;
            }

            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest boundary distance between two rectangles.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <param name="rect1">The first rectangle.</param>
        /// <param name="rect2">The second rectangle.</param>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Two squares 100 by 100 with their corners
        /// 0.00085 apart in x and in y are 0.0012 apart.
        /// </returns>
        public static double DistanceTo(GeoRectangle2 rect1, GeoRectangle2 rect2)
        {
            if (Collision2.Meets(rect1, rect2))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            GeoLine2[] r1Edges = rect1.GetEdges();
            GeoLine2[] r2Edges = rect2.GetEdges();

            foreach (var r1e in r1Edges)
            {
                foreach (var r2e in r2Edges)
                {
                    // Sides of two rectangles, not a caller's segment: a side shorter than the tolerance is the side it is.
                    double d = DistanceTo(r1e, r2e, Exactly);
                    if (d < minDistance) minDistance = d;
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a rectangle to a polygon.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="poly">The polygon.</param>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public static double DistanceTo(GeoRectangle2 rect, GeoPolygon2 poly)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            if (Collision2.Meets(rect, poly))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            GeoLine2[] rectEdges = rect.GetEdges();

            for (int i = 0; i < poly.EdgeCount; i++)
            {
                GeoLine2 pe = poly.GetEdgeAt(i);
                foreach (var re in rectEdges)
                {
                    // Sides of two shapes, not a caller's segment: a side shorter than the tolerance is the side it is.
                    double d = DistanceTo(pe, re, Exactly);
                    if (d < minDistance) minDistance = d;
                }
            }

            return minDistance;
        }

        #endregion

        #region Polygon - Shapes

        /// <summary>
        /// Calculates the shortest distance from a polygon to a point: nought only where the point lies in the polygon or on
        /// its rim, otherwise the gap, measured exactly, the polygon's sides as the sides they are however short. A point
        /// 0.0005 off the square 100 by 100 is 0.0005 from it.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <returns>Nought for a point in the polygon or on its rim, otherwise the distance to the nearest side.</returns>
        public static double DistanceTo(GeoPolygon2 poly, GeoPoint2 point)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // A polygon is a filled region here, exactly like a rectangle or a circle, so a point inside
            // it is at distance zero. Projection2 reports the boundary point instead, so the two
            // deliberately disagree for interior points.
            if (Containment2.Contains(poly, point, Exactly))
            {
                return 0.0;
            }

            return DistanceTo(point, Projection2.ProjectToPolygon(poly, point, Exactly));
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a polygon to a line segment.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. The segment from (100.0005, 50) to
        /// (150, 50) is 0.0005 from the square 100 by 100 with a corner at the origin, and so is the same segment run
        /// the other way.
        /// </returns>
        public static double DistanceTo(GeoPolygon2 poly, GeoLine2 line)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // Zero when the segment crosses the polygon or lies inside it, matching DistanceTo(rect, line).
            if (Collision2.Meets(poly, line))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            for (int i = 0; i < poly.EdgeCount; i++)
            {
                double d = DistanceToSide(poly.GetEdgeAt(i), line, Tolerance.Global);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest boundary distance between two polygons.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public static double DistanceTo(GeoPolygon2 poly1, GeoPolygon2 poly2)
        {
            if (poly1 == null) throw new ArgumentNullException(nameof(poly1));
            if (poly2 == null) throw new ArgumentNullException(nameof(poly2));
            if (Collision2.Meets(poly1, poly2)) return 0.0;

            double minDistance = double.MaxValue;
            for (int i = 0; i < poly1.EdgeCount; i++)
            {
                GeoLine2 e1 = poly1.GetEdgeAt(i);
                for (int j = 0; j < poly2.EdgeCount; j++)
                {
                    // Sides of two shapes, not a caller's segment: a side shorter than the tolerance is the side it is.
                    double d = DistanceTo(e1, poly2.GetEdgeAt(j), Exactly);
                    if (d < minDistance) minDistance = d;
                }
            }
            return minDistance;
        }

        #endregion

        #region Polyline - Shapes

        /// <summary>
        /// Calculates the shortest Euclidean distance from a polyline to a point.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The shortest distance to the polyline.</returns>
        public static double DistanceTo(GeoPolyline2 polyline, GeoPoint2 point)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            // A polyline is a curve, not a region, so only its path counts: a point sitting in the mouth
            // of a horseshoe is at its distance from the path, never at zero.
            return DistanceTo(point, Projection2.ProjectToPolyline(polyline, point, Exactly));
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a line segment.
        /// Returns 0 if they intersect.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="line">The line segment.</param>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public static double DistanceTo(GeoPolyline2 polyline, GeoLine2 line)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            if (CrossesOrStartsOn(polyline, line))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 edge = polyline.GetEdgeAt(i);
                double d = DistanceToSide(edge, line, Tolerance.Global);
                if (d < minDistance) minDistance = d;
            }

            return minDistance;
        }

        /// <summary>
        /// Whether a segment crosses an edge of a polyline or starts on it: where the distance between them is taken
        /// as nought without measuring. A segment that only comes within the tolerance is measured, so its distance is
        /// the gap, not nought; so is one whose line crosses an edge's only past their ends, and one that starts a hair
        /// off the polyline.
        /// </summary>
        private static bool CrossesOrStartsOn(GeoPolyline2 polyline, GeoLine2 line)
        {
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (Collision2.Crosses(polyline.GetEdgeAt(i), line, Exactly))
                {
                    return true;
                }
            }

            return Containment2.IsPointOn(polyline, line.StartPoint, Exactly);
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a rectangle.
        /// Returns 0 if they intersect.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public static double DistanceTo(GeoPolyline2 polyline, GeoRectangle2 rect)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (Collision2.Meets(polyline, rect)) return 0.0;

            double minDistance = double.MaxValue;
            GeoLine2[] rectEdges = rect.GetEdges();
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 pe = polyline.GetEdgeAt(i);
                foreach (var re in rectEdges)
                {
                    double d = DistanceTo(pe, re, Exactly);
                    if (d < minDistance) minDistance = d;
                }
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a polygon.
        /// Returns 0 if they intersect.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public static double DistanceTo(GeoPolyline2 polyline, GeoPolygon2 poly)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            if (Collision2.Meets(polyline, poly)) return 0.0;

            double minDistance = double.MaxValue;
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 ple = polyline.GetEdgeAt(i);
                for (int j = 0; j < poly.EdgeCount; j++)
                {
                    double d = DistanceTo(ple, poly.GetEdgeAt(j), Exactly);
                    if (d < minDistance) minDistance = d;
                }
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a circle.
        /// Returns 0 if they intersect.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. The chain from (0, 40.0005) to (50, 40.0005)
        /// is 0.0005 from the circle of radius 40 about the origin.
        /// </returns>
        public static double DistanceTo(GeoPolyline2 polyline, GeoCircle2 circle)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double minDistance = double.MaxValue;
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                double d = DistanceTo(circle, polyline.GetEdgeAt(i), Exactly);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between two polylines.
        /// Returns 0 if they intersect.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public static double DistanceTo(GeoPolyline2 pl1, GeoPolyline2 pl2)
        {
            if (pl1 == null) throw new ArgumentNullException(nameof(pl1));
            if (pl2 == null) throw new ArgumentNullException(nameof(pl2));
            if (Collision2.Meets(pl1, pl2)) return 0.0;

            double minDistance = double.MaxValue;
            for (int i = 0; i < pl1.EdgeCount; i++)
            {
                GeoLine2 e1 = pl1.GetEdgeAt(i);
                for (int j = 0; j < pl2.EdgeCount; j++)
                {
                    double d = DistanceTo(e1, pl2.GetEdgeAt(j), Exactly);
                    if (d < minDistance) minDistance = d;
                }
            }
            return minDistance;
        }

        #endregion
        #region Chains that may curve

        /// <summary>
        /// Gets the distance from a chain that may curve to a point.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPoint2 point) => DistanceTo(chain, point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a chain that may curve to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Measured to the arcs themselves rather than to the chords that stand in for them elsewhere, so
        /// a point beyond a round end is as far away as it looks. The edges are the edges they are, however short, and a
        /// point at the centre of an arc is as far from it as any other: the tolerance changes nothing here.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPoint2 point, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(chain), point);
        }

        /// <summary>
        /// Gets the distance from a chain that may curve to a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoLine2 line) => DistanceTo(chain, line, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a chain that may curve to a straight segment, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoLine2 line, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            List<GeoEdge2> edges = ArcChain2.EdgesOf(chain);

            // A segment crossing an edge is nought from the chain, however short it is.
            if (ArcChain2.Meet(edges, line)) return 0.0;

            return ArcChain2.DistanceTo(edges, line, tolerance);
        }

        /// <summary>
        /// Gets the distance from a chain that may curve to an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoArc2 arc) => DistanceTo(chain, arc, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a chain that may curve to an arc, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoArc2 arc, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(chain), arc);
        }

        /// <summary>
        /// Gets the distance from a chain that may curve to a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPolyline2 polyline) => DistanceTo(chain, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a chain that may curve to a straight chain, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            
            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polyline));
        }

        /// <summary>
        /// Gets the distance from a chain that may curve to a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPolygon2 polygon) => DistanceTo(chain, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a chain that may curve to a straight loop, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            
            if (Collision2.Meets(chain, polygon)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polygon));
        }

        /// <summary>
        /// Gets the distance from a chain that may curve to a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPolylineArc2 other) => DistanceTo(chain, other, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a chain that may curve to a chain that may curve, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the other is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPolylineArc2 other, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (other == null) throw new ArgumentNullException(nameof(other));
            
            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(other));
        }

        /// <summary>
        /// Gets the distance from a chain that may curve to a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPolygonArc2 loop) => DistanceTo(chain, loop, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a chain that may curve to a loop that may curve, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            if (Collision2.Meets(chain, loop)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(loop));
        }

        /// <summary>
        /// Gets the distance from a loop that may curve to a point.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPoint2 point) => DistanceTo(loop, point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a loop that may curve to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Measured to the arcs themselves rather than to the chords that stand in for them elsewhere, so
        /// a point beyond a round end is as far away as it looks. Nought only where the point lies in the loop or on its
        /// rim: a point 0.0005 off the square 100 by 100 with straight edges is 0.0005 from it. The edges are the edges they are, however short.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPoint2 point, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (Containment2.Contains(loop, point, Exactly)) return 0.0;
            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), point);
        }

        /// <summary>
        /// Gets the distance from a loop that may curve to a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoLine2 line) => DistanceTo(loop, line, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a loop that may curve to a straight segment, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoLine2 line, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            if (Collision2.Meets(loop, line)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), line, tolerance);
        }

        /// <summary>
        /// Gets the distance from a loop that may curve to an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoArc2 arc) => DistanceTo(loop, arc, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a loop that may curve to an arc, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoArc2 arc, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            if (Containment2.Contains(loop, arc.StartPoint, Exactly)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), arc);
        }

        /// <summary>
        /// Gets the distance from a loop that may curve to a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPolyline2 polyline) => DistanceTo(loop, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a loop that may curve to a straight chain, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            
            if (Collision2.Meets(loop, polyline)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polyline));
        }

        /// <summary>
        /// Gets the distance from a loop that may curve to a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPolygon2 polygon) => DistanceTo(loop, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a loop that may curve to a straight loop, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            
            if (Collision2.Meets(loop, polygon)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polygon));
        }

        /// <summary>
        /// Gets the distance from a loop that may curve to a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPolylineArc2 chain) => DistanceTo(loop, chain, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a loop that may curve to a chain that may curve, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            if (Collision2.Meets(loop, chain)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(chain));
        }

        /// <summary>
        /// Gets the distance from a loop that may curve to a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPolygonArc2 other) => DistanceTo(loop, other, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a loop that may curve to a loop that may curve, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the other is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoPolygonArc2 other, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (other == null) throw new ArgumentNullException(nameof(other));
            
            if (Collision2.Meets(loop, other)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(other));
        }

        #endregion

        /// <summary>
        /// Gets the distance from a chain that may curve to a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoCircle2 circle) => DistanceTo(chain, circle, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a chain that may curve to a circle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoCircle2 circle, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(chain), circle);
        }

        /// <summary>
        /// Gets the distance from a loop that may curve to a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoCircle2 circle) => DistanceTo(loop, circle, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a loop that may curve to a circle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoCircle2 circle, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            if (Containment2.Contains(loop, circle.Center, Exactly)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), circle);
        }

    }
}
