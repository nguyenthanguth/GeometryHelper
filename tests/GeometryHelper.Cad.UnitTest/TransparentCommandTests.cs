using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// DIST, ID, LAYER, COLOR, LINETYPE, LWEIGHT, UNITS, QUICKCALC, TEXTSCR and the name of any system variable run inside
    /// another command as AutoCAD 2021 runs them, asking after "&gt;&gt;" and giving the prompt back with "Resuming LINE
    /// command."; the dialogs' names run their command-line forms, as AutoCAD's console does.
    /// </summary>
    public class TransparentCommandTests
    {
        private static CadCommandSession Line(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, "LINE", P(0, 300));
        }

        private static void AssertResumedLine(CadCommandSession session)
        {
            Assert.False(session.IsTransparent);
            Assert.Equal("LINE", session.Current.Name);
            Assert.Equal("Resuming LINE command.", session.History.Last());
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
        }

        [Fact]
        public void DistTypedAtLinesNextPoint_MeasuresTwoPoints_AndTheLineGoesOnFromItsOwnLastPoint()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'DIST");
            Assert.True(session.IsTransparent);
            Assert.Equal(">>Specify first point:", session.PromptText);
            Run(session, P(10, 10), P(13, 14));

            Assert.Contains(session.History, l =>
                l.StartsWith("Distance = 5.0000,  Angle in XY Plane = 53", StringComparison.Ordinal)
            );
            AssertResumedLine(session);

            // The point DIST took is not the line's: the line runs from where it started.
            Run(session, "@100<0", Enter);
            DxfLine made = All<DxfLine>(document.ModelSpace).Last();
            Near(new GeoPoint3(0, 300, 0), made.StartPoint);
            Near(new GeoPoint3(100, 300, 0), made.EndPoint);
        }

        [Fact]
        public void IdTypedAtLinesNextPoint_WritesThePointsCoordinates_AndLeavesTheLineAsItWas()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'ID");
            Assert.Equal(">>Specify point:", session.PromptText);
            Run(session, P(5, 6));

            Assert.Contains("X = 5.0000     Y = 6.0000     Z = 0.0000", session.History);
            AssertResumedLine(session);
            Assert.Equal(new GeoPoint2(0, 300), session.LastPoint);
        }

        [Fact]
        public void AVariablesNameTypedWithAnApostrophe_AsksItsValueAfterTheArrows_AndTheLineGoesOn()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'PDMODE");
            Assert.True(session.IsTransparent);
            Assert.Equal(">>Enter new value for PDMODE <0>:", session.PromptText);
            Run(session, "3");

            Assert.Equal(3, document.Database.Pdmode);
            AssertResumedLine(session);

            // The same, for a variable of the session and for one that can only be read.
            Run(session, "'CIRCLERAD");
            Assert.Equal(">>Enter new value for CIRCLERAD <0.0000>:", session.PromptText);
            Run(session, "4");
            Assert.Equal(4.0, session.Remembered["circle"]);
            AssertResumedLine(session);

            Run(session, "'EXTMAX");
            Assert.StartsWith(
                "EXTMAX = ",
                session.History.First(l => l.StartsWith("EXTMAX", StringComparison.Ordinal))
            );
            AssertResumedLine(session);
        }

        [Fact]
        public void AVariablesNameWithAnApostropheAtTheCommandPrompt_IsTheVariable()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "'PDMODE");

            Assert.True(session.IsRunning);
            Assert.Equal("Enter new value for PDMODE <0>:", session.PromptText);
            Run(session, "34");
            Assert.Equal(34, document.Database.Pdmode);
        }

        [Fact]
        public void LayerTyped_AsksTheOptionsOfMinusLayer_AndSetsTheCurrentLayer()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'LAYER");
            Assert.Equal("-LAYER", session.Current.Name);
            Assert.Contains("Current layer:  \"0\"", session.History);
            Assert.StartsWith(">>Enter an option [", session.PromptText, StringComparison.Ordinal);
            Run(session, "Set", "Steel", Enter);

            Assert.Equal("Steel", document.Database.Clayer);
            AssertResumedLine(session);
        }

        [Fact]
        public void ColorTyped_AsksTheDefaultObjectColor_AndSetsIt()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'COLOR");
            Assert.StartsWith(">>Enter default object color", session.PromptText, StringComparison.Ordinal);
            Run(session, "red");

            Assert.Equal(1, document.Database.Cecolor.ColorIndex);
            AssertResumedLine(session);
        }

        [Fact]
        public void LinetypeAndLweight_RunTheirTypedForms_WhileTheLineWaits()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'LINETYPE");
            Assert.Equal("-LINETYPE", session.Current.Name);
            Assert.StartsWith(">>Enter an option [", session.PromptText, StringComparison.Ordinal);
            Run(session, Enter);
            AssertResumedLine(session);

            Run(session, "'LWEIGHT");
            Assert.Equal("-LWEIGHT", session.Current.Name);
            Assert.StartsWith(">>Enter default lineweight", session.PromptText, StringComparison.Ordinal);
            Run(session, "0.35");
            Assert.Equal(DxfLineWeight.LineWeight035, document.Database.Celweight);
            AssertResumedLine(session);
        }

        [Fact]
        public void UnitsTyped_AsksTheChoicesOfMinusUnits_AndEscEndsTheLineToo()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'UNITS");
            Assert.Equal("-UNITS", session.Current.Name);
            Assert.Equal(">>Enter choice, 1 to 5 <2>:", session.PromptText);
            Run(session, "3");
            Assert.Equal(">>Enter number of digits to right of decimal point (0 to 8) <4>:", session.PromptText);
            Run(session, Esc);

            Assert.False(session.IsRunning);
            Assert.Equal(2, document.Database.Lunits);
        }

        [Fact]
        public void QuickcalcAndTextscr_RunWhileTheLineWaits_AndGiveItsPromptBack()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'QUICKCALC");
            AssertResumedLine(session);
            Run(session, "'TEXTSCR");
            AssertResumedLine(session);
        }

        [Fact]
        public void AnotherCommandThanTheseIsStillRefusedTransparently_AndMeasuregeomToo()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'MEASUREGEOM");

            Assert.Contains("** That command may not be invoked transparently **", session.History);
            Assert.False(session.IsTransparent);
            Assert.Equal("LINE", session.Current.Name);
        }

        [Fact]
        public void AFifthCommandInsideFour_IsRefusedAsAutoCadRefusesIt_TheTextGoingToThePrompt()
        {
            CadDocument document = Plan();
            CadCommandSession session = Line(document);

            Run(session, "'SETVAR", "'SETVAR", "'SETVAR");
            Assert.True(session.IsTransparent);
            Assert.StartsWith(">>Enter variable name", session.PromptText, StringComparison.Ordinal);

            // The fifth is refused, and its text goes to the prompt the fourth waits at, which finds no variable of that name.
            Run(session, "'SETVAR");

            Assert.Contains("Commands may not be nested more than 4 deep", session.History);
            Assert.Contains("Unknown variable name.  Type SETVAR ? for a list of variables.", session.History);

            // That one has ended, and the two before it end in turn; the line resumes.
            Assert.StartsWith(">>Enter variable name", session.PromptText, StringComparison.Ordinal);
            Run(session, Enter, Enter);
            AssertResumedLine(session);
        }
    }
}
