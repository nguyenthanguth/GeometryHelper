using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// ARC, PLINE, TEXT and MTEXT given the picks AutoCAD 2021 was given in its console, every number here read back from
    /// the entities it drew; the drawing of 110 objects they come from AUDITs clean in AutoCAD.
    /// </summary>
    public class DrawAuditTests
    {
        private static double[] Bulges(DxfPolyline polyline) =>
            Enumerable.Range(0, polyline.NumberOfVertices).Select(polyline.GetBulgeAt).ToArray();

        private static void Same(double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(
                    Math.Abs(expected[i] - actual[i]) < 1E-9,
                    "Bulge " + i + " is " + actual[i] + ", not " + expected[i] + "."
                );
            }
        }

        [Fact]
        public void PlineWidthAfterHalfwidth_OffersTheEndingWidthGiven()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021: half-widths 1 and 2, then Width offers <4.0000>, the ending width, as it does with no segment between.
            Run(session, "PLINE", P(0, 0), "H", "1", "2", "W");

            Assert.Equal("Specify starting width <4.0000>:", session.PromptText);
        }

        [Fact]
        public void PlineEndedInArcMode_KeepsTheLastArcsBulgeOnItsLastVertex_ButNotOnceLineIsChosen()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "PLINE", P(300, 200), "A", "A", "180", "R", "5", "90", Enter);
            Run(session, "PLINE", P(0, 600), P(10, 600), "A", P(20, 610), "L", Enter);
            Run(session, "PLINE", P(100, 600), P(110, 600), "A", P(120, 610), P(130, 600), "U", Enter);

            // AutoCAD 2021 wrote 1, 1; then 0, 0.4142, 0 and the same after Undo.
            DxfPolyline[] lines = All<DxfPolyline>(document.ModelSpace).ToArray();
            double quarter = Math.Tan(Math.PI / 8.0);
            Same(new[] { 1.0, 1.0 }, Bulges(lines[0]));
            Same(new[] { 0.0, quarter, 0.0 }, Bulges(lines[1]));
            Same(new[] { 0.0, quarter, 0.0 }, Bulges(lines[2]));
        }

        [Fact]
        public void PlineFirstArc_LeavesTheWayTheLineDrawnLastLeft()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", P(800, 200), P(810, 210), Enter, "PLINE", P(820, 200), "A", P(830, 200), Enter);

            // AutoCAD 2021: the arc leaves at 45 degrees, the line's way, and turns 90 clockwise to (830, 200).
            double quarter = Math.Tan(Math.PI / 8.0);
            Same(new[] { -quarter, -quarter }, Bulges(All<DxfPolyline>(document.ModelSpace).Single()));
        }

        [Fact]
        public void PlineArcByAngleAndRadius_OffersTheWayThePolylineDrawnLastLeft()
        {
            CadCommandSession session = Session(Blank());

            Run(session, "PLINE", P(0, 0), P(10, 0), "A", P(20, 10), Enter);
            Run(session, "PLINE", P(100, 0), "A", "A", "90", "R", "5");

            // The last arc ended going straight up.
            Assert.Equal(
                "Specify direction of chord for arc (hold Ctrl to switch direction) <90>:",
                session.PromptText
            );
        }

        [Fact]
        public void PlineFirstArc_AfterAnUndo_LeavesTheWayTheSegmentLeftLastLeft()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "PLINE", P(100, 600), P(110, 600), "A", P(120, 610), P(130, 600), "U", Enter);
            Run(session, "PLINE", P(200, 600), "A", P(210, 610), Enter);

            // AutoCAD 2021: the arc left after Undo ends going up, so the next arc turns 90 degrees clockwise, not 270.
            double quarter = Math.Tan(Math.PI / 8.0);
            Same(new[] { -quarter, -quarter }, Bulges(All<DxfPolyline>(document.ModelSpace).Last()));
        }

        [Fact]
        public void AnAngleOffered_IsWrittenToThePlacesOfAuprec()
        {
            CadDocument document = Blank();
            document.Database.Auprec = 0;
            CadCommandSession session = Session(document);

            Run(session, "LINE", P(0, 0), P(10, 7), Enter, "PLINE", P(20, 0), "A", "A", "90", "R", "5");

            // AutoCAD 2021 offers the line's 34.99 degrees as 35, then the arc's end, 79.99, at an AUPREC of 2.
            Assert.Equal(
                "Specify direction of chord for arc (hold Ctrl to switch direction) <35>:",
                session.PromptText
            );
            Run(session, Enter, Enter);
            document.Database.Auprec = 2;
            Run(session, "PLINE", P(40, 0), "A", "A", "90", "R", "5");
            Assert.Equal(
                "Specify direction of chord for arc (hold Ctrl to switch direction) <79.99>:",
                session.PromptText
            );
        }

        [Fact]
        public void ArcEnterAfterAPline_GoesOnFromItsEnd()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "LINE", P(800, 200), P(810, 210), Enter);
            Run(session, "PLINE", P(900, 200), P(910, 200), "A", P(920, 210), Enter, "ARC", Enter, P(930, 230));

            // AutoCAD 2021: centre (945, 210), radius 25, from 126.87 to 180 degrees.
            DxfArc arc = All<DxfArc>(document.ModelSpace).Single();
            Near(new GeoPoint3(945, 210, 0), arc.Center, 1E-9);
            Assert.Equal(25.0, arc.Radius, 9);
            Assert.Equal(2.214297435588, arc.StartAngle, 9);
            Assert.Equal(Math.PI, arc.EndAngle, 9);
        }

        [Fact]
        public void TextUnderAngdirOne_StoresItsRotationWithinAWholeTurn()
        {
            CadDocument document = Blank();
            document.Database.Angdir = true;
            CadCommandSession session = Session(document);

            Run(session, "TEXT", P(0, -300), "2.5", "30", "Abc", Enter);

            // AutoCAD 2021 wrote 5.759586531581, 330 degrees, for 30 clockwise.
            Assert.Equal(5.759586531581, All<DxfDBText>(document.ModelSpace).Single().Rotation, 9);
        }

        [Fact]
        public void MTextTurned_TakesItsWidthAndItsPointAlongItsTurn()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "MTEXT", P(250, 400), "R", "30", P(280, 380), "Abc");
            Run(session, "MTEXT", P(900, 400), "J", "BR", "R", "30", P(930, 380), "Abc");
            document.Database.Angdir = true;
            Run(session, "MTEXT", P(0, -400), "R", "30", P(30, -420), "Abc");

            // AutoCAD 2021: the box measured along the text's way, 15.9808 and 35.9808 wide; bottom right at the corner picked.
            DxfMText[] texts = All<DxfMText>(document.ModelSpace).ToArray();
            Assert.Equal(15.980762113533, texts[0].Width, 9);
            Near(new GeoPoint3(250, 400, 0), texts[0].Location, 1E-9);
            Assert.Equal(15.980762113533, texts[1].Width, 9);
            Near(new GeoPoint3(930, 380, 0), texts[1].Location, 1E-9);
            Assert.Equal(35.980762113533, texts[2].Width, 9);
            Near(new GeoPoint3(0, -400, 0), texts[2].Location, 1E-9);
        }

        [Fact]
        public void MTextLineSpacing_LastsFromOneMTextToTheNext_UntilHeightOrStyleIsChosen()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "MTEXT", P(0, 500), "L", "E", "1.5x", P(30, 480), "Abc");
            Run(session, "MTEXT", P(50, 500), P(80, 480), "Abc");
            Run(session, "MTEXT", P(100, 500), "R", "0", P(130, 480), "Abc");
            Run(session, "MTEXT", P(150, 500), "L");
            Assert.Equal("Enter line spacing type [At least/Exactly] <Exactly>:", session.PromptText);
            Run(session, "E");
            Assert.Equal("Enter line spacing factor or distance <1.5x>:", session.PromptText);
            Run(session, "2x", P(180, 480), "Abc");
            Run(session, "MTEXT", P(200, 500), "H", "3", P(230, 480), "Abc");
            Run(session, "MTEXT", P(250, 500), P(280, 480), "Abc");
            Run(session, "MTEXT", P(300, 500), "L", "E", "2x", P(330, 480), "Abc");
            Run(session, "MTEXT", P(350, 500), "S", "Standard", P(380, 480), "Abc");

            // AutoCAD 2021: Exactly 1.5 three times, then 2; Height gives At least 1 and the next keeps it; so does Style.
            DxfMText[] texts = All<DxfMText>(document.ModelSpace).ToArray();
            DxfLineSpacingStyle exactly = DxfLineSpacingStyle.Exactly;
            DxfLineSpacingStyle atLeast = DxfLineSpacingStyle.AtLeast;
            Assert.Equal(
                new[] { exactly, exactly, exactly, exactly, atLeast, atLeast, exactly, atLeast },
                texts.Select(t => t.LineSpacingStyle)
            );
            Assert.Equal(new[] { 1.5, 1.5, 1.5, 2.0, 1.0, 1.0, 2.0, 1.0 }, texts.Select(t => t.LineSpacingFactor));
        }
    }
}
