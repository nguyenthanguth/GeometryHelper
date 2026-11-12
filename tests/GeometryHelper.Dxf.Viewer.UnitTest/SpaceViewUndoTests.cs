using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// ZOOM and PAN typed in model space and on the paper of a layout are undo steps, one each, as AutoCAD 2021's console
    /// undoes them (probed: ZOOM 2X, ZOOM 2X and -PAN took one U each, in model space and on the paper, ZOOM Extents too,
    /// and ZOOM Previous is a step of its own), with no window.
    /// </summary>
    public class SpaceViewUndoTests
    {
        private static CadView ViewOf(CadDocument document, CadCommandSession session)
        {
            var view = new CadView(800, 600);
            view.ZoomTo(new DisplayBox(0, 0, 100, 50));
            session.View = view;
            return view;
        }

        [Fact]
        public void TypedZoomAndPanInModelSpace_AreOneUndoStepEach_UndoAndRedoRestoreTheView_TheDrawingNotReadAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 50), Enter);
            CadView view = ViewOf(document, session);
            double start = view.PixelsPerUnit;
            CadUndoStep drawn = document.NextUndo;

            Run(session, "ZOOM", "2X");
            Run(session, "ZOOM", "2X");
            Run(session, "PAN", P(0, 0), P(10, 0));
            double panned = view.CenterX;
            Assert.Equal(start * 4.0, view.PixelsPerUnit, 9);
            Assert.Equal("PAN", document.NextUndo.Name);
            DxfDatabase database = document.Database;

            // The console undid the PAN, then each ZOOM, one U each; the drawing was not touched.
            Run(session, "U");
            Assert.Equal(50.0, view.CenterX, 9);
            Assert.Equal(start * 4.0, view.PixelsPerUnit, 9);
            Run(session, "U");
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
            Run(session, "U");
            Assert.Equal(start, view.PixelsPerUnit, 9);
            Assert.Same(drawn, document.NextUndo);
            Assert.Same(database, document.Database);
            Assert.Single(All<DxfLine>(document.ModelSpace));

            // REDO puts them back, one at a time.
            Run(session, "REDO");
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
            Run(session, "REDO");
            Run(session, "REDO");
            Assert.Equal(start * 4.0, view.PixelsPerUnit, 9);
            Assert.Equal(panned, view.CenterX, 9);
            Assert.Same(database, document.Database);

            // And the next U after all of them takes the line, not a view.
            Run(session, "U");
            Run(session, "U");
            Run(session, "U");
            Run(session, "U");
            Assert.Empty(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void TypedZoomAndPanOnThePaper_AreOneUndoStepEach_AndUndoRestoresThePapersView()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.CurrentSpace = document.Spaces[1];
            var paper = new CadView(800, 600);
            paper.ZoomTo(new DisplayBox(0, 0, 420, 297));
            session.View = paper;
            double start = paper.PixelsPerUnit;
            double centerX = paper.CenterX;

            Run(session, "ZOOM", "2X");
            Run(session, "PAN", P(0, 0), P(10, 0));
            Run(session, "ZOOM", "E");
            Assert.Equal("ZOOM", document.NextUndo.Name);

            Run(session, "U");
            Assert.Equal(start * 2.0, paper.PixelsPerUnit, 9);
            Assert.Equal(centerX - 10.0, paper.CenterX, 9);
            Run(session, "U");
            Assert.Equal(centerX, paper.CenterX, 9);
            Run(session, "U");
            Assert.Equal(start, paper.PixelsPerUnit, 9);
            Assert.Null(document.NextUndo);
        }

        [Fact]
        public void ZoomPrevious_IsAnUndoStepToo_UndoPutsTheZoomBack()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadView view = ViewOf(document, session);
            double start = view.PixelsPerUnit;

            Run(session, "ZOOM", "2X");
            Run(session, "ZOOM", "2X");
            Run(session, "ZOOM", "P");
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
            Assert.Equal("ZOOM", document.NextUndo.Name);

            // AutoCAD's U after ZOOM P gave the second zoom back.
            Run(session, "U");
            Assert.Equal(start * 4.0, view.PixelsPerUnit, 9);
        }

        [Fact]
        public void AViewStep_AfterAnEdit_IsUndoneBeforeIt_AndAnEditAfterAnUndoneViewStepForgetsItsRedo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadView view = ViewOf(document, session);
            double start = view.PixelsPerUnit;
            Run(session, "ZOOM", "2X");
            Run(session, "LINE", P(0, 0), P(10, 10), Enter);
            Run(session, "ZOOM", "2X");

            Run(session, "U");
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
            Assert.Single(All<DxfLine>(document.ModelSpace));
            Run(session, "U");
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.Equal(start * 2.0, view.PixelsPerUnit, 9);
            Run(session, "U");
            Assert.Equal(start, view.PixelsPerUnit, 9);

            // A new edit now forgets the two steps that could be redone.
            Run(session, "LINE", P(0, 0), P(5, 5), Enter);
            Assert.False(document.CanRedo);
        }

        [Fact]
        public void ZoomPreviousOfTheModel_AndThePaper_KeepSeparateLists_AfterTheirViewsAreUndone()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadView model = ViewOf(document, session);
            double modelStart = model.PixelsPerUnit;
            Run(session, "ZOOM", "2X");

            document.CurrentSpace = document.Spaces[1];
            var paper = new CadView(800, 600);
            paper.ZoomTo(new DisplayBox(0, 0, 420, 297));
            session.View = paper;
            Run(session, "ZOOM", "P");
            Assert.Equal("No previous view saved.", session.History.Last());

            document.CurrentSpace = document.ModelSpace;
            session.View = model;
            Run(session, "ZOOM", "P");
            Assert.Equal(modelStart, model.PixelsPerUnit, 9);
        }
    }
}
