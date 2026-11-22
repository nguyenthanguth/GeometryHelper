using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The grips of a spline as AutoCAD 2021's GetGripPoints gives them (probed in its console: one on each fit point, or
    /// each control point where there are no fit data, in order) and their hover menu with the words of AutoCAD's grip table:
    /// Stretch Fit Point, Add Fit Point and Remove Fit Point, or Stretch Vertex, Add Vertex and Remove Vertex.
    /// </summary>
    public sealed class GripMenuSplineTests
    {
        /// <summary>The cubic through (0, 0), (100, 50), (200, 0) and (300, 80): grips 0 to 3.</summary>
        private static string FitSpline(CadDocument document)
        {
            var spline = new DxfSpline();
            document.Database.ModelSpace.AppendEntity(spline);
            spline.FitData = new DxfFitData(
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(100, 50, 0),
                    new GeoPoint3(200, 0, 0),
                    new GeoPoint3(300, 80, 0),
                },
                3,
                0.0
            );
            return spline.Handle;
        }

        /// <summary>The cubic of the control points (0, 0), (50, 100), (100, 0), (150, 100) and (200, 0): grips 0 to 4.</summary>
        private static string ControlSpline(CadDocument document)
        {
            var spline = new DxfSpline(
                3,
                false,
                false,
                false,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(50, 100, 0),
                    new GeoPoint3(100, 0, 0),
                    new GeoPoint3(150, 100, 0),
                    new GeoPoint3(200, 0, 0),
                },
                new[] { 0.0, 0, 0, 0, 0.5, 1, 1, 1, 1 },
                null,
                0,
                0
            );
            document.Database.ModelSpace.AppendEntity(spline);
            return spline.Handle;
        }

        private static string[] Texts(CadDocument document, string handle, int grip) =>
            CadGripMenus.Of(document, handle, grip).Select(i => i.Text).ToArray();

        [Fact]
        public void AFitSplineHasAGripOnEachFitPoint_AControlSplineOnEachControlPoint()
        {
            CadDocument document = Blank();
            string fit = FitSpline(document);
            string control = ControlSpline(document);

            IReadOnlyList<CadGrip> onFit = CadGrips.Of(document.FindEntity(fit));
            Assert.Equal(4, onFit.Count);
            Assert.All(onFit, g => Assert.Equal(CadGripKind.Vertex, g.Kind));
            Near(P(100, 50), onFit[1].Point);
            Near(P(300, 80), onFit[3].Point);

            IReadOnlyList<CadGrip> onControl = CadGrips.Of(document.FindEntity(control));
            Assert.Equal(5, onControl.Count);
            Near(P(50, 100), onControl[1].Point);
            Near(P(200, 0), onControl[4].Point);
        }

        [Fact]
        public void TheMenuOfASplinePoint_IsStretchAddRemove_WithTheFitPointWordsWhereThereAreFitData()
        {
            CadDocument document = Blank();
            string fit = FitSpline(document);
            string control = ControlSpline(document);

            Assert.Equal(new[] { "Stretch Fit Point", "Add Fit Point", "Remove Fit Point" }, Texts(document, fit, 1));
            Assert.Equal(new[] { "Stretch Vertex", "Add Vertex", "Remove Vertex" }, Texts(document, control, 2));
            Assert.Equal(
                new[] { CadGripAction.StretchVertex, CadGripAction.AddVertex, CadGripAction.RemoveVertex },
                CadGripMenus.Of(document, control, 2).Select(i => i.Action)
            );
            Assert.Empty(CadGripMenus.Of(document, fit, 9));
        }

        [Fact]
        public void StretchAFitPoint_TheSplineGoesThroughItAtTheNewPlace_OneUndoStep()
        {
            CadDocument document = Blank();
            string handle = FitSpline(document);

            Assert.True(CadGrips.Drag(document, handle, 1, P(110, 70)).Succeeded);

            var spline = Entity<DxfSpline>(document, handle);
            Near(new GeoPoint3(110, 70, 0), spline.GetFitPointAt(1), 1E-9);
            Assert.Equal(4, spline.NumFitPoints);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Near(new GeoPoint3(100, 50, 0), Entity<DxfSpline>(document, handle).GetFitPointAt(1), 1E-9);
        }

        [Fact]
        public void StretchAControlPoint_MovesThatPointAlone()
        {
            CadDocument document = Blank();
            string handle = ControlSpline(document);

            Assert.True(CadGrips.Drag(document, handle, 2, P(100, -40)).Succeeded);

            var spline = Entity<DxfSpline>(document, handle);
            Near(new GeoPoint3(100, -40, 0), spline.GetControlPointAt(2), 1E-9);
            Near(new GeoPoint3(50, 100, 0), spline.GetControlPointAt(1), 1E-9);
            Assert.Equal(5, spline.NumControlPoints);
        }

        [Fact]
        public void AddFitPointAfterAGrip_PutsItBetweenThatPointAndTheNext_AfterTheLastItExtendsTheEnd()
        {
            CadDocument document = Blank();
            string handle = FitSpline(document);

            Assert.True(CadGrips.AddVertex(document, handle, 1, P(150, 40)).Succeeded);

            var spline = Entity<DxfSpline>(document, handle);
            Assert.Equal(5, spline.NumFitPoints);
            Near(new GeoPoint3(150, 40, 0), spline.GetFitPointAt(2), 1E-9);
            Near(new GeoPoint3(200, 0, 0), spline.GetFitPointAt(3), 1E-9);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(4, Entity<DxfSpline>(document, handle).NumFitPoints);

            Assert.True(CadGrips.AddVertex(document, handle, 3, P(350, 20)).Succeeded);
            Near(new GeoPoint3(350, 20, 0), Entity<DxfSpline>(document, handle).GetFitPointAt(4), 1E-9);
        }

        [Fact]
        public void AddVertexOfAControlSpline_PutsInAControlPoint_AtThePointGiven()
        {
            CadDocument document = Blank();
            string handle = ControlSpline(document);

            Assert.True(CadGrips.AddVertex(document, handle, 1, P(60, 50)).Succeeded);

            var spline = Entity<DxfSpline>(document, handle);
            Assert.Equal(6, spline.NumControlPoints);
            Assert.Contains(spline.ControlPoints, p => Math.Abs(p.X - 60) < 1E-9 && Math.Abs(p.Y - 50) < 1E-9);
            Assert.True(document.Undo());
            Assert.Equal(5, Entity<DxfSpline>(document, handle).NumControlPoints);
        }

        [Fact]
        public void RemoveAFitPointOrAControlPoint_TakesItOut_AndTheLeastASplineKeepsIsRefused()
        {
            CadDocument document = Blank();
            string fit = FitSpline(document);
            string control = ControlSpline(document);

            Assert.True(CadGrips.RemoveVertex(document, fit, 1).Succeeded);
            Assert.Equal(3, Entity<DxfSpline>(document, fit).NumFitPoints);
            Assert.True(CadGrips.RemoveVertex(document, control, 2).Succeeded);
            Assert.Equal(4, Entity<DxfSpline>(document, control).NumControlPoints);

            // A cubic keeps four control points; fit data keep two points.
            Assert.False(CadGrips.RemoveVertex(document, control, 0).Succeeded);
            Assert.False(
                CadGripMenus.Of(document, control, 0).Single(i => i.Action == CadGripAction.RemoveVertex).Enabled
            );
            Assert.True(CadGrips.RemoveVertex(document, fit, 0).Succeeded);
            Assert.False(CadGrips.RemoveVertex(document, fit, 0).Succeeded);
            Assert.False(CadGripMenus.Of(document, fit, 0).Single(i => i.Action == CadGripAction.RemoveVertex).Enabled);
        }

        [Fact]
        public void AddVertexFromTheMenu_AsksAsAutoCADsStringsSay_AndAPointPutsThePointIn()
        {
            CadDocument document = Blank();
            string fit = FitSpline(document);
            string control = ControlSpline(document);
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction(fit, 1, CadGripAction.AddVertex));
            Assert.Equal("** ADD FIT POINT **", session.History.Last());
            Assert.Equal("Specify new fit point or [Base point/eXit]:", session.PromptText);
            Assert.NotNull(session.Prompt.Preview);
            Run(session, P(150, 40));
            Assert.False(session.IsRunning);
            Assert.Equal(5, Entity<DxfSpline>(document, fit).NumFitPoints);

            Assert.True(session.StartGripAction(control, 1, CadGripAction.AddVertex));
            Assert.Equal("** ADD CONTROL VERTEX **", session.History.Last());
            Assert.Equal("Specify new vertex point or [Base point/eXit]:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.Equal(5, Entity<DxfSpline>(document, control).NumControlPoints);
        }

        [Fact]
        public void RemoveVertexFromTheMenu_OfAFitPoint_TakesItOutAtOnce_AndStretchVertexAsksStretch()
        {
            CadDocument document = Blank();
            string handle = FitSpline(document);
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction(handle, 2, CadGripAction.RemoveVertex));
            Assert.False(session.IsRunning);
            Assert.Equal(3, Entity<DxfSpline>(document, handle).NumFitPoints);
            Assert.True(document.Undo());
            Assert.Equal(4, Entity<DxfSpline>(document, handle).NumFitPoints);
            Assert.True(session.StartGripAction(handle, 2, CadGripAction.RemoveVertex));
            Assert.Equal(3, Entity<DxfSpline>(document, handle).NumFitPoints);

            Assert.True(session.StartGripAction(handle, 1, CadGripAction.StretchVertex));
            Assert.Equal("** STRETCH **", session.History.Last());
            Run(session, P(100, 60));
            Near(new GeoPoint3(100, 60, 0), Entity<DxfSpline>(document, handle).GetFitPointAt(1), 1E-9);
        }

        [Fact]
        public void RemoveVertexOfAControlVertex_TakesItOutAtOnce_AsOneUndoStep()
        {
            CadDocument document = Blank();
            string handle = ControlSpline(document);
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.Equal(5, Entity<DxfSpline>(document, handle).NumControlPoints);
            Assert.True(session.StartGripAction(handle, 2, CadGripAction.RemoveVertex));
            Assert.False(session.IsRunning);
            Assert.Equal(4, Entity<DxfSpline>(document, handle).NumControlPoints);
            Assert.True(document.Undo());
            Assert.Equal(5, Entity<DxfSpline>(document, handle).NumControlPoints);
        }
    }
}
