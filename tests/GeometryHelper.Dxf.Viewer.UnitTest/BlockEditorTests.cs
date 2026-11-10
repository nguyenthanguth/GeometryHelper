using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The Block Editor, as AutoCAD 2021's BEDIT: a block opened alone, drawn and modified in, saved (every INSERT drawn anew),
    /// saved as another block, or closed with its changes discarded; &lt;Current Drawing&gt; opening model space itself; the
    /// commands that leave the block refused; undo stepping inside the session and taking the whole of it back after.
    /// </summary>
    public class BlockEditorTests
    {
        /// <summary>A metric drawing with the block DOOR (a sill 900 long from the origin) placed twice, at (0, 0) and (2 000, 0).</summary>
        private static CadDocument Doors()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var door = new DxfBlockTableRecord { Name = "DOOR", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(door);
            door.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(900, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), door));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(2000, 0, 0), door));
            return CadDocument.From(database, "doors.dxf");
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static int Lines(CadDocument document, string block) => document.Database.BlockTable[block].Count(e => e is DxfLine && !e.IsErased);

        /// <summary>The items model space draws for its first INSERT: one per line of the block as it was last drawn.</summary>
        private static int DrawnOfFirstDoor(CadDocument document)
        {
            string insert = All<DxfBlockReference>(document.ModelSpace)[0].Handle;
            return document.GetDisplay(document.ModelSpace).Items.Count(i => i.Handle == insert);
        }

        [Fact]
        public void ALineDrawnInTheBlockAndSaved_RedefinesIt_AndEveryInsertDrawsIt_OneUndoStepTakingTheSessionBack()
        {
            CadDocument document = Doors();
            string start = document.Database.ToText();
            int before = DrawnOfFirstDoor(document);
            CadCommandSession session = Start(document, "-BEDIT", "DOOR");

            Assert.Equal("DOOR", document.BlockEditor.Name);
            Assert.True(document.CurrentSpace.IsBlockEditor);
            Assert.Same(document.Database.BlockTable["DOOR"], document.CurrentSpace.Block);
            Assert.Contains("Block Editor opened: DOOR (1 object)", session.History);
            Assert.Single(document.GetDisplay(document.CurrentSpace).Items.Select(i => i.Handle).Distinct());

            Run(session, "LINE", P(0, 0), P(0, 2100), Enter);

            Assert.Equal(2, Lines(document, "DOOR"));
            Assert.Equal(2, document.GetDisplay(document.CurrentSpace).Items.Select(i => i.Handle).Distinct().Count());
            Assert.True(document.BlockEditor.HasUnsavedChanges);

            Run(session, "BSAVE");

            Assert.False(document.BlockEditor.HasUnsavedChanges);
            Run(session, "BCLOSE");
            Assert.Null(document.BlockEditor);
            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Assert.False(document.ModelSpace.IsBlockEditor);
            Assert.True(DrawnOfFirstDoor(document) > before);
            Assert.Equal(2, Lines(document, "DOOR"));
            AssertReopens(document);

            Assert.Equal("BEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void BcloseDiscardingTheChanges_LeavesTheDrawingAsItWas_WithNoUndoStep()
        {
            CadDocument document = Doors();
            string start = document.Database.ToText();
            CadCommandSession session = Start(document, "-BEDIT", "DOOR", "LINE", P(0, 0), P(0, 2100), Enter, "ERASE", P(450, 0), Enter, "BCLOSE");

            Assert.Equal("Save changes to DOOR? [Save/Discard/Cancel] <Save>:", session.PromptText);
            Run(session, "D");

            Assert.Null(document.BlockEditor);
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
            Assert.False(document.IsModified);
        }

        [Fact]
        public void BcloseCancelled_KeepsTheEditorOpen_WithItsChanges()
        {
            CadDocument document = Doors();
            CadCommandSession session = Start(document, "-BEDIT", "DOOR", "LINE", P(0, 0), P(0, 2100), Enter, "BCLOSE", "C");

            Assert.NotNull(document.BlockEditor);
            Assert.Equal(2, Lines(document, "DOOR"));
            Assert.True(document.BlockEditor.HasUnsavedChanges);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void BsaveAs_CopiesTheContentToANewBlock_TheOneOpenedLeftAsItWas_AndTheEditorGoesOnWithTheNewOne()
        {
            CadDocument document = Doors();
            string start = document.Database.ToText();
            CadCommandSession session = Start(document, "-BEDIT", "DOOR", "LINE", P(0, 0), P(0, 2100), Enter, "BSAVEAS", "DOOR_OPEN");

            Assert.Equal(1, Lines(document, "DOOR"));
            Assert.Equal(2, Lines(document, "DOOR_OPEN"));
            Assert.Equal("DOOR_OPEN", document.BlockEditor.Name);
            Assert.Same(document.Database.BlockTable["DOOR_OPEN"], document.CurrentSpace.Block);
            Assert.False(document.BlockEditor.HasUnsavedChanges);

            Run(session, "BCLOSE");
            Assert.Null(document.BlockEditor);
            AssertReopens(document);
            Assert.Equal("BEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void BsaveAsOfANameTaken_IsRefused_AndChangesNothing()
        {
            CadDocument document = Doors();
            Start(document, "-B", "SILL", P(0, 0), Enter);
            CadCommandSession session = Start(document, "-BEDIT", "DOOR", "LINE", P(0, 0), P(0, 2100), Enter, "BSAVEAS", "SILL");

            Assert.Contains("Block \"SILL\" already exists.", session.History);
            Assert.Equal("DOOR", document.BlockEditor.Name);
            Assert.Equal(2, Lines(document, "DOOR"));
        }

        [Fact]
        public void CurrentDrawing_OpensModelSpaceItself_AndSaveWritesTheLineBackToIt()
        {
            CadDocument document = Doors();
            CadCommandSession session = Start(document, "-BEDIT", CadBlockEditor.CurrentDrawing);

            Assert.True(document.BlockEditor.IsCurrentDrawing);
            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Assert.True(document.ModelSpace.IsBlockEditor);

            Run(session, "LINE", P(0, 3000), P(900, 3000), Enter, "BCLOSE", "S");

            Assert.Null(document.BlockEditor);
            Assert.False(document.ModelSpace.IsBlockEditor);
            Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Equal("BEDIT", document.NextUndo.Name);
        }

        [Fact]
        public void CurrentDrawingClosedWithoutSaving_TakesTheLineDrawnOutOfModelSpace()
        {
            CadDocument document = Doors();
            string start = document.Database.ToText();
            Start(document, "-BEDIT", CadBlockEditor.CurrentDrawing, "LINE", P(0, 3000), P(900, 3000), Enter, "BCLOSE", "D");

            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ANameNotInTheDrawing_MakesANewEmptyBlock_WhichClosingWithoutSavingDoesNotKeep()
        {
            CadDocument document = Doors();
            string start = document.Database.ToText();
            CadCommandSession session = Start(document, "-BEDIT", "WINDOW_1200");

            Assert.True(document.Database.BlockTable.Has("WINDOW_1200"));
            Assert.True(document.BlockEditor.IsNew);
            Assert.True(document.BlockEditor.HasUnsavedChanges);
            Assert.Contains("Block Editor opened: WINDOW_1200 (0 objects)", session.History);

            Run(session, "BCLOSE", "D");

            Assert.False(document.Database.BlockTable.Has("WINDOW_1200"));
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ANewBlockDrawnInAndSaved_IsKept()
        {
            CadDocument document = Doors();
            Start(document, "-BEDIT", "WINDOW_1200", "LINE", P(0, 0), P(1200, 0), Enter, "BCLOSE", Enter);

            Assert.Null(document.BlockEditor);
            Assert.Equal(1, Lines(document, "WINDOW_1200"));
            AssertReopens(document);
        }

        [Fact]
        public void CommandsThatLeaveTheBlock_AreRefusedInTheBlockEditor()
        {
            CadDocument document = Doors();
            CadCommandSession session = Start(document, "-BEDIT", "DOOR");

            foreach (string command in new[] { "BEDIT", "-BEDIT", "LAYOUT", "XREF", "PLOT", "MSPACE", "QSAVE" })
            {
                int before = session.History.Count;
                Assert.False(session.Start(command));
                Assert.Equal("Not allowed in the Block Editor.", session.History.Last());
                Assert.True(session.History.Count > before);
                Assert.False(session.IsRunning);
            }

            Assert.Equal("DOOR", document.BlockEditor.Name);
        }

        [Fact]
        public void UndoInsideTheEditor_TakesBackEachEdit_ButNotPastWhereTheBlockWasOpened()
        {
            CadDocument document = Doors();
            Start(document, "-B", "SILL", P(0, 0), Enter);
            CadCommandSession session = Start(document, "-BEDIT", "DOOR", "LINE", P(0, 0), P(0, 2100), Enter, "CIRCLE", P(0, 0), "50");

            Assert.Equal(2, Lines(document, "DOOR"));
            Run(session, "U");
            Assert.Empty(document.Database.BlockTable["DOOR"].OfType<DxfCircle>());
            Run(session, "U");
            Assert.Equal(1, Lines(document, "DOOR"));
            Assert.False(document.BlockEditor.HasUnsavedChanges);
            Run(session, "U");

            Assert.Equal("Everything has been undone", session.History.Last());
            Assert.True(document.Database.BlockTable.Has("SILL"));
            Assert.Equal("DOOR", document.BlockEditor.Name);
            Assert.Same(document.Database.BlockTable["DOOR"], document.CurrentSpace.Block);
        }

        [Fact]
        public void TheEditPicksAndModifiesTheBlocksOwnEntities_AsInModelSpace()
        {
            CadDocument document = Doors();
            CadCommandSession session = Start(document, "-BEDIT", "DOOR", "MOVE", P(450, 0), Enter, P(0, 0), P(0, 100));

            DxfLine sill = Assert.Single(document.Database.BlockTable["DOOR"].OfType<DxfLine>());
            Assert.Equal(100.0, sill.StartPoint.Y, 9);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void BlockPropertiesWithNothingSelected_ShowTheBlockGroup_AndSetItsDescription()
        {
            CadDocument document = Doors();
            Start(document, "-BEDIT", "DOOR");

            CadSelectionInfo info = CadProperties.Describe(document, document.CurrentSpace, null, null);
            CadPropertyGroup block = info.Groups[0];

            Assert.Equal("Block", block.Title);
            Assert.Equal(new[] { "Name", "Description", "Units", "Allow exploding", "Scale uniformly", "Annotative" }, block.Rows.Select(r => r.Label));
            Assert.True(block.Rows[0].IsReadOnly);
            Assert.True(block.Rows[5].IsReadOnly);

            Assert.True(CadPropertyEditor.Set(document, null, "Description", "Single-leaf door, 900 clear opening.").Succeeded);
            Assert.True(CadPropertyEditor.Set(document, null, "Scale uniformly", "Yes").Succeeded);

            Assert.Equal("Single-leaf door, 900 clear opening.", document.Database.BlockTable["DOOR"].Comments);
            Assert.Equal(DxfBlockScaling.Uniform, document.Database.BlockTable["DOOR"].BlockScaling);
            Assert.True(document.BlockEditor.HasUnsavedChanges);
        }

        [Fact]
        public void TheEditBlockDefinitionDialog_IsAskedThroughTheWindow_AndOkOpensTheBlockChosen()
        {
            CadDocument document = Doors();
            CadCommandSession session = Session(document);
            string title = null;
            string offered = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "BEDIT")
                {
                    var asked = (CadBlockEditRequest)e.Payload;
                    title = asked.Title;
                    offered = asked.Name;
                    asked.Name = "DOOR";
                    asked.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "BE");

            Assert.Equal("Edit Block Definition", title);
            Assert.Equal(CadBlockEditor.CurrentDrawing, offered);
            Assert.Equal("DOOR", document.BlockEditor.Name);
        }
    }
}
