using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A curved loop in the plane is reversed and weighed as it is, and a side shorter than a hundredth of a millimetre is a
    /// side to round a corner beside and to move when the loop is offset.
    /// </summary>
    /// <remarks>
    /// Reversing put the loop back together within the global tolerance of a thousandth, and the centroid weighed the
    /// straight loop through its corners within it, so the arcs and sides a loop keeps within a finer tolerance went. A side
    /// shorter than the global vector tolerance, 1E-5, had no direction: the fillet beside it found no centre and the offset
    /// left it where it was. The loops with a side that short are built as work at a millionth leaves them. No test sets the
    /// global tolerance.
    /// </remarks>
    public class ArcLoopG1wpTests
    {
        private static readonly Tolerance Millionth = new Tolerance(
            1E-6,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-6
        );
        private static readonly Tolerance Tight = new Tolerance(
            1E-12,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-12
        );

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygonArc2 Rectangle() =>
            new GeoPolygonArc2(new[] { P(0, 0), P(100, 0), P(100, 50), P(0, 50) }, null);

        private static int ArcsOf(GeoPolygonArc2 loop) => loop.GetEdges().Count(edge => edge.IsArc);

        // The square 10 by 10 at the origin with its corner (0, 0) cut off by a side 0.000007 long, its legs 0.000005.
        private static GeoPolygonArc2 FinelyCut() =>
            GeoPolygonArc2.FromValidated(
                new[] { P(0.000005, 0), P(10, 0), P(10, 10), P(0, 10), P(0, 0.000005) },
                new double[5]
            );

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionthReversed_KeepsItsFourArcs()
        {
            GeoPolygonArc2 rounded = Corner2.Fillet(Rectangle(), 0.0005, Millionth);

            GeoPolygonArc2 reversed = rounded.Reverse();

            Assert.Equal(8, reversed.VertexCount);
            Assert.Equal(4, ArcsOf(reversed));
            Assert.Equal(-(5000.0 - 0.0005 * 0.0005 * (4.0 - Math.PI)), reversed.SignedArea, 9);
        }

        [Fact]
        public void ARectangleWithOneCornerRoundedAtFiveTenThousandths_HasItsCentroidInTheMiddle()
        {
            // The arc moves the centroid by less than a billionth; weighed through corners merged within a thousandth, the
            // arc's end went and the centroid moved 0.00008.
            Assert.True(Corner2.TryFilletAt(Rectangle(), 2, 0.0005, out GeoPolygonArc2 rounded, Millionth));

            GeoPoint2 centroid = rounded.Centroid;

            Assert.Equal(50.0, centroid.X, 8);
            Assert.Equal(25.0, centroid.Y, 8);
        }

        [Fact]
        public void ACornerBesideASideSevenMillionthsLong_IsRoundedAtAMillionth()
        {
            // The corner turns by 45 degrees, so a radius of 0.00001 takes 0.0000041 of each side, and the side 0.000007
            // long has room for it.
            Assert.True(Corner2.TryFilletAt(FinelyCut(), 0, 0.00001, out GeoPolygonArc2 rounded, Millionth));

            double take = 0.00001 * (Math.Sqrt(2.0) - 1.0);

            Assert.Equal(6, rounded.VertexCount);
            Assert.Equal(1, ArcsOf(rounded));
            Assert.Contains(rounded.Vertices, corner => corner.IsEqualTo(P(0.000005 + take, 0), Tight));
            Assert.Contains(rounded.Vertices, corner =>
                corner.IsEqualTo(P(0.000005 - take / Math.Sqrt(2.0), take / Math.Sqrt(2.0)), Tight)
            );
        }

        [Fact]
        public void ACornerOfAStraightLoopBesideASideSevenMillionthsLong_IsRoundedAtAMillionth()
        {
            var polygon = new GeoPolygon3(
                FinelyCut().Vertices.Select(corner => new GeoPoint3(corner.X, corner.Y, 0)),
                Tight
            );

            Assert.True(Corner3.TryFilletAt(polygon, 0, 0.00001, out GeoPolygonArc3 rounded, Millionth));

            Assert.Equal(6, rounded.VertexCount);
        }

        [Fact]
        public void ALoopWithASideSevenMillionthsLongGrownByAHalf_KeepsThatSideLengthened()
        {
            // The side moves out with the others and is 0.41422 long; with no direction, it stayed where it was and the
            // corner came out square.
            GeoPolygonArc2 grown = Assert.Single(FinelyCut().Offset(0.5, Millionth));

            double reach = 0.000005 - 0.5 * (Math.Sqrt(2.0) - 1.0);

            Assert.Equal(5, grown.VertexCount);
            Assert.Contains(grown.Vertices, corner => corner.IsEqualTo(P(reach, -0.5), Millionth));
            Assert.Contains(grown.Vertices, corner => corner.IsEqualTo(P(-0.5, reach), Millionth));
        }
    }
}
