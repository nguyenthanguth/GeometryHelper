using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// What C13 left of the block commands: the Enhanced Attribute Editor showing a value as it is typed, Apply keeping it
    /// open and Cancel taking back what was not applied; INSERT dragging the block; -ATTEDIT taking the one attribute
    /// clicked and marking it; ATTSYNC; and INSERT of another drawing's block asking to redefine the drawing's own.
    /// </summary>
    public class BlockEditingTests
    {
        /// <summary>A metric drawing with a circle of radius 10 about (100, 100) and a line from (100, 90) to (100, 70).</summary>
        private static CadDocument Bubble()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(100, 100, 0), new GeoVector3(0, 0, 1), 10.0));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 90, 0), new GeoPoint3(100, 70, 0)));
            return CadDocument.From(database, "blocks.dxf");
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static string[] Handles(CadDocument document) =>
            document.ModelSpace.Block.Select(e => e.Handle).ToArray();

        /// <summary>The block GB of the bubble with the attribute GRID centred on its circle, 3.5 high, base point (100, 70).</summary>
        private static CadDocument WithGridBubble()
        {
            CadDocument document = Bubble();
            Start(document, "-ATT", Enter, "grid", "Grid letter", "A", "J", "MC", P(100, 100), "3.5", "0");
            Start(document, "-B", "GB", P(100, 70), Handles(document), Enter);
            return document;
        }

        private static void UndoesToStart(CadDocument document, string start, string step)
        {
            Assert.Equal(step, document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        private static string Value(CadDocument document) =>
            Assert.Single(All<DxfBlockReference>(document.ModelSpace)).AttributeCollection.Single().TextString;

        /// <summary>Answers the Enhanced Attribute Editor once, as the window would, with what the editor does while it is open.</summary>
        private static void WhileEditing(CadCommandSession session, Action<CadAttributeEditRequest> edit)
        {
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "EATTEDIT")
                {
                    edit((CadAttributeEditRequest)e.Payload);
                    e.Handled = true;
                }
            };
        }

        [Fact]
        public void TheEnhancedAttributeEditor_ShowsAValueAsItIsTyped_KeepsWhatApplyWrote_AndCancelTakesBackTheRest()
        {
            CadDocument document = WithGridBubble();
            Start(document, "-I", "GB", P(0, 0), Enter, Enter, Enter, "C");
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);
            WhileEditing(session, r =>
            {
                r.Rows[0].Value = "D";
                r.Preview();
                Assert.Equal("D", Value(document));
                Assert.Contains(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>(), t =>
                    t.Text == "D"
                );

                r.Apply();
                r.Rows[0].Value = "E";
                r.Rows[0].Height = 0.0;
                r.Preview();
                Assert.Equal("D", Value(document));

                r.Rows[0].Height = 3.5;
                r.Preview();
                Assert.Equal("E", Value(document));
                r.Action = CadDialogAction.Cancel;
            });

            Run(session, "EATTEDIT", P(0, 40));

            Assert.False(session.IsRunning);
            Assert.Equal("D", Value(document));
            Assert.DoesNotContain("Value must be positive and nonzero.", session.History);
            AssertReopens(document);
            UndoesToStart(document, start, "EATTEDIT");
        }

        [Fact]
        public void CancelOfAValueTypedAndNeverApplied_LeavesTheDrawingAsItWas_WithNoUndoStep()
        {
            CadDocument document = WithGridBubble();
            Start(document, "-I", "GB", P(0, 0), Enter, Enter, Enter, "C");
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);
            WhileEditing(session, r =>
            {
                r.Rows[0].Value = "Z";
                r.Preview();
                Assert.Equal("Z", Value(document));
                r.Action = CadDialogAction.Cancel;
            });

            Run(session, "EATTEDIT", P(0, 40));

            Assert.Equal(start, document.Database.ToText());
            Assert.Equal("-INSERT", document.NextUndo.Name);
        }

        [Fact]
        public void InsertDragsTheBlock_ByTheInsertionPointScaleAndRotation_AsTheInsertIsThenPlaced()
        {
            CadDocument document = WithGridBubble();
            CadCommandSession session = Start(document, "-I", "GB");

            CadBlockDrag drag = session.Prompt.Drag;
            Assert.NotNull(drag);
            Assert.Equal("GB", drag.Block);
            Assert.NotEmpty(drag.Items);
            Assert.DoesNotContain(drag.Items, i => i is DisplayText2);

            // The circle's centre, 30 above the base point (100, 70), comes 30 above the cursor.
            Affine2 moved = drag.Transform(P(500, 500));
            Near(P(500, 530), new GeoPoint2(moved.X(100, 100), moved.Y(100, 100)));

            Run(session, P(500, 500));
            Assert.Equal("Enter X scale factor, specify opposite corner, or [Corner/XYZ] <1>:", session.PromptText);
            Affine2 corner = session.Prompt.Drag.Transform(P(502, 501));
            Near(P(520, 530), new GeoPoint2(corner.X(110, 100), corner.Y(110, 100)));

            Run(session, "2", Enter);
            Assert.Equal("Specify rotation angle <0>:", session.PromptText);

            // (110, 100) is (10, 30) from the base point: doubled and turned a quarter, (-60, 20) from (500, 500).
            Affine2 turned = session.Prompt.Drag.Transform(P(500, 510));
            var dragged = new GeoPoint2(turned.X(110, 100), turned.Y(110, 100));
            Near(P(440, 520), dragged);

            Run(session, "90", Enter);

            Assert.False(session.IsRunning);
            GeoPoint3 placed = Assert
                .Single(All<DxfBlockReference>(document.ModelSpace))
                .BlockTransform.Transform(new GeoPoint3(110, 100, 0));
            Near(new GeoPoint3(dragged.X, dragged.Y, 0), placed);
        }

        [Fact]
        public void InsertDragsTheBlockByTheBasePointPicked_AndAsksNoDragBeforeThePointIsKnown()
        {
            CadDocument document = WithGridBubble();
            CadCommandSession session = Start(document, "-I", "GB", "B");

            Assert.Equal("Specify base point:", session.PromptText);
            Assert.Null(session.Prompt.Drag);

            Run(session, P(100, 100));
            Affine2 moved = session.Prompt.Drag.Transform(P(0, 0));
            Near(P(0, 0), new GeoPoint2(moved.X(100, 100), moved.Y(100, 100)));
        }

        /// <summary>
        /// The bubble with a second attribute LEVEL, left on its baseline at (115, 80), made the block GB and inserted at the
        /// origin with the defaults A and L1: GRID centred on (0, 30), LEVEL from (15, 10).
        /// </summary>
        private static CadDocument WithTwoAttributes()
        {
            CadDocument document = Bubble();
            Start(document, "-ATT", Enter, "grid", "Grid letter", "A", "J", "MC", P(100, 100), "3.5", "0");
            Start(document, "-ATT", Enter, "level", "Level", "L1", P(115, 80), "3.5", "0");
            Start(document, "-B", "GB", P(100, 70), Handles(document), Enter);
            Start(document, "-I", "GB", P(0, 0), Enter, Enter, Enter, Enter, Enter);
            return document;
        }

        private static DxfAttributeReference Attribute(CadDocument document, string tag) =>
            Assert.Single(All<DxfBlockReference>(document.ModelSpace)).AttributeCollection.Single(a => a.Tag == tag);

        [Fact]
        public void TypedAttEdit_TakesTheOneAttributeClicked_AndMarksItWhileItIsEdited()
        {
            CadDocument document = WithTwoAttributes();

            CadCommandSession session = Start(document, "-ATE", Enter, Enter, Enter, Enter, P(16.5, 11.5), Enter);

            Assert.Contains("1 attributes selected.", session.History);
            Assert.NotNull(session.Prompt.Mark);
            Near(P(15, 10), session.Prompt.Mark.Value);
            Run(session, "V", "R", "Q");
            Near(P(15, 10), session.Prompt.Mark.Value);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("Q", Attribute(document, "LEVEL").TextString);
            Assert.Equal("A", Attribute(document, "GRID").TextString);
            Assert.Equal("-ATTEDIT", document.NextUndo.Name);
        }

        [Fact]
        public void TypedAttEdit_TakesNoAttributeForAClickOnTheBlocksLines_AndAllOfThemForAWindow()
        {
            CadDocument document = WithTwoAttributes();
            string insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).Handle;

            CadCommandSession session = Start(document, "-ATE", Enter, Enter, Enter, Enter, P(0, 5), Enter);
            Assert.Contains("0 attributes selected.", session.History);
            Assert.False(session.IsRunning);

            session = Start(document, "-ATE", Enter, Enter, Enter, Enter, P(16.5, 11.5), new[] { insert }, Enter);
            Assert.Contains("2 attributes selected.", session.History);
            DxfAttributeReference first = Assert
                .Single(All<DxfBlockReference>(document.ModelSpace))
                .AttributeCollection[0];
            GeoPoint3 held = first.Tag == "GRID" ? first.AlignmentPoint : first.Position;
            Near(new GeoPoint2(held.X, held.Y), session.Prompt.Mark.Value);
        }

        [Fact]
        public void TypedAttEdit_TakesOnlyTheAttributesACrossingBoxTouches_TypedOrDraggedOnTheCanvas()
        {
            CadDocument document = WithTwoAttributes();
            string insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).Handle;

            // The box from (-20, 25) to (20, 35) crosses the circle and GRID, centred on (0, 30), and not LEVEL, 10 to 13.5 high.
            CadCommandSession session = Start(
                document,
                "-ATE",
                Enter,
                Enter,
                Enter,
                Enter,
                "C",
                P(-20, 25),
                P(20, 35),
                Enter
            );
            Assert.Contains("1 attributes selected.", session.History);
            Near(P(0, 30), session.Prompt.Mark.Value);
            Run(session, Esc);

            session = Start(document, "-ATE", Enter, Enter, Enter, Enter);
            Assert.True(session.FeedSelection(new[] { insert }, new DisplayBox(-20, 5, 20, 15), true));
            Run(session, Enter);
            Assert.Contains("1 attributes selected.", session.History);
            Near(P(15, 10), session.Prompt.Mark.Value);
        }

        /// <summary>
        /// GB defined again as AutoCAD's BLOCK would leave it: LEVEL (default L0) at (100, 60) first, then GRID left on its
        /// baseline at (100, 95), 5 high, the circle kept.
        /// </summary>
        private static void Redefine(CadDocument document)
        {
            DxfBlockTableRecord record = document.Database.BlockTable["GB"];

            foreach (DxfEntity old in record.ToList())
            {
                old.Erase();
            }

            record.AppendEntity(new DxfCircle(new GeoPoint3(100, 100, 0), new GeoVector3(0, 0, 1), 10.0));
            record.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(100, 60, 0), "L0", "LEVEL", "Level", null));
            var grid = new DxfAttributeDefinition(new GeoPoint3(100, 95, 0), "A", "GRID", "Grid letter", null);
            record.AppendEntity(grid);
            grid.Height = 5.0;
            document.Regen();
        }

        /// <summary>
        /// GB with GRID inserted at the origin (C, with a stray OLD attribute) and at (200, 0) twice the size (D), and nested in
        /// the block PAIR at (0, 0) (E); then GB defined again.
        /// </summary>
        private static CadDocument Synced()
        {
            CadDocument document = WithGridBubble();
            Start(document, "-I", "GB", P(0, 0), Enter, Enter, Enter, "C");
            Start(document, "-I", "GB", P(200, 0), "2", Enter, Enter, "D");
            DxfBlockReference first = All<DxfBlockReference>(document.ModelSpace)[0];
            first.AttributeCollection.AppendAttribute(
                new DxfAttributeReference(new GeoPoint3(0, 0, 0), "X", "OLD", null)
            );

            var pair = new DxfBlockTableRecord { Name = "PAIR" };
            document.Database.BlockTable.Add(pair);
            var nested = new DxfBlockReference(new GeoPoint3(0, 0, 0), document.Database.BlockTable["GB"]);
            pair.AppendEntity(nested);
            var value = new DxfAttributeReference();
            value.SetAttributeFromBlock(
                document.Database.BlockTable["GB"].OfType<DxfAttributeDefinition>().Single(),
                nested.BlockTransform
            );
            value.TextString = "E";
            nested.AttributeCollection.AppendAttribute(value);

            Redefine(document);

            // Read again, as the file would be: the edits made here past the undo list are then where undo starts.
            return CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "synced.dxf");
        }

        private static void InLine(DxfBlockReference insert, string grid, double scale)
        {
            GeoPoint3 at = insert.Position;
            Assert.Equal(new[] { "LEVEL", "GRID" }, insert.AttributeCollection.Select(a => a.Tag).ToArray());
            DxfAttributeReference level = insert.AttributeCollection[0];
            Assert.Equal("L0", level.TextString);
            Near(new GeoPoint3(at.X, at.Y - (10 * scale), 0), level.Position, 1E-9);
            DxfAttributeReference value = insert.AttributeCollection[1];
            Assert.Equal(grid, value.TextString);
            Near(new GeoPoint3(at.X, at.Y + (25 * scale), 0), value.Position, 1E-9);
            Assert.Equal(5.0 * scale, value.Height, 9);
        }

        [Fact]
        public void AttSyncByName_BringsEveryInsertInLineWithTheBlock_KeepingTheValuesOfTagsKept_AsOneUndoStep()
        {
            CadDocument document = Synced();
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "ATTSYNC");
            Assert.Equal("Enter an option [?/Name/Select] <Select>:", session.PromptText);
            Run(session, "?");
            Assert.Contains("GB", session.History);
            Assert.DoesNotContain("PAIR", session.History);
            Run(session, "N");
            Assert.Equal("Enter name of block to sync or [?]:", session.PromptText);
            Run(session, "gb");

            Assert.False(session.IsRunning);
            Assert.Contains("ATTSYNC complete.", session.History);
            List<DxfBlockReference> inserts = All<DxfBlockReference>(document.ModelSpace);
            InLine(inserts[0], "C", 1.0);
            InLine(inserts[1], "D", 2.0);
            InLine(document.Database.BlockTable["PAIR"].OfType<DxfBlockReference>().Single(), "E", 1.0);
            AssertReopens(document);
            UndoesToStart(document, start, "ATTSYNC");
        }

        /// <summary>
        /// Autodesk's help of ATTSYNC: it "does not change any values assigned to attributes in existing blocks", and it
        /// "removes any format or property changes made with the ATTEDIT or EATTEDIT commands".
        /// </summary>
        [Fact]
        public void AttSync_TakesBackAnAttributeMovedResizedAndRecolouredOnItsInsert_AndKeepsItsValue()
        {
            CadDocument document = Synced();
            DxfBlockReference insert = All<DxfBlockReference>(document.ModelSpace)[0];
            DxfAttributeReference grid = insert.AttributeCollection.Single(a => a.Tag == "GRID");
            grid.Position = new GeoPoint3(500, 500, 0);
            grid.Height = 9.0;
            grid.Rotation = 0.5;
            grid.ColorIndex = 1;

            Start(document, "ATTSYNC", "N", "GB");

            insert = All<DxfBlockReference>(document.ModelSpace)[0];
            InLine(insert, "C", 1.0);
            grid = insert.AttributeCollection.Single(a => a.Tag == "GRID");
            Assert.Equal(0.0, grid.Rotation, 9);
            Assert.Equal(256, grid.ColorIndex);
        }

        [Fact]
        public void AttSyncBySelect_AsksToSyncTheBlockPicked_AndOfABlockWithNoAttributesSaysSo()
        {
            CadDocument document = Synced();
            CadCommandSession session = Start(document, "ATTSYNC", Enter);
            Assert.Equal("Select a block:", session.PromptText);
            Run(session, P(0, 40));
            Assert.Equal("ATTSYNC block GB? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            InLine(All<DxfBlockReference>(document.ModelSpace)[0], "C", 1.0);

            Start(
                document,
                "-B",
                "PLAIN",
                P(0, 0),
                new[] { All<DxfBlockReference>(document.ModelSpace)[1].Handle },
                Enter
            );
            session = Start(document, "ATTSYNC", "N", "PLAIN");
            Assert.Contains("This block has no attribute definitions.", session.History);
        }

        [Fact]
        public void TypedInsertOfAnotherDrawingsBlock_AsksToRedefineTheDrawingsOwn_YesTakingTheirs_NoKeepingIt()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-c13b-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                string file = Path.Combine(folder, "bubbles.dxf");
                WithGridBubble().Database.Save(file);

                CadDocument kept = Bubble();
                Start(kept, "-B", "GB", P(0, 0), new[] { All<DxfLine>(kept.ModelSpace).Single().Handle }, Enter);
                CadCommandSession session = Start(kept, "-I", "GB=" + file);
                Assert.Equal("Block \"GB\" already exists. Redefine it? [Yes/No] <No>:", session.PromptText);
                Run(session, Enter, P(10, 20), Enter, Enter, Enter);
                Assert.IsType<DxfLine>(Assert.Single(kept.Database.BlockTable["GB"]));

                CadDocument document = Bubble();
                Start(
                    document,
                    "-B",
                    "GB",
                    P(0, 0),
                    new[] { All<DxfLine>(document.ModelSpace).Single().Handle },
                    Enter
                );
                string start = document.Database.ToText();
                session = Start(document, "-I", "GB=" + file, "Y", P(10, 20), Enter, Enter, Enter, "D");

                Assert.False(session.IsRunning);
                DxfBlockTableRecord block = document.Database.BlockTable["GB"];
                Assert.Equal(3, block.Count);
                Assert.Equal(new GeoPoint3(100, 70, 0), block.Origin);
                DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
                Assert.Equal("D", insert.AttributeCollection.Single().TextString);
                Near(new GeoPoint3(10, 50, 0), insert.BlockTransform.Transform(new GeoPoint3(100, 100, 0)), 1E-9);
                AssertReopens(document);
                UndoesToStart(document, start, "-INSERT");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void ThePaletteAsksTheWindowToRedefine_AndItsYesRedefinesTheBlock()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-c13b-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                string file = Path.Combine(folder, "bubbles.dxf");
                WithGridBubble().Database.Save(file);
                CadDocument document = Bubble();
                Start(
                    document,
                    "-B",
                    "GB",
                    P(0, 0),
                    new[] { All<DxfLine>(document.ModelSpace).Single().Handle },
                    Enter
                );
                CadCommandSession session = Session(document);
                string asked = null;
                session.UiRequested += (sender, e) =>
                {
                    if (e.Payload is CadInsertRequest request)
                    {
                        request.FileName = file;
                        request.Name = "GB";
                        request.SpecifyPointOnScreen = false;
                        request.Point = new GeoPoint3(10, 20, 0);
                        request.Action = CadDialogAction.Ok;
                        e.Handled = true;
                    }
                    else if (e.Name == "REDEFINE")
                    {
                        asked = (string)e.Payload;
                        e.Answer = "Yes";
                        e.Handled = true;
                    }
                };

                Run(session, "INSERT", "D");

                Assert.Equal("GB", asked);
                Assert.False(session.IsRunning);
                Assert.Equal(3, document.Database.BlockTable["GB"].Count);
                Assert.Equal(
                    "D",
                    Assert.Single(All<DxfBlockReference>(document.ModelSpace)).AttributeCollection.Single().TextString
                );
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
