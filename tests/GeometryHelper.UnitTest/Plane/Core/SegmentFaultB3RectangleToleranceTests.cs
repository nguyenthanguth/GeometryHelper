using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment against a rectangle reads the tolerance it is given.
    /// </summary>
    /// <remarks>
    /// The rectangle's segment test asked whether the segment's start lay in the rectangle with the global tolerance,
    /// 1E-3, whatever tolerance it was given. A segment starting 0.0002 outside the rectangle 100 by 100, asked at 1E-4,
    /// collided through its start. The polyline and the straight edge go through the same test, and did the same.
    /// <see cref="Tolerance.Global"/> is left at its default in every test here.
    /// </remarks>
    public class SegmentFaultB3RectangleToleranceTests
    {
        /// <summary>From (50.0002, 0), two ten-thousandths right of the rectangle's right side, to (60, 0).</summary>
        private static GeoLine2 JustOutside() => new GeoLine2(P(50.0002, 0), P(60, 0));

        [Fact]
        public void ASegmentStartingTwoTenThousandthsOutside_DoesNotCollideAtATenThousandth()
        {
            // The gap, 0.0002, is twice the tolerance: no crossing of the side x = 50, and the start is outside.
            GeoRectangle2 square = Square();
            GeoLine2 segment = JustOutside();

            Assert.False(Collision2.CollidesWith(square, segment, TenThousandth));
            Assert.False(square.CollidesWith(segment, TenThousandth));
            Assert.False(segment.CollidesWith(square, TenThousandth));
        }

        [Fact]
        public void ThePolylineAndTheEdgeStartingThere_DoNotCollideAtATenThousandth()
        {
            // The polyline from (50.0002, 0) to (60, 0) and up to (60, 10), and the straight edge of its first leg, ask
            // the rectangle the same question edge by edge.
            GeoRectangle2 square = Square();
            var polyline = new GeoPolyline2(P(50.0002, 0), P(60, 0), P(60, 10));
            var edge = new GeoEdge2(P(50.0002, 0), P(60, 0));

            Assert.False(Collision2.CollidesWith(polyline, square, TenThousandth));
            Assert.False(square.CollidesWith(polyline, TenThousandth));
            Assert.False(edge.CollidesWith(square, TenThousandth));
        }

        [Fact]
        public void TheSameSegmentAtAThousandth_Collides()
        {
            // The gap of 0.0002 is within 1E-3: the start lies on the side.
            Assert.True(Square().CollidesWith(JustOutside(), Thousandth));
        }

        [Fact]
        public void ASegmentStartingTwoTenThousandthsInside_CollidesAtATenThousandth()
        {
            // From (49.9998, 0) it crosses the side x = 50 at (50, 0).
            Assert.True(Square().CollidesWith(new GeoLine2(P(49.9998, 0), P(60, 0)), TenThousandth));
        }

        [Fact]
        public void ASegmentWhollyInside_CollidesAtATenThousandth()
        {
            // From (10, 0) to (20, 0), forty from every side: no crossing, held by the start.
            Assert.True(Square().CollidesWith(new GeoLine2(P(10, 0), P(20, 0)), TenThousandth));
        }
    }
}
