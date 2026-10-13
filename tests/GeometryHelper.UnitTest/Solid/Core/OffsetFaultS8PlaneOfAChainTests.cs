using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A flat open chain has a plane whatever its shape, so a flat Z chain with an arc can be offset in it.
    /// </summary>
    /// <remarks>
    /// The plane of an open chain was taken from Newell's area vector of its vertices closed into a loop. The Z through
    /// (0, 0), (10, 0), (0, 10), (10, 10) closes into a bow tie whose two halves cancel: an area of 0, so no normal, and
    /// the flat chain had no plane. With a bulge of 0.2 on its first edge, <c>Offset3.OffsetInPlane</c> threw "The chain
    /// does not lie in one plane, so it has no side to offset to". The same chain in the plane offsets.
    /// </remarks>
    public class OffsetFaultS8PlaneOfAChainTests
    {
        private static readonly GeoPoint3[] Z = { P(0, 0, 2), P(10, 0, 2), P(0, 10, 2), P(10, 10, 2) };

        [Fact]
        public void AFlatZChain_HasThePlaneItLiesIn()
        {
            var chain = new GeoPolyline3(Z);

            Assert.True(chain.TryGetPlane(out GeoPlane3 plane, Thousandth));
            Assert.Equal(1.0, Math.Abs(plane.Normal.Z), 1E-12);
            Assert.Equal(2.0, plane.Origin.Z, 1E-12);
        }

        [Fact]
        public void AFlatZChainWithAnArc_HasThePlaneItLiesIn()
        {
            var chain = new GeoPolylineArc3(Z, new[] { 0.2, 0.0, 0.0, 0.0 }, Enumerable.Repeat(new GeoVector3(0, 0, 1), 4));

            Assert.True(chain.TryGetPlane(out GeoPlane3 plane, Thousandth));
            Assert.Equal(1.0, Math.Abs(plane.Normal.Z), 1E-12);
        }

        [Fact]
        public void AFlatZChainWithAnArc_IsOffsetInItsPlane()
        {
            // The first edge bows 1 below the axis (bulge 0.2: radius 13 about (5, 12, 2)); moved 0.3 to the left, towards
            // its centre, it starts at (0.11538, 0.27692, 2). Whatever the rest, every point stays in z = 2.
            var chain = new GeoPolylineArc3(Z, new[] { 0.2, 0.0, 0.0, 0.0 }, Enumerable.Repeat(new GeoVector3(0, 0, 1), 4));

            GeoPolylineArc3[] offset = Offset3.OffsetInPlane(chain, 0.3, new GeoVector3(0, 0, 1), OffsetOptions.Default, Thousandth);

            Assert.NotEmpty(offset);
            Assert.All(offset.SelectMany(c => c.Vertices), v => Assert.Equal(2.0, v.Z, 1E-9));
            Assert.Equal(0.0, offset[0].StartPoint.DistanceTo(P(0.11538461538461586, 0.2769230769230777, 2)), 1E-9);
        }

        [Fact]
        public void AFlatChainThatClosesIntoALoopWithArea_HadItsPlaneAlready()
        {
            // Guard: an L closes into a triangle of area 50, so Newell's vector found the plane.
            var chain = new GeoPolyline3(P(0, 0, 2), P(10, 0, 2), P(10, 10, 2));

            Assert.True(chain.TryGetPlane(out GeoPlane3 plane, Thousandth));
            Assert.Equal(1.0, Math.Abs(plane.Normal.Z), 1E-12);
        }
    }
}
