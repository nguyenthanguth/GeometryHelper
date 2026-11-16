using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The hover menus of a line's and an arc's grips and Convert to Line of a polyline's, with the words and prompts of
    /// AutoCAD 2021's grip table (<c>acdb24res.dll</c>): an end offers Stretch and Lengthen, the middle of an arc Stretch and
    /// Radius, and Convert to Line asks "Pick to convert to line segment:".
    /// </summary>
    public class GripMenuLineArcTests
    {
        private static string[] Texts(CadDocument document, string handle, int grip) =>
            CadGripMenus.Of(document, handle, grip).Select(i => i.Text).ToArray();

        // A line A1 from (0, 0) to (100, 0) has grips 0 (start), 1 (end) and 2 (middle); the arc A4 of radius 20 about
        // (300, 0) from 0 to 90 degrees has 0 (start), 1 (end), 2 (middle) and 3 (centre).
        [Fact]
        public void AnEndOfALineOrAnArcOffersStretchAndLengthen_TheMiddleOfAnArcStretchAndRadius()
        {
            CadDocument document = Plan();

            Assert.Equal(new[] { "Stretch", "Lengthen" }, Texts(document, "A1", 0));
            Assert.Equal(new[] { "Stretch", "Lengthen" }, Texts(document, "A1", 1));
            Assert.Equal(new[] { "Stretch", "Lengthen" }, Texts(document, "A4", 0));
            Assert.Equal(new[] { "Stretch", "Lengthen" }, Texts(document, "A4", 1));
            Assert.Equal(new[] { "Stretch", "Radius" }, Texts(document, "A4", 2));
            Assert.Equal(
                new[] { CadGripAction.Stretch, CadGripAction.Lengthen },
                CadGripMenus.Of(document, "A1", 1).Select(i => i.Action)
            );
        }

        [Fact]
        public void TheMiddleOfALine_TheCentreOfAnArc_AndACircle_HaveNoMenu()
        {
            CadDocument document = Plan();

            Assert.Empty(CadGripMenus.Of(document, "A1", 2));
            Assert.Empty(CadGripMenus.Of(document, "A4", 3));
            Assert.Empty(CadGripMenus.Of(document, "A3", 0));
            Assert.Empty(CadGripMenus.Of(document, "A3", 1));
            Assert.Empty(CadGripMenus.Of(document, "A1", 7));
        }

        [Fact]
        public void LengthenTheEndOfALine_PutsItWhereThePointFallsAlongTheLine_AsOneUndoStep()
        {
            CadDocument document = Plan();

            Assert.True(CadGrips.Lengthen(document, "A1", 1, P(150, 40)).Succeeded);

            var line = Entity<DxfLine>(document, "A1");
            Near(P(0, 0), P(line.StartPoint.X, line.StartPoint.Y));
            Near(P(150, 0), P(line.EndPoint.X, line.EndPoint.Y));
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.False(document.CanUndo);
            Near(P(100, 0), P(Entity<DxfLine>(document, "A1").EndPoint.X, Entity<DxfLine>(document, "A1").EndPoint.Y));
        }

        [Fact]
        public void LengthenTheStartOfALine_KeepsTheEnd_AndAPointBehindTheKeptEndIsRefused()
        {
            CadDocument document = Plan();

            Assert.True(CadGrips.Lengthen(document, "A1", 0, P(-30, 10)).Succeeded);
            var line = Entity<DxfLine>(document, "A1");
            Near(P(-30, 0), P(line.StartPoint.X, line.StartPoint.Y));
            Near(P(100, 0), P(line.EndPoint.X, line.EndPoint.Y));

            // The end at (100, 0) dragged to a point behind the start would turn the line round.
            CadEditResult refused = CadGrips.Lengthen(document, "A1", 1, P(-40, 0));
            Assert.False(refused.Succeeded);
            Near(P(100, 0), P(Entity<DxfLine>(document, "A1").EndPoint.X, Entity<DxfLine>(document, "A1").EndPoint.Y));
        }

        [Fact]
        public void LengthenTheEndOfAnArc_TakesItRoundTheArcToTheAngleOfThePoint_TheRadiusAndTheStartKept()
        {
            CadDocument document = Plan();

            Assert.True(CadGrips.Lengthen(document, "A4", 1, P(290, 30)).Succeeded);

            var arc = Entity<DxfArc>(document, "A4");
            Assert.Equal(20.0, arc.Radius, 9);
            Assert.Equal(0.0, arc.StartAngle, 9);
            Assert.Equal(Math.Atan2(30, -10), arc.EndAngle, 9);
            Assert.Equal("GRIP", document.NextUndo.Name);

            Assert.True(CadGrips.Lengthen(document, "A4", 0, P(300, -40)).Succeeded);
            Assert.Equal(1.5 * Math.PI, Entity<DxfArc>(document, "A4").StartAngle, 9);
        }

        [Fact]
        public void LengthenAnArcToItsOwnOtherEnd_OrTheCentre_IsRefused()
        {
            CadDocument document = Plan();

            Assert.False(CadGrips.Lengthen(document, "A4", 1, P(320, 0)).Succeeded);
            Assert.False(CadGrips.Lengthen(document, "A4", 1, P(300, 0)).Succeeded);
            Assert.Equal(Math.PI / 2.0, Entity<DxfArc>(document, "A4").EndAngle, 9);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void RadiusInTheMiddleOfAnArc_IsTheDistanceFromTheCentre_TheAnglesKept()
        {
            CadDocument document = Plan();

            Assert.True(CadGrips.ChangeRadius(document, "A4", 2, P(300, 30)).Succeeded);

            var arc = Entity<DxfArc>(document, "A4");
            Assert.Equal(30.0, arc.Radius, 9);
            Assert.Equal(0.0, arc.StartAngle, 9);
            Assert.Equal(Math.PI / 2.0, arc.EndAngle, 9);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.False(CadGrips.ChangeRadius(document, "A4", 2, P(300, 0)).Succeeded);
            Assert.False(CadGrips.ChangeRadius(document, "A4", 0, P(300, 30)).Succeeded);
            Assert.Equal(30.0, Entity<DxfArc>(document, "A4").Radius, 9);
        }

        [Fact]
        public void LengthenAndRadiusOnALockedLayer_AreRefused()
        {
            CadDocument document = Plan();

            Assert.False(CadGrips.Lengthen(document, "A8", 1, P(150, -50)).Succeeded);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void LengthenFromTheMenu_AsksForTheEndPoint_PreviewsIt_AndAPointMakesTheEdit()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("A1", 1, CadGripAction.Lengthen));

            Assert.Equal("** LENGTHEN **", session.History.Last());
            Assert.Equal("Specify end point or [Base point/eXit]:", session.PromptText);
            Assert.NotNull(session.Prompt.Preview);

            Run(session, P(180, 25));

            Assert.False(session.IsRunning);
            Near(P(180, 0), P(Entity<DxfLine>(document, "A1").EndPoint.X, Entity<DxfLine>(document, "A1").EndPoint.Y));
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void LengthenAnArcFromTheMenu_SaysEndPointOfArc_AndRadiusAsksForARadius()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("A4", 1, CadGripAction.Lengthen));
            Assert.Equal("Specify end point of arc or [Base point/eXit]:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.False(document.CanUndo);

            Assert.True(session.StartGripAction("A4", 2, CadGripAction.Radius));
            Assert.Equal("** RADIUS **", session.History.Last());
            Assert.Equal("Specify radius or [Base point/eXit]:", session.PromptText);
            Run(session, P(300, 35));
            Assert.Equal(35.0, Entity<DxfArc>(document, "A4").Radius, 9);
        }

        [Fact]
        public void LengthenWithABasePoint_TakesThePointAsFarFromItAsTheGripIsFromWhereItStands()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("A1", 1, CadGripAction.Lengthen));
            Run(session, "Base point", P(10, 10), P(70, 10));

            // From the base (10, 10) to (70, 10) is 60 along, so the end (100, 0) goes to (160, 0).
            Near(P(160, 0), P(Entity<DxfLine>(document, "A1").EndPoint.X, Entity<DxfLine>(document, "A1").EndPoint.Y));
        }

        [Fact]
        public void ALengthenThatCannotBeMade_SaysSoAndAsksAgain_AndEscLeavesTheLineAlone()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("A1", 1, CadGripAction.Lengthen));
            Run(session, P(-50, 0));

            Assert.True(session.IsRunning);
            Assert.Equal(
                "The point would leave the line no length.",
                session.History.Last(h => h.StartsWith("The point"))
            );
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Near(P(100, 0), P(Entity<DxfLine>(document, "A1").EndPoint.X, Entity<DxfLine>(document, "A1").EndPoint.Y));
        }

        // A polyline from (0, 0) with an arc segment of bulge 1 to (100, 0) and a straight one to (100, 100): grips 0 to 2 are
        // its vertices, 3 the middle of the arc segment and 4 the middle of the straight one.
        private static CadDocument ArcPolyline() =>
            CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Polyline("0", "M1", false, 0, 0, 1, 100, 0, 0, 100, 100, 0)
                ),
                "convert.dxf"
            );

        [Fact]
        public void ConvertToLineFromTheMenu_AsksForAPick_AsAutoCADsStringSays_AndAPointMakesTheSegmentStraight()
        {
            CadDocument document = ArcPolyline();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("M1", 3, CadGripAction.ConvertToLine));

            Assert.True(session.IsRunning);
            Assert.Equal("** CONVERT TO LINE **", session.History.Last());
            Assert.Equal("Pick to convert to line segment:", session.PromptText);
            Assert.Equal(1.0, Entity<DxfPolyline>(document, "M1").GetBulgeAt(0));

            Run(session, P(50, -50));

            Assert.False(session.IsRunning);
            Assert.Equal(0.0, Entity<DxfPolyline>(document, "M1").GetBulgeAt(0));
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.False(document.CanUndo);
            Assert.Equal(1.0, Entity<DxfPolyline>(document, "M1").GetBulgeAt(0));
        }

        [Fact]
        public void ConvertToLineFromTheMenu_EscAtItsPromptLeavesTheArcSegmentAsItWas()
        {
            CadDocument document = ArcPolyline();
            CadCommandSession session = Session(document);

            Assert.True(session.StartGripAction("M1", 3, CadGripAction.ConvertToLine));
            Run(session, Esc);

            Assert.False(session.IsRunning);
            Assert.Equal(1.0, Entity<DxfPolyline>(document, "M1").GetBulgeAt(0));
            Assert.False(document.CanUndo);
        }
    }
}
