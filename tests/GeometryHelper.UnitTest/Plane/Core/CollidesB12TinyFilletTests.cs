using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A fillet smaller than the global tolerance keeps its arcs, and the sides after it stay straight.
    /// </summary>
    /// <remarks>
    /// A fillet of radius 0.0005 leaves its two tangent points 0.0007 apart, within the global point tolerance, so the
    /// loop dropped the second. It kept the fillet's bulge on the first, which then bent the whole side after it: the
    /// rectangle 100 by 50 filleted so at a tolerance of 1E-4 came back with arcs of radius 35 and 71 and an area of
    /// 7 140 for 5 000. The loop now keeps every arc the fillet leaves, as a chain does, so the four corners are
    /// four arcs of radius 0.0005.
    /// </remarks>
    public class CollidesB12TinyFilletTests
    {
        [Fact]
        public void ARectangleFilletedByLessThanTheGlobalTolerance_KeepsStraightSidesAndItsArea()
        {
            var rectangle = new GeoPolygon2(new[] { P(0, 0), P(100, 0), P(100, 50), P(0, 50) });

            GeoPolygonArc2 filleted = Corner2.Fillet(rectangle, 0.0005, TenThousandth);

            Assert.Equal(8, filleted.VertexCount);
            Assert.Equal(4, filleted.Bulges.Count(bulge => bulge != 0.0));
            Assert.Equal(5000.0, filleted.Area, 0.1);
        }
    }
}
