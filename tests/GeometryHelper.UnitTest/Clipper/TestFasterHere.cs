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
    }
}
