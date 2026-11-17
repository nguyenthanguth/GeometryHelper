using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// LIMITS with LIMCHECK, TIME with the variables TDCREATE, TDUPDATE, TDINDWG and TDUSRTIMER, STATUS, FILL, QTEXT and
    /// MULTIPLE, with the prompts, the words and the numbers AutoCAD 2021's console gave for the same.
    /// </summary>
    public class LimitsTimeStatusTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        [Fact]
        public void LimitsSetTwoCornersInOneUndoStep_AskedAsAutoCadAsksThem()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LIMITS");

            Assert.Contains("Reset Model space limits:", session.History);
            Assert.Equal("Specify lower left corner or [ON/OFF] <0.0000,0.0000>:", session.PromptText);
            Run(session, "10,20");
            Assert.StartsWith("Specify upper right corner <", session.PromptText, StringComparison.Ordinal);
            Run(session, "210,120");

            Assert.False(session.IsRunning);
            Assert.Equal(new GeoPoint2(10, 20), document.Database.Limmin);
            Assert.Equal(new GeoPoint2(210, 120), document.Database.Limmax);
            Assert.True(document.Undo());
            Assert.Equal(new GeoPoint2(0, 0), document.Database.Limmin);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void LimitsWithAnUpperRightNotAboveAndRightOfTheLowerLeft_AreInvalid_AndAskedAgain()
        {
            CadDocument document = Blank();
            GeoPoint2 was = document.Database.Limmax;
            CadCommandSession session = Start(document, "LIMITS", "10,10", "5,5");

            Assert.Equal(
                "Invalid limits.",
                session.History.Last(h => h.StartsWith("Invalid", StringComparison.Ordinal))
            );
            Assert.StartsWith("Specify lower left corner or [ON/OFF] <", session.PromptText, StringComparison.Ordinal);
            Assert.Equal(was, document.Database.Limmax);
            Assert.False(document.CanUndo);

            // Enter takes both corners as they are.
            Run(session, Enter, Enter);
            Assert.False(session.IsRunning);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void LimitsOnAndOff_SetLimcheck_AndLimcheckRefusesAPointOutsideTheLimits()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LIMITS", "0,0", "200,100", "LIMITS", "ON");
            Assert.True(document.Database.Limcheck);

            Run(session, "LINE", "0,0", "300,0");
            Assert.Equal("**Outside limits", session.History.Last());
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, "100,50", Enter);
            DxfLine line = Assert.Single(All<DxfLine>(document.ModelSpace));
            Near(new GeoPoint3(100, 50, 0), line.EndPoint);

            // A point at the limit itself is inside, and a point that only measures is never refused.
            Run(session, "POINT", "200,100");
            Assert.Single(All<DxfDBPoint>(document.ModelSpace));
            Run(session, "DIST", "0,0", "500,0");
            Assert.NotEqual("**Outside limits", session.History.Last());

            Run(session, "LIMITS", "OFF");
            Assert.False(document.Database.Limcheck);
            Run(session, "POINT", "900,900");
            Assert.Equal(2, All<DxfDBPoint>(document.ModelSpace).Count);
        }

        [Fact]
        public void ATypedPointOutsideTheLimitsIsRefusedAsAClickIs()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LIMITS", "0,0", "50,50", "LIMCHECK", "1", "CIRCLE");

            Run(session, "100,0");
            Assert.Equal("**Outside limits", session.History.Last());
            Assert.StartsWith("Specify center point for circle", session.PromptText, StringComparison.Ordinal);
            Assert.False(session.Feed(new GeoPoint2(-5, 5)));
            Assert.Equal("**Outside limits", session.History.Last());
            Assert.True(session.Feed(new GeoPoint2(10, 10)));
            Assert.StartsWith("Specify radius of circle", session.PromptText, StringComparison.Ordinal);
        }

        [Fact]
        public void TimeShowsTheTimesOfTheDrawing_AsAutoCadWritesThem()
        {
            CadDocument document = Blank();
            var now = new DateTime(2026, 11, 17, 8, 34, 0, DateTimeKind.Unspecified);
            document.Times.Clock = () => now;
            document.Times.OpenedAt = now;
            CadCommandSession session = Start(document, "TIME");

            Assert.Contains("Current time:             Tuesday, November 17, 2026  8:34:00:000 AM", session.History);
            Assert.Contains("  Created:                Tuesday, November 17, 2026  8:34:00:000 AM", session.History);
            Assert.Contains("  Total editing time:     0 days 00:00:00:000", session.History);
            Assert.Contains("  Elapsed timer (on):     0 days 00:00:00:000", session.History);
            Assert.Contains("  Next automatic save in: <no modifications yet>", session.History);
            Assert.Equal("Enter option [Display/ON/OFF/Reset]:", session.PromptText);

            now = now.AddMinutes(95).AddSeconds(3).AddMilliseconds(250);
            Run(session, "D");
            Assert.Contains("Current time:             Tuesday, November 17, 2026  10:09:03:250 AM", session.History);
            Assert.Contains("  Total editing time:     0 days 01:35:03:250", session.History);
            Assert.Contains("  Elapsed timer (on):     0 days 01:35:03:250", session.History);
        }

        [Fact]
        public void TimeOnOffAndReset_StartStopAndClearTheUsersTimer_AndTdusrtimerReadsIt()
        {
            CadDocument document = Blank();
            var now = new DateTime(2026, 11, 17, 8, 0, 0);
            document.Times.Clock = () => now;
            document.Times.OpenedAt = now;
            CadCommandSession session = Start(document, "TIME");

            now = now.AddHours(1);
            Run(session, "OFF");
            Assert.Equal("Timer off.", session.History.Last());
            now = now.AddHours(5);
            Run(session, "D");
            Assert.Contains("  Elapsed timer (off):    0 days 01:00:00:000", session.History);
            Assert.Contains("  Total editing time:     0 days 06:00:00:000", session.History);
            Run(session, "ON");
            Assert.Equal("Timer on.", session.History.Last());
            now = now.AddMinutes(30);
            Run(session, "D");
            Assert.Contains("  Elapsed timer (on):     0 days 01:30:00:000", session.History);
            Run(session, "R");
            Assert.Equal("Timer reset to zero.", session.History.Last());
            now = now.AddMinutes(15);
            Run(session, "D", Esc);

            Assert.Equal("0.01041667", ((string)Value(session, "TDUSRTIMER")));
            Assert.Equal("0.28125000", ((string)Value(session, "TDINDWG")));
        }

        [Fact]
        public void TheDatesOfTheDrawing_AreJulianDatesToEightPlaces_AndOnlyRead()
        {
            var database = new DxfDatabase("AC1027");
            CadDocument document = CadDocument.From(database, "dates.dxf");
            var created = new DateTime(2026, 11, 17, 8, 34, 0);
            document.Times.OpenedAt = created;
            CadCommandSession session = Start(document, "TDCREATE");

            Assert.Equal("TDCREATE = 2461361.85694444 (read only)", session.History.Last());
            Assert.False(session.IsRunning);
            Run(session, "TDUPDATE");
            Assert.Equal("TDUPDATE = 2461361.85694444 (read only)", session.History.Last());
            Run(session, "SETVAR", "TDINDWG");
            Assert.EndsWith("(read only)", session.History.Last(), StringComparison.Ordinal);
            Assert.NotNull(session.Registry.FindVariable("TDUUPDATE"));
            Assert.NotNull(session.Registry.FindVariable("TDUCREATE"));
        }

        [Fact]
        public void TimeSaysWhenTheNextAutosaveIs_OnceTheDrawingIsChanged()
        {
            CadDocument document = Blank();
            var now = new DateTime(2026, 11, 17, 8, 0, 0);
            document.Times.Clock = () => now;
            document.Times.OpenedAt = now;
            Run(Session(document), "POINT", "10,10");
            now = now.AddMinutes(4).AddSeconds(30);

            CadCommandSession session = Start(document, "TIME");
            Assert.Contains("  Next automatic save in: 0 days 00:05:30:000", session.History);
        }

        [Fact]
        public void StatusWritesTheReportOfAutoCad_ForAnEmptyDrawing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "STATUS");
            string[] lines = session.History.ToArray();

            Assert.Contains("0 objects in blank.dxf", lines);
            Assert.Contains("Model space uses       *Nothing*", lines);
            Assert.Contains("Current space:        Model space", lines);
            Assert.Contains("Current layout:       Model", lines);
            Assert.Contains("Current layer:        \"0\"", lines);
            Assert.Contains("Current color:        BYLAYER -- 7 (white)", lines);
            Assert.Contains("Current linetype:     BYLAYER -- \"Continuous\"", lines);
            Assert.Contains("Current lineweight:   BYLAYER", lines);
            Assert.Contains("Current elevation:    0.0000  thickness:    0.0000", lines);
            Assert.Contains("Fill on  Grid off  Ortho off  Qtext off  Snap off  Tablet off", lines);
            Assert.Contains("Insertion base is      X:    0.0000   Y:    0.0000   Z:    0.0000", lines);
            Assert.Contains("Snap resolution is     X:   10.0000   Y:   10.0000", lines);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void StatusGivesWhatTheSpaceUses_AndMarksTheCornerOutsideTheLimitsOver()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(
                document,
                "LIMITS",
                "0,0",
                "200,100",
                "LINE",
                "-30,5",
                "100,250",
                Enter,
                "STATUS"
            );
            string[] lines = session.History.ToArray();

            Assert.Contains("Model space limits are X:    0.0000   Y:    0.0000  (Off)", lines);
            Assert.Contains("                       X:  200.0000   Y:  100.0000", lines);
            Assert.Contains("1 objects in blank.dxf", lines);
            Assert.Contains("Model space uses       X:  -30.0000   Y:    5.0000 **Over", lines);
            Assert.Contains("                       X:  100.0000   Y:  250.0000 **Over", lines);

            Run(session, "LIMITS", "ON", "STATUS");
            Assert.Contains("Model space limits are X:    0.0000   Y:    0.0000", session.History.ToArray());
        }

        [Fact]
        public void FillOffDrawsTheFilledAreasByTheirOutlines_AndUndoBringsThemBack()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(
                new DxfSolid(
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(10, 0, 0),
                    new GeoPoint3(0, 10, 0),
                    new GeoPoint3(10, 10, 0)
                )
            );
            CadDocument document = CadDocument.From(database, "fill.dxf");
            Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>());

            CadCommandSession session = Start(document, "FILL");
            Assert.Equal("Enter mode [ON/OFF] <ON>:", session.PromptText);
            Run(session, "OFF");

            Assert.False(document.Database.Fillmode);
            Assert.Empty(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>());
            Run(session, "FILL");
            Assert.Equal("Enter mode [ON/OFF] <OFF>:", session.PromptText);
            Run(session, "X");
            Assert.Equal("Invalid option keyword.", session.History.Last());
            Run(session, Esc);

            Assert.True(document.Undo());
            Assert.True(document.Database.Fillmode);
            Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>());
        }

        [Fact]
        public void QtextOnShowsEachTextAsItsBox_AndOffBringsTheLettersBack()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "QTEXT");
            Assert.Equal("Enter mode [ON/OFF] <OFF>:", session.PromptText);
            DisplayText2 text = Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>());
            Assert.False(text.IsQuick);

            Run(session, "ON");
            Assert.True(document.Database.Qtextmode);
            text = Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>());
            Assert.True(text.IsQuick);
            Assert.Equal("NOTE", text.Text);

            Run(session, "QTEXT");
            Assert.Equal("Enter mode [ON/OFF] <ON>:", session.PromptText);
            Run(session, "OFF");
            text = Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>());
            Assert.False(text.IsQuick);
            Assert.True(document.Undo());
            Assert.True(document.Database.Qtextmode);
        }

        [Fact]
        public void MultipleStartsTheCommandAgainEachTimeItEnds_UntilEsc()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MULTIPLE");
            Assert.Equal("Enter command name to repeat:", session.PromptText);

            Run(session, "POINT", "1,1", "2,2", "3,3");
            Assert.Equal(3, All<DxfDBPoint>(document.ModelSpace).Count);
            Assert.True(session.IsRunning);
            Assert.Equal("POINT", session.Current.Name);

            Run(session, Esc);
            Assert.False(session.IsRunning);
            Run(session, "5,5");
            Assert.Equal(3, All<DxfDBPoint>(document.ModelSpace).Count);
        }

        [Fact]
        public void MultipleWithAnUnknownCommand_SaysSoAndEnds_AndOneThatAsksNothingIsNotRepeatedForever()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MULTIPLE", "FOO");
            Assert.Equal("Unknown command \"FOO\".  Press F1 for help.", session.History.Last());
            Assert.False(session.IsRunning);

            Run(session, "MULTIPLE", "REGEN");
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void MultipleKeepsLineGoing_ADrawnLineEndedByEnterStartsTheNext()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "MULTIPLE", "LINE", "1,1", "5,5", Enter, "3,3", "7,7", Enter);

            Assert.Equal(2, All<DxfLine>(document.ModelSpace).Count);
            Assert.Equal("Specify first point:", session.PromptText);
            Run(session, "9,9", "20,20", Esc);
            Assert.False(session.IsRunning);
            Assert.Equal(3, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void TheSketchVariables_KeepAutoCadsRanges()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SKPOLY", "5");
            Assert.Equal("Requires an integer between 0 and 2.", session.History.Last());
            Run(session, "2", "SKPOLY");
            Assert.Equal("Enter new value for SKPOLY <2>:", session.PromptText);
            Run(session, Esc, "SKTOLERANCE", "2");
            Assert.Equal("Cannot set SKTOLERANCE to that value.", session.History.Last());
            Run(session, "SKTOLERANCE", "0.25", "SKTOLERANCE");
            Assert.Equal("Enter new value for SKTOLERANCE <0.2500>:", session.PromptText);

            double was = document.Database.Sketchinc;
            Run(session, Esc, "SKETCHINC", "0.5");
            Assert.Equal(0.5, document.Database.Sketchinc);
            Assert.True(document.Undo());
            Assert.Equal(was, document.Database.Sketchinc);
        }

        private static object Value(CadCommandSession session, string name)
        {
            CadSystemVariable variable = session.Registry.FindVariable(name);
            return variable.GetValue(session);
        }
    }
}
