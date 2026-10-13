using System;
using GeometryHelper.Arranging;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A straight closed shape (polygon, rectangle, triangle, face) collides with a segment, a ray, a polyline or another
    /// of them where they cross, one holds the other, or they come within the point tolerance of each other, the rims of
    /// a face's holes included; and Arranging asks it of every obstacle within the tolerance of a label's place.
    /// </summary>
    /// <remarks>
    /// The straight closed shapes asked only for a crossing, found within the end slack of two segments, or for a start
    /// or a corner inside; so did polyline against polyline (F2, Q1). A segment ending half a thousandth above the top
    /// of a square at a shallow angle crossed nothing within the slack and started 0.01 off, so it did not collide at a
    /// thousandth, while the same square as a curved loop said it did. Their distances already read the gap, and stay
    /// as they are. Arranging dropped every obstacle whose box did not overlap the label's before asking at all (F3).
    /// </remarks>
    public class CollidesB7StraightRegionNearMissTests
    {
        /// <summary>
        /// The segment from (40, 100.01) down to (60, 100.0005): it ends half a thousandth above the top of the square
        /// from the origin to (100, 100). Its line meets the top 1.05 past its end, outside the end slack at a
        /// thousandth, and it starts 0.01 above the top.
        /// </summary>
        private static GeoLine2 EndingAboveTheTop(double gap) => new GeoLine2(P(40, 100.01), P(60, 100 + gap));

        private static GeoPolygon2 SquarePolygon() => new GeoPolygon2(P(0, 0), P(100, 0), P(100, 100), P(0, 100));

        private static GeoRectangle2 SquareRectangle() => new GeoRectangle2(P(50, 50), 100, 100);

        /// <summary>The triangle whose top side is the top of the square.</summary>
        private static GeoTriangle2 TopTriangle() => new GeoTriangle2(P(0, 0), P(100, 100), P(0, 100));

        /// <summary>The square with a hole 20 by 20 in its middle, from (40, 40) to (60, 60).</summary>
        private static GeoFace2 SquareWithAHole()
            => new GeoFace2(SquarePolygon(), new[] { new GeoPolygon2(P(40, 40), P(60, 40), P(60, 60), P(40, 60)) });

        /// <summary>
        /// The quadrilateral with its top corner at (0, 100) and its two upper sides falling from there at 10 degrees, to
        /// (-50, 91.18) and (50, 91.18), closed through (0, 0).
        /// </summary>
        private static GeoPoint2[] PeakCorners()
        {
            double drop = 50 * Math.Tan(10 * Math.PI / 180);

            return new[] { P(-50, 100 - drop), P(0, 100), P(50, 100 - drop), P(0, 0) };
        }

        /// <summary>
        /// The ray along X from (-100, 100 + gap): it passes the peak gap above it. The peak's sides meet its line
        /// gap / tan 10 degrees either side, 0.0029 out along them for a gap of 0.0005, outside the end slack at a
        /// thousandth.
        /// </summary>
        private static GeoRay2 PassingOverThePeak(double gap) => new GeoRay2(P(-100, 100 + gap), GeoVector2.XAxis);

        [Fact]
        public void ASegmentEndingFiveTenThousandthsAboveASquare_CollidesAtAThousandthAsThePolygonRectangleTriangleAndFace()
        {
            // The audit's repro. On 2755f30 all four said false at 1E-3; the same square as a GeoPolygonArc2 said true.
            GeoLine2 segment = EndingAboveTheTop(0.0005);
            GeoPolygon2 polygon = SquarePolygon();
            GeoRectangle2 rectangle = SquareRectangle();
            GeoTriangle2 triangle = TopTriangle();
            GeoFace2 face = SquareWithAHole();

            foreach (var tolerance in new[] { Thousandth, Tenth })
            {
                Assert.True(Collision2.CollidesWith(polygon, segment, tolerance));
                Assert.True(polygon.CollidesWith(segment, tolerance));
                Assert.True(segment.CollidesWith(polygon, tolerance));
                Assert.True(Collision2.CollidesWith(rectangle, segment, tolerance));
                Assert.True(rectangle.CollidesWith(segment, tolerance));
                Assert.True(segment.CollidesWith(rectangle, tolerance));
                Assert.True(Triangle2.CollidesWith(triangle, segment, tolerance));
                Assert.True(triangle.CollidesWith(segment, tolerance));
                Assert.True(segment.CollidesWith(triangle, tolerance));
                Assert.True(Face2.CollidesWith(face, segment, tolerance));
                Assert.True(face.CollidesWith(segment, tolerance));
                Assert.True(segment.CollidesWith(face, tolerance));
            }
        }

        [Fact]
        public void ASegmentEndingFiveTenThousandthsAboveASquare_DoesNotCollideAtATenThousandth()
        {
            // A guard: the gap is five times the tolerance.
            GeoLine2 segment = EndingAboveTheTop(0.0005);

            Assert.False(segment.CollidesWith(SquarePolygon(), TenThousandth));
            Assert.False(segment.CollidesWith(SquareRectangle(), TenThousandth));
            Assert.False(segment.CollidesWith(TopTriangle(), TenThousandth));
            Assert.False(segment.CollidesWith(SquareWithAHole(), TenThousandth));
        }

        [Fact]
        public void ASegmentEndingTwoThousandthsAboveASquare_DoesNotCollideAtAThousandth()
        {
            // A guard: twice the tolerance.
            GeoLine2 segment = EndingAboveTheTop(0.002);

            Assert.False(segment.CollidesWith(SquarePolygon(), Thousandth));
            Assert.False(segment.CollidesWith(SquareRectangle(), Thousandth));
            Assert.False(segment.CollidesWith(TopTriangle(), Thousandth));
            Assert.False(segment.CollidesWith(SquareWithAHole(), Thousandth));
        }

        [Fact]
        public void ASegmentEndingFiveTenThousandthsAboveASquare_KeepsItsDistanceOfTheGap()
        {
            // A guard: the distances read the gap on 2755f30 and keep it.
            GeoLine2 segment = EndingAboveTheTop(0.0005);

            Assert.Equal(0.0005, SquarePolygon().DistanceTo(segment), 1E-9);
            Assert.Equal(0.0005, SquareRectangle().DistanceTo(segment), 1E-9);
            Assert.Equal(0.0005, SquareWithAHole().DistanceTo(segment, Thousandth), 1E-9);
            Assert.Equal(0.0005, SquareWithAHole().DistanceTo(segment, TenThousandth), 1E-9);
        }

        [Fact]
        public void APolylineEndingFiveTenThousandthsAboveASquare_CollidesWithThePolygonAndTheRectangleAtAThousandth()
        {
            // The polyline forms go edge by edge through the segment's.
            var polyline = new GeoPolyline2(P(20, 120), P(40, 100.01), P(60, 100.0005));

            Assert.True(polyline.CollidesWith(SquarePolygon(), Thousandth));
            Assert.True(SquarePolygon().CollidesWith(polyline, Thousandth));
            Assert.True(polyline.CollidesWith(SquareRectangle(), Thousandth));
            Assert.True(SquareRectangle().CollidesWith(polyline, Thousandth));
            Assert.False(polyline.CollidesWith(SquarePolygon(), TenThousandth));
        }

        [Fact]
        public void ASegmentInAHoleEndingFiveTenThousandthsShortOfItsRim_ReachesTheMaterialAtAThousandth()
        {
            // From (41, 58) up to (50, 59.9995), inside the hole of the face, ending half a thousandth below the hole's
            // top rim at y = 60, rising at 12.5 degrees: its line meets the rim 0.0023 past its end. The material lies
            // half a thousandth away. On 2755f30 the hole swallowed it.
            var segment = new GeoLine2(P(41, 58), P(50, 59.9995));
            GeoFace2 face = SquareWithAHole();

            Assert.True(face.CollidesWith(segment, Thousandth));
            Assert.True(segment.CollidesWith(face, Thousandth));
            Assert.False(face.CollidesWith(segment, TenThousandth));
            Assert.Equal(0.0005, face.DistanceTo(segment, Thousandth), 1E-9);
        }

        [Fact]
        public void ARayPassingFiveTenThousandthsOverAPeak_CollidesWithThePolygonTriangleAndFaceAtAThousandth()
        {
            GeoRay2 ray = PassingOverThePeak(0.0005);
            GeoPoint2[] c = PeakCorners();
            var polygon = new GeoPolygon2(c);
            var triangle = new GeoTriangle2(c[0], c[1], c[2]);
            var face = new GeoFace2(polygon, new[] { new GeoPolygon2(P(-5, 45), P(5, 45), P(5, 55), P(-5, 55)) });

            foreach (var tolerance in new[] { Thousandth, Tenth })
            {
                Assert.True(Collision2.CollidesWith(polygon, ray, tolerance));
                Assert.True(polygon.CollidesWith(ray, tolerance));
                Assert.True(ray.CollidesWith(polygon, tolerance));
                Assert.True(triangle.CollidesWith(ray, tolerance));
                Assert.True(ray.CollidesWith(triangle, tolerance));
                Assert.True(face.CollidesWith(ray, tolerance));
                Assert.True(ray.CollidesWith(face, tolerance));
            }

            Assert.False(ray.CollidesWith(polygon, TenThousandth));
            Assert.False(ray.CollidesWith(triangle, TenThousandth));
            Assert.False(ray.CollidesWith(face, TenThousandth));
        }

        [Fact]
        public void ARayPassingTwoThousandthsOverAPeak_DoesNotCollideAtAThousandth()
        {
            // A guard: twice the tolerance.
            GeoRay2 ray = PassingOverThePeak(0.002);
            GeoPoint2[] c = PeakCorners();

            Assert.False(ray.CollidesWith(new GeoPolygon2(c), Thousandth));
            Assert.False(ray.CollidesWith(new GeoTriangle2(c[0], c[1], c[2]), Thousandth));
        }

        [Fact]
        public void ASegmentWhollyInsideASquareOrAcrossIt_StillCollides()
        {
            // Guards: one held whole, one crossing.
            var inside = new GeoLine2(P(10, 10), P(20, 30));
            var across = new GeoLine2(P(-10, 50), P(110, 50));

            foreach (var tolerance in new[] { TenThousandth, Thousandth, Tenth })
            {
                Assert.True(inside.CollidesWith(SquarePolygon(), tolerance));
                Assert.True(inside.CollidesWith(SquareRectangle(), tolerance));
                Assert.True(across.CollidesWith(SquarePolygon(), tolerance));
                Assert.True(across.CollidesWith(SquareRectangle(), tolerance));
                Assert.True(across.CollidesWith(SquareWithAHole(), tolerance));
            }
        }

        #region F2 (Q1): two straight closed shapes, and polyline against polyline

        /// <summary>
        /// The diamond whose lowest corner, (60, 100 + gap), hangs above the top of the square, its two lower sides rising
        /// from there to (40, 100.01) and (80, 100.01): for a gap of 0.0005 their lines meet the top 1.05 out, outside the
        /// end slack, and no corner of either lies inside the other.
        /// </summary>
        private static GeoPolygon2 HangingDiamond(double gap)
            => new GeoPolygon2(P(40, 100.01), P(60, 100 + gap), P(80, 100.01), P(60, 120));

        [Fact]
        public void ADiamondHangingFiveTenThousandthsAboveASquare_CollidesWithThePolygonRectangleTriangleAndFace()
        {
            // Polygon against polygon (Collision2.cs:312) and against the rectangle (:241, what Arranging asks of a
            // polygon obstacle), the triangle and the face; both ways round.
            GeoPolygon2 diamond = HangingDiamond(0.0005);

            foreach (var tolerance in new[] { Thousandth, Tenth })
            {
                Assert.True(diamond.CollidesWith(SquarePolygon(), tolerance));
                Assert.True(SquarePolygon().CollidesWith(diamond, tolerance));
                Assert.True(Collision2.CollidesWith(SquareRectangle(), diamond, tolerance));
                Assert.True(diamond.CollidesWith(SquareRectangle(), tolerance));
                Assert.True(diamond.CollidesWith(TopTriangle(), tolerance));
                Assert.True(TopTriangle().CollidesWith(diamond, tolerance));
                Assert.True(diamond.CollidesWith(SquareWithAHole(), tolerance));
                Assert.True(SquareWithAHole().CollidesWith(diamond, tolerance));
            }

            Assert.False(diamond.CollidesWith(SquarePolygon(), TenThousandth));
            Assert.False(SquareRectangle().CollidesWith(diamond, TenThousandth));
        }

        [Fact]
        public void ADiamondHangingTwoThousandthsAboveASquare_DoesNotCollideAtAThousandth()
        {
            // A guard.
            GeoPolygon2 diamond = HangingDiamond(0.002);

            Assert.False(diamond.CollidesWith(SquarePolygon(), Thousandth));
            Assert.False(SquareRectangle().CollidesWith(diamond, Thousandth));
            Assert.False(diamond.CollidesWith(TopTriangle(), Thousandth));
        }

        [Fact]
        public void TwoTrianglesAndTwoFacesFiveTenThousandthsApart_CollideAtAThousandth()
        {
            // The flat triangle (40, 100.01), (60, 100.0005), (80, 100.01) above the top triangle; the diamond as a face
            // with a small hole, above the square face.
            var flat = new GeoTriangle2(P(40, 100.01), P(60, 100.0005), P(80, 100.01));
            var diamondFace = new GeoFace2(HangingDiamond(0.0005), new[] { new GeoPolygon2(P(58, 108), P(62, 108), P(60, 112)) });

            Assert.True(flat.CollidesWith(TopTriangle(), Thousandth));
            Assert.True(TopTriangle().CollidesWith(flat, Thousandth));
            Assert.True(Triangle2.CollidesWith(flat, TopTriangle(), Thousandth));
            Assert.True(diamondFace.CollidesWith(SquareWithAHole(), Thousandth));
            Assert.True(SquareWithAHole().CollidesWith(diamondFace, Thousandth));
            Assert.True(flat.CollidesWith(SquareWithAHole(), Thousandth));
            Assert.False(flat.CollidesWith(TopTriangle(), TenThousandth));
        }

        [Fact]
        public void APolylineEndingFiveTenThousandthsAboveAnother_CollidesWithItAtAThousandth()
        {
            // As a segment it does, since D3 of the segment-fault round; as a polyline of one edge it did not
            // (Collision2.cs:481).
            var descending = new GeoPolyline2(P(40, 100.01), P(60, 100.0005));
            var top = new GeoPolyline2(P(0, 100), P(100, 100));

            Assert.True(new GeoLine2(P(40, 100.01), P(60, 100.0005)).CollidesWith(top, Thousandth));
            Assert.True(descending.CollidesWith(top, Thousandth));
            Assert.True(top.CollidesWith(descending, Thousandth));
            Assert.False(descending.CollidesWith(top, TenThousandth));
        }

        #endregion

        #region F3: Arranging asks every obstacle within the tolerance of a label's place

        /// <summary>
        /// One label: the box 40 by 10 on the leader from the origin to (60, 0), offset 5, so that its first place spans
        /// (10, 5) to (50, 15). A line comes down at 10 degrees to end the gap above the middle of that place's top side,
        /// and a polygon's shallow V hangs the gap below the middle of its bottom side: both outside the place's box, so
        /// that Arranging's box prefilter dropped them before asking CollidesWith.
        /// </summary>
        private static ArrangeItem LabelBetweenTwoObstacles(double gap)
        {
            double s = 10 * Math.PI / 180;
            double rise = 10 * Math.Tan(s);

            return new ArrangeItem
            {
                Leader = new GeoLine2(P(0, 0), P(60, 0)),
                Box = new GeoRectangle2(P(30, 0), 40, 10),
                Offset = 5,
                BlockLines = new[] { new GeoLine2(P(30 - 20 * Math.Cos(s), 15 + gap + 20 * Math.Sin(s)), P(30, 15 + gap)) },
                BlockPolygons = new[] { new GeoPolygon2(P(20, 5 - gap - rise), P(30, 5 - gap), P(40, 5 - gap - rise), P(30, -20)) },
            };
        }

        /// <summary>The least distance from where the label was put to its two obstacles.</summary>
        private static double Clearance(ArrangeItem item, ArrangeResult result)
        {
            var placed = new GeoRectangle2(P(item.Box.Center.X + result.Translation.X, item.Box.Center.Y + result.Translation.Y), item.Box.Width, item.Box.Height);

            return Math.Min(placed.DistanceTo(item.BlockLines[0]), placed.DistanceTo(item.BlockPolygons[0]));
        }

        [Fact]
        public void ALabelWhosePlaceIsFiveTenThousandthsFromTwoObstacles_IsNotPutThereAtAThousandth()
        {
            // On 2755f30 the label was put at its first place, half a thousandth from both obstacles.
            ArrangeItem item = LabelBetweenTwoObstacles(0.0005);
            ArrangeResult result = Assert.Single(Arranger.Run(new[] { item }, new ArrangeOptions { Tolerance = Thousandth }));

            Assert.True(!result.Placed || Clearance(item, result) > 1E-3);
        }

        [Fact]
        public void ALabelWhosePlaceIsFiveTenThousandthsFromTwoObstacles_IsPutClearOfThemAtATenThousandth()
        {
            // A guard: at 1E-4 the place is clear of both.
            ArrangeItem item = LabelBetweenTwoObstacles(0.0005);
            ArrangeResult result = Assert.Single(Arranger.Run(new[] { item }, new ArrangeOptions { Tolerance = TenThousandth }));

            Assert.True(result.Placed);
            Assert.True(Clearance(item, result) > 1E-4);
        }

        #endregion
    }
}
