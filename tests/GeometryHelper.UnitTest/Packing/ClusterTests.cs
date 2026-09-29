using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Packing;
using GeometryHelper.Packing.Algorithms;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// <see cref="ClusterBuilder"/>: the boxes of a group as blocks, compact or as they stand, one block per sheet.
    /// </summary>
    public class ClusterTests
    {
        private static List<Cluster> Build(Footprint[] boxes, GroupLayout layout, double spacing = 0.0, double usableWidth = 1000.0, double usableHeight = 1000.0)
            => ClusterBuilder.Build(0, boxes, Enumerable.Range(0, boxes.Length).ToArray(), layout, spacing, usableWidth, usableHeight, 1e-9);

        private static Footprint[] Squares(int count, double side)
            => Enumerable.Range(0, count).Select(i => new Footprint(i * 1000.0, -i * 500.0, side, side)).ToArray();

        // Whether no two boxes of a block come closer than the gap.
        private static void AssertApart(Cluster cluster, Footprint[] boxes, double gap)
        {
            for (int i = 0; i < cluster.Members.Count; i++)
            {
                for (int j = i + 1; j < cluster.Members.Count; j++)
                {
                    Footprint a = boxes[cluster.Members[i]], b = boxes[cluster.Members[j]];
                    (double ax, double ay) = cluster.Offsets[i];
                    (double bx, double by) = cluster.Offsets[j];
                    bool apart = ax + a.Width + gap <= bx + 1e-9 || bx + b.Width + gap <= ax + 1e-9
                        || ay + a.Height + gap <= by + 1e-9 || by + b.Height + gap <= ay + 1e-9;
                    Assert.True(apart, $"boxes {i} and {j}");
                }
            }
        }

        /// <summary>
        /// Four squares make a square block, two by two, not a row or a column of the same area, the first at the upper left.
        /// </summary>
        [Fact]
        public void FourSquares_MakeASquareBlock()
        {
            List<Cluster> clusters = Build(Squares(4, 10.0), GroupLayout.Compact);

            Cluster block = Assert.Single(clusters);
            Assert.Equal(20.0, block.Width);
            Assert.Equal(20.0, block.Height);
            Assert.False(block.Kept);
            Assert.Equal(new[] { (0.0, 10.0), (10.0, 10.0), (0.0, 0.0), (10.0, 0.0) }, block.Offsets);
        }

        [Fact]
        public void TheBoxesOfABlock_StandTheSpacingApart()
        {
            Footprint[] boxes = Squares(4, 10.0);
            Cluster block = Assert.Single(Build(boxes, GroupLayout.Compact, spacing: 5.0));

            Assert.Equal(25.0, block.Width);
            Assert.Equal(25.0, block.Height);
            AssertApart(block, boxes, 5.0);
        }

        /// <summary>
        /// A view and its sections: the view at the upper left of the block, the sections round it, the block no larger
        /// than the row they would make.
        /// </summary>
        [Fact]
        public void AViewAndItsSections_MakeACompactBlock()
        {
            Footprint[] boxes = { new Footprint(0, 0, 100, 60), new Footprint(0, 0, 30, 15), new Footprint(0, 0, 30, 15), new Footprint(0, 0, 30, 15) };
            Cluster block = Assert.Single(Build(boxes, GroupLayout.Compact, spacing: 2.0));

            AssertApart(block, boxes, 2.0);
            Assert.Equal((0.0, block.Height - 60.0), block.Offsets[0]);
            Assert.True(block.Area <= (100.0 + 3 * 32.0) * 60.0);
            Assert.True(block.Width < 100.0 + 3 * 32.0);
        }

        /// <summary>
        /// Kept, the boxes stand to one another as given, overlapping or not, and the block is the box round them.
        /// </summary>
        [Fact]
        public void KeptBoxes_StandAsGiven()
        {
            Footprint[] boxes = { new Footprint(100, 100, 20, 10), new Footprint(130, 100, 20, 10), new Footprint(105, 105, 45, 20) };
            Cluster block = Assert.Single(Build(boxes, GroupLayout.Keep));

            Assert.True(block.Kept);
            Assert.Equal(50.0, block.Width);
            Assert.Equal(25.0, block.Height);
            Assert.Equal(new[] { (0.0, 0.0), (30.0, 0.0), (5.0, 5.0) }, block.Offsets);
        }

        [Fact]
        public void KeptBoxesTooWideForASheet_AreLaidOutCompactly()
        {
            Footprint[] boxes = { new Footprint(0, 0, 60, 10), new Footprint(500, 0, 60, 10) };
            List<Cluster> clusters = Build(boxes, GroupLayout.Keep, usableWidth: 200.0, usableHeight: 100.0);

            Cluster block = Assert.Single(clusters);
            Assert.False(block.Kept);
            Assert.True(block.Width <= 200.0);
        }

        /// <summary>
        /// A group too large for one sheet is split in its order: as many boxes as go onto a sheet, then the next.
        /// </summary>
        [Fact]
        public void AGroupTooLargeForASheet_IsSplitInItsOrder()
        {
            Footprint[] boxes = Squares(10, 40.0);
            List<Cluster> clusters = Build(boxes, GroupLayout.Compact, usableWidth: 100.0, usableHeight: 100.0);

            Assert.Equal(new[] { 4, 4, 2 }, clusters.Select(c => c.Members.Count));
            Assert.Equal(Enumerable.Range(0, 10), clusters.SelectMany(c => c.Members));
            Assert.All(clusters, c => Assert.True(c.Width <= 100.0 && c.Height <= 100.0));
        }

        [Fact]
        public void AGroupOfManyBoxes_HoldsEachOnceInItsOrder()
        {
            var random = new Random(3);
            Footprint[] boxes = Enumerable.Range(0, 300).Select(i => new Footprint(0, 0, 5 + random.Next(40), 5 + random.Next(30))).ToArray();
            List<Cluster> clusters = Build(boxes, GroupLayout.Compact, spacing: 1.0, usableWidth: 400.0, usableHeight: 300.0);

            Assert.Equal(Enumerable.Range(0, 300), clusters.SelectMany(c => c.Members));
            foreach (Cluster block in clusters)
            {
                Assert.True(block.Width <= 400.0 + 1e-9 && block.Height <= 300.0 + 1e-9);
                AssertApart(block, boxes, 1.0);
            }
        }
    }
}
