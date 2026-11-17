using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// TRACE (wide segments joined in a mitre, as the TRACE entities AutoCAD 2021 made for the same points) and SKETCH (the
    /// freehand stroke as lines, a polyline or a spline), with their prompts and options.
    /// </summary>
    public class TraceSketchTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static void Corners(DxfSolid solid, params double[] xy)
        {
            for (short i = 0; i < 4; i++)
            {
                Near(new GeoPoint3(xy[2 * i], xy[2 * i + 1], 0), solid.GetPointAt(i), 1E-9);
            }
        }

        [Fact]
        public void ATraceOfWidth3ThroughThreePoints_IsTwoSolidsJoinedInAMitre_AsAutoCadsAre()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "TRACE");

            Assert.StartsWith("Specify trace width <", session.PromptText, StringComparison.Ordinal);
            Run(session, "3");
            Assert.Equal("Specify start point:", session.PromptText);
            Run(session, "0,0", "100,0");
            Assert.Equal("Specify next point:", session.PromptText);
            Run(session, "100,50");
            Assert.Empty(All<DxfSolid>(document.ModelSpace).Skip(1));
            Run(session, Enter);

            var solids = All<DxfSolid>(document.ModelSpace);
            Assert.Equal(2, solids.Count);
            Corners(solids[0], 0, 1.5, 0, -1.5, 98.5, 1.5, 101.5, -1.5);
            Corners(solids[1], 98.5, 1.5, 101.5, -1.5, 98.5, 50, 101.5, 50);
            Assert.False(session.IsRunning);

            // One undo takes the whole trace back.
            Assert.True(document.Undo());
            Assert.Empty(All<DxfSolid>(document.ModelSpace));
        }

        [Fact]
        public void TheWidthOfATraceIsKept_AsTracewidOfTheHeader_AndOfferedNextTime()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "TRACE", "-2");

            Assert.Equal("Value must be positive.", session.History.Last());
            Run(session, "3", "0,0", "10,0", Enter);
            Assert.Equal(3.0, document.Database.Tracewid);
            Assert.Equal(
                "40=3",
                string.Join(
                    ";",
                    document.Database.GetHeader("$TRACEWID").Select(g => g.TypeCode + "=" + g.Value.Trim())
                )
            );

            Run(session, "TRACE");
            Assert.Equal("Specify trace width <3.0000>:", session.PromptText);
            Run(session, "0", "0,5", "10,5", Enter);
            DxfSolid flat = All<DxfSolid>(document.ModelSpace).Last();
            Corners(flat, 0, 5, 0, 5, 10, 5, 10, 5);
        }

        [Fact]
        public void ATraceOfTwoPoints_IsOneSolidSquareAtBothEnds_AndFillOffDrawsItsOutline()
        {
            CadDocument document = Blank();
            Start(document, "TRACE", "2", "0,0", "0,10", Enter);

            DxfSolid solid = Assert.Single(All<DxfSolid>(document.ModelSpace));
            Corners(solid, -1, 0, 1, 0, -1, 10, 1, 10);
            Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>());
            Run(Session(document), "FILL", "OFF");
            Assert.Empty(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayFill2>());
        }

        [Fact]
        public void ATraceThatTurnsBackOnItself_IsCutSquare_ThereBeingNoMitre()
        {
            CadDocument document = Blank();
            Start(document, "TRACE", "2", "0,0", "10,0", "0,0", Enter);

            var solids = All<DxfSolid>(document.ModelSpace);
            Assert.Equal(2, solids.Count);
            Corners(solids[0], 0, 1, 0, -1, 10, 1, 10, -1);
        }

        [Fact]
        public void SketchWritesItsTypeIncrementAndTolerance_AndAsksForTheStroke()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SKETCH");

            Assert.Contains(
                "Type = Lines  Increment = "
                    + (document.Database.Sketchinc).ToString(
                        "0.0000",
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                    + "  Tolerance = 0.5000",
                session.History
            );
            Assert.Equal("Specify sketch or [Type/Increment/toLerance]:", session.PromptText);
            Run(session, "X");
            Assert.Equal("2D point or option keyword required.", session.History.Last());
        }

        [Fact]
        public void SketchTypeIncrementAndToleranceAreAskedAndChecked_AsAutoCadChecksThem()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SKETCH", "T");

            Assert.Equal("Enter sketch type [Lines/Polyline/Spline] <Lines>:", session.PromptText);
            Run(session, "X");
            Assert.Equal("Invalid option keyword.", session.History.Last());
            Run(session, "P");
            Assert.Equal("Specify sketch or [Type/Increment/toLerance]:", session.PromptText);
            Assert.Equal("Enter new value for SKPOLY <1>:", Run(session, Esc, "SKPOLY").PromptText);
            Run(session, Esc, "SKETCH");
            Assert.Contains(
                "Type = Polyline",
                session.History.Last(h => h.StartsWith("Type =", StringComparison.Ordinal))
            );

            Run(session, "I");
            Assert.StartsWith("Specify sketch increment <", session.PromptText, StringComparison.Ordinal);
            Run(session, "-1");
            Assert.Equal("Value must be positive.", session.History.Last());
            Run(session, "0.25");
            Assert.Equal(0.25, document.Database.Sketchinc);
            Run(session, "L");
            Assert.Equal("Specify spline fit tolerance <0.5000>:", session.PromptText);
            Run(session, "2");
            Assert.Equal("Requires a value between 0 and 1.", session.History.Last());
            Run(session, "0.75", Esc);
            Run(session, "SKTOLERANCE");
            Assert.Equal("Enter new value for SKTOLERANCE <0.7500>:", session.PromptText);
        }

        [Fact]
        public void ASketchOfLines_IsALineForEachPieceTheCursorRecorded_AnIncrementApart()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SKETCH", "I", "1", "T", "L");
            Assert.Equal("Specify sketch or [Type/Increment/toLerance]:", session.PromptText);

            Run(session, P(0, 0));
            Assert.True(session.Prompt.Tracks);

            // A move of less than the increment records nothing; one of a whole increment does.
            session.Feed(P(0.4, 0));
            session.Feed(P(1.2, 0));
            session.Feed(P(2.1, 0.8));
            session.Feed(P(2.5, 0.8));
            session.Feed(P(4, 2));
            Run(session, Enter);
            Assert.Equal("Specify sketch or [Type/Increment/toLerance]:", session.PromptText);
            Run(session, Enter);

            var lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(3, lines.Count);
            Near(new GeoPoint3(0, 0, 0), lines[0].StartPoint);
            Near(new GeoPoint3(1.2, 0, 0), lines[0].EndPoint);
            Near(new GeoPoint3(2.1, 0.8, 0), lines[1].EndPoint);
            Near(new GeoPoint3(4, 2, 0), lines[2].EndPoint);
            Assert.False(session.IsRunning);
            Assert.True(document.Undo());
            Assert.Empty(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void ASketchOfPolyline_IsOnePolylineThroughThePoints_AndASplineOneSpline()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SKETCH", "I", "1", "T", "P", P(0, 0));
            session.Feed(P(2, 1));
            session.Feed(P(4, -1));
            session.Feed(P(6, 0));
            Run(session, Enter, Esc);

            DxfPolyline polyline = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.False(polyline.Closed);

            Run(session, "SKETCH", "T", "S", P(0, 10));
            session.Feed(P(2, 11));
            session.Feed(P(4, 9));
            session.Feed(P(6, 10));
            Run(session, Enter, Esc);
            DxfSpline spline = Assert.Single(All<DxfSpline>(document.ModelSpace));
            Assert.Equal(4, spline.NumFitPoints);
        }

        [Fact]
        public void ASketchEndedWithNothingRecorded_MakesNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SKETCH", "I", "5", P(0, 0));
            session.Feed(P(1, 1));
            Run(session, Enter, Enter);

            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.False(session.IsRunning);
        }
    }
}
