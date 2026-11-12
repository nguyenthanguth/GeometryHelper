using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// LAYERP, Layer Previous, as AutoCAD 2021 words it: the last change or set of changes to the settings of layers undone
    /// without undoing any other edit, one set for a command, a Layer manager session, or a change of the layer box; a layer
    /// added, renamed or deleted left as it is; LAYERPMODE turning the tracking on or off; LAYERP itself one undo step.
    /// </summary>
    public class LayerPreviousTests
    {
        private const string Restored = "Restored previous layer status.";
        private const string Nothing = "*No previous layer status*";

        private static CadCommandSession Start(CadDocument document, params object[] inputs) =>
            Run(Session(document), inputs);

        [Fact]
        public void LayerpWithNothingChanged_SaysThereIsNoPreviousLayerStatus_AndMakesNoStep()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "LAYERP");

            Assert.Equal(Nothing, session.History.Last());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void LayerpPutsBackTheLastChange_AndTheOnesBeforeItOneByOne_TheOtherEditsLeftAlone()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "LAYOFF", new[] { "A1" }, Enter);
            Start(document, "LAYTHW");
            Assert.True(document.Database.LayerTable["Steel"].IsOff);
            Assert.True(CadLayerEditor.SetLocked(document, "Steel", true).Succeeded);
            Assert.True(CadLayerEditor.SetColor(document, "Steel", 5).Succeeded);

            Run(session, "LAYERP");
            Assert.Equal(Restored, session.History.Last());
            Assert.Equal(1, document.Database.LayerTable["Steel"].ColorIndex);
            Assert.True(document.Database.LayerTable["Steel"].IsLocked);
            Assert.True(document.Database.LayerTable["Steel"].IsOff);

            Run(session, "LAYERP");
            Assert.False(document.Database.LayerTable["Steel"].IsLocked);
            Assert.True(document.Database.LayerTable["Steel"].IsOff);

            Run(session, "LAYERP");
            Assert.Equal(Restored, session.History.Last());
            Assert.False(document.Database.LayerTable["Steel"].IsOff);

            Run(session, "LAYERP");
            Assert.Equal(Nothing, session.History.Last());
        }

        [Fact]
        public void AnEditThatIsNotOfLayers_IsNotUndoneByLayerp()
        {
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
            CadCommandSession session = Start(document, "ERASE", new[] { "A3" }, Enter);
            Assert.Empty(All<DxfCircle>(document.ModelSpace));

            Run(session, "LAYERP");

            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Assert.Empty(All<DxfCircle>(document.ModelSpace));
        }

        [Fact]
        public void AWholeCommand_IsOneSet_LayisoTurningOffTwoLayersAndMovingTheCurrentOne()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "LAYISO", new[] { "A1" }, Enter);
            Assert.Equal("Steel", document.Database.Clayer);
            Assert.True(document.Database.LayerTable["0"].IsOff);
            Assert.True(document.Database.LayerTable["Locked"].IsOff);

            Run(session, "LAYERP");

            Assert.Equal(Restored, session.History.Last());
            Assert.Equal("0", document.Database.Clayer);
            Assert.False(document.Database.LayerTable["0"].IsOff);
            Assert.False(document.Database.LayerTable["Locked"].IsOff);
            Run(session, "LAYERP");
            Assert.Equal(Nothing, session.History.Last());
        }

        [Fact]
        public void ACommandThatChangesALayerAndChangesItBack_LeavesNoSet()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "LAYOFF", new[] { "A1" }, "U", Enter, "LAYERP");

            Assert.Equal(Nothing, session.History.Last());
        }

        [Fact]
        public void ALayerManagerSession_IsOneSet_WhateverNumberOfCellsWereChanged()
        {
            CadDocument document = Plan();
            document.BeginLayerSet();
            Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
            Assert.True(CadLayerEditor.SetColor(document, "Steel", 5).Succeeded);
            Assert.True(CadLayerEditor.SetFrozen(document, "Locked", true).Succeeded);
            document.EndLayerSet();
            Assert.True(document.CanUndo);

            CadCommandSession session = Start(document, "LAYERP");

            Assert.Equal(Restored, session.History.Last());
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Assert.Equal(1, document.Database.LayerTable["Steel"].ColorIndex);
            Assert.False(document.Database.LayerTable["Locked"].IsFrozen);
            Run(session, "LAYERP");
            Assert.Equal(Nothing, session.History.Last());
        }

        [Fact]
        public void TheCurrentLayerSetByTheLayerBox_IsPutBack()
        {
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.SetCurrent(document, "Steel").Succeeded);

            Start(document, "LAYERP");

            Assert.Equal("0", document.Database.Clayer);
        }

        [Fact]
        public void ALayerAddedRenamedOrDeleted_IsNotUndone_AndAPreviousCurrentLayerThatIsGone_IsSaidAndSkipped()
        {
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.Add(document, "B").Succeeded);
            Assert.True(CadLayerEditor.Add(document, "C").Succeeded);
            Assert.True(CadLayerEditor.SetCurrent(document, "B").Succeeded);
            Assert.True(CadLayerEditor.SetCurrent(document, "C").Succeeded);
            Assert.True(CadLayerEditor.Rename(document, "Steel", "Beam").Succeeded);
            Assert.True(CadLayerEditor.Delete(document, "B").Succeeded);
            CadCommandSession session = Start(document, "LAYERP");

            Assert.Equal("C", document.Database.Clayer);
            Assert.Contains(
                "*Note* The previously current layer was not set because it no longer exists.",
                session.History
            );
            Assert.Contains(Restored, session.History);
            Assert.True(document.Database.LayerTable.Has("Beam"));
            Assert.True(document.Database.LayerTable.Has("C"));

            Run(session, "LAYERP");
            Assert.Equal("0", document.Database.Clayer);
        }

        [Fact]
        public void LayerpThatLeavesTheCurrentLayerOff_Warns()
        {
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.SetOn(document, "0", false).Succeeded);
            Assert.True(CadLayerEditor.SetCurrent(document, "Steel").Succeeded);
            Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
            CadCommandSession session = Start(document, "LAYERP");

            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Run(session, "LAYERP");
            Assert.Equal("0", document.Database.Clayer);
            Assert.Contains("*Warning* The current layer is turned off.", session.History);
        }

        [Fact]
        public void LayerpIsOneUndoStep_UndoBringsTheChangeBack_AndLayerpCanTakeItBackAgain()
        {
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
            CadCommandSession session = Start(document, "LAYERP");
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Assert.Equal("LAYERP", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.True(document.Database.LayerTable["Steel"].IsOff);

            Run(session, "LAYERP");
            Assert.Equal(Restored, session.History.Last());
            Assert.False(document.Database.LayerTable["Steel"].IsOff);

            Assert.True(document.Undo());
            Assert.True(document.Redo());
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Run(session, "LAYERP");
            Assert.Equal(Nothing, session.History.Last());
        }

        [Fact]
        public void ALayerEditTakenBackWithU_IsTakenOutOfTheSets_AndPutInAgainByRedo()
        {
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
            Assert.True(document.Undo());

            CadCommandSession session = Start(document, "LAYERP");
            Assert.Equal(Nothing, session.History.Last());

            Assert.True(document.Redo());
            Assert.True(document.Database.LayerTable["Steel"].IsOff);
            Run(session, "LAYERP");
            Assert.Equal(Restored, session.History.Last());
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
        }

        [Fact]
        public void LayerpmodeAsksOnOrOff_WithTheModeAsDefault_AndWithItOffLayerpSaysSo_AndNothingIsKept()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "LAYERPMODE");
            Assert.Equal("Enter LAYERP mode [ON/OFF] <ON>:", session.PromptText);
            Run(session, "OFF");
            Assert.False(document.LayerPreviousMode);

            Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
            Run(session, "LAYERP");
            Assert.Equal("Layer-Previous is disabled. Use LAYERPMODE to turn it on. ", session.History.Last());
            Assert.True(document.Database.LayerTable["Steel"].IsOff);

            Run(session, "LAYERPMODE");
            Assert.Equal("Enter LAYERP mode [ON/OFF] <OFF>:", session.PromptText);
            Run(session, "ON");
            Assert.True(document.LayerPreviousMode);
            Run(session, "LAYERP");
            Assert.Equal(Nothing, session.History.Last());

            Run(session, "LAYERPMODE", Enter);
            Assert.True(document.LayerPreviousMode);
        }

        [Fact]
        public void TheDigitAliasesOfAcadPgp_RunLayerp_Layiso_Layon_AndLayoff()
        {
            Assert.Equal("LAYISO", CadShortcuts.Default.Aliases["1"]);
            Assert.Equal("LAYERP", CadShortcuts.Default.Aliases["2"]);
            Assert.Equal("LAYON", CadShortcuts.Default.Aliases["22"]);
            Assert.Equal("LAYOFF", CadShortcuts.Default.Aliases["3"]);

            CadDocument document = Plan();
            CadCommandSession session = Start(document, "3", new[] { "A1" }, Enter);
            Assert.True(document.Database.LayerTable["Steel"].IsOff);

            Run(session, "2");

            Assert.Equal(Restored, session.History.Last());
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
        }
    }
}
