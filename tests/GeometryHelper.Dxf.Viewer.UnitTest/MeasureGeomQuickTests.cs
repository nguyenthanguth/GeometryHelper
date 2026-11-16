using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MEASUREGEOM's Quick option, in the presenter's simple form: the dimensions of what the cursor is over and of the geometry
    /// near it drawn as its preview, written on the command line at a click, and the areas inside closed boundaries. The plan
    /// is that of <see cref="EditingDrawings.PlanDatabase"/>: a line A1 from (0, 0) to (100, 0), a line A2 from (0, 0) to
    /// (0, 50), a circle A3 of radius 10 about (200, 0), an arc A4 of radius 20 about (300, 0) from 0 to 90 degrees and a line A8
    /// from (0, -50) to (100, -50).
    /// </summary>
    public class MeasureGeomQuickTests
    {
        private static string Degrees(double radians) =>
            (radians * 180.0 / System.Math.PI).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
            + "°";

        private static CadQuickMeasure.Reading Read(CadDocument document, double x, double y) =>
            CadQuickMeasure.Read(document, P(x, y), 0.6, Degrees);

        /// <summary>A drawing of a rectangle 100 by 60 from the origin, and inside it one 30 by 30 from (20, 10).</summary>
        private static CadDocument Rooms()
        {
            CadDocument document = Blank();
            foreach (double[] box in new[] { new[] { 0.0, 0, 100, 60 }, new[] { 20.0, 10, 50, 40 } })
            {
                var room = new DxfPolyline();
                room.AddVertexAt(0, P(box[0], box[1]), 0, 0, 0);
                room.AddVertexAt(1, P(box[2], box[1]), 0, 0, 0);
                room.AddVertexAt(2, P(box[2], box[3]), 0, 0, 0);
                room.AddVertexAt(3, P(box[0], box[3]), 0, 0, 0);
                room.Closed = true;
                document.Database.ModelSpace.AppendEntity(room);
            }

            return CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "rooms.dxf");
        }

        [Fact]
        public void MeasureGeomOffersQuickAndMode_AndQuickAsksToMoveTheCursor()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "MEASUREGEOM");
            Assert.Equal(
                "Enter an option [Distance/Radius/Angle/ARea/Quick/Mode/eXit] <Distance>:",
                session.PromptText
            );
            Run(session, "Quick");

            Assert.Equal("Move cursor or [Distance/Radius/Angle/ARea/Quick/Mode/eXit] <eXit>:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void OverALine_ItsLengthAndAngleAreRead_AndAWhatSharesACornerGivesTheAngleThere()
        {
            CadDocument document = Plan();

            CadQuickMeasure.Reading reading = Read(document, 50, 1);

            Assert.Equal("100.0000", reading.Dimensions[0].Text);
            Assert.Contains("Length = 100.0000", reading.Report);
            Assert.Contains("Angle in XY Plane = 0°", reading.Report);

            // A2 meets A1 at the origin at a right angle.
            Assert.Contains("90°", reading.Dimensions.Select(d => d.Text));
            Assert.Contains("Angle = 90°", reading.Report);
        }

        [Fact]
        public void TheDistanceToAParallelLineNearBy_IsReadOnTheCursorsSide()
        {
            CadDocument document = Plan();

            CadQuickMeasure.Reading below = Read(document, 50, -3);
            Assert.Contains("Distance = 50.0000", below.Report);
            CadQuickMeasure.Dimension across = below.Dimensions.Single(d => d.Text == "50.0000");
            Near(P(50, 0), across.Start);
            Near(P(50, -50), across.End);

            // On the other side of A1 the nearest parallel one is the polyline A5, along y = 100.
            CadQuickMeasure.Reading above = Read(document, 50, 3);
            Assert.Contains("Distance = 100.0000", above.Report);
        }

        [Fact]
        public void OverACircle_TheRadiusAndTheDiameterAreRead()
        {
            CadDocument document = Plan();

            CadQuickMeasure.Reading reading = Read(document, 200, 10);

            Assert.Equal("R 10.0000", reading.Dimensions.Single().Text);
            Assert.Contains("Radius = 10.0000", reading.Report);
            Assert.Contains("Diameter = 20.0000", reading.Report);
            Assert.Contains("Area = 314.1593", reading.Report);
        }

        [Fact]
        public void OverAnArc_TheRadiusAngleAndLengthAreRead()
        {
            CadDocument document = Plan();

            CadQuickMeasure.Reading reading = Read(document, 320, 0);

            Assert.Contains("Radius = 20.0000", reading.Report);
            Assert.Contains("Angle = 90°", reading.Report);
            Assert.Contains("Arc length = 31.4159", reading.Report);
        }

        [Fact]
        public void InsideTheSmallestClosedBoundary_TheWidthHeightAreaAndPerimeterAreRead()
        {
            CadDocument document = Rooms();

            CadQuickMeasure.Reading inner = Read(document, 30, 20);
            Assert.Equal(new[] { "Area = 900.0000", "Perimeter = 120.0000" }, inner.Report);
            Assert.Equal(new[] { "30.0000", "30.0000", "Area 900.0000" }, inner.Dimensions.Select(d => d.Text));

            // Between the two rectangles the outer one is the room: 100 by 60.
            CadQuickMeasure.Reading outer = Read(document, 70, 30);
            Assert.Equal(new[] { "Area = 6000.0000", "Perimeter = 320.0000" }, outer.Report);
            CadQuickMeasure.Dimension width = outer.Dimensions[0];
            Near(P(0, 30), width.Start);
            Near(P(100, 30), width.End);
            Assert.Equal("100.0000", width.Text);
            Assert.Equal("60.0000", outer.Dimensions[1].Text);

            // Outside every boundary there is nothing to read.
            Assert.Empty(Read(document, 500, 500).Report);
        }

        [Fact]
        public void InsideACircle_TheAreaIsRead()
        {
            CadDocument document = Plan();

            CadQuickMeasure.Reading reading = Read(document, 203, 3);

            Assert.Equal(new[] { "Area = 314.1593", "Perimeter = 62.8319" }, reading.Report);
        }

        [Fact]
        public void AClickWritesTheReadingOnTheCommandLine_AndTheCursorGoesOn()
        {
            CadDocument document = Rooms();
            CadCommandSession session = Session(document);
            session.SnapAperture = 6.0;

            Run(session, "MEASUREGEOM", "Quick", P(70, 30));

            Assert.Contains("Area = 6000.0000", session.History);
            Assert.Contains("Perimeter = 320.0000", session.History);
            Assert.True(session.IsRunning);
            Assert.StartsWith("Move cursor or", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void TheCursorDrawsTheDimensionsAsItsPreview()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            session.SnapAperture = 6.0;
            Run(session, "MEASUREGEOM", "Quick");

            CadPreview preview = session.Prompt.Preview;

            Assert.NotNull(preview);
            Assert.NotEmpty(preview.Items(P(50, -1)));
            Assert.Empty(preview.Items(P(5000, 5000)));
        }

        [Fact]
        public void AnotherOptionAtTheCursorPrompt_GoesOnWithThatMeasure()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "MEASUREGEOM", "Quick", "Distance");

            Assert.Equal("Specify first point:", session.PromptText);
            Run(session, P(0, 0), P(30, 40));
            Assert.Contains("Distance = 50.0000", string.Join("|", session.History));
        }

        [Fact]
        public void ModeYes_MakesMeasuregeomStartWithQuick_AndModeNoGivesItBack()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "MEASUREGEOM", "Mode");
            Assert.Equal("Always default to quick measure behavior? [Yes/No] <No>:", session.PromptText);
            Run(session, "Yes", Esc);
            Assert.False(session.IsRunning);

            Run(session, "MEASUREGEOM");
            Assert.StartsWith("Move cursor or", session.PromptText);
            Run(session, "Mode");
            Assert.Equal("Always default to quick measure behavior? [Yes/No] <Yes>:", session.PromptText);
            Run(session, "No", Esc);

            Run(session, "MEASUREGEOM");
            Assert.StartsWith("Enter an option", session.PromptText);
        }

        [Fact]
        public void TheExistingMeasures_AreAsTheyWere()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "MEASUREGEOM", "Radius", new[] { "A3" });

            Assert.Contains("Radius = 10.0000", session.History);
            Assert.Contains("Diameter = 20.0000", session.History);
        }
    }
}
