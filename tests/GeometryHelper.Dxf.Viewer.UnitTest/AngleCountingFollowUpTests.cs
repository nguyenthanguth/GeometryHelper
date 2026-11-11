using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The angles AutoCAD 2021 counts by ANGBASE and ANGDIR that the presenter still took or showed raw, each number as
    /// accoreconsole gave it with ANGBASE 90 and ANGDIR 1 (nought to the north, clockwise): the rows of a dimension and an
    /// ellipse in Properties, DIMEDIT's obliquing angle, MEASUREGEOM's angle and the &lt;30 angle override; and what the
    /// round found beside them: ATTSYNC by Select on a block with no attribute definitions, a row distance of -ARRAY typed
    /// in feet, and ALL in a drawing written by hand.
    /// </summary>
    public class AngleCountingFollowUpTests
    {
        private static readonly double Root3 = Math.Sqrt(3.0);

        /// <summary>A blank drawing counting angles from the north, clockwise, written to whole degrees.</summary>
        private static CadDocument North()
        {
            CadDocument document = Blank();
            Count(document, 90.0, true);
            return document;
        }

        private static void Count(CadDocument document, double angbase, bool clockwise)
        {
            document.Database.Angbase = angbase * Math.PI / 180.0;
            document.Database.Angdir = clockwise;
            document.Database.Auprec = 0;
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static string Row(CadDocument document, string handle, string label) =>
            CadProperties
                .Describe(document, null, new[] { handle })
                .Groups.SelectMany(g => g.Rows)
                .Single(r => r.Label == label)
                .Value;

        private static double Degrees(double radians)
        {
            double degrees = radians * 180.0 / Math.PI % 360.0;
            return degrees < 0.0 ? degrees + 360.0 : degrees;
        }

        /// <summary>A dimension 100 long from the origin along the X axis, its line 20 above, and its handle.</summary>
        private static string Dimension(CadDocument document)
        {
            Start(document, "DIMLINEAR", P(0, 0), P(100, 0), P(50, 20));
            return All<DxfDimension>(document.ModelSpace).Last().Handle;
        }

        [Fact]
        public void TheRowsOfAnEllipseArc_ShowItsAnglesCountedFromNorthClockwise_AndTakeThemSo()
        {
            // AutoCAD's LIST of the arc of 30 to 120 degrees: "Start Angle: 60", "End Angle: 330"; the same of one whose major
            // axis points north, the angles being from the major axis.
            CadDocument document = Blank();
            Start(document, "ELLIPSE", "A", "C", P(600, 0), P(700, 0), "50", "30", "120");
            Start(document, "ELLIPSE", "A", "C", P(600, 300), P(600, 400), "50", "30", "120");
            DxfEllipse[] arcs = All<DxfEllipse>(document.ModelSpace).ToArray();
            Assert.Equal(2, arcs.Length);
            Count(document, 90.0, true);

            foreach (DxfEllipse arc in arcs)
            {
                Assert.Equal(30.0, Degrees(arc.StartAngle), 9);
                Assert.Equal("60", Row(document, arc.Handle, "Start angle"));
                Assert.Equal("330", Row(document, arc.Handle, "End angle"));
            }

            Assert.True(CadPropertyEditor.Set(document, new[] { arcs[0].Handle }, "Start angle", "45").Succeeded);
            Assert.Equal(45.0, Degrees(Entity<DxfEllipse>(document, arcs[0].Handle).StartAngle), 9);
            Assert.Equal("45", Row(document, arcs[0].Handle, "Start angle"));
        }

        [Fact]
        public void TheTextRotationRowOfADimension_IsCountedFromNorthClockwise_AsDimeditRotateTakesIt()
        {
            // AutoCAD: DIMEDIT Rotate 30 keeps the words at 60 degrees from the X axis, and LIST writes "text angle: 30".
            CadDocument document = North();
            string dimension = Dimension(document);
            Start(document, "DIMEDIT", "R", "30", new[] { dimension }, Enter);
            Assert.Equal(60.0, Degrees(Entity<DxfDimension>(document, dimension).TextRotation), 9);

            Assert.Equal("30", Row(document, dimension, "Text rotation"));

            Assert.True(CadPropertyEditor.Set(document, new[] { dimension }, "Text rotation", "45").Succeeded);
            Assert.Equal(45.0, Degrees(Entity<DxfDimension>(document, dimension).TextRotation), 9);
            Assert.Equal("45", Row(document, dimension, "Text rotation"));
        }

        [Fact]
        public void DimeditObliqueThirty_SlantsTheExtensionLinesSixtyDegreesFromTheXAxis()
        {
            // AutoCAD kept 30 as 420 degrees and 120 as 330 on a dimension along the X axis; LIST writes "obliquing angle: 30".
            CadDocument document = North();
            string dimension = Dimension(document);

            Start(document, "DIMEDIT", "O", new[] { dimension }, Enter, "30");
            Assert.Equal(60.0, Degrees(Entity<DxfRotatedDimension>(document, dimension).Oblique), 9);

            Start(document, "DIMEDIT", "O", new[] { dimension }, Enter, "120");
            Assert.Equal(330.0, Degrees(Entity<DxfRotatedDimension>(document, dimension).Oblique), 9);
        }

        [Fact]
        public void DimeditOblique_OnADimensionTurnedThirtyDegrees_KeepsTheSlantFromItsLine()
        {
            // AutoCAD, counting from east: 60 on a dimension turned 30 degrees kept 30, and 100 kept 70.
            CadDocument document = Blank();
            Start(document, "DIMLINEAR", P(0, 0), P(100, 0), "R", "30", P(50, 40));
            string dimension = All<DxfDimension>(document.ModelSpace).Last().Handle;

            Start(document, "DIMEDIT", "O", new[] { dimension }, Enter, "60");
            Assert.Equal(30.0, Degrees(Entity<DxfRotatedDimension>(document, dimension).Oblique), 9);

            Count(document, 90.0, true);
            Start(document, "DIMEDIT", "O", new[] { dimension }, Enter, "30");
            Assert.Equal(30.0, Degrees(Entity<DxfRotatedDimension>(document, dimension).Oblique), 9);
        }

        [Theory]
        [InlineData(0.0, false, "30")]
        [InlineData(90.0, true, "60")]
        [InlineData(0.0, true, "330")]
        [InlineData(90.0, false, "300")]
        [InlineData(45.0, true, "15")]
        public void MeasuregeomAngleOfThirtyDegrees_IsWrittenAsTheDrawingCountsAnAngle(
            double angbase,
            bool clockwise,
            string written
        )
        {
            // AutoCAD writes the angle between the two endpoints as it writes a direction of that many degrees.
            CadDocument document = Blank();
            Count(document, angbase, clockwise);

            CadCommandSession session = Start(
                document,
                "MEASUREGEOM",
                "A",
                Enter,
                P(0, 0),
                P(100, 0),
                P(86.6025403784, 50),
                "X"
            );

            Assert.Contains("Angle = " + written + "°", session.History);
        }

        [Fact]
        public void AnAngleOverrideOfThirty_HoldsTheNextPointThirtyClockwiseOfNorth()
        {
            CadDocument document = North();

            CadCommandSession session = Start(document, "LINE", P(0, 200), "<30");
            Assert.Contains("Specify next point or [Undo]: <30 Angle Override: 30", session.History);
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Assert.Equal(60.0, Degrees(session.AngleOverride.Value), 9);
            Run(session, "100");
            Assert.Null(session.AngleOverride);
            Run(session, Enter);

            Near(
                new GeoPoint3(50, 200 + 50 * Root3, 0),
                Assert.Single(All<DxfLine>(document.ModelSpace)).EndPoint,
                1E-9
            );
        }

        [Fact]
        public void TwoAngleBracketsOfOverride_CountTheAngleFromEast_AndSayItAsTheDrawingCounts()
        {
            // AutoCAD with ANGBASE 90 and ANGDIR 1: "<<30 Angle Override: 60".
            CadDocument document = North();

            CadCommandSession session = Start(document, "LINE", P(0, 400), "<<30");
            Assert.Contains("Specify next point or [Undo]: <<30 Angle Override: 60", session.History);
            Run(session, "100", Enter);

            Near(new GeoPoint3(50 * Root3, 450, 0), Assert.Single(All<DxfLine>(document.ModelSpace)).EndPoint, 1E-9);
        }

        [Fact]
        public void AnAngleOverride_GivesWayToAPointTyped_AndRunsBackWhereTheCursorIsBehind()
        {
            CadDocument document = Blank();

            // "Coordinates and object snaps take precedence over an angle override": AutoCAD ended the line at 100,300.
            Start(document, "LINE", P(0, 300), "<30", "100,300", Enter);
            Near(new GeoPoint3(100, 300, 0), Assert.Single(All<DxfLine>(document.ModelSpace)).EndPoint, 1E-9);

            CadCommandSession session = Start(document, "LINE", P(0, 0), "<30");
            session.Cursor = P(-100, -20);
            Run(session, "100", Enter);
            Near(new GeoPoint3(-50 * Root3, -50, 0), All<DxfLine>(document.ModelSpace).Last().EndPoint, 1E-9);
        }

        [Fact]
        public void AnAngleOverrideWithNoBasePoint_IsNotAPoint()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "LINE", "<30");

            Assert.Null(session.AngleOverride);
            Assert.Equal("Specify first point:", session.PromptText);
            Assert.Contains("Invalid point.", session.History);
        }

        [Fact]
        public void TheSnapHoldsTheCursorOnTheOverride_OverOrtho_AndAnEndpointStillWins()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(200, 0), P(300, 0), Enter);
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            var snap = new ObjectSnap(Tight)
            {
                Ortho = true,
                AngleOverride = Math.PI / 6.0,
                Modes = CadSnapModes.Endpoint,
            };

            CadSnapResult held = snap.Snap(index, P(100, 0), 5.0, P(0, 0));
            Assert.Equal(CadSnapKind.None, held.Kind);
            Near(P(75, 25 * Root3), held.Point, 1E-9);

            CadSnapResult end = snap.Snap(index, P(201, 1), 5.0, P(0, 0));
            Assert.Equal(CadSnapKind.Endpoint, end.Kind);
            Near(P(200, 0), end.Point, 1E-9);
        }

        [Fact]
        public void AttsyncBySelect_OfABlockWithNoAttributeDefinitions_SaysSoAndAsksForAnotherBlock()
        {
            CadDocument document = Blank();
            Start(document, "CIRCLE", P(100, 100), "10");
            Start(
                document,
                "-B",
                "PLAIN",
                P(0, 0),
                All<DxfEntity>(document.ModelSpace).Select(e => e.Handle).ToArray(),
                Enter
            );
            Start(document, "-I", "PLAIN", P(50, 50), Enter, Enter, Enter);

            CadCommandSession session = Start(document, "ATTSYNC", Enter, P(160, 150));

            Assert.Contains("This block has no attribute definitions.", session.History);
            Assert.DoesNotContain(session.History, l => l.StartsWith("ATTSYNC block", StringComparison.Ordinal));
            Assert.Equal("Select a block:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ARowDistanceOfArrayTypedInFeet_IsReadInTheDrawingsUnits()
        {
            CadDocument document = Blank();
            document.Database.Lunits = 4;
            Start(document, "CIRCLE", P(0, 0), "5");
            string circle = All<DxfCircle>(document.ModelSpace).Single().Handle;

            Start(document, "-ARRAY", new[] { circle }, Enter, "R", "3", "1", "2'");

            Assert.Equal(
                new[] { 0.0, 24.0, 48.0 },
                All<DxfCircle>(document.ModelSpace).Select(c => c.Center.Y).OrderBy(y => y).ToArray()
            );
        }

        [Fact]
        public void AllInADrawingWrittenByHand_TakesEveryObject()
        {
            // Three walls and a column written as R12: no header, no tables, no handles.
            const string room =
                "  0\nSECTION\n  2\nENTITIES\n"
                + "  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 30\n0\n 11\n4000\n 21\n0\n 31\n0\n"
                + "  0\nLINE\n  8\nWALLS\n 10\n4000\n 20\n0\n 30\n0\n 11\n4000\n 21\n3000\n 31\n0\n"
                + "  0\nLINE\n  8\n0\n 10\n4000\n 20\n3000\n 30\n0\n 11\n0\n 21\n3000\n 31\n0\n"
                + "  0\nCIRCLE\n  8\n0\n 10\n2000\n 20\n1500\n 30\n0\n 40\n300\n"
                + "  0\nENDSEC\n  0\nEOF\n";
            CadDocument document = CadDocument.From(DxfDatabase.Parse(room), "room.dxf");

            CadCommandSession session = Start(document, "ERASE", "ALL");
            Assert.Contains("4 found", session.History);
            Run(session, Enter);

            Assert.Empty(All<DxfEntity>(document.ModelSpace));
        }
    }
}
