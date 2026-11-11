using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A curved loop in space filleted, chamfered or offset keeps what the same work on the loop in its plane keeps: every
    /// arc however small, every cut and side the tolerance keeps, and the corner named by its vertex.
    /// </summary>
    /// <remarks>
    /// The loop was laid out in its plane within the global tolerance of a thousandth, which dropped a side the loop had kept
    /// within a finer one, and put back together within it, which dropped the arcs and the cuts shorter than that. A straight
    /// loop was read as a curved one the same way before it was filleted. The loops with a side that short are built as work
    /// at a finer tolerance leaves them. No test sets the global tolerance.
    /// </remarks>
    public class ArcLoopShapingG1woTests
    {
        private static readonly Tolerance Millionth = new Tolerance(
            1E-6,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-6
        );
        private static readonly Tolerance Billionth = new Tolerance(
            1E-9,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-9
        );
        private static readonly Tolerance Thousandth = new Tolerance(
            1E-3,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-3
        );
        private static readonly Tolerance Tight = new Tolerance(
            1E-12,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-12
        );

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static GeoPolygonArc3 Loop(params GeoPoint3[] corners) =>
            new GeoPolygonArc3(corners, null, null, Tight);

        // The rectangle 100 by 50 standing in the plane x = 20.
        private static GeoPolygonArc3 Rectangle() =>
            Loop(
                new GeoPoint3(20, 0, 0),
                new GeoPoint3(20, 100, 0),
                new GeoPoint3(20, 100, 50),
                new GeoPoint3(20, 0, 50)
            );

        // The square 10 by 10 at the origin with the leg (10, 0) to (10.0004, 0.0004), 0.00057 long, after its corner (10, 0).
        private static GeoPoint3[] Stepped() =>
            new[] { P(0, 0), P(10, 0), P(10.0004, 0.0004), P(10.0004, 10), P(0, 10) };

        private static double[] RadiiOf(GeoPolygonArc3 loop) =>
            loop.GetEdges().Where(edge => edge.IsArc).Select(edge => edge.ToArc().Radius).ToArray();

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionth_KeepsItsFourArcs()
        {
            GeoPolygonArc3 rounded = Rectangle().Fillet(0.0005, Millionth);

            Assert.Equal(8, rounded.VertexCount);
            Assert.All(RadiiOf(rounded), radius => Assert.Equal(0.0005, radius, 12));
            Assert.Equal(4, RadiiOf(rounded).Length);
        }

        [Fact]
        public void ARectangleFilletedByARadiusAtEachCornerAtAMillionth_KeepsEachArc()
        {
            GeoPolygonArc3 rounded = Rectangle().Fillet(new[] { 0.0005, 0.0003, 0.0005, 0.0003 }, Millionth);

            double[] radii = RadiiOf(rounded);

            Assert.Equal(8, rounded.VertexCount);
            Assert.Equal(4, radii.Length);
            Assert.Equal(0.0016, radii.Sum(), 12);
        }

        [Fact]
        public void ARectangleChamferedByThreeTenThousandthsAtAMillionth_KeepsItsEightCorners()
        {
            GeoPolygonArc3 cut = Rectangle().Chamfer(0.0003, Millionth);

            Assert.Equal(8, cut.VertexCount);
            Assert.Equal(300.0 - 8 * 0.0003 + 4 * 0.0003 * Math.Sqrt(2.0), cut.Length, 9);
        }

        [Fact]
        public void OneCornerFilletedAfterALegShorterThanTheGlobalTolerance_IsTheCornerNamed()
        {
            // Vertex 3 is (10.0004, 10); laid out within a thousandth, the short leg went and vertex 3 was (0, 10).
            GeoPolygonArc3 loop = Loop(Stepped());

            Assert.True(loop.TryFilletAt(3, 1.0, out GeoPolygonArc3 rounded, Millionth));

            Assert.Equal(6, rounded.VertexCount);
            Assert.True(rounded[3].IsEqualTo(P(10.0004, 9), Tight));
            Assert.True(rounded[4].IsEqualTo(P(9.0004, 10), Tight));
            Assert.True(rounded[2].IsEqualTo(P(10.0004, 0.0004), Tight));
        }

        [Fact]
        public void OneCornerChamferedAfterALegShorterThanTheGlobalTolerance_IsTheCornerNamed()
        {
            GeoPolygonArc3 loop = Loop(Stepped());

            Assert.True(loop.TryChamferAt(3, 1.0, 1.0, out GeoPolygonArc3 cut, Millionth));

            Assert.Equal(6, cut.VertexCount);
            Assert.True(cut[3].IsEqualTo(P(10.0004, 9), Tight));
            Assert.True(cut[4].IsEqualTo(P(9.0004, 10), Tight));
        }

        [Fact]
        public void ALoopWithACornerCutOffShorterThanTheGlobalToleranceGrownByATenth_KeepsTheCutLengthened()
        {
            // The cut 0.00057 long moves out along with the sides and is 0.0834 long; laid out within a thousandth, the cut
            // went and the corner came out square.
            GeoPolygonArc3 loop = Loop(P(0.0004, 0), P(10, 0), P(10, 10), P(0, 10), P(0, 0.0004));

            GeoPolygonArc3 grown = Assert.Single(loop.Offset(0.1, Millionth));

            double reach = 0.0004 - 0.1 * Math.Sqrt(2.0) + 0.1;

            Assert.Equal(5, grown.VertexCount);
            Assert.Contains(grown.Vertices, corner => corner.IsEqualTo(P(reach, -0.1), Millionth));
            Assert.Contains(grown.Vertices, corner => corner.IsEqualTo(P(-0.1, reach), Millionth));
        }

        [Fact]
        public void AStraightLoopFilletedAfterASideShorterThanTheGlobalTolerance_IsTheCornerNamed()
        {
            var polygon = new GeoPolygon3(Stepped(), Tight);

            Assert.True(Corner3.TryFilletAt(polygon, 3, 1.0, out GeoPolygonArc3 rounded, Millionth));

            Assert.Equal(6, rounded.VertexCount);
            Assert.True(rounded[3].IsEqualTo(P(10.0004, 9), Tight));
        }

        [Fact]
        public void AStraightLoopWithASideShorterThanTheGlobalToleranceFilletedAtAMillionth_KeepsThatSide()
        {
            var polygon = new GeoPolygon3(Stepped(), Tight);

            // Each end of the leg 0.00057 long turns by 45 degrees and takes 0.00004 of it at a radius of 0.0001.
            GeoPolygonArc3 rounded = Corner3.Fillet(polygon, 0.0001, Millionth);

            Assert.Equal(10, rounded.VertexCount);
            Assert.Equal(5, RadiiOf(rounded).Length);
            Assert.All(RadiiOf(rounded), radius => Assert.Equal(0.0001, radius, 12));
        }

        [Fact]
        public void AChainSmallerThanTheGlobalToleranceGrownWithMitersAtABillionth_LeavesNoStrayPieceInAMiter()
        {
            // A star of nine corners some 0.005 across; grown by 0.00137 it left a stray piece 0.00004 long inside a miter
            // the global tolerance could not see. The same chain a million times larger, grown within a thousandth, is one
            // piece of the same length.
            GeoPoint2[] corners =
            {
                new GeoPoint2(0.002714474964670055, 0.0021815179030871905),
                new GeoPoint2(0.0003979379355382902, 0.0010860729054751298),
                new GeoPoint2(-0.0009919752635786999, 0.003205889222548999),
                new GeoPoint2(-0.001374730437030097, 0.0006779850278329769),
                new GeoPoint2(-0.004017507292794224, -0.00036236339519759),
                new GeoPoint2(-0.0017963545207715604, -0.0021775884314420295),
                new GeoPoint2(0.0002629622603692075, -0.0037426725103135495),
                new GeoPoint2(0.0009376765621625675, -0.0006347071621864863),
                new GeoPoint2(0.0043361567432226465, 0.00045632681245736057),
            };
            double[] bulges =
            {
                -0.14142376707933088,
                0.2816892583769231,
                0,
                0,
                -0.1578680273414906,
                0,
                0.08600113773066609,
                0,
                0,
            };
            const double Distance = 0.0013689049144938797;

            var chain = new GeoPolylineArc2(corners, bulges);
            var larger = new GeoPolylineArc2(
                corners.Select(corner => new GeoPoint2(corner.X * 1E6, corner.Y * 1E6)),
                bulges
            );

            GeoPolylineArc2 grown = Assert.Single(Offset2.Offset(chain, Distance, OffsetJoin.Miter, Billionth));
            GeoPolylineArc2 reference = Assert.Single(
                Offset2.Offset(larger, Distance * 1E6, OffsetJoin.Miter, Thousandth)
            );

            Assert.Equal(reference.Length * 1E-6, grown.Length, 12);
        }
    }
}
