using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Fault round G7: a curved loop that crosses itself, grown with miters or chamfers, lost the miter or the chamfer at a
    /// tip of its region at a crossing, read at 14543f9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At a crossing the region read even-odd lies in two opposite quarters. The loops that bound it were joined there round
    /// the quarters of the space between, so a tip of the region at the crossing was no corner of them, and its miter was never
    /// built: where the quarter across the crossing was too small to hold the miter, as a lens or a small lobe is, the miter's
    /// point was left out.
    /// </para>
    /// <para>
    /// The expected areas are an oracle's, built apart from the code under test: the region read even-odd off a polygon of
    /// the loop within 1E-6, grown by the straight path with round joins, with the miter of every corner of the region that
    /// opens added, the tips at its crossings among them, each run on round its own circle to where it meets the other, or
    /// squared off at the limit, or for chamfers cut square to the corner's bisector at the distance. Round joins give the
    /// oracle's area before and after.
    /// </para>
    /// </remarks>
    public class OffsetFaultG7MiterTipTests
    {
        private static readonly OffsetOptions Miter = new OffsetOptions(OffsetJoin.Miter);

        /// <summary>The loop 1:152 of the item 6 fuzz: ten corners about the origin, most edges arcs, crossing itself at nine places.</summary>
        private static GeoPolygonArc2 Random1Of152() => new GeoPolygonArc2(
            new[]
            {
                P(4.136973616730874, 8.956718760988078), P(-1.0251323650708102, 1.9965966567381273), P(0.40688016936503324, 3.0274458755866824),
                P(9.407229516379175, 0.13935977133892408), P(-6.38526230882167, -0.4872193329442389), P(-3.743948244370497, -4.524095866141885),
                P(-6.351106295525612, 1.1171229887367815), P(-2.227404011519348, 8.4150139607559), P(-0.3370376445059833, -4.391043882067801),
                P(-2.5245364999978506, -4.373737775894692),
            },
            new[]
            {
                0.0, -0.2517930292299916, 0.0, -0.2968465164289095, -0.25827591566288655, -0.11854640665396421, 0.0, 0.20608063801474885,
                0.40553933480081117, 0.500978422724166,
            });

        /// <summary>
        /// The loop 3:106 of the item 6 fuzz: eight corners, crossing itself at thirteen places, two of them where the arc
        /// leaving (9.104, 2.814) dips 0.04 through the edge from (9.772, 1.219) to (4.622, -2.141), 2.3 degrees to it.
        /// </summary>
        private static GeoPolygonArc2 Random3Of106() => new GeoPolygonArc2(
            new[]
            {
                P(6.9202171531134375, 4.597090331184255), P(9.772298876090115, 1.2190479185520893), P(4.621967000245101, -2.141028983584153),
                P(-5.676463141886733, 8.989394204220453), P(9.104171078234991, 2.81400720254239), P(-9.432066669423165, 3.487811886466952),
                P(-2.7981192864468873, 7.285228826704074), P(-7.69980831895946, -8.938388097536931),
            },
            new[] { -0.17932155201179978, 0.0, 0.6486618450603734, 0.12622283102302945, -0.6918214069641294, 0.0, -0.5231888607252337, -0.1773908118146429 });

        /// <summary>
        /// A bow tie crossing itself at the origin: a lobe 10 long whose back is an arc of bulge -0.25 from (-10, -2) to
        /// (-10, 2), and a lobe 1 long whose back is the segment from (1, -0.2) to (1, 0.2).
        /// </summary>
        private static GeoPolygonArc2 BowTieWithASmallLobe() => new GeoPolygonArc2(
            new[] { P(-10, 2), P(1, -0.2), P(1, 0.2), P(-10, -2) },
            new[] { 0.0, 0.0, 0.0, -0.25 });

        /// <summary>
        /// The loop 1:146 of the item 6 fuzz: four corners in a box 13.28 by 10, three of its edges arcs, its first and last
        /// edges crossing at (5.723, 5.984), 1.3 degrees apart.
        /// </summary>
        private static GeoPolygonArc2 Random1Of146() => new GeoPolygonArc2(
            new[] { P(7.011740746447696, 7.296606226496682), P(6.363612989133042, -2.6293578709612406), P(-4.669512098035548, 5.333382978724959), P(8.606849628783227, -2.7051721199905367) },
            new[] { 0.4527450044419361, 0.0, 0.41989703183988913, -0.6106898295277217 });

        [Fact]
        public void Loop1Of146GrownByThreeWithChamfers_KeepsTheChamfersAtItsTips()
        {
            // The tips of the region at the crossing are 1.3 degrees wide, and each is cut square to its bisector 3 out. The
            // oracle, with the chamfer of every corner that opens, reads 212.0581; 0.83 was left out beside the crossing, near
            // (4.3, 8.9) and (2.4, 6.3), and the offset was 211.2289.
            GeoPolygonArc2 loop = Random1Of146();

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 3.0, new OffsetOptions(OffsetJoin.Chamfer), Thousandth));

            Assert.Equal(212.0581, grown.Area, 1E-3);
        }

        [Fact]
        public void Loop1Of152GrownByAThirdOfAMillimetreWithMiters_KeepsTheMitersAtItsTips()
        {
            // The oracle reads 92.9087 (round joins: 88.8817 against 88.8817). A sliver of 1.89 at the tips of the region at
            // its crossings near (0, -4.4) was left out, and the offset was 90.9943.
            GeoPolygonArc2 loop = Random1Of152();

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 0.3, Miter, Thousandth));

            Assert.Equal(92.9087, grown.Area, 1E-3);
        }

        [Fact]
        public void Loop3Of106GrownByThreeWithMitersAtATenth_KeepsTheMitersOfTheLensItCuts()
        {
            // The arc that dips through the edge cuts a lens 0.7 long from it, whose tips are 2.3 degrees wide: each is mitered
            // to the limit of 30, along the edge and round the arc, past the lens. The oracle reads 530.5185, the same within
            // 1E-3 and at a tenth; 131.26 was left out, and the offset was 399.2603.
            GeoPolygonArc2 loop = Random3Of106();

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 3.0, Miter, Tenth));

            Assert.Equal(530.5185, grown.Area, 1E-3);
        }

        [Fact]
        public void ABowTieGrownByOneWithMiters_KeepsTheMiterOfItsLongLobePastTheSmallOne()
        {
            // The tip of the long lobe at the origin is 22.6 degrees wide, and its miter runs the two edges on, moved by 1, to
            // (sqrt 1.04 / 0.2, 0) = (5.099, 0), within the limit of 10. The small lobe grown reaches no further than x = 2,
            // so the point (4, 0), 0.22 inside the miter, is in the offset, and the oracle reads 54.4598. The miter was cut
            // off at x = 2, 1.92 left out, and the offset was 52.5391.
            GeoPolygonArc2 loop = BowTieWithASmallLobe();

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 1.0, Miter, Thousandth));

            Assert.Equal(PointLocation.Inside, grown.Locate(P(4, 0), Thousandth));
            Assert.Equal(5.0990195, grown.Vertices.Max(v => v.X), 1E-6);
            Assert.Equal(54.4598, grown.Area, 1E-3);
        }

        [Fact]
        public void ASquareWhoseBottomCurlsIntoALoopGrownByThreeWithMiters_GrowsNoHornsAtTheCrossing()
        {
            // The bottom edge runs from the origin up to (6, 2), back to (4, 2) and down to (10, 0), crossing itself at
            // (5, 1.667). The two quarters of the region there are one piece, round the hole the loop makes, and no tips: the
            // straight path and the oracle give 253.2631 within x = -3 to 13. Mitered as two tips, they grew horns out to
            // (-4.487, 1.667) and (14.487, 1.667), 254.7369.
            var loop = new GeoPolygonArc2(new[] { P(0, 0), P(6, 2), P(4, 2), P(10, 0), P(10, 10), P(0, 10) }, new double[6]);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 3.0, Miter, Thousandth));

            Assert.Equal(253.2631, grown.Area, 1E-3);
            Assert.InRange(grown.Vertices.Min(v => v.X), -3.0 - 1E-9, 13.0);
            Assert.InRange(grown.Vertices.Max(v => v.X), -3.0, 13.0 + 1E-9);
        }

        [Fact]
        public void ALobeNoWiderThanTheToleranceAtACrossing_IsReadWithinAFinerStepAndCutAsTheOracleCutsIt()
        {
            // The loop of seed 5161 of the reviewer's curved fuzz: two edges crossing at 4.8 degrees near (2.80, -2.90) are both crossed
            // by a third 0.06 from there, 0.006 apart, which at a tolerance of 0.01 is one place, so that the lobe between is
            // a sliver. That place is read within a hundredth of the tolerance, where the lobe is a triangle and its tip is cut
            // square: grown by 3 with chamfers it is 301.4205, the oracle's. Joined round the space between at 0.01 it was
            // 301.4067, within the tolerance; cut as a tip there, the sliver kept 0.136 more, 301.5953.
            var loop = new GeoPolygonArc2(
                new[]
                {
                    P(-6.983785, -5.099169), P(4.909302, -3.467134), P(-0.411188, -2.037612), P(1.351476, 0.139131), P(-3.289377, 6.23748),
                    P(1.235113, -9.385043), P(0.148585, 9.876023), P(-8.358244, 0.839152), P(3.179253, -2.439366), P(-1.390652, -7.176731),
                },
                new[] { -0.2465, 0.0, 0.3284, -0.4442, 0.2506, -0.6906, -0.022, 0.5543, 0.0, -0.4561 });

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 3.0, new OffsetOptions(OffsetJoin.Chamfer), new Tolerance(1E-2, 1E-4)));

            Assert.Equal(301.4205, grown.Area, 0.02);
        }

        [Fact]
        public void TheDxfOffsetOfAPolylineThatCrossesItself_IsNone_AsAutoCadGivesNone()
        {
            // A closed DXF polyline that crosses itself has no offset, as AutoCAD 2021's GetOffsetCurves gives none of a bow tie
            // (the maintainer's decision, point 79 of the AutoCAD checks): the square whose bottom curls into a loop 0.06 high,
            // its sides crossing at (5, 0.05), and the bow tie 20 long and 0.02 high at its ends.
            DxfPolyline Closed(params GeoPoint2[] points)
            {
                var polyline = new DxfPolyline();

                for (int i = 0; i < points.Length; i++)
                {
                    polyline.AddVertexAt(i, points[i], 0, 0, 0);
                }

                polyline.Closed = true;
                return polyline;
            }

            Assert.Empty(Closed(P(0, 0), P(6, 0.06), P(4, 0.06), P(10, 0), P(10, 10), P(0, 10)).GetOffsetCurves(1.0));
            Assert.Empty(Closed(P(-10, 0.01), P(10, -0.01), P(10, 0.01), P(-10, -0.01)).GetOffsetCurves(1.0));
        }

        [Fact]
        public void ACrossingWithinTheToleranceOfACorner_IsReadWithinAFinerStepAndGrowsNoHorn()
        {
            // The loop of seed 1036 of the reviewer's straight fuzz: a corner lies 0.0044 off an edge and 0.0057 from where
            // that edge crosses another, which at a tolerance of 0.01 is one place. The crossing is read within a hundredth of
            // the tolerance, where the corner and the crossing lie apart: grown by 3 with miters it is 484.4865, the straight
            // path's and the oracle's. Joined round the space between at 0.01 it was 484.4827; joined as tips there, the
            // corner's turn read as the tip's, it grew a horn to 504.98.
            var loop = new GeoPolygonArc2(
                new[]
                {
                    P(5.980264, 8.391616), P(3.700746, 8.905191), P(8.34025, -8.144685), P(-6.255616, -7.038972), P(-4.003168, 0.894665),
                    P(-4.250402, 6.639121), P(-0.920583, -6.939917), P(-9.319689, -2.912664), P(-3.707002, 4.442028), P(-0.823001, -0.58429),
                },
                new double[10]);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 3.0, Miter, new Tolerance(1E-2, 1E-4)));

            Assert.Equal(484.4865, grown.Area, 0.01);
        }

        [Fact]
        public void ALoopThatCrossesItselfGrownWithMiters_IsTheSameFromEveryStartAndEitherWay()
        {
            // The loop of seed 1024 of the reviewer's straight fuzz, eight corners crossing itself at seven places, grown by 1
            // with miters at 1E-3: the straight path's 110.1120 from every start and both ways round. Its lobes were put back
            // together in the order the loop was drawn in, and from three starts each way it came out 110.1090, 1.3 tolerances
            // off.
            GeoPoint2[] corners =
            {
                P(-4.440205, 0.634499), P(-7.525558, 0.548613), P(-1.395223, 4.321708), P(-6.659873, 3.26902), P(7.496569, 4.079338),
                P(8.378131, 1.650867), P(-5.781848, 5.066835), P(0.795296, -3.79884),
            };

            foreach (bool reversed in new[] { false, true })
            {
                for (int start = 0; start < corners.Length; start++)
                {
                    GeoPoint2[] drawn = (reversed ? corners.Reverse() : corners).ToArray();
                    var loop = new GeoPolygonArc2(drawn.Skip(start).Concat(drawn.Take(start)), new double[drawn.Length]);

                    GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 1.0, Miter, Thousandth));

                    Assert.Equal(110.1120, grown.Area, 1E-4);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ABowTieOfThirtyDegreesWithAVertexOnItsEdgeBesideTheCrossing_IsMiteredAtItsTips(bool withVertex)
        {
            // The vertex at (0.0048296, -0.0012941) lies on the edge from (-10, 2.679492) to (0.2, -0.05359), 0.005 from the
            // crossing at the origin. The loop runs straight on through it, so it is no corner, and grown by 1 at a tolerance of
            // 0.01 the bow tie is mitered at its tips, 59.9696, with the vertex as without it. Read as a corner, it left the
            // miters out, 58.0684.
            var points = new List<GeoPoint2> { P(-10, 2.679492), P(0.2, -0.05359), P(0.2, 0.05359), P(-10, -2.679492) };

            if (withVertex)
            {
                points.Insert(1, P(0.0048296, -0.0012941));
            }

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(new GeoPolygonArc2(points, new double[points.Count]), 1.0, Miter, new Tolerance(1E-2, 1E-4)));

            Assert.Equal(59.9696, grown.Area, 1E-3);
        }

        [Theory]
        [InlineData(1E-2, 0.005)]
        [InlineData(1E-3, 0.0005)]
        public void TheBowTieWithAVertexOnItsEdgeBesideTheCrossing_KeepsTheMiterOfItsLongLobe(double tolerance, double beside)
        {
            // A vertex on the edge from (-10, 2) to (1, -0.2), the given distance from the crossing at the origin, within the
            // tolerance of it: the loop runs straight on through it, and the bow tie grown by 1 is the same as without it,
            // mitered out to (5.099, 0). Read as a corner, the vertex left the miter out.
            var t = new Tolerance(tolerance, tolerance * 0.01);
            double along = beside / System.Math.Sqrt(1.04);
            GeoPolygonArc2 plain = BowTieWithASmallLobe();
            var withVertex = new GeoPolygonArc2(
                new[] { P(-10, 2), P(along, -0.2 * along), P(1, -0.2), P(1, 0.2), P(-10, -2) },
                new[] { 0.0, 0.0, 0.0, 0.0, -0.25 });

            GeoPolygonArc2 expected = Assert.Single(Offset2.Offset(plain, 1.0, Miter, t));
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(withVertex, 1.0, Miter, t));

            Assert.Equal(5.0990195, grown.Vertices.Max(v => v.X), 1E-6);
            Assert.Equal(expected.Area, grown.Area, 1E-6);
        }
    }
}
