using System;
using System.Collections.Generic;
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
    /// Fault round G7a: a curved loop that crosses itself where the tolerance does not tell a crossing from a corner of the
    /// loop or another crossing beside it, offset with miters or chamfers, read at 17d06dd.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A crossing a corner of the loop lies within the tolerance of was joined round the space between, as when rounded,
    /// since the tolerance read the corner's turn as the tip's own and a miter run on along it grew a horn. A corner that
    /// bends the loop by a degree did so too, and the tip's miter was left out. Two crossings within the tolerance of each
    /// other are one place, which turns the blunt end of the sliver between them into a point, and the miter of the corner
    /// beside it ran on further than the loop's own corners take it.
    /// </para>
    /// <para>
    /// The expected areas are an oracle's, built apart from the code under test: the region read even-odd off a polygon of
    /// the loop within 1E-6, grown or shrunk by the straight path with round joins, with the miter of every corner that
    /// opens added or taken out, the tips at its crossings and the corner beside them among them, or for chamfers each cut
    /// square to the corner's bisector at the distance.
    /// </para>
    /// </remarks>
    public class OffsetFaultG7aCoarseCrossingTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, 1E-4);

        /// <summary>A bow tie of 30 degrees crossing itself at the origin, its lobes 10 and 0.2 long.</summary>
        private static GeoPolygonArc2 BowTieOfThirtyDegrees() => new GeoPolygonArc2(
            new[] { P(-10, 2.679492), P(0.2, -0.05359), P(0.2, 0.05359), P(-10, -2.679492) },
            new double[4]);

        /// <summary>
        /// A bow tie crossing itself at the origin: a lobe 10 long whose back is an arc of bulge -0.25 from (-10, -2) to
        /// (-10, 2), and a lobe 1 long whose back is the segment from (1, -0.2) to (1, 0.2).
        /// </summary>
        private static GeoPolygonArc2 BowTieWithASmallLobe() => new GeoPolygonArc2(
            new[] { P(-10, 2), P(1, -0.2), P(1, 0.2), P(-10, -2) },
            new[] { 0.0, 0.0, 0.0, -0.25 });

        /// <summary>
        /// A bow tie with a corner on its first edge the given distance from the crossing at the origin, past it where the
        /// distance is positive, the part of the edge beyond the corner from the crossing turned about the corner by the given
        /// angle in degrees, counter-clockwise.
        /// </summary>
        private static GeoPolygonArc2 WithACornerBeside(GeoPolygonArc2 bowTie, double beside, double degrees)
        {
            List<GeoPoint2> points = bowTie.Vertices.ToList();
            List<double> bulges = bowTie.Bulges.ToList();
            GeoVector2 along = points[0].GetVectorTo(points[1]).Normalize();
            var corner = P(along.X * beside, along.Y * beside);
            double angle = degrees * Math.PI / 180.0;
            int turned = beside > 0.0 ? 1 : 0;
            GeoVector2 arm = corner.GetVectorTo(points[turned]);

            points[turned] = P(
                corner.X + Math.Cos(angle) * arm.X - Math.Sin(angle) * arm.Y,
                corner.Y + Math.Sin(angle) * arm.X + Math.Cos(angle) * arm.Y);
            points.Insert(1, corner);
            bulges.Insert(1, 0.0);

            return new GeoPolygonArc2(points, bulges);
        }

        /// <summary>
        /// The loop 2:22 of the item 6 fuzz: ten corners, all but one edge arcs. The edges from (-3.518, 2.844) and from
        /// (-6.538, 2.921) cross 1.7 degrees apart at (-1.6656, 2.3453), and the edge from (-2.639, -1.235) crosses both
        /// 0.0078 from there, at points 0.00027 apart.
        /// </summary>
        private static GeoPolygonArc2 Random2Of22() => new GeoPolygonArc2(
            new[]
            {
                P(4.974181426211343, 2.3094530833463427), P(-3.518013476169674, 2.8438531853462816), P(5.202596064285652, 0.49707669322242864),
                P(6.365705494007891, -1.6794462090728093), P(6.723996622825041, 4.16482141901032), P(5.2122985083667075, 5.7594205233079485),
                P(-6.537845701229687, 2.9207237031872957), P(6.998112237545715, -1.9941852763268102), P(-2.6392084884639866, -1.2345566932272902),
                P(-3.3412833015161025, 4.460129828406558),
            },
            new[]
            {
                0.501818573196334, 0.0, 0.10051916465187416, 0.5337577390641708, -0.2816312175158557, 0.2883052303401312, -0.17516430265976313,
                0.6656101446438627, 0.5027930979629947, 0.42413967434509625,
            });

        /// <summary>
        /// The loop 2:21 of the item 6 fuzz: four corners, three edges arcs. The two edges either side of (-7.352, 4.040) run
        /// out to it and back 2.4 degrees apart, and the edge from (-3.351, -7.500) crosses both 0.21 from it, at points 0.0057
        /// apart.
        /// </summary>
        private static GeoPolygonArc2 Random2Of21() => new GeoPolygonArc2(
            new[] { P(-5.328003287002446, 9.64715434221884), P(8.41513851583709, -0.13216840575084454), P(-7.352356481064277, 4.03989128490905), P(-3.3506100221307067, -7.500150351552363) },
            new[] { -0.6027057737590306, 0.0, -0.5593748146944562, -0.3263279259327463 });

        [Fact]
        public void Loop2Of22ShrunkByAThirdOfAMillimetreWithMiters_CutsNoMoreThanItsCorners()
        {
            // Within 1E-3 the two crossings 0.00027 apart were one place, where the end of the sliver between the two edges 1.7
            // degrees apart, 0.00027 wide, came to a point; the corner of the space outside it was mitered and squared off at
            // the limit, and cut 0.063 out of the loop beside it. The oracle reads four loops of 24.2838; they were 24.2205.
            GeoPolygonArc2[] shrunk = Offset2.Offset(Random2Of22(), -0.3, new OffsetOptions(OffsetJoin.Miter), Thousandth);

            Assert.Equal(4, shrunk.Length);
            Assert.Equal(24.2838, shrunk.Sum(loop => loop.Area), 1E-3);
        }

        [Fact]
        public void Loop2Of22ShrunkByOneWithMiters_KeepsNoMoreThanTheOracle()
        {
            // The same place, shrunk by 1: the oracle reads one loop of 2.8570; it was 3.3581.
            GeoPolygonArc2 shrunk = Assert.Single(Offset2.Offset(Random2Of22(), -1.0, new OffsetOptions(OffsetJoin.Miter), Thousandth));

            Assert.Equal(2.8570, shrunk.Area, 1E-3);
        }

        [Fact]
        public void Loop2Of21GrownByThreeWithMitersAtAHundredth_GrowsNoMoreThanTheOracle()
        {
            // At 0.01 the two crossings 0.0057 apart were one place, where the spike's end, 0.0057 wide, came to a point. The
            // oracle reads 386.3506, as within 1E-4; it was 387.3845, 0.69 of it outside the offset.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(Random2Of21(), 3.0, new OffsetOptions(OffsetJoin.Miter), Hundredth));

            Assert.Equal(386.3506, grown.Area, 1E-3);
        }

        [Fact]
        public void ASpeckTheFinerStepLeavesBesideACrossing_IsNoLoopWithinTheTolerance()
        {
            // The loop 4:95 of the reviewer's fuzz at a twentieth of its size, ten corners in a box 0.95 by 0.86, read within a
            // finer step where the tolerance of 0.01 is coarse at a crossing. Shrunk by 0.1 with miters it is one loop of
            // 0.022622, as within the tolerance it was; the finer step also left a triangle 0.0013 by 0.0014 near (0.30, -0.15),
            // a point within the tolerance, which came back as a second loop.
            var loop = new GeoPolygonArc2(
                new[]
                {
                    P(-0.1923775713389635, -0.25516584690435135), P(0.22487501321587475, 0.23299901780346366), P(-0.07864783358697212, 0.12112541944772258),
                    P(-0.4690189566318965, 0.03314106749982626), P(-0.04286568357742651, -0.3577687115677487), P(0.38541360752909154, 0.4627163526428474),
                    P(0.4040842749663089, -0.2946885478658083), P(-0.22856746741038167, -0.39475068538205266), P(0.4767224732678022, 0.2667254376070227),
                    P(-0.16011011631233157, -0.2909169368403577),
                },
                new[] { 0.0, -0.42017484219752943, 0.0, 0.4331677723364755, -0.3355471098961062, 0.0, 0.33981722585848395, -0.052683499992212, -0.2251025082194723, -0.39003714420368757 });

            GeoPolygonArc2 shrunk = Assert.Single(Offset2.Offset(loop, -0.1, new OffsetOptions(OffsetJoin.Miter), Hundredth));

            Assert.Equal(0.022622, shrunk.Area, 1E-5);
        }

        [Fact]
        public void ACornerBendingTheLoopOneDegreeBesideTheCrossing_KeepsTheMiterOfTheLongLobe()
        {
            // The corner lies 0.005 past the crossing on the edge from (-10, 2), within 0.01 of it, and the loop turns there by
            // 1 degree. Grown by 1 the long lobe's tip is mitered past the small lobe, to about (5.1, 0): the oracle reads
            // 54.4194. The crossing was joined round the space between, its miter left out, 52.5008.
            GeoPolygonArc2 loop = WithACornerBeside(BowTieWithASmallLobe(), 0.005, 1.0);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 1.0, new OffsetOptions(OffsetJoin.Miter), Hundredth));

            Assert.Equal(PointLocation.Inside, grown.Locate(P(4, 0), Hundredth));
            Assert.Equal(54.4194, grown.Area, 1E-3);
        }

        [Fact]
        public void ACornerBendingTheLoopOneDegreeBesideTheCrossingWithinAThousandth_KeepsTheMiterOfTheLongLobe()
        {
            // The same within 1E-3, the corner 0.0005 past the crossing: the oracle reads 54.4192; it was 52.5007.
            GeoPolygonArc2 loop = WithACornerBeside(BowTieWithASmallLobe(), 0.0005, 1.0);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 1.0, new OffsetOptions(OffsetJoin.Miter), Thousandth));

            Assert.Equal(54.4192, grown.Area, 1E-3);
        }

        [Fact]
        public void ACornerBendingTheLongLobeTenDegreesBeforeTheCrossing_KeepsItsMiter()
        {
            // The corner lies 0.005 before the crossing on the edge from (-10, 2.679), and the long lobe's side beyond it is
            // turned 10 degrees about it. Grown by 1, the oracle reads 69.4893; it was 67.5663.
            GeoPolygonArc2 loop = WithACornerBeside(BowTieOfThirtyDegrees(), -0.005, -10.0);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 1.0, new OffsetOptions(OffsetJoin.Miter), Hundredth));

            Assert.Equal(69.4893, grown.Area, 1E-3);
        }

        [Fact]
        public void ACornerBendingTheLoopTwoDegreesBesideTheCrossing_KeepsTheChamfersOfTheTips()
        {
            // The corner lies 0.005 past the crossing of the bow tie of 30 degrees, the loop turning there by 2 degrees. Grown
            // by 3 with chamfers each tip is cut square 3 out: the oracle reads 137.9741; the tips were not cut, 137.1987.
            GeoPolygonArc2 loop = WithACornerBeside(BowTieOfThirtyDegrees(), 0.005, 2.0);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 3.0, new OffsetOptions(OffsetJoin.Chamfer), Hundredth));

            Assert.Equal(137.9741, grown.Area, 1E-3);
        }
    }
}
