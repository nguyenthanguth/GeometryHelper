using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The Settings of LAYOFF and LAYFRZ change what a pick takes, as AutoCAD 2021's console does (probed through its standard
    /// input over blocks nested three deep and a layout viewport worked through): the Block selection setting takes the layer
    /// of the block reference the object picked is in (Block, the innermost), of the object itself (Entity) or of the
    /// reference picked (None); the Viewports setting freezes the layer in the viewport worked through (Vpfreeze, "Layer
    /// "WALLS" has been frozen in this viewport.") or turns it off or freezes it everywhere (Off, Freeze); the current layer is
    /// asked about with "Layer "WALLS" is current, do you want to Vpfreeze it? [Yes/No] &lt;No&gt;:".
    /// </summary>
    public class LayerSettingsPickTests
    {
        /// <summary>
        /// A reference T1 of BO on LB, whose block holds a reference of BI on LN (a line on LI at y 0), a line on L2 at y 20 and a
        /// reference of BZ on LN2 (a line on layer 0 at y 40); a reference T2 of BS on LS (a line on LI at y 60). Layer 0 is
        /// current.
        /// </summary>
        private static CadDocument Nested() =>
            CadDocument.From(
                TestDrawings.Drawing(
                    TestDrawings.Layer("LB", 1)
                        + TestDrawings.Layer("LI", 2)
                        + TestDrawings.Layer("LN", 3)
                        + TestDrawings.Layer("L2", 4)
                        + TestDrawings.Layer("LN2", 5)
                        + TestDrawings.Layer("LS", 6),
                    TestDrawings.Block("BI", TestDrawings.Line(0, 0, 100, 0, "LI"))
                        + TestDrawings.Block("BZ", TestDrawings.Line(0, 40, 100, 40, "0"))
                        + TestDrawings.Block(
                            "BO",
                            TestDrawings.Insert("BI", 0, 0, layer: "LN"),
                            TestDrawings.Line(0, 20, 100, 20, "L2"),
                            TestDrawings.Insert("BZ", 0, 0, layer: "LN2")
                        )
                        + TestDrawings.Block("BS", TestDrawings.Line(0, 60, 100, 60, "LI")),
                    TestDrawings.Insert("BO", 0, 0, layer: "LB", handle: "T1"),
                    TestDrawings.Insert("BS", 0, 0, layer: "LS", handle: "T2")
                ),
                "nested.dxf"
            );

        [Theory]
        [InlineData("LAYOFF", 0, "B", "LN")]
        [InlineData("LAYOFF", 0, "E", "LI")]
        [InlineData("LAYOFF", 0, "N", "LB")]
        [InlineData("LAYOFF", 20, "B", "LB")]
        [InlineData("LAYOFF", 20, "E", "L2")]
        [InlineData("LAYOFF", 20, "N", "LB")]
        [InlineData("LAYOFF", 40, "B", "LN2")]
        [InlineData("LAYOFF", 40, "N", "LB")]
        [InlineData("LAYOFF", 60, "B", "LS")]
        [InlineData("LAYOFF", 60, "E", "LI")]
        [InlineData("LAYOFF", 60, "N", "LS")]
        [InlineData("LAYFRZ", 0, "B", "LN")]
        [InlineData("LAYFRZ", 0, "E", "LI")]
        [InlineData("LAYFRZ", 0, "N", "LB")]
        [InlineData("LAYFRZ", 40, "B", "LN2")]
        [InlineData("LAYFRZ", 60, "E", "LI")]
        public void ABlockSelectionLevel_TakesTheLayerOfTheReferenceInside_TheObject_OrTheReferencePicked(
            string command,
            double y,
            string level,
            string layer
        )
        {
            CadDocument document = Nested();
            CadCommandSession session = Session(document);

            Run(session, command, "S", "B", level, P(50, y));

            Assert.Equal(
                "Layer \"" + layer + "\" has been " + (command == "LAYOFF" ? "turned off" : "frozen") + ".",
                session.History.Last()
            );

            foreach (DxfLayerTableRecord record in document.Database.LayerTable)
            {
                Assert.Equal(record.Name == layer, command == "LAYOFF" ? record.IsOff : record.IsFrozen);
            }
        }

        [Fact]
        public void TheEntityLevel_OnAnObjectOnLayerZero_TakesLayerZero_WhichIsCurrent_SoItAsks()
        {
            CadCommandSession session = Run(Session(Nested()), "LAYOFF", "S", "B", "E", P(50, 40));

            Assert.Equal("Layer \"0\" is current, do you want to turn it off? [Yes/No] <No>:", session.PromptText);
        }

        [Fact]
        public void ThePickOfABlockWithNothingNested_TakesTheLayerOfTheReference_ByDefault()
        {
            CadDocument document = Nested();
            CadCommandSession session = Run(Session(document), "LAYOFF", P(50, 60));

            Assert.Equal("Layer \"LS\" has been turned off.", session.History.Last());
        }

        /// <summary>
        /// A rectangle on WALLS in model space and a viewport on Layout1 showing it, worked through (MSPACE); WALLS is a layer of
        /// its own, layer 0 current.
        /// </summary>
        private static CadDocument InAViewport(out CadCommandSession session, out string handle)
        {
            CadDocument document = Blank();
            session = Session(document);
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            document.Database.Clayer = "WALLS";
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.Database.Clayer = "0";
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            handle = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            Run(session, "MS");
            return document;
        }

        private static bool FrozenInTheViewport(CadDocument document, string handle) =>
            Entity<DxfViewport>(document, handle).IsLayerFrozenInViewport(document.Database.LayerTable["WALLS"]);

        [Fact]
        public void LayoffInAViewport_WithVpfreeze_FreezesTheLayerInThatViewportAlone_AndUThawsIt()
        {
            CadDocument document = InAViewport(out CadCommandSession session, out string handle);

            Run(session, "LAYOFF", P(1000, 0));

            Assert.Equal("Layer \"WALLS\" has been frozen in this viewport.", session.History.Last());
            Assert.True(FrozenInTheViewport(document, handle));
            Assert.False(document.Database.LayerTable["WALLS"].IsOff);

            Run(session, "U");

            Assert.False(FrozenInTheViewport(document, handle));
        }

        [Fact]
        public void LayoffInAViewport_WithOff_TurnsTheLayerOffEverywhere()
        {
            CadDocument document = InAViewport(out CadCommandSession session, out string handle);

            Run(session, "LAYOFF", "S", "V", "O", P(1000, 0));

            Assert.Equal("Layer \"WALLS\" has been turned off.", session.History.Last());
            Assert.True(document.Database.LayerTable["WALLS"].IsOff);
            Assert.False(FrozenInTheViewport(document, handle));
        }

        [Fact]
        public void LayfrzInAViewport_FreezesInTheViewportOrEverywhere_BySetting()
        {
            CadDocument document = InAViewport(out CadCommandSession session, out string handle);

            Run(session, "LAYFRZ", P(1000, 0));
            Assert.Equal("Layer \"WALLS\" has been frozen in this viewport.", session.History.Last());
            Assert.True(FrozenInTheViewport(document, handle));
            Assert.False(document.Database.LayerTable["WALLS"].IsFrozen);

            Run(session, Enter, "U");
            Assert.False(FrozenInTheViewport(document, handle));

            Run(session, "LAYFRZ", "S", "V", "F", P(1000, 0));
            Assert.Equal("Layer \"WALLS\" has been frozen.", session.History.Last());
            Assert.True(document.Database.LayerTable["WALLS"].IsFrozen);
        }

        [Fact]
        public void TheCurrentLayerInAViewport_IsAskedAboutAsVpfreeze_AndYesFreezesItThere()
        {
            CadDocument document = InAViewport(out CadCommandSession session, out string handle);
            document.Database.Clayer = "WALLS";

            Run(session, "LAYOFF", P(1000, 0));

            Assert.Equal("Layer \"WALLS\" is current, do you want to Vpfreeze it? [Yes/No] <No>:", session.PromptText);

            Run(session, "Y");

            Assert.Equal("Layer \"WALLS\" has been frozen in this viewport.", session.History.Last());
            Assert.True(FrozenInTheViewport(document, handle));
        }

        [Fact]
        public void LayfrzOnTheCurrentLayer_SaysItWithOneSpace_WhereMinusLayerSaysTwo()
        {
            CadCommandSession session = Run(Session(Nested()), "LAYFRZ", "S", "B", "E", P(50, 40));

            Assert.Equal("Cannot freeze layer \"0\". It is the CURRENT layer.", session.History.Last());
        }
    }
}
