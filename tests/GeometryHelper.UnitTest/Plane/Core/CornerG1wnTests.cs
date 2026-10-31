using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A loop in the plane filleted, chamfered or offset keeps what the work at the tolerance asked keeps, as a chain does,
    /// and a straight chain or loop is filleted corner for corner.
    /// </summary>
    /// <remarks>
    /// The loop was put back together within the global tolerance of a thousandth: an arc or a cut shorter than that went,
    /// and so did a side the loop had kept within a finer tolerance. A straight chain or loop was read as a curved one
    /// through the public constructor, which drops a leg shorter than the global tolerance before the fillet is asked. The
    /// loops and chains with a side that short are built as work at a millionth leaves them. No test sets the global
    /// tolerance.
    /// </remarks>
    public class CornerG1wnTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);
        private static readonly Tolerance Thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);
        private static readonly Tolerance Tight = new Tolerance(1E-12, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-12);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygonArc2 Rectangle() => new GeoPolygonArc2(new[] { P(0, 0), P(100, 0), P(100, 50), P(0, 50) }, null);

        private static int ArcsOf(GeoPolygonArc2 loop) => loop.Bulges.Count(bulge => bulge != 0.0);

        // The square 10 by 10 at the origin with its corner (0, 0) cut off by a side 0.00057 long.
        private static GeoPoint2[] Chamfered() => new[] { P(0.0004, 0), P(10, 0), P(10, 10), P(0, 10), P(0, 0.0004) };

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionth_KeepsItsFourArcs()
        {
            GeoPolygonArc2 rounded = Corner2.Fillet(Rectangle(), 0.0005, Millionth);

            Assert.Equal(8, rounded.VertexCount);
            Assert.Equal(4, ArcsOf(rounded));
            Assert.Equal(5000.0 - 0.0005 * 0.0005 * (4.0 - Math.PI), rounded.Area, 9);
        }

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAThousandth_KeepsItsArcsAsAChainDoes()
        {
            // The arcs are 0.0007 across, shorter than the tolerance; the fillet keeps an arc however small, and the chain
            // round the same corners keeps its three.
            GeoPolygonArc2 rounded = Corner2.Fillet(Rectangle(), 0.0005, Thousandth);
            GeoPolylineArc2 chain = Corner2.Fillet(new GeoPolylineArc2(new[] { P(0, 0), P(100, 0), P(100, 50), P(0, 50), P(0, 0) }, null), 0.0005, Thousandth);

            Assert.Equal(4, ArcsOf(rounded));
            Assert.Equal(3, chain.Bulges.Count(bulge => bulge != 0.0));
            Assert.Equal(5000.0 - 0.0005 * 0.0005 * (4.0 - Math.PI), rounded.Area, 9);
        }

        [Fact]
        public void OneCornerFilletedAtFiveTenThousandthsAtAMillionth_IsAnArc()
        {
            Assert.True(Corner2.TryFilletAt(Rectangle(), 2, 0.0005, out GeoPolygonArc2 rounded, Millionth));

            Assert.Equal(5, rounded.VertexCount);
            Assert.Equal(1, ArcsOf(rounded));
            Assert.Equal(5000.0 - 0.0005 * 0.0005 * (1.0 - Math.PI / 4.0), rounded.Area, 9);
        }

        [Fact]
        public void OneCornerChamferedByFourTenThousandthsAtAMillionth_IsCut()
        {
            Assert.True(Corner2.TryChamferAt(Rectangle(), 2, 0.0004, 0.0004, out GeoPolygonArc2 cut, Millionth));

            Assert.Equal(5, cut.VertexCount);
            Assert.Contains(cut.Vertices, corner => corner.IsEqualTo(P(100, 49.9996), Millionth));
            Assert.Contains(cut.Vertices, corner => corner.IsEqualTo(P(99.9996, 50), Millionth));
            Assert.Equal(5000.0 - 0.5 * 0.0004 * 0.0004, cut.Area, 9);
        }

        [Fact]
        public void EveryCornerChamferedByThreeTenThousandthsAtAMillionth_IsCut()
        {
            GeoPolygonArc2 cut = Corner2.Chamfer(Rectangle(), 0.0003, 0.0003, Millionth);

            Assert.Equal(8, cut.VertexCount);
            Assert.Equal(5000.0 - 4 * 0.5 * 0.0003 * 0.0003, cut.Area, 9);
        }

        [Fact]
        public void ALoopWithASideShorterThanTheGlobalTolerance_KeepsItWhenAnotherCornerIsFilleted()
        {
            GeoPolygonArc2 plate = GeoPolygonArc2.FromValidated(Chamfered(), new double[5]);

            Assert.True(Corner2.TryFilletAt(plate, 2, 1.0, out GeoPolygonArc2 rounded, Millionth));

            Assert.Equal(6, rounded.VertexCount);
            Assert.Contains(rounded.Vertices, corner => corner.IsEqualTo(P(0.0004, 0), Tight));
            Assert.Contains(rounded.Vertices, corner => corner.IsEqualTo(P(0, 0.0004), Tight));
            Assert.Equal(100.0 - 0.5 * 0.0004 * 0.0004 - (1.0 - Math.PI / 4.0), rounded.Area, 9);
        }

        [Fact]
        public void ARectangleOffsetOutByFourTenThousandthsWithRoundJoinsAtAMillionth_KeepsItsFourArcs()
        {
            GeoPolygonArc2[] grown = Offset2.Offset(Rectangle(), 0.0004, OffsetJoin.Round, Millionth);

            Assert.Single(grown);
            Assert.Equal(8, grown[0].VertexCount);
            Assert.Equal(4, ArcsOf(grown[0]));
            Assert.Equal(5000.0 + 300.0 * 0.0004 + Math.PI * 0.0004 * 0.0004, grown[0].Area, 9);
        }

        [Fact]
        public void AStraightLoopWithASideShorterThanTheGlobalTolerance_IsFilletedCornerForCorner()
        {
            GeoPoint2[] corners = Chamfered();
            var plate = new GeoPolygon2(corners, corners.Length);

            // The two corners of the short side have no room for a radius of 1 and stay; the other three are rounded.
            GeoPolygonArc2 rounded = Corner2.Fillet(plate, 1.0, Millionth);

            Assert.Equal(8, rounded.VertexCount);
            Assert.Equal(3, ArcsOf(rounded));
            Assert.Contains(rounded.Vertices, corner => corner.IsEqualTo(P(0.0004, 0), Tight));
            Assert.Contains(rounded.Vertices, corner => corner.IsEqualTo(P(0, 0.0004), Tight));
            Assert.Equal(100.0 - 0.5 * 0.0004 * 0.0004 - 3 * (1.0 - Math.PI / 4.0), rounded.Area, 9);
        }

        [Fact]
        public void AStraightChainWithALegShorterThanTheGlobalTolerance_IsFilletedAtTheCornerAsked()
        {
            var corners = new[] { P(0, 0), P(10, 0), P(10.0004, 0.0004), P(10.0004, 10), P(0, 10) };
            var chain = new GeoPolyline2(corners, corners.Length);

            Assert.True(Corner2.TryFilletAt(chain, 3, 1.0, out GeoPolylineArc2 rounded, Millionth));

            Assert.Equal(6, rounded.VertexCount);
            Assert.True(rounded[2].IsEqualTo(P(10.0004, 0.0004), Tight));
            Assert.True(rounded[3].IsEqualTo(P(10.0004, 9), Millionth));
            Assert.True(rounded[4].IsEqualTo(P(9.0004, 10), Millionth));
            Assert.NotEqual(0.0, rounded.Bulges[3]);
        }
    }
}
