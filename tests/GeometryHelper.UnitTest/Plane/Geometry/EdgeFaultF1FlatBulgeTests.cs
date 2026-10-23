using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An edge that bows less off its chord than the centre of its arc can be placed to is read as its chord: the edge from
    /// (0, 0) to (100, 0) with a bulge of 1E-12, on a radius of 2.5E13, put its middle at (50.002, 0).
    /// </summary>
    public class EdgeFaultF1FlatBulgeTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        [Theory]
        [InlineData(1E-12)]
        [InlineData(-1E-12)]
        [InlineData(1E-9)]
        public void AnEdgeBowingLessThanItsCentreIsKnownTo_IsItsChord(double bulge)
        {
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), bulge);

            Assert.False(edge.IsArc);
            Assert.Equal(bulge, edge.Bulge);
            Assert.Equal(new GeoPoint2(50, 0), edge.MidPoint);
            Assert.Equal(new GeoPoint2(50, 0), edge.GetPointAtParameter(0.5));
            Assert.Equal(100.0, edge.Length);
            Assert.Equal(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0)), edge.ToLine());
            Assert.Equal(1.0, edge.ToArc().Radius / (100 * (1 + bulge * bulge) / (4 * Math.Abs(bulge))), 9);
            Assert.Equal(0.0, edge.DistanceTo(new GeoPoint2(50, 0), Thousandth));
        }

        [Fact]
        public void ASquareWhoseBottomBowsATrillionth_HasNoArcs()
        {
            var corners = new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) };
            var square = new GeoPolygonArc2(corners, new[] { 1E-12, 0.0, 0.0, 0.0 });
            var chain = new GeoPolylineArc2(corners, new[] { 1E-12, 0.0, 0.0, 0.0 });

            Assert.False(square.HasArcs);
            Assert.False(chain.HasArcs);
            Assert.True(new GeoPolygonArc2(corners, new[] { 1E-7, 0.0, 0.0, 0.0 }).HasArcs);
        }

        [Fact]
        public void AnEdgeBowingATenMillionthOfItsChord_IsStillAnArc()
        {
            // Bowing 5E-6 on a radius of 2.5E8, its centre is placed far closer than that.
            var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1E-7);

            Assert.True(edge.IsArc);
            Assert.Equal(-5E-6, edge.MidPoint.Y, 6);
        }
    }
}
