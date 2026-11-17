using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// LAYMRG and LAYDEL, as AutoCAD 2021's command line forms ask them (probed in its console): the layers picked by an object
    /// or by Name until Enter, the target layer, the warning and "Do you wish to continue? [Yes/No] &lt;No&gt;:". A merge puts
    /// every object of the layers, those in blocks too, on the target and deletes them; a delete erases the objects with the
    /// layers.
    /// </summary>
    public class LayerMergeAndDeleteCommandTests
    {
        /// <summary>
        /// Layers WALL, DOOR and TRIM; in model space A1 on WALL, A2 and the circle A3 on DOOR, A4 on TRIM and A5, an INSERT of
        /// WIN on 0; the block WIN holds a line on DOOR, one on WALL and one on 0.
        /// </summary>
        private static CadDocument Walls()
        {
            string window = TestDrawings.Block(
                "WIN",
                TestDrawings.Line(0, 0, 10, 0, "DOOR"),
                TestDrawings.Line(10, 0, 10, 10, "WALL"),
                TestDrawings.Line(0, 10, 10, 10)
            );
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("WALL", 1) + TestDrawings.Layer("DOOR", 2) + TestDrawings.Layer("TRIM", 3),
                window,
                TestDrawings.Line(0, 0, 100, 0, "WALL", handle: "A1"),
                TestDrawings.Line(0, 10, 100, 10, "DOOR", handle: "A2"),
                TestDrawings.Circle(50, 50, 5, "DOOR", handle: "A3"),
                TestDrawings.Line(0, 20, 100, 20, "TRIM", handle: "A4"),
                TestDrawings.Insert("WIN", 200, 0, handle: "A5")
            );
            return CadDocument.From(database, "walls.dxf");
        }

        private static List<string> InBlock(CadDocument document, string block) =>
            document.Database.BlockTable[block].Select(e => e.Layer).ToList();

        private static string Layer(CadDocument document, string handle) => document.FindEntity(handle).Layer;

        [Fact]
        public void AnObjectOnDoorMergedIntoAnObjectOnWall_PutsEverythingOfDoorOnWall_BlockDefinitionsToo_AndDeletesDoor()
        {
            CadDocument document = Walls();
            CadCommandSession session = Run(Session(document), "LAYMRG");
            Assert.Equal("Select object on layer to merge or [Name]:", session.PromptText);

            Run(session, (object)new[] { "A2" });
            Assert.Equal("Selected layers: DOOR.", session.History.Last());
            Assert.Equal("Select object on layer to merge or [Name/Undo]:", session.PromptText);

            Run(session, Enter);
            Assert.Equal("Select object on target layer or [Name]:", session.PromptText);

            Run(session, (object)new[] { "A1" });
            Assert.Contains("You are about to merge layer \"DOOR\" into layer \"WALL\".", session.History);
            Assert.Equal("Do you wish to continue? [Yes/No] <No>:", session.PromptText);

            Run(session, "Yes");

            Assert.False(document.Database.LayerTable.Has("DOOR"));
            Assert.Equal("WALL", Layer(document, "A2"));
            Assert.Equal("WALL", Layer(document, "A3"));
            Assert.Equal("TRIM", Layer(document, "A4"));
            Assert.Equal(new[] { "WALL", "WALL", "0" }, InBlock(document, "WIN"));
            Assert.Equal("Deleting layer \"DOOR\".", session.History[session.History.Count - 2]);
            Assert.Equal("1 layer deleted.", session.History.Last());
        }

        [Fact]
        public void TwoLayersPickedAndOneByName_AreMergedTogether_TheWarningCountingThem()
        {
            CadDocument document = Walls();

            CadCommandSession session = Run(
                Session(document),
                "-LAYMRG",
                new[] { "A2" },
                "Name",
                "TRIM",
                Enter,
                "Name",
                "WALL"
            );
            Assert.Contains("Selected layers: DOOR,TRIM.", session.History);
            Assert.Contains("You are about to merge 2 layers into layer \"WALL\".", session.History);

            Run(session, "Yes");

            Assert.False(document.Database.LayerTable.Has("DOOR"));
            Assert.False(document.Database.LayerTable.Has("TRIM"));
            Assert.Equal("WALL", Layer(document, "A4"));
            Assert.Equal("2 layers deleted.", session.History.Last());
        }

        [Fact]
        public void AnswerNo_AbortsTheMerge_AndChangesNothing()
        {
            CadDocument document = Walls();

            CadCommandSession session = Run(Session(document), "LAYMRG", new[] { "A2" }, Enter, new[] { "A1" });
            Run(session, Enter);

            Assert.Equal("Operation aborted.", session.History.Last());
            Assert.True(document.Database.LayerTable.Has("DOOR"));
            Assert.Equal("DOOR", Layer(document, "A2"));
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TheMergeIsOneUndoStep_AndUndoPutsTheLayerAndItsObjectsBack()
        {
            CadDocument document = Walls();
            CadCommandSession session = Run(Session(document), "LAYMRG", new[] { "A2" }, Enter, new[] { "A1" }, "Yes");
            Assert.False(document.Database.LayerTable.Has("DOOR"));

            Run(session, "U");

            Assert.True(document.Database.LayerTable.Has("DOOR"));
            Assert.Equal("DOOR", Layer(document, "A2"));
            Assert.Equal("DOOR", Layer(document, "A3"));
            Assert.Equal(new[] { "DOOR", "WALL", "0" }, InBlock(document, "WIN"));
        }

        [Fact]
        public void TheCurrentLayer_ALayerWithItself_AndANameTheDrawingHasNot_AreRefusedInAutoCadsWords()
        {
            CadDocument document = Walls();
            Assert.True(CadLayerEditor.SetCurrent(document, "TRIM").Succeeded);
            CadCommandSession session = Run(Session(document), "LAYMRG", new[] { "A4" });
            Assert.Equal("Cannot merge current layer.", session.History.Last());
            Assert.Equal("Select object on layer to merge or [Name]:", session.PromptText);

            Run(session, "Name", "NOPE");
            Assert.Equal("Invalid Layer name.", session.History.Last());
            Assert.Equal("Enter layer name or [?]:", session.PromptText);

            Run(session, "DOOR", Enter, new[] { "A3" });
            Assert.Equal("Cannot merge a layer with itself. Layer DOOR is already selected.", session.History.Last());
            Assert.Equal("Select object on target layer or [Name]:", session.PromptText);
        }

        [Fact]
        public void UndoTakesTheLastLayerOutOfTheList_AndAnEnterWithNothingPickedEndsTheCommand()
        {
            CadDocument document = Walls();

            CadCommandSession session = Run(Session(document), "LAYMRG", new[] { "A2" }, new[] { "A4" });
            Assert.Equal("Selected layers: DOOR,TRIM.", session.History.Last());

            Run(session, "Undo");
            Assert.Equal("Selected layers: DOOR.", session.History.Last());

            Assert.False(Run(Session(document), "LAYMRG", Enter).IsRunning);
        }

        [Fact]
        public void TheQuestionMarkOfNameListsTheLayersThatMatch_OneOnALine()
        {
            CadDocument document = Walls();

            CadCommandSession session = Run(Session(document), "LAYMRG", "Name", "?", "*");

            Assert.Equal(new[] { "0", "DOOR", "TRIM", "WALL" }, session.History.Skip(session.History.Count - 4));

            Run(session, "?", "NO*");
            Assert.Equal("No matching names found.", session.History.Last());
        }

        [Fact]
        public void LayerZeroAndALayerOfAnExternalReference_AreNotMerged()
        {
            CadDocument document = Walls();
            Assert.True(CadLayerEditor.SetCurrent(document, "TRIM").Succeeded);

            CadEditResult zero = CadLayerEditor.Merge(document, new[] { "0" }, "WALL");
            CadEditResult missing = CadLayerEditor.Merge(document, new[] { "DOOR" }, "NOPE");
            CadEditResult self = CadLayerEditor.Merge(document, new[] { "DOOR", "WALL" }, "WALL");
            CadEditResult none = CadLayerEditor.Merge(document, new string[0], "WALL");

            Assert.False(zero.Succeeded);
            Assert.Equal("Layer \"0\" cannot be merged: layer 0 and Defpoints are kept.", zero.Message);
            Assert.Equal("Cannot find layer \"NOPE\".", missing.Message);
            Assert.StartsWith("Cannot merge a layer with itself.", self.Message);
            Assert.Equal("No layer is selected.", none.Message);
            Assert.True(document.Database.LayerTable.Has("DOOR"));
        }

        [Fact]
        public void LaydelOfDoor_ErasesTheObjectsOnItInTheDrawingAndInBlocks_AndTheLayer()
        {
            CadDocument document = Walls();
            CadCommandSession session = Run(Session(document), "LAYDEL");
            Assert.Equal("Select object on layer to delete or [Name]:", session.PromptText);

            Run(session, new[] { "A2" }, Enter);
            Assert.Contains("You are about to delete layer \"DOOR\" from this drawing.", session.History);
            Assert.Equal("Do you wish to continue? [Yes/No] <No>:", session.PromptText);

            Run(session, "Yes");

            Assert.False(document.Database.LayerTable.Has("DOOR"));
            Assert.Null(document.FindEntity("A2"));
            Assert.Null(document.FindEntity("A3"));
            Assert.NotNull(document.FindEntity("A1"));
            Assert.Equal(new[] { "WALL", "0" }, InBlock(document, "WIN"));
            Assert.Equal("Deleting layer \"DOOR\".", session.History[session.History.Count - 2]);
            Assert.Equal("1 layer deleted.", session.History.Last());
        }

        [Fact]
        public void LaydelOfTwoLayers_ListsThemInTheWarning_AndIsOneUndoStep()
        {
            CadDocument document = Walls();

            CadCommandSession session = Run(Session(document), "-LAYDEL", new[] { "A2" }, "Name", "TRIM", Enter);
            int at = session
                .History.ToList()
                .IndexOf("You are about to delete the following layers from this drawing.");
            Assert.True(at > 0);
            Assert.Equal(new[] { "DOOR", "TRIM" }, session.History.Skip(at + 1).Take(2));

            Run(session, "Yes");
            Assert.Equal("2 layers deleted.", session.History.Last());
            Assert.Null(document.FindEntity("A4"));

            Run(session, "U");
            Assert.NotNull(document.FindEntity("A4"));
            Assert.NotNull(document.FindEntity("A2"));
            Assert.True(document.Database.LayerTable.Has("TRIM"));
            Assert.Equal(new[] { "DOOR", "WALL", "0" }, InBlock(document, "WIN"));
        }

        [Fact]
        public void LaydelRefusesTheCurrentLayerAndLayerZero_InAutoCadsWords()
        {
            CadDocument document = Walls();
            Assert.True(CadLayerEditor.SetCurrent(document, "WALL").Succeeded);

            CadCommandSession session = Run(Session(document), "LAYDEL", new[] { "A1" });
            Assert.Equal("Cannot delete current layer.", session.History.Last());

            Run(session, "Name", "0");
            Assert.Equal("Cannot delete layer \"0\".", session.History.Last());
            Assert.Equal("Select object on layer to delete or [Name]:", session.PromptText);
        }

        [Fact]
        public void LaydelDeletesALayerAViewportFreezes_ThawingItThereFirst_AndMergeDoesToo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Assert.True(CadLayerEditor.Add(document, "DIM").Succeeded);
            Assert.True(CadLayerEditor.Add(document, "OLD").Succeeded);
            Assert.True(CadLayerEditor.SetCurrent(document, "OLD").Succeeded);
            Run(session, "LINE", P(0, 0), P(2000, 1000), Enter);
            Assert.True(CadLayerEditor.SetCurrent(document, "0").Succeeded);
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            DxfViewport viewport = All<DxfViewport>(document.CurrentSpace).Single(v => v.Number != 1);
            Assert.True(
                CadViewportLayerEditor
                    .SetFrozen(document, new[] { viewport.Handle }, new[] { "DIM", "OLD" }, true)
                    .Succeeded
            );
            Assert.False(CadLayerEditor.Delete(document, "DIM").Succeeded);

            Run(session, "LAYDEL", "Name", "DIM", Enter, "Yes");
            Assert.False(document.Database.LayerTable.Has("DIM"));
            Assert.Equal("1 layer deleted.", session.History.Last());

            Run(session, "LAYMRG", "Name", "OLD", Enter, "Name", "0", "Yes");
            Assert.False(document.Database.LayerTable.Has("OLD"));
            Assert.Equal("1 layer deleted.", session.History.Last());
            Assert.Equal("0", All<DxfLine>(document.ModelSpace).Single().Layer);
        }
    }
}
