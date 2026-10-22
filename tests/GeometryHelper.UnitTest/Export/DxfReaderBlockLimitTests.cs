using System;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfText;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The bound on what INSERTs place from blocks, which multiply through nesting, and the thickness a placement turns over.
    /// </summary>
    public class DxfReaderBlockLimitTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>
        /// Blocks B0 to B(levels): B0 a line, each other one fan INSERTs of the one below, so B(levels) places fan^levels lines;
        /// the drawing has these INSERTs, by block number.
        /// </summary>
        private static string Bomb(int levels, int fan, params int[] inserted)
        {
            var blocks = new StringBuilder(G(0, "SECTION", 2, "BLOCKS", 0, "BLOCK", 8, "0", 2, "B0", 70, 0, 10, "0", 20, "0", 30, "0", 0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "1", 0, "ENDBLK", 8, "0"));

            for (int level = 1; level <= levels; level++)
            {
                blocks.Append(G(0, "BLOCK", 8, "0", 2, "B" + level, 70, 0, 10, "0", 20, "0", 30, "0"));

                for (int i = 0; i < fan; i++)
                {
                    blocks.Append(G(0, "INSERT", 8, "0", 2, "B" + (level - 1), 10, i * 3, 20, level, 30, "0"));
                }

                blocks.Append(G(0, "ENDBLK", 8, "0"));
            }

            blocks.Append(G(0, "ENDSEC"));
            string entities = string.Concat(inserted.Select(b => G(0, "INSERT", 8, "0", 2, "B" + b, 10, "0", 20, "0")));
            return File("AC1015", 4, blocks.ToString(), entities);
        }

        [Fact]
        public void InsertsThatWouldPlaceMoreThanTheLimit_AreLeftOutWhole_AndTheRestPlaced_InTheDrawingAndTheBlocks()
        {
            // B4 places 10 000 lines, past the limit of 5 000; B1 places 10. Of the blocks read on their own, B1 to B3 place
            // 10, 100 and 1 000, and B4 three of its INSERTs of B3, the 3 880 left holding three of 1 000 and not a fourth.
            DxfDocument read = DxfReader.Parse(Bomb(4, 10, 4, 1), new DxfReadOptions { MaxPlacedEntities = 5000, PlanAs2D = true, Tolerance = Tol });

            Assert.Equal(10, read.Entities.Count);
            Assert.All(read.Entities, e => Assert.Equal(new[] { "B1", "B0" }, e.BlockPath.ToArray()));
            Assert.Equal(1, read.Skipped["INSERT"]);
            Assert.Equal(new[] { 1, 10, 100, 1000, 3000 }, read.Blocks.Select(b => b.Entities.Count).ToArray());
            Assert.Contains("8 INSERTs were not placed, 7 of them inside the blocks read for Blocks: the entities placed from blocks would have passed DxfReadOptions.MaxPlacedEntities, 5000, had they placed the 17000 more they would have taken.", read.Warnings);
        }

        [Fact]
        public void ABombOfNestedBlocks_IsBoundedByTheLimit_FastAndWithoutPlacingItsMillions()
        {
            // Five levels of 100: the top places 10 000 000 000 lines, worked out from the blocks and never placed; under the
            // limit of 1 000, B1 (100) is placed, and of the blocks on their own B1 and eight INSERTs of B1 in B2.
            DxfDocument read = DxfReader.Parse(Bomb(5, 100, 5, 1), new DxfReadOptions { MaxPlacedEntities = 1000, Tolerance = Tol });

            Assert.Equal(100, read.Entities.Count);
            Assert.Equal(1, read.Skipped["INSERT"]);
            Assert.Equal(new[] { 1, 100, 800, 0, 0, 0 }, read.Blocks.Select(b => b.Entities.Count).ToArray());
            Assert.True(read.Entities.Count + read.Blocks.Sum(b => b.Entities.Count) <= 1000 + 1 + 100);
        }

        [Fact]
        public void AMInsertOfABlockOfInserts_PastTheLimit_IsNotPlaced_NorItsCellsMade()
        {
            string text = WithBlock(
                G(0, "BLOCK", 8, "0", 2, "B0", 70, 0, 10, "0", 20, "0", 30, "0", 0, "POINT", 8, "0", 10, "0", 20, "0", 0, "ENDBLK", 8, "0")
                + G(0, "BLOCK", 8, "0", 2, "TEN", 70, 0, 10, "0", 20, "0", 30, "0") + string.Concat(Enumerable.Range(0, 10).Select(i => G(0, "INSERT", 8, "0", 2, "B0", 10, i, 20, "0"))) + G(0, "ENDBLK", 8, "0"),
                G(0, "INSERT", 8, "0", 100, "AcDbMInsertBlock", 2, "TEN", 10, "0", 20, "0", 70, 1000, 71, 1000, 44, "1", 45, "1")
                + G(0, "INSERT", 8, "0", 2, "TEN", 10, "0", 20, "0"));

            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { MaxPlacedEntities = 100000, Tolerance = Tol });

            Assert.Equal(10, read.Entities.Count);
            Assert.Equal(1, read.Skipped["INSERT"]);
            Assert.Contains(read.Warnings, w => w.StartsWith("1 INSERT was not placed: ", StringComparison.Ordinal) && w.Contains("100000, had they placed the 10000000 more"));
        }

        [Fact]
        public void TheLimit_Is2000000ByDefault_AndRefusesNoughtOrLess()
        {
            Assert.Equal(2000000, new DxfReadOptions().MaxPlacedEntities);
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfReadOptions { MaxPlacedEntities = 0 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfReadOptions { MaxPlacedEntities = -5 });
        }

        [Fact]
        public void TheThicknessOfAnEntity_PlacedByAMirrorAlongZ_IsTurnedOver_AndKeepsItsSignOtherwise()
        {
            string text = WithBlock(
                G(0, "BLOCK", 8, "0", 2, "T", 70, 0, 10, "0", 20, "0", 30, "0")
                + G(0, "LINE", 8, "0", 39, "2", 10, "0", 20, "0", 11, "1", 21, "0")
                + G(0, "CIRCLE", 8, "0", 39, "-4", 10, "0", 20, "0", 40, "1")
                + G(0, "ENDBLK", 8, "0"),
                G(0, "INSERT", 8, "0", 2, "T", 10, "0", 20, "0", 43, "-3")
                + G(0, "INSERT", 8, "0", 2, "T", 10, "0", 20, "0", 41, "-1", 42, "1", 43, "3")
                + G(0, "INSERT", 8, "0", 2, "T", 10, "0", 20, "0", 210, "0", 220, "0", 230, "-1"));

            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });

            // Along -z the thickness of 2 stands 6 down; a mirror in x leaves it up; the extrusion turned down turns it over.
            Assert.Equal(new[] { -6.0, 12.0, 6.0, -12.0, -2.0, 4.0 }, read.Entities.Select(e => e.Thickness).ToArray());
        }

        /// <summary>
        /// Blocks that insert each other: A an INSERT of B and a MINSERT of C, B an INSERT of A and a line, C ten lines; the
        /// drawing an INSERT of A and then one of B. Placed, A places B's line and the MINSERT, B places A, each refusing the
        /// other inside itself.
        /// </summary>
        private static string Cycle(int columns, int rows) => WithBlock(
            G(0, "BLOCK", 8, "0", 2, "A", 70, 0, 10, "0", 20, "0", 30, "0")
            + G(0, "INSERT", 8, "0", 2, "B", 10, "0", 20, "0")
            + G(0, "INSERT", 8, "0", 100, "AcDbMInsertBlock", 2, "C", 10, "0", 20, "0", 70, columns, 71, rows, 44, "1", 45, "1")
            + G(0, "ENDBLK", 8, "0")
            + G(0, "BLOCK", 8, "0", 2, "B", 70, 0, 10, "0", 20, "0", 30, "0")
            + G(0, "INSERT", 8, "0", 2, "A", 10, "5", 20, "0")
            + G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0")
            + G(0, "ENDBLK", 8, "0")
            + G(0, "BLOCK", 8, "0", 2, "C", 70, 0, 10, "0", 20, "0", 30, "0")
            + string.Concat(Enumerable.Range(0, 10).Select(i => G(0, "LINE", 8, "0", 10, i, 20, "0", 11, i, 21, "1")))
            + G(0, "ENDBLK", 8, "0"),
            G(0, "INSERT", 8, "0", 2, "A", 10, "0", 20, "0") + G(0, "INSERT", 8, "0", 2, "B", 10, "0", 20, "0"));

        [Fact]
        public void TheSizeOfABlockThatInsertsItself_IsWorkedOutOnTheWayItIsPlaced_NotKeptFromAnother()
        {
            // B sized inside A, where A counts nought, is 1; sized for its own INSERT, it places A and its 50 000 lines, past
            // the bound of 1 000, and is not placed. Each block read on its own: B keeps its line, C its ten.
            DxfDocument read = DxfReader.Parse(Cycle(100, 50), new DxfReadOptions { MaxPlacedEntities = 1000, Tolerance = Tol });

            Assert.Empty(read.Entities);
            Assert.Equal(2, read.Skipped["INSERT"]);
            Assert.Equal(new[] { 0, 1, 10 }, read.Blocks.Select(b => b.Entities.Count).ToArray());
            Assert.Contains(read.Warnings, w => w.StartsWith("5 INSERTs were not placed, 3 of them inside the blocks read for Blocks: ", StringComparison.Ordinal));
        }

        [Fact]
        public void AnInsertWhoseBlockPlacesMoreThanItsSizeSaid_IsTakenBackWhole_AndTheBoundHolds()
        {
            // The sizes taken as nought, every INSERT is let through and the count of what is placed alone holds the bound:
            // each INSERT that passes it is taken back, its entities and what it counted, and the reading goes on.
            DxfDocument read = DxfReader.Parse(Cycle(100, 50), new DxfReadOptions { MaxPlacedEntities = 1000, Tolerance = Tol, UnsizedBlocks = true });

            Assert.Empty(read.Entities);
            Assert.Equal(new[] { "INSERT" }, read.Skipped.Keys.ToArray());
            Assert.Equal(2, read.Skipped["INSERT"]);
            Assert.DoesNotContain(read.Warnings, w => w.Contains("inside themselves"));
            Assert.Equal(new[] { 1, 1, 10 }, read.Blocks.Select(b => b.Entities.Count).ToArray());
            Assert.Contains(read.Warnings, w => w.StartsWith("4 INSERTs were not placed, 2 of them inside the blocks read for Blocks: ", StringComparison.Ordinal));
        }

        private static string WithBlock(string blocks, string entities) => File("AC1015", 4, G(0, "SECTION", 2, "BLOCKS") + blocks + G(0, "ENDSEC"), entities);
    }
}
