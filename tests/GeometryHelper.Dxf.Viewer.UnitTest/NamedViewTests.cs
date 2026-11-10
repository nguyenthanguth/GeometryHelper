using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Named views as AutoCAD 2021 keeps and asks them: -VIEW's ?, Save, Window, Restore, sEttings Layer snapshot and Delete,
    /// each view a VIEW record, its layer snapshot the hidden layer state ACAD_VIEWS_ and its name.
    /// </summary>
    public class NamedViewTests
    {
        private static CadDocument Plan()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "A", ColorIndex = 1 });
            return CadDocument.From(db, "plan.dxf");
        }

        private static CadCommandSession Viewing(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.View = new CadView(400, 300);
            session.View.ZoomTo(new DisplayBox(0, 0, 400, 300), 0.0);
            return session;
        }

        [Fact]
        public void ViewWindow_SavesTheViewAutoCad2021Saved_AndQuestionMarkListsItAsAutoCadDoes()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Viewing(document), "-VIEW", "?", Enter);
            Assert.Equal(new[] { "Saved views:", "  No matching views found." }, session.History.Skip(session.History.Count - 2));

            Run(session, "-VIEW", "W", "V2", "10,20", "110,80");
            Assert.False(session.IsRunning);
            DxfViewTableRecord view = document.Database.ViewTable["v2"];
            Assert.Equal(60.0, view.CenterPoint.X, 9);
            Assert.Equal(50.0, view.CenterPoint.Y, 9);
            Assert.Equal(100.0, view.Width, 9);
            Assert.Equal(60.0, view.Height, 9);
            Assert.False(view.IsPaperspaceView);
            Assert.Equal("-VIEW", document.NextUndo.Name);
            AssertReopens(document);

            Run(session, "-VIEW", "?", "*");
            Assert.Equal(new[] { "Saved views:", "View name                        Space", "\"V2\"                               M" }, session.History.Skip(session.History.Count - 3));
        }

        [Fact]
        public void ViewLayerSnapshot_IsTheHiddenStateAcadViews_AndRestoreBringsTheViewAndItsLayersBack()
        {
            CadDocument document = Plan();
            CadCommandSession session = Run(Viewing(document), "-VIEW", "S", "V1", "-VIEW", "E", "L", "V1", "S");
            Assert.False(session.IsRunning);
            DxfViewTableRecord view = document.Database.ViewTable["V1"];
            Assert.Equal("ACAD_VIEWS_V1", view.LayerState);
            Assert.Equal(new[] { "ACAD_VIEWS_V1" }, document.Database.LayerStateManager.GetLayerStateNames());
            Assert.Empty(document.Database.LayerStateManager.GetLayerStateNames(false, true));
            Assert.Equal(200.0, view.CenterPoint.X, 9);
            Assert.Equal(400.0, view.Width, 9);
            AssertReopens(document);

            CadLayerEditor.SetOn(document, "A", false);
            session.View.ZoomTo(new DisplayBox(1000, 1000, 1100, 1075), 0.0);
            Run(session, "-VIEW", "R", "v1");
            Assert.Equal(200.0, session.View.CenterX, 6);
            Assert.Equal(150.0, session.View.CenterY, 6);
            Assert.False(document.Database.LayerTable["A"].IsOff);

            Run(session, "-VIEW", "D", "V*");
            Assert.Empty(document.Database.ViewTable.Cast<DxfViewTableRecord>());
            Assert.Empty(document.Database.LayerStateManager.GetLayerStateNames());
            AssertReopens(document);
        }
    }
}
