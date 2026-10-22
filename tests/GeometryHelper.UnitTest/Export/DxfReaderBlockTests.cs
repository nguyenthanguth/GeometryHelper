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
    /// Blocks and the INSERTs that place them, written by hand as AutoCAD writes them, read by <see cref="DxfReader"/>: the
    /// look an entity takes from its INSERT, the blocks a file defines, those that are not placed and why, and the shapes a
    /// placement gives.
    /// </summary>
    public class DxfReaderBlockTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static DxfDocument Plan(string text) => DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, Tolerance = Tol });

        private static DxfDocument Space(string text) => DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });

        /// <summary>A BLOCKS section of these blocks.</summary>
        private static string Blocks(params string[] blocks) => G(0, "SECTION", 2, "BLOCKS") + string.Concat(blocks) + G(0, "ENDSEC");

        /// <summary>A block of a name, a base point and flags, with these entities.</summary>
        private static string Block(string name, string entities, double x = 0.0, double y = 0.0, int flags = 0)
            => G(0, "BLOCK", 8, "0", 2, name, 70, flags, 10, x, 20, y, 30, "0", 3, name) + entities + G(0, "ENDBLK", 8, "0");

        private static string WithBlocks(string blocks, string entities) => File("AC1015", 4, blocks, entities);

        private static string Insert(string block, double x, double y, string handle = null, string layer = "0")
            => handle == null ? G(0, "INSERT", 8, layer, 2, block, 10, x, 20, y, 30, "0") : G(0, "INSERT", 5, handle, 8, layer, 2, block, 10, x, 20, y, 30, "0");

        private static void Near(GeoPoint2 expected, GeoPoint2 actual, string what)
            => Assert.True(expected.DistanceTo(actual) <= 1E-12, what + ": " + expected + " read as " + actual);

        // ----- The look an entity takes from its INSERT -----------------------------------------------------------------

        [Fact]
        public void AnEntityOnLayer0_TakesTheLayerOfItsInsert_ThroughEveryLevel_AndOneOnALayerOfItsOwnKeepsIt()
        {
            string text = WithBlocks(
                Blocks(
                    Block("INNER", G(0, "LINE", 5, "11", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0")),
                    Block("OUTER", G(0, "LINE", 5, "21", 8, "0", 10, "0", 20, "0", 11, "2", 21, "0")
                        + G(0, "LINE", 5, "22", 8, "Own", 10, "0", 20, "0", 11, "3", 21, "0")
                        + Insert("INNER", 0, 5, "23")
                        + Insert("INNER", 0, 9, "24", "Middle"))),
                Insert("OUTER", 100, 0, "31", "Steel"));

            DxfDocument read = Plan(text);

            Assert.Equal(new[] { "21", "22", "11", "11" }, read.Entities.Select(e => e.Handle).ToArray());
            Assert.Equal(new[] { "Steel", "Own", "Steel", "Middle" }, read.Entities.Select(e => e.Layer).ToArray());
            Assert.Equal(new[] { "OUTER", "INNER" }, read.Entities[3].BlockPath.ToArray());
            Assert.Equal(new[] { "31", "24" }, read.Entities[3].InsertHandles.ToArray());
            Near(new GeoPoint2(101, 9), ((GeoLine2)read.Entities[3].Shape).EndPoint, "the inner line");
            Assert.Contains("Steel", read.Layers);
            Assert.Contains("Middle", read.Layers);
        }

        [Fact]
        public void AColourLinetypeAndLineweightByBlock_TakeTheInserts_ThroughEveryLevel_AndByLayerStaysByLayer()
        {
            string text = WithBlocks(
                Blocks(
                    Block("C", G(0, "LINE", 5, "C1", 8, "0", 62, 0, 6, "BYBLOCK", 370, -2, 10, "0", 20, "0", 11, "1", 21, "0")),
                    Block("B", G(0, "LINE", 5, "B1", 8, "0", 62, 0, 6, "ByBlock", 370, -2, 10, "0", 20, "0", 11, "1", 21, "0")
                        + G(0, "LINE", 5, "B2", 8, "Own", 62, 256, 6, "BYLAYER", 370, -1, 10, "0", 20, "0", 11, "2", 21, "0")
                        + G(0, "LINE", 5, "B3", 8, "0", 62, 0, 420, 0x123456, 10, "0", 20, "0", 11, "3", 21, "0")
                        + G(0, "LINE", 5, "B4", 8, "0", 62, 5, 6, "HIDDEN", 370, 18, 10, "0", 20, "0", 11, "4", 21, "0")
                        + G(0, "INSERT", 5, "B5", 8, "0", 62, 0, 6, "BYBLOCK", 370, -2, 2, "C", 10, "0", 20, "9"))),
                G(0, "INSERT", 5, "M1", 8, "Steel", 62, 3, 420, 0xFF8000, 6, "DASHED", 370, 35, 2, "B", 10, "0", 20, "0")
                + G(0, "LINE", 5, "M2", 8, "0", 62, 0, 6, "BYBLOCK", 370, -2, 10, "0", 20, "0", 11, "5", 21, "0"));

            DxfDocument read = Plan(text);
            DxfEntity Of(string handle) => read.Entities.Single(e => e.Handle == handle);

            DxfEntity byBlock = Of("B1");
            Assert.Equal("Steel", byBlock.Layer);
            Assert.Equal(3, byBlock.Colour);
            Assert.Equal(0xFF8000, byBlock.TrueColour);
            Assert.Equal("DASHED", byBlock.Linetype);
            Assert.Equal(35, byBlock.Lineweight);

            DxfEntity byLayer = Of("B2");
            Assert.Equal("Own", byLayer.Layer);
            Assert.Null(byLayer.Colour);
            Assert.Null(byLayer.TrueColour);
            Assert.Null(byLayer.Linetype);
            Assert.Null(byLayer.Lineweight);

            // A true colour of its own is kept; the colour by block is the INSERT's.
            Assert.Equal(3, Of("B3").Colour);
            Assert.Equal(0x123456, Of("B3").TrueColour);

            // A look of its own is kept whole.
            Assert.Equal(5, Of("B4").Colour);
            Assert.Null(Of("B4").TrueColour);
            Assert.Equal("HIDDEN", Of("B4").Linetype);
            Assert.Equal(18, Of("B4").Lineweight);

            // The INSERT of C inside B takes B's INSERT's look by block, and the line of C takes that in turn.
            DxfEntity nested = Of("C1");
            Assert.Equal("Steel", nested.Layer);
            Assert.Equal(3, nested.Colour);
            Assert.Equal(0xFF8000, nested.TrueColour);
            Assert.Equal("DASHED", nested.Linetype);
            Assert.Equal(35, nested.Lineweight);

            // In the drawing itself there is no block to take a look from: BYBLOCK is no colour, and the linetype as named.
            DxfEntity top = Of("M2");
            Assert.Equal("0", top.Layer);
            Assert.Null(top.Colour);
            Assert.Equal("BYBLOCK", top.Linetype);
            Assert.Null(top.Lineweight);
            Assert.Empty(top.BlockPath);
            Assert.Empty(top.InsertHandles);
        }

        [Fact]
        public void TheThicknessOfAPlacedEntity_IsStretchedAsItsExtrusionIs()
        {
            string text = WithBlocks(
                Blocks(Block("T", G(0, "LINE", 8, "0", 39, "10", 10, "0", 20, "0", 11, "1", 21, "0") + G(0, "CIRCLE", 8, "0", 39, "-4", 10, "0", 20, "0", 40, "1"))),
                G(0, "INSERT", 8, "0", 2, "T", 10, "0", 20, "0", 41, "2", 42, "2", 43, "3")
                + G(0, "INSERT", 8, "0", 2, "T", 10, "0", 20, "0", 41, "-1", 42, "1", 43, "1"));

            DxfDocument read = Space(text);

            Assert.Equal(new[] { 30.0, -12.0, 10.0, -4.0 }, read.Entities.Select(e => e.Thickness).ToArray());
        }

        // ----- The blocks of the file ------------------------------------------------------------------------------------

        [Fact]
        public void TheBlocks_HoldTheirOwnEntities_InTheirOwnCoordinates_InTheOrderOfTheFile()
        {
            string text = WithBlocks(
                Blocks(
                    Block("BOLT", G(0, "CIRCLE", 5, "61", 8, "0", 62, 0, 10, "0", 20, "0", 40, "10"), 2, 3),
                    Block("*U7", G(0, "LINE", 5, "62", 8, "0", 10, "0", 20, "0", 11, "4", 21, "0") + Insert("BOLT", 50, 0, "63"), 0, 0, 1)),
                Insert("*U7", 1000, 0, "64"));

            DxfDocument read = Plan(text);

            Assert.Equal(new[] { "BOLT", "*U7" }, read.Blocks.Select(b => b.Name).ToArray());
            Assert.Equal(new GeoPoint3(2, 3, 0), read.Blocks[0].BasePoint);
            Assert.Equal(1, read.Blocks[1].Flags);

            // The block's circle about its own origin, on layer 0, its colour by block no colour, as no INSERT places it.
            DxfEntity bolt = Assert.Single(read.Blocks[0].Entities);
            Assert.Equal(new GeoCircle2(new GeoPoint2(0, 0), 10), Assert.IsType<GeoCircle2>(bolt.Shape));
            Assert.Equal("0", bolt.Layer);
            Assert.Null(bolt.Colour);
            Assert.Empty(bolt.BlockPath);

            // The anonymous block holds its line, and the BOLT it places within itself.
            Assert.Equal(new[] { "62", "61" }, read.Blocks[1].Entities.Select(e => e.Handle).ToArray());
            Assert.Equal(new[] { "BOLT" }, read.Blocks[1].Entities[1].BlockPath.ToArray());
            Assert.Equal(new GeoPoint2(48, -3), ((GeoCircle2)read.Blocks[1].Entities[1].Shape).Center);

            // The drawing places the anonymous block like any other.
            Assert.Equal(new[] { "*U7", "*U7" }, read.Entities.Select(e => e.BlockPath[0]).ToArray());
            Assert.Equal(new GeoPoint2(1048, -3), ((GeoCircle2)read.Entities[1].Shape).Center);
            Assert.Empty(read.Skipped);
            Assert.Empty(read.Warnings);
        }

        [Fact]
        public void TheBlocksOfTheLayouts_AreNotRead_NorAmongTheBlocks_AndAWarningCountsTheirEntities()
        {
            string text = WithBlocks(
                Blocks(
                    Block("*Model_Space", string.Empty),
                    Block("*Paper_Space", G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0")),
                    Block("*Paper_Space0", G(0, "CIRCLE", 8, "0", 10, "0", 20, "0", 40, "1") + G(0, "POINT", 8, "0", 10, "0", 20, "0")),
                    Block("B", G(0, "POINT", 8, "0", 10, "1", 20, "1"))),
                Insert("B", 0, 0));

            DxfDocument read = Plan(text);

            Assert.Equal(new[] { "B" }, read.Blocks.Select(b => b.Name).ToArray());
            Assert.IsType<GeoPoint2>(Assert.Single(read.Entities).Shape);
            Assert.Contains("3 entities of the blocks of the layouts (*Model_Space, *Paper_Space) were not read: the drawing is read from its ENTITIES section.", read.Warnings);
        }

        [Fact]
        public void WithExpandBlocksFalse_TheInsertsAreCountedAndNotPlaced_AndTheBlocksStillRead()
        {
            string text = WithBlocks(Blocks(Block("B", G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0"))), Insert("B", 5, 5) + Insert("B", 9, 9) + G(0, "POINT", 8, "0", 10, "1", 20, "1"));

            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { ExpandBlocks = false, PlanAs2D = true, Tolerance = Tol });

            Assert.IsType<GeoPoint2>(Assert.Single(read.Entities).Shape);
            Assert.Equal(2, read.Skipped["INSERT"]);
            Assert.IsType<GeoLine2>(Assert.Single(Assert.Single(read.Blocks).Entities).Shape);
            Assert.Empty(read.Warnings);
            Assert.True(new DxfReadOptions().ExpandBlocks);
        }

        [Fact]
        public void BlocksDefinedAfterTheEntities_AreStillPlaced_EachWhereItsInsertStands()
        {
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 0, "ENDSEC")
                + G(0, "SECTION", 2, "ENTITIES")
                + G(0, "POINT", 5, "1", 8, "0", 10, "0", 20, "0") + Insert("B", 10, 0, "2") + G(0, "POINT", 5, "3", 8, "0", 10, "5", 20, "5") + Insert("B", 20, 0, "4")
                + G(0, "ENDSEC")
                + Blocks(Block("B", G(0, "LINE", 5, "9", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0")))
                + G(0, "EOF");

            DxfDocument read = Plan(text);

            Assert.Equal(new[] { "1", "9", "3", "9" }, read.Entities.Select(e => e.Handle).ToArray());
            Assert.Equal(new GeoPoint2(21, 0), ((GeoLine2)read.Entities[3].Shape).EndPoint);
        }

        [Fact]
        public void ABlockDefinedTwice_KeepsTheFirstDefinition_AndAWarningSaysSo()
        {
            string text = WithBlocks(
                Blocks(Block("B", G(0, "POINT", 8, "0", 10, "1", 20, "1")), Block("b", G(0, "POINT", 8, "0", 10, "2", 20, "2"))),
                Insert("B", 0, 0));

            DxfDocument read = Plan(text);

            Assert.Equal(new GeoPoint2(1, 1), Assert.Single(read.Entities).Shape);
            Assert.Single(read.Blocks);
            Assert.Contains("1 block was defined again under a name already defined, and the first definition kept: b.", read.Warnings);
        }

        // ----- INSERTs that are not placed ------------------------------------------------------------------------------

        [Fact]
        public void ABlockThatInsertsItself_ThroughAnother_IsNotPlacedAgain_AndTheRestIsRead()
        {
            string text = WithBlocks(
                Blocks(
                    Block("A", G(0, "LINE", 5, "A1", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0") + Insert("B", 10, 0, "A2")),
                    Block("B", G(0, "CIRCLE", 5, "B1", 8, "0", 10, "0", 20, "0", 40, "1") + Insert("A", 10, 0, "B2")),
                    Block("S", G(0, "POINT", 5, "S1", 8, "0", 10, "0", 20, "0") + Insert("S", 1, 1, "S2"))),
                Insert("A", 0, 0, "M1") + Insert("S", 0, 0, "M2"));

            DxfDocument read = Plan(text);

            Assert.Equal(new[] { "A1", "B1", "S1" }, read.Entities.Select(e => e.Handle).ToArray());
            Assert.Equal(new[] { "A", "B" }, read.Entities[1].BlockPath.ToArray());
            Assert.Equal(2, read.Skipped["INSERT"]);
            Assert.Contains("2 INSERTs of blocks inside themselves were not placed again, so as not to place them without end: A > B > A the first.", read.Warnings);

            // Each block on its own holds what it can.
            Assert.Equal(new[] { "A1", "B1" }, read.Blocks[0].Entities.Select(e => e.Handle).ToArray());
            Assert.Equal(new[] { "B1", "A1" }, read.Blocks[1].Entities.Select(e => e.Handle).ToArray());
        }

        [Fact]
        public void AChainOfInsertsMoreThan64Deep_IsCutThere_WithAWarning()
        {
            var blocks = new StringBuilder();

            for (int i = 0; i < 70; i++)
            {
                blocks.Append(Block("D" + i, G(0, "POINT", 8, "0", 10, "0", 20, "0") + (i < 69 ? Insert("D" + (i + 1), 1, 0) : string.Empty)));
            }

            DxfDocument read = Plan(WithBlocks(Blocks(blocks.ToString()), Insert("D0", 0, 0)));

            Assert.Equal(64, read.Entities.Count);
            Assert.Equal(new GeoPoint2(63, 0), read.Entities[63].Shape);
            Assert.Equal(64, read.Entities[63].BlockPath.Count);
            Assert.Equal(1, read.Skipped["INSERT"]);
            Assert.Contains("1 INSERT nested more than 64 deep was not placed.", read.Warnings);
        }

        [Fact]
        public void AnInsertOfABlockTheFileDoesNotDefine_IsCounted_AndNamedInAWarning()
        {
            DxfDocument read = Plan(WithBlocks(Blocks(Block("B", G(0, "POINT", 8, "0", 10, "0", 20, "0"))), Insert("NOPE", 0, 0) + Insert("B", 0, 0) + Insert("NOPE", 1, 0) + Insert("Gone", 0, 0)));

            Assert.Single(read.Entities);
            Assert.Equal(3, read.Skipped["INSERT"]);
            Assert.Contains("3 INSERTs name blocks the file does not define and were left out: NOPE, Gone.", read.Warnings);
        }

        [Fact]
        public void AnInsertOfAnExternalReference_IsNotPlaced_AndItsBlockIsAmongTheBlocks()
        {
            string text = WithBlocks(
                Blocks(Block("XREF", string.Empty, 0, 0, 4), Block("B", G(0, "POINT", 8, "0", 10, "0", 20, "0"))),
                Insert("XREF", 0, 0) + Insert("B", 0, 0));

            DxfDocument read = Plan(text);

            Assert.Single(read.Entities);
            Assert.Equal(1, read.Skipped["INSERT"]);
            Assert.Equal(4, read.Blocks[0].Flags);
            Assert.Contains("1 INSERT of an external reference was not placed: the drawing it refers to is another file, which is not read.", read.Warnings);
        }

        [Fact]
        public void AMInsertOfMoreThanAMillionCells_IsNotPlaced()
        {
            string text = WithBlocks(
                Blocks(Block("B", G(0, "POINT", 8, "0", 10, "0", 20, "0"))),
                G(0, "INSERT", 8, "0", 100, "AcDbMInsertBlock", 2, "B", 10, "0", 20, "0", 70, 2000, 71, 1000, 44, "1", 45, "1"));

            DxfDocument read = Plan(text);

            Assert.Empty(read.Entities);
            Assert.Equal(1, read.Skipped["INSERT"]);
            Assert.Contains("1 MINSERT of more than 1 048 576 cells was not placed.", read.Warnings);
        }

        [Fact]
        public void AMInsertWithASpacingOfNought_PlacesItsCellsShiftedAlikeOnce_AsEzdxfDoes()
        {
            string text = WithBlocks(
                Blocks(Block("B", G(0, "POINT", 8, "0", 10, "0", 20, "0"))),
                G(0, "INSERT", 8, "0", 100, "AcDbMInsertBlock", 2, "B", 10, "0", 20, "0", 70, 3, 71, 2, 44, "0", 45, "5"));

            DxfDocument read = Plan(text);

            Assert.Equal(new object[] { new GeoPoint2(0, 0), new GeoPoint2(0, 5) }, read.Entities.Select(e => e.Shape).ToArray());
        }

        [Fact]
        public void AttributesAndTheirDefinitions_AreCountedAsTheTextTheyAre()
        {
            string text = WithBlocks(
                Blocks(Block("TAG", G(0, "ATTDEF", 8, "0", 10, "0", 20, "0", 40, "2.5", 1, "", 2, "NO", 3, "Number?") + G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0"), 0, 0, 2)),
                G(0, "INSERT", 8, "0", 66, 1, 2, "TAG", 10, "0", 20, "0") + G(0, "ATTRIB", 8, "0", 10, "0", 20, "0", 40, "2.5", 1, "17", 2, "NO") + G(0, "SEQEND", 8, "0")
                + G(0, "INSERT", 8, "0", 66, 1, 2, "TAG", 10, "5", 20, "0") + G(0, "ATTRIB", 8, "0", 10, "5", 20, "0", 40, "2.5", 1, "18", 2, "NO") + G(0, "SEQEND", 8, "0"));

            DxfDocument read = Plan(text);

            Assert.Equal(2, read.Entities.Count);
            Assert.Equal(2, read.Skipped["ATTRIB"]);
            Assert.Equal(2, read.Skipped["ATTDEF"]);
            Assert.False(read.Skipped.ContainsKey("INSERT"));
        }

        [Fact]
        public void AnInsertInPaperSpace_IsCountedWithPaperSpace()
        {
            string text = WithBlocks(Blocks(Block("B", G(0, "POINT", 8, "0", 10, "0", 20, "0"))), G(0, "INSERT", 8, "0", 67, 1, 2, "B", 10, "0", 20, "0") + Insert("B", 1, 1));

            DxfDocument read = Plan(text);

            Assert.Equal(new GeoPoint2(1, 1), Assert.Single(read.Entities).Shape);
            Assert.Contains("1 entity of paper space was not read: the reader reads model space.", read.Warnings);
        }

        // ----- The shapes a placement gives ------------------------------------------------------------------------------

        [Fact]
        public void AnInsertThatPutsItsBlockWhereItIs_ReadsItsEntitiesToTheBit_AsTheDrawingsOwn()
        {
            string entities =
                G(0, "LINE", 8, "0", 10, "0.1", 20, "0.2", 30, "0.3", 11, "10.7", 21, "-3.3", 31, "1E-7")
                + G(0, "CIRCLE", 8, "0", 10, "1.1", 20, "2.2", 30, "3.3", 40, "4.4", 210, "0.6", 220, "0", 230, "0.8")
                + G(0, "ARC", 8, "0", 10, "1.1", 20, "2.2", 30, "0", 40, "4.4", 50, "33.3", 51, "-12.5", 210, "0", 220, "0", 230, "-1")
                + G(0, "ARC", 8, "0", 10, "1.1", 20, "2.2", 30, "0.5", 40, "4.4", 50, "33.3", 51, "190", 210, "1", 220, "2", 230, "3")
                + G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 3, 70, 1, 38, "2.5", 10, "0", 20, "0", 42, "0.41", 10, "10", 20, "0", 10, "10", 20, "10", 42, "-0.2", 210, "0.3", 220, "-0.5", 230, "0.8")
                + G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 3, 70, 0, 10, "0", 20, "0", 42, "0.41", 10, "10", 20, "0", 10, "10", 20, "10")
                + G(0, "ELLIPSE", 8, "0", 10, "1", 20, "2", 30, "3", 11, "20", 21, "5", 31, "0", 40, "0.4", 41, "1.0", 42, "2.5")
                + G(0, "ELLIPSE", 8, "0", 10, "1", 20, "2", 30, "0", 11, "20", 21, "5", 31, "0", 40, "0.4", 41, "0", 42, "6.283185307179586")
                + G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 70, 8, 71, 2, 72, 6, 73, 3, 40, "0", 40, "0", 40, "0", 40, "1", 40, "1", 40, "1", 41, "1", 41, "0.7", 41, "1", 10, "0", 20, "10", 30, "0", 10, "5", 20, "15", 30, "0", 10, "10", 20, "10", 30, "0")
                + G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 70, 8, 71, 3, 74, 4, 11, "0", 21, "0", 31, "0", 11, "3", 21, "4", 31, "0", 11, "7", 21, "1", 31, "0", 11, "9", 21, "6", 31, "0")
                + G(0, "3DFACE", 8, "0", 10, "0", 20, "0", 30, "0", 11, "4", 21, "0", 31, "0", 12, "4", 22, "3", 32, "1", 13, "0", 23, "3", 33, "0")
                + G(0, "SOLID", 8, "0", 10, "0", 20, "0", 11, "4", 21, "0", 12, "0", 22, "3", 13, "4", 23, "3", 210, "0", 220, "0", 230, "-1")
                + G(0, "RAY", 8, "0", 10, "1", 20, "1", 30, "0", 11, "0.6", 21, "0.8", 31, "0")
                + G(0, "POLYLINE", 8, "0", 66, 1, 70, 8, 10, "0", 20, "0", 30, "0")
                + G(0, "VERTEX", 8, "0", 70, 32, 10, "0", 20, "0", 30, "0") + G(0, "VERTEX", 8, "0", 70, 32, 10, "5", 20, "0", 30, "3") + G(0, "VERTEX", 8, "0", 70, 32, 10, "5", 20, "5", 30, "6")
                + G(0, "SEQEND", 8, "0");

            foreach (bool plan in new[] { false, true })
            {
                var options = new DxfReadOptions { PlanAs2D = plan, Tolerance = Tol };
                DxfDocument own = DxfReader.Parse(R2000(entities), options);
                DxfDocument placed = DxfReader.Parse(WithBlocks(Blocks(Block("ALL", entities)), Insert("ALL", 0, 0) + G(0, "INSERT", 8, "0", 2, "ALL", 41, "1", 50, "0", 210, "0", 220, "0", 230, "1")), options);

                Assert.Equal(own.Entities.Count * 2, placed.Entities.Count);

                // Written out, every number of a shape is written as it is held, so two files alike are two readings alike.
                var expected = new DxfWriter();
                var actual = new DxfWriter();

                foreach (DxfEntity entity in own.Entities.Concat(own.Entities))
                {
                    expected.Add(entity);
                }

                foreach (DxfEntity entity in placed.Entities)
                {
                    actual.Add(entity);
                }

                Assert.Equal(expected.ToString(), actual.ToString());
                Assert.Equal(own.Entities.Select(e => e.Shape.GetType()).Concat(own.Entities.Select(e => e.Shape.GetType())), placed.Entities.Select(e => e.Shape.GetType()));
            }
        }

        [Fact]
        public void AnEntityPlaced_WrittenAndReadAgain_IsTheSameShapeWithTheSameLook_InTheDrawingItself()
        {
            string text = WithBlocks(
                Blocks(Block("B", G(0, "CIRCLE", 8, "0", 62, 0, 10, "5", 20, "5", 40, "3") + G(0, "ARC", 8, "Own", 10, "5", 20, "5", 40, "4", 50, "30", 51, "200"), 2, 1)),
                G(0, "INSERT", 8, "Steel", 62, 4, 2, "B", 10, "100", 20, "50", 41, "3", 42, "1", 43, "1", 50, "30"));

            DxfDocument read = Plan(text);
            var writer = new DxfWriter();

            foreach (DxfEntity entity in read.Entities)
            {
                writer.Add(entity);
            }

            DxfDocument again = Plan(writer.ToString());

            Assert.Equal(new[] { typeof(GeoEllipse2), typeof(GeoSpline2) }, again.Entities.Select(e => e.Shape.GetType()).ToArray());
            Assert.Equal(new[] { "Steel", "Own" }, again.Entities.Select(e => e.Layer).ToArray());
            Assert.Equal(new int?[] { 4, null }, again.Entities.Select(e => e.Colour).ToArray());
            Assert.All(again.Entities, e => Assert.Empty(e.BlockPath));

            var spline = (GeoSpline2)read.Entities[1].Shape;
            var back = (GeoSpline2)again.Entities[1].Shape;
            Assert.Equal(spline.ControlPoints.ToArray(), back.ControlPoints.ToArray());
            Assert.Equal(spline.Weights.ToArray(), back.Weights.ToArray());
        }

        [Fact]
        public void ACircleUnderAScalingEvenToARoundingOfTheTolerance_StaysACircle_AndOneFurtherOffIsAnEllipse()
        {
            string Scaled(string y) => WithBlocks(Blocks(Block("B", G(0, "CIRCLE", 8, "0", 10, "0", 20, "0", 40, "10") + G(0, "ARC", 8, "0", 10, "0", 20, "0", 40, "10", 50, "0", 51, "90"))), G(0, "INSERT", 8, "0", 2, "B", 10, "0", 20, "0", 41, "1", 42, y, 43, "1"));

            // A share of 1E-7 moves the circle of radius 10 by 1E-6, within the point tolerance: a circle of radius 10.
            DxfDocument near = Plan(Scaled("1.0000001"));
            Assert.Equal(10.0, Assert.IsType<GeoCircle2>(near.Entities[0].Shape).Radius);
            Assert.IsType<GeoArc2>(near.Entities[1].Shape);

            // A share of 1E-3 moves it by 0.01: the ellipse 10.01 by 10, and the arc the spline that is exactly its part.
            DxfDocument off = Plan(Scaled("1.001"));
            var ellipse = Assert.IsType<GeoEllipse2>(off.Entities[0].Shape);
            Assert.Equal(10.01, ellipse.MajorRadius, 12);
            Assert.Equal(10.0, ellipse.MinorRadius, 12);
            var part = Assert.IsType<GeoSpline2>(off.Entities[1].Shape);
            Assert.Equal(new GeoPoint2(10, 0), part.StartPoint);
            Near(new GeoPoint2(0, 10.01), part.EndPoint, "the end of the quarter");
        }

        [Fact]
        public void AClosedPolyfaceMesh_PlacedByAScaling_IsTheBodyScaled_ItsVolumeTimesTheScales_AndMirroredStillFacesOut()
        {
            var cube = new StringBuilder(G(0, "POLYLINE", 8, "0", 66, 1, 70, 64, 71, 8, 72, 6, 10, "0", 20, "0", 30, "0"));
            int[][] corners = { new[] { 0, 0, 0 }, new[] { 10, 0, 0 }, new[] { 10, 10, 0 }, new[] { 0, 10, 0 }, new[] { 0, 0, 10 }, new[] { 10, 0, 10 }, new[] { 10, 10, 10 }, new[] { 0, 10, 10 } };
            int[][] faces = { new[] { 1, 4, 3, 2 }, new[] { 5, 6, 7, 8 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 4, 8, 7 }, new[] { 4, 1, 5, 8 } };

            foreach (int[] c in corners)
            {
                cube.Append(G(0, "VERTEX", 8, "0", 70, 192, 10, c[0], 20, c[1], 30, c[2]));
            }

            foreach (int[] f in faces)
            {
                cube.Append(G(0, "VERTEX", 8, "0", 70, 128, 10, "0", 20, "0", 30, "0", 71, f[0], 72, f[1], 73, f[2], 74, f[3]));
            }

            cube.Append(G(0, "SEQEND", 8, "0"));

            string text = WithBlocks(
                Blocks(Block("CUBE", cube.ToString())),
                G(0, "INSERT", 8, "0", 2, "CUBE", 10, "100", 20, "0", 41, "2", 42, "3", 43, "0.5", 50, "30")
                + G(0, "INSERT", 8, "0", 2, "CUBE", 10, "0", 20, "0", 41, "-2", 42, "3", 43, "1", 50, "30"));

            DxfDocument read = Space(text);
            var body = Assert.IsType<GeoSolid3>(read.Entities[0].Shape);
            var mirrored = Assert.IsType<GeoSolid3>(read.Entities[1].Shape);

            Assert.True(body.IsClosed(Tol));
            Assert.Equal(3000.0, body.GetVolume(Tol), 6);

            // Mirrored, each face is taken the other way round, so the body faces outwards as in its block.
            Assert.True(mirrored.Validate(Tol).IsValid);
            Assert.Equal(6000.0, mirrored.GetVolume(Tol), 6);
        }

        [Fact]
        public void APlacementPastTheLargestDouble_LeavesItsEntitiesOut_AsNotFinite_AndATiltedPlaneThatTakesACircleThereToo()
        {
            string text = WithBlocks(
                Blocks(Block("B", G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "10", 21, "0") + G(0, "POINT", 8, "0", 10, "5", 20, "5"))),
                G(0, "INSERT", 8, "0", 2, "B", 10, "0", 20, "0", 41, "1E308", 42, "1E308", 43, "1"));

            DxfDocument read = Plan(text);

            Assert.Empty(read.Entities);
            Assert.Equal(1, read.Skipped["LINE"]);
            Assert.Equal(1, read.Skipped["POINT"]);

            // On the extrusion (0.6, 0, 0.8) a centre at y = -1.7E308 and a height of 1.7E308 lands at an x of 2.38E308 in the
            // world, past the largest double.
            DxfDocument tilted = Space(R2000(
                G(0, "CIRCLE", 8, "0", 10, "0", 20, "-1.7E308", 30, "1.7E308", 40, "1", 210, "0.6", 220, "0", 230, "0.8")
                + G(0, "LWPOLYLINE", 8, "0", 100, "AcDbPolyline", 90, 2, 70, 0, 38, "1.7E308", 10, "0", 20, "-1.7E308", 10, "1", 20, "-1.7E308", 210, "0.6", 220, "0", 230, "0.8")
                + G(0, "POINT", 8, "0", 10, "1", 20, "1")));

            Assert.IsType<GeoPoint3>(Assert.Single(tilted.Entities).Shape);
            Assert.Equal(1, tilted.Skipped["CIRCLE"]);
            Assert.Equal(1, tilted.Skipped["LWPOLYLINE"]);
        }

        [Fact]
        public void ASplineThroughFitPoints_PlacedUnevenly_IsTheBlocksSplinePlaced()
        {
            string spline = G(0, "SPLINE", 8, "0", 100, "AcDbSpline", 70, 8, 71, 3, 74, 4, 11, "0", 21, "0", 31, "0", 11, "3", 21, "4", 31, "0", 11, "7", 21, "1", 31, "0", 11, "9", 21, "6", 31, "0");
            DxfDocument own = Space(R2000(spline));
            DxfDocument placed = Space(WithBlocks(Blocks(Block("B", spline)), G(0, "INSERT", 8, "0", 2, "B", 10, "5", 20, "6", 30, "7", 41, "2", 42, "-1", 43, "1")));

            var mine = (GeoSpline3)own.Entities[0].Shape;
            var there = Assert.IsType<GeoSpline3>(Assert.Single(placed.Entities).Shape);

            Assert.Equal(mine.Knots.ToArray(), there.Knots.ToArray());
            Assert.Equal(mine.ControlPoints.Select(p => new GeoPoint3(5 + 2 * p.X, 6 - p.Y, 7 + p.Z)).ToArray(), there.ControlPoints.ToArray());
            Assert.Contains(placed.Warnings, w => w.Contains("fit points alone"));
        }

        [Fact]
        public void AnR12InsertWithoutAHandle_IsPlaced_ItsHandleNull()
        {
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1009", 0, "ENDSEC")
                + Blocks(G(0, "BLOCK", 8, "0", 2, "B", 70, 0, 10, "1", 20, "1", 30, "0") + G(0, "LINE", 8, "0", 10, "1", 20, "1", 11, "2", 21, "1") + G(0, "ENDBLK", 8, "0"))
                + G(0, "SECTION", 2, "ENTITIES") + G(0, "INSERT", 8, "0", 2, "B", 10, "10", 20, "10", 30, "0") + G(0, "ENDSEC", 0, "EOF");

            DxfEntity line = Assert.Single(Plan(text).Entities);

            Assert.Equal(new GeoLine2(10, 10, 11, 10), line.Shape);
            Assert.Null(line.Handle);
            Assert.Equal(new string[] { null }, line.InsertHandles.ToArray());
        }

        [Fact]
        public void ToMillimetres_ScalesTheInsertionPointTheBasePointAndTheSpacings_NotTheScales()
        {
            string text = File("AC1015", 6, Blocks(Block("B", G(0, "POINT", 8, "0", 10, "1", 20, "0"), 1, 0)),
                G(0, "INSERT", 8, "0", 100, "AcDbMInsertBlock", 2, "B", 10, "2", 20, "0", 41, "3", 42, "3", 43, "3", 70, 2, 71, 1, 44, "0.5", 45, "0"));

            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, ToMillimetres = true, Tolerance = Tol });

            Assert.Equal(new object[] { new GeoPoint2(2000, 0), new GeoPoint2(2500, 0) }, read.Entities.Select(e => e.Shape).ToArray());
            Assert.Equal(new GeoPoint3(1000, 0, 0), read.Blocks[0].BasePoint);
        }
    }
}
