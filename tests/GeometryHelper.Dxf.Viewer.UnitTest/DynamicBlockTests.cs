using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Dynamic blocks in use, as AutoCAD 2021 drives them: the custom grips of a dynamic INSERT selected, a grip dragged or
    /// clicked, the list of a visibility grip, the Custom group of Properties, RESETBLOCK, each one undo step, the block
    /// worked out anew into an anonymous block still named by its dynamic block. The blocks are made in code as AutoCAD saves
    /// them (see the core's dynamic block tests).
    /// </summary>
    public class DynamicBlockTests
    {
        /// <summary>
        /// A drawing of R2018 with the block GH_PANEL, a rectangle 100 by 50 whose Width from (0, 0) to (100, 0), a list of 100,
        /// 150 and 200, stretches its right edge, and an INSERT of it at (1 000, 500) on layer Doors.
        /// </summary>
        private static CadDocument Panel(
            out string handle,
            Action<DxfBlockTableRecord, DxfBlockLinearParameter> more = null
        )
        {
            var db = new DxfDatabase("AC1032");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Doors" });
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine[] lines =
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            more?.Invoke(block, width);
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block) { Layer = "Doors" };
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(db, "panel.dxf");
        }

        /// <summary>A drawing with the block GH_DOOR, a leaf from (0, 0) to (90, 0) flipped by Hand about the y axis, inserted at the origin.</summary>
        private static CadDocument Door(out string handle)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_DOOR", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var leaf = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0));
            block.AppendEntity(leaf);
            DxfBlockFlipParameter hand = block.AddFlipParameter(
                "Hand",
                "Which way the door opens",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(0, 50, 0)
            );
            block.AddFlipAction(hand, new[] { leaf });
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(db, "door.dxf");
        }

        /// <summary>A drawing with the block GH_SIGN, a frame, a cross and a tick, of the states Cross, Tick and Blank, inserted at the origin.</summary>
        private static CadDocument Sign(out string handle)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_SIGN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine[] lines =
            {
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0)),
                new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            block.AddVisibilityParameter(
                "Mark",
                "What the sign shows",
                new GeoPoint3(20, -10, 0),
                new[]
                {
                    new DxfBlockVisibilityState("Cross", new DxfEntity[] { lines[1] }),
                    new DxfBlockVisibilityState("Tick", new DxfEntity[] { lines[2] }),
                    new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
                }
            );
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(db, "sign.dxf");
        }

        /// <summary>A drawing with the block GH_ARROW, a line from (50, 25) to (80, 25) turned by Angle about (50, 25), inserted at the origin.</summary>
        private static CadDocument Arrow(out string handle)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_ARROW", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var arrow = new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0));
            block.AppendEntity(arrow);
            DxfBlockRotationParameter angle = block.AddRotationParameter(
                "Angle",
                "Turn of the arrow",
                new GeoPoint3(50, 25, 0),
                30,
                0
            );
            block.AddRotateAction(angle, new[] { arrow });
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            return CadDocument.From(db, "arrow.dxf");
        }

        private static DxfBlockReference Insert(CadDocument document, string handle) =>
            Entity<DxfBlockReference>(document, handle);

        private static object ValueOf(CadDocument document, string handle, string name) =>
            Insert(document, handle).DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name).Value;

        private static CadPropertyGroup Group(CadDocument document, string handle, string title) =>
            CadProperties
                .Describe(document, document.ModelSpace, new[] { handle })
                .Groups.SingleOrDefault(g => g.Title == title);

        private static CadPropertyRow Row(CadPropertyGroup group, string label) =>
            group.Rows.Single(r => r.Label == label);

        [Fact]
        public void ADynamicInsert_HasItsCustomGripsBeforeItsInsertionPoint_AsAutoCadGivesThem()
        {
            CadDocument document = Panel(out string handle);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));

            Assert.Equal(new[] { CadGripKind.CustomLinear, CadGripKind.Insertion }, grips.Select(g => g.Kind));
            Near(P(1100, 500), grips[0].Point);
            Assert.Equal(1.0, grips[0].Direction.X, 9);
            Assert.Equal(0.0, grips[0].Direction.Y, 9);
            Assert.Equal(new[] { "Width" }, grips[0].Custom.PropertyNames);
            Assert.Null(grips[1].Custom);
        }

        [Fact]
        public void TheWidthGripDragged_SetsTheWidthToTheNearestOfItsList_AsOneUndoStep()
        {
            CadDocument document = Panel(out string handle);

            CadEditResult result = document.GripDrag(handle, 0, P(1180, 530));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(200.0, (double)ValueOf(document, handle, "Width"), 9);
            DxfBlockReference insert = Insert(document, handle);
            Assert.True(insert.BlockTableRecord.IsAnonymous);
            Assert.Equal("GH_PANEL", insert.DynamicBlockTableRecord.Name);
            Assert.Contains(insert.BlockTableRecord.OfType<DxfLine>(), l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(200, 0, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(200, 50, 0), Tight)
            );
            Near(P(1200, 500), CadGrips.Of(insert)[0].Point);
            Assert.Equal("GRIP", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(100.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal("GH_PANEL", Insert(document, handle).Name);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AGripClickedAndAPointPicked_StretchesTheWidth_ThroughAutoCadsGripEdit()
        {
            CadDocument document = Panel(out string handle);
            document.Selection.Set(new[] { handle });
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripEdit(new[] { new KeyValuePair<string, int>(handle, 0) }));
            Assert.Equal("** STRETCH **", session.History.Last());
            Run(session, P(1160, 400));

            Assert.False(session.IsRunning);
            Assert.Equal(150.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal("GRIP", document.NextUndo.Name);
        }

        [Fact]
        public void AGripDraggedBehindItsBasePoint_IsRefused_AndOneOnALockedLayerToo()
        {
            CadDocument document = Panel(out string handle);
            string before = document.Database.ToText();

            Assert.False(document.GripDrag(handle, 0, P(900, 500)).Succeeded);

            document.Database.LayerTable["Doors"].IsLocked = true;
            Assert.False(document.GripDrag(handle, 0, P(1150, 500)).Succeeded);
            Assert.False(CadDynamicBlocks.SetProperty(document, new[] { handle }, "Width", "150").Succeeded);
            document.Database.LayerTable["Doors"].IsLocked = false;
            Assert.Equal(before, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TheCustomGroup_ListsTheWidthTyped_AndNamesTheInsertByItsDynamicBlock()
        {
            CadDocument document = Panel(out string handle);

            CadPropertyGroup custom = Group(document, handle, CadProperties.CustomGroup);
            Assert.NotNull(custom);
            Assert.Equal("100.0000", Row(custom, "Width").Value);
            Assert.Null(Row(custom, "Width").Choices);
            Assert.False(Row(custom, "Width").IsReadOnly);

            CadEditResult result = CadDynamicBlocks.SetProperty(document, new[] { handle }, "Width", "170");

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(150.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal("150.0000", Row(Group(document, handle, CadProperties.CustomGroup), "Width").Value);
            Assert.Equal("GH_PANEL", Row(Group(document, handle, "Misc"), "Name").Value);
            Assert.StartsWith("*U", Insert(document, handle).Name, StringComparison.Ordinal);
            Assert.Equal("Properties", document.NextUndo.Name);

            Assert.False(CadDynamicBlocks.SetProperty(document, new[] { handle }, "Width", "wide").Succeeded);
            Assert.False(CadDynamicBlocks.SetProperty(document, new[] { handle }, "Depth", "10").Succeeded);
        }

        [Fact]
        public void TheFlipGripClicked_FlipsTheDoor_AndTheCustomGroupNamesItsStates()
        {
            CadDocument document = Door(out string handle);
            CadGrip flip = CadGrips.Of(Insert(document, handle))[0];
            Assert.Equal(CadGripKind.CustomFlip, flip.Kind);

            CadPropertyRow row = Row(Group(document, handle, CadProperties.CustomGroup), "Hand");
            Assert.Equal("Not flipped", row.Value);
            Assert.Equal(new[] { "Not flipped", "Flipped" }, row.Choices);

            Assert.True(CadDynamicBlocks.Click(document, handle, 0).Succeeded);

            Assert.Equal((short)1, ValueOf(document, handle, "Hand"));
            Assert.Contains(Insert(document, handle).BlockTableRecord.OfType<DxfLine>(), l =>
                l.EndPoint.IsEqualTo(new GeoPoint3(-90, 0, 0), Tight)
            );
            Assert.Equal("Flipped", Row(Group(document, handle, CadProperties.CustomGroup), "Hand").Value);

            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handle }, "Hand", "Not flipped").Succeeded);
            Assert.Equal((short)0, ValueOf(document, handle, "Hand"));
            Assert.False(CadDynamicBlocks.Click(document, handle, 1).Succeeded);
        }

        [Fact]
        public void TheVisibilityGripClicked_ListsTheStatesTheCurrentTicked_AndAStateChosenIsShown()
        {
            CadDocument document = Sign(out string handle);
            CadGrip grip = CadGrips.Of(Insert(document, handle))[0];
            Assert.Equal(CadGripKind.CustomList, grip.Kind);
            Near(P(20, -10), grip.Point);

            IReadOnlyList<string> states = CadDynamicBlocks.ValuesOf(document, handle, 0, out string current);
            Assert.Equal(new[] { "Cross", "Tick", "Blank" }, states);
            Assert.Equal("Cross", current);
            Assert.Equal(
                new[] { "Cross", "Tick", "Blank" },
                Row(Group(document, handle, CadProperties.CustomGroup), "Mark").Choices
            );

            Assert.True(CadDynamicBlocks.Choose(document, handle, 0, "Tick").Succeeded);

            Assert.Equal("Tick", ValueOf(document, handle, "Mark"));
            CadDynamicBlocks.ValuesOf(document, handle, 0, out current);
            Assert.Equal("Tick", current);
            Assert.False(CadDynamicBlocks.Choose(document, handle, 0, "Arrow").Succeeded);
            Assert.False(document.GripDrag(handle, 0, P(0, 0)).Succeeded);
        }

        [Fact]
        public void TheRotationGripDraggedAQuarterTurn_TurnsTheArrowAQuarterTurn()
        {
            CadDocument document = Arrow(out string handle);
            CadGrip grip = CadGrips.Of(Insert(document, handle))[0];
            Assert.Equal(CadGripKind.CustomRotation, grip.Kind);
            Near(P(80, 25), grip.Point);

            Assert.True(document.GripDrag(handle, 0, P(50, 70)).Succeeded);

            Assert.Equal(Math.PI / 2, (double)ValueOf(document, handle, "Angle"), 9);
            Near(P(50, 55), CadGrips.Of(Insert(document, handle))[0].Point);
            // The angle to the places of AUPREC, nought in a drawing whose header gives none, as AutoCAD 2021 reads it.
            Assert.Equal("90", Row(Group(document, handle, CadProperties.CustomGroup), "Angle").Value);

            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handle }, "Angle", "180").Succeeded);
            Assert.Equal(Math.PI, (double)ValueOf(document, handle, "Angle"), 9);
        }

        [Fact]
        public void ResetBlock_PutsTheWidthBack_AsOneUndoStep_AndSaysHowManyWereReset()
        {
            CadDocument document = Panel(out string handle);
            Assert.True(CadDynamicBlocks.SetProperty(document, new[] { handle }, "Width", "200").Succeeded);
            CadCommandSession session = Session(document);

            Run(session, "RESETBLOCK", new[] { handle }, Enter);

            Assert.Equal("1 block reset.", session.History.Last());
            Assert.Equal("GH_PANEL", Insert(document, handle).Name);
            Assert.Equal(100.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal("RESETBLOCK", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(200.0, (double)ValueOf(document, handle, "Width"), 9);
        }

        [Fact]
        public void APlainInsert_HasNoCustomGripsNorCustomGroup_AndResetBlockPassesItOver()
        {
            CadDocument document = Plan();

            Assert.Equal(
                new[] { CadGripKind.Insertion },
                CadGrips.Of(Entity<DxfBlockReference>(document, "A7")).Select(g => g.Kind)
            );
            Assert.Null(Group(document, "A7", CadProperties.CustomGroup));
            Assert.Equal("BOX", Row(Group(document, "A7", "Misc"), "Name").Value);

            // AutoCAD 2021 counted a plain INSERT among those reset, and said nothing of a line alone (probe GhDyn1).
            CadCommandSession session = Session(document);
            string before = document.Database.ToText();
            Run(session, "RESETBLOCK", new[] { "A7", "A1" }, Enter);
            Assert.Equal("1 block reset.", session.History.Last());
            Run(session, "RESETBLOCK", new[] { "A1" }, Enter);
            Assert.DoesNotContain("reset", session.History.Last(), StringComparison.Ordinal);
            Assert.Equal(before, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TheBlockEditor_ShowsTheFirstVisibilityState_AndAnotherChosenHidesWhatItDoesNotList()
        {
            CadDocument document = Sign(out _);
            DxfBlockTableRecord block = document.Database.BlockTable["GH_SIGN"];
            string[] lines = block.OfType<DxfLine>().Select(l => l.Handle).ToArray();
            Assert.True(document.OpenBlockEditor("GH_SIGN").Succeeded);
            CadBlockEditor editor = document.BlockEditor;

            Assert.Equal(new[] { "Cross", "Tick", "Blank" }, editor.VisibilityStates);
            Assert.Equal("Cross", editor.VisibilityState);
            Assert.Equal(new[] { lines[0], lines[1] }, Drawn(document));

            Assert.True(document.SetBlockEditorVisibilityState("tick").Succeeded);

            Assert.Equal("Tick", editor.VisibilityState);
            Assert.Equal(new[] { lines[0], lines[2] }, Drawn(document));
            Assert.Empty(document.GetPickIndex(document.CurrentSpace).Pick(30, 30, 1.0));
            Assert.False(document.CanUndo);

            // A line drawn in the editor joins every state, as no state lists it.
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 50), P(40, 50), Enter);
            Assert.True(document.SetBlockEditorVisibilityState("Blank").Succeeded);
            Assert.Equal(2, Drawn(document).Length);
            Assert.False(document.SetBlockEditorVisibilityState("Arrow").Succeeded);
        }

        [Fact]
        public void ABlockOfNoVisibilityParameter_HasNoStatesInTheBlockEditor()
        {
            CadDocument document = Panel(out _);
            Assert.True(document.OpenBlockEditor("GH_PANEL").Succeeded);

            Assert.Empty(document.BlockEditor.VisibilityStates);
            Assert.Null(document.BlockEditor.VisibilityState);
            Assert.False(document.SetBlockEditorVisibilityState("Cross").Succeeded);
        }

        /// <summary>
        /// A drawing with the block GH_PLATE, a rectangle 100 by 50 whose XY parameter Width and Height from (0, 0) to (100, 50)
        /// stretches its right and top edges at the far corner, inserted at (1 000, 500).
        /// </summary>
        private static CadDocument Plate(out string handle) => Plate(false, out handle);

        /// <summary>
        /// The plate with its bottom and left edges too; where asked, a stretch of the bottom left corner that follows the XY
        /// parameter's base point (BaseXDelta, BaseYDelta, as AutoCAD's Block Editor writes one tied to the base grip), made by
        /// the text of the drawing, and with it the base grip AutoCAD shows.
        /// </summary>
        private static CadDocument Plate(bool baseStretch, out string handle)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_PLATE", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var right = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
            var top = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0));
            var bottom = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var left = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));
            block.AppendEntity(right);
            block.AppendEntity(top);
            block.AppendEntity(bottom);
            block.AppendEntity(left);
            DxfBlockXYParameter xy = block.AddXYParameter(
                "Width",
                null,
                "Height",
                null,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 50, 0)
            );
            block.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new DxfEntity[] { right, top });

            if (baseStretch)
            {
                block.AddStretchAction(
                    xy,
                    new GeoPoint2(-10, -10),
                    new GeoPoint2(10, 10),
                    new DxfEntity[] { left, bottom }
                );
            }

            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block);
            db.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;

            if (baseStretch)
            {
                // The second stretch follows the base point: its connections named so, as AutoCAD names them.
                string[] lines = db.ToText().Replace("\r\n", "\n").Split('\n');
                int seen = -1;

                for (int i = 0; i + 1 < lines.Length; i += 2)
                {
                    if (lines[i].Trim() != "0" || lines[i + 1].Trim() != "BLOCKSTRETCHACTION" || ++seen != 1)
                    {
                        continue;
                    }

                    for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                    {
                        lines[j + 1] =
                            lines[j + 1].Trim() == "EndXDelta" ? "BaseXDelta"
                            : lines[j + 1].Trim() == "EndYDelta" ? "BaseYDelta"
                            : lines[j + 1];
                    }
                }

                db = DxfDatabase.Parse(string.Join("\n", lines));
            }

            return CadDocument.From(db, "plate.dxf");
        }

        [Fact]
        public void AnXYParameter_ShowsItsEndAndItsTwoOtherCorners_AsAutoCadDoes()
        {
            CadDocument document = Plate(out string handle);

            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));

            // AutoCAD 2021 gave (1 100, 550), (1 000, 550) and (1 100, 500), the insertion point (1 000, 500) last (plugin GhDb1).
            Assert.Equal(
                new[]
                {
                    CadGripKind.CustomPoint,
                    CadGripKind.CustomPoint,
                    CadGripKind.CustomPoint,
                    CadGripKind.Insertion,
                },
                grips.Select(g => g.Kind)
            );
            Near(P(1100, 550), grips[0].Point);
            Near(P(1000, 550), grips[1].Point);
            Near(P(1100, 500), grips[2].Point);
        }

        [Fact]
        public void AnXYCornerDragged_SetsTheDistanceOfTheAxisItTakesFromTheEnd_AndMovesTheBasePointAlongTheOther_AsAutoCadDid()
        {
            // AutoCAD 2021 moved each grip by (20, 10): the end set Width 120 and Height 60; base x with end y Height 60, the
            // base point's x moved with it (Width 80) and the right edge stayed at x 100; end x with base y Width 120, the base
            // point's y moved (Height 40) and the top edge stayed at y 50 (plugins GhDb1 and GhDy2a).
            CadDocument document = Plate(out string handle);
            Assert.True(document.GripDrag(handle, 0, P(1120, 560)).Succeeded);
            Assert.Equal(120.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal(60.0, (double)ValueOf(document, handle, "Height"), 9);

            document = Plate(out handle);
            Assert.True(document.GripDrag(handle, 1, P(1020, 560)).Succeeded);
            Assert.Equal(80.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal(60.0, (double)ValueOf(document, handle, "Height"), 9);
            Near(P(1020, 560), CadGrips.Of(Insert(document, handle))[1].Point);
            Near(P(1100, 560), CadGrips.Of(Insert(document, handle))[0].Point);
            Assert.Contains(Insert(document, handle).BlockTableRecord.OfType<DxfLine>(), l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(100, 60, 0), Tight)
            );

            document = Plate(out handle);
            Assert.True(document.GripDrag(handle, 2, P(1120, 510)).Succeeded);
            Assert.Equal(120.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal(40.0, (double)ValueOf(document, handle, "Height"), 9);
            Assert.Contains(Insert(document, handle).BlockTableRecord.OfType<DxfLine>(), l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(0, 50, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(120, 50, 0), Tight)
            );
            Assert.Equal("GRIP", document.NextUndo.Name);

            // A corner dragged onto the base point's line along the axis it sets is refused.
            Assert.False(document.GripDrag(handle, 2, P(1000, 530)).Succeeded);
        }

        [Fact]
        public void TheBaseGripOfAnXYParameter_MovesTheBasePoint_AndTheStretchOfItFollows()
        {
            // AutoCAD 2021 showed the base grip of a plate whose bottom left corner a stretch of the base point holds, second
            // after the end grip, and dragged by (20, 10) it read Width 80 and Height 40 with the bottom and left edges
            // starting at (20, 10) and the end point staying (plugin GhDy2a); the corner of base x and end y by (20, 10)
            // moved the stretch by (20, 0) alone.
            CadDocument document = Plate(true, out string handle);
            IReadOnlyList<CadGrip> grips = CadGrips.Of(Insert(document, handle));
            Assert.Equal(5, grips.Count);
            Near(P(1100, 550), grips[0].Point);
            Near(P(1000, 500), grips[1].Point);
            Near(P(1000, 550), grips[2].Point);
            Near(P(1100, 500), grips[3].Point);
            Assert.Equal(CadGripKind.Insertion, grips[4].Kind);

            Assert.True(document.GripDrag(handle, 1, P(1020, 510)).Succeeded);

            Assert.Equal(80.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal(40.0, (double)ValueOf(document, handle, "Height"), 9);
            IEnumerable<DxfLine> lines = Insert(document, handle).BlockTableRecord.OfType<DxfLine>();
            Assert.Contains(lines, l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(20, 10, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Tight)
            );
            Assert.Contains(lines, l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(20, 10, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(0, 50, 0), Tight)
            );
            Assert.Contains(lines, l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(100, 50, 0), Tight)
            );
            Assert.Equal("GRIP", document.NextUndo.Name);

            document = Plate(true, out handle);
            Assert.True(document.GripDrag(handle, 2, P(1020, 560)).Succeeded);

            Assert.Equal(80.0, (double)ValueOf(document, handle, "Width"), 9);
            Assert.Equal(60.0, (double)ValueOf(document, handle, "Height"), 9);
            lines = Insert(document, handle).BlockTableRecord.OfType<DxfLine>();
            Assert.Contains(lines, l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(20, 0, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Tight)
            );
            Assert.Contains(lines, l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(100, 0, 0), Tight)
                && l.EndPoint.IsEqualTo(new GeoPoint3(100, 60, 0), Tight)
            );
        }

        [Fact]
        public void TheBaseGripDragged_PastTheEndPoint_IsRefusedAndTheDrawingKept()
        {
            CadDocument document = Plate(true, out string handle);

            Assert.False(document.GripDrag(handle, 1, P(1100, 510)).Succeeded);
            Assert.Equal(100.0, (double)ValueOf(document, handle, "Width"), 9);
        }

        [Fact]
        public void ABlockTableGrip_ListsTheRows_AndARowChosenSetsTheTable()
        {
            CadDocument document = Panel(out string handle, (block, width) =>
                block.AddBlockPropertiesTable(
                    new GeoPoint3(0, -20, 0),
                    new[] { DxfBlockPropertiesTableColumn.Distance(width) },
                    new[] { new DxfBlockPropertiesTableRow(100.0), new DxfBlockPropertiesTableRow(200.0) }
                )
            );

            CadGrip grip = CadGrips.Of(Insert(document, handle))[1];
            Assert.Equal(CadGripKind.CustomList, grip.Kind);
            Assert.Equal(DxfDynamicBlockGripType.BlockTable, grip.Custom.GripType);
            Near(P(1000, 480), grip.Point);

            IReadOnlyList<string> rows = CadDynamicBlocks.ValuesOf(document, handle, 1, out string current);
            Assert.Equal(new[] { "100.0000", "200.0000" }, rows);
            Assert.Equal("100.0000", current);

            Assert.True(CadDynamicBlocks.Choose(document, handle, 1, "200.0000").Succeeded);

            Assert.Equal((short)1, ValueOf(document, handle, "Block Table1"));
            Assert.Equal(200.0, (double)ValueOf(document, handle, "Width"), 9);
            CadDynamicBlocks.ValuesOf(document, handle, 1, out current);
            Assert.Equal("200.0000", current);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.False(document.GripDrag(handle, 1, P(1000, 400)).Succeeded);
        }

        [Fact]
        public void ACustomGripDragged_PreviewsTheBlockWorkedOutAtTheCursor_TheDrawingLeftAsItWas()
        {
            CadDocument document = Panel(out string handle);
            document.Selection.Set(new[] { handle });
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            string before = document.Database.ToText();

            Assert.True(session.StartGripEdit(new[] { new KeyValuePair<string, int>(handle, 0) }));
            CadPreview preview = session.Prompt.Preview;
            Assert.NotNull(preview);
            Assert.True(preview.ShowsBand);

            // At (1 180, 530) the Width goes to 200, the nearest of its list, and the right edge is drawn at x 1 200.
            List<string> shapes = PreviewTests.Shapes(preview.Items(P(1180, 530)));
            Assert.Contains(shapes, s => s.Contains("1200.000000 500.000000 1200.000000 550.000000"));
            Assert.DoesNotContain(shapes, s => s.Contains("1100.000000 500.000000 1100.000000 550.000000"));
            Assert.Empty(preview.Items(P(900, 500)));
            Assert.Equal(before, document.Database.ToText());
            Assert.False(document.CanUndo);

            Run(session, P(1180, 530));
            Assert.Equal(200.0, (double)ValueOf(document, handle, "Width"), 9);
        }

        [Fact]
        public void AFlipOrAListGripHot_OrTwoGrips_PreviewNothingOfTheBlock()
        {
            CadDocument document = Sign(out string handle);
            CadCommandSession session = Session(document);
            Assert.True(session.StartGripEdit(new[] { new KeyValuePair<string, int>(handle, 0) }));
            Assert.Null(session.Prompt.Preview);

            document = Panel(out handle);
            session = Session(document);
            Assert.True(
                session.StartGripEdit(
                    new[] { new KeyValuePair<string, int>(handle, 0), new KeyValuePair<string, int>(handle, 1) }
                )
            );
            Assert.Null(session.Prompt.Preview);
        }

        /// <summary>The handles the space shown draws, each once, in their order.</summary>
        private static string[] Drawn(CadDocument document) =>
            document.GetDisplay(document.CurrentSpace).Items.Select(i => i.Handle).Distinct().ToArray();
    }
}
