using System;
using System.Collections.Generic;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest
{
    public class RectangleTests
    {
        [Fact]
        public void Rectangle_CreationAndProperties_WorkCorrectly()
        {
            // Vertical rectangle (AABB) represented by Center
            var rect1 = new GeoRectangle2(0.0, 0.0, 4.0, 6.0);
            Assert.Equal(new GeoPoint2(2.0, 3.0), rect1.Center);
            Assert.Equal(4.0, rect1.Width);
            Assert.Equal(6.0, rect1.Height);
            Assert.Equal(0.0, rect1.AngleRad);
            Assert.False(rect1.IsRotated);
            Assert.Equal(20.0, rect1.Length);

            Assert.Equal(new GeoPoint2(0.0, 0.0), rect1.LowerLeft);
            Assert.Equal(new GeoPoint2(4.0, 0.0), rect1.LowerRight);
            Assert.Equal(new GeoPoint2(0.0, 6.0), rect1.UpperLeft);
            Assert.Equal(new GeoPoint2(4.0, 6.0), rect1.UpperRight);

            // Rotated rectangle (OBB)
            var center = new GeoPoint2(0.0, 0.0);
            var rect2 = new GeoRectangle2(center, 4.0, 2.0, Math.PI / 2.0); // Width 4 along local X-axis (Y-axis), height 2 along local Y-axis (-X)
            Assert.True(rect2.IsRotated);
            Assert.Equal(12.0, rect2.Length);

            // Rotate 90 degrees counter-clockwise transforms local coordinates from (x, y) to (-y, x).
            // Local LowerLeft is (-halfW, -halfH) = (-2, -1) -> (1.0, -2.0)
            Assert.True(rect2.LowerLeft.IsEqualTo(new GeoPoint2(1.0, -2.0)));
            // Local LowerRight is (halfW, -halfH) = (2, -1) -> (1.0, 2.0)
            Assert.True(rect2.LowerRight.IsEqualTo(new GeoPoint2(1.0, 2.0)));
            // Local UpperRight is (halfW, halfH) = (2, 1) -> (-1.0, 2.0)
            Assert.True(rect2.UpperRight.IsEqualTo(new GeoPoint2(-1.0, 2.0)));
            // Local UpperLeft is (-halfW, halfH) = (-2, 1) -> (-1.0, -2.0)
            Assert.True(rect2.UpperLeft.IsEqualTo(new GeoPoint2(-1.0, -2.0)));
        }

        [Fact]
        public void Rectangle_ContainsPoint_WorksCorrectly()
        {
            var center = new GeoPoint2(0.0, 0.0);
            var rect = new GeoRectangle2(center, 4.0, 2.0, Math.PI / 4.0); // Rotate 45 degrees

            // Center is definitely inside
            Assert.True(rect.Contains(center));

            // Point (0, 0.9) - rotated 45 degrees, local X-axis is 0.9*sin(45) ~ 0.63, local Y-axis is 0.9*cos(45) ~ 0.63.
            // HalfWidth = 2.0, HalfHeight = 1.0. Both 0.63 are within bounds.
            Assert.True(rect.Contains(new GeoPoint2(0.0, 0.9)));

            // Point (2, 0) - on the boundary when unrotated, but falls outside when rotated 45 degrees (distance 2 > HalfHeight of rotation axis)
            // Local projection: X = 2*cos(45) ~ 1.41 (<= 2.0), Y = -2*sin(45) ~ -1.41 (not in [-1.0, 1.0])
            Assert.False(rect.Contains(new GeoPoint2(2.0, 0.0)));
        }

        [Fact]
        public void Rectangle_IntersectsWith_WorksCorrectly_SAT()
        {
            var center1 = new GeoPoint2(0.0, 0.0);
            var rect1 = new GeoRectangle2(center1, 4.0, 2.0, 0.0);

            // 1. Two AABBs completely disjoint
            var rect2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 2.0, 2.0, 0.0);
            Assert.False(rect1.CollidesWith(rect2));

            // 2. Hai AABB giao nhau
            var rect3 = new GeoRectangle2(new GeoPoint2(3.0, 0.0), 3.0, 2.0, 0.0);
            Assert.True(rect1.CollidesWith(rect3));

            // 3. Two AABBs nested
            var rect4 = new GeoRectangle2(center1, 1.0, 1.0, 0.0);
            Assert.True(rect1.CollidesWith(rect4));

            // 4. Two rotated OBBs intersecting
            var rect5 = new GeoRectangle2(new GeoPoint2(2.5, 1.5), 2.0, 2.0, Math.PI / 4.0); // Rotate 45 degrees
            Assert.True(rect1.CollidesWith(rect5));

            // 5. Two rotated OBBs disjoint
            var rect6 = new GeoRectangle2(new GeoPoint2(4.0, 3.0), 2.0, 2.0, Math.PI / 4.0);
            Assert.False(rect1.CollidesWith(rect6));
        }

        [Fact]
        public void Rectangle_ContainsPointOnBoundary_WorksCorrectly()
        {
            var center = new GeoPoint2(0.0, 0.0);
            var rect = new GeoRectangle2(center, 4.0, 2.0, 0.0);

            // Point lies exactly on the vertex corner
            Assert.True(rect.Contains(new GeoPoint2(2.0, 1.0)));

            // Point lies exactly on the side edge
            Assert.True(rect.Contains(new GeoPoint2(2.0, 0.5)));
        }

        [Fact]
        public void Rectangle_IntersectsWithContactOnly_WorksCorrectly()
        {
            var rect1 = new GeoRectangle2(0.0, 0.0, 4.0, 4.0); // X: [0, 4], Y: [0, 4]

            // External contact touching edge
            var rect2 = new GeoRectangle2(4.0, 0.0, 4.0, 4.0); // X: [4, 8], Y: [0, 4]
            Assert.True(rect1.CollidesWith(rect2));

            // External contact touching corner
            var rect3 = new GeoRectangle2(4.0, 4.0, 4.0, 4.0); // X: [4, 8], Y: [4, 8]
            Assert.True(rect1.CollidesWith(rect3));
        }

        [Fact]
        public void Rectangle_IntersectsWithExtremeAspectRatios_WorksCorrectly()
        {
            // A extremely thin rectangle (long bar shape)
            var rectThin = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 10.0, 0.0001, Math.PI / 4.0);
            var rectTarget = new GeoRectangle2(new GeoPoint2(1.0, 1.0), 2.0, 2.0, 0.0);

            Assert.True(rectThin.CollidesWith(rectTarget));
        }

        [Fact]
        public void Rectangle_IntersectsWithLine_WorksCorrectly()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 4.0); // X: [-2, 2], Y: [-2, 2]

            Assert.True(rect.CollidesWith(new GeoLine2(-5.0, 0.0, 5.0, 0.0)));   // Passes through
            Assert.True(rect.CollidesWith(new GeoLine2(-1.0, -1.0, 1.0, 1.0)));  // Completely inside
            Assert.False(rect.CollidesWith(new GeoLine2(-5.0, 5.0, 5.0, 5.0)));  // Passes above, no contact
        }

        [Fact]
        public void Rectangle_IntersectsWithPolygon_WorksCorrectly()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 4.0); // X: [-2, 2], Y: [-2, 2]

            var overlapping = new GeoPolygon2(
                new GeoPoint2(1.0, 1.0),
                new GeoPoint2(5.0, 1.0),
                new GeoPoint2(5.0, 5.0),
                new GeoPoint2(1.0, 5.0)
            );
            var far = new GeoPolygon2(
                new GeoPoint2(10.0, 10.0),
                new GeoPoint2(12.0, 10.0),
                new GeoPoint2(12.0, 12.0)
            );

            Assert.True(rect.CollidesWith(overlapping));
            Assert.False(rect.CollidesWith(far));

            // Calling in reverse direction must give the same result
            Assert.True(overlapping.CollidesWith(rect));
            Assert.False(far.CollidesWith(rect));
        }

        [Fact]
        public void Rectangle_CornerConstructor_MatchesCentreConstructor()
        {
            var fromCorner = new GeoRectangle2(10.0, 20.0, 4.0, 6.0);
            var fromCentre = new GeoRectangle2(new GeoPoint2(12.0, 23.0), 4.0, 6.0);

            Assert.Equal(fromCentre, fromCorner);
            Assert.Equal(0.0, fromCorner.AngleRad);
            Assert.False(fromCorner.IsRotated);
        }

        [Fact]
        public void Rectangle_GetEdges_ConnectsVerticesInOrder()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 0.6);

            var vertices = rect.GetVertices();
            var edges = rect.GetEdges();

            Assert.Equal(4, edges.Length);
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(vertices[i], edges[i].StartPoint);
                Assert.Equal(vertices[(i + 1) % 4], edges[i].EndPoint);
            }
        }

        [Fact]
        public void Rectangle_Equality_AndHashCode_WorkCorrectly()
        {
            var a = new GeoRectangle2(new GeoPoint2(1.0, 2.0), 4.0, 2.0, 0.5);
            var b = new GeoRectangle2(new GeoPoint2(1.0, 2.0), 4.0, 2.0, 0.5);
            var rotatedDifferently = new GeoRectangle2(new GeoPoint2(1.0, 2.0), 4.0, 2.0, 0.6);

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a.Equals((object)b));

            object notARectangle = "not a GeoRectangle2";
            Assert.False(a.Equals(notARectangle));
            Assert.True(a != rotatedDifferently);
        }

        [Fact]
        public void Rectangle_IsRotated_IgnoresNegligibleAngles()
        {
            Assert.False(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 0.0).IsRotated);
            Assert.False(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 1e-9).IsRotated);
            Assert.True(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 0.05).IsRotated);
            Assert.True(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, Math.PI / 4.0).IsRotated);
        }

        [Fact]
        public void Rectangle_IsRotated_UsesAngularToleranceAndIgnoresFullTurns()
        {
            // The threshold is EqualAngleRad (1 degree by default), so anything the library would already
            // call parallel to the world axes must not report itself as rotated.
            var belowThreshold = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 0.5 * Math.PI / 180.0);
            Assert.False(belowThreshold.IsRotated);
            Assert.True(belowThreshold.IsParallelTo(new GeoLine2(new GeoPoint2(0.0, 0.0), new GeoPoint2(1.0, 0.0))));

            var aboveThreshold = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 2.0 * Math.PI / 180.0);
            Assert.True(aboveThreshold.IsRotated);

            // Full turns are not a rotation.
            Assert.False(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 2.0 * Math.PI).IsRotated);
            Assert.False(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, -4.0 * Math.PI).IsRotated);
        }

        [Fact]
        public void Rectangle_Constructor_RejectsNegativeExtents()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoRectangle2(new GeoPoint2(0.0, 0.0), -4.0, 2.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, -2.0));

            // Zero extents stay legal: a degenerate box is still a usable input.
            var flat = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 0.0);
            Assert.Equal(0.0, flat.Height);
        }

        [Fact]
        public void Rectangle_Translate_KeepsSizeAndRotation()
        {
            var rect = new GeoRectangle2(new GeoPoint2(1.0, 2.0), 4.0, 2.0, Math.PI / 6.0);
            var moved = rect.Translate(new GeoVector2(10.0, -5.0));

            Assert.True(moved.Center.IsEqualTo(new GeoPoint2(11.0, -3.0)));
            Assert.Equal(rect.Width, moved.Width);
            Assert.Equal(rect.Height, moved.Height);
            Assert.Equal(rect.AngleRad, moved.AngleRad);

            // Operators mirror the method, and the reverse translation restores the original.
            Assert.Equal(moved, rect + new GeoVector2(10.0, -5.0));
            Assert.Equal(rect, moved - new GeoVector2(10.0, -5.0));
        }

        [Fact]
        public void Rectangle_RotateBy_MovesCenterAndAccumulatesAngle()
        {
            var rect = new GeoRectangle2(new GeoPoint2(10.0, 0.0), 4.0, 2.0);
            var rotated = rect.RotateBy(Math.PI / 2.0, new GeoPoint2(0.0, 0.0));

            Assert.True(rotated.Center.IsEqualTo(new GeoPoint2(0.0, 10.0)));
            Assert.Equal(Math.PI / 2.0, rotated.AngleRad, 9);
            Assert.Equal(rect.Width, rotated.Width);
            Assert.Equal(rect.Height, rotated.Height);
        }

        [Fact]
        public void Rectangle_DistanceTo_IsZeroWhenTouchingAndPositiveWhenApart()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0); // X: [-2, 2], Y: [-1, 1]

            // Edge contact: boundary-to-boundary distance is 0.
            var touching = new GeoRectangle2(new GeoPoint2(4.0, 0.0), 4.0, 2.0);
            Assert.Equal(0.0, rect.DistanceTo(touching), 9);

            // Apart by 3 units along the X-axis.
            var apart = new GeoRectangle2(new GeoPoint2(7.0, 0.0), 4.0, 2.0);
            Assert.Equal(3.0, rect.DistanceTo(apart), 9);

            // To horizontal segment above, 4 units away from top edge.
            Assert.Equal(4.0, rect.DistanceTo(new GeoLine2(-5.0, 5.0, 5.0, 5.0)), 9);

            // To polygon on the right, 3 units away from right edge.
            var poly = new GeoPolygon2(
                new GeoPoint2(5.0, -1.0),
                new GeoPoint2(9.0, -1.0),
                new GeoPoint2(9.0, 1.0),
                new GeoPoint2(5.0, 1.0));
            Assert.Equal(3.0, rect.DistanceTo(poly), 9);
        }

        [Fact]
        public void Rectangle_DistanceTo_NullPolygon_Throws()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0);

            Assert.Throws<ArgumentNullException>(() => rect.DistanceTo((GeoPolygon2)null));
            Assert.Throws<ArgumentNullException>(() => rect.CollidesWith((GeoPolygon2)null));
        }

        [Fact]
        public void Rectangle_IntersectsWith_NarrowGapCountsAsTouchingWithinTolerance()
        {
            var left = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0);   // X extends to 2.0

            // Gap narrower than tolerance is treated as touching.
            var almostTouching = new GeoRectangle2(new GeoPoint2(4.0 + 1e-6, 0.0), 4.0, 2.0);
            Assert.True(left.CollidesWith(almostTouching));

            // Gap wider than tolerance is completely disjoint.
            var clearlyApart = new GeoRectangle2(new GeoPoint2(4.1, 0.0), 4.0, 2.0);
            Assert.False(left.CollidesWith(clearlyApart));
        }

        [Fact]
        public void Rectangle_GetVertices_PreservesSizeUnderRotation()
        {
            var rect = new GeoRectangle2(new GeoPoint2(3.0, -2.0), 6.0, 4.0, 1.1);
            var vertices = rect.GetVertices();

            // Adjacent edges must preserve correct width and height even if rotated.
            Assert.Equal(6.0, vertices[0].DistanceTo(vertices[1]), 9);
            Assert.Equal(4.0, vertices[1].DistanceTo(vertices[2]), 9);
            Assert.Equal(6.0, vertices[2].DistanceTo(vertices[3]), 9);
            Assert.Equal(4.0, vertices[3].DistanceTo(vertices[0]), 9);

            // Center of the shape is still the midpoint of the two diagonals.
            Assert.True(vertices[0].GetMiddlePoint(vertices[2]).IsEqualTo(rect.Center));
            Assert.True(vertices[1].GetMiddlePoint(vertices[3]).IsEqualTo(rect.Center));
        }

        [Fact]
        public void Rectangle_ToPolygon_ConvertsToEquivalentPolygon()
        {
            var center = new GeoPoint2(2.0, 3.0);
            var rect = new GeoRectangle2(center, 6.0, 4.0, 0.5); // Rotated rectangle

            GeoPolygon2 polygon = rect.ToPolygon();

            Assert.NotNull(polygon);
            Assert.Equal(4, polygon.VertexCount);

            GeoPoint2[] vertices = rect.GetVertices();
            for (int i = 0; i < 4; i++)
            {
                Assert.True(polygon[i].IsEqualTo(vertices[i]));
            }

            // Area of polygon should equal rectangle area
            Assert.Equal(24.0, polygon.GetArea(), 9);
        }

        [Fact]
        public void Rectangle_ToPolyline_ProducesClosedPolylineBoundary()
        {
            var center = new GeoPoint2(2.0, 3.0);
            var rect = new GeoRectangle2(center, 6.0, 4.0, 0.5); // Rotated rectangle

            GeoPolyline2 polyline = rect.ToPolyline();

            Assert.NotNull(polyline);
            Assert.Equal(5, polyline.VertexCount);

            GeoPoint2[] vertices = rect.GetVertices();
            for (int i = 0; i < 4; i++)
            {
                Assert.True(polyline[i].IsEqualTo(vertices[i]));
            }
            // Closed loop
            Assert.True(polyline[4].IsEqualTo(vertices[0]));

            // Length of boundary should equal perimeter of rectangle
            Assert.Equal(20.0, polyline.Length, 9);
        }

        [Fact]
        public void Rectangle_MiddlePoints_CalculateCorrectly()
        {
            // Test 1: Unrotated rectangle
            var center = new GeoPoint2(10.0, 20.0);
            var rectUnrotated = new GeoRectangle2(center, 6.0, 4.0, 0.0);

            Assert.True(rectUnrotated.LowerMiddle.IsEqualTo(new GeoPoint2(10.0, 18.0)));
            Assert.True(rectUnrotated.RightMiddle.IsEqualTo(new GeoPoint2(13.0, 20.0)));
            Assert.True(rectUnrotated.UpperMiddle.IsEqualTo(new GeoPoint2(10.0, 22.0)));
            Assert.True(rectUnrotated.LeftMiddle.IsEqualTo(new GeoPoint2(7.0, 20.0)));

            // Test 2: Rotated rectangle
            var rectRotated = new GeoRectangle2(center, 6.0, 4.0, 0.5);

            // Centroid of opposite middle points must be the rectangle center
            Assert.True(rectRotated.LowerMiddle.GetMiddlePoint(rectRotated.UpperMiddle).IsEqualTo(center));
            Assert.True(rectRotated.LeftMiddle.GetMiddlePoint(rectRotated.RightMiddle).IsEqualTo(center));

            // Distances from center to middle points must equal half dimensions
            Assert.Equal(2.0, center.DistanceTo(rectRotated.LowerMiddle), 9);
            Assert.Equal(2.0, center.DistanceTo(rectRotated.UpperMiddle), 9);
            Assert.Equal(3.0, center.DistanceTo(rectRotated.LeftMiddle), 9);
            Assert.Equal(3.0, center.DistanceTo(rectRotated.RightMiddle), 9);
        }

        [Fact]
        public void Rectangle_Combine_EnclosesBothRectangles()
        {
            // Test 1: Simple aligned rectangles combination
            var rect1 = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 2.0, 2.0, 0.0);
            var rect2 = new GeoRectangle2(new GeoPoint2(4.0, 0.0), 2.0, 2.0, 0.0);

            GeoRectangle2 combined = rect1.Combine(rect2);

            Assert.Equal(2.0, combined.Center.X, 9);
            Assert.Equal(0.0, combined.Center.Y, 9);
            Assert.Equal(6.0, combined.Width, 9);
            Assert.Equal(2.0, combined.Height, 9);
            Assert.Equal(0.0, combined.AngleRad, 9);

            // Test 2: Rotated rectangles combination
            var rectA = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 0.5);
            var rectB = new GeoRectangle2(new GeoPoint2(5.0, 5.0), 2.0, 4.0, -0.2);

            GeoRectangle2 combinedRotated = rectA.Combine(rectB);

            // Angle of the combined rectangle must match rectA
            Assert.Equal(rectA.AngleRad, combinedRotated.AngleRad, 9);

            // Combined rectangle must contain all vertices of rectA and rectB
            foreach (var pt in rectA.GetVertices())
            {
                Assert.True(combinedRotated.Contains(pt));
            }
            foreach (var pt in rectB.GetVertices())
            {
                Assert.True(combinedRotated.Contains(pt));
            }
        }

        [Fact]
        public void Rectangle_Combine_Point_EnclosesRectangleAndPoint()
        {
            // Test 1: Point inside rectangle does not change size
            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 0.0);
            var combinedInside = rect.Combine(new GeoPoint2(1.0, 0.5));

            Assert.Equal(rect.Center.X, combinedInside.Center.X, 9);
            Assert.Equal(rect.Center.Y, combinedInside.Center.Y, 9);
            Assert.Equal(rect.Width, combinedInside.Width, 9);
            Assert.Equal(rect.Height, combinedInside.Height, 9);

            // Test 2: Point outside unrotated rectangle
            var pointOutside = new GeoPoint2(4.0, 3.0);
            var combinedOutside = rect.Combine(pointOutside);

            Assert.True(combinedOutside.Contains(pointOutside));
            foreach (var v in rect.GetVertices())
            {
                Assert.True(combinedOutside.Contains(v));
            }

            // Expected bounds: X from -2 to 4 (width = 6, centerX = 1), Y from -1 to 3 (height = 4, centerY = 1)
            Assert.Equal(1.0, combinedOutside.Center.X, 9);
            Assert.Equal(1.0, combinedOutside.Center.Y, 9);
            Assert.Equal(6.0, combinedOutside.Width, 9);
            Assert.Equal(4.0, combinedOutside.Height, 9);

            // Test 3: Point outside rotated rectangle
            var rotatedRect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, Math.PI / 4.0);
            var targetPoint = new GeoPoint2(5.0, 0.0);
            var combinedRotated = rotatedRect.Combine(targetPoint);

            Assert.Equal(rotatedRect.AngleRad, combinedRotated.AngleRad, 9);
            Assert.True(combinedRotated.Contains(targetPoint));
            foreach (var v in rotatedRect.GetVertices())
            {
                Assert.True(combinedRotated.Contains(v));
            }
        }

        [Fact]
        public void Rectangle_Combine_PointsArray_EnclosesAllPoints()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 2.0, 2.0, 0.0);
            
            // Empty / null collection returns this
            Assert.Equal(rect.Width, rect.Combine((GeoPoint2[])null).Width);
            Assert.Equal(rect.Width, rect.Combine(new GeoPoint2[0]).Width);

            // Multiple points
            var points = new[]
            {
                new GeoPoint2(-5.0, 0.0),
                new GeoPoint2(5.0, 0.0),
                new GeoPoint2(0.0, 4.0),
                new GeoPoint2(0.0, -4.0)
            };

            var combined = rect.Combine(points);

            Assert.Equal(0.0, combined.Center.X, 9);
            Assert.Equal(0.0, combined.Center.Y, 9);
            Assert.Equal(10.0, combined.Width, 9);
            Assert.Equal(8.0, combined.Height, 9);

            foreach (var p in points)
            {
                Assert.True(combined.Contains(p));
            }
            foreach (var v in rect.GetVertices())
            {
                Assert.True(combined.Contains(v));
            }
        }

        [Fact]
        public void Rectangle_GetClosestOnBoundary_WorksCorrectly()
        {
            var rect1 = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 4.0, 2.0, 0.0); // X in [-2, 2], Y in [-1, 1]
            var rect2 = new GeoRectangle2(new GeoPoint2(10.0, 0.0), 4.0, 2.0, 0.0); // X in [8, 12], Y in [-1, 1]

            // Test 1: Rectangle - Rectangle
            // Closest is from (2, 0) on rect1 to (8, 0) on rect2, length = 6
            var segRects = rect1.GetClosestOnBoundary(rect2);
            Assert.Equal(6.0, segRects.Length, 9);
            Assert.True(segRects.StartPoint.IsEqualTo(new GeoPoint2(2.0, 0.0)));
            Assert.True(segRects.EndPoint.IsEqualTo(new GeoPoint2(8.0, 0.0)));

            // Test 2: Rectangle - Circle
            var circle = new GeoCircle2(new GeoPoint2(0.0, 6.0), 2.0); // bottom of circle is (0, 4)
            var segCircle = rect1.GetClosestOnBoundary(circle);
            // Closest is from top edge of rect1 (0, 1) to bottom of circle (0, 4), length = 3
            Assert.Equal(3.0, segCircle.Length, 9);
            Assert.True(segCircle.StartPoint.IsEqualTo(new GeoPoint2(0.0, 1.0)));
            Assert.True(segCircle.EndPoint.IsEqualTo(new GeoPoint2(0.0, 4.0)));
        }

        [Fact]
        public void Rectangle_Combine_RectangleOverload_MatchesTheVertexOverload()
        {
            // Combining with a rectangle is combining with its four corners, and the two routes have to
            // stay identical now that they share one implementation.
            var baseRect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 10.0, 10.0, 0.0);
            var other = new GeoRectangle2(new GeoPoint2(20.0, 3.0), 4.0, 4.0, 0.7);

            Assert.True(baseRect.Combine(other).Equals(baseRect.Combine(other.GetVertices())));

            var rotated = new GeoRectangle2(new GeoPoint2(-3.0, 2.0), 6.0, 2.0, Math.PI / 5.0);
            Assert.True(rotated.Combine(other).Equals(rotated.Combine(other.GetVertices())));
        }

        [Fact]
        public void Rectangle_Combine_NullOrEmptyInput_LeavesTheRectangleUnchanged()
        {
            var rect = new GeoRectangle2(new GeoPoint2(1.0, 2.0), 4.0, 3.0, 0.4);

            Assert.True(rect.Combine().Equals(rect));
            Assert.True(rect.Combine(new GeoPoint2[0]).Equals(rect));
            Assert.True(rect.Combine((GeoPoint2[])null).Equals(rect));
            Assert.True(rect.Combine((IEnumerable<GeoPoint2>)null).Equals(rect));
            Assert.True(rect.Combine(new List<GeoPoint2>()).Equals(rect));
        }

        [Fact]
        public void Rectangle_Combine_WalksTheSequenceOnlyOnce()
        {
            // A caller may hand in a sequence that cannot be replayed, so the bounds have to be built in
            // a single pass.
            int passes = 0;
            IEnumerable<GeoPoint2> OnePassOnly()
            {
                passes++;
                yield return new GeoPoint2(20.0, 0.0);
                yield return new GeoPoint2(0.0, 20.0);
            }

            var rect = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 10.0, 10.0, 0.0);
            var combined = rect.Combine(OnePassOnly());

            Assert.Equal(1, passes);
            Assert.Equal(25.0, combined.Width, 9);
            Assert.Equal(25.0, combined.Height, 9);
        }

        [Fact]
        public void Rectangle_Combine_OnePassMatchesAccumulatingPointByPoint()
        {
            var rnd = new Random(7);
            var points = new List<GeoPoint2>();
            for (int i = 0; i < 2000; i++)
            {
                points.Add(new GeoPoint2(rnd.NextDouble() * 100.0 - 50.0, rnd.NextDouble() * 100.0 - 50.0));
            }

            var start = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 10.0, 10.0, Math.PI / 7.0);

            var oneShot = start.Combine(points);

            var accumulated = start;
            foreach (var p in points)
            {
                accumulated = accumulated.Combine(p);
            }

            Assert.Equal(oneShot.Width, accumulated.Width, 9);
            Assert.Equal(oneShot.Height, accumulated.Height, 9);
            Assert.Equal(0.0, oneShot.Center.DistanceTo(accumulated.Center), 9);

            foreach (var p in points)
            {
                Assert.True(oneShot.Contains(p));
            }
        }

        [Fact]
        public void Rectangle_Combine_DegenerateRectangle_GrowsToReachThePoint()
        {
            var flat = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 0.0, 0.0, 0.0);
            var grown = flat.Combine(new GeoPoint2(3.0, 4.0));

            Assert.Equal(3.0, grown.Width, 9);
            Assert.Equal(4.0, grown.Height, 9);
            Assert.True(grown.Contains(new GeoPoint2(3.0, 4.0)));
            Assert.True(grown.Contains(new GeoPoint2(0.0, 0.0)));
        }
    }
}


