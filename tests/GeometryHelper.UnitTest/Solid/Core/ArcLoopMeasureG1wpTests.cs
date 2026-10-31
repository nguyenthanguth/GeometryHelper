using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A curved loop in space is measured and reversed as it is, every arc and side however small, and a straight chain in
    /// space keeps every cut the tolerance asked keeps.
    /// </summary>
    /// <remarks>
    /// The area, the centroid and the check for crossings laid the loop out in its plane within the global tolerance of a
    /// thousandth, which dropped the arcs and sides shorter than that a loop keeps within a finer one, and reversing put the
    /// loop back together within it. A straight chain's chamfer was put back together within it too. The loops with a side
    /// that short are built as work at a finer tolerance leaves them. No test sets the global tolerance.
    /// </remarks>
    public class ArcLoopMeasureG1wpTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);
        private static readonly Tolerance Tight = new Tolerance(1E-12, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-12);

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static GeoPolygonArc3 Loop(params GeoPoint3[] corners) => new GeoPolygonArc3(corners, null, null, Tight);

        // The rectangle 100 by 50 standing in the plane x = 20.
        private static GeoPolygonArc3 Rectangle()
            => Loop(new GeoPoint3(20, 0, 0), new GeoPoint3(20, 100, 0), new GeoPoint3(20, 100, 50), new GeoPoint3(20, 0, 50));

        private static int ArcsOf(GeoPolygonArc3 loop) => loop.GetEdges().Count(edge => edge.IsArc);

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionth_HasTheAreaOfItsArcs()
        {
            // Laid out within a thousandth, the arcs 0.0007 across went and the area read 4 999.9375.
            GeoPolygonArc3 rounded = Rectangle().Fillet(0.0005, Millionth);

            double exact = 5000.0 - 0.0005 * 0.0005 * (4.0 - Math.PI);

            Assert.Equal(exact, rounded.Area, 9);
            Assert.Equal(exact, Math.Abs(rounded.SignedArea), 9);
        }

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionthReversed_KeepsItsFourArcs()
        {
            GeoPolygonArc3 rounded = Rectangle().Fillet(0.0005, Millionth);

            GeoPolygonArc3 reversed = rounded.Reverse();

            Assert.Equal(8, reversed.VertexCount);
            Assert.Equal(4, ArcsOf(reversed));
            Assert.Equal(-rounded.Normal.X, reversed.Normal.X, 12);
            Assert.Equal(5000.0 - 0.0005 * 0.0005 * (4.0 - Math.PI), reversed.Area, 9);
        }

        [Fact]
        public void ARectangleWithOneCornerRoundedAtFiveTenThousandths_HasItsCentroidInTheMiddle()
        {
            // The arc takes some 5E-8 square units off the corner (20, 100, 50), which moves the centroid by less than a
            // billionth; laid out within a thousandth, the arc's end went, the side after it ran in a slant from its start,
            // and the centroid moved 0.00008.
            Assert.True(Rectangle().TryFilletAt(2, 0.0005, out GeoPolygonArc3 rounded, Millionth));

            GeoPoint3 centroid = rounded.Centroid;

            Assert.Equal(20.0, centroid.X, 9);
            Assert.Equal(50.0, centroid.Y, 8);
            Assert.Equal(25.0, centroid.Z, 8);
        }

        [Fact]
        public void ALoopCrossingItselfInALobeSmallerThanTheGlobalTolerance_IsNotSimpleAtAMillionth()
        {
            // The side from (0, 10) runs down to (0.0004, 0) and the one back to (10, 0) starts at (0, 0.0004), so the two
            // cross near (0.0004, 0.0004). Laid out within a thousandth, the corner (0, 0.0004) went with the lobe.
            GeoPolygonArc3 loop = Loop(P(10, 0), P(10, 10), P(0, 10), P(0.0004, 0), P(0, 0.0004));

            Assert.False(loop.IsSimple(Millionth));
        }

        [Fact]
        public void AChainCutByThreeTenThousandthsAtAMillionth_KeepsTheCut()
        {
            // The cut runs from (9.9997, 0, 0) to (10, 0.0003, 0), 0.00042 long; put back together within a thousandth, its
            // ends were one corner and the chain turned at (9.9997, 0, 0).
            var chain = new GeoPolyline3(P(0, 0), P(10, 0), P(10, 10));

            GeoPolyline3 cut = Corner3.Chamfer(chain, 0.0003, Millionth);

            Assert.Equal(4, cut.VertexCount);
            Assert.True(cut.Vertices[1].IsEqualTo(P(9.9997, 0), Tight));
            Assert.True(cut.Vertices[2].IsEqualTo(P(10, 0.0003), Tight));
        }

        [Fact]
        public void OneCornerOfAChainCutByThreeTenThousandthsAtAMillionth_KeepsTheCut()
        {
            var chain = new GeoPolyline3(P(0, 0), P(10, 0), P(10, 10), P(20, 10));

            Assert.True(Corner3.TryChamferAt(chain, 2, 0.0003, 0.0002, out GeoPolyline3 cut, Millionth));

            Assert.Equal(5, cut.VertexCount);
            Assert.True(cut.Vertices[2].IsEqualTo(P(10, 9.9997), Tight));
            Assert.True(cut.Vertices[3].IsEqualTo(P(10.0002, 10), Tight));
        }
    }
}
