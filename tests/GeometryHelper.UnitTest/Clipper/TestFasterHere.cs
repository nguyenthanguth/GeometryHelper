using System.Diagnostics;
using GeometryHelper.Clipper;
using Xunit;

namespace GeometryHelper.UnitTest.Clipper
{
    /// <summary>
    /// Work of Clipper2 2.0.0 that grew with the square of the local minima, maxima or horizontals on one scanline, faster in
    /// this copy with the same paths, each held to a time several times what it takes now and well under what it took.
    /// </summary>
    public class TestFasterHere
    {
        [Fact]
        public void AZigzagOf100000Vertices_IsUnitedInUnderThreeSeconds()
        {
            // Its 50 000 minima lie on one scanline, and each walked the active edge list from its head to its place: six
            // seconds. The walk starts where one to its left stopped.
            var zigzag = new Path64();

            for (int i = 0; i <= 100000; i++)
            {
                zigzag.Add(new Point64(i * 10L, i % 2 == 0 ? 0 : 100));
            }

            zigzag.Add(new Point64(1000000, 200));
            zigzag.Add(new Point64(0, 200));

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(new Paths64 { zigzag }, FillRule.NonZero);
            watch.Stop();

            Assert.Single(union);
            Assert.Equal(Clipper2.Area(zigzag), Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void ACombOf25000Teeth_IsUnitedInUnderThreeSeconds()
        {
            // Each tooth's side ends at a horizontal, with no edge to pair with in the list, and the search for one walked to the
            // list's end: five of its seven seconds; the 50 000 horizontals on its two lines were each tried against every other.
            var comb = new Path64();
            long x = 0;

            for (int i = 0; i < 25000; i++)
            {
                comb.Add(new Point64(x, 0));
                comb.Add(new Point64(x + 10, 0));
                comb.Add(new Point64(x + 10, 100));
                comb.Add(new Point64(x + 20, 100));
                x += 20;
            }

            comb.Add(new Point64(x, 0));
            comb.Add(new Point64(x + 10, 0));
            comb.Add(new Point64(x + 10, 200));
            comb.Add(new Point64(0, 200));

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(new Paths64 { comb }, FillRule.NonZero);
            watch.Stop();

            Assert.Single(union);
            Assert.Equal(Clipper2.Area(comb), Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void ACombOf40000TeethPointingUpShrunkBy1_IsOffsetInUnderThreeSeconds()
        {
            // The union of its raw outline does the horizontals at the tips from the right, and each output path started there
            // looked left along the list of edges for the nearest that adds to another, past every tooth not yet done to the
            // list's head: 18 to 24 seconds. The walk now goes on past the stretch a long walk before it found none in.
            Path64 comb = Comb(40000, true);

            var watch = Stopwatch.StartNew();
            Paths64 offset = Clipper2.InflatePaths(new Paths64 { comb }, -1, JoinType.Miter, EndType.Polygon);
            watch.Stop();

            // 2 400 a tooth and 1 600 more, less the length round it, 416 a tooth and 808 more, and 4 back at the corners.
            Assert.Single(offset);
            Assert.Equal(1984.0 * 40000 + 796, Clipper2.Area(offset[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void ACombOf40000TeethPointingDownGrownBy1_IsOffsetInUnderThreeSeconds()
        {
            // Here the walk from each tooth's tip went past every tooth not yet done to the left side of the back, the nearest
            // edge that adds to an output path: 19 to 26 seconds.
            Path64 comb = Comb(40000, false);

            var watch = Stopwatch.StartNew();
            Paths64 offset = Clipper2.InflatePaths(new Paths64 { comb }, 1, JoinType.Miter, EndType.Polygon);
            watch.Stop();

            Assert.Single(offset);
            Assert.Equal(2816.0 * 40000 + 2412, Clipper2.Area(offset[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void TwentyThousandSquaresSideBySide_AreUnitedInUnderThreeSeconds()
        {
            // Every horizontal segment along the strip's top and bottom walked the whole run of points there to stretch over
            // it, only to find its left end claimed by the first: 17 seconds. The ends of a long run are filed now.
            var squares = new Paths64();

            for (int i = 0; i < 20000; i++)
            {
                squares.Add(new Path64 { new Point64(10 * i, 0), new Point64(10 * i + 10, 0), new Point64(10 * i + 10, 10), new Point64(10 * i, 10) });
            }

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(squares, FillRule.NonZero);
            watch.Stop();

            Assert.Single(union);
            Assert.Equal(20000.0 * 100, Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void TwentyRowsOf8000SquaresSharingTheirSides_AreUnitedInUnderThreeSeconds()
        {
            // The path along the shared sides doubles back on itself, and the cleaning walked round it once for each point it
            // took off the end of such a spike: 23 to 25 seconds on .NET Framework, against 0.8 now. It reads only the points
            // whose neighbours have changed now.
            var squares = new Paths64();

            for (int j = 0; j < 20; j++)
            {
                for (int i = 0; i < 8000; i++)
                {
                    squares.Add(new Path64 { new Point64(10 * i, 10 * j), new Point64(10 * i + 10, 10 * j), new Point64(10 * i + 10, 10 * j + 10), new Point64(10 * i, 10 * j + 10) });
                }
            }

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(squares, FillRule.NonZero);
            watch.Stop();

            Assert.Single(union);
            Assert.Equal(160000.0 * 100, Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        /// <summary>
        /// A comb along X: teeth 4 wide and 200 long, 4 apart, on a back 200 deep, pointing down from it, or up when mirrored.
        /// </summary>
        private static Path64 Comb(int teeth, bool up)
        {
            var comb = new Path64 { new Point64(0, 0) };
            long x = 0;

            for (int i = 0; i < teeth; i++)
            {
                comb.Add(new Point64(x + 4, 0));
                comb.Add(new Point64(x + 4, 200));
                comb.Add(new Point64(x + 8, 200));
                comb.Add(new Point64(x + 8, 0));
                x += 8;
            }

            comb.Add(new Point64(x + 4, 0));
            comb.Add(new Point64(x + 4, 400));
            comb.Add(new Point64(0, 400));

            if (up)
            {
                var mirrored = new Path64();

                for (int i = comb.Count - 1; i >= 0; i--)
                {
                    mirrored.Add(new Point64(comb[i].X, -comb[i].Y));
                }

                comb = mirrored;
            }

            return comb;
        }
    }
}
