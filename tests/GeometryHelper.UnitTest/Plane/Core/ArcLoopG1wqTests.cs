using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A curved loop in the plane is copied, moved, turned and transformed edge for edge; a straight chain or polygon is cut
    /// within the tolerance asked; a segment of any length carries a line to mirror across; an offset by less than 1E-5 is
    /// squared off past the miter limit; and a polygon of any area is weighed for its centroid.
    /// </summary>
    /// <remarks>
    /// The copies were put back together within the global tolerance of a thousandth, which dropped the arcs and sides a
    /// loop keeps within a finer one; the cuts of a straight chain or polygon were too. The mirror, and the squared-off join
    /// of an offset, read the global vector tolerance of 1E-5 as a length, so a segment or an offset shorter than that had
    /// no direction. The centroid took an area of a millionth or less for none and averaged the corners. No test sets the
    /// global tolerance.
    /// </remarks>
    public class ArcLoopG1wqTests
    {
        private static readonly Tolerance Billionth = new Tolerance(
            1E-9,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-9
        );
        private static readonly Tolerance Millionth = new Tolerance(
            1E-6,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-6
        );
        private static readonly Tolerance Tenth = new Tolerance(
            0.1,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            0.1
        );
        private static readonly Tolerance Tight = new Tolerance(
            1E-12,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-12
        );

        private static readonly double Root2 = Math.Sqrt(2.0);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        // The rectangle 100 by 50 with every corner rounded by 0.0005 at a millionth: eight edges, four of them arcs.
        private static GeoPolygonArc2 Rounded() =>
            Corner2.Fillet(
                new GeoPolygonArc2(new[] { P(0, 0), P(100, 0), P(100, 50), P(0, 50) }, null),
                0.0005,
                Millionth
            );

        private static readonly double RoundedArea = 5000.0 - 0.0005 * 0.0005 * (4.0 - Math.PI);

        private static int ArcsOf(GeoPolygonArc2 loop) => loop.GetEdges().Count(edge => edge.IsArc);

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionthCopied_KeepsItsFourArcs()
        {
            GeoPolygonArc2 copy = Rounded().Clone();

            Assert.Equal(8, copy.VertexCount);
            Assert.Equal(4, ArcsOf(copy));
            Assert.Equal(RoundedArea, copy.Area, 9);
        }

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionthMoved_KeepsItsFourArcsWhereTheyWere()
        {
            GeoPolygonArc2 rounded = Rounded();

            GeoPolygonArc2 moved = rounded.Translate(new GeoVector2(5, 7));

            Assert.Equal(8, moved.VertexCount);
            Assert.Equal(4, ArcsOf(moved));

            for (int i = 0; i < rounded.VertexCount; i++)
            {
                Assert.True(moved[i].IsEqualTo(rounded[i].Add(new GeoVector2(5, 7)), Tight));
                Assert.Equal(rounded.Bulges[i], moved.Bulges[i]);
            }
        }

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionthTurnedAboutAPoint_KeepsItsFourArcs()
        {
            GeoPolygonArc2 turned = Rounded().RotateBy(0.5, P(1, 2));

            Assert.Equal(8, turned.VertexCount);
            Assert.Equal(4, ArcsOf(turned));
            Assert.Equal(RoundedArea, turned.Area, 9);
        }

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionthMirrored_KeepsItsFourArcsTurnedTheOtherWay()
        {
            GeoPolygonArc2 rounded = Rounded();

            GeoPolygonArc2 mirrored = rounded.TransformBy(GeoTransform2.Mirror(new GeoLine2(P(0, 0), P(1, 0))));

            Assert.Equal(8, mirrored.VertexCount);
            Assert.Equal(4, ArcsOf(mirrored));
            Assert.Equal(-rounded.SignedArea, mirrored.SignedArea, 9);
        }

        [Fact]
        public void ARectangleFilletedAtFiveTenThousandthsAtAMillionthScaledToATenth_KeepsItsFourArcs()
        {
            // The arcs come out 0.00007 across, which the global tolerance of a thousandth dropped with them.
            GeoPolygonArc2 scaled = Rounded().TransformBy(GeoTransform2.Scaling(0.1));

            Assert.Equal(8, scaled.VertexCount);
            Assert.Equal(4, ArcsOf(scaled));
            Assert.Equal(RoundedArea * 0.01, scaled.Area, 11);
        }

        [Fact]
        public void AChainCutByThreeTenThousandthsAtAMillionth_TurnsAtBothEndsOfTheCut()
        {
            // The two ends of the cut stand 0.00042 apart, one corner within the global tolerance of a thousandth.
            var chain = new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10));

            GeoPolyline2 cut = Corner2.Chamfer(chain, 0.0003, 0.0003, Millionth);

            Assert.Equal(4, cut.VertexCount);
            Assert.True(cut[1].IsEqualTo(P(9.9997, 0), Tight));
            Assert.True(cut[2].IsEqualTo(P(10, 0.0003), Tight));
        }

        [Fact]
        public void OneCornerOfAChainCutByThreeTenThousandthsAtAMillionth_TurnsAtBothEndsOfTheCut()
        {
            var chain = new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10));

            Assert.True(Corner2.TryChamferAt(chain, 1, 0.0003, 0.0003, out GeoPolyline2 cut, Millionth));

            Assert.Equal(4, cut.VertexCount);
            Assert.True(cut[2].IsEqualTo(P(10, 0.0003), Tight));
        }

        [Fact]
        public void AChainCutByThreeHundredthsAtATenth_TurnsWhereTheCutStarts()
        {
            // A cut 0.042 long is no edge within a tenth, so the chain turns once; built within a thousandth, it turned twice.
            var chain = new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10));

            GeoPolyline2 cut = Corner2.Chamfer(chain, 0.03, 0.03, Tenth);

            Assert.Equal(3, cut.VertexCount);
            Assert.True(cut[1].IsEqualTo(P(9.97, 0), Tight));
        }

        [Fact]
        public void ATriangleCutByThreeTenThousandthsAtAMillionth_HasSixCorners()
        {
            // The cuts are 0.00023, 0.00042 and 0.00023 long, each one corner within the global tolerance of a thousandth.
            var triangle = new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10));

            GeoPolygon2 cut = Corner2.Chamfer(triangle, 0.0003, 0.0003, Millionth);

            double off = 0.5 * 0.0003 * 0.0003 * Math.Sin(Math.PI / 4.0);

            Assert.Equal(6, cut.VertexCount);
            Assert.Equal(50.0 - off - 0.0003 * 0.0003 * 0.5 - off, cut.Area, 12);
        }

        [Fact]
        public void OneCornerOfATriangleCutByThreeTenThousandthsAtAMillionth_HasFourCorners()
        {
            var triangle = new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10));

            Assert.True(Corner2.TryChamferAt(triangle, 1, 0.0003, 0.0003, out GeoPolygon2 cut, Millionth));

            Assert.Equal(4, cut.VertexCount);
            Assert.Contains(cut.Vertices, corner => corner.IsEqualTo(P(10, 0.0003), Tight));
        }

        [Fact]
        public void ASegmentSevenMillionthsLong_CarriesALineToMirrorAcross()
        {
            GeoTransform2 mirror = GeoTransform2.Mirror(new GeoLine2(P(0, 0), P(0.000007, 0.000007)));

            Assert.True(mirror.Transform(P(1, 0)).IsEqualTo(P(0, 1), Tight));
        }

        [Fact]
        public void ASquareTenMillionthsAcrossGrownByTwoMillionthsWithChamfers_IsCutAtTwoMillionthsFromEveryCorner()
        {
            // The square of side 10 grown by 2 with chamfers, a millionth the size: within the vector tolerance the ways out
            // to the moved ends, 0.000002 long, had no direction, and the corners were rounded.
            GeoPolygonArc2 square = GeoPolygonArc2.FromValidated(
                new[] { P(0, 0), P(0.00001, 0), P(0.00001, 0.00001), P(0, 0.00001) },
                new double[4]
            );

            GeoPolygonArc2 grown = Assert.Single(
                Offset2.Offset(square, 0.000002, new OffsetOptions(OffsetJoin.Chamfer), Billionth)
            );

            Assert.Equal(8, grown.VertexCount);
            Assert.Equal(0, ArcsOf(grown));
            Assert.Equal((196.0 - 2.0 * Math.Pow(4.0 - 2.0 * Root2, 2)) * 1E-12, grown.Area, 1E-20);
            Assert.Contains(grown.Vertices, corner =>
                corner.DistanceTo(P(0.000008 + 0.000002 * Root2, -0.000002)) <= 1E-15
            );
        }

        [Fact]
        public void ATriangleWithATenDegreeTipAMillionthTheSizeGrownByAMillionth_IsSquaredOffAtTenMillionthsFromTheTip()
        {
            // The triangle with a 10 degree tip grown by 1, a millionth the size: its miter would reach 0.0000115 from the
            // tip, past the limit of 10, and it is squared off 0.00001 from it.
            GeoPolygonArc2 triangle = GeoPolygonArc2.FromValidated(
                new[] { P(0, 0), P(0.0001, 0), P(0.0000984807753012208, 0.000017364817766693) },
                new double[3]
            );

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(triangle, 0.000001, OffsetOptions.Default, Billionth));

            Assert.Equal(4, grown.VertexCount);
            Assert.Contains(grown.Vertices, corner =>
                corner.DistanceTo(P(-0.00000995070971190755, -0.000001)) <= 1E-12
            );
            Assert.Contains(grown.Vertices, corner =>
                corner.DistanceTo(P(-0.00000997318424992736, -0.000000743114854953158)) <= 1E-12
            );
        }

        [Fact]
        public void AnLOfFourTenMillionthsOfASquareUnit_BalancesWhereItsAreaDoes()
        {
            // The L 0.001 by 0.001 with legs 0.00025 wide: its corners average to (0.000417, 0.000417), its area balances
            // at (0.000339, 0.000339).
            var l = new GeoPolygon2(
                new[] { P(0, 0), P(0.001, 0), P(0.001, 0.00025), P(0.00025, 0.00025), P(0.00025, 0.001), P(0, 0.001) },
                6
            );

            double along =
                (0.00025 * 0.001 * 0.0005 + 0.00025 * 0.00075 * 0.000125) / (0.00025 * 0.001 + 0.00025 * 0.00075);

            Assert.Equal(along, l.Centroid.X, 15);
            Assert.Equal(along, l.Centroid.Y, 15);
        }

        [Fact]
        public void CornersOnOneLineALastDigitOffIt_AreAveraged()
        {
            // The corners stand on the line y = 3x to the rounding of their coordinates, so the area is the rounding's and the
            // centroid is where the corners average, not where that rounding would weigh them.
            var line = new GeoPolygon2(P(1000.1, 3000.3), P(1000.3, 3000.9), P(1000.2, 3000.6), P(1000.4, 3001.2));

            GeoPoint2 centroid = line.Centroid;

            Assert.Equal(1000.25, centroid.X, 9);
            Assert.Equal(3000.75, centroid.Y, 9);
        }
    }
}
