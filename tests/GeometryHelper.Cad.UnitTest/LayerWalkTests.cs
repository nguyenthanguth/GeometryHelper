using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// LAYWALK, AutoCAD 2021's LayerWalk: the layers selected in its dialog are the only ones shown, every other layer turned
    /// off, and the layers go back as they were when it closes, unless Restore on exit is cleared. Select objects picks layers
    /// on the canvas; Purge Layers deletes the unused ones selected.
    /// </summary>
    public class LayerWalkTests
    {
        /// <summary>Layers WALL (A1), DOOR (A2 and A3) and TRIM (nothing on it), the current layer 0.</summary>
        private static CadDocument Walls()
        {
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("WALL", 1) + TestDrawings.Layer("DOOR", 2) + TestDrawings.Layer("TRIM", 3),
                string.Empty,
                TestDrawings.Line(0, 0, 100, 0, "WALL", handle: "A1"),
                TestDrawings.Line(0, 10, 100, 10, "DOOR", handle: "A2"),
                TestDrawings.Circle(50, 50, 5, "DOOR", handle: "A3")
            );
            return CadDocument.From(database, "walls.dxf");
        }

        private static List<string> Off(CadDocument document) =>
            document.Database.LayerTable.Where(l => l.IsOff).Select(l => l.Name).OrderBy(n => n).ToList();

        private static CadCommandSession Walking(CadDocument document, System.Action<CadLayerWalkRequest> dialog)
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "LAYWALK")
                {
                    dialog((CadLayerWalkRequest)e.Payload);
                    e.Handled = true;
                }
            };
            return session;
        }

        [Fact]
        public void WalkingOnDoor_ShowsOnlyDoor_AndClosingPutsEveryLayerBack()
        {
            CadDocument document = Walls();
            List<string> seen = null;
            CadCommandSession session = Walking(document, request =>
            {
                Assert.Equal(new[] { "0", "DOOR", "TRIM", "WALL" }, request.Rows.Select(r => r.Name));
                Assert.True(request.Walk(new[] { "door" }));
                seen = Off(document);
                Assert.Equal(new[] { "DOOR" }, request.Selected);
            });

            Run(session, "LAYWALK");

            Assert.Equal(new[] { "0", "TRIM", "WALL" }, seen);
            Assert.Empty(Off(document));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void WalkingTwoLayers_ThenNone_ShowsBothThenEveryLayerAsItWas()
        {
            CadDocument document = Walls();
            var seen = new List<List<string>>();
            CadCommandSession session = Walking(document, request =>
            {
                request.Walk(new[] { "WALL", "DOOR" });
                seen.Add(Off(document));
                request.Walk(new string[0]);
                seen.Add(Off(document));
                request.Walk(new[] { "TRIM" });
                seen.Add(Off(document));
            });

            Run(session, "LAYWALK");

            Assert.Equal(new[] { "0", "TRIM" }, seen[0]);
            Assert.Empty(seen[1]);
            Assert.Equal(new[] { "0", "DOOR", "WALL" }, seen[2]);
            Assert.Empty(Off(document));
        }

        [Fact]
        public void ALayerFrozenBeforeTheWalk_IsThawedWhileItIsWalked_AndFrozenAgainWhenItIsNot()
        {
            CadDocument document = Walls();
            Assert.True(CadLayerEditor.SetFrozen(document, "TRIM", true).Succeeded);
            var frozen = new List<bool>();
            CadCommandSession session = Walking(document, request =>
            {
                request.Walk(new[] { "TRIM" });
                frozen.Add(document.Database.LayerTable["TRIM"].IsFrozen);
                request.Walk(new[] { "WALL" });
                frozen.Add(document.Database.LayerTable["TRIM"].IsFrozen);
            });

            Run(session, "LAYWALK");

            Assert.Equal(new[] { false, true }, frozen);
            Assert.True(document.Database.LayerTable["TRIM"].IsFrozen);
            Assert.False(document.Database.LayerTable["TRIM"].IsOff);
        }

        [Fact]
        public void RestoreOnExitCleared_LeavesTheWalkInTheDrawing_AndOneUndoTakesItBack()
        {
            CadDocument document = Walls();
            CadCommandSession session = Walking(document, request =>
            {
                request.Walk(new[] { "WALL" });
                request.RestoreOnExit = false;
            });

            Run(session, "LAYWALK");

            Assert.Equal(new[] { "0", "DOOR", "TRIM" }, Off(document));
            Assert.Equal("LAYWALK", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Empty(Off(document));
        }

        [Fact]
        public void TheFilterTakesWildcards_ListingTheLayersThatMatchAnyPattern()
        {
            CadDocument document = Walls();
            var listed = new List<string[]>();
            CadCommandSession session = Walking(document, request =>
            {
                listed.Add(request.Listed.Select(r => r.Name).ToArray());
                request.Filter = "W*,T?IM";
                listed.Add(request.Listed.Select(r => r.Name).ToArray());
                request.Filter = "*";
                listed.Add(request.Listed.Select(r => r.Name).ToArray());
                request.Filter = "nothing";
                listed.Add(request.Listed.Select(r => r.Name).ToArray());
            });

            Run(session, "LAYWALK");

            Assert.Equal(new[] { "0", "DOOR", "TRIM", "WALL" }, listed[0]);
            Assert.Equal(new[] { "TRIM", "WALL" }, listed[1]);
            Assert.Equal(listed[0], listed[2]);
            Assert.Empty(listed[3]);
        }

        [Fact]
        public void PurgeLayers_DeletesTheUnusedLayersSelected_AndKeepsAUsedOne()
        {
            CadDocument document = Walls();
            IReadOnlyList<string> purged = null;
            var rows = new List<bool>();
            CadCommandSession session = Walking(document, request =>
            {
                rows.AddRange(request.Rows.Select(r => r.IsUsed));
                request.Walk(new[] { "WALL" });
                purged = request.Purge(new[] { "TRIM", "WALL", "0" });
                Assert.Equal(new[] { "0", "DOOR", "WALL" }, request.Rows.Select(r => r.Name));
            });

            Run(session, "LAYWALK");

            Assert.Equal(new[] { "TRIM" }, purged);
            Assert.False(document.Database.LayerTable.Has("TRIM"));
            Assert.Equal(new[] { true, true, false, true }, rows);
            Assert.Empty(Off(document));
        }

        [Fact]
        public void SelectObjects_AsksOnTheCanvas_WalksTheLayersOfTheObjects_AndShowsTheDialogAgain()
        {
            CadDocument document = Walls();
            var visits = new List<IReadOnlyList<string>>();
            int shown = 0;
            CadCommandSession session = Walking(document, request =>
            {
                visits.Add(request.Selected);
                request.Action = shown++ == 0 ? CadDialogAction.SelectObjects : CadDialogAction.Cancel;
            });

            Run(session, "LAYWALK");
            Assert.Equal("Select objects on the layers to walk:", session.PromptText);

            Run(session, (object)new[] { "A2", "A3", "A1" }, Enter);

            Assert.Equal(2, visits.Count);
            Assert.Empty(visits[0]);
            Assert.Equal(new[] { "DOOR", "WALL" }, visits[1]);
            Assert.Empty(Off(document));
        }

        [Fact]
        public void EscapeWhileSelectingObjects_PutsTheLayersBackAsWell()
        {
            CadDocument document = Walls();
            CadCommandSession session = Walking(document, request =>
            {
                request.Walk(new[] { "DOOR" });
                request.Action = CadDialogAction.SelectObjects;
            });

            Run(session, "LAYWALK");
            Assert.Equal(new[] { "0", "TRIM", "WALL" }, Off(document));

            Run(session, Esc);

            Assert.Empty(Off(document));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void WithNoWindow_TheCommandSaysItIsTheDialogOfTheWindow()
        {
            CadDocument document = Walls();
            CadCommandSession session = Run(Session(document), "LAYWALK");

            Assert.Contains("LayerWalk dialog", session.History.Last());
            Assert.Empty(Off(document));
        }

        [Fact]
        public void TheCommandIsRegistered_WithNoAlias()
        {
            Assert.NotNull(CadCommandRegistry.CreateDefault().Find("LAYWALK"));
        }
    }
}
