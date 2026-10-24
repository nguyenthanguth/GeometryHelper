using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.OffsetFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Fault round F3: curved offsets that come back empty at a tolerance of 0.1, and a miter rounded off beside a sliver,
    /// read at d65ff2d.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three crossings within a tenth of each other: the crossings of a loop that crosses itself were made one place when each
    /// lay within the tolerance of the first found there, so three edges crossing in a triangle 0.1 across made two places
    /// 0.101 apart, an edge was cut twice within the tolerance, the loop could not be cut at its crossings, and it was offset
    /// as a simple loop: nothing.
    /// </para>
    /// <para>
    /// Crossings left uncut beside an end: two pieces that cross within the tolerance of where either ends are not cut there,
    /// and each runs on past the crossing; at a tolerance of 0.1 on a shape a few millimetres across, the runs end up to twice
    /// the tolerance apart, or a stub the slack lets stand carries a run past the next, the loop does not close, and the
    /// answer was nothing.
    /// </para>
    /// <para>
    /// A sliver turned backwards: a piece cut back past its own end by the corner before it to 0.0002 has no way of its own to
    /// run on, the miter at the next corner was refused, and the corner was rounded off.
    /// </para>
    /// <para>
    /// Every expected answer is the one the same loop gives within 1E-3, the straight path's, or one read by the distance
    /// from the source; none was found by the code under test alone.
    /// </para>
    /// </remarks>
    public class OffsetFaultF3Tests
    {
        private static readonly OffsetOptions Round = new OffsetOptions(OffsetJoin.Round);

        private static readonly OffsetOptions Miter = new OffsetOptions(OffsetJoin.Miter);

        private static readonly OffsetOptions Chamfer = new OffsetOptions(OffsetJoin.Chamfer);

        /// <summary>
        /// A loop of the S2 crossing fuzz (seed 3, random loop 106) that crosses itself at twelve places: three of them, where
        /// the arcs leaving (4.622, -2.141) and (-5.676, 8.989) cross the last edge and each other near (4.9, 3.7), lie 0.070,
        /// 0.084 and 0.101 apart.
        /// </summary>
        private static GeoPolygonArc2 LoopWithThreeCrossingsATenthApart() => new GeoPolygonArc2(
            new[]
            {
                P(6.9202171531134375, 4.597090331184255), P(9.772298876090115, 1.2190479185520893), P(4.621967000245101, -2.141028983584153),
                P(-5.676463141886733, 8.989394204220453), P(9.104171078234991, 2.81400720254239), P(-9.432066669423165, 3.487811886466952),
                P(-2.7981192864468873, 7.285228826704074), P(-7.69980831895946, -8.938388097536931),
            },
            new[] { -0.17932155201179978, 0.0, 0.6486618450603734, 0.12622283102302945, -0.6918214069641294, 0.0, -0.5231888607252337, -0.1773908118146429 });

        /// <summary>The audit's case GA 7006 (offset seed 1): twelve straight edges in a box 7.9 by 5.1, with a notch on top.</summary>
        private static GeoPolygonArc2 TwelveStraightCorners() => Loop(new[]
        {
            P(1.3593138102777311, -8.2644966818654542), P(1.0209089002815639, -9.051533908912063), P(-2.8702518774408645, -8.1528675998533053),
            P(0.57416419880214431, -3.9446377994343704), P(0.86495048675187092, -3.9121580091689188), P(1.629952219347732, -4.5003844828607829),
            P(2.5039346370979882, -5.1153609719469806), P(2.886978432207215, -5.2795732939829287), P(5.077300800128679, -5.9911736157181368),
            P(3.4452113981734191, -7.6541401745546489), P(3.52257204383917, -7.8634001008530952), P(1.6809807311497882, -7.2319699722043618),
        });

        /// <summary>The audit's case GA 1870 (offset seed 1): ten straight edges about (11, -20), with a spike 2.7 long and 12 degrees wide.</summary>
        private static GeoPolygonArc2 TenStraightCornersWithASpike() => Loop(new[]
        {
            P(9.46657355175024, -15.024003587035558), P(12.145338459734361, -19.22593065408525), P(12.097131378793827, -20.104790699697954),
            P(14.348030373327573, -21.5198021217657), P(11.809362035199303, -20.592367525630184), P(12.369781093818931, -21.975339698748556),
            P(10.808072276950124, -21.578530844472184), P(10.293814813992247, -21.177500756133366), P(8.4165115583918748, -23.35611628360703),
            P(8.90810643776487, -20.426984943322051),
        });

        /// <summary>The audit's case GA 4078 (offset seed 1): a star of sixteen corners about (-1, -17), some of its edges arcs.</summary>
        private static GeoPolygonArc2 StarOfSixteenCorners() => Loop(
            new[]
            {
                P(-2.9692922773386976, -16.833773156577276), P(-5.9713053396452436, -14.226382442371616), P(-2.2433586601237816, -15.484791751814392),
                P(-2.1659120818801405, -12.776135668775044), P(-0.740061017816668, -15.040958944017357), P(1.7079143108206245, -12.386319009592242),
                P(0.89647340941984965, -15.629563631494319), P(3.3240364588874431, -15.931904120909881), P(1.0216554786722409, -17.36026120476766),
                P(2.6791114648584564, -19.223742355032197), P(0.5286207327122634, -19.09927713013716), P(0.18780254399741447, -21.569585270293118),
                P(-1.305847683170638, -19.329802784322009), P(-3.3290942047715824, -21.10536744100877), P(-2.5675724254685885, -18.286169666682106),
                P(-6.1543358336264626, -18.468949820307664),
            },
            0.0, 0.0, -0.003, 0.0, 0.0, -0.0003, 0.03, -0.09, 0.09, 0.0, 0.0003, 0.0, 0.0003, 0.0003, 0.003);

        /// <summary>The audit's case GA 606 (offset seed 1): a comb of three teeth about (9, 3), the ends of its teeth arcs.</summary>
        private static GeoPolygonArc2 CombOfThreeTeeth() => Loop(
            new[]
            {
                P(4.2803364347514519, 5.0699538194634437), P(5.2763997965823251, 5.5960216115339092), P(8.07848317967082, 0.2905217562686836),
                P(8.5202113232684056, 0.52381911274471), P(5.718127940179909, 5.829318968009936), P(6.7141913020107822, 6.3553867600804024),
                P(9.51627468509928, 1.0498869048151762), P(9.9580028286968627, 1.2831842612912028), P(7.1559194456083661, 6.5886841165564292),
                P(8.15198280743924, 7.1147519086268947), P(10.954066190527737, 1.8092520533616687), P(11.39579433412532, 2.0425494098376955),
                P(8.5937109510368241, 7.3480492651029214), P(9.5897743128676964, 7.8741170571733878), P(13.559392438909732, 0.35799226221431724),
                P(8.2499545607934888, -2.4461709754956265),
            },
            -0.09, 0.0, 0.09, 0.0, 0.0, 0.0, 0.0, 0.0003, 0.0, 0.003, 0.15, 0.0, 0.15, 0.0, 0.0, -0.09);

        /// <summary>The signed areas of an answer, largest first: positive for a loop wound as the source, negative for a hole.</summary>
        private static double[] SignedAreas(GeoPolygonArc2[] loops, GeoPolygonArc2 source)
            => loops.Select(l => l.IsClockwise == source.IsClockwise ? l.Area : -l.Area).OrderByDescending(a => a).ToArray();

        /// <summary>How far every point of an answer of round joins stands off the distance from the source, at worst.</summary>
        private static double WorstOff(GeoPolygonArc2[] loops, GeoPolygonArc2 source, double distance)
            => loops.Max(l => WorstOffDistance(PiecesOf(l), PiecesOf(source), distance));

        [Fact]
        public void ALoopWhoseThreeCrossingsLieWithinATenthGrownWithRoundJoins_IsReadEvenOddAtATenth()
        {
            // Read even-odd the loop grown by 0.3 is an outer loop and three holes; within 1E-3 they enclose 181.675, 11.456,
            // 8.094 and 1.577. At a tenth the three crossings are one place, and the loops stand within a tenth of 0.3 from
            // the loop all round: 181.722, 11.275, 8.096 and 1.577. At d65ff2d the crossings were not cut and the answer was
            // empty.
            GeoPolygonArc2 loop = LoopWithThreeCrossingsATenthApart();

            GeoPolygonArc2[] grown = Offset2.Offset(loop, 0.3, Round, Tenth);

            double[] areas = SignedAreas(grown, loop);
            Assert.Equal(4, areas.Length);
            Assert.Equal(181.675436, areas[0], 0.1);
            Assert.Equal(-1.577336, areas[1], 0.1);
            Assert.Equal(-8.094155, areas[2], 0.1);
            Assert.Equal(-11.455688, areas[3], 0.3);
            Assert.True(WorstOff(grown, loop, 0.3) <= Tenth.EqualPoint);
        }

        [Fact]
        public void TheSameLoopShrunkWithRoundJoins_IsTheSixLoopsItPinchesInto()
        {
            // Shrunk by 0.3 the region falls into six loops, 46.151, 22.759, 17.068, 8.694, 0.920 and 0.544 on the straight
            // path. At d65ff2d, offset as a simple loop, it gave two, 8.094 and 1.577.
            GeoPolygonArc2 loop = LoopWithThreeCrossingsATenthApart();

            GeoPolygonArc2[] shrunk = Offset2.Offset(loop, -0.3, Round, Tenth);

            double[] areas = SignedAreas(shrunk, loop);
            Assert.Equal(6, areas.Length);
            Assert.Equal(46.151335, areas[0], 0.1);
            Assert.Equal(22.758805, areas[1], 0.3);
            Assert.True(WorstOff(shrunk, loop, 0.3) <= Tenth.EqualPoint);
        }

        [Fact]
        public void TwelveStraightCornersGrownByFourWithRoundJoinsAtATenth_IsOneLoopStandingFourOff()
        {
            // Two moved pieces beside the notch cross 0.057 from the end of one, are not cut there, and the run is left 0.31
            // open. Within 1E-3 the loop is one of 150.317, as the straight path gives (150.320, its round joins cut within
            // 1E-5). At d65ff2d the answer was empty.
            GeoPolygonArc2 loop = TwelveStraightCorners();

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 4.0, Round, Tenth));

            Assert.Equal(150.319643, grown.Area, 0.01);
            Assert.True(WorstOff(new[] { grown }, loop, 4.0) <= Tenth.EqualPoint);
        }

        [Fact]
        public void TwelveStraightCornersGrownByFourWithMitersAtATenth_IsTheStraightPathsLoop()
        {
            // Every edge is straight, so the two paths draw the same miters: 186.800611. At d65ff2d the answer was empty.
            GeoPolygonArc2 loop = TwelveStraightCorners();
            GeoPolygon2 straight = Assert.Single(Offset2.Offset(new GeoPolygon2(loop.Vertices.ToList()), 4.0, Miter, Tenth));

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 4.0, Miter, Tenth));

            Assert.Equal(straight.Area, grown.Area, 1E-6);
        }

        [Fact]
        public void ALoopWithANarrowSpikeShrunkAtATenth_IsOneLoop()
        {
            // The spike, 12 degrees wide and 2.7 long, is gone shrunk by 0.3, and the round joins either side of its foot meet
            // 0.054 from where the next starts, but a stub 0.14 long that stands 0.27 from the shape, within the slack of a
            // tenth, carries the run 0.21 past it. Within 1E-3 the round answer is 9.4526 (the straight path 9.4565) and the
            // chamfered 9.4352 (9.4294). At d65ff2d both were empty.
            GeoPolygonArc2 loop = TenStraightCornersWithASpike();

            GeoPolygonArc2 round = Assert.Single(Offset2.Offset(loop, -0.3, Round, Tenth));
            GeoPolygonArc2 chamfered = Assert.Single(Offset2.Offset(loop, -0.3, Chamfer, Tenth));

            Assert.Equal(9.456550, round.Area, 0.01);
            Assert.Equal(9.429353, chamfered.Area, 0.01);
            Assert.True(WorstOff(new[] { round }, loop, 0.3) <= Tenth.EqualPoint);
        }

        [Fact]
        public void AStarOfSixteenCornersShrunkByOneAtATenth_IsOneLoop()
        {
            // Shrunk by 1 the runs either side of a crossing left uncut end 0.116 apart. Round, the loop stands 1 from the
            // star all round, 4.7789; chamfered, it holds less than the round one, 4.4606. At d65ff2d both were empty.
            GeoPolygonArc2 loop = StarOfSixteenCorners();

            GeoPolygonArc2 round = Assert.Single(Offset2.Offset(loop, -1.0, Round, Tenth));
            GeoPolygonArc2 chamfered = Assert.Single(Offset2.Offset(loop, -1.0, Chamfer, Tenth));

            Assert.True(WorstOff(new[] { round }, loop, 1.0) <= Tenth.EqualPoint);
            Assert.InRange(chamfered.Area, 0.9 * round.Area, round.Area);
        }

        [Fact]
        public void ACombOfThreeTeethGrownWithMitersAtATenth_IsOneLoopRoundTheRoundOne()
        {
            // Grown by 0.3 a miter holds what a round join does, 62.1298, and a chamfer between the two: within 1E-3 the miter
            // is 62.2963 and the chamfer 62.1582. At d65ff2d both were empty.
            GeoPolygonArc2 loop = CombOfThreeTeeth();
            double round = Offset2.Offset(loop, 0.3, Round, Tenth).Max(l => l.Area);

            GeoPolygonArc2 mitered = Assert.Single(Offset2.Offset(loop, 0.3, Miter, Tenth));
            GeoPolygonArc2 chamfered = Assert.Single(Offset2.Offset(loop, 0.3, Chamfer, Tenth));

            Assert.Equal(62.296301, mitered.Area, 0.01);
            Assert.InRange(chamfered.Area, round, mitered.Area);
        }

        [Fact]
        public void ALoopWithASliverBeforeATipGrownWithMitersAtAThousandth_IsMiteredAtTheTip()
        {
            // The tip at (3.4164, -7.0194), 22.6 degrees, is mitered 1.546 out, under the limit of 3: 54.769871, as at 1E-4,
            // where the piece before it keeps its arc. At d65ff2d the piece before was a sliver 1.94E-4 long turned backwards,
            // the miter was refused and the tip rounded off: 54.441155.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(LoopWithASliverBeforeATip(), 0.3, Miter, Thousandth));
            GeoPolygonArc2 finer = Assert.Single(Offset2.Offset(LoopWithASliverBeforeATip(), 0.3, Miter, TenThousandth));

            Assert.Equal(finer.Area, grown.Area, 1E-6);
            Assert.Contains(grown.Vertices, v => Math.Abs(v.DistanceTo(P(3.4163702621200915, -7.0193847627469275)) - 1.546) <= 1E-3);
        }
    }
}
