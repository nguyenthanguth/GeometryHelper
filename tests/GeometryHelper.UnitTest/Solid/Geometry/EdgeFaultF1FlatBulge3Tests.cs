using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An edge in space that bows less off its chord than the centre of its arc can be placed to is read as its chord, as an
    /// edge in the plane is: the edge from (0, 0, 0) to (100, 0, 0) with a bulge of 1E-12 put its middle at (50.008, 0, 0).
    /// </summary>
    public class EdgeFaultF1FlatBulge3Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        [Fact]
        public void AnEdgeInSpaceBowingLessThanItsCentreIsKnownTo_IsItsChord()
        {
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1E-12, new GeoVector3(0, 0, 1));

            Assert.False(edge.IsArc);
            Assert.Equal(new GeoPoint3(50, 0, 0), edge.MidPoint);
            Assert.Equal(new GeoPoint3(50, 0, 0), edge.GetPointAtParameter(0.5));
            Assert.Equal(100.0, edge.Length);
            Assert.Equal(1.0, edge.ToArc().Radius / 2.5E13, 9);
            Assert.Equal(0.0, edge.DistanceTo(new GeoPoint3(50, 0, 0), Thousandth));
        }

        [Fact]
        public void ASquareInSpaceWhoseBottomBowsATrillionth_CountsNoEdgeCurved()
        {
            var corners = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0) };
            var up = new GeoVector3(0, 0, 1);
            var square = new GeoPolygonArc3(corners, new[] { 1E-12, 0.0, 0.0, 0.0 }, new[] { up, up, up, up });
            var chain = new GeoPolylineArc3(corners, new[] { 1E-12, 0.0, 0.0, 0.0 }, new[] { up, up, up, up });

            Assert.Contains(" 0 curved", square.ToString());
            Assert.Contains(" 0 of 3 edges curved", chain.ToString());
        }

        [Fact]
        public void AnEdgeInSpaceBowingATenMillionthOfItsChord_IsStillAnArc()
        {
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1E-7, new GeoVector3(0, 0, 1));

            Assert.True(edge.IsArc);
            Assert.Equal(5E-6, Math.Abs(edge.MidPoint.Y), 6);
        }
    }
}
