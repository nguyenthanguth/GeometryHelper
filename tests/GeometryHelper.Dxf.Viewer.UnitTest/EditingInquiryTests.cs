using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// DIST, AREA, ID and LIST write what they measure in AutoCAD's words and change nothing; ELLIPSE and POINT draw, and
    /// LAYOUT New adds a layout, each one undo step.
    /// </summary>
    public class EditingInquiryTests
    {
        private static CadCommandSession Measure(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        [Fact]
        public void DistWritesTheDistanceAnglesAndDeltasAsAutoCadDoes_AndChangesNothing()
        {
            // AUPREC 0, as AutoCAD's template has it: angles to whole degrees.
            CadDocument document = Plan();
            document.Database.Auprec = 0;

            CadCommandSession session = Measure(document, "DI", P(0, 0), P(100, 100));

            Assert.Contains("Distance = 141.4214,  Angle in XY Plane = 45,  Angle from XY Plane = 0", session.History);
            Assert.Equal("Delta X = 100.0000,  Delta Y = 100.0000,  Delta Z = 0.0000", session.History.Last());
            Assert.False(document.IsModified);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void DistWritesAnAngleDownwardsAsTheTurnFromNought_ToThePlacesOfAuprec()
        {
            // At the plan's AUPREC of 2, angles are written to two places.
            CadDocument document = Plan();

            CadCommandSession session = Measure(document, "DIST", P(0, 0), P(0, -20));

            Assert.Contains("Distance = 20.0000,  Angle in XY Plane = 270.00,  Angle from XY Plane = 0.00", session.History);
        }

        [Fact]
        public void IdWritesTheCoordinatesOfThePoint()
        {
            CadDocument document = Plan();

            CadCommandSession session = Measure(document, "ID");

            Assert.Equal("Specify point:", session.PromptText);
            Run(session, "12.5,-3");
            Assert.Equal("X = 12.5000     Y = -3.0000     Z = 0.0000", session.History.Last());
        }

        [Fact]
        public void AreaOfPointsClosesBackToTheFirst_EnterTakingTotal()
        {
            CadDocument document = Plan();
            CadCommandSession session = Measure(document, "AA");

            Assert.Equal("Specify first corner point or [Object] <Object>:", session.PromptText);
            Run(session, P(0, 0), P(100, 0));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, P(100, 50), P(0, 50));
            Assert.Equal("Specify next point or [Undo/Total] <Total>:", session.PromptText);
            Run(session, Enter);

            Assert.Equal("Area = 5000.0000, Perimeter = 300.0000", session.History.Last());
            Assert.False(session.IsRunning);
            Assert.False(document.IsModified);
        }

        [Fact]
        public void AreaOfAnObject_ACircle_AnOpenPolyline_AndALineThatHasNone()
        {
            CadDocument document = Plan();

            CadCommandSession session = Measure(document, "AREA", Enter, new[] { "A3" });
            Assert.Equal("Area = 314.1593, Circumference = 62.8319", session.History.Last());

            Run(session, "AREA", "O", new[] { "A5" });
            Assert.Equal("Area = 0.0000, Length = 150.0000", session.History.Last());

            Run(session, "AREA", "O", new[] { "A1" });
            Assert.Equal("Selected object does not have an area.", session.History.Last());
        }

        [Fact]
        public void ListWritesALineAsAutoCadDoes()
        {
            CadDocument document = Plan();
            document.Database.Auprec = 0;

            CadCommandSession session = Measure(document, "LI", new[] { "A1" }, Enter);

            Assert.Contains("                  LINE      Layer: \"Steel\"", session.History);
            Assert.Contains("                            Space: Model space", session.History);
            Assert.Contains("                   Handle = a1", session.History);
            Assert.Contains("              from point, X=   0.0000  Y=   0.0000  Z=   0.0000", session.History);
            Assert.Contains("                to point, X= 100.0000  Y=   0.0000  Z=   0.0000", session.History);
            Assert.Contains("          Length = 100.0000,  Angle in XY Plane =      0", session.History);
            Assert.False(document.IsModified);
        }

        [Fact]
        public void ListTakesTheObjectsSelectedFirst_ACircleAndATextInHandleOrder()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A6", "A3" });

            CadCommandSession session = Measure(document, "LIST");

            int circle = session.History.ToList().IndexOf("                  CIRCLE    Layer: \"0\"");
            int text = session.History.ToList().IndexOf("                  TEXT      Layer: \"0\"");
            Assert.True(circle >= 0 && text > circle, circle + " " + text);
            Assert.Contains("            radius   10.0000", session.History);
            Assert.Contains("              area  314.1593", session.History);
            Assert.Contains("                  Contents = NOTE", session.History);
        }

        [Fact]
        public void EllipseByTheEndsOfAnAxis_ThenTheOtherHalf_IsOneUndoStep()
        {
            CadDocument document = Blank();

            CadCommandSession session = Measure(document, "EL");
            Assert.Equal("Specify axis endpoint of ellipse or [Center]:", session.PromptText);
            Run(session, P(0, 0), P(100, 0));
            Assert.Equal("Specify distance to other axis or [Rotation]:", session.PromptText);
            Run(session, "25");

            DxfEllipse ellipse = Assert.Single(All<DxfEllipse>(document.ModelSpace));
            Near(new GeoPoint3(50, 0, 0), ellipse.Center);
            Assert.Equal(50.0, ellipse.MajorRadius, 9);
            Assert.Equal(0.5, ellipse.RadiusRatio, 9);
            Assert.True(ellipse.Closed);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Empty(All<DxfEllipse>(document.ModelSpace));
        }

        [Fact]
        public void EllipseByItsCentre_TheLongerHalfBecomingTheMajorAxis_AndByARotation()
        {
            CadDocument document = Blank();

            Measure(document, "ELLIPSE", "C", P(0, 0), P(10, 0), "20");

            DxfEllipse tall = Assert.Single(All<DxfEllipse>(document.ModelSpace));
            Assert.Equal(20.0, tall.MajorRadius, 9);
            Assert.Equal(0.0, tall.MajorAxis.X, 9);
            Assert.Equal(0.5, tall.RadiusRatio, 9);

            Measure(document, "ELLIPSE", P(0, 100), P(40, 100), "R", "60");

            DxfEllipse turned = All<DxfEllipse>(document.ModelSpace).Last();
            Assert.Equal(20.0, turned.MajorRadius, 9);
            Assert.Equal(0.5, turned.RadiusRatio, 9);
        }

        [Fact]
        public void PointSaysThePointModesAndDrawsAPointObject()
        {
            CadDocument document = Blank();

            CadCommandSession session = Measure(document, "PO");
            Assert.Equal("Current point modes:  PDMODE=0  PDSIZE=0.0000", session.History.Last());
            Assert.Equal("Specify a point:", session.PromptText);
            Run(session, P(5, 5));

            DxfDBPoint point = Assert.Single(All<DxfDBPoint>(document.ModelSpace));
            Near(new GeoPoint3(5, 5, 0), point.Position);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfDBPoint>(document.ModelSpace));
        }

        [Fact]
        public void LayoutNewAddsTheLayoutOfferedAfterTheLast_AndUndoTakesItBack()
        {
            CadDocument document = Blank();
            Assert.Equal(new[] { "Model", "Layout1" }, document.Spaces.Select(s => s.Name));

            CadCommandSession session = Measure(document, "LO");
            Assert.Equal("Enter layout option [New/?] <New>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter name of new layout <Layout2>:", session.PromptText);
            Run(session, Enter);

            Assert.Equal(new[] { "Model", "Layout1", "Layout2" }, document.Spaces.Select(s => s.Name));
            Assert.Equal("Layout \"Layout2\" created.", session.History.Last());
            Assert.Single(All<DxfViewport>(document.Spaces[2]));
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "Model", "Layout1" }, document.Spaces.Select(s => s.Name));
            Assert.True(document.Redo());
            Assert.Equal("Layout2", document.Spaces.Last().Name);
        }

        [Fact]
        public void LayoutNewRefusesANameTakenAndListsTheLayouts()
        {
            CadDocument document = Blank();

            CadCommandSession session = Measure(document, "LAYOUT", "N", "layout1");
            Assert.Equal("A layout named \"layout1\" already exists.", session.History.Last());

            Run(session, "LAYOUT", "N", "Plan 1-50");
            Assert.Equal("Plan 1-50", document.Spaces.Last().Name);

            Run(session, "LAYOUT", "?");
            Assert.Equal(new[] { "Model", "Layout1", "Plan 1-50" }, session.History.Skip(session.History.Count - 3));
        }

        [Fact]
        public void TheNewCommandsHaveAutoCadsAliases()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            Assert.Equal("DIST", registry.Find("DI").Name);
            Assert.Equal("AREA", registry.Find("AA").Name);
            Assert.Equal("LIST", registry.Find("LI").Name);
            Assert.Equal("LIST", registry.Find("LS").Name);
            Assert.Equal("ID", registry.Find("ID").Name);
            Assert.Equal("ELLIPSE", registry.Find("EL").Name);
            Assert.Equal("POINT", registry.Find("PO").Name);
            Assert.Equal("LAYOUT", registry.Find("LO").Name);
        }
    }
}
