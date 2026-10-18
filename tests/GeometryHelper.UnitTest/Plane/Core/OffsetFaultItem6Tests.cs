using System;
using System.Globalization;
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
    /// A curved loop that crosses itself is offset the way the straight offsets read one, even-odd, with its arcs kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The offset of a loop is built piece by piece and joined at the loop's vertices, which can tell nothing of two edges that
    /// cross away from one. Two neighbouring arcs that meet again beside the vertex they share, as the arcs at each inner
    /// vertex of a gear of 642 teeth do, are such a loop: the run round it misses closing by twice the distance, and the answer
    /// was empty, or a loop too many, or one left out. The loop is read now as the straight offsets read it, as the region that
    /// lies inside an odd number of the simple loops its crossings cut it into, and that region is offset.
    /// </para>
    /// </remarks>
    public class OffsetFaultItem6Tests
    {
        private static readonly OffsetOptions Round = new OffsetOptions(OffsetJoin.Round);

        /// <summary>The net area of loops, those wound against the source counted against it.</summary>
        private static double NetArea(GeoPolygon2[] loops, bool clockwise)
            => loops.Sum(loop => loop.IsClockwise == clockwise ? loop.Area : -loop.Area);

        /// <summary>The net area of the curved loops, those wound against the source counted against it.</summary>
        private static double NetArea(GeoPolygonArc2[] loops, bool clockwise)
            => loops.Sum(loop => loop.IsClockwise == clockwise ? loop.Area : -loop.Area);

        /// <summary>What the straight path gives for the loop cut within 1E-5: the loops and their net area.</summary>
        private static GeoPolygon2[] Straight(GeoPolygonArc2 source, double distance, OffsetOptions options, Tolerance tolerance)
            => Offset2.Offset(source.Flatten(1E-5), distance, new OffsetOptions(options.Join, options.MiterLimit, 1E-5), tolerance);

        private static GeoPolygonArc2 Parse(string text)
        {
            string[] items = text.Split(';');
            var points = new GeoPoint2[items.Length];
            var bulges = new double[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                string[] bits = items[i].Split('b');
                string[] xy = bits[0].Split(',');

                points[i] = P(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture));
                bulges[i] = bits.Length > 1 ? double.Parse(bits[1], CultureInfo.InvariantCulture) : 0.0;
            }

            return new GeoPolygonArc2(points, bulges);
        }

        /// <summary>The bowtie from (0, 0) to (10, 0), across to (0, 10), along to (10, 10) and back across, crossing at (5, 5), its bottom and top bowed 0.3 outwards.</summary>
        private static GeoPolygonArc2 BowedBowtie()
            => Loop(new[] { P(0, 0), P(10, 0), P(0, 10), P(10, 10) }, 0.3, 0.0, -0.3, 0.0);

        /// <summary>The five-pointed star drawn in one stroke, five chords of a circle of radius 10 each bowed by 0.05.</summary>
        private static GeoPolygonArc2 BowedPentagram()
        {
            var points = new GeoPoint2[5];

            for (int k = 0; k < 5; k++)
            {
                double angle = Math.PI / 2.0 + 2.0 * Math.PI * 2.0 * k / 5.0;
                points[k] = P(10.0 * Math.Cos(angle), 10.0 * Math.Sin(angle));
            }

            return Loop(points, 0.05, 0.05, 0.05, 0.05, 0.05);
        }

        [Fact]
        public void TwoArcsCrossingBesideTheirCornerGrownByATenth_AreOneLoopAtTheDistance()
        {
            // The loop crosses itself at X = (-1.980, 0.198). Grown by 0.1 the lens between the arcs past X fills, and what
            // stands 0.1 from the loop is one loop, as the straight path gives it on the loop cut within 1E-5 (5.955385, its
            // round corners cut within 1E-5 too). At 5a7843c the run round the loop misses closing by 0.2000624 at X, twice
            // the distance, and the answer is empty.
            GeoPolygonArc2 source = ArcsCrossingBesideTheirCorner();
            double straight = NetArea(Straight(source, 0.1, Round, Thousandth), source.IsClockwise);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, 0.1, Round, Thousandth));

            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(source), 0.1) <= 1E-9);
            Assert.Equal(straight, grown.Area, 1E-3);
        }

        [Fact]
        public void TwoArcsCrossingBesideTheirCornerGrownByAHalf_AreOneLoopAtTheDistance()
        {
            // Grown by 0.5 the lens is lost in the loop round it: one loop of 14.5114, the straight path's 14.511405.
            GeoPolygonArc2 source = ArcsCrossingBesideTheirCorner();
            double straight = NetArea(Straight(source, 0.5, Round, Thousandth), source.IsClockwise);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, 0.5, Round, Thousandth));

            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(source), 0.5) <= 1E-9);
            Assert.Equal(straight, grown.Area, 1E-3);
        }

        [Fact]
        public void TwoArcsCrossingBesideTheirCornerShrunkByATenth_AreOneLoopAtTheDistance()
        {
            // Shrunk, the loop was already right: 2.645362, the straight path's 2.645363. It is the same answer now that the
            // loop is cut at its crossing first.
            GeoPolygonArc2 source = ArcsCrossingBesideTheirCorner();
            double straight = NetArea(Straight(source, -0.1, Round, Thousandth), source.IsClockwise);

            GeoPolygonArc2 shrunk = Assert.Single(Offset2.Offset(source, -0.1, Round, Thousandth));

            Assert.True(WorstOffDistance(PiecesOf(shrunk), PiecesOf(source), -0.1) <= 1E-9);
            Assert.Equal(straight, shrunk.Area, 1E-3);
        }

        [Fact]
        public void TwoArcsStoppingShortOfTheirCrossingGrownByATenth_AreOneLoopAtTheDistance()
        {
            // The same corner run only 70 % of the way to X is simple, and grown by 0.1 comes back as one loop of 0.33832
            // (the straight path, its corners cut within the default share, 0.338243). A simple loop is offset as it always was.
            GeoPolygonArc2 source = ArcsStoppingShortOfTheirCrossing();

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, 0.1, Round, Thousandth));

            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(source), 0.1) <= 1E-9);
            Assert.Equal(0.33832, grown.Area, 1E-5);
        }

        [Theory]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        public void TheGearOf641TeethGrownOrShrunkByAHalfWithRoundCorners_IsOneLoopAtTheDistance(double distance)
        {
            // 641 teeth is the last count at which the gear is simple: the two arcs at each inner vertex set off 1.1E-5
            // radians apart. Grown by 0.5 its valleys fill and it is one loop round the tips; shrunk by 0.5 its teeth go and
            // it is one loop round the roots.
            GeoPolygonArc2 source = Gear(641);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, distance, Round, Thousandth));

            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(source), distance) <= 1E-9);
        }

        [Theory]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        public void TheGearOf642TeethGrownOrShrunkByAHalfWithRoundCorners_IsOneLoopAtTheDistance(double distance)
        {
            // One more tooth and the arcs cross: at each inner vertex the two arcs meet again 0.0079 away, and the loop runs
            // round a lens 3E-9 in area and 4E-7 wide, a slit in the gear. Grown by 0.5 the slits close and the answer is the
            // loop round the tips, one of 31 667.81; it was that loop and the loop round the roots beside it. Shrunk by 0.5
            // every slit is a hole 1 wide at its tip, and the loop round the roots, 28 106.67 (the straight path's 28 106.72,
            // 28 106.93 for 641 teeth), it was nothing. Every point stands 0.5 from the slits as well as from the rest.
            GeoPolygonArc2 source = Gear(642);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, distance, Round, Thousandth));

            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(source), distance) <= 1E-9);
            Assert.Equal(distance > 0.0 ? 31667.81 : 28106.67, grown.Area, 1E-2);
        }

        [Fact]
        public void TheGearOf642TeethGrownByAHalfWithMiterCorners_IsOneLoop()
        {
            // The same gear with mitered corners came back as nothing.
            GeoPolygonArc2 source = Gear(642);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, 0.5, new OffsetOptions(OffsetJoin.Miter), Thousandth));

            Assert.InRange(grown.Area, 32000.0, 32400.0);
        }

        [Fact]
        public void TheLoopOfAuditCaseGA2686GrownByHalfAThousandthWithMiterLimitTwo_IsOneLoop()
        {
            // 14 vertices, bulges from -0.6 to 0.6, two arcs of it meeting again beside their vertex. Grown by 0.0005 within
            // (1E-4, 1E-6) with a mitre limit of 2 it came back as nothing; the straight path gives 4 955.4877 (the loop cut
            // within 1E-5).
            GeoPolygonArc2 source = Parse(
                "-32.688506930098356,-9.624934706857978b-0.6;-13.01204284350985,-17.772174150973367b-0.3;"
                + "-5.6465336764954195,-46.012211069441975b0.03;15.637547810411204,-25.363745731906072b0.15;"
                + "42.820108263393521,-38.571592700559385b-0.15;40.42486488674863,-9.0005098621588715b0.0003;"
                + "56.333206703543738,5.1250067622000355b0.09;41.06047502179608,20.576137577917851b-0.03;"
                + "48.236259118245343,50.212673156866956;19.802338211962713,41.006255031674677b-0.0003;"
                + "2.0513751062531362,56.592420119836312b0.003;-8.0100696789766737,33.680525758391312b-0.6;"
                + "-28.383529098440935,29.390447313779337b-0.3;-25.528780583507004,10.052471511203494b0.003");
            var tolerance = new Tolerance(1E-4, 1E-6);
            var options = new OffsetOptions(OffsetJoin.Miter, 2.0, 0.0);
            double straight = NetArea(Straight(source, 0.0005, options, tolerance), source.IsClockwise);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, 0.0005, options, tolerance));

            Assert.Equal(straight, grown.Area, 0.1);
        }

        [Fact]
        public void ABowtieWithBowedSidesGrownByOne_IsOneLoopThatHoldsBothLobes()
        {
            // The loop crosses itself at (5, 5) and is two lobes, one wound against the other. It is read even-odd, both
            // lobes: each grown by 1 with round corners, the two of them together.
            GeoPolygonArc2 source = BowedBowtie();
            double straight = NetArea(Straight(source, 1.0, Round, Thousandth), source.IsClockwise);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, 1.0, Round, Thousandth));

            Assert.True(WorstOffDistance(PiecesOf(grown), PiecesOf(source), 1.0) <= 1E-9);
            Assert.Equal(straight, grown.Area, 1E-2);
        }

        [Fact]
        public void ABowtieWithBowedSidesShrunkByAHalf_IsTwoLoops()
        {
            // Shrunk, the two lobes part at the pinch: one loop in each, as the straight path gives them.
            GeoPolygonArc2 source = BowedBowtie();
            GeoPolygon2[] straight = Straight(source, -0.5, Round, Thousandth);

            GeoPolygonArc2[] shrunk = Offset2.Offset(source, -0.5, Round, Thousandth);

            Assert.Equal(2, straight.Length);
            Assert.Equal(2, shrunk.Length);
            Assert.True(WorstOffDistance(shrunk.SelectMany(PiecesOf).ToArray(), PiecesOf(source), -0.5) <= 1E-9);
            Assert.Equal(NetArea(straight, source.IsClockwise), NetArea(shrunk, source.IsClockwise), 1E-2);
        }

        [Theory]
        [InlineData(0.3)]
        [InlineData(-0.2)]
        public void AFivePointedStarDrawnInOneStrokeWithBowedSides_IsOffsetAsItsEvenOddRegion(double distance)
        {
            // Drawn in one stroke the star crosses itself five times, and the pentagon in its middle is inside two of the
            // loops its crossings make: not in the region, a hole. Grown by 0.3 the hole is smaller, shrunk by 0.2 it is
            // larger, and the straight path gives the same loops.
            GeoPolygonArc2 source = BowedPentagram();
            GeoPolygon2[] straight = Straight(source, distance, Round, Thousandth);

            GeoPolygonArc2[] got = Offset2.Offset(source, distance, Round, Thousandth);

            Assert.Equal(straight.Length, got.Length);
            Assert.True(WorstOffDistance(got.SelectMany(PiecesOf).ToArray(), PiecesOf(source), distance) <= 1E-9);
            Assert.Equal(NetArea(straight, source.IsClockwise), NetArea(got, source.IsClockwise), 1E-2);
        }

        /// <summary>The signed areas of loops, those wound against the source counted against it, smallest first.</summary>
        private static double[] AreasOf(GeoPolygonArc2[] loops, bool clockwise)
            => loops.Select(loop => loop.IsClockwise == clockwise ? loop.Area : -loop.Area).OrderBy(area => area).ToArray();

        /// <summary>The signed areas of straight loops, those wound against the source counted against it, smallest first.</summary>
        private static double[] AreasOf(GeoPolygon2[] loops, bool clockwise)
            => loops.Select(loop => loop.IsClockwise == clockwise ? loop.Area : -loop.Area).OrderBy(area => area).ToArray();

        /// <summary>Asserts that the offset has every loop the straight path gives, each of the area it gives within a margin.</summary>
        private static void AssertTheLoopsOfTheStraightPath(GeoPolygonArc2 source, double distance, OffsetOptions options, Tolerance tolerance, double margin)
        {
            double[] straight = AreasOf(Straight(source, distance, options, tolerance), source.IsClockwise);
            double[] got = AreasOf(Offset2.Offset(source, distance, options, tolerance), source.IsClockwise);

            Assert.Equal(straight.Length, got.Length);

            for (int i = 0; i < straight.Length; i++)
            {
                Assert.Equal(straight[i], got[i], margin);
            }
        }

        [Fact]
        public void ALoopOfFourBowedEdgesCrossingItselfShrunkByOneWithMiterCorners_IsOneLoop()
        {
            // Four edges with bulges from -0.62 to 0.31 in a square of 15, three crossings. Shrunk by 1 with the default
            // miter limit it came back as nothing; the straight path gives one loop of 79.288 and round corners 79.301.
            GeoPolygonArc2 source = Parse(
                "5.4071815383654,-6.9588685394073222b-0.11289581694309403;8.5213624027191486,6.4709868265646442b0.30742179211574694;"
                + "7.0941910692929255,-8.535239826205764b-0.43644652689641639;-6.7566770579464164,5.1056127972461347b-0.62019506614664333");

            AssertTheLoopsOfTheStraightPath(source, -1.0, OffsetOptions.Default, Thousandth, 0.1);
        }

        [Theory]
        [InlineData(10.0)]
        [InlineData(2.0)]
        public void ALoopOfEightEdgesCrossingItselfShrunkByThreeTenthsWithMiterCorners_KeepsSixLoops(double miterLimit)
        {
            // Six loops of 21.512, 3.540, 3.340, 3.226, 1.083 and 0.747: the one of 3.540 was left out, with its corners
            // squared off, when it was the pieces of two other loops that stood about it.
            GeoPolygonArc2 source = Parse(
                "3.6572303686557479,-3.7262285378418074b0.048859344305870689;-6.5070450382805642,9.8772798105502879b-0.61638980587776271;"
                + "-2.8771717999489841,6.54039025145601b-0.26292500587316464;7.1048861449141469,7.4953345290829141b-0.29381271972964179;"
                + "4.7817005844701548,6.0077974274790833b0.33488691050321173;5.9251005090424336,0.17428252388457111;"
                + "-3.23126045671816,-2.2772250754186993b-0.63807115393600944;4.4912959702738071,9.2235654402633962b-0.27405134140236831");

            AssertTheLoopsOfTheStraightPath(source, -0.3, new OffsetOptions(OffsetJoin.Miter, miterLimit, 0.0), Thousandth, 0.01);
        }

        [Theory]
        [InlineData(OffsetJoin.Miter, 10.0)]
        [InlineData(OffsetJoin.Miter, 2.0)]
        [InlineData(OffsetJoin.Chamfer, 10.0)]
        public void ALoopOfTenEdgesCrossingItselfGrownByFiftyThousandthsWithMiterOrChamferCorners_KeepsTheHoleOf4_31(OffsetJoin join, double miterLimit)
        {
            // Five loops, among them a hole of 4.31 between the arcs and edges that cross beside it. The hole was left out
            // with mitered and chamfered corners, whose cut across the corner two other pieces cross within 0.0002.
            GeoPolygonArc2 source = Parse(
                "-3.4748114615095833,-8.0278672641272966b-0.15922127787918838;-7.87889578280919,7.3101038007578367;"
                + "9.3151584264427214,-3.5079600724894364;-1.2488410860527495,-0.69623838676895922;4.3724543854465949,-7.316538559885946;"
                + "-6.5365259892011647,5.2377792425629579b-0.25238729545492083;-0.29820065493611558,-1.5479472240190706b-0.54497801905729715;"
                + "8.9654014161626812,4.5037553154415235;-7.6606450870915523,9.8156990855074b0.3342398180785775;"
                + "2.3897070029702547,0.016059647321728932b0.06231977304551739");

            AssertTheLoopsOfTheStraightPath(source, 0.05, new OffsetOptions(join, miterLimit, 0.0), Thousandth, 0.02);
        }

        [Theory]
        [InlineData(OffsetJoin.Round)]
        [InlineData(OffsetJoin.Miter)]
        [InlineData(OffsetJoin.Chamfer)]
        public void ALoopOfEightEdgesCrossingItselfGrownByFiftyThousandths_KeepsTheHoleOf0_011(OffsetJoin join)
        {
            // Three loops: the outside of 43.68, a hole of 0.037 and a hole of 0.011, at (4.995, -3.450). The smaller hole
            // was filled in, because the piece that bounds it stands nearer to the loop than the distance for a stretch of
            // 0.002 and was read as lying inside the offset.
            GeoPolygonArc2 source = Parse(
                "8.6945653886974625,9.0392918135222473;4.0791916726525823,9.3832749684263348b0.36578297487729361;"
                + "3.2113780515321437,-3.4750895078643644b-0.50278189070652324;-0.12242463888713395,-5.8277220166417409b0.029087104242801254;"
                + "5.4670603645346425,-3.1120218583904311b-0.50617749644731047;3.3669166608559529,-3.1520749689787975b0.36035310051420377;"
                + "9.0167300771068462,-1.9994487110522812;-0.0584429549325467,-1.7656535151254644b-0.038245385297688395");

            AssertTheLoopsOfTheStraightPath(source, 0.05, new OffsetOptions(join), Thousandth, 0.005);
        }

        [Theory]
        [InlineData(0.05)]
        [InlineData(0.3)]
        [InlineData(1.0)]
        public void AFiveVertexLoopWhoseArcsCrossAgainBesideACorner_IsOneLoopGrown(double distance)
        {
            // Two arcs of bulges -0.2943 and 0.4856 leave the vertex (-6.435, -3.631) an angle of 2E-5 apart and cross again
            // 0.0007 from it, a lens too small to tell from the vertex: the corner is a cusp, which the offset goes round, and
            // not a turn of 179.99998 degrees to the side the headings say. Read that way the corner was tied by a chord and
            // the whole answer was empty; grown by 0.3 it is one loop of 78.857, the straight path's.
            GeoPolygonArc2 source = Parse(
                "9.22005940192382,0.8750372058130047b0.39405869324414922;-2.9497767672640158,1.3649341796361529;"
                + "9.3025170077115824,-3.9827184351080644b0.061145228967603882;9.2643587846608639,-4.439614794421761b-0.2943075079444365;"
                + "-6.4352961333632921,-3.6307565465712717b0.48563872267754671");

            AssertTheLoopsOfTheStraightPath(source, distance, Round, Thousandth, 0.01);
        }

        [Theory]
        [InlineData(0.05)]
        [InlineData(0.3)]
        [InlineData(1.0)]
        public void ANineVertexLoopWhoseArcsCrossAgainBesideATip_IsOneLoopGrown(double distance)
        {
            // The arcs of bulges 0.3303 and -0.4409 meet at (9.524, -6.196) an angle of 2E-4 apart and cross again 0.0005 from
            // it. Read by the angle, the tip turned the way that closed it and was tied by a chord across the tip: the answer
            // was empty. Grown by 0.3 it is one loop of 105.87, the straight path's.
            GeoPolygonArc2 source = Parse(
                "9.5237039399909342,-6.1962010041792883b-0.395481889646259;-0.21019880204005048,-4.6944391702741566b-0.49943798594150596;"
                + "3.3962197198514925,-1.4855423530030727;-3.7006429553500571,-1.7844298723081273b0.14124037997854888;"
                + "2.7791218519113592,8.396604488788455;6.0042192675192929,0.73592870996144022b0.569121988801808;"
                + "-7.7136647970013623,-5.26994040946939b0.3558976655154944;9.1000599968713054,-0.12173070112323892b0.1526116942300515;"
                + "7.0183429573747986,-5.6090103162494538b0.44091814399739637");

            AssertTheLoopsOfTheStraightPath(source, distance, Round, Thousandth, 0.01);
        }

        [Fact]
        public void ANineVertexLoopCrossingItselfThroughTheCornerOfAMiterGrownByOne_KeepsItsOuterLoop()
        {
            // A piece of the offset passes through the corner where two others meet, which turns the way that keeps it outside
            // both of them. Read by one of the two alone it was inside, its second half was dropped, and the outer loop, 149.06
            // (the straight path's 150.89, its arcs not run on along their circles), was lost with it; the hole of 1.60 stood.
            GeoPolygonArc2 source = Parse(
                "7.696204137846923,-6.9722061497029877b0.62044294267913469;2.0358558427709426,-2.762087365967262;"
                + "-4.1536621442780186,-9.4107035544750754b-0.44799512976221512;-0.80093506295277628,-4.2909458253024821b0.58361183772031766;"
                + "-8.8982879830982018,2.5228140095820706b0.39562826785055361;4.7757690189293438,-6.516307339312652;"
                + "-3.9721162868533817,-4.9443368776442185;9.70852608778911,-2.586366940562784;"
                + "5.4169605651018031,-5.6613457648369234b0.69690824644496119");

            AssertTheLoopsOfTheStraightPath(source, 1.0, OffsetOptions.Default, Thousandth, 2.0);
        }

        [Fact]
        public void ASevenVertexLoopCrossingItselfGrownByFiftyThousandthsWithMiterCornersOfLimitTwo_IsOneLoop()
        {
            // The same fault in a loop of 54.34: the outer loop was lost at a corner a piece passed through.
            GeoPolygonArc2 source = Parse(
                "-3.1419172199172518,-1.5391200834601744b-0.37205897982793812;-1.8506857342322753,6.9843736463153618b-0.67542666540268181;"
                + "7.4170259281140858,-6.71031855824884;8.2819542001383155,-0.97822800789877284b0.24121609439198677;"
                + "-4.394691788775237,-2.8284419480843672b-0.16522966663596672;5.16901487259614,1.6794710195062095;"
                + "6.996671146245987,8.6046148550718158b0.5122798968163691");

            AssertTheLoopsOfTheStraightPath(source, 0.05, new OffsetOptions(OffsetJoin.Miter, 2.0, 0.0), Thousandth, 0.01);
        }

        [Fact]
        public void ASevenVertexLoopWithAThinLensTouchingACornerShrunkByFiftyThousandths_IsOneLoop()
        {
            // The even-odd region is 33.2, with a lens 0.07 across that touches the corner of a triangle in it, and shrunk by
            // 0.05 it is one loop of 30.865. The offset of the lens and the offset of the rest ended apart at that corner,
            // 0.0043 from each other's starts, because the stretches of the pieces between their crossings there, hair-short,
            // were thrown away, and none of the three runs closed: the answer was empty.
            GeoPolygonArc2 source = Parse(
                "-8.1504782932579882,9.1182020023084256;2.0534073338161267,0.25153384090006981b0.31721731043291146;"
                + "2.116449937278615,-3.5230229671686066b0.31392520769216348;-3.1097906050783539,-2.7680071037113692b-0.54974633019871366;"
                + "-4.6731058297087937,-0.851934696944399b0.24201019999664752;-1.3437971991225126,2.9970766850733561b0.30112685654364846;"
                + "-0.30975884306698909,-1.9894961509804681");

            AssertTheLoopsOfTheStraightPath(source, -0.05, Round, Thousandth, 0.01);
        }

        [Fact]
        public void AnEightVertexLoopCrossingItselfShrunkByFiftyThousandths_KeepsItsOuterLoop()
        {
            // Three loops of 186.176, 45.341 and 0.760; the outer one was two runs 0.0076 apart, joined through two stretches
            // of 0.0038 that were thrown away, and was left out.
            GeoPolygonArc2 source = Parse(
                "3.4232754602205357,-5.2120845556315425;-1.4942412411301582,0.349920583120511;-1.5636818630451721,3.2227010155202347;"
                + "-5.2969275951836856,2.4438118806312836;8.0937005198065677,-8.6698678409074752b0.58737444378779013;"
                + "8.7903050607025186,7.6851677325019452b0.68844876661358811;-3.7066428147752974,-2.380908770664087b0.11017578952488294;"
                + "-6.0756569803113383,-9.8170800506216853");

            AssertTheLoopsOfTheStraightPath(source, -0.05, Round, Thousandth, 0.01);
        }

        [Fact]
        public void ASevenVertexLoopCrossingItselfGrownByFiftyThousandths_HasAHoleOf0_30TheStraightPathFillsIn()
        {
            // The even-odd region grown by 0.05 leaves a hole of 0.3026 near (2.2, -2.8): none of 300 points inside it lies in the
            // region or within 0.05 of it. The straight path fills it in, and has one loop of 78.580 for the two of this.
            GeoPolygonArc2 source = Parse(
                "-7.79198354007303,-7.8789582279878472;1.9809012729585636,-4.37855134456351b0.58293747849899225;"
                + "0.29469258631332451,-0.72622602373651546b-0.0096026516098541848;-5.00608892878801,-6.1268820968116078b0.071835812074987127;"
                + "-0.9837994589394885,8.4689568255417775;7.5451031502080639,-5.4950904173287984b-0.62568792110573868;"
                + "2.0372614972466891,-1.9429741110387138b-0.50731859281999925");

            GeoPolygonArc2[] grown = Offset2.Offset(source, 0.05, Round, Thousandth);

            Assert.Equal(2, grown.Length);
            Assert.Equal(78.898, grown.Max(loop => loop.Area), 1E-2);
            Assert.Equal(0.3026, grown.Min(loop => loop.Area), 1E-3);
        }

        [Fact]
        public void ATenVertexLoopCrossingItselfShrunkByThreeTenthsWithChamferCornersAtATolerance1EMinus4_KeepsTheLoopOf16_26()
        {
            // Five loops, the largest of 16.258 (Round has it at 16.297). Two pieces lie along one line a hair apart, each kept
            // where the other was thrown away: the run of one ends part-way along the first edge of the other, 0.001 from where
            // that begins, at both ends of the loop, and none of the runs closed.
            GeoPolygonArc2 source = Parse(
                "-5.8337993807316755,-4.3903884638056105b-0.043150693431101117;"
                + "4.9947238876459785,8.683187001702926b0.39074003691353831;"
                + "5.5815076993692241,5.1230915706246591b-0.65682728223354891;"
                + "3.584616656221737,4.3423033851861508b0.23106164007031438;"
                + "-9.3584795665733882,-0.30215246151301756b-0.0398290056455084;"
                + "-4.139889718098515,-1.1163054179010459b-0.26375223517592633;"
                + "-1.3713505358301799,0.30822056825655686b-0.18598239071945766;"
                + "-4.2359392131846114,2.1984400377601574b-0.12145604538799082;"
                + "-1.5059651953661657,-4.6535381091076591b-0.080434968965330533;"
                + "5.578813662556378,5.8688778131589654b0.02729715394195964");
            var tolerance = new Tolerance(1E-4, 1E-6);

            AssertTheLoopsOfTheStraightPath(source, -0.3, new OffsetOptions(OffsetJoin.Chamfer), tolerance, 0.01);
        }

        [Theory]
        [InlineData(1E-3, 1E-5)]
        [InlineData(1E-4, 1E-6)]
        public void ATenVertexLoopCrossingItselfGrownByThreeTenthsWithMiterCorners_KeepsTheHoleRoundKeeps(double point, double vector)
        {
            // Round has three loops, the outer one of 132.94 and a hole of 10.99 beside one of 0.58. The straight path has the
            // hole as two, 6.63 and 3.99. Miter corners lost the hole whole: its run closed on itself at a point, and went on
            // to a second loop closed only by a piece 2.5 long thrown away at the crossing there, which stands the distance
            // from the shape all the same. It comes back as one hole of 10.65 touching itself, 123.27 net (the straight path's
            // 123.14).
            GeoPolygonArc2 source = Parse(
                "-7.19565233085102,-2.4615238851222792b-0.082781096726088355;0.13161850633640526,-1.5469683853662435;"
                + "-8.918934985491882,-4.5536933162033986b0.11738094846596048;"
                + "-1.7319746742639577,-6.64957455203383b0.52915602625774039;"
                + "8.0173694985068238,-4.8515260474996307b0.40211735018627581;"
                + "-6.5596480092777165,1.9133751894875317b-0.57131928385762465;"
                + "-1.7083237002176812,-6.6093479639894088b-0.10672865296096012;"
                + "9.0486438381712162,1.2898282386780853b0.41368166381198979;"
                + "0.54258588261091489,-4.6359238934870453b-0.015296733898714554;"
                + "5.4233036914017525,1.5047657357085331b0.11056452049434395");
            var tolerance = new Tolerance(point, vector);

            GeoPolygonArc2[] grown = Offset2.Offset(source, 0.3, OffsetOptions.Default, tolerance);

            Assert.Equal(3, grown.Length);
            Assert.Equal(123.139, NetArea(grown, source.IsClockwise), 0.2);
            Assert.Contains(grown, loop => Math.Abs(loop.Area - 10.65) < 0.05);
        }

        [Fact]
        public void AFiveVertexLoopShrunkByOneWithMiterCorners_LosesAnIslandTheMiterSpikesCover()
        {
            // Round has three loops, 25.73, 0.757 and 0.203; Miter with the default limit of 10 has the first two. The island of
            // 0.203, at (1.4..2.1, 4.5..5.2), lies wholly inside the wedges of the miters nearby. The wedges were built apart from
            // the code, from the loop alone: at every vertex and every crossing, in each quadrant of under a half turn that the
            // shrunk offset moves away from, the two moved pieces run on along their own circles or lines until they meet or are
            // cut square at the limit times the distance from the corner. Of the 622 sampled points that Round has and Miter has
            // not, all 622 are inside one. With a limit of 2 the wedges are short of the island, and it stays.
            GeoPolygonArc2 source = Parse(
                "-5.43643432456834,-9.6068745290892554;4.8974039474955777,6.9119030129685548b-0.10120083000566849;"
                + "5.6680744028035885,-9.8603683709448049b-0.6781697286191255;8.3174984801176457,8.0507222647083552;"
                + "-0.087358676869124352,5.6298973949765312b-0.47988065517501932");

            GeoPolygonArc2[] limitTen = Offset2.Offset(source, -1.0, OffsetOptions.Default, Thousandth);
            GeoPolygonArc2[] limitTwo = Offset2.Offset(source, -1.0, new OffsetOptions(OffsetJoin.Miter, 2.0, 0.0), Thousandth);
            GeoPolygonArc2[] round = Offset2.Offset(source, -1.0, Round, Thousandth);

            Assert.Equal(3, round.Length);
            Assert.Equal(3, limitTwo.Length);
            Assert.Equal(2, limitTen.Length);
        }

        [Theory]
        [InlineData(0.002)]
        [InlineData(-0.002)]
        public void ASevenVertexLoopCrossingItselfOffsetByTwoThousandthsWithMiterCorners_HasTheLoopsRoundCornersGive(double distance)
        {
            // One edge is an arc of bulge 3.8E-5 and another of 6.6E-9, and the miter of a corner beside the second is a loop two
            // thousandths across with an arc of nearly no bulge. Held to be a point of that miter, every point within 76 000 of it
            // was inside it, and the offset was cut away wherever it passed: grown by 0.002 it was nothing, and shrunk 2 loops
            // where round corners give 6. It is 3 loops (122.29, 31.47 and 1.59) and 6 (41.13, 28.95, 15.94, 1.57, 0.66, 0.62).
            GeoPolygonArc2 source = Parse(
                "-9.2215182256053758,0.1278016484006308;8.8341456553126427,2.6429815183593792b3.8354424777620189E-05;"
                + "-2.4534087220455554,9.2043106813003845;-7.2856366249200128,-1.9554226901174623;"
                + "5.3680509214140706,9.6759087311457428b-0.34993751274884566;4.7285632485191176,-4.3851013362338307;"
                + "-1.6933014577689125,-0.053740548926285214b-0.15940219250479815");

            AssertTheLoopsOfTheStraightPath(source, distance, OffsetOptions.Default, Thousandth, 0.01);
        }

        [Fact]
        public void ALoopCrossingItselfShrunkByTwoThousandthsWithChamferCornersAtATolerance1EMinus3_KeepsTheLoopOf15_80()
        {
            // The offset is twice the tolerance, and a corner cut across at the chamfer is 0.0019 long, nearer than twice the
            // tolerance to its ends: it was taken for a closed run of one edge and no part of the loop, the runs on either side
            // of it ended 0.0021 apart, and the loop of 15.80 (Round has it) was lost. The loops are 44.20 and 15.80.
            GeoPolygonArc2 source = Parse(
                "-0.84034154696406027,0.82337633279309586b-0.47218162267104796;6.1063106153655369,3.2418054403931862;"
                + "6.60612348309072,3.877451025823806b0.31367846681442035;"
                + "-9.0043917666209818,1.9824364744045013b-0.51586131836094951;"
                + "2.4988917598961358,5.4444927049076615b0.31809137604110482;-7.7671468620035551,-0.35234478784368584;"
                + "-1.4700713807111949,-1.8866710699566962b0.50367374997757075");

            AssertTheLoopsOfTheStraightPath(source, -0.002, new OffsetOptions(OffsetJoin.Chamfer), Thousandth, 0.01);
        }

        /// <summary>Asserts that the offset has as many loops as the straight path gives, and a net area within a share of its.</summary>
        private static void AssertTheCountAndNetAreaOfTheStraightPath(GeoPolygonArc2 source, double distance, OffsetOptions options, Tolerance tolerance, double share)
        {
            GeoPolygon2[] straight = Straight(source, distance, options, tolerance);
            GeoPolygonArc2[] got = Offset2.Offset(source, distance, options, tolerance);

            Assert.Equal(straight.Length, got.Length);
            Assert.Equal(NetArea(straight, source.IsClockwise), NetArea(got, source.IsClockwise), Math.Abs(NetArea(straight, source.IsClockwise)) * share);
        }

        [Theory]
        [InlineData(0.3)]
        [InlineData(1.0)]
        [InlineData(-0.3)]
        public void ANineVertexLoopCrossingItselfAtATolerance0_1_IsOffsetAsTheStraightPathOffsetsIt(double distance)
        {
            // At a tolerance of 0.1 two crossings of this loop lie within the tolerance of one another on an edge, and the piece
            // between them has its start and end in one place with a bulge: its length was asked and has no chord, so
            // InvalidOperationException ("Two points in the same place have no chord to bulge") was thrown. Refusing the loop
            // for the crossing path instead left the simple path to offset it wrong: grown by 0.3 or 1 it gave nothing, where
            // the straight path gives two loops (183.58 and -0.54) and one (249.89). The piece is left out: it has no length.
            GeoPolygonArc2 source = Parse(
                "-1.2568689376380622,5.5666811371066984b0.031704765619572584;"
                + "-0.041821436044676474,4.04788871949906b-0.62951099720295089;"
                + "-7.4081617674828326,-8.29850112940115b-0.14617949633215521;"
                + "-7.289127957676131,0.35947705170115363b0.26960220270305979;-8.5376774326607947,1.2725776207039967;"
                + "7.9174225581425333,-8.1959138709101431b0.53867916280342221;"
                + "9.3757060074134273,9.1795000150704311b-0.55459498597988621;-4.0089220525738423,-2.9602926284821205;"
                + "4.1744219577752162,5.3616452568032074b-0.66686665144137414");
            var tolerance = new Tolerance(0.1, 1E-3);

            AssertTheCountAndNetAreaOfTheStraightPath(source, distance, Round, tolerance, 0.04);
        }

        [Theory]
        [InlineData(0.3)]
        [InlineData(1.0)]
        [InlineData(-0.3)]
        public void AnEightVertexLoopCrossingItselfAtATolerance0_1_IsOffsetAsTheStraightPathOffsetsIt(double distance)
        {
            // The same piece without a length in another loop: grown by 0.3 or 1 it gave nothing, where the straight path gives
            // four loops (129.27, -12.63, -6.56 and -7.42, and 180.04, -2.69, -0.36 and -0.49), and shrunk by 0.3 one loop where
            // it gives eight.
            GeoPolygonArc2 source = Parse(
                "4.7448831958439577,6.7667151972496491b0.3838476119953429;"
                + "-8.60805866243693,-2.7264518536284807b-0.29110292167919821;"
                + "-8.3561255961452261,-2.0028728768242861b0.021721235812511841;9.1465243600059871,7.7629319661124292;"
                + "-7.681098327823495,8.38348689413792b0.12320270651169252;"
                + "9.8078426904081581,-4.1713574780949187b-0.40273777623788348;"
                + "3.0202256110591,9.6887342630367428b0.68402509800345856;"
                + "-6.1743859276987543,-1.8400852623582296b-0.2334402246090771");
            var tolerance = new Tolerance(0.1, 1E-3);

            AssertTheCountAndNetAreaOfTheStraightPath(source, distance, Round, tolerance, 0.04);
        }

        [Fact]
        public void ATenVertexLoopCrossingItselfShrunkByThreeTenthsWithMiterCorners_KeepsEightLoops()
        {
            // Eight loops, the smallest of 0.358, which was left out when a piece passed through the corner of two others.
            GeoPolygonArc2 source = Parse(
                "1.7585331628837313,-4.7913026412908462b-0.67054435069232443;5.7072912136592393,9.55114237943252b0.30783132668949276;"
                + "1.9039429965913044,-0.53537765077100019b0.57633845148437568;4.2020793977203219,-7.2475583279726834;"
                + "-9.9869928788333162,8.7113326036889731;-5.3077645019198609,1.0648977528628407;"
                + "1.4170144365248323,4.26760493510757b-0.18354515893549894;-3.7501127523137781,-5.3863034375879462;"
                + "1.4957133454716356,-2.4069054761933648b0.43504318424269717;-4.2462062622635655,0.922764507552035b0.64049270234093658");

            AssertTheLoopsOfTheStraightPath(source, -0.3, OffsetOptions.Default, Thousandth, 0.01);
        }

        [Theory]
        [InlineData("0,0;10,0;10,10b0.2;5,0;3,-6b-0.2", 0.5)]
        [InlineData("0,0;10,0;10,10b0.2;5,0;3,-6b-0.2", -0.5)]
        [InlineData("0,0;10,0;10,10b0.2;5,0.0005;3,-6b-0.2", 0.5)]
        [InlineData("0,0;10,0;10,10b0.2;5,0.0005;3,-6b-0.2", -0.5)]
        [InlineData("0,0;10,0;10,10b0.2;5,-0.0005;3,-6b-0.2", 0.5)]
        [InlineData("0,0;10,0;10,10b0.2;5,-0.0005;3,-6b-0.2", -0.5)]
        public void ALoopWhoseVertexLiesOnAnEdgeAndLeavesToTheOtherSideOfIt_IsReadAsCrossingThere(string text, double distance)
        {
            // The vertex (5, 0) lies on the edge from the origin to (10, 0), which the edge before it reaches from above and
            // the edge after it leaves below. That is a crossing, though the meeting is at the vertex: the loop is the
            // triangle with an arrowhead of 10.0 beside it, which grown by 0.5 is one loop of 74.484 and shrunk by 0.5 two
            // loops, 21.342 and 10.017. It was read as touching and offset as one loop wound round itself, 10.02 grown.
            GeoPolygonArc2 source = Parse(text);

            AssertTheLoopsOfTheStraightPath(source, distance, Round, Thousandth, 0.01);
        }

        [Theory]
        [InlineData("0,0b0.1;6,6b0.2;6,-6b0.1;0,0b0.1;-6,6b-0.2;-6,-6b0.1", 0.5)]
        [InlineData("0,0b0.1;6,6b0.2;6,-6b0.1;0,0b0.1;-6,6b-0.2;-6,-6b0.1", -0.5)]
        [InlineData("0,0.0005b0.1;6,6b0.2;6,-6b0.1;0,-0.0005b0.1;-6,6b-0.2;-6,-6b0.1", 0.5)]
        [InlineData("0,0.0005b0.1;6,6b0.2;6,-6b0.1;0,-0.0005b0.1;-6,6b-0.2;-6,-6b0.1", -0.5)]
        public void AFigureEightThatPassesThroughOneVertexTwice_IsReadAsCrossingThere(string text, double distance)
        {
            // Two bowed triangles joined at the origin, which the loop passes through twice (or by 0.001, once above it and
            // once below). Grown by 0.5 it is one loop of 82.726; shrunk, two of 10.293 and 18.222. It was grown to 10.29.
            GeoPolygonArc2 source = Parse(text);

            AssertTheLoopsOfTheStraightPath(source, distance, Round, Thousandth, 0.01);
        }

        [Theory]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        public void ALoopWhoseVertexOnlyTouchesAnEdgeFromOneSide_IsOffsetAsTheStraightPathOffsetsIt(double distance)
        {
            // The vertex (5, 0) touches the bottom edge from above, both of its edges on the one side: the loop is a square
            // pinched at its bottom, not a loop crossing itself.
            GeoPolygonArc2 source = Parse("0,0;10,0;10,10b0.1;5,0;0,10b0.1");

            AssertTheLoopsOfTheStraightPath(source, distance, Round, Thousandth, 0.01);
        }

        [Fact]
        public void ARectangleWithANotchOfAThousandthBelowItsBaseGrownByOneWithMiterCorners_IsOneLoop()
        {
            // Two arcs from the top corners meet below the base at (5, -0.001), 0.001 deep, and cross the base on either
            // side of it. The notch is as deep as the tolerance, and the sides of its corner, 0.0013 long, were cut off
            // so near their ends that the miter of the corner was read as a chord, which covered the offset beside it:
            // the answer was empty. It is the answer a tolerance ten times finer gives, 118.72.
            GeoPolygonArc2 source = Parse("0,0;10,0;10,10b0.1;5,-0.001b-0.1;0,10");
            var finer = new Tolerance(1E-4, 1E-6);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(source, 1.0, OffsetOptions.Default, Thousandth));
            GeoPolygonArc2 grownFiner = Assert.Single(Offset2.Offset(source, 1.0, OffsetOptions.Default, finer));

            Assert.Equal(grownFiner.Area, grown.Area, 1E-2);
            Assert.InRange(grown.Area, 118.0, 119.5);
        }

        [Fact]
        public void ARectangleWithANotchOfAThousandthBelowItsBaseShrunkByAHundredthOfAThousandth_IsOneLoop()
        {
            // The same notch shrunk by 0.0001 at a tolerance of 1E-4: one loop of 49.995, the straight path's.
            GeoPolygonArc2 source = Parse("0,0;10,0;10,10b0.1;5,-0.001b-0.1;0,10");
            var finer = new Tolerance(1E-4, 1E-6);

            AssertTheLoopsOfTheStraightPath(source, -0.0001, OffsetOptions.Default, finer, 1E-3);
        }
    }
}
