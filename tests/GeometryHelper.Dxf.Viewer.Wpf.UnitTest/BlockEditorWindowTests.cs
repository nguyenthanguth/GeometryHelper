using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Block Editor in the window: the Edit Block Definition dialog's list and answers, the green Block Editor tab with the
    /// one Model tab while a block is open, its label and status, BCLOSE's question answered by the window, and the Block
    /// Editor's own background setting.
    /// </summary>
    public sealed class BlockEditorWindowTests
    {
        /// <summary>A drawing with a layout, the blocks DOOR_900 (a sill and a described leaf) and GRID_BUBBLE (an attribute), DOOR_900 placed once.</summary>
        private static CadDocument Plan()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var door = new DxfBlockTableRecord
            {
                Name = "DOOR_900",
                Origin = new GeoPoint3(0, 0, 0),
                Comments = "Single-leaf door, 900 clear opening.",
            };
            database.BlockTable.Add(door);
            door.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(900, 0, 0)));
            door.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 900, 0)));
            var bubble = new DxfBlockTableRecord { Name = "GRID_BUBBLE", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(bubble);
            bubble.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5.0));
            bubble.AppendEntity(
                new DxfAttributeDefinition(
                    new GeoPoint3(0, 0, 0),
                    "A",
                    "GRID",
                    "Grid letter",
                    (DxfTextStyleTableRecord)null
                )
            );
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 0, 0), door));
            return CadDocument.From(database, "plan-level2.dxf");
        }

        private static MainViewModel Open(CadDocument document)
        {
            var model = new MainViewModel();
            model.OpenDocument(document);
            return model;
        }

        [Fact]
        public void TheDialog_ListsCurrentDrawingThenTheNamedBlocks_WithTheirTags_AndShowsTheDescriptionOfTheOneChosen()
        {
            CadDocument document = Plan();
            var dialog = new BlockEditViewModel(new CadBlockEditRequest(document, "Edit Block Definition", "DOOR_900"));

            Assert.Equal(
                new[] { CadBlockEditor.CurrentDrawing, "DOOR_900", "GRID_BUBBLE" },
                dialog.Rows.Select(r => r.Name)
            );
            Assert.Equal("attributes", dialog.Rows[2].Tag);
            Assert.Equal("DOOR_900", dialog.Selected.Name);
            Assert.Equal("Single-leaf door, 900 clear opening.", dialog.Description);
            Assert.True(dialog.CanOk);

            dialog.Name = "WINDOW*";
            Assert.False(dialog.CanOk);
            Assert.Null(dialog.Selected);

            dialog.Selected = dialog.Rows[0];
            Assert.Equal(CadBlockEditor.CurrentDrawing, dialog.Name);
            Assert.Equal(string.Empty, dialog.Description);
            Assert.True(dialog.CanOk);
        }

        [Fact]
        public void SaveBlockAs_ListsNoCurrentDrawing_AndRefusesANameTaken()
        {
            CadDocument document = Plan();
            var dialog = new BlockEditViewModel(new CadBlockEditRequest(document, "Save Block As", string.Empty));

            Assert.Equal(new[] { "DOOR_900", "GRID_BUBBLE" }, dialog.Rows.Select(r => r.Name));
            Assert.Equal("Block name", dialog.NameLabel);
            dialog.Name = "DOOR_900";
            Assert.False(dialog.CanOk);
            dialog.Name = "DOOR_1000";
            Assert.True(dialog.CanOk);
        }

        [Fact]
        public void BeditThroughTheDialog_ShowsTheGreenTab_OneModelTab_AndTheLabel_AndBcloseGivesTheLayoutsBack()
        {
            CadDocument document = Plan();
            MainViewModel model = Open(document);
            int tabs = model.SpaceTabs.Count;
            model.ShowBlockEdit = dialog =>
            {
                dialog.Name = "DOOR_900";
                dialog.Close(CadDialogAction.Ok);
                return true;
            };

            model.StartCommand("BEDIT");

            Assert.True(model.IsBlockEditing);
            Assert.True(model.BlockEditorTab.IsTabVisible);
            Assert.Same(model.BlockEditorTab, model.SelectedTab);
            Assert.Equal("Model", Assert.Single(model.SpaceTabs).Name);
            Assert.Equal("[-][Block Editor: DOOR_900][2D Wireframe]", model.ViewportLabel);
            Assert.Equal("Block Editor: DOOR_900", model.StatusText);

            model.StartCommand("LINE");
            model.Execute("0,0");
            model.Execute("450,450");
            model.Execute(string.Empty);

            Assert.Equal("Unsaved changes to DOOR_900", model.StatusText);

            string asked = null;
            model.AskSaveBlockChanges = name =>
            {
                asked = name;
                return "Save";
            };

            ((BlockEditorRibbonViewModel)model.BlockEditorTab.TabContent).CloseCommand.Execute(null);

            Assert.Equal("DOOR_900", asked);
            Assert.False(model.IsBlockEditing);
            Assert.False(model.BlockEditorTab.IsTabVisible);
            Assert.True(model.IsHomeTabSelected);
            Assert.Equal(tabs, model.SpaceTabs.Count);
            Assert.Equal(3, document.Database.BlockTable["DOOR_900"].Count());
        }

        [Fact]
        public void BlockProperties_LetsTheSelectionGo_AndPropertiesShowsTheBlockGroup()
        {
            CadDocument document = Plan();
            MainViewModel model = Open(document);
            model.Execute("-BEDIT");
            model.Execute("DOOR_900");

            ((BlockEditorRibbonViewModel)model.BlockEditorTab.TabContent).BlockPropertiesCommand.Execute(null);

            Assert.Equal("Block", model.PropertyGroups[0].Title);
            Assert.False(((BlockEditorRibbonViewModel)model.BlockEditorTab.TabContent).HasVisibilityStates);
        }

        [Fact]
        public void TheScreenshotModesStub_TakesEveryDialogHookOff_SoSaveAsAndBeditAskOnTheCommandLine()
        {
            MainViewModel model = Open(Plan());
            bool shown = false;
            model.SaveAsRequested += (sender, e) => shown = true;
            model.OpenRequested += (sender, e) => shown = true;
            model.ShowBlockEdit = dialog => shown = true;
            model.AskSaveBlockChanges = name =>
            {
                shown = true;
                return "Save";
            };
            model.ShowBlockDefinition = dialog => shown = true;

            model.StubDialogs();
            model.Execute("SAVEAS");

            Assert.StartsWith("Enter file format [", model.Prompt, StringComparison.Ordinal);
            Assert.EndsWith("/Dxf/Template/Standards] <Dxf>:", model.Prompt, StringComparison.Ordinal);
            model.Escape();
            model.OpenCommand.Execute(null);
            model.Execute("BEDIT");
            Assert.Equal("Enter block name or [?]:", model.Prompt);
            Assert.Null(model.ShowBlockEdit);
            Assert.Null(model.AskSaveBlockChanges);
            Assert.False(shown);
        }

        [Fact]
        public void TheBlockEditorBackground_IsAGlobalSettingOfItsOwn_3B4049ByDefault_AndReadBackFromItsJson()
        {
            var settings = new GlobalSettings();
            Assert.Equal(0x3B4049, settings.BlockEditorBackground);

            settings.BlockEditorBackground = 0x202020;
            string path = System.IO.Path.GetTempFileName();

            try
            {
                System.IO.File.WriteAllText(path, settings.ToJson());
                Assert.Equal(0x202020, GlobalSettings.Load(path).BlockEditorBackground);
            }
            finally
            {
                System.IO.File.Delete(path);
            }

            Assert.Equal(0x3B4049, new CanvasOptions().BlockEditorBackground);
        }
    }
}
