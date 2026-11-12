using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The switch from one space to another is one undo step and ZOOM Previous lists follow U and REDO, as AutoCAD 2021's
    /// console does (probed on the paper of a layout: LAYOUT Set was one U, ZOOM 2X three times then ZOOM Previous and U gave
    /// the last zoom back with the entry returned to the list, and U of a zoom took its entry off the list), with no window.
    /// </summary>
    public class LayoutSwitchUndoTests
    {
        private static CadView ViewOf(CadCommandSession session)
        {
            var view = new CadView(800, 600);
            view.ZoomTo(new DisplayBox(0, 0, 100, 50));
            session.View = view;
            return view;
        }

        [Fact]
        public void AnotherLayoutShown_IsOneUndoStep_UndoGoesBackAndRedoForward_TheDrawingNotReadAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 50), Enter);
            DxfDatabase database = document.Database;
            CadUndoStep drawn = document.NextUndo;

            Assert.True(document.ShowSpace(document.Spaces[1]).Succeeded);

            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Assert.Equal("LAYOUT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Assert.Same(drawn, document.NextUndo);
            Assert.Same(database, document.Database);
            Assert.True(document.Redo());
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Assert.Same(database, document.Database);
            Assert.Single(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void ShowingTheSpaceAlreadyShown_MakesNoStep()
        {
            CadDocument document = Blank();

            CadEditResult result = document.ShowSpace(document.ModelSpace);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Records);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void LayoutSetTyped_IsOneStep_AndTilemodeToo_UEachTakesBackToTheSpaceBefore()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            string layout = document.Spaces[1].Name;

            Run(session, "LAYOUT", "S", layout);
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Run(session, "TILEMODE", "1");
            Assert.Same(document.ModelSpace, document.CurrentSpace);

            Run(session, "U");
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Run(session, "U");
            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Assert.False(document.CanUndo);
            Run(session, "REDO");
            Assert.Same(document.Spaces[1], document.CurrentSpace);
        }

        [Fact]
        public void ASwitchBetweenEdits_IsUndoneInOrder_AndTheSelectionBelongingToTheSpaceIsLetGo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 50), Enter);
            string line = All<DxfLine>(document.ModelSpace).Single().Handle;
            document.Selection.Set(new[] { line });
            document.ShowSpace(document.Spaces[1]);
            Assert.Equal(0, document.Selection.Count);
            Run(session, "LINE", P(0, 0), P(10, 10), Enter);
            Assert.Single(All<DxfLine>(document.Spaces[1]));

            Run(session, "U");
            Assert.Empty(All<DxfLine>(document.Spaces[1]));
            Assert.Same(document.Spaces[1], document.CurrentSpace);
            Run(session, "U");
            Assert.Same(document.ModelSpace, document.CurrentSpace);
            Run(session, "U");
            Assert.Empty(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void ZoomPrevious_ThenU_GivesTheEntryBack_SoAnotherZoomPreviousGoesToTheSameView()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadView view = ViewOf(session);
            double start = view.PixelsPerUnit;
            Run(session, "ZOOM", "2X");
            Run(session, "ZOOM", "2X");

            Run(session, "ZOOM", "P");
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
            Run(session, "U");
            Assert.Equal(start * 4.0, view.PixelsPerUnit, 9);

            // AutoCAD: the P that follows goes to the view the first P went to, then to the one before it.
            Run(session, "ZOOM", "P");
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
            Run(session, "ZOOM", "P");
            Assert.Equal(start, view.PixelsPerUnit, 9);
        }

        [Fact]
        public void AZoomUndone_TakesItsEntryOffTheList_ZoomPreviousGoesToTheViewBeforeIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadView view = ViewOf(session);
            double start = view.PixelsPerUnit;
            Run(session, "ZOOM", "2X");
            Run(session, "ZOOM", "2X");
            Run(session, "ZOOM", "2X");

            Run(session, "U");
            Assert.Equal(start * 4.0, view.PixelsPerUnit, 9);
            Run(session, "ZOOM", "P");
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
        }

        [Fact]
        public void AZoomRedone_PutsItsEntryBack()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadView view = ViewOf(session);
            double start = view.PixelsPerUnit;
            Run(session, "ZOOM", "2X");
            Run(session, "ZOOM", "2X");
            Run(session, "U");
            Run(session, "U");
            Run(session, "REDO");
            Run(session, "REDO");

            Run(session, "ZOOM", "P");
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
            Run(session, "ZOOM", "P");
            Assert.Equal(start, view.PixelsPerUnit, 9);
            Run(session, "ZOOM", "P");
            Assert.Equal("No previous view saved.", session.History.Last());
        }

        [Fact]
        public void ARealTimeGestureJoinedToTheOneBefore_TakesBothEntriesOffTogether_WhenUndone()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadView view = ViewOf(session);
            double start = view.PixelsPerUnit;

            // Two wheel gestures made straight after each other: one undo step, two entries.
            DisplayBox first = view.VisibleBox;
            view.ZoomAt(400, 300, 1.2);
            document.RecordSpaceView(document.ModelSpace, view, first, "ZOOM", true);
            DisplayBox second = view.VisibleBox;
            view.ZoomAt(400, 300, 1.2);
            document.RecordSpaceView(document.ModelSpace, view, second, "ZOOM", true);

            Run(session, "U");
            Assert.Equal(start, view.PixelsPerUnit, 9);
            Run(session, "ZOOM", "P");
            Assert.Equal("No previous view saved.", session.History.Last());
            Run(session, "REDO");
            Run(session, "ZOOM", "P");
            Assert.Equal(start * 1.2, view.PixelsPerUnit, 9);
        }

        [Fact]
        public void AViewportsZoomPrevious_FollowsUndoAndRedoToo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            string handle = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            var paper = new CadView(800, 600);
            paper.ZoomTo(new DisplayBox(0, 0, 420, 297));
            session.View = paper;
            document.EnterViewport(handle);
            DxfViewport viewport = Entity<DxfViewport>(document, handle);
            double start = viewport.ViewHeight;
            Run(session, "ZOOM", "2X");
            Run(session, "ZOOM", "2X");

            Run(session, "ZOOM", "P");
            Assert.Equal(start / 2.0, Entity<DxfViewport>(document, handle).ViewHeight, 9);
            Run(session, "U");
            Assert.Equal(start / 4.0, Entity<DxfViewport>(document, handle).ViewHeight, 9);
            Run(session, "ZOOM", "P");
            Assert.Equal(start / 2.0, Entity<DxfViewport>(document, handle).ViewHeight, 9);
            Run(session, "ZOOM", "P");
            Assert.Equal(start, Entity<DxfViewport>(document, handle).ViewHeight, 9);

            // Back past the zooms: each U takes its entry off; nothing is left to go back to.
            Run(session, "U");
            Run(session, "U");
            Run(session, "U");
            Run(session, "U");
            Assert.Equal(start, Entity<DxfViewport>(document, handle).ViewHeight, 9);
            Run(session, "ZOOM", "P");
            Assert.Equal("No previous view saved.", session.History.Last());
        }
    }
}
