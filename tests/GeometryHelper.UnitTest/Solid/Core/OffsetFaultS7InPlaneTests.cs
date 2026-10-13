using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A straight chain offset in its plane in space has the plane's answer: <c>PlanarRegion3</c> carries its own copy of
    /// the polyline offset, which kept the same stretches past the other leg of a sharp inside corner
    /// (<see cref="Plane.OffsetFaultS7PolylineTests"/>).
    /// </summary>
    public class OffsetFaultS7InPlaneTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        [Fact]
        public void TheInsideOfASharpCornerOffsetByOneInItsPlane_IsOneChain()
        {
            // The chain (10, 10, 5), (0, 0, 5), (10, 0, 5), offset by 1 to the left seen from +z: one chain from
            // (10.70711, 9.29289, 5) through (1 + sqrt 2, 1, 5) to (10, 1, 5). On the pin two more pieces came with it,
            // (1.414, 0, 5) to (0.707, -0.707, 5) and (0, 1, 5) to (1, 1, 5).
            var chain = new GeoPolyline3(P(10, 10, 5), P(0, 0, 5), P(10, 0, 5));

            GeoPolyline3[] offset = Offset3.OffsetInPlane(chain, 1.0, Up, OffsetOptions.Default, Thousandth);

            GeoPolyline3 one = Assert.Single(offset);
            Assert.Equal(3, one.VertexCount);
            Assert.Equal(0.0, one.Vertices[0].DistanceTo(P(10 + Math.Sqrt(0.5), 10 - Math.Sqrt(0.5), 5)), 1E-9);
            Assert.Equal(0.0, one.Vertices[1].DistanceTo(P(1 + Math.Sqrt(2.0), 1, 5)), 1E-9);
            Assert.Equal(0.0, one.Vertices[2].DistanceTo(P(10, 1, 5)), 1E-9);
        }

        [Fact]
        public void TheOutsideOfTheSameCornerOffsetInItsPlane_IsMitred()
        {
            // Guard: to the right, the outside, the legs move out by 1 and meet at the mitre (-1 - sqrt 2, -1, 5),
            // 1 / sin 22.5 deg = 2.613 from the corner, within the limit of 10.
            var chain = new GeoPolyline3(P(10, 10, 5), P(0, 0, 5), P(10, 0, 5));

            GeoPolyline3 one = Assert.Single(Offset3.OffsetInPlane(chain, -1.0, Up, OffsetOptions.Default, Thousandth));

            Assert.Equal(3, one.VertexCount);
            Assert.Equal(0.0, one.Vertices[1].DistanceTo(P(-1 - Math.Sqrt(2.0), -1, 5)), 1E-9);
        }
    }
}
