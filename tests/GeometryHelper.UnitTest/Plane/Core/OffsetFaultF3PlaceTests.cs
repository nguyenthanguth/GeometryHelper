using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Fault round F3, the second pass: where crossings of a loop that crosses itself are made one place, which tolerance a
    /// loop that cannot be cut is read within, and the retry of a loop cut at its crossings, read on top of the first pass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Places without a bound: crossings within the tolerance of each other, or linked through others, were made one place
    /// however far the chain ran, and every edge through it was cut at its first crossing, millimetres off where it crossed.
    /// </para>
    /// <para>
    /// A loop not cut: one whose crossings lie too close along an edge to be cut within the tolerance was offset as a simple
    /// loop, and then again as one within a hundredth of the tolerance, which left a loop where the region read even-odd is
    /// empty; and a loop cut at its crossings whose offset left a run open was never offset again.
    /// </para>
    /// <para>
    /// Every expected answer is the straight path's on the same loop, the one within 1E-3, or one read by the distance from
    /// the source; none was found by the code under test alone.
    /// </para>
    /// </remarks>
    public class OffsetFaultF3PlaceTests
    {
        private static readonly OffsetOptions Round = new OffsetOptions(OffsetJoin.Round);

        /// <summary>
        /// A loop from (-1, 0) along a zigzag of the given teeth, its corners 0.09 apart along x and the amplitude above and
        /// below y = 0, to the far end and back along y = 0, which every tooth crosses: a row of crossings 0.09 apart.
        /// </summary>
        private static GeoPolygonArc2 RowOfCrossings(int teeth, double amplitude)
        {
            var points = new List<GeoPoint2> { P(-1, 0) };

            for (int k = 0; k <= teeth; k++)
            {
                points.Add(P(0.09 * k, k % 2 == 0 ? -amplitude : amplitude));
            }

            points.Add(P(0.09 * teeth + 1, 0));
            return new GeoPolygonArc2(points, new double[points.Count]);
        }

        /// <summary>The star polygon {37/18} of radius 16.670 about the origin, its first corner turned 0.1 from the x axis.</summary>
        private static GeoPolygonArc2 StarOf37By18()
        {
            var points = new GeoPoint2[37];

            for (int k = 0; k < 37; k++)
            {
                double angle = 0.1 + 2.0 * Math.PI * k * 18 / 37;
                points[k] = P(16.670 * Math.Cos(angle), 16.670 * Math.Sin(angle));
            }

            return new GeoPolygonArc2(points, new double[37]);
        }

        /// <summary>
        /// Diameters of a circle of radius 10, the first 0.01 from the x axis and each the next a two-hundredth of a half
        /// turn round, joined end to start: 19 900 crossings at the centre.
        /// </summary>
        private static GeoPolygonArc2 TwoHundredDiameters()
        {
            var points = new List<GeoPoint2>();

            for (int k = 0; k < 200; k++)
            {
                double angle = Math.PI * k / 200 + 0.01;
                points.Add(P(10 * Math.Cos(angle), 10 * Math.Sin(angle)));
                points.Add(P(-10 * Math.Cos(angle), -10 * Math.Sin(angle)));
            }

            return new GeoPolygonArc2(points, new double[points.Count]);
        }

        /// <summary>
        /// A loop of eight corners 2.62 by 2.37 with its arcs, that crosses itself at three places, cut at all of
        /// them at a tolerance of 0.1.
        /// </summary>
        private static GeoPolygonArc2 EightCornersCutAtATenth() => new GeoPolygonArc2(
            new[]
            {
                P(2.263565202094484, 0.9583770155264077), P(3.272434515967713, 0.1309254495032398), P(2.7391802437039647, 2.4818060732034724),
                P(2.9312806959453113, 2.469742154380907), P(2.4127390972009852, 2.368741813347669), P(0.7980997500734023, 0.8605669269052311),
                P(3.4139995416358255, 0.2863762706557456), P(2.4198750326563485, 2.381466076055706),
            },
            new[] { -0.08471363269943447, -0.14792852906879436, -0.2812996923836412, 0.15382748034495275, 0.0, -0.2148262825397897, 0.0, 0.1731052094479581 });

        /// <summary>
        /// A loop of eight corners 1.91 by 2.01 with its arcs, that crosses itself once, within 0.1 of a corner: at
        /// a tolerance of 0.1 the pass through the corner reads as a touch, within 1E-3 as a crossing.
        /// </summary>
        private static GeoPolygonArc2 EightCornersCrossingBesideACorner() => new GeoPolygonArc2(
            new[]
            {
                P(0.8784705235958227, 1.4952340365806662), P(0.2576758179238767, 1.7114370530650422), P(1.2657891717864778, 2.076496060588267),
                P(1.7173881125324741, 1.1935224584634672), P(2.154960179744004, 0.06472456484643802), P(0.2471181617459247, 1.375377424719411),
                P(1.6446015045265272, 0.1838445495786179), P(2.071384343226514, 0.10783005848854982),
            },
            new[] { -0.24806404148603978, 0.0, -0.03758249736278435, -0.08567629427913401, -0.07503954906716923, 0.16637013371399145, -0.03587120330700244, 0.0 });

        /// <summary>The signed areas of an answer, largest first: positive for a loop wound as the source, negative for a hole.</summary>
        private static double[] SignedAreas(GeoPolygonArc2[] loops, GeoPolygonArc2 source)
            => loops.Select(l => l.IsClockwise == source.IsClockwise ? l.Area : -l.Area).OrderByDescending(a => a).ToArray();

        /// <summary>How far every point of an answer of round joins stands off the distance from the source, at worst.</summary>
        private static double WorstOff(GeoPolygonArc2[] loops, GeoPolygonArc2 source, double distance)
            => loops.Max(l => WorstOffDistance(PiecesOf(l), PiecesOf(source), distance));

        [Fact]
        public void ARowOfFortyCrossingsATenthApartShrunkAtATenth_IsTwoLoopsStandingTheDistanceOff()
        {
            // The forty teeth cross y = 0 0.09 apart, and the even-odd region is the two end triangles and the slivers between
            // the teeth, which shrunk by 0.3 leave the triangles only: 0.4335 each on the straight path. Within a tenth the
            // crossings are twenty places of two, each crossing within 0.09 of its place. Made one place 3.5 long, the edges
            // were cut up to 3.5 off where they crossed and one loop of 8.89 came back, 5.89 of it within 0.009 of the source.
            GeoPolygonArc2 loop = RowOfCrossings(40, 6.0);

            GeoPolygonArc2[] shrunk = Offset2.Offset(loop, -0.3, Round, Tenth);

            Assert.Equal(2, shrunk.Length);
            Assert.True(WorstOff(shrunk, loop, 0.3) <= Tenth.EqualPoint);
            Assert.InRange(SignedAreas(shrunk, loop)[0], 0.4335 - 0.01, 0.5939 + 1E-4);
        }

        [Fact]
        public void ARowOfTwentyTeethThreeHighShrunkAtATenth_IsTwoLoopsStandingTheDistanceOff()
        {
            // The same with twenty teeth 3 high: 0.1509 each on the straight path, 0.2167 and 0.1509 within a tenth. Made one
            // place, the crossings gave a loop of 1.7950.
            GeoPolygonArc2 loop = RowOfCrossings(20, 3.0);

            GeoPolygonArc2[] shrunk = Offset2.Offset(loop, -0.3, Round, Tenth);

            Assert.Equal(2, shrunk.Length);
            Assert.True(WorstOff(shrunk, loop, 0.3) <= Tenth.EqualPoint);
            Assert.InRange(SignedAreas(shrunk, loop)[0], 0.1509 - 0.01, 0.2167 + 1E-4);
        }

        [Fact]
        public void TwoHundredDiametersOffsetWithRoundJoins_TakeSecondsAndLittleMemory()
        {
            // The 19 900 crossings at the centre are one place. Every pair of them within the tolerance was kept, 2E8 pairs,
            // and the offset ran out of memory.
            GeoPolygonArc2 loop = TwoHundredDiameters();
            AppDomain.MonitoringIsEnabled = true;
            long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;

            var watch = Stopwatch.StartNew();
            Offset2.Offset(loop, 0.3, Round, Thousandth);
            watch.Stop();

            long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), "offsetting 200 diameters took " + watch.ElapsedMilliseconds + " ms");
            Assert.True(allocated < 200L * 1024 * 1024, "offsetting 200 diameters allocated " + allocated / (1024 * 1024) + " MB");
        }

        [Fact]
        public void AStarOf37By18ShrunkAtATenth_IsThirtySevenLoopsAsWithinAThousandth()
        {
            // The star's 629 crossings lie too close along its edges to be cut within a tenth; cut within 1E-3, shrunk by 0.3,
            // it is 37 loops of 0.8242 each, as the straight path and the offset within 1E-3 give it. Offset as a simple
            // loop within 1E-3 it was one loop of 0.5229 at its centre, where the region read even-odd is empty. Within a tenth,
            // before, it was none.
            GeoPolygonArc2 star = StarOf37By18();
            double point = Offset2.Offset(new GeoPolygon2(star.Vertices.ToList()), -0.3, Round, Thousandth).Max(l => l.Area);

            GeoPolygonArc2[] shrunk = Offset2.Offset(star, -0.3, Round, Tenth);

            Assert.Equal(37, shrunk.Length);
            Assert.All(shrunk, l => Assert.Equal(point, l.Area, 0.01));
        }

        [Fact]
        public void ALoopCutAtItsCrossingsGrownAtATenth_IsOffsetAgainWithinAHundredthOfIt()
        {
            // Cut at its three crossings within a tenth, the loop grown by 0.3 left a run open, and the answer was nothing.
            // Cut and offset again within 1E-3 it is a loop of 4.8860 with a hole of 0.0024, finer than the tolerance, as the
            // answer within 1E-3 and the straight path within 1E-3 give it.
            GeoPolygonArc2 loop = EightCornersCutAtATenth();

            GeoPolygonArc2[] grown = Offset2.Offset(loop, 0.3, Round, Tenth);

            double[] areas = SignedAreas(grown, loop);
            Assert.Equal(2, areas.Length);
            Assert.Equal(4.886, areas[0], 1E-3);
            Assert.Equal(-0.0024, areas[1], 1E-4);
            Assert.True(WorstOff(grown, loop, 0.3) <= Tenth.EqualPoint);
        }

        [Fact]
        public void ALoopThatCrossesItselfBesideACornerGrownAtATenth_IsReadEvenOddWithinAHundredthOfIt()
        {
            // At a tenth the crossing beside the corner reads as a touch, the loop is offset as a simple loop, and its offset
            // left runs open: nothing, then as now within 1E-3 as a simple loop again. Cut at the crossing within 1E-3 it is one
            // loop of 4.1023, as the offset and the straight path within 1E-3 give it.
            GeoPolygonArc2 loop = EightCornersCrossingBesideACorner();

            GeoPolygonArc2 grown = Assert.Single(Offset2.Offset(loop, 0.3, Round, Tenth));

            Assert.Equal(4.1023, grown.Area, 1E-3);
            Assert.True(WorstOff(new[] { grown }, loop, 0.3) <= Tenth.EqualPoint);
        }

        [Fact]
        public void ASliverTurnedBackwardsRunOnToAPointBesideIt_RunsBackTheShortWay()
        {
            // The whole moved edge is a quarter circle of radius 10 counter-clockwise from (10, 0) to (0, 10); the piece is a
            // sliver 0.05 long turned backwards, from 0.05 past its end back to the end; the point is 0.02 past the end. Run
            // on from the sliver's start the way the circle runs, the piece swept 359.8 degrees, 62.8 long; it runs back 0.03.
            GeoPoint2 At(double along) => P(10 * Math.Cos(Math.PI / 2 + along / 10), 10 * Math.Sin(Math.PI / 2 + along / 10));
            var whole = new GeoEdge2(P(10, 0), P(0, 10), Math.Tan(Math.PI / 8));
            var sliver = new GeoEdge2(At(0.05), At(0.0), -Math.Tan(0.005 / 4));

            GeoEdge2 run = ArcOffset2.StretchAlong(sliver, whole, At(0.02), true);

            Assert.Equal(0.0, run.StartPoint.DistanceTo(At(0.05)), 1E-12);
            Assert.Equal(0.0, run.EndPoint.DistanceTo(At(0.02)), 1E-12);
            Assert.Equal(0.03, run.Length, 1E-6);
        }
    }
}
