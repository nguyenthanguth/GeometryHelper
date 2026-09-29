using System;
using System.Collections.Generic;
using GeometryHelper.Packing.Algorithms;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// <see cref="MaxRectsBin"/>: boxes into one bin, highest first, then furthest left, never overlapping.
    /// </summary>
    public class MaxRectsBinTests
    {
        [Fact]
        public void TheFirstBox_GoesToTheUpperLeftCorner()
        {
            var bin = new MaxRectsBin(100.0, 50.0, 0.0);

            Assert.True(bin.TryPlace(30.0, 20.0, out double x, out double y));
            Assert.Equal(0.0, x);
            Assert.Equal(30.0, y);
        }

        /// <summary>
        /// A sheet of the A series halves into two of the next size down, so four A4 fill an A2 exactly, and two A3 do,
        /// row by row from the upper left.
        /// </summary>
        [Fact]
        public void BoxesThatAddUpToTheBin_FillItExactly()
        {
            var bin = new MaxRectsBin(594.0, 420.0, 0.0);
            var corners = new List<(double X, double Y)>();
            for (int i = 0; i < 4; i++)
            {
                Assert.True(bin.TryPlace(297.0, 210.0, out double x, out double y));
                corners.Add((x, y));
            }

            Assert.Equal(new[] { (0.0, 210.0), (297.0, 210.0), (0.0, 0.0), (297.0, 0.0) }, corners);
            Assert.False(bin.TryPlace(1.0, 1.0, out _, out _));
            Assert.Equal(0, bin.FreeCount);

            var halves = new MaxRectsBin(594.0, 420.0, 0.0);
            Assert.True(halves.TryPlace(297.0, 420.0, out double x0, out _));
            Assert.True(halves.TryPlace(297.0, 420.0, out double x1, out _));
            Assert.Equal(0.0, x0);
            Assert.Equal(297.0, x1);
            Assert.False(halves.TryPlace(1.0, 1.0, out _, out _));
        }

        /// <summary>
        /// A box too short for the row goes beside the tall one, and the next one into the space beneath it.
        /// </summary>
        [Fact]
        public void AShortBox_LeavesTheSpaceBeneathItForTheNext()
        {
            var bin = new MaxRectsBin(100.0, 100.0, 0.0);

            Assert.True(bin.TryPlace(60.0, 100.0, out _, out _));
            Assert.True(bin.TryPlace(40.0, 30.0, out double x1, out double y1));
            Assert.True(bin.TryPlace(40.0, 70.0, out double x2, out double y2));

            Assert.Equal((60.0, 70.0), (x1, y1));
            Assert.Equal((60.0, 0.0), (x2, y2));
            Assert.False(bin.TryPlace(1.0, 1.0, out _, out _));
        }

        /// <summary>
        /// The highest space wins, though a lower one is kept before it: after a box at the upper left and one beside
        /// it, the space under the second reaches higher than the space under both, and the third box goes there.
        /// </summary>
        [Fact]
        public void AHigherSpace_IsTakenBeforeALowerOne()
        {
            var bin = new MaxRectsBin(100.0, 100.0, 0.0);

            Assert.True(bin.TryPlace(40.0, 40.0, out double x0, out double y0));
            Assert.True(bin.TryPlace(60.0, 30.0, out double x1, out double y1));
            Assert.True(bin.TryPlace(50.0, 20.0, out double x2, out double y2));

            Assert.Equal((0.0, 60.0), (x0, y0));
            Assert.Equal((40.0, 70.0), (x1, y1));
            Assert.Equal((40.0, 50.0), (x2, y2));
        }

        [Fact]
        public void ABoxLargerThanTheBin_DoesNotGoIn_AndLeavesItAsItWas()
        {
            var bin = new MaxRectsBin(100.0, 50.0, 0.0);

            Assert.False(bin.TryPlace(100.5, 10.0, out _, out _));
            Assert.False(bin.TryPlace(10.0, 50.5, out _, out _));
            Assert.Equal(1, bin.FreeCount);
            Assert.True(bin.TryPlace(100.0, 50.0, out _, out _));
        }

        /// <summary>
        /// Sizes that ought to add up to the bin do, within the slack; more than the slack over does not go in.
        /// </summary>
        [Fact]
        public void TheSlack_LetsSizesThatAddUpInFloatingPointGoIn()
        {
            // 0.3 - 0.1 is a hair under 0.2 in floating point: with no slack the second box does not go in.
            var exact = new MaxRectsBin(0.3, 1.0, 0.0);
            Assert.True(exact.TryPlace(0.1, 1.0, out _, out _));
            Assert.False(exact.TryPlace(0.2, 1.0, out _, out _));

            var tight = new MaxRectsBin(0.3, 1.0, 1e-9);
            Assert.True(tight.TryPlace(0.1, 1.0, out _, out _));
            Assert.True(tight.TryPlace(0.2, 1.0, out _, out _));

            var loose = new MaxRectsBin(100.0, 50.0, 1e-4);
            Assert.True(loose.TryPlace(100.00005, 50.0, out _, out _));

            Assert.False(new MaxRectsBin(100.0, 50.0, 1e-4).TryPlace(100.001, 50.0, out _, out _));
        }

        /// <summary>
        /// Boxes of every size, one after another until the bin is full: each inside the bin and clear of every other.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void ManyBoxes_NeverOverlap_AndStayInside(int seed)
        {
            var random = new Random(seed);
            var bin = new MaxRectsBin(1000.0, 700.0, 0.0);
            var placed = new List<(double X, double Y, double W, double H)>();
            for (int i = 0; i < 400; i++)
            {
                double w = 5.0 + random.Next(120), h = 5.0 + random.Next(90);
                if (bin.TryPlace(w, h, out double x, out double y))
                {
                    placed.Add((x, y, w, h));
                }
            }

            Assert.True(placed.Count > 50);
            double area = 0.0;
            for (int i = 0; i < placed.Count; i++)
            {
                var a = placed[i];
                area += a.W * a.H;
                Assert.True(a.X >= 0.0 && a.Y >= 0.0 && a.X + a.W <= 1000.0 && a.Y + a.H <= 700.0, $"box {i} at {a}");
                for (int j = i + 1; j < placed.Count; j++)
                {
                    var b = placed[j];
                    bool apart = a.X + a.W <= b.X || b.X + b.W <= a.X || a.Y + a.H <= b.Y || b.Y + b.H <= a.Y;
                    Assert.True(apart, $"boxes {i} {a} and {j} {b}");
                }
            }

            // Boxes of whole sizes up to a fifth of the bin fill most of it.
            Assert.True(area > 0.8 * 1000.0 * 700.0, $"filled {area / 7000.0:0.#}%");
        }
    }
}
