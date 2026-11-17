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
    /// The visibility states of the Block Editor, as AutoCAD 2021's BPARAMETER Visibility, BVSTATE, BVHIDE and BVSHOW make them:
    /// a first state showing every object, states added showing none, all or the state shown, objects shown and hidden in one state
    /// or in all, states renamed, moved and deleted, and an INSERT of the saved block showing the state it is set to.
    /// </summary>
    public class BlockVisibilityAuthoringTests
    {
        /// <summary>A drawing with the block SIGN of a frame, a cross and a tick, and an INSERT of it at the origin.</summary>
        private static CadDocument Sign(out string[] lines)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "SIGN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine[] made =
            {
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0)),
                new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0)),
            };

            foreach (DxfLine line in made)
            {
                block.AppendEntity(line);
            }

            lines = made.Select(l => l.Handle).ToArray();
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            return CadDocument.From(db, "sign.dxf");
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static CadDocument SignWithStates(out string[] lines, out CadCommandSession session)
        {
            CadDocument document = Sign(out lines);
            session = Start(document, "-BEDIT", "SIGN", "BPARAMETER", "Visibility", P(20, -10));
            Run(session, "BVSTATE", "New", "Cross", "Hide all", "BVSTATE", "New", "Tick", "Hide all");
            Run(session, "BVSTATE", "Set", "Cross", "BVSHOW", new[] { lines[1] }, Enter, "Current");
            Run(session, "BVSTATE", "Set", "Tick", "BVSHOW", new[] { lines[2] }, Enter, "Current");
            return document;
        }

        [Fact]
        public void AVisibilityParameter_StartsTheBlockWithOneStateShowingEveryObject()
        {
            CadDocument document = Sign(out string[] lines);
            CadCommandSession session = Start(document, "-BEDIT", "SIGN", "BPARAMETER", "Visibility", P(20, -10));

            Assert.Equal(new[] { "VisibilityState0" }, document.BlockEditor.VisibilityStates);
            Assert.Equal("VisibilityState0", document.BlockEditor.VisibilityState);
            Assert.Equal("Visibility parameter", Assert.Single(document.BlockEditor.Elements).TypeName);
            Assert.Equal(lines.Length, document.Database.BlockTable["SIGN"].GetVisibilityStates()[0].Entities.Count);

            Run(session, "BPARAMETER", "Visibility");
            Assert.Contains("** Visibility parameter already exists in block definition. **", session.History);
        }

        [Fact]
        public void NewStatesAndTheObjectsTheyShow_AreMadeByBvstateBvshowAndBvhide_AndAnInsertShowsTheStateItIsSetTo()
        {
            CadDocument document = SignWithStates(out string[] lines, out CadCommandSession session);

            Assert.Equal(new[] { "VisibilityState0", "Cross", "Tick" }, document.BlockEditor.VisibilityStates);
            IReadOnlyList<DxfBlockVisibilityState> states = document.Database.BlockTable["SIGN"].GetVisibilityStates();
            Assert.Equal(lines.Length, states[0].Entities.Count);
            Assert.Equal(new[] { lines[1] }, states[1].Entities.Select(e => e.Handle));
            Assert.Equal(new[] { lines[2] }, states[2].Entities.Select(e => e.Handle));

            Run(session, "BVSTATE", "Delete", "VisibilityState0");
            Assert.Equal(new[] { "Cross", "Tick" }, document.BlockEditor.VisibilityStates);

            Run(session, "BVHIDE", new[] { lines[0] }, Enter, "All");
            SaveAndClose(session);
            AssertReopens(document);

            DxfBlockReference insert = All<DxfBlockReference>(document.ModelSpace)[0];
            DxfDynamicBlockReferenceProperty state = insert.DynamicBlockReferencePropertyCollection.First(p =>
                p.PropertyName == "Visibility1"
            );
            Assert.Equal("Cross", state.Value);
            state.Value = "Tick";
            Assert.Equal("Tick", state.Value);

            List<DxfLine> shown = insert
                .BlockTableRecord.OfType<DxfLine>()
                .Where(l => !l.IsErased && l.Visible)
                .ToList();
            Assert.Equal(new GeoPoint3(10, 20, 0), Assert.Single(shown).StartPoint);
        }

        [Fact]
        public void AnObjectHiddenInTheCurrentStateAlone_IsShownInTheOthers_AndBvshowBringsItBack()
        {
            CadDocument document = SignWithStates(out string[] lines, out CadCommandSession session);
            Run(session, "BVSTATE", "Set", "Cross", "BVHIDE", new[] { lines[1] }, Enter, "Current");

            IReadOnlyList<DxfBlockVisibilityState> states = document.Database.BlockTable["SIGN"].GetVisibilityStates();
            Assert.Empty(states[1].Entities);
            Assert.Contains(lines[1], states[0].Entities.Select(e => e.Handle));

            Run(session, "BVSHOW", new[] { lines[1] }, Enter, "Current");
            states = document.Database.BlockTable["SIGN"].GetVisibilityStates();
            Assert.Equal(new[] { lines[1] }, states[1].Entities.Select(e => e.Handle));
        }

        [Fact]
        public void TheEditorShowsTheStateSet_HidingWhatItDoesNotShow_EvenWhereNoOtherStateShowsIt()
        {
            CadDocument document = SignWithStates(out string[] lines, out CadCommandSession session);
            Run(session, "BVHIDE", new[] { lines[0] }, Enter, "All");
            Run(session, "BVSTATE", "Set", "Cross");

            Assert.True(
                document.StateSetsOf(
                    document.BlockEditor.Space.Block,
                    out HashSet<string> shown,
                    out HashSet<string> hidden
                )
            );
            Assert.Contains(lines[1], shown);
            Assert.Contains(lines[0], hidden);
            Assert.Contains(lines[2], hidden);
        }

        [Fact]
        public void TheCurrentStateCannotBeDeleted_NorTheLastOne_AndANameAlreadyTakenIsRefused()
        {
            CadDocument document = SignWithStates(out _, out CadCommandSession session);

            Run(session, "BVSTATE", "Delete", "Tick");
            Assert.Contains("Cannot delete the current visibility state.", session.History);
            Run(session, "BVSTATE", "New", "Cross", "Hide all");
            Assert.Contains("The visibility state Cross already exists.", session.History);

            CadEditResult onlyOne = document.DeleteVisibilityStates(new[] { "VisibilityState0", "Cross" });
            Assert.True(onlyOne.Succeeded);
            CadEditResult last = document.DeleteVisibilityStates(new[] { "Tick" });
            Assert.False(last.Succeeded);
        }

        [Fact]
        public void AStateRenamedAndMoved_KeepsItsObjects_AndEachIsOneUndoStep()
        {
            CadDocument document = SignWithStates(out string[] lines, out _);

            Assert.True(document.RenameVisibilityState("Cross", "X mark").Succeeded);
            Assert.Equal(new[] { "VisibilityState0", "X mark", "Tick" }, document.BlockEditor.VisibilityStates);
            Assert.True(document.MoveVisibilityState("Tick", -2).Succeeded);
            Assert.Equal(new[] { "Tick", "VisibilityState0", "X mark" }, document.BlockEditor.VisibilityStates);
            Assert.Equal(
                new[] { lines[1] },
                document.Database.BlockTable["SIGN"].GetVisibilityStates()[2].Entities.Select(e => e.Handle)
            );
            Assert.False(document.RenameVisibilityState("Tick", "x MARK").Succeeded);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "VisibilityState0", "X mark", "Tick" }, document.BlockEditor.VisibilityStates);
        }

        [Fact]
        public void TheVisibilityParameterErased_ShowsEveryObjectAgain_AndAnotherCommandWithoutOneSaysSo()
        {
            CadDocument document = SignWithStates(out string[] lines, out CadCommandSession session);
            Run(session, "BVHIDE", new[] { lines[0] }, Enter, "All");

            string parameter = document.BlockEditor.Elements[0].Handle;
            Assert.True(document.EraseBlockElements(new[] { parameter }).Succeeded);

            Assert.Empty(document.BlockEditor.Elements);
            Assert.All(document.Database.BlockTable["SIGN"].OfType<DxfLine>(), l => Assert.True(l.Visible));
            Run(session, "BVSTATE");
            Assert.Contains("** No Visibility Parameter Present. **", session.History);
        }

        [Fact]
        public void AnObjectErasedInTheEditor_LeavesTheParameterAndItsStates_WhenTheBlockIsSaved()
        {
            CadDocument document = SignWithStates(out string[] lines, out CadCommandSession session);
            string erased = lines[1];

            Run(session, "ERASE", new[] { erased }, Enter);

            Assert.Matches(
                @"\n\s*33[12]\s*\n\s*" + erased + @"\s*\n",
                document.Database.ToText().Replace("\r\n", "\n")
            );

            Run(session, "BSAVE");

            Assert.DoesNotMatch(
                @"\n\s*33[12]\s*\n\s*" + erased + @"\s*\n",
                document.Database.ToText().Replace("\r\n", "\n")
            );
            Assert.Equal(new[] { "VisibilityState0", "Cross", "Tick" }, document.BlockEditor.VisibilityStates);
        }

        private static void SaveAndClose(CadCommandSession session)
        {
            Run(session, "BSAVE", "BCLOSE");
            Assert.Null(session.Document.BlockEditor);
        }
    }
}
