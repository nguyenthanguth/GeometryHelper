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
    /// A curved chain whose vertices all lie on one line still has a plane: the one its arcs turn in, when they agree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plane of a curved chain was asked of its vertices alone, and vertices on one line give none. The half circle
    /// from (0, 0, 2) to (10, 0, 2), bulge 1 about +Z (radius 5 about (5, 0, 2), through (5, -5, 2)), had no plane, and
    /// <c>Offset3.OffsetInPlane</c> threw "The chain does not lie in one plane, so it has no side to offset to". It lies in
    /// z = 2. Moved 1 to the left about +Z, which at its start, running along -y, is +x, towards the centre: the half
    /// circle of radius 4 about (5, 0, 2) from (1, 0, 2) to (9, 0, 2).
    /// </para>
    /// <para>
    /// The S of two arcs along the x axis, (0, 0, 2) to (10, 0, 2) with bulge 0.5 and on to (20, 0, 2) with bulge -0.5, both
    /// about +Z: each turns 4 atan 0.5 = 106.26 degrees on a radius of 6.25, the first about (5, 3.75, 2) and the second
    /// about (15, -3.75, 2), and they meet at (10, 0, 2) along one tangent, (0.6, 0.8, 0). Moved 1 to the left, the first
    /// comes towards its centre (radius 5.25) and the second goes away from its own (radius 7.25): from (0.8, 0.6, 2)
    /// through (9.2, 0.6, 2) to (20.8, 0.6, 2), still one chain, since the tangent does not turn where they meet.
    /// </para>
    /// <para>
    /// Arcs that turn in different planes leave the chain without one: the same two arcs with the second turning about +Y
    /// instead are still refused.
    /// </para>
    /// </remarks>
    public class OffsetFaultS8PlaneOfArcsTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        private static GeoPolylineArc3 HalfCircle()
            => new GeoPolylineArc3(new[] { P(0, 0, 2), P(10, 0, 2) }, new[] { 1.0, 0.0 }, Enumerable.Repeat(Up, 2));

        private static GeoPolylineArc3 S(GeoVector3 secondNormal)
            => new GeoPolylineArc3(new[] { P(0, 0, 2), P(10, 0, 2), P(20, 0, 2) }, new[] { 0.5, -0.5, 0.0 }, new[] { Up, secondNormal, Up });

        [Fact]
        public void AHalfCircleWhoseEndsAloneGiveNoPlane_HasThePlaneItTurnsIn()
        {
            Assert.True(HalfCircle().TryGetPlane(out GeoPlane3 plane, Thousandth));
            Assert.Equal(1.0, Math.Abs(plane.Normal.Z), 1E-12);
            Assert.Equal(2.0, plane.Origin.Z, 1E-12);
        }

        [Fact]
        public void AHalfCircleOffsetByOneInItsPlane_IsTheHalfCircleOfFourInside()
        {
            GeoPolylineArc3 one = Assert.Single(Offset3.OffsetInPlane(HalfCircle(), 1.0, Up, OffsetOptions.Default, Thousandth));

            Assert.Equal(2, one.VertexCount);
            Assert.Equal(0.0, one.StartPoint.DistanceTo(P(1, 0, 2)), 1E-9);
            Assert.Equal(0.0, one.EndPoint.DistanceTo(P(9, 0, 2)), 1E-9);
            Assert.Equal(1.0, Math.Abs(one.Bulges[0]), 1E-9);
        }

        [Fact]
        public void AnSOfTwoArcsOnOneLine_IsOffsetByOneAsOneChain()
        {
            GeoPolylineArc3 one = Assert.Single(Offset3.OffsetInPlane(S(Up), 1.0, Up, OffsetOptions.Default, Thousandth));

            Assert.Equal(3, one.VertexCount);
            Assert.Equal(0.0, one.Vertices[0].DistanceTo(P(0.8, 0.6, 2)), 1E-9);
            Assert.Equal(0.0, one.Vertices[1].DistanceTo(P(9.2, 0.6, 2)), 1E-9);
            Assert.Equal(0.0, one.Vertices[2].DistanceTo(P(20.8, 0.6, 2)), 1E-9);
        }

        [Fact]
        public void TwoArcsOnOneLineTurningInDifferentPlanes_StillHaveNoPlane()
        {
            // Guard: the second arc turns about +Y, out of z = 2.
            GeoPolylineArc3 chain = S(new GeoVector3(0, 1, 0));

            Assert.False(chain.TryGetPlane(out _, Thousandth));
            Assert.Throws<ArgumentException>(() => Offset3.OffsetInPlane(chain, 1.0, Up, OffsetOptions.Default, Thousandth));
        }
    }
}
