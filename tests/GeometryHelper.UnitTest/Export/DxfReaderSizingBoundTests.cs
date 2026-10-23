using System.Linq;
using System.Text;
using GeometryHelper.Export;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfLines;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The bound on the blocks a reading works out for the sizes of its INSERTs, one for the whole reading.
    /// </summary>
    public class DxfReaderSizingBoundTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>
        /// Blocks X0 to X(levels), each two INSERTs of the next and a line, the last one INSERT of X0 and a line, so that every
        /// block meets the ring and no size of one is kept; the drawing has an INSERT of each of them, this many times over.
        /// </summary>
        private static string RingOfRoots(int levels, int times)
        {
            var blocks = new StringBuilder(G(0, "SECTION", 2, "BLOCKS"));

            for (int level = 0; level <= levels; level++)
            {
                blocks.Append(G(0, "BLOCK", 8, "0", 2, "X" + level, 70, 0, 10, "0", 20, "0", 30, "0"));

                for (int i = 0; i < (level < levels ? 2 : 1); i++)
                {
                    blocks.Append(G(0, "INSERT", 8, "0", 2, level < levels ? "X" + (level + 1) : "X0", 10, i, 20, "0"));
                }

                blocks.Append(G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "1") + G(0, "ENDBLK", 8, "0"));
            }

            blocks.Append(G(0, "ENDSEC"));
            var entities = new StringBuilder();

            for (int t = 0; t < times; t++)
            {
                for (int level = 0; level <= levels; level++)
                {
                    entities.Append(G(0, "INSERT", 8, "0", 2, "X" + level, 10, t, 20, level));
                }
            }

            return File("AC1015", 4, blocks.ToString(), entities.ToString());
        }

        [Fact]
        public void ARingOfManyRoots_WorksOutNoMoreBlocksThanTheBound_InAll()
        {
            // Each of the 26 blocks is the root of an INSERT, and each would work out the 1 000 blocks the bound allows, 26 000
            // in all, were the bound one for each INSERT; it is one for the reading, so 1 000 are worked out, and every INSERT is
            // refused, those of the blocks read on their own too.
            var options = new DxfReadOptions { Tolerance = Tol, KeepGroups = false, MostSizedBlocks = 1000 };
            DxfDocument once = DxfReader.Parse(RingOfRoots(25, 1), options);
            DxfDocument thrice = DxfReader.Parse(RingOfRoots(25, 3), options);

            Assert.Equal(1000, once.SizedBlocks);
            Assert.Equal(1000, thrice.SizedBlocks);

            foreach ((DxfDocument read, int inserts) in new[] { (once, 26), (thrice, 78) })
            {
                Assert.Empty(read.Entities);
                Assert.Equal(inserts, read.Skipped["INSERT"]);
                Assert.All(read.Blocks, b => Assert.Equal("LINE", Assert.Single(b.Entities).Type));
                Assert.Equal(
                    new[] { (inserts + 51) + " INSERTs were not placed, 51 of them inside the blocks read for Blocks: the reading had worked out the sizes of 1000 blocks, as many as it works out, and not yet what their blocks place, so they could not be held to DxfReadOptions.MaxPlacedEntities." },
                    read.Warnings.ToArray());
            }
        }

        [Fact]
        public void ARingWithinTheBound_IsReadAsBefore_EachRootWorkedOutOnce()
        {
            // Five blocks in a ring: an INSERT of the drawing places its root and the ring on from it, refusing the root inside
            // itself, a block twice as often as the one before it but after X4, which inserts X0 once: from X0 31 lines, from
            // X1 23, from X2 19, from X3 17 and from X4 16. Working each root out visits as many blocks as it places lines, 106
            // in all, and the second and third INSERTs of each root reuse its size.
            DxfDocument read = DxfReader.Parse(RingOfRoots(4, 3), new DxfReadOptions { Tolerance = Tol, KeepGroups = false, MostSizedBlocks = 1000 });

            Assert.Equal(106, read.SizedBlocks);
            Assert.Equal(3 * 106, read.Entities.Count);
            Assert.DoesNotContain(read.Warnings, w => w.Contains("worked out the sizes"));
        }
    }
}
