using System;
using System.Globalization;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Item 6, step S3, after review: the cases that guard the corner join against dropping a loop, and the fold of two
    /// straight edges told from the rounding of the coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A corner whose pieces cross within the point tolerance of the end of a long piece is cut at the crossing, both pieces,
    /// as every other corner is. Left uncut, a long piece stays whole beside a neighbour that the pruning never cuts when both
    /// are straight, the runs stay open, and the answer is empty. The quadrilateral, the step loop and the thin stars below
    /// pass at 9ee3f57 and are here to keep them so.
    /// </para>
    /// <para>
    /// Two straight edges that fold back are told from a fold on one line by the rounding of the coordinates, not by an
    /// absolute sine: a needle 6 long whose sine is 1E-12 is read by its turn at 0 and 1E4, and one of 1E-10 at 1E5.
    /// </para>
    /// </remarks>
    public class OffsetFaultItem6S3ReviewTests
    {
        private static readonly Tolerance Tenth = new Tolerance(0.1, 1E-3);

        private static readonly Tolerance Thousandth = new Tolerance(1E-3, 1E-5);

        private static GeoPolygonArc2 Star(string text)
        {
            string[] items = text.Split(';');
            var points = new GeoPoint2[items.Length];
            var bulges = new double[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                string[] parts = items[i].Split('b');
                string[] xy = parts[0].Split(',');

                points[i] = new GeoPoint2(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture));
                bulges[i] = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 0.0;
            }

            return new GeoPolygonArc2(points, bulges);
        }

        private const string ThinStarA = "4.9914053072337934,0.64330316341429328;1.2705312142212295,0.38821969377135651b-0.082467386421033823;-2.6889226289835451,0.79351057018711946b-0.15055426438364866;-2.2033677460878054,0.1662218662869169b0.077187611408153389;-4.6009475040647887,-0.71552755883165731b0.017743477489679804;-4.0790955852634445,-1.1787381361604048b-0.10964342433011318;4.1528041269978395,-1.2076007937587974b-0.027554768266880303;2.7105850410569379,-0.36583072556707757b-0.0015615659074678934";

        private const string ThinStarB = "9.3393880696903384,0.28832648144013096b0.131013906505431;5.2751017914983214,2.1284899352977313;-1.1341788359763858,1.7795123871773264b0.20843147717343247;-4.077770086072193,1.8699268254263435b-0.054954970630330474;-6.5700158478860287,0.47527479406774781b0.1321031599687893;-5.7407443362680066,-0.81733576223520021b0.1542813353726088;-2.0072442246645057,-0.49057637212400734b-0.24951408989704871;-1.41275691241883,-2.5493610172215337b-0.1890007562185641;5.1484945845647463,-2.2309806354888684;7.1121990056261142,-0.2411668140810671b-0.037621692888262531";

        private const string ThinStarC = "7.6895070795975826,0.45827813247951732b-0.10761501354054315;6.7845480678136729,2.2678779193600711b-0.10902611834883044;2.5395774082388387,2.9165227167000918b0.15670018848343759;-0.47386070755019855,0.66361539608032882b-0.093141294011446329;-5.6494226476034086,2.27134231187847b-0.045643919983712922;-9.6813802953688022,0.6148199148467538;-6.6492339963808318,-0.71611126409063819b0.14777325019136689;-5.1318302336430968,-2.4440899110671555b-0.1524761842575279;-0.2012578156145238,-0.75436956538630839b-0.21918815559204116;1.2961950187681097,-3.2055139295340798b0.17698321185399929;2.4372030822624837,-1.1616729572697457b-0.22485817502013322;4.27997256380015,-0.38686818043527083b0.0369081994457674";

        [Fact]
        public void AThinStarShrunkWithRoundJoinsAtATenth_KeepsItsLoop()
        {
            // Eight vertices, none more than 1.5 from the x axis, shrunk by 0.3 at a tolerance of 0.1: the loop of 3.428017
            // (the straight path's 3.533217). The corner at (4.153, -1.208) is crossed 0.0046 past the end of the piece after it.
            // Passes at 9ee3f57; it guards the corner join against dropping the loop.
            GeoPolygonArc2 shrunk = Assert.Single(Offset2.Offset(Star(ThinStarA), -0.3, new OffsetOptions(OffsetJoin.Round), Tenth));

            Assert.Equal(3.428017, shrunk.Area, 1E-5);
        }

        [Fact]
        public void AThinStarShrunkWithChamfersAtATenth_KeepsItsLoop()
        {
            // Passes at 9ee3f57; it guards the corner join against dropping the loop.
            GeoPolygonArc2 shrunk = Assert.Single(Offset2.Offset(Star(ThinStarB), -1.0, new OffsetOptions(OffsetJoin.Chamfer), Tenth));

            Assert.Equal(15.142789, shrunk.Area, 1E-5);
        }

        [Fact]
        public void AThinStarThatSplitsInTwoShrunkWithChamfersAtATenth_KeepsBothLoops()
        {
            // The straight path gives 6.004608 and 4.487536; the round answer 6.000348 and 4.503812. Passes at 9ee3f57; it
            // guards the corner join against dropping a loop.
            GeoPolygonArc2[] shrunk = Offset2.Offset(Star(ThinStarC), -1.0, new OffsetOptions(OffsetJoin.Chamfer), Tenth);

            Assert.Equal(2, shrunk.Length);
            Assert.Contains(shrunk, loop => Math.Abs(loop.Area - 4.499705) <= 1E-5);
            Assert.Contains(shrunk, loop => Math.Abs(loop.Area - 5.981558) <= 1E-5);
        }

        private static GeoPolygonArc2 Quadrilateral()
            => new GeoPolygonArc2(
                new[] { new GeoPoint2(1.5801, 1.4899), new GeoPoint2(-0.3855, 4.9363), new GeoPoint2(-0.5885, 2.4105), new GeoPoint2(-5.2119, 4.0805) },
                new double[4]);

        [Fact]
        public void AQuadrilateralOffsetWithMitersBy3Point7543_IsOneLoop()
        {
            // A corner of the quadrilateral is crossed between 1E-5 and the point tolerance past the end of the piece after
            // it, a long straight piece. The straight path gives 409.132481. Passes at 9ee3f57.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(Quadrilateral(), 3.7543, new OffsetOptions(OffsetJoin.Miter), Tolerance.Default));

            Assert.Equal(409.132481, grown.Area, 1E-5);
        }

        [Fact]
        public void AQuadrilateralOffsetWithChamfersBy3Point7543_IsOneLoop()
        {
            // The straight path gives 123.435407. Passes at 9ee3f57.
            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(Quadrilateral(), 3.7543, new OffsetOptions(OffsetJoin.Chamfer), Tolerance.Default));

            Assert.Equal(123.435407, grown.Area, 1E-5);
        }

        [Fact]
        public void AStepGrownByOneWithRoundJoinsAtATenth_IsNotCutShort()
        {
            // The loop (0, 0), (10, 0), (10, 5), (6, 5), (6, 5.91), (0, 5.91) grown by 1 at a tolerance of 0.1 with round
            // joins: 90.206991 (the straight path 90.183869 with its round joins cut within 1E-6). Passes at 9ee3f57.
            var step = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 5), new GeoPoint2(6, 5), new GeoPoint2(6, 5.91), new GeoPoint2(0, 5.91) },
                new double[6]);

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(step, 1.0, new OffsetOptions(OffsetJoin.Round), Tenth));

            Assert.Equal(90.206991, grown.Area, 1E-5);
        }

        [Theory]
        [InlineData(1E-12, 0.0)]
        [InlineData(1E-12, 1E4)]
        [InlineData(1E-10, 0.0)]
        [InlineData(1E-10, 1E4)]
        [InlineData(1E-10, 1E5)]
        [InlineData(1E-6, 0.0)]
        [InlineData(1E-6, 1E4)]
        [InlineData(1E-6, 1E5)]
        public void ANeedleWhoseFoldIsAboveTheRoundingOfTheCoordinates_IsOneLoopWhereverItStands(double sine, double shift)
        {
            // The needle is 6 long and 6 x sine wide at its foot. Whichever way the fold is read at a tolerance of 1E-3, the
            // needle dropped gives 112.282743 and the needle kept up to 115.845469; empty is not an answer. The fold of 6E-12
            // standing near 1E5 is not tested: it is under half the spacing of the coordinates there, and the edges are one line.
            double width = 6.0 * sine;
            double y = shift * 0.7;
            var points = new[]
            {
                new GeoPoint2(shift, y),
                new GeoPoint2(shift + 10.0, y),
                new GeoPoint2(shift + 10.0, y + 10.0),
                new GeoPoint2(shift + 4.0 + width, y + 10.0),
                new GeoPoint2(shift + 4.0, y + 16.0),
                new GeoPoint2(shift + 4.0, y + 10.0),
                new GeoPoint2(shift, y + 10.0),
            };

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(new GeoPolygonArc2(points, new double[points.Length]), 0.3, new OffsetOptions(OffsetJoin.Round), Thousandth));

            Assert.InRange(grown.Area, 112.282743 - 1E-2, 115.845469 + 1E-2);
        }
    }
}
