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
    /// In-place editing of a block reference, as AutoCAD 2021's REFEDIT: the reference's objects brought into the drawing where
    /// the reference draws them, edited with every command, REFSET adding objects to the working set and taking them out,
    /// REFCLOSE saving them back to the block (every reference of it drawn anew) or discarding them; the objects outside the
    /// working set locked; undo stepping inside the session and taking the whole of it back after. The command AutoCAD
    /// 2021's accoreconsole does not have (REFEDIT, REFSET and REFCLOSE are of the drawing editor), so the words are the
    /// presenter's own; XFADECTL and REFEDITNAME are probed.
    /// </summary>
    public class RefEditTests
    {
        /// <summary>
        /// A metric drawing with the block BOX (a line from (0, 0) to (100, 0) and a circle of 10 about (50, 30)) placed at
        /// (1 000, 0) and at (2 000, 0) twice as large and turned a quarter, and a line of the drawing's own from (0, 500) to
        /// (300, 500).
        /// </summary>
        private static CadDocument Boxes()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var box = new DxfBlockTableRecord { Name = "BOX", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(box);
            box.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            box.AppendEntity(new DxfCircle(new GeoPoint3(50, 30, 0), new GeoVector3(0, 0, 1), 10.0));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 500, 0), new GeoPoint3(300, 500, 0)));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 0, 0), box));
            var turned = new DxfBlockReference(new GeoPoint3(2000, 0, 0), box);
            database.ModelSpace.AppendEntity(turned);
            turned.ScaleFactors = new GeoVector3(2, 2, 2);
            turned.Rotation = Math.PI / 2.0;
            return CadDocument.From(database, "boxes.dxf");
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfBlockReference First(CadDocument document) => All<DxfBlockReference>(document.ModelSpace)[0];

        private static IEnumerable<DxfEntity> Alive(DxfBlockTableRecord block) => block.Where(e => !e.IsErased);

        private static int Drawn(CadDocument document, DxfEntity insert) =>
            document.GetDisplay(document.ModelSpace).Items.Count(i => i.Handle == insert.Handle);

        /// <summary>REFEDIT on the first BOX with every object taken: the line and circle of the block copied where it stands.</summary>
        private static CadCommandSession Opened(CadDocument document)
        {
            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0));
            return Run(session, Enter);
        }

        [Fact]
        public void Refedit_BringsTheObjectsOfTheReferenceIntoTheDrawing_WhereTheReferenceDrawsThem()
        {
            CadDocument document = Boxes();
            int before = Drawn(document, First(document));
            CadCommandSession session = Start(document, "REFEDIT");

            Assert.Equal("Select reference:", session.PromptText);
            Run(session, P(1050, 0));
            Assert.Equal("Enter object selection method [All/Nested] <All>:", session.PromptText);
            Run(session, Enter);

            Assert.Contains("2 Added to working set", session.History);
            Assert.NotNull(document.RefEdit);
            Assert.Equal("BOX", document.RefEdit.Name);
            Assert.Equal(2, document.RefEdit.WorkingSet.Count);

            // The line of the block is copied to (1 000, 0)-(1 100, 0), its circle to (1 050, 30); the drawing's own line stays first.
            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(2, lines.Count);
            Near(new GeoPoint3(1000, 0, 0), lines[1].StartPoint);
            Near(new GeoPoint3(1100, 0, 0), lines[1].EndPoint);
            DxfCircle circle = Assert.Single(All<DxfCircle>(document.ModelSpace));
            Near(new GeoPoint3(1050, 30, 0), circle.Center);
            Assert.Equal(10.0, circle.Radius, 9);

            // The block still holds what it held, and no reference draws what stands in the drawing now.
            Assert.Equal(2, Alive(document.Database.BlockTable["BOX"]).Count());
            Assert.True(before > 0);
            Assert.Equal(0, Drawn(document, First(document)));
            Assert.Equal(0, Drawn(document, All<DxfBlockReference>(document.ModelSpace)[1]));
        }

        [Fact]
        public void ATurnedAndScaledReference_GivesItsObjectsWhereItDrawsThem_ASaveFindsTheBlockAgain()
        {
            CadDocument document = Boxes();
            DxfBlockReference turned = All<DxfBlockReference>(document.ModelSpace)[1];

            // The line of the block is (2 000, 0)-(2 000, 200) under it, the circle of 10 is 20 about (1 940, 100).
            CadCommandSession session = Start(document, "REFEDIT", P(2000, 100), Enter);

            DxfLine line = All<DxfLine>(document.ModelSpace)[1];
            Near(new GeoPoint3(2000, 0, 0), line.StartPoint, 1E-9);
            Near(new GeoPoint3(2000, 200, 0), line.EndPoint, 1E-9);
            DxfCircle circle = Assert.Single(All<DxfCircle>(document.ModelSpace));
            Near(new GeoPoint3(1940, 100, 0), circle.Center, 1E-9);
            Assert.Equal(20.0, circle.Radius, 9);

            // Save with nothing changed: the block holds the line and circle where it held them.
            Run(session, "REFCLOSE", Enter);

            DxfBlockTableRecord box = document.Database.BlockTable["BOX"];
            DxfLine back = Assert.Single(Alive(box).OfType<DxfLine>());
            Near(new GeoPoint3(0, 0, 0), back.StartPoint, 1E-9);
            Near(new GeoPoint3(100, 0, 0), back.EndPoint, 1E-9);
            DxfCircle round = Assert.Single(Alive(box).OfType<DxfCircle>());
            Near(new GeoPoint3(50, 30, 0), round.Center, 1E-9);
            Assert.Equal(10.0, round.Radius, 9);
            Assert.NotNull(turned);
        }

        [Fact]
        public void AnObjectMovedInTheDrawingAndSaved_IsMovedInTheBlock_EveryReferenceDrawsIt_OneUndoStepTakesTheSessionBack()
        {
            CadDocument document = Boxes();
            string start = document.Database.ToText();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;

            Run(session, "MOVE", new[] { circle }, Enter, P(0, 0), P(0, 50));
            Near(new GeoPoint3(1050, 80, 0), Entity<DxfCircle>(document, circle).Center);

            Run(session, "REFCLOSE");
            Assert.Equal("Enter option [Save/Discard reference changes] <Save>:", session.PromptText);
            Run(session, Enter);

            Assert.Null(document.RefEdit);
            Assert.Contains("2 block instances updated", session.History);
            DxfBlockTableRecord box = document.Database.BlockTable["BOX"];
            DxfCircle moved = Assert.Single(Alive(box).OfType<DxfCircle>());
            Near(new GeoPoint3(50, 80, 0), moved.Center);
            Assert.Single(Alive(box).OfType<DxfLine>());

            // No object of the working set stays in the drawing; every reference draws the two objects of the block again.
            Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Empty(All<DxfCircle>(document.ModelSpace));
            Assert.Equal(2, Drawn(document, First(document)));
            Assert.Equal(2, Drawn(document, All<DxfBlockReference>(document.ModelSpace)[1]));
            AssertReopens(document);

            Assert.Equal("REFEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ObjectsSavedBack_AreNewObjects_OfNewHandles()
        {
            CadDocument document = Boxes();
            string[] was = Alive(document.Database.BlockTable["BOX"]).Select(e => e.Handle).ToArray();
            Run(Opened(document), "REFCLOSE", Enter);

            string[] now = Alive(document.Database.BlockTable["BOX"]).Select(e => e.Handle).ToArray();
            Assert.Equal(was.Length, now.Length);
            Assert.Empty(now.Intersect(was));
        }

        [Fact]
        public void ReficloseDiscard_LeavesTheBlockAsItWas_ButTheObjectsDrawnInTheDrawingStay_OneUndoStepTakesItBack()
        {
            CadDocument document = Boxes();
            string start = document.Database.ToText();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "ERASE", new[] { circle }, Enter, "LINE", P(0, 800), P(300, 800), Enter);
            Assert.Equal(3, All<DxfLine>(document.ModelSpace).Count);

            Run(session, "REFCLOSE", "D");

            Assert.Null(document.RefEdit);
            Assert.DoesNotContain(session.History, line => line.Contains("updated"));
            DxfBlockTableRecord box = document.Database.BlockTable["BOX"];
            Assert.Equal(2, Alive(box).Count());
            Assert.Single(Alive(box).OfType<DxfCircle>());

            // The copies of the block's objects are gone; the line drawn in the drawing is the drawing's now.
            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(2, lines.Count);
            Near(new GeoPoint3(0, 800, 0), lines[1].StartPoint);
            Assert.Empty(All<DxfCircle>(document.ModelSpace));
            Assert.Equal(2, Drawn(document, First(document)));

            Assert.Equal("REFEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ALineDrawnInTheDrawingAndAddedByRefset_IsSavedIntoTheBlock_WhereAReferenceDrawsIt()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Opened(document);
            Run(session, "LINE", P(1000, 300), P(1100, 300), Enter);
            string drawn = All<DxfLine>(document.ModelSpace)[2].Handle;

            // A line drawn after the session began is not in the working set until REFSET adds it.
            Assert.Equal(2, document.RefEdit.WorkingSet.Count);
            Run(session, "REFSET");
            Assert.Equal("Enter an option [Add/Remove] <Add>:", session.PromptText);
            Run(session, Enter, new[] { drawn }, Enter);

            Assert.Contains("1 Added to working set", session.History);
            Assert.Equal(3, document.RefEdit.WorkingSet.Count);

            Run(session, "REFCLOSE", Enter);

            DxfBlockTableRecord box = document.Database.BlockTable["BOX"];
            List<DxfLine> lines = Alive(box).OfType<DxfLine>().ToList();
            Assert.Equal(2, lines.Count);
            DxfLine added = lines.Single(l => Math.Abs(l.StartPoint.Y - 300) < 1E-9);
            Near(new GeoPoint3(0, 300, 0), added.StartPoint);
            Near(new GeoPoint3(100, 300, 0), added.EndPoint);
            Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Equal(3, Drawn(document, First(document)));
        }

        [Fact]
        public void RefsetRemove_PutsAnObjectBackInTheDrawing_AndASaveTakesItOutOfTheBlock()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;

            Run(session, "REFSET", "R", new[] { circle }, Enter);

            Assert.Contains("1 Removed from working set", session.History);
            Assert.Single(document.RefEdit.WorkingSet);

            // The circle is the drawing's now: it can be erased like any object drawn in the session.
            Run(session, "ERASE", new[] { circle }, Enter);
            Assert.Null(document.FindEntity(circle));
        }

        [Fact]
        public void ARemovedObjectKeptInTheDrawing_IsTakenOutOfTheBlockBySave_AndPutBackByDiscard()
        {
            CadDocument saved = Boxes();
            CadCommandSession session = Opened(saved);
            string circle = Assert.Single(All<DxfCircle>(saved.ModelSpace)).Handle;
            Run(session, "REFSET", "R", new[] { circle }, Enter, "REFCLOSE", Enter);

            Assert.Empty(Alive(saved.Database.BlockTable["BOX"]).OfType<DxfCircle>());
            DxfCircle kept = Assert.Single(All<DxfCircle>(saved.ModelSpace));
            Near(new GeoPoint3(1050, 30, 0), kept.Center);
            Assert.Equal(1, Drawn(saved, First(saved)));

            CadDocument discarded = Boxes();
            session = Opened(discarded);
            circle = Assert.Single(All<DxfCircle>(discarded.ModelSpace)).Handle;
            Run(session, "REFSET", "R", new[] { circle }, Enter, "REFCLOSE", "D");

            Assert.Single(Alive(discarded.Database.BlockTable["BOX"]).OfType<DxfCircle>());
            Assert.Empty(All<DxfCircle>(discarded.ModelSpace));
        }

        [Fact]
        public void AnObjectOfTheDrawingAddedByRefset_MovesIntoTheBlock_WhenSaved_ThroughTheReferencesPlacingBackwards()
        {
            CadDocument document = Boxes();
            DxfLine own = All<DxfLine>(document.ModelSpace)[0];
            CadCommandSession session = Opened(document);

            // The drawing's own line is locked, and REFSET takes it all the same.
            Run(session, "REFSET", Enter, new[] { own.Handle }, Enter);
            Assert.Contains("1 Added to working set", session.History);
            Assert.Equal(3, document.RefEdit.WorkingSet.Count);

            Run(session, "REFCLOSE", Enter);

            // (0, 500)-(300, 500) is (-1 000, 500)-(-700, 500) in a block placed at (1 000, 0).
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            DxfLine moved = Alive(document.Database.BlockTable["BOX"])
                .OfType<DxfLine>()
                .Single(l => l.StartPoint.X < -1);
            Near(new GeoPoint3(-1000, 500, 0), moved.StartPoint);
            Near(new GeoPoint3(-700, 500, 0), moved.EndPoint);
        }

        [Fact]
        public void AnObjectTakenOutByRefset_AndAddedAgain_IsInTheSetOnceMore_AndSavedWithIt()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;

            Run(session, "REFSET", "R", new[] { circle }, Enter);
            Assert.Single(document.RefEdit.WorkingSet);
            Run(session, "REFSET", Enter, new[] { circle }, Enter);

            Assert.Contains("1 Added to working set", session.History);
            Assert.Equal(2, document.RefEdit.WorkingSet.Count);

            Run(session, "REFCLOSE", Enter);

            Assert.Single(Alive(document.Database.BlockTable["BOX"]).OfType<DxfCircle>());
            Assert.Empty(All<DxfCircle>(document.ModelSpace));
        }

        [Fact]
        public void AnObjectErasedInTheDrawing_LeavesTheBlock_WhenSaved()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;

            Run(session, "ERASE", new[] { circle }, Enter);
            Assert.Single(document.RefEdit.WorkingSet);
            Run(session, "REFCLOSE", Enter);

            Assert.Contains("1 object removed from BOX", session.History);
            Assert.Contains("2 block instances updated", session.History);
            Assert.Empty(Alive(document.Database.BlockTable["BOX"]).OfType<DxfCircle>());
            Assert.Single(Alive(document.Database.BlockTable["BOX"]).OfType<DxfLine>());
            Assert.Equal(1, Drawn(document, First(document)));
        }

        [Fact]
        public void ObjectsOutsideTheWorkingSet_AreLocked_UntilTheLockIsSwitchedOff()
        {
            CadDocument document = Boxes();
            DxfLine own = All<DxfLine>(document.ModelSpace)[0];
            CadCommandSession session = Opened(document);

            // ERASE finds nothing to erase among the objects the working set does not hold.
            Run(session, "ERASE", new[] { own.Handle }, Enter);
            Assert.False(Entity<DxfLine>(document, own.Handle).IsErased);

            // Any other way to change one is refused with a word of why.
            CadEditResult refused = document.ApplyEdit(
                "TEST",
                new[] { new CadEditRecord(CadEditRecordKind.Erased, own.Handle) },
                s => Entity<DxfLine>(document, own.Handle).Erase()
            );
            Assert.True(refused.Refused);
            Assert.Contains("working set", refused.Message, StringComparison.Ordinal);

            // An object drawn in the session is not locked.
            Run(session, "LINE", P(0, 900), P(100, 900), Enter);
            string drawn = All<DxfLine>(document.ModelSpace).Last().Handle;
            Run(session, "ERASE", new[] { drawn }, Enter);
            Assert.Null(document.FindEntity(drawn));

            CadDocument open = Boxes();
            open.RefEditLocksObjects = false;
            DxfLine other = All<DxfLine>(open.ModelSpace)[0];
            Opened(open);
            Run(Session(open), "ERASE", new[] { other.Handle }, Enter);
            Assert.Null(open.FindEntity(other.Handle));
        }

        [Fact]
        public void ABlockWithAttributeDefinitions_AsksWhetherToDisplayThem_EnterIsNo()
        {
            // AcRefEdRes.dll: "Display attribute definitions [Yes/No] <No>: ", asked of a block that holds some.
            CadDocument document = Boxes();
            document
                .Database.BlockTable["BOX"]
                .AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, -20, 0), "val", "T", "t", null));
            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0), Enter);

            Assert.Equal("Display attribute definitions [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");

            Assert.True(document.RefEditAttributeDefinitions);
            Assert.Equal(3, document.RefEdit.WorkingSet.Count);
            Assert.True(document.RefEdit.ShowsAttributeDefinitions);

            CadDocument plain = Boxes();
            Start(plain, "REFEDIT", P(1050, 0), Enter);
            Assert.Equal(2, plain.RefEdit.WorkingSet.Count);
            Assert.False(plain.RefEditAttributeDefinitions);
        }

        [Fact]
        public void TheLock_IsKeptWhereTheDocumentSetsIt_AndNoPromptAsksIt()
        {
            CadDocument document = Boxes();
            document.RefEditLocksObjects = false;
            Start(document, "REFEDIT", P(1050, 0), Enter);

            Assert.False(document.RefEdit.LocksObjects);
        }

        [Fact]
        public void TheNestingLevel_Enter_TakesNext_AsTheResourceStringsDefaultIs()
        {
            CadDocument document = Nested();
            CadCommandSession session = Start(document, "REFEDIT", P(1250, 1000));

            Assert.Contains("Reference: A", session.History);
            Run(session, Enter);

            Assert.Contains("Reference: A > B", session.History);
            Assert.Equal("Select nesting level [Ok/Next] <Next>:", session.PromptText);
        }

        [Fact]
        public void TheSelectionMethod_NestedAsksForPoints_AndAllTakesEverything()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0), "N");

            Assert.StartsWith("Select nested objects", session.PromptText, StringComparison.Ordinal);
            Run(session, "A");

            Assert.Equal(2, document.RefEdit.WorkingSet.Count);
        }

        [Fact]
        public void Refset_SaysWhatItDoes_AsTheResourceStringsHaveIt()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;

            Run(session, "REFSET");
            Assert.Contains("Transfer objects between the RefEdit working set and host drawing...", session.History);
            Run(session, Enter, new[] { circle }, Enter);

            Assert.Contains("** 1 selected object is already in the working set **", session.History);

            Run(session, "REFSET", "R", new[] { circle }, Enter);
            Assert.Contains("1 Removed from working set", session.History);

            Run(session, "REFSET", "R", new[] { circle }, Enter);
            Assert.Contains("** 1 selected object is not in the working set **", session.History);
        }

        [Fact]
        public void ASave_SaysTheObjectsAddedToAndRemovedFromTheBlock_AndTheInstancesUpdated()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Opened(document);
            Run(session, "LINE", P(1000, 300), P(1100, 300), Enter);
            string drawn = All<DxfLine>(document.ModelSpace)[2].Handle;
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "REFSET", Enter, new[] { drawn }, Enter);
            Run(session, "ERASE", new[] { circle }, Enter);
            Run(session, "REFCLOSE");
            Assert.Equal("Enter option [Save/Discard reference changes] <Save>:", session.PromptText);
            Run(session, Enter);

            Assert.Contains("1 object added to BOX", session.History);
            Assert.Contains("1 object removed from BOX", session.History);
            Assert.Equal("2 block instances updated", session.History.Last());
        }

        [Fact]
        public void ABlockScaledUnequally_IsRefused_AsAutoCADRefusesIt()
        {
            CadDocument document = Boxes();
            DxfBlockReference stretched = All<DxfBlockReference>(document.ModelSpace)[1];
            stretched.ScaleFactors = new GeoVector3(2, 3, 1);

            CadEditResult result = document.OpenRefEdit(new[] { stretched.Handle }, null);

            Assert.True(result.Refused);
            Assert.Equal("** Cannot REFEDIT a non-uniformly scaled block **", result.Message);
            Assert.Null(document.RefEdit);

            // A mirror is not an unequal scale.
            stretched.ScaleFactors = new GeoVector3(-2, 2, 2);
            Assert.True(document.OpenRefEdit(new[] { stretched.Handle }, null).Succeeded);
        }

        [Fact]
        public void AnAnonymousBlock_IsRefused_InTheWordsOfAutoCAD()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var anonymous = new DxfBlockTableRecord { Name = "*U", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(anonymous);
            anonymous.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            var dimension = new DxfBlockTableRecord { Name = "*D", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(dimension);
            dimension.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), anonymous));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(5, 0, 0), dimension));
            CadDocument document = CadDocument.From(database, "anonymous.dxf");
            IReadOnlyList<DxfBlockReference> references = All<DxfBlockReference>(document.ModelSpace);

            Assert.Equal(
                "** Cannot REFEDIT anonymous block **",
                document.OpenRefEdit(new[] { references[0].Handle }, null).Message
            );
            Assert.Equal(
                "** Cannot REFEDIT dimension or table blocks **",
                document.OpenRefEdit(new[] { references[1].Handle }, null).Message
            );
        }

        [Fact]
        public void UndoInsideTheSession_StopsWhereTheEditorOpened_AndAfterTheCloseOneUndoTakesBackAll()
        {
            CadDocument document = Boxes();
            string start = document.Database.ToText();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "MOVE", new[] { circle }, Enter, P(0, 0), P(0, 50));

            Assert.True(document.CanUndo);
            Assert.True(document.Undo());
            Near(new GeoPoint3(1050, 30, 0), Entity<DxfCircle>(document, circle).Center);

            // The step that opened the session is not undone while it is open.
            Assert.False(document.CanUndo);
            Assert.False(document.Undo());
            Assert.NotNull(document.RefEdit);
            Assert.Equal(2, document.RefEdit.WorkingSet.Count);

            Run(session, "MOVE", new[] { circle }, Enter, P(0, 0), P(0, 70), "REFCLOSE", Enter);
            DxfCircle moved = Assert.Single(Alive(document.Database.BlockTable["BOX"]).OfType<DxfCircle>());
            Near(new GeoPoint3(50, 100, 0), moved.Center);

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
            Assert.True(document.Redo());
            Assert.Null(document.RefEdit);
            Assert.Equal(
                100.0,
                Assert.Single(Alive(document.Database.BlockTable["BOX"]).OfType<DxfCircle>()).Center.Y,
                9
            );
        }

        [Fact]
        public void PromptingForNestedObjects_TakesTheOnesPickedAlone()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0), "N");

            Assert.StartsWith("Select nested objects", session.PromptText, StringComparison.Ordinal);

            // A point on the circle picks the circle of the block; the line does not reach it.
            Run(session, P(1060, 30));
            Assert.Contains("1 found", session.History);
            Run(session, Enter);

            Assert.Contains("1 Added to working set", session.History);
            Assert.Single(All<DxfCircle>(document.ModelSpace));
            Assert.Single(All<DxfLine>(document.ModelSpace));

            // The reference still draws the line it keeps, and not the circle that stands in the drawing.
            Assert.Equal(1, Drawn(document, First(document)));
        }

        [Fact]
        public void PromptingForNestedObjects_NothingPicked_OpensNothing()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0), "N", P(5000, 5000), Enter);

            Assert.Contains("0 found", session.History);
            Assert.Contains(CadRefEdit.NoObjects, session.History);
            Assert.Null(document.RefEdit);
            Assert.False(document.CanUndo);
        }

        /// <summary>The block B (a line from (0, 0) to (50, 0)) placed at (100, 0) in the block A (a circle of 5), which is placed at (1 000, 1 000) twice as large.</summary>
        private static CadDocument Nested()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var b = new DxfBlockTableRecord { Name = "B", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(b);
            b.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)));
            var a = new DxfBlockTableRecord { Name = "A", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(a);
            a.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5.0));
            a.AppendEntity(new DxfBlockReference(new GeoPoint3(100, 0, 0), b));
            var outer = new DxfBlockReference(new GeoPoint3(1000, 1000, 0), a);
            database.ModelSpace.AppendEntity(outer);
            outer.ScaleFactors = new GeoVector3(2, 2, 2);
            return CadDocument.From(database, "nested.dxf");
        }

        [Fact]
        public void ANestedReference_OffersItsLevels_TheOutermostFirst_AndOkTakesTheOneShown()
        {
            CadDocument document = Nested();

            // The point is on the line of B, which A holds: the levels are A, then B inside it.
            CadCommandSession session = Start(document, "REFEDIT", P(1250, 1000));

            Assert.Contains("Reference: A", session.History);
            Assert.Equal("Select nesting level [Ok/Next] <Next>:", session.PromptText);
            Run(session, "O", Enter);

            Assert.Equal("A", document.RefEdit.Name);
            Assert.Single(document.RefEdit.Chain);

            // The circle of A is 10 about (1 000, 1 000) and B's INSERT stands at (1 200, 1 000), twice as large.
            DxfCircle circle = Assert.Single(All<DxfCircle>(document.ModelSpace));
            Near(new GeoPoint3(1000, 1000, 0), circle.Center);
            Assert.Equal(10.0, circle.Radius, 9);
            DxfBlockReference inner = All<DxfBlockReference>(document.ModelSpace).Single(r => r.Name == "B");
            Near(new GeoPoint3(1200, 1000, 0), inner.Position);
            Assert.Equal(2.0, inner.ScaleFactors.X, 9);
        }

        [Fact]
        public void NextChoosesTheNestedReference_ItsObjectsStandWhereBothPlaceThem_AndASaveWritesThemIntoItsBlock()
        {
            CadDocument document = Nested();
            CadCommandSession session = Start(document, "REFEDIT", P(1250, 1000), "N");

            Assert.Contains("Reference: A > B", session.History);
            Run(session, "O", Enter);

            Assert.Equal("B", document.RefEdit.Name);
            Assert.Equal(2, document.RefEdit.Chain.Count);
            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Near(new GeoPoint3(1200, 1000, 0), line.StartPoint);
            Near(new GeoPoint3(1300, 1000, 0), line.EndPoint);

            // Moved 10 up in the drawing: 5 up in B, which A, twice as large, places.
            Run(session, "MOVE", new[] { line.Handle }, Enter, P(0, 0), P(0, 10), "REFCLOSE", Enter);

            DxfLine saved = Assert.Single(Alive(document.Database.BlockTable["B"]).OfType<DxfLine>());
            Near(new GeoPoint3(0, 5, 0), saved.StartPoint);
            Near(new GeoPoint3(50, 5, 0), saved.EndPoint);
            Assert.Equal(2, Alive(document.Database.BlockTable["A"]).Count());
        }

        /// <summary>The block TAG (a line from (0, 0) to (100, 0) and the attribute definition LEVEL at (0, -20)) placed at (1 000, 0).</summary>
        private static CadDocument Tagged()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var tag = new DxfBlockTableRecord { Name = "TAG", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(tag);
            tag.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            var level = new DxfAttributeDefinition(new GeoPoint3(0, -20, 0), "L0", "LEVEL", "Level", null);
            tag.AppendEntity(level);
            var insert = new DxfBlockReference(new GeoPoint3(1000, 0, 0), tag);
            database.ModelSpace.AppendEntity(insert);
            var value = new DxfAttributeReference();
            value.SetAttributeFromBlock(level, insert.BlockTransform);
            value.TextString = "L3";
            insert.AttributeCollection.AppendAttribute(value);
            return CadDocument.From(DxfDatabase.Parse(database.ToText()), "tag.dxf");
        }

        [Fact]
        public void TheAttributeDefinitionsOfTheBlock_StayInIt_AndTheAttributesOfTheReferenceAreNotTouched()
        {
            CadDocument document = Tagged();
            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0), Enter, Enter);

            Assert.Contains("1 Added to working set", session.History);
            Assert.Empty(All<DxfAttributeDefinition>(document.ModelSpace));
            Assert.Single(All<DxfLine>(document.ModelSpace));

            Run(session, "REFCLOSE", Enter);

            DxfBlockTableRecord tag = document.Database.BlockTable["TAG"];
            Assert.Single(Alive(tag).OfType<DxfAttributeDefinition>());
            Assert.Single(Alive(tag).OfType<DxfLine>());
            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal("L3", Assert.Single(insert.AttributeCollection).TextString);
        }

        [Fact]
        public void WithTheDefinitionsDisplayed_TheyAreInTheWorkingSet_AndASaveWritesTheirChangeIntoTheBlock()
        {
            CadDocument document = Tagged();
            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0), Enter, "Y");

            Assert.Contains("2 Added to working set", session.History);
            DxfAttributeDefinition shown = Assert.Single(All<DxfAttributeDefinition>(document.ModelSpace));
            Near(new GeoPoint3(1000, -20, 0), shown.Position);

            Run(session, "MOVE", new[] { shown.Handle }, Enter, P(0, 0), P(0, -10), "REFCLOSE", Enter);

            DxfAttributeDefinition definition = Assert.Single(
                Alive(document.Database.BlockTable["TAG"]).OfType<DxfAttributeDefinition>()
            );
            Near(new GeoPoint3(0, -30, 0), definition.Position);
            Assert.Equal("LEVEL", definition.Tag);

            // The reference is left as it was: its attribute value is not moved, as AutoCAD runs no ATTSYNC.
            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Equal("L3", Assert.Single(insert.AttributeCollection).TextString);
        }

        [Fact]
        public void ACommandThatLeavesTheEdit_IsRefused_BeduitAndASecondRefeditAmongThem()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Opened(document);

            Run(session, "BEDIT");
            Assert.Equal("** BEDIT command not allowed during reference editing **", session.History.Last());
            Assert.Null(document.BlockEditor);

            Run(session, "REFEDIT");
            Assert.Equal("** Command not allowed, BOX already checked out for editing **", session.History.Last());
            Assert.False(session.IsRunning);

            Run(session, "QSAVE");
            Assert.Equal("** Drawing save not allowed during reference editing **", session.History.Last());
        }

        [Fact]
        public void ARefeditInTheBlockEditor_IsRefused()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Start(document, "-BEDIT", "BOX");

            Run(session, "REFEDIT");
            Assert.Equal(CadBlockEditor.NotAllowed, session.History.Last());
            Run(session, "REFSET");
            Assert.Equal(CadBlockEditor.NotAllowed, session.History.Last());
            Run(session, "REFCLOSE");
            Assert.Equal(CadBlockEditor.NotAllowed, session.History.Last());
            Assert.Null(document.RefEdit);
        }

        [Fact]
        public void RefsetAndRefclose_WithNothingBeingEdited_SayIt()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Start(document, "REFSET");
            Assert.Equal(CadRefEdit.NotEditing, session.History.Last());
            Run(session, "REFCLOSE");
            Assert.Equal(CadRefEdit.NotEditing, session.History.Last());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AnObjectThatIsNoReference_IsRefused_AndTheEnterAfterItEndsTheCommand()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Start(document, "REFEDIT", P(100, 500));

            Assert.Contains("Object is not a valid reference", session.History);
            Assert.Equal("Select reference:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.Null(document.RefEdit);
        }

        [Fact]
        public void TheReferenceSelectedBeforeTheCommand_IsTaken_AsPickfirstTakesIt()
        {
            CadDocument document = Boxes();
            document.Selection.Set(new[] { First(document).Handle });
            CadCommandSession session = Start(document, "REFEDIT");

            Assert.StartsWith("Enter object selection method", session.PromptText, StringComparison.Ordinal);
            Run(session, Enter);
            Assert.Equal("BOX", document.RefEdit.Name);
            Assert.Equal(0, document.Selection.Count);
        }

        [Fact]
        public void AnExternalReference_IsRefused_ItsFileIsNotWrittenBack()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            DxfBlockTableRecord xref = database.AttachXref("site.dxf", "SITE");
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), xref));
            CadDocument document = CadDocument.From(database, "xref.dxf");

            CadEditResult result = document.OpenRefEdit(new[] { First(document).Handle }, null);

            Assert.True(result.Refused);
            Assert.Equal("External references cannot be edited in place in this version.", result.Message);
            Assert.Null(document.RefEdit);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ARefeditOfABlockWithNothingToEdit_IsRefused()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var empty = new DxfBlockTableRecord { Name = "EMPTY", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(empty);
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), empty));
            CadDocument document = CadDocument.From(database, "empty.dxf");

            CadEditResult result = document.OpenRefEdit(new[] { First(document).Handle }, null);

            Assert.True(result.Refused);
            Assert.Equal("** At least one object must be selected to edit a reference **", result.Message);
            Assert.Null(document.RefEdit);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void XfadectlAndRefeditname_AreSystemVariables_AsAutoCadHasThem()
        {
            CadDocument document = Boxes();
            CadCommandSession session = Start(document, "REFEDITNAME");

            // Read only, as AutoCAD 2021 probed: REFEDITNAME = "" (read only).
            Assert.Equal("REFEDITNAME = \"\" (read only)", session.History.Last());

            // XFADECTL 70 by default, 0 to 90, as AutoCAD 2021 probed ("Requires an integer between 0 and 90.").
            Assert.Equal(70, document.XFadeControl);
            Run(session, Esc, "XFADECTL", "95");
            Assert.Equal("Requires an integer between 0 and 90.", session.History.Last());
            Run(session, "40");
            Assert.Equal(40, document.XFadeControl);
            Assert.False(document.CanUndo);

            Run(session, "REFEDIT", P(1050, 0), Enter, "REFEDITNAME");
            Assert.Equal("REFEDITNAME = \"BOX\" (read only)", session.History.Last());
        }

        [Fact]
        public void TheLockedObjects_AreTheOnesTheCanvasFades_ThoseOfTheWorkingSetAndOfTheSessionAreNot()
        {
            CadDocument document = Boxes();
            Assert.Empty(document.RefEditLockedHandles());

            CadCommandSession session = Opened(document);
            Run(session, "LINE", P(0, 900), P(100, 900), Enter);
            string drawn = All<DxfLine>(document.ModelSpace).Last().Handle;
            string[] faded = document.RefEditLockedHandles().ToArray();

            // The drawing's own line and the two references are faded; the copies and the line drawn are not.
            Assert.Equal(3, faded.Length);
            Assert.Contains(All<DxfLine>(document.ModelSpace)[0].Handle, faded);
            Assert.Contains(First(document).Handle, faded);
            Assert.DoesNotContain(drawn, faded);
            Assert.DoesNotContain(document.RefEdit.WorkingSet[0], faded);

            Run(session, "REFCLOSE", Enter);
            Assert.Empty(document.RefEditLockedHandles());
        }

        [Fact]
        public void ASessionSavedInTheFile_IsPickedUpAgain_OnOpeningIt()
        {
            CadDocument document = Boxes();
            Opened(document);
            string text = document.Database.ToText();

            CadDocument reopened = CadDocument.From(DxfDatabase.Parse(text), "again.dxf");

            Assert.NotNull(reopened.RefEdit);
            Assert.Equal("BOX", reopened.RefEdit.Name);
            Assert.Equal(2, reopened.RefEdit.WorkingSet.Count);
            Assert.Equal(0, Drawn(reopened, First(reopened)));

            CadCommandSession session = Start(reopened, "REFCLOSE", Enter);
            Assert.Null(reopened.RefEdit);
            Assert.Equal(2, Alive(reopened.Database.BlockTable["BOX"]).Count());
            Assert.Equal(2, Drawn(reopened, First(reopened)));
            Assert.Contains("2 block instances updated", session.History);
        }

        [Fact]
        public void ABlockStretchedAlongOneAxis_IsRefused_WhereItOnceSkippedItsCircle()
        {
            // A circle under a scale unlike along its axes is an ellipse there; AutoCAD 2021 refuses the whole reference
            // (AcRefEdRes.dll: "Cannot REFEDIT a non-uniformly scaled block").
            DxfDatabase database = CadTemplate.CreateMetric();
            var box = new DxfBlockTableRecord { Name = "BOX", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(box);
            box.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            box.AppendEntity(new DxfCircle(new GeoPoint3(50, 30, 0), new GeoVector3(0, 0, 1), 10.0));
            var stretched = new DxfBlockReference(new GeoPoint3(1000, 0, 0), box);
            database.ModelSpace.AppendEntity(stretched);
            stretched.ScaleFactors = new GeoVector3(2, 1, 1);
            CadDocument document = CadDocument.From(database, "stretched.dxf");

            CadCommandSession session = Start(document, "REFEDIT", P(1100, 0), Enter);

            Assert.Null(document.RefEdit);
            Assert.Contains("** Cannot REFEDIT a non-uniformly scaled block **", session.History);
            Assert.False(document.CanUndo);
        }
    }
}
